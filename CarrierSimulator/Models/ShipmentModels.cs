using System.ComponentModel.DataAnnotations;

namespace CarrierSimulator.Models;

public sealed class ShipmentSummary
{
    public string? DeliveryId { get; set; }
    public string OrderId { get; set; } = "";
    public string OrderNo { get; set; } = "";
    public string SupplierId { get; set; } = "";
    public string? TrackingNo { get; set; }
    public string OrderStatusName { get; set; } = "";
    public string StatusName { get; set; } = "";
    public bool HasShipment { get; set; }
    public bool CanHandoff { get; set; }
}
public sealed class HandoffInput
{
    [Required] public string OrderId { get; set; } = "";
    [Required] public string SupplierId { get; set; } = "";
    public string? Keyword { get; set; }
}
public sealed class ShipmentDetail
{
    public string DeliveryId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string SupplierId { get; set; } = "";
    public string? TrackingNo { get; set; }
    public string PackageTemperature { get; set; } = "";
    public string StatusCode { get; set; } = "";
    public string StatusName { get; set; } = "";
    public DateTime? EstimatedArrivalAt { get; set; }
    public bool HasException { get; set; }
    public string? ExceptionMessage { get; set; }
    public List<TrackingEvent> Events { get; set; } = [];
}
public sealed class TrackingEvent
{
    public string EventId { get; set; } = "";
    public string StatusName { get; set; } = "";
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public decimal? TemperatureCelsius { get; set; }
    public bool IsTemperatureException { get; set; }
}
public sealed class EventInput
{
    [Required, StringLength(36)] public string DeliveryId { get; set; } = "";
    [Required, StringLength(36)] public string EventId { get; set; } = Guid.NewGuid().ToString("N");
    [Required, StringLength(30)] public string StatusCode { get; set; } = "IN_TRANSIT";
    [StringLength(200, ErrorMessage = "当前位置最多 200 字")] public string? Location { get; set; }
    [Required(ErrorMessage = "请填写事件说明"), StringLength(500)] public string Description { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.Now;
    public decimal? TemperatureCelsius { get; set; }
}
public sealed class ShipmentsPage
{
    public string? Keyword { get; set; }
    public List<ShipmentSummary> Shipments { get; set; } = [];
    public ShipmentDetail? Selected { get; set; }
    public EventInput Input { get; set; } = new();
    public string? Error { get; set; }
    public string ShopUrl { get; set; } = "";
    public int ShipmentCount => Shipments.Count(item => item.HasShipment);
    public int WaitingCount => Shipments.Count - ShipmentCount;
    public static readonly Dictionary<string, string> StatusNames = new()
    {
        ["SHIPPED"] = "已发货", ["IN_TRANSIT"] = "运输中", ["OUT_FOR_DELIVERY"] = "派送中",
        ["DELIVERED"] = "已签收", ["EXCEPTION"] = "物流异常", ["RETURNING"] = "退回中", ["RETURNED"] = "已退回"
    };
}
