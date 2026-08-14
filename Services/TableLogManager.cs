using DBFreshColdChain.Models;
using DBFreshColdChain.Repositories;
using DBFreshColdChain.Interfaces;
using System;
using System.Text.Json.Nodes;
namespace DBFreshColdChain.Services
{
    public class TableLogManager: GroupC_ITableLogManager
    {
        private readonly DbHelper _dbHelper;

        public TableLogManager(DbHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }
        public bool WriteTableChangeLog(Log_Auditrails? logData = null)
        {
            if (logData == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(logData.LogId))
            {
                logData.LogId = "LOG_" + Guid.NewGuid().ToString("N"); //自动生成日志编号
            }
            if (logData.OpTime == null)
            {
                logData.OpTime = DateTime.Now;
            }
            logData.OldValue = logData.OldValue.Length > 1000 ? logData.OldValue.Substring(0, 1000) : logData.OldValue; //截断保护
            _dbHelper.GroupC_AddLogRecord(logData); //Respositories层接口
            return true;
        }
        
    }
}
