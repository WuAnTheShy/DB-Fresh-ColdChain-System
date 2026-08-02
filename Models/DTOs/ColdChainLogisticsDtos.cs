namespace FreshGroupSystem.Models.DTOs;

/// <summary>运费报价请求中的单个商品项</summary>
public class FreightItemDto
{
    public string ProductID { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>阶梯冷链运费报价请求</summary>
public class FreightQuoteRequest
{
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public decimal GoodsAmount { get; set; }
    public List<FreightItemDto> Items { get; set; } = new();
}

/// <summary>发货请求（B 组下单后调用，扣减批次并记录溯源）</summary>
public class ShipmentRequest
{
    public string OrderID { get; set; } = string.Empty;
    public string SupplierID { get; set; } = string.Empty;
    public List<FreightItemDto> Items { get; set; } = new();
}

/// <summary>运费报价结果</summary>
public class FreightQuoteDto
{
    public decimal FreightAmount { get; set; }
    public string RuleSummary { get; set; } = string.Empty;
}
