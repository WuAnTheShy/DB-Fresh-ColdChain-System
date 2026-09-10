using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

// B 组对外暴露的接口 - 供 A 组、C 组调用。
// 【铁律】其他组只能通过此接口操作 B 组的表。
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

    Task<int> AutoConfirmShippedOrdersAsync(CancellationToken cancellationToken = default);

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

    Task ConfirmOrderReceiptAsync(
        string orderId,
        string customerId,
        CancellationToken cancellationToken = default);

    Task DeductPointsForRefundAsync(
        string customerId,
        string orderId,
        int pointsToDeduct,
        CancellationToken cancellationToken = default,
        IDbTransaction? externalTransaction = null);

    // 部分退款时按比例扣回积分并将订单置为"退款中" - 供 C 组调用。
    Task DeductPointsForPartialRefundAsync(
        string customerId,
        string orderId,
        int pointsToDeduct,
        CancellationToken cancellationToken = default,
        IDbTransaction? externalTransaction = null);

    // 消费者提交退款申请后订单进入"退款审核中"，并记录申请前状态 - 供 C 组调用。
    Task EnterRefundReviewAsync(
        string orderId,
        CancellationToken cancellationToken = default,
        IDbTransaction? externalTransaction = null);

    // 退款申请被驳回/取消且订单已无待审核申请时，订单回退到申请前状态 - 供 C 组调用。
    Task ExitRefundReviewAsync(
        string orderId,
        CancellationToken cancellationToken = default,
        IDbTransaction? externalTransaction = null);

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
