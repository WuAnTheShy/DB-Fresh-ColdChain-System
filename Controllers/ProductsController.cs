// 负责处理与生鲜产品和商品库存相关的页面跳转与请求交互

using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Filters;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Controllers;

// 产品目录与平台级库存（入库/出库/低库存/批次）为「商品管理员」职能：
// 普通供应商通过「我的货物 → 进货/库存」完成自己货物的入库，不再进入平台物品管理页。
[RequireAdmin]
public class ProductsController : Controller
{
    private readonly IProductInventoryService _service;
    private readonly IProductRepository _productRepo;

    public ProductsController(IProductInventoryService service, IProductRepository productRepo)
    {
        _service = service;
        _productRepo = productRepo;
    }

    // 产品

    [HttpGet]
    public async Task<IActionResult> Index(int pageIndex = 1, int pageSize = 10, string? keyword = null)
    {
        var r = await _service.GetProductsAsync(pageIndex, pageSize, keyword);
        ViewBag.Keyword = keyword;
        return View(r.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, string? supplierId)
    {
        var r = await _service.GetProductByIdAsync(id);
        if (!r.IsSuccess) return NotFound(r.Message);

        // 供应商图文区块：下拉选择供应商后显示其简介与图片
        var media = await _service.GetSupplierProductMediaAsync(id, supplierId);
        ViewBag.Media = media.Data ?? new ProductSupplierMediaDto();
        return View(r.Data);
    }

    [HttpGet]
    [RequireAdmin]
    public async Task<IActionResult> Create()
    {
        await LoadCategoriesAsync();
        return View(new CreateProductDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> Create(CreateProductDto dto, IFormFile[]? images)
    {
        // 图片队列校验：新增商品至少 1 张（避免“新商品没有图”），类型/大小白名单
        var (uploads, uploadError) = await ReadValidImagesAsync(images);
        if (!string.IsNullOrEmpty(uploadError))
            ModelState.AddModelError("", uploadError);
        else if (uploads.Count == 0)
            ModelState.AddModelError("", "请上传至少 1 张商品图片（jpg / png / webp / gif，单张 ≤ 5MB）");

        if (string.IsNullOrWhiteSpace(dto.ProductName))
            ModelState.AddModelError(nameof(CreateProductDto.ProductName), "请填写商品名称");
        if (string.IsNullOrWhiteSpace(dto.CategoryID))
            ModelState.AddModelError(nameof(CreateProductDto.CategoryID), "请选择商品分类（如 水果 / 蔬菜 / 肉蛋）");

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            return View(dto);
        }

        var r = await _service.CreateProductAsync(dto);
        if (!r.IsSuccess)
        {
            await LoadCategoriesAsync();
            ModelState.AddModelError("", r.Message);
            return View(dto);
        }

        var attach = await _service.AddProductImagesAsync(r.Data!.ProductID, uploads);
        TempData[attach.IsSuccess ? "Success" : "Error"] = attach.IsSuccess
            ? $"商品「{r.Data.ProductName}」创建成功，已上传 {uploads.Count} 张图片。"
            : $"商品创建成功，但图片上传失败：{attach.Message}";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [RequireAdmin]
    public async Task<IActionResult> Edit(string id)
    {
        var r = await _service.GetProductByIdAsync(id);
        if (!r.IsSuccess) return NotFound(r.Message);
        var p = r.Data!;
        await LoadCategoriesAsync();
        ViewBag.ProductId = id;
        ViewBag.ProductImages = await LoadPlatformImagesAsync(id);
        return View(new UpdateProductDto
        {
            ProductName = p.ProductName, CategoryID = p.CategoryID, Unit = p.Unit,
            WeightKG = p.WeightKG, VolumeLitre = p.VolumeLitre,
            ExpiryHours = p.ExpiryHours, StorageReq = p.StorageReq,
            Description = p.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> Edit(string id, UpdateProductDto dto, IFormFile[]? images)
    {
        var (uploads, uploadError) = await ReadValidImagesAsync(images);
        if (!string.IsNullOrEmpty(uploadError))
            ModelState.AddModelError("", uploadError);
        if (string.IsNullOrWhiteSpace(dto.ProductName))
            ModelState.AddModelError(nameof(UpdateProductDto.ProductName), "请填写商品名称");

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            ViewBag.ProductId = id;
            ViewBag.ProductImages = await LoadPlatformImagesAsync(id);
            return View(dto);
        }

        var r = await _service.UpdateProductAsync(id, dto);
        if (!r.IsSuccess)
        {
            await LoadCategoriesAsync();
            ViewBag.ProductId = id;
            ViewBag.ProductImages = await LoadPlatformImagesAsync(id);
            ModelState.AddModelError("", r.Message);
            return View(dto);
        }

        TempData["Success"] = r.Message;
        if (uploads.Count > 0)
        {
            var attach = await _service.AddProductImagesAsync(id, uploads);
            TempData[attach.IsSuccess ? "Success" : "Error"] = attach.IsSuccess
                ? $"商品信息已保存，已新增 {uploads.Count} 张商品图片。"
                : $"{r.Message}，但图片上传失败：{attach.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// 商品管理员删除商品的一张平台通用图片（仅 SupplierID 为空的平台图；
    /// 供应商自己在「我的货物」上传的图需由对应供应商删除，不在商品编辑页管理）。
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> DeleteImage(string id, string imageId)
    {
        var img = await _productRepo.GetProductImageByIdAsync(imageId);
        if (img == null)
            TempData["Error"] = "图片不存在或已删除";
        else if (!string.Equals(img.ProductID, id, StringComparison.OrdinalIgnoreCase))
            TempData["Error"] = "该图片不属于此商品";
        else if (!string.IsNullOrEmpty(img.SupplierID))
            TempData["Error"] = "该图片由供应商上传，需由对应供应商在「我的货物」中删除";
        else
        {
            await _productRepo.DeleteProductImageAsync(imageId);
            TempData["Success"] = "商品图片已删除";
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> Delete(string id)
    {
        var r = await _service.DeleteProductAsync(id);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    // 库存

    [HttpGet]
    public async Task<IActionResult> Inventory(string productId)
    {
        // 自动标记已过期批次
        await _service.MarkExpiredBatchesAsync();
        var r = await _service.GetInventoryAsync(productId);
        if (!r.IsSuccess) return NotFound(r.Message);
        var batches = await _service.GetBatchesAsync(productId);
        ViewBag.Batches = batches.Data;
        var options = await _service.GetStockInSupplierOptionsAsync(productId);
        ViewBag.SupplierOptions = options.Data ?? new List<SupplierQuoteOptionDto>();
        return View(r.Data);
    }

    [HttpGet]
    public async Task<IActionResult> LowStock(int threshold = 10)
    {
        var r = await _service.GetLowStockProductsAsync(threshold);
        ViewBag.Threshold = threshold;
        return View(r.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StockIn(string productId, int quantity, string? supplierId, string? batchNo, DateTime? productionDate)
    {
        var r = await _service.StockInAsync(
            new UpdateInventoryDto { ProductID = productId, Quantity = quantity },
            supplierId, batchNo, productionDate);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Inventory), new { productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StockOut(string productId, int quantity, string? supplierId)
    {
        var r = await _service.StockOutAsync(new UpdateInventoryDto
        {
            ProductID = productId,
            Quantity = quantity,
            SupplierID = string.IsNullOrWhiteSpace(supplierId) ? null : supplierId
        });
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Inventory), new { productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBatch(CreateStockBatchDto dto)
    {
        var r = await _service.AddBatchAsync(dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Inventory), new { productId = dto.ProductID });
    }

    // 表单辅助

    /// <summary>填充分类下拉选项（按名称排序，供新增/编辑商品选择，避免手输分类 ID）</summary>
    private async Task LoadCategoriesAsync()
    {
        var r = await _service.GetAllCategoriesAsync();
        ViewBag.Categories = r.Data?
            .OrderBy(c => c.CategoryName, StringComparer.CurrentCultureIgnoreCase)
            .ToList() ?? new List<CategoryDto>();
    }

    /// <summary>读取该商品的平台通用图（SupplierID 为空；供应商各自上传的货物图不在此编辑页管理）</summary>
    private async Task<List<InvProductImage>> LoadPlatformImagesAsync(string productId)
    {
        var all = await _productRepo.GetProductImagesAsync(productId);
        return all.Where(i => string.IsNullOrEmpty(i.SupplierID)).ToList();
    }

    /// <summary>
    /// 图片队列入参校验：仅接受 jpg/png/webp/gif，单张 ≤ 5MB，最多 9 张。
    /// 任一文件不合法即整体失败并返回错误信息（避免部分上传造成排序空洞）。
    /// </summary>
    private static async Task<(List<ProductImageUploadDto> Images, string? Error)> ReadValidImagesAsync(IFormFile[]? files)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "image/gif"
        };
        const long maxBytes = 5 * 1024 * 1024;
        const int maxCount = 9;

        var list = new List<ProductImageUploadDto>();
        if (files == null || files.Length == 0)
            return (list, null);

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            if (list.Count >= maxCount)
                return (list, $"最多只能上传 {maxCount} 张图片");
            if (!allowed.Contains(file.ContentType))
                return (list, "仅支持 jpg / png / webp / gif 格式的图片");
            if (file.Length > maxBytes)
                return (list, $"单张图片不能超过 5MB（当前文件 {file.FileName} 过大）");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            list.Add(new ProductImageUploadDto
            {
                Data = ms.ToArray(),
                ContentType = file.ContentType
            });
        }
        return (list, null);
    }
}
