namespace FreshColdChain.Models.DTOs;

public class SupplierDto
{
    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? LicenseNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int CreditLevel { get; set; }
    public string? ContactPhone { get; set; }
    public string? LoginAccount { get; set; }
    public int ProductCount { get; set; }
    /// <summary>状态: Pending / Active / Disabled / Rejected</summary>
    public string Status { get; set; } = "Active";
}

public class CreateSupplierDto
{
    public string SupplierName { get; set; } = string.Empty;
    public string? LicenseNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int CreditLevel { get; set; }
    public string? ContactPhone { get; set; }
    public string? LoginAccount { get; set; }
    public string? LoginPassword { get; set; }
}

/// <summary>
/// 供应商的某个产品及其供货价（未报价时 SupplyPrice 为 null）
/// </summary>
public class SupplierProductQuoteDto
{
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal? SupplyPrice { get; set; }
    public DateTime? UpdateTime { get; set; }
    /// <summary>产品典型保质期（小时）</summary>
    public int? ProductExpiryHours { get; set; }
    /// <summary>该供应商声明的保质期（小时），未声明为 null</summary>
    public int? ShelfLifeHours { get; set; }

    /// <summary>商品文字介绍（供应商维护，团长可参考/复制/改写）</summary>
    public string? Description { get; set; }

    /// <summary>商品图片（按展示顺序，供应商维护；含图片 ID 供删除操作使用）</summary>
    public List<SupplierProductImageDto> Images { get; set; } = new();
}

/// <summary>供应商门户展示用的商品图片条目（ImageID 用于删除，ImageUrl 用于展示）</summary>
public class SupplierProductImageDto
{
    public string ImageID { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>数据库里是否真有图片二进制数据（false 表示旧记录/未上传成功）</summary>
    public bool HasImageData { get; set; }

    /// <summary>是否当前供应商自己上传的图片（false = 平台通用图，供应商不可删除）</summary>
    public bool IsOwned { get; set; }
}

/// <summary>/images/product/{id} 接口返回的图片内容（BLOB 二进制 + MIME 类型）</summary>
public class ProductImageContentDto
{
    public byte[]? Data { get; set; }
    public string? ContentType { get; set; }
}

/// <summary>
/// 跨组查询用 — C 组通过此 DTO 获取供应商账户信息（不含密码）
/// </summary>
public class SupplierAccountDto
{
    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? LicenseNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int CreditLevel { get; set; }
    public string? ContactPhone { get; set; }
    public string? LoginAccount { get; set; }
}
