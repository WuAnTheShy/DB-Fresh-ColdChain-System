using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

/// <summary>
/// B 组对外暴露的接口 - 供 A 组、C 组调用
/// 【铁律】其他组只能通过此接口操作 B 组的表
/// </summary>
public interface IOrderService
{
    /// <summary>创建订单并完成库存、优惠券和积分事务闭环</summary>
    Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>分页查询订单</summary>
    Task<OrderListViewModel> GetOrdersAsync(OrderQueryRequest request);

    /// <summary>查询订单详情及供应商拆单</summary>
    Task<OrderDetailViewModel?> GetOrderDetailAsync(int orderId);

    /// <summary>执行已支付→已发货或已发货→已完成的合法状态流转</summary>
    Task TransitionOrderAsync(
        int orderId,
        OrderStatus targetStatus,
        CancellationToken cancellationToken = default);

    /// <summary>取消已支付但尚未发货的订单，并补偿 B/A 组资产</summary>
    Task CancelOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    /// <summary>扣减买家积分（C组退款时调用）</summary>
    Task DeductPointsForRefundAsync(int customerId, int orderId, int pointsToDeduct);

    /// <summary>查询用户当前会员等级</summary>
    Task<CrmMemberLevel?> GetCustomerLevelAsync(int customerId);
}

public interface ICustomerService
{
    /// <summary>新增消费者并安全生成密码哈希</summary>
    Task<int> CreateCustomerAsync(CustomerCreateRequest request);

    /// <summary>获取消费者信息</summary>
    Task<CrmCustomer?> GetCustomerAsync(int customerId);

    /// <summary>获取消费者中心资料、会员等级和地址摘要</summary>
    Task<CustomerProfileViewModel?> GetProfileAsync(int customerId);

    /// <summary>更新消费者可编辑的基础资料</summary>
    Task UpdateProfileAsync(CustomerProfileUpdateRequest request);

    /// <summary>获取消费者全部收货地址</summary>
    Task<AddressListViewModel?> GetAddressesAsync(int customerId);

    /// <summary>获取地址编辑数据，并校验地址归属</summary>
    Task<AddressUpsertRequest?> GetAddressForEditAsync(int customerId, int addressId);

    /// <summary>新增收货地址</summary>
    Task<int> CreateAddressAsync(AddressUpsertRequest request);

    /// <summary>编辑收货地址</summary>
    Task UpdateAddressAsync(AddressUpsertRequest request);

    /// <summary>删除收货地址；删除默认地址时自动顺延</summary>
    Task DeleteAddressAsync(int customerId, int addressId);

    /// <summary>设置默认收货地址</summary>
    Task SetDefaultAddressAsync(int customerId, int addressId);
}

public interface ICouponService
{
    /// <summary>校验优惠券是否可用</summary>
    Task<bool> ValidateCouponAsync(int recordId, int customerId, decimal orderAmount);

    /// <summary>查询可领取券模板和消费者当前可用券</summary>
    Task<CouponCenterViewModel?> GetCouponCenterAsync(int customerId);

    /// <summary>原子领取优惠券，防止重复领取和超发</summary>
    Task ClaimCouponAsync(int customerId, int couponId);
}
