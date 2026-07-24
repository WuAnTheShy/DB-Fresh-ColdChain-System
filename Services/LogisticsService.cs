using Microsoft.EntityFrameworkCore;
using FreshGroupSystem.Common;
using FreshGroupSystem.Data;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Services;

public class LogisticsService : ILogisticsService
{
    private readonly AppDbContext _context;

    public LogisticsService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取待配送订单（状态=已确认）
    /// </summary>
    public async Task<ApiResponse<List<OrderResultDto>>> GetPendingDeliveryOrdersAsync()
    {
        var orders = await _context.Orders
            .Include(o => o.GroupLeader)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.Status == 2) // 已确认，待配送
            .OrderBy(o => o.CreateTime)
            .ToListAsync();

        var list = orders.Select(MapToDto).ToList();
        return ApiResponse<List<OrderResultDto>>.Success(list);
    }

    /// <summary>
    /// 开始配送（状态 2→3）
    /// </summary>
    public async Task<ApiResponse> StartDeliveryAsync(int orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
            return ApiResponse.Fail("订单不存在", 404);
        if (order.Status != 2)
            return ApiResponse.Fail("当前订单状态不允许配送");

        order.Status = 3; // 配送中
        order.UpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();

        return ApiResponse.Success("配送已开始");
    }

    /// <summary>
    /// 配送完成（状态 3→4）
    /// </summary>
    public async Task<ApiResponse> CompleteDeliveryAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            return ApiResponse.Fail("订单不存在", 404);
        if (order.Status != 3)
            return ApiResponse.Fail("当前订单状态不允许完成配送");

        // 扣除实际库存
        foreach (var item in order.OrderItems)
        {
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId);
            if (inventory != null)
            {
                inventory.StockQuantity -= item.Quantity;
                inventory.LockedQuantity -= item.Quantity;
            }
        }

        order.Status = 4; // 已完成
        order.UpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();

        return ApiResponse.Success("配送已完成");
    }

    /// <summary>
    /// 某团长的配送汇总
    /// </summary>
    public async Task<ApiResponse<List<OrderResultDto>>> GetDeliverySummaryByLeaderAsync(int leaderId)
    {
        var orders = await _context.Orders
            .Include(o => o.GroupLeader)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.GroupLeaderId == leaderId && (o.Status == 3 || o.Status == 4))
            .OrderByDescending(o => o.CreateTime)
            .ToListAsync();

        var list = orders.Select(MapToDto).ToList();
        return ApiResponse<List<OrderResultDto>>.Success(list);
    }

    // ========== 私有方法 ==========
    private static string GetStatusName(int status) => status switch
    {
        1 => "待确认",
        2 => "已确认",
        3 => "配送中",
        4 => "已完成",
        5 => "已取消",
        _ => "未知"
    };

    private static OrderResultDto MapToDto(Models.Order order)
        => new()
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            GroupLeaderName = order.GroupLeader?.Name ?? "",
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            StatusName = GetStatusName(order.Status),
            Remark = order.Remark,
            CreateTime = order.CreateTime,
            Items = order.OrderItems.Select(oi => new OrderItemResultDto
            {
                ProductName = oi.Product?.Name ?? "",
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                Subtotal = oi.Subtotal
            }).ToList()
        };
}
