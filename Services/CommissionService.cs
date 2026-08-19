using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Models;
using DBFreshColdChain.Models.CrossGroup;
using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Repositories;
using FreshColdChain.Repositories;
using Newtonsoft.Json;
using System.Data;

namespace DBFreshColdChain.Services
{

    public class CommissionService: ICommissionService
    {
        // Repository层句柄
        // 事务核心句柄
        private readonly IUnitOfWork _uow;
        // 数据库访问句柄
        private readonly IPromoterRepository _ipromoterRepository;
        // Interface层句柄（修正字段名，与构造函数一致）
        private readonly ITableLogService _logManager;

        private readonly ICommissionRepository _icommissionRecordRepository;
        // 构造函数
        public CommissionService(IUnitOfWork uow, IPromoterRepository ipromoterRepository, ITableLogService logManager, ICommissionRepository icommissionRecordRepository)
        {
            _uow = uow;
            _ipromoterRepository = ipromoterRepository;
            _logManager = logManager;
            _icommissionRecordRepository = icommissionRecordRepository;
        }
        //佣金结算触发函数
        public async Task<CommissionResult> RegisterCompletedOrderAsync(CommissionOrderRequest commissionOrderRequest,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            var _commissionResult = new CommissionResult();
            bool ownTransaction = false;
            try
            {
                // 事务控制：如果外部没传事务，自己开启
                if (transaction == null)
                {
                    await _uow.BeginAsync();
                    ownTransaction = true;
                    transaction = _uow.Transaction;
                }
                var _promoterInfo = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(commissionOrderRequest.promoterID, transaction);
                if (_promoterInfo == null)
                {
                    throw new Exception("团长信息不存在");
                }

                var _oldTotalSales = _promoterInfo.TotalSales;
                await _ipromoterRepository.GroupC_UpdatePromoterTotalSalesAsync(commissionOrderRequest.promoterID, commissionOrderRequest.goodsAmount, transaction);
                var _newTotalSales = _oldTotalSales + commissionOrderRequest.goodsAmount;

                var _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { TotalSales = _oldTotalSales });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { TotalSales = _newTotalSales });
                await _logManager.WriteTableChangeLog(_tableLog);

                _commissionResult.CommBaseAmount = _promoterInfo.BaseCommissionRate * commissionOrderRequest.finalAmount;

                if ((_oldTotalSales < 1000 && _newTotalSales >= 1000) ||
                    (_oldTotalSales < 2000 && _newTotalSales >= 2000))
                {
                    _commissionResult.CommBonusAmount = 50;
                }
                else if ((_oldTotalSales < 3000 && _newTotalSales >= 3000) ||
                         (_oldTotalSales < 4000 && _newTotalSales >= 4000) ||
                         (_oldTotalSales < 6000 && _newTotalSales >= 6000) ||
                         (_oldTotalSales < 7000 && _newTotalSales >= 7000))
                {
                    _commissionResult.CommBonusAmount = 150;
                }
                else if ((_oldTotalSales < 8000 && _newTotalSales >= 8000) ||
                         (_oldTotalSales < 9000 && _newTotalSales >= 9000))
                {
                    _commissionResult.CommBonusAmount = 450;
                }
                else if ((_oldTotalSales < 5000 && _newTotalSales >= 5000) ||
                         (_oldTotalSales < 10000 && _newTotalSales >= 10000))
                {
                    _commissionResult.CommBonusAmount = 750;
                }
                else
                {
                    _commissionResult.CommBonusAmount = 0;
                }

                var totalCommission = _commissionResult.CommBaseAmount + _commissionResult.CommBonusAmount;

                var _oldPromoterPendingBalance = await _ipromoterRepository.GroupC_FindPromoterPendingBalanceAsync(commissionOrderRequest.promoterID, transaction);
                await _ipromoterRepository.GroupC_UpdatePromoterPendingBalanceAsync(commissionOrderRequest.promoterID, totalCommission, transaction);
                var _newPromoterPendingBalance = _oldPromoterPendingBalance + totalCommission;

