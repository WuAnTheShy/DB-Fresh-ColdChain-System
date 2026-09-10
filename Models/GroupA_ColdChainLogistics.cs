using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

// A组可配置的地区、温层阶梯运费规则。
[Table("Log_FreightTemplates")]
public class LogFreightTemplate
{
    [Key, Column("TemplateID")] public string TemplateID { get; set; } = Guid.NewGuid().ToString();
    [Column("TemplateName")] public string TemplateName { get; set; } = string.Empty;
    [Column("DestinationProvince")] public string DestinationProvince { get; set; } = "*";
    [Column("DestinationCity")] public string DestinationCity { get; set; } = "*";
    [Column("DestinationDistrict")] public string DestinationDistrict { get; set; } = "*";
    [Column("TemperatureZone")] public string TemperatureZone { get; set; } = "CHILLED";
    [Column("BaseWeight")] public decimal BaseWeight { get; set; }
    [Column("BaseFee")] public decimal BaseFee { get; set; }
    [Column("ExtraWeightUnit")] public decimal ExtraWeightUnit { get; set; } = 1m;
    [Column("ExtraWeightFee")] public decimal ExtraWeightFee { get; set; }
    [Column("PackagingFee")] public decimal PackagingFee { get; set; }
    [Column("FreeShippingThreshold")] public decimal? FreeShippingThreshold { get; set; }
    [Column("IsEnabled")] public int IsEnabled { get; set; } = 1;

    [NotMapped]
    public bool IsEnabledChecked
    {
        get => IsEnabled == 1;
        set => IsEnabled = value ? 1 : 0;
    }
}

// 一个供应商对应一张冷链发货履约单。
// 承运商、外部运单号、时效、备注等扩展属性原存于一对一表 Log_LogisticsDetails，
// 已物理合并到本表（见历史迁移）。
// IsRegistered 标记扩展信息是否已登记，用于保持登记幂等语义。
[Table("Log_ExpressDeliveries")]
public class LogExpressDelivery
{
    [Key, Column("DeliveryID")] public string DeliveryID { get; set; } = Guid.NewGuid().ToString();
    [Column("OrderID")] public string OrderID { get; set; } = string.Empty;
    [Column("SupplierID")] public string SupplierID { get; set; } = string.Empty;
    [Column("TrackingNo")] public string TrackingNo { get; set; } = string.Empty;
    [Column("PackageTemp")] public string PackageTemp { get; set; } = "CHILLED";
    [Column("CarrierCode")] public string? CarrierCode { get; set; }
    [Column("CarrierName")] public string? CarrierName { get; set; }
    [Column("CarrierTrackingKey")] public string? CarrierTrackingKey { get; set; }
    [Column("EstimatedArrivalAt")] public DateTime? EstimatedArrivalAt { get; set; }
    [Column("Remark")] public string? Remark { get; set; }
    [Column("IsRegistered")] public int IsRegistered { get; set; }
    [Column("LogisticsStatus")] public string LogisticsStatus { get; set; } = "SHIPPED";
    [Column("ShippedAt")] public DateTime ShippedAt { get; set; } = DateTime.Now;
}

// 订单商品和实际扣减库存批次之间的精准溯源映射。
[Table("Log_FulfillmentBatchItems")]
public class LogFulfillmentBatchItem
{
    [Key, Column("AllocationID")] public string AllocationID { get; set; } = Guid.NewGuid().ToString();
    [Column("DeliveryID")] public string DeliveryID { get; set; } = string.Empty;
    [Column("ProductID")] public string ProductID { get; set; } = string.Empty;
    [Column("BatchID")] public string BatchID { get; set; } = string.Empty;
    [Column("Quantity")] public int Quantity { get; set; }
}
