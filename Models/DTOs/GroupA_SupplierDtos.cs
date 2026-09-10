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
    // 该供应商关联的商品名称（仅名称）
    public List<string> ProductNames { get; set; } = new();
    // 状态: Pending / Active / Disabled / Rejected
    public string Status { get; set; } = "Active";

    public bool IsActiveAccount =>
        string.Equals(Status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase);
    public bool IsPendingAccount =>
        string.Equals(Status?.Trim(), "Pending", StringComparison.OrdinalIgnoreCase);
}

public class CreateSupplierDto
{
    public string SupplierName { get; set; } = string.Empty;
    public string? LicenseNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    // 信用分（新增时留空，由后台统一生成初始值 30；已传入合法值时沿用传入值）
    public int CreditLevel { get; set; }
    public string? ContactPhone { get; set; }
    public string? LoginAccount { get; set; }
    public string? LoginPassword { get; set; }
    // 入库状态：留空按 Active（管理员代建直接生效）；供应商自助入驻传 Pending 待审核
    public string? Status { get; set; }
}

// 供应商的某个产品及其供货价（未报价时 SupplyPrice 为 null）
public class SupplierProductQuoteDto
{
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal? SupplyPrice { get; set; }
    public DateTime? UpdateTime { get; set; }
    // 产品典型保质期（小时）
    public int? ProductExpiryHours { get; set; }
    // 该供应商声明的保质期（小时），未声明为 null
    public int? ShelfLifeHours { get; set; }

    // 商品文字介绍（供应商维护，团长可参考/复制/改写）
    public string? Description { get; set; }

    // 商品图片（按展示顺序，供应商维护；含图片 ID 供删除操作使用）
    public List<SupplierProductImageDto> Images { get; set; } = new();
}

// 供应商门户展示用的商品图片条目（ImageID 用于删除，ImageUrl 用于展示）
public class SupplierProductImageDto
{
    public string ImageID { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    // 数据库里是否真有图片二进制数据（false 表示旧记录/未上传成功）
    public bool HasImageData { get; set; }

    // 是否当前供应商自己上传的图片（false = 平台通用图，供应商不可删除）
    public bool IsOwned { get; set; }
}

// /images/product/{id} 接口返回的图片内容（BLOB 二进制 + MIME 类型）
public class ProductImageContentDto
{
    public byte[]? Data { get; set; }
    public string? ContentType { get; set; }
}

// 跨组查询用 — C 组通过此 DTO 获取供应商账户信息（不含密码）
public class SupplierAccountDto
{
    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? LicenseNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int CreditLevel { get; set; }
    public string? ContactPhone { get; set; }
    public string? LoginAccount { get; set; }
    public string Status { get; set; } = "Active";
}
