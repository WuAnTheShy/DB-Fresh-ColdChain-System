using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IPointRepository
{
    Task InsertLogAsync(CrmPointLog log, IDbTransaction? transaction = null);
    Task<bool> HasPointLogAsync(
        string customerId,
        string orderId,
        string changeType,
        IDbTransaction? transaction = null);
    Task<List<CrmMemberLevel>> GetAllLevelsAsync(IDbTransaction? transaction = null);
    Task<CrmMemberLevel?> GetLevelByIdAsync(
        string memberLevelId,
        IDbTransaction? transaction = null);

    Task<CrmMemberLevel?> GetLevelForSpentAsync(
        decimal totalSpent,
        IDbTransaction? transaction = null);
}
