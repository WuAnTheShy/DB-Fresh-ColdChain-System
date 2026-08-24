using FreshColdChain.Interfaces;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using FreshColdChain.Models;
using Newtonsoft.Json;
using System.Data;
using System.Transactions;
namespace FreshColdChain.Services
{

    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _uow;
        private readonly IPaymentRepository _ipaymentRepository;
        private readonly ITableLogService _logManager;


        public PaymentService(IUnitOfWork uow,IPaymentRepository ipaymentRepository, ITableLogService logmanager)
        {
            _uow = uow;
            _ipaymentRepository = ipaymentRepository;
            _logManager = logmanager;
        }
        public async Task<Result> CreatePaymentRecord(PaymentRequest paymentRequest,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default) //创建支付流水函数
        {
            var _result = new Result();
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
                
                //throw new Exception("团长信息不存在");
                if (paymentRequest.orderID == null || paymentRequest.orderID == string.Empty
                || paymentRequest.payMethod == null || paymentRequest.payMethod == string.Empty
                || paymentRequest.status == null || paymentRequest.status == string.Empty) //不完整的订单信息或支付渠道信息或订单状态
                {
                    throw new Exception("不完整的订单信息或支付渠道信息或订单状态");
                }
                GroupC_CreatePaymentRequest _createPaymentRequest = new GroupC_CreatePaymentRequest();
                _createPaymentRequest.PayId = "PAY_" + Guid.NewGuid().ToString("N"); //自动生成支付流水编号
                _createPaymentRequest.PayMethod = paymentRequest.payMethod;
                _createPaymentRequest.TransactionNo = paymentRequest.transactionNo;

                GroupC_UpdatePaymentRequest _updatePaymentRequest = new GroupC_UpdatePaymentRequest();
                _updatePaymentRequest.PayId = _createPaymentRequest.PayId;
                GroupC_FinPaymentRecord _finPaymentRecord = new GroupC_FinPaymentRecord();
                if (paymentRequest.status == "Failed") //支付失败
                {
                    _updatePaymentRequest.IsSuccess = false;
                    if (paymentRequest.errorMessage == null || paymentRequest.errorMessage == string.Empty)  //支付失败时一定得有错误信息
                    {
                        throw new Exception("支付失败原因缺失");
                    }
                    _updatePaymentRequest.ErrorMessage = paymentRequest.errorMessage;

                    //设置数据库处理的支付流水类
                    _finPaymentRecord.PayId = _createPaymentRequest.PayId;
                    _finPaymentRecord.PayMethod = _createPaymentRequest.PayMethod;
                    _finPaymentRecord.Status = "Failed";
                    _finPaymentRecord.Remark = "Failed Reason:" + _updatePaymentRequest.ErrorMessage;
                    _finPaymentRecord.OrderId = paymentRequest.orderID;
                    _finPaymentRecord.PayAmount = paymentRequest.payAmount;
                    _finPaymentRecord.PayTime = DateTime.Now;
                    _finPaymentRecord.TransactionNo = _createPaymentRequest.TransactionNo;
                }
                else if (paymentRequest.status == "Success")   //支付成功
                {
                    _updatePaymentRequest.IsSuccess = true;
                    //设置数据库处理的支付流水类
                    _finPaymentRecord.PayId = _createPaymentRequest.PayId;
                    _finPaymentRecord.PayMethod = _createPaymentRequest.PayMethod;
                    _finPaymentRecord.Status = "Success";
                    _finPaymentRecord.Remark = string.Empty;
                    _finPaymentRecord.OrderId = paymentRequest.orderID;
                    _finPaymentRecord.PayAmount = paymentRequest.payAmount;
                    _finPaymentRecord.PayTime = DateTime.Now;
                    _finPaymentRecord.TransactionNo = _createPaymentRequest.TransactionNo;

                }
                //调用Repository层函数添加数据记录
                await _ipaymentRepository.GroupC_AddPaymentRecordAsync(_finPaymentRecord,transaction);
                //记录表修改日志
                var _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Create";
                _tableLog.TableName = "FIN_PAYMENTRECORDS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = string.Empty;
                _tableLog.NewValue = JsonConvert.SerializeObject(new
                {
                    PayId = _finPaymentRecord.PayId,
                    OrderId = _finPaymentRecord.OrderId,
                    PayMethod = _finPaymentRecord.PayMethod,
                    TransactionNo = _finPaymentRecord.TransactionNo,
                    PayAmount = _finPaymentRecord.PayAmount,
                    Status = _finPaymentRecord.Status,
                    PayTime = _finPaymentRecord.PayTime,
                    Remark = _finPaymentRecord.Remark
                });
                await _logManager.WriteTableChangeLog(_tableLog);
                // 所有业务操作成功，提交事务
                if(ownTransaction)
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

        //组合查询支付流水（管理端查询页用）
        public async Task<List<GroupC_FinPaymentRecord>> SearchPaymentsAsync(DateTime? startTime, DateTime? endTime,
            string? orderId, string? status)
        {
            return await _ipaymentRepository.SearchAsync(startTime, endTime, orderId, status);
        }
    }

}
