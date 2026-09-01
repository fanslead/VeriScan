import { Button, Modal, Skeleton, Tag, Typography } from '@douyinfe/semi-ui';
import type { AiConfiguration, AiConfigurationTestResult } from '@/shared/api/types';
import { findProtocol } from './aiConfigurationLabels';

interface AiConfigurationTestDialogProps {
  visible: boolean;
  configuration: AiConfiguration | null;
  result: AiConfigurationTestResult | null;
  loading?: boolean;
  onClose: () => void;
}

export function AiConfigurationTestDialog({
  visible,
  configuration,
  result,
  loading = false,
  onClose,
}: AiConfigurationTestDialogProps) {
  const passed = result?.succeeded === true;
  const protocol = result ? findProtocol(configuration?.protocol ?? 'openAiChatCompletions') : null;

  return (
    <Modal
      visible={visible}
      title="合成连接测试"
      onCancel={onClose}
      footer={[
        <Button key="close" type="primary" theme="solid" onClick={onClose} disabled={loading}>
          完成
        </Button>,
      ]}
    >
      <div className="ai-test-dialog">
        <div className="ai-test-dialog__target">
          <span className="ai-test-dialog__target-mark" aria-hidden="true">
            ◎
          </span>
          <div>
            <strong>{configuration?.name ?? 'AI 配置'}</strong>
            <span>
              {configuration ? `${protocol?.shortLabel} · ${configuration.model}` : '正在准备请求'}
            </span>
          </div>
        </div>
        {loading ? (
          <div className="ai-test-dialog__loading" aria-live="polite">
            <Skeleton.Paragraph rows={3} />
            <span>正在发送一条不含业务内容的合成请求…</span>
          </div>
        ) : result ? (
          <div
            className={`ai-test-dialog__result${passed ? ' is-passed' : ' is-failed'}`}
            role="status"
          >
            <div className="ai-test-dialog__result-head">
              <span className="ai-test-dialog__result-mark" aria-hidden="true">
                {passed ? '✓' : '!'}
              </span>
              <div>
                <strong>{passed ? '测试通过' : '测试未通过'}</strong>
                <Typography.Text>
                  {passed
                    ? '上游已返回可解析结果，这份草稿现在可以进入发布流程。'
                    : '请求没有通过校验，请检查地址、模型名称和服务端凭据引用。'}
                </Typography.Text>
              </div>
            </div>
            <div className="ai-test-dialog__metrics">
              <span>
                <small>接口协议</small>
                <strong>{protocol?.shortLabel || result.protocol}</strong>
              </span>
              <span>
                <small>响应耗时</small>
                <strong>{result.latencyMs} ms</strong>
              </span>
              <span>
                <small>输入 Token</small>
                <strong>{result.inputTokens ?? '—'}</strong>
              </span>
              <span>
                <small>输出 Token</small>
                <strong>{result.outputTokens ?? '—'}</strong>
              </span>
            </div>
            {!passed ? <Tag color="red">当前草稿不能发布</Tag> : null}
          </div>
        ) : null}
      </div>
    </Modal>
  );
}
