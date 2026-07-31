# A 组 — 供应商 + 商品 + 库存

## 负责的表（对齐数据库设计文档）

| 表名 | 说明 | 主键 |
|------|------|------|
| `Inv_Suppliers` | 供应商（含资质、信用等级） | `SupplierID` VARCHAR2(36) |
| `Inv_Category` | 商品分类 | `CategoryID` VARCHAR2(36) |
| `Inv_Products` | 商品（含重量、体积、保质期、温区） | `ProductID` VARCHAR2(36) |
| `Inv_StockSummary` | 库存汇总（总/锁定/可用） | `StockID` VARCHAR2(36) |
| `Inv_StockBatches` | 库存批次（FEFO 先进先出） | `BatchID` VARCHAR2(36) |
| `Biz_PriceRules` | 动态定价规则 | `RuleID` VARCHAR2(36) |

## 接口

| 接口 | 用途 |
|------|------|
| `IProductInventoryService` | 产品、分类、库存、批次管理（供 B/C 组调用） |
| `ISupplierService` | 供应商管理（供 C 组调用） |

### C 组可调用的跨组方法

| 方法 | 所属接口 | 说明 |
|------|---------|------|
| `GetProductStockAsync(productId)` | `IProductInventoryService` | 查商品库存总量，返回 `int` |
| `FindSupplierAccountAsync(id,name,account,phone)` | `ISupplierService` | 按条件查供应商账户（不含密码） |
| `VerifySupplierPasswordAsync(account, pwd)` | `ISupplierService` | 验证供应商登录密码 |
| `DeleteSupplierAsync(id)` | `ISupplierService` | 删除供应商 |

## 关键设计

- **所有主键 VARCHAR2(36)**，应用层生成 GUID
- **FEFO 出库**：`StockOutAsync` 按过期时间从早到晚扣减批次
- **库存汇总 + 批次分离**：`Inv_StockSummary` 存聚合数据，`Inv_StockBatches` 存批次明细
