using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using Newtonsoft.Json;
using System.Data;
using System.Runtime.ConstrainedExecution;
using System.Transactions;

namespace FreshColdChain.Services
{
    public class RefundService : IRefundService
    {
		private readonly IUnitOfWork _uow;
		private readonly IPromoterRepository _ipromoterRepository;
		private readonly IRefundRepository _irefundRepository;
		private readonly IPromoterService _ipromoterManager;
		private readonly ITableLogService _logManager;
		private readonly ICommissionRepository _icommissionRepository;
		private readonly IOrderService _orderService;   //B组真实订单接口（替代原 Mock_IGroupB / Mock_IGroupA）
		public RefundService(IUnitOfWork uow, IPromoterRepository ipromoterRepository,
			IPromoterService ipromoterManager, IRefundRepository irefundRepository,
								ITableLogService log_Auditrails,
		ICommissionRepository icommissionRepository,
								IOrderService orderService)
		{
			_uow = uow;
			_ipromoterRepository = ipromoterRepository;
			_irefundRepository = irefundRepository;
			_ipromoterManager = ipromoterManager;
			_logManager = log_Auditrails;
		_icommissionRepository = icommissionRepository;
			_orderService = orderService;
		}

		//退款上下文：一次退款所需的全部订单侧信息与计算结果
        private sealed class RefundContext
        {
            public BizOrder Order { get; set; } = new();
            public IReadOnlyList<BizOrderDetail> Details { get; set; } = [];
            public OrderStatus OrderStatus { get; set; }
            public CommissionRecord? Record { get; set; }
            public decimal RefundAmount { get; set; }
            public string? DetailId { get; set; }
            public string? SupplierId { get; set; }
            public int RefundQty { get; set; }
            public bool IsFullRefund { get; set; }
            public decimal Ratio { get; set; }
        }

        public async Task<Result> Refund(GroupC_RefundRequest refundRequest) //直接退款（免审核，兼容原接口）
        {
            // 开启事务（仅管理C组自己的表：FIN_REFUND / FIN_PROCOMRECORDS / CRM_PROMOTERS）
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (string.IsNullOrEmpty(refundRequest.OrderId))     //不完整的输入信息
                {
                    await _uow.RollbackAsync();
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "不完整的退款请求信息";
                    return _result;
                }
                var ctx = await BuildContextFromRequestAsync(refundRequest, _uow.Transaction);
                //添加退款记录（免审流程直接置为已通过）
                await RefundRecord(ctx.Order.OrderId, ctx.DetailId, ctx.SupplierId, ctx.RefundQty, ctx.RefundAmount,
                    refundRequest.LiabilityType, refundRequest.Remark, "Approved", _uow.Transaction);
                await ExecuteRefundCoreAsync(ctx, _uow.Transaction);
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

        public async Task<Result> ApplyRefund(GroupC_RefundRequest refundRequest) //消费者申请退款：仅创建待审核申请单，不动资金
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (string.IsNullOrEmpty(refundRequest.OrderId))
                {
                    await _uow.RollbackAsync();
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "不完整的退款请求信息";
                    return _result;
                }
                //校验订单可退状态并计算退款金额（与直接退款同一套规则）
                var ctx = await BuildContextFromRequestAsync(refundRequest, _uow.Transaction);
                //同一订单存在待审核申请时禁止重复申请
                if (await _irefundRepository.HasPendingApplicationAsync(ctx.Order.OrderId, _uow.Transaction))
                {
                    throw new Exception("该订单已有待审核的退款申请，请勿重复提交！");
                }
                //创建待审核申请单（不写佣金/积分/订单状态，等待管理员审核）
                await RefundRecord(ctx.Order.OrderId, ctx.DetailId, ctx.SupplierId, ctx.RefundQty, ctx.RefundAmount,
                    refundRequest.LiabilityType, refundRequest.Remark, "Pending", _uow.Transaction);
                await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
        }

        public async Task<Result> AuditRefund(string refundId, bool approved, string auditorId, string? auditRemark = null) //管理员审核退款申请
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (string.IsNullOrEmpty(refundId))
                {
                    throw new Exception("退款申请编号为空");
                }
                var application = await _irefundRepository.GetByIdAsync(refundId, _uow.Transaction);
                if (application == null)
                {
                    throw new Exception("退款申请不存在");
                }
                if (application.Status != "Pending")    //幂等：已处理的申请拒绝重复审核
                {
                    throw new Exception("该申请已被处理，请勿重复审核");
                }

                if (!approved)
                {
                    //驳回：仅更新申请单状态（乐观锁防并发重复审核），不涉及任何资金与订单状态变动
                    if (!await _irefundRepository.TryUpdateStatusAsync(refundId, "Pending", "Rejected", auditorId, auditRemark, _uow.Transaction))
                    {
                        throw new Exception("申请状态已变化，审核失败请重试");
                    }
                    await _uow.CommitAsync();
                    _result.IsSuccess = true;
                    return _result;
                }

