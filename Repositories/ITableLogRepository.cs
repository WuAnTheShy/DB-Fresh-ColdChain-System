using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
{
    public interface ITableLogRepository
    {
        Task GroupC_AddLogRecordAsync(GroupC_LogAuditrails logData,
            CancellationToken cancellationToken = default, IDbTransaction? transaction = null); //添加日志

        //组合查询操作日志（管理端）：时间区间 [startTime, endTime) + 表名 + 操作类型 + 操作者ID，按操作时间倒序
        Task<List<GroupC_LogAuditrails>> SearchAsync(DateTime? startTime, DateTime? endTime,
            string? tableName, string? actionType, string? operatorId);

        //查询日志中出现过的全部表名（筛选下拉用）
        Task<List<string>> GetDistinctTableNamesAsync();
    }

}
