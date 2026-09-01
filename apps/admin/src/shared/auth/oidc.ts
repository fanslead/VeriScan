import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

export interface OidcConfiguration {
  authority: string;
  clientId: string;
  redirectUri: string;
}

export const isMockMode = !import.meta.env.PROD && import.meta.env.VITE_API_MODE === 'mock';

export function getOidcConfiguration(): OidcConfiguration | null {
  const authority = import.meta.env.VITE_OIDC_AUTHORITY?.trim();
  const clientId = import.meta.env.VITE_OIDC_CLIENT_ID?.trim();
  const redirectUri = import.meta.env.VITE_OIDC_REDIRECT_URI?.trim();
  if (!authority || !clientId || !redirectUri) return null;
  return { authority, clientId, redirectUri };
}

export function createOidcUserManager(configuration = getOidcConfiguration()): UserManager | null {
  if (!configuration || typeof window === 'undefined') return null;

  const sessionStore = window.sessionStorage;
  return new UserManager({
    authority: configuration.authority,
    client_id: configuration.clientId,
    redirect_uri: configuration.redirectUri,
    post_logout_redirect_uri: window.location.origin,
    response_type: 'code',
    scope: 'openid profile email',
    stateStore: new WebStorageStateStore({
      prefix: 'veriscan.oidc.state.',
      store: sessionStore,
    }),
    userStore: new WebStorageStateStore({
      prefix: 'veriscan.oidc.user.',
      store: sessionStore,
    }),
    automaticSilentRenew: false,
    monitorSession: false,
  });
}
