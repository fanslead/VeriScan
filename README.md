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
Database__AutoMigrate=true \
Security__ApiKey__Pepper='replace-with-at-least-32-bytes-local-only' \
ASPNETCORE_URLS='http://127.0.0.1:5000' \
dotnet run --project apps/api/Api
```

管理后台使用 Vite；本地开发默认地址为 `http://127.0.0.1:5173`。真实模式先将 `apps/admin/.env.example` 复制为 `apps/admin/.env.local`，并在 Keycloak 创建本地用户、授予 `veriscan-admin` 角色。Mock 数据必须通过 `VITE_API_MODE=mock` 显式启用，不会在真实模式或 OIDC 配置缺失时自动降级。

仓库仍在按 [实施计划](docs/IMPLEMENTATION_PLAN.md) 分阶段建设。生产架构和协议边界以 [技术方案](VeriScan-CMS-%E6%8A%80%E6%9C%AF%E6%96%B9%E6%A1%88-v2.md) 为准。
