namespace FreshColdChain.Models.DTOs;

public class InventoryDto
{
    public string StockID { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int TotalQty { get; set; }
    public int LockedQty { get; set; }
    public int AvailableQty { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class UpdateInventoryDto
{
    public string ProductID { get; set; } = string.Empty;
    public int Quantity { get; set; }

    // 供货供应商ID（可选）。指定时出库只扣该供应商的批次，
    // 不传则保持旧语义：按商品全批次 FEFO 出库。
    public string? SupplierID { get; set; }
}

public class StockBatchDto
{
    public string BatchID { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? SupplierID { get; set; }
    public string? SupplierName { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal InPrice { get; set; }
    public int InitialQty { get; set; }
    public int CurrentQty { get; set; }
    public string Status { get; set; } = "ACTIVE";
}

// 入库时可选供应商报价选项（下拉用）
public class SupplierQuoteOptionDto
{
    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal SupplyPrice { get; set; }
    // 生效保质期（小时）：供应商声明的，未声明则用产品典型值
    public int? ShelfLifeHours { get; set; }
}

public class CreateStockBatchDto
{
    public string ProductID { get; set; } = string.Empty;
    public string? SupplierID { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int InitialQty { get; set; }
}

// 下单校验用的“供应商货物 + 供应商级可用量”快照。
// 粒度 = (商品, 供应商)：同一商品的不同供应商各自拥有货物售价与可用库存。
public class SupplierGoodsInventoryDto
{
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SupplierID { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    // 该供应商对商品设置的售价（货物售价）
    public decimal SalePrice { get; set; }
    // 货物上下架状态：ACTIVE=可售
    public string Status { get; set; } = "ACTIVE";
    // 该 (商品, 供应商) 的活跃批次可用量
    public int AvailableQty { get; set; }
}
