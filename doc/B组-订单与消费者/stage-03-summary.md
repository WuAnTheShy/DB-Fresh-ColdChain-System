# 阶段 3 总结：客户与营销管理

完成日期：2026-07-27

## 阶段目标

补齐消费者、收货地址、会员等级和优惠券领取的三层闭环，使 GroupB 负责的客户与营销表
具备可演示的 Service、Controller 和 Bootstrap View，并保持营销资产的事务一致性。

## 已完成内容

1. 消费者管理：
   - 支持新增、按 ID 查看和编辑基础资料；
   - 手机号码建立唯一约束；
   - 使用 ASP.NET Core `PasswordHasher<CrmCustomer>` 保存密码哈希；
   - 页面请求不包含积分、累计消费、会员等级或密码哈希，避免越权修改。
2. 收货地址管理：
   - 支持新增、列表、编辑、删除和设置默认地址；
   - 第一条地址自动设为默认；
   - 删除或取消当前默认地址时自动顺延其他地址；
   - 所有写操作先锁定消费者行，串行化同一消费者的地址变更。
3. 默认地址数据库约束：
   - `IsDefault` 增加 0/1 检查约束；
   - Oracle 函数式唯一索引只索引 `IsDefault = 1` 的 `CustomerId`；
   - Service 不变量和数据库唯一索引共同保证最多一个默认地址。
4. 会员自动定级：
   - 下单仍按交易开始前的当前会员等级计算积分；
   - 累计消费增加后查询最高符合 `MinSpent` 的等级；
   - `TotalSpent` 和 `MemberLevelId` 与订单、积分、用券在同一事务提交或回滚。
5. 优惠券领取与查询：
   - 优惠券中心展示进行中的券模板、剩余数量和消费者领取状态；
   - 展示未使用、模板启用且仍在有效期内的用户券记录 ID；
   - 领券按消费者行、券模板行的固定顺序加锁；
   - 检查活动状态、时间窗口、库存和重复领取；
   - 条件扣减模板库存并在同一事务写入用户券记录。
6. 防薅羊毛约束：
   - `Mkt_CouponRecords(CouponId, CustomerId)` 增加唯一约束；
   - 每位消费者对同一券模板限领一张；
   - 重复领取或库存不足时，库存与用户券记录均不改变。
7. 统一页面入口：
   - 新增消费者、消费者中心、资料编辑、地址管理和优惠券中心页面；
   - 首页与导航统一改为中文 Bootstrap 界面；
   - 所有写表单启用防伪令牌和中文客户端校验。

## 自动化验证

测试总计 14 条，全部通过：

- 阶段 2 订单事务场景 4 条；
- 阶段 3 客户营销场景 10 条：
  1. 新增消费者生成密码哈希并初始化基础等级；
  2. 编辑资料不改变积分和累计消费；
  3. 第一条地址自动成为默认地址；
  4. 编辑唯一地址时仍保持默认地址；
  5. 切换默认地址后仍只有一个默认地址；
  6. 删除默认地址时自动顺延；
  7. 累计消费跨门槛后自动写回会员等级；
  8. 领券同时扣库存并产生可用券；
  9. 重复领券回滚且不扣库存；
  10. 库存不足时不创建用户券。

执行命令：

```powershell
dotnet build FreshColdChain.csproj --no-restore
dotnet build tests\FreshColdChain.Tests\FreshColdChain.Tests.csproj --no-restore
dotnet run --project tests\FreshColdChain.Tests\FreshColdChain.Tests.csproj --no-restore
dotnet format FreshColdChain.csproj --verify-no-changes --no-restore
dotnet format tests\FreshColdChain.Tests\FreshColdChain.Tests.csproj --verify-no-changes --no-restore
git diff --check
```

结果：两个项目均为 0 个警告、0 个错误；14/14 场景通过；格式和空白检查通过。

## 浏览器 QA

- Browser 插件可用，未使用外部浏览器回退。
- `/Customer/Create`：
  - 1440×900 桌面视口正常渲染；
  - 页面标题、表单、导航和页脚完整；
  - 空表单提交后显示姓名、手机、密码和确认密码的中文错误。
- `/Customer/CreateAddress?customerId=1`：
  - 390×844 移动视口正常渲染；
  - 页面宽度 390、文档滚动宽度 375，无水平溢出；
  - 空表单提交后显示全部必填字段的中文错误；
  - 移动端折叠导航可展开。
- 两个页面均未出现框架错误覆盖层，浏览器控制台无 warning/error。
- 桌面和移动截图保存在 Codex 可视化目录，未写入仓库。

## 涉及的主要文件

- `Models/CustomerCreateRequest.cs`
- `Models/CustomerProfileUpdateRequest.cs`
- `Models/AddressUpsertRequest.cs`
- `Models/CustomerProfileViewModel.cs`
- `Models/AddressListViewModel.cs`
- `Models/CouponCenterViewModel.cs`
- `Interfaces/IOrderService.cs`
- `Services/CustomerService.cs`
- `Services/CouponService.cs`
- `Services/OrderService.cs`
- `Repositories/CustomerRepository.cs`
- `Repositories/CouponRepository.cs`
- `Repositories/PointRepository.cs`
- `Controllers/CustomerController.cs`
- `Controllers/CouponController.cs`
- `Views/Customer/`
- `Views/Coupon/`
- `Views/Home/Index.cshtml`
- `Views/Shared/_Layout.cshtml`
- `database_schema.sql`
- `tests/FreshColdChain.Tests/CustomerMarketingScenarioTests.cs`

## 遗留事项

- 仓库没有隔离 Oracle 测试库说明，本阶段未执行真实 DDL 和 SQL 集成测试。
- 优惠券模板目前通过数据库数据维护，尚未提供后台模板 CRUD；阶段目标只要求领券和可用券查询。
- A 组真实库存与冷链运费适配尚未接入，当前下单仍使用 Dummy 库存且运费为 0。
- 订单列表、详情、合法状态流转和取消订单留待阶段 4。
- 退款积分扣回幂等、C 组佣金撤销和最终种子数据留待阶段 5。

## 下一阶段

阶段 4：完成订单查询、详情、供应商拆单展示和合法状态机；实现取消订单，并以接口契约
接入 A 组物流/运费和 C 组佣金触发能力，不直接访问其他组负责的数据表。