                //通过：按申请单重建退款上下文并重新校验订单当前状态（申请后可能已变化，如过退款期/已退款）
                var ctx = await BuildContextFromApplicationAsync(application, _uow.Transaction);
                //执行退款资金操作（佣金/销售额回滚、佣金记录更新、B组积分扣减与订单状态变更）
                await ExecuteRefundCoreAsync(ctx, _uow.Transaction);
                //全部成功后置为已通过（乐观锁）
                if (!await _irefundRepository.TryUpdateStatusAsync(refundId, "Pending", "Approved", auditorId, auditRemark, _uow.Transaction))
                {
                    throw new Exception("申请状态已变化，审核失败请重试");
                }
                await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
        }

        public async Task<List<FinRefund>> GetPendingRefundsAsync() //管理端待审核列表
        {
            return await _irefundRepository.GetByStatusAsync("Pending");
        }

        public async Task<List<FinRefund>> GetOrderRefundsAsync(string orderId) //消费者端查询订单退款记录
        {
            return await _irefundRepository.GetByOrderIdAsync(orderId);
        }

        //加载订单与佣金记录，并做退款准入校验（状态机 + 已结算拦截 + 14天退款期）
        private async Task<RefundContext> LoadOrderContextAsync(string orderId, IDbTransaction? transaction)
        {
            //【串联B组】通过B组真实接口获取订单及明细
            var orderDetail = await _orderService.GetOrderDetailAsync(orderId);
            if (orderDetail?.Order == null)
            {
                throw new Exception("订单不存在，无法退款！");
            }
            var order = orderDetail.Order;
            var orderStatus = OrderStatusCodes.Parse(order.OrderStatus);
            if (orderStatus is OrderStatus.PendingPayment or OrderStatus.Cancelled)
            {
                throw new Exception("订单未支付或已取消，无法退款！");
            }
            if (orderStatus == OrderStatus.Refunded)
            {
                throw new Exception("订单已退款，请勿重复操作！");
            }

            //查询C组自己的佣金记录（FIN_PROCOMRECORDS），用于退款期校验与佣金回滚
            var record = await _icommissionRepository.GetByOrderIdAsync(orderId, transaction);

            //退款准入校验（仅已签收订单）：
            //防线一：佣金已过退款期并完成二段结算（Settled），佣金已转入可提现余额，禁止退款
            //防线二：超过可退款期14天，拒绝退款（优先取佣金记录签收时间；无佣金记录的订单用订单最后状态变更时间兜底）
            if (orderStatus == OrderStatus.Completed)
            {
                if (record?.Status == "Settled")
                {
                    throw new Exception("该订单佣金已过退款期并完成结算，无法退款！");
                }
                var signTime = record?.SignDate ?? order.UpdatedAt;
                if (signTime != null && DateTime.Now - signTime.Value >= TimeSpan.FromDays(14))
                {
                    throw new Exception("该订单已过可退期14天，无法退款！");
                }
            }

            return new RefundContext
            {
                Order = order,
                Details = orderDetail.Details,
                OrderStatus = orderStatus,
                Record = record
            };
        }

        //按消费者退款请求构建退款上下文（计算退款金额/数量/明细）
        private async Task<RefundContext> BuildContextFromRequestAsync(GroupC_RefundRequest refundRequest, IDbTransaction? transaction)
        {
            var ctx = await LoadOrderContextAsync(refundRequest.OrderId!, transaction);
            //  区分是否是整单退款: 如果 ProductID 为空，表示整单退款
            if (string.IsNullOrEmpty(refundRequest.ProductID))
            {
                ctx.IsFullRefund = true;
                ctx.RefundAmount = ctx.Order.FinalAmount;   // 整单退时，退款金额 = 订单实付金额
                ctx.RefundQty = 0;                          // 整单不计数量
            }
            else
            {
                //【串联B组】从B组订单明细快照中匹配退款商品
                var detail = ctx.Details.FirstOrDefault(d => d.ProductId == refundRequest.ProductID);
                if (detail == null)
                {
                    throw new Exception("该订单中不存在此商品，请核对退款商品！");
                }
                if (refundRequest.RefundQty <= 0 || refundRequest.RefundQty > detail.Quantity) //退款数量不合法
                {
                    throw new Exception("退款数量不合法，请重新输入退款数量！");
                }
                ctx.DetailId = detail.OrderDetailId;
                ctx.SupplierId = detail.SupplierId;    //供应商信息直接取自B组订单明细快照
                ctx.RefundQty = refundRequest.RefundQty;
                ctx.RefundAmount = ctx.RefundQty * detail.UnitPrice;    //计算退款金额
            }
            FinalizeRatio(ctx);
            return ctx;
        }

        //按已存在的退款申请单重建退款上下文（审核通过时调用，信任申请单中的金额并重新校验订单状态）
        private async Task<RefundContext> BuildContextFromApplicationAsync(FinRefund application, IDbTransaction? transaction)
        {
            var ctx = await LoadOrderContextAsync(application.OrderId!, transaction);
            ctx.IsFullRefund = string.IsNullOrEmpty(application.DetailId);
            ctx.DetailId = application.DetailId;
            ctx.SupplierId = application.SupplierId;
            ctx.RefundQty = application.RefundQty;
            ctx.RefundAmount = application.RefundAmount;
            FinalizeRatio(ctx);
            return ctx;
        }

