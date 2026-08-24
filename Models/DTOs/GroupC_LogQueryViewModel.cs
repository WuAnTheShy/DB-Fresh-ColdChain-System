namespace FreshColdChain.Models.DTOs
{
    /// <summary>
    /// 管理端"操作日志"查询页视图模型
    /// </summary>
    public class GroupC_LogQueryViewModel
    {
        // 查询条件（回显用）
        public DateTime? StartDate { get; set; }                 // 开始日期（含当天）
        public DateTime? EndDate { get; set; }                   // 结束日期（含当天）
        public string? TableName { get; set; }                   // 操作表名
        public string? ActionType { get; set; }                  // 操作类型: Create / Update / Delete / Copy
        public string? OperatorId { get; set; }                  // 操作者ID（精确匹配）

        // 筛选项与结果
        public List<string> TableNames { get; set; } = new();    // 日志中出现过的表名
        public List<GroupC_LogAuditrails> Logs { get; set; } = new();
    }
}