                _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
                await _logManager.WriteTableChangeLog(_tableLog);

                var record = new CommissionRecord
                {
                    RecordId = "PROC_" + Guid.NewGuid().ToString("N"),
                    PromoterId = commissionOrderRequest.promoterID,
                    OrderId = commissionOrderRequest.orderID,  // 需要从请求中传入订单ID
                    FinalAmount = commissionOrderRequest.finalAmount,
                    CommBaseAmount = _commissionResult.CommBaseAmount,
                    CommBonusAmount = _commissionResult.CommBonusAmount,
                    TotalCommission = totalCommission,
                    SignDate = DateTime.Now,
                    ExpectedSettleDate = DateTime.Now.AddDays(14),
                    Status = "Pending",
                    RefundedAmount = 0
                };
                await _icommissionRecordRepository.InsertAsync(record, transaction);
                _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Create";
                _tableLog.TableName = "FIN_PROCOMRECORDS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.NewValue = JsonConvert.SerializeObject(new {
                    PromoterId = commissionOrderRequest.promoterID,
                    OrderId = commissionOrderRequest.orderID,  // 需要从请求中传入订单ID
                    FinalAmount = commissionOrderRequest.finalAmount,
                    CommBaseAmount = _commissionResult.CommBaseAmount,
                    CommBonusAmount = _commissionResult.CommBonusAmount,
                    TotalCommission = totalCommission,
                    SignDate = DateTime.Now,
                    ExpectedSettleDate = DateTime.Now.AddDays(14),
                    Status = "Pending",
                    RefundedAmount = 0
                });

                await _logManager.WriteTableChangeLog(_tableLog);
                // 如果事务是自己建立的，则提交事务
                if (ownTransaction)
                    await _uow.CommitAsync();
                _commissionResult.IsSuccess = true;
                _commissionResult.CommSettlementDate = DateTime.Now;
                return _commissionResult;
            }
            catch (Exception ex)
            {
                if (ownTransaction && _uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _commissionResult.IsSuccess = false;
                _commissionResult.CommSettlementDate = DateTime.Now;
                _commissionResult.ErrorMessage = $"系统错误：{ex.Message}";
                return _commissionResult;
            }

        }
        //过退款期激活佣金
        public async Task<Result> ActivatePromoterMoney(ActivateCommissionOrderRequest request, 
            IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
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
                var _promoterInfo = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(request.promoterID, transaction);
                if (_promoterInfo == null)
                {

                    throw new Exception("团长信息不存在");
                }

                var totalCommission = request.commBaseAmount + request.commBonusAmount;
                var _oldPromoterPendingBalance = _promoterInfo.PendingBalance;
                var _oldPromoterCurrentBalance = _promoterInfo.CurrentBalance;

                await _ipromoterRepository.GroupC_UpdatePromoterPendingBalanceAsync(request.promoterID, -totalCommission, transaction);
                await _ipromoterRepository.GroupC_UpdatePromoterCurrentBalanceAsync(request.promoterID, totalCommission, transaction);

                var _newPromoterPendingBalance = _oldPromoterPendingBalance - totalCommission;
                var _newPromoterCurrentBalance = _oldPromoterCurrentBalance + totalCommission;


                var record = await _icommissionRecordRepository.GetByOrderIdAsync(request.orderID, transaction);
                if (record != null && record.Status == "Pending")
                {
                    await _icommissionRecordRepository.UpdateStatusAsync(record.RecordId, "Settled", transaction);
                }

                var _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
                await _logManager.WriteTableChangeLog(_tableLog);

                _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { CurrentBalance = _oldPromoterCurrentBalance });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { CurrentBalance = _newPromoterCurrentBalance });
                await _logManager.WriteTableChangeLog(_tableLog);

                _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "FIN_PROCOMRECORDS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { Status = "Pending" });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = "Settled" });
                await _logManager.WriteTableChangeLog(_tableLog);
                // 所有业务操作成功，提交事务
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
