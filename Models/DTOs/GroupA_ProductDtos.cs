namespace FreshColdChain.Models.DTOs;

public class ProductDto
{
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryID { get; set; }
    public string? CategoryName { get; set; }
    public string? Unit { get; set; }
    public decimal? WeightKG { get; set; }
    public decimal? VolumeLitre { get; set; }
    public int? ExpiryHours { get; set; }
    public string? StorageReq { get; set; }
    public int AvailableStock { get; set; }

    // 兼容跨组商品目录：价格、上下架状态和供应商归属于 Inv_Goods。
    public decimal DefaultPrice { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? SupplierName { get; set; }

    // 商品文字介绍（供应商维护）
    public string? Description { get; set; }

    // 商品图片（按展示顺序，最多 3 张）
    public List<string> Images { get; set; } = new();
}

// 产品详情页"供应商图文"区块数据：报价该商品的供应商选项 + 选中供应商的简介与图片
public class ProductSupplierMediaDto
{
    // 报价该商品的全部供应商（下拉选项）
    public List<SupplierMediaOptionDto> Suppliers { get; set; } = new();

    public string? SelectedSupplierID { get; set; }
    public string? SelectedSupplierName { get; set; }

    // 选中供应商的简介（该供应商未写时为商品通用介绍；未选择供应商时为 null）
    public string? Description { get; set; }

    // 选中供应商的图片：自己上传的在前，平台通用图在后，最多 3 张
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

// 商品图片上传内容（新增/编辑商品时随表单提交，服务层将二进制写入
// Inv_ProductImages.ImageData，对外地址统一为 /images/product/{ImageID}，
// 供供应商门户 / 团长商品上架 / 消费者前端展示）。
public class ProductImageUploadDto
{
    public byte[] Data { get; set; } = Array.Empty<byte>();

    // MIME 类型（如 image/jpeg）
    public string ContentType { get; set; } = "image/jpeg";
}
