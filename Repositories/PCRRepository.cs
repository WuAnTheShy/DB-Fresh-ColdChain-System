using Dapper;
using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
{
    public class PCRRepository : IPCRRepository
    {
        private readonly IUnitOfWork _uow;

        public PCRRepository(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<bool> InsertRelationAsync(string customerId, string promoterId, IDbTransaction? transaction = null)
        {
            const string sql = @"
                INSERT INTO CRM_PCR (CONSUMERID, PROMOTERID)
                VALUES (:CustomerId, :PromoterId)";
            var rows = await _uow.Connection.ExecuteAsync(
                sql,
                new { CustomerId = customerId, PromoterId = promoterId },
                transaction);
            return rows > 0;
        }

        public async Task<bool> ExistsRelationAsync(string customerId, string promoterId, IDbTransaction? transaction = null)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM CRM_PCR
                WHERE CONSUMERID = :CustomerId AND PROMOTERID = :PromoterId";
            var count = await _uow.Connection.ExecuteScalarAsync<int>(
                sql,
                new { CustomerId = customerId, PromoterId = promoterId },
                transaction);
            return count > 0;
        }

        public async Task<bool> DeleteRelationAsync(
            string customerId,
            string promoterId,
            IDbTransaction? transaction = null)
        {
            const string sql = @"
                DELETE FROM CRM_PCR
                WHERE CONSUMERID = :CustomerId AND PROMOTERID = :PromoterId";
            var rows = await _uow.Connection.ExecuteAsync(
                sql,
                new { CustomerId = customerId, PromoterId = promoterId },
                transaction);
            return rows > 0;
        }

        public async Task<List<string>> GetPromoterIdsByCustomerAsync(string customerId, IDbTransaction? transaction = null)
        {
            const string sql = @"
                SELECT PROMOTERID
                FROM CRM_PCR
                WHERE CONSUMERID = :CustomerId";
            var result = await _uow.Connection.QueryAsync<string>(
                sql,
                new { CustomerId = customerId },
                transaction);
            return result.ToList();
        }

        public async Task<List<GroupC_CrmPCRelation>> GetRelationsByPromoterAsync(string promoterId, IDbTransaction? transaction = null)
        {
            const string sql = @"
                SELECT
                    p.CONSUMERID AS CustomerId,
                    p.PROMOTERID AS PromoterId,
                    c.CUSTOMERNAME AS CustomerName,
                    c.PHONE AS Phone
                FROM CRM_PCR p
                LEFT JOIN CRM_CUSTOMERS c ON c.CUSTOMERID = p.CONSUMERID
                WHERE p.PROMOTERID = :PromoterId
                ORDER BY p.CONSUMERID";
            var result = await _uow.Connection.QueryAsync<GroupC_CrmPCRelation>(
                sql,
                new { PromoterId = promoterId },
                transaction);
            return result.ToList();
        }
    }
}
