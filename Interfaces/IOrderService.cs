using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

/// <summary>
/// B 组对外暴露的接口 - 供 A 组、C 组调用
/// 【铁律】其他组只能通过此接口操作 B 组的表
/// </summary>
public interface IOrderService
{
    /// <summary>创建订单（A组下单时可能调用）</summary>
    Task<int> CreateOrderAsync(BizOrder order, List<BizOrderDetail> details);

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
