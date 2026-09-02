# Webhook 接入与投递

## 1. 适用范围

Webhook 用于通知调用方“异步审核批次已经进入终态”，不携带审核原文、规则证据、供应商密钥或完整条目结果。调用方收到通知后，应使用自己的应用 API Key 查询 `statusUrl` 获取权威结果。

只有以下请求会产生业务 Webhook：

- `mode=async`；
- `mode=auto` 且实际进入异步队列。

`mode=sync` 和最终选择同步执行的 `auto` 请求不会产生业务 Webhook。连接测试使用独立的 `webhook.test` 事件，不受业务通知开关影响。

## 2. 异步处理与投递流程

```text
提交批次
  ├─ 校验应用 API Key、Idempotency-Key、规则版本和请求体
  ├─ 规则预判并决定 sync / async
  └─ async: 同事务写入审核批次和处理任务，返回 HTTP 202
         ↓
审核 Worker 以 PostgreSQL 租约领取任务
  ├─ 执行需要的外部 AI 调用
  ├─ 保存批次终态和 Outbox 事实
  └─ Webhook 已启用时，同事务写入 webhook_publications
         ↓
Webhook Worker 以 SKIP LOCKED 领取发布记录
  └─ 使用稳定 eventId / Idempotency-Key 提交到 Svix
         ↓
Svix 签名并向应用 HTTPS 地址投递、退避重试和记录尝试
         ↓
接收方验签，以 eventId 或 svix-id 去重，查询 statusUrl
```

取消尚未开始的异步批次时，取消请求必须提供独立的 `Idempotency-Key`。取消成功会形成 `moderation.cancelled` 终态；同一个取消键可安全重放，但不能跨批次复用。

## 3. 应用配置接口

这些接口使用管理后台 JWT。读取要求 Viewer，写入、测试和密钥轮换要求 Operator。

| 操作     | 方法与路径                                                   | 关键语义                                     |
| -------- | ------------------------------------------------------------ | -------------------------------------------- |
| 查询配置 | `GET /api/admin/v1/applications/{id}/webhook`                | 不返回签名密钥和 Svix 内部标识               |
| 保存地址 | `PUT /api/admin/v1/applications/{id}/webhook`                | 首次配置或地址变化时仅本次响应返回新密钥     |
| 启停通知 | `PATCH /api/admin/v1/applications/{id}/webhook`              | 当前配置版本测试成功后才允许启用             |
| 发起测试 | `POST /api/admin/v1/applications/{id}/webhook/tests`         | 返回 HTTP 202、`testId` 和 `statusUrl`       |
| 查询测试 | `GET /api/admin/v1/applications/{id}/webhook/tests/{testId}` | 状态为 `pending/delivering/succeeded/failed` |
| 轮换密钥 | `POST /api/admin/v1/applications/{id}/webhook/secret/rotate` | 仅本次响应返回新密钥，并自动停用通知         |

推荐操作顺序：保存地址并保存一次性密钥 → 发起真实连接测试 → 等待测试成功 → 启用通知。

接收地址必须是公开可访问的 DNS HTTPS URL，最长 2048 字符，不允许凭据、查询参数、片段、本机名、内部域名或 IP 字面量。VeriScan 先做输入校验，Svix 在实际解析和连接时继续执行 SSRF 防护与 TLS 校验。

修改地址会创建或恢复地址版本对应的独立 Svix 端点，立即轮换并返回新的签名密钥，同时使旧测试结果失效。每个端点绑定自己的 Channel，发布消息时只指定当前配置记录保存的端点 Channel；这样已经进入投递或重试的旧事件仍发送到旧地址，新事件也不会广播给历史端点。轮换密钥默认保留 24 小时旧密钥验证宽限，但通知仍会立即停用，必须更新接收方、重新测试后再启用。

## 4. 事件契约

当前 schema 版本为 `1.0`，事件类型如下：

- `webhook.test`
- `moderation.completed`
- `moderation.failed`
- `moderation.cancelled`

业务终态事件示例：

```json
{
  "schemaVersion": "1.0",
  "eventId": "019c9f23-ef56-7d17-8df5-cba19fd83765",
  "eventType": "moderation.completed",
  "occurredAt": "2026-09-02T08:00:00Z",
  "data": {
    "applicationId": "8f68a4cc-42ca-4e2f-a1fc-5f62c0a76015",
    "requestId": "019c9f23-b8ac-7d07-9a4e-8c4924a9e307",
    "processingStatus": "completed",
    "statusUrl": "/api/v1/moderation/batches/019c9f23-b8ac-7d07-9a4e-8c4924a9e307",
    "submittedAt": "2026-09-02T07:59:58Z",
    "finalizedAt": "2026-09-02T08:00:00Z",
    "summary": {
      "itemCount": 4,
      "passCount": 2,
      "rejectCount": 1,
      "reviewCount": 1,
      "failedCount": 0,
      "cancelledCount": 0
    }
  }
}
```

`processingStatus` 取值为 `completed`、`completed_with_errors`、`failed` 或 `cancelled`。接收方不能仅凭 `summary` 代替结果查询，也不能假设未知字段永远不会增加；应按 `schemaVersion` 解析并忽略不认识的兼容字段。

## 5. 签名验证与接收方幂等

Svix 使用 `svix-id`、`svix-timestamp`、`svix-signature` 请求头。接收方必须使用原始请求体和官方 Svix/Standard Webhooks 库验签，在验签成功后再反序列化 JSON。不要先解析再重新序列化，否则字节变化会导致签名验证失败。

投递语义为至少一次：

- VeriScan 本地发布记录通过唯一业务去重键避免重复入队；
- 向 Svix 提交时，发布记录 ID 同时作为稳定 `eventId` 和供应商 `Idempotency-Key`；
- 网络超时可能使发送方无法确认接收结果，接收方仍必须用 `eventId` 或 `svix-id` 建立唯一约束；
- 只有在业务处理已可靠落库后才返回 2xx；临时失败返回非 2xx 让 Svix 按计划重试。

不要把签名密钥写入浏览器持久存储、日志、代码库或普通配置表。密钥只在首次保存地址、地址变化和主动轮换的响应中出现一次。

## 6. 启停、故障和恢复边界

- 停用开关只阻止新的业务终态进入 Webhook 队列，不撤回已入队或已提交 Svix 的事件。
- Svix 不可用不会阻塞审核提交、执行或查询；本地 Worker 会有界重试，达到上限后保留死信状态供排查。
- 连接测试走与正式通知相同的供应商和网络路径，但一次成功不代表未来持续可用，也不替代生产告警和故障演练。
- Svix 负责目标地址的网络投递重试；VeriScan 不在其外层再次重放已被 Svix 接受的消息，避免形成叠加重试风暴。
- 接收方应监控验签失败、非 2xx、处理延迟和重复事件，并为 `statusUrl` 查询设置自己的重试与限流策略。

本地 Svix 部署、健康检查和凭据生成见 [`infra/README.md`](../infra/README.md)。
