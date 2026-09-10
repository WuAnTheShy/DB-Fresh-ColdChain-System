using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

// 收货地址新增和编辑请求。
public sealed class AddressUpsertRequest
{
    [StringLength(36, ErrorMessage = "地址ID不能超过36个字符")]
    public string? AddressId { get; set; }

    [Required(ErrorMessage = "消费者ID不能为空")]
    [StringLength(36, ErrorMessage = "消费者ID不能超过36个字符")]
    public string CustomerId { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入收件人")]
    [StringLength(50, ErrorMessage = "收件人不能超过50个字符")]
    public string ReceiverName { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入联系电话")]
    [StringLength(20, ErrorMessage = "联系电话不能超过20个字符")]
    [RegularExpression(@"^1\d{10}$", ErrorMessage = "请输入11位中国大陆手机号码")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入省份")]
    [StringLength(50, ErrorMessage = "省份不能超过50个字符")]
    public string Province { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入城市")]
    [StringLength(50, ErrorMessage = "城市不能超过50个字符")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入区县")]
    [StringLength(50, ErrorMessage = "区县不能超过50个字符")]
    public string District { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入详细地址")]
    [StringLength(200, ErrorMessage = "详细地址不能超过200个字符")]
    public string DetailAddress { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
