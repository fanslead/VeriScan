import { Input, Select, TextArea } from '@douyinfe/semi-ui';
import type {
  AiApiVersionLocation,
  AiConfigurationDraftInput,
  AiDecodingMode,
  AiProtocol,
} from '@/shared/api/types';
import {
  authSchemeLabel,
  authSchemeOptions,
  apiVersionLocationOptions,
  decodingModeOptions,
  protocolOptions,
} from './aiConfigurationLabels';
import type { FieldKey } from './aiConfigurationFormModel';

export interface AiConfigurationFormSectionProps {
  values: AiConfigurationDraftInput;
  update: <K extends FieldKey>(key: K, value: AiConfigurationDraftInput[K]) => void;
  markTouched: (key: FieldKey) => void;
  error: (key: FieldKey) => string | undefined;
}

export function RouteIdentitySection({
  values,
  update,
  markTouched,
  error,
  onProtocolChange,
}: AiConfigurationFormSectionProps & { onProtocolChange: (value: AiProtocol) => void }) {
  const protocol =
    protocolOptions.find((item) => item.value === values.protocol) ?? protocolOptions[0];
  return (
    <section className="ai-config-form__section">
      <div className="ai-config-form__section-head">
        <span className="section-kicker">ROUTE IDENTITY</span>
        <h2>路由身份</h2>
        <p>协议会自动带出常用入口；如果使用自建网关，仍可以按实际情况修改。</p>
      </div>
      <div className="ai-config-form__grid ai-config-form__grid--two">
        <label className="form-field">
          <span>
            配置名称 <i>*</i>
          </span>
          <Input
            value={values.name}
            onChange={(value) => update('name', value)}
            onBlur={() => markTouched('name')}
            placeholder="例如：主路由 · OpenAI"
            maxLength={100}
            aria-invalid={Boolean(error('name'))}
          />
          {error('name') ? <small className="field-error">{error('name')}</small> : null}
        </label>
        <label className="form-field">
          <span>
            接口协议 <i>*</i>
          </span>
          <Select
            value={values.protocol}
            onChange={(value) => onProtocolChange(value as AiProtocol)}
            optionList={protocolOptions.map((item) => ({ value: item.value, label: item.label }))}
          />
          <small className="field-hint">{protocol.hint}</small>
        </label>
      </div>
      <label className="form-field">
        <span>
          模型名称 <i>*</i>
        </span>
        <Input
          value={values.model}
          onChange={(value) => update('model', value)}
          onBlur={() => markTouched('model')}
          placeholder="例如：gpt-4o-mini"
          maxLength={200}
          aria-invalid={Boolean(error('model'))}
        />
        {error('model') ? <small className="field-error">{error('model')}</small> : null}
      </label>
    </section>
  );
}

