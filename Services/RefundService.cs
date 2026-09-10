using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using Newtonsoft.Json;
using System.Data;
using System.Runtime.ConstrainedExecution;
using System.Transactions;

namespace FreshColdChain.Services
{
    public class RefundService : IRefundService
    {
		private readonly IUnitOfWork _uow;
		private readonly IPromoterRepository _ipromoterRepository;
		private readonly IRefundRepository _irefundRepository;
		private readonly IPromoterService _ipromoterManager;
		private readonly ITableLogService _logManager;
		private readonly ICommissionRepository _icommissionRepository;
		private readonly IOrderService _orderService;   //B组真实订单接口（替代原 Mock_IGroupB / Mock_IGroupA）
		private readonly IOrderRepository _orderRepository;
		private readonly ICustomerRepository _customerRepository;
		private readonly IColdChainLogisticsService _coldChainLogisticsService;
		private readonly ILogExpressDeliveryRepository _deliveryRepository;
		public RefundService(IUnitOfWork uow, IPromoterRepository ipromoterRepository,
			IPromoterService ipromoterManager, IRefundRepository irefundRepository,
								ITableLogService log_Auditrails,
		ICommissionRepository icommissionRepository,
								IOrderService orderService, IOrderRepository orderRepository,
								ICustomerRepository customerRepository,
								IColdChainLogisticsService coldChainLogisticsService,
								ILogExpressDeliveryRepository deliveryRepository)
		{
			_uow = uow;
			_ipromoterRepository = ipromoterRepository;
			_irefundRepository = irefundRepository;
			_ipromoterManager = ipromoterManager;
			_logManager = log_Auditrails;
		_icommissionRepository = icommissionRepository;
			_orderService = orderService;
			_orderRepository = orderRepository;
			_customerRepository = customerRepository;
			_coldChainLogisticsService = coldChainLogisticsService;
			_deliveryRepository = deliveryRepository;
		}

		//退款上下文：一次退款所需的全部订单侧信息与计算结果
        private sealed class RefundContext
        {
            public BizOrder Order { get; set; } = new();
            public IReadOnlyList<BizOrderDetail> Details { get; set; } = [];
            public OrderStatus OrderStatus { get; set; }
            public CommissionRecord? Record { get; set; }
            public decimal RefundAmount { get; set; }
            public string? DetailId { get; set; }
            public string? SupplierId { get; set; }
            public int RefundQty { get; set; }
            public bool IsFullRefund { get; set; }
            public bool HasShipped { get; set; }
            public decimal Ratio { get; set; }
        }

        public async Task<Result> Refund(GroupC_RefundRequest refundRequest) //直接退款（免审核，兼容原接口）
        {
            // C 组发起事务，财务记录和通过 B 组接口执行的积分、订单变更共同提交。
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (string.IsNullOrEmpty(refundRequest.OrderId))     //不完整的输入信息
                {
                    await _uow.RollbackAsync();
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "不完整的退款请求信息";
                    return _result;
                }
                var ctx = await BuildContextFromRequestAsync(refundRequest, _uow.Transaction);
                //添加退款记录（免审流程直接置为已通过）
                var recordResult = await RefundRecord(ctx.Order.OrderId, ctx.DetailId, ctx.SupplierId, ctx.RefundQty, ctx.RefundAmount,
                    refundRequest.LiabilityType, refundRequest.Remark, "Approved", _uow.Transaction);
                if (!recordResult.IsSuccess)
                    throw new Exception(recordResult.ErrorMessage ?? "退款记录写入失败");
                await ExecuteRefundCoreAsync(ctx, _uow.Transaction);
                //支付流水是否需要回滚？默认不回滚支付流水好了
                await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                if(_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }

        }

        public async Task<Result> ApplyRefund(GroupC_RefundRequest refundRequest) //消费者申请退款：仅创建待审核申请单，不动资金
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (string.IsNullOrEmpty(refundRequest.OrderId))
                {
                    await _uow.RollbackAsync();
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "不完整的退款请求信息";
                    return _result;
                }
                var itemSelections = (refundRequest.Items ?? [])
                    .Where(item => !string.IsNullOrWhiteSpace(item.ProductID))
                    .ToList();
                if (itemSelections.Count != itemSelections
                        .Select(item => item.ProductID)
                        .Distinct(StringComparer.Ordinal)
                        .Count())
                    throw new Exception("退货商品不能重复选择");

                // 兼容旧调用；新统一入口一次可提交一个或多个商品。
                var contexts = new List<RefundContext>();
                if (itemSelections.Count == 0)
                {
                    contexts.Add(await BuildContextFromRequestAsync(refundRequest, _uow.Transaction));
                }
                else
                {
                    foreach (var item in itemSelections)
                    {
                        contexts.Add(await BuildContextFromRequestAsync(new GroupC_RefundRequest
                        {
                            OrderId = refundRequest.OrderId,
                            ProductID = item.ProductID,
                            RefundQty = item.RefundQty,
                            LiabilityType = refundRequest.LiabilityType,
                            Remark = refundRequest.Remark
                        }, _uow.Transaction));
                    }

                    var orderDetails = contexts[0].Details;
                    var isFullOrderSelection = itemSelections.Count == orderDetails.Count &&
                        orderDetails.All(detail => itemSelections.Any(item =>
                            string.Equals(item.ProductID, detail.ProductId, StringComparison.Ordinal) &&
                            item.RefundQty == detail.Quantity));
                    if (isFullOrderSelection)
                    {
                        contexts =
                        [
                            await BuildContextFromRequestAsync(new GroupC_RefundRequest
                            {
                                OrderId = refundRequest.OrderId,
                                LiabilityType = refundRequest.LiabilityType,
                                Remark = refundRequest.Remark
                            }, _uow.Transaction)
                        ];
                    }
                }

