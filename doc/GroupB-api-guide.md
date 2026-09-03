# GroupB REST API 使用说明

更新日期：2026-09-02

## 1. 通用约定

- B 组内部及跨组 ID 均以 JSON 字符串传递，最大长度 36；不得转为 JavaScript `Number`。
- 订单状态在响应体中使用：`PENDING_PAYMENT`、`PAID`、`SHIPPED`、`COMPLETED`、`CANCELLED`、`REFUNDING`、`REFUNDED`；请求（Query/Body）中使用 `OrderStatus` 枚举名（如 `Paid` / `Shipped`），且不接受整数枚举。
- 业务失败返回 `400`，资源不存在返回 `404`；错误体统一包含 `message` 和 `traceId`。
- 演示消费者：`13800138000` / `FreshB2026!`，ID 为 `10000000000000000000000000000001`。

错误响应示例：

```json
{
  "message": "手机号或密码错误",
  "traceId": "0HN..."
}
```

## 2. 注册与登录

### `POST /api/auth/customer/register`

```json
{
  "customerName": "张三",
  "phone": "13900139000",
  "email": "zhangsan@example.com",
  "password": "SafePass123!",
  "confirmPassword": "SafePass123!"
}
```

成功返回 `201`：

```json
{
  "customerId": "bdf7c23641fd4c48a7052158df1e17e2"
}
```

### `POST /api/auth/customer/login`

```json
{
  "phone": "13900139000",
  "password": "SafePass123!"
}
```

```json
{
  "customerId": "bdf7c23641fd4c48a7052158df1e17e2",
  "customerName": "张三",
  "phone": "13900139000"
}
```

登录接口只完成凭据校验并返回消费者身份，不签发 Token；部署环境应由统一认证层补充 Cookie/JWT 会话。

## 3. 消费者与地址

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `/api/customers/{customerId}` | 查询消费者、等级和地址 |
| `PUT` | `/api/customers/{customerId}` | 更新姓名、手机和邮箱 |
| `GET` | `/api/customers/{customerId}/addresses` | 查询地址列表 |
| `POST` | `/api/customers/{customerId}/addresses` | 新增地址 |
| `PUT` | `/api/customers/{customerId}/addresses/{addressId}` | 编辑地址 |
| `DELETE` | `/api/customers/{customerId}/addresses/{addressId}` | 删除地址 |
| `PUT` | `/api/customers/{customerId}/addresses/{addressId}/default` | 设为默认地址 |

新增地址请求示例：

```json
{
  "addressId": null,
  "customerId": "10000000000000000000000000000001",
  "receiverName": "张三",
  "phone": "13900139000",
  "province": "浙江省",
  "city": "杭州市",
  "district": "西湖区",
  "detailAddress": "文三路1号",
  "isDefault": true
}
```

## 4. 优惠券

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `/api/customers/{customerId}/coupons` | 查询可领取券和已领取可用券 |
| `POST` | `/api/customers/{customerId}/coupons/{couponId}/claim` | 原子领券 |

每位消费者对同一券模板限领一张。服务端会在同一事务中锁定消费者和券模板、检查活动窗口与库存、扣减库存并写入用户券。

## 5. 订单

### `POST /api/orders`

客户端只提交 ID 和数量，商品名、价格、供应商、运费、优惠金额、积分和订单状态由服务端确定。

```json
{
  "customerId": "10000000000000000000000000000001",
  "addressId": "20000000000000000000000000000001",
  "couponRecordId": "40000000000000000000000000000001",
  "items": [
    { "productId": "P1", "quantity": 2 }
  ]
}
```

成功返回 `201`：

```json
{
  "orderId": "7a535f84c5d3457496c4d6d49c27da7d",
  "orderNo": "ORD2026080810300012345678901234",
  "goodsAmount": 100.00,
  "discountAmount": 20.00,
  "freightAmount": 0.00,
  "freightQuote": {
    "schemaVersion": 1,
    "freightAmount": 0.00,
    "province": "浙江省",
    "city": "杭州市",
    "district": "西湖区",
    "ruleSummary": "按地区、温层、首重和续重计算",
    "dataSource": "GROUP_A"
  },
  "finalAmount": 80.00,
  "pointsEarned": 8
}
```

