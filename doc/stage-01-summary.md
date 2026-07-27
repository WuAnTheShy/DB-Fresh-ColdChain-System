# 阶段 1 总结：事务底座与基线审计

完成日期：2026-07-27

## 阶段目标

在继续开发 GroupB 业务前，确认需求、跨组现状和现有代码缺口，并修复会导致订单
“看似有事务、实际跨连接执行”的基础问题。

## 已完成内容

1. 阅读并核对 `tmp` 中 3 份 PDF、仓库 Markdown 需求、数据库设计和 GroupB DDL。
2. 对照远端 `dev-groupA`、`dev-groupC`，确认当前跨组接口状态和主键类型差异。
3. 修复 Repository 的事务连接生命周期：
   - 事务操作全部使用事务所属连接；
   - 普通查询由 Repository 管理独立连接；
   - 不释放由上层 Service 管理的事务连接。
4. 将订单创建、退款扣积分中的消费者读取纳入事务，保证读写一致。
5. 消费者不存在时终止下单并触发回滚，避免订单与积分资产状态不完整。
6. 在 `doc` 中建立总体实施计划、需求完成矩阵和本阶段总结。
7. 将原始参考资料目录加入 Git 忽略范围，避免提交课程原始 PDF 和阅读中间文件。

## 涉及文件

- `.gitignore`
- `Repositories/BaseRepository.cs`
- `Repositories/OrderRepository.cs`
- `Repositories/CustomerRepository.cs`
- `Repositories/CouponRepository.cs`
- `Repositories/PointRepository.cs`
- `Services/OrderService.cs`
- `doc/GroupB-implementation-plan.md`
- `doc/GroupB-progress.md`
- `doc/stage-01-summary.md`

## 关键实现逻辑

`BaseRepository.WithConnectionAsync` 统一决定连接所有权。传入事务时，它从
`IDbTransaction.Connection` 获取已打开连接，并把相同事务继续传给 Dapper；
未传入事务时才创建和释放新连接。这样 Order、Detail、Customer、Point 和后续 Coupon
操作能够真正处于同一 Oracle 事务中。

## 验证结果

- .NET 10 编译通过，0 个警告、0 个错误。
- Kestrel 成功启动并监听本地端口，验证后已停止进程。
- Git 空白错误检查通过。
- 已审计 GroupB Repository 的连接创建位置，业务仓储不再自行创建事务外连接。
- 未执行共享 Oracle 数据库写入测试；缺少隔离测试库信息，不能安全插入测试订单。

## 遗留事项

- Controller 仍使用硬编码商品明细，不能作为正式下单入口。
- 优惠券尚未在下单事务中完成全量校验和原子核销。
- 积分倍率、会员自动升级、供应商拆单和库存接口尚未完成。
- 设计文档主键类型与 A/B 分支实现不一致，需在联调阶段统一。
- 固定数据库连接配置需要在最终交付前迁移并轮换凭据。

## 下一阶段

阶段 2：真实下单与营销资产闭环。完成真实请求 DTO、服务端金额计算、A 组库存契约与
Dummy、优惠券原子核销、积分倍率和事务失败场景验证。
