using Dapper;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Leader;

public class GroupLeaderRepository : BaseRepository<GroupLeader>, IGroupLeaderRepository
{
    public GroupLeaderRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<GroupLeader?> GetLeaderWithOrdersAsync(int leaderId)
    {
        var leader = await GetByIdAsync(leaderId);
        if (leader == null) return null;

        // 查该团长的所有订单 + 订单明细
        var ordersSql = """
            SELECT * FROM "ORDER" WHERE "GROUP_LEADER_ID" = :LeaderId ORDER BY "CREATE_TIME" DESC
            """;
        var orders = (await _uow.Connection.QueryAsync<Models.Order>(
            ordersSql,
            new { LeaderId = leaderId },
            _uow.Transaction
        )).ToList();

        // 查每个订单的明细
        foreach (var order in orders)
        {
            var itemsSql = """
                SELECT oi.*, p.*
                FROM "ORDER_ITEM" oi
                LEFT JOIN "PRODUCT" p ON oi."PRODUCT_ID" = p."ID"
                WHERE oi."ORDER_ID" = :OrderId
                """;
            var items = await _uow.Connection.QueryAsync<OrderItem, Product, OrderItem>(
                itemsSql,
                (oi, prod) => { oi.Product = prod; return oi; },
                new { OrderId = order.Id },
                _uow.Transaction,
                splitOn: "ID"
            );
            order.OrderItems = items.ToList();
        }

        leader.Orders = orders;
        return leader;
    }

    public async Task<(List<GroupLeader> Items, int Total)> GetPagedWithOrderCountAsync(int pageIndex, int pageSize)
    {
        var countSql = """SELECT COUNT(*) FROM "GROUP_LEADER" """;
        var dataSql = $"""
            SELECT gl.*, COUNT(o."ID") AS OrderCount
            FROM "GROUP_LEADER" gl
            LEFT JOIN "ORDER" o ON gl."ID" = o."GROUP_LEADER_ID"
            GROUP BY gl."ID", gl."NAME", gl."PHONE", gl."COMMUNITY_NAME", gl."ADDRESS", gl."STATUS", gl."CREATE_TIME"
            ORDER BY gl."ID"
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY
            """;

        var total = await _uow.Connection.ExecuteScalarAsync<int>(countSql, transaction: _uow.Transaction);

        var items = await _uow.Connection.QueryAsync<GroupLeader>(dataSql,
            new { Skip = (pageIndex - 1) * pageSize, Take = pageSize },
            _uow.Transaction);

        return (items.ToList(), total);
    }
}
