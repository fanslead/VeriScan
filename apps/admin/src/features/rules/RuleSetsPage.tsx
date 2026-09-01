import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Empty, Modal, Skeleton, Tag, Toast } from '@douyinfe/semi-ui';
import { IconBranch, IconPlus, IconShield, IconTickCircle } from '@douyinfe/semi-icons';
import { useSearchParams } from 'react-router-dom';
import { moderationService, ruleSetService } from '@/shared/api/services';
import type { RuleSet, RuleSetDraftInput } from '@/shared/api/types';
import { ErrorState } from '@/shared/ui/ErrorState';
import { PageIntro } from '@/shared/ui/PageIntro';
import { RuleSetEditor } from './RuleSetEditor';
import { ruleCategoryOptions } from './ruleSetFormModel';
import { useAdminCapability } from '@/shared/auth/permissions';

const statusMeta = {
  draft: { label: '草稿', color: 'amber' as const },
  published: { label: '已发布', color: 'cyan' as const },
  archived: { label: '已归档', color: 'grey' as const },
};

const formatDate = (value: string | null) =>
  value
    ? new Intl.DateTimeFormat('zh-CN', {
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
      }).format(new Date(value))
    : '尚未执行';

const categoryLabel = (category: string) =>
  ruleCategoryOptions.find((item) => item.value === category)?.label ?? '其他分类';

