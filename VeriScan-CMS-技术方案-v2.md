# 明鉴智能内容审核系统（VeriScan CMS）技术方案 V2.1

> 文档状态：详细设计评审稿
> 版本：2.1-draft
> 日期：2026-09-01
> 适用范围：文本内容审核；图片、音频和视频不在首期范围内

## 0. 执行摘要

### 0.1 建设目标

VeriScan 面向业务系统提供可审计、可追溯、可降级的批量文本审核能力。系统采用“本地快筛 + 外部 AI 精判 + 调用方复核”的分层模式：

1. 一级快速检测在本地执行规范化、确定性规则和路由判定；
2. 所有未被可信硬规则终结且未命中完整结果缓存的内容，默认进入外部 AI 语义审核；
3. 模型争议或策略要求复核时返回终态 `review`，由调用方自行复核；VeriScan 不承载人工审核流程，只记录机器结果；
4. 调用方按“租户 → 应用 → API Key”认证、限流、授权和统计；Key 可以轮换，应用是稳定的数据归属边界；
5. PostgreSQL 是事实源，Redis 仅承担缓存、通知和短期加速，不作为审核事实或唯一任务队列。

### 0.2 关键架构决策

| 编号 | 决策 | 说明 |
| --- | --- | --- |
| ADR-001 | 不把“规则未命中”视为安全 | 未被硬规则终结的内容默认必须进入外部 AI；如果应用策略禁止数据外发，则返回 `review` 或明确错误，不能自动 `pass` |
| ADR-002 | 使用统一 AI 网关隔离供应商协议 | 业务层只依赖统一审核契约；网关适配 OpenAI Chat Completions、OpenAI Responses 和 Anthropic Messages 三类协议 |
| ADR-003 | 模型配置版本化发布 | 请求 URL、协议、模型、Prompt、输出模式、超时、限额和凭证引用形成不可变配置版本；调用方不能在审核请求中指定任意模型或 URL |
| ADR-004 | 规则信号与模型标签分离 | 规则权重属于策略信号；外部生成式模型的标签不是概率，只有另行校准后才能称为 `risk_score` |
| ADR-005 | AI 不可用时禁止静默放行 | 超时、配额耗尽、上游错误、输出非法和未知语言进行异步重试，最终返回系统错误；只有应用显式配置 `review_on_ai_failure` 时才可降级为带原因和 `degraded=true` 的 `review` |
| ADR-006 | 应用是调用与统计边界 | 每个应用可以拥有多个独立、可轮换 API Key；幂等、配额、数据统计和历史查询按 `application_id` 聚合，不依赖某一把 Key |
| ADR-007 | 外部 AI 是新的数据安全边界 | 每个租户/应用必须显式批准可用供应商、地域、模型、数据分类和保留策略；不允许未经审批的数据外发或跨地域故障切换 |
| ADR-008 | RAG 默认关闭 | 只有消融评测证明质量收益大于延迟和攻击面后，才按租户、地区和策略版本开启 |
| ADR-009 | Native AOT 为可选部署优化 | 首版使用 CoreCLR；完成所有依赖的 AOT/裁剪兼容验证后再启用 |
| ADR-010 | 平台不承载人工复审 | `review` 是已经完成的机器结果；系统不创建复审任务、不分配审核员、不等待人工回写，只保存和返回结果 |
| ADR-011 | 多租户字段从首期预留 | 数据、缓存、审计和索引必须同时包含租户与应用边界，不依赖全局共享命名空间 |

### 0.3 成功标准

系统是否成功不以单一“准确率”或平均延迟判断，而以以下指标联合验收：

- 关键违规类别召回率、自动拒绝精确率和安全内容误杀率；
- `review` 返回率、来源分布和调用方可识别的原因码；
- 单条与整批请求的 P50/P95/P99；
- `item/s`、`request/s`、AI 路由率、供应商 RPM/TPM、并发和预算利用率；
- AI 输出解析失败率、上游错误率、重试/切换率和依赖故障时的错误放行数；
- 每条决定能否追溯到租户、应用、API Key、策略、规则、AI 配置、Prompt 和知识版本；
- 能否按应用稳定统计请求量、item 数、决定分布、延迟、Token、估算费用和错误，而不泄露完整 Key 或原文。

### 0.4 适用性判断

改用外部 AI 对首期是合理选择：它显著降低模型转换、GPU、量化和推理集群运维成本，并允许按业务评测快速切换供应商。但它不是无条件优于本地模型，系统会新增数据外发、供应商可用性、网络尾延迟、配额和按 Token 付费风险。

满足以下条件时建议采用本方案：业务允许经审批后向指定供应商外发内容；同步延迟可以接受公网模型调用；已获得与目标吞吐匹配的 RPM/TPM/并发和费用预算。若内容不得离域、要求离线可用，或需要长期高吞吐且外部调用成本不可接受，则应重新评估私有部署，而不能靠隐藏重试或“未命中即通过”弥补。

领域层保留 `IModerationAiClient` 边界，使未来可新增私有模型适配器，但首期只实现外部协议，避免同时维护两套推理体系。

## 1. 项目范围

### 1.1 核心能力

- 同步小批量文本审核；
- 异步大批量文本审核；
- 规则与外部 AI 的策略编排，以及 `pass/reject/review` 结果返回；
- 词库、正则、阈值、AI 供应商、模型、Prompt 和知识库的版本化管理；
- 审核记录、证据片段和配置变更审计；
- 运营看板、规则/AI 配置管理和数据导出；
- 应用创建、API Key 创建/轮换/撤销、应用级配额、告警和使用统计；
- 多租户隔离、数据外发策略和供应商成本治理。

### 1.2 首期非目标

- 不支持图片、音频、视频的原生理解；
- 不建设本地大模型推理集群或自动化训练平台；离线标注和调用方可选反馈只进入候选评测集，不能自动修改线上 Prompt 或模型配置；
- 不把 RAG 作为规则事实源或唯一判定器；
- 不提供通用大模型代理接口；外部 AI 只能执行服务端发布的审核 Prompt，调用方不能透传 Prompt、工具、供应商参数或请求 URL；
- 不建设人工复审队列、审核员工作台、领取/SLA/回写状态机；调用方收到 `review` 后自行处理；
- 不在首期引入 Kafka、RabbitMQ 等额外消息中间件；需要持久任务时使用 PostgreSQL Job/Outbox；
- 不承诺任何未在目标硬件和冻结数据集上验证过的吞吐或准确率数字。

## 2. 总体架构

### 2.1 逻辑架构

```text
业务服务端调用方
   │
   │ X-API-Key
   ▼
接入层：API Key 认证、租户/应用解析、限流、幂等、输入校验、OpenAPI
   │
   ▼
检测编排层
   ├─ 精确结果缓存（带完整版本）
   ├─ 一级快速检测
   │    ├─ Unicode 规范化与原文位置映射
   │    ├─ AC 词库 / 安全正则 / 组合规则
   │    └─ 硬规则终结 / 外部 AI 路由
   │
   ├─ 外部 AI 网关
   │    ├─ Chat Completions / Responses / Messages 协议适配器
   │    ├─ 模型路由、凭证引用、超时、重试、熔断和限额
   │    ├─ 可选 RAG 政策证据
   │    └─ 严格输出解析与故障降级
   │
   └─ 结果聚合、记录落库、Webhook 通知
   │
   ├─ PostgreSQL：应用、Key 摘要、配置版本、任务、记录、统计、审计
   └─ Redis：非权威缓存、限流、版本通知、单飞锁

外部边界：仅允许访问已审批 HTTPS 供应商域名；AI 凭证由服务端密钥管理系统提供
```

### 2.2 部署组件

| 组件 | 主要职责 | 扩缩容方式 |
| --- | --- | --- |
| `veriscan-api` | Minimal API、API Key 认证、幂等、规则快筛、同步审核、请求查询 | CPU 横向扩展 |
| `veriscan-worker` | 异步 AI 调用、Outbox、Webhook、统计聚合和规则快照 | CPU/网络横向扩展 |
| `veriscan-ai-gateway` | 协议适配、模型路由、凭证注入、出站安全、重试/熔断、结果归一化 | 可独立部署；按连接、并发和供应商配额扩展 |
| `veriscan-admin-web` | React 管理后台静态资源 | CDN/静态托管 |
| PostgreSQL | 所有权威业务数据、任务、配置和可选 pgvector | 主库 + 备库；一致性敏感读走主库 |
| Redis | 缓存、限流、通知 | 可丢失后重建，不承载唯一事实 |

### 2.3 技术栈

| 类别 | 选型 | 约束 |
| --- | --- | --- |
| API | ASP.NET Core 10 Minimal API | 使用内置 OpenAPI、Problem Details、Rate Limiting；不混用 MVC Controller |
| 运行时 | .NET 10 CoreCLR | Native AOT 通过兼容性 Gate 后可选 |
| ORM | EF Core 10 + Npgsql | 管理与事务路径默认 EF；高吞吐追加写入可在压测后使用 Npgsql Batch/COPY |
| 词匹配 | `ToolGood.Words` 3.x 或经验证的内部 AC 实现 | 禁用已废弃的 `ToolGood.Words.Core`；锁定精确版本并验证线程安全/AOT |
| 正则 | .NET Regex | 优先 `NonBacktracking`；强制输入上限、匹配超时和发布前测试 |
| AI 抽象 | 项目领域接口 + `Microsoft.Extensions.AI` | 单次 Prompt→结构化响应；业务层不依赖供应商 DTO，不引入 Agent Framework |
| AI 协议适配 | 官方 SDK 或基于 `IHttpClientFactory` 的 typed client | 同一配置版本只允许一条传输实现；支持 Chat Completions、Responses、Messages |
| 凭证 | KMS/Vault/Secrets Manager 引用 | 数据库只保存 `secret_ref` 和版本，不保存可回显明文 |
| Embedding | 首期不启用；RAG 阶段另设外部 Embeddings 适配器 | 不能把生成协议错误复用为向量协议；维度、模型和快照必须版本化 |
| 数据库 | PostgreSQL 16+ | 所有时间使用 `timestamptz`，高量表按时间分区 |
| 向量扩展 | pgvector 0.8+ | HNSW + iterative scan；精确版本写入镜像和 SBOM |
| 缓存 | IMemoryCache + Redis 7+ | 必须配置内存上限；Redis 不改变判定语义 |
| 前端 | React 19 + TypeScript + Semi Design | TanStack Query 负责服务器状态；Zustand 仅存客户端 UI 状态 |
| 可观测性 | OpenTelemetry + 结构化日志 | 禁止记录原文、完整 Prompt、密钥和证件号 |

所有 NuGet、npm、容器镜像、SDK 和数据库扩展都必须锁定精确版本或 digest，不允许生产环境使用 `latest`。外部模型标识优先固定到供应商提供的稳定快照；若只能使用浮动别名，必须增强持续评测、漂移告警和回滚路由。

## 3. 审核语义与状态模型

### 3.1 分离处理状态和业务决定

```text
ProcessingStatus:
accepted | processing | retry_wait |
completed | completed_with_errors | failed | cancelled

Decision:
pass | reject | review | null
```

- `decision=review` 是 VeriScan 的终态机器结论，表示调用方需要在自己的业务流程中人工复核；系统不等待复核结果；
- `decision=null` 仅用于请求尚未完成或发生系统错误；
- 不得把 `failed`、超时、未知语言或解析失败映射为 `pass`；
- VeriScan 不接受人工决定回写；调用方如需记录其最终处理结果，应存储在自身业务系统。

Item 合法状态转移：

```text
accepted → processing
processing → completed(pass|reject|review)
processing → retry_wait → processing
processing → failed
accepted/retry_wait → cancelled
```

`completed`、`completed_with_errors`、`failed`、`cancelled` 是终态。Webhook 的 `pending/delivered/failed` 是独立投递状态，不进入审核 item 状态机。

批次根状态按子项聚合：存在 `accepted/processing/retry_wait` 时为 `processing`；全部子项为 `completed` 时为 `completed`，无论其中决定是 `pass/reject/review`；同时含 `completed` 与 `failed/cancelled` 时为 `completed_with_errors`；全部取消时为 `cancelled`；没有完成结果且至少一个 item 为 `failed`、其余为 `failed/cancelled` 时为 `failed`。

### 3.2 评分语义

系统不再使用一个任意叠加的全局“置信度”。内部保留三类值：

