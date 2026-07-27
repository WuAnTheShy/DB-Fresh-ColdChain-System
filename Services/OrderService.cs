using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Oracle.ManagedDataAccess.Client;

namespace FreshColdChain.Services;

/// <summary>
/// 订单服务 - B 组的核心！包含订单事务、积分计算、优惠券核销
/// 
/// 【关键概念】
/// IDbTransaction 就是你学过的 BEGIN/COMMIT/ROLLBACK
/// 这里用 C# 代码控制，而不是在 SQL Plus 里手动敲
/// </summary>
public class OrderService : IOrderService
{
    private readonly OrderRepository _orderRepo;
    private readonly CustomerRepository _customerRepo;
    private readonly CouponRepository _couponRepo;
    private readonly PointRepository _pointRepo;
    private readonly string _connectionString;

    public OrderService(IConfiguration configuration,
        OrderRepository orderRepo, CustomerRepository customerRepo,
        CouponRepository couponRepo, PointRepository pointRepo)
    {
        _orderRepo = orderRepo;
        _customerRepo = customerRepo;
        _couponRepo = couponRepo;
        _pointRepo = pointRepo;
        _connectionString = configuration.GetConnectionString("OracleConnection")!;
    }

    /// <summary>
    /// 核心下单流程（事务演示）
    /// 对应你学过的:
    ///   BEGIN
    ///     INSERT INTO Biz_Orders ...
    ///     INSERT INTO Biz_OrderDetails ...
    ///     UPDATE Crm_Customers SET Points = ...
    ///     INSERT INTO Crm_PointLogs ...
    ///     UPDATE Mkt_CouponRecords SET Status = 1 ...
    ///   COMMIT;  -- 全部成功
    ///   -- 任何一步失败 → ROLLBACK
    /// </summary>
    public async Task<int> CreateOrderAsync(BizOrder order, List<BizOrderDetail> details)
    {
        // 第1步：建立数据库连接
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        // 第2步：开始事务（相当于 SQL 中的 BEGIN）
        using var transaction = conn.BeginTransaction();

        try
        {
            // 第3步：插入订单主表
            var orderId = await _orderRepo.CreateOrderAsync(order, transaction);
            order.OrderId = orderId;

            // 第4步：关联订单ID到明细并批量插入
            foreach (var detail in details)
                detail.OrderId = orderId;
            await _orderRepo.InsertDetailsAsync(details, transaction);

            // 第5步：计算并发放积分（如 消费100元=10积分）
            var pointsEarned = (int)(order.FinalAmount / 10);
            var customer = await _customerRepo.GetByIdAsync(order.CustomerId, transaction)
                ?? throw new InvalidOperationException("消费者不存在，无法创建订单");

            var newPoints = customer.Points + pointsEarned;
            await _customerRepo.UpdatePointsAsync(customer.CustomerId, newPoints, transaction);

            // 积分流水记录（每笔必记，防篡改）
            await _pointRepo.InsertLogAsync(new CrmPointLog
            {
                CustomerId = customer.CustomerId,
                ChangeAmount = pointsEarned,
                BalanceAfter = newPoints,
                ChangeType = "ORDER_EARN",
                OrderId = orderId
            }, transaction);

            // 累计消费更新（用于会员等级判定）
            await _customerRepo.UpdateTotalSpentAsync(
                customer.CustomerId,
                order.FinalAmount,
                transaction);

            // 第6步：核销优惠券（如果有）
            // TODO: 从请求中获取使用的优惠券记录ID

            // 第7步：所有操作成功 → 提交事务（COMMIT）
            transaction.Commit();

            return orderId;
        }
        catch
        {
            // 任何一步失败 → 回滚所有操作（ROLLBACK）
            transaction.Rollback();
            throw; // 把异常向上抛出，Controller 层处理
        }
    }

    /// <summary>
    /// 退款时扣回积分 - 供 C 组调用
    /// </summary>
    public async Task DeductPointsForRefundAsync(int customerId, int orderId, int pointsToDeduct)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            var customer = await _customerRepo.GetByIdAsync(customerId, transaction);
            if (customer == null) throw new Exception("用户不存在");

            var newPoints = customer.Points - pointsToDeduct;
            if (newPoints < 0) newPoints = 0;

            await _customerRepo.UpdatePointsAsync(customerId, newPoints, transaction);
            await _pointRepo.InsertLogAsync(new CrmPointLog
            {
                CustomerId = customerId,
                ChangeAmount = -pointsToDeduct,
                BalanceAfter = newPoints,
                ChangeType = "REFUND_DEDUCT",
                OrderId = orderId
            }, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 动态会员等级判定 - 状态机
    /// 根据累计消费金额自动匹配等级
    /// </summary>
    public async Task<CrmMemberLevel?> GetCustomerLevelAsync(int customerId)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId);
        if (customer == null) return null;

        var allLevels = await _pointRepo.GetAllLevelsAsync();

        // 从高到低匹配: 累计消费 >= 等级门槛 → 取最高满足的等级
        return allLevels
            .Where(l => customer.TotalSpent >= l.MinSpent)
            .OrderByDescending(l => l.MinSpent)
            .FirstOrDefault();
    }
}
