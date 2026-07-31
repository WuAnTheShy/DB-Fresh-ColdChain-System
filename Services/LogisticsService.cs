using FreshGroupSystem.Common;
using FreshGroupSystem.Data;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;
using FreshGroupSystem.Repositories;
using FreshGroupSystem.Repositories.Order;

namespace FreshGroupSystem.Services;

public class LogisticsService : ILogisticsService
{
    private readonly IOrderRepository _orderRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IUnitOfWork _uow;

    public LogisticsService(
        IOrderRepository orderRepo,
        IInventoryRepository inventoryRepo,
        IUnitOfWork uow)
    {
        _orderRepo = orderRepo;
        _inventoryRepo = inventoryRepo;
        _uow = uow;
    }

    /// <summary>
    /// 获取待配送订单（状态=已确认）
    /// </summary>
    public async Task<ApiResponse<List<OrderResultDto>>> GetPendingDeliveryOrdersAsync()
    {
        var orders = await _orderRepo.GetOrdersByStatusAsync(2); // 已确认，待配送
        var list = orders.Select(MapToDto).ToList();
        return ApiResponse<List<OrderResultDto>>.Success(list);
    }

    /// <summary>
    /// 开始配送（状态 2→3）
    /// </summary>
    public async Task<ApiResponse> StartDeliveryAsync(int orderId)
    {
        var order = await _orderRepo.GetByIdAsync(orderId);
        if (order == null)
            return ApiResponse.Fail("订单不存在", 404);
        if (order.Status != 2)
            return ApiResponse.Fail("当前订单状态不允许配送");

        order.Status = 3; // 配送中
        order.UpdateTime = DateTime.Now;
        _orderRepo.Update(order);

        return ApiResponse.Success("配送已开始");
    }

    /// <summary>
    /// 配送完成（状态 3→4）：扣除实际库存（事务保护）
    /// </summary>
    public async Task<ApiResponse> CompleteDeliveryAsync(int orderId)
    {
        var order = await _orderRepo.GetOrderDetailAsync(orderId);
        if (order == null)
            return ApiResponse.Fail("订单不存在", 404);
        if (order.Status != 3)
            return ApiResponse.Fail("当前订单状态不允许完成配送");

        try
        {
            await _uow.BeginAsync();

            foreach (var item in order.OrderItems)
            {
                var inventory = await _inventoryRepo.GetByProductIdAsync(item.ProductId);
                if (inventory != null)
                {
                    if (inventory.StockQuantity < item.Quantity)
                    {
                        await _uow.RollbackAsync();
                        return ApiResponse.Fail($"产品「{item.Product?.Name ?? item.ProductId.ToString()}」库存不足，无法完成配送");
                    }
                    inventory.StockQuantity -= item.Quantity;
                    inventory.LockedQuantity -= item.Quantity;
                    _inventoryRepo.Update(inventory);
                }
            }

            order.Status = 4; // 已完成
            order.UpdateTime = DateTime.Now;
            _orderRepo.Update(order);

            await _uow.CommitAsync();

            return ApiResponse.Success("配送已完成");
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 某团长的配送汇总
    /// </summary>
    public async Task<ApiResponse<List<OrderResultDto>>> GetDeliverySummaryByLeaderAsync(int leaderId)
    {
        var orders = await _orderRepo.GetOrdersByLeaderIdAsync(leaderId);
        var list = orders
            .Where(o => o.Status == 3 || o.Status == 4)
            .Select(MapToDto)
            .ToList();
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
