using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Repositories;
using Newtonsoft.Json;
using System.Data;

namespace FreshColdChain.Services
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
                else if (_uow.Transaction == null)
                {
                    //外部事务（B组传入）：挂载到工作单元，使C组仓储复用事务所在的连接，
                    //避免"C组自建连接 + B组事务"导致的连接/事务不匹配异常
                    _uow.AttachExternalTransaction(transaction);
                }
                //无团长的普通订单不产生佣金，直接返回成功（0佣金），不阻塞B组订单完成
                if (string.IsNullOrEmpty(commissionOrderRequest.promoterID))
                {
                    if (ownTransaction)
                        await _uow.CommitAsync();
                    _commissionResult.IsSuccess = true;
                    _commissionResult.CommSettlementDate = DateTime.Now;
                    return _commissionResult;
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

                //等级挂钩：累计销售额跨档导致等级变化时，同步更新佣金比例（等级越高比例越高）。
                //本单仍按结算前的旧比例计佣，新比例从下一单开始生效；退款回滚时在 RefundRollbackMoney 内对称回退
                var _levelRate = GroupC_LevelCommissionPolicy.ResolveRate(_newTotalSales);
                if (_promoterInfo.BaseCommissionRate != _levelRate)
                {
                    await _ipromoterRepository.GroupC_UpdatePromoterCommissionRateAsync(commissionOrderRequest.promoterID, _levelRate, transaction);
                    _tableLog = new GroupC_LogAuditrails();
                    _tableLog.ActionType = "Update";
                    _tableLog.TableName = "CRM_PROMOTERS";
                    _tableLog.OperatorType = "Platform";
                    _tableLog.OperatorId = "\\";
                    _tableLog.OldValue = JsonConvert.SerializeObject(new { BaseCommissionRate = _promoterInfo.BaseCommissionRate });
                    _tableLog.NewValue = JsonConvert.SerializeObject(new { BaseCommissionRate = _levelRate });
                    await _logManager.WriteTableChangeLog(_tableLog);
                }

                _commissionResult.CommBaseAmount = _promoterInfo.BaseCommissionRate * commissionOrderRequest.finalAmount;

                //阶梯奖励：累计销售额每跨过一档即发放对应奖励，一单跨多档时全部叠加到该单奖励佣金
                _commissionResult.CommBonusAmount = GroupC_CommissionBonusPolicy.CalculateCrossedBonus(_oldTotalSales, _newTotalSales);

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
                    // FIN_PROCOMRECORDS.RECORDID 为 VARCHAR2(36)：4 位前缀 + 32 位 GUID。
                    RecordId = "PROC" + Guid.NewGuid().ToString("N"),
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
        //过退款期激活佣金（二段结算：待结算余额 → 可提现余额）
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
                else if (_uow.Transaction == null)
                {
                    //外部事务：挂载到工作单元，使C组仓储复用事务所在的连接
                    _uow.AttachExternalTransaction(transaction);
                }
                if (string.IsNullOrEmpty(request.orderID))
                {
                    throw new Exception("订单编号为空");
                }

                //先查佣金记录并校验状态，再动账（顺序不能反，否则重复调用/退款后调用会错误加钱）
                var record = await _icommissionRecordRepository.GetByOrderIdAsync(request.orderID, transaction);
                if (record == null)
                {
                    throw new Exception("该订单无佣金记录，无法激活");
                }
                if (record.Status == "Settled")
                {
                    //幂等：已激活过直接返回成功，不重复动账
                    if (ownTransaction)
                        await _uow.CommitAsync();
                    _result.IsSuccess = true;
                    return _result;
                }
                if (record.Status != "Pending")
                {
                    throw new Exception($"佣金记录状态为 {record.Status}，不允许激活");
                }

                var _promoterInfo = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(record.PromoterId, transaction);
                if (_promoterInfo == null)
                {
                    throw new Exception("团长信息不存在");
                }

                //激活金额以佣金记录为准（不信任调用方传入的金额）：
                //发生过部分退款时，按订单金额留存比例计算剩余有效佣金
                //（注：部分退款且跨阶梯回滚的极端场景下，与退款时按档位撤销的金额可能存在微小差异，
                //  如需分毫不差需另增"已回滚佣金"字段，当前按比例口径与记录自洽）
                var refundRatio = record.FinalAmount > 0
                    ? record.RefundedAmount / record.FinalAmount
                    : 0m;
                var totalCommission = record.TotalCommission * (1 - refundRatio);

                var _oldPromoterPendingBalance = _promoterInfo.PendingBalance;
                var _oldPromoterCurrentBalance = _promoterInfo.CurrentBalance;

                //乐观锁：仅当状态仍为 Pending 时才置为 Settled，防止与退款/其他结算任务并发导致重复动账
                if (!await _icommissionRecordRepository.TryUpdateStatusAsync(record.RecordId, "Pending", "Settled", transaction))
                {
                    throw new Exception("佣金记录状态已变化，激活失败请重试");
                }

                await _ipromoterRepository.GroupC_UpdatePromoterPendingBalanceAsync(record.PromoterId, -totalCommission, transaction);
                await _ipromoterRepository.GroupC_UpdatePromoterCurrentBalanceAsync(record.PromoterId, totalCommission, transaction);

                var _newPromoterPendingBalance = _oldPromoterPendingBalance - totalCommission;
                var _newPromoterCurrentBalance = _oldPromoterCurrentBalance + totalCommission;

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
                _tableLog.NewValue = JsonConvert.SerializeObject(new { Status = "Settled" });
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
