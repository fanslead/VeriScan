# VeriScan（明鉴）

[中文](README.md) | [English](README.en.md)

VeriScan 是面向业务系统的内容安全审核服务。它用版本化关键词规则完成低成本快筛，将未决内容路由到可配置的外部 AI，并通过应用级 API Key 暴露批量审核接口。系统只记录和返回 `pass`、`reject`、`review` 结果；`review` 由调用方自行接入人工流程，VeriScan 不内置二次人工复审。

> 当前仓库仍处于本地开发与验证阶段。下方压测是单机短时结果，不是生产 SLA；尤其是并发 128 时已复现 API 进程异常退出，详见[压测结果](#压测结果2026-09-01)。

## 界面预览

### 风险总览

![VeriScan 风险总览](docs/images/readme/dashboard.jpg)

### 面向运营人员的规则编辑器

![VeriScan 规则编辑器](docs/images/readme/rule-editor.jpg)

规则以“关键词、风险分类、命中后的处理方式”呈现，支持逐条添加和每行一个关键词的批量添加。运营人员无需理解 `black`、`suspicious`、`white`、内部分类代码或小数权重。

### 外部 AI 配置

![VeriScan AI 配置](docs/images/readme/ai-configuration.jpg)

管理后台可配置模型、请求地址、协议、超时、并发和供应商 API 密钥。当前适配 Chat Completions、Responses API 和 Anthropic Messages 三类请求格式；密钥写入后不会再次明文回显。

## 核心能力

- **两级审核**：编译后的关键词规则先行，可信硬拒绝直接返回；未决内容进入外部 AI。
- **可治理规则**：草稿校验、不可变发布、复制新版本、应用显式绑定、请求固化实际版本。
- **外部 AI 路由**：后台管理模型与端点，支持连接测试、发布和启用；未配置或调用失败时按策略返回 `review`，不会伪装为 AI 判定。
- **应用级鉴权**：每个应用拥有独立 API Key，可撤销、轮换、设置有效期和 scope，并按应用统计调用量与判定分布。
- **结果留痕**：记录请求、条目、实际规则版本、路由、风险分与最终状态；不承载人工复审队列。
- **轻量依赖**：ASP.NET Core 10、PostgreSQL 16、Redis、React 19 + Vite；管理后台使用 pnpm 管理。

## 仓库结构

```text
apps/
  api/                  ASP.NET Core 10 API
  admin/                React 19 + Semi Design 管理后台
packages/
  contracts/            规划中的 OpenAPI 派生客户端与共享契约
tests/
  backend/              后端单元与集成测试
  performance/          规则引擎基线与 HTTP 压测脚本
infra/                  PostgreSQL、Redis、Keycloak 本地依赖
docs/                   实施、验收和界面文档
```

## 本地启动

前置环境：.NET SDK 10.0.400、Node.js 24+、pnpm 11+、Docker。

### 1. 安装依赖并启动基础设施

```bash
pnpm install
cp infra/.env.example infra/.env
# 只可用于本地开发；启动 PostgreSQL、Redis 和 Keycloak
docker compose --env-file infra/.env -f infra/compose.yaml up -d --wait
```

请修改 `infra/.env` 中的示例密码。该 Compose 文件只启动基础设施，API 与前端按下面步骤运行。

### 2. 启动 API

首次部署先生成并安全保存一个固定的 AI 凭据加密主密钥：

```bash
openssl rand -base64 32
export VERISCAN_AI_MASTER_KEY='<粘贴并持久化保存上一步生成的值>'
```

然后启动 API。示例值仅限本机，重启时必须复用同一个 `Security__AiCredentials__MasterKey`：

```bash
ConnectionStrings__VeriScan='Host=127.0.0.1;Port=5432;Database=veriscan;Username=veriscan;Password=veriscan-local-postgres-change-me' \
ConnectionStrings__Redis='127.0.0.1:6379,password=veriscan-local-redis-change-me' \
Database__AutoMigrate=true \
Security__ApiKey__Pepper='replace-with-at-least-32-bytes-local-only' \
Security__AiCredentials__MasterKey="$VERISCAN_AI_MASTER_KEY" \
ExternalAi__AllowedHosts__0='api.openai.com' \
ExternalAi__AllowedPorts__0=443 \
ASPNETCORE_URLS='http://127.0.0.1:5000' \
dotnet run --project apps/api/Api
```

外部 AI 默认没有出站主机权限。启用自建或其他供应商端点时，必须把目标主机和端口加入 `ExternalAi__AllowedHosts` / `ExternalAi__AllowedPorts`，并同时使用部署侧网络策略约束出站流量。

### 3. 启动管理后台

```bash
cp apps/admin/.env.example apps/admin/.env.local
pnpm --dir apps/admin dev
```

默认访问 `http://127.0.0.1:5173`。在 Keycloak 中创建本地用户并授予 `veriscan-admin` 角色后登录。Mock 数据仅在显式设置 `VITE_API_MODE=mock` 时启用，真实模式或 OIDC 配置缺失时不会自动降级。

## 配置流程

1. 在“规则与词库”中创建草稿，以业务语言配置关键词和命中动作，校验后发布。
2. 在“AI 配置”中选择协议，填写模型、服务地址和 API 密钥，完成连接测试后发布并启用。
3. 在“应用”中创建调用方应用，绑定已发布规则版本，并创建具有 `moderation:submit` / `moderation:read` scope 的 API Key。
4. 只在创建或轮换时复制 API Key 明文；服务端只保存摘要，之后无法找回原值。

AI 供应商密钥由管理后台提交，服务端使用独立主密钥进行 AES-GCM 加密后入库。读取接口只返回“已配置”状态；编辑时留空表示保留，填写新值表示轮换。供应商密钥、API Key、pepper 和 AI 加密主密钥都不得写入日志、`appsettings*.json` 或 Git。生产环境应将主密钥交给 Secret Manager / Vault / KMS，并与数据库分离备份。

## 调用审核 API

### 提交批量审核

```bash
curl --request POST 'http://127.0.0.1:5000/api/v1/moderation/batches' \
  --header 'Content-Type: application/json' \
  --header 'X-API-Key: <application-api-key>' \
  --header 'Idempotency-Key: order-comment-20260901-001' \
  --data '{
    "mode": "sync",
    "items": [
      {
        "id": "comment-001",
        "content": "待审核文本",
        "contentType": "plain_text",
        "language": "zh-CN"
      }
    ]
  }'
```

约束：每批 1–100 条；单条文本最多 65,536 个字符；当前只支持 `plain_text`。同步请求返回 HTTP 200，`results[].decision` 为 `pass`、`reject` 或 `review`。建议为可重放请求提供唯一 `Idempotency-Key`。

### 查询已记录批次

```bash
curl 'http://127.0.0.1:5000/api/v1/moderation/batches/<request-id>' \
  --header 'X-API-Key: <application-api-key>'
```

API Key 只能查询所属应用的记录。`/healthz` 是存活探针，`/readyz` 会实际检查 PostgreSQL 和待执行迁移。

## 压测结果（2026-09-01）

### 测试环境与口径

- Apple M1 Max，10 个逻辑处理器，64 GB 内存，Docker Desktop。
- .NET 10.0.11、PostgreSQL 16.10、Redis 7.4.5。
- API 使用 Development / Information 日志；日志与 EF SQL 输出会影响吞吐。
- HTTP 数据为客户端端到端观测，包含 API Key 校验、JSON、规则判断、Redis / PostgreSQL 和审核记录持久化。
- 没有启用外部 AI 供应商，因此结果不代表真实 AI 网络延迟或供应商限流能力。

### 规则引擎进程内基线

10,000 条规则、100,000 次测量，连续运行 3 次并取吞吐中位数：

| 指标     |          结果 |
| -------- | ------------: |
| 规则编译 |     27.847 ms |
| 平均延迟 |      3.765 µs |
| P95      |      4.375 µs |
| P99      |      4.875 µs |
| 吞吐     | 265,583 次/秒 |
| 分配     |    1,432 B/次 |

该基线只测进程内规则求值，不包含 HTTP、数据库、鉴权、AI 或网络。

### HTTP 稳定档位

每个场景运行 3 次，表中为按吞吐取中位数的那次结果：

| 场景                            | 请求量 × 条目数 | 并发 |                      吞吐 | 客户端 P95 |      P99 | 错误 |
| ------------------------------- | --------------: | ---: | ------------------------: | ---------: | -------: | ---: |
| 可信硬拒绝                      |       2,000 × 1 |   32 |               1,733 批/秒 |   28.95 ms | 34.16 ms |    0 |
| 混合批次（9 拒绝 + 1 建议复核） |        500 × 10 |   16 | 776.8 批/秒 / 7,768 条/秒 |   29.09 ms | 33.04 ms |    0 |

混合批次语义抽样为 9 个 `reject`、1 个 `review`，路由均为 `local_rules`。由于没有活动 AI 配置，这里的 `review` 是策略要求的保守结果，不是外部模型推理结果。截图中的 P95 是服务端看板聚合值，与表中的客户端端到端 P95 口径不同，不能直接互换。

### 并发扫描与已知失败边界

| 并发 | 请求量 |            吞吐 |      P95 | 结果                                                                |
| ---: | -----: | --------------: | -------: | ------------------------------------------------------------------- |
|    1 |    500 |   153.8 请求/秒 |  9.12 ms | 0 错误                                                              |
|    8 |  1,000 |   971.4 请求/秒 | 11.10 ms | 0 错误                                                              |
|   64 |  3,000 | 1,751.9 请求/秒 | 56.13 ms | 0 错误                                                              |
|  128 |  3,000 |  无有效吞吐结论 |     无效 | **API 以退出码 139 异常退出；仅 8 个 HTTP 200，2,992 个客户端错误** |

并发 128 的数字不能作为性能成绩。容器虽然被 Docker 自动拉起，但本地 Keycloak loopback 兼容 sidecar 仍绑定旧网络命名空间，需要重建后管理端认证才恢复。这说明当前部署恢复链路也有缺口。生产试运行前至少需要：定位 native crash、增加并发上限与背压/限流、让 sidecar 跟随主容器重建，并完成 60 分钟以上的稳定性与真实 AI 供应商压测。目前只有不高于并发 64 的短时本地结果可作为开发基线。

### 复现命令

规则引擎：

```bash
dotnet run --project tests/performance/RuleEngine.Baseline/RuleEngine.Baseline.csproj \
  --configuration Release -- 10000 100000
```

HTTP 压测会真实写入审核记录。请先创建临时应用 API Key，测试后立即撤销；脚本不会输出密钥：

```bash
VERISCAN_API_KEY='<temporary-key>' \
node tests/performance/http-load.mjs hard 2000 32

VERISCAN_API_KEY='<temporary-key>' \
node tests/performance/http-load.mjs mixed 500 16
```

可用 `VERISCAN_BASE_URL` 覆盖默认的 `http://127.0.0.1:5000`。不要在生产数据环境直接运行。

## 构建、测试与镜像

```bash
dotnet test VeriScan.slnx
dotnet build VeriScan.slnx -c Release
dotnet format VeriScan.slnx --verify-no-changes
pnpm --dir apps/admin test
pnpm --dir apps/admin lint
pnpm --dir apps/admin typecheck
pnpm --dir apps/admin build
pnpm --dir apps/admin format:check
```

```bash
docker build -f apps/api/Dockerfile -t veriscan-api:local .
docker build -f apps/admin/Dockerfile -t veriscan-admin:local \
  --build-arg VITE_OIDC_AUTHORITY=https://identity.example/realms/veriscan \
  --build-arg VITE_OIDC_REDIRECT_URI=https://admin.example/auth/callback .
```

API 镜像默认启动服务，也可执行一次性迁移 Job：`dotnet VeriScan.Api.dll --migrate`。生产部署必须从 Secret 管理系统注入数据库、Redis、API Key pepper 与 AI 凭据加密主密钥，不得写入镜像或仓库。

## 项目文档

- [实施计划](docs/IMPLEMENTATION_PLAN.md)
- [验收报告](docs/ACCEPTANCE_REPORT.md)
- [技术方案](VeriScan-CMS-%E6%8A%80%E6%9C%AF%E6%96%B9%E6%A1%88-v2.md)
