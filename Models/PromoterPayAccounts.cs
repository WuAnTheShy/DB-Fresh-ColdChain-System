using System.Text.RegularExpressions;

namespace FreshColdChain.Models
{
    // 团长收款账户平台常量与校验。
    public static class PromoterPayAccounts
    {
        public const string WeChat = "WeChat";
        public const string Alipay = "Alipay";
        public const string BankCard = "BankCard";

        public static readonly string[] All = [WeChat, Alipay, BankCard];

        public static string Normalize(string? platform) => platform switch
        {
            Alipay => Alipay,
            BankCard => BankCard,
            WeChat => WeChat,
            _ => WeChat
        };

        public static bool IsValidPlatform(string? platform) =>
            platform is WeChat or Alipay or BankCard;

        public static string Label(string? platform) => Normalize(platform) switch
        {
            Alipay => "支付宝",
            BankCard => "银行卡",
            _ => "微信"
        };

        public static string Placeholder(string? platform) => Normalize(platform) switch
        {
            Alipay => "支付宝账号（手机号或邮箱）",
            BankCard => "银行卡号",
            _ => "微信号"
        };

        public static (bool Ok, string Error) ValidateAccount(string? platform, string? accountNo)
        {
            var normalizedPlatform = Normalize(platform);
            var value = (accountNo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return (false, $"请填写{Label(normalizedPlatform)}账号");

            if (normalizedPlatform == BankCard)
            {
                var digits = Regex.Replace(value, @"[\s-]", string.Empty);
                if (!Regex.IsMatch(digits, @"^\d{10,19}$"))
                    return (false, "银行卡号应为 10~19 位数字");
            }
            else if (value.Length is < 2 or > 64)
            {
                return (false, $"{Label(normalizedPlatform)}账号长度应在 2~64 个字符之间");
            }

            return (true, string.Empty);
        }

        // 平台 + 账号 拼成展示串（账号为空时只显示平台标签）。
        public static string FormatAccountInfo(string? platform, string? accountNo) =>
            $"{Label(platform)}：{(accountNo ?? string.Empty).Trim()}";
    }
}
