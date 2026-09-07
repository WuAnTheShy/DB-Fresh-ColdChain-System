namespace FreshColdChain.Models
{
    public class GroupC_SysUser
    {
        public string UserId { get; set; } = string.Empty;           // 用户编号 (主键)
        public string Username { get; set; } = string.Empty;         // 登录名
        public string PasswordHash { get; set; } = string.Empty;     // 密码哈希
        public string RoleId { get; set; } = string.Empty;           // 角色编号 (外键, 关联 Sys_Roles)
        public string RealName { get; set; } = string.Empty;         // 真实姓名
        public string Phone { get; set; } = string.Empty;            // 手机号
        public string AdminKind { get; set; } = "ACCOUNT";           // 管理员种类: ACCOUNT/FINANCE/LOG/PRODUCT
        public string Status { get; set; } = "Enabled";              // 账号状态: Enabled / Disabled / Locked
        public DateTime? LastLoginTime { get; set; }                 // 最近登录时间
        public DateTime CreateTime { get; set; }                     // 创建时间
    }
}

