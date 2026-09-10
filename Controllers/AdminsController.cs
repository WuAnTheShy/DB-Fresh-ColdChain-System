using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Filters;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace FreshColdChain.Controllers
{
    public class AdminsController : Controller
    {
        private readonly PromoterService _promoterService;
        private readonly SystemAdminService _systemAdminService;
        private readonly WithdrawalService _withdrawalService;
        private readonly IRefundService _refundService;
        private readonly IPaymentService _paymentService;
        private readonly ITableLogService _tableLogService;
        private readonly ISupplierService _supplierService;
        public AdminsController(PromoterService promoterService, SystemAdminService systemAdminService, WithdrawalService withdrawalService, IRefundService refundService, IPaymentService paymentService, ITableLogService tableLogService, ISupplierService supplierService)
        {
            _promoterService = promoterService;
            _systemAdminService = systemAdminService;
            _withdrawalService = withdrawalService;
            _refundService = refundService;
            _paymentService = paymentService;
            _tableLogService = tableLogService;
            _supplierService = supplierService;
        }

        public async Task<IActionResult> PendingPromoters()
        {
            var pendingList = await _promoterService.GetPendingPromotersAsync();
            return View(pendingList);
        }
        // 管理员首页：按管理员种类分别呈现各自工作台
        public async Task<IActionResult> Dashboard()
        {
            var session = HttpContext.Session;
            // 日志管理员只有操作日志一个界面；商品管理员直达商品管理首页（A 组页面）
            if (AdminSession.IsKind(session, AdminSession.LogKind))
                return RedirectToAction(nameof(OperationLogs));
            if (AdminSession.IsKind(session, AdminSession.ProductKind))
                return RedirectToAction("AdminIndex", "Goods");

            var adminName = session.GetString("AdminName");
            ViewBag.AdminName = adminName ?? "管理员";
            ViewBag.AdminKind = AdminSession.GetKind(session);

            // —— 财务管理员工作台 ——
            if (AdminSession.IsKind(session, AdminSession.FinanceKind))
            {
                var pendingWithdrawals = await _withdrawalService.GetPendingWithdrawalsAsync();
                ViewBag.PendingWithdrawalCount = pendingWithdrawals.Count;
                ViewBag.PendingWithdrawalAmount = pendingWithdrawals.Sum(w => w.ApplyAmount);
                ViewBag.RecentWithdrawals = pendingWithdrawals.Take(5).ToList();

                var pendingRefunds = await _refundService.GetPendingRefundsAsync();
                ViewBag.PendingRefundCount = pendingRefunds.Count;
                ViewBag.PendingRefundAmount = pendingRefunds.Sum(r => r.RefundAmount);
                ViewBag.RecentRefunds = pendingRefunds.Take(5).ToList();
                return View();
            }

            // —— 账号管理员工作台（默认） ——
            var pendingPromoters = await _promoterService.GetPendingPromotersAsync();
            ViewBag.PendingPromoterCount = pendingPromoters.Count;

            var pendingSuppliers = await _supplierService.GetSuppliersByStatusAsync("Pending");
            ViewBag.PendingSupplierCount = pendingSuppliers.Data?.Count ?? 0;

            var pendingAdmins = await _systemAdminService.GetPendingAdminsAsync();
            ViewBag.PendingAdminCount = pendingAdmins.Count;
            return View();
        }
        // 团长审核通过
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePromoter(string promoterId)
        {
            string adminId = HttpContext.Session.GetString("AdminName") ?? "System";
            var result = await _systemAdminService.ApprovePromoterAsync(promoterId, adminId);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "审核通过成功！";
            }
            else
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            return RedirectToAction(nameof(AccountReview));
        }

        // 团长审核拒绝
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPromoter(string promoterId)
        {
            string adminId = HttpContext.Session.GetString("AdminName") ?? "System";
            var result = await _systemAdminService.RejectPromoterAsync(promoterId, adminId);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "审核拒绝成功！";
            }
            else
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            return RedirectToAction(nameof(AccountReview));
        }

        public async Task<IActionResult> PendingWithdrawals()
        {
            var list = await _withdrawalService.GetPendingWithdrawalsAsync();
            return View(list);
        }



        // 提现审核通过
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveWithdrawal(string withdrawalId)
        {
            var adminId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _withdrawalService.ApproveWithdrawal(adminId, new GroupC_WithdrawApproved
            {
                WithdrawalId = withdrawalId,
                UserId = adminId,
                AuditTime = DateTime.Now
            });
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(PendingWithdrawals));
        }

        // 提现驳回（需要输入原因）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectWithdrawal(string withdrawalId, string rejectReason)
        {
            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                TempData["ErrorMessage"] = "请填写驳回原因";
                return RedirectToAction(nameof(PendingWithdrawals));
            }
            var adminId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _withdrawalService.RejectWithdrawal(adminId, new GroupC_WithdrawRejected
            {
                WithdrawalId = withdrawalId,
                UserId = adminId,
                AuditTime = DateTime.Now,
                RejectReason = rejectReason
            });
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(PendingWithdrawals));
        }

        // 退款审核列表
        public async Task<IActionResult> PendingRefunds()
        {
            var list = await _refundService.GetPendingRefundsAsync();
            return View(list);
        }

        // 退款审核通过：执行退款资金操作（佣金/积分回滚、订单状态变更）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRefund(string refundId)
        {
            var adminId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _refundService.AuditRefund(refundId, true, adminId);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "退款已通过，资金回滚已执行" : result.ErrorMessage;
            return RedirectToAction(nameof(PendingRefunds));
        }

        // 退款审核驳回（需要输入原因）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRefund(string refundId, string rejectReason)
        {
            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                TempData["ErrorMessage"] = "请填写驳回原因";
                return RedirectToAction(nameof(PendingRefunds));
            }
            var adminId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _refundService.AuditRefund(refundId, false, adminId, rejectReason);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "退款申请已驳回" : result.ErrorMessage;
            return RedirectToAction(nameof(PendingRefunds));
        }

        // 支付流水与退款记录查询（支持时间区间、订单号、状态组合筛选）
        public async Task<IActionResult> PaymentRecords(DateTime? startDate, DateTime? endDate,
            string? orderId, string? payStatus, string? refundStatus)
        {
            // 结束日期按"含当天"处理：转为次日 0 点的开区间上界
            var endExclusive = endDate?.Date.AddDays(1);
            var model = new GroupC_PaymentQueryViewModel
            {
                StartDate = startDate?.Date,
                EndDate = endDate?.Date,
                OrderId = orderId,
                PayStatus = payStatus,
                RefundStatus = refundStatus,
                Payments = await _paymentService.SearchPaymentsAsync(startDate?.Date, endExclusive, orderId, payStatus),
                Refunds = await _refundService.SearchRefundsAsync(startDate?.Date, endExclusive, orderId, refundStatus)
            };
            return View(model);
        }

        // 操作日志查询（支持时间区间、表名、操作类型、操作者组合筛选）
        public async Task<IActionResult> OperationLogs(DateTime? startDate, DateTime? endDate,
            string? tableName, string? actionType, string? operatorId)
        {
            // 结束日期按"含当天"处理：转为次日 0 点的开区间上界
            var endExclusive = endDate?.Date.AddDays(1);
            var model = new GroupC_LogQueryViewModel
            {
                StartDate = startDate?.Date,
                EndDate = endDate?.Date,
                TableName = tableName,
                ActionType = actionType,
                OperatorId = operatorId,
                TableNames = await _tableLogService.GetLoggedTableNamesAsync(),
                Logs = await _tableLogService.SearchLogsAsync(startDate?.Date, endExclusive, tableName, actionType, operatorId)
            };
            return View(model);
        }

        // 角色管理（团长/供应商）

        // 角色管理主页：原 hub 已被账号管理员的「注册审核/账号新增/账号管理」三个页面取代
        public IActionResult RoleManagement()
        {
            return RedirectToAction(nameof(AccountReview));
        }

        // 团长管理列表（启用/禁用）
        public async Task<IActionResult> ManagePromoters()
        {
            var list = await _promoterService.GetAllPromotersAsync();
            return View(list.ToList());
        }

        // 团长启用/禁用（仅 Enable <-> Disable；Pending 需走注册审核）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPromoterStatus(string promoterId, string targetStatus)
        {
            var adminId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _systemAdminService.SetPromoterStatus(adminId, promoterId, targetStatus);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? (targetStatus == "Enable" ? "团长已重新启用" : "团长已禁用")
                    : result.ErrorMessage;
            return RedirectToAction(nameof(AccountManage));
        }

        // 管理员新增团长（免审核，直接生效）
        [HttpGet]
        public IActionResult AddPromoter()
        {
            return View(new GroupC_PromoterAddInfo { BaseCommissionRate = 0.03m });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPromoter(GroupC_PromoterAddInfo addInfo)
        {
            var result = await _promoterService.AddPromoterByAdmin(addInfo);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "团长已创建（免审核，直接生效）";
                return RedirectToAction(nameof(AccountManage));
            }
            TempData["ErrorMessage"] = result.ErrorMessage;
            return View(addInfo);
        }

        // 管理员更新团长基础佣金比例
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCommissionRate(GroupC_UpdateCommisionRequest request)
        {
            var result = await _promoterService.UpdateCommissionRate(request);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "佣金比例已更新" : result.ErrorMessage;
            return RedirectToAction(nameof(AccountManage));
        }

        // 供应商注册审核列表（Pending 状态）
        public async Task<IActionResult> PendingSuppliers()
        {
            var result = await _supplierService.GetSuppliersByStatusAsync("Pending");
            return View(result.Data ?? new List<SupplierDto>());
        }

        // 供应商审核通过（信用分由管理员在审核时填写）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveSupplier(string supplierId, int creditLevel)
        {
            if (creditLevel < 0 || creditLevel > 100)
            {
                TempData["ErrorMessage"] = "请填写信用分（0-100）后再通过审核";
                return RedirectToAction(nameof(AccountReview));
            }
            return await ChangeSupplierStatus(supplierId, "Active", "供应商入驻已通过审核", nameof(AccountReview), creditLevel);
        }

        // 供应商审核驳回（需要输入原因）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectSupplier(string supplierId, string rejectReason)
        {
            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                TempData["ErrorMessage"] = "请填写驳回原因";
                return RedirectToAction(nameof(AccountReview));
            }
            return await ChangeSupplierStatus(supplierId, "Rejected", $"供应商入驻已驳回：{rejectReason}", nameof(AccountReview));
        }

        // 供应商管理列表（启用/禁用）
        public async Task<IActionResult> ManageSuppliers()
        {
            var result = await _supplierService.GetAllSuppliersAsync();
            return View(result.Data ?? new List<SupplierDto>());
        }

        // 供应商启用/禁用（Active -> Disabled / Disabled|Rejected -> Active）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetSupplierStatus(string supplierId, string targetStatus)
        {
            return await ChangeSupplierStatus(supplierId, targetStatus,
                targetStatus == "Active" ? "供应商已重新启用" : "供应商已禁用", nameof(AccountManage));
        }

        // 供应商状态变更公共入口：调服务层做流转校验，成功后写操作日志
        // creditLevel 不为 null 时（审核通过）一并登记管理员填写的信用分
        private async Task<IActionResult> ChangeSupplierStatus(string supplierId, string targetStatus,
            string successMessage, string redirectAction, int? creditLevel = null)
        {
            var result = await _supplierService.SetSupplierStatusAsync(supplierId, targetStatus, creditLevel);
            if (result.IsSuccess)
            {
                await _tableLogService.WriteTableChangeLog(new GroupC_LogAuditrails
                {
                    TableName = "INV_SUPPLIERS",
                    ActionType = "Update",
                    OperatorType = "Admin",
                    OperatorId = HttpContext.Session.GetString("AdminName") ?? "Admin",
                    NewValue = JsonConvert.SerializeObject(new { SupplierId = supplierId, Status = targetStatus })
                });
                TempData["SuccessMessage"] = successMessage;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }
            return RedirectToAction(redirectAction);
        }

        // 账号管理员的三个工作页面

        // 注册审核：团长 + 供应商 + 管理员 三类待审账号
        public async Task<IActionResult> AccountReview()
        {
            ViewBag.PendingPromoters = await _promoterService.GetPendingPromotersAsync();
            var pendingSuppliers = await _supplierService.GetSuppliersByStatusAsync("Pending");
            ViewBag.PendingSuppliers = pendingSuppliers.Data ?? new List<SupplierDto>();
            ViewBag.PendingAdmins = await _systemAdminService.GetPendingAdminsAsync();
            return View();
        }

        // 管理员账号管理：团长 + 供应商 + 管理员（启禁用/佣金调整）
        public async Task<IActionResult> AccountManage()
        {
            var promoters = await _promoterService.GetAllPromotersAsync();
            ViewBag.Promoters = promoters.ToList();
            var suppliers = await _supplierService.GetAllSuppliersAsync();
            ViewBag.Suppliers = suppliers.Data ?? new List<SupplierDto>();
            ViewBag.Admins = await _systemAdminService.GetAllAdminsAsync();
            ViewBag.CurrentAdminId = HttpContext.Session.GetString("AdminId");
            return View();
        }

        // 账号新增：团长 + 供应商 + 管理员（均免审核直接生效）
        public IActionResult AccountCreate()
        {
            ViewBag.NewPromoter = new GroupC_PromoterAddInfo { BaseCommissionRate = 0.03m };
            ViewBag.AdminKinds = AdminSession.Kinds;
            return View();
        }

        // 管理员新增供应商（复用 B 组供应商创建服务）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSupplier(CreateSupplierDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.SupplierName))
            {
                TempData["ErrorMessage"] = "供应商名称不能为空";
                return RedirectToAction(nameof(AccountCreate));
            }
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "提交信息格式有误，请检查后重新提交";
                return RedirectToAction(nameof(AccountCreate));
            }
            // 初始信用分无需管理员填写，由服务层统一生成（默认 30）
            var result = await _supplierService.CreateSupplierAsync(dto);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "供应商已创建（直接生效）" : result.Message;
            return RedirectToAction(nameof(AccountManage));
        }

        // 管理员新增管理员账号（免审核，直接 Enabled）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdmin(GroupC_AdminRegisterInfo registerInfo)
        {
            if (registerInfo == null || string.IsNullOrWhiteSpace(registerInfo.LoginAccount))
            {
                TempData["ErrorMessage"] = "登录账号不能为空";
                return RedirectToAction(nameof(AccountCreate));
            }
            var result = await _systemAdminService.AddAdminByAdmin(registerInfo);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "管理员已创建（免审核，直接生效）" : result.ErrorMessage;
            return RedirectToAction(result.IsSuccess ? nameof(AccountManage) : nameof(AccountCreate));
        }

        // 管理员注册审核通过（账号管理员操作）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAdmin(string userId)
        {
            string operatorId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _systemAdminService.ApproveAdminAsync(userId, operatorId);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "管理员注册已审核通过，该管理员可登录" : result.ErrorMessage;
            return RedirectToAction(nameof(AccountReview));
        }

        // 管理员注册审核拒绝（可填原因）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectAdmin(string userId, string? rejectReason)
        {
            string operatorId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _systemAdminService.RejectAdminAsync(userId, operatorId, rejectReason);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "管理员注册申请已驳回" : result.ErrorMessage;
            return RedirectToAction(nameof(AccountReview));
        }

        // 管理员启用/禁用
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAdminStatus(string userId, string targetStatus)
        {
            string operatorId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _systemAdminService.SetAdminStatus(operatorId, userId, targetStatus);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? (targetStatus == "Enabled" ? "管理员已重新启用" : "管理员已禁用")
                    : result.ErrorMessage;
            return RedirectToAction(nameof(AccountManage));
        }

        // 财务管理

        // 财务管理主页（hub：提现审核 / 退款管理 / 支付流水入口 + 待办统计）
        public IActionResult FinanceManagement()
        {
            // 财务管理员工作台已并入 Dashboard，保留此 action 兼容旧链接
            return RedirectToAction(nameof(Dashboard));
        }
    }
}

