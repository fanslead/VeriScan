# VeriScan 实施计划

状态：执行中
基线：`VeriScan-CMS-技术方案-v2.md` V2.1-draft

## 1. 交付原则

- 以可运行的纵向切片推进，不先堆满空壳模块。
- PostgreSQL 是业务事实源；Redis 只承担缓存、限流与失效通知，不替代持久化。
- 应用是鉴权、配额、幂等和统计边界；API Key 只是可轮换凭证。
- `review` 是机器审核终态，平台不建设人工复审队列或回写状态机。
- 外部 AI 默认 fail-closed；规则未决内容不得因为 AI 故障被当作通过。
- 后端按领域拆分端点、服务、存储和适配器，单文件保持单一职责。
- 前端以实际任务流组织信息，禁止把技术实现细节暴露给运营用户。
- 每个批次执行：范围审查 → 静态检查 → 测试/构建 → 白名单暂存 → cached diff 审查 → Git 提交。

## 2. Monorepo 边界

```text
apps/api
  Api/                   入口、认证、中间件、Minimal API 端点
  Application/           用例编排、DTO 映射、事务边界
  Domain/                实体、值对象、领域规则
  Infrastructure/        EF Core、Redis、密钥摘要、外部协议适配器

apps/admin
  src/app/               路由、Provider、全局布局
  src/features/          按业务能力垂直拆分
  src/shared/            UI 原语、请求客户端、通用状态

packages/contracts       OpenAPI 生成客户端，不手写第二份服务端契约
tests/backend            API 契约、认证、领域与集成测试
infra                    PostgreSQL、Redis、Keycloak、容器与可观测性
```

领域层不得引用 ASP.NET Core、EF Core、Redis 或供应商 SDK。端点不得直接注入 DbContext。外部 AI 业务层只依赖 `IModerationAiClient`；每个发布配置只选择一种传输实现。

## 3. 分阶段实施

### Batch 0：工程基线

交付：

- 根级 SDK、包管理、格式与质量规则；
- .NET 解决方案、React 工作区、测试入口；
- PostgreSQL/Redis/Keycloak 本地编排和安全的示例环境变量；
- 架构、UX、ADR 和提交边界文档；
- CI 执行 restore、lint、typecheck、test、build。

验收：全新 clone 可按 README 启动依赖并完成空项目构建；仓库无密钥和生成产物。

### Batch 1：应用、API Key 与规则审核主链路

后端：

- 租户、应用、API Key、Key 事件、审核请求/item、规则和命中证据模型；
- Key 创建时仅返回一次明文，服务端保存带版本 pepper 的 HMAC 摘要；
- `X-API-Key` 认证、scope、应用状态、过期和撤销校验；
- 批量审核、请求查询、规则规范化和确定性 `pass/reject/review`；
- RFC 7807、幂等记录、审计和 Outbox 基础。

前端：

- 管理后台框架、应用列表/详情、创建应用；
- API Key 一次性展示、复制确认、轮换和撤销；
- 审核记录列表与详情的脱敏只读视图；
- loading、空状态、错误恢复、危险操作确认和键盘可访问性。

验收：创建应用和 Key 后，使用该 Key 调用批量接口并在后台看到同一应用的结果记录；无效/过期/撤销 Key 均返回同构 401。

### Batch 2：外部 AI 网关

- 配置草稿、合成测试、审批发布、灰度和回滚；
- Chat Completions、Responses、Messages 三种非流式适配器；
- canonical schema、本地严格校验和 provider-specific wire schema；
- 有界重试、超时、熔断、限流、结果未知、安全拒绝和截断映射；
- Provider credential 只保存密钥系统引用；请求 URL 执行 SSRF 防护；
- 模型、Prompt、适配器和 schema 版本完整追溯。

验收：官方 wire fixture、伪服务集成测试、故障映射和发布门禁全部通过；不依赖真实付费 Key 完成 CI。

### Batch 3：统计、缓存与异步处理

- 入口事件、AI invocation、Token/费用、应用和 Key 聚合；
- Outbox 去重消费、小时/日聚合与可重建验证；
- L1/Redis 缓存身份、撤销传播和数据库补偿；
- 异步批次、任务租约、Webhook 签名、重试和 dead-letter；
- OpenTelemetry 指标、Trace、结构化日志和告警面板。

验收：幂等重放不重复调用 AI，不重复计费；统计可由事实事件重建；Redis 故障不绕过鉴权。

### Batch 4：规则、配置与知识治理

- 词库/规则导入、校验、测试、版本发布和回滚；
- AI 路由、阈值、外发策略、预算和应用绑定；
- RAG 快照、成员关系、证据 hash 和可选检索；
- 变更审批、审计、差异预览和配置影响提示。

验收：运行请求绑定不可变版本；配置发布不会改变历史结果的解释口径。

当前落地：已完成词库草稿的批量录入、规范化冲突校验、checksum、不可变发布、复制新版本、受绑定保护的归档、应用显式绑定、请求 `policyId` 一致性校验和历史审核版本追溯。列表使用有限预览，编辑按需读取完整版本。组合规则、正则沙箱、双人审批、Outbox 原子分发、灰度比例和独立评测集仍属于后续交付，不以当前词条校验冒充已经完成。

### Batch 5：生产化与性能验收

- 容器镜像、迁移 Job、健康/就绪探针、备份恢复演练；
- Key 泄露、Provider 故障、超时风暴、缓存穿透和大批次压测；
- WCAG 基础可访问性、响应式、暗色主题和真实浏览器回归；
- 安全扫描、依赖许可、容量模型和运行手册。

验收：以本地/测试环境实测数据填写性能基线，不沿用技术方案中的估算值冒充实测结果。

## 4. Git 提交批次

建议按以下顺序形成可回退提交：

1. `chore(repo): establish monorepo foundation`
2. `feat(api): add applications and api key lifecycle`
3. `feat(api): add deterministic moderation pipeline`
4. `feat(admin): add production admin shell and application flows`
5. `feat(integration): connect admin workflows to api contracts`
6. `feat(ai): add versioned external model gateway`
7. `feat(usage): add metering and operational observability`
8. `chore(release): add deployment and acceptance gates`

实际提交以文件职责和验证证据为准；禁止使用 `git add .` 或 `git add -A`。

## 5. 测试矩阵

| 层级 | 核心证据 |
| --- | --- |
| Domain | 规则归一化、命中组合、阈值边界、终态映射 |
| Application | 幂等并发 owner、Key 生命周期、失败不放行、版本冻结 |
| API | OpenAPI、状态码、Problem Details、Header 鉴权、批量混合结果 |
| Infrastructure | PostgreSQL 约束、迁移、Outbox、Redis 降级、协议 fixture |
| Frontend | 表单校验、一次性 Key、错误恢复、路由权限、空/loading 状态 |
| E2E | 创建应用 → 创建 Key → 调用审核 → 查询记录 → 查看应用统计 |
| Performance | 规则 P95、入口吞吐、Provider 容量、Token/费用和资源曲线 |

## 6. 暂缓但保留边界

- 图片、音频和视频多模态审核；
- 调用方人工复核结果反馈，仅可作为独立反馈数据，不改变 VeriScan 已完成状态；
- 多租户计费结算；
- 生产供应商和真实密钥验收。
