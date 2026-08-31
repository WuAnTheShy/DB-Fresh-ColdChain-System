using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

/// <summary>B 组消费者登录请求。</summary>
public sealed class GroupBCustomerLoginRequest
{
    [Required(ErrorMessage = "请输入手机号码")]
    [RegularExpression(@"^1\d{10}$", ErrorMessage = "请输入11位中国大陆手机号码")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入登录密码")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "密码长度必须为8到100个字符")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>B 组消费者身份验证结果，不暴露密码哈希。</summary>
public sealed class GroupBCustomerLoginResult
{
    public string CustomerId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? Avatar { get; init; }
}
