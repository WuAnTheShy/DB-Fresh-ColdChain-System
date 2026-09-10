using System.Data;
using System.Reflection;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using FreshColdChain.Services;

namespace FreshColdChain.Tests;

internal static class FinancialTransactionScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("真实支付服务的流水和审计共用外部事务", PaymentExternalTransactionAsync),
            ("支付自有事务在审计失败时整体回滚", PaymentOwnedTransactionAsync),
            ("外部支付审计失败不接管调用方事务", PaymentExternalFailureAsync),
            ("C组审核末步失败时B组积分订单同时回滚", RefundAuditTransactionAsync),
            ("C组直接退款与审计共同提交回滚", DirectRefundTransactionAsync),
            ("佣金记录编号符合数据库36位长度", CommissionRecordIdFitsDatabaseColumnAsync),
            ("退款佣金撤销失败不能继续批准退款", RefundCommissionFailureAsync),
            ("佣金仓储返回更新失败时拒绝批准退款", RefundCommissionRecordFailureAsync),
            ("退款申请进入审核中且驳回回退、通过后转已退款", RefundReviewStatusAsync)
        };
        var failures = 0;
        foreach (var (name, run) in scenarios)
        {
            try { await run(); Console.WriteLine($"PASS {name}"); }
            catch (Exception exception) { failures++; Console.WriteLine($"FAIL {name}\n{exception}"); }
        }
        Console.WriteLine($"跨组财务事务场景总数: {scenarios.Length}, 通过: {scenarios.Length - failures}, 失败: {failures}");
        return failures == 0 ? 0 : 1;
    }

    private static PaymentRequest Payment() => new()
    {
        orderID = TestIds.Order, payMethod = "Mock", status = "Success", payAmount = 100m
    };

    private static async Task PaymentExternalTransactionAsync()
    {
        foreach (var commit in new[] { false, true })
        {
            var uow = new FinancialUnitOfWork();
            var payments = new FakePaymentRepository();
            var logs = new FinancialLogRepository();
            var service = new PaymentService(uow, payments, new TableLogService(logs));
            using var transaction = new FakeOrderTransaction();
            var result = await service.CreatePaymentRecord(Payment(), transaction);
            AssertEx.True(result.IsSuccess);
            AssertEx.True(ReferenceEquals(transaction, logs.LastTransaction));
            AssertEx.True(uow.LastTransaction == null);
            AssertEx.True(!transaction.Committed && !transaction.RolledBack);
            AssertEx.Equal(0, payments.Records.Count);
            AssertEx.Equal(0, logs.Records.Count);
            if (commit) transaction.Commit(); else transaction.Rollback();
            AssertEx.Equal(commit ? 1 : 0, payments.Records.Count);
            AssertEx.Equal(commit ? 1 : 0, logs.Records.Count);
        }
    }

    private static async Task PaymentOwnedTransactionAsync()
    {
        foreach (var fail in new[] { false, true })
        {
            var uow = new FinancialUnitOfWork();
            var payments = new FakePaymentRepository();
            var logs = new FinancialLogRepository { ThrowOnInsert = fail };
            var service = new PaymentService(uow, payments, new TableLogService(logs));
            var result = await service.CreatePaymentRecord(Payment());
            AssertEx.Equal(!fail, result.IsSuccess);
            AssertEx.Equal(!fail, uow.LastTransaction!.Committed);
            AssertEx.Equal(fail, uow.LastTransaction.RolledBack);
            AssertEx.Equal(fail ? 0 : 1, payments.Records.Count);
            AssertEx.Equal(fail ? 0 : 1, logs.Records.Count);
            AssertEx.True(ReferenceEquals(uow.LastTransaction, logs.LastTransaction));
        }
    }

    private static async Task PaymentExternalFailureAsync()
    {
        var uow = new FinancialUnitOfWork();
        var payments = new FakePaymentRepository();
        var logs = new FinancialLogRepository { ThrowOnInsert = true };
        var service = new PaymentService(uow, payments, new TableLogService(logs));
        using var transaction = new FakeOrderTransaction();
        var result = await service.CreatePaymentRecord(Payment(), transaction);
        AssertEx.True(!result.IsSuccess);
        AssertEx.True(!transaction.Committed && !transaction.RolledBack);
        AssertEx.True(uow.LastTransaction == null);
        transaction.Rollback();
        AssertEx.Equal(0, payments.Records.Count);
        AssertEx.Equal(0, logs.Records.Count);
    }

    private static async Task RefundAuditTransactionAsync()
    {
        foreach (var partial in new[] { false, true })
        foreach (var failFinalUpdate in new[] { false, true })
        {
            var context = SeedOrder();
            var uow = new FinancialUnitOfWork();
            var application = new FinRefund
            {
                RefundId = "REF-TX", OrderId = TestIds.Order, Status = "Pending",
                DetailId = partial ? "DETAIL-TX" : null, SupplierId = partial ? "SUP1" : null,
                RefundQty = partial ? 1 : 0, RefundAmount = partial ? 50m : 100m
            };
            var refunds = FinancialProxy.Create<IRefundRepository>((method, args) => method.Name switch
            {
                nameof(IRefundRepository.GetByIdAsync) => Task.FromResult<FinRefund?>(application),
                nameof(IRefundRepository.TryUpdateStatusAsync) => FinalizeRefund(args!),
                _ => throw new NotSupportedException(method.Name)
            });
            Task<bool> FinalizeRefund(object?[] args)
            {
                AssertEx.True(ReferenceEquals(uow.Transaction, args[5]));
                if (failFinalUpdate) return Task.FromResult(false);
                ((FakeOrderTransaction)args[5]!).Stage(() => application.Status = (string)args[2]!);
                return Task.FromResult(true);
            }
            var service = RefundService(context, uow, refunds, new FinancialLogRepository());
            var result = await service.AuditRefund("REF-TX", true, "ADMIN");
            AssertEx.Equal(!failFinalUpdate, result.IsSuccess);
            AssertEx.True(context.TransactionManager.LastTransaction == null);
            AssertEx.Equal(!failFinalUpdate, uow.LastTransaction!.Committed);
            AssertEx.Equal(failFinalUpdate, uow.LastTransaction.RolledBack);
            AssertEx.Equal(failFinalUpdate ? "Pending" : "Approved", application.Status);
            AssertEx.Equal(failFinalUpdate ? 100 : partial ? 90 : 80, context.CustomerRepository.Customer.Points);
            AssertEx.Equal(failFinalUpdate ? OrderStatusCodes.Paid : partial ? OrderStatusCodes.Refunding : OrderStatusCodes.Refunded,
                context.OrderRepository.Orders.Single().OrderStatus);
            AssertEx.Equal(failFinalUpdate ? 0 : 1, context.PointRepository.Logs.Count);
        }
    }

    // 退款「审核中」状态闭环：申请即进入退款审核中并记录申请前状态，
    // 驳回/取消且无其他待审核申请时回退，审核通过则转“已退款/退款中”。
    private static async Task RefundReviewStatusAsync()
    {
        // 1) 提交退款申请：订单进入“退款审核中”，申请前状态被记录用于回退
        foreach (var previousStatus in new[] { OrderStatusCodes.Paid, OrderStatusCodes.Completed })
        {
            var context = SeedOrder();
            var order = context.OrderRepository.Orders.Single();
            order.OrderStatus = previousStatus;
            var uow = new FinancialUnitOfWork();
            var applications = new List<FinRefund>();
            var refunds = FinancialProxy.Create<IRefundRepository>((method, args) =>
            {
                if (method.Name == nameof(IRefundRepository.GetByOrderIdAsync))
                    return Task.FromResult(applications.ToList());
                if (method.Name != nameof(IRefundRepository.InsertRefundAsync))
                    throw new NotSupportedException(method.Name);
                ((FakeOrderTransaction)args![1]!).Stage(() => applications.Add((FinRefund)args[0]!));
                return Task.CompletedTask;
            });
            var service = RefundService(context, uow, refunds, new FinancialLogRepository());

            var applied = await service.ApplyRefund(new GroupC_RefundRequest
            {
                OrderId = TestIds.Order, LiabilityType = "Customer", Remark = "整单退款"
            });

            AssertEx.True(applied.IsSuccess);
            AssertEx.Equal(1, applications.Count);
            AssertEx.Equal("Pending", applications[0].Status);
            AssertEx.Equal(OrderStatusCodes.RefundReviewing, order.OrderStatus);
            AssertEx.Equal(previousStatus, order.StatusBeforeRefund);
        }

        // 2) 驳回：无其他待审核申请时回退到申请前状态；仍有待审核申请时保持审核中
        foreach (var previousStatus in new[] { OrderStatusCodes.Paid, OrderStatusCodes.Completed })
        foreach (var hasOtherPending in new[] { false, true })
        {
            var context = SeedOrder();
            var order = context.OrderRepository.Orders.Single();
            order.OrderStatus = OrderStatusCodes.RefundReviewing;
            order.StatusBeforeRefund = previousStatus;
            var uow = new FinancialUnitOfWork();
            var service = RefundService(context, uow,
                RejectedApplicationRefunds(uow, hasOtherPending), new FinancialLogRepository());

            var result = await service.AuditRefund("REF-TX", false, "ADMIN");

            AssertEx.True(result.IsSuccess);
            AssertEx.Equal(
                hasOtherPending ? OrderStatusCodes.RefundReviewing : previousStatus,
                order.OrderStatus);
            AssertEx.Equal(hasOtherPending, order.StatusBeforeRefund != null);
        }

        // 3) 通过：整单退款转“已退款”，部分退款转“退款中”，均不再保留回退状态
        foreach (var partial in new[] { false, true })
        foreach (var previousStatus in new[] { OrderStatusCodes.Paid, OrderStatusCodes.Completed })
        {
            var context = SeedOrder();
            var order = context.OrderRepository.Orders.Single();
            order.OrderStatus = OrderStatusCodes.RefundReviewing;
            order.StatusBeforeRefund = previousStatus;
            var uow = new FinancialUnitOfWork();
            var service = RefundService(context, uow,
                PendingApplicationRefunds(uow, partial), new FinancialLogRepository());

            var result = await service.AuditRefund("REF-TX", true, "ADMIN");

            AssertEx.True(result.IsSuccess);
            AssertEx.Equal(partial ? OrderStatusCodes.Refunding : OrderStatusCodes.Refunded, order.OrderStatus);
            AssertEx.True(order.StatusBeforeRefund == null);
        }
    }

    // 驳回场景的退款申请仓储：GetByIdAsync 返回原始申请单；GetByOrderIdAsync 返回更新后的申请单列表，
    // 与真实事务中“同连接可见已更新数据”的语义一致（可模拟该订单是否还有其他待审核申请）
    private static IRefundRepository RejectedApplicationRefunds(FinancialUnitOfWork uow, bool hasOtherPending)
    {
        var application = RefundApplication(partial: false);
        return FinancialProxy.Create<IRefundRepository>((method, args) => method.Name switch
        {
            nameof(IRefundRepository.GetByIdAsync) => Task.FromResult<FinRefund?>(application),
            nameof(IRefundRepository.TryUpdateStatusAsync) => Reject(args!),
            nameof(IRefundRepository.GetByOrderIdAsync) => QueryApplications(args!),
            _ => throw new NotSupportedException(method.Name)
        });

        Task<bool> Reject(object?[] args)
        {
            AssertEx.Equal("Pending", (string)args[1]!);
            AssertEx.Equal("Rejected", (string)args[2]!);
            AssertEx.True(ReferenceEquals(uow.Transaction, args[5]));
            return Task.FromResult(true);
        }

        Task<List<FinRefund>> QueryApplications(object?[] args)
        {
            AssertEx.True(ReferenceEquals(uow.Transaction, args[1]));
            var updated = RefundApplication(partial: false);
            updated.Status = "Rejected";
            var applications = new List<FinRefund> { updated };
            if (hasOtherPending)
            {
                applications.Add(new FinRefund
                {
                    RefundId = "REF-OTHER", OrderId = TestIds.Order, Status = "Pending",
                    DetailId = "DETAIL-TX", SupplierId = "SUP1", RefundQty = 1, RefundAmount = 50m
                });
            }
            return Task.FromResult(applications);
        }
    }

    // 通过场景的退款申请仓储：GetByIdAsync 返回待审核申请单（partial 决定部分退款 / 整单退款）
    private static IRefundRepository PendingApplicationRefunds(FinancialUnitOfWork uow, bool partial)
    {
        var application = RefundApplication(partial);
        return FinancialProxy.Create<IRefundRepository>((method, args) => method.Name switch
        {
            nameof(IRefundRepository.GetByIdAsync) => Task.FromResult<FinRefund?>(application),
            nameof(IRefundRepository.TryUpdateStatusAsync) => Approve(args!),
            _ => throw new NotSupportedException(method.Name)
        });

        Task<bool> Approve(object?[] args)
        {
            AssertEx.Equal("Pending", (string)args[1]!);
            AssertEx.Equal("Approved", (string)args[2]!);
            AssertEx.True(ReferenceEquals(uow.Transaction, args[5]));
            return Task.FromResult(true);
        }
    }

    private static FinRefund RefundApplication(bool partial) => new()
    {
        RefundId = "REF-TX", OrderId = TestIds.Order, Status = "Pending",
        DetailId = partial ? "DETAIL-TX" : null, SupplierId = partial ? "SUP1" : null,
        RefundQty = partial ? 1 : 0, RefundAmount = partial ? 50m : 100m
    };

    private static async Task DirectRefundTransactionAsync()
    {
        foreach (var failAudit in new[] { false, true })
        {
            var context = SeedOrder();
            var uow = new FinancialUnitOfWork();
            var logs = new FinancialLogRepository { ThrowOnInsert = failAudit };
            var records = new List<FinRefund>();
            var refunds = FinancialProxy.Create<IRefundRepository>((method, args) =>
            {
                if (method.Name != nameof(IRefundRepository.InsertRefundAsync)) throw new NotSupportedException(method.Name);
                AssertEx.True(ReferenceEquals(uow.Transaction, args![1]));
                ((FakeOrderTransaction)args[1]!).Stage(() => records.Add((FinRefund)args[0]!));
                return Task.CompletedTask;
            });
            var service = RefundService(context, uow, refunds, logs);
            var result = await service.Refund(new GroupC_RefundRequest { OrderId = TestIds.Order, LiabilityType = "Customer" });
            AssertEx.Equal(!failAudit, result.IsSuccess);
            AssertEx.True(context.TransactionManager.LastTransaction == null);
            AssertEx.True(ReferenceEquals(uow.LastTransaction, logs.LastTransaction));
            AssertEx.Equal(failAudit ? 0 : 1, records.Count);
            AssertEx.Equal(failAudit ? 0 : 1, logs.Records.Count);
            AssertEx.Equal(failAudit ? 100 : 80, context.CustomerRepository.Customer.Points);
            AssertEx.Equal(failAudit ? OrderStatusCodes.Paid : OrderStatusCodes.Refunded, context.OrderRepository.Orders.Single().OrderStatus);
        }
    }

    private static async Task CommissionRecordIdFitsDatabaseColumnAsync()
    {
        var uow = new FinancialUnitOfWork();
        var promoter = new GroupC_CrmPromoter
        {
            PromoterId = "PROM-TX",
            BaseCommissionRate = GroupC_LevelCommissionPolicy.BronzeRate,
            TotalSales = 0m,
            PendingBalance = 0m
        };
        var promoters = FinancialProxy.Create<IPromoterRepository>((method, _) => method.Name switch
        {
            nameof(IPromoterRepository.GroupC_FindPromoterRecordAsync) =>
                Task.FromResult<GroupC_CrmPromoter?>(promoter),
            nameof(IPromoterRepository.GroupC_UpdatePromoterTotalSalesAsync) => Task.CompletedTask,
            nameof(IPromoterRepository.GroupC_FindPromoterPendingBalanceAsync) =>
                Task.FromResult<decimal?>(promoter.PendingBalance),
            nameof(IPromoterRepository.GroupC_UpdatePromoterPendingBalanceAsync) => Task.CompletedTask,
            _ => throw new NotSupportedException(method.Name)
        });
        CommissionRecord? insertedRecord = null;
        var commissions = FinancialProxy.Create<ICommissionRepository>((method, args) => method.Name switch
        {
            nameof(ICommissionRepository.InsertAsync) => CaptureRecord(args!),
            _ => throw new NotSupportedException(method.Name)
        });
        Task<string> CaptureRecord(object?[] args)
        {
            insertedRecord = (CommissionRecord)args[0]!;
            return Task.FromResult(insertedRecord.RecordId);
        }

        var service = new CommissionService(
            uow,
            promoters,
            new TableLogService(new FinancialLogRepository()),
            commissions);
        var result = await service.RegisterCompletedOrderAsync(new CommissionOrderRequest
        {
            orderID = TestIds.Order,
            promoterID = promoter.PromoterId,
            finalAmount = 100m,
            goodsAmount = 100m
        });

        AssertEx.True(result.IsSuccess);
        AssertEx.True(insertedRecord != null);
        AssertEx.Equal(36, insertedRecord!.RecordId.Length);
        AssertEx.True(insertedRecord.RecordId.StartsWith("PROC", StringComparison.Ordinal));
    }

    private static async Task RefundCommissionFailureAsync()
    {
        var context = SeedOrder();
        context.OrderRepository.Orders.Single().OrderStatus = OrderStatusCodes.Completed;
        var uow = new FinancialUnitOfWork();
        var record = new CommissionRecord { RecordId = "COMM-TX", PromoterId = "PROM-TX", SignDate = DateTime.Now, CommBaseAmount = 10m };
        var commission = FinancialProxy.Create<ICommissionRepository>((method, _) =>
            method.Name == nameof(ICommissionRepository.GetByOrderIdAsync)
                ? Task.FromResult<CommissionRecord?>(record) : throw new InvalidOperationException("佣金撤销失败后不应继续更新佣金记录"));
        var promoter = FinancialProxy.Create<IPromoterRepository>((method, _) => method.Name switch
        {
            nameof(IPromoterRepository.GroupC_FindPromoterRecordAsync) => Task.FromResult<GroupC_CrmPromoter?>(new() { TotalSales = 1000m, PendingBalance = 100m }),
            nameof(IPromoterRepository.GroupC_UpdatePromoterTotalSalesAsync) => throw new InvalidOperationException("模拟团长销售额撤销失败"),
            _ => throw new NotSupportedException(method.Name)
        });
        var refunds = FinancialProxy.Create<IRefundRepository>((method, _) => method.Name == nameof(IRefundRepository.GetByIdAsync)
            ? Task.FromResult<FinRefund?>(new() { RefundId = "REF-TX", OrderId = TestIds.Order, RefundAmount = 100m, Status = "Pending" })
            : throw new InvalidOperationException("佣金撤销失败后不应批准退款"));
        var service = RefundService(context, uow, refunds, new FinancialLogRepository(), commission, promoter);
        var result = await service.AuditRefund("REF-TX", true, "ADMIN");
        AssertEx.True(!result.IsSuccess);
        AssertEx.True(result.ErrorMessage?.Contains("模拟团长销售额撤销失败", StringComparison.Ordinal) == true);
        AssertEx.True(uow.LastTransaction!.RolledBack && !uow.LastTransaction.Committed);
        AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(OrderStatusCodes.Completed, context.OrderRepository.Orders.Single().OrderStatus);
    }

    private static async Task RefundCommissionRecordFailureAsync()
    {
        foreach (var failStatus in new[] { false, true })
        {
            var context = SeedOrder();
            context.OrderRepository.Orders.Single().OrderStatus = OrderStatusCodes.Completed;
            var uow = new FinancialUnitOfWork();
            var commission = FinancialProxy.Create<ICommissionRepository>((method, _) => method.Name switch
            {
                nameof(ICommissionRepository.GetByOrderIdAsync) => Task.FromResult<CommissionRecord?>(new()
                    { RecordId = "COMM-TX", PromoterId = "REMOVED-PROMOTER", SignDate = DateTime.Now }),
                nameof(ICommissionRepository.UpdateStatusAsync) => Task.FromResult(!failStatus),
                nameof(ICommissionRepository.UpdateRefundedAmountAsync) => Task.FromResult(false),
                _ => throw new NotSupportedException(method.Name)
            });
            var promoter = FinancialProxy.Create<IPromoterRepository>((method, _) =>
                method.Name == nameof(IPromoterRepository.GroupC_FindPromoterRecordAsync)
                    ? Task.FromResult<GroupC_CrmPromoter?>(null) : throw new NotSupportedException(method.Name));
            var refunds = FinancialProxy.Create<IRefundRepository>((method, _) =>
                method.Name == nameof(IRefundRepository.GetByIdAsync)
                    ? Task.FromResult<FinRefund?>(new() { RefundId = "REF-TX", OrderId = TestIds.Order, RefundAmount = 100m })
                    : throw new InvalidOperationException("佣金更新失败后不应批准退款"));
            var result = await RefundService(context, uow, refunds, new FinancialLogRepository(), commission, promoter)
                .AuditRefund("REF-TX", true, "ADMIN");
            AssertEx.True(!result.IsSuccess);
            AssertEx.True(result.ErrorMessage?.Contains(failStatus ? "退款佣金状态更新失败" : "佣金已退金额更新失败", StringComparison.Ordinal) == true);
            AssertEx.True(uow.LastTransaction!.RolledBack && !uow.LastTransaction.Committed);
            AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
            AssertEx.Equal(OrderStatusCodes.Completed, context.OrderRepository.Orders.Single().OrderStatus);
        }
    }

    private static TestContext SeedOrder()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.Points = 100;
        context.OrderRepository.Orders.Add(new BizOrder
        {
            OrderId = TestIds.Order, CustomerId = TestIds.Customer, OrderNo = "ORD-FIN-TX",
            OrderStatus = OrderStatusCodes.Paid, TotalAmount = 100m, FinalAmount = 100m,
            PointsEarned = 20, CreatedAt = DateTime.Now
        });
        context.OrderRepository.Details.Add(new BizOrderDetail
        {
            OrderId = TestIds.Order, OrderDetailId = "DETAIL-TX", ProductId = "P1", ProductName = "测试商品",
            SupplierId = "SUP1", Quantity = 2, UnitPrice = 50m, SubTotal = 100m
        });
        return context;
    }

    private static RefundService RefundService(TestContext context, FinancialUnitOfWork uow,
        IRefundRepository refunds, FinancialLogRepository logs, ICommissionRepository? commissions = null,
        IPromoterRepository? promoters = null) => new(uow,
        promoters ?? FinancialProxy.Create<IPromoterRepository>(), FinancialProxy.Create<IPromoterService>(), refunds,
        new TableLogService(logs), commissions ?? FinancialProxy.Create<ICommissionRepository>((method, _) =>
            method.Name == nameof(ICommissionRepository.GetByOrderIdAsync)
                ? Task.FromResult<CommissionRecord?>(null) : throw new NotSupportedException(method.Name)),
        context.Service, context.OrderRepository, context.CustomerRepository,
        FinancialProxy.Create<IColdChainLogisticsService>(), FinancialProxy.Create<ILogExpressDeliveryRepository>((method, _) =>
            method.Name == nameof(ILogExpressDeliveryRepository.GetByOrderIdAsync)
                ? Task.FromResult(new List<LogExpressDelivery>()) : throw new NotSupportedException(method.Name)));
}

