using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Common;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupLeadersController : ControllerBase
{
    private readonly IGroupLeaderService _service;

    public GroupLeadersController(IGroupLeaderService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页获取团长列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<GroupLeaderDto>>> GetList(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
        => await _service.GetLeadersAsync(pageIndex, pageSize);

    /// <summary>
    /// 获取团长详情（含订单）
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<GroupLeaderDto>> GetDetail(int id)
        => await _service.GetLeaderByIdAsync(id);

    /// <summary>
    /// 创建团长
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<GroupLeaderDto>> Create([FromBody] CreateGroupLeaderDto dto)
        => await _service.CreateLeaderAsync(dto);

    /// <summary>
    /// 更新团长信息
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResponse<GroupLeaderDto>> Update(int id, [FromBody] CreateGroupLeaderDto dto)
        => await _service.UpdateLeaderAsync(id, dto);

    /// <summary>
    /// 删除团长
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse> Delete(int id)
        => await _service.DeleteLeaderAsync(id);

    /// <summary>
    /// 启用团长
    /// </summary>
    [HttpPost("{id}/enable")]
    public async Task<ApiResponse> Enable(int id)
        => await _service.EnableLeaderAsync(id);

    /// <summary>
    /// 停用团长
    /// </summary>
    [HttpPost("{id}/disable")]
    public async Task<ApiResponse> Disable(int id)
        => await _service.DisableLeaderAsync(id);
}
