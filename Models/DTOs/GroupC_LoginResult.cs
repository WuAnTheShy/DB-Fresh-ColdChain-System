namespace FreshColdChain.Models.DTOs
{
    public class SupplierLoginResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string SuppierId { get; set; }
    }
    public class CustomerLoginResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }
}

