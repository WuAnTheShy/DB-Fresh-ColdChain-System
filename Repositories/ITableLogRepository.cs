using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
{
    public interface ITableLogRepository
    {
        Task GroupC_AddLogRecordAsync(GroupC_LogAuditrails logData, CancellationToken cancellationToken = default); //添加日志
    }

}