export function ConnectionSection({
  values,
  update,
  markTouched,
  error,
}: AiConfigurationFormSectionProps) {
  const locationOptions =
    values.protocol === 'anthropicMessages'
      ? apiVersionLocationOptions.filter((item) => item.value === 'header')
      : apiVersionLocationOptions.filter((item) => item.value !== 'header');
  const updateApiVersion = (value: string) => {
    const apiVersion = value || null;
    update('apiVersion', apiVersion);
    if (!apiVersion) {
      update('apiVersionLocation', values.protocol === 'anthropicMessages' ? 'header' : 'none');
    } else if (values.apiVersionLocation === 'none') {
      update('apiVersionLocation', values.protocol === 'anthropicMessages' ? 'header' : 'query');
    }
  };
  return (
    <section className="ai-config-form__section">
      <div className="ai-config-form__section-head">
        <span className="section-kicker">CONNECTION</span>
        <h2>连接入口</h2>
        <p>只保存服务地址和凭据引用，不在管理台接收或展示密钥明文。</p>
      </div>
      <div className="ai-config-form__grid ai-config-form__grid--wide">
        <label className="form-field">
          <span>
            服务基础地址 <i>*</i>
          </span>
          <Input
            value={values.baseUrl}
            onChange={(value) => update('baseUrl', value)}
            onBlur={() => markTouched('baseUrl')}
            placeholder="https://api.example.com"
            aria-invalid={Boolean(error('baseUrl'))}
          />
          {error('baseUrl') ? <small className="field-error">{error('baseUrl')}</small> : null}
        </label>
        <label className="form-field">
          <span>
            请求路径 <i>*</i>
          </span>
          <Input
            value={values.endpointPath}
            onChange={(value) => update('endpointPath', value)}
            onBlur={() => markTouched('endpointPath')}
            placeholder="/v1/chat/completions"
            aria-invalid={Boolean(error('endpointPath'))}
          />
          {error('endpointPath') ? (
            <small className="field-error">{error('endpointPath')}</small>
          ) : null}
        </label>
      </div>
      <div className="ai-config-form__grid ai-config-form__grid--four">
        <label className="form-field">
          <span>
            认证方式 <i>*</i>
          </span>
          <Select
            value={values.authScheme}
            onChange={(value) =>
              update('authScheme', value as AiConfigurationDraftInput['authScheme'])
            }
            optionList={authSchemeOptions}
          />
        </label>
        <label className="form-field">
          <span>
            凭据引用 <i>*</i>
          </span>
          <Input
            value={values.credentialRef}
            onChange={(value) => update('credentialRef', value)}
            onBlur={() => markTouched('credentialRef')}
            placeholder="config://provider-prod"
            aria-invalid={Boolean(error('credentialRef'))}
          />
          {error('credentialRef') ? (
            <small className="field-error">{error('credentialRef')}</small>
          ) : null}
        </label>
        <label className="form-field">
          <span>服务商版本</span>
          <Input
            value={values.apiVersion ?? ''}
            onChange={updateApiVersion}
            onBlur={() => markTouched('apiVersion')}
            placeholder={values.protocol === 'anthropicMessages' ? '2023-06-01' : '可留空'}
            aria-invalid={Boolean(error('apiVersion'))}
          />
          {error('apiVersion') ? (
            <small className="field-error">{error('apiVersion')}</small>
          ) : null}
        </label>
        <label className="form-field">
          <span>
            版本发送位置 <i>*</i>
          </span>
          <Select
            value={values.apiVersionLocation}
            onChange={(value) => update('apiVersionLocation', value as AiApiVersionLocation)}
            optionList={locationOptions}
          />
          <small className="field-hint">
            {values.protocol === 'anthropicMessages'
              ? 'Messages 仅允许使用受控 Header。'
              : 'OpenAI / Azure 兼容服务可选择固定 Query 参数。'}
          </small>
          {error('apiVersionLocation') ? (
            <small className="field-error">{error('apiVersionLocation')}</small>
          ) : null}
        </label>
      </div>
      <div className="ai-config-secure-note">
        <strong>{authSchemeLabel(values.authScheme)} · 安全引用</strong>
        <span>凭据由服务端安全注入上游请求，页面不会接触密钥明文。</span>
      </div>
    </section>
  );
}

export function DecisionPolicySection({
  values,
  update,
  markTouched,
  error,
}: AiConfigurationFormSectionProps) {
  return (
    <section className="ai-config-form__section">
      <div className="ai-config-form__section-head">
        <span className="section-kicker">DECISION POLICY</span>
        <h2>判定策略</h2>
        <p>明确模型输出边界，便于审核记录保留稳定、可解释的结果。</p>
      </div>
      <label className="form-field">
        <span>
          系统提示词 <i>*</i>
        </span>
        <TextArea
          value={values.systemPrompt}
          onChange={(value: string) => update('systemPrompt', value)}
          onBlur={() => markTouched('systemPrompt')}
          autosize={{ minRows: 4, maxRows: 8 }}
          maxCount={12000}
          aria-invalid={Boolean(error('systemPrompt'))}
        />
        {error('systemPrompt') ? (
          <small className="field-error">{error('systemPrompt')}</small>
        ) : null}
      </label>
      <label className="form-field">
        <span>
          解码策略 <i>*</i>
        </span>
        <Select
          value={values.decodingMode}
          onChange={(value) => update('decodingMode', value as AiDecodingMode)}
          optionList={decodingModeOptions.map((item) => ({ value: item.value, label: item.label }))}
        />
        <small className="field-hint">
          {decodingModeOptions.find((item) => item.value === values.decodingMode)?.hint}
        </small>
      </label>
    </section>
  );
}

