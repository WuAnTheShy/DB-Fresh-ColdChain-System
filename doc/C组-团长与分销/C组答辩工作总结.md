# 生鲜冷链团购分销供应链系统

## C 组工作总结与实现思路（答辩材料）

---

## 1. 一分钟开场：C 组到底做什么

本系统按课程分组拆成三条业务链：

- **A 组**：供应商、货物、库存批次、冷链运费与物流履约。
- **B 组**：消费者商城、下单支付、订单状态机、积分优惠券。
- **C 组**：团长分销体系、平台后台治理、佣金与提现、支付流水、退款资金回滚、操作审计。

C 组不负责把货发出去，也不负责消费者怎么逛商城。C 组负责的是：**谁能成为团长、团长卖什么、卖完怎么分钱、出了退款怎么把钱收回来、平台管理员怎么监管**。

答辩时可以这样定位一句话：

> C 组是分销结算中台。向上给消费者端提供团长货盘和关注关系，向下承接订单完成后的佣金入账；同时用四类管理员把「改身份、改资金、改货品、看日志」拆开，避免一个超级账号包办全部。

---

## 2. C 组工作全景

| 子模块 | 核心产出 | 关键代码 |
| --- | --- | --- |
| 团长门户 | 注册登录、工作台、业绩、佣金、提现、上架、推文、粉丝 | `PromotersController`、`PromoterService` |
| 管理后台 | 四类管理员、注册审核、启禁用、提现/退款审核、流水、日志 | `AdminsController`、`SystemAdminService` |
| 佣金结算 | 等级比例、阶梯奖励、14 天两段结算、退款对称回滚 | `CommissionService`、`GroupC_CommissionSettlementWorker` |
| 提现 | 三账户资金、冻结、审核打款/驳回解冻 | `WithdrawalService` |
| 支付流水 | 订单支付成功/失败落账，供财务对账 | `PaymentService` |
| 退款资金 | C 组发起事务，回滚佣金并回调 B 组扣积分、改订单 | `RefundService` |
| 跨组目录 | 可用团长、带货商品、售价校验，B 组不直读 C 组表 | `GroupCPromoterCatalogService` |
| 统一授权 | B 组订单/履约入口向 C 组问「这个人能不能做这件事」 | `GroupCAuthorizationService` |
| 审计 | 关键写操作写入 `LOG_AUDITTRAILS` | `TableLogService` |

分层方式与全项目一致：Razor/API → Controller → Service → Repository（Dapper）→ Oracle。写路径普遍自建或挂载 `IUnitOfWork` 事务，失败整单回滚。

---

## 3. 团长模块：从入驻到带货

### 3.1 实现思路

团长不是仓库，也不是快递。他是社区分销员。所以数据上必须把三件事拆开：

1. **身份**：`CRM_PROMOTERS` 存账号、邀请码、佣金比例、三本账（待结算 / 可提现 / 冻结）。
2. **货盘**：不是绑「一个商品」，而是绑 **（团长，商品，供应商）** 三元组，表 `CRM_PRODUCT_ENTRIES`。同一葡萄可以来自不同供应商，团长分别入团、分别定价。
3. **粉丝**：`CRM_PCR` 存消费者关注关系。下单时 B 组把 `PromoterID` 写进订单，C 组之后按这个 ID 计佣。

### 3.2 注册审核怎么做

实现方式不是「注册完立刻能登」，而是状态机：

1. 登录页 `Account/PromoterRegister` 收集姓名、账号、手机、两次密码。
2. `PromoterService.RegisterPromoter` 查重账号，密码 SHA-256 后 Base64 入库，生成 `PRO_` + GUID 和 8 位邀请码，状态写成 **`Pending`**。
3. 账号管理员在 `AccountReview` 点通过，`SystemAdminService.ApprovePromoterAsync` 把状态改为 **`Enable`**。
4. 登录时 `LoginPromoter` 按状态拒绝：Pending 提示等待审核，Disable/Frozen 提示不可用。成功后 Session 只放 `PromoterId`、`PromoterName`，后续所有团长页用 `EnsureLoggedIn()` 卡住未登录访问。

