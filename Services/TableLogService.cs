using FreshColdChain.Interfaces;
using FreshColdChain.Repositories;
using FreshColdChain.Models;
using System.Data;
namespace FreshColdChain.Services
{
    public class TableLogService : ITableLogService
    {
        private readonly ITableLogRepository _itableLogRepository;

        public TableLogService(ITableLogRepository itableLogRepository)
        {
           _itableLogRepository = itableLogRepository;    
        }
        public async Task<bool> WriteTableChangeLog(GroupC_LogAuditrails? logData = null,
            IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
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
            await _itableLogRepository.GroupC_AddLogRecordAsync(logData, cancellationToken, transaction);
            return true;
        }

        //组合查询操作日志（管理端查询页用）
        public async Task<List<GroupC_LogAuditrails>> SearchLogsAsync(DateTime? startTime, DateTime? endTime,
            string? tableName, string? actionType, string? operatorId)
        {
            return await _itableLogRepository.SearchAsync(startTime, endTime, tableName, actionType, operatorId);
        }

        //查询日志中出现过的全部表名（筛选下拉用）
        public async Task<List<string>> GetLoggedTableNamesAsync()
        {
            return await _itableLogRepository.GetDistinctTableNamesAsync();
        }
    }
}
