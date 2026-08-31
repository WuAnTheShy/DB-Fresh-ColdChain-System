using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

/// <summary>
/// 创建订单请求。客户端只提交标识和数量，价格等可信数据由服务端获取。
/// </summary>
public sealed class CreateOrderRequest
{
    [Required(ErrorMessage = "消费者ID不能为空")]
    [StringLength(36, ErrorMessage = "消费者ID不能超过36个字符")]
    public string CustomerId { get; set; } = string.Empty;

    [Required(ErrorMessage = "收货地址ID不能为空")]
    [StringLength(36, ErrorMessage = "收货地址ID不能超过36个字符")]
    public string AddressId { get; set; } = string.Empty;

    [StringLength(36, ErrorMessage = "优惠券记录ID不能超过36个字符")]
    public string? CouponRecordId { get; set; }

    [Range(0, 100000000, ErrorMessage = "抵扣积分不能为负数")]
    public int PointsToUse { get; set; }

    [MinLength(1, ErrorMessage = "订单至少需要一件商品")]
    public List<CreateOrderItemRequest> Items { get; set; } = [new()];
}

/// <summary>
/// 创建订单时的商品请求。
/// </summary>
public sealed class CreateOrderItemRequest
{
    [Required(ErrorMessage = "商品ID不能为空")]
    [StringLength(64, ErrorMessage = "商品ID不能超过64个字符")]
    public string ProductId { get; set; } = string.Empty;

    [Required(ErrorMessage = "团长ID不能为空")]
    [StringLength(36, ErrorMessage = "团长ID不能超过36个字符")]
    public string PromoterId { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "客户端商品价格无效")]
    public decimal? ClientUnitPrice { get; set; }

    [Range(1, 9999, ErrorMessage = "商品数量必须在1到9999之间")]
    public int Quantity { get; set; } = 1;
}