管理员代建团长走 `AddPromoterByAdmin`，直接生效，适合演示签约团长，不必再走 Pending。

答辩可强调：**审核与启禁用是两条路**。Pending 只能在注册审核里处理，账号管理页只允许 Enable ↔ Disable，防止绕过入驻核验。

### 3.3 商品上架与定价怎么做

页面 `ProductListing` 的实现思路是「先搜 A 组货盘，再在 C 组落自己的入团记录」：

1. 关键词交给 A 组 `ISupplierService.SearchSupplierProductEntriesAsync`。按供应商搜，返回该供应商全部货物；按商品名搜，返回所有供应该商品的组合。
2. 入团前再读 `Inv_Goods`：必须 `ACTIVE`，供应商下架则抛错，不允许入团。
3. 定价规则在服务层强制校验，前端填什么都不信任：

`|团长价 - 推荐价| < |推荐价 - 供应商报价| / 2`

未填则默认推荐价。推荐价等于报价时，团长价必须等于推荐价。入团和改价共用同一函数规则。

4. 出团是软删除入团记录，历史订单不受影响。

**为什么这样设计**：生鲜有多家供货、价格窗口很窄。把定价锁在报价与推荐价之间，避免团长恶性低价或虚高；把入团粒度做成商品×供应商，才能和 A 组货物上下架对齐。供应商事后下架时，团长端仍能看见并标注已下架，消费者目录则过滤掉，两边口径不同是刻意的。

### 3.4 图文推文怎么存

如果把富文本直接塞进 Oracle 大字段，列表查询会很重，图片也不好管。实现方式是 **库表只存路径，内容落文件**：

- 团长在 `EntryDetail` 用 `_RichIntroEditor` 提交 JSON（标题 + 多段文字/图片）。
- `PromoterIntroStore.SaveAsync` 写到 `wwwroot/uploads/promoter-desc/desc_{团长}_{商品}_{供应商}.json`。
- `CRM_PRODUCT_ENTRIES.PROMOTERDESC` 只存 `/uploads/promoter-desc/....json`。
- 本地插图上传校验扩展名和 5MB，文件名随机，落到 `wwwroot/uploads/promoter-img/`。
- 读的时候兼容三种历史值：空、JSON 路径、早期纯文字（自动包成一段）。
- 给 B 组目录时再 `ToPlainTextAsync` 抽纯文本简介，避免把文件路径当商品介绍展示。

消费者看推文走 REST：`GET /api/promoters/{id}/featured-products/{productId}/intro`。商品不是该团长在售货盘则 404。

### 3.5 关注关系怎么接到下单

C 端点关注，调用 `POST /api/customers/{customerId}/following/{promoterId}`，最终 `BindCustomerToPromoterAsync` 向 `CRM_PCR` 插一行；已存在则幂等成功。取消关注删除关系。

下单不由 C 组执行。B 组结算时按购物车里的团长拆单，订单带上 `PromoterID`。C 组只在「订单变成已完成」时被回调计佣。这样 **关注是 CRM，计佣是财务，两张表通过订单上的团长编号衔接**。

---

## 4. 佣金两段结算：这是 C 组最核心的算法

### 4.1 为什么不能下单立刻给钱

生鲜冷链有售后窗口。如果签收当天就把佣金变成可提现，退款时可能钱已经提走。所以做成两段：

- **一段**：订单完成后，钱进 **待结算余额** `PendingBalance`，佣金记录状态 `Pending`，预计结算日 = 签收日 + 14 天。
- **二段**：过了退款期，定时任务把钱转到 **可提现余额** `CurrentBalance`，记录改为 `Settled`。

### 4.2 一段结算如何被触发

B 组订单状态机走到「已发货 → 已完成」时，在 **同一笔订单事务** 里调用：

`ICommissionService.RegisterCompletedOrderAsync(request, transaction)`

