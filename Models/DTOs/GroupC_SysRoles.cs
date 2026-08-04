namespace DBFreshColdChain.Models.DTOs
{
    public class GroupC_SysRole
    {
        public string RoleId { get; set; } = string.Empty;           //角色编号 (主键)
        public string RoleName { get; set; } = string.Empty;         //角色名称
        public string RoleCode { get; set; } = string.Empty;         //角色编码
        public string Description { get; set; } = string.Empty;      //角色描述
        public string Status { get; set; } = "Enabled";              //状态
        public DateTime CreateTime { get; set; }                     //创建时间
    }
}