internal sealed class FinancialUnitOfWork : IUnitOfWork
{
    public FakeOrderTransaction? LastTransaction { get; private set; }
    public IDbConnection Connection => LastTransaction?.Connection ?? throw new InvalidOperationException("事务尚未开启");
    public IDbTransaction? Transaction { get; private set; }
    public Task BeginAsync() { LastTransaction = new(); Transaction = LastTransaction; return Task.CompletedTask; }
    public Task CommitAsync() { Transaction!.Commit(); Transaction = null; return Task.CompletedTask; }
    public Task RollbackAsync() { Transaction!.Rollback(); Transaction = null; return Task.CompletedTask; }
    public void AttachExternalTransaction(IDbTransaction transaction) => throw new NotSupportedException();
    public void Dispose() { }
}

internal sealed class FinancialLogRepository : ITableLogRepository
{
    public List<GroupC_LogAuditrails> Records { get; } = [];
    public IDbTransaction? LastTransaction { get; private set; }
    public bool ThrowOnInsert { get; init; }
    public Task GroupC_AddLogRecordAsync(GroupC_LogAuditrails logData,
        CancellationToken cancellationToken = default, IDbTransaction? transaction = null)
    {
        LastTransaction = transaction;
        if (ThrowOnInsert) throw new InvalidOperationException("模拟审计写入失败");
        if (transaction is FakeOrderTransaction staged) staged.Stage(() => Records.Add(logData));
        else Records.Add(logData);
        return Task.CompletedTask;
    }
    public Task<List<GroupC_LogAuditrails>> SearchAsync(DateTime? start, DateTime? end, string? table, string? action, string? user) => throw new NotSupportedException();
    public Task<List<string>> GetDistinctTableNamesAsync() => throw new NotSupportedException();
}

// 仅用于场景测试：未明确声明的调用立即失败，避免大量不相关仓储方法的空实现。
public class FinancialProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        Handler != null ? Handler(method!, args) : throw new NotSupportedException(method?.Name);
    public static T Create<T>(Func<MethodInfo, object?[]?, object?>? handler = null) where T : class
    {
        var instance = Create<T, FinancialProxy>();
        ((FinancialProxy)(object)instance).Handler = handler;
        return instance;
    }
}
