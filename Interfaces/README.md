# A 组 — 供应商 + 产品 + 库存

## 接口目录

| 接口 | 用途 |
|------|------|
| `IProductInventoryService` | 产品和库存管理（供 B 组下单时调用） |
| `ISupplierService` | 供应商管理 |

## 负责的表

| 表 | 说明 |
|----|------|
| `SUPPLIER` | 供应商 |
| `PRODUCT` | 产品 |
| `INVENTORY` | 库存 |

## 跨组接口说明

B 组（订单）创建订单时需要查询产品信息和锁定库存，请调用 `IProductInventoryService` 接口：

- `GetProductByIdAsync(int id)` — 查询产品是否存在、是否上架
- `GetInventoryAsync(int productId)` — 查询可用库存
- `StockInAsync` / `StockOutAsync` — 入库/出库

事务控制由 B 组负责（调用方 BeginAsync/CommitAsync/RollbackAsync）。
