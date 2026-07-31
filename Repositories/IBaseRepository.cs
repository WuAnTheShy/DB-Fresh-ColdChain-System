namespace FreshGroupSystem.Repositories;

public interface IBaseRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetByIdAsync(string id);
    Task<List<T>> GetAllAsync();
    Task<List<T>> GetPagedAsync(int pageIndex, int pageSize);
    Task<int> CountAsync();
    Task<bool> AnyAsync();
    Task<T> AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
    Task SaveChangesAsync();
}
