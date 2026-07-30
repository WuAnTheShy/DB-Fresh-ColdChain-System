using DBFreshColdChain.Models;
using DBFreshColdChain.Repositories;
using DBFreshColdChain.Interfaces;
using Newtonsoft.Json;
namespace DBFreshColdChain.Services
{
    public class PromoterManager: GroupC_IProMonterManager
    {
        //Repository层句柄
        private readonly DbHelper _dbHelper;
        //Interface层句柄
        private readonly GroupC_ITableLogManager _logManager;
        //构造函数
        public PromoterManager(DbHelper dbHelper, GroupC_ITableLogManager logManager)
        {
            _dbHelper = dbHelper;
            _logManager = logManager;
        }
        public bool FindPromoterInfo(string? promoterID, ref CrmPromoter promoterInfo)    //查找团长信息函数
        {
            if(promoterID == string.Empty || promoterID == null) //空值查找无效
                return false;
            var _promoterInfo = _dbHelper.GroupC_FindPromoterRecord(promoterID);
            if(_promoterInfo == null )
                return false;
            return true;
        }

        public bool CommisionSettlement(string? promoterID, decimal finalAmount, decimal goodsAmount,ref CommissionInfo commissionInfo)  //结算佣金总业务函数
        {
            //获取团长信息
            CrmPromoter _promoterInfo = new CrmPromoter();
            if (!FindPromoterInfo(promoterID, ref _promoterInfo))                
            {
                commissionInfo = new CommissionInfo();
                return false;
            }

            //销售额结算
            var _oldTotalSales = _promoterInfo.TotalSales;
            _dbHelper.GroupC_UpdatePromoterTotalSales(promoterID, goodsAmount);
            var _newTotalSales = _oldTotalSales + goodsAmount;

            //产生日志信息
            var _tableLog = new Log_Auditrails();
            _tableLog.ActionType = "Update";
            _tableLog.TableName = "CRM_PROMOTERS";
            _tableLog.OperatorType = "Platform";
            _tableLog.OperatorId = "\\";
            _tableLog.OldValue = JsonConvert.SerializeObject(new { TotalSales = _oldTotalSales });
            _tableLog.NewValue = JsonConvert.SerializeObject(new { TotalSales = _newTotalSales });
            _logManager.WriteTableChangeLog(_tableLog);
            //基础佣金计算
            commissionInfo.CommBaseAmount = _promoterInfo.BaseCommissionRate * finalAmount;
            //奖励佣金计算
            if ((_oldTotalSales < 1000 && _newTotalSales >= 1000) ||
                (_oldTotalSales < 2000 && _newTotalSales >= 2000))
            {
                commissionInfo.CommBonusAmount = 50;
            }
            else if((_oldTotalSales < 3000 && _newTotalSales >= 3000) ||
                    (_oldTotalSales < 4000 && _newTotalSales >= 4000) ||
                    (_oldTotalSales < 6000 && _newTotalSales >= 6000) ||
                    (_oldTotalSales < 7000 && _newTotalSales >= 7000))
            {
                commissionInfo.CommBonusAmount = 150;
            }
            else if ((_oldTotalSales < 8000 && _newTotalSales >= 8000) ||
                    (_oldTotalSales < 9000 && _newTotalSales >= 9000))
            {
                commissionInfo.CommBonusAmount = 450;
            }
            else if ((_oldTotalSales < 5000 && _newTotalSales >= 5000) ||
                    (_oldTotalSales < 10000 && _newTotalSales >= 10000))
            {
                commissionInfo.CommBonusAmount = 750;
            }
            else
            {
                commissionInfo.CommBonusAmount = 0;
            }
            //计算总佣金
            var totalCommission = commissionInfo.CommBaseAmount + commissionInfo.CommBonusAmount;
            //写入团长表的待结算余额中
            var _oldPromoterPendingBalance = _dbHelper.GroupC_FindPromoterPendingBalance(promoterID);
            _dbHelper.GroupC_UpdatePromoterPendingBalance(promoterID, totalCommission);
            var _newPromoterPendingBalance = _oldPromoterPendingBalance + totalCommission;
            //产生日志信息
            _tableLog = new Log_Auditrails();
            _tableLog.ActionType = "Update";
            _tableLog.TableName = "CRM_PROMOTERS";
            _tableLog.OperatorType = "Platform";
            _tableLog.OperatorId = "\\";
            _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
            _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
            _logManager.WriteTableChangeLog(_tableLog);
            //时间获取
            commissionInfo.CommSettlementDate = DateTime.Now;
            return true;
        }

        public bool ActivatePromoterMoney(string? promoterID, decimal commBaseAmount, decimal commBonusAmount) //过可退期后团长佣金可提现化函数
        {
            CrmPromoter _promoterInfo = new CrmPromoter();
            if (!FindPromoterInfo(promoterID, ref _promoterInfo))
                return false;
            var totalCommission = commBaseAmount + commBonusAmount; //先计算该单的总佣金
            //记录旧的待结算余额数据
            var _oldPromoterPendingBalance = _promoterInfo.PendingBalance;
            var _oldPromoterCurrentBalance = _promoterInfo.CurrentBalance;
            //更改团长余额信息(扣减旧的，加上新的）
            _dbHelper.GroupC_UpdatePromoterPendingBalance(promoterID, -totalCommission);
            _dbHelper.GroupC_UpdatePromoterCurrentBalance(promoterID, totalCommission);
            //记录新的待结算余额数据
            var _newPromoterPendingBalance = _oldPromoterPendingBalance - totalCommission;
            var _newPromoterCurrentBalance = _oldPromoterCurrentBalance + totalCommission;
            //产生日志信息（两条）
            //Pendingbalance的扣减
            var _tableLog = new Log_Auditrails();
            _tableLog.ActionType = "Update";
            _tableLog.TableName = "CRM_PROMOTERS";
            _tableLog.OperatorType = "Platform";
            _tableLog.OperatorId = "\\";
            _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
            _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
            _logManager.WriteTableChangeLog(_tableLog);
            //Currentbalance的增添
            _tableLog = new Log_Auditrails();
            _tableLog.ActionType = "Update";
            _tableLog.TableName = "CRM_PROMOTERS";
            _tableLog.OperatorType = "Platform";
            _tableLog.OperatorId = "\\";
            _tableLog.OldValue = JsonConvert.SerializeObject(new { CurrentBalance = _oldPromoterCurrentBalance });
            _tableLog.NewValue = JsonConvert.SerializeObject(new { CurrentBalance = _newPromoterCurrentBalance });
            _logManager.WriteTableChangeLog(_tableLog);
            return true;
        }

    }
}
