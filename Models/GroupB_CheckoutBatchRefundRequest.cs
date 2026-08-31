using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

public sealed class CheckoutBatchRefundRequest
{
    [Required(ErrorMessage = "请填写退款原因")]
    [StringLength(200, ErrorMessage = "退款原因不能超过200字")]
    public string Remark { get; set; } = string.Empty;
}
