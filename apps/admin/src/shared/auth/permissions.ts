import type { User } from 'oidc-client-ts';
import { useAuthStore } from './authStore';
import { isMockMode } from './oidc';

export type AdminCapability =
  | 'view'
  | 'operate'
  | 'editRules'
  | 'editAi'
  | 'publish'
  | 'audit'
  | 'platform';

const capabilityRoles: Record<AdminCapability, string[]> = {
  view: [
    'veriscan-viewer',
    'veriscan-operator',
    'veriscan-ruleset-editor',
    'veriscan-ai-config-editor',
    'veriscan-publisher',
    'veriscan-auditor',
    'veriscan-platform-admin',
  ],
  operate: ['veriscan-operator', 'veriscan-platform-admin'],
  editRules: ['veriscan-ruleset-editor', 'veriscan-platform-admin'],
  editAi: ['veriscan-ai-config-editor', 'veriscan-platform-admin'],
  publish: ['veriscan-publisher', 'veriscan-platform-admin'],
  audit: ['veriscan-auditor', 'veriscan-platform-admin'],
  platform: ['veriscan-platform-admin'],
};

const readRoles = (user: User | null): Set<string> => {
  const profile = user?.profile as Record<string, unknown> | undefined;
  const direct = profile?.role;
  const realmAccess = profile?.realm_access as { roles?: unknown } | undefined;
  const values = [
    ...(typeof direct === 'string' ? [direct] : []),
    ...(Array.isArray(direct)
      ? direct.filter((role): role is string => typeof role === 'string')
      : []),
    ...(Array.isArray(realmAccess?.roles)
      ? realmAccess.roles.filter((role): role is string => typeof role === 'string')
      : []),
  ];
  return new Set(values);
};

export const hasAdminCapability = (user: User | null, capability: AdminCapability): boolean => {
  if (isMockMode) return true;
  const roles = readRoles(user);
  if (roles.has('veriscan-admin')) return true;
  return capabilityRoles[capability].some((role) => roles.has(role));
};

export const useAdminCapability = (capability: AdminCapability): boolean => {
  const user = useAuthStore((state) => state.user);
  return hasAdminCapability(user, capability);
};
