import type { User } from 'oidc-client-ts';
import { hasAdminCapability } from './permissions';

describe('hasAdminCapability', () => {
  it('平台管理员拥有全部治理能力', () => {
    const user = {
      profile: { realm_access: { roles: ['veriscan-platform-admin'] } },
    } as unknown as User;

    expect(hasAdminCapability(user, 'operate')).toBe(true);
    expect(hasAdminCapability(user, 'editRules')).toBe(true);
    expect(hasAdminCapability(user, 'publish')).toBe(true);
  });

  it('只读成员不能执行写操作', () => {
    const user = {
      profile: { realm_access: { roles: ['veriscan-viewer'] } },
    } as unknown as User;

    expect(hasAdminCapability(user, 'view')).toBe(true);
    expect(hasAdminCapability(user, 'operate')).toBe(false);
    expect(hasAdminCapability(user, 'publish')).toBe(false);
  });
});
