# DB-Fresh-ColdChain-System

本系统为数据库课程设计的"生鲜冷链团购分销供应链系统"。

服务器地址：http://111.231.80.220/

系统包含 **四大角色界面**：消费者（C 端商城）、团长（分销门户）、供应商（供货门户）、管理员（后台管理）。

---

## 一、环境要求

| 组件 | 版本 | 说明 |
| --- | --- | --- |
| .NET SDK | **10.x** | 后端为 `net10.0`，需安装 .NET 10 SDK |
| Node.js | 18+（推荐 20+） | 前端 Vue3 + Vite 构建 |
| npm | 随 Node 安装 | 前端依赖管理 |
| Oracle | 18c / 任意版本 | 数据库，需可访问实例 |

> 当前分支：`dev-groupB`。共享测试库连接串已写入 `appsettings.Development.json`（用户 `COLDCHAIN`），如果端口不通或不可用，请按下文第三节配置你自己的数据库。

---

## 二、快速启动（推荐）

分别在两个终端运行 **后端** 和 **前端**。

### 1. 启动后端（ASP.NET Core）

在项目根目录 `DB-Fresh-ColdChain-System/` 下执行：

```powershell
dotnet run --project FreshColdChain.csproj --profile http
```

- 后端默认监听 **http://localhost:5064**
- 首次运行会自动还原 NuGet 依赖，若报 SDK 版本错误请确认已安装 .NET 10 SDK
- 若报 `AssemblyInfo` 特性重复（CS0579），说明仓库内存在多余的临时 `.cs` 目录被通配符包含，删除后重新 build 即可

### 2. 启动前端（消费者端 SPA，Vue3）

在 `ClientApp/` 目录下执行：

```powershell
cd ClientApp
npm install        # 仅首次需要
npm run dev
```

- 前端开发服务器监听 **http://localhost:8080**，浏览器打开 **http://localhost:8080/app/**
- 开发模式下 `/api` 请求会自动代理到后端

> **端口说明**：后端默认监听 `5064`（见 `Properties/launchSettings.json`），前端 `ClientApp/vite.config.js` 的 `/api` 代理已同步指向 `http://localhost:5064`。若改动后端端口，请同步修改 vite 代理后重启前端。

---

## 三、数据库配置

### 1. 初始化表结构与数据

全新 schema 执行 `groupB_ddl.sql`，其中包含 B 组 8 张核心表、1 张会员定级历史扩展表及最小演示数据。已有 schema 不要重复执行基础脚本，应按文件名顺序执行 `migrations/` 中尚未应用的 B 组增量脚本；其中 `20260831_add_coupon_type.sql` 补齐结算所需的优惠券类型列，`20260902_add_freight_quote_snapshot.sql` 增加结构化运费报价快照列。

### 2. 连接串配置

连接串已写入 `appsettings.Development.json`：

```json
"ConnectionStrings": {
  "OracleConnection": "User Id=COLDCHAIN;Password=Oracle123;Data Source=<host>:1521/XE",
  "OracleDb": "User Id=COLDCHAIN;Password=Oracle123;Data Source=<host>:1521/XE"
}
```

若需在运行时覆盖，可设置环境变量（Linux/macOS 用 `export`，PowerShell 用 `$env:`）：

```powershell
$env:ConnectionStrings__OracleConnection = "User Id=xxx;Password=xxx;Data Source=host:1521/XE"
$env:ConnectionStrings__OracleDb = "User Id=xxx;Password=xxx;Data Source=host:1521/XE"
dotnet run --project FreshColdChain.csproj --profile http
```

> 没有可用的数据库时后端也能启动（仅巡检服务会记日志），但登录、下单等依赖数据库的功能无法使用。

---

## 四、各角色界面与访问方式

系统提供四个角色入口，总入口为 **角色选择页**：

- 地址：`http://localhost:5064/Account/RoleSelect`
- 从后端首页（`http://localhost:5064/`）也可进入

### 1. 消费者界面（C 端商城，Vue SPA）

| 项目 | 说明 |
| --- | --- |
| 访问地址 | `http://localhost:8080/app/`（前端 dev 模式）或 `http://localhost:5064/app/`（生产构建产物） |
| 登录/注册 | `http://localhost:8080/app/auth` |
| 功能 | 商品浏览/搜索、关注团长、购物车、结算下单、订单跟踪、退款、优惠券、积分、会员等级、消息中心 |
| 演示账号 | 手机号 `13800138000`，密码 `FreshB2026!`（数据在 `groupB_ddl.sql` 中） |
| 说明 | 消费者为独立 SPA，依赖后端 API；未登录时访问购物车/订单等页面会自动跳转登录 |

