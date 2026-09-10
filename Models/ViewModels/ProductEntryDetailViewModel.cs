using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Models.ViewModels;

// 团长端“商品上架 → 已上架商品详情页”视图模型。
// 对应某个（商品 × 供应商）入团组合的独立详情页：展示商品图片 / 价格区间 / 商品介绍，
// 并支持团长在此页修改团长定价与带货介绍。
public class ProductEntryDetailViewModel
{
    // 返回商品上架页时保留的搜索关键词
    public string Keyword { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string SupplierId { get; set; } = string.Empty;

    // 该（商品 × 供应商）入团组合的详情数据
    public PromoterProductEntryDetailDto Entry { get; set; } = new();

    // 该供应商上传的该商品的所有图片（一次全量提供，供团长编辑图文介绍时插入；不再只取前 3 张）。
    public List<string> SupplierImages { get; set; } = new();

    // 当前已保存的图文介绍内容（若为历史纯文字则自动包装为单段），供图文编辑器回填。
    public PromoterRichContent? RichContent { get; set; }
}
