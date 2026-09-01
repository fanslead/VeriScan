import type { ReactNode } from 'react';
import { useAuthStore } from '@/shared/auth/authStore';
import { isMockMode } from '@/shared/auth/oidc';
import { LoadingBlock } from '@/shared/ui/LoadingBlock';
import { LoginPage } from './LoginPage';

export function AuthGate({ children }: { children: ReactNode }) {
  const status = useAuthStore((state) => state.status);
  if (isMockMode || status === 'authenticated') return children;
  if (status === 'loading') {
    return (
      <div className="auth-screen" role="status" aria-label="正在准备管理后台">
        <div className="auth-panel auth-panel--loading">
          <span className="auth-mark" aria-hidden="true">
            ◌
          </span>
          <LoadingBlock rows={3} />
        </div>
      </div>
    );
  }
  if (status === 'unavailable') return <LoginPage unavailable />;
  return <LoginPage />;
}
