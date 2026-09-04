namespace FreshColdChain.Models.DTOs;

public class ProductDto
{
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? Unit { get; set; }
    public decimal? WeightKG { get; set; }
    public decimal? VolumeLitre { get; set; }
    public int? ExpiryHours { get; set; }
    public string? StorageReq { get; set; }
    public int AvailableStock { get; set; }

    /// <summary>商品文字介绍（供应商维护）</summary>
    public string? Description { get; set; }

    /// <summary>商品图片（按展示顺序，最多 3 张）</summary>
    public List<string> Images { get; set; } = new();
}

/// <summary>产品详情页"供应商图文"区块数据：报价该商品的供应商选项 + 选中供应商的简介与图片</summary>
public class ProductSupplierMediaDto
{
    /// <summary>报价该商品的全部供应商（下拉选项）</summary>
    public List<SupplierMediaOptionDto> Suppliers { get; set; } = new();

    public string? SelectedSupplierID { get; set; }
    public string? SelectedSupplierName { get; set; }

    /// <summary>选中供应商的简介（该供应商未写时为商品通用介绍；未选择供应商时为 null）</summary>
    public string? Description { get; set; }

    /// <summary>选中供应商的图片：自己上传的在前，平台通用图在后，最多 3 张</summary>
    public List<string> Images { get; set; } = new();
}

public class SupplierMediaOptionDto
{
    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
}

public class CreateProductDto
{
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryID { get; set; }
    public string? Unit { get; set; }
    public decimal? WeightKG { get; set; }
    public decimal? VolumeLitre { get; set; }
    public int? ExpiryHours { get; set; }
    public string? StorageReq { get; set; }
    public string? Description { get; set; }
}

public class UpdateProductDto
{
    public string? ProductName { get; set; }
    public string? CategoryID { get; set; }
    public string? Unit { get; set; }
    public decimal? WeightKG { get; set; }
    public decimal? VolumeLitre { get; set; }
    public int? ExpiryHours { get; set; }
    public string? StorageReq { get; set; }
    public string? Description { get; set; }
}
