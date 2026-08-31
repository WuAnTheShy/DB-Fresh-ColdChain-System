using FreshColdChain.Models.CrossGroup;
using FreshColdChain.Models;
namespace FreshColdChain.Interfaces
{
    public interface ITableLogService
    {
        //表修改日志记录函数（独立连接，自建事务）
        Task<bool> WriteTableChangeLog(GroupC_LogAuditrails? logData = null);

        //组合查询操作日志（管理端）：时间区间 [startTime, endTime) + 表名 + 操作类型 + 操作者ID
        Task<List<GroupC_LogAuditrails>> SearchLogsAsync(DateTime? startTime, DateTime? endTime,
            string? tableName, string? actionType, string? operatorId);

        //查询日志中出现过的全部表名（筛选下拉用）
        Task<List<string>> GetLoggedTableNamesAsync();
    }
}
