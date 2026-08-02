using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 订单服务 - 负责下单事务、服务端计价、优惠券核销和积分资产闭环。
/// </summary>
public sealed class OrderService : IOrderService
{
    private const decimal MaxOrderAmount = 99_999_999.99m;

    private readonly IOrderRepository _orderRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly ICouponRepository _couponRepo;
    private readonly IPointRepository _pointRepo;
    private readonly IInventoryService _inventoryService;
    private readonly ILogisticsService _logisticsService;
    private readonly ICommissionService _commissionService;
    private readonly IOrderTransactionManager _transactionManager;

    public OrderService(
        IOrderRepository orderRepo,
        ICustomerRepository customerRepo,
        ICouponRepository couponRepo,
        IPointRepository pointRepo,
        IInventoryService inventoryService,
        ILogisticsService logisticsService,
        ICommissionService commissionService,
        IOrderTransactionManager transactionManager)
    {
        _orderRepo = orderRepo;
        _customerRepo = customerRepo;
        _couponRepo = couponRepo;
        _pointRepo = pointRepo;
        _inventoryService = inventoryService;
        _logisticsService = logisticsService;
        _commissionService = commissionService;
        _transactionManager = transactionManager;
    }

    /// <summary>
    /// 创建订单。所有金额和商品快照均由服务端生成，客户端提交的只有标识和数量。
    /// </summary>
    public async Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var reservationItems = ValidateAndNormalizeRequest(request);

        return await _transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var customer = await _customerRepo.GetByIdForUpdateAsync(
                    request.CustomerId,
                    transaction)
                ?? throw new OrderBusinessException("消费者不存在，无法创建订单");

            var address = await _customerRepo.GetAddressAsync(
                    request.CustomerId,
                    request.AddressId,
                    transaction)
                ?? throw new OrderBusinessException(
                    "收货地址不存在或不属于当前消费者");

            var productSnapshots = await _inventoryService.ReserveAsync(
                reservationItems,
                transaction,
                cancellationToken);
            var details = CreateTrustedDetails(reservationItems, productSnapshots);
            var goodsAmount = details.Sum(detail => detail.SubTotal);
            EnsureAmountFitsDatabase(goodsAmount);

            var coupon = await GetCouponAsync(
                request.CouponRecordId,
                request.CustomerId,
                goodsAmount,
                transaction);
            var discountAmount = coupon == null
                ? 0m
                : Math.Min(coupon.DiscountAmount, goodsAmount);
            var freightAmount = await _logisticsService.CalculateFreightAsync(
                new FreightCalculationRequest
                {
                    CustomerId = customer.CustomerId,
                    Province = address.Province,
                    City = address.City,
                    District = address.District,
                    GoodsAmount = goodsAmount,
                    Items = CreateFulfillmentItems(details)
                },
                transaction,
                cancellationToken);
            EnsureAmountFitsDatabase(freightAmount);
            var finalAmount = goodsAmount - discountAmount + freightAmount;
            EnsureAmountFitsDatabase(finalAmount);

            var pointsMultiplier = await GetPointsMultiplierAsync(
                customer.MemberLevelId,
                transaction);
            var pointsEarned = CalculatePoints(finalAmount, pointsMultiplier);

            var order = new BizOrder
            {
                OrderNo = GenerateOrderNo(),
                CustomerId = request.CustomerId,
                PromoterId = customer.PromoterId,
                AddressId = request.AddressId,
                ReceiverName = address.ReceiverName,
                ReceiverPhone = address.Phone,
                ShippingAddress = CreateShippingAddress(address),
                TotalAmount = goodsAmount,
                DiscountAmount = discountAmount,
                FreightAmount = freightAmount,
                FinalAmount = finalAmount,
                PointsEarned = pointsEarned,
                OrderStatus = 1,
                CreatedAt = DateTime.Now
            };

            var orderId = await _orderRepo.CreateOrderAsync(order, transaction);
            foreach (var detail in details)
                detail.OrderId = orderId;
            await _orderRepo.InsertDetailsAsync(details, transaction);

            if (coupon != null)
            {
                var couponUsed = await _couponRepo.TryUseCouponAsync(
                    coupon.RecordId,
                    request.CustomerId,
                    orderId,
                    transaction);
                if (!couponUsed)
                    throw new OrderBusinessException("优惠券已被使用，请重新选择");
            }

            if (pointsEarned > 0)
            {
                var newPoints = checked(customer.Points + pointsEarned);
                await _customerRepo.UpdatePointsAsync(
                    customer.CustomerId,
                    newPoints,
                    transaction);
                await _pointRepo.InsertLogAsync(new CrmPointLog
                {
                    CustomerId = customer.CustomerId,
                    ChangeAmount = pointsEarned,
                    BalanceAfter = newPoints,
                    ChangeType = "ORDER_EARN",
                    OrderId = orderId
                }, transaction);
            }

