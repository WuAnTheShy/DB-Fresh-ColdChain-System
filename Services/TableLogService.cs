using FreshColdChain.Interfaces;
using FreshColdChain.Repositories;
using FreshColdChain.Models;
namespace FreshColdChain.Services
{
    public class TableLogService : ITableLogService
    {
        private readonly ITableLogRepository _itableLogRepository;

        public TableLogService(ITableLogRepository itableLogRepository)
        {
           _itableLogRepository = itableLogRepository;    
        }
        public async Task<bool> WriteTableChangeLog(GroupC_LogAuditrails? logData = null)
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

            logData.OldValue ??= string.Empty;    // 如果 OldValue 为 null，赋值为空字符串
            logData.NewValue ??= string.Empty;    // 如果 NewValue 为 null，同样处理
            logData.OldValue = logData.OldValue.Length > 1000 ? logData.OldValue.Substring(0, 1000) : logData.OldValue; //截断保护
            await _itableLogRepository.GroupC_AddLogRecordAsync(logData); //Respositories层接口
            return true;
        }
        
    }
}
