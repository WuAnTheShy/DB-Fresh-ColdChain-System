using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

[ApiController]
[Route("api/customers")]
public sealed class CustomersApiController(
    ICustomerService customerService) : GroupBApiController
{
    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetProfile(string customerId)
    {
        var profile = await customerService.GetProfileAsync(customerId);
        if (profile == null)
            return ApiNotFound("消费者不存在");

        var customer = profile.Customer;
        return Ok(new
        {
            customer = new
            {
                customer.CustomerId,
                customer.CustomerName,
                customer.Phone,
                customer.Email,
                customer.PromoterId,
                customer.MemberLevelId,
                customer.TotalSpent,
                customer.Points,
                customer.GrowthValue,
                customer.CreatedAt,
                customer.UpdatedAt
            },
            profile.MemberLevel,
            profile.Addresses
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CustomerCreateRequest request)
    {
        var customerId = await customerService.CreateCustomerAsync(request);
        return CreatedAtAction(
            nameof(GetProfile),
            new { customerId },
            new { customerId });
    }

    [HttpPut("{customerId}")]
    public async Task<IActionResult> UpdateProfile(
        string customerId,
        CustomerProfileUpdateRequest request)
    {
        if (request.CustomerId != customerId)
            return ApiBadRequest("消费者标识不一致");

        await customerService.UpdateProfileAsync(request);
        return NoContent();
    }

    [HttpGet("{customerId}/addresses")]
    public async Task<IActionResult> GetAddresses(string customerId)
    {
        var result = await customerService.GetAddressesAsync(customerId);
        return result == null
            ? ApiNotFound("消费者不存在")
            : Ok(result);
    }

    [HttpGet("{customerId}/addresses/{addressId}")]
    public async Task<IActionResult> GetAddress(string customerId, string addressId)
    {
        var address = await customerService.GetAddressForEditAsync(
            customerId,
            addressId);
        return address == null
            ? ApiNotFound("收货地址不存在")
            : Ok(address);
    }

    [HttpPost("{customerId}/addresses")]
    public async Task<IActionResult> CreateAddress(
        string customerId,
        AddressUpsertRequest request)
    {
        if (request.CustomerId != customerId)
            return ApiBadRequest("消费者标识不一致");

        request.AddressId = null;
        var addressId = await customerService.CreateAddressAsync(request);
        return CreatedAtAction(
            nameof(GetAddress),
            new { customerId, addressId },
            new { addressId });
    }

    [HttpPut("{customerId}/addresses/{addressId}")]
    public async Task<IActionResult> UpdateAddress(
        string customerId,
        string addressId,
        AddressUpsertRequest request)
    {
        if (request.CustomerId != customerId)
            return ApiBadRequest("消费者标识不一致");
        if (request.AddressId != addressId)
            return ApiBadRequest("地址标识不一致");

        await customerService.UpdateAddressAsync(request);
        return NoContent();
    }

    [HttpDelete("{customerId}/addresses/{addressId}")]
    public async Task<IActionResult> DeleteAddress(string customerId, string addressId)
    {
        await customerService.DeleteAddressAsync(customerId, addressId);
        return NoContent();
    }

    [HttpPut("{customerId}/addresses/{addressId}/default")]
    public async Task<IActionResult> SetDefaultAddress(
        string customerId,
        string addressId)
    {
        await customerService.SetDefaultAddressAsync(customerId, addressId);
        return NoContent();
    }
}
