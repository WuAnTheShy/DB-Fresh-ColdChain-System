using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;
using DBFreshColdChain.Repositories;
using DBFreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Diagnostics;
namespace DBFreshColdChain.Controllers
{
    public class AccountController : Controller
    {
        private readonly GroupC_PromoterManager _promoterManager;
        public AccountController(GroupC_PromoterManager promoterManager)
        {
            _promoterManager = promoterManager;
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
            }
            else if (role == "管理员")
            {
                ViewBag.Role = "管理员";
            }
            else 
            {
                ViewBag.Role = "缺省角色信息";
            }

            return View();
        }
        [HttpPost]
        public IActionResult Login(string username, string password,string? role = null)
        {
            ViewBag.Role = role;
            if (role == "团长")
            {
                var loginResult = _promoterManager.LoginPromoter(username, password);
                if(loginResult.IsSuccess == true)  //登录成功
                {
                    HttpContext.Session.SetString("PromoterName", username);
                    return RedirectToAction("Index", "Promoters");    
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
            return View();
            
        }
        public IActionResult PromoterRegister()
        {
            ViewBag.Role = "团长";
            return View("PromoterRegister");
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
            registerResult = await _promoterManager.RegisterPromoter(registerInfo);
            if (registerResult.IsSuccess == true)
            {
                return RedirectToAction("Login", "Account", new { role = "团长" });
            }
            ModelState.AddModelError("", registerResult.Message);
            return View("PromoterRegister");
        }
    }
    
}