| 值 | 含义 | 是否可用于概率阈值 |
| --- | --- | --- |
| `rule_signal` | 规则命中、权重、范围、动作和证据 | 否 |
| `category_risk_score` | 对外部 AI 标签另行校准后的分类别风险概率 | 是 |
| `ai_label` | 外部模型归一化后的 `safe/unsafe/review` 及类别 | 否，除非另有校准层 |

面向调用方可提供 `risk_score`，但必须同时返回 `score_source` 和 `calibration_version`。若结果来自生成式标签或硬规则，`risk_score` 可以为 `null`。

### 3.3 策略优先级

1. 合法且已发布的硬拒绝规则可以直接 `reject`；
2. 白名单只能抑制指定规则或指定上下文，不能全局抵消硬拒绝、其他类别或模型风险；
3. 普通规则命中转化为类别信号，不直接伪装成概率；
4. 未被硬规则终结的内容默认进入已发布的外部 AI 路由；禁止仅因“没有命中规则”而通过；
5. AI 网关把供应商响应归一为严格标签；模型标签不能推翻硬拒绝；
6. 策略把本地规则证据、AI 标签和可选校准分数映射为 `pass/reject/review`；
7. 高风险类别可以配置为即使 AI 判定明确也返回 `review`，由调用方复核；
8. 应用禁止外发、供应商不可用或输出非法时，按已发布的失败策略异步重试、返回错误，或显式降级为 `review`，绝不自动通过。

示例半开区间：

```text
hard_reject rule                       → reject
valid AI label = safe                  → pass（仅在策略允许时）
valid AI label = unsafe                → reject 或 review
valid AI label = review/unknown        → review
AI unavailable/invalid                 → retry/review/error
```

阈值按 `tenant + policy + category + calibration_version` 管理，不再使用全局固定的 0.35/0.85。

### 3.4 故障矩阵

| 故障 | 同步接口行为 | 异步接口行为 | 是否允许通过 |
| --- | --- | --- | --- |
| Redis 不可用 | 绕过缓存和 Redis 限流降级，使用本地保护 | 继续，以 PostgreSQL 为准 | 可以，前提是完整审核成功 |
| PostgreSQL 权威写入不可用 | 返回 `503`/item `system_error` | 保持任务失败并重试 | 否 |
| 规则快照未就绪或校验失败 | 实例 readiness 失败 | 不接收新任务 | 否 |
| 应用 Key 无效、过期或撤销 | `401` | 不创建任务 | 否 |
| 应用被停用或无策略权限 | `403` | 不创建任务 | 否 |
| 外部 AI 连接/读取超时 | 默认 item `system_error`；配置 `review_on_ai_failure` 时返回降级 `review` | 有界重试后同同步最终语义 | 否 |
| 上游 `429/5xx` 或熔断开启 | 遵守 `Retry-After`；同步不做超过 deadline 的重试 | 带抖动指数退避，达到上限进入 item error/dead-letter 或降级 `review` | 否 |
| 外部 AI 输出非法/不完整 | 默认 item `invalid_ai_output`；可切换获批备用路由 | 达到上限后 item error 或降级 `review` | 否 |
| 数据外发策略不允许 | 显式配置时返回 `review`，否则 `422 policy_external_ai_denied` | 同左 | 否 |
| RAG 不可用 | 按策略退化为无 RAG AI 审核；否则 item error 或降级 `review` | 同左 | 不能因为 RAG 失败而通过 |
| Webhook 失败 | 审核结果仍有效，记录通知失败 | 指数退避重试 | 不影响已完成决定 |

## 4. 一级快速检测

### 4.1 文本规范化

规范化按版本执行，至少覆盖：

- Unicode NFKC；
- 全角/半角、大小写和常见空白统一；
- 零宽字符、控制字符和异常分隔符处理；
- 繁简转换、常见形近字和谐音映射作为可配置策略；
- URL、邮箱、电话、证件号等结构化片段识别；
- 原文到规范化文本的双向位置映射。

任何证据片段都必须定位到原文，不能只返回规范化文本中的 offset。不得静默截断长文本；超过限制时应分块或返回明确的 `content_too_large`。

### 4.2 规则类型

| 类型 | 用途 | 默认动作 |
| --- | --- | --- |
| `hard_reject` | 法律/业务上足够确定的违规模式 | 直接拒绝，不允许 AI 覆盖 |
| `risk_signal` | 可疑词、组合词、结构化模式 | 提升指定类别信号 |
| `context_exception` | 新闻引用、固定业务术语等例外 | 仅抑制绑定的规则 |
| `force_review` | 高风险但不应自动拒绝的模式 | 直接返回终态 `review` |
| `monitor_only` | 新规则灰度、离线观测 | 记录，不改变决定 |

词条需要记录语言、匹配方式、边界要求、类别、来源、适用场景、规则动作和测试用例。禁止同一规则版本中出现无法解释的重复或循环例外。

### 4.3 正则安全

- 优先使用 `RegexOptions.NonBacktracking`；
- 必须设置匹配超时，即使使用非回溯引擎；
- 限制 pattern 长度、输入长度、捕获组数量和不安全语法；
- 新规则必须通过正例、反例、超长输入和恶意输入测试；
- 编译或测试失败的规则不能发布；
- 动态数据库正则不得假设可使用源码生成器，也不得以 `RegexOptions.Compiled` 作为 Native AOT 性能保证。

### 4.4 外部 AI 路由边界

一级规则引擎只允许三种终结方式：可信硬拒绝、明确返回 `review`、命中完整版本化结果缓存。其余内容进入外部 AI。首期不设置“未命中规则直接通过”或本地轻量模型旁路。

- 路由输入只能来自已发布策略，调用方不能指定供应商、模型、Prompt 或 URL；
- 按 `tenant_id + application_id + policy_revision` 选择 AI 路由，校验数据分类、地域和预算；
- 发送前执行长度/token 预算、必要脱敏和数据外发许可检查；
- 同一 item 默认一次独立 AI 请求，避免批内文本互相污染；只有隔离性、顺序、解析和质量评测通过后才能启用小规模 micro-batch；
- 缓存命中和硬规则终结仍记录应用调用量，但不计为实际 AI 调用；
- 低风险自动通过样本按比例进入 shadow AI 与离线盲标抽检，用于估算漏放和模型漂移；盲标是评测活动，不形成平台在线复审流程。

### 4.5 规则热发布

```text
编辑 draft
  → 数据库事务生成 ruleset revision
  → 后台完整编译 AC/正则
  → 执行契约测试与性能测试
  → 生成不可变快照和 checksum
  → 审批
  → CAS 原子切换 active revision
  → Outbox 发布变更事件
  → 实例加载并在请求边界原子替换
```

通知丢失由定时版本校验补偿。编译失败继续使用上一稳定版本并告警。“每 5 分钟轮询”只能作为补偿机制，不能描述为实时生效。

## 5. 外部 AI 网关与 RAG

### 5.1 职责与抽象边界

外部 AI 承担未决文本的语义分类、高风险类别二次确认、异步模型二次判定和抽样评测。它不承担工具调用、多步 Agent、自主联网或修改策略，因此不引入 Agent Framework。

领域层只依赖统一的 `IModerationAiClient`，输入为版本化审核上下文，输出为 `AiModerationResult`。供应商 DTO、鉴权 Header、URL 和错误码只存在于适配器层。`Microsoft.Extensions.AI` 可作为单次 Prompt→响应的客户端抽象；当某个协议的结构化输出或专有字段无法被它完整表达时，该适配器可以使用官方 SDK 或 typed `HttpClient`，但同一个配置版本只能选择一条传输路径，禁止同时发送或失败后偷偷改走另一实现。

### 5.2 支持的协议

| `protocol` | 默认端点 | 请求关键字段 | 响应提取 | 鉴权 |
| --- | --- | --- | --- | --- |
| `openai_chat_completions` | `POST /v1/chat/completions` | `model`、`messages`、解码参数、`max_completion_tokens`、`response_format.json_schema` | 恰好一个 `choices[0]` 的 `message.content/refusal`、`finish_reason`、`usage` | 默认 `Authorization: Bearer` |
| `openai_responses` | `POST /v1/responses` | `model`、`instructions`、`input`、`max_output_tokens`、`text.format`、`store:false` | 原始 REST `output[].content[]` 中的 `output_text/refusal`、`status`、`incomplete_details`、`usage`；`output_text` 顶层值仅视为 SDK 便利属性 | 默认 `Authorization: Bearer` |
| `anthropic_messages` | `POST /v1/messages` | `model`、顶层 `system`、`messages`、`max_tokens`、解码参数、`output_config.format` | `content[]` 的 text/其他 block、顶层 `stop_reason`、`stop_details`、`usage`；拒绝以 `stop_reason=refusal` 及 `stop_details` 判定，不从 `content[]` 猜测 | 默认 `x-api-key` + `anthropic-version` |

“兼容 OpenAI”不能只靠 URL 认定。每个模型配置还要声明并通过探测的 capability：`json_schema`、`json_object`、`seed`、`temperature_zero`、`temperature_omitted`、`store_false`、最大上下文、Token usage、错误格式和 `Retry-After`。不支持严格结构化输出时，可以降级为 Prompt JSON + 本地严格校验，但不得把未验证的自由文本直接用于决定。认证方式可从 `bearer/x-api-key/api-key` 受控枚举中选择，credential 只能注入相应 Header，禁止放入 query string；协议默认值如上表。

每个适配器都有不可变 `adapter_contract_version` 和官方 wire fixture。fixture 必须覆盖成功、拒绝、内容过滤、输出 Token 截断、上下文窗口耗尽、多个 choice/output item、非文本 block、usage 缺失以及协议错误；发布模型配置时同时冻结适配器版本，避免升级 SDK 后静默改变解析语义。

三类严格结构化输出的 wire mapping 固定为：

```text
Chat Completions:
response_format = {
  type: "json_schema",
  json_schema: { name: "veriscan_moderation", strict: true, schema: <schema> }
}

Responses:
text.format = {
  type: "json_schema",
  name: "veriscan_moderation",
  strict: true,
  schema: <schema>
}

Anthropic Messages:
output_config.format = {
  type: "json_schema",
  schema: <schema>
}
```

5.6 节的 schema 是供应商无关的权威领域契约（canonical schema），服务端始终用它对最终输出做严格校验。`<schema>` 是由已发布的 provider-specific transformer 从 canonical schema 生成的有效 wire schema：必须符合该协议/模型已探测的约束子集，不能传占位符，也不能由运行时随意删改。例如供应商 wire 层不支持 `pattern/minLength/maxLength/maxItems` 时，可在发布时按已审批的确定性规则转换/移除，但这些约束仍必须由 canonical schema 在本地强制。

每个模型配置版本同时冻结 `canonical_schema_hash + effective_wire_schema_hash + schema_transformer_version`，测试接口和发布门禁展示实际 wire schema 及差异。若官方 SDK 会自动转换不支持的约束，也要取得并验证转换后结果，typed `HttpClient` 实现不得自行假设等价性。供应商 capability 不支持严格结构化输出时，不得发送对应字段，转而使用已评测的 Prompt JSON 模式，并仍执行 canonical schema 本地校验。

首期仅使用非流式、无工具、无对话状态的单轮请求：Chat 不发送 tools；Responses 固定 `store=false`，不使用 `previous_response_id`、conversation、background 和内置工具；Messages 不发送 tools。流式响应和供应商异步 Batch API不进入在线审核链路。

### 5.3 模型配置

管理后台展示的“模型配置”实际由供应商、协议、凭证和发布版本组成。建议字段：

```json
{
  "name": "prod-safety-primary",
  "protocol": "openai_responses",
  "baseUrl": "https://api.example.com",
  "endpointPath": "/v1/responses",
  "apiKey": "<write-only>",
  "authScheme": "bearer",
  "model": "provider-model-snapshot-2026-08-01",
  "apiVersion": null,
  "apiVersionLocation": null,
  "structuredOutputMode": "json_schema",
  "promptTemplateVersion": "moderation-zh@17",
  "decodingMode": "send_temperature_zero",
  "maxInputTokens": 4096,
  "maxOutputTokens": 256,
  "connectTimeoutMs": 2000,
  "requestTimeoutMs": 15000,
  "maxAttempts": 2,
  "concurrencyLimit": 100,
  "rpmLimit": 3000,
  "tpmLimit": 3000000,
  "dataRegion": "approved-region",
  "retentionClass": "no-training-approved",
  "pricingVersion": "provider-a-2026-09"
}
```

约束：

