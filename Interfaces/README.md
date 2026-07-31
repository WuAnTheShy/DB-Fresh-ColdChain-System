# 跨组交互接口规范

## 接口目录

| 接口 | 所属模块 | 路径 |
|------|---------|------|
| `IProductInventoryService` | 产品库存（A组） | `Interfaces/IProductInventoryService.cs` |
| `ISupplierService` | 供应商（A组） | `Interfaces/ISupplierService.cs` |
| `IOrderService` | 订单（B组） | `Interfaces/IOrderService.cs` |
| `IGroupLeaderService` | 团长（C组） | `Interfaces/IGroupLeaderService.cs` |
| `ILogisticsService` | 物流（C组） | `Interfaces/ILogisticsService.cs` |

## 铁律

1. **任何小组绝不允许直接写 SQL 操作其他小组负责的表**
2. 跨表操作必须通过对方提供的 Service 接口调用
3. 事务由业务发起方控制（调用方 Begin/Commit/Rollback）

## Mock 隔离测试

如果依赖的小组还没开发完，需要创建 Dummy 实现：

```csharp
// 示例: MockProductInventoryService
public class MockProductInventoryService : IProductInventoryService
{
    public Task<ApiResponse<InventoryDto>> GetInventoryAsync(int productId)
        => Task.FromResult(ApiResponse<InventoryDto>.Success(new InventoryDto
        {
            ProductId = productId,
            ProductName = "Mock产品",
            StockQuantity = 999,
            LockedQuantity = 0,
            AvailableQuantity = 999
        }));
    // ... 其余方法返回成功假数据
}
```

## 每组负责的表

| 组 | 表 | 
|----|-----|
| A组 | `SUPPLIER`, `PRODUCT`, `INVENTORY` |
| B组 | `ORDER`, `ORDER_ITEM` |
| C组 | `GROUP_LEADER` + 物流业务 |
