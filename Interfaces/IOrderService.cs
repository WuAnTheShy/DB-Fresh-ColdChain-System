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

    /// <summary>扣减买家积分（C组退款时调用）</summary>
    Task DeductPointsForRefundAsync(int customerId, int orderId, int pointsToDeduct);

    /// <summary>查询用户当前会员等级</summary>
    Task<CrmMemberLevel?> GetCustomerLevelAsync(int customerId);
}

public interface ICustomerService
{
    /// <summary>获取消费者信息</summary>
    Task<CrmCustomer?> GetCustomerAsync(int customerId);
}

public interface ICouponService
{
    /// <summary>校验优惠券是否可用</summary>
    Task<bool> ValidateCouponAsync(int recordId, int customerId, decimal orderAmount);
}
