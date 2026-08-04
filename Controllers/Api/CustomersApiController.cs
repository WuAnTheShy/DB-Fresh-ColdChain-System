using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

[ApiController]
[Route("api/customers")]
public sealed class CustomersApiController(
    ICustomerService customerService) : GroupBApiController
{
    [HttpGet("{customerId:int}")]
    public async Task<IActionResult> GetProfile(int customerId)
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

    [HttpPut("{customerId:int}")]
    public async Task<IActionResult> UpdateProfile(
        int customerId,
        CustomerProfileUpdateRequest request)
    {
        if (request.CustomerId != customerId)
            return ApiBadRequest("消费者标识不一致");

        await customerService.UpdateProfileAsync(request);
        return NoContent();
    }

    [HttpGet("{customerId:int}/addresses")]
    public async Task<IActionResult> GetAddresses(int customerId)
    {
        var result = await customerService.GetAddressesAsync(customerId);
        return result == null
            ? ApiNotFound("消费者不存在")
            : Ok(result);
    }

    [HttpGet("{customerId:int}/addresses/{addressId:int}")]
    public async Task<IActionResult> GetAddress(int customerId, int addressId)
    {
        var address = await customerService.GetAddressForEditAsync(
            customerId,
            addressId);
        return address == null
            ? ApiNotFound("收货地址不存在")
            : Ok(address);
    }

    [HttpPost("{customerId:int}/addresses")]
    public async Task<IActionResult> CreateAddress(
        int customerId,
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

    [HttpPut("{customerId:int}/addresses/{addressId:int}")]
    public async Task<IActionResult> UpdateAddress(
        int customerId,
        int addressId,
        AddressUpsertRequest request)
    {
        if (request.CustomerId != customerId)
            return ApiBadRequest("消费者标识不一致");
        if (request.AddressId != addressId)
            return ApiBadRequest("地址标识不一致");

        await customerService.UpdateAddressAsync(request);
        return NoContent();
    }

    [HttpDelete("{customerId:int}/addresses/{addressId:int}")]
    public async Task<IActionResult> DeleteAddress(int customerId, int addressId)
    {
        await customerService.DeleteAddressAsync(customerId, addressId);
        return NoContent();
    }

    [HttpPut("{customerId:int}/addresses/{addressId:int}/default")]
    public async Task<IActionResult> SetDefaultAddress(
        int customerId,
        int addressId)
    {
        await customerService.SetDefaultAddressAsync(customerId, addressId);
        return NoContent();
    }
}
