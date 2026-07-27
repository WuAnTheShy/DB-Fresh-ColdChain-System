using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 消费者服务
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly CustomerRepository _customerRepo;
    private readonly PointRepository _pointRepo;

    public CustomerService(CustomerRepository customerRepo, PointRepository pointRepo)
    {
        _customerRepo = customerRepo;
        _pointRepo = pointRepo;
    }

    public async Task<CrmCustomer?> GetCustomerAsync(int customerId)
    {
        return await _customerRepo.GetByIdAsync(customerId);
    }
}