- 管理员配置的是经校验的 `baseUrl + endpointPath`，不是让业务请求透传任意 URL；只允许 HTTPS、批准端口和域名 allowlist；
- 全局出站域名/端口 allowlist 只能由 `platform_admin` 经安全审批维护；租户 `ai_config_editor` 只能从 allowlist 中选择，不能借模型配置扩大网络边界；
- 禁止 IP literal、localhost、私网/链路本地/云元数据地址、URL userinfo 和跨域重定向；连接前后校验 DNS 解析，防止 SSRF 与 DNS rebinding；
- credential Header 名称来自 `bearer/x-api-key/api-key` 受控枚举；后台不得配置 `Host`、`Content-Length`、转发头或其他任意鉴权 Header，仅允许显式白名单中的非敏感供应商 Header；API version 也只能按适配器允许的 header/query 名称注入；
- 凭据在管理后台以密码字段只写录入；开发/单机部署由服务端使用数据库之外的主密钥执行 AES-GCM 加密，数据库只保存密文，生产部署优先改由 KMS/Vault 托管并保存 `credential_ref`；列表、详情、审计 diff 与日志均不得返回明文或密文；编辑时空值表示保留，非空值表示轮换；
- 解码模式必须显式选择 `send_temperature_zero|omit_temperature|provider_fixed` 并通过 capability 探测；某些模型不接受非默认 temperature 时必须省略字段，不能强发 `0`。Token 上限、超时、并发、RPM/TPM、价格和数据策略不能留空依赖供应商默认值；
- 模型别名、Prompt、输出 schema、协议 capability、价格和凭证版本全部参与发布快照与结果审计。

### 5.4 配置发布、路由与回滚

```text
编辑 draft
  → URL/证书/allowlist 静态校验
  → 使用合成文本执行连接和协议探测
  → 输出 schema、错误映射、Token usage 和超时测试
  → 冻结 evaluation run 与成本基线
  → 双人审批
  → 生成不可变 ai_model_config revision
  → 按应用/策略灰度
  → 原子切换 active route，保留上一稳定版本回滚
```

每条 AI 路由包含主配置和可选备用配置。只有网络超时、`408/429/5xx`、熔断或严格解析失败等已定义的 retryable failure 才能重试/切换；内容判定结果、供应商安全拒绝和非 retryable `4xx` 不触发备用模型。跨供应商或跨地域切换必须预先获得租户数据策略授权，不能以“高可用”为由静默外发。

### 5.5 调用流程、弹性与成本控制

1. 根据 `tenant + application + policy revision` 解析不可变 AI 路由；
2. 校验外发许可、数据地域、内容分类、应用预算和供应商 RPM/TPM；
3. 规范化/必要脱敏，按本地 tokenizer 估算输入并拒绝静默截断；
4. 构造固定 Prompt 和严格输出 schema，附内部 correlation ID，不附完整 API Key；
5. 通过有界队列、并发信号量和 `IHttpClientFactory` 连接池调用上游；
6. 仅对 retryable failure 进行带抖动指数退避，尊重 `Retry-After`，同步请求不执行可能超过 deadline 的重试；
7. 校验 HTTP 状态、内容类型、响应大小、finish/stop reason、schema、枚举和 Token usage；
8. 归一结果并保存实际配置版本、供应商 request ID、尝试次数、延迟、Token 和计费快照；
9. 超时、熔断、预算耗尽或非法输出按策略异步重试、返回错误或降级 `review`，不得 `pass`。

每个供应商/模型独立配置 bulkhead、熔断器、并发、RPM 和 TPM；备用模型也要预留容量。重试预算必须纳入总调用量与费用，不能把一次业务 item 的多次上游请求只统计成一次。估算费用使用“调用时生效的价格版本 × 供应商返回的 Token usage”，标记为估算值，不替代供应商账单。

每个逻辑 AI 调用生成稳定 `ai_call_id`。适配器若已验证供应商支持幂等请求 Header，则所有同供应商重试复用由该 ID 派生的 idempotency value；否则网络超时只能标记 `outcome_unknown=true`，重试可能重复执行和计费，必须受更严格的 attempt/费用上限约束。无论供应商是否去重，每次 HTTP 尝试都写一条 `ai_invocation`，不能用内部幂等掩盖真实 Token 与费用。

### 5.6 统一输出与严格解析

