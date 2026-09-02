import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Input, Modal, Skeleton, Switch, Tag, Toast } from '@douyinfe/semi-ui';
import { IconKey, IconLink, IconRefresh, IconSend, IconTickCircle } from '@douyinfe/semi-icons';
import { moderationService } from '@/shared/api/services';
import type { WebhookTestStatus } from '@/shared/api/types';
import { ApiHttpError } from '@/shared/api/errors';
import { useAdminCapability } from '@/shared/auth/permissions';
import { WebhookSecretDialog } from './WebhookSecretDialog';

const terminalTestStatuses: WebhookTestStatus[] = ['succeeded', 'failed'];

const errorMessage = (error: unknown, fallback: string) =>
  error instanceof ApiHttpError ? error.message : fallback;

const testStatusCopy: Record<
  WebhookTestStatus,
  { title: string; detail: string; tone: 'grey' | 'blue' | 'green' | 'red' }
> = {
  pending: { title: '等待发送', detail: '测试通知已进入发送流程', tone: 'blue' },
  delivering: { title: '正在确认', detail: '等待接收服务返回结果', tone: 'blue' },
  succeeded: { title: '连接正常', detail: '当前地址已通过连接测试', tone: 'green' },
  failed: { title: '连接失败', detail: '请检查接收地址后重新测试', tone: 'red' },
};

