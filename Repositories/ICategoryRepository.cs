//操作InvCategory表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface ICategoryRepository : IBaseRepository<InvCategory>
{
    Task<List<InvCategory>> GetSubCategoriesAsync(string? parentId);//按父分类 ID 查询下面的子分类列表
}
