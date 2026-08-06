using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Models.CrossGroup;
using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Repositories;
using FreshColdChain.Repositories;
using DBFreshColdChain.Models;
using Newtonsoft.Json;
using System.Data;
using System.Runtime.ConstrainedExecution;

namespace DBFreshColdChain.Services
{
    public class RefundService : IRefundService
    {
        private readonly IUnitOfWork _uow;
        private readonly IPromoterRepository _ipromoterRepository ;
        private readonly IRefundRepository _irefundRepository;
        private readonly IPromoterService _ipromoterManager;
        private readonly ITableLogService _logManager;
        private readonly Mock_IGroupA _mockGroupAInterface;
        private readonly Mock_IGroupB _mockGroupBInterface;
        public RefundService(IUnitOfWork uow,IPromoterRepository ipromoterRepository, 
            IPromoterService ipromoterManager, IRefundRepository irefundRepository,
                                ITableLogService log_Auditrails, 
                                Mock_IGroupA mockGroupAInterface, 
                                Mock_IGroupB mockGroupBInterface)
        {
            _uow = uow;
            _ipromoterRepository = ipromoterRepository;
            _irefundRepository = irefundRepository;
            _ipromoterManager = ipromoterManager;
            _logManager = log_Auditrails;
            _mockGroupAInterface = mockGroupAInterface;
            _mockGroupBInterface = mockGroupBInterface;
        }

