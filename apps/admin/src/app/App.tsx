import { lazy, Suspense } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AdminLayout } from './layout/AdminLayout';
import { LoadingBlock } from '@/shared/ui/LoadingBlock';
import { AuthProvider } from '@/shared/auth/AuthProvider';
import { AuthGate } from '@/features/auth/AuthGate';
import { AuthCallbackPage } from '@/features/auth/AuthCallbackPage';
import { LoginPage } from '@/features/auth/LoginPage';
import { CapabilityGuard } from '@/shared/auth/CapabilityGuard';

const DashboardPage = lazy(async () => ({
  default: (await import('@/features/dashboard/DashboardPage')).DashboardPage,
}));
const ApiKeysPage = lazy(async () => ({
  default: (await import('@/features/apiKeys/ApiKeysPage')).ApiKeysPage,
}));
const ApplicationDetailPage = lazy(async () => ({
  default: (await import('@/features/applications/ApplicationDetailPage')).ApplicationDetailPage,
}));
const ApplicationListPage = lazy(async () => ({
  default: (await import('@/features/applications/ApplicationListPage')).ApplicationListPage,
}));
const CreateApplicationPage = lazy(async () => ({
  default: (await import('@/features/applications/CreateApplicationPage')).CreateApplicationPage,
}));
const ModerationRecordsPage = lazy(async () => ({
  default: (await import('@/features/records/ModerationRecordsPage')).ModerationRecordsPage,
}));
const RuleSetsPage = lazy(async () => ({
  default: (await import('@/features/rules/RuleSetsPage')).RuleSetsPage,
}));
const AiConfigurationsPage = lazy(async () => ({
  default: (await import('@/features/ai/AiConfigurationsPage')).AiConfigurationsPage,
}));
const AuditEventsPage = lazy(async () => ({
  default: (await import('@/features/audit/AuditEventsPage')).AuditEventsPage,
}));

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false },
    mutations: { retry: 0 },
  },
});

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Suspense
            fallback={
              <div className="route-loading">
                <LoadingBlock rows={3} />
              </div>
            }
          >
            <Routes>
              <Route path="auth/callback" element={<AuthCallbackPage />} />
              <Route path="auth/login" element={<LoginPage />} />
              <Route
                element={
                  <AuthGate>
                    <AdminLayout />
                  </AuthGate>
                }
              >
                <Route index element={<DashboardPage />} />
                <Route path="applications" element={<ApplicationListPage />} />
                <Route
                  path="applications/new"
                  element={
                    <CapabilityGuard capability="operate">
                      <CreateApplicationPage />
                    </CapabilityGuard>
                  }
                />
                <Route path="applications/:appId" element={<ApplicationDetailPage />} />
                <Route path="applications/:appId/keys" element={<ApiKeysPage />} />
                <Route path="records" element={<ModerationRecordsPage />} />
                <Route path="records/:recordId" element={<ModerationRecordsPage />} />
                <Route path="ai-settings" element={<AiConfigurationsPage />} />
                <Route path="rules" element={<RuleSetsPage />} />
                <Route
                  path="audit"
                  element={
                    <CapabilityGuard capability="audit">
                      <AuditEventsPage />
                    </CapabilityGuard>
                  }
                />
                <Route path="*" element={<Navigate to="/" replace />} />
              </Route>
            </Routes>
          </Suspense>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
