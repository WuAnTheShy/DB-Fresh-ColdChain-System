//商品图片读取接口：从 Inv_ProductImages.ImageData（BLOB）直接返回图片字节流
//所有端（供应商门户 / 团长商品上架 / 消费者端）统一引用 /images/product/{imageId}

using FreshColdChain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers;

[ApiController]
[Route("images/product")]
public sealed class ProductImagesController(ISupplierService supplierService) : ControllerBase
{
    // 按图片 ID 返回图片二进制。
    // 图片 ID 每次上传新生成（不可变），删除后行即消失，可放心缓存。
    [HttpGet("{imageId}")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Get(string imageId, CancellationToken cancellationToken)
    {
        var r = await supplierService.GetProductImageContentAsync(imageId);
        if (!r.IsSuccess || r.Data?.Data == null)
            return NotFound();

        return File(r.Data.Data, r.Data.ContentType ?? "image/jpeg");
    }
}
