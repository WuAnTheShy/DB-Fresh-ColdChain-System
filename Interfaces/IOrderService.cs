using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

/// <summary>
/// B 组对外暴露的接口 - 供 A 组、C 组调用。
/// 【铁律】其他组只能通过此接口操作 B 组的表。
/// </summary>
public interface IOrderService
{
    Task<CreateCheckoutBatchResult> CreateCheckoutBatchAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<CheckoutBatchSummary?> GetCheckoutBatchAsync(
        string checkoutBatchId,
        string customerId);

    Task<CheckoutBatchPaymentResult> PayCheckoutBatchAsync(
        string checkoutBatchId,
        string customerId,
        CheckoutBatchPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<int> ExpirePendingCheckoutBatchesAsync(CancellationToken cancellationToken = default);

    Task<OrderListViewModel> GetOrdersAsync(OrderQueryRequest request);

    Task<OrderDetailViewModel?> GetOrderDetailAsync(string orderId);

    Task TransitionOrderAsync(
        string orderId,
        OrderStatus targetStatus,
        CancellationToken cancellationToken = default);

    Task CancelOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default);

    Task ConfirmOrderItemReceiptAsync(
        string orderId,
        string orderDetailId,
        string customerId,
        CancellationToken cancellationToken = default);

    Task DeductPointsForRefundAsync(
        string customerId,
        string orderId,
        int pointsToDeduct,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 部分退款时按比例扣回积分并将订单置为"退款中" - 供 C 组调用。
    /// </summary>
    Task DeductPointsForPartialRefundAsync(
        string customerId,
        string orderId,
        int pointsToDeduct,
        CancellationToken cancellationToken = default);

    Task<CrmMemberLevel?> GetCustomerLevelAsync(string customerId);
}

public interface ICustomerService
{
    Task<string> CreateCustomerAsync(CustomerCreateRequest request);

    Task<GroupBCustomerLoginResult> LoginAsync(GroupBCustomerLoginRequest request);

    Task<GroupBCustomerPasswordResetCodeResult> SendPasswordResetCodeAsync(
        GroupBCustomerPasswordResetCodeRequest request);

    Task ResetPasswordAsync(GroupBCustomerPasswordResetRequest request);

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
