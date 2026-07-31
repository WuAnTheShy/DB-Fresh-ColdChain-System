using Dapper;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Order;

public class OrderRepository : BaseRepository<Models.Order>, IOrderRepository
{
    public OrderRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<Models.Order?> GetOrderDetailAsync(int orderId)
    {
        // 1. 查订单 + 团长
        var orderSql = """
            SELECT o.*, gl.*
            FROM "ORDER" o
            LEFT JOIN "GROUP_LEADER" gl ON o."GROUP_LEADER_ID" = gl."ID"
            WHERE o."ID" = :Id
            """;
        var order = (await _uow.Connection.QueryAsync<Models.Order, GroupLeader, Models.Order>(
            orderSql,
            (o, gl) => { o.GroupLeader = gl; return o; },
            new { Id = orderId },
            _uow.Transaction,
            splitOn: "ID"
        )).FirstOrDefault();

        if (order == null) return null;

        // 2. 查订单明细 + 产品
        var itemsSql = """
            SELECT oi.*, p.*
            FROM "ORDER_ITEM" oi
            LEFT JOIN "PRODUCT" p ON oi."PRODUCT_ID" = p."ID"
            WHERE oi."ORDER_ID" = :OrderId
            """;
        var items = await _uow.Connection.QueryAsync<OrderItem, Product, OrderItem>(
            itemsSql,
            (oi, prod) => { oi.Product = prod; return oi; },
            new { OrderId = orderId },
            _uow.Transaction,
            splitOn: "ID"
        );

        order.OrderItems = items.ToList();
        return order;
    }

    public async Task<List<Models.Order>> GetOrdersByLeaderIdAsync(int leaderId)
    {
        var sql = """
            SELECT o.*, gl.*
            FROM "ORDER" o
            LEFT JOIN "GROUP_LEADER" gl ON o."GROUP_LEADER_ID" = gl."ID"
            WHERE o."GROUP_LEADER_ID" = :LeaderId
            ORDER BY o."CREATE_TIME" DESC
            """;
        var orders = (await _uow.Connection.QueryAsync<Models.Order, GroupLeader, Models.Order>(
            sql,
            (o, gl) => { o.GroupLeader = gl; return o; },
            new { LeaderId = leaderId },
            _uow.Transaction,
            splitOn: "ID"
        )).ToList();

        // 加载订单明细
        foreach (var order in orders)
        {
            order.OrderItems = await GetOrderItemsAsync(order.Id);
        }

        return orders;
    }

    public async Task<List<Models.Order>> GetOrdersByStatusAsync(int status)
    {
        var sql = """
            SELECT o.*, gl.*
            FROM "ORDER" o
            LEFT JOIN "GROUP_LEADER" gl ON o."GROUP_LEADER_ID" = gl."ID"
            WHERE o."STATUS" = :Status
            ORDER BY o."CREATE_TIME" DESC
            """;
        var orders = (await _uow.Connection.QueryAsync<Models.Order, GroupLeader, Models.Order>(
            sql,
            (o, gl) => { o.GroupLeader = gl; return o; },
            new { Status = status },
            _uow.Transaction,
            splitOn: "ID"
        )).ToList();

        foreach (var order in orders)
        {
            order.OrderItems = await GetOrderItemsAsync(order.Id);
        }

        return orders;
    }

    public async Task<(List<Models.Order> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, int? status = null)
    {
        var where = status.HasValue ? """ WHERE o."STATUS" = :Status """ : "";
        var countSql = $"""SELECT COUNT(*) FROM "ORDER" o {where}""";
        var dataSql = $"""
            SELECT o.*, gl.*
            FROM "ORDER" o
            LEFT JOIN "GROUP_LEADER" gl ON o."GROUP_LEADER_ID" = gl."ID"
            {where}
            ORDER BY o."CREATE_TIME" DESC
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY
            """;

        var dp = new DynamicParameters();
        if (status.HasValue) dp.Add(":Status", status.Value);
        dp.Add(":Skip", (pageIndex - 1) * pageSize);
        dp.Add(":Take", pageSize);

        var total = await _uow.Connection.ExecuteScalarAsync<int>(countSql, dp, _uow.Transaction);

        var orders = (await _uow.Connection.QueryAsync<Models.Order, GroupLeader, Models.Order>(
            dataSql,
            (o, gl) => { o.GroupLeader = gl; return o; },
            dp,
            _uow.Transaction,
            splitOn: "ID"
        )).ToList();

        foreach (var order in orders)
        {
            order.OrderItems = await GetOrderItemsAsync(order.Id);
        }

        return (orders, total);
    }

    // ========== 私有帮助方法 ==========

    private async Task<List<OrderItem>> GetOrderItemsAsync(int orderId)
    {
        var sql = """
            SELECT oi.*, p.*
            FROM "ORDER_ITEM" oi
            LEFT JOIN "PRODUCT" p ON oi."PRODUCT_ID" = p."ID"
            WHERE oi."ORDER_ID" = :OrderId
            """;
        var items = await _uow.Connection.QueryAsync<OrderItem, Product, OrderItem>(
            sql,
            (oi, prod) => { oi.Product = prod; return oi; },
            new { OrderId = orderId },
            _uow.Transaction,
            splitOn: "ID"
        );
        return items.ToList();
    }
}
