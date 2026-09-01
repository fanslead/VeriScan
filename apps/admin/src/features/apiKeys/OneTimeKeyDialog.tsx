import { useEffect, useState } from 'react';
import { Button, Checkbox, Modal, Toast, Typography } from '@douyinfe/semi-ui';
import type { OneTimeApiKey } from '@/shared/api/types';

interface OneTimeKeyDialogProps {
  visible: boolean;
  payload: OneTimeApiKey | null;
  rotation?: boolean;
  onClose: () => void;
}

export function OneTimeKeyDialog({
  visible,
  payload,
  rotation = false,
  onClose,
}: OneTimeKeyDialogProps) {
  const [confirmed, setConfirmed] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!visible) {
      setConfirmed(false);
      setCopied(false);
    }
  }, [visible]);

  const copyKey = async () => {
    if (!payload) return;
    try {
      await navigator.clipboard.writeText(payload.plaintext);
      setCopied(true);
      Toast.success({ content: '已复制，请立即保存到安全位置' });
    } catch {
      Toast.error({ content: '复制失败，请手动选择并复制' });
    }
  };

  return (
    <Modal
      visible={visible}
      title={rotation ? '请保存新的 API Key' : '请保存这枚 API Key'}
      closable={false}
      maskClosable={false}
      onCancel={() => undefined}
      footer={[
        <Button key="copy" onClick={copyKey} disabled={!payload}>
          {copied ? '再次复制' : '复制 Key'}
        </Button>,
        <Button key="done" type="primary" theme="solid" disabled={!confirmed} onClick={onClose}>
          我已安全保存
        </Button>,
      ]}
    >
      <div className="one-time-key-dialog">
        <div className="one-time-key-dialog__notice">
          <span className="notice-mark" aria-hidden="true">
            !
          </span>
          <div>
            <Typography.Text strong>关闭后将无法再次查看完整 Key</Typography.Text>
            <Typography.Text type="tertiary">
              {rotation
                ? '旧 Key 仍然有效；请完成切换后再从列表撤销旧凭证。'
                : '请不要把它放进前端代码、截图或公开文档。'}
            </Typography.Text>
          </div>
        </div>
        <div className="key-reveal-box" aria-label="新建 API Key">
          <code>{payload?.plaintext ?? '正在生成…'}</code>
        </div>
        <Checkbox
          checked={confirmed}
          onChange={(event) => setConfirmed(Boolean(event.target.checked))}
        >
          我已将完整 Key 保存到安全位置
        </Checkbox>
      </div>
    </Modal>
  );
}