期望模型只返回符合以下 JSON Schema 的对象，不直接返回系统内部状态机字段：

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["label", "categories", "reasonCodes", "evidence"],
  "properties": {
    "label": {
      "type": "string",
      "enum": ["safe", "unsafe", "review"]
    },
    "categories": {
      "type": "array",
      "maxItems": 16,
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["code", "severity"],
        "properties": {
          "code": {
            "type": "string",
            "pattern": "^[a-z][a-z0-9_]{0,63}$"
          },
          "severity": {
            "type": "string",
            "enum": ["low", "medium", "high"]
          }
        }
      }
    },
    "reasonCodes": {
      "type": "array",
      "maxItems": 16,
      "items": {
        "type": "string",
        "pattern": "^[A-Z][A-Z0-9_]{0,63}$"
      }
    },
    "evidence": {
      "type": "array",
      "maxItems": 8,
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["quote"],
        "properties": {
          "quote": {
            "type": "string",
            "minLength": 1,
            "maxLength": 256
          }
        }
      }
    }
  }
}
```

- canonical schema 作为不可变权威制品发布，记录 `output_schema_version + output_schema_hash`；三协议通过版本化 transformer 分别生成 Chat `response_format.json_schema`、Responses `text.format`、Messages `output_config.format` 使用的 effective wire schema，并另存 `effective_wire_schema_hash + schema_transformer_version`；不支持时使用 Prompt JSON，但仍执行 canonical schema 本地校验；
- `label`、类别和原因码必须属于当前策略允许集合，未知值整体判为非法；
- `safe/unsafe/review` 是模型标签，不是概率；没有独立校准层时 `riskScore=null`；
- Chat `finish_reason=content_filter`/`message.refusal`、Responses `refusal` content、Messages 顶层 `stop_reason=refusal` 及 `stop_details` 属于供应商安全拒绝：不可重试、不可跨供应商绕过；按应用策略返回 item error，或降级为 `reviewSource=provider_refusal`；
- Chat `finish_reason=length`、Responses `status=incomplete`、Messages `stop_reason=max_tokens|model_context_window_exceeded` 属于截断，不得解析部分 JSON；只有预算和 deadline 允许时才可用更大输出上限对同一获批模型重试一次，否则 error/降级 `review`；
- 空数组、多个候选/output message、非文本 block、超大响应或混入 Markdown 均不得当作成功；schema 非法可按已发布策略尝试一次获批备用模型，但不能处理供应商安全拒绝；
- evidence quote 必须在本地重新定位到原文，否则丢弃并记录不一致；
- 自由文本解释仅供脱敏展示，不是权威政策事实；最终决定和对外原因由本地策略层生成。

### 5.7 数据保护与 Prompt Injection

外部 AI 是第三方数据处理边界。每个应用必须关联 `external_ai_policy`，至少定义允许的供应商、模型、地域、内容敏感级别、脱敏规则、保留/训练承诺和可否故障切换。供应商的 DPA、子处理方、数据驻留和删除承诺未通过审批时，对应内容不得外发。

待审文本和 RAG 证据都放在固定不可信数据边界内。系统提示明确禁止遵循文本中的指令；同时从协议层禁用工具、联网、文件、代码执行和多轮状态。原文、完整 Prompt/响应、Provider Key 与应用 API Key 均不得进入普通日志、Trace、指标或前端错误上报。

### 5.8 RAG 定位与检索

RAG 默认关闭，只提供已审批的政策证据，不直接决定状态。策略显式选择 `rag_required|rag_optional|rag_disabled`；`required` 检索失败时只能 error/降级 `review`，`optional` 才可执行无 RAG 降级。首期可用本地词法检索；若需要语义检索，必须单独实现并审批外部 Embeddings 协议适配器，不能假设 Chat/Responses/Messages 端点等同于 Embeddings。历史用户原文判例默认不进入 Prompt。

检索流程：

1. 按租户、司法辖区、语言、类别、策略版本和生效时间过滤；
2. 词法召回；启用向量时再与对应 embedding 快照联合召回；
3. 应用经评测确定的最低相关性阈值，允许零结果，禁止无条件注入固定 TopK；
4. 只注入短小、脱敏、已审批的摘要；
5. 对排序后的实际证据集合计算 `rag_evidence_set_hash`，记录 `rag_execution_mode=applied|zero_result|disabled|degraded`、检索 as-of 时间、文档/chunk ID、版本、分数、Prompt hash、AI 配置和知识快照。

RAG 上线 Gate：有/无 RAG 消融评测证明业务收益；Retrieval Recall@K 和误召回率达标；Prompt Injection、知识投毒、跨租户检索和 PII 测试通过；新增延迟和外部 Token 费用在预算内；失败时可以退化为无 RAG 模式、返回错误或降级 `review`。

## 6. API 契约

### 6.1 通用约定

- 使用 ASP.NET Core 10 Minimal API，不混用 Controller；
- DTO 使用不可变 `sealed record`，不直接暴露 EF Entity；
- 枚举以字符串序列化；时间使用 `DateTimeOffset`/ISO 8601；
- 使用 .NET 内置 OpenAPI、RFC 7807 Problem Details；
- 每个异步调用传递 `CancellationToken` 和请求 deadline；
- 租户、应用和 Key ID 从 `X-API-Key` 解析，不信任请求体中的 tenant/application ID；
- `language` 只是调用方提示；系统应校验或检测语言，不得因客户端错误声明而绕过应执行的模型/规则；
- 同一批次的 `items[].id` 必须唯一，响应数组保持请求顺序；`context` 使用受控 schema，任何会影响判定的上下文字段都必须进入请求指纹、HMAC 和缓存身份；
- 每个写接口支持审计和明确的 HTTP 状态码。

### 6.2 应用 API Key 鉴权

业务审核 API 只接受以下 Header，不接受 query string、Cookie 或请求体中的 Key：

```http
X-API-Key: vsk_live_<public-key-id>.<256-bit-random-secret>
```

Key 规则：

- 每个应用可拥有多把 Key，用于灰度、轮换和紧急撤销；`application_id` 是稳定主体，`api_key_id` 只是本次调用凭证；
- Key 前缀包含可公开的定位 ID，数据库按 ID 定位记录后，使用服务端 pepper 的 HMAC-SHA-256 摘要和恒定时间比较校验高熵 secret；数据库不保存明文；test/live 使用不同前缀和密钥域；
- pepper 存在 KMS/Vault，保存版本号并支持轮换；Key secret 至少由 CSPRNG 生成 256 bit 熵；
- Key 只在创建/轮换成功响应中展示一次，以后后台仅显示前缀、末四位、状态、权限、过期时间和最后使用时间；
- Key 绑定 `tenant_id`、`application_id`、环境、scopes、可选 CIDR allowlist、`not_before`、`expires_at` 和状态；
- `expires_at` 必填，最大有效期由平台安全策略限制；每应用限制活跃 Key 数，避免长期积累无人管理的凭证；
- Header 在解析前限制数量和长度；缺失、格式错误、未知、过期、撤销的 Key 统一返回不泄露细节的 `401`；已认证但应用停用或缺 scope 返回 `403`；
- 限流以 `application_id` 聚合，避免轮换多把 Key 绕过应用配额；同时保留 Key/IP 异常检测和紧急保护；
- API Key 只能用于服务端到服务端调用，禁止放入 React/移动端包、LocalStorage、浏览器请求、URL、日志或供应商请求；
- OpenAPI 声明 `type: apiKey, in: header, name: X-API-Key`；所有业务端点显式要求该 security scheme。

应用 Key 的 scopes 首期只允许 `moderation:submit`、`moderation:read`。应用和 Key 管理接口使用后台 OIDC + MFA + RBAC，不接受应用 Key：

```http
POST   /api/admin/v1/applications
GET    /api/admin/v1/applications/{applicationId}
PATCH  /api/admin/v1/applications/{applicationId}
POST   /api/admin/v1/applications/{applicationId}/api-keys
GET    /api/admin/v1/applications/{applicationId}/api-keys
POST   /api/admin/v1/applications/{applicationId}/api-keys/{keyId}/rotate
DELETE /api/admin/v1/applications/{applicationId}/api-keys/{keyId}
```

创建/轮换 Key 的成功响应示例：

```json
{
  "keyId": "0198...",
  "keyPrefix": "vsk_live_ab12",
  "apiKey": "vsk_live_ab12.<shown-only-once>",
  "scopes": ["moderation:submit", "moderation:read"],
  "expiresAt": "2027-03-01T00:00:00Z"
}
```

创建应用和 Key 返回 `201 Created + Location`；撤销返回 `204 No Content`。完整 `apiKey` 只在创建/轮换响应中展示一次，不得被访问日志、审计详情或 APM 捕获。轮换采用“新 Key 先启用 → 可配置短重叠窗口 → 调用方切换 → 旧 Key 撤销”，撤销目标传播时间首期设为不超过 60 秒；高风险应用可配置每次直接查库实现更短窗口。

### 6.3 创建批量审核

```http
POST /api/v1/moderation/batches
X-API-Key: vsk_live_<public-key-id>.<secret>
Idempotency-Key: <unique-key>
Content-Type: application/json
```

```json
{
  "policyId": "ugc-default",
  "mode": "auto",
  "items": [
    {
      "id": "client-item-001",
      "content": "待审核文本",
      "language": "zh-CN",
      "contentType": "plain_text",
      "context": {
        "scene": "comment",
        "authorType": "user"
      }
    }
  ]
}
```

`mode`：

- `sync`：只接受满足同步限制的批次，不得静默切换为异步；deadline 到期时取消未完成工作并返回 `504`/逐 item 超时，已经持久化的请求仍可按 `requestId` 查询；
- `async`：持久化后返回 `202 Accepted`；
- `auto`：由批大小、当前队列和预计外部 AI 路由决定；只有该模式可以自动返回 `200` 或 `202`。

初始安全上限，最终由 Phase 0 压测确认：

| 项目 | 同步 | 异步 |
| --- | ---: | ---: |
| 最大 item 数 | 100 | 1000 |
| 单 item UTF-8 大小 | 64 KiB | 64 KiB |
| 请求体上限 | 4 MiB | 16 MiB |
| 外部 AI 最大输入 | 默认 4096 token，并受模型配置更小上限约束；超限分块或拒绝，禁止静默截断 | 同左 |

`policyId` 可省略并使用应用默认策略；若显式提供，只能选择该应用 allowlist 中的策略。审核请求不能包含供应商、模型、Prompt、凭证或外部 URL。

相同 `(application_id, Idempotency-Key)`：

- 请求指纹相同：返回已有 `requestId` 和当前状态；
- 请求指纹不同：返回 `409 Conflict`；
- 幂等记录保留时间必须大于客户端最大重试窗口。

幂等规范：

- `Idempotency-Key` 必须匹配 `[A-Za-z0-9._:-]{16,128}`，区分大小写，不允许空白、Unicode、逗号或多个同名 Header；
- 数据库保存 `HMAC-SHA256(idempotency_pepper, UTF8(application_id || 0x00 || key))`，不保存原值；pepper 与内容/API Key pepper 分离；
- `request_fingerprint` 对调用方请求语义做确定性 JSON canonicalization：字段按名称排序、数组保留原顺序、拒绝重复属性、补齐服务端定义的默认值，并包含 `mode`、调用方提交的 policy selector、逐 item ID/原始字符串/language/contentType/context；
- 指纹中的 policy selector 保留“显式 policyId”或“使用应用默认”这一调用语义；首次请求解析并冻结实际 policy revision。重放在 active revision 改变后仍返回原请求，不能改用新策略；
- 幂等映射在原请求处于 `accepted/processing/retry_wait` 时绝不删除，并随长时间 backlog 延长；请求进入终态后仍保留客户端最大重试窗口。首期 `expires_at = max(submittedAt + 24h, finalizedAt + clientRetryWindow)`；只有原请求已终态且超过该时间才可清理，之后同 Key 才可被视为新请求；
- 先原子写入幂等占位和请求，再启动审核；并发相同请求只能有一个 owner，其余返回同一请求，不能重复调用 AI。

同步请求若在形成完整响应前整体超时，返回 `504 Problem Details`；若批次仍在 deadline 内完成聚合、但个别 item 已得到确定的 `timeout/unavailable` 结果，则返回 `200` 并在对应 item 中标记错误。两种情况都必须可通过 `requestId` 追踪，不能让客户端猜测请求是否已执行。

### 6.4 同步响应

```json
{
  "requestId": "0198...",
  "applicationId": "ugc-service-prod",
  "processingStatus": "completed",
  "policyVersion": "ugc-default@42",
  "submittedAt": "2026-09-01T10:00:00+08:00",
  "machineCompletedAt": "2026-09-01T10:00:00.120+08:00",
  "finalizedAt": "2026-09-01T10:00:00.120+08:00",
  "results": [
    {
      "id": "client-item-001",
      "processingStatus": "completed",
      "decision": "review",
      "reviewRequired": true,
      "reviewSource": "model_ambiguous",
      "degraded": false,
      "riskScore": 0.71,
      "scoreSource": "external_ai_calibrated",
      "categories": [
        { "code": "harassment", "riskScore": 0.71 }
      ],
      "reasonCodes": ["AI_REVIEW_REQUIRED"],
      "evidenceSpans": [],
      "route": "external_ai",
      "rulesetVersion": "rs-20260901-0042",
      "aiModelConfigVersion": "prod-safety-primary@12",
      "promptTemplateVersion": "moderation-zh@17",
      "calibrationVersion": "cal-zh-ugc-011",
      "ragProfileVersion": null,
      "ragKnowledgeSnapshot": null,
      "ragExecutionMode": "disabled",
      "error": null
    }
  ]
}
```

结果必须使用数组以保留顺序并检测重复客户端 ID。外部生成式模型没有独立校准分数时返回 `riskScore: null`。系统不得返回供应商凭证、完整请求 URL、原始 Prompt/响应或可能暴露上游账号的内部 Header。

`machineCompletedAt` 表示机器链路已结束，可能得到 `pass/reject/review`，也可能得到 item error；`finalizedAt` 表示该 item/批次已进入任一终态。成功的三种决定中二者同时填写；失败 item 也填写二者并保留 `decision=null + error`；处理前取消时可只有 `finalizedAt`。`reviewRequired/reviewSource` 方便调用方路由自己的复核流程。

如果应用显式启用了 `review_on_ai_failure`，降级结果必须为 `decision=review`、`degraded=true`，并返回 `reviewSource=ai_failure_fallback`、明确 `reasonCodes` 和非敏感 `error.code`；不得伪装成正常模型争议。未启用时保持 `decision=null` 和 item error。

### 6.5 异步接口

```http
202 Accepted
Location: /api/v1/moderation/batches/{requestId}
Retry-After: 2
```

```json
{
  "requestId": "0198...",
  "processingStatus": "accepted",
  "statusUrl": "/api/v1/moderation/batches/0198...",
  "submittedAt": "2026-09-01T10:00:00+08:00"
}
```

- `GET /api/v1/moderation/batches/{requestId}`：查询批次和逐 item 状态；
- `POST /api/v1/moderation/batches/{requestId}/cancel`：仅取消未开始项目；
- Webhook 地址必须在后台预注册和验证，调用请求不得提交任意 URL；
- Webhook 事件签名、包含事件 ID，并由接收方幂等处理；
- 大结果支持分页或导出任务，不能一次返回无限数组。

查询接口默认只允许同一应用且具备 `moderation:read` scope 的 Key 访问；租户后台用户可按 RBAC 跨本租户应用查询。Key 轮换后新 Key 可以读取该应用旧 Key 创建的请求。返回 `200/404/403`。

取消接口要求独立 `Idempotency-Key`，其唯一空间为 `(application_id, request_id, operation=cancel, key_digest)`，指纹包含 target request 和操作名，不与创建批次 Key 共用。取消已终结批次返回 `409`，重复取消相同 target/fingerprint 返回第一次操作结果；状态更新、取消事件和幂等操作记录在同一事务内提交，确保只产生一个取消事件。

### 6.6 状态码和部分失败

| HTTP 状态 | 含义 |
| --- | --- |
| `200` | 同步响应信封已完整形成，所有 item 都进入终态；成功 item 为 `completed + pass/reject/review`，失败 item 为 `failed + decision:null + error`；混合时批次为 `completed_with_errors` |
| `202` | 已持久接收，异步处理中 |
| `400` | JSON/字段不合法 |
| `401` | API Key 缺失、格式错误、无效、过期或已撤销；错误信息不区分具体原因 |
| `403` | Key 已认证但应用停用、缺少 scope 或无策略权限 |
| `409` | 幂等键冲突或状态冲突 |
| `413` | 请求体或 item 超限 |
| `422` | 内容格式可解析但不受支持 |
| `429` | 租户配额或有界队列已满，返回 `Retry-After` |
| `503` | 权威依赖不可用或审核能力未就绪 |
| `504` | 明确要求同步的请求超过服务端 deadline |

HTTP 级错误使用 Problem Details。批内某个 item 的超时、未知语言或模型错误写入该 item 的 `error.code` 和 `retryable`，不得丢失其他 item 的成功结果。

### 6.7 AI 配置与统计管理接口

以下接口仅供后台 OIDC 用户使用，按 `ai_config_editor/publisher/tenant_admin` 授权，不接受应用 API Key：

```http
POST /api/admin/v1/ai/configurations
GET  /api/admin/v1/ai/configurations/{configId}
PUT  /api/admin/v1/ai/configurations/{configId}
POST /api/admin/v1/ai/configurations/{configId}/test
POST /api/admin/v1/ai/configurations/{configId}/publish
POST /api/admin/v1/ai/routes
POST /api/admin/v1/ai/routes/{routeId}/publish
GET  /api/admin/v1/usage?applicationId=...&from=...&to=...
```

- `apiKey` 通过 TLS 到达后端后立即交给凭据保护器；单机版保存带随机 nonce 的认证密文，KMS/Vault 模式保存引用。任何响应都只返回 `hasCredential/credentialSource`，服务器不得在日志、异常或审计 diff 中序列化 secret；
- 创建 model config/route 返回 `201 Created + Location`；draft 可以编辑，published 不可修改，只能从旧版克隆新 revision；
- `test` 使用系统合成文本并返回 URL/证书、协议、模型可达性、schema、Token usage、延迟和非敏感错误；测试通过不等于可以发布；
- `publish` 校验审批、evaluation run、数据地域、配额、价格和回滚目标，以事务/CAS 切换 active revision；
- 统计接口返回 `dataThrough` 水位，明确聚合存在短延迟；费用字段命名为 `estimatedCost`，不能冒充供应商结算账单。

## 7. 数据模型

### 7.1 设计原则

- 权威结果可复现，记录所有实际生效版本；
- 原文最小化存储，哈希不是匿名化；
- 配置和规则发布后不可变，通过新版本演进；
- 审核和配置事件追加写入，不覆盖历史结果；
- 高频记录按月/日分区，保留策略可以快速删除整分区；
- 所有调用链表带 `tenant_id` 和适用的 `application_id`，所有分数带 `CHECK (value >= 0 AND value <= 1)`。

内部主键统一使用 UUID/UUIDv7。每个策略、规则集、AI 模型配置、AI 路由、Prompt、校准、外发策略和 RAG 快照另外拥有全局唯一、不可变的 `public_revision_id`，例如 `ugc-default@42`。API 只返回公共版本 ID，数据库通过唯一约束将其映射到内部 UUID FK；公共 ID 一经发布不得复用或改指向其他配置。

### 7.2 核心表

#### applications / application_api_keys / api_key_events

```text
applications:
id uuid PK
tenant_id uuid NOT NULL
public_id varchar NOT NULL
name varchar NOT NULL
environment varchar NOT NULL       -- test | live
status varchar NOT NULL            -- active | suspended | archived
default_policy_id uuid NOT NULL
allowed_policy_ids jsonb NOT NULL
external_ai_policy_id uuid NOT NULL
quota_profile_id uuid NOT NULL
owner_id uuid
created_at/updated_at timestamptz NOT NULL
UNIQUE (tenant_id, public_id)

application_api_keys:
id uuid PK
tenant_id uuid NOT NULL
application_id uuid NOT NULL
public_key_id varchar NOT NULL UNIQUE
key_prefix varchar NOT NULL
secret_digest bytea NOT NULL
pepper_version varchar NOT NULL
last_four char(4) NOT NULL
scopes jsonb NOT NULL
cidr_allowlist jsonb NULL
status varchar NOT NULL            -- active | suspended | revoked
not_before timestamptz NULL
expires_at timestamptz NOT NULL
last_used_at timestamptz
rotation_group_id uuid
created_by/revoked_by uuid
created_at/revoked_at timestamptz

