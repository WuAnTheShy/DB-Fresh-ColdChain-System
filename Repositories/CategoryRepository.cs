using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public class CategoryRepository : BaseRepository<InvCategory>, ICategoryRepository
{
    public CategoryRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<List<InvCategory>> GetSubCategoriesAsync(string? parentId)
    {
        if (string.IsNullOrEmpty(parentId))
        {
            var sql = """SELECT * FROM Inv_Category WHERE ParentID IS NULL ORDER BY CategoryID """;
            return (await _uow.Connection.QueryAsync<InvCategory>(sql, transaction: _uow.Transaction)).ToList();
        }
        else
        {
            var sql = """SELECT * FROM Inv_Category WHERE ParentID = :Id ORDER BY CategoryID """;
            return (await _uow.Connection.QueryAsync<InvCategory>(sql, new { Id = parentId }, _uow.Transaction)).ToList();
        }
    }
}
