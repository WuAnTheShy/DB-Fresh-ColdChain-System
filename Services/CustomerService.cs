using System.Text.RegularExpressions;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Microsoft.AspNetCore.Identity;

namespace FreshColdChain.Services;

/// <summary>
/// 消费者服务，负责资料、地址和默认地址不变量。
/// </summary>
public sealed class CustomerService : ICustomerService
{
    private static readonly Regex MainlandPhonePattern = new(
        @"^1\d{10}$",
        RegexOptions.CultureInvariant);

    private readonly ICustomerRepository _customerRepo;
    private readonly IPointRepository _pointRepo;
    private readonly IOrderTransactionManager _transactionManager;
    private readonly IPasswordHasher<CrmCustomer> _passwordHasher;

    public CustomerService(
        ICustomerRepository customerRepo,
        IPointRepository pointRepo,
        IOrderTransactionManager transactionManager,
        IPasswordHasher<CrmCustomer> passwordHasher)
    {
        _customerRepo = customerRepo;
        _pointRepo = pointRepo;
        _transactionManager = transactionManager;
        _passwordHasher = passwordHasher;
    }

    public async Task<string> CreateCustomerAsync(CustomerCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        NormalizeAndValidateCreateRequest(request);

        return await _transactionManager.ExecuteAsync(async transaction =>
        {
            if (await _customerRepo.PhoneExistsAsync(
                request.Phone,
                null,
                transaction))
                throw new GroupBBusinessException("该手机号码已注册");

            var baseLevel = await _pointRepo.GetLevelForSpentAsync(0m, transaction);
            var customer = new CrmCustomer
            {
                CustomerId = GroupBIds.NewId(),
                CustomerName = request.CustomerName,
                Phone = request.Phone,
                Email = request.Email,
                MemberLevelId = baseLevel?.MemberLevelId,
                TotalSpent = 0m,
                Points = 0
            };
            customer.PasswordHash = _passwordHasher.HashPassword(
                customer,
                request.Password);

            return await _customerRepo.CreateCustomerAsync(customer, transaction);
        });
    }