api_key_events:
id, tenant_id, application_id, api_key_id, actor_id,
action, reason, source_ip, trace_id, created_at
```

完整 Key 永不入库，`secret_digest = HMAC-SHA256(pepper_version, public_key_id || 0x00 || secret)`；校验时按 public ID 定位、重新计算并恒定时间比较。`last_used_at` 采用内存/Redis 合并后周期性写回，不能让每次审核额外更新热点行。Key 撤销采用追加审计与状态更新，不硬删除历史 Key；应用归档后历史审核和统计仍保留 `application_id`。Provider credential 与应用 API Key 使用完全独立的表、密钥域和轮换流程。

#### moderation_requests

```text
id uuid/uuidv7 PK
tenant_id uuid NOT NULL
application_id uuid NOT NULL
created_by_api_key_id uuid NOT NULL
idempotency_key_digest bytea NOT NULL
request_fingerprint bytea NOT NULL
policy_id uuid NOT NULL
policy_version bigint NOT NULL
policy_public_revision_id varchar NOT NULL
mode varchar NOT NULL
processing_status varchar NOT NULL
item_count int NOT NULL
trace_id varchar
submitted_at timestamptz NOT NULL
machine_completed_at timestamptz
finalized_at timestamptz
expires_at timestamptz NOT NULL
```

`created_by_api_key_id` 记录首次创建该业务请求的 Key。相同应用使用另一把 Key 重放同一个幂等键时，不能覆盖此字段，而是写入独立调用事件。

#### request_idempotency_records

```text
id uuid/uuidv7 PK
tenant_id uuid NOT NULL
application_id uuid NOT NULL
idempotency_key_digest bytea NOT NULL
request_fingerprint bytea NOT NULL
moderation_request_id uuid NOT NULL
created_at/expires_at timestamptz NOT NULL
UNIQUE (application_id, idempotency_key_digest)
```

创建批次时与 `moderation_requests` 在同一事务内插入。清理器只删除“目标请求已进入终态且 `expires_at < now()`”的映射；活动请求必须续期/保留并继续返回原状态。删除到期映射后可允许 Key 被视为新请求，历史审核记录仍可按 `requestId` 保留；不能把唯一约束直接放在长期保存的审核表上，否则 TTL 重用语义无法实现。

#### idempotent_operations

```text
id uuid/uuidv7 PK
tenant_id uuid NOT NULL
application_id uuid NOT NULL
target_request_id uuid NOT NULL
operation varchar NOT NULL
idempotency_key_digest bytea NOT NULL
operation_fingerprint bytea NOT NULL
http_status int NULL
response_snapshot jsonb NULL
created_at/expires_at timestamptz NOT NULL
UNIQUE (application_id, target_request_id, operation, idempotency_key_digest)
```

用于取消等资源操作；创建批次由 `request_idempotency_records` 负责。Key 摘要算法、canonicalization 和 TTL 与 API 契约一致，状态变更和操作幂等记录必须处于同一事务。

#### api_request_events

```text
id uuid/uuidv7 PK
tenant_id uuid NULL
application_id uuid NULL
api_key_id uuid NULL
presented_key_fingerprint bytea NULL
moderation_request_id uuid NULL
route_template varchar NOT NULL
auth_outcome varchar NOT NULL          -- success | missing | invalid | expired | revoked | forbidden
idempotency_outcome varchar NULL       -- new | replay | conflict
http_status int NOT NULL
item_count int NULL
latency_ms int NOT NULL
source_ip_hmac bytea NULL
created_at timestamptz NOT NULL
```

该表记录每次实际进入 API 的调用，包括无法解析主体的失败鉴权、幂等重放和冲突，是按 Key 统计调用次数与异常行为的事实来源。未知 Key 仅保存由独立审计 pepper 计算的短期 HMAC 指纹，不保存完整 Key、原文、User-Agent 原值或任意 Header。按时间分区并设置短于审核事实的保留期，长期趋势写入聚合表。

#### moderation_items

```text
id uuid/uuidv7
request_id uuid NOT NULL
tenant_id uuid NOT NULL
application_id uuid NOT NULL
client_item_id varchar NOT NULL
content_hmac bytea NOT NULL
content_hmac_key_version varchar NOT NULL
encrypted_content bytea NULL
external_content_ref varchar NULL
language varchar
content_type varchar NOT NULL
scene varchar
processing_status varchar NOT NULL
decision varchar NULL
review_source varchar NULL
degraded boolean NOT NULL DEFAULT false
risk_score numeric(6,5) NULL
score_source varchar NULL
route varchar NOT NULL
reason_codes jsonb NOT NULL
category_scores jsonb NOT NULL
ruleset_version_id uuid NOT NULL
ruleset_public_revision_id varchar NOT NULL
normalization_version varchar NOT NULL
ai_route_version_id uuid NULL
ai_route_public_revision_id varchar NULL
ai_model_config_version_id uuid NULL
ai_model_config_public_revision_id varchar NULL
provider_model_snapshot varchar NULL
prompt_template_version_id uuid NULL
prompt_template_public_revision_id varchar NULL
prompt_template_hash bytea NULL
calibration_version_id uuid NULL
calibration_public_revision_id varchar NULL
external_ai_policy_version_id uuid NULL
external_ai_policy_public_revision_id varchar NULL
rag_profile_version_id uuid NULL
rag_profile_public_revision_id varchar NULL
rag_knowledge_snapshot_id uuid NULL
rag_knowledge_snapshot_public_revision_id varchar NULL
rag_execution_mode varchar NULL
rag_evidence_set_hash bytea NULL
rag_retrieved_as_of timestamptz NULL
ai_labels jsonb NULL
created_at timestamptz NOT NULL
machine_completed_at timestamptz NULL
finalized_at timestamptz NULL
expires_at timestamptz NOT NULL
UNIQUE (request_id, client_item_id)
```

`content_hmac` 建议为：

```text
HMAC(tenant_key,
     application_id || normalization_version || content_type ||
     canonical_policy_context || normalized_content)
```

不能使用未加密 SHA-256 作为敏感短文本的匿名化手段。HMAC 密钥轮换时保留 `content_hmac_key_version`，禁止尝试用新旧租户密钥跨租户关联内容。

#### moderation_evidence

保存规则/AI 证据、RAG 文档/chunk ID、检索分数、原文范围、规范化范围、类别、来源版本和安全展示文本。AI 返回的 quote 只有在本地重新定位成功后才能保存为证据。证据范围使用 Unicode 标量或 UTF-16 index 必须在 API 契约中固定，不能混用。

#### moderation_events

保存完整机器状态机的不可变事件、租户、应用、来源、时间和附加元数据，包括取消、重试和 dead-letter。系统没有 `review_tasks/review_events` 表，也不接受人工决定回写。

#### policy_versions / ruleset_versions

保存策略阈值、分类映射、失败策略、规则版本、审批状态、checksum、创建人与审批人。`published` 版本不可修改。

#### word_rules / regex_rules

规则属于特定 `ruleset_version_id`，包括动作、类别、语言、场景、证据模板、优先级、来源和测试用例。正则额外保存引擎模式、超时和静态检查结果。

#### ai_providers / ai_model_config_versions / ai_route_versions

```text
ai_providers:
id, tenant_id NULL, name, protocol_family, allowed_base_domains,
allowed_regions, status, created_at

ai_model_config_versions:
id, public_revision_id, provider_id, protocol, base_url, endpoint_path,
credential_source, credential_ciphertext NULL, credential_ref NULL,
credential_version, auth_scheme, model_name,
api_version, api_version_location,
capabilities jsonb, prompt_template_version_id,
output_schema_version, output_schema_hash,
adapter_contract_version, decoding_options jsonb,
max_input_tokens, max_output_tokens,
connect_timeout_ms, request_timeout_ms, max_attempts,
concurrency_limit, rpm_limit, tpm_limit,
data_region, retention_class, pricing_version_id,
status, checksum, created_by, approved_by, created_at, published_at

ai_route_versions:
id, public_revision_id, tenant_id, policy_version_id,
primary_model_config_version_id, fallback_model_config_version_id,
retry_policy jsonb, fallback_conditions jsonb, rollout jsonb,
status, created_at, published_at
```

发布版本不可原地编辑。Provider credential 明文不入数据库：单机模式保存带认证标签的 `credential_ciphertext`，主密钥由部署 Secret 注入；KMS/Vault 模式仅保存 `credential_ref`。应用只能引用租户允许的已发布路由。价格使用带生效区间的 `pricing_versions`，历史调用保存计费快照，避免价格更新改写历史估算。

#### ai_invocations / calibration_versions / evaluation_runs

```text
ai_invocations:
id uuid/uuidv7 PK
tenant_id uuid NOT NULL
application_id uuid NOT NULL
moderation_item_id uuid NOT NULL
ai_route_version_id uuid NOT NULL
ai_model_config_version_id uuid NOT NULL
ai_call_id uuid NOT NULL
provider_idempotency_value_hash bytea NULL
provider_request_id varchar NULL
attempt_no int NOT NULL
status varchar NOT NULL
outcome_unknown boolean NOT NULL DEFAULT false
http_status int NULL
error_code varchar NULL
retryable boolean NOT NULL
input_tokens/output_tokens/cached_tokens int NULL
estimated_cost numeric NULL
currency char(3) NULL
pricing_version_id uuid NULL
latency_ms int NOT NULL
started_at/completed_at timestamptz NOT NULL
```

默认不保存原始 Prompt 和供应商响应；只保存 hash、严格解析后的标签、非敏感元数据和版本。`provider_request_id` 只用于供应商排障，不返回普通调用方。`calibration_versions` 与 `evaluation_runs` 保存冻结数据集、Prompt、模型配置和指标，支持模型/Prompt 漂移比较。

#### rag_knowledge_snapshots / rag_documents / rag_chunks

```text
rag_knowledge_snapshots:
id, public_revision_id, tenant_id, policy_version_id,
status, checksum, document_count, chunk_count,
embedding_model_config_version_id NULL, embedding_dimension NULL,
created_by, approved_by, created_at, published_at

rag_documents:
id, tenant_id, document_type, jurisdiction, locale, category,
source, provenance, approval_status, effective_from, effective_to,
sensitivity, created_at

rag_chunks:
id, tenant_id, document_id, content, content_tsv,
content_hash, chunk_index, created_at

rag_snapshot_chunks:
rag_knowledge_snapshot_id, rag_chunk_id,
PRIMARY KEY (rag_knowledge_snapshot_id, rag_chunk_id)

rag_snapshot_vectors_<dimension>:
rag_knowledge_snapshot_id, rag_chunk_id,
embedding_model_config_version_id, embedding vector(<dimension>)
```

`published` 快照不可变，checksum 覆盖成员 chunk、内容 hash、检索参数和 embedding 配置。每次审核先冻结 `rag_knowledge_snapshot_id`，所有词法/向量查询都必须带此 ID；`policy_version` 不能替代知识快照。文档/chunk 可以被多个快照复用，通过成员表绑定，结果记录中的公共快照 ID 可唯一还原当时可检索集合。

索引：

```sql
CREATE INDEX ... ON rag_chunks USING gin (content_tsv);
CREATE INDEX ... ON rag_snapshot_chunks
  (rag_knowledge_snapshot_id, rag_chunk_id);