C 组不另开连接。`CommissionService` 发现外部事务后 `AttachExternalTransaction`，用 B 组的连接写自己的表。C 组失败则整笔订单完成回滚，订单保持已发货。无团长订单直接返回 0 佣金成功，不阻塞普通购买。

一段内部步骤：

1. 累计销售额 `TotalSales += 本单商品金额`。
2. 基础佣金用 **结算前的旧比例**：`CommBaseAmount = BaseCommissionRate × 实付金额`。本单不享受刚升上去的新比例，避免「这一单把你送进钻石，这一单又按钻石计」的套利。
3. 按新累计销售额 `GroupC_LevelCommissionPolicy.ResolveRate` 更新下一单比例：青铜 3% / 白银 4% / 黄金 5% / 钻石 8%，阈值 1000 / 3000 / 5000。
4. 阶梯奖励 `GroupC_CommissionBonusPolicy.CalculateCrossedBonus(旧销售额, 新销售额)`：每跨过一档发一笔固定奖，一单跨多档叠加。例如 1000/2000 各 50，3000/4000 各 150，5000 为 750 等。
5. 总佣金加入 `PendingBalance`，插入 `FIN_PROCOMRECORDS`。

### 4.3 二段激活如何防并发

后台 `GroupC_CommissionSettlementWorker` 继承 `BackgroundService`，启动先补扫一次，之后每小时扫到期且仍为 Pending 的记录。每条独立事务，单条失败不影响其它。

`ActivatePromoterMoney` 的关键实现：

1. **先查记录再动账**，防止重复激活给两次钱。
2. 已是 `Settled` 则幂等成功。
3. 激活金额以记录为准，不信任调用方传入金额；若发生过部分退款，按 `RefundedAmount / FinalAmount` 比例只激活剩余佣金。
4. 用 `TryUpdateStatusAsync(Pending → Settled)` 做乐观锁，和退款并发时只有一方能改成功。
5. `PendingBalance -= 金额`，`CurrentBalance += 金额`，写审计。

答辩被问「定时任务挂了怎么办」：启动补扫会把停机期间到期的记录补上；激活幂等，重复跑不会多给钱。

### 4.4 退款如何对称撤销

财务审核通过退款后，`RefundService.RefundRollbackMoney`：

- 基础佣金按退款比例从待结算余额扣回。
- 累计销售额减少后，若跌破档位，用 **同一张阶梯表** `CalculateRollbackBonus` 撤销对应奖励。
- 等级比例也按新销售额回退。

发放和撤销共用政策类，答辩时这是加分点：不是拍脑袋减一笔，而是规则对称。

---

## 5. 提现：三账户 + 冻结

团长工作台展示三本账，避免把待结算误当成能取的钱。

| 账户 | 含义 | 何时变 |
| --- | --- | --- |
| `PendingBalance` | 还在 14 天窗口里 | 一段增加，激活或退款减少 |
| `CurrentBalance` | 可提现 | 二段增加，申请提现减少，驳回加回 |
| `FrozenAmount` | 审核中 | 申请增加，通过或驳回减少 |

申请 `ApplyWithdrawal` 的实现顺序：

1. 必须已绑定微信 / 支付宝 / 银行卡（格式校验在 `PromoterPayAccounts`）。
2. 已有 Pending 提现则拒绝，防止连点两笔把余额扣成负数。
3. 余额不足拒绝。
4. **先扣可提现、再加冻结、再插提现单**，全部在一个事务里。任何一步失败回滚。

审核通过：提现单 `Approved`，打款时间取审核时间，冻结减少（钱视为已打出系统）。驳回必须填原因：冻结减少、可提现加回。

---

## 6. 管理员模块：按职责拆权

### 6.1 为什么要四种管理员

如果一个账号既能改团长身份，又能批提现，还能改货品，演示好看但不符合课程里的权限与审计要求。C 组把 `SYS_USERS.ADMIN_KIND` 分成：

