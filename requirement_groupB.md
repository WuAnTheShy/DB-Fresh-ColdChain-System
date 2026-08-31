**《生鲜冷链团购分销供应链系统》团队开发规范与协作指导手册**

**一、 技术栈统一标准**

为了保证期末答辩时系统具有**单一入口、统一界面**，严禁各小组私自更改前后端框架。

**系统架构**：B/S 架构 (Browser/Server 网页系统)

**开发语言**：C#

**后端框架**：ASP.NET Core MVC (使用 Controller 控制流转，View 渲染页面)

**前端 UI**：HTML5 + CSS + JavaScript，**强制使用 Bootstrap 框架**（确保所有人写的按钮和表格样式统一）。

**数据库**：Oracle 18c

**数据访问技术**：微型ORM框架 Dapper（严禁在网页前端直接写 SQL 语句）。

**二、 代码分层与目录规范**

每个人的代码必须严格遵循“三层架构”，禁止出现“面条式代码”。骨架项目目录如下：

Controllers/：控制层。接收前端 HTTP 请求，调用 Service，返回 View 页面。

Views/：表现层。存放 .cshtml 文件，只写 HTML 和简单的 Razor 语法。

Services/：业务逻辑层。**核心层！所有复杂的计算、扣减、校验逻辑必须写在这里。**

Repositories/ ：数据访问层。只负责与 Oracle 数据库的 CRUD 操作。

Interfaces/：存放所有的跨组交互接口（见第三节）。

**三、 跨组交互接口规范（最高铁律）**

**【铁律】任何小组，绝不允许直接写 SQL 增删改查“其他小组负责的表”！必须通过调用对方提供的 C# Interface 实现。**

1. **接口先行**：开发前，三大组长必须共同定义好 Interfaces/ 下的接口契约（方法名、入参、出参）。
2. **Mock（假数据）隔离测试**：如果依赖的小组还没开发完，本组需创建一个“永远返回成功”的假实现（Dummy Class）进行联调，不要以“别人没写完”为由拖延进度。
3. **事务控制权归属**：跨模块调用时，**数据库事务（Transaction）由业务发起方控制**。例如：B组发起下单调用A组扣库存，BeginTransaction 和 Rollback 必须写在 B 组的 Controller 或 Service 中。

**四、 Git 协同开发流规范**

1. **忽略文件**：仓库已配置 Visual Studio 模板的 .gitignore，**严禁提交 bin/、obj/ 等编译缓存文件**。
2. **分支管理**：

main 分支：主干分支。只有大组长有权合并代码，平时必须保持处于可编译运行状态。

三大开发分支：dev-groupA、dev-groupB、dev-groupC。各小组成员只能在自己的组分支上提交代码。

1. **Commit 提交规范**：
   提交信息必须清晰，格式为：<动作>: <具体做了什么>。

   正确示范：feat: 新增FEFO库存批次扣减算法 / fix: 修复优惠券核销时积分未扣减的bug

   错误示范：update / 111 / 修改了代码

   **五、任务节点**

   **1. 7月30日前有初步成果
 2. 8月15日前已基本完成（差一些简单调试）**

**3. 8月30日前全部完成并撰写报告**



小组 1：供应链与库存物流组
人员：余浩然，梁皓诚，王耀增
负责表：Inv_Suppliers (供应商), Inv_Category (分类), Inv_Products (商品), Biz_PriceRules (定价规则), Inv_StockSummary (库存汇总), Inv_StockBatches (库存批次), Biz_OrderDetailBatches (明细批次关联), Log_ExpressDeliveries (物流), Log_FreightTemplates (运费模板)
具体核心职责：
商品主数据与动态定价引擎
批次化库存防超卖与 FEFO 算法（核心难点）
并发事务锁
冷链运费算法与溯源发货
阶梯冷链运费计算
精准溯源：


小组 2：C端交易与营销组
人员：魏世杰，闫容浩，吴语真，童文景
负责表：Crm_Customers (消费者), Crm_UserAddresses (地址), Crm_MemberLevels (会员等级), Mkt_Coupons (优惠券), Mkt_CouponRecords (用券记录), Crm_PointLogs (积分流水), Biz_Orders (订单主表), Biz_OrderDetails (订单明细)
具体核心职责与复杂业务逻辑：
购物车与核心订单事务控制（核心难点）
全局数据库事务
供应商拆单逻辑
动态会员等级与成长体系
实现自动化定级状态机
营销资产（券/积分）校验与闭环
防薅羊毛逻辑
资产流水保证

小组 3：分销财务与系统风控组
人员：许桓鸣，何轩越，宋张志恒
负责表：Crm_Promoters (团长), Fin_WithdrawalRecords (提现记录), Fin_PaymentRecords (支付流水), Fin_Refunds (退款), Sys_Roles (角色), Sys_Users (后台用户), Log_AuditTrails (操作日志)
具体核心职责与复杂业务逻辑：
分销体系与阶梯佣金自动结算（项目亮点）
异步佣金计算引擎
阶梯业绩激励
正逆向财务流转中心（核心难点）
复杂的售后退款链路 
B组扣回买家因该笔订单获得的积分 -> 命令 C组自身撤销团长因该笔订单获得的预计佣金。这是一个牵一发而动全身的核心业务点。
RBAC权限模型与防篡改审计系统
动态权限控制



每组功能
1.	供应商（大数据库）（包括各种商品种类信息、订单从这里调）
2.	交易订单（管理员）
3.	团长（消费者）


订单：供应商给平台（订单）、平台给团长、团长给平台、平台给消费者