        private static void FinalizeRatio(RefundContext ctx)
        {
            if (ctx.Order.FinalAmount <= 0)
            {
                throw new Exception("订单实付金额异常，无法计算退款比例！");
            }
            ctx.Ratio = ctx.RefundAmount / ctx.Order.FinalAmount;  //退款金额占订单实付比例
        }

        //执行退款的资金操作：佣金/销售额回滚、佣金记录更新、B组积分扣减与订单状态变更（不含申请单写入）
        private async Task ExecuteRefundCoreAsync(RefundContext ctx, IDbTransaction? transaction)
        {
            // 仅已签收（Completed）订单需要回滚团长佣金；未签收订单尚未结算佣金，无需回滚
            if (ctx.OrderStatus == OrderStatus.Completed && ctx.Record != null)
            {
                // 基础佣金按退款比例回滚；阶梯奖励佣金在 RefundRollbackMoney 内按累计销售额跌破的档位撤销
                var baseRollback = ctx.Record.CommBaseAmount * ctx.Ratio;
                //如果团长和消费者解绑了，那么团长佣金不会扣（平台在解绑触发时自动清空团长的待结算余额，防止团长反复解绑/绑定刷回退佣金差）
                var _promoterInfo = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(ctx.Record.PromoterId, transaction);
                if (_promoterInfo != null)    //团长存在,则回滚（销售额同样按退款比例回滚）
                {
                    await RefundRollbackMoney(ctx.Record.PromoterId, baseRollback, ctx.Order.TotalAmount * ctx.Ratio, transaction);
                }
                //更改佣金记录：整单退整体置为已退款；部分退仅累加已退金额
                if (ctx.IsFullRefund)
                {
                    await _icommissionRepository.UpdateStatusAsync(ctx.Record.RecordId, "Refunded", transaction);
                }
                await _icommissionRepository.UpdateRefundedAmountAsync(ctx.Record.RecordId, ctx.RefundAmount, transaction);
            }
            //未签收订单退款：无需回滚佣金。
            //B组 DeductPointsForRefundAsync 在"已支付未发货"状态整单退款时会自动释放库存；
            //已发货订单的物流拦截/通知供应商接口需AB组之间另行约定，待补充

            //【串联B组】积分回滚 + 订单状态变更
            var pointsToDeduct = (int)Math.Floor(ctx.Order.PointsEarned * ctx.Ratio);
            if (ctx.IsFullRefund)
            {
                //整单退款：B组扣回积分并将订单置为"已退款"（未发货订单同时释放库存）
                await _orderService.DeductPointsForRefundAsync(ctx.Order.CustomerId, ctx.Order.OrderId, pointsToDeduct);
            }
            else
            {
                //部分退款：B组按比例扣回积分并将订单置为"退款中"
                await _orderService.DeductPointsForPartialRefundAsync(ctx.Order.CustomerId, ctx.Order.OrderId, pointsToDeduct);
            }
        }

        private async Task<Result> RefundRollbackMoney(string? promoterID, decimal baseCommission, decimal goodsAmount,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)  //退款回滚与退款信息记录（baseCommission为基础佣金回滚额，阶梯奖励佣金按跌破档位在内部计算）
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
                //计算新的累计销售额：退款使累计销售额向下跌破阶梯档位时，对应奖励佣金全部撤销（与一段结算的发放逻辑对称）
                var _newPromoterTotalSales = _oldPromoterTotalSales - goodsAmount;
                var _bonusRollback = GroupC_CommissionBonusPolicy.CalculateRollbackBonus(_oldPromoterTotalSales, _newPromoterTotalSales);
                var _totalCommissionRollback = baseCommission + _bonusRollback;
                //回滚销售额与佣金
                await _ipromoterRepository.GroupC_UpdatePromoterTotalSalesAsync(promoterID, -goodsAmount, transaction);
                await _ipromoterRepository.GroupC_UpdatePromoterPendingBalanceAsync(promoterID, -_totalCommissionRollback, transaction);
                //记录新的佣金与销售额
                var _newPromoterPendingBalance = _oldPromoterPendingBalance - _totalCommissionRollback;
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
                    await _uow.CommitAsync();
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
            string status = "Approved",
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default
            )  //添加退款信息函数（status: Pending=待审核申请单, Approved=已执行的退款记录）
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
                _finRefund.Status = status;
                await _irefundRepository.InsertRefundAsync(_finRefund, transaction);

                //产生日志信息
                var _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Create";
                _tableLog.TableName = "FIN_REFUND";
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
                    Remark = _finRefund.Remark,
                    Status = _finRefund.Status
                });
                await _logManager.WriteTableChangeLog(_tableLog);
                if (ownTransaction)
                    await _uow.CommitAsync();
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
