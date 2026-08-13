using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

/// <summary>
/// 新增消费者请求，原始密码仅用于生成哈希，不进入持久化模型。
/// </summary>
public sealed class CustomerCreateRequest
{
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

    [Required(ErrorMessage = "请输入登录密码")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "密码长度必须为8到100个字符")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "请再次输入登录密码")]
    [Compare(nameof(Password), ErrorMessage = "两次输入的密码不一致")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
