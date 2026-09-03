using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Models.ViewModels;

/// <summary>
/// 团长端“商品上架 → 已上架商品详情页”视图模型。
/// 对应某个（商品 × 供应商）入团组合的独立详情页：展示商品图片 / 价格区间 / 商品介绍，
/// 并支持团长在此页修改团长定价与带货介绍。
/// </summary>
public class ProductEntryDetailViewModel
{
    /// <summary>返回商品上架页时保留的搜索关键词</summary>
    public string Keyword { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string SupplierId { get; set; } = string.Empty;

    /// <summary>该（商品 × 供应商）入团组合的详情数据</summary>
    public PromoterProductEntryDetailDto Entry { get; set; } = new();
}
