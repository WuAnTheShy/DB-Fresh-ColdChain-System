using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public class SupplierPriceRepository : BaseRepository<InvSupplierPrice>, ISupplierPriceRepository
{
    public SupplierPriceRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<InvSupplierPrice?> GetQuoteAsync(string supplierId, string productId)
    {
        var sql = """SELECT * FROM Inv_SupplierPrices WHERE SupplierID = :SupplierId AND ProductID = :ProductId """;
        return await _uow.Connection.QuerySingleOrDefaultAsync<InvSupplierPrice>(sql,
            new { SupplierId = supplierId, ProductId = productId }, _uow.Transaction);
    }

    public async Task<List<InvSupplierPrice>> GetQuotesBySupplierAsync(string supplierId)
    {
        var sql = """SELECT * FROM Inv_SupplierPrices WHERE SupplierID = :SupplierId ORDER BY ProductID """;
        return (await _uow.Connection.QueryAsync<InvSupplierPrice>(sql,
            new { SupplierId = supplierId }, _uow.Transaction)).ToList();
    }
}
