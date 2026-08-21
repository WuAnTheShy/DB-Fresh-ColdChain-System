using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using Newtonsoft.Json;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace FreshColdChain.Services
{
    public class AccountService
    {
        // Repository层句柄
        // 事务核心句柄
        private readonly IUnitOfWork _uow;
        // 接口访问句柄
        private readonly ISupplierService _isupplierService;
        // Interface层句柄（修正字段名，与构造函数一致）
        private readonly ITableLogService _logManager;
        private readonly ICustomerService _customerService;
        // 构造函数
        public AccountService(
                IUnitOfWork uow,
                ISupplierService isupplierService,
                ITableLogService logManager,
                ICustomerService customerService)  
        {
            _uow = uow;
            _isupplierService = isupplierService;
            _logManager = logManager;
            _customerService = customerService;  
        }
        // 供应商登录验证
        public async Task<SupplierLoginResult> LoginSupplier(string loginAccount, string password)
        {
            if (string.IsNullOrWhiteSpace(loginAccount) || string.IsNullOrWhiteSpace(password))
                return new SupplierLoginResult { IsSuccess = false, Message = "账号或密码不能为空" };

            var response = await _isupplierService.FindSupplierAccountAsync(null, null, loginAccount);
            // 检查响应是否成功
            if (!response.IsSuccess || response.Data == null || response.Data.Count == 0)
                return new SupplierLoginResult { IsSuccess = false, Message = "账号不存在" };

            var supplier = response.Data.First();
            // 验证密码
            var verifyResponse = await _isupplierService.VerifySupplierPasswordAsync(loginAccount, password);
            if (!verifyResponse.IsSuccess || !verifyResponse.Data)
                return new SupplierLoginResult { IsSuccess = false, Message = "账号或密码错误" };
            var expiryDate = (DateTime)(supplier.ExpiryDate != null ? supplier.ExpiryDate : DateTime.MinValue);
            // 检查状态
            if (DateTime.Compare(expiryDate, DateTime.Now) < 0)
                return new SupplierLoginResult { IsSuccess = false, Message = "资质已过期" };

            return new SupplierLoginResult
            {
                IsSuccess = true,
                SuppierId = supplier.SupplierID
            };
        }

        // 消费者登录验证
        public async Task<CustomerLoginResult> LoginCustomer(string username, string password)
        {
            // 参数校验
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return new CustomerLoginResult { IsSuccess = false, Message = "账号或密码不能为空" };

            try
            {
                // 2. 构造请求对象（注意：username 对应手机号）
                var request = new GroupBCustomerLoginRequest
                {
                    Phone = username,
                    Password = password
                };

                // 3. 调用 B 组提供的消费者登录接口
                var result = await _customerService.LoginAsync(request);

                // 4. 判断返回结果是否有效
                if (result != null && !string.IsNullOrEmpty(result.CustomerId))
                {
                    return new CustomerLoginResult
                    {
                        IsSuccess = true,
                        CustomerId = result.CustomerId,
                        CustomerName = result.CustomerName,
                        Phone = result.Phone
                    };
                }
                return new CustomerLoginResult { IsSuccess = false, Message = "账号或密码错误" };
            }
            catch (Exception ex)
            {
                return new CustomerLoginResult { IsSuccess = false, Message = ex.Message };
            }
        }

    }
}
