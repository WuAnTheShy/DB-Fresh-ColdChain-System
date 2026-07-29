using DBFreshColdChain.Models;
using DBFreshColdChain.Repositories;
using Newtonsoft.Json;
namespace DBFreshColdChain.Services
{
    public class PromoterManager: GroupC_IProMonterManager
    {
        //Repository层句柄
        private readonly DbHelper _dbHelper;
        //Interface层句柄
        private readonly GroupC_ITableLogManager _iTableLogManager;
        //构造函数
        public PromoterManager(DbHelper dbHelper, GroupC_ITableLogManager iTableLogManager)
        {
            _dbHelper = dbHelper;
            _iTableLogManager = iTableLogManager;
        }
        protected bool FindPromoterInfo(string promoterID, ref CrmPromoter promoterInfo)    //查找团长信息函数
        {
            if(promoterID == string.Empty || promoterID == null) //空值查找无效
                return false;
            var _promoterInfo = _dbHelper.GroupC_FindPromoterRecord(promoterID);
            if(_promoterInfo == null )
                return false;
            return true;
        }

        public bool CommisionSettlement(string promoterID, decimal finalAmount, decimal goodsAmount,ref CommissionInfo commissionInfo)  //结算佣金总业务函数
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
            _dbHelper.GroupC_AddPromoterTotalSales(promoterID, goodsAmount);
            var _newTotalSales = _promoterInfo.TotalSales;

            //产生日志信息
            var _tableLog = new Log_Auditrails();
            _tableLog.ActionType = "Update";
            _tableLog.TableName = "CRM_PROMOTERS";
            _tableLog.OperatorType = "Platform";
            _tableLog.OperatorId = "\\";
            _tableLog.OldValue = JsonConvert.SerializeObject(new { TotalSales = _oldTotalSales });
            _tableLog.NewValue = JsonConvert.SerializeObject(new { TotalSales = _newTotalSales });
            _iTableLogManager.WriteTableChangeLog(_tableLog);
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
            var _newPromoterPendingBalance = _dbHelper.GroupC_FindPromoterPendingBalance(promoterID);
            //产生日志信息
            _tableLog = new Log_Auditrails();
            _tableLog.ActionType = "Update";
            _tableLog.TableName = "CRM_PROMOTERS";
            _tableLog.OperatorType = "Platform";
            _tableLog.OperatorId = "\\";
            _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
            _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
            _iTableLogManager.WriteTableChangeLog(_tableLog);
            //时间获取
            commissionInfo.CommSettlementDate = DateTime.Now;
            return true;
        }
       


    }
}
