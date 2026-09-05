using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Interfaces;

/// <summary>
/// A组动态定价引擎：根据商品、数量、当前时间计算实时售价
/// 支持时段折扣、临期折扣、批量折扣、手动调价四种触发类型
/// </summary>
public interface IPricingService
{
    /// <summary>计算商品实时价格</summary>
    Task<ApiResponse<PriceCalculationResult>> CalculatePriceAsync(PriceCalculationRequest request);

    /// <summary>按产品 ID 查询该商品的价格规则（供应商可选，null 表示全部）</summary>
    Task<ApiResponse<List<PriceRuleDto>>> GetRulesByProductAsync(string productId, string? supplierId = null);

    /// <summary>获取价格规则（管理列表，供应商可选，null 表示全部）</summary>
    Task<ApiResponse<PagedResult<PriceRuleDto>>> GetAllRulesAsync(int pageIndex, int pageSize, string? supplierId = null);

    /// <summary>创建价格规则</summary>
    Task<ApiResponse<PriceRuleDto>> CreateRuleAsync(SavePriceRuleDto dto);

    /// <summary>更新价格规则（supplierId 非空时校验归属）</summary>
    Task<ApiResponse<PriceRuleDto>> UpdateRuleAsync(string ruleId, SavePriceRuleDto dto, string? supplierId = null);

    /// <summary>按 ID 获取单条规则（supplierId 非空时校验归属）</summary>
    Task<ApiResponse<PriceRuleDto>> GetRuleByIdAsync(string ruleId, string? supplierId = null);

    /// <summary>删除价格规则（supplierId 非空时校验归属）</summary>
    Task<ApiResponse> DeleteRuleAsync(string ruleId, string? supplierId = null);
}