                var ctx = contexts[0];
                var existingApplications = await _irefundRepository.GetByOrderIdAsync(
                    ctx.Order.OrderId, _uow.Transaction);
                var occupiedApplications = existingApplications
                    .Where(application => application.Status is "Pending" or "Approved")
                    .ToList();
                var hasWholeOrderApplication = occupiedApplications.Any(application =>
                    string.IsNullOrWhiteSpace(application.DetailId));

                if (ctx.IsFullRefund && occupiedApplications.Count > 0)
                {
                    throw new Exception("该订单已有退款记录，不能重复申请整单退款");
                }
                if (!ctx.IsFullRefund && hasWholeOrderApplication)
                {
                    throw new Exception("该订单已有整单退款申请，请等待平台处理");
                }
                foreach (var selectedContext in contexts.Where(context => !context.IsFullRefund))
                {
                    var detail = selectedContext.Details.First(item =>
                        string.Equals(item.OrderDetailId, selectedContext.DetailId, StringComparison.Ordinal));
                    var occupiedQuantity = occupiedApplications
                        .Where(application => string.Equals(
                            application.DetailId, selectedContext.DetailId, StringComparison.Ordinal))
                        .Sum(application => application.RefundQty);
                    var remainingQuantity = Math.Max(0, detail.Quantity - occupiedQuantity);
                    if (selectedContext.RefundQty > remainingQuantity)
                        throw new Exception($"商品“{detail.ProductName}”最多还可申请退款 {remainingQuantity} 件");
                }

                // 未发货部分退款：退回移除本次商品后减少的运费。
                // 按“退前剩余商品运费 - 退后剩余商品运费”计算，可正确处理首重、续重和包邮门槛。
                await AddUnshippedPartialRefundFreightAsync(
                    contexts,
                    occupiedApplications,
                    _uow.Transaction);

                // 同一次多选申请在一个事务中写入；任一商品失败则全部回滚。
                foreach (var selectedContext in contexts)
                {
                    var recordResult = await RefundRecord(selectedContext.Order.OrderId, selectedContext.DetailId,
                        selectedContext.SupplierId, selectedContext.RefundQty, selectedContext.RefundAmount,
                        refundRequest.LiabilityType, refundRequest.Remark, "Pending", _uow.Transaction);
                    if (!recordResult.IsSuccess)
                        throw new Exception(recordResult.ErrorMessage ?? "退款申请记录写入失败");
                }

                //申请提交成功即把订单置为“退款审核中”，并记录申请前状态供驳回/取消时回退
                await _orderService.EnterRefundReviewAsync(
                    ctx.Order.OrderId,
                    externalTransaction: _uow.Transaction);

                await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
        }

        public async Task<RefundPreviewResult> PreviewRefundAsync(GroupC_RefundRequest refundRequest)
        {
            await _uow.BeginAsync();
            var result = new RefundPreviewResult();
            try
            {
                var contexts = await BuildPreviewContextsAsync(refundRequest, _uow.Transaction);
                var totalRefund = contexts.Sum(context => context.RefundAmount);
                decimal goodsRefund;
                if (contexts.Count == 1 && contexts[0].IsFullRefund)
                {
                    goodsRefund = Math.Max(
                        0m,
                        contexts[0].Order.FinalAmount - contexts[0].Order.FreightAmount);
                }
                else
                {
                    goodsRefund = contexts.Sum(context =>
                    {
                        var detail = context.Details.First(item =>
                            string.Equals(item.OrderDetailId, context.DetailId, StringComparison.Ordinal));
                        return CalculateGoodsRefundAmount(context, detail, context.RefundQty);
                    });
                }

                result.RefundAmount = Math.Round(totalRefund, 2, MidpointRounding.AwayFromZero);
                result.GoodsRefundAmount = Math.Round(goodsRefund, 2, MidpointRounding.AwayFromZero);
                result.FreightRefundAmount = Math.Round(
                    Math.Max(0m, result.RefundAmount - result.GoodsRefundAmount),
                    2,
                    MidpointRounding.AwayFromZero);
                result.IsSuccess = true;
                await _uow.CommitAsync();
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                result.ErrorMessage = $"系统错误：{ex.Message}";
            }
            return result;
        }

        private async Task<List<RefundContext>> BuildPreviewContextsAsync(
            GroupC_RefundRequest refundRequest,
            IDbTransaction? transaction)
        {
            if (string.IsNullOrEmpty(refundRequest.OrderId))
                throw new Exception("不完整的退款请求信息");

            var itemSelections = (refundRequest.Items ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item.ProductID))
                .ToList();
            if (itemSelections.Count == 0)
                throw new Exception("请选择退款商品");
            if (itemSelections.Count != itemSelections
                    .Select(item => item.ProductID)
                    .Distinct(StringComparer.Ordinal)
                    .Count())
                throw new Exception("退货商品不能重复选择");

