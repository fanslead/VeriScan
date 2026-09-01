import { useEffect, type PropsWithChildren } from 'react';
import { useAuthStore } from './authStore';

export function AuthProvider({ children }: PropsWithChildren) {
  const manager = useAuthStore((state) => state.manager);

  useEffect(() => {
    void useAuthStore.getState().bootstrap();
  }, []);

  useEffect(() => {
    if (!manager) return undefined;
    const removeLoaded = manager.events.addUserLoaded((user) => {
      useAuthStore.setState({ status: 'authenticated', user, error: null });
    });
    const removeUnloaded = manager.events.addUserUnloaded(() => {
      useAuthStore.setState({ status: 'unauthenticated', user: null });
    });
    const removeExpired = manager.events.addAccessTokenExpired(() => {
      void useAuthStore.getState().handleUnauthorized();
    });
    return () => {
      removeLoaded();
      removeUnloaded();
      removeExpired();
    };
  }, [manager]);

  return children;
}
