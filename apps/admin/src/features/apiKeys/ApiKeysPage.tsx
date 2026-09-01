import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Modal, Skeleton, Tag, Toast, Typography } from '@douyinfe/semi-ui';
import { IconArrowLeft, IconKey, IconPlus } from '@douyinfe/semi-icons';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import type { ApiKey, CreateKeyInput, OneTimeApiKey } from '@/shared/api/types';
import { moderationService } from '@/shared/api/services';
import { ConfirmDangerModal } from '@/shared/ui/ConfirmDangerModal';
import { EmptyState } from '@/shared/ui/EmptyState';
import { ErrorState } from '@/shared/ui/ErrorState';
import { OneTimeKeyDialog } from './OneTimeKeyDialog';
import { ApiKeysTable } from './ApiKeysTable';
import { CreateApiKeyModal } from './CreateApiKeyModal';
import { useAdminCapability } from '@/shared/auth/permissions';

export function ApiKeysPage() {
  const canManage = useAdminCapability('operate');
  const { appId = '' } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [createVisible, setCreateVisible] = useState(searchParams.get('create') === '1');
  const [revokeTarget, setRevokeTarget] = useState<ApiKey | null>(null);
  const [rotateTarget, setRotateTarget] = useState<ApiKey | null>(null);
  const [revealPayload, setRevealPayload] = useState<OneTimeApiKey | null>(null);
  const [isRotation, setIsRotation] = useState(false);
  const application = useQuery({
    queryKey: ['application', appId],
    queryFn: () => moderationService.getApplication(appId),
    enabled: Boolean(appId),
  });
  const keys = useQuery({
    queryKey: ['application-keys', appId],
    queryFn: () => moderationService.listKeys(appId),
    enabled: Boolean(appId),
  });

  useEffect(() => {
    if (searchParams.get('create') !== '1') return;
    setCreateVisible(true);
    const next = new URLSearchParams(searchParams);
    next.delete('create');
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['application-keys', appId] });
    void queryClient.invalidateQueries({ queryKey: ['application', appId] });
  };
  const createMutation = useMutation({
    mutationFn: moderationService.createKey,
    onSuccess: (payload) => {
      setCreateVisible(false);
      setRevealPayload(payload);
      refresh();
    },
  });
  const rotateMutation = useMutation({
    mutationFn: moderationService.rotateKey,
    onSuccess: (payload) => {
      setRotateTarget(null);
      setIsRotation(true);
      setRevealPayload(payload);
      refresh();
    },
    onError: () => Toast.error({ content: '轮换失败，请稍后重试' }),
  });
  const revokeMutation = useMutation({
    mutationFn: moderationService.revokeKey,
    onSuccess: () => {
      setRevokeTarget(null);
      Toast.success({ content: 'API Key 已撤销' });
      refresh();
    },
    onError: () => Toast.error({ content: '撤销失败，请稍后重试' }),
  });

  if (application.isPending || keys.isPending)
    return (
      <div className="page-stack">
        <Skeleton.Title style={{ width: 340 }} />
        <Skeleton.Paragraph rows={7} />
      </div>
    );
  if (application.isError || keys.isError)
    return (
      <div className="page-stack">
        <ErrorState
          title="凭证暂时无法加载"
          onRetry={() => {
            void application.refetch();
            void keys.refetch();
          }}
        />
      </div>
    );
  const app = application.data;
  const rows = keys.data;
  const submitCreate = (input: CreateKeyInput) => createMutation.mutate(input);

  return (
    <div className="page-stack api-keys-page">
      <button
        className="back-link"
        type="button"
        onClick={() => navigate(`/applications/${app.id}`)}
      >
        <IconArrowLeft />
        返回 {app.name}
      </button>
      <div className="page-intro">
        <div>
          <div className="eyebrow">{app.name} · 接入密钥</div>
          <Typography.Title heading={1}>API Key</Typography.Title>
          <Typography.Text type="tertiary">
            凭证只在创建或轮换后显示一次；每枚 Key 都可以独立撤销。
          </Typography.Text>
        </div>
        {canManage ? (
          <div className="page-actions">
            <Button
              theme="solid"
              type="primary"
              icon={<IconPlus />}
              onClick={() => setCreateVisible(true)}
            >
              创建 API Key
            </Button>
          </div>
        ) : null}
      </div>
      <div className="key-policy-banner">
        <div className="key-policy-banner__icon">
          <IconKey />
        </div>
        <div>
          <strong>当前有 {rows.filter((key) => key.status === 'active').length} 枚有效凭证</strong>
          <span>建议为生产、预发布等不同环境分别使用独立 Key，并设置到期时间。</span>
        </div>
      </div>
      <Card className="panel table-panel">
        <div className="table-toolbar">
          <div>
            <span className="section-kicker">全部接入密钥</span>
            <Typography.Title heading={4}>凭证列表</Typography.Title>
          </div>
          <span className="table-count">{rows.length} 枚</span>
        </div>
        {rows.length === 0 ? (
          <EmptyState
            title="还没有 API Key"
            description={
              canManage ? '为应用创建第一枚凭证，开始接入审核接口。' : '当前应用尚未创建接入凭证。'
            }
            actionText={canManage ? '创建 API Key' : undefined}
            onAction={canManage ? () => setCreateVisible(true) : undefined}
          />
        ) : (
          <ApiKeysTable
            rows={rows}
            canManage={canManage}
            onRotate={setRotateTarget}
            onRevoke={setRevokeTarget}
          />
        )}
      </Card>
      <div className="page-footnote">
        <Tag color="grey">安全提示</Tag>
        <span>API Key 仅用于服务端调用，不要放入浏览器、移动端或代码仓库。</span>
      </div>
      {canManage ? (
        <CreateApiKeyModal
          visible={createVisible}
          applicationId={app.id}
          applicationName={app.name}
          loading={createMutation.isPending}
          onCancel={() => setCreateVisible(false)}
          onSubmit={submitCreate}
        />
      ) : null}
      <OneTimeKeyDialog
        visible={Boolean(revealPayload)}
        payload={revealPayload}
        rotation={isRotation}
        onClose={() => {
          setRevealPayload(null);
          setIsRotation(false);
        }}
      />
      {canManage ? (
        <ConfirmDangerModal
          visible={Boolean(revokeTarget)}
          title="撤销这枚 API Key？"
          description={`撤销后，使用“${revokeTarget?.name ?? ''}”的服务会立即无法通过认证。此操作不可恢复。`}
          confirmText="确认撤销"
          loading={revokeMutation.isPending}
          onCancel={() => setRevokeTarget(null)}
          onConfirm={(reason) => {
            if (revokeTarget) {
              revokeMutation.mutate({ applicationId: app.id, keyId: revokeTarget.id, reason });
            }
          }}
        />
      ) : null}
      {canManage ? (
        <Modal
          visible={Boolean(rotateTarget)}
          title="生成新的 API Key？"
          onCancel={() => setRotateTarget(null)}
          onOk={() => {
            if (rotateTarget) rotateMutation.mutate(rotateTarget);
          }}
          okText="生成新 Key"
          cancelText="先不操作"
          confirmLoading={rotateMutation.isPending}
        >
          <div className="rotate-confirm-copy">
            <Typography.Text>
              会为“{rotateTarget?.name ?? ''}”生成一枚新的凭证，旧 Key
              在切换完成前仍然有效。完成切换后，请回到列表手动撤销旧 Key。
            </Typography.Text>
          </div>
        </Modal>
      ) : null}
    </div>
  );
}