            var contexts = new List<RefundContext>();
            foreach (var item in itemSelections)
            {
                contexts.Add(await BuildContextFromRequestAsync(new GroupC_RefundRequest
                {
                    OrderId = refundRequest.OrderId,
                    ProductID = item.ProductID,
                    RefundQty = item.RefundQty,
                    LiabilityType = refundRequest.LiabilityType
                }, transaction));
            }

            var orderDetails = contexts[0].Details;
            var isFullOrderSelection = itemSelections.Count == orderDetails.Count &&
                orderDetails.All(detail => itemSelections.Any(item =>
                    string.Equals(item.ProductID, detail.ProductId, StringComparison.Ordinal) &&
                    item.RefundQty == detail.Quantity));
            if (isFullOrderSelection)
            {
                contexts =
                [
                    await BuildContextFromRequestAsync(new GroupC_RefundRequest
                    {
                        OrderId = refundRequest.OrderId,
                        LiabilityType = refundRequest.LiabilityType
                    }, transaction)
                ];
            }

            var first = contexts[0];
            var occupiedApplications = (await _irefundRepository.GetByOrderIdAsync(
                    first.Order.OrderId,
                    transaction))
                .Where(application => application.Status is "Pending" or "Approved")
                .ToList();
            var hasWholeOrderApplication = occupiedApplications.Any(application =>
                string.IsNullOrWhiteSpace(application.DetailId));
            if (first.IsFullRefund && occupiedApplications.Count > 0)
                throw new Exception("该订单已有退款记录，不能重复申请整单退款");
            if (!first.IsFullRefund && hasWholeOrderApplication)
                throw new Exception("该订单已有整单退款申请，请等待平台处理");

            foreach (var context in contexts.Where(item => !item.IsFullRefund))
            {
                var detail = context.Details.First(item =>
                    string.Equals(item.OrderDetailId, context.DetailId, StringComparison.Ordinal));
                var occupiedQuantity = occupiedApplications
                    .Where(application => string.Equals(
                        application.DetailId,
                        context.DetailId,
                        StringComparison.Ordinal))
                    .Sum(application => application.RefundQty);
                var remainingQuantity = Math.Max(0, detail.Quantity - occupiedQuantity);
                if (context.RefundQty > remainingQuantity)
                    throw new Exception($"商品“{detail.ProductName}”最多还可申请退款 {remainingQuantity} 件");
            }

