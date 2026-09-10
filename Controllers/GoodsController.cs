using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Filters;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Controllers;

// 货物（Inv_Goods）管理：
// - 供应商登录后维护「自己家」货物（售价/上下架/温区/描述），看不到别人家；
// - 平台管理员查看全部货物（含归属供应商）。
// 货物归属（SupplierID == 当前登录供应商）在服务层强制校验。
public class GoodsController : Controller
{
    private readonly IGoodsService _goodsService;
    private readonly IProductInventoryService _productService;
    private readonly IGoodsRepository _goodsRepo;
    private readonly IProductRepository _productRepo;
    private readonly IStockBatchRepository _batchRepo;

    public GoodsController(
        IGoodsService goodsService,
        IProductInventoryService productService,
        IGoodsRepository goodsRepo,
        IProductRepository productRepo,
        IStockBatchRepository batchRepo)
    {
        _goodsService = goodsService;
        _productService = productService;
        _goodsRepo = goodsRepo;
        _productRepo = productRepo;
        _batchRepo = batchRepo;
    }

    // 供应商：我的货物

    [HttpGet]
    public async Task<IActionResult> MyGoods()
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction("Login", "Account", new { role = "供应商" });

        var r = await _goodsService.GetSupplierGoodsAsync(supplierId);
        return View(r.Data ?? new List<GoodsDto>());
    }

    // 供应商对现有物品建立自己的货物（不设数量上限，数量走进货）
    [HttpGet]
    public async Task<IActionResult> Add()
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction("Login", "Account", new { role = "供应商" });

        var products = await _productService.GetProductsAsync(1, 1000);
        ViewBag.Products = products.Data?.Items ?? new List<ProductDto>();
        return View(new CreateGoodsDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(CreateGoodsDto dto)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction("Login", "Account", new { role = "供应商" });

        var r = await _goodsService.AddGoodsAsync(supplierId, dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(MyGoods));
    }

    // 供应商维护自己货物：编辑页（售价/温区/保质期/上下架 + 商品图文：文字介绍与图片）
    [HttpGet]
    public async Task<IActionResult> Edit(string productId)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction("Login", "Account", new { role = "供应商" });

        var goods = await _goodsRepo.GetAsync(productId, supplierId);
        if (goods == null)
        {
            TempData["Error"] = "未找到该货物或不属于你";
            return RedirectToAction(nameof(MyGoods));
        }

        // 商品图片：该商品全部图片，自己上传的排前、平台通用图在后（编辑页用于展示/删除/上传）
        var images = (await _productRepo.GetProductImagesAsync(productId))
            .OrderBy(i => i.SupplierID != supplierId)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.CreateTime)
            .Select(i => new SupplierProductImageDto
            {
                ImageID = i.ImageID,
                ImageUrl = i.ImageUrl,
                HasImageData = i.HasData,
                IsOwned = i.SupplierID == supplierId
            })
            .ToList();

        ViewBag.ProductId = productId;
        ViewBag.ProductName = goods.Product?.ProductName ?? productId;
        ViewBag.SupplierName = goods.Supplier?.SupplierName ?? supplierId;
        ViewBag.Images = images;
        // 温区跟随物品，不在货物侧维护，仅在编辑页只读展示
        ViewBag.StorageReq = goods.StorageReq ?? goods.Product?.StorageReq;
        return View(new UpdateGoodsDto
        {
            SalePrice = goods.SalePrice,
            ShelfLifeHours = goods.ShelfLifeHours,
            Description = goods.Description,
            Status = goods.Status
        });
    }

    // 供应商维护自己货物：改售价/温区/保质期/描述
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string productId, UpdateGoodsDto dto)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction("Login", "Account", new { role = "供应商" });

        var r = await _goodsService.UpdateGoodsAsync(supplierId, productId, dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(MyGoods));
    }

    // 供应商上下架自己货物
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string productId, string status)
    {
        var supplierId = HttpContext.Session.GetString("SupplierId");
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction("Login", "Account", new { role = "供应商" });

        var r = await _goodsService.SetGoodsStatusAsync(supplierId, productId, status);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(MyGoods));
    }

    // 供应商：进货/库存（只看自己批次，供应商固定为自己）

    [HttpGet]
    public async Task<IActionResult> StockIn(string productId)
    {
        var supplierId = SupplierSession.GetSupplierId(HttpContext.Session);
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction("Login", "Account", new { role = "供应商" });

        // 校验该货物属于当前供应商
        var goods = await _goodsRepo.GetAsync(productId, supplierId);
        if (goods == null)
        {
            TempData["Error"] = "未找到该货物或不属于你";
            return RedirectToAction(nameof(MyGoods));
        }

        var batches = await _batchRepo.GetByProductAndSupplierWithSupplierAsync(productId, supplierId);
        ViewBag.ProductId = productId;
        ViewBag.ProductName = goods.Product?.ProductName ?? productId;
        ViewBag.SupplierName = goods.Supplier?.SupplierName ?? supplierId;
        ViewBag.Batches = batches;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StockIn(string productId, int quantity, string? batchNo, DateTime? productionDate)
    {
        var supplierId = SupplierSession.GetSupplierId(HttpContext.Session);
        if (string.IsNullOrEmpty(supplierId))
            return RedirectToAction("Login", "Account", new { role = "供应商" });

        var r = await _productService.StockInAsync(
            new UpdateInventoryDto { ProductID = productId, Quantity = quantity },
            supplierId, batchNo, productionDate);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(StockIn), new { productId });
    }

    // 管理员：全部货物

    [HttpGet]
    [RequireAdmin]
    public async Task<IActionResult> AdminIndex(string? keyword)
    {
        var r = await _goodsService.GetAllGoodsAsync(keyword);
        ViewBag.Keyword = keyword;
        return View(r.Data ?? new List<GoodsDto>());
    }

    // 管理员上下架任意货物（按 物品+供应商 定位，不受当前登录供应商限制）
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> AdminToggleStatus(string productId, string supplierId, string status)
    {
        var r = await _goodsService.SetGoodsStatusAsync(supplierId, productId, status);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(AdminIndex));
    }

    // 商品管理员按“商品（物品）”整体下架/上架：连带该物品所有供应商的货物一并处理，
    // 保证数据一致（下架后不存在任何供应商仍在售该商品）。
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> AdminSetProductGoodsStatus(string productId, string status)
    {
        var r = await _goodsService.AdminSetProductGoodsStatusAsync(productId, status);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction("Index", "Products");
    }
}
