# 阶段 2 总结：真实下单与营销资产闭环

完成日期：2026-07-27

## 阶段目标

将硬编码下单骨架升级为服务端可信的真实下单流程，并在不直接访问 A 组数据表的前提下，
完成库存契约、优惠券、积分和供应商拆单的事务闭环。

## 已完成内容

1. 新增创建订单请求和结果模型：
   - 客户端只提交消费者、地址、优惠券记录、商品 ID 和数量；
   - 商品名、价格、供应商、金额、积分、状态和订单号全部由服务端生成。
2. 新增 `IInventoryService` 事务型跨组契约和 `DummyInventoryService`。
3. 新增 Oracle 事务执行器，并为 Repository 建立可替换接口，支持隔离测试。
4. 消费者行在下单时使用 `FOR UPDATE` 锁定，地址必须属于当前消费者。
5. 重复商品在服务端合并，库存服务返回的可信快照生成订单明细。
6. 优惠券在事务内完成归属、状态、模板状态、有效期和金额门槛校验：
   - 查询时锁定用户券和券模板；
   - 核销时再次要求 `Status = 0`；
   - 并发重复用券会失败并回滚。
7. 按当前会员等级的 `PointsMultiplier` 计算积分，并同步写入积分余额和流水。
8. 按供应商对订单明细分组，在下单成功结果中展示拆单小计。
9. 下单页面支持动态增删商品、中文校验、成功金额和拆单结果展示。
10. 修复 `MapStaticAssets` 在压缩请求下返回空 CSS/JS 的问题，改用 `UseStaticFiles`。
11. 新增不依赖第三方测试框架的事务场景测试。

## 自动化验证

场景测试共 4 条，全部通过：

1. 正常下单：订单和明细提交，优惠券核销，双倍积分到账。
2. 库存不足：事务回滚，不创建订单或营销资产。
3. 优惠券无效：事务回滚，不创建订单或营销资产。
4. 积分流水写入失败：已暂存的订单、明细、用券和积分变化全部回滚。

命令：

```powershell
dotnet build FreshColdChain.csproj --nologo --no-restore
dotnet build tests\FreshColdChain.Tests\FreshColdChain.Tests.csproj --nologo --no-restore
dotnet run --project tests\FreshColdChain.Tests\FreshColdChain.Tests.csproj --no-build --no-restore
```

结果：两个项目均为 0 个警告、0 个错误，4/4 场景通过。

## 浏览器 QA

- 页面：`/Order/Create`
- 桌面视口：通过。
- 移动视口：390×844，通过，无水平溢出。
- 动态添加商品：行数从 1 变为 2，字段名称正确编号。
- 动态删除商品：行数恢复为 1。
- 无效提交：消费者、地址、商品均显示中文范围提示。
- Bootstrap 规则加载：1298 条，提交按钮、卡片和容器样式生效。
- 浏览器控制台：无相关警告或错误。
- 静态资源兼容：带 `Accept-Encoding: gzip, deflate, br` 请求仍返回完整 CSS。

## 涉及的主要文件

- `Models/CreateOrderRequest.cs`
- `Models/CreateOrderResult.cs`
- `Models/InventoryReservation.cs`
- `Interfaces/IInventoryService.cs`
- `Interfaces/IOrderTransactionManager.cs`
- `Services/OrderService.cs`
- `Services/OracleOrderTransactionManager.cs`
- `Services/DummyInventoryService.cs`
- `Repositories/IOrderRepository.cs`
- `Repositories/ICustomerRepository.cs`
- `Repositories/ICouponRepository.cs`
- `Repositories/IPointRepository.cs`
- `Controllers/OrderController.cs`
- `Views/Order/Create.cshtml`
- `Program.cs`
- `tests/FreshColdChain.Tests/`
- `doc/B组-订单与消费者/GroupB-cross-group-contracts.md`

## 遗留事项

- A 组尚未实现 `IInventoryService`，当前 Dummy 不会持久化库存。
- 运费暂为 0，需在阶段 4 接入 A 组冷链运费接口。
- 会员等级尚未在累计消费变化后自动写回。
- 领券防超发和优惠券管理页面尚未完成。
- 退款积分扣回尚缺少按订单幂等控制。
- 尚未在隔离 Oracle 测试库执行真实 SQL 集成测试。

## 下一阶段

阶段 3：完成消费者资料、地址 CRUD、默认地址约束、会员自动升级、领券与可用券查询，
并提供统一的 Bootstrap 管理页面。
