using FreshColdChain.Models.CrossGroup;
using FreshColdChain.Models;
using System.Data;
namespace FreshColdChain.Interfaces
{
    public interface ITableLogService
    {
        // 有外部事务时与业务共同提交；未传事务时保留独立写入方式。
        Task<bool> WriteTableChangeLog(GroupC_LogAuditrails? logData = null,
            IDbTransaction? transaction = null, CancellationToken cancellationToken = default);

        //组合查询操作日志（管理端）：时间区间 [startTime, endTime) + 表名 + 操作类型 + 操作者ID
        Task<List<GroupC_LogAuditrails>> SearchLogsAsync(DateTime? startTime, DateTime? endTime,
            string? tableName, string? actionType, string? operatorId);

        //查询日志中出现过的全部表名（筛选下拉用）
        Task<List<string>> GetLoggedTableNamesAsync();
    }
}
