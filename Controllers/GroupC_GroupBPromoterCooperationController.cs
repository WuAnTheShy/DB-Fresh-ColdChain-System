using System.Security.Cryptography;
using System.Text;
using DBFreshColdChain.Models.DTOs;
using Dapper;
using FreshColdChain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DBFreshColdChain.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/internal/group-b/promoter-cooperation")]
public class GroupC_GroupBPromoterCooperationController : ControllerBase
{
    private const int MaxBatchSize = 500;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public GroupC_GroupBPromoterCooperationController(
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    [HttpGet("{promoterId}/internal-suppliers")]
    [ProducesResponseType(typeof(GroupC_PromoterCooperationRangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GroupC_PromoterCooperationRangeResponse>> GetCooperationRange(
        string promoterId,
        CancellationToken cancellationToken)
    {
        if (!IsGroupBCaller()) return StatusCode(StatusCodes.Status403Forbidden);
        if (string.IsNullOrWhiteSpace(promoterId)) return BadRequest("PromoterId is required.");

        var supplierIds = await GetActiveInternalSupplierIdsAsync(promoterId.Trim(), cancellationToken);
        return Ok(new GroupC_PromoterCooperationRangeResponse
        {
            PromoterId = promoterId.Trim(),
            InternalSupplierIds = supplierIds
        });
    }

    [HttpPost("verify-products")]
    [ProducesResponseType(typeof(List<GroupC_PromoterProductCooperationVerificationResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<GroupC_PromoterProductCooperationVerificationResult>>> VerifyProductCooperation(
        [FromBody] GroupC_PromoterProductCooperationVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsGroupBCaller()) return StatusCode(StatusCodes.Status403Forbidden);
        if (string.IsNullOrWhiteSpace(request.PromoterId)) return BadRequest("PromoterId is required.");
        var products = request.Products ?? new List<GroupC_PromoterProductSupplierItem>();
        if (products.Count > MaxBatchSize)
            return BadRequest($"Products cannot contain more than {MaxBatchSize} items.");

        var supplierIds = await GetActiveInternalSupplierIdsAsync(request.PromoterId.Trim(), cancellationToken);
        var allowedSupplierIds = new HashSet<string>(supplierIds, StringComparer.Ordinal);
        var results = products.Select(product => new GroupC_PromoterProductCooperationVerificationResult
        {
            ProductId = product.ProductId,
            InternalSupplierId = product.InternalSupplierId,
            IsAllowed = !string.IsNullOrWhiteSpace(product.ProductId)
                && !string.IsNullOrWhiteSpace(product.InternalSupplierId)
                && allowedSupplierIds.Contains(product.InternalSupplierId)
        }).ToList();

        return Ok(results);
    }

    private async Task<List<string>> GetActiveInternalSupplierIdsAsync(
        string promoterId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT INTERNALSUPPLIERID
            FROM CRM_PROMOTER_SUPPLIER_COOPERATIONS
            WHERE PROMOTERID = :PromoterId
              AND UPPER(STATUS) = 'ACTIVE'
              AND STARTTIME <= SYSDATE
              AND (ENDTIME IS NULL OR ENDTIME >= SYSDATE)";

        var supplierIds = await _unitOfWork.Connection.QueryAsync<string>(
            new CommandDefinition(sql, new { PromoterId = promoterId }, _unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        return supplierIds.ToList();
    }

    private bool IsGroupBCaller()
    {
        var expectedApiKey = _configuration["CrossGroup:GroupBApiKey"];
        if (string.IsNullOrWhiteSpace(expectedApiKey)
            || !Request.Headers.TryGetValue("X-GroupB-Api-Key", out var suppliedApiKey))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedApiKey),
            Encoding.UTF8.GetBytes(suppliedApiKey.ToString()));
    }
}
