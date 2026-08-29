# 阶段 4 总结：订单查询与状态流转

完成日期：2026-07-27

## 阶段目标

补齐订单列表、详情、供应商拆单展示、合法状态机和取消订单，并以事务型接口冻结 A 组
运费/物流与 C 组佣金边界，保证 B 组不直接操作其他组数据表。

## 已完成内容

1. 订单查询：
   - 支持按订单号/消费者姓名、消费者 ID 和状态筛选；
   - 支持分页、总数和页码归一化；
   - SQL 参数化，不拼接用户输入。
2. 订单详情：
   - 展示消费者、订单时间、金额、积分和下单时收货信息快照；
   - 按 `SupplierId` 聚合商品，展示供应商小计；
   - 通过 A 组只读契约展示每个供应商的履约状态和运单号。
3. 地址快照：
   - `Biz_Orders` 新增收件人、电话和完整地址快照；
   - 下单在消费者行锁内读取地址并保存，后续地址修改不影响历史订单。
4. 合法状态机：
   - 新增 `OrderStatus` 和 `OrderStateMachine`；
   - 本阶段开放已支付→已发货、已发货→已完成、已支付→已取消；
   - 非法跳转在调用跨组服务前被拒绝；
   - 状态更新要求数据库旧状态匹配，防止并发覆盖。
5. 发货与佣金：
   - 发货前调用 A 组 `CreateShipmentAsync`；
   - 完成前调用 C 组 `RegisterCompletedOrderAsync`；
   - 跨组写入和 B 组状态更新同事务提交或回滚。
6. 取消订单：
   - 仅允许取消已支付未发货订单；
   - 调用 A 组释放库存；
   - 归还该订单已核销用户券；
   - 扣回订单奖励积分并写 `ORDER_CANCEL` 流水；
   - 冲减累计消费并重新计算会员等级；
   - 任一步失败时订单与全部资产保持原状。
7. 运费契约：
   - 下单通过 A 组接口计算冷链运费，不再在 B 组硬编码赋值；
   - 当前 Dummy 返回 0，不代表 A 组算法完成。
8. 页面：
   - 新增订单管理列表和筛选空态；
   - 完成订单详情、状态操作和供应商拆单页面；
   - 首页与主导航增加订单管理入口。

## 自动化验证

测试总计 23 条，全部通过：

- 下单事务场景 4 条；
- 客户营销场景 10 条；
- 阶段 4 订单生命周期场景 9 条：
  1. 状态和关键词分页查询；
  2. 详情按供应商分组并展示履约状态；
  3. 下单调用运费契约并保存地址快照；
  4. 已支付订单发货并创建物流；
  5. 已发货订单完成并登记佣金；
  6. 非法状态跳转回滚；
  7. 佣金登记失败时完成回滚；
  8. 取消订单归还库存与营销资产；
  9. 库存释放失败时取消整体回滚。

执行命令：

```powershell
dotnet build FreshColdChain.csproj --no-restore
dotnet build tests\FreshColdChain.Tests\FreshColdChain.Tests.csproj --no-restore
dotnet run --project tests\FreshColdChain.Tests\FreshColdChain.Tests.csproj --no-build --no-restore
dotnet format FreshColdChain.csproj --verify-no-changes --no-restore
dotnet format tests\FreshColdChain.Tests\FreshColdChain.Tests.csproj --verify-no-changes --no-restore
git diff --check
```

结果：两个项目均为 0 个警告、0 个错误；23/23 场景通过；格式和空白检查通过。

## 浏览器 QA

- Browser 插件可用，未使用外部浏览器回退。
- 页面：`/Order?CustomerId=0`。
- 1440×900 桌面视口：
  - 页面标题、筛选、提示、空态、入口和页脚完整；
  - 无水平溢出。
- 筛选交互：
  - 消费者 ID 修改为 `-1`、状态改为“已支付”并提交；
  - URL、输入值和状态选择保持；
  - 页面显示受控中文查询条件错误。
- 390×844 移动视口：
  - 筛选项自动纵向排列；
  - 文档滚动宽度 375，小于视口宽度 390，无水平溢出；
  - 折叠导航可展开。
- 页面无框架错误覆盖层，浏览器控制台无 warning/error。
- 桌面和移动截图保存在 Codex 可视化目录，未写入仓库。

## 涉及的主要文件

- `Models/OrderStatus.cs`
- `Models/OrderQueryModels.cs`
- `Models/OrderDetailViewModel.cs`
- `Models/CrossGroupOrderContracts.cs`
- `Models/BizOrder.cs`
- `Interfaces/IInventoryService.cs`
- `Interfaces/ILogisticsService.cs`
- `Interfaces/ICommissionService.cs`
- `Interfaces/IOrderService.cs`
- `Services/OrderStateMachine.cs`
- `Services/OrderService.cs`
- `Services/DummyInventoryService.cs`
- `Services/DummyLogisticsService.cs`
- `Services/DummyCommissionService.cs`
- `Repositories/OrderRepository.cs`
- `Repositories/CustomerRepository.cs`
- `Repositories/CouponRepository.cs`
- `Controllers/OrderController.cs`
- `Views/Order/Index.cshtml`
- `Views/Order/Detail.cshtml`
- `groupB_ddl.sql`
- `tests/FreshColdChain.Tests/OrderLifecycleScenarioTests.cs`
- `doc/GroupB-cross-group-contracts.md`

## 遗留事项

- A 组现有远端物流 Service 使用另一套 EF 订单表和状态码，尚未提供本阶段契约的适配器。
- C 组尚未提供佣金登记与撤销接口，当前为无数据库写入 Dummy。
- 系统尚未接入 C 组真实支付记录；本阶段取消补偿 B/A 组资产，支付退款在阶段 5 完成。
- 仓库没有隔离 Oracle 测试库说明，真实 DDL、分页 SQL、行锁和跨组事务尚未集成验证。
- 退款积分扣回尚未按订单幂等，退款状态和佣金撤销留待阶段 5。

## 下一阶段

阶段 5：完成退款协同和幂等、C 组佣金撤销、DDL/种子数据与迁移说明、配置安全化、
Oracle 集成验证、部署说明和最终报告，并逐项关闭 GroupB 需求矩阵。
