namespace FreshColdChain.Models;

/// <summary>B 组 VARCHAR2(36) 主键生成与校验规则。</summary>
public static class GroupBIds
{
    public const int MaxLength = 36;

    public static string NewId() => Guid.NewGuid().ToString("N");

    public static bool IsValid(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim().Length <= MaxLength;
    }
}

/// <summary>本地演示数据使用的稳定标识。</summary>
public static class GroupBDemoIds
{
    public const string BasicLevel = "00000000000000000000000000000001";
    public const string Customer = "10000000000000000000000000000001";
    public const string Address = "20000000000000000000000000000001";
    public const string Coupon = "30000000000000000000000000000001";
    public const string CouponRecord = "40000000000000000000000000000001";
}
