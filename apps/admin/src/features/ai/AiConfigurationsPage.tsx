import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Tag, Toast } from '@douyinfe/semi-ui';
import { IconPlus } from '@douyinfe/semi-icons';
import { useSearchParams } from 'react-router-dom';
import { apiMode, aiConfigurationService } from '@/shared/api/services';
import { ApiHttpError } from '@/shared/api/errors';
import type {
  AiConfiguration,
  AiConfigurationDraftInput,
  AiConfigurationTestResult,
} from '@/shared/api/types';
import { ErrorState } from '@/shared/ui/ErrorState';
import { PageIntro } from '@/shared/ui/PageIntro';
import {
  AiConfigurationConfirmDialog,
  type AiConfigurationAction,
} from './AiConfigurationConfirmDialog';
import { AiConfigurationEditor } from './AiConfigurationEditor';
import { AiConfigurationList } from './AiConfigurationList';
import { AiConfigurationSummary } from './AiConfigurationSummary';
import { AiConfigurationTestDialog } from './AiConfigurationTestDialog';
import { useAdminCapability } from '@/shared/auth/permissions';

const errorMessage = (error: unknown, fallback: string) => {
  if (error instanceof ApiHttpError && error.status === 409) return error.message;
  return fallback;
};

const actionCopy: Record<AiConfigurationAction, string> = {
  publish: '配置已发布',
  activate: 'AI 路由已切换',
  archive: '配置已归档',
};

interface LifecycleRequest {
  configurationId: string;
  action: AiConfigurationAction;
}

