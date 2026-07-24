using Microsoft.EntityFrameworkCore;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<GroupLeader> GroupLeaders => Set<GroupLeader>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 订单明细：唯一约束，防止同一订单同一产品重复
        modelBuilder.Entity<OrderItem>()
            .HasIndex(oi => new { oi.OrderId, oi.ProductId })
            .IsUnique();

        // 库存：产品和库存一对一关系
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Inventory)
            .WithOne(i => i.Product)
            .HasForeignKey<Inventory>(i => i.ProductId);

        // 软删除 / 查���过滤器（只查上架产品）
        // modelBuilder.Entity<Product>().HasQueryFilter(p => p.Status == 1);
    }
}
