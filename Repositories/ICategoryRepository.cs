using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public interface ICategoryRepository : IBaseRepository<InvCategory>
{
    Task<List<InvCategory>> GetSubCategoriesAsync(string? parentId);
}
