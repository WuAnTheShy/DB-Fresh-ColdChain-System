using DBFreshColdChain.Interfaces;
using FreshColdChain.Interfaces;
using DBFreshColdChain.Models.CrossGroup;
using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Repositories;
using FreshColdChain.Repositories;
using DBFreshColdChain.Models;
using Newtonsoft.Json;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace DBFreshColdChain.Services
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

        // 构造函数
        public AccountService(IUnitOfWork uow, ISupplierService isupplierService, ITableLogService logManager)
        {
            _uow = uow;
            _isupplierService = isupplierService;
            _logManager = logManager;
        }

        // 供应商登录验证
        public async Task<GroupC_SupplierLoginResult> LoginSupplier(string loginAccount, string password)
        {
            if (string.IsNullOrWhiteSpace(loginAccount) || string.IsNullOrWhiteSpace(password))
                return new GroupC_SupplierLoginResult { IsSuccess = false, Message = "账号或密码不能为空" };

            var response = await _isupplierService.FindSupplierAccountAsync(null, null, loginAccount);
            // 检查响应是否成功
            if (!response.IsSuccess || response.Data == null || response.Data.Count == 0)
                return new GroupC_SupplierLoginResult { IsSuccess = false, Message = "账号不存在" };

            var supplier = response.Data.First();
            // 验证密码
            var verifyResponse = await _isupplierService.VerifySupplierPasswordAsync(loginAccount, password);
            if (!verifyResponse.IsSuccess || !verifyResponse.Data)
                return new GroupC_SupplierLoginResult { IsSuccess = false, Message = "账号或密码错误" };
            var expiryDate = (DateTime)(supplier.ExpiryDate != null ? supplier.ExpiryDate : DateTime.MinValue);
            // 检查状态
            if (DateTime.Compare(expiryDate, DateTime.Now) < 0)
                return new GroupC_SupplierLoginResult { IsSuccess = false, Message = "资质已过期" };

            return new GroupC_SupplierLoginResult
            {
                IsSuccess = true,
                SuppierId = supplier.SupplierID
            };
        }
    }
}
