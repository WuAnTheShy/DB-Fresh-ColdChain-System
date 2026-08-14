using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Models;
using DBFreshColdChain.Repositories;
using Newtonsoft.Json;
using System.Transactions;
namespace DBFreshColdChain.Services
{

    public class PaymentManager: GroupC_IPaymentManager
    {
        private readonly DbHelper _dbHelper;
        private readonly GroupC_ITableLogManager _logManager;

        public PaymentManager(DbHelper dbHelper, GroupC_ITableLogManager logmanager)
        {
            _dbHelper = dbHelper;
            _logManager = logmanager;
        }
        public bool CreatePaymentRecord(string? orderID, string? payMethod, string? transactionNo,decimal payAmount,string? status,string? errorMessage) //创建支付流水函数
        {
            if( orderID == null || orderID == string.Empty 
            ||  payMethod == null || payMethod == string.Empty 
            || status == null || status == string.Empty) //不完整的订单信息或支付渠道信息或订单状态
                return false;

            CreatePaymentRequest _createPaymentRequest = new CreatePaymentRequest();
            _createPaymentRequest.PayId = "PAY_" + Guid.NewGuid().ToString("N"); //自动生成支付流水编号
            _createPaymentRequest.PayMethod = payMethod;
            _createPaymentRequest.TransactionNo = transactionNo;

            UpdatePaymentRequest _updatePaymentRequest = new UpdatePaymentRequest();
            _updatePaymentRequest.PayId = _createPaymentRequest.PayId;
            FinPaymentRecord _finPaymentRecord = new FinPaymentRecord();
            if (status == "Failed") //支付失败
            {
                _updatePaymentRequest.IsSuccess = false;
                if (errorMessage == null || errorMessage == string.Empty)  //支付失败时一定得有错误信息
                {
                    return false;
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
            _dbHelper.GroupC_AddPaymentRecord( _finPaymentRecord );
            //记录表修改日志
            var _tableLog = new Log_Auditrails();
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
            return true;
        }
    }

}
