using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;
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
    private readonly IPromoterService? _promoterService;
    private readonly IPaymentRepository? _paymentRepository;

    private static readonly IReadOnlySet<string> SupportedBanks = new HashSet<string>(StringComparer.Ordinal)
    {
        "中国工商银行", "中国农业银行", "中国银行", "中国建设银行",
        "交通银行", "招商银行", "中国邮政储蓄银行", "浦发银行"
    };

    public OrderService(
        IOrderRepository orderRepo,
        ICustomerRepository customerRepo,
        ICouponRepository couponRepo,
        IPointRepository pointRepo,
        IInventoryService inventoryService,
        ILogisticsService logisticsService,
        ICommissionService commissionService,
        IOrderTransactionManager transactionManager,
        IPromoterService? promoterService = null,
        IPaymentRepository? paymentRepository = null)
    {
        _orderRepo = orderRepo;
        _customerRepo = customerRepo;
        _couponRepo = couponRepo;
        _pointRepo = pointRepo;
        _inventoryService = inventoryService;
        _logisticsService = logisticsService;
        _commissionService = commissionService;
        _transactionManager = transactionManager;
        _promoterService = promoterService;
        _paymentRepository = paymentRepository;
    }

    public async Task<CheckoutBatchSummary?> GetCheckoutBatchAsync(
        string checkoutBatchId,
        string customerId)
    {
        if (!GroupBIds.IsValid(checkoutBatchId) || !GroupBIds.IsValid(customerId))
            return null;

        var orders = await _orderRepo.GetByCheckoutBatchAsync(checkoutBatchId, customerId);
        if (orders.Count == 0) return null;
        return CreateCheckoutBatchSummary(checkoutBatchId, orders);
    }

    public async Task<CheckoutBatchPaymentResult> PayCheckoutBatchAsync(
        string checkoutBatchId,
        string customerId,
        CheckoutBatchPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!GroupBIds.IsValid(checkoutBatchId) || !GroupBIds.IsValid(customerId))
            throw new OrderBusinessException("结算批次不存在");
        if (_paymentRepository == null)
            throw new OrderBusinessException("支付流水服务未配置");

        var paymentMethod = request.PaymentMethod?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!CheckoutPaymentMethods.All.Contains(paymentMethod))
            throw new OrderBusinessException("请选择微信、支付宝或银行卡支付");
        var bankName = string.IsNullOrWhiteSpace(request.BankName) ? null : request.BankName.Trim();
        if (paymentMethod == CheckoutPaymentMethods.BankCard &&
            (bankName == null || !SupportedBanks.Contains(bankName)))
        {
            throw new OrderBusinessException("请选择支持的银行");
        }
        if (paymentMethod != CheckoutPaymentMethods.BankCard && bankName != null)
            throw new OrderBusinessException("微信或支付宝支付无需选择银行");

        return await _transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var orders = await _orderRepo.GetByCheckoutBatchForUpdateAsync(
                checkoutBatchId,
                customerId,
                transaction);
            if (orders.Count == 0)
                throw new OrderBusinessException("结算批次不存在或不属于当前消费者");

            if (orders.All(order => order.OrderStatus == OrderStatusCodes.Paid))
            {
                return new CheckoutBatchPaymentResult
                {
                    CheckoutBatchId = checkoutBatchId,
                    PaymentMethod = paymentMethod,
                    BankName = bankName,
                    PaidAmount = orders.Sum(order => order.FinalAmount),
                    ChildOrderCount = orders.Count,
                    PointsEarned = orders.Sum(order => order.PointsEarned),
                    AlreadyPaid = true
                };
            }
            if (orders.Any(order => order.OrderStatus != OrderStatusCodes.PendingPayment))
                throw new OrderBusinessException("结算批次状态不一致，无法支付");

            var customer = await _customerRepo.GetByIdForUpdateAsync(customerId, transaction)
                ?? throw new OrderBusinessException("消费者不存在");

            var expiresAt = orders.Min(order => order.PaymentExpiresAt)
                ?? throw new OrderBusinessException("结算批次缺少支付截止时间");
            if (expiresAt <= DateTime.Now)
            {
                foreach (var order in orders)
                {
                    var changed = await _orderRepo.TryUpdateStatusAsync(
                        order.OrderId,
                        OrderStatus.PendingPayment,
                        OrderStatus.Cancelled,
                        transaction);
                    if (!changed)
                        throw new OrderBusinessException("结算批次状态已变化，请刷新后重试");
                    await _inventoryService.ReleaseAsync(
                        new FulfillmentOrderRequest
                        {
                            OrderId = order.OrderId,
                            Items = CreateFulfillmentItems(
                                await _orderRepo.GetDetailsAsync(order.OrderId, transaction))
                        },
                        transaction,
                        cancellationToken);
                }
                var pointsToRestore = orders.Sum(order => order.PointsUsed);
                if (pointsToRestore > 0)
                {
                    var restoredBalance = checked(customer.Points + pointsToRestore);
                    await _customerRepo.UpdatePointsAsync(customerId, restoredBalance, transaction);
                    await _pointRepo.InsertLogAsync(new CrmPointLog
                    {
                        PointLogId = GroupBIds.NewId(),
                        CustomerId = customerId,
                        ChangeAmount = pointsToRestore,
                        BalanceAfter = restoredBalance,
                        ChangeType = "ORDER_REDEEM_RESTORE",
                        OrderId = orders[0].OrderId
                    }, transaction);
                }
                return new CheckoutBatchPaymentResult
                {
                    CheckoutBatchId = checkoutBatchId,
                    PaymentMethod = paymentMethod,
                    BankName = bankName,
                    ChildOrderCount = orders.Count,
                    IsExpired = true
                };
            }

            var transactionNo = $"SIM{DateTime.Now:yyyyMMddHHmmss}{Guid.NewGuid():N}"[..34];
            var pointsEligibleAmount = orders.Sum(order =>
                Math.Max(0m, order.FinalAmount - order.FreightAmount));
            var totalPointsEarned = (int)Math.Floor(pointsEligibleAmount);
            var remainingPointsEarned = totalPointsEarned;
            for (var index = 0; index < orders.Count; index++)
            {
                var order = orders[index];
                var orderEligibleAmount = Math.Max(0m, order.FinalAmount - order.FreightAmount);
                var orderPointsEarned = index == orders.Count - 1
                    ? remainingPointsEarned
                    : Math.Min(remainingPointsEarned, (int)Math.Floor(orderEligibleAmount));
                remainingPointsEarned -= orderPointsEarned;
                var changed = await _orderRepo.TryUpdateStatusAsync(
                    order.OrderId,
                    OrderStatus.PendingPayment,
                    OrderStatus.Paid,
                    transaction);
                if (!changed)
                    throw new OrderBusinessException("结算批次状态已变化，请刷新后重试");
                if (!await _orderRepo.UpdatePointsEarnedAsync(
                    order.OrderId,
                    orderPointsEarned,
                    transaction))
                    throw new OrderBusinessException("订单积分入账失败，请重试");

                await _paymentRepository.GroupC_AddPaymentRecordAsync(
                    new GroupC_FinPaymentRecord
                    {
                        PayId = $"PAY_{Guid.NewGuid():N}",
                        OrderId = order.OrderId,
                        PayMethod = paymentMethod == CheckoutPaymentMethods.BankCard
                            ? $"{paymentMethod}:{bankName}"
                            : paymentMethod,
                        TransactionNo = transactionNo,
                        PayAmount = order.FinalAmount,
                        Status = "Success",
                        PayTime = DateTime.Now,
                        Remark = "SIMULATED"
                    },
                    transaction);
            }

            if (totalPointsEarned > 0)
            {
                var newPoints = checked(customer.Points + totalPointsEarned);
                await _customerRepo.UpdatePointsAsync(customerId, newPoints, transaction);
                await _pointRepo.InsertLogAsync(new CrmPointLog
                {
                    PointLogId = GroupBIds.NewId(),
                    CustomerId = customerId,
                    ChangeAmount = totalPointsEarned,
                    BalanceAfter = newPoints,
                    ChangeType = "ORDER_EARN",
                    OrderId = orders[0].OrderId
                }, transaction);
            }

            return new CheckoutBatchPaymentResult
            {
                CheckoutBatchId = checkoutBatchId,
                TransactionNo = transactionNo,
                PaymentMethod = paymentMethod,
                BankName = bankName,
                PaidAmount = orders.Sum(order => order.FinalAmount),
                ChildOrderCount = orders.Count,
                PointsEarned = totalPointsEarned
            };
        });
    }

    /// <summary>
    /// 将一次消费者结算按团长拆成多个待支付子订单。库存校验和全部子订单写入
    /// 共用一个事务，任一商品缺货时整个批次回滚。
    /// </summary>
    public async Task<CreateCheckoutBatchResult> CreateCheckoutBatchAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var batchItems = ValidateAndNormalizeBatchRequest(request);
        if (_promoterService == null)
            throw new OrderBusinessException("团长服务未配置，暂时无法结算");

        var followedPromoterIds = (await _promoterService
                .GetBoundPromoterIdsAsync(request.CustomerId))
            .ToHashSet(StringComparer.Ordinal);
        var missingFollow = batchItems
            .Select(item => item.PromoterId)
            .Distinct(StringComparer.Ordinal)
            .FirstOrDefault(promoterId => !followedPromoterIds.Contains(promoterId));
        if (missingFollow != null)
            throw new OrderBusinessException("结算商品所属团长尚未关注，请刷新购物车后重试");

        return await _transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var customer = await _customerRepo.GetByIdForUpdateAsync(
                    request.CustomerId,
                    transaction)
                ?? throw new OrderBusinessException("消费者不存在，无法创建结算批次");
            var address = await _customerRepo.GetAddressAsync(
                    request.CustomerId,
                    request.AddressId,
                    transaction)
                ?? throw new OrderBusinessException("收货地址不存在或不属于当前消费者");

            var reservationItems = batchItems.Select(item => new InventoryReservationItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity
            }).ToList();
            var snapshots = await _inventoryService.ReserveAsync(
                reservationItems,
                transaction,
                cancellationToken);
            var details = CreateTrustedDetails(reservationItems, snapshots);
            var itemsByProductId = batchItems.ToDictionary(
                item => item.ProductId,
                StringComparer.Ordinal);
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
            if (request.PointsToUse < 0 || request.PointsToUse % 100 != 0)
                throw new OrderBusinessException("抵扣积分必须为100的整数倍");
            if (request.PointsToUse > customer.Points)
                throw new OrderBusinessException("可用积分不足");
            var maxPointsToUse = checked((int)Math.Floor(
                Math.Max(0m, goodsAmount - discountAmount) * 0.10m) * 100);
            if (request.PointsToUse > maxPointsToUse)
                throw new OrderBusinessException($"本次最多可使用{maxPointsToUse}积分抵扣订单金额的10%");
            var pointsDiscountAmount = request.PointsToUse / 100m;
            var checkoutBatchId = GroupBIds.NewId();
            var paymentExpiresAt = DateTime.Now.AddMinutes(15);
            var groupedDetails = details
                .GroupBy(detail => itemsByProductId[detail.ProductId].PromoterId)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToList();

            var remainingDiscount = discountAmount;
            var remainingPointsDiscount = pointsDiscountAmount;
            var remainingPointsUsed = request.PointsToUse;
            var orderResults = new List<CreateOrderResult>(groupedDetails.Count);
            for (var index = 0; index < groupedDetails.Count; index++)
            {
                var group = groupedDetails[index];
                var groupDetails = group.ToList();
                var groupGoodsAmount = groupDetails.Sum(detail => detail.SubTotal);
                var groupDiscount = index == groupedDetails.Count - 1
                    ? remainingDiscount
                    : Math.Min(
                        remainingDiscount,
                        Math.Round(
                            discountAmount * groupGoodsAmount / goodsAmount,
                            2,
                            MidpointRounding.AwayFromZero));
                remainingDiscount -= groupDiscount;
                var groupPointsDiscount = index == groupedDetails.Count - 1
                    ? remainingPointsDiscount
                    : Math.Min(
                        remainingPointsDiscount,
                        Math.Round(
                            pointsDiscountAmount * (groupGoodsAmount - groupDiscount) /
                            Math.Max(0.01m, goodsAmount - discountAmount),
                            2,
                            MidpointRounding.AwayFromZero));
                var groupPointsUsed = index == groupedDetails.Count - 1
                    ? remainingPointsUsed
                    : Math.Min(remainingPointsUsed, (int)Math.Round(
                        groupPointsDiscount * 100m,
                        0,
                        MidpointRounding.AwayFromZero));
                remainingPointsDiscount -= groupPointsDiscount;
                remainingPointsUsed -= groupPointsUsed;

                var freightAmount = await _logisticsService.CalculateFreightAsync(
                    new FreightCalculationRequest
                    {
                        CustomerId = customer.CustomerId,
                        Province = address.Province,
                        City = address.City,
                        District = address.District,
                        GoodsAmount = groupGoodsAmount,
                        Items = CreateFulfillmentItems(groupDetails)
                    },
                    transaction,
                    cancellationToken);
                EnsureAmountFitsDatabase(freightAmount);
                var finalAmount = groupGoodsAmount - groupDiscount - groupPointsDiscount + freightAmount;
                EnsureAmountFitsDatabase(finalAmount);

                var order = new BizOrder
                {
                    OrderId = GroupBIds.NewId(),
                    OrderNo = GenerateOrderNo(),
                    CustomerId = request.CustomerId,
                    CheckoutBatchId = checkoutBatchId,
                    PromoterId = group.Key,
                    AddressId = request.AddressId,
                    ReceiverName = address.ReceiverName,
                    ReceiverPhone = address.Phone,
                    ShippingAddress = CreateShippingAddress(address),
                    TotalAmount = groupGoodsAmount,
                    DiscountAmount = groupDiscount,
                    FreightAmount = freightAmount,
                    FinalAmount = finalAmount,
                    PointsEarned = 0,
                    PointsUsed = groupPointsUsed,
                    PointsDiscountAmount = groupPointsDiscount,
                    OrderStatus = OrderStatusCodes.PendingPayment,
                    PaymentExpiresAt = paymentExpiresAt,
                    CreatedAt = DateTime.Now
                };

                var orderId = await _orderRepo.CreateOrderAsync(order, transaction);
                foreach (var detail in groupDetails)
                    detail.OrderId = orderId;
                await _orderRepo.InsertDetailsAsync(groupDetails, transaction);
                orderResults.Add(new CreateOrderResult
                {
                    OrderId = orderId,
                    OrderNo = order.OrderNo,
                    GoodsAmount = groupGoodsAmount,
                    DiscountAmount = groupDiscount,
                    FreightAmount = freightAmount,
                    FinalAmount = finalAmount,
                    PointsEarned = 0,
                    PointsUsed = groupPointsUsed,
                    PointsDiscountAmount = groupPointsDiscount,
                    SupplierGroups = CreateSupplierGroups(groupDetails)
                });
            }

            if (coupon != null)
            {
                var couponUsed = await _couponRepo.TryUseCouponAsync(
                    coupon.RecordId,
                    request.CustomerId,
                    orderResults[0].OrderId,
                    transaction);
                if (!couponUsed)
                    throw new OrderBusinessException("优惠券已被使用，请重新选择");
            }

            if (request.PointsToUse > 0)
            {
                var newPoints = customer.Points - request.PointsToUse;
                await _customerRepo.UpdatePointsAsync(customer.CustomerId, newPoints, transaction);
                await _pointRepo.InsertLogAsync(new CrmPointLog
                {
                    PointLogId = GroupBIds.NewId(),
                    CustomerId = customer.CustomerId,
                    ChangeAmount = -request.PointsToUse,
                    BalanceAfter = newPoints,
                    ChangeType = "ORDER_REDEEM",
                    OrderId = orderResults[0].OrderId
                }, transaction);
            }

            var snapshotsById = snapshots.ToDictionary(
                snapshot => snapshot.ProductId,
                StringComparer.Ordinal);
            var priceChanges = batchItems
                .Where(item => item.ClientUnitPrice.HasValue &&
                    item.ClientUnitPrice.Value != snapshotsById[item.ProductId].UnitPrice)
                .Select(item => new OrderPriceChangeResult
                {
                    ProductId = item.ProductId,
                    ProductName = snapshotsById[item.ProductId].ProductName,
                    PreviousPrice = item.ClientUnitPrice!.Value,
                    LatestPrice = snapshotsById[item.ProductId].UnitPrice
                })
                .ToList();

            return new CreateCheckoutBatchResult
            {
                CheckoutBatchId = checkoutBatchId,
                PaymentExpiresAt = paymentExpiresAt,
                GoodsAmount = orderResults.Sum(order => order.GoodsAmount),
                DiscountAmount = orderResults.Sum(order => order.DiscountAmount),
                FreightAmount = orderResults.Sum(order => order.FreightAmount),
                FinalAmount = orderResults.Sum(order => order.FinalAmount),
                PointsUsed = request.PointsToUse,
                PointsDiscountAmount = pointsDiscountAmount,
                Orders = orderResults,
                PriceChanges = priceChanges
            };
        });
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
                OrderId = GroupBIds.NewId(),
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
                OrderStatus = OrderStatusCodes.Paid,
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
                    PointLogId = GroupBIds.NewId(),
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

    public async Task<OrderDetailViewModel?> GetOrderDetailAsync(string orderId)
    {
        if (!GroupBIds.IsValid(orderId))
            return null;

        var header = await _orderRepo.GetDetailHeaderAsync(orderId);
        if (header == null)
            return null;

        var details = await _orderRepo.GetDetailsAsync(orderId);
        var supplierIds = details
            .Where(detail => !string.IsNullOrWhiteSpace(detail.SupplierId))
            .Select(detail => detail.SupplierId!)
            .Distinct()
            .OrderBy(supplierId => supplierId)
            .ToList();
        var fulfillmentStatuses =
            await _logisticsService.GetSupplierStatusesAsync(
                orderId,
                supplierIds);
        var status = OrderStatusCodes.Parse(header.OrderStatus);
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
        string orderId,
        OrderStatus targetStatus,
        CancellationToken cancellationToken = default)
    {
        if (!GroupBIds.IsValid(orderId))
            throw new OrderBusinessException("订单ID格式不正确");
        if (targetStatus is not (OrderStatus.Shipped or OrderStatus.Completed))
            throw new OrderBusinessException("目标订单状态不受此操作支持");

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = await GetLockedOrderContextAsync(orderId, transaction);
            var currentStatus = OrderStatusCodes.Parse(context.Order.OrderStatus);
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
                        orderID = context.Order.OrderId,
                        promoterID = context.Order.PromoterId ?? context.Customer.PromoterId,
                        finalAmount = context.Order.FinalAmount,
                        goodsAmount = context.Order.TotalAmount
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
        string orderId,
        CancellationToken cancellationToken = default)
    {
        if (!GroupBIds.IsValid(orderId))
            throw new OrderBusinessException("订单ID格式不正确");

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = await GetLockedOrderContextAsync(orderId, transaction);
            var currentStatus = OrderStatusCodes.Parse(context.Order.OrderStatus);
            OrderStateMachine.EnsureTransition(
                currentStatus,
                OrderStatus.Cancelled);

            await _inventoryService.ReleaseAsync(
                CreateFulfillmentOrderRequest(
                    context.Order,
                    context.Details),
                transaction,
                cancellationToken);

            var currentPoints = context.Customer.Points;
            if (context.Order.PointsUsed > 0)
            {
                currentPoints = checked(currentPoints + context.Order.PointsUsed);
                await _customerRepo.UpdatePointsAsync(
                    context.Customer.CustomerId,
                    currentPoints,
                    transaction);
                await _pointRepo.InsertLogAsync(new CrmPointLog
                {
                    PointLogId = GroupBIds.NewId(),
                    CustomerId = context.Customer.CustomerId,
                    ChangeAmount = context.Order.PointsUsed,
                    BalanceAfter = currentPoints,
                    ChangeType = "ORDER_REDEEM_RESTORE",
                    OrderId = context.Order.OrderId
                }, transaction);
            }

            if (context.Order.PointsEarned > 0)
            {
                if (currentPoints < context.Order.PointsEarned)
                {
                    throw new OrderBusinessException(
                        "当前积分不足以撤销该订单奖励，请联系管理员处理");
                }

                var newPoints = currentPoints - context.Order.PointsEarned;
                await _customerRepo.UpdatePointsAsync(
                    context.Customer.CustomerId,
                    newPoints,
                    transaction);
                await _pointRepo.InsertLogAsync(new CrmPointLog
                {
                    PointLogId = GroupBIds.NewId(),
                    CustomerId = context.Customer.CustomerId,
                    ChangeAmount = -context.Order.PointsEarned,
                    BalanceAfter = newPoints,
                    ChangeType = "ORDER_CANCEL",
                    OrderId = context.Order.OrderId
                }, transaction);
            }

            if (context.Order.CheckoutBatchId == null &&
                !await _customerRepo.TrySubtractTotalSpentAsync(
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

            var newTotalSpent = context.Order.CheckoutBatchId == null
                ? context.Customer.TotalSpent - context.Order.FinalAmount
                : context.Customer.TotalSpent;
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
        string customerId,
        string orderId,
        int pointsToDeduct,
        CancellationToken cancellationToken = default)
    {
        if (!GroupBIds.IsValid(customerId))
            throw new OrderBusinessException("消费者ID格式不正确");
        if (!GroupBIds.IsValid(orderId))
            throw new OrderBusinessException("订单ID格式不正确");
        if (pointsToDeduct < 0)
            throw new OrderBusinessException("扣回积分不能为负数");

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = await GetLockedOrderContextAsync(orderId, transaction);
            if (context.Customer.CustomerId != customerId)
                throw new OrderBusinessException("订单不属于指定消费者");

            var currentStatus = OrderStatusCodes.Parse(context.Order.OrderStatus);
            if (currentStatus == OrderStatus.Refunded)
                return;
            if (currentStatus is OrderStatus.PendingPayment or OrderStatus.Cancelled)
                throw new OrderBusinessException("当前订单状态不允许退款");
            if (currentStatus is not (
                OrderStatus.Paid or
                OrderStatus.Shipped or
                OrderStatus.Completed or
                OrderStatus.Refunding))
            {
                throw new OrderBusinessException("订单状态无效，无法完成退款");
            }

            // 订单处于"退款中"说明此前发生过部分退款，允许继续整单退款并扣回剩余积分
            if (currentStatus != OrderStatus.Refunding &&
                await _pointRepo.HasPointLogAsync(
                    customerId,
                    orderId,
                    "REFUND_DEDUCT",
                    transaction))
            {
                throw new OrderBusinessException("退款积分流水已存在但订单状态不一致");
            }

            if (currentStatus == OrderStatus.Paid)
            {
                await _inventoryService.ReleaseAsync(
                    CreateFulfillmentOrderRequest(
                        context.Order,
                        context.Details),
                    transaction,
                    cancellationToken);
            }

            var requestedDeduction = Math.Min(
                pointsToDeduct,
                context.Order.PointsEarned);
            var actualDeduction = Math.Min(
                context.Customer.Points,
                requestedDeduction);

            if (actualDeduction > 0)
            {
                var newPoints = context.Customer.Points - actualDeduction;
                await _customerRepo.UpdatePointsAsync(
                    customerId,
                    newPoints,
                    transaction);
                await _pointRepo.InsertLogAsync(new CrmPointLog
                {
                    PointLogId = GroupBIds.NewId(),
                    CustomerId = customerId,
                    ChangeAmount = -actualDeduction,
                    BalanceAfter = newPoints,
                    ChangeType = "REFUND_DEDUCT",
                    OrderId = orderId
                }, transaction);
            }

            if (!await _orderRepo.TryUpdateStatusAsync(
                orderId,
                currentStatus,
                OrderStatus.Refunded,
                transaction))
            {
                throw new OrderBusinessException("订单状态已变化，请刷新后重试");
            }
        });
    }

    /// 部分退款时按比例扣回积分并将订单置为"退款中" - 供 C 组调用。
    /// </summary>
    public async Task DeductPointsForPartialRefundAsync(
        string customerId,
        string orderId,
        int pointsToDeduct,
        CancellationToken cancellationToken = default)
    {
        if (!GroupBIds.IsValid(customerId))
            throw new OrderBusinessException("消费者ID格式不正确");
        if (!GroupBIds.IsValid(orderId))
            throw new OrderBusinessException("订单ID格式不正确");
        if (pointsToDeduct < 0)
            throw new OrderBusinessException("扣回积分不能为负数");

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = await GetLockedOrderContextAsync(orderId, transaction);
            if (context.Customer.CustomerId != customerId)
                throw new OrderBusinessException("订单不属于指定消费者");

            var currentStatus = OrderStatusCodes.Parse(context.Order.OrderStatus);
            if (currentStatus is OrderStatus.PendingPayment or OrderStatus.Cancelled
                or OrderStatus.Refunded)
                throw new OrderBusinessException("当前订单状态不允许部分退款");
            if (currentStatus == OrderStatus.Paid)
                throw new OrderBusinessException("未发货订单请使用整单退款");
            if (currentStatus is not (
                OrderStatus.Shipped or
                OrderStatus.Completed or
                OrderStatus.Refunding))
            {
                throw new OrderBusinessException("订单状态无效，无法部分退款");
            }

            var actualDeduction = Math.Min(
                context.Customer.Points,
                pointsToDeduct);
            if (actualDeduction > 0)
            {
                var newPoints = context.Customer.Points - actualDeduction;
                await _customerRepo.UpdatePointsAsync(
                    customerId,
                    newPoints,
                    transaction);
                await _pointRepo.InsertLogAsync(new CrmPointLog
                {
                    PointLogId = GroupBIds.NewId(),
                    CustomerId = customerId,
                    ChangeAmount = -actualDeduction,
                    BalanceAfter = newPoints,
                    ChangeType = "REFUND_DEDUCT",
                    OrderId = orderId
                }, transaction);
            }

            if (currentStatus != OrderStatus.Refunding &&
                !await _orderRepo.TryUpdateStatusAsync(
                    orderId,
                    currentStatus,
                    OrderStatus.Refunding,
                    transaction))
            {
                throw new OrderBusinessException("订单状态已变化，请刷新后重试");
            }
        });
    }

    /// <summary>
    /// 根据累计消费金额动态查询消费者应处的最高会员等级。
    /// </summary>
    public async Task<CrmMemberLevel?> GetCustomerLevelAsync(string customerId)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId);
        if (customer == null)
            return null;

        return await _pointRepo.GetLevelForSpentAsync(customer.TotalSpent);
    }

    public async Task ConfirmOrderItemReceiptAsync(
        string orderId,
        string orderDetailId,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        if (!GroupBIds.IsValid(orderId) || !GroupBIds.IsValid(orderDetailId))
            throw new OrderBusinessException("订单或商品明细ID格式不正确");

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = await GetLockedOrderContextAsync(orderId, transaction);
            if (!string.Equals(context.Customer.CustomerId, customerId, StringComparison.Ordinal))
                throw new OrderBusinessException("订单不属于当前消费者");

            var detail = context.Details.SingleOrDefault(item => item.OrderDetailId == orderDetailId)
                ?? throw new OrderBusinessException("订单商品不存在");
            if (string.Equals(detail.ReceiptStatus, "RECEIVED", StringComparison.Ordinal))
                return;
            if (context.Order.OrderStatus != OrderStatusCodes.Shipped)
                throw new OrderBusinessException("商品发货后才能确认收货");

            if (!await _orderRepo.TryConfirmDetailReceiptAsync(
                orderDetailId,
                orderId,
                transaction))
            {
                throw new OrderBusinessException("商品收货状态已变化，请刷新后重试");
            }

            if (await _orderRepo.HasUnreceivedDetailsExceptAsync(
                orderId,
                orderDetailId,
                transaction))
            {
                return;
            }

            await _commissionService.RegisterCompletedOrderAsync(
                new CommissionOrderRequest
                {
                    orderID = context.Order.OrderId,
                    promoterID = context.Order.PromoterId ?? context.Customer.PromoterId,
                    finalAmount = context.Order.FinalAmount,
                    goodsAmount = context.Order.TotalAmount
                },
                transaction,
                cancellationToken);
            if (!await _orderRepo.TryUpdateStatusAsync(
                orderId,
                OrderStatus.Shipped,
                OrderStatus.Completed,
                transaction))
            {
                throw new OrderBusinessException("订单状态已变化，请刷新后重试");
            }
        });
    }

    private static IReadOnlyList<BatchOrderItem> ValidateAndNormalizeBatchRequest(
        CreateOrderRequest request)
    {
        if (!GroupBIds.IsValid(request.CustomerId))
            throw new OrderBusinessException("消费者ID格式不正确");
        if (!GroupBIds.IsValid(request.AddressId))
            throw new OrderBusinessException("收货地址ID格式不正确");
        if (request.CouponRecordId != null && !GroupBIds.IsValid(request.CouponRecordId))
            throw new OrderBusinessException("优惠券记录ID格式不正确");
        if (request.Items == null || request.Items.Count == 0)
            throw new OrderBusinessException("结算批次至少需要一件商品");

        var normalized = new Dictionary<string, BatchOrderItem>(StringComparer.Ordinal);
        foreach (var item in request.Items)
        {
            var productId = item.ProductId?.Trim() ?? string.Empty;
            var promoterId = item.PromoterId?.Trim() ?? string.Empty;
            if (productId.Length is 0 or > 64)
                throw new OrderBusinessException("商品ID不能为空且不能超过64个字符");
            if (promoterId.Length is 0 or > 36)
                throw new OrderBusinessException("团长ID不能为空且不能超过36个字符");
            if (item.Quantity is <= 0 or > 9999)
                throw new OrderBusinessException("商品数量必须在1到9999之间");

            if (normalized.TryGetValue(productId, out var existing))
            {
                if (!string.Equals(existing.PromoterId, promoterId, StringComparison.Ordinal))
                    throw new OrderBusinessException($"商品 {productId} 不能同时归属于多个团长");
                if (existing.ClientUnitPrice != item.ClientUnitPrice)
                    throw new OrderBusinessException($"商品 {productId} 的购物车价格数据不一致");
                existing.Quantity = checked(existing.Quantity + item.Quantity);
                if (existing.Quantity > 9999)
                    throw new OrderBusinessException($"商品 {productId} 的合计数量不能超过9999");
                continue;
            }

            normalized[productId] = new BatchOrderItem
            {
                ProductId = productId,
                PromoterId = promoterId,
                Quantity = item.Quantity,
                ClientUnitPrice = item.ClientUnitPrice
            };
        }

        return normalized.Values
            .OrderBy(item => item.PromoterId, StringComparer.Ordinal)
            .ThenBy(item => item.ProductId, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<InventoryReservationItem> ValidateAndNormalizeRequest(
        CreateOrderRequest request)
    {
        if (!GroupBIds.IsValid(request.CustomerId))
            throw new OrderBusinessException("消费者ID格式不正确");
        if (!GroupBIds.IsValid(request.AddressId))
            throw new OrderBusinessException("收货地址ID格式不正确");
        if (request.CouponRecordId != null &&
            !GroupBIds.IsValid(request.CouponRecordId))
        {
            throw new OrderBusinessException("优惠券记录ID格式不正确");
        }
        if (request.Items == null || request.Items.Count == 0)
            throw new OrderBusinessException("订单至少需要一件商品");

        var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in request.Items)
        {
            var productId = item.ProductId?.Trim();
            if (string.IsNullOrWhiteSpace(productId))
                throw new OrderBusinessException("商品ID不能为空");
            if (productId.Length > 64)
                throw new OrderBusinessException("商品ID不能超过64个字符");
            if (item.Quantity is <= 0 or > 9999)
                throw new OrderBusinessException("商品数量必须在1到9999之间");

            quantities.TryGetValue(productId, out var currentQuantity);
            var mergedQuantity = checked(currentQuantity + item.Quantity);
            if (mergedQuantity > 9999)
                throw new OrderBusinessException($"商品 {productId} 的合计数量不能超过9999");
            quantities[productId] = mergedQuantity;
        }

        return quantities
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
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

        var snapshotsByProductId = new Dictionary<string, InventoryProductSnapshot>(
            StringComparer.Ordinal);
        foreach (var snapshot in productSnapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.ProductId))
                throw new OrderBusinessException("库存服务返回的商品ID为空");
            if (!snapshotsByProductId.TryAdd(snapshot.ProductId, snapshot))
                throw new OrderBusinessException("库存服务返回了重复商品");
            if (string.IsNullOrWhiteSpace(snapshot.ProductName))
                throw new OrderBusinessException($"商品 {snapshot.ProductId} 缺少名称");
            if (string.IsNullOrWhiteSpace(snapshot.SupplierId))
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
                OrderDetailId = GroupBIds.NewId(),
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
        string? couponRecordId,
        string customerId,
        decimal goodsAmount,
        IDbTransaction transaction)
    {
        if (couponRecordId == null)
            return null;

        return await _couponRepo.GetUsableCouponForUpdateAsync(
                couponRecordId,
                customerId,
                goodsAmount,
                transaction)
            ?? throw new OrderBusinessException("优惠券不可用、已过期或未达到使用门槛");
    }

    private async Task<int> GetPointsMultiplierAsync(
        string? memberLevelId,
        IDbTransaction transaction)
    {
        if (memberLevelId == null)
            return 1;

        var level = await _pointRepo.GetLevelByIdAsync(
            memberLevelId,
            transaction);
        return Math.Max(1, level?.PointsMultiplier ?? 1);
    }

    private async Task<LockedOrderContext> GetLockedOrderContextAsync(
        string orderId,
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
        if (request.CustomerId != null && !GroupBIds.IsValid(request.CustomerId))
            throw new OrderBusinessException("消费者ID格式不正确");
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

    private static CheckoutBatchSummary CreateCheckoutBatchSummary(
        string checkoutBatchId,
        IReadOnlyList<BizOrder> orders)
    {
        var statuses = orders.Select(order => order.OrderStatus).Distinct(StringComparer.Ordinal).ToList();
        return new CheckoutBatchSummary
        {
            CheckoutBatchId = checkoutBatchId,
            PaymentExpiresAt = orders.Min(order => order.PaymentExpiresAt) ?? orders.Min(order => order.CreatedAt),
            OrderStatus = statuses.Count == 1 ? statuses[0] : "MIXED",
            FinalAmount = orders.Sum(order => order.FinalAmount),
            ChildOrderCount = orders.Count,
            Orders = orders.Select(order => new CheckoutBatchOrderSummary
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,
                PromoterId = order.PromoterId ?? string.Empty,
                FinalAmount = order.FinalAmount,
                OrderStatus = order.OrderStatus
            }).ToList()
        };
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
            .GroupBy(detail => detail.SupplierId ?? string.Empty)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
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
            .GroupBy(detail => detail.SupplierId!)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
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

    private sealed class BatchOrderItem
    {
        public string ProductId { get; init; } = string.Empty;
        public string PromoterId { get; init; } = string.Empty;
        public int Quantity { get; set; }
        public decimal? ClientUnitPrice { get; init; }
    }
}
