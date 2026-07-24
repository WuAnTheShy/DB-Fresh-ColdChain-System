using Microsoft.EntityFrameworkCore;
using FreshGroupSystem.Data;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Order;

public class OrderRepository : BaseRepository<Models.Order>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context) { }

    public async Task<Models.Order?> GetOrderDetailAsync(int orderId)
        => await _context.Orders
            .Include(o => o.GroupLeader)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId);

    public async Task<List<Models.Order>> GetOrdersByLeaderIdAsync(int leaderId)
        => await _context.Orders
            .Where(o => o.GroupLeaderId == leaderId)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.CreateTime)
            .ToListAsync();

    public async Task<List<Models.Order>> GetOrdersByStatusAsync(int status)
        => await _context.Orders
            .Where(o => o.Status == status)
            .Include(o => o.GroupLeader)
            .OrderByDescending(o => o.CreateTime)
            .ToListAsync();
}
