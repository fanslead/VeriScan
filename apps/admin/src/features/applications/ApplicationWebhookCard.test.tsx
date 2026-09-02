import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ApplicationWebhook } from '@/shared/api/types';
import { moderationService } from '@/shared/api/services';
import { ApplicationWebhookCard } from './ApplicationWebhookCard';

vi.mock('@/shared/auth/permissions', () => ({ useAdminCapability: () => true }));
vi.mock('@/shared/api/services', () => ({
  moderationService: {
    getApplicationWebhook: vi.fn(),
    saveApplicationWebhook: vi.fn(),
    setApplicationWebhookStatus: vi.fn(),
    testApplicationWebhook: vi.fn(),
    getApplicationWebhookTest: vi.fn(),
    rotateApplicationWebhookSecret: vi.fn(),
  },
}));

const applicationId = '8f68a4cc-42ca-4e2f-a1fc-5f62c0a76015';

const unconfiguredWebhook: ApplicationWebhook = {
  configured: false,
  id: null,
  applicationId,
  endpointUrl: null,
  enabled: false,
  revision: null,
  currentRevisionTested: false,
  lastTestId: null,
  lastTestStatus: null,
  lastTestHttpStatusCode: null,
  lastTestLatencyMilliseconds: null,
  lastTestedAt: null,
  updatedAt: null,
};

const renderCard = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  render(
    <QueryClientProvider client={queryClient}>
      <ApplicationWebhookCard applicationId={applicationId} />
    </QueryClientProvider>,
  );
  return queryClient;
};

describe('ApplicationWebhookCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('只把一次性签名密钥放入确认弹窗，不写入查询缓存', async () => {
    const user = userEvent.setup();
    let current = structuredClone(unconfiguredWebhook);
    vi.mocked(moderationService.getApplicationWebhook).mockImplementation(async () => current);
    vi.mocked(moderationService.saveApplicationWebhook).mockImplementation(
      async (_id, endpointUrl) => {
        current = {
          ...current,
          configured: true,
          id: '43cf608e-d9a4-44e7-844e-c51d5b23bd5a',
          endpointUrl,
          revision: 1,
          updatedAt: '2026-09-02T05:00:00Z',
        };
        return { webhook: current, signingSecret: 'whsec_only_shown_once' };
      },
    );
    const queryClient = renderCard();

    const endpoint = await screen.findByLabelText('接收地址');
    await user.type(endpoint, 'https://hooks.example.com/moderation');
    await user.click(screen.getByRole('button', { name: '保存地址' }));

    expect(await screen.findByText('请保存 Webhook 签名密钥')).toBeInTheDocument();
    expect(screen.getByLabelText('Webhook 签名密钥')).toHaveTextContent('whsec_only_shown_once');
    expect(queryClient.getQueryData(['application-webhook', applicationId])).toEqual(current);
    expect(
      JSON.stringify(
        queryClient
          .getQueryCache()
          .getAll()
          .map((query) => query.state.data),
      ),
    ).not.toContain('whsec_only_shown_once');
  });

  it('连接测试通过前禁止启用，通过后才允许打开通知', async () => {
    const user = userEvent.setup();
    let current: ApplicationWebhook = {
      ...unconfiguredWebhook,
      configured: true,
      id: '43cf608e-d9a4-44e7-844e-c51d5b23bd5a',
      endpointUrl: 'https://hooks.example.com/moderation',
      revision: 1,
      updatedAt: '2026-09-02T05:00:00Z',
    };
    vi.mocked(moderationService.getApplicationWebhook).mockImplementation(async () => current);
    vi.mocked(moderationService.testApplicationWebhook).mockResolvedValue({
      testId: 'a7585725-548f-46b7-965e-f548e1732b03',
      statusUrl: `/api/admin/v1/applications/${applicationId}/webhook/tests/test-id`,
      submittedAt: '2026-09-02T05:01:00Z',
    });
    vi.mocked(moderationService.getApplicationWebhookTest).mockImplementation(async () => {
      current = {
        ...current,
        currentRevisionTested: true,
        lastTestId: 'a7585725-548f-46b7-965e-f548e1732b03',
        lastTestStatus: 'succeeded',
        lastTestHttpStatusCode: 204,
        lastTestLatencyMilliseconds: 18,
        lastTestedAt: '2026-09-02T05:01:01Z',
      };
      return {
        testId: current.lastTestId!,
        applicationId,
        configurationRevision: 1,
        status: 'succeeded',
        httpStatusCode: 204,
        latencyMilliseconds: 18,
        failureCode: null,
        submittedAt: '2026-09-02T05:01:00Z',
        completedAt: '2026-09-02T05:01:01Z',
      };
    });
    vi.mocked(moderationService.setApplicationWebhookStatus).mockImplementation(
      async (_id, enabled) => {
        current = { ...current, enabled };
        return current;
      },
    );
    renderCard();

    const statusSwitch = await screen.findByLabelText('启用 Webhook 通知');
    expect(statusSwitch).toBeDisabled();
    await user.click(screen.getByRole('button', { name: /发送测试/ }));

    await waitFor(() => expect(screen.getAllByText('连接正常')).toHaveLength(2));
    await waitFor(() => expect(statusSwitch).toBeEnabled());
    await user.click(statusSwitch);

    await waitFor(() =>
      expect(moderationService.setApplicationWebhookStatus).toHaveBeenCalledWith(
        applicationId,
        true,
      ),
    );
    expect(await screen.findByText('通知已启用')).toBeInTheDocument();
  });

  it('后台状态刷新时保留尚未保存的地址草稿', async () => {
    const user = userEvent.setup();
    const current: ApplicationWebhook = {
      ...unconfiguredWebhook,
      configured: true,
      id: '43cf608e-d9a4-44e7-844e-c51d5b23bd5a',
      endpointUrl: 'https://hooks.example.com/original',
      revision: 1,
      updatedAt: '2026-09-02T05:00:00Z',
    };
    vi.mocked(moderationService.getApplicationWebhook).mockResolvedValue(current);
    const queryClient = renderCard();

    const endpoint = await screen.findByLabelText('接收地址');
    await user.clear(endpoint);
    await user.type(endpoint, 'https://hooks.example.com/draft');
    queryClient.setQueryData(['application-webhook', applicationId], {
      ...current,
      updatedAt: '2026-09-02T05:02:00Z',
    });

    await waitFor(() =>
      expect(screen.getByLabelText('接收地址')).toHaveValue('https://hooks.example.com/draft'),
    );
    expect(screen.getByText('有未保存的更改')).toBeInTheDocument();
  });
});
