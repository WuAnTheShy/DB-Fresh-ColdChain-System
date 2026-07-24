using Microsoft.EntityFrameworkCore;
using FreshGroupSystem.Data;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Supplier;

public class SupplierRepository : BaseRepository<Models.Supplier>, ISupplierRepository
{
    public SupplierRepository(AppDbContext context) : base(context) { }

    public async Task<List<Product>> GetProductsBySupplierIdAsync(int supplierId)
        => await _context.Products
            .Where(p => p.SupplierId == supplierId)
            .Include(p => p.Inventory)
            .ToListAsync();
}
