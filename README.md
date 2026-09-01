# VeriScan

明鉴智能内容审核系统。仓库采用 monorepo 管理 API、管理后台、共享契约、测试和部署资产。

## 仓库结构

```text
apps/
  api/                  ASP.NET Core 10 API
  admin/                React 19 + Semi Design 管理后台
packages/
  contracts/            规划中的 OpenAPI 派生客户端与共享契约
tests/
  backend/              后端单元与集成测试
infra/                  本地依赖与部署资产
docs/                   实施、体验和决策文档
```

## 本地开发

前置环境：.NET SDK 10.0.400、Node.js 24+、pnpm 11+、Docker。

```bash
pnpm install
cp infra/.env.example infra/.env
docker compose --env-file infra/.env -f infra/compose.yaml up -d --wait
dotnet build VeriScan.slnx
pnpm build
```

首次运行 API 时，需要显式提供本地数据库连接和 API Key pepper；示例值只可用于本机开发：

```bash
ConnectionStrings__VeriScan='Host=127.0.0.1;Port=5432;Database=veriscan;Username=veriscan;Password=veriscan-local-postgres-change-me' \
ConnectionStrings__Redis='127.0.0.1:6379' \
Database__AutoMigrate=true \
Security__ApiKey__Pepper='replace-with-at-least-32-bytes-local-only' \
ASPNETCORE_URLS='http://127.0.0.1:5000' \
dotnet run --project apps/api/Api
```

API Key 身份使用进程内 L1 与 Redis L2 的短时混合缓存；撤销、轮换和应用状态变化会主动失效。Redis 未配置或暂时不可用时，鉴权会回源 PostgreSQL，不会因为缓存故障跳过密钥摘要、有效期、状态或应用状态校验。

规则集采用不可变版本治理：草稿可以批量编辑，必须通过服务端规范化、分类、权重、重复与冲突校验后才能发布；已发布版本只能复制为新草稿，仍被应用绑定的版本不能归档。应用显式绑定一个已发布的 `ruleset@...`，每个审核请求都会固化实际版本。调用方可传 `policyId` 做一致性保护，但不能借此选择其他规则版本。白词只抑制同分类的可疑信号，不会直接绕过外部 AI；除可信硬拒绝外，未决内容仍进入外部 AI，故障时返回 `review`。

管理端规则 API 位于 `/api/admin/v1/rule-sets`，应用切换版本使用 `PUT /api/admin/v1/applications/{applicationId}/rule-set`。规则后台支持批量词条录入、校验、发布、复制版本、归档和按应用绑定；列表只返回最多 8 条预览，编辑时单独读取完整版本，避免大词库把列表响应无限放大。

外部 AI 默认没有出站主机权限，因此不会因为数据库里出现任意 URL 就发起请求。启用前要显式设置允许主机和凭据引用；数据库只保存 `config://ProviderA`，明文通过服务端配置注入：

```bash
ExternalAi__AllowedHosts__0='api.openai.com' \
ExternalAi__AllowedPorts__0=443 \
ExternalAi__Credentials__ProviderA='replace-with-provider-secret' \
dotnet run --project apps/api/Api
```

不要把供应商密钥写入 `appsettings*.json`、管理台表单或 Git。生产环境应以 Vault/KMS 实现替换默认配置解析器，并用出站网络策略再次限制同一域名集合。

管理后台使用 Vite；本地开发默认地址为 `http://127.0.0.1:5173`。真实模式先将 `apps/admin/.env.example` 复制为 `apps/admin/.env.local`，并在 Keycloak 创建本地用户、授予 `veriscan-admin` 角色。Mock 数据必须通过 `VITE_API_MODE=mock` 显式启用，不会在真实模式或 OIDC 配置缺失时自动降级。

常用验证命令：

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

仓库仍在按 [实施计划](docs/IMPLEMENTATION_PLAN.md) 分阶段建设。生产架构和协议边界以 [技术方案](VeriScan-CMS-%E6%8A%80%E6%9C%AF%E6%96%B9%E6%A1%88-v2.md) 为准。
