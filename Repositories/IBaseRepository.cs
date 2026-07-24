using System.Linq.Expressions;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// 通用仓储接口：封装所有表的公共 CRUD 操作
/// </summary>
public interface IBaseRepository<T> where T : class
{
    // ========== 查 ==========
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
    Task<List<T>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<T, bool>>? predicate = null);

    // ========== 增 ==========
    Task<T> AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);

    // ========== 改 ==========
    void Update(T entity);
    void UpdateRange(IEnumerable<T> entities);

    // ========== 删 ==========
    void Delete(T entity);
    void DeleteRange(IEnumerable<T> entities);

    // ========== 保存 ==========
    Task<int> SaveChangesAsync();
}
