namespace FreshGroupSystem.Models.DTOs;

/// <summary>
/// 下单请求
/// </summary>
public class CreateOrderDto
{
    public int GroupLeaderId { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public string? Remark { get; set; }
}

public class OrderItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// 订单返回结果
/// </summary>
public class OrderResultDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string GroupLeaderName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
    public List<OrderItemResultDto> Items { get; set; } = new();
}

public class OrderItemResultDto
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public class UpdateOrderStatusDto
{
    public int Status { get; set; }
}
