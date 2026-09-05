namespace FreshColdChain.Models.DTOs;

/// <summary>运费报价请求中的单个商品项</summary>
public class FreightItemDto
{
    public string ProductID { get; set; } = string.Empty;
    /// <summary>订单侧已校验的供应商快照；旧调用未传时取商品当前供应商。</summary>
    public string? SupplierID { get; set; }
    public int Quantity { get; set; }
}

/// <summary>阶梯冷链运费报价请求</summary>
public class FreightQuoteRequest
{
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    /// <summary>供应商 ID（货物级售价归属；普通供应商固定为自己，管理员可指定）</summary>
    public string? SupplierID { get; set; }
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
    public List<FreightQuoteItemDto> Items { get; set; } = new();
}

/// <summary>报价结果中的单个商品明细</summary>
public class FreightQuoteItemDto
{
    public string ProductID { get; set; } = string.Empty;
    public string SupplierID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal => UnitPrice * Quantity;
}

// ========== 精准溯源 DTO ==========

/// <summary>溯源明细：单条批次扣减记录（含商品名、批次号、发货单等可读信息）</summary>
public class BatchAllocationDto
{
    public string AllocationID { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchID { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public int Quantity { get; set; }
    /// <summary>所属发货单 ID（反向溯源时标识去向）</summary>
    public string DeliveryID { get; set; } = string.Empty;
    /// <summary>物流运单号（反向溯源时定位快递）</summary>
    public string TrackingNo { get; set; } = string.Empty;
}

/// <summary>正向溯源：一张发货单的完整批次链路</summary>
public class DeliveryTraceDto
{
    public string DeliveryID { get; set; } = string.Empty;
    public string OrderID { get; set; } = string.Empty;
    public string SupplierID { get; set; } = string.Empty;
    public string TrackingNo { get; set; } = string.Empty;
    public string LogisticsStatus { get; set; } = string.Empty;
    public DateTime ShippedAt { get; set; }
    public List<BatchAllocationDto> Allocations { get; set; } = new();
}

/// <summary>反向溯源：一个批次被哪些发货单使用</summary>
public class BatchTraceDto
{
    public string BatchID { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public List<BatchAllocationDto> Allocations { get; set; } = new();
}

/// <summary>发货单摘要（列表用）</summary>
public class ShipmentSummaryDto
{
    public string DeliveryID { get; set; } = string.Empty;
    public string OrderID { get; set; } = string.Empty;
    public string SupplierID { get; set; } = string.Empty;
    public string TrackingNo { get; set; } = string.Empty;
    public string LogisticsStatus { get; set; } = string.Empty;
    public DateTime ShippedAt { get; set; }
    public int ItemCount { get; set; }
}
