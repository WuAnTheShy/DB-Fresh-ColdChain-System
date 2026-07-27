生鲜冷链团购分销供应链系统

数据库设计文档

2452286 魏世杰

2453594 闫容浩

2453358 童文景

2454267 吴语真

2454284 梁皓诚

2452310 许桓鸣

2454283 余浩然

2451393 王耀增

2452642 何轩越

2454285 宋张志恒

目 录

[1. 生鲜冷链团购分销供应链系统数据需求 1](#_Toc229427722)

[1.1 供应商与商品库存功能数据需求 1](#_Toc229427723)

[1.2 团长分销功能数据需求 1](#_Toc229427724)

[1.3 消费者与营销功能数据需求 1](#_Toc229427725)

[1.4 订单与物流履约功能数据需求 1](#_Toc229427726)

[1.5 后台管理与审计功能数据需求 2](#_Toc229427727)

[1.6 组织结构 2](#_Toc229427728)

[2. 概念设计 3](#_Toc229427729)

[2.1 总体E-R图 3](#_Toc229427730)

[2.2 库存管理模块E-R图 4](#_Toc229427731)

[2.3 订单履约模块E-R图 5](#_Toc229427732)

[2.4 分销结算模块E-R图 5](#_Toc229427733)

[3. 逻辑设计 7](#_Toc229427734)

[3.1 表的设计 7](#_Toc229427735)

[3.1.1 Sys\_Roles表 8](#_Toc229427736)

[3.1.2 Sys\_Users表 8](#_Toc229427737)

[3.1.3 Inv\_Suppliers表 9](#_Toc229427738)

[3.1.4 Crm\_Promoters表 9](#_Toc229427739)

[3.1.5 Inv\_Category表 9](#_Toc229427740)

[3.1.6 Crm\_MemberLevels表 10](#_Toc229427741)

[3.1.7 Mkt\_Coupons表 10](#_Toc229427742)

[3.1.8 Log\_FreightTemplates表 10](#_Toc229427743)

[3.1.9 Log\_AuditTrails表 11](#_Toc229427744)

[3.1.10 Crm\_Customers表 11](#_Toc229427745)

[3.1.11 Inv\_Products表 12](#_Toc229427746)

[3.1.12 Fin\_WithdrawalRecords表 12](#_Toc229427747)

[3.1.13 Inv\_StockSummary表 12](#_Toc229427748)

[3.1.14 Inv\_StockBatches表 13](#_Toc229427749)

[3.1.15 Crm\_UserAddresses表 13](#_Toc229427750)

[3.1.16 Biz\_PriceRules表 14](#_Toc229427751)

[3.1.17 Biz\_Orders表 14](#_Toc229427752)

[3.1.18 Log\_ExpressDeliveries表 15](#_Toc229427753)

[3.1.19 Biz\_OrderDetails表 15](#_Toc229427754)

[3.1.20 Biz\_OrderDetailBatches表 15](#_Toc229427755)

[3.1.21 Mkt\_CouponRecords表 16](#_Toc229427756)

[3.1.22 Crm\_PointLogs表 16](#_Toc229427757)

[3.1.23 Fin\_Refunds表 16](#_Toc229427758)

[3.1.24 Fin\_PaymentRecords表 17](#_Toc229427759)

[3.2 数据库关系图 17](#_Toc229427760)

[附录A. 图表索引 19](#_Toc229427761)

# 生鲜冷链团购分销供应链系统数据需求

本系统为“生鲜冷链团购分销供应链系统”，涵盖供应商与商品库存、团长分销、消费者与营销、订单与物流履约以及后台管理与审计五大模块。

## 供应商与商品库存功能数据需求

系统需要存储以下数据：

* 供应商基础信息（资质号、到期时间、信用等级）
* 商品分类信息
* 商品基础属性（单位、重量、体积、保质期、温区要求）
* 商品批次信息（生产日期、过期时间、进价、库存数量）
* 库存聚合数据（总库存、锁定库存、可用库存）
* 动态定价规则

涉及数据表：
Inv\_Suppliers、Inv\_Category、Inv\_Products、Inv\_StockBatches、Inv\_StockSummary、Biz\_PriceRules

## 团长分销功能数据需求

系统需要存储：

* 团长基本信息
* 邀请码与绑定关系
* 分销佣金金额
* 阶梯奖励统计数据
* 提现申请记录

涉及数据表：
Crm\_Promoters、Fin\_WithdrawalRecords、Biz\_Orders

## 消费者与营销功能数据需求

系统需要存储：

* 团长基本信息
* 邀请码与绑定关系
* 分销佣金金额
* 阶梯奖励统计数据
* 提现申请记录

涉及数据表：
Crm\_Promoters、Fin\_WithdrawalRecords、Biz\_Orders

## 订单与物流履约功能数据需求

系统需要存储：

* 订单主信息
* 订单明细
* 批次扣减记录（FEFO机制）
* 运费模板
* 物流配送信息
* 支付记录
* 退款记录

涉及数据表：
Biz\_Orders、Biz\_OrderDetails、Biz\_OrderDetailBatches、Log\_FreightTemplates、Log\_ExpressDeliveries、Fin\_PaymentRecords、Fin\_Refunds

## 后台管理与审计功能数据需求

系统需要存储：

* 系统角色
* 后台用户
* 操作日志

涉及数据表：
Sys\_Roles、Sys\_Users、Log\_AuditTrails

## 组织结构

列出文档的组织结构。

第一章：第一章：系统数据需求说明。
第二章：概念结构设计与E-R模型说明。
第三章：逻辑结构设计与关系模式说明。

附录A：是本文档的图表索引。

# 概念设计

本系统为生鲜冷链团购分销供应链系统，涉及供应商管理、商品库存管理、团长分销体系、消费者交易履约体系以及平台审计监管模块。

概念设计阶段采用E-R模型对系统中的实体、属性及实体间联系进行抽象建模。

## 总体E-R图

本系统总体E-R模型包含五大类核心实体：

* 用户类实体
* 商品类实体
* 订单类实体
* 财务类实体
* 日志与系统管理类实体

**（1）核心实体说明**

**① 供应商**

主要属性：
供应商编号、供应商名称、资质编号、资质到期时间、信用等级、联系电话、登录账号

说明：存储入驻平台供应商的基础信息与资质情况。

**② 商品**

主要属性：
商品编号、商品名称、分类编号、供应商编号、单位、重量、体积、保质期小时数、存储温区、默认价格

说明：存储商品基本信息。

**③ 商品批次**

主要属性：
批次编号、商品编号、生产日期、过期日期、进价、当前库存数量、状态

说明：用于实现FEFO（先进先出）库存管理。

**④ 消费者**

主要属性：
消费者编号、OpenID、手机号、积分余额、成长值、绑定团长编号

说明：存储平台注册用户信息。

**⑤ 团长**

主要属性：
团长编号、团长姓名、手机号、基础佣金比例、当前余额、待结算余额、累计销售额

说明：用于管理分销体系。

**⑥ 订单**

主要属性：
订单编号、订单号、消费者编号、团长编号、商品总金额、运费、优惠金额、实付金额、状态

说明：存储订单交易核心信息。

**（2）实体之间的联系**

1. 供应商 与 商品 为 1：N关系
2. 商品 与 商品批次 为 1：N关系
3. 消费者 与 订单 为 1：N关系
4. 订单 与 订单明细 为 1：N关系
5. 订单明细 与 商品批次 为 N:M关系
6. 团长 与 订单 为 1：N关系
7. 订单 与 支付记录 为 1：N关系
8. 订单 与 物流配送 为 1：N关系

图 2‑1 总体E-R图

## 库存管理模块E-R图

涉及实体：

* 商品
* 商品批次
* 库存汇总
* **主要联系**
* 商品 与 商品批次 为 1:N
* 商品 与 库存汇总 为 1:1

设计特点：

* 采用库存聚合表（Inv\_StockSummary）提升并发查询性能
* 采用批次表（Inv\_StockBatches）实现FEFO先进先出

图 2‑2 库存管理模块E-R图

## 订单履约模块E-R图

涉及实体：

* 订单
* 订单明细
* 批次扣减记录
* 物流配送
* **主要联系**
* 订单 与 订单明细 为 1:N
* 订单明细 与 商品批次 为 N:M（通过中间表实现）
* 订单 与 物流配送 为 1:N

设计特点：

* 支持多供应商拆单
* 支持批次级库存追溯

图 2‑3 订单契约模块E-R图

## 分销结算模块E-R图

涉及实体：

* 团长
* 消费者
* 订单
* 提现记录

1. **实体关系**
2. 团长 与 消费者 之间为 1:N 关系（绑定关系）
3. 团长 与 订单 之间为 1:N 关系
4. 团长 与 提现记录 之间为 1:N 关系
5. **设计说明**

* 订单签收后触发佣金结算
* 支持阶梯奖励机制
* 支持售后退款冲抵机制

图 2‑4 分销结算模块E-R图

# 逻辑设计

逻辑设计阶段将概念模型转化为关系模型，定义数据表结构及外键约束。

系统共设计 24 张数据表。

## 表的设计

本系统数据库采用 Oracle 数据库管理系统，所有主键统一采用 VARCHAR2(36) 类型作为唯一标识。

根据功能划分，数据表分为以下几类：

* **（1）系统管理类表**

用于管理平台后台角色及操作人员信息。

* **Sys\_Roles**：存储系统角色信息。
* **Sys\_Users**：存储后台管理员账户信息。
* **Log\_AuditTrails**：存储系统操作日志，用于数据审计与防篡改。
* **（2）供应商与商品管理类表**

用于管理供应商信息、商品信息及库存结构。

* **Inv\_Suppliers**：存储供应商基础信息及资质信息。
* **Inv\_Category**：存储商品分类结构信息。
* **Inv\_Products**：存储商品基础属性信息。
* **Inv\_StockSummary**：存储商品库存聚合数据。
* **Inv\_StockBatches**：存储商品批次级库存信息。
* **Biz\_PriceRules**：存储商品动态定价规则。
* **（3）消费者与营销管理类表**

用于管理用户信息、会员体系与优惠券体系。

* **Crm\_Customers**：存储消费者基础信息。
* **Crm\_UserAddresses**：存储用户收货地址信息。
* **Crm\_MemberLevels**：存储会员等级规则。
* **Mkt\_Coupons**：存储优惠券定义信息。
* **Mkt\_CouponRecords**：存储用户优惠券持有与使用记录。
* **Crm\_PointLogs**：存储积分变动流水记录。
* **（4）订单与履约管理类表**

用于管理订单生命周期与物流履约过程。

* **Biz\_Orders**：存储订单主信息。
* **Biz\_OrderDetails**：存储订单明细信息。
* **Biz\_OrderDetailBatches**：存储订单明细与批次扣减对应关系。
* **Log\_ExpressDeliveries**：存储物流配送信息。
* **Log\_FreightTemplates**：存储运费与冷链费用模板。
* **（5）财务管理类表**

用于管理支付、退款与分销结算相关数据。

* **Fin\_PaymentRecords**：存储订单支付流水。
* **Fin\_Refunds**：存储退款与责任判定信息。
* **Crm\_Promoters**：存储团长分销信息及佣金统计数据。
* **Fin\_WithdrawalRecords**：存储团长提现申请记录。

### Sys\_Roles表

表格 3‑1 Sys\_Roles表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| RoleID | varchar2 | 36 | 角色编号 | PK |
| RoleName | varchar2 | 50 | 角色名称 | 非空 |
| Description | varchar2 | 200 | 角色描述 |  |

### Sys\_Users表

表格 3‑2 Sys\_Users表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| UserID | varchar2 | 36 | 用户编号 | PK |
| Username | varchar2 | 50 | 登录名 | 唯一 |
| PasswordHash | varchar2 | 255 | 加密密码 |  |
| RoleID | varchar2 | 36 | 角色编号 | FK，参照Sys\_Roles表 |
| RealName | varchar2 | 50 | 真实姓名 |  |
| Status | varchar2 | 20 | 状态 |  |

### Inv\_Suppliers表

表格 3‑3 Inv\_Suppliers表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| SupplierID | varchar2 | 36 | 供应商编号 | PK |
| SupplierName | varchar2 | 100 | 供应商名称 | 非空 |
| LicenseNo | varchar2 | 100 | 资质编号 |  |
| ExpiryDate | date |  | 资质到期时间 |  |
| CreditLevel | number | 5 | 信用等级 |  |
| ContactPhone | varchar2 | 20 | 联系电话 |  |
| LoginAccount | varchar2 | 50 | 登录账号 |  |
| LoginPassword | varchar2 | 255 | 登录密码 |  |

### Crm\_Promoters表

表格 3‑4 Crm\_Promoters表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| PromoterID | varchar2 | 36 | 团长编号 | PK |
| PromoterName | varchar2 | 50 | 团长姓名 | 非空 |
| Phone | varchar2 | 20 | 联系电话 | 非空 |
| BaseCommissionRate | number | 5,2 | 基础佣金比例 |  |
| InviteCode | varchar2 | 50 | 邀请码 | 唯一 |
| CurrentBalance | number | 10,2 | 当前余额 | 默认0 |
| PendingBalance | number | 10,2 | 待结算余额 | 默认0 |
| TotalSales | number | 12,2 | 累计销售额 | 默认0 |
| TotalOrderCount | number | 10 | 累计订单数 | 默认0 |

### Inv\_Category表

表格 3‑5 Inv\_Category表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| CategoryID | varchar2 | 36 | 分类编号 | PK |
| CategoryName | varchar2 | 100 | 分类名称 | 非空 |
| ParentID | varchar2 | 36 | 上级分类编号 |  |

### Crm\_MemberLevels表

表格 3‑6 Crm\_MemberLevels表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| LevelID | varchar2 | 36 | 等级编号 | PK |
| LevelName | varchar2 | 50 | 等级名称 |  |
| MinGrowthValue | number | 10 | 最小成长值 |  |
| PointsRate | number | 5,2 | 积分倍率 |  |

### Mkt\_Coupons表

表格 3‑7 Mkt\_Coupons表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| CouponID | varchar2 | 36 | 优惠券编号 | PK |
| Title | varchar2 | 100 | 优惠券名称 |  |
| CouponType | varchar2 | 20 | 类型 |  |
| MinAmount | number | 10,2 | 使用门槛 |  |
| DiscountValue | number | 10,2 | 优惠金额 |  |
| LimitCategoryID | varchar2 | 36 | 限定分类编号 |  |

### Log\_FreightTemplates表

表格 3‑8 Log\_FreightTemplates表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| TemplateID | varchar2 | 36 | 模板编号 | PK |
| TemplateName | varchar2 | 100 | 模板名称 |  |
| DestinationProvince | varchar2 | 50 | 目的省份 |  |
| BaseWeight | number | 10,2 | 首重 |  |
| BaseFee | number | 10,2 | 首重费用 |  |
| ExtraWeightFee | number | 10,2 | 续重费用 |  |
| PackagingFee | number | 10,2 | 冷链包装费 |  |

### Log\_AuditTrails表

表格 3‑9 Log\_AuditTrails表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| LogID | varchar2 | 36 | 日志编号 | PK |
| TableName | varchar2 | 50 | 表名 |  |
| RecordID | varchar2 | 36 | 记录编号 |  |
| ActionType | varchar2 | 20 | 操作类型 |  |
| OldValue | varchar2 | 1000 | 修改前值 |  |
| NewValue | varchar2 | 1000 | 修改后值 |  |
| OperatorType | varchar2 | 20 | 操作端类型 |  |
| OperatorID | varchar2 | 36 | 操作人编号 |  |
| OpTime | date |  | 操作时间 |  |

### Crm\_Customers表

表格 3‑10 Crm\_Customers表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| CustomerID | varchar2 | 36 | 用户编号 | PK |
| OpenID | varchar2 | 100 | 微信标识 | 唯一 |
| Phone | varchar2 | 20 | 手机号 |  |
| PointsBalance | number | 10 | 积分余额 | 默认0 |
| GrowthValue | number | 10 | 成长值 | 默认0 |
| BoundPromoterID | varchar2 | 36 | 绑定团长编号 | FK，参照Crm\_Promoters表 |
| BindExpireTime | date |  | 绑定失效时间 |  |

### Inv\_Products表

表格 3‑11 Inv\_Products表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| ProductID | varchar2 | 36 | 商品编号 | PK |
| CategoryID | varchar2 | 36 | 分类编号 | FK，参照Inv\_Category表 |
| SupplierID | varchar2 | 36 | 供应商编号 | FK，参照Inv\_Suppliers表 |
| ProductName | varchar2 | 100 | 商品名称 | 非空 |
| Unit | varchar2 | 20 | 单位 |  |
| WeightKG | number | 10,2 | 单件重量 |  |
| VolumeLitre | number | 10,2 | 单件体积 |  |
| ExpiryHours | number | 10 | 保质期小时数 |  |
| StorageReq | varchar2 | 20 | 存储温区 |  |
| DefaultPrice | number | 10,2 | 默认价格 |  |

### Fin\_WithdrawalRecords表

表格 3‑12 Fin\_WithdrawalRecords表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| WithdrawalID | varchar2 | 36 | 提现编号 | PK |
| PromoterID | varchar2 | 36 | 团长编号 | FK，参照Crm\_Promoters表 |
| ApplyAmount | number | 10,2 | 申请金额 |  |
| AccountInfo | varchar2 | 200 | 提现账户信息 |  |
| ApplyTime | date |  | 申请时间 |  |
| AuditTime | date |  | 审核时间 |  |
| AuditStatus | varchar2 | 20 | 审核状态 |  |

### Inv\_StockSummary表

表格 3‑13 Inv\_StockSummary表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| StockID | varchar2 | 36 | 库存编号 | PK |
| ProductID | varchar2 | 36 | 商品编号 | FK，参照Inv\_Products表 |
| TotalQty | number | 10 | 总库存 | 默认0 |
| LockedQty | number | 10 | 锁定库存 | 默认0 |
| AvailableQty | number | 10 | 可用库存 | 默认0 |

### Inv\_StockBatches表

表格 3‑14 Inv\_StockBatches表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| BatchID | varchar2 | 36 | 批次编号 | PK |
| ProductID | varchar2 | 36 | 商品编号 | FK，参照Inv\_Products表 |
| SupplierID | varchar2 | 36 | 供应商编号 | FK，参照Inv\_Suppliers表 |
| BatchNo | varchar2 | 50 | 批号 | 非空 |
| ProductionDate | date |  | 生产日期 |  |
| ExpiryDate | date |  | 失效日期 |  |
| InPrice | number | 10,2 | 进价 |  |
| InitialQty | number | 10 | 初始数量 |  |
| CurrentQty | number | 10 | 当前数量 |  |
| Status | varchar2 | 20 | 状态 |  |

### Crm\_UserAddresses表

表格 3‑15 Crm\_UserAddresses表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| AddressID | varchar2 | 36 | 地址编号 | PK |
| CustomerID | varchar2 | 36 | 用户编号 | FK，参照Crm\_Customers表 |
| ReceiverName | varchar2 | 50 | 收件人 |  |
| Phone | varchar2 | 20 | 联系电话 |  |
| Province | varchar2 | 50 | 省份 |  |
| City | varchar2 | 50 | 城市 |  |
| District | varchar2 | 50 | 区县 |  |
| DetailAddress | varchar2 | 200 | 详细地址 |  |
| IsDefault | number | 1 | 是否默认 |  |

### Biz\_PriceRules表

表格 3‑16 Biz\_PriceRules表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| RuleID | varchar2 | 36 | 规则编号 | PK |
| ProductID | varchar2 | 36 | 商品编号 | FK，参照Inv\_Products表 |
| RuleName | varchar2 | 100 | 规则名称 |  |
| TimeWindow | varchar2 | 50 | 时间窗口 |  |
| DiscountRate | number | 5,2 | 折扣率 |  |
| TriggerType | varchar2 | 50 | 触发类型 |  |

### Biz\_Orders表

表格 3‑17 Biz\_Orders表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| OrderID | varchar2 | 36 | 订单编号 | PK |
| OrderNo | varchar2 | 50 | 订单号 | 唯一 |
| CustomerID | varchar2 | 36 | 用户编号 | FK，参照Crm\_Customers表 |
| PromoterID | varchar2 | 36 | 团长编号 | FK，参照Crm\_Promoters表 |
| AddressID | varchar2 | 36 | 地址编号 | FK，参照Crm\_UserAddresses表 |
| GoodsAmount | number | 10,2 | 商品金额 |  |
| FreightAmount | number | 10,2 | 运费 |  |
| CouponAmount | number | 10,2 | 优惠金额 |  |
| FinalAmount | number | 10,2 | 实付金额 |  |
| Status | varchar2 | 20 | 状态 |  |
| CommBaseAmount | number | 10,2 | 基础佣金 |  |
| CommBonusAmount | number | 10,2 | 奖励佣金 |  |
| CommSettlementDate | date |  | 结算时间 |  |

### Log\_ExpressDeliveries表

表格 3‑18 Log\_ExpressDeliveries表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| DeliveryID | varchar2 | 36 | 配送编号 | PK |
| OrderID | varchar2 | 36 | 订单编号 | FK，参照Biz\_Orders表 |
| LogisticsCompany | varchar2 | 100 | 物流公司 |  |
| TrackingNo | varchar2 | 100 | 运单号 |  |
| PackageTemp | varchar2 | 20 | 包裹温区 |  |
| LogisticsStatus | varchar2 | 20 | 物流状态 |  |

### Biz\_OrderDetails表

表格 3‑19 Biz\_OrderDetails表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| DetailID | varchar2 | 36 | 明细编号 | PK |
| OrderID | varchar2 | 36 | 订单编号 | FK，参照Biz\_Orders表 |
| DeliveryID | varchar2 | 36 | 配送编号 | FK，参照Log\_ExpressDeliveries表 |
| ProductID | varchar2 | 36 | 商品编号 | FK，参照Inv\_Products表 |
| Quantity | number | 10 | 数量 |  |
| UnitPrice | number | 10,2 | 单价 |  |
| SubTotal | number | 10,2 | 小计 |  |

### Biz\_OrderDetailBatches表

表格 3‑20 Biz\_OrderDetailBatches表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| DetailRecordID | varchar2 | 36 | 记录编号 | PK |
| DetailID | varchar2 | 36 | 明细编号 | FK，参照Biz\_OrderDetails表 |
| BatchID | varchar2 | 36 | 批次编号 | FK，参照Inv\_StockBatches表 |
| DeductQty | number | 10 | 扣减数量 |  |

### Mkt\_CouponRecords表

表格 3‑21 Mkt\_CouponRecords表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| RecordID | varchar2 | 36 | 记录编号 | PK |
| CouponID | varchar2 | 36 | 优惠券编号 | FK，参照Mkt\_Coupons表 |
| CustomerID | varchar2 | 36 | 用户编号 | FK，参照Crm\_Customers表 |
| Status | varchar2 | 20 | 状态 |  |
| UsedTime | date |  | 使用时间 |  |
| OrderID | varchar2 | 36 | 订单编号 | FK，参照Biz\_Orders表 |

### Crm\_PointLogs表

表格 3‑22 Crm\_PointLogs表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| LogID | varchar2 | 36 | 日志编号 | PK |
| CustomerID | varchar2 | 36 | 用户编号 | FK，参照Crm\_Customers表 |
| ChangeType | varchar2 | 20 | 变动类型 |  |
| PointsAmount | number | 10 | 变动积分 |  |
| BalanceAfter | number | 10 | 变动后余额 |  |
| Reason | varchar2 | 200 | 变动原因 |  |
| RelatedOrderID | varchar2 | 36 | 关联订单 |  |

### Fin\_Refunds表

表格 3‑23 Fin\_Refunds表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| RefundID | varchar2 | 36 | 退款编号 | PK |
| OrderID | varchar2 | 36 | 订单编号 | FK，参照Biz\_Orders表 |
| DetailID | varchar2 | 36 | 明细编号 | FK，参照Biz\_OrderDetails表 |
| SupplierID | varchar2 | 36 | 供应商编号 |  |
| RefundQty | number | 10 | 退款数量 |  |
| RefundAmount | number | 10,2 | 退款金额 |  |
| LiabilityType | varchar2 | 50 | 责任类型 |  |
| AuditStatus | varchar2 | 20 | 审核状态 |  |

### Fin\_PaymentRecords表

表格 3‑24 Fin\_PaymentRecords表

|  |  |  |  |  |
| --- | --- | --- | --- | --- |
| 字段名 | 数据类型 | 长度 | 说明 | 备注 |
| PayID | varchar2 | 36 | 支付编号 | PK |
| OrderID | varchar2 | 36 | 订单编号 | FK，参照Biz\_Orders表 |
| PayMethod | varchar2 | 50 | 支付方式 |  |
| TransactionNo | varchar2 | 100 | 交易流水号 |  |
| PayAmount | number | 10,2 | 支付金额 |  |
| Status | varchar2 | 20 | 支付状态 |  |

## 数据库关系图

数据库关系图体现以下特点：

* 所有数据表均定义主键
* 外键约束保证参照完整性
* 订单明细与商品批次通过中间表实现 N:M 关系
* 商品与库存汇总之间为 1:1 关系
* 供应商与商品之间为 1:N 关系
* 消费者与订单之间为 1:N 关系

![](data:image/png;base64...)

图 3‑1 数据库关系图

1. 图表索引

[图 2‑1 总体E-R图 2](#_Toc325730179)

[图 2‑2 \*\*模块E-R图 2](#_Toc325730180)

[图 3‑1 数据库关系图 3](#_Toc325730181)

[表格 3‑1 user表 3](#_Toc325730182)
