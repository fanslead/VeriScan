import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Modal, Tag } from '@douyinfe/semi-ui';
import type { AiConfiguration, AiConfigurationDraftInput, AiProtocol } from '@/shared/api/types';
import { findProtocol, statusMeta } from './aiConfigurationLabels';
import {
  canUseSuggestion,
  createInitialValues,
  toDraftValues,
  validateAiConfiguration,
  type FieldKey,
} from './aiConfigurationFormModel';
import {
  ConnectionSection,
  DecisionPolicySection,
  LimitsSection,
  RouteIdentitySection,
  type AiConfigurationFormSectionProps,
} from './AiConfigurationFormSections';

interface AiConfigurationEditorProps {
  visible: boolean;
  configuration?: AiConfiguration | null;
  loading?: boolean;
  onCancel: () => void;
  onSubmit: (values: AiConfigurationDraftInput) => void;
}

export function AiConfigurationEditor({
  visible,
  configuration,
  loading = false,
  onCancel,
  onSubmit,
}: AiConfigurationEditorProps) {
  const formRef = useRef<HTMLFormElement>(null);
  const [values, setValues] = useState<AiConfigurationDraftInput>(createInitialValues);
  const [touched, setTouched] = useState<Partial<Record<FieldKey, boolean>>>({});

  useEffect(() => {
    if (!visible) {
      setValues((current) => (current.apiKey ? { ...current, apiKey: '' } : current));
      return;
    }
    setValues(configuration ? toDraftValues(configuration) : createInitialValues());
    setTouched({});
    if (formRef.current) formRef.current.scrollTop = 0;
  }, [configuration, visible]);

  const errors = useMemo(
    () => validateAiConfiguration(values, configuration?.hasCredential ?? false),
    [configuration?.hasCredential, values],
  );
  const update = <K extends FieldKey>(key: K, value: AiConfigurationDraftInput[K]) =>
    setValues((current) => ({ ...current, [key]: value }));
  const markTouched = (key: FieldKey) => setTouched((current) => ({ ...current, [key]: true }));
  const error = (key: FieldKey) => (touched[key] ? errors[key] : undefined);

  const changeProtocol = (value: AiProtocol) => {
    const previousSuggestion = findProtocol(values.protocol);
    const nextSuggestion = findProtocol(value);
    setValues((current) => ({
      ...current,
      protocol: value,
      baseUrl: canUseSuggestion(current.baseUrl, previousSuggestion.baseUrl, '')
        ? nextSuggestion.baseUrl
        : current.baseUrl,
      endpointPath: canUseSuggestion(current.endpointPath, previousSuggestion.endpointPath, '')
        ? nextSuggestion.endpointPath
        : current.endpointPath,
      authScheme:
        current.authScheme === previousSuggestion.authScheme
          ? nextSuggestion.authScheme
          : current.authScheme,
      apiVersion: canUseSuggestion(current.apiVersion, previousSuggestion.apiVersion, null)
        ? nextSuggestion.apiVersion
        : current.apiVersion,
      apiVersionLocation: (() => {
        const nextApiVersion = canUseSuggestion(
          current.apiVersion,
          previousSuggestion.apiVersion,
          null,
        )
          ? nextSuggestion.apiVersion
          : current.apiVersion;
        const currentLocationAllowed =
          value === 'anthropicMessages'
            ? current.apiVersionLocation === 'header'
            : current.apiVersionLocation !== 'header';
        if (currentLocationAllowed) return current.apiVersionLocation;
        return nextApiVersion ? nextSuggestion.apiVersionLocation : 'none';
      })(),
    }));
    markTouched('protocol');
  };

  const submit = () => {
    setTouched({
      name: true,
      model: true,
      baseUrl: true,
      endpointPath: true,
      apiKey: true,
      apiVersion: true,
      apiVersionLocation: true,
      systemPrompt: true,
      maxInputTokens: true,
      maxOutputTokens: true,
      connectTimeoutMs: true,
      requestTimeoutMs: true,
      maxAttempts: true,
      dataRegion: true,
      retentionClass: true,
    });
    if (Object.keys(errors).length > 0) return;
    onSubmit({
      ...values,
      name: values.name.trim(),
      baseUrl: values.baseUrl.trim(),
      endpointPath: values.endpointPath.trim(),
      apiKey: values.apiKey.trim(),
      model: values.model.trim(),
      apiVersion: values.apiVersion?.trim() || null,
      systemPrompt: values.systemPrompt.trim(),
      dataRegion: values.dataRegion.trim(),
      retentionClass: values.retentionClass.trim(),
    });
  };

  const sectionProps: AiConfigurationFormSectionProps = {
    values,
    hasExistingCredential: configuration?.hasCredential ?? false,
    update,
    markTouched,
    error,
  };
  const status = configuration ? statusMeta[configuration.status] : null;

  return (
    <Modal
      visible={visible}
      title={configuration ? '编辑 AI 配置草稿' : '创建 AI 配置草稿'}
      width="min(780px, calc(100vw - 24px))"
      onCancel={onCancel}
      footer={[
        <Button key="cancel" onClick={onCancel} disabled={loading}>
          取消
        </Button>,
        <Button key="submit" type="primary" theme="solid" loading={loading} onClick={submit}>
          保存草稿
        </Button>,
      ]}
    >
      <form
        ref={formRef}
        className="ai-config-form"
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
        noValidate
      >
        <div className="ai-config-form__lead">
          <div>
            <span className="section-kicker">MODEL ROUTING / DRAFT</span>
            <strong>{configuration ? '只修改草稿内容' : '先保存，再测试与发布'}</strong>
            <p>
              {configuration
                ? '已发布版本不可原地修改；若要调整线上路由，请创建新的草稿版本。'
                : '地址、模型和策略会在服务端校验，API 密钥会加密保存且不会再次回显。'}
            </p>
          </div>
          {status ? (
            <Tag color={status.color}>{status.label}</Tag>
          ) : (
            <Tag color="amber">新草稿</Tag>
          )}
        </div>
        <RouteIdentitySection {...sectionProps} onProtocolChange={changeProtocol} />
        <ConnectionSection {...sectionProps} />
        <DecisionPolicySection {...sectionProps} />
        <LimitsSection {...sectionProps} />
      </form>
    </Modal>
  );
}
