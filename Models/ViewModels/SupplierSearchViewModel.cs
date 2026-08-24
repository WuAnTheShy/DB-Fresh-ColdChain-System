using FreshColdChain.Models.DTOs; // 引用 A 组 DTO

namespace FreshColdChain.Models.ViewModels
{
    public class SupplierSearchViewModel
    {
        public string Keyword { get; set; } = string.Empty;

        // 搜索结果列表（A 组返回的 SupplierAccountDto）
        public List<SupplierAccountDto> Suppliers { get; set; } = new();

        // 当前团长已经绑定的供应商 ID 集合
        public List<string> BoundSupplierIds { get; set; } = new();
    }
}