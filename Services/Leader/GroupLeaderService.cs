using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;
using FreshGroupSystem.Repositories.Leader;

namespace FreshGroupSystem.Services.Leader;

public class GroupLeaderService : IGroupLeaderService
{
    private readonly IGroupLeaderRepository _leaderRepo;

    public GroupLeaderService(IGroupLeaderRepository leaderRepo)
    {
        _leaderRepo = leaderRepo;
    }

    public async Task<ApiResponse<PagedResult<GroupLeaderDto>>> GetLeadersAsync(int pageIndex, int pageSize)
    {
        var (items, total) = await _leaderRepo.GetPagedWithOrderCountAsync(pageIndex, pageSize);

        return ApiResponse<PagedResult<GroupLeaderDto>>.Success(new PagedResult<GroupLeaderDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(MapToDto).ToList()
        });
    }

    public async Task<ApiResponse<GroupLeaderDto>> GetLeaderByIdAsync(int id)
    {
        var leader = await _leaderRepo.GetLeaderWithOrdersAsync(id);
        if (leader == null)
            return ApiResponse<GroupLeaderDto>.Fail("团长不存在", 404);

        return ApiResponse<GroupLeaderDto>.Success(MapToDto(leader));
    }

    public async Task<ApiResponse<GroupLeaderDto>> CreateLeaderAsync(CreateGroupLeaderDto dto)
    {
        var leader = new Models.GroupLeader
        {
            Name = dto.Name,
            Phone = dto.Phone,
            CommunityName = dto.CommunityName,
            Address = dto.Address,
            Status = 1
        };

        await _leaderRepo.AddAsync(leader);
        await _leaderRepo.SaveChangesAsync();

        return ApiResponse<GroupLeaderDto>.Success(MapToDto(leader), "团长创建成功");
    }

    public async Task<ApiResponse<GroupLeaderDto>> UpdateLeaderAsync(int id, CreateGroupLeaderDto dto)
    {
        var leader = await _leaderRepo.GetByIdAsync(id);
        if (leader == null)
            return ApiResponse<GroupLeaderDto>.Fail("团长不存在", 404);

        leader.Name = dto.Name;
        leader.Phone = dto.Phone;
        leader.CommunityName = dto.CommunityName;
        leader.Address = dto.Address;

        _leaderRepo.Update(leader);
        await _leaderRepo.SaveChangesAsync();

        return ApiResponse<GroupLeaderDto>.Success(MapToDto(leader), "团长信息更新成功");
    }

    public async Task<ApiResponse> DeleteLeaderAsync(int id)
    {
        var leader = await _leaderRepo.GetByIdAsync(id);
        if (leader == null)
            return ApiResponse.Fail("团长不存在", 404);

        _leaderRepo.Delete(leader);
        await _leaderRepo.SaveChangesAsync();

        return ApiResponse.Success("团长已删除");
    }

    public async Task<ApiResponse> EnableLeaderAsync(int id)
        => await SetLeaderStatus(id, 1, "团长已启用");

    public async Task<ApiResponse> DisableLeaderAsync(int id)
        => await SetLeaderStatus(id, 0, "团长已停用");

    // ========== 私有方法 ==========

    private async Task<ApiResponse> SetLeaderStatus(int id, int status, string message)
    {
        var leader = await _leaderRepo.GetByIdAsync(id);
        if (leader == null)
            return ApiResponse.Fail("团长不存在", 404);

        leader.Status = status;
        _leaderRepo.Update(leader);
        await _leaderRepo.SaveChangesAsync();

        return ApiResponse.Success(message);
    }

    private static GroupLeaderDto MapToDto(Models.GroupLeader leader)
        => new()
        {
            Id = leader.Id,
            Name = leader.Name,
            Phone = leader.Phone,
            CommunityName = leader.CommunityName,
            Address = leader.Address,
            Status = leader.Status,
            OrderCount = leader.Orders?.Count ?? leader.OrderCount
        };
}