最终下单时，B 组会把结构化运费结果保存为 `Biz_Orders.FreightQuoteSnapshot`。
快照用于审计和展示，不用于重新执行 A 组运费算法。

其他订单接口：

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `/api/orders?customerId=...&status=PAID&page=1&pageSize=10` | 分页查询 |
| `GET` | `/api/orders/{orderId}` | 查询订单详情和供应商履约单元 |
| `POST` | `/api/orders/{orderId}/cancel` | 取消未发货订单并补偿库存、券、积分和累计消费 |
| `POST` | `/api/orders/{orderId}/items/{orderDetailId}/confirm-receipt` | 消费者确认本人订单中的单件商品收货 |

消费者 API 不提供通用订单状态流转入口。发货只能由供应商履约入口触发；订单完成由消费者逐项确认收货后自动判定。
单件确认收货按钮仅在对应包裹物流状态为 `DELIVERED` 时开放。

`GET /api/orders/{orderId}` 的物流部分示例：

```json
{
  "packages": [
    {
      "packageNumber": 1,
      "subTotal": 100.00,
      "itemIds": ["order-detail-id"],
      "logistics": {
        "carrierCode": "SF",
        "carrierName": "顺丰冷运",
        "trackingNo": "SF1234567890",
        "packageTemperature": "CHILLED",
        "statusCode": "IN_TRANSIT",
        "statusName": "运输中",
        "estimatedArrivalAt": "2026-09-03T18:00:00+08:00",
        "hasException": false,
        "exceptionMessage": null,
        "dataSource": "GROUP_A",
        "isFallback": false,
        "events": [
          {
            "eventId": "event-id",
            "statusCode": "IN_TRANSIT",
            "statusName": "运输中",
            "location": "杭州分拨中心",
            "description": "冷链运输中",
            "occurredAt": "2026-09-02T15:00:00+08:00",
            "temperatureCelsius": 3.5,
            "isTemperatureException": false
          }
        ]
      }
    }
  ],
  "freightQuote": {
    "schemaVersion": 1,
    "freightAmount": 12.00,
    "ruleSummary": "按地区、温层、首重和续重计算",
    "dataSource": "GROUP_A"
  }
}
```

`packages` 为消费者展示模型，不返回供应商标识。`dataSource=FALLBACK` 表示 A 组高级物流接口尚未接入，数据不能视为生产持久化结果。

## 6. 供应商履约页面

供应商通过统一登录建立 `SupplierId` 会话后访问：

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `/SupplierFulfillment` | 查询本人履约订单，支持状态、关键词和分页 |
| `GET` | `/SupplierFulfillment/Detail/{orderId}` | 查看本人负责的订单商品和物流状态 |
| `POST` | `/SupplierFulfillment/Ship/{orderId}` | 为本人订单登记发货并调用 A 组冷链接口 |
| `POST` | `/SupplierFulfillment/AddTrackingEvent/{orderId}` | 为本人已发货包裹追加状态、地点、描述和温度 |

发货表单中的 `SupplierId` 不作为可信身份，Controller 始终使用当前会话供应商覆盖该值。
多供应商订单在全部供应商都完成发货后才更新为 `SHIPPED`。

允许的主要物流状态流：

```text
SHIPPED → IN_TRANSIT → OUT_FOR_DELIVERY → DELIVERED
                   ↘ EXCEPTION → IN_TRANSIT / RETURNING
SHIPPED / IN_TRANSIT / OUT_FOR_DELIVERY → RETURNING → RETURNED
```

温度越界或超过预计送达时间会产生 `EXCEPTION`。当前高级轨迹来源为 `FALLBACK` 时，
数据只在进程内保存；正式环境应替换为 A 组持久化 Provider。

## 7. 数据库初始化与自检

1. 在隔离 Oracle 18c schema 执行 `groupB_ddl.sql`。
2. 运行 `pwsh -NoProfile -File tests/verify-groupb-ddl.ps1` 做静态职责和种子覆盖检查。
3. 使用环境变量 `ConnectionStrings__OracleConnection` 提供连接字符串。
4. 使用演示账号登录并验证地址、领券、下单、发货、完成和退款流程。
