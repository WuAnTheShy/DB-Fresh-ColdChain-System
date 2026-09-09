using FreshColdChain.Models.DTOs;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Interfaces;
using FreshColdChain.Services;
using FreshColdChain.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Diagnostics;
namespace FreshColdChain.Controllers
{
    public class AccountController : Controller
    {
        private readonly PromoterService _promoterService;
        private readonly SystemAdminService  _systemAdminService;
        private readonly AccountService _accountService;
        
        public AccountController(PromoterService promoterService, SystemAdminService systemAdminService, AccountService accountService)
        {
            _promoterService = promoterService;
            _systemAdminService = systemAdminService;
            _accountService = accountService;
        }
        public IActionResult RoleSelect()
        {
            return View();
        }

        [HttpGet("/Login/supplier")]
        public IActionResult SupplierLogin()
        {
            return RedirectToAction(nameof(Login), new { role = "供应商" });
        }

        public IActionResult Login(string? role = null)
        {
            if (role == "团长")
            {
                ViewBag.Role = "团长";
            }
            else if (role == "消费者")
            {
                ViewBag.Role = "消费者";
                return Redirect("/app/");
            }
            else if (role == "管理员")
            {
                ViewBag.Role = "管理员";
                ViewBag.AdminKinds = AdminSession.Kinds;
            }
            else if (role == "供应商")
            {
                ViewBag.Role = "供应商";
            }
            else 
            {
                ViewBag.Role = "缺省角色信息";
            }

            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password,string? role = null, string? adminKind = null)
        {
            ViewBag.Role = role;
            if (role == "管理员")
            {
                ViewBag.AdminKinds = AdminSession.Kinds;
            }
            if (role == "团长")
            {
                var loginResult = _promoterService.LoginPromoter(username, password);
                if(loginResult.IsSuccess == true)  //登录成功
                {
                    HttpContext.Session.SetString("PromoterName", loginResult.PromoterName ?? username);
                    HttpContext.Session.SetString("PromoterId", loginResult.PromoterId);
                    return RedirectToAction("Dashboard", "Promoters");
                }
                ModelState.AddModelError("", loginResult.Message);
                return View();
            }
            else if (role == "管理员")
            {
                var loginResult = _systemAdminService.LoginAdmin(username, password, adminKind);
                if (loginResult.IsSuccess == true && !string.IsNullOrWhiteSpace(loginResult.UserId))
                {
                    HttpContext.Session.SetString("AdminId", loginResult.UserId);
                    HttpContext.Session.SetString("AdminName", username);
                    // 记录管理员种类，登录后按种类进入各自的首页/导航
                    HttpContext.Session.SetString(FreshColdChain.Filters.AdminSession.KindKey,
                        string.IsNullOrEmpty(adminKind) ? FreshColdChain.Filters.AdminSession.AccountKind : adminKind);
                    return RedirectToAction("Dashboard", "Admins");
                }
                ModelState.AddModelError("", loginResult.Message);
                return View();
            }
            else if(role == "供应商")
            {
                var loginResult = await _accountService.LoginSupplier(username, password);
                if (loginResult.IsSuccess == true)  //登录成功
                {
                    HttpContext.Session.SetString("SupplierName", username);
                    // 同步建立供应商门户会话（SuppliersController 以 SupplierId 判断登录态），
                    // 避免进入供应商首页后还需二次登录
                    if (!string.IsNullOrEmpty(loginResult.SuppierId))
                        HttpContext.Session.SetString("SupplierId", loginResult.SuppierId);
                    // 供应商登录后直接进入「我的货物」工作台（供货价/上下架/入库的唯一入口；
                    // 商品管理员负责平台级物品与库存管理）
                    return RedirectToAction("MyGoods", "Goods");
                }
                ModelState.AddModelError("", loginResult.Message);
                return View();
            }
            return View();
        }
        // 管理员退出：清理管理员相关会话后回到管理员登录页
        public IActionResult Logout()
        {
            FreshColdChain.Filters.AdminSession.SignOut(HttpContext.Session);
            return RedirectToAction(nameof(Login), "Account", new { role = "管理员" });
        }

        [HttpGet]
        public IActionResult Register(string? role = null)
        {
            ViewBag.Role = role;
            if (role == "团长")
            {
                return PromoterRegister();
            }
            else if(role == "管理员")
            {
                return AdminRegister();
            }
            else if (role == "供应商")
            {
                return SupplierRegister();
            }
            return View();
        }
        public IActionResult PromoterRegister()
        {
            ViewBag.Role = "团长";
            return View("PromoterRegister");
        }
        public IActionResult AdminRegister()
        {
            ViewBag.Role = "管理员";
            ViewBag.AdminKinds = AdminSession.Kinds;
            return View("AdminRegister");
        }
        public IActionResult SupplierRegister()
        {
            ViewBag.Role = "供应商";
            // 供应商注册走公开入驻入口（Suppliers/Register，创建 Pending 待审核账号）；
            // 原跳转目标 Suppliers/Create 为管理员专属操作（[RequireAdmin]），未登录会被弹回登录页
            return RedirectToAction("Register", "Suppliers");
        }

        [HttpPost]
        public async Task<IActionResult> PromoterRegister(string promotername, string username, string password, string phonenumber, string password_again)
        {
            if (password != password_again)
            {
                ModelState.AddModelError("", "两次输入密码不同");
                return View("PromoterRegister");
            }
            var registerInfo = new GroupC_PromoterRegisterInfo();
            registerInfo.PromoterName = promotername;
            registerInfo.Phone = phonenumber;
            registerInfo.LoginPassword = password;
            registerInfo.LoginAccount = username;
            var registerResult = new GroupC_PromoterRegisterResult();
            registerResult = await _promoterService.RegisterPromoter(registerInfo);
            if (registerResult.IsSuccess == true)
            {
                return RedirectToAction("Login", "Account", new { role = "团长" });
            }
            ModelState.AddModelError("", registerResult.Message);
            return View("PromoterRegister");
        }
        [HttpPost]
        public async Task<IActionResult> AdminRegister(string realname, string username, string password, string phonenumber, string password_again, string? adminKind = null)
        {
            ViewBag.Role = "管理员";
            ViewBag.AdminKinds = AdminSession.Kinds;
            if (password != password_again)
            {
                ModelState.AddModelError("", "两次输入密码不同");
                return View("AdminRegister");
            }
            var registerInfo = new GroupC_AdminRegisterInfo();
            registerInfo.RealName = realname;
            registerInfo.Phone = phonenumber;
            registerInfo.LoginPassword = password;
            registerInfo.LoginAccount = username;
            registerInfo.AdminKind = adminKind ?? AdminSession.AccountKind;
            var registerResult = new Result();
            registerResult = await _systemAdminService.RegisterAdmin(registerInfo);
            if (registerResult.IsSuccess == true)
            {
                return RedirectToAction("Login", "Account", new { role = "管理员" });
            }
            ModelState.AddModelError("", registerResult.ErrorMessage);
            return View("AdminRegister");
        }

    }



}