export function LimitsSection({
  values,
  update,
  markTouched,
  error,
}: AiConfigurationFormSectionProps) {
  const numberInput = (
    key:
      | 'maxInputTokens'
      | 'maxOutputTokens'
      | 'connectTimeoutMs'
      | 'requestTimeoutMs'
      | 'maxAttempts',
  ) => (
    <Input
      type="number"
      value={String(values[key])}
      onChange={(value) => update(key, Number(value) || 0)}
      onBlur={() => markTouched(key)}
      aria-invalid={Boolean(error(key))}
    />
  );
  return (
    <section className="ai-config-form__section ai-config-form__section--last">
      <div className="ai-config-form__section-head">
        <span className="section-kicker">LIMITS &amp; RETENTION</span>
        <h2>容量与合规</h2>
        <p>这些边界会随版本冻结，并写入审核链路的运行上下文。</p>
      </div>
      <div className="ai-config-form__grid ai-config-form__grid--four">
        <label className="form-field">
          <span>最大输入 Token</span>
          {numberInput('maxInputTokens')}
          {error('maxInputTokens') ? (
            <small className="field-error">{error('maxInputTokens')}</small>
          ) : null}
        </label>
        <label className="form-field">
          <span>最大输出 Token</span>
          {numberInput('maxOutputTokens')}
          {error('maxOutputTokens') ? (
            <small className="field-error">{error('maxOutputTokens')}</small>
          ) : null}
        </label>
        <label className="form-field">
          <span>连接超时 (ms)</span>
          {numberInput('connectTimeoutMs')}
          {error('connectTimeoutMs') ? (
            <small className="field-error">{error('connectTimeoutMs')}</small>
          ) : null}
        </label>
        <label className="form-field">
          <span>请求超时 (ms)</span>
          {numberInput('requestTimeoutMs')}
          {error('requestTimeoutMs') ? (
            <small className="field-error">{error('requestTimeoutMs')}</small>
          ) : null}
        </label>
      </div>
      <div className="ai-config-form__grid ai-config-form__grid--three">
        <label className="form-field">
          <span>最大尝试次数</span>
          {numberInput('maxAttempts')}
          {error('maxAttempts') ? (
            <small className="field-error">{error('maxAttempts')}</small>
          ) : null}
        </label>
        <label className="form-field">
          <span>
            数据区域 <i>*</i>
          </span>
          <Input
            value={values.dataRegion}
            onChange={(value) => update('dataRegion', value)}
            onBlur={() => markTouched('dataRegion')}
            placeholder="global"
            aria-invalid={Boolean(error('dataRegion'))}
          />
          {error('dataRegion') ? (
            <small className="field-error">{error('dataRegion')}</small>
          ) : null}
        </label>
        <label className="form-field">
          <span>
            保留策略 <i>*</i>
          </span>
          <Input
            value={values.retentionClass}
            onChange={(value) => update('retentionClass', value)}
            onBlur={() => markTouched('retentionClass')}
            placeholder="30d"
            aria-invalid={Boolean(error('retentionClass'))}
          />
          {error('retentionClass') ? (
            <small className="field-error">{error('retentionClass')}</small>
          ) : null}
        </label>
      </div>
    </section>
  );
}
