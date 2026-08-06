using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

/// <summary>A组可配置的地区、温层阶梯运费规则。</summary>
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
}

/// <summary>一个供应商对应一张冷链发货履约单。</summary>
[Table("Log_ExpressDeliveries")]
public class LogExpressDelivery
{
    [Key, Column("DeliveryID")] public string DeliveryID { get; set; } = Guid.NewGuid().ToString();
    [Column("OrderID")] public string OrderID { get; set; } = string.Empty;
    [Column("SupplierID")] public string SupplierID { get; set; } = string.Empty;
    [Column("TrackingNo")] public string TrackingNo { get; set; } = string.Empty;
    [Column("PackageTemp")] public string PackageTemp { get; set; } = "CHILLED";
    [Column("LogisticsStatus")] public string LogisticsStatus { get; set; } = "SHIPPED";
    [Column("ShippedAt")] public DateTime ShippedAt { get; set; } = DateTime.Now;
}

/// <summary>订单商品和实际扣减库存批次之间的精准溯源映射。</summary>
[Table("Log_FulfillmentBatchItems")]
public class LogFulfillmentBatchItem
{
    [Key, Column("AllocationID")] public string AllocationID { get; set; } = Guid.NewGuid().ToString();
    [Column("DeliveryID")] public string DeliveryID { get; set; } = string.Empty;
    [Column("ProductID")] public string ProductID { get; set; } = string.Empty;
    [Column("BatchID")] public string BatchID { get; set; } = string.Empty;
    [Column("Quantity")] public int Quantity { get; set; }
}
