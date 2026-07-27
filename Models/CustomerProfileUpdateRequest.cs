using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

/// <summary>
/// 消费者资料编辑请求，不允许从页面修改密码、积分、累计消费和会员等级。
/// </summary>
public sealed class CustomerProfileUpdateRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "消费者ID必须大于0")]
    public int CustomerId { get; set; }

    [Required(ErrorMessage = "请输入消费者姓名")]
    [StringLength(100, ErrorMessage = "消费者姓名不能超过100个字符")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入手机号码")]
    [StringLength(20, ErrorMessage = "手机号码不能超过20个字符")]
    [RegularExpression(@"^1\d{10}$", ErrorMessage = "请输入11位中国大陆手机号码")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "邮箱不能超过100个字符")]
    [EmailAddress(ErrorMessage = "请输入有效的邮箱地址")]
    public string? Email { get; set; }
}
