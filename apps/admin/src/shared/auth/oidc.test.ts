import { describe, expect, it } from 'vitest';
import { createOidcUserManager } from './oidc';

describe('OIDC 管理登录配置', () => {
  it('使用 Authorization Code + PKCE 配置，并将状态放入 sessionStorage', async () => {
    const manager = createOidcUserManager({
      authority: 'https://sso.example.com/realms/veriscan',
      clientId: 'veriscan-admin',
      redirectUri: 'http://127.0.0.1:5173/auth/callback',
    });
    if (!manager) throw new Error('OIDC manager should be created in browser tests');

    await manager.settings.stateStore.set('proof', 'state-value');
    await manager.settings.userStore.set('user', 'user-value');

    expect(manager.settings.response_type).toBe('code');
    expect(manager.settings.disablePKCE).toBe(false);
    expect(window.sessionStorage.length).toBe(2);
    expect(window.localStorage.length).toBe(0);
  });
});
