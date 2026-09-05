using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Models.ViewModels;
using FreshColdChain.Services;
using FreshColdChain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers
{
    public class PromotersController : Controller
    {
        private const string SessionPromoterIdKey = "PromoterId";
        private const string SessionPromoterNameKey = "PromoterName";
        private readonly PromoterPortalDataProvider _dataProvider;
        private readonly ISupplierService _supplierService;
        private readonly PromoterService _promoterService;
        private readonly WithdrawalService _withdrawalService;
        private readonly PromoterIntroStore _introStore;

        public PromotersController(
            PromoterPortalDataProvider dataProvider,
            ISupplierService supplierService,
            PromoterService promoterService,
            WithdrawalService withdrawalService,
            PromoterIntroStore introStore)
        { 
            _dataProvider = dataProvider;
            _supplierService = supplierService;
            _promoterService = promoterService;
            _withdrawalService = withdrawalService;
            _introStore = introStore;
        }

        public async Task<IActionResult> Dashboard()
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;
            var vm = _dataProvider.BuildDashboard(promoterId);

            // 左下角速览：已上架商品 + 团内消费者
            vm.ListedProducts = await _promoterService.GetProductEntryDetailsAsync(promoterId);
            vm.BoundCustomers = await _promoterService.GetBoundCustomersByPromoterAsync(promoterId);
            return View(vm);
        }


        public IActionResult Performance()
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoter = _dataProvider.GetPromoter(GetPromoterId()!);
            return View(_dataProvider.BuildPerformance(promoter));
        }

        public IActionResult Commissions(string? status)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            return View(_dataProvider.BuildCommissions(GetPromoterId()!, status));
        }

        [HttpGet]
        public IActionResult Withdrawals()
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            return View(_dataProvider.BuildWithdrawals(GetPromoterId()!));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdrawals(WithdrawalApplyForm form)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;
            var promoter = _dataProvider.GetPromoter(promoterId);

            if (form.ApplyAmount <= 0)
            {
                TempData["ErrorMessage"] = "提现金额必须大于 0。";
                return View(_dataProvider.BuildWithdrawals(promoterId, form));
            }

            var platform = PromoterPayAccounts.Normalize(form.AccountPlatform);
            var boundAccount = promoter.GetBoundPayAccount(platform);
            if (string.IsNullOrWhiteSpace(boundAccount))
            {
                TempData["ErrorMessage"] = $"请先绑定{PromoterPayAccounts.Label(platform)}收款账户后再申请提现。";
                return View(_dataProvider.BuildWithdrawals(promoterId, form));
            }

            var request = new GroupC_WithdrawalRequest
            {
                PromoterId = promoterId,
                ApplyAmount = form.ApplyAmount,
                AccountPlatform = platform,
                AccountInfo = PromoterPayAccounts.FormatAccountInfo(platform, boundAccount)
            };

            var result = await _withdrawalService.ApplyWithdrawal(promoterId, request);
            if (result.IsSuccess)
                TempData["SuccessMessage"] = "提现申请已提交，请等待审核。";
            else
                TempData["ErrorMessage"] = result.ErrorMessage;

            var vm = _dataProvider.BuildWithdrawals(promoterId, form);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BindPayAccount(string platform, string accountNo, string? returnAction)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;

            var result = await _promoterService.BindPayAccountAsync(promoterId, platform, accountNo);
            if (result.IsSuccess)
                TempData["SuccessMessage"] = $"{PromoterPayAccounts.Label(platform)}已绑定。";
            else
                TempData["ErrorMessage"] = result.ErrorMessage;

            return RedirectToSafeAction(returnAction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnbindPayAccount(string platform, string? returnAction)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;

            var result = await _promoterService.UnbindPayAccountAsync(promoterId, platform);
            if (result.IsSuccess)
                TempData["SuccessMessage"] = $"{PromoterPayAccounts.Label(platform)}已解绑。";
            else
                TempData["ErrorMessage"] = result.ErrorMessage;

            return RedirectToSafeAction(returnAction);
        }

        public IActionResult Profile()
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            return View(_dataProvider.BuildProfile(GetPromoterId()!));
        }

        /// <summary>
        /// 团长自助更换预置头像（avatar 传空则恢复默认文字头像）。
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAvatar(string avatar)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;

            var result = await _promoterService.UpdatePromoterAvatarAsync(promoterId, avatar);
            if (result.IsSuccess)
                TempData["SuccessMessage"] = "头像已更新！";
            else
                TempData["ErrorMessage"] = result.ErrorMessage;

            return RedirectToAction("Profile");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Remove(SessionPromoterIdKey);
            HttpContext.Session.Remove(SessionPromoterNameKey);
            return RedirectToAction("RoleSelect", "Account");
        }

        private string? GetPromoterId() => HttpContext.Session.GetString(SessionPromoterIdKey);

        private IActionResult? EnsureLoggedIn()
        {
            if (string.IsNullOrEmpty(GetPromoterId()))
                return RedirectToAction("Login", "Account", new { role = "团长" });
            return null;
        }

        private IActionResult RedirectToSafeAction(string? returnAction)
        {
            var action = returnAction is "Profile" or "Withdrawals" ? returnAction : "Withdrawals";
            return RedirectToAction(action);
        }


        // ========= 商品上架（原“供应商绑定”模块改造）：搜索供应商→其商品 / 搜索商品→跨供应商，入团/移除 =========

        /// <summary>
        /// 商品上架页：关键词为供应商名称/ID → 返回该供应商提供的全部商品；
        /// 关键词为商品名称 → 返回所有供货该商品的（供应商×商品）组合。
        /// 无关键词时仅展示空搜索框（并附当前已入团商品数）。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ProductListing(string? keyword)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;

            var model = new ProductListingViewModel { Keyword = keyword ?? "" };

            // 1. 有关键词才调用 A 组接口搜索（供应商×商品）条目
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var response = await _supplierService.SearchSupplierProductEntriesAsync(keyword);
                if (response.IsSuccess)
                {
                    model.Entries = response.Data ?? new List<SupplierProductEntryDto>();
                }
                else
                {
                    TempData["ErrorMessage"] = "搜索失败：" + response.Message;
                }
            }

            // 2. 获取当前团长已入团的（商品|供应商）组合及其团长定价，用于前端判断显示“入团”还是“移除”
            var listed = await _promoterService.GetActiveProductEntriesAsync(promoterId);
            model.ListedKeys = listed.Select(x => $"{x.ProductId}|{x.SupplierId}").ToHashSet();
            model.ListedPrices = listed.ToDictionary(x => $"{x.ProductId}|{x.SupplierId}", x => x.PromoterPrice);

            // 3. 已上架商品详情（商品/供应商名称 + 报价/推荐价 + 团长定价）
            model.ListedProducts = await _promoterService.GetProductEntryDetailsAsync(promoterId);

            return View(model);
        }

        /// <summary>
        /// 提交搜索（PRG 模式，重定向回 ProductListing 并携带关键词统一渲染）
        /// </summary>
        [HttpPost]
        public IActionResult SearchProducts(string keyword)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            return RedirectToAction("ProductListing", new { keyword });
        }

        /// <summary>
        /// 已上架商品独立详情页：展示该（商品 × 供应商）组合的商品图、报价/推荐价/定价范围与商品介绍，
        /// 并允许团长在此修改团长定价与带货介绍（模仿消费者端商品详情页的独立页面样式）。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EntryDetail(string productId, string supplierId, string? keyword)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;

            var entry = await _promoterService.GetProductEntryDetailAsync(promoterId, productId, supplierId);
            if (entry == null)
            {
                TempData["ErrorMessage"] = "未找到该已上架商品，可能已被移除。";
                return RedirectToAction("ProductListing", new { keyword });
            }

            // 一次提供该供应商上传的该商品的所有图片（图文编辑器的“供应商图库”），
            // 以及当前已保存的图文介绍内容（历史纯文字自动包装为段落）
            var supplierImages = await _promoterService.GetSupplierProductImagesAsync(productId);
            var richContent = await _introStore.LoadAsync(entry.PromoterDesc);

            return View(new ProductEntryDetailViewModel
            {
                Keyword = keyword ?? "",
                ProductId = productId,
                SupplierId = supplierId,
                Entry = entry,
                SupplierImages = supplierImages,
                RichContent = richContent
            });
        }

        /// <summary>
        /// 将（商品，供应商）加入/移出团长入团商品（商品入团表 CRM_PRODUCT_ENTRIES）。
        /// 入团时携带团长定价 price（留空则默认推荐价），以及该组合的报价 supplyPrice、推荐价 defaultPrice，
        /// 由服务层校验定价规则：|团长价 - 推荐价| &lt; |推荐价 - 报价| / 2。
        /// description 为供应商商品文字，入团时默认复制为团长带货介绍。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ToggleProductEntry(string productId, string supplierId, string action, string? keyword,
            decimal? price, decimal supplyPrice, decimal defaultPrice, string? description = null)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;

            bool success = false;
            try
            {
                if (action == "bind")
                {
                    success = await _promoterService.AddProductEntryAsync(promoterId, productId, supplierId, price, supplyPrice, defaultPrice, description);
                    TempData["SuccessMessage"] = success
                        ? "商品已上架！点击记录行的「详情」按钮，可设置团长定价与带货介绍。"
                        : "入团失败，请重试。";
                }
                else if (action == "unbind")
                {
                    success = await _promoterService.RemoveProductEntryAsync(promoterId, productId, supplierId);
                    TempData["SuccessMessage"] = success ? "已将该商品移出入团商品！" : "移除失败，请重试。";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "操作发生异常：" + ex.Message;
            }

            // 操作完成后回商品上架页并保留上次关键词，便于继续操作
            return RedirectToAction("ProductListing", new { keyword });
        }

        /// <summary>
        /// 更新已入团（商品，供应商）组合的团长定价（仅已入团商品可定价）。
        /// 定价规则由服务层校验：|团长价 - 推荐价| &lt; |推荐价 - 报价| / 2，未填写则默认推荐价。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateEntryPrice(string productId, string supplierId, string? keyword,
            decimal? price, decimal supplyPrice, decimal defaultPrice)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;

            try
            {
                var success = await _promoterService.UpdateEntryPriceAsync(promoterId, productId, supplierId, price, supplyPrice, defaultPrice);
                TempData["SuccessMessage"] = success ? "团长定价已更新！" : "定价更新失败，请重试。";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "定价保存失败：" + ex.Message;
            }

            // 定价调整在独立详情页完成，保存后回到该商品详情页
            return RedirectToAction("EntryDetail", new { productId, supplierId, keyword });
        }

        /// <summary>
        /// 更新已入团（商品，供应商）组合的团长带货介绍文字（团长主动书写/改写商品介绍）。
        /// 给消费者端展示的始终是团长的文字。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateEntryDescription(string productId, string supplierId, string? keyword, string? contentJson)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;

            try
            {
                // 团长端提交的是富文本 JSON（title + 多段 text/images），
                // 服务层会写入 wwwroot 图文内容文件，并把相对路径存入 PROMOTERDESC。
                var success = await _promoterService.UpdateEntryDescriptionAsync(promoterId, productId, supplierId, contentJson);
                TempData["SuccessMessage"] = success ? "图文介绍已保存！" : "介绍保存失败，请重试。";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "介绍保存失败：" + ex.Message;
            }

            // 介绍编辑在独立详情页完成，保存后回到该商品详情页
            return RedirectToAction("EntryDetail", new { productId, supplierId, keyword });
        }

        /// <summary>
        /// 团长端图文介绍编辑器上传本地图片：保存到 wwwroot/uploads/promoter-img/，
        /// 返回可访问的相对路径；校验扩展名与大小（5MB），文件名随机生成。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UploadPromoterImage(IFormFile? file)
        {
            if (EnsureLoggedIn() != null)
                return Json(new { ok = false, message = "登录状态已失效，请刷新页面后重试。" });

            var result = await _introStore.UploadImageAsync(file);
            return Json(result.Ok
                ? new { ok = true, url = result.Url }
                : new { ok = false, message = result.Message });
        }


        [HttpGet]
        public async Task<IActionResult> BoundConsumers()
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;

            var promoterId = GetPromoterId()!;
            var promoterName = HttpContext.Session.GetString(SessionPromoterNameKey) ?? string.Empty;
            var items = await _promoterService.GetBoundCustomersByPromoterAsync(promoterId);

            return View(new PromoterBoundConsumersViewModel
            {
                PromoterId = promoterId,
                PromoterName = promoterName,
                TotalCount = items.Count,
                Items = items
            });
        }
    }
}


