namespace FreshColdChain.Models.DTOs
{
    public class GroupC_AdminLoginResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } 
        public string? UserName { get; set;} = string.Empty;
        public string? UserId { get; set; }
    }

}