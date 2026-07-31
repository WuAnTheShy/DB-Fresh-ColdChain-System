namespace FreshGroupSystem.Repositories;

/// <summary>
/// 通用仓储接口（Dapper 版本）
/// 移除了 Expression 谓词方法（无法转为 SQL），改用具体 Repository 的方法
/// </summary>
public interface IBaseRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task<List<T>> GetPagedAsync(int pageIndex, int pageSize);
    Task<int> CountAsync();
    Task<bool> AnyAsync();
    Task<T> AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
    Task SaveChangesAsync();  // Dapper 下为 no-op，保持调用方兼容
}