export function ApplicationWebhookCard({ applicationId }: { applicationId: string }) {
  const canOperate = useAdminCapability('operate');
  const queryClient = useQueryClient();
  const [endpointUrl, setEndpointUrl] = useState('');
  const [activeTestId, setActiveTestId] = useState<string | null>(null);
  const [signingSecret, setSigningSecret] = useState<string | null>(null);
  const [secretRotation, setSecretRotation] = useState(false);
  const reportedTestId = useRef<string | null>(null);
  const syncedEndpointRef = useRef('');
  const queryKey = useMemo(() => ['application-webhook', applicationId] as const, [applicationId]);

  useEffect(() => {
    syncedEndpointRef.current = '';
    setEndpointUrl('');
    setActiveTestId(null);
    setSigningSecret(null);
    reportedTestId.current = null;
  }, [applicationId]);

  const webhookQuery = useQuery({
    queryKey,
    queryFn: () => moderationService.getApplicationWebhook(applicationId),
    enabled: Boolean(applicationId),
  });

  useEffect(() => {
    if (!webhookQuery.data) return;
    const savedEndpoint = webhookQuery.data.endpointUrl ?? '';
    const previouslySyncedEndpoint = syncedEndpointRef.current;
    setEndpointUrl((current) => (current === previouslySyncedEndpoint ? savedEndpoint : current));
    syncedEndpointRef.current = savedEndpoint;
    if (
      webhookQuery.data.lastTestId &&
      webhookQuery.data.lastTestStatus &&
      !terminalTestStatuses.includes(webhookQuery.data.lastTestStatus)
    ) {
      setActiveTestId(webhookQuery.data.lastTestId);
    }
  }, [webhookQuery.data]);

  const testQuery = useQuery({
    queryKey: ['application-webhook-test', applicationId, activeTestId],
    queryFn: () => moderationService.getApplicationWebhookTest(applicationId, activeTestId!),
    enabled: Boolean(applicationId && activeTestId),
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status && terminalTestStatuses.includes(status) ? false : 1000;
    },
  });

  useEffect(() => {
    const test = testQuery.data;
    if (
      !test ||
      !terminalTestStatuses.includes(test.status) ||
      reportedTestId.current === test.testId
    )
      return;
    reportedTestId.current = test.testId;
    void queryClient.invalidateQueries({ queryKey });
    if (test.status === 'succeeded') {
      Toast.success({ content: '连接测试通过，现在可以启用通知' });
    } else {
      Toast.error({ content: '连接测试失败，请检查接收地址后重试' });
    }
  }, [queryClient, queryKey, testQuery.data]);

  const saveMutation = useMutation({
    mutationFn: () => moderationService.saveApplicationWebhook(applicationId, endpointUrl.trim()),
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKey, saved.webhook);
      const savedEndpoint = saved.webhook.endpointUrl ?? '';
      syncedEndpointRef.current = savedEndpoint;
      setEndpointUrl(savedEndpoint);
      setActiveTestId(null);
      if (saved.signingSecret) {
        setSecretRotation(false);
        setSigningSecret(saved.signingSecret);
      }
      Toast.success({
        content: saved.webhook.revision === 1 ? '接收地址已保存' : '接收地址已更新',
      });
    },
    onError: (error) =>
      Toast.error({ content: errorMessage(error, '接收地址保存失败，请检查后重试') }),
    onSettled: () => void queryClient.invalidateQueries({ queryKey }),
  });

  const testMutation = useMutation({
    mutationFn: () => moderationService.testApplicationWebhook(applicationId),
    onSuccess: (accepted) => {
      reportedTestId.current = null;
      setActiveTestId(accepted.testId);
      Toast.info({ content: '测试通知已发送，正在确认接收结果' });
      void queryClient.invalidateQueries({ queryKey });
    },
    onError: (error) =>
      Toast.error({ content: errorMessage(error, '测试通知发送失败，请稍后重试') }),
  });

  const statusMutation = useMutation({
    mutationFn: (enabled: boolean) =>
      moderationService.setApplicationWebhookStatus(applicationId, enabled),
    onSuccess: (updated) => {
      queryClient.setQueryData(queryKey, updated);
      Toast.success({ content: updated.enabled ? 'Webhook 通知已启用' : 'Webhook 通知已停用' });
    },
    onError: (error) =>
      Toast.error({ content: errorMessage(error, '通知状态更新失败，请稍后重试') }),
    onSettled: () => void queryClient.invalidateQueries({ queryKey }),
  });

  const rotateMutation = useMutation({
    mutationFn: () => moderationService.rotateApplicationWebhookSecret(applicationId),
    onSuccess: (rotated) => {
      setSecretRotation(true);
      setSigningSecret(rotated.signingSecret);
      setActiveTestId(null);
      Toast.success({ content: '签名密钥已轮换' });
    },
    onError: (error) =>
      Toast.error({ content: errorMessage(error, '签名密钥轮换失败，请稍后重试') }),
    onSettled: () => void queryClient.invalidateQueries({ queryKey }),
  });

  if (webhookQuery.isPending) {
    return (
      <Card className="panel webhook-panel">
        <Skeleton.Title style={{ width: 220 }} />
        <Skeleton.Paragraph rows={3} />
      </Card>
    );
  }

  if (webhookQuery.isError) {
    return (
      <Card className="panel webhook-panel">
        <div className="webhook-load-error">
          <div>
            <strong>Webhook 配置暂时无法加载</strong>
            <span>请稍后重试，不会影响现有通知设置。</span>
          </div>
          <Button onClick={() => void webhookQuery.refetch()}>重新加载</Button>
        </div>
      </Card>
    );
  }

  const webhook = webhookQuery.data;
  const savedEndpoint = webhook.endpointUrl ?? '';
  const endpointChanged = endpointUrl.trim() !== savedEndpoint;
  const busy =
    saveMutation.isPending ||
    testMutation.isPending ||
    statusMutation.isPending ||
    rotateMutation.isPending;
  const testStatus = testQuery.data?.status ?? webhook.lastTestStatus;
  const testDetails = testStatus ? testStatusCopy[testStatus] : null;
  const testLatency = testQuery.data?.latencyMilliseconds ?? webhook.lastTestLatencyMilliseconds;
  const canEnable = webhook.currentRevisionTested && !endpointChanged;
  const workflowStage = !webhook.configured
    ? 0
    : !webhook.currentRevisionTested
      ? 1
      : webhook.enabled
        ? 3
        : 2;

  const toggleStatus = (enabled: boolean) => {
    if (enabled && !canEnable) {
      Toast.warning({ content: endpointChanged ? '请先保存新地址并完成测试' : '请先完成连接测试' });
      return;
    }
    statusMutation.mutate(enabled);
  };

  const confirmRotation = () => {
    Modal.confirm({
      title: '轮换签名密钥？',
      content: '轮换后通知会暂停。请在接收服务中更新密钥，重新测试后再启用。',
      okText: '确认轮换',
      cancelText: '暂不轮换',
      onOk: () => rotateMutation.mutate(),
    });
  };

  return (
    <>
      <Card
        className="panel webhook-panel"
        title={
          <div className="panel-heading webhook-panel__heading">
            <div>
              <span className="section-kicker">结果通知</span>
              <h2>Webhook</h2>
            </div>
            <div className="webhook-panel__switch">
              <div>
                <strong>{webhook.enabled ? '通知已启用' : '通知未启用'}</strong>
                <span>{webhook.enabled ? '异步审核完成后发送' : '不会发送新的结果通知'}</span>
              </div>
              <Switch
                aria-label="启用 Webhook 通知"
                checked={webhook.enabled}
                loading={statusMutation.isPending}
                disabled={!canOperate || busy || (!webhook.enabled && !canEnable)}
                onChange={toggleStatus}
              />
            </div>
          </div>
        }
      >
        <div className="webhook-flow" aria-label="Webhook 配置进度">
          {['保存地址', '连接测试', '启用通知'].map((label, index) => (
            <div
              className={`webhook-flow__step${index < workflowStage ? ' is-complete' : ''}${
                index === workflowStage ? ' is-current' : ''
              }`}
              key={label}
            >
              <span>{index < workflowStage ? <IconTickCircle /> : index + 1}</span>
              <strong>{label}</strong>
            </div>
          ))}
        </div>

        <div className="webhook-panel__content">
          <div className="webhook-endpoint-form">
            <label htmlFor="application-webhook-url">接收地址</label>
            <Input
              id="application-webhook-url"
              prefix={<IconLink />}
              value={endpointUrl}
              disabled={!canOperate || busy}
              placeholder="https://hooks.example.com/moderation"
              onChange={setEndpointUrl}
            />
            <div className="webhook-endpoint-form__meta">
              <span>使用公开可访问的 HTTPS 地址</span>
              {endpointChanged && webhook.configured ? <em>有未保存的更改</em> : null}
            </div>
            <div className="webhook-actions">
              <Button
                type="primary"
                theme="solid"
                loading={saveMutation.isPending}
                disabled={!canOperate || busy || !endpointUrl.trim() || !endpointChanged}
                onClick={() => saveMutation.mutate()}
              >
                {webhook.configured ? '保存新地址' : '保存地址'}
              </Button>
              <Button
                icon={<IconSend />}
                loading={testMutation.isPending}
                disabled={!canOperate || busy || !webhook.configured || endpointChanged}
                onClick={() => testMutation.mutate()}
              >
                发送测试
              </Button>
              {webhook.configured && canOperate ? (
                <Button
                  theme="borderless"
                  icon={<IconKey />}
                  disabled={busy}
                  onClick={confirmRotation}
                >
                  轮换签名密钥
                </Button>
              ) : null}
            </div>
          </div>

          <div className={`webhook-test-state webhook-test-state--${testStatus ?? 'idle'}`}>
            <div className="webhook-test-state__icon">
              {testStatus === 'succeeded' ? <IconTickCircle /> : <IconRefresh />}
            </div>
            <div>
              <span>最近连接测试</span>
              <strong>{testDetails?.title ?? '尚未测试'}</strong>
              <p>{testDetails?.detail ?? '保存地址后，发送一条测试通知确认接收状态。'}</p>
              {testStatus === 'succeeded' && testLatency != null ? (
                <small>响应耗时 {testLatency} ms</small>
              ) : null}
            </div>
            {testDetails ? <Tag color={testDetails.tone}>{testDetails.title}</Tag> : null}
          </div>
        </div>
      </Card>

      <WebhookSecretDialog
        visible={Boolean(signingSecret)}
        signingSecret={signingSecret}
        rotation={secretRotation}
        onClose={() => setSigningSecret(null)}
      />
    </>
  );
}
