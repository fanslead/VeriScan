import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Button, Card, Input, Select, TextArea, Toast, Typography } from '@douyinfe/semi-ui';
import { IconArrowLeft, IconInfoCircle, IconTickCircle } from '@douyinfe/semi-icons';
import { useNavigate } from 'react-router-dom';
import { moderationService } from '@/shared/api/services';
import type { ApplicationEnvironment } from '@/shared/api/types';

interface FormValues {
  name: string;
  environment: ApplicationEnvironment;
  slug: string;
  description: string;
  policyVersion: string;
}

const initialValues: FormValues = {
  name: '',
  environment: 'live',
  slug: '',
  description: '',
  policyVersion: '2026.08',
};

export function CreateApplicationPage() {
  const navigate = useNavigate();
  const [values, setValues] = useState(initialValues);
  const [touched, setTouched] = useState<Record<keyof FormValues, boolean>>({
    name: false,
    environment: false,
    slug: false,
    description: false,
    policyVersion: false,
  });
  const mutation = useMutation({
    mutationFn: moderationService.createApplication,
    onSuccess: (application) => {
      Toast.success({ content: '应用已创建' });
      navigate(`/applications/${application.id}`);
    },
  });
  const nameError =
    touched.name && values.name.trim().length < 2 ? '请输入至少 2 个字的应用名称' : '';
  const slugError =
    touched.slug && !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(values.slug)
      ? '请使用小写字母、数字和连字符'
      : '';
  const canSubmit =
    values.name.trim().length >= 2 &&
    /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(values.slug) &&
    !mutation.isPending;
  const update = <K extends keyof FormValues>(key: K, value: FormValues[K]) =>
    setValues((current) => ({ ...current, [key]: value }));

  const submit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setTouched({
      name: true,
      environment: true,
      slug: true,
      description: true,
      policyVersion: true,
    });
    if (!canSubmit) return;
    mutation.mutate({
      ...values,
      name: values.name.trim(),
      description: values.description.trim(),
    });
  };

  return (
    <div className="page-stack create-page">
      <button className="back-link" type="button" onClick={() => navigate('/applications')}>
        <IconArrowLeft />
        返回应用
      </button>
      <div className="page-intro">
        <div>
          <div className="eyebrow">接入管理 · 创建应用</div>
          <Typography.Title heading={1}>创建应用</Typography.Title>
          <Typography.Text type="tertiary">
            先建立应用边界，再为它签发可轮换的访问凭证。
          </Typography.Text>
        </div>
      </div>
      <div className="form-layout">
        <Card className="panel form-panel">
          <form onSubmit={submit} noValidate>
            <div className="form-section">
              <div className="section-kicker">基础信息</div>
              <Typography.Title heading={4}>应用信息</Typography.Title>
              <p className="form-help">名称会显示在记录、统计和运营通知中。</p>
            </div>
            <label className="form-field">
              <span>
                应用名称 <i>*</i>
              </span>
              <Input
                value={values.name}
                onChange={(value) => update('name', value)}
                onBlur={() => setTouched((current) => ({ ...current, name: true }))}
                placeholder="例如：星河电商社区"
                aria-invalid={Boolean(nameError)}
                aria-describedby={nameError ? 'name-error' : undefined}
              />
              {nameError ? (
                <small id="name-error" className="field-error">
                  {nameError}
                </small>
              ) : null}
            </label>
            <label className="form-field">
              <span>
                应用环境 <i>*</i>
              </span>
              <Select
                value={values.environment}
                onChange={(value) => update('environment', value as ApplicationEnvironment)}
                optionList={[
                  { value: 'live', label: '正式环境 · live' },
                  { value: 'test', label: '测试环境 · test' },
                ]}
              />
              <small className="field-hint">环境会决定凭证前缀和调用边界。</small>
            </label>
            <label className="form-field">
              <span>
                应用标识 <i>*</i>
              </span>
              <Input
                value={values.slug}
                onChange={(value) =>
                  update('slug', value.toLowerCase().replace(/[^a-z0-9-]/g, '-'))
                }
                onBlur={() => setTouched((current) => ({ ...current, slug: true }))}
                prefix="app_"
                placeholder="xinghe-commerce"
                aria-invalid={Boolean(slugError)}
                aria-describedby={slugError ? 'slug-error' : undefined}
              />
              {slugError ? (
                <small id="slug-error" className="field-error">
                  {slugError}
                </small>
              ) : (
                <small className="field-hint">用于识别应用，不建议频繁修改。</small>
              )}
            </label>
            <label className="form-field">
              <span>应用说明</span>
              <TextArea
                value={values.description}
                onChange={(value: string) => update('description', value)}
                placeholder="说明这套审核入口服务的业务内容"
                autosize={{ minRows: 4, maxRows: 7 }}
                maxCount={200}
              />
            </label>
            <label className="form-field">
              <span>
                审核策略 <i>*</i>
              </span>
              <Select
                value={values.policyVersion}
                onChange={(value) => update('policyVersion', String(value))}
                optionList={[
                  { value: '2026.08', label: '社区基础策略 · 2026.08' },
                  { value: '2026.07', label: '客服合规策略 · 2026.07' },
                ]}
              />
            </label>
            {mutation.isError ? (
              <div className="inline-error" role="alert">
                <IconInfoCircle />
                <span>应用创建失败，请重试。已经填写的信息会保留。</span>
              </div>
            ) : null}
            <div className="form-actions">
              <Button type="tertiary" onClick={() => navigate('/applications')}>
                取消
              </Button>
              <Button
                type="primary"
                theme="solid"
                htmlType="submit"
                loading={mutation.isPending}
                disabled={!canSubmit}
                icon={<IconTickCircle />}
              >
                创建应用
              </Button>
            </div>
          </form>
        </Card>
        <aside className="form-aside">
          <div className="aside-mark" aria-hidden="true">
            ◎
          </div>
          <Typography.Title heading={5}>创建后可以做什么？</Typography.Title>
          <ul>
            <li>为不同环境分别创建 API Key，支持独立撤销。</li>
            <li>按应用查看审核量、结论分布和调用时延。</li>
            <li>为应用绑定策略与外部模型路由。</li>
          </ul>
          <div className="aside-note">
            <IconInfoCircle />
            <span>应用创建后会立即生效，首次调用前请先保存好 API Key。</span>
          </div>
        </aside>
      </div>
    </div>
  );
}