| 种类 | 登录后去哪 | 能做什么 |
| --- | --- | --- |
| ACCOUNT 账号 | 自己的工作台 | 审注册、代建账号、启禁用、改团长佣金比例 |
| FINANCE 财务 | 财务工作台 | 审提现、审退款、查支付/退款流水 |
| LOG 日志 | 直接进操作日志 | 只读审计 |
| PRODUCT 商品 | `/Goods/AdminIndex` | 复用 A 组平台货品、定价、运费模板 |

登录必须选对种类，和库里不一致就拒绝。侧栏按种类裁剪菜单。商品级页面用 `[RequireAdmin]`，其它种类误入会被送回自己的 Dashboard。

新管理员注册也是 Pending，必须由 **已启用的账号管理员** 审核，避免任何人自助变成超管。

### 6.2 财务退款审核的实现方式

消费者在 C 端提交退款申请后，记录先是待审。财务点通过调用 `RefundService.AuditRefund`：

1. **C 组开启事务**（退款是财务动作，事务所有权在 C 组，不是 B 组）。
2. 写/更新退款单。
3. 回滚团长佣金（见 4.4）。
4. 把当前事务传给 B 组 `DeductPointsForRefundAsync`，扣积分、改订单状态。B 组不得自行 Commit。
5. 任一步失败，C 组整体 Rollback：不会出现「佣金扣了、积分没扣」或反过来。

驳回只改申请单状态，不动资金。

### 6.3 支付流水

B 组批次支付成功后调用 `IPaymentService.CreatePaymentRecord`，C 组生成 `PAY_` 流水号写入 `FIN_PAYMENTRECORDS`，成功/失败都落账，失败必须带原因。审计与支付共用发起方事务，审计失败视为支付失败并由事务所有者回滚。财务页 `PaymentRecords` 按日期、订单号、状态筛选，结束日期按「含当天」转成次日 0 点开区间。

---

## 7. 跨组协作：C 组对外提供什么、调用什么

答辩老师常问「三组怎么接」。C 组侧可以按「我们提供 / 我们调用」讲。

### 7.1 C 组提供给 B 组

| 接口 | 何时用 | 实现要点 |
| --- | --- | --- |
| `ICommissionService.RegisterCompletedOrderAsync` | 订单完成 | 挂载 B 组事务；无团长返回 0 佣金 |
| `IGroupCPromoterCatalogService` | 拼消费者目录、下单前校验带货 | 只走 `PromoterService`，B 组不写 C 组 SQL |
| `ValidatePromoterProductsAsync` | 结算 | 团长必须 Enable，且该（商品×供应商）已入团，才允许按团长价卖 |
| `IPaymentService.CreatePaymentRecord` | 支付 | 写流水，不改订单状态（订单仍归 B） |
| `IRefundService.GetOrderRefundsAsync` | 消息中心 | B 组查订单后再来问退款单，合并成消费者消息 |
| `IGroupCAuthorizationService.AuthorizeAsync` | 后台读订单/供应商履约 | C 组查管理员角色或供应商 Active 且资质未过期 |

带货校验逻辑可以口述：先看团长是否启用，再看是否有合作供应商，再看入团表有没有这一行。三关都过，才把团长售价回给 B 组。B 组不信任购物车里的缓存价。

### 7.2 C 组调用 A / B 组

| 调用 | 用途 |
| --- | --- |
| A 组 `SearchSupplierProductEntriesAsync` | 团长上架搜索货盘 |
| A 组 `Inv_Goods` 状态 | 入团前确认货物在售 |
| A 组物流只读 | 退款时判断是否已发货，作为责任与流程依据 |
| B 组 `IOrderService` 扣积分、改订单 | 退款核心，必须传入 C 组事务 |

原则：**谁发起业务，谁拥有事务**。下单事务归 B；退款事务归 C。被调用方只挂载连接，禁止自己 Commit。

---

## 8. 前端怎么落地（答辩演示口径）

团长和管理员都不是 Vue，而是 **服务端渲染 MVC**：

