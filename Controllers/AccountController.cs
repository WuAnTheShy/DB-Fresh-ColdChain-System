using FreshColdChain.Models.DTOs;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Interfaces;
using FreshColdChain.Services;
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
        public async Task<IActionResult> Login(string username, string password,string? role = null)
        {
            ViewBag.Role = role;
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
                var loginResult = _systemAdminService.LoginAdmin(username, password);
                if (loginResult.IsSuccess == true)  //登录成功
                {
                    HttpContext.Session.SetString("AdminName", username);
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
                    return RedirectToAction("Index", "SuppliersHome");
                }
                ModelState.AddModelError("", loginResult.Message);
                return View();
            }
            return View();
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
            return View("AdminRegister");
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
        public async Task<IActionResult> AdminRegister(string realname, string username, string password, string phonenumber, string password_again)
        {
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