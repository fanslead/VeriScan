import { create } from 'zustand';
import type { User, UserManager } from 'oidc-client-ts';
import { createOidcUserManager, getOidcConfiguration, isMockMode } from './oidc';

export type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated' | 'unavailable';

interface AuthState {
  status: AuthStatus;
  user: User | null;
  manager: UserManager | null;
  error: string | null;
  bootstrap: () => Promise<void>;
  startLogin: (returnPath?: string) => Promise<void>;
  completeCallback: (url?: string) => Promise<string | null>;
  logout: () => Promise<void>;
  getAccessToken: () => string | null;
  handleUnauthorized: () => Promise<void>;
}

let bootstrapPromise: Promise<void> | null = null;

const callbackReturnPath = (user: User): string | null => {
  if (typeof user.state !== 'object' || user.state === null) return null;
  const state = user.state as { returnPath?: unknown };
  return typeof state.returnPath === 'string' && state.returnPath.startsWith('/')
    ? state.returnPath
    : null;
};

export const useAuthStore = create<AuthState>((set, get) => ({
  status: 'loading',
  user: null,
  manager: null,
  error: null,

  bootstrap: async () => {
    if (bootstrapPromise) return bootstrapPromise;
    bootstrapPromise = (async () => {
      if (isMockMode) {
        set({ status: 'authenticated', user: null, manager: null, error: null });
        return;
      }

      const configuration = getOidcConfiguration();
      if (!configuration) {
        set({
          status: 'unavailable',
          user: null,
          manager: null,
          error: '当前管理入口暂未开放，请联系系统管理员。',
        });
        return;
      }

      try {
        const manager = createOidcUserManager(configuration);
        if (!manager) throw new Error('OIDC manager is unavailable');
        const user = await manager.getUser();
        if (!user || user.expired) {
          if (user?.expired) await manager.removeUser();
          set({ status: 'unauthenticated', user: null, manager, error: null });
          return;
        }
        set({ status: 'authenticated', user, manager, error: null });
      } catch {
        set({
          status: 'unavailable',
          user: null,
          manager: null,
          error: '登录服务暂时不可用，请稍后重试或联系系统管理员。',
        });
      }
    })().finally(() => {
      bootstrapPromise = null;
    });
    return bootstrapPromise;
  },

  startLogin: async (returnPath = `${window.location.pathname}${window.location.search}`) => {
    const manager = get().manager ?? createOidcUserManager();
    if (!manager) {
      set({ status: 'unavailable', error: '当前管理入口暂未开放，请联系系统管理员。' });
      return;
    }
    set({ manager, error: null });
    try {
      await manager.signinRedirect({
        state: { returnPath: returnPath.startsWith('/') ? returnPath : '/' },
      });
    } catch {
      set({ status: 'unauthenticated', error: '无法打开登录页面，请稍后重试。' });
    }
  },

  completeCallback: async (url) => {
    const manager = get().manager ?? createOidcUserManager();
    if (!manager) throw new Error('当前管理入口暂未开放');
    set({ manager, status: 'loading', error: null });
    try {
      const user = await manager.signinCallback(url);
      if (!user || user.expired) throw new Error('登录状态已失效');
      set({ status: 'authenticated', user, manager, error: null });
      return callbackReturnPath(user);
    } catch (error) {
      set({ status: 'unauthenticated', user: null, manager, error: '登录未完成，请重新尝试。' });
      throw error;
    }
  },

  logout: async () => {
    const manager = get().manager;
    set({ status: 'unauthenticated', user: null, error: null });
    if (manager) {
      try {
        await manager.signoutRedirect();
      } catch {
        await manager.removeUser();
      }
    }
  },

  getAccessToken: () => get().user?.access_token ?? null,

  handleUnauthorized: async () => {
    const manager = get().manager;
    if (manager) {
      try {
        await manager.removeUser();
      } catch {
        set({ error: '登录状态已失效，请重新登录。' });
      }
    }
    set({ status: 'unauthenticated', user: null, error: '登录状态已失效，请重新登录。' });
    if (manager && !window.location.pathname.startsWith('/auth/')) {
      await get().startLogin();
    }
  },
}));
