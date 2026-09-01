# VeriScan 本地基础设施

本目录提供开发环境所需的 PostgreSQL、Redis 和 Keycloak。三项服务均通过固定版本镜像启动，数据使用 Docker 命名卷持久化；Redis 只承担缓存、限流和通知，不是业务事实源。Keycloak 使用同一个 PostgreSQL 实例中的独立数据库，避免与业务迁移共用 schema。

## 启动

需要 Docker Desktop 或兼容 Docker Compose v2 的运行时。

```bash
cp infra/.env.example infra/.env
docker compose --env-file infra/.env -f infra/compose.yaml up -d
docker compose --env-file infra/.env -f infra/compose.yaml ps
```

复制模板后即可使用本地开发值启动；密码变量必须显式来自 `infra/.env`，Compose 不为敏感变量提供回退值。所有示例值都不得带入生产环境。

```bash
docker compose --env-file infra/.env -f infra/compose.yaml up -d
```

服务地址：

| 服务 | 本地地址 | 用途 |
| --- | --- | --- |
| PostgreSQL | `localhost:5432` | 业务权威数据库 |
| Redis | `localhost:6379` | 缓存、限流、失效通知 |
| Keycloak | `http://localhost:8080` | 本地 OIDC/MFA 管理认证 |
| Keycloak 管理台 | `http://localhost:8080/admin` | 使用 `.env` 中的 bootstrap 管理员登录 |
| 本地 Realm | `veriscan-local` | 管理后台 OIDC 客户端所在 Realm |

`veriscan-admin` 是使用 Authorization Code + PKCE 的公开 SPA 客户端，`veriscan-api` 是 API audience。Realm 在管理后台的 access token 中注入 `veriscan-api` audience，API 开发环境也会校验该值，不使用“本地关闭 audience校验”的宽松配置。

Keycloak 会在首次初始化时导入 `keycloak/realm/veriscan-local-realm.json`。该文件不包含用户、客户端密钥或生产凭证。登录管理后台前，在 Keycloak 的 `veriscan-local` Realm 中创建本地开发用户，并按需授予 `veriscan-admin`、`veriscan-operator` 或 `veriscan-auditor` 角色。

## 配置与数据

- `infra/.env.example` 是不含真实凭证的模板；实际 `infra/.env` 已被忽略，不应提交。
- PostgreSQL 数据保存在 `${COMPOSE_PROJECT_NAME:-veriscan}_postgres_data` 卷。
- Redis AOF 数据保存在 `${COMPOSE_PROJECT_NAME:-veriscan}_redis_data` 卷。
- Keycloak 使用 PostgreSQL 实例中的 `${POSTGRES_KEYCLOAK_DB}` 独立数据库保存 Realm 数据，不与业务数据库共用 schema。
- 主机端口只绑定到 `127.0.0.1`，避免把本地依赖暴露到局域网。

查看日志和停止服务：

```bash
docker compose --env-file infra/.env -f infra/compose.yaml logs -f keycloak
docker compose --env-file infra/.env -f infra/compose.yaml down
```

`down` 默认保留命名卷。只有明确需要清理本地数据时，才执行：

```bash
docker compose --env-file infra/.env -f infra/compose.yaml down -v
```

## 健康检查

Compose 会等待 PostgreSQL 通过健康检查后再启动 Keycloak。PostgreSQL 首次初始化时会通过 `postgres/init/001-create-keycloak-database.sh` 创建独立的 Keycloak 数据库；如果已经存在旧数据卷，需要手动创建该数据库或重新初始化本地卷。Redis 和 Keycloak 也提供健康检查。Keycloak 的就绪探针使用镜像内置 Bash 的 TCP 连接能力请求管理端口 `/health/ready`，不依赖镜像中未保证存在的 curl；该端口只在容器网络内可用。应用自身应继续实现独立的 liveness/readiness 检查，不应把本地 Compose 状态当作生产可用性证明。

提交前可以不启动容器，仅校验 Compose 配置。由于密码变量是必填项，使用示例文件校验：

```bash
docker compose --env-file infra/.env.example -f infra/compose.yaml config --quiet
```

## 生产边界

此 Compose 仅用于本地开发和集成测试，不提供高可用、备份、TLS 终止、密钥托管或生产数据隔离。生产环境应使用受管 PostgreSQL/Redis、独立 Keycloak 部署、外部密钥管理系统、固定镜像 digest、网络策略和恢复演练；不得复用示例密码、默认端口暴露或本地 Realm 数据。
