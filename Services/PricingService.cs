using System.Text.RegularExpressions;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// A组动态定价引擎
///
/// 计算流程：
///   1. 查商品 DefaultPrice
///   2. 查该商品所有启用的规则（IsActive=1，在有效期内，按 Priority 升序）
///   3. 遍历规则，第一个满足触发条件的规则生效：
///      - TimeBased:      当前时间在 TimeWindow 内 → 应用折扣率
///      - BulkDiscount:   购买量 >= MinQuantity → 应用折扣率
///      - ExpiryApproaching: 商品批次近 N 小时过期 → 应用折扣率
///      - ManualPrice:    直接使用 ManualPrice 作为最终售价
///   4. 无规则命中 → 返回 DefaultPrice
/// </summary>
public class PricingService : IPricingService
{
    private readonly IPriceRuleRepository _ruleRepo;
    private readonly IProductRepository _productRepo;
    private readonly IStockBatchRepository _batchRepo;
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// DB 存储的 TriggerType → 代码内部 TriggerType 映射。
    /// DB 数据使用 NEAR_EXPIRY/SCHEDULED/QUANTITY/TIME_SLOT/SEASONAL/BULK 等值，
    /// 代码内部使用 ExpiryApproaching/TimeBased/BulkDiscount/ManualPrice。
    /// </summary>
    private static readonly Dictionary<string, string> TriggerTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NEAR_EXPIRY"] = "ExpiryApproaching",
        ["SCHEDULED"] = "TimeBased",
        ["TIME_SLOT"] = "TimeBased",
        ["SEASONAL"] = "TimeBased",
        ["QUANTITY"] = "BulkDiscount",
        ["BULK"] = "BulkDiscount",
        ["ExpiryApproaching"] = "ExpiryApproaching",
        ["TimeBased"] = "TimeBased",
        ["BulkDiscount"] = "BulkDiscount",
        ["ManualPrice"] = "ManualPrice",
    };

    /// <summary>将 DB 的 TriggerType 映射为内部标准值，未知类型返回原值</summary>
    private static string NormalizeTriggerType(string? dbType)
        => !string.IsNullOrWhiteSpace(dbType) && TriggerTypeMap.TryGetValue(dbType, out var mapped)
            ? mapped
            : dbType ?? string.Empty;

    public PricingService(
        IPriceRuleRepository ruleRepo,
        IProductRepository productRepo,
        IStockBatchRepository batchRepo,
        IUnitOfWork uow)
    {
        _ruleRepo = ruleRepo;
        _productRepo = productRepo;
        _batchRepo = batchRepo;
        _uow = uow;
    }

    // ==================== 价格计算（核心算法） ====================

    public async Task<ApiResponse<PriceCalculationResult>> CalculatePriceAsync(PriceCalculationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProductID) || request.Quantity <= 0)
            return ApiResponse<PriceCalculationResult>.Fail("商品 ID 和数量无效");

        // 1. 查商品
        var product = await _productRepo.GetByIdAsync(request.ProductID);
        if (product == null)
            return ApiResponse<PriceCalculationResult>.Fail("商品不存在", 404);

        // 2. 查所有启用的规则（已在 Repository 层按 Priority 排序）
        var now = request.ReferenceTime ?? DateTime.Now;
        var rules = await _ruleRepo.GetActiveByProductIdAsync(request.ProductID, now);

        // 3. 遍历规则取第一个命中
        foreach (var rule in rules)
        {
            if (await IsRuleTriggeredAsync(rule, product, request.Quantity, now))
            {
                var finalPrice = ComputeFinalPrice(rule, product.DefaultPrice);
                return ApiResponse<PriceCalculationResult>.Success(new PriceCalculationResult
                {
                    ProductID = product.ProductID,
                    DefaultPrice = product.DefaultPrice,
                    FinalPrice = finalPrice,
                    MatchedRuleName = rule.RuleName,
                    MatchedRuleID = rule.RuleID,
                    TriggerType = rule.TriggerType
                });
            }
        }

        // 4. 无规则命中，返回默认价格
        return ApiResponse<PriceCalculationResult>.Success(new PriceCalculationResult
        {
            ProductID = product.ProductID,
            DefaultPrice = product.DefaultPrice,
            FinalPrice = product.DefaultPrice,
            MatchedRuleName = null,
            MatchedRuleID = null
        });
    }

    // ==================== 触发条件判断 ====================

    /// <summary>
    /// 判断当前规则是否满足触发条件
    /// </summary>
    private async Task<bool> IsRuleTriggeredAsync(BizPriceRule rule, InvProduct product, decimal quantity, DateTime now)
    {
        var triggerType = NormalizeTriggerType(rule.TriggerType);
        return triggerType switch
        {
            "TimeBased" => IsTimeInWindow(rule.TimeWindow, now),
            "BulkDiscount" => IsBulkMatch(rule, quantity),
            "ExpiryApproaching" => await IsProductExpiringSoonAsync(product, rule.TimeWindow, now),
            "ManualPrice" => rule.ManualPrice.HasValue,
            _ => true // 未知类型默认触发（向后兼容）
        };
    }

    /// <summary>
    /// 解析 TimeWindow 并判断当前时间是否在窗口内。支持格式：
    ///   "HH:mm-HH:mm"  — 时段窗口（支持跨天如 "22:00-06:00"）
    ///   ALL_DAY          — 全时段始终匹配
    ///   WEEKEND_ONLY     — 周六日
    ///   NIGHT_22_TO_02   — 深夜 22:00-02:00
    ///   AFTER_18_00      — 18:00 之后
    ///   SUMMER_SEASON    — 6-8 月夏季
    ///   空/NULL           — 全时段匹配
    /// </summary>
    private static bool IsTimeInWindow(string? timeWindow, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(timeWindow))
            return true;

        // 特殊关键字匹配（大小写不敏感）
        var tw = timeWindow.Trim().ToUpperInvariant();

        if (tw is "ALL_DAY" or "ALLDAY")
            return true;

        if (tw == "WEEKEND_ONLY")
            return now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        if (tw == "SUMMER_SEASON")
            return now.Month is >= 6 and <= 8;

        if (tw == "AFTER_18_00")
            return now.TimeOfDay >= TimeSpan.FromHours(18);

        // NIGHT_22_TO_02 → 转为 "22:00-02:00"
        if (tw == "NIGHT_22_TO_02")
            return now.TimeOfDay >= TimeSpan.FromHours(22)
                || now.TimeOfDay <= TimeSpan.FromHours(2);

        // 通用 "HH:mm-HH:mm" 格式
        var parts = timeWindow.Split('-');
        if (parts.Length == 2
            && TimeSpan.TryParse(parts[0], out var start)
            && TimeSpan.TryParse(parts[1], out var end))
        {
            var current = now.TimeOfDay;
            if (start > end)
                return current >= start || current <= end; // 跨天
            else
                return current >= start && current <= end;
        }

        return false; // 无法识别的格式，不触发
    }

    /// <summary>批量折扣：数量 >= MinQuantity，且不超过 MaxQuantity</summary>
    private static bool IsBulkMatch(BizPriceRule rule, decimal quantity)
    {
        if (rule.MinQuantity.HasValue && quantity < rule.MinQuantity.Value)
            return false;
        if (rule.MaxQuantity.HasValue && quantity > rule.MaxQuantity.Value)
            return false;
        return true;
    }

    /// <summary>
    /// 临期折扣：查询产品批次，有批次在阈值时间内过期则触发。
    /// 阈值从 TimeWindow 解析（如 "EXPIRY_LESS_THAN_3_DAYS" → 72h），默认 24 小时。
    /// </summary>
    private async Task<bool> IsProductExpiringSoonAsync(InvProduct product, string? timeWindow, DateTime now)
    {
        if (product.ExpiryHours is not > 0) return false;

        var thresholdHours = ParseExpiryThresholdHours(timeWindow);
        var batches = await _batchRepo.GetByProductIdAsync(product.ProductID);
        var threshold = now.AddHours(thresholdHours);

        return batches.Any(b => b.ExpiryDate.HasValue && b.ExpiryDate.Value <= threshold);
    }

    /// <summary>从 TimeWindow 解析临期天数阈值，如 EXPIRY_LESS_THAN_3_DAYS → 72h，解析失败默认 24h</summary>
    private static int ParseExpiryThresholdHours(string? timeWindow)
    {
        if (string.IsNullOrWhiteSpace(timeWindow)) return 24;
        var match = System.Text.RegularExpressions.Regex.Match(
            timeWindow, @"EXPIRY[_ ]LESS[_ ]THAN[_ ](\d+)[_ ]DAYS", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var days) && days > 0)
            return days * 24;
        return 24;
    }

    // ==================== 价格计算 ====================

    private static decimal ComputeFinalPrice(BizPriceRule rule, decimal defaultPrice)
    {
        // ManualPrice：直接使用手动设置的价格
        if (NormalizeTriggerType(rule.TriggerType) == "ManualPrice" && rule.ManualPrice.HasValue)
            return rule.ManualPrice.Value;

        // 折扣率计算：DB 中 DiscountRate 存储的是「折后价格占比」（0.8 = 8折 = 原价×0.8）
        // 例如 DefaultPrice=100, DiscountRate=0.8 → FinalPrice=80
        if (rule.DiscountRate.HasValue)
        {
            var rate = Math.Clamp(rule.DiscountRate.Value, 0m, 1m);
            return Math.Round(defaultPrice * rate, 2);
        }

        // 无折扣率，返回原价
        return defaultPrice;
    }

    // ==================== 规则管理 CRUD ====================

    public async Task<ApiResponse<List<PriceRuleDto>>> GetRulesByProductAsync(string productId)
    {
        var rules = await _ruleRepo.GetByProductIdAsync(productId);
        var product = await _productRepo.GetByIdAsync(productId);

        var list = rules.Select(r => MapToDto(r, product)).ToList();
        return ApiResponse<List<PriceRuleDto>>.Success(list);
    }

    public async Task<ApiResponse<PagedResult<PriceRuleDto>>> GetAllRulesAsync(int pageIndex, int pageSize)
    {
        // 数据库级分页，避免全表加载
        var paged = await _ruleRepo.GetPagedAsync(pageIndex, pageSize);
        var total = await _ruleRepo.CountAsync();

        // 批量加载关联产品名
        var productIds = paged.Select(r => r.ProductID).Distinct().ToList();
        var productMap = new Dictionary<string, InvProduct?>();
        foreach (var pid in productIds)
            productMap[pid] = await _productRepo.GetByIdAsync(pid);

        return ApiResponse<PagedResult<PriceRuleDto>>.Success(new PagedResult<PriceRuleDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total,
            Items = paged.Select(r => MapToDto(r, productMap.GetValueOrDefault(r.ProductID))).ToList()
        });
    }

    public async Task<ApiResponse<PriceRuleDto>> CreateRuleAsync(SavePriceRuleDto dto)
    {
        // 校验商品存在
        var product = await _productRepo.GetByIdAsync(dto.ProductID);
        if (product == null)
            return ApiResponse<PriceRuleDto>.Fail("商品不存在", 404);

        // 校验 TriggerType 和对应字段
        if (dto.TriggerType == "ManualPrice" && dto.ManualPrice == null)
            return ApiResponse<PriceRuleDto>.Fail("手动调价类型必须填写 ManualPrice");
        if (dto.TriggerType != "ManualPrice" && dto.DiscountRate == null)
            return ApiResponse<PriceRuleDto>.Fail("非手动调价类型必须填写 DiscountRate");

        var rule = new BizPriceRule
        {
            ProductID = dto.ProductID,
            RuleName = dto.RuleName,
            TriggerType = dto.TriggerType,
            TimeWindow = dto.TimeWindow,
            DiscountRate = dto.DiscountRate,
            ManualPrice = dto.ManualPrice,
            MinQuantity = dto.MinQuantity,
            MaxQuantity = dto.MaxQuantity,
            Priority = dto.Priority,
            IsActive = dto.IsActive ? 1 : 0,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo
        };

        try
        {
            await _uow.BeginAsync();
            await _ruleRepo.AddAsync(rule);
            await _uow.CommitAsync();
            return ApiResponse<PriceRuleDto>.Success(MapToDto(rule, product), "规则创建成功");
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return ApiResponse<PriceRuleDto>.Fail($"规则创建失败：{ex.Message}");
        }
    }

    public async Task<ApiResponse<PriceRuleDto>> UpdateRuleAsync(string ruleId, SavePriceRuleDto dto)
    {
        var rule = await _ruleRepo.GetByIdAsync(ruleId);
        if (rule == null)
            return ApiResponse<PriceRuleDto>.Fail("规则不存在", 404);

        // 校验 TriggerType 和对应字段
        if (dto.TriggerType == "ManualPrice" && dto.ManualPrice == null)
            return ApiResponse<PriceRuleDto>.Fail("手动调价类型必须填写 ManualPrice");
        if (dto.TriggerType != "ManualPrice" && dto.DiscountRate == null)
            return ApiResponse<PriceRuleDto>.Fail("非手动调价类型必须填写 DiscountRate");

        var product = await _productRepo.GetByIdAsync(rule.ProductID);

        rule.RuleName = dto.RuleName;
        rule.TriggerType = dto.TriggerType;
        rule.TimeWindow = dto.TimeWindow;
        rule.DiscountRate = dto.DiscountRate;
        rule.ManualPrice = dto.ManualPrice;
        rule.MinQuantity = dto.MinQuantity;
        rule.MaxQuantity = dto.MaxQuantity;
        rule.Priority = dto.Priority;
        rule.IsActive = dto.IsActive ? 1 : 0;
        rule.EffectiveFrom = dto.EffectiveFrom;
        rule.EffectiveTo = dto.EffectiveTo;

        try
        {
            await _uow.BeginAsync();
            _ruleRepo.Update(rule);
            await _uow.CommitAsync();
            return ApiResponse<PriceRuleDto>.Success(MapToDto(rule, product), "规则更新成功");
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return ApiResponse<PriceRuleDto>.Fail($"规则更新失败：{ex.Message}");
        }
    }

    public async Task<ApiResponse<PriceRuleDto>> GetRuleByIdAsync(string ruleId)
    {
        var rule = await _ruleRepo.GetByIdAsync(ruleId);
        if (rule == null)
            return ApiResponse<PriceRuleDto>.Fail("规则不存在", 404);

        var product = await _productRepo.GetByIdAsync(rule.ProductID);
        return ApiResponse<PriceRuleDto>.Success(MapToDto(rule, product));
    }

    public async Task<ApiResponse> DeleteRuleAsync(string ruleId)
    {
        var rule = await _ruleRepo.GetByIdAsync(ruleId);
        if (rule == null)
            return ApiResponse.Fail("规则不存在", 404);

        try
        {
            await _uow.BeginAsync();
            _ruleRepo.Delete(rule);
            await _uow.CommitAsync();
            return ApiResponse.Success("规则已删除");
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return ApiResponse.Fail($"规则删除失败：{ex.Message}");
        }
    }

    // ==================== 映射 ====================

    private static PriceRuleDto MapToDto(BizPriceRule r, InvProduct? product) => new()
    {
        RuleID = r.RuleID,
        ProductID = r.ProductID,
        ProductName = product?.ProductName,
        RuleName = r.RuleName,
        TriggerType = NormalizeTriggerType(r.TriggerType),
        TimeWindow = r.TimeWindow,
        DiscountRate = r.DiscountRate,
        ManualPrice = r.ManualPrice,
        MinQuantity = r.MinQuantity,
        MaxQuantity = r.MaxQuantity,
        Priority = r.Priority,
        IsActive = r.IsActive == 1,
        EffectiveFrom = r.EffectiveFrom,
        EffectiveTo = r.EffectiveTo
    };
}
