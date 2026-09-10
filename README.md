# DB-Fresh-ColdChain-System

数据库课程设计 —— 生鲜冷链团购分销供应链系统。

系统地址/服务器地址：http://111.231.80.220/

包含四大角色界面：**消费者**（C 端商城）、**团长**（分销门户）、**供应商**（供货门户）、**管理员**（后台管理）。

## 快速开始

```powershell
# 后端（在项目根目录执行，监听 http://localhost:5064）
dotnet run --project FreshColdChain.csproj --profile http

# 前端消费者端 SPA（监听 http://localhost:8080/app/）
cd ClientApp
npm install
npm run dev
```

## 文档与数据库

| 内容 | 位置 |
| --- | --- |
| **完整说明**：环境要求、启动步骤、各角色入口、数据库配置、常见问题 | [`doc/README.md`](doc/README.md) |
| **数据库结构**：A/B/C 三组共 34 张表的完整 DDL（不含业务数据） | [`database_schema.sql`](database_schema.sql) |
| **全部文档**：按 A/B/C 组分类归档 | [`doc/`](doc/) |

`doc/` 目录结构：

```
doc/
├── README.md                 # 项目总览与运行说明
├── project_design.md         # 课程设计报告（需求、E-R 图、逻辑设计）
├── 业务设计思路.txt
├── A组-供应商与冷链/          # A 组物流持久化部署与验收
├── B组-订单与消费者/          # B 组需求、API 指南、跨组契约、进度与阶段总结
├── C组-团长与分销/            # C 组模块设计报告与答辩总结
└── 跨组集成/                 # 分支合入与跨组接口修复记录
```
