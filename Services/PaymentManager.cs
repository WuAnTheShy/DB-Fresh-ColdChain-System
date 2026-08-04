using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Repositories;
using FreshColdChain.Repositories;
using Newtonsoft.Json;
using System.Transactions;
using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;
namespace DBFreshColdChain.Services
{

    public class GroupC_PaymentManager : GroupC_IPaymentManager
    {
        private readonly IUnitOfWork _uow;
        private readonly PaymentRepository _paymentRepository;
        private readonly GroupC_ITableLogManager _logManager;


        public GroupC_PaymentManager(IUnitOfWork uow,PaymentRepository paymentRepository, GroupC_ITableLogManager logmanager)
        {
            _uow = uow;
            _paymentRepository = paymentRepository;
            _logManager = logmanager;
        }
        public async Task<Result> CreatePaymentRecord(string? orderID, string? payMethod, string? transactionNo,decimal payAmount,string? status,string? errorMessage) //创建支付流水函数
        {
            // 开启事务
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (orderID == null || orderID == string.Empty
            || payMethod == null || payMethod == string.Empty
            || status == null || status == string.Empty) //不完整的订单信息或支付渠道信息或订单状态
                {
                    await _uow.RollbackAsync();
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "不完整的订单信息或支付渠道信息或订单状态";
                    return _result;
                }
                GroupC_CreatePaymentRequest _createPaymentRequest = new GroupC_CreatePaymentRequest();
                _createPaymentRequest.PayId = "PAY_" + Guid.NewGuid().ToString("N"); //自动生成支付流水编号
                _createPaymentRequest.PayMethod = payMethod;
                _createPaymentRequest.TransactionNo = transactionNo;

                GroupC_UpdatePaymentRequest _updatePaymentRequest = new GroupC_UpdatePaymentRequest();
                _updatePaymentRequest.PayId = _createPaymentRequest.PayId;
                GroupC_FinPaymentRecord _finPaymentRecord = new GroupC_FinPaymentRecord();
                if (status == "Failed") //支付失败
                {
                    _updatePaymentRequest.IsSuccess = false;
                    if (errorMessage == null || errorMessage == string.Empty)  //支付失败时一定得有错误信息
                    {
                        await _uow.RollbackAsync();
                        _result.IsSuccess = false;
                        _result.ErrorMessage = "支付失败原因缺失";
                        return _result;
                    }
                    _updatePaymentRequest.ErrorMessage = errorMessage;

                    //设置数据库处理的支付流水类
                    _finPaymentRecord.PayId = _createPaymentRequest.PayId;
                    _finPaymentRecord.PayMethod = _createPaymentRequest.PayMethod;
                    _finPaymentRecord.Status = "Failed";
                    _finPaymentRecord.Remark = "Failed Reason:" + _updatePaymentRequest.ErrorMessage;
                    _finPaymentRecord.OrderId = orderID;
                    _finPaymentRecord.PayAmount = payAmount;
                    _finPaymentRecord.PayTime = DateTime.Now;
                    _finPaymentRecord.TransactionNo = _createPaymentRequest.TransactionNo;
                }
                else if (status == "Success")   //支付成功
                {
                    _updatePaymentRequest.IsSuccess = true;
                    //设置数据库处理的支付流水类
                    _finPaymentRecord.PayId = _createPaymentRequest.PayId;
                    _finPaymentRecord.PayMethod = _createPaymentRequest.PayMethod;
                    _finPaymentRecord.Status = "Success";
                    _finPaymentRecord.Remark = string.Empty;
                    _finPaymentRecord.OrderId = orderID;
                    _finPaymentRecord.PayAmount = payAmount;
                    _finPaymentRecord.PayTime = DateTime.Now;
                    _finPaymentRecord.TransactionNo = _createPaymentRequest.TransactionNo;

                }
                //调用Repository层函数添加数据记录
                _paymentRepository.GroupC_AddPaymentRecord(_finPaymentRecord);
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
                _logManager.WriteTableChangeLog(_tableLog);
                // 所有业务操作成功，提交事务
                await _uow.CommitAsync();
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
            _result.IsSuccess = true;
            return _result;
        }
    }

}
