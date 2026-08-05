namespace DBFreshColdChain.Models.DTOs
{ 
    public class GroupC_LogAuditrails
    {
        public string LogId { get; set; } = string.Empty;           //日志编号，自动生成
        public string TableName { get; set; } = string.Empty;       //操作表名
        public string RecordId { get; set; } = string.Empty;        //记录编号
        public string ActionType { get; set; } = string.Empty;      //操作类型
        public string? OldValue { get; set; } = string.Empty;        //更改的旧值
        public string? NewValue { get; set; } = string.Empty;        //更改的新值
        public string OperatorType { get; set; } = string.Empty;    //操作者
        public string OperatorId { get; set; } = string.Empty;      //操作者ID
        public DateTime? OpTime { get; set; }                       //操作时间
    }
}