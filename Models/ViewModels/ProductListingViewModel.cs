using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Models.ViewModels;

/// <summary>
/// 团长端“商品上架”视图模型。
/// 搜索供应商/商品 → 得到可入团的（供应商×商品）条目列表；
/// ListedKeys 记录当前团长已入团的“商品|供应商”组合，用于前端渲染入团/移除按钮状态。
/// </summary>
public class ProductListingViewModel
{
    /// <summary>搜索关键词（供应商名称/ID 或 商品名称）</summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>搜索命中的可入团（供应商×商品）条目</summary>
    public List<SupplierProductEntryDto> Entries { get; set; } = new();

    /// <summary>当前团长已入团的组合键集合，格式：“商品ID|供应商ID”</summary>
    public HashSet<string> ListedKeys { get; set; } = new();

    /// <summary>当前团长已入团组合的团长定价，键格式：“商品ID|供应商ID”（未定价时为 null）</summary>
    public Dictionary<string, decimal?> ListedPrices { get; set; } = new();

    /// <summary>当前团长已入团商品详情列表（已上架商品展示区，含商品名/供应商名/报价/推荐价/团长定价）</summary>
    public List<PromoterProductEntryDetailDto> ListedProducts { get; set; } = new();
}