-- 仅在启用且维度冻结的独立向量表/分区上创建 HNSW
CREATE INDEX ... ON rag_snapshot_vectors_<dimension>
USING hnsw (embedding vector_cosine_ops);
```

若启用外部 Embeddings 并更换维度，使用新向量表/分区和新索引生成一个新知识快照，不能把不同模型向量混入同一索引，也不能把内容发送给未经该应用外发策略批准的 embedding 供应商。

多租户 HNSW 查询必须执行租户/知识快照过滤并启用经过压测的 iterative scan；少数超大租户可采用 list partition 或独立表/索引，防止共享近邻图导致召回和性能互相影响。

#### application_usage_hourly / application_usage_daily

聚合表按 `tenant_id + application_id + bucket + policy/route` 保存：业务请求数、幂等重放数、item 数、缓存/规则/AI route、`pass/reject/review` 分布、降级 review、成功/错误、AI 实际调用与重试次数、输入/输出/缓存 Token、估算费用、延迟直方图状态和数据版本。Key ID 可以作为受控排障维度，但产品主看板按应用聚合，避免轮换导致趋势断裂。

使用事件由审核事务和 `ai_invocations` 通过 Outbox 可靠产生。每个 Outbox 事件拥有全局唯一 `event_id`；聚合 Worker 在同一数据库事务内先写 `usage_consumed_events(consumer_name, event_id)` 唯一记录，再更新聚合，冲突即跳过，保证至少一次投递不会重复计数。支持按不可变事实重建聚合并比对校验。Redis 计数用于实时限流而非账单事实；OpenTelemetry 指标用于运维而非精确业务结算。百分位从原始直方图/可合并 sketch 计算，禁止平均各分片 P95。

#### audit_events / outbox_events

- `audit_events`：租户后台管理员、系统服务账号的敏感操作、对象、前后版本、原因、IP、User-Agent 和时间；不存在平台审核员 actor；
- `outbox_events`：应用/Key 变更、规则发布、AI 配置切换、审核完成、使用统计和 Webhook 事件；与业务事务一起提交，由 Worker 至少一次投递。

#### moderation_jobs / webhook_deliveries

`moderation_jobs` 保存异步 item/batch 的状态、优先级、尝试次数、`available_at`、租约持有者、租约到期时间和最后错误。Worker 使用 `FOR UPDATE SKIP LOCKED` 领取任务，租约到期后可恢复；达到最大重试次数后进入可审计的 dead-letter 状态并形成 item error，或按应用已发布策略降级为 `review`，同时告警。

`webhook_deliveries` 保存事件 ID、预注册目标 ID、签名版本、尝试次数、下次重试时间、响应状态和最终状态。Webhook 至少一次投递，不能与审核事务绑成同步网络调用。

### 7.3 索引、分区与保留

- `moderation_events`、`audit_events` 优先按时间分区；`moderation_items` 达到容量阈值后再分区，DDL 设计必须让分区键进入主键/唯一约束，不能直接照搬未分区表的 `UNIQUE (request_id, client_item_id)`；
- 常用索引包含 `(tenant_id, application_id, created_at)`、`(application_id, processing_status, created_at)`、`(application_id, content_hmac)`；
- 统计查询优先读按应用汇总表或分析副本，不与在线审核争抢主库；
- 若持续 1500 item/s，理论上每天可产生 1.296 亿条记录，必须根据真实占空比制定热数据、归档和删除容量模型；
- 原文、AI 调用元数据、证据、审计和聚合指标分别配置保留期限，备份也必须遵守删除和法律保留政策；Key 撤销审计不得因轮换而丢失。

## 8. 缓存与一致性

### 8.1 缓存边界

- 只缓存完整、成功、可复现的判定；
- 不缓存系统错误或 `ai_failure_fallback` 降级结果；正常模型争议/策略要求产生的终态 `review` 可与 `pass/reject` 一样按完整版本缓存；
- 不做跨内容的语义结果缓存，避免相似文本错误复用；
- Redis 故障只能降低性能，不能改变审核结果；
- 缓存命中仍生成调用记录并标注 `route=cache`。

### 8.2 版本化缓存键

```text
moderation:v3:
{tenantId}:{applicationId}:{policyPublicId}@{policyRevision}:{rulesetRevision}:
{normalizationVersion}:{hmacKeyVersion}:
{aiRouteRevision}:{aiModelConfigRevision}:{promptTemplateVersion}:
{calibrationVersionOrNone}:{externalAiPolicyRevision}:
{ragProfileOrNone}@{ragKnowledgeSnapshotOrNone}:
{ragExecutionMode}:{ragEvidenceSetHashOrNone}:{contentHmac}
```

`contentHmac` 的输入包含所有受控、会影响判定的 canonical policy context，例如应用、语言、场景和 author type。策略、AI 路由/模型配置、外发策略、阈值/校准、Prompt 或 RAG 知识快照任一变化都必须形成新缓存身份。启用 RAG 时先完成固定快照检索并计算 evidence set hash，再查询 AI 结果缓存；这仍可节省外部 AI 调用但不能跳过检索。`degraded` 无 RAG 结果与 `ai_failure_fallback` 一律不缓存；`zero_result` 以固定空集合 hash 区分。API Key 轮换不改变应用判定语义，因此 Key ID 不进入结果缓存键；缓存命中仍按本次 `api_key_id` 写入 `api_request_event`。无法证明缓存身份完整时关闭结果缓存，只保留规则与配置快照缓存。

规则或 AI 配置更新通过新版本自然失效，不扫描删除旧 key。旧 key 由 TTL 淘汰。TTL、最大内存、淘汰策略和单应用/租户上限必须显式配置。必须覆盖跨应用相同内容、跨策略相同版本号、模型/Prompt 更新、阈值更新、上下文差异、RAG 更新和 HMAC 密钥轮换的缓存回归测试。

### 8.3 两级缓存

- L1 `IMemoryCache`：保存不可变规则快照、配置快照和极小的热点结果；必须设置 `SizeLimit`；
- L2 Redis：精确结果、限流计数、短期单飞锁和版本通知；
- 高并发相同内容使用 single-flight，防止缓存击穿；
- Redis Pub/Sub 只负责加速通知，PostgreSQL active version + Outbox/轮询负责最终一致。

API Key 元数据使用独立短 TTL L1 缓存，按 `public_key_id` 定位并在本地恒定时间校验摘要；缓存中不得保存明文 Key。创建、暂停、撤销和应用停用通过 Outbox/Redis 通知失效，并以数据库版本轮询补偿，首期撤销传播 SLO ≤ 60 秒。Redis 不可用时回源 PostgreSQL，不能因为缓存故障跳过鉴权或配额。

## 9. 结果记录与运营后台

### 9.1 `review` 结果边界

- `review` 与 `pass/reject` 一样是 VeriScan 已完成的机器结果，通过同步响应、查询或签名 Webhook 返回；
- 系统记录 `reviewSource=model_ambiguous|policy_required|provider_refusal|ai_failure_fallback`、类别、原因码、是否降级和实际版本；
- 平台不提供待复审列表、领取、审核员、SLA、批量人工决定或人工回写 API；
- 管理后台只能查询/统计 `review` 记录，不能把它改成 `pass/reject`；
- 调用方负责自己的人工流程、最终业务动作和留痕，避免形成两个互相冲突的事实源；
- 如未来确需接收调用方最终结果，应作为独立的反馈/评测数据接口重新设计，不得修改原审核决定。

### 9.2 后台模块

1. 数据看板：按租户/应用/时间查看请求、item、决定、`reviewSource`、route、缓存、AI 调用/重试、Token、费用、P95/P99 和错误；
2. 应用管理：创建/停用应用、test/live 环境、负责人、默认/允许策略、外发策略、Webhook 和配额；
3. API Key 管理：一次性创建、掩码列表、轮换、暂停、撤销、过期提醒、scope/CIDR 和异常使用告警；
4. 策略管理：分类体系、AI 路由、失败策略、灰度范围和回滚；
5. 规则管理：以“关键词、风险分类、直接拦截/交给 AI 判断/作为语境例外”的业务语言逐条配置，实时展示命中结果预览；批量添加采用每行一个关键词和统一处理方式，高级规则、测试集、审批和发布分层呈现；
6. AI 供应商与模型配置：协议、URL、只写 API 密钥、模型、Prompt、schema、超时、限额、价格、地域、连接测试、评测、灰度和回滚；
7. RAG 管理：来源、审批、生效/失效、检索测试和投毒检查；
8. 记录查询：按应用、决定、类别、`reviewSource`、降级和错误筛选，详情默认脱敏，只读展示证据与版本；
9. 审计中心：敏感查看、导出、应用/Key/AI 配置变更、登录和权限变更；
10. 系统健康：版本滞后、AI 上游、熔断、队列、数据库和 Redis 状态。

AI“测试连接”只允许使用系统内置合成/脱敏文本，并显示协议状态、模型标识、结构化解析、Token usage 和耗时；不能把当前页面中的用户原文当测试数据。Key 明文只在创建成功弹窗显示一次，禁止写入 LocalStorage、埋点、错误上报、浏览器历史或第三方分析工具。

### 9.3 权限模型

预置角色：`viewer`、`application_admin`、`policy_editor`、`ai_config_editor`、`publisher`、`auditor`、`tenant_admin`、`platform_admin`。

- 应用 API Key scope 与后台 RBAC 分离；应用 Key 永远不能调用应用/Key/AI 配置、规则发布、审计和导出接口；系统不存在复审决定接口；
- 编辑与发布分权；生产规则、AI URL/凭证引用、模型、Prompt、外发策略和知识变更至少双人审批；
- 原文查看、批量导出、硬拒绝规则发布和保留策略修改使用单独权限；
- 所有权限变更和敏感读取进入不可变审计事件。

### 9.4 应用统计口径

应用是长期统计主体，API Key 是可轮换的诊断维度。每次入口调用都写入带 `application_id + api_key_id` 的 `api_request_event`，业务审核请求另保留首次创建它的 Key，因此换 Key 不会切断应用趋势，也能正确统计另一把 Key 的幂等重放和异常调用。

首期统计至少包括：

- 请求：提交请求、幂等重放、item、同步/异步、成功/部分失败/失败；
- 决定：`pass/reject/review`，并拆分 `model_ambiguous/policy_required/provider_refusal/ai_failure_fallback`；
- 路由：缓存、硬规则、外部 AI、RAG，以及 AI 主/备配置；
- 依赖：AI 实际调用、重试、HTTP 错误、解析错误、熔断和降级；
- 资源：输入/输出/缓存 Token、估算费用、请求与 item 延迟直方图；
- 安全：鉴权失败、限流、异常 IP/CIDR、撤销后调用和预算告警。

“业务请求数”“幂等重放次数”“AI 实际调用次数”必须分别统计。缓存命中算一次应用审核调用但不算 AI 调用；一次 item 因重试产生两个上游请求，算一个 item、两个 AI invocation。运营查询默认按应用聚合，只有受授权排障页可以按 Key 前缀下钻，任何指标或导出都不得包含完整 Key 或原文。

## 10. 安全、隐私与合规

### 10.1 身份与网络

- 业务审核 API 统一使用应用级 `X-API-Key`；后台使用 OIDC + MFA；内部 AI 网关使用独立服务身份/mTLS，三类凭证不得复用；
- 每 Key、应用、租户、来源 IP 和 AI 供应商分别设置 QPS、item/s、并发、RPM/TPM、批大小、日/月额度与费用预算；
- 服务层所有查询强制 `tenant_id + application_id` 作用域，关键共享表可启用 PostgreSQL RLS 作为纵深防御；迁移、后台、API 和 Worker 账号分离；
- 内外网全部 TLS，Redis 使用 ACL/TLS，PostgreSQL 使用最小权限账号；
- AI 网关使用独立受限出站网络，只能连接发布配置 allowlist 中的 HTTPS 目标，禁用自动重定向并防御 SSRF/DNS rebinding；
- Webhook 采用预注册目标、出站 allowlist、事件签名和重放保护。

### 10.2 数据保护

- 原文默认不进入应用日志、指标、Trace、异常消息和 Prompt 调试日志；应用 Key、Provider Key 和一次性创建响应同样禁止记录；
- 原文存储支持 `none | encrypted | external_reference` 三种策略；
- 加密密钥与数据库分离管理，缓存不保存原文；
- 导出文件加密、短期有效、带水印并记录下载审计；
- 明确数据驻留、保留、删除、法律保留、备份删除和数据主体请求流程；
- 外发前按应用 `external_ai_policy` 校验供应商、模型、地域、内容分级、脱敏、保留和训练承诺；不满足时 fail-closed；
- 与供应商完成 DPA/子处理方/数据驻留/删除 SLA 审批；供应商协议默认留存行为必须实测或书面确认，不能因请求了 `store=false` 就假设零留存；
- 历史判例、训练数据和 RAG 入库前脱敏，禁止跨租户复用未经授权的样本。

### 10.3 对抗与供应链

- 覆盖 Unicode 绕过、形近字、空格插入、超长文本、压缩炸弹、正则 ReDoS；
- 把待审文本和 RAG 内容视为 Prompt Injection 来源，禁止工具调用和策略覆盖；
- 把供应商响应视为不可信数据，只允许 JSON schema 中的枚举和受限文本，不执行工具调用、URL、代码或响应 Header 指令；
- 锁定依赖、生成 SBOM、执行漏洞和许可证扫描；
- SDK、Prompt、输出 schema、AI 配置、规则快照和容器镜像校验 checksum/signature；
- 管理后台所有导入执行文件类型、大小、编码和公式注入检查。

### 10.4 密钥生命周期

- 应用 Key 创建、轮换、暂停、撤销、过期与失败鉴权写入不可变审计；日志只记录 `api_key_id`/前缀，不记录完整 Key；
- Provider credential 只由 AI 网关在调用前解密或按 `credential_ref` 读取，常驻内存时间最小化，不返回 API/Worker/前端；数据库备份与加密主密钥必须分离保存；
- 应用 Key、Provider credential、Webhook signing secret 和 HMAC/加密主密钥使用不同用途、不同密钥域和不同轮换计划；
- 疑似泄露时可以单独撤销 Key 或凭证、使本地缓存失效、停止对应应用/AI 路由并生成安全事件；
- 定期扫描源码、镜像、日志和前端构建产物中的密钥特征；任何测试 Key 不得拥有生产供应商或生产数据权限。

## 11. 性能、容量与 SLO

### 11.1 指标定义

- `request/s`：HTTP 请求数；
- `item/s`：审核文本条数；
- `per-item decision latency`：单 item 从接收到结果完成；
- `batch completion latency`：整批最后一个 item 完成时间；
- `ai-route ratio`：实际需要外部 AI 的 item 比例；
- `outbound-attempt factor`：每个 AI item 的平均上游尝试次数，包含重试和备用路由；
- `provider RPM/TPM/concurrency`：供应商请求、Token 和并发限制；
- `review ratio`：返回给调用方、建议其人工复核的 item 比例；该指标不是平台队列容量；
- 延迟必须报告 P50/P95/P99，平均值仅作辅助。

### 11.2 容量公式

若入口速率为 `λ`、AI 路由率为 `r`、每个路由 item 平均上游尝试次数为 `a`、计划最大利用率为 `u`，则实际 AI 出站速率为：

```text
λ_ai_calls = λ × r × a
```

对每个实际承载流量的供应商路由 `j`，令其流量占比为 `p_j`、平均尝试因子为 `a_j`、用于容量准入的 Token 预算为 `T_j`、连接占用预算为 `L_j` 秒、批准并发为 `N_j`。要求 `Σ p_j = 1`，且网关全局尝试因子定义为 `a = Σ(p_j × a_j)`。其上游调用容量上界为：

```text
C_j = min(
  RPM_j / 60,
  TPM_j / (60 × T_j),
  N_j / L_j
)