- 布局：`_PromoterLayout` / `_AdminLayout`，左侧栏 + 右侧工作台，CSS 共用 `promoter-portal.css`，用 `role-promoter` / `role-admin` 换色。
- 写操作带 Anti-Forgery Token，结果用 TempData + `_Alerts` 提示。
- 搜索上架用 PRG（Post-Redirect-Get），刷新不会重复提交。
- 图片上传走 Ajax JSON，不整页刷新。
- 消费者看团长，走 REST：`/api/promoters`、关注 API、推文 API、评价摘要。团长门户本身不对 C 端 SPA 开放。

演示建议顺序（最能体现 C 组）：

1. 角色选择页注册一个新团长 → 登录被拒（Pending）。
2. 账号管理员审核通过 → 团长进入工作台。
3. 商品上架：搜索、入团、改价、写推文。
4. 切到消费者端关注该团长、下单、确认收货。
5. 团长佣金明细出现待结算；说明 14 天与定时任务（演示可提数据库把预计结算日改到过去再等一轮，或讲清设计）。
6. 绑定收款账户申请提现 → 财务管理员通过/驳回。
7. 再走一笔退款，看佣金回滚和日志。

---

## 9. 技术要点（被问「亮点」时讲这些）

1. **状态机比开关可靠**：团长、提现、佣金、管理员种类都是显式状态，而不是一个布尔字段。
2. **三账户资金模型**：待结算 / 可提现 / 冻结分离，配合冻结与乐观锁，避免超提和重复结算。
3. **政策表复用**：等级比例、阶梯奖励发放与退款撤销走同一套常量，规则可讲清楚。
4. **事务所有权清晰**：跨组写操作不各提交一次，而是发起方一条事务贯穿。
5. **职责分离的后台**：四种管理员 + 审计日志，对应课程的完整性与安全性要求。
6. **货盘与内容解耦**：入团关系在库里，图文在文件系统，列表查询轻，推文可以图文并茂。
7. **对 B 组只暴露契约**：目录、校验、计佣、支付、授权都是接口，B 组不直接 Update C 组表。

---

## 10. 可能被问到的问题与答法

**Q：团长定价为什么不让随便填？**  
A：生鲜有供应商报价和平台推荐价。允许区间是「偏离推荐价不得超过报价与推荐价差距的一半」，把团长价锁在合理带货空间，由服务层强制校验。

**Q：佣金为什么 14 天？**  
A：对齐售后窗口。窗口内退款从待结算扣，不出现「钱已提走再追回」。到期由后台服务激活，启动时会补扫。

**Q：C 组和 B 组同时改余额怎么办？**  
A：二段激活用状态乐观锁 Pending→Settled；提现用「只能有一笔 Pending」。退款与激活抢同一条佣金记录时，只有一方能改状态成功，另一方重试或失败回滚。

**Q：管理员拆四种会不会太复杂？**  
A：课程系统规模下，这是为了演示职责分离。日志管理员不能批钱，财务不能改货，商品管理员走 A 组页面但套 C 组登录外壳。

**Q：图文为什么不存数据库？**  
A：介绍是大 JSON 且含多图。库里存路径，文件按团长+商品+供应商命名，避免路径穿越；给目录接口时再抽纯文本。

**Q：你们组的表有哪些？**  
A：核心是 `CRM_PROMOTERS`、`CRM_PRODUCT_ENTRIES`、`CRM_PCR`、`FIN_PROCOMRECORDS`、`FIN_WITHDRAWALRECORDS`、`FIN_PAYMENTRECORDS`、`FIN_REFUNDS`、`SYS_USERS`/`SYS_ROLES`、`LOG_AUDITTRAILS`。订单、库存、物流表分别归 B、A。

---

## 11. 收束（30 秒结尾）

C 组把分销闭环补全：团长入驻要审、货盘从 A 组货里挑、售价有规则、内容自己写、粉丝关系给 B 组下单用；订单完成后 C 组计佣，过窗口才能提现，财务审钱，退款按同一套规则把佣金和积分收回。后台按账号、财务、日志、商品拆权，关键变更全部留审计。这样三组拼起来，才是完整的生鲜冷链团购分销系统。
