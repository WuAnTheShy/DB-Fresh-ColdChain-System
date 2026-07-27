using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 消费者服务
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepo;

    public CustomerService(ICustomerRepository customerRepo)
    {
        _customerRepo = customerRepo;
    }

    public async Task<CrmCustomer?> GetCustomerAsync(int customerId)
    {
        return await _customerRepo.GetByIdAsync(customerId);
    }
}
