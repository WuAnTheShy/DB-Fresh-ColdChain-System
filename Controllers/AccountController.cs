using DBFreshColdChain.Models;
using DBFreshColdChain.Repositories;
using DBFreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
namespace DBFreshColdChainSystem.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult RoleSelect()
        {
            return View();
        }
        public IActionResult Login(string? role = null)
        {
            if (role == "Promoter")
            {
                ViewBag.Role = "团长";
            }
            else if (role == "Consumer")
            {
                ViewBag.Role = "消费者";
            }
            else if (role == "Admin")
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
               
            if (username == "admin" && password == "123456")
            {
                return RedirectToAction("Index", "Home");
            }
            ModelState.AddModelError("", "用户名或密码错误");
            return View();
        }

    }
    
}