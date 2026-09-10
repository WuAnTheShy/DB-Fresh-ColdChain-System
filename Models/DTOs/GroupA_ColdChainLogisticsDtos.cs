namespace FreshColdChain.Models.DTOs;

// 运费报价请求中的单个商品项
public class FreightItemDto
{
    public string ProductID { get; set; } = string.Empty;
    // 订单侧已校验的供应商快照；旧调用未传时取商品当前供应商。
    public string? SupplierID { get; set; }
    public int Quantity { get; set; }
}

// 阶梯冷链运费报价请求
public class FreightQuoteRequest
{
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    // 供应商 ID（货物级售价归属；普通供应商固定为自己，管理员可指定）
    public string? SupplierID { get; set; }
    public decimal GoodsAmount { get; set; }
    public List<FreightItemDto> Items { get; set; } = new();
}

// 发货请求（B 组下单后调用，扣减批次并记录溯源）
public class ShipmentRequest
{
    public string OrderID { get; set; } = string.Empty;
    public string SupplierID { get; set; } = string.Empty;
    public List<FreightItemDto> Items { get; set; } = new();
}

// 运费报价结果
public class FreightQuoteDto
{
    public decimal FreightAmount { get; set; }
    public string RuleSummary { get; set; } = string.Empty;
    public List<FreightQuoteItemDto> Items { get; set; } = new();
}

// 报价结果中的单个商品明细
public class FreightQuoteItemDto
{
    public string ProductID { get; set; } = string.Empty;
    public string SupplierID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal => UnitPrice * Quantity;
}

// 精准溯源 DTO

// 溯源明细：单条批次扣减记录（含商品名、批次号、发货单等可读信息）
public class BatchAllocationDto
{
    public string AllocationID { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchID { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public int Quantity { get; set; }
    // 所属发货单 ID（反向溯源时标识去向）
    public string DeliveryID { get; set; } = string.Empty;
    // 物流运单号（反向溯源时定位快递）
    public string TrackingNo { get; set; } = string.Empty;
}

// 正向溯源：一张发货单的完整批次链路
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

// 反向溯源：一个批次被哪些发货单使用
public class BatchTraceDto
{
    public string BatchID { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public List<BatchAllocationDto> Allocations { get; set; } = new();
}

// 页面下拉选项


// 下拉选项通用项（Value=提交值，Text=显示文本）
public class OptionItemDto
{
    public string Value { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

// 有发货记录的订单摘要（溯源订单下拉数据源）
public class ShippedOrderOptionDto
{
    public string OrderID { get; set; } = string.Empty;
    public string? OrderNo { get; set; }
}
