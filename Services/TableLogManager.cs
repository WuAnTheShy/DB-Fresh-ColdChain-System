using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Repositories;
using FreshColdChain.Repositories;
using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;
using System;
using System.Text.Json.Nodes;
namespace DBFreshColdChain.Services
{
    public class GroupC_TableLogManager : GroupC_ITableLogManager
    {
        private readonly IUnitOfWork _uow;
        private readonly TableLogRepository _tableLogRepository;

        public GroupC_TableLogManager(IUnitOfWork uow, TableLogRepository tableLogRepository)
        {
            _uow = uow;
           _tableLogRepository = tableLogRepository;    
        }
        public bool WriteTableChangeLog(GroupC_LogAuditrails? logData = null)
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
            _tableLogRepository.GroupC_AddLogRecord(logData); //Respositories层接口
            return true;
        }
        
    }
}