export function AiConfigurationsPage() {
  const canEdit = useAdminCapability('editAi');
  const canPublish = useAdminCapability('publish');
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const [editorVisible, setEditorVisible] = useState(false);
  const [editorConfiguration, setEditorConfiguration] = useState<AiConfiguration | null>(null);
  const [actionTarget, setActionTarget] = useState<AiConfiguration | null>(null);
  const [action, setAction] = useState<AiConfigurationAction | null>(null);
  const [testTarget, setTestTarget] = useState<AiConfiguration | null>(null);
  const [testResult, setTestResult] = useState<AiConfigurationTestResult | null>(null);

  const configurations = useQuery({
    queryKey: ['ai-configurations'],
    queryFn: aiConfigurationService.list,
  });

  useEffect(() => {
    if (searchParams.get('create') !== '1') return;
    setEditorConfiguration(null);
    setEditorVisible(true);
    const next = new URLSearchParams(searchParams);
    next.delete('create');
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['ai-configurations'] });
  };

  const saveMutation = useMutation({
    mutationFn: ({
      configuration,
      values,
    }: {
      configuration: AiConfiguration | null;
      values: AiConfigurationDraftInput;
    }) =>
      configuration
        ? aiConfigurationService.update(configuration.id, values)
        : aiConfigurationService.create(values),
    onSuccess: (_, variables) => {
      setEditorVisible(false);
      setEditorConfiguration(null);
      Toast.success({ content: variables.configuration ? '草稿已保存' : 'AI 配置草稿已创建' });
      refresh();
    },
    onError: (error) =>
      Toast.error({ content: errorMessage(error, '保存失败，请检查填写内容后重试') }),
  });

  const testMutation = useMutation({
    mutationFn: (configurationId: string) => aiConfigurationService.test(configurationId),
    onSuccess: (result) => {
      setTestResult(result);
      refresh();
    },
    onError: (error) => {
      setTestTarget(null);
      setTestResult(null);
      Toast.error({ content: errorMessage(error, '合成测试未完成，请稍后重试') });
    },
  });

  const revisionMutation = useMutation({
    mutationFn: (configurationId: string) => aiConfigurationService.createRevision(configurationId),
    onSuccess: (draft) => {
      setEditorConfiguration(draft);
      setEditorVisible(true);
      Toast.success({ content: '新版本草稿已创建，请继续编辑' });
      refresh();
    },
    onError: (error) => Toast.error({ content: errorMessage(error, '新版本创建失败，请稍后重试') }),
  });

  const lifecycleMutation = useMutation({
    mutationFn: ({ configurationId, action: nextAction }: LifecycleRequest) => {
      if (nextAction === 'publish') return aiConfigurationService.publish(configurationId);
      if (nextAction === 'activate') return aiConfigurationService.activate(configurationId);
      return aiConfigurationService.archive(configurationId);
    },
    onSuccess: (_, variables) => {
      setActionTarget(null);
      setAction(null);
      Toast.success({ content: actionCopy[variables.action] });
      refresh();
    },
    onError: (error) => Toast.error({ content: errorMessage(error, '操作未完成，请稍后重试') }),
  });

  const rows = configurations.data ?? [];
  const busyId = saveMutation.isPending
    ? (saveMutation.variables?.configuration?.id ?? null)
    : testMutation.isPending
      ? (testTarget?.id ?? null)
      : lifecycleMutation.isPending
        ? (lifecycleMutation.variables?.configurationId ?? null)
        : revisionMutation.isPending
          ? (revisionMutation.variables ?? null)
          : null;

  const openCreate = () => {
    setEditorConfiguration(null);
    setEditorVisible(true);
  };
  const openEdit = (configuration: AiConfiguration) => {
    setEditorConfiguration(configuration);
    setEditorVisible(true);
  };
  const openAction = (configuration: AiConfiguration, nextAction: AiConfigurationAction) => {
    setActionTarget(configuration);
    setAction(nextAction);
  };
  const runTest = (configuration: AiConfiguration) => {
    setTestTarget(configuration);
    setTestResult(null);
    testMutation.mutate(configuration.id);
  };
  const createRevision = (configuration: AiConfiguration) => {
    revisionMutation.mutate(configuration.id);
  };

  if (configurations.isError) {
    return (
      <div className="page-stack">
        <PageIntro
          eyebrow="智能研判 · 模型服务"
          title="AI 配置"
          description="管理外部模型路由的版本、连接入口与生效状态。"
        />
        <ErrorState title="AI 配置暂时无法加载" onRetry={() => configurations.refetch()} />
      </div>
    );
  }

  return (
    <div className="page-stack ai-config-page">
      <PageIntro
        eyebrow="智能研判 · 模型服务"
        title="AI 配置"
        description="管理外部模型路由的版本、连接入口与生效状态。"
        actions={
          canEdit ? (
            <Button theme="solid" type="primary" icon={<IconPlus />} onClick={openCreate}>
              创建配置草稿
            </Button>
          ) : undefined
        }
      />

      {apiMode === 'mock' ? (
        <div className="ai-config-mode-note">
          <Tag color="green">演示数据</Tag>
          <span>当前使用本地演示数据，切换到真实模式后会严格以服务端配置为准。</span>
        </div>
      ) : null}

      {!configurations.isPending ? <AiConfigurationSummary configurations={rows} /> : null}
      <div className="ai-config-list-heading">
        <div>
          <span className="section-kicker">全部可用版本</span>
          <h2>模型路由版本</h2>
        </div>
        <span>{configurations.isPending ? '正在同步…' : `${rows.length} 份配置`}</span>
      </div>
      <AiConfigurationList
        configurations={rows}
        loading={configurations.isPending}
        error={false}
        busyId={busyId}
        canEdit={canEdit}
        canPublish={canPublish}
        onRetry={() => void configurations.refetch()}
        onEdit={openEdit}
        onTest={runTest}
        onCreateRevision={createRevision}
        onPublish={(configuration) => openAction(configuration, 'publish')}
        onActivate={(configuration) => openAction(configuration, 'activate')}
        onArchive={(configuration) => openAction(configuration, 'archive')}
      />
      <div className="page-footnote ai-config-page__footnote">
        <Tag color="grey">发布规则</Tag>
        <span>只有当前草稿通过合成测试后才能发布；已发布版本不可原地修改。</span>
      </div>

      <AiConfigurationEditor
        visible={editorVisible}
        configuration={editorConfiguration}
        loading={saveMutation.isPending}
        onCancel={() => {
          if (saveMutation.isPending) return;
          setEditorVisible(false);
          setEditorConfiguration(null);
        }}
        onSubmit={(values) => saveMutation.mutate({ configuration: editorConfiguration, values })}
      />
      <AiConfigurationConfirmDialog
        visible={Boolean(actionTarget && action)}
        configuration={actionTarget}
        action={action}
        loading={lifecycleMutation.isPending}
        onCancel={() => {
          if (lifecycleMutation.isPending) return;
          setActionTarget(null);
          setAction(null);
        }}
        onConfirm={() => {
          if (actionTarget && action) {
            lifecycleMutation.mutate({ configurationId: actionTarget.id, action });
          }
        }}
      />
      <AiConfigurationTestDialog
        visible={Boolean(testTarget)}
        configuration={testTarget}
        result={testResult}
        loading={testMutation.isPending}
        onClose={() => {
          if (testMutation.isPending) return;
          setTestTarget(null);
          setTestResult(null);
        }}
      />
    </div>
  );
}
