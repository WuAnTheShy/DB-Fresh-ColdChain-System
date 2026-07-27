using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

/// <summary>
/// 创建订单请求。客户端只提交标识和数量，价格等可信数据由服务端获取。
/// </summary>
public sealed class CreateOrderRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "消费者ID必须大于0")]
    public int CustomerId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "收货地址ID必须大于0")]
    public int AddressId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "优惠券记录ID必须大于0")]
    public int? CouponRecordId { get; set; }

    [MinLength(1, ErrorMessage = "订单至少需要一件商品")]
    public List<CreateOrderItemRequest> Items { get; set; } = [new()];
}

/// <summary>
/// 创建订单时的商品请求。
/// </summary>
public sealed class CreateOrderItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "商品ID必须大于0")]
    public int ProductId { get; set; }

    [Range(1, 9999, ErrorMessage = "商品数量必须在1到9999之间")]
    public int Quantity { get; set; } = 1;
}