            await _customerRepo.UpdateTotalSpentAsync(
                customer.CustomerId,
                finalAmount,
                transaction);
            var newTotalSpent = customer.TotalSpent + finalAmount;
            var qualifiedLevel = await _pointRepo.GetLevelForSpentAsync(
                newTotalSpent,
                transaction);
            if (qualifiedLevel != null &&
                qualifiedLevel.MemberLevelId != customer.MemberLevelId)
            {
                await _customerRepo.UpdateMemberLevelAsync(
                    customer.CustomerId,
                    qualifiedLevel.MemberLevelId,
                    transaction);
            }

            return new CreateOrderResult
            {
                OrderId = orderId,
                OrderNo = order.OrderNo,
                GoodsAmount = goodsAmount,
                DiscountAmount = discountAmount,
                FreightAmount = freightAmount,
                FinalAmount = finalAmount,
                PointsEarned = pointsEarned,
                SupplierGroups = CreateSupplierGroups(details)
            };
        });
    }

    public async Task<OrderListViewModel> GetOrdersAsync(OrderQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        NormalizeOrderQuery(request);

        var totalCount = await _orderRepo.CountOrdersAsync(request);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (decimal)request.PageSize);
        if (totalPages > 0 && request.Page > totalPages)
            request.Page = totalPages;

        var offset = checked((request.Page - 1) * request.PageSize);
        return new OrderListViewModel
        {
            Query = request,
            Orders = await _orderRepo.GetOrdersAsync(request, offset),
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<OrderDetailViewModel?> GetOrderDetailAsync(int orderId)
    {
        if (orderId <= 0)
            return null;

        var header = await _orderRepo.GetDetailHeaderAsync(orderId);
        if (header == null)
            return null;

        var details = await _orderRepo.GetDetailsAsync(orderId);
        var supplierIds = details
            .Where(detail => detail.SupplierId.HasValue)
            .Select(detail => detail.SupplierId!.Value)
            .Distinct()
            .OrderBy(supplierId => supplierId)
            .ToList();
        var fulfillmentStatuses =
            await _logisticsService.GetSupplierStatusesAsync(
                orderId,
                supplierIds);
        var status = (OrderStatus)header.OrderStatus;
        return new OrderDetailViewModel
        {
            OrderId = orderId,
            Order = header.ToOrder(),
            CustomerName = header.CustomerName,
            Details = details,
            SupplierGroups = CreateSupplierGroupViewModels(
                details,
                fulfillmentStatuses),
            CanShip = OrderStateMachine.CanTransition(
                status,
                OrderStatus.Shipped),
            CanComplete = OrderStateMachine.CanTransition(
                status,
                OrderStatus.Completed),
            CanCancel = OrderStateMachine.CanTransition(
                status,
                OrderStatus.Cancelled)
        };
    }

    public async Task TransitionOrderAsync(
        int orderId,
        OrderStatus targetStatus,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
            throw new OrderBusinessException("订单ID必须大于0");
        if (targetStatus is not (OrderStatus.Shipped or OrderStatus.Completed))
            throw new OrderBusinessException("目标订单状态不受此操作支持");

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = await GetLockedOrderContextAsync(orderId, transaction);
            var currentStatus = (OrderStatus)context.Order.OrderStatus;
            OrderStateMachine.EnsureTransition(currentStatus, targetStatus);

            if (targetStatus == OrderStatus.Shipped)
            {
                await _logisticsService.CreateShipmentAsync(
                    CreateFulfillmentOrderRequest(
                        context.Order,
                        context.Details),
                    transaction,
                    cancellationToken);
            }
            else
            {
                await _commissionService.RegisterCompletedOrderAsync(
                    new CommissionOrderRequest
                    {
                        OrderId = context.Order.OrderId,
                        OrderNo = context.Order.OrderNo,
                        CustomerId = context.Customer.CustomerId,
                        PromoterId = context.Customer.PromoterId,
                        CommissionBaseAmount = context.Order.FinalAmount,
                        CompletedAt = DateTime.Now
                    },
                    transaction,
                    cancellationToken);
            }

            if (!await _orderRepo.TryUpdateStatusAsync(
                orderId,
                currentStatus,
                targetStatus,
                transaction))
            {
                throw new OrderBusinessException("订单状态已变化，请刷新后重试");
            }
        });
    }

    public async Task CancelOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
            throw new OrderBusinessException("订单ID必须大于0");

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = await GetLockedOrderContextAsync(orderId, transaction);
            var currentStatus = (OrderStatus)context.Order.OrderStatus;
            OrderStateMachine.EnsureTransition(
                currentStatus,
                OrderStatus.Cancelled);

            await _inventoryService.ReleaseAsync(
                CreateFulfillmentOrderRequest(
                    context.Order,
                    context.Details),
                transaction,
                cancellationToken);

            if (context.Order.PointsEarned > 0)
            {
                if (context.Customer.Points < context.Order.PointsEarned)
                {
                    throw new OrderBusinessException(
                        "当前积分不足以撤销该订单奖励，请联系管理员处理");
                }

                var newPoints = context.Customer.Points - context.Order.PointsEarned;
                await _customerRepo.UpdatePointsAsync(
                    context.Customer.CustomerId,
                    newPoints,
                    transaction);
                await _pointRepo.InsertLogAsync(new CrmPointLog
                {
                    CustomerId = context.Customer.CustomerId,
                    ChangeAmount = -context.Order.PointsEarned,
                    BalanceAfter = newPoints,
                    ChangeType = "ORDER_CANCEL",
                    OrderId = context.Order.OrderId
                }, transaction);
            }

            if (!await _customerRepo.TrySubtractTotalSpentAsync(
                context.Customer.CustomerId,
                context.Order.FinalAmount,
                transaction))
            {
                throw new OrderBusinessException(
                    "累计消费金额不足以撤销该订单，请联系管理员处理");
            }

            _ = await _couponRepo.RestoreCouponForCancelledOrderAsync(
                context.Order.OrderId,
                context.Customer.CustomerId,
                transaction);

            var newTotalSpent =
                context.Customer.TotalSpent - context.Order.FinalAmount;
            var qualifiedLevel = await _pointRepo.GetLevelForSpentAsync(
                newTotalSpent,
                transaction);
            if (qualifiedLevel != null &&
                qualifiedLevel.MemberLevelId != context.Customer.MemberLevelId)
            {
                await _customerRepo.UpdateMemberLevelAsync(
                    context.Customer.CustomerId,
                    qualifiedLevel.MemberLevelId,
                    transaction);
            }

            if (!await _orderRepo.TryUpdateStatusAsync(
                orderId,
                currentStatus,
                OrderStatus.Cancelled,
                transaction))
            {
                throw new OrderBusinessException("订单状态已变化，请刷新后重试");
            }
        });
    }

    /// <summary>
    /// 退款时扣回积分 - 供 C 组调用。
    /// </summary>
    public async Task DeductPointsForRefundAsync(
        int customerId,
        int orderId,
        int pointsToDeduct)
    {
        if (customerId <= 0)
            throw new OrderBusinessException("消费者ID必须大于0");
        if (orderId <= 0)
            throw new OrderBusinessException("订单ID必须大于0");
        if (pointsToDeduct <= 0)
            throw new OrderBusinessException("扣回积分必须大于0");

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            var customer = await _customerRepo.GetByIdForUpdateAsync(
                    customerId,
                    transaction)
                ?? throw new OrderBusinessException("消费者不存在");

            var actualDeduction = Math.Min(customer.Points, pointsToDeduct);
            if (actualDeduction == 0)
                return;

            var newPoints = customer.Points - actualDeduction;
            await _customerRepo.UpdatePointsAsync(customerId, newPoints, transaction);
            await _pointRepo.InsertLogAsync(new CrmPointLog
            {
                CustomerId = customerId,
                ChangeAmount = -actualDeduction,
                BalanceAfter = newPoints,
                ChangeType = "REFUND_DEDUCT",
                OrderId = orderId
            }, transaction);
        });
    }

    /// <summary>
    /// 根据累计消费金额动态查询消费者应处的最高会员等级。
    /// </summary>
    public async Task<CrmMemberLevel?> GetCustomerLevelAsync(int customerId)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId);
        if (customer == null)
            return null;

        return await _pointRepo.GetLevelForSpentAsync(customer.TotalSpent);
    }

    private static IReadOnlyList<InventoryReservationItem> ValidateAndNormalizeRequest(
        CreateOrderRequest request)
    {
        if (request.CustomerId <= 0)
            throw new OrderBusinessException("消费者ID必须大于0");
        if (request.AddressId <= 0)
            throw new OrderBusinessException("收货地址ID必须大于0");
        if (request.CouponRecordId <= 0)
            throw new OrderBusinessException("优惠券记录ID必须大于0");
        if (request.Items == null || request.Items.Count == 0)
            throw new OrderBusinessException("订单至少需要一件商品");

        var quantities = new Dictionary<int, int>();
        foreach (var item in request.Items)
        {
            if (item.ProductId <= 0)
                throw new OrderBusinessException("商品ID必须大于0");
            if (item.Quantity is <= 0 or > 9999)
                throw new OrderBusinessException("商品数量必须在1到9999之间");

            quantities.TryGetValue(item.ProductId, out var currentQuantity);
            var mergedQuantity = checked(currentQuantity + item.Quantity);
            if (mergedQuantity > 9999)
                throw new OrderBusinessException($"商品 {item.ProductId} 的合计数量不能超过9999");
            quantities[item.ProductId] = mergedQuantity;
        }

        return quantities
            .OrderBy(pair => pair.Key)
            .Select(pair => new InventoryReservationItem
            {
                ProductId = pair.Key,
                Quantity = pair.Value
            })
            .ToList();
    }

    private static List<BizOrderDetail> CreateTrustedDetails(
        IReadOnlyList<InventoryReservationItem> reservationItems,
        IReadOnlyList<InventoryProductSnapshot> productSnapshots)
    {
        if (productSnapshots.Count != reservationItems.Count)
            throw new OrderBusinessException("库存服务返回的商品数据不完整");

        var snapshotsByProductId = new Dictionary<int, InventoryProductSnapshot>();
        foreach (var snapshot in productSnapshots)
        {
            if (!snapshotsByProductId.TryAdd(snapshot.ProductId, snapshot))
                throw new OrderBusinessException("库存服务返回了重复商品");
            if (string.IsNullOrWhiteSpace(snapshot.ProductName))
                throw new OrderBusinessException($"商品 {snapshot.ProductId} 缺少名称");
            if (snapshot.SupplierId <= 0)
                throw new OrderBusinessException($"商品 {snapshot.ProductId} 缺少有效供应商");
            if (snapshot.UnitPrice <= 0 || snapshot.UnitPrice > MaxOrderAmount)
                throw new OrderBusinessException($"商品 {snapshot.ProductId} 的价格无效");
        }

        var details = new List<BizOrderDetail>(reservationItems.Count);
        foreach (var item in reservationItems)
        {
            if (!snapshotsByProductId.TryGetValue(item.ProductId, out var snapshot))
                throw new OrderBusinessException($"库存服务未返回商品 {item.ProductId}");

            var subTotal = snapshot.UnitPrice * item.Quantity;
            EnsureAmountFitsDatabase(subTotal);
            details.Add(new BizOrderDetail
            {
                ProductId = item.ProductId,
                ProductName = snapshot.ProductName.Trim(),
                Quantity = item.Quantity,
                UnitPrice = snapshot.UnitPrice,
                SubTotal = subTotal,
                SupplierId = snapshot.SupplierId
            });
        }

        return details;
    }

    private async Task<MktCouponUsage?> GetCouponAsync(
        int? couponRecordId,
        int customerId,
        decimal goodsAmount,
        IDbTransaction transaction)
    {
        if (!couponRecordId.HasValue)
            return null;

        return await _couponRepo.GetUsableCouponForUpdateAsync(
                couponRecordId.Value,
                customerId,
                goodsAmount,
                transaction)
            ?? throw new OrderBusinessException("优惠券不可用、已过期或未达到使用门槛");
    }

    private async Task<int> GetPointsMultiplierAsync(
        int? memberLevelId,
        IDbTransaction transaction)
    {
        if (!memberLevelId.HasValue)
            return 1;

        var level = await _pointRepo.GetLevelByIdAsync(
            memberLevelId.Value,
            transaction);
        return Math.Max(1, level?.PointsMultiplier ?? 1);
    }

    private async Task<LockedOrderContext> GetLockedOrderContextAsync(
        int orderId,
        IDbTransaction transaction)
    {
        var initialOrder = await _orderRepo.GetByIdAsync(orderId, transaction)
            ?? throw new OrderBusinessException("订单不存在");
        var customer = await _customerRepo.GetByIdForUpdateAsync(
                initialOrder.CustomerId,
                transaction)
            ?? throw new OrderBusinessException("订单消费者不存在");
        var lockedOrder = await _orderRepo.GetByIdForUpdateAsync(
                orderId,
                transaction)
            ?? throw new OrderBusinessException("订单不存在");
        if (lockedOrder.CustomerId != customer.CustomerId)
            throw new OrderBusinessException("订单消费者已变化，请刷新后重试");

        return new LockedOrderContext
        {
            Order = lockedOrder,
            Customer = customer,
            Details = await _orderRepo.GetDetailsAsync(orderId, transaction)
        };
    }

    private static void NormalizeOrderQuery(OrderQueryRequest request)
    {
        if (request.CustomerId <= 0)
            throw new OrderBusinessException("消费者ID必须大于0");
        if (request.Status.HasValue &&
            !Enum.IsDefined(request.Status.Value))
        {
            throw new OrderBusinessException("订单状态筛选值无效");
        }

        request.Keyword = string.IsNullOrWhiteSpace(request.Keyword)
            ? null
            : request.Keyword.Trim();
        if (request.Keyword?.Length > 50)
            throw new OrderBusinessException("关键词不能超过50个字符");
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 50);
    }

    private static string CreateShippingAddress(CrmUserAddress address)
    {
        return string.Join(
            " ",
            new[]
            {
                address.Province,
                address.City,
                address.District,
                address.DetailAddress
            }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static IReadOnlyList<FulfillmentOrderItem> CreateFulfillmentItems(
        IEnumerable<BizOrderDetail> details)
    {
        return details.Select(detail => new FulfillmentOrderItem
        {
            ProductId = detail.ProductId,
            ProductName = detail.ProductName,
            SupplierId = detail.SupplierId
                ?? throw new OrderBusinessException(
                    $"商品 {detail.ProductId} 缺少供应商"),
            Quantity = detail.Quantity,
            UnitPrice = detail.UnitPrice,
            SubTotal = detail.SubTotal
        }).ToList();
    }

    private static FulfillmentOrderRequest CreateFulfillmentOrderRequest(
        BizOrder order,
        IReadOnlyList<BizOrderDetail> details)
    {
        return new FulfillmentOrderRequest
        {
            OrderId = order.OrderId,
            OrderNo = order.OrderNo,
            ReceiverName = order.ReceiverName,
            ReceiverPhone = order.ReceiverPhone,
            ShippingAddress = order.ShippingAddress,
            Items = CreateFulfillmentItems(details)
        };
    }

    private static IReadOnlyList<OrderSupplierGroupViewModel>
        CreateSupplierGroupViewModels(
            IEnumerable<BizOrderDetail> details,
            IReadOnlyList<SupplierFulfillmentStatus> fulfillmentStatuses)
    {
        var statusBySupplier = fulfillmentStatuses
            .GroupBy(status => status.SupplierId)
            .ToDictionary(group => group.Key, group => group.First());
        return details
            .GroupBy(detail => detail.SupplierId ?? 0)
            .OrderBy(group => group.Key)
            .Select(group => new OrderSupplierGroupViewModel
            {
                SupplierId = group.Key,
                SubTotal = group.Sum(detail => detail.SubTotal),
                FulfillmentStatus = statusBySupplier.TryGetValue(
                    group.Key,
                    out var fulfillment)
                    ? fulfillment.StatusName
                    : "未同步",
                TrackingNo = fulfillment?.TrackingNo,
                Items = group.ToList()
            })
            .ToList();
    }

    private static int CalculatePoints(decimal finalAmount, int pointsMultiplier)
    {
        try
        {
            var basePoints = decimal.ToInt32(decimal.Floor(finalAmount / 10m));
            return checked(basePoints * pointsMultiplier);
        }
        catch (OverflowException)
        {
            throw new OrderBusinessException("订单积分超出系统可处理范围");
        }
    }

    private static IReadOnlyList<SupplierOrderGroupResult> CreateSupplierGroups(
        IEnumerable<BizOrderDetail> details)
    {
        return details
            .GroupBy(detail => detail.SupplierId!.Value)
            .OrderBy(group => group.Key)
            .Select(group => new SupplierOrderGroupResult
            {
                SupplierId = group.Key,
                SubTotal = group.Sum(detail => detail.SubTotal),
                Items = group.Select(detail => new OrderItemResult
                {
                    ProductId = detail.ProductId,
                    ProductName = detail.ProductName,
                    Quantity = detail.Quantity,
                    UnitPrice = detail.UnitPrice,
                    SubTotal = detail.SubTotal
                }).ToList()
            })
            .ToList();
    }

    private static void EnsureAmountFitsDatabase(decimal amount)
    {
        if (amount is < 0 or > MaxOrderAmount)
            throw new OrderBusinessException("订单金额超出数据库可保存范围");
    }

    private static string GenerateOrderNo()
    {
        return $"ORD{DateTime.UtcNow:yyyyMMddHHmmssfff}{Guid.NewGuid():N}"[..31];
    }

    private sealed class LockedOrderContext
    {
        public required BizOrder Order { get; init; }
        public required CrmCustomer Customer { get; init; }
        public required List<BizOrderDetail> Details { get; init; }
    }
}
