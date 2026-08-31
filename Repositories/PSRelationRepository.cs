using Dapper;
using FreshColdChainSystem.Repositories;
using FreshColdChain.Repositories;
using System.Data;

namespace FreshColdChain.Repositories
{
    public class PromoterSupplierRepository : IPromoterSupplierRepository
    {
        private readonly IUnitOfWork _uow;

        public PromoterSupplierRepository(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<bool> AddOrUpdateRelationAsync(string promoterId, string supplierId, string status = "Active", IDbTransaction? transaction = null)
        {
            const string sql = @"
                MERGE INTO  CRM_PSRELATION T
                USING (SELECT :PromoterId AS PROMOTERID, :SupplierId AS SUPPLIERID FROM DUAL) S
                ON (T.PROMOTERID = S.PROMOTERID AND T.SUPPLIERID = S.SUPPLIERID)
                WHEN MATCHED THEN
                    UPDATE SET STATUS = :Status, UPDATETIME = SYSDATE
                WHEN NOT MATCHED THEN
                    INSERT (PROMOTERID, SUPPLIERID, STATUS, CREATETIME)
                    VALUES (S.PROMOTERID, S.SUPPLIERID, :Status, SYSDATE)";
            var rows = await _uow.Connection.ExecuteAsync(sql, new { PromoterId = promoterId, SupplierId = supplierId, Status = status }, transaction);
            return rows > 0;
        }

        public async Task<bool> SoftDeleteRelationAsync(string promoterId, string supplierId, IDbTransaction? transaction = null)
        {
            const string sql = @"
                UPDATE CRM_PSRELATION
                SET STATUS = 'Inactive', UPDATETIME = SYSDATE
                WHERE PROMOTERID = :PromoterId AND SUPPLIERID = :SupplierId";
            var rows = await _uow.Connection.ExecuteAsync(sql, new { PromoterId = promoterId, SupplierId = supplierId }, transaction);
            return rows > 0;
        }

        public async Task<List<string>> GetActiveSupplierIdsByPromoterAsync(string promoterId, IDbTransaction? transaction = null)
        {
            const string sql = @"
                SELECT SUPPLIERID
                FROM  CRM_PSRELATION
                WHERE PROMOTERID = :PromoterId AND STATUS = 'Active'";
            var result = await _uow.Connection.QueryAsync<string>(sql, new { PromoterId = promoterId }, transaction);
            return result.ToList();
        }

        public async Task<Dictionary<string, bool>> ValidateRelationsAsync(string promoterId, List<string> supplierIds, IDbTransaction? transaction = null)
        {
            if (supplierIds == null || !supplierIds.Any())
                return new Dictionary<string, bool>();

            var parameters = new
            {
                PromoterId = promoterId,
                SupplierIds = supplierIds.ToArray()
            };

            const string sql = @"
                SELECT SUPPLIERID, STATUS
                FROM  CRM_PSRELATION
                WHERE PROMOTERID = :PromoterId
                  AND SUPPLIERID IN :SupplierIds
                  AND STATUS = 'Active'";
            var activeList = await _uow.Connection.QueryAsync<(string SupplierId, string Status)>(sql, parameters, transaction);
            var activeSet = activeList.Select(x => x.SupplierId).ToHashSet();
            return supplierIds.ToDictionary(sid => sid, sid => activeSet.Contains(sid));
        }

        public async Task<bool> IsRelationActiveAsync(string promoterId, string supplierId, IDbTransaction? transaction = null)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM CRM_PSRELATION
                WHERE PROMOTERID = :PromoterId AND SUPPLIERID = :SupplierId AND STATUS = 'Active'";
            var count = await _uow.Connection.ExecuteScalarAsync<int>(sql, new { PromoterId = promoterId, SupplierId = supplierId }, transaction);
            return count > 0;
        }
    }
}
