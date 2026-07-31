using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Interfaces;

/// <summary>
/// 团长管理服务接口
/// </summary>
public interface IGroupLeaderService
{
    Task<ApiResponse<PagedResult<GroupLeaderDto>>> GetLeadersAsync(int pageIndex, int pageSize);
    Task<ApiResponse<GroupLeaderDto>> GetLeaderByIdAsync(int id);
    Task<ApiResponse<GroupLeaderDto>> CreateLeaderAsync(CreateGroupLeaderDto dto);
    Task<ApiResponse<GroupLeaderDto>> UpdateLeaderAsync(int id, CreateGroupLeaderDto dto);
    Task<ApiResponse> DeleteLeaderAsync(int id);
    Task<ApiResponse> EnableLeaderAsync(int id);
    Task<ApiResponse> DisableLeaderAsync(int id);
}
