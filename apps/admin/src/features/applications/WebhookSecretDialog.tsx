import { useEffect, useState } from 'react';
import { Button, Checkbox, Modal, Toast, Typography } from '@douyinfe/semi-ui';

interface WebhookSecretDialogProps {
  visible: boolean;
  signingSecret: string | null;
  rotation?: boolean;
  onClose: () => void;
}

export function WebhookSecretDialog({
  visible,
  signingSecret,
  rotation = false,
  onClose,
}: WebhookSecretDialogProps) {
  const [confirmed, setConfirmed] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!visible) {
      setConfirmed(false);
      setCopied(false);
    }
  }, [visible]);

  const copySecret = async () => {
    if (!signingSecret) return;
    try {
      await navigator.clipboard.writeText(signingSecret);
      setCopied(true);
      Toast.success({ content: '已复制，请立即保存到接收服务的安全配置中' });
    } catch {
      Toast.error({ content: '复制失败，请手动选择并复制' });
    }
  };

  return (
    <Modal
      visible={visible}
      title={rotation ? '请保存新的签名密钥' : '请保存 Webhook 签名密钥'}
      closable={false}
      maskClosable={false}
      onCancel={() => undefined}
      footer={[
        <Button key="copy" onClick={copySecret} disabled={!signingSecret}>
          {copied ? '再次复制' : '复制密钥'}
        </Button>,
        <Button key="done" type="primary" theme="solid" disabled={!confirmed} onClick={onClose}>
          我已安全保存
        </Button>,
      ]}
    >
      <div className="one-time-key-dialog webhook-secret-dialog">
        <div className="one-time-key-dialog__notice">
          <span className="notice-mark" aria-hidden="true">
            !
          </span>
          <div>
            <Typography.Text strong>关闭后将无法再次查看完整密钥</Typography.Text>
            <Typography.Text type="tertiary">
              {rotation
                ? '请先在接收服务中更新密钥，再重新测试并启用通知。'
                : '接收服务需要使用它验证通知来源，请勿写入前端代码或公开文档。'}
            </Typography.Text>
          </div>
        </div>
        <div className="key-reveal-box" aria-label="Webhook 签名密钥">
          <code>{signingSecret ?? '正在生成…'}</code>
        </div>
        <Checkbox
          checked={confirmed}
          onChange={(event) => setConfirmed(Boolean(event.target.checked))}
        >
          我已将完整密钥保存到安全位置
        </Checkbox>
      </div>
    </Modal>
  );
}
