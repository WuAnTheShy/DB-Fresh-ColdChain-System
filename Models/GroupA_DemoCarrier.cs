using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

/// <summary>独立物流商演示接入，默认关闭；可读取全部 Oracle 运单或限制供应商范围。</summary>
public sealed class DemoCarrierOptions
{
    public const string SectionName = "GroupA:DemoCarrier";
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public bool IncludeAllDatabaseShipments { get; set; }
    public string[] SupplierIds { get; set; } = [];
    public string CarrierCode { get; set; } = "FRESH_SIM";
    public string CarrierName { get; set; } = "鲜链模拟承运";
    public string PackageTemperature { get; set; } = "CHILLED";
    public int EstimatedTransitHours { get; set; } = 48;
}

public sealed class CarrierHandoffCommand
{
    [Required, StringLength(36)] public string OrderId { get; set; } = "";
    [Required, StringLength(36)] public string SupplierId { get; set; } = "";
}

public sealed class CarrierEventCommand
{
    [Required, StringLength(36)] public string EventId { get; set; } = GroupBIds.NewId();
    [Required, StringLength(30)] public string StatusCode { get; set; } = LogisticsStatusCodes.InTransit;
    [StringLength(200, ErrorMessage = "当前位置最多 200 字")] public string? Location { get; set; }
    [Required, StringLength(500)] public string Description { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.Now;
    public decimal? TemperatureCelsius { get; set; }
}

public sealed class CarrierShipmentSummary
{
    public string? DeliveryId { get; set; }
    public string OrderId { get; set; } = "";
    public string OrderNo { get; set; } = "";
    public string SupplierId { get; set; } = "";
    public string? TrackingNo { get; set; }
    public string? StatusCode { get; set; }
    public string OrderStatusCode { get; set; } = "";
    public string OrderStatusName => OrderStatusCode switch
    {
        OrderStatusCodes.PendingPayment => "待支付",
        OrderStatusCodes.Paid => "已支付",
        OrderStatusCodes.Shipped => "已发货",
        OrderStatusCodes.Completed => "已完成",
        OrderStatusCodes.Cancelled => "已取消",
        OrderStatusCodes.Refunding => "退款中",
        OrderStatusCodes.Refunded => "已退款",
        "DELIVERED" => "历史已送达",
        _ => OrderStatusCode
    };
    public bool HasShipment => !string.IsNullOrWhiteSpace(DeliveryId);
    public bool CanHandoff => !HasShipment && OrderStatusCode == OrderStatusCodes.Paid;
    public string StatusName => HasShipment
        ? LogisticsStatusCodes.GetName(StatusCode)
        : OrderStatusCode == OrderStatusCodes.Paid ? "待供应商发货" : "历史运单缺失";
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
}
