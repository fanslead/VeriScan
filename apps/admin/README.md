# VeriScan 管理后台

基于 Vite 6、React 19、TypeScript 与 Semi Design 的管理后台前端。

## 本地运行

```bash
pnpm install
pnpm --filter @veriscan/admin dev
```

默认按真实管理接口启动，接口基址为 `VITE_API_BASE_URL`，未配置时使用同源 `/api/admin/v1`。本地开发服务器将 `/api` 代理到 `VITE_API_PROXY_TARGET`，示例值见 `.env.example`。

本地演示需要显式设置 `VITE_API_MODE=mock`。mock 模式不需要后端或登录服务；生产构建始终使用真实接口。未设置 OIDC 或配置不完整时，真实模式会安全停留在登录入口，不会降级为演示数据。

真实模式使用 Keycloak OIDC Authorization Code + PKCE。请在身份提供方登记 `VITE_OIDC_REDIRECT_URI`，并配置 `VITE_OIDC_AUTHORITY`、`VITE_OIDC_CLIENT_ID`。登录状态由 `oidc-client-ts` 保存到浏览器 `sessionStorage`，管理接口请求自动携带 Bearer Token；401 会统一回到登录流程。

管理接口契约位于 `/api/admin/v1`：应用使用 `/applications`，凭证使用 `/applications/{applicationId}/api-keys`，轮换为 `POST .../{keyId}/rotate`，撤销为 `DELETE .../{keyId}`。页面通过独立 adapter 将后端 DTO 映射为视图模型。

## 质量检查

```bash
pnpm --filter @veriscan/admin lint
pnpm --filter @veriscan/admin typecheck
pnpm --filter @veriscan/admin test
pnpm --filter @veriscan/admin build
```

应用、凭证、审核记录分别位于 `src/features`，请求协议、Problem Details 错误映射和 DTO adapter 位于 `src/shared/api`。API Key 完整明文只在创建或轮换成功后显示一次；轮换先生成新凭证，旧凭证在切换完成前保持有效，之后由操作者单独撤销。