        public async Task<Result> Refund(GroupC_RefundRequest refundRequest) //整体处理退款函数
        {
            // 开启事务
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (refundRequest.OrderId == null || refundRequest.OrderId == string.Empty)     //不完整的输入信息
                {
                    await _uow.RollbackAsync();
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "不完整的退款请求信息";
                    return _result;
                }
                //获取退款订单相关信息
                var _orderInfo = _mockGroupBInterface.FindOrderInfo(refundRequest.OrderId);
                //退款自动审核:过了可退款期14天，拒绝退款
                if (DateTime.Now - _orderInfo.CommSettlementDate >= TimeSpan.FromDays(14))
                {
                    throw new Exception("该订单已过可退期14天，无法退款！");
                }
                decimal _refundAmount = 0;
                string? _detailId = null;
                string? _supplierId = null;
                int _refundQty = 0;
                //  区分是否是整单退款:
                // 如果 ProductID 为空，表示整单退款
                if (refundRequest.ProductID == null)
                {
                    // 整单退时，退款金额 = 订单实付金额
                    _refundAmount = _orderInfo.FinalAmount;
                    // 整单退时，按订单所有明细回滚，但不用记 detailId和supplierId
                    _detailId = null;
                    _supplierId = null;
                    _refundQty = 0; // 整单不计数量
                }
                else
                {
                    var _detailInfo = _mockGroupBInterface.FindDetailInfo(refundRequest.ProductID);     //获取退款商品明细相关信息
                    var _supplierInfo = _mockGroupAInterface.FindProductInfo(refundRequest.ProductID);  //获取退款商品供应商相关信息
                    _detailId = _detailInfo.DetailId;
                    _supplierId = null;
                    _refundQty = refundRequest.RefundQty;
                    if (refundRequest.RefundQty > _detailInfo.Quantity) //退款数额大于下单数额
                    {
                        throw new Exception("退期数量大于购买数量，请重新输入退款数量！");
                    }
                    //计算退款金额
                    _refundAmount = _refundQty * _detailInfo.UnitPrice;
                }

                //添加退款记录
                await RefundRecord(_orderInfo.OrderId, _detailId, _supplierId, _refundQty, _refundAmount, "Customer", refundRequest.Remark, _uow.Transaction);
                //更新订单状态为已整单退款/部分退款
                if (refundRequest.ProductID == null)    //全部退款
                    _mockGroupBInterface.ChangeStatusToAllRefund(refundRequest.OrderId);
                else                                    //部分退款
                {
                    _mockGroupBInterface.ChangeStatusToPartRefund(refundRequest.OrderId);
                }
                // 检查订单状态：若用户未签收，则不回滚佣金,但需告知供应商不再需要发货对应商品
                if (_orderInfo.Status == "已签收")
                {
                    // 计算应扣减的佣金总额 = (退款金额 / 订单实付金额) * (基础佣金 + 奖励佣金)
                    var ratio = _refundAmount / _orderInfo.FinalAmount;
                    var rollbackCommission = (_orderInfo.CommBaseAmount + _orderInfo.CommBonusAmount) * ratio;
                    //如果团长和消费者解绑了，那么团长佣金不会扣（平台在解绑触发时自动清空团长的待结算余额，防止团长反复解绑/绑定刷回退佣金差）
                    var _promoterInfo = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(refundRequest.ProductID,_uow.Transaction);
                    if (_promoterInfo != null)    //团长存在,则回滚
                    {
                        await RefundRollbackMoney(_orderInfo.PromoterId, rollbackCommission, _orderInfo.GoodsAmount, _uow.Transaction);
                    }
                }
                else //让A组告诉对应供应商不再需要发货对应商品
                {
                    //具体接口函数需要B组提供,因为涉及到AB组之间定的物流发货逻辑
                }
                //调用B组的积分回滚函数
                var ratioPoints = _refundAmount / _orderInfo.FinalAmount;
                _mockGroupBInterface.RollbackPoints(_orderInfo.CustomerId, ratioPoints);
                //支付流水是否需要回滚？默认不回滚支付流水好了
                await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                if(_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
            
        }

        private async Task<Result> RefundRollbackMoney(string? promoterID, decimal totalCommission, decimal goodsAmount,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)  //退款回滚与退款信息记录
        {

            var _result = new Result();
            bool ownTransaction = false;
            try
            {
                if(transaction == null)
                {
                    await _uow.BeginAsync();
                    ownTransaction = true;
                    transaction = _uow.Transaction;

                }
                if (promoterID == null || promoterID == string.Empty)
                {
                    throw new Exception("团长信息为空");
                }
                var _promoterInfo = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(promoterID, transaction);
                if (_promoterInfo == null)    //无效的团长编号
                {
                    throw new Exception("该团长信息不存在");

                }
                //回滚佣金与销售额
                //记录旧的累计销售额，待结算余额
                var _oldPromoterTotalSales = _promoterInfo.TotalSales;
                var _oldPromoterPendingBalance = _promoterInfo.PendingBalance;
                //回滚销售额与佣金
                await _ipromoterRepository.GroupC_UpdatePromoterTotalSalesAsync(promoterID, -goodsAmount, transaction);
                await _ipromoterRepository.GroupC_UpdatePromoterPendingBalanceAsync(promoterID, -totalCommission, transaction);
                //记录新的佣金与销售额
                var _newPromoterTotalSales = _oldPromoterTotalSales - goodsAmount;
                var _newPromoterPendingBalance = _oldPromoterPendingBalance - totalCommission;
                //产生日志信息（两条）
                //Pendingbalance的扣减
                var _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
                await _logManager.WriteTableChangeLog(_tableLog);
                //TotalSales的扣减
                _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { TotalSales = _oldPromoterTotalSales });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { TotalSales = _newPromoterTotalSales });
                await _logManager.WriteTableChangeLog(_tableLog);
                if (ownTransaction)
                    await _uow.RollbackAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                if (ownTransaction && _uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
        }

        public async Task<Result> RefundRecord(string? orderID, string? detailID, string? supplierID, int refundQty, decimal refundAmount, string? liabilityType,string? remark,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default
            )  //添加退款信息函数
        {
            var _result = new Result();
            bool ownTransaction = false;
            try
            {
                if (transaction == null)
                {
                    await _uow.BeginAsync();
                    ownTransaction = true;
                    transaction = _uow.Transaction;

                }

                FinRefund _finRefund = new FinRefund();
                _finRefund.RefundId = "REF_" + Guid.NewGuid().ToString("N"); //自动生成退款编号
                _finRefund.OrderId = orderID;
                _finRefund.DetailId = detailID;
                _finRefund.SupplierId = supplierID;
                _finRefund.RefundAmount = refundAmount;
                _finRefund.RefundQty = refundQty;
                _finRefund.LiabilityType = liabilityType;
                _finRefund.ApplyTime = DateTime.Now;
                _finRefund.Remark = remark;
                await _irefundRepository.InsertRefundAsync(_finRefund, transaction);

               //产生日志信息
               var _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Create";
                _tableLog.TableName = "FIN_REFUNDS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = string.Empty;
                _tableLog.NewValue = JsonConvert.SerializeObject(new
                {
                    RefundId = _finRefund.RefundId,
                    OrderId = _finRefund.OrderId,
                    DetailId = _finRefund.DetailId,
                    SupplierId = _finRefund.SupplierId,
                    RefundQty = _finRefund.RefundQty,
                    RefundAmount = _finRefund.RefundAmount,
                    LiabilityType = _finRefund.LiabilityType,
                    ApplyTime = _finRefund.ApplyTime,
                    Remark = _finRefund.Remark
                });
                await _logManager.WriteTableChangeLog(_tableLog);
                if (ownTransaction)
                    await _uow.RollbackAsync();
                _result.IsSuccess = true;
                return _result;

            }
            catch (Exception ex)
            {
                if (ownTransaction && _uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
        }
    }
}
