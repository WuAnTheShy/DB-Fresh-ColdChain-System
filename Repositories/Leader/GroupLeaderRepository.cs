using Microsoft.EntityFrameworkCore;
using FreshGroupSystem.Data;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Leader;

public class GroupLeaderRepository : BaseRepository<Models.GroupLeader>, IGroupLeaderRepository
{
    public GroupLeaderRepository(AppDbContext context) : base(context) { }

    public async Task<Models.GroupLeader?> GetLeaderWithOrdersAsync(int leaderId)
        => await _context.GroupLeaders
            .Include(g => g.Orders)
                .ThenInclude(o => o.OrderItems)
            .FirstOrDefaultAsync(g => g.Id == leaderId);
}
