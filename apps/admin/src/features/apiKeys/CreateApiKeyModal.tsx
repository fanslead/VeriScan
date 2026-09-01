import { useEffect, useState } from 'react';
import { DatePicker, Input, Modal } from '@douyinfe/semi-ui';
import { IconKey } from '@douyinfe/semi-icons';
import type { CreateKeyInput } from '@/shared/api/types';

interface CreateApiKeyModalProps {
  visible: boolean;
  applicationId: string;
  applicationName: string;
  loading?: boolean;
  onCancel: () => void;
  onSubmit: (input: CreateKeyInput) => void;
}

export function CreateApiKeyModal({
  visible,
  applicationId,
  applicationName,
  loading = false,
  onCancel,
  onSubmit,
}: CreateApiKeyModalProps) {
  const [name, setName] = useState('');
  const [expiresAt, setExpiresAt] = useState('');

  useEffect(() => {
    if (!visible) {
      setName('');
      setExpiresAt('');
    }
  }, [visible]);

  const canSubmit = name.trim().length >= 2 && Boolean(expiresAt);
  const submit = () => {
    if (canSubmit) onSubmit({ applicationId, name: name.trim(), expiresAt });
  };

  return (
    <Modal
      visible={visible}
      title="创建 API Key"
      onCancel={onCancel}
      onOk={submit}
      okText="生成并显示 Key"
      cancelText="取消"
      confirmLoading={loading}
      okButtonProps={{ disabled: !canSubmit }}
    >
      <div className="key-form">
        <div className="modal-lead">
          <span className="modal-lead__icon">
            <IconKey />
          </span>
          <div>
            <strong>为 {applicationName} 创建凭证</strong>
            <span>生成后完整 Key 只会展示一次。</span>
          </div>
        </div>
        <label className="form-field">
          <span>
            凭证名称 <i>*</i>
          </span>
          <Input
            value={name}
            onChange={setName}
            placeholder="例如：生产服务"
            autoFocus
            maxLength={40}
          />
        </label>
        <label className="form-field">
          <span>
            到期时间 <i>*</i>
          </span>
          <DatePicker
            type="date"
            value={expiresAt ? new Date(expiresAt) : undefined}
            disabledDate={(date) => Boolean(date && date.getTime() <= Date.now())}
            onChange={(date) => setExpiresAt(date instanceof Date ? date.toISOString() : '')}
            placeholder="请选择未来日期"
            style={{ width: '100%' }}
          />
        </label>
        <div className="form-hint">每枚凭证都必须设置到期时间；建议不晚于一年，便于定期轮换。</div>
      </div>
    </Modal>
  );
}
