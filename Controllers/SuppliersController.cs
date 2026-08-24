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
