import { useState } from 'react';
import { Button, Modal, TextArea, Typography } from '@douyinfe/semi-ui';

interface ConfirmDangerModalProps {
  visible: boolean;
  title: string;
  description: string;
  confirmText: string;
  onConfirm: (reason: string) => void;
  onCancel: () => void;
  loading?: boolean;
}

export function ConfirmDangerModal({
  visible,
  title,
  description,
  confirmText,
  onConfirm,
  onCancel,
  loading = false,
}: ConfirmDangerModalProps) {
  const [reason, setReason] = useState('');

  const close = () => {
    setReason('');
    onCancel();
  };

  return (
    <Modal
      visible={visible}
      title={title}
      onCancel={close}
      footer={[
        <Button key="cancel" onClick={close} disabled={loading}>
          先不操作
        </Button>,
        <Button
          key="confirm"
          type="danger"
          theme="solid"
          loading={loading}
          disabled={reason.trim().length < 4}
          onClick={() => onConfirm(reason.trim())}
        >
          {confirmText}
        </Button>,
      ]}
    >
      <div className="danger-modal-copy">
        <Typography.Text>{description}</Typography.Text>
        <div className="field-label">操作原因</div>
        <TextArea
          value={reason}
          onChange={setReason}
          placeholder="请填写至少 4 个字，便于后续追溯"
          maxCount={120}
          showClear
          autosize={{ minRows: 3, maxRows: 5 }}
          aria-label="操作原因"
        />
      </div>
    </Modal>
  );
}