λ_j_calls = λ × r × p_j × a_j
```

当 `r>0` 时，各资源池相互独立的完整链路安全容量满足：

```text
λ_safe <= u × min(
  C_access,
  C_fast,
  C_persistence,
  C_gateway / (r × Σ(p_j × a_j)),
  min over j where p_j>0 [ C_j / (r × p_j × a_j) ]
)
```

若 `r=0`，公式中 AI Gateway/Provider 两项直接省略，禁止除零。供应商容量不能简单相加；固定分流按各自 `p_j` 验收，主备路由还必须建立“主路由故障、备用 `p_j=1`”的独立容量档案，不能把平时闲置的备用配额视为已经验证。只有已获数据合规批准、具备真实配额且在对应流量比例下可同时工作的路由才进入公式。`C_persistence` 必须包含 API request event、moderation request/item、AI invocation、evidence、event、usage、job 和 outbox 的真实写入，而不是只测一张表。

重试会直接放大 RPM、TPM、费用和尾延迟，所以 `a_j` 必须使用故障压测实测值，不能固定假设为 1。示例中的平均 Token 只用于估算；准入使用按输入长度分桶的 P95/上限 Token 与供应商真实计费口径，并按其实际固定窗口、滑动窗口或 token bucket 验证持续和突发额度。Webhook 和统计聚合为异步链路，但池容量不足会形成 backlog，同样需要单独容量 Gate。

例如目标 `λ=1500 item/s`，硬规则/缓存后仍有 `r=90%` 进入 AI，平均 `a=1.05`：

```text
λ_ai_calls = 1500 × 0.90 × 1.05 = 1417.5 calls/s
required RPM ≈ 85,050
```

若平均每次调用 600 Token，还需要约 5103 万 TPM，未含 70% 利用率折扣。这说明改成外部 AI 后，原有“整体 1500 item/s”不能直接沿用为完成审核吞吐；它最多先作为异步受理伸展目标。最终决定吞吐必须由已购买供应商配额、真实 Token 分布、费用预算和压测共同证明。

批量接口默认逐 item 调用外部 AI，整批同步延迟由最慢 item 决定。大批量应优先 `async/auto`；不能用单 item 平均延迟推导 batch P99，也不能为降低调用次数默认把不相关应用内容拼入同一 Prompt。

### 11.3 初始目标与准入条件

以下是 Phase 0 需要验证的目标，不是未经压测的既成事实：

Phase 0 必须先冻结 `reference_profile_id`，其中包含 API/Gateway 资源、数据库拓扑、协议、供应商地域、模型配置版本、Prompt、供应商配额、Token 分布和运行参数。没有该 ID 的报告不得宣称绝对吞吐。

| 指标 | 初始目标/准入条件 |
| --- | --- |
| 一级计算延迟 | 1 KiB 中文文本，预热后 P95 ≤ 5 ms，P99 ≤ 10 ms |
| 一级吞吐 | 固定 `reference_profile_id` 和文本分布下 ≥ 8000 item/s |
| API Key 校验 | 热缓存 P95 ≤ 2 ms；冷路径回库单独报告；撤销传播 ≤ 60 秒 |
| 同步规则终结批次 | batch ≤ 50 且不调用 AI 时，端到端 P95 ≤ 100 ms |
| AI Gateway 自身开销 | 不含供应商等待，P95 ≤ 20 ms；同时报告排队、序列化和连接池等待 |
| 外部 AI 调用 | 按每个供应商/模型/地域和输入 Token 桶实测 P50/P95/P99；未达到同步 deadline 的配置只能用于异步 |
| 同步 AI 批次 | 首期建议 batch ≤ 10；SLO = 已签署供应商 P95 + 网关/持久化预算，必须用整批 P99 验证 |
| 异步受理 | 成功持久化并返回 `202` 的 P95 ≤ 100 ms |
| 最终决定吞吐 | 只有供应商 RPM/TPM/并发、预算及 `λ×r×a` 持续压测全部通过，才承诺目标 `λ` |
| 长期资源利用率 | Gateway、CPU、连接池和供应商配额长期 ≤ 70%，突发时有界排队 |
| API 可用性 | 月度 99.9%，计划维护除外；依赖故障不得错误放行 |
| 系统错误率 | 内部错误与供应商超时、`429`、`5xx`、解析错误分开报告；任何一类都不得错误放行 |

`1500 item/s` 伸展目标分成“异步受理吞吐”和“机器最终决定吞吐”两个指标。后者只适用于容量档案明确允许的 `(λ, r, a, T)` 包络。实际五分钟滚动 AI 路由率、Token 或重试率超过已验证包络时，系统必须限流、切换异步、申请配额、返回错误或按策略降级 `review`，不能继续堆积无界队列。

### 11.4 正确性 Gate

不再使用容易受类别不平衡误导的总体 Accuracy 作为主要门槛。首个生产 Gate 暂定：

- 关键高风险类别 Recall ≥ 98%；
- 全部上线类别 macro Recall ≥ 95%；
- 自动 `reject` Precision ≥ 95%；
- 明确安全集自动拒绝率 ≤ 1%；
- 只有对外提供 `riskScore` 时，外部 AI 标签的独立校准误差才适用 ECE 等签署阈值；
- `review` 返回率及其中 `ai_failure_fallback` 占比不超过业务签署阈值；
- AI 非法输出、上游和系统失败导致的自动 `pass` 数为 0。

这些是业务目标，必须在自有冻结数据集上证明；如现实数据无法达到，应调整自动化范围而不是修改统计口径。

统计口径：

- 关键类别每类至少包含 1000 个独立正样本和足量近邻安全反例；若目标置信区间需要更多样本，以统计功效计算结果为准；
- Recall/Precision Gate 使用 95% Wilson 置信区间下界，安全内容误拒率使用置信区间上界；不能只比较点估计；
- `review` 作为 abstain 单独计算，不得从分母删除；分别报告自动决定覆盖率、自动拒绝 Recall 和违规捕获率（`reject+review`）；VeriScan 不宣称调用方人工处理后的端到端指标；
- `failed/timeout/unknown_language` 计入系统错误和未覆盖率，不能从报告中消失；
- macro 指标按上线类别等权，micro 指标按样本计数；两者同时报告；
- 评测集由双人独立标注和仲裁形成，报告一致率；测试集对规则、Prompt 和模型开发人员保持盲测；
- “明确安全集”必须包含容易误杀的引用、新闻、教育、医学、反讽和相似词样本，并记录数据集版本。

### 11.5 压测基线

每份报告必须固定：

- CPU、核心、内存、OS、容器、网络出口和数据库规格；
- 应用、SDK/适配器、协议、AI 配置、供应商地域、Prompt、规则和数据库版本；
- 供应商 RPM/TPM/并发配额、连接/请求超时、重试/熔断和价格版本；
- 文本字符/token 的 P50/P95/P99、语言和类别分布；
- batch size、并发、缓存冷/热、AI 路由率、重试率和多应用公平性；
- RAG 数据量、过滤条件、HNSW 参数和数据库部署位置；
- 预热时间、稳定压测至少 60 分钟、突发和故障恢复；
- P50/P95/P99、item/s、request/s、AI calls/s、RPM/TPM、Token、费用、队列、错误、RSS、GC、连接池和数据库指标。

容量采用至少三次独立 60 分钟运行的最差达标结果；利用率、路由率和队列使用五分钟滚动窗口，同时确认整场测试 backlog 不持续增长。长文本按 P95/P99 长度和最大分块数另设场景，验证证据 offset 与分块聚合，不得仅用 1 KiB 样本推导。

### 11.6 可用性与持久化容量 Gate

至少拆分两个 SLI：

- API Acceptance Availability：生产 `POST /moderation/batches` 与 `GET /moderation/batches/{id}` 的合格请求能否完成鉴权、持久受理或查询；
- Machine Decision Completion：需要 AI 的 item 能否在目标时间内得到机器 `pass/reject/review`，供应商超时、配额、`5xx` 和非法输出均计入该 SLI。

服务端 `5xx/504` 和因自身容量不足产生的失败计为不可用；客户端输入、无效 Key 和应用显式额度导致的 `4xx/429` 单列。由于系统没有购买足够供应商配额而产生的 `429` 计为容量失败，不能伪装成客户端限流来维持 99.9%。返回 `202` 只证明已持久受理，不代表机器决定完成。

生产宣称 99.9% 前必须完成：

- API/Worker/AI Gateway 实例故障和滚动发布演练；
- 主供应商超时、`429`、`5xx`、DNS/证书异常、非法 JSON 和价格/配额突变演练；
- PostgreSQL 主库故障切换，实测切换时间、复制延迟和数据丢失不超过 RTO/RPO；
- Redis 故障下启用保守的本地限流；若无法维持租户配额隔离，则返回可审计错误而不是无限放量；
- Worker/Gateway 或供应商恢复后 backlog 在约定时间内清空，且不挤占在线请求 SLO；
- 备用路由只在批准的数据地域和失败条件下工作，未获批准时正确返回错误或降级 `review`；
- PITR、规则/AI 配置/Prompt 回滚和 Webhook 重放通过实际演练。

每个 `capacity_profile` 还必须记录平均/P95 每 item 的数据库、索引、WAL、备份和归档字节。`1500 item/s` 只有在包含 API request event、moderation request/item、AI invocation、usage、evidence、event、job、outbox 和审计的整链路写入压测通过，且供应商调用容量另行达标后，才能发布为机器最终决定吞吐；否则只能标为异步受理或未验证伸展目标。

## 12. 可观测性与运维

### 12.1 核心指标

- API：按租户/应用的请求数、item 数、状态码、鉴权失败、限流、幂等命中和 deadline；
- Key：创建/轮换/撤销、活跃 Key、撤销传播延迟、异常 IP/地域、失败鉴权和最后使用；
- 编排：各 route 比例、各阶段延迟、取消、部分失败；
- 规则：active version、版本滞后、编译失败、命中分布；
- AI Gateway：供应商、协议、模型配置版本、队列、并发、RPM/TPM、Token、费用、重试、切换、熔断、解析失败、超时和上游状态；
- RAG：查询耗时、零结果、Recall@K 抽检、跨策略拒绝；
- 数据库：连接池、锁、复制延迟、WAL、分区、慢查询和磁盘；
- Redis：命中率、内存、淘汰、连接和通知延迟；
- `review`：按正常模型争议、策略要求和 AI 故障降级统计返回量与比例，不跟踪调用方人工处理状态。

所有 Trace 使用 `requestId/itemId/traceId/applicationId/apiKeyId` 关联，但不写入完整 Key、原文、Prompt/响应或 Provider credential。可观测性标签不得使用任意应用名称和上游 request ID 等高基数/敏感值；这些只进入受控结构化记录。

### 12.2 健康与发布

- liveness 仅表示进程存活；API readiness 要求 Key 验证依赖、规则和配置快照可用；Gateway readiness 要求出站策略、凭证引用和至少一个获批路由可解析。供应商临时故障进入独立 dependency health，不应造成所有异步受理实例同时摘除；
- AI 配置、Prompt、规则和策略采用灰度发布，支持按租户/应用/比例回滚；
- 优雅停机停止接收新任务，等待在途请求或把任务交还 PostgreSQL；
- 数据库迁移作为独立发布步骤，应用实例不在启动时并发迁移；
- 一致性敏感的应用/Key、策略和幂等读写走主库；只读副本仅服务历史查询和报表。

### 12.3 备份与灾备

- PostgreSQL 开启加密备份和 WAL/PITR；建议初始目标 RPO ≤ 5 分钟、RTO ≤ 60 分钟；
- 每季度执行一次真实恢复演练，并校验应用、Key 状态、规则、AI 配置、审核、统计、审计和密钥引用可用性；
- Redis 不纳入权威 RPO，丢失后重建；
- Prompt/schema、AI 配置、规则快照和容器制品保存在不可变制品库并保留当前/上一稳定版本；单机模式的凭据密文随数据库备份，但加密主密钥必须由独立 Secret 备份恢复，KMS/Vault 模式则恢复密钥引用；两种模式都不得复制凭证明文；
- 单节点部署不宣称高可用，生产高可用需要 API/Worker/Gateway 多实例、获批供应商容量和 PostgreSQL 故障切换。

## 13. 测试与质量保障

### 13.1 自动化测试

- 单元测试：规范化、原文位置映射、规则优先级、阈值边界、状态机；
- 属性/模糊测试：Unicode、超长文本、恶意正则、重复 JSON 字段；
- 契约测试：OpenAPI `X-API-Key` security scheme、幂等 Key 格式/canonical fingerprint/并发 owner/TTL、取消独立幂等、重复 item ID、部分失败、Problem Details；
- 鉴权测试：Key 格式/长度、恒定时间比较、scope、CIDR、过期/撤销、应用停用、轮换重叠、撤销传播、跨应用查询和限流绕过；
- 集成测试：PostgreSQL/Redis/KMS 故障、Outbox 重放与 `event_id` 消费去重、统计重建、缓存版本、规则原子切换；
- AI 协议契约测试：三类请求/响应 fixture、认证 Header、结构化输出、Token usage、finish/stop/incomplete、错误映射、响应上限和 capability 探测；
- AI 弹性测试：连接/读取超时、DNS/TLS、`408/429/5xx`、`Retry-After`、熔断、重试预算、备用路由、非法/空/截断输出；
- RAG 测试：知识快照不可变/可复现、权限过滤、零结果、过期政策、Prompt Injection、知识投毒；
- 安全测试：越权、完整 Key 泄漏扫描、跨租户/应用相同 HMAC、AI URL SSRF/DNS rebinding、Prompt Injection、Webhook SSRF、导出和审计；
- 负载/混沌测试：多应用公平性、持续/峰值 RPM/TPM、供应商故障、依赖重启、主从切换和恢复。生产供应商压测必须提前获其许可；常规 CI 使用协议仿真服务，不能产生不可控费用。

### 13.2 审核评测集

评测集必须版本化并区分 train/calibration/test；禁止用训练数据验收。至少覆盖：

- 各类别的安全、违规和争议样本；
- 简体、繁体、英文及目标语言；
- 短文本、长文本、跨句和引用上下文；
- 同义、谐音、形近、编码、插空和对抗改写；
- 新旧政策、分布外内容和历史线上误判；
- 时间外推盲测，避免只在已知表达上取得虚高指标。

每次规则、AI 模型配置、Prompt、校准或 RAG 变更都必须生成新的 evaluation run，并与上一生产版本比较关键类别回归。

## 14. 部署形态

### 14.1 单节点试点

同一主机可部署 API、Worker、AI Gateway、PostgreSQL、Redis 和静态后台。必须设置 CPU、内存、连接池和出站并发配额；试点环境只允许连接测试供应商账号，不得携带生产 Provider credential。

适用：开发、演示、低流量试点。限制：无高可用、维护会中断、不能据此推导生产容量。

### 14.2 生产部署

- API 多实例、无状态；
- Worker 按任务分片扩展，使用 PostgreSQL 租约避免重复执行；
- AI Gateway 独立资源池，按连接、并发和供应商配额扩展；出站网络执行域名/IP allowlist；
- PostgreSQL 主库承担一致性读写，备库承担报表并支持故障切换；
- Redis 可使用 Sentinel/Cluster，也可在明确可重建的前提下采用较简单高可用；
- Ingress 终止 TLS，内部服务仍使用认证和加密；
- Native AOT 仅在 `dotnet publish -p:PublishAot=true` 无不可接受警告、三类协议客户端/KMS/数据库依赖可用、并发/回滚测试通过后启用。

## 15. 演进路线与退出条件

### Phase 0：基线与技术验证

交付：

- 分类体系、策略语义和数据保留决策；
- 冻结评测集和双人标注规范；
- AC/正则以及 Chat Completions、Responses、Messages 三类协议 PoC；
- 候选供应商/模型质量、延迟、结构化输出、地域、留存、RPM/TPM 和成本评测；
- API、数据、威胁模型和容量基线。

退出条件：选定主/备供应商模型和协议能力；错误语义、数据外发政策、业务指标、预算和配额获得签署；不存在“未命中即安全”的路径。

### Phase 1：规则、API 与审计闭环

交付：

- 应用、一次性 API Key、轮换/撤销、同步/异步审核 API、限流、幂等、Problem Details、OpenAPI；
- 规则版本、原子发布、缓存版本和 Outbox；
- `pass/reject/review` 终态记录、应用统计、审计、保留与删除；
- 管理后台应用/Key/规则/记录基础能力。

退出条件：Key 越权/轮换/撤销测试通过；规则和基础设施故障均不会自动放行；规则链路仅以“规则能力”试点，不宣称语义召回。

### Phase 2：外部 AI 语义审核

交付：

- AI Gateway、三协议适配器、模型配置/Prompt/schema 版本化发布；
- 单轮无工具调用、严格解析、故障错误/降级 `review`、质量看板和漂移检测；
- 端到端正确性与性能 Gate。

退出条件：关键类别、误杀、`review` 返回率、外发合规、供应商 SLO 和费用达标，才允许扩大自动 `pass/reject` 范围。

### Phase 3：多供应商韧性与成本治理

交付：

- 主/备路由、获批地域故障切换、熔断、RPM/TPM 和预算控制；
- 按应用 Token/费用/错误/延迟统计、异常告警、灰度和回滚；
- 供应商故障、协议漂移和 `λ×r×a` 混合流量压测。

退出条件：供应商配额与预算满足目标包络，备用路由通过合规审批，非法输出和上游失败不产生自动通过。

### Phase 4：RAG 与规模化

交付：

- 已审批政策知识库、FTS + pgvector、检索审计；
- 消融评测、Prompt Injection/投毒测试；
- 高可用、灾备和大规模归档。

退出条件：RAG 明确改善业务指标且新增延迟、安全风险和运维成本在预算内。

### Phase 5：多模态与受控反馈

交付：OCR、音频转写、多模态分类；调用方可选提交的脱敏反馈进入独立评测数据流水线；租户级配额、策略和模型进一步隔离。

反馈接口如实施，必须与审核结果只读事实分离，只能生成评测候选，不能修改历史决定或自动更新 Prompt/模型配置。

## 16. 上线检查清单

### 架构与契约

- [ ] 未命中规则的内容仍经过已发布外部 AI 路由；
- [ ] `processing_status` 与 `decision` 分离；
- [ ] `decision=review` 是 `processing_status=completed` 的终态结果，系统不存在人工复审队列或回写接口；
- [ ] 同步/异步、幂等、部分失败、限流和 deadline 已定义；
- [ ] 创建/取消幂等键空间、canonical fingerprint、并发 owner、TTL 和重用语义通过测试；
- [ ] 结果记录租户、应用、Key、策略、规则、AI 配置、Prompt、校准和 RAG 版本；
- [ ] 缓存身份覆盖应用、策略、AI 路由/配置、外发策略、校准、Prompt、RAG 快照、HMAC key 和全部判定上下文；
- [ ] 不存在 AI/数据库失败后默认 `pass` 的代码路径。

### AI 与质量

- [ ] Chat Completions、Responses、Messages 三类协议契约和错误映射通过测试；
- [ ] URL/模型/Prompt/schema/capability/价格形成不可变配置版本；
- [ ] 业务冻结数据集、供应商/模型/Prompt 回归和时间外推测试通过；
- [ ] 分类别阈值已校准并记录版本；
- [ ] 正确性 Gate 使用签署的样本量、标注协议和 95% 置信区间口径；
- [ ] 上游非法/截断输出、超时、`429/5xx`、熔断、重试和降级 `review` 已覆盖；
- [ ] RAG 未通过 Gate 时保持关闭。

### 数据与安全

- [ ] `X-API-Key` 创建、一次性显示、摘要存储、scope、轮换、撤销、跨应用隔离和配额通过测试；
- [ ] Provider credential 与应用 Key 分离，单机加密或 KMS 引用、轮换和泄漏响应通过演练；
- [ ] 原文不进入日志、Trace 和非加密缓存；
- [ ] 外发策略、DPA/地域/留存审批与 AI URL SSRF/DNS rebinding 防护通过测试；
- [ ] 保留、删除、备份和恢复流程通过演练；
- [ ] 正则 ReDoS、Prompt Injection、Webhook SSRF 和跨租户检索通过测试；
- [ ] 敏感读取、导出、应用/Key、规则和 AI 配置发布有完整审计。
- [ ] Outbox 使用全局 event ID 和消费唯一约束，统计重放不会重复计数；RAG 查询绑定不可变知识快照。

### 性能与运维

- [ ] 压测固定本地资源、供应商/地域/协议/配额/价格版本、文本分布、Token、批大小和路由率；
- [ ] 同时报告 item/s、request/s、AI calls/s、RPM/TPM、费用、P50/P95/P99 和整批完成时间；
- [ ] Gateway、供应商配额和连接池长期利用率不超过 70%，队列有界；
- [ ] readiness、灰度、回滚、优雅停机和告警通过；
- [ ] PostgreSQL PITR、主库故障切换、制品回滚和 backlog recovery 完成实际演练；
- [ ] 持久化、WAL、复制、备份、分区和恢复容量纳入目标 item/s 压测。

## 17. 参考依据

- [OpenAI Chat Completions API](https://developers.openai.com/api/reference/resources/chat)
- [OpenAI Responses API](https://developers.openai.com/api/reference/resources/responses/methods/create)
- [OpenAI API 数据控制](https://platform.openai.com/docs/models/default-usage-policies-by-endpoint)
- [Anthropic Messages API](https://platform.claude.com/docs/en/api/messages/create)
- [Anthropic Structured Outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- [Anthropic API 鉴权](https://platform.claude.com/docs/en/manage-claude/authentication)
- [Anthropic API 数据保留](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention)
- [Microsoft.Extensions.AI 概述](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [IHttpClientFactory 指南](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)
- [ASP.NET Core Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [ASP.NET Core 10 Native AOT](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot?view=aspnetcore-10.0)
- [EF Core NativeAOT 与预编译查询限制](https://learn.microsoft.com/en-us/ef/core/performance/nativeaot-and-precompiled-queries)
- [.NET 正则回溯与超时](https://learn.microsoft.com/en-us/dotnet/standard/base-types/backtracking-in-regular-expressions)
- [pgvector HNSW、过滤与 iterative scan](https://github.com/pgvector/pgvector)
- [ToolGood.Words NuGet](https://www.nuget.org/packages/ToolGood.Words)
