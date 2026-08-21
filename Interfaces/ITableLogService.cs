using FreshColdChain.Models.CrossGroup;
using FreshColdChain.Models;
namespace FreshColdChain.Interfaces
{
    public interface ITableLogService
    {
        //表修改日志记录函数（独立连接，自建事务）
        Task<bool> WriteTableChangeLog(GroupC_LogAuditrails? logData = null);
    }
}