            await AddUnshippedPartialRefundFreightAsync(
                contexts,
                occupiedApplications,
                transaction);
            return contexts;
        }

        public async Task<Result> CancelRefundApplicationAsync(
            string orderId,
            string refundId,
            string customerId)
        {
            await _uow.BeginAsync();
            try
            {
                var application = await _irefundRepository.GetByIdAsync(refundId, _uow.Transaction);
                if (application == null ||
                    !string.Equals(application.OrderId, orderId, StringComparison.Ordinal))
                    throw new Exception("退款申请不存在");
                if (!string.Equals(application.Status, "Pending", StringComparison.Ordinal))
                    throw new Exception("仅待审核的退款申请可以取消");

                var cancelled = await _irefundRepository.TryUpdateStatusAsync(
                    refundId,
                    "Pending",
                    "Cancelled",
                    customerId,
                    null,
                    _uow.Transaction);
                if (!cancelled)
                    throw new Exception("退款申请状态已变化，请刷新后重试");

                //取消申请后，若订单已无待审核申请，订单从“退款审核中”回退到申请前状态
                await ExitRefundReviewIfNoPendingAsync(orderId, _uow.Transaction);

                await _uow.CommitAsync();
                return new Result { IsSuccess = true };
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                return new Result { IsSuccess = false, ErrorMessage = $"系统错误：{ex.Message}" };
            }
        }

        public async Task<Result> ApplyCheckoutBatchRefundAsync(string checkoutBatchId, string customerId, string remark)
        {
            await _uow.BeginAsync();
            try
            {
                var transaction = _uow.Transaction ?? throw new InvalidOperationException("退款事务未初始化");
                var orders = await _orderRepository.GetByCheckoutBatchForUpdateAsync(checkoutBatchId, customerId, transaction);
                if (orders.Count == 0)
                    throw new Exception("结算批次不存在或不属于当前消费者");
                foreach (var order in orders)
                {
                    var ctx = await BuildContextFromRequestAsync(new GroupC_RefundRequest
                    {
                        OrderId = order.OrderId, LiabilityType = "Customer", Remark = remark
                    }, transaction);
                    if (await _irefundRepository.HasPendingApplicationAsync(order.OrderId, transaction))
                        throw new Exception($"订单 {order.OrderNo} 已有待审核退款申请");
                    var created = await RefundRecord(ctx.Order.OrderId, null, null, 0, ctx.RefundAmount,
                        "Customer", remark, "Pending", transaction);
                    if (!created.IsSuccess)
                        throw new Exception(created.ErrorMessage ?? "退款申请记录写入失败");
                    //未支付/已取消的订单不进入退款审核中，此处接口内部会自行跳过
                    await _orderService.EnterRefundReviewAsync(
                        order.OrderId,
                        externalTransaction: transaction);
                }
                await _uow.CommitAsync();
                return new Result { IsSuccess = true };
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open) await _uow.RollbackAsync();
                return new Result { IsSuccess = false, ErrorMessage = $"系统错误：{ex.Message}" };
            }
        }

        public async Task<Result> AuditRefund(string refundId, bool approved, string auditorId, string? auditRemark = null) //管理员审核退款申请
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (string.IsNullOrEmpty(refundId))
                {
                    throw new Exception("退款申请编号为空");
                }
                var application = await _irefundRepository.GetByIdAsync(refundId, _uow.Transaction);
                if (application == null)
                {
                    throw new Exception("退款申请不存在");
                }
                if (application.Status != "Pending")    //幂等：已处理的申请拒绝重复审核
                {
                    throw new Exception("该申请已被处理，请勿重复审核");
                }

                if (!approved)
                {
                    //驳回：仅更新申请单状态（乐观锁防并发重复审核），不涉及任何资金变动
                    if (!await _irefundRepository.TryUpdateStatusAsync(refundId, "Pending", "Rejected", auditorId, auditRemark, _uow.Transaction))
                    {
                        throw new Exception("申请状态已变化，审核失败请重试");
                    }
                    //驳回成功后，若订单已无待审核申请，订单从“退款审核中”回退到申请前状态
                    await ExitRefundReviewIfNoPendingAsync(application.OrderId!, _uow.Transaction);
                    await _uow.CommitAsync();
                    _result.IsSuccess = true;
                    return _result;
                }

                //通过：按申请单重建退款上下文并重新校验订单当前状态（申请后可能已变化，如过退款期/已退款）
                var ctx = await BuildContextFromApplicationAsync(application, _uow.Transaction);
                //执行退款资金操作（佣金/销售额回滚、佣金记录更新、B组积分扣减与订单状态变更）
                await ExecuteRefundCoreAsync(ctx, _uow.Transaction);
                //全部成功后置为已通过（乐观锁）
                if (!await _irefundRepository.TryUpdateStatusAsync(refundId, "Pending", "Approved", auditorId, auditRemark, _uow.Transaction))
                {
                    throw new Exception("申请状态已变化，审核失败请重试");
                }
                await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
        }

        public async Task<List<FinRefund>> GetPendingRefundsAsync() //管理端待审核列表
        {
            return await _irefundRepository.GetByStatusAsync("Pending");
        }

        public async Task<List<FinRefund>> GetOrderRefundsAsync(string orderId) //消费者端查询订单退款记录
        {
            return await _irefundRepository.GetByOrderIdAsync(orderId);
        }

        public async Task<List<FinRefund>> GetOrderRefundsAsync(IReadOnlyCollection<string> orderIds) //消费者端批量查询订单退款记录
        {
            if (orderIds == null || orderIds.Count == 0)
            {
                return new List<FinRefund>();
            }
            return await _irefundRepository.GetByOrderIdsAsync(orderIds);
        }

        //存在待审核退款申请的订单编号：订单真实状态往往仍是“已完成”，
        //消费者端“退款售后”列表需据此把这些订单一起筛出来展示为“退款待审核”
        public async Task<List<string>> GetOrderIdsWithPendingRefundAsync()
        {
            var pending = await _irefundRepository.GetByStatusAsync("Pending");
            return pending
                .Select(refund => refund.OrderId)
                .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
                .Select(orderId => orderId!.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public async Task<List<FinRefund>> SearchRefundsAsync(DateTime? startTime, DateTime? endTime,
            string? orderId, string? status) //管理端组合查询退款记录
        {
            return await _irefundRepository.SearchAsync(startTime, endTime, orderId, status);
        }

        //加载订单与佣金记录，并做退款准入校验（状态机 + 已结算拦截 + 14天退款期）
        private async Task<RefundContext> LoadOrderContextAsync(string orderId, IDbTransaction? transaction)
        {
            //【串联B组】通过B组真实接口获取订单及明细
            var orderDetail = await _orderService.GetOrderDetailAsync(orderId);
            if (orderDetail?.Order == null)
            {
                throw new Exception("订单不存在，无法退款！");
            }
            var order = orderDetail.Order;
            var orderStatus = OrderStatusCodes.Parse(order.OrderStatus);
            if (orderStatus is OrderStatus.PendingPayment or OrderStatus.Cancelled)
            {
                throw new Exception("订单未支付或已取消，无法退款！");
            }
            if (orderStatus == OrderStatus.Refunded)
            {
                throw new Exception("订单已退款，请勿重复操作！");
            }

            //订单处于“退款审核中”时，真实交易状态记录在 StatusBeforeRefund：
            //退款准入（佣金结算拦截、14 天可退期、运费是否已发生、佣金回滚）必须按申请前状态判断，
            //否则待审核期间会绕过这些防线。
            var admissionStatus = orderStatus == OrderStatus.RefundReviewing
                ? ParseStatusBeforeRefund(order.StatusBeforeRefund, orderStatus)
                : orderStatus;

            //查询C组自己的佣金记录（FIN_PROCOMRECORDS），用于退款期校验与佣金回滚
            var record = await _icommissionRepository.GetByOrderIdAsync(orderId, transaction);

            //退款准入校验（仅已签收订单）：
            //防线一：佣金已过退款期并完成二段结算（Settled），佣金已转入可提现余额，禁止退款
            //防线二：超过可退款期14天，拒绝退款（优先取佣金记录签收时间；无佣金记录的订单用订单最后状态变更时间兜底）
            if (admissionStatus == OrderStatus.Completed)
            {
                if (record?.Status == "Settled")
                {
                    throw new Exception("该订单佣金已过退款期并完成结算，无法退款！");
                }
                var signTime = record?.SignDate ?? order.UpdatedAt;
                if (signTime != null && DateTime.Now - signTime.Value >= TimeSpan.FromDays(14))
                {
                    throw new Exception("该订单已过可退期14天，无法退款！");
                }
            }

            var deliveries = await _deliveryRepository.GetByOrderIdAsync(orderId);
            return new RefundContext
            {
                Order = order,
                Details = orderDetail.Details,
                OrderStatus = admissionStatus,
                Record = record,
                // REFUNDING 可能来自“已发货后部分退款”，也可能来自
                // “未发货部分退款”，不能再仅靠订单状态判断运费是否已发生。
                HasShipped = admissionStatus is OrderStatus.Shipped or OrderStatus.Completed ||
                    deliveries.Count > 0
            };
        }

        //回退状态缺失或非法（历史脏数据）时按当前状态处理，避免单条脏数据阻断退款审核
        private static OrderStatus ParseStatusBeforeRefund(string? statusBeforeRefund, OrderStatus fallback)
        {
            if (string.IsNullOrWhiteSpace(statusBeforeRefund))
            {
                return fallback;
            }

            try
            {
                return OrderStatusCodes.Parse(statusBeforeRefund);
            }
            catch (ArgumentException)
            {
                return fallback;
            }
        }

        //退款申请被驳回/取消后，若订单已无待审核申请，则把订单从“退款审核中”回退到申请前状态
        private async Task ExitRefundReviewIfNoPendingAsync(string orderId, IDbTransaction? transaction)
        {
            var applications = await _irefundRepository.GetByOrderIdAsync(orderId, transaction);
            if (applications.Any(application =>
                    string.Equals(application.Status, "Pending", StringComparison.Ordinal)))
            {
                return;
            }

            await _orderService.ExitRefundReviewAsync(orderId, externalTransaction: transaction);
        }

        //按消费者退款请求构建退款上下文（计算退款金额/数量/明细）
        private async Task<RefundContext> BuildContextFromRequestAsync(GroupC_RefundRequest refundRequest, IDbTransaction? transaction)
        {
            var ctx = await LoadOrderContextAsync(refundRequest.OrderId!, transaction);
            // 兼容旧接口：ProductID 为空时直接按整单退款处理。
            if (string.IsNullOrEmpty(refundRequest.ProductID))
            {
                ctx.IsFullRefund = true;
                // 发货后冷链运费已实际发生，整单退款也不退运费。
                ctx.RefundAmount = ctx.HasShipped
                    ? Math.Max(0m, ctx.Order.FinalAmount - ctx.Order.FreightAmount)
                    : ctx.Order.FinalAmount;
                ctx.RefundQty = 0;                          // 整单不计数量
            }
            else
            {
                //【串联B组】从B组订单明细快照中匹配退款商品
                var detail = ctx.Details.FirstOrDefault(d => d.ProductId == refundRequest.ProductID);
                if (detail == null)
                {
                    throw new Exception("该订单中不存在此商品，请核对退款商品！");
                }
                if (refundRequest.RefundQty <= 0 || refundRequest.RefundQty > detail.Quantity) //退款数量不合法
                {
                    throw new Exception("退款数量不合法，请重新输入退款数量！");
                }
                // 新统一入口始终提交商品和数量。单商品订单退满全部数量时，
                // 服务端自动识别为整单退款，前端不再传递或选择退款类型。
                if (ctx.Details.Count == 1 && refundRequest.RefundQty == detail.Quantity)
                {
                    ctx.IsFullRefund = true;
                    ctx.RefundAmount = ctx.HasShipped
                        ? Math.Max(0m, ctx.Order.FinalAmount - ctx.Order.FreightAmount)
                        : ctx.Order.FinalAmount;
                    ctx.RefundQty = 0;
                }
                else
                {
                    ctx.DetailId = detail.OrderDetailId;
                    ctx.SupplierId = detail.SupplierId;    //供应商信息直接取自B组订单明细快照
                    ctx.RefundQty = refundRequest.RefundQty;
                    var discountRate = ctx.Order.TotalAmount <= 0
                        ? 1m
                        : Math.Max(0m, ctx.Order.TotalAmount - ctx.Order.DiscountAmount) / ctx.Order.TotalAmount;
                    ctx.RefundAmount = Math.Round(
                        ctx.RefundQty * detail.UnitPrice * discountRate,
                        2,
                        MidpointRounding.AwayFromZero);
                }
            }
            FinalizeRatio(ctx);
            return ctx;
        }

        //按已存在的退款申请单重建退款上下文（审核通过时调用，信任申请单中的金额并重新校验订单状态）
        private async Task<RefundContext> BuildContextFromApplicationAsync(FinRefund application, IDbTransaction? transaction)
        {
            var ctx = await LoadOrderContextAsync(application.OrderId!, transaction);
            ctx.IsFullRefund = string.IsNullOrEmpty(application.DetailId);
            ctx.DetailId = application.DetailId;
            ctx.SupplierId = application.SupplierId;
            ctx.RefundQty = application.RefundQty;
            ctx.RefundAmount = application.RefundAmount;
            // 退款记录保存“商品退款 + 运费退款”合计值。
            // 积分与佣金只能按商品实付额回滚，不能把运费计入退款比例。
            decimal? goodsRefundAmount = null;
            if (!ctx.IsFullRefund)
            {
                var detail = ctx.Details.FirstOrDefault(item =>
                    string.Equals(item.OrderDetailId, ctx.DetailId, StringComparison.Ordinal));
                if (detail == null)
                    throw new Exception("退款对应的订单明细不存在");
                goodsRefundAmount = CalculateGoodsRefundAmount(ctx, detail, ctx.RefundQty);
            }
            FinalizeRatio(ctx, goodsRefundAmount);
            return ctx;
        }

        private static void FinalizeRatio(RefundContext ctx, decimal? goodsRefundAmount = null)
        {
            if (ctx.Order.FinalAmount <= 0)
            {
                throw new Exception("订单实付金额异常，无法计算退款比例！");
            }
            ctx.Ratio = ctx.IsFullRefund
                ? 1m
                : Math.Min(1m, ctx.RefundQty <= 0 || ctx.Order.TotalAmount <= 0
                    ? 0m
                    : (goodsRefundAmount ?? ctx.RefundAmount) /
                      Math.Max(0.01m, ctx.Order.TotalAmount - ctx.Order.DiscountAmount));
        }

        private async Task AddUnshippedPartialRefundFreightAsync(
            IReadOnlyList<RefundContext> contexts,
            IReadOnlyList<FinRefund> occupiedApplications,
            IDbTransaction? transaction)
        {
            if (contexts.Count == 0 || contexts.Any(context => context.IsFullRefund))
                return;

            var first = contexts[0];
            if (first.HasShipped || first.Order.FreightAmount <= 0)
                return;

            var address = await _customerRepository.GetAddressAsync(
                first.Order.CustomerId,
                first.Order.AddressId,
                transaction);
            if (address == null)
                throw new Exception("订单收货地址不存在，无法计算应退运费");

            var occupiedByDetail = occupiedApplications
                .Where(application => !string.IsNullOrWhiteSpace(application.DetailId))
                .GroupBy(application => application.DetailId!, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(application => application.RefundQty),
                    StringComparer.Ordinal);
            var selectedByDetail = contexts.ToDictionary(
                context => context.DetailId!,
                context => context.RefundQty,
                StringComparer.Ordinal);

            var beforeItems = first.Details
                .Select(detail => new FreightItemDto
                {
                    ProductID = detail.ProductId,
                    SupplierID = detail.SupplierId,
                    Quantity = Math.Max(0, detail.Quantity -
                        occupiedByDetail.GetValueOrDefault(detail.OrderDetailId))
                })
                .Where(item => item.Quantity > 0)
                .ToList();
            var afterItems = first.Details
                .Select(detail => new FreightItemDto
                {
                    ProductID = detail.ProductId,
                    SupplierID = detail.SupplierId,
                    Quantity = Math.Max(0, detail.Quantity -
                        occupiedByDetail.GetValueOrDefault(detail.OrderDetailId) -
                        selectedByDetail.GetValueOrDefault(detail.OrderDetailId))
                })
                .Where(item => item.Quantity > 0)
                .ToList();

            var beforeGoodsAmount = first.Details.Sum(detail =>
                Math.Max(0, detail.Quantity - occupiedByDetail.GetValueOrDefault(detail.OrderDetailId)) *
                detail.UnitPrice);
            var afterGoodsAmount = first.Details.Sum(detail =>
                Math.Max(0, detail.Quantity - occupiedByDetail.GetValueOrDefault(detail.OrderDetailId) -
                    selectedByDetail.GetValueOrDefault(detail.OrderDetailId)) * detail.UnitPrice);

            var beforeFreight = await QuoteFreightAsync(beforeItems, beforeGoodsAmount, address);
            var afterFreight = afterItems.Count == 0
                ? 0m
                : await QuoteFreightAsync(afterItems, afterGoodsAmount, address);

            var previouslyRefundedFreight = occupiedApplications
                .Where(application => !string.IsNullOrWhiteSpace(application.DetailId))
                .Sum(application => CalculateRecordedFreightRefund(first, application));
            var remainingFreightBudget = Math.Max(
                0m,
                first.Order.FreightAmount - previouslyRefundedFreight);
            var refundableFreight = Math.Min(
                remainingFreightBudget,
                Math.Max(0m, beforeFreight - afterFreight));
            refundableFreight = Math.Round(refundableFreight, 2, MidpointRounding.AwayFromZero);
            if (refundableFreight <= 0)
                return;

            var totalGoodsRefund = contexts.Sum(context => context.RefundAmount);
            var remainingAllocation = refundableFreight;
            for (var index = 0; index < contexts.Count; index++)
            {
                var freightShare = index == contexts.Count - 1
                    ? remainingAllocation
                    : Math.Min(
                        remainingAllocation,
                        Math.Round(
                            refundableFreight * contexts[index].RefundAmount /
                            Math.Max(0.01m, totalGoodsRefund),
                            2,
                            MidpointRounding.AwayFromZero));
                contexts[index].RefundAmount += freightShare;
                remainingAllocation -= freightShare;
            }
        }

        private async Task<decimal> QuoteFreightAsync(
            List<FreightItemDto> items,
            decimal goodsAmount,
            CrmUserAddress address)
        {
            var quote = await _coldChainLogisticsService.QuoteFreightAsync(new FreightQuoteRequest
            {
                Province = address.Province,
                City = address.City,
                District = address.District,
                GoodsAmount = goodsAmount,
                Items = items
            });
            if (!quote.IsSuccess || quote.Data == null)
                throw new Exception($"应退运费计算失败：{quote.Message}");
            return quote.Data.FreightAmount;
        }

        private static decimal CalculateRecordedFreightRefund(
            RefundContext ctx,
            FinRefund application)
        {
            var detail = ctx.Details.FirstOrDefault(item =>
                string.Equals(item.OrderDetailId, application.DetailId, StringComparison.Ordinal));
            if (detail == null)
                return 0m;
            var goodsRefund = CalculateGoodsRefundAmount(ctx, detail, application.RefundQty);
            return Math.Max(0m, application.RefundAmount - goodsRefund);
        }

        private static decimal CalculateGoodsRefundAmount(
            RefundContext ctx,
            BizOrderDetail detail,
            int refundQty)
        {
            var discountRate = ctx.Order.TotalAmount <= 0
                ? 1m
                : Math.Max(0m, ctx.Order.TotalAmount - ctx.Order.DiscountAmount) /
                  ctx.Order.TotalAmount;
            return Math.Round(
                refundQty * detail.UnitPrice * discountRate,
                2,
                MidpointRounding.AwayFromZero);
        }

        //执行退款的资金操作：佣金/销售额回滚、佣金记录更新、B组积分扣减与订单状态变更（不含申请单写入）
        private async Task ExecuteRefundCoreAsync(RefundContext ctx, IDbTransaction? transaction)
        {
            if (transaction?.Connection?.State != ConnectionState.Open)
                throw new InvalidOperationException("退款事务未初始化或已失效");
            // 仅已签收（Completed）订单需要回滚团长佣金；未签收订单尚未结算佣金，无需回滚
            if (ctx.OrderStatus == OrderStatus.Completed && ctx.Record != null)
            {
                // 基础佣金按退款比例回滚；阶梯奖励佣金在 RefundRollbackMoney 内按累计销售额跌破的档位撤销
                var baseRollback = ctx.Record.CommBaseAmount * ctx.Ratio;
                //如果团长和消费者解绑了，那么团长佣金不会扣（平台在解绑触发时自动清空团长的待结算余额，防止团长反复解绑/绑定刷回退佣金差）
                var _promoterInfo = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(ctx.Record.PromoterId, transaction);
                if (_promoterInfo != null)    //团长存在,则回滚（销售额同样按退款比例回滚）
                {
                    var rollbackResult = await RefundRollbackMoney(
                        ctx.Record.PromoterId, baseRollback, ctx.Order.TotalAmount * ctx.Ratio, transaction);
                    if (!rollbackResult.IsSuccess)
                        throw new InvalidOperationException(rollbackResult.ErrorMessage ?? "退款佣金撤销失败");
                }
                //更改佣金记录：整单退整体置为已退款；部分退仅累加已退金额
                if (ctx.IsFullRefund)
                {
                    if (!await _icommissionRepository.UpdateStatusAsync(ctx.Record.RecordId, "Refunded", transaction))
                        throw new InvalidOperationException("退款佣金状态更新失败");
                }
                if (!await _icommissionRepository.UpdateRefundedAmountAsync(ctx.Record.RecordId, ctx.RefundAmount, transaction))
                    throw new InvalidOperationException("佣金已退金额更新失败");
            }
            //未签收订单退款：无需回滚佣金。
            // B 组下单时不锁库存，因此未发货退款不需要释放库存。
            //已发货订单的物流拦截/通知供应商接口需AB组之间另行约定，待补充

            //【串联B组】积分回滚 + 订单状态变更
            var pointsToDeduct = (int)Math.Floor(ctx.Order.PointsEarned * ctx.Ratio);
            if (ctx.IsFullRefund)
            {
                // 整单退款：B 组复用 C 组事务扣回积分并将订单置为“已退款”。
                await _orderService.DeductPointsForRefundAsync(ctx.Order.CustomerId, ctx.Order.OrderId,
                    pointsToDeduct, externalTransaction: transaction);
            }
            else
            {
                //部分退款：B组按比例扣回积分并将订单置为"退款中"
                await _orderService.DeductPointsForPartialRefundAsync(ctx.Order.CustomerId, ctx.Order.OrderId,
                    pointsToDeduct, externalTransaction: transaction);
            }
        }

        private async Task<Result> RefundRollbackMoney(string? promoterID, decimal baseCommission, decimal goodsAmount,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)  //退款回滚与退款信息记录（baseCommission为基础佣金回滚额，阶梯奖励佣金按跌破档位在内部计算）
        {

            var _result = new Result();
            bool ownTransaction = false;
            try
            {
                if(transaction == null)
                {
                    await _uow.BeginAsync();
                    ownTransaction = true;
                    transaction = _uow.Transaction;

                }
                if (promoterID == null || promoterID == string.Empty)
                {
                    throw new Exception("团长信息为空");
                }
                var _promoterInfo = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(promoterID, transaction);
                if (_promoterInfo == null)    //无效的团长编号
                {
                    throw new Exception("该团长信息不存在");

                }
                //回滚佣金与销售额
                //记录旧的累计销售额，待结算余额
                var _oldPromoterTotalSales = _promoterInfo.TotalSales;
                var _oldPromoterPendingBalance = _promoterInfo.PendingBalance;
                //计算新的累计销售额：退款使累计销售额向下跌破阶梯档位时，对应奖励佣金全部撤销（与一段结算的发放逻辑对称）
                var _newPromoterTotalSales = _oldPromoterTotalSales - goodsAmount;
                var _bonusRollback = GroupC_CommissionBonusPolicy.CalculateRollbackBonus(_oldPromoterTotalSales, _newPromoterTotalSales);
                var _totalCommissionRollback = baseCommission + _bonusRollback;
                //回滚销售额与佣金
                await _ipromoterRepository.GroupC_UpdatePromoterTotalSalesAsync(promoterID, -goodsAmount, transaction);
                await _ipromoterRepository.GroupC_UpdatePromoterPendingBalanceAsync(promoterID, -_totalCommissionRollback, transaction);
                //记录新的佣金与销售额
                var _newPromoterPendingBalance = _oldPromoterPendingBalance - _totalCommissionRollback;
                //产生日志信息（两条）
                //Pendingbalance的扣减
                var _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
                if (!await _logManager.WriteTableChangeLog(_tableLog, transaction, cancellationToken))
                    throw new InvalidOperationException("退款审计日志写入失败");
                //TotalSales的扣减
                _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { TotalSales = _oldPromoterTotalSales });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { TotalSales = _newPromoterTotalSales });
                if (!await _logManager.WriteTableChangeLog(_tableLog, transaction, cancellationToken))
                    throw new InvalidOperationException("退款审计日志写入失败");

                //等级挂钩回滚：退款使累计销售额跌破档位导致等级下降时，佣金比例同步回退到对应档位（与结算跨档升级逻辑对称）
                var _levelRate = GroupC_LevelCommissionPolicy.ResolveRate(_newPromoterTotalSales);
                if (_promoterInfo.BaseCommissionRate != _levelRate)
                {
                    if (!await _ipromoterRepository.GroupC_UpdatePromoterCommissionRateAsync(promoterID, _levelRate, transaction))
                        throw new InvalidOperationException("退款后团长佣金比例更新失败");
                    _tableLog = new GroupC_LogAuditrails();
                    _tableLog.ActionType = "Update";
                    _tableLog.TableName = "CRM_PROMOTERS";
                    _tableLog.OperatorType = "Platform";
                    _tableLog.OperatorId = "\\";
                    _tableLog.OldValue = JsonConvert.SerializeObject(new { BaseCommissionRate = _promoterInfo.BaseCommissionRate });
                    _tableLog.NewValue = JsonConvert.SerializeObject(new { BaseCommissionRate = _levelRate });
                    if (!await _logManager.WriteTableChangeLog(_tableLog, transaction, cancellationToken))
                        throw new InvalidOperationException("退款审计日志写入失败");
                }

                if (ownTransaction)
                    await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                if (ownTransaction && _uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
        }

        public async Task<Result> RefundRecord(string? orderID, string? detailID, string? supplierID, int refundQty, decimal refundAmount, string? liabilityType,string? remark,
            string status = "Approved",
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default
            )  //添加退款信息函数（status: Pending=待审核申请单, Approved=已执行的退款记录）
        {
            var _result = new Result();
            bool ownTransaction = false;
            try
            {
                if (transaction == null)
                {
                    await _uow.BeginAsync();
                    ownTransaction = true;
                    transaction = _uow.Transaction;

                }

                FinRefund _finRefund = new FinRefund();
                _finRefund.RefundId = "REF_" + Guid.NewGuid().ToString("N"); //自动生成退款编号
                _finRefund.OrderId = orderID;
                _finRefund.DetailId = detailID;
                _finRefund.SupplierId = supplierID;
                _finRefund.RefundAmount = refundAmount;
                _finRefund.RefundQty = refundQty;
                _finRefund.LiabilityType = liabilityType;
                _finRefund.ApplyTime = DateTime.Now;
                _finRefund.Remark = remark;
                _finRefund.Status = status;
                await _irefundRepository.InsertRefundAsync(_finRefund, transaction);

                //产生日志信息
                var _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Create";
                _tableLog.TableName = "FIN_REFUND";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = string.Empty;
                _tableLog.NewValue = JsonConvert.SerializeObject(new
                {
                    RefundId = _finRefund.RefundId,
                    OrderId = _finRefund.OrderId,
                    DetailId = _finRefund.DetailId,
                    SupplierId = _finRefund.SupplierId,
                    RefundQty = _finRefund.RefundQty,
                    RefundAmount = _finRefund.RefundAmount,
                    LiabilityType = _finRefund.LiabilityType,
                    ApplyTime = _finRefund.ApplyTime,
                    Remark = _finRefund.Remark,
                    Status = _finRefund.Status
                });
                if (!await _logManager.WriteTableChangeLog(_tableLog, transaction, cancellationToken))
                    throw new InvalidOperationException("退款审计日志写入失败");
                if (ownTransaction)
                    await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;

            }
            catch (Exception ex)
            {
                if (ownTransaction && _uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
        }
    }
}