    public async Task<GroupBCustomerLoginResult> LoginAsync(
        GroupBCustomerLoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Phone = request.Phone?.Trim() ?? string.Empty;
        if (!MainlandPhonePattern.IsMatch(request.Phone) ||
            request.Password.Length is < 8 or > 100)
        {
            throw new GroupBBusinessException("手机号或密码错误");
        }

        var customer = await _customerRepo.GetByPhoneAsync(request.Phone);
        if (customer == null ||
            _passwordHasher.VerifyHashedPassword(
                customer,
                customer.PasswordHash,
                request.Password) == PasswordVerificationResult.Failed)
        {
            throw new GroupBBusinessException("手机号或密码错误");
        }

        return new GroupBCustomerLoginResult
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.CustomerName,
            Phone = customer.Phone
        };
    }

    public async Task<CrmCustomer?> GetCustomerAsync(string customerId)
    {
        return !GroupBIds.IsValid(customerId)
            ? null
            : await _customerRepo.GetByIdAsync(customerId);
    }

    public async Task<CustomerProfileViewModel?> GetProfileAsync(string customerId)
    {
        if (!GroupBIds.IsValid(customerId))
            return null;

        var customer = await _customerRepo.GetByIdAsync(customerId);
        if (customer == null)
            return null;

        var addressesTask = _customerRepo.GetAddressesAsync(customerId);
        var levelTask = customer.MemberLevelId != null
            ? _pointRepo.GetLevelByIdAsync(customer.MemberLevelId)
            : _pointRepo.GetLevelForSpentAsync(customer.TotalSpent);

        await Task.WhenAll(addressesTask, levelTask);
        return new CustomerProfileViewModel
        {
            Customer = customer,
            MemberLevel = await levelTask,
            Addresses = await addressesTask
        };
    }

    public async Task UpdateProfileAsync(CustomerProfileUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        NormalizeAndValidateProfile(request);

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            _ = await _customerRepo.GetByIdForUpdateAsync(
                    request.CustomerId,
                    transaction)
                ?? throw new GroupBBusinessException("消费者不存在");
            if (await _customerRepo.PhoneExistsAsync(
                request.Phone,
                request.CustomerId,
                transaction))
            {
                throw new GroupBBusinessException("该手机号码已被其他消费者使用");
            }

            if (!await _customerRepo.UpdateProfileAsync(request, transaction))
                throw new GroupBBusinessException("消费者资料更新失败，请刷新后重试");
        });
    }

    public async Task<AddressListViewModel?> GetAddressesAsync(string customerId)
    {
        if (!GroupBIds.IsValid(customerId) ||
            await _customerRepo.GetByIdAsync(customerId) == null)
            return null;

        return new AddressListViewModel
        {
            CustomerId = customerId,
            Addresses = await _customerRepo.GetAddressesAsync(customerId)
        };
    }

    public async Task<AddressUpsertRequest?> GetAddressForEditAsync(
        string customerId,
        string addressId)
    {
        if (!GroupBIds.IsValid(customerId) || !GroupBIds.IsValid(addressId))
            return null;

        var address = await _customerRepo.GetAddressAsync(customerId, addressId);
        return address == null ? null : MapAddress(address);
    }

    public async Task<string> CreateAddressAsync(AddressUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        NormalizeAndValidateAddress(request);

        return await _transactionManager.ExecuteAsync(async transaction =>
        {
            _ = await _customerRepo.GetByIdForUpdateAsync(
                    request.CustomerId,
                    transaction)
                ?? throw new GroupBBusinessException("消费者不存在");

            var existingAddresses = await _customerRepo.GetAddressesAsync(
                request.CustomerId,
                transaction);
            var shouldBeDefault = request.IsDefault || existingAddresses.Count == 0;
            if (shouldBeDefault)
            {
                await _customerRepo.ClearDefaultAddressesAsync(
                    request.CustomerId,
                    transaction);
            }

            return await _customerRepo.CreateAddressAsync(
                CreateAddressEntity(request, shouldBeDefault),
                transaction);
        });
    }

    public async Task UpdateAddressAsync(AddressUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        NormalizeAndValidateAddress(request);
        if (!GroupBIds.IsValid(request.AddressId))
            throw new GroupBBusinessException("地址ID格式不正确");

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            _ = await _customerRepo.GetByIdForUpdateAsync(
                    request.CustomerId,
                    transaction)
                ?? throw new GroupBBusinessException("消费者不存在");

            var address = await _customerRepo.GetAddressAsync(
                    request.CustomerId,
                    request.AddressId!,
                    transaction)
                ?? throw new GroupBBusinessException("收货地址不存在或不属于当前消费者");
            var addresses = await _customerRepo.GetAddressesAsync(
                request.CustomerId,
                transaction);

            var shouldBeDefault = request.IsDefault;
            CrmUserAddress? promotedAddress = null;
            if (request.IsDefault)
            {
                await _customerRepo.ClearDefaultAddressesAsync(
                    request.CustomerId,
                    transaction);
            }
            else if (address.IsDefault == 1)
            {
                promotedAddress = addresses.FirstOrDefault(
                    item => item.AddressId != address.AddressId);
                shouldBeDefault = promotedAddress == null;
            }

            var updatedAddress = CreateAddressEntity(request, shouldBeDefault);
            updatedAddress.AddressId = address.AddressId;
            if (!await _customerRepo.UpdateAddressAsync(updatedAddress, transaction))
                throw new GroupBBusinessException("收货地址更新失败，请刷新后重试");

            if (promotedAddress != null &&
                !await _customerRepo.SetDefaultAddressAsync(
                    request.CustomerId,
                    promotedAddress.AddressId,
                    transaction))
            {
                throw new GroupBBusinessException("默认地址顺延失败，请刷新后重试");
            }
        });
    }

    public async Task DeleteAddressAsync(string customerId, string addressId)
    {
        EnsureValidIds(customerId, addressId);

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            _ = await _customerRepo.GetByIdForUpdateAsync(customerId, transaction)
                ?? throw new GroupBBusinessException("消费者不存在");
            var address = await _customerRepo.GetAddressAsync(
                    customerId,
                    addressId,
                    transaction)
                ?? throw new GroupBBusinessException("收货地址不存在或不属于当前消费者");

            var fallback = address.IsDefault == 1
                ? (await _customerRepo.GetAddressesAsync(customerId, transaction))
                    .FirstOrDefault(item => item.AddressId != addressId)
                : null;

            if (!await _customerRepo.DeleteAddressAsync(
                customerId,
                addressId,
                transaction))
            {
                throw new GroupBBusinessException("收货地址删除失败，请刷新后重试");
            }

            if (fallback != null &&
                !await _customerRepo.SetDefaultAddressAsync(
                    customerId,
                    fallback.AddressId,
                    transaction))
            {
                throw new GroupBBusinessException("默认地址顺延失败，请刷新后重试");
            }
        });
    }

    public async Task SetDefaultAddressAsync(string customerId, string addressId)
    {
        EnsureValidIds(customerId, addressId);

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            _ = await _customerRepo.GetByIdForUpdateAsync(customerId, transaction)
                ?? throw new GroupBBusinessException("消费者不存在");
            _ = await _customerRepo.GetAddressAsync(customerId, addressId, transaction)
                ?? throw new GroupBBusinessException("收货地址不存在或不属于当前消费者");

            await _customerRepo.ClearDefaultAddressesAsync(customerId, transaction);
            if (!await _customerRepo.SetDefaultAddressAsync(
                customerId,
                addressId,
                transaction))
            {
                throw new GroupBBusinessException("默认地址设置失败，请刷新后重试");
            }
        });
    }

    private static CrmUserAddress CreateAddressEntity(
        AddressUpsertRequest request,
        bool isDefault)
    {
        return new CrmUserAddress
        {
            AddressId = GroupBIds.NewId(),
            CustomerId = request.CustomerId,
            ReceiverName = request.ReceiverName,
            Phone = request.Phone,
            Province = request.Province,
            City = request.City,
            District = request.District,
            DetailAddress = request.DetailAddress,
            IsDefault = isDefault ? 1 : 0
        };
    }

    private static AddressUpsertRequest MapAddress(CrmUserAddress address)
    {
        return new AddressUpsertRequest
        {
            AddressId = address.AddressId,
            CustomerId = address.CustomerId,
            ReceiverName = address.ReceiverName,
            Phone = address.Phone,
            Province = address.Province,
            City = address.City,
            District = address.District,
            DetailAddress = address.DetailAddress,
            IsDefault = address.IsDefault == 1
        };
    }

    private static void NormalizeAndValidateProfile(CustomerProfileUpdateRequest request)
    {
        if (!GroupBIds.IsValid(request.CustomerId))
            throw new GroupBBusinessException("消费者ID格式不正确");

        request.CustomerName = RequiredTrimmed(request.CustomerName, "消费者姓名", 100);
        request.Phone = RequiredTrimmed(request.Phone, "手机号码", 20);
        if (!MainlandPhonePattern.IsMatch(request.Phone))
            throw new GroupBBusinessException("请输入11位中国大陆手机号码");

        request.Email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();
        if (request.Email?.Length > 100)
            throw new GroupBBusinessException("邮箱不能超过100个字符");
        if (request.Email != null &&
            !System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
        {
            throw new GroupBBusinessException("请输入有效的邮箱地址");
        }
    }

    private static void NormalizeAndValidateCreateRequest(CustomerCreateRequest request)
    {
        request.CustomerName = RequiredTrimmed(request.CustomerName, "消费者姓名", 100);
        request.Phone = RequiredTrimmed(request.Phone, "手机号码", 20);
        if (!MainlandPhonePattern.IsMatch(request.Phone))
            throw new GroupBBusinessException("请输入11位中国大陆手机号码");

        request.Email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();
        if (request.Email?.Length > 100)
            throw new GroupBBusinessException("邮箱不能超过100个字符");
        if (request.Email != null &&
            !System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
        {
            throw new GroupBBusinessException("请输入有效的邮箱地址");
        }
        if (request.Password.Length is < 8 or > 100)
            throw new GroupBBusinessException("密码长度必须为8到100个字符");
        if (!string.Equals(
            request.Password,
            request.ConfirmPassword,
            StringComparison.Ordinal))
        {
            throw new GroupBBusinessException("两次输入的密码不一致");
        }
    }

    private static void NormalizeAndValidateAddress(AddressUpsertRequest request)
    {
        if (!GroupBIds.IsValid(request.CustomerId))
            throw new GroupBBusinessException("消费者ID格式不正确");

        request.ReceiverName = RequiredTrimmed(request.ReceiverName, "收件人", 50);
        request.Phone = RequiredTrimmed(request.Phone, "联系电话", 20);
        if (!MainlandPhonePattern.IsMatch(request.Phone))
            throw new GroupBBusinessException("请输入11位中国大陆手机号码");
        request.Province = RequiredTrimmed(request.Province, "省份", 50);
        request.City = RequiredTrimmed(request.City, "城市", 50);
        request.District = RequiredTrimmed(request.District, "区县", 50);
        request.DetailAddress = RequiredTrimmed(request.DetailAddress, "详细地址", 200);
    }

    private static string RequiredTrimmed(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new GroupBBusinessException($"请输入{fieldName}");
        if (normalized.Length > maxLength)
            throw new GroupBBusinessException($"{fieldName}不能超过{maxLength}个字符");
        return normalized;
    }

    private static void EnsureValidIds(string customerId, string addressId)
    {
        if (!GroupBIds.IsValid(customerId))
            throw new GroupBBusinessException("消费者ID格式不正确");
        if (!GroupBIds.IsValid(addressId))
            throw new GroupBBusinessException("地址ID格式不正确");
    }
}
