using DBFreshColdChain.Models.DTOs;
using System.Data;

namespace DBFreshColdChain.Repositories
{
    public interface ITableLogRepository
    {
        Task GroupC_AddLogRecordAsync(GroupC_LogAuditrails logData, CancellationToken cancellationToken = default); //添加日志
    }

}