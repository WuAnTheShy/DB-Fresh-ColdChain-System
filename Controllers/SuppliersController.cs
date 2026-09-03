//负责管理与供应商（合作商）相关的交互请求

using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Interfaces;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Controllers;

public class SuppliersController : Controller
{
    private readonly ISupplierService _service;

    public SuppliersController(ISupplierService service) => _service = service;

    [HttpGet]
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
        // 多供应商模式下产品数 = 该供应商已报价的产品数
        if (quotes.IsSuccess) r.Data!.ProductCount = quotes.Data!.Count;
        return View(r.Data);
    }

    // ========== 供应商门户（供应商登录后自己维护报价）==========

    [HttpGet]
    public IActionResult Login()
    {
        // 已登录则直接进入我的报价
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("SupplierId")))
            return RedirectToAction(nameof(MyQuotes));
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string loginAccount, string password)
    {
        var r = await _service.SupplierLoginAsync(loginAccount ?? "", password ?? "");
        if (!r.IsSuccess)
        {
            ModelState.AddModelError("", r.Message);
            return View();
        }
        HttpContext.Session.SetString("SupplierId", r.Data!.SupplierID);
        return RedirectToAction(nameof(MyQuotes));
    }

    /// <summary>供应商自己的报价页：只能看到并维护自己的产品报价</summary>
    [HttpGet]
    public async Task<IActionResult> MyQuotes()
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction(nameof(Login));

        var supplier = await _service.GetSupplierByIdAsync(supplierId);
        if (!supplier.IsSuccess)
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        var quotes = await _service.GetAllProductQuotesForSupplierAsync(supplierId);
        ViewBag.Quotes = quotes.Data ?? new List<SupplierProductQuoteDto>();
        return View(supplier.Data);
    }

    /// <summary>供应商设置自己产品的供货价和保质期（supplierId 取自登录态，无法替别人报价）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMyPrice(string productId, decimal supplyPrice, int? shelfLifeHours)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction(nameof(Login));

        var r = await _service.SetSupplyPriceAsync(supplierId, productId, supplyPrice, shelfLifeHours);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(MyQuotes));
    }

    /// <summary>商品信息编辑页：文字介绍 + 图片管理（须已报价的商品）</summary>
    [HttpGet]
    public async Task<IActionResult> EditProductInfo(string productId)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction(nameof(Login));

        var r = await _service.GetProductInfoForSupplierAsync(supplierId, productId);
        if (!r.IsSuccess)
        {
            TempData["Error"] = r.Message;
            return RedirectToAction(nameof(MyQuotes));
        }

        ViewBag.SupplierName = (await _service.GetSupplierByIdAsync(supplierId)).Data?.SupplierName;
        return View(r.Data);
    }

    /// <summary>供应商维护自己供货商品的文字介绍（图片由供应商提供，文字供团长参考/复制/改写）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMyDescription(string productId, string? description)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction(nameof(Login));

        var r = await _service.UpdateProductDescriptionAsync(supplierId, productId, description);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(EditProductInfo), new { productId });
    }

    /// <summary>
    /// 供应商上传自己供货商品的图片（二进制直接写入 Inv_ProductImages.ImageData BLOB，
    /// 之后统一通过 /images/product/{ImageID} 接口读取，多机部署也不会出现文件丢失）。
    /// 仅允许 jpg/png/webp/gif，单张不超过 5MB。
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadMyProductImage(string productId, IFormFile? imageFile)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction(nameof(Login));

        if (imageFile == null || imageFile.Length == 0)
        {
            TempData["Error"] = "请选择要上传的图片文件";
            return RedirectToAction(nameof(MyQuotes));
        }

        // 文件类型白名单 + 大小校验（供应商照片为商品展示图）
        var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "image/gif"
        };
        if (!allowedContentTypes.Contains(imageFile.ContentType))
        {
            TempData["Error"] = "仅支持 jpg / png / webp / gif 格式的图片";
            return RedirectToAction(nameof(MyQuotes));
        }
        const long maxBytes = 5 * 1024 * 1024;
        if (imageFile.Length > maxBytes)
        {
            TempData["Error"] = "单张图片不能超过 5MB";
            return RedirectToAction(nameof(MyQuotes));
        }

        // 读入内存后交给服务层写入数据库 BLOB
        using var ms = new MemoryStream();
        await imageFile.CopyToAsync(ms);
        var r = await _service.AddProductImageAsync(supplierId, productId, ms.ToArray(), imageFile.ContentType);

        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(EditProductInfo), new { productId });
    }

    /// <summary>供应商删除自己供货商品的某张图片（BLOB 随行删除）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMyProductImage(string imageId, string productId)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction(nameof(Login));

        var r = await _service.DeleteProductImageAsync(supplierId, imageId);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(EditProductInfo), new { productId });
    }

    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateSupplierDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSupplierDto dto)
    {
        var r = await _service.CreateSupplierAsync(dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
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
    public async Task<IActionResult> Edit(string id, CreateSupplierDto dto)
    {
        var r = await _service.UpdateSupplierAsync(id, dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var r = await _service.DeleteSupplierAsync(id);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
