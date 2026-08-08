using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

/// <summary>
/// B 组对外暴露的接口 - 供 A 组、C 组调用。
/// 【铁律】其他组只能通过此接口操作 B 组的表。
/// </summary>
public interface IOrderService
{
    Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderListViewModel> GetOrdersAsync(OrderQueryRequest request);

    Task<OrderDetailViewModel?> GetOrderDetailAsync(string orderId);

    Task TransitionOrderAsync(
        string orderId,
        OrderStatus targetStatus,
        CancellationToken cancellationToken = default);

    Task CancelOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default);

    Task DeductPointsForRefundAsync(
        string customerId,
        string orderId,
        int pointsToDeduct,
        CancellationToken cancellationToken = default);

    Task<CrmMemberLevel?> GetCustomerLevelAsync(string customerId);
}

public interface ICustomerService
{
    Task<string> CreateCustomerAsync(CustomerCreateRequest request);

    Task<CrmCustomer?> GetCustomerAsync(string customerId);

    Task<CustomerProfileViewModel?> GetProfileAsync(string customerId);

    Task UpdateProfileAsync(CustomerProfileUpdateRequest request);

    Task<AddressListViewModel?> GetAddressesAsync(string customerId);

    Task<AddressUpsertRequest?> GetAddressForEditAsync(
        string customerId,
        string addressId);

    Task<string> CreateAddressAsync(AddressUpsertRequest request);

    Task UpdateAddressAsync(AddressUpsertRequest request);

    Task DeleteAddressAsync(string customerId, string addressId);

    Task SetDefaultAddressAsync(string customerId, string addressId);
}

public interface ICouponService
{
    Task<bool> ValidateCouponAsync(
        string recordId,
        string customerId,
        decimal orderAmount);

    Task<CouponCenterViewModel?> GetCouponCenterAsync(string customerId);

    Task ClaimCouponAsync(string customerId, string couponId);
}
