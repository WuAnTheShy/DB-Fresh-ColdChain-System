using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;
using FreshGroupSystem.Repositories;
using FreshGroupSystem.Repositories.Order;
using FreshGroupSystem.Repositories.Leader;

namespace FreshGroupSystem.Services.Order;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepo;
    private readonly IProductRepository _productRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IGroupLeaderRepository _leaderRepo;
    private readonly IBaseRepository<OrderItem> _orderItemRepo;
    private readonly IUnitOfWork _uow;

    public OrderService(
        IOrderRepository orderRepo,
        IProductRepository productRepo,
        IInventoryRepository inventoryRepo,
        IGroupLeaderRepository leaderRepo,
        IUnitOfWork uow)
    {
        _orderRepo = orderRepo;
        _productRepo = productRepo;
        _inventoryRepo = inventoryRepo;
        _leaderRepo = leaderRepo;
        _uow = uow;
        // 使用泛型 BaseRepository 处理 OrderItem
        _orderItemRepo = new BaseRepository<OrderItem>(uow);
    }

    /// <summary>
    /// 创建订单：校验库��� → 锁定库存 → 创建订单 + 明细（事务保护）
    /// </summary>
    public async Task<ApiResponse<OrderResultDto>> CreateOrderAsync(CreateOrderDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return ApiResponse<OrderResultDto>.Fail("订单明细不能为空");

        // 校验团长
        var leader = await _leaderRepo.GetByIdAsync(dto.GroupLeaderId);
        if (leader == null)
            return ApiResponse<OrderResultDto>.Fail("团长不存在", 404);
        if (leader.Status != 1)
            return ApiResponse<OrderResultDto>.Fail("该团长已停用");

        decimal totalAmount = 0;
        var orderItems = new List<OrderItem>();

        try
        {
            await _uow.BeginAsync();

            foreach (var item in dto.Items)
            {
                var product = await _productRepo.GetByIdAsync(item.ProductId);
                if (product == null)
                    return ApiResponse<OrderResultDto>.Fail($"产品 ID={item.ProductId} 不存在");
                if (product.Status != 1)
                    return ApiResponse<OrderResultDto>.Fail($"产品「{product.Name}」已下架");

                // 检查库存
                var inventory = await _inventoryRepo.GetByProductIdAsync(item.ProductId);
                if (inventory == null || inventory.AvailableQuantity < item.Quantity)
                    return ApiResponse<OrderResultDto>.Fail($"产品「{product.Name}」库存不足");

                var subtotal = product.Price * item.Quantity;
                totalAmount += subtotal;

                orderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    Subtotal = subtotal
                });

                // 锁定库存
                inventory.LockedQuantity += item.Quantity;
                _inventoryRepo.Update(inventory);
            }

            // 创建订单
            var order = new Models.Order
            {
                OrderNo = GenerateOrderNo(),
                GroupLeaderId = dto.GroupLeaderId,
                TotalAmount = totalAmount,
                Status = 1, // 待确认
                Remark = dto.Remark
            };

            await _orderRepo.AddAsync(order);

            // 创建订单明细（设置 OrderId）
            foreach (var oi in orderItems)
            {
                oi.OrderId = order.Id;
                await _orderItemRepo.AddAsync(oi);
            }

            order.OrderItems = orderItems;
            order.GroupLeader = leader;

            await _uow.CommitAsync();

            return ApiResponse<OrderResultDto>.Success(MapToResultDto(order));
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<OrderResultDto>> GetOrderDetailAsync(int orderId)
    {
        var order = await _orderRepo.GetOrderDetailAsync(orderId);
        if (order == null)
            return ApiResponse<OrderResultDto>.Fail("订单不存在", 404);

        return ApiResponse<OrderResultDto>.Success(MapToResultDto(order));
    }

    public async Task<ApiResponse<PagedResult<OrderResultDto>>> GetOrdersAsync(int pageIndex, int pageSize, int? status = null)
    {
        var (items, total) = await _orderRepo.GetPagedWithDetailsAsync(pageIndex, pageSize, status);

        var result = new PagedResult<OrderResultDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(MapToResultDto).ToList()
        };

        return ApiResponse<PagedResult<OrderResultDto>>.Success(result);
    }

    public async Task<ApiResponse<List<OrderResultDto>>> GetOrdersByLeaderAsync(int leaderId)
    {
        var orders = await _orderRepo.GetOrdersByLeaderIdAsync(leaderId);
        var list = orders.Select(MapToResultDto).ToList();
        return ApiResponse<List<OrderResultDto>>.Success(list);
    }

    public async Task<ApiResponse> UpdateOrderStatusAsync(int orderId, int newStatus)
    {
        var order = await _orderRepo.GetByIdAsync(orderId);
        if (order == null)
            return ApiResponse.Fail("订单不存在", 404);

        order.Status = newStatus;
        order.UpdateTime = DateTime.Now;
        _orderRepo.Update(order);
        await _orderRepo.SaveChangesAsync();

        return ApiResponse.Success("订单状态更新成功");
    }

    public async Task<ApiResponse> CancelOrderAsync(int orderId)
    {
        var order = await _orderRepo.GetOrderDetailAsync(orderId);
        if (order == null)
            return ApiResponse.Fail("订单不存在", 404);
        if (order.Status != 1)
            return ApiResponse.Fail("只能取消待确认的订单");

        try
        {
            await _uow.BeginAsync();

            // 释放锁定库存
            foreach (var item in order.OrderItems)
            {
                var inventory = await _inventoryRepo.GetByProductIdAsync(item.ProductId);
                if (inventory != null)
                {
                    inventory.LockedQuantity -= item.Quantity;
                    _inventoryRepo.Update(inventory);
                }
            }

            order.Status = 5; // 已取消
            order.UpdateTime = DateTime.Now;
            _orderRepo.Update(order);

            await _uow.CommitAsync();

            return ApiResponse.Success("订单已取消");
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    // ========== 私有方法 ==========

    private static string GenerateOrderNo()
        => $"ORD{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";

    private static string GetStatusName(int status) => status switch
    {
        1 => "待确认",
        2 => "已确认",
        3 => "配送中",
        4 => "已完成",
        5 => "已取消",
        _ => "未知"
    };

    private static OrderResultDto MapToResultDto(Models.Order order)
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
