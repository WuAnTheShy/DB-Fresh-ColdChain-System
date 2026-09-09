// 负责管理与供应商（合作商）相关的交互请求

using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Filters;
using FreshColdChain.Interfaces;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Controllers;

public class SuppliersController : Controller
{
    private readonly ISupplierService _service;

    public SuppliersController(ISupplierService service) => _service = service;

    [HttpGet]
    [RequireAdmin]
    public async Task<IActionResult> Index(int pageIndex = 1, int pageSize = 10)
    {
        var r = await _service.GetSuppliersAsync(pageIndex, pageSize);
        return View(r.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var r = await _service.GetSupplierByIdAsync(id);
        if (!r.IsSuccess) return NotFound(r.Message);
        var quotes = await _service.GetSupplierProductQuotesAsync(id);
        ViewBag.Quotes = quotes.Data ?? new List<SupplierProductQuoteDto>();
        // 多供应商模式下产品数 = 该供应商已建立货物（上架供货）的商品数
        if (quotes.IsSuccess) r.Data!.ProductCount = quotes.Data!.Count;
        return View(r.Data);
    }

    // 供应商门户（供货价与商品图文入口已收敛到「我的货物」→ 货物行「编辑」）

    /// <summary>旧供应商登录页已合并到主入口（Account 角色选择登录），统一跳转过去。</summary>
    [HttpGet]
    public IActionResult Login()
    {
        return RedirectToAction("Login", "Account", new { role = "供应商" });
    }

    /// <summary>
    /// 供应商上传自己供货商品的图片（二进制直接写入 Inv_ProductImages.ImageData BLOB，
    /// 之后统一通过 /images/product/{ImageID} 接口读取，多机部署也不会出现文件丢失）。
    /// 仅允许 jpg/png/webp/gif，单张不超过 5MB。操作成功后返回「我的货物」的货物编辑页。
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadMyProductImage(string productId, IFormFile? imageFile)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction("Login", "Account", new { role = "供应商" });

        if (imageFile == null || imageFile.Length == 0)
        {
            TempData["Error"] = "请选择要上传的图片文件";
            return RedirectToAction("Edit", "Goods", new { productId });
        }

        // 文件类型白名单 + 大小校验（供应商照片为商品展示图）
        var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "image/gif"
        };
        if (!allowedContentTypes.Contains(imageFile.ContentType))
        {
            TempData["Error"] = "仅支持 jpg / png / webp / gif 格式的图片";
            return RedirectToAction("Edit", "Goods", new { productId });
        }
        const long maxBytes = 5 * 1024 * 1024;
        if (imageFile.Length > maxBytes)
        {
            TempData["Error"] = "单张图片不能超过 5MB";
            return RedirectToAction("Edit", "Goods", new { productId });
        }

        // 读入内存后交给服务层写入数据库 BLOB
        using var ms = new MemoryStream();
        await imageFile.CopyToAsync(ms);
        var r = await _service.AddProductImageAsync(supplierId, productId, ms.ToArray(), imageFile.ContentType);

        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction("Edit", "Goods", new { productId });
    }

    /// <summary>供应商删除自己供货商品的某张图片（BLOB 随行删除）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMyProductImage(string imageId, string productId)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction("Login", "Account", new { role = "供应商" });

        var r = await _service.DeleteProductImageAsync(supplierId, imageId);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction("Edit", "Goods", new { productId });
    }

    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [RequireAdmin]
    public IActionResult Create() => View(new CreateSupplierDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> Create(CreateSupplierDto dto)
    {
        var r = await _service.CreateSupplierAsync(dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [RequireAdmin]
    public async Task<IActionResult> Edit(string id)
    {
        var r = await _service.GetSupplierByIdAsync(id);
        if (!r.IsSuccess) return NotFound(r.Message);
        return View(new CreateSupplierDto
        {
            SupplierName = r.Data!.SupplierName, LicenseNo = r.Data.LicenseNo,
            ExpiryDate = r.Data.ExpiryDate, CreditLevel = r.Data.CreditLevel,
            ContactPhone = r.Data.ContactPhone, LoginAccount = r.Data.LoginAccount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> Edit(string id, CreateSupplierDto dto)
    {
        var r = await _service.UpdateSupplierAsync(id, dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> Delete(string id)
    {
        var r = await _service.DeleteSupplierAsync(id);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
