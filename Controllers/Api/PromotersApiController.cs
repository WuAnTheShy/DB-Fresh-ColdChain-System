using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

/// <summary>
/// 消费者端：查看团长信息与团长带货商品（公开浏览，无需登录）。
/// </summary>
[ApiController]
[Route("api/promoters")]
public sealed class PromotersApiController(
    PromoterService promoterService) : ControllerBase
{
    /// <summary>
    /// 团长列表（仅启用状态），供消费者端浏览/选择团长。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPromoters(CancellationToken cancellationToken)
    {
        var all = await promoterService.GetAllPromotersAsync();
        var list = all
            .Where(p => string.Equals(p.Status, "Enable", StringComparison.OrdinalIgnoreCase))
            .Select(p =>
            {
                var avatar = string.IsNullOrWhiteSpace(p.Avatar) ? null : p.Avatar.Trim();
                return new
                {
                    p.PromoterId,
                    p.PromoterName,
                    p.Status,
                    Avatar = avatar,
                    AvatarUrl = string.IsNullOrWhiteSpace(avatar)
                        ? null
                        : $"/images/avatars/{avatar}.png"
                };
            })
            .ToList();

        return Ok(list);
    }

    /// <summary>
    /// 查看团长基本信息（含头像）。
    /// </summary>
    [HttpGet("{promoterId}")]
    public async Task<IActionResult> GetPromoter(string promoterId, CancellationToken cancellationToken)
    {
        var info = await promoterService.GetPromoterBasicInfoAsync(promoterId, cancellationToken);
        if (info == null)
            return NotFound(new { message = "团长不存在" });

        return Ok(new
        {
            info.PromoterId,
            info.PromoterName,
            info.Status,
            info.Avatar,
            info.AvatarUrl
        });
    }

    /// <summary>
    /// 查看团长带货商品列表：返回（商品文字介绍、价格、商品图片等）。
    /// 文字介绍为团长写的介绍（未写时兜底为供应商商品文字）。
    /// </summary>
    [HttpGet("{promoterId}/featured-products")]
    public async Task<IActionResult> GetFeaturedProducts(string promoterId, CancellationToken cancellationToken)
    {
        var info = await promoterService.GetPromoterBasicInfoAsync(promoterId, cancellationToken);
        if (info == null)
            return NotFound(new { message = "团长不存在" });

        var products = await promoterService.GetPromoterFeaturedProductsAsync(promoterId);

        return Ok(new
        {
            promoter = new
            {
                info.PromoterId,
                info.PromoterName,
                info.Status,
                info.Avatar,
                info.AvatarUrl
            },
            products = products.Select(p => new
            {
                p.ProductID,
                p.ProductName,
                p.Unit,
                p.SupplierID,
                p.SupplierName,
                p.SupplyPrice,
                p.DefaultPrice,
                p.Price,
                p.PromoterDesc,
                p.Images
            })
        });
    }

    /// <summary>
    /// 查看某团长对某商品的「团长推文」（图文介绍）：返回标题与顺序段落，段落图片为相对站点路径。
    /// 商品不是该团长的在售入团商品时返回 404；无图文内容时返回 hasIntro=false。
    /// </summary>
    [HttpGet("{promoterId}/featured-products/{productId}/intro")]
    public async Task<IActionResult> GetFeaturedProductIntro(string promoterId, string productId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productId))
            return BadRequest(new { message = "缺少商品编号" });

        var intro = await promoterService.GetProductIntroAsync(promoterId, productId);
        if (intro == null)
            return NotFound(new { message = "该商品不是此团长的在售商品" });

        return Ok(new
        {
            hasIntro = intro.HasIntro,
            title = intro.Title,
            sections = intro.Sections.Select(s => new { s.Text, s.Images })
        });
    }
}
