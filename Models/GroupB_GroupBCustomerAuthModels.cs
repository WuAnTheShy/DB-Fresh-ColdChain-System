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
}

/// <summary>申请消费者密码重置验证码。</summary>
public sealed class GroupBCustomerPasswordResetCodeRequest
{
    [Required(ErrorMessage = "请输入手机号码")]
    [RegularExpression(@"^1\d{10}$", ErrorMessage = "请输入11位中国大陆手机号码")]
    public string Phone { get; set; } = string.Empty;
}

/// <summary>模拟短信验证码发送结果，仅用于当前演示支付环境。</summary>
public sealed class GroupBCustomerPasswordResetCodeResult
{
    public string VerificationId { get; init; } = string.Empty;
    public string SimulatedCode { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public int ExpiresInSeconds { get; init; }
}

/// <summary>使用手机号和模拟短信验证码重置消费者密码。</summary>
public sealed class GroupBCustomerPasswordResetRequest
{
    [Required(ErrorMessage = "请输入手机号码")]
    [RegularExpression(@"^1\d{10}$", ErrorMessage = "请输入11位中国大陆手机号码")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "验证码会话已失效，请重新获取")]
    public string VerificationId { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入验证码")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "请输入6位验证码")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入新密码")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "密码长度必须为8到100个字符")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "请再次输入新密码")]
    [Compare(nameof(NewPassword), ErrorMessage = "两次输入的密码不一致")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