export function RuleSetsPage() {
  const canEdit = useAdminCapability('editRules');
  const canPublish = useAdminCapability('publish');
  const [searchParams] = useSearchParams();
  const applicationId = searchParams.get('applicationId') ?? '';
  const queryClient = useQueryClient();
  const [editorVisible, setEditorVisible] = useState(false);
  const [editorRuleSet, setEditorRuleSet] = useState<RuleSet | null>(null);
  const [validationIssues, setValidationIssues] = useState<Record<string, string[]>>({});

  const ruleSets = useQuery({ queryKey: ['rule-sets'], queryFn: ruleSetService.list });
  const application = useQuery({
    queryKey: ['application', applicationId],
    queryFn: () => moderationService.getApplication(applicationId),
    enabled: Boolean(applicationId),
  });

  const refresh = () => void queryClient.invalidateQueries({ queryKey: ['rule-sets'] });
  const loadEditor = useMutation({
    mutationFn: ruleSetService.get,
    onSuccess: (ruleSet) => {
      setEditorRuleSet(ruleSet);
      setEditorVisible(true);
    },
    onError: () => Toast.error({ content: '规则集详情读取失败，请重试' }),
  });
  const save = useMutation({
    mutationFn: ({ ruleSet, input }: { ruleSet: RuleSet | null; input: RuleSetDraftInput }) =>
      ruleSet ? ruleSetService.update(ruleSet.id, input) : ruleSetService.create(input),
    onSuccess: (_, variables) => {
      setEditorVisible(false);
      setEditorRuleSet(null);
      Toast.success({ content: variables.ruleSet ? '规则草稿已保存' : '规则草稿已创建' });
      refresh();
    },
    onError: () => Toast.error({ content: '规则草稿保存失败，请检查内容后重试' }),
  });
  const validate = useMutation({
    mutationFn: ruleSetService.validate,
    onSuccess: (result, ruleSetId) => {
      setValidationIssues((current) => ({
        ...current,
        [ruleSetId]: result.issues.map((issue) =>
          issue.ruleIndex === null
            ? issue.message
            : `第 ${issue.ruleIndex + 1} 条：${issue.message}`,
        ),
      }));
      Toast[result.valid ? 'success' : 'warning']({
        content: result.valid
          ? `校验通过，共 ${result.ruleCount} 条规则`
          : '校验未通过，请查看问题',
      });
      refresh();
    },
    onError: () => Toast.error({ content: '规则校验失败，请重试' }),
  });
  const lifecycle = useMutation({
    mutationFn: ({ id, action }: { id: string; action: 'publish' | 'archive' | 'revision' }) =>
      action === 'publish'
        ? ruleSetService.publish(id)
        : action === 'archive'
          ? ruleSetService.archive(id)
          : ruleSetService.createRevision(id),
    onSuccess: (result, variables) => {
      Toast.success({
        content:
          variables.action === 'publish'
            ? '规则版本已发布'
            : variables.action === 'archive'
              ? '规则版本已归档'
              : '新版本草稿已创建',
      });
      refresh();
      if (variables.action === 'revision') {
        setEditorRuleSet(result);
        setEditorVisible(true);
      }
    },
    onError: () => Toast.error({ content: '操作失败，请确认版本状态与应用绑定情况' }),
  });
  const bind = useMutation({
    mutationFn: (revisionId: string) => moderationService.bindRuleSet(applicationId, revisionId),
    onSuccess: (updated) => {
      queryClient.setQueryData(['application', applicationId], updated);
      void queryClient.invalidateQueries({ queryKey: ['applications'] });
      refresh();
      Toast.success({ content: '应用规则版本已切换' });
    },
    onError: () => Toast.error({ content: '规则版本切换失败，请重试' }),
  });

  const confirmPublish = (ruleSet: RuleSet) =>
    Modal.confirm({
      title: '发布不可变规则版本？',
      content: `发布后“${ruleSet.name}”不可原地修改，应用不会自动切换。`,
      okText: '确认发布',
      onOk: () => lifecycle.mutateAsync({ id: ruleSet.id, action: 'publish' }),
    });

  const confirmArchive = (ruleSet: RuleSet) =>
    Modal.confirm({
      title: '归档这个规则版本？',
      content: '归档后不能再绑定到应用，已有审核记录仍保留版本事实。',
      okText: '确认归档',
      okButtonProps: { type: 'danger' },
      onOk: () => lifecycle.mutateAsync({ id: ruleSet.id, action: 'archive' }),
    });

  return (
    <div className="page-stack rule-set-page">
      <PageIntro
        eyebrow="内容治理 · 规则版本"
        title="规则与词库"
        description="维护可校验、可追溯、按应用绑定的不可变规则版本。"
        actions={
          canEdit ? (
            <Button
              type="primary"
              theme="solid"
              icon={<IconPlus />}
              onClick={() => {
                setEditorRuleSet(null);
                setEditorVisible(true);
              }}
            >
              新建草稿
            </Button>
          ) : undefined
        }
      />

      {applicationId ? (
        <Card className="panel rule-binding-banner">
          <div className="rule-binding-banner__mark">
            <IconShield />
          </div>
          <div>
            <span className="section-kicker">当前应用规则</span>
            <strong>{application.data?.name ?? '正在读取应用…'}</strong>
            <small>当前版本：{application.data?.policyVersion ?? '未绑定'}</small>
          </div>
          <Tag color={application.data?.policyVersion ? 'cyan' : 'amber'}>
            {application.data?.policyVersion ? '已绑定' : '待配置'}
          </Tag>
        </Card>
      ) : null}

      {ruleSets.isPending ? (
        <Card className="panel">
          <Skeleton.Paragraph rows={8} />
        </Card>
      ) : ruleSets.isError ? (
        <ErrorState title="规则版本暂时无法读取" onRetry={() => void ruleSets.refetch()} />
      ) : ruleSets.data.length === 0 ? (
        <Card className="panel rule-set-empty">
          <Empty title="还没有规则集" description="创建第一个草稿，校验后即可发布。" />
        </Card>
      ) : (
        <div className="rule-set-list">
          {ruleSets.data.map((ruleSet) => {
            const meta = statusMeta[ruleSet.status];
            const isBound = application.data?.policyVersion === ruleSet.publicRevisionId;
            const issues = validationIssues[ruleSet.id] ?? [];
            return (
              <Card key={ruleSet.id} className={`panel rule-set-card${isBound ? ' is-bound' : ''}`}>
                <div className="rule-set-card__head">
                  <div className="rule-set-card__identity">
                    <span className="rule-set-card__mark">
                      <IconBranch />
                    </span>
                    <div>
                      <div className="rule-set-card__title">
                        <h2>{ruleSet.name}</h2>
                        <Tag color={meta.color}>{meta.label}</Tag>
                        {isBound ? <Tag color="cyan">当前应用使用中</Tag> : null}
                      </div>
                      <code>{ruleSet.publicRevisionId}</code>
                    </div>
                  </div>
                  <div className="rule-set-card__actions">
                    {ruleSet.status === 'draft' && canEdit ? (
                      <>
                        <Button onClick={() => validate.mutate(ruleSet.id)}>校验</Button>
                        <Button
                          loading={loadEditor.isPending && loadEditor.variables === ruleSet.id}
                          onClick={() => loadEditor.mutate(ruleSet.id)}
                        >
                          编辑
                        </Button>
                        {canPublish ? (
                          <Button
                            type="primary"
                            theme="solid"
                            onClick={() => confirmPublish(ruleSet)}
                          >
                            发布
                          </Button>
                        ) : null}
                      </>
                    ) : ruleSet.status !== 'draft' && canEdit ? (
                      <>
                        <Button
                          onClick={() => lifecycle.mutate({ id: ruleSet.id, action: 'revision' })}
                        >
                          创建新版本
                        </Button>
                        {canPublish &&
                        ruleSet.status === 'published' &&
                        ruleSet.applicationCount === 0 ? (
                          <Button
                            type="danger"
                            theme="borderless"
                            onClick={() => confirmArchive(ruleSet)}
                          >
                            归档
                          </Button>
                        ) : null}
                      </>
                    ) : null}
                    {canEdit && applicationId && ruleSet.status === 'published' && !isBound ? (
                      <Button
                        type="primary"
                        theme="solid"
                        loading={bind.isPending}
                        onClick={() => bind.mutate(ruleSet.publicRevisionId)}
                      >
                        绑定到此应用
                      </Button>
                    ) : null}
                  </div>
                </div>
                <div className="rule-set-card__facts">
                  <div>
                    <span>规则数量</span>
                    <strong>{ruleSet.ruleCount}</strong>
                  </div>
                  <div>
                    <span>绑定应用</span>
                    <strong>{ruleSet.applicationCount}</strong>
                  </div>
                  <div>
                    <span>最近校验</span>
                    <strong>{formatDate(ruleSet.lastValidatedAt)}</strong>
                  </div>
                  <div>
                    <span>发布时间</span>
                    <strong>{formatDate(ruleSet.publishedAt)}</strong>
                  </div>
                </div>
                <div className="rule-set-card__preview">
                  {ruleSet.rules.slice(0, 8).map((rule) => (
                    <Tag
                      key={rule.id}
                      color={
                        rule.type === 'black' ? 'red' : rule.type === 'white' ? 'cyan' : 'amber'
                      }
                    >
                      {rule.term} · {categoryLabel(rule.category)}
                    </Tag>
                  ))}
                  {ruleSet.regexRules
                    .slice(0, Math.max(0, 8 - ruleSet.rules.length))
                    .map((rule) => (
                      <Tag key={rule.id} color="cyan">
                        格式识别 · {categoryLabel(rule.category)}
                      </Tag>
                    ))}
                  {ruleSet.combinationRules
                    .slice(0, Math.max(0, 8 - ruleSet.rules.length - ruleSet.regexRules.length))
                    .map((rule) => (
                      <Tag key={rule.id} color="amber">
                        {rule.name} · 组合条件
                      </Tag>
                    ))}
                  {ruleSet.ruleCount > 8 ? <span>另有 {ruleSet.ruleCount - 8} 条</span> : null}
                </div>
                {issues.length > 0 ? (
                  <div className="rule-set-card__issues">
                    <strong>校验问题</strong>
                    {issues.slice(0, 4).map((issue) => (
                      <span key={issue}>{issue}</span>
                    ))}
                  </div>
                ) : ruleSet.lastValidatedAt ? (
                  <div className="rule-set-card__validated">
                    <IconTickCircle /> 内容校验通过
                    <code>{ruleSet.lastValidatedChecksum?.slice(0, 16)}…</code>
                  </div>
                ) : null}
              </Card>
            );
          })}
        </div>
      )}

      <RuleSetEditor
        visible={editorVisible}
        ruleSet={editorRuleSet}
        loading={save.isPending}
        onCancel={() => setEditorVisible(false)}
        onSubmit={(input) => save.mutate({ ruleSet: editorRuleSet, input })}
      />
    </div>
  );
}
