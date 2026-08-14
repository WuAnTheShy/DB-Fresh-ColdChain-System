using DBFreshColdChain.Models.DTOs;
using Dapper;
using FreshColdChain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DBFreshColdChain.Controllers;

[ApiController]
[Route("api/group-c/promoters")]
public class GroupC_PromotersApiController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public GroupC_PromotersApiController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("available")]
    [ProducesResponseType(typeof(GroupC_PagedResult<GroupC_AvailablePromoterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<GroupC_PagedResult<GroupC_AvailablePromoterDto>>> GetAvailablePromoters(
        [FromQuery] GroupC_AvailablePromoterQuery query,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
            Skip = (pageIndex - 1) * pageSize,
            Take = pageSize
        };
        const string whereClause = @"
            FROM CRM_PROMOTERS
            WHERE UPPER(STATUS) IN ('ENABLE', 'ENABLED', 'ACTIVE')
              AND INSTR(PROMOTERID || CHR(1) || PROMOTERNAME, NVL(:Keyword, CHR(1))) > 0";

        var totalCount = await _unitOfWork.Connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) " + whereClause, parameters, _unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        var items = await _unitOfWork.Connection.QueryAsync<GroupC_AvailablePromoterDto>(
            new CommandDefinition(@"
                SELECT PROMOTERID AS PromoterId, PROMOTERNAME AS PromoterName
                " + whereClause + @"
                ORDER BY PROMOTERID
                OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY", parameters, _unitOfWork.Transaction,
                cancellationToken: cancellationToken));

        return Ok(new GroupC_PagedResult<GroupC_AvailablePromoterDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items.AsList()
        });
    }

    [HttpGet("{promoterId}")]
    [ProducesResponseType(typeof(GroupC_PromoterBasicInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupC_PromoterBasicInfoDto>> GetPromoterBasicInfo(
        string promoterId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promoterId))
            return NotFound(new { message = "Promoter not found" });

        const string sql = @"
            SELECT PROMOTERID AS PromoterId, PROMOTERNAME AS PromoterName, STATUS AS Status
            FROM CRM_PROMOTERS
            WHERE PROMOTERID = :PromoterId";
        var result = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<GroupC_PromoterBasicInfoDto>(
            new CommandDefinition(sql, new { PromoterId = promoterId.Trim() }, _unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        return result is null ? NotFound(new { message = "Promoter not found" }) : Ok(result);
    }
}
