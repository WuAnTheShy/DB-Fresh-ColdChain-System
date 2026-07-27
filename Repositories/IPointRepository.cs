using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IPointRepository
{
    Task InsertLogAsync(CrmPointLog log, IDbTransaction? transaction = null);
    Task<List<CrmMemberLevel>> GetAllLevelsAsync(IDbTransaction? transaction = null);
    Task<CrmMemberLevel?> GetLevelByIdAsync(
        int memberLevelId,
        IDbTransaction? transaction = null);

    Task<CrmMemberLevel?> GetLevelForSpentAsync(
        decimal totalSpent,
        IDbTransaction? transaction = null);
}