### 2. 团长界面（分销门户，MVC Razor）

| 项目 | 说明 |
| --- | --- |
| 登录入口 | 角色选择页 → 「团长登入」→ 或直达 `http://localhost:5064/Account/Login?role=团长` |
| 登录后首页 | `http://localhost:5064/Promoters/Dashboard` |
| 主要页面 | 业绩总览、佣金明细、提现申请、商品上架（商品入团/出团、设置售价）、团内消费者、个人资料 |
| 注册 | 登录页点击「点击注册新账户」，可注册新团长账号（需管理员审核） |
| 说明 | 未登录访问团长远页面会自动跳转到团长登录页 |

### 3. 供应商界面（供货门户，MVC Razor）

| 项目 | 说明 |
| --- | --- |
| 登录入口 | 角色选择页 → 「供应商登入」→ 或直达 `http://localhost:5064/Account/Login?role=供应商` |
| 登录后首页 | `http://localhost:5064/SuppliersHome` |
| 主要页面 | 履约工作台（`/SupplierFulfillment`）、产品与库存管理（`/Products`）、供应商报价管理（`/Suppliers/MyQuotes`，设置供货价与保质期） |
| 说明 | 供应商账号由后台/数据初始化，供应商登录后维护自己产品的供货价 |

### 4. 管理员界面（后台管理，MVC Razor）

| 项目 | 说明 |
| --- | --- |
| 登录入口 | 角色选择页 → 「管理员登入」→ 或直达 `http://localhost:5064/Account/Login?role=管理员` |
| 登录后首页 | `http://localhost:5064/Admins/Dashboard` |
| 主要页面 | 待审核团长、提现审核、退款审核、佣金/支付/结算管理等 |
| 注册 | 登录页点击「点击注册新账户」可注册管理员（生产环境应限制） |

---

## 五、常用命令速查

```powershell
# 后端
dotnet build FreshColdChain.csproj            # 编译
dotnet run --project FreshColdChain.csproj --profile http   # 运行（端口 5064）

# 前端
cd ClientApp
npm install                                    # 安装依赖
npm run dev                                    # 开发模式（端口 8080）
npm run build                                  # 生产构建，产物输出到 wwwroot/app
npm run preview                                # 预览生产构建

# 测试（独立场景测试，不连真实数据库）
dotnet run --project tests/FreshColdChain.Tests/FreshColdChain.Tests.csproj --no-restore
```

---

## 六、目录结构说明

```
DB-Fresh-ColdChain-System/
├── Controllers/          # MVC + API 控制器（Account / Promoters / Suppliers / Admins / Api 等）
├── Views/                # 团长、供应商、管理员、角色选择等 Razor 视图
├── ClientApp/            # 消费者端 Vue3 + Vite SPA（dev 端口 8080，产物输出 wwwroot/app）
├── Services/             # 业务服务层（订单、客户、优惠券、佣金、结算等）
├── Repositories/         # Dapper 数据访问层
├── Interfaces/           # 服务/仓库接口
├── Models/               # 实体、DTO、ViewModel
├── migrations/           # 增量 SQL 迁移脚本
├── groupB_ddl.sql        # B 组建表 + 演示数据脚本
├── doc/                  # 设计/进度/API 文档（含 GroupB-api-guide.md 演示账号说明）
└── tests/                # 自动化场景测试
```

---

## 七、常见问题

| 问题 | 解决方式 |
| --- | --- |
| 前端打开 `8080/app/` 但接口报 500/连不上 | 确认后端已启动且端口与 `vite.config.js` 代理一致（5213 或 5064，见上文"端口说明"） |
| 登录提示数据库错误 | 检查 Oracle 是否可访问、连接串是否正确、`groupB_ddl.sql` 是否已执行 |
| `dotnet run` 报缺少 .NET SDK | 安装 .NET 10 SDK，`dotnet --list-sdks` 确认 |
| 后端启动后 `/app/` 404 | 先 `cd ClientApp && npm run build` 生成前端产物，或直接使用前端 dev 模式 `8080/app/` |
| 团长/管理员注册后无法登录 | 注册的团长需要管理员在后台审核通过；管理员账号按需由数据初始化 |
