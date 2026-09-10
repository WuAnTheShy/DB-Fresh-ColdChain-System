using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers;

// 消费者资料与收货地址管理。
public sealed class CustomerController : Controller
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<CustomerController> _logger;

    public CustomerController(
        ICustomerService customerService,
        ILogger<CustomerController> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string id = GroupBDemoIds.Customer)
    {
        var model = await _customerService.GetProfileAsync(id);
        return model == null ? NotFound() : View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CustomerCreateRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerCreateRequest request)
    {
        if (!ModelState.IsValid)
            return View(request);

        try
        {
            var customerId = await _customerService.CreateCustomerAsync(request);
            TempData["SuccessMessage"] = "消费者创建成功";
            return RedirectToAction(nameof(Index), new { id = customerId });
        }
        catch (GroupBBusinessException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "创建消费者失败");
            ModelState.AddModelError(string.Empty, "系统暂时无法创建消费者，请稍后重试");
        }

        return View(request);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var customer = await _customerService.GetCustomerAsync(id);
        if (customer == null)
            return NotFound();

        return View(new CustomerProfileUpdateRequest
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.CustomerName,
            Phone = customer.Phone,
            Email = customer.Email
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        CustomerProfileUpdateRequest request)
    {
        if (id != request.CustomerId)
            ModelState.AddModelError(string.Empty, "消费者标识不一致，请刷新后重试");
        if (!ModelState.IsValid)
            return View(request);

        try
        {
            await _customerService.UpdateProfileAsync(request);
            TempData["SuccessMessage"] = "消费者资料已更新";
            return RedirectToAction(nameof(Index), new { id = request.CustomerId });
        }
        catch (GroupBBusinessException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "更新消费者 {CustomerId} 资料失败", id);
            ModelState.AddModelError(string.Empty, "系统暂时无法更新资料，请稍后重试");
        }

        return View(request);
    }

    public async Task<IActionResult> Addresses(string customerId)
    {
        var model = await _customerService.GetAddressesAsync(customerId);
        return model == null ? NotFound() : View(model);
    }

    [HttpGet]
    public IActionResult CreateAddress(string customerId)
    {
        if (!GroupBIds.IsValid(customerId))
            return BadRequest();

        return View(new AddressUpsertRequest { CustomerId = customerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAddress(AddressUpsertRequest request)
    {
        request.AddressId = null;
        if (!ModelState.IsValid)
            return View(request);

        try
        {
            await _customerService.CreateAddressAsync(request);
            TempData["SuccessMessage"] = "收货地址已新增";
            return RedirectToAction(nameof(Addresses), new
            {
                customerId = request.CustomerId
            });
        }
        catch (GroupBBusinessException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "消费者 {CustomerId} 新增地址失败",
                request.CustomerId);
            ModelState.AddModelError(string.Empty, "系统暂时无法新增地址，请稍后重试");
        }

        return View(request);
    }

    [HttpGet]
    public async Task<IActionResult> EditAddress(string customerId, string id)
    {
        var model = await _customerService.GetAddressForEditAsync(customerId, id);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAddress(
        string id,
        AddressUpsertRequest request)
    {
        if (id != request.AddressId)
            ModelState.AddModelError(string.Empty, "地址标识不一致，请刷新后重试");
        if (!ModelState.IsValid)
            return View(request);

        try
        {
            await _customerService.UpdateAddressAsync(request);
            TempData["SuccessMessage"] = "收货地址已更新";
            return RedirectToAction(nameof(Addresses), new
            {
                customerId = request.CustomerId
            });
        }
        catch (GroupBBusinessException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "更新地址 {AddressId} 失败", id);
            ModelState.AddModelError(string.Empty, "系统暂时无法更新地址，请稍后重试");
        }

        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(string customerId, string id)
    {
        try
        {
            await _customerService.DeleteAddressAsync(customerId, id);
            TempData["SuccessMessage"] = "收货地址已删除";
        }
        catch (GroupBBusinessException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "删除地址 {AddressId} 失败", id);
            TempData["ErrorMessage"] = "系统暂时无法删除地址，请稍后重试";
        }

        return RedirectToAction(nameof(Addresses), new { customerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultAddress(string customerId, string id)
    {
        try
        {
            await _customerService.SetDefaultAddressAsync(customerId, id);
            TempData["SuccessMessage"] = "默认收货地址已更新";
        }
        catch (GroupBBusinessException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "设置默认地址 {AddressId} 失败", id);
            TempData["ErrorMessage"] = "系统暂时无法设置默认地址，请稍后重试";
        }

        return RedirectToAction(nameof(Addresses), new { customerId });
    }
}
