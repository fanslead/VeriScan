import { useEffect, useState } from 'react';
import { Button, Input, Modal, Tag, TextArea } from '@douyinfe/semi-ui';
import type { RuleSet, RuleSetDraftInput } from '@/shared/api/types';
import { createRuleSetDraft, serializeRuleLines } from './ruleSetFormModel';

interface RuleSetEditorProps {
  visible: boolean;
  ruleSet: RuleSet | null;
  loading: boolean;
  onCancel: () => void;
  onSubmit: (input: RuleSetDraftInput) => void;
}

export function RuleSetEditor({
  visible,
  ruleSet,
  loading,
  onCancel,
  onSubmit,
}: RuleSetEditorProps) {
  const [name, setName] = useState('');
  const [lines, setLines] = useState('');
  const [errors, setErrors] = useState<string[]>([]);

  useEffect(() => {
    if (!visible) return;
    setName(ruleSet?.name ?? '');
    setLines(serializeRuleLines(ruleSet));
    setErrors([]);
  }, [ruleSet, visible]);

  const submit = () => {
    const draft = createRuleSetDraft(name, lines);
    setErrors(draft.errors);
    if (draft.value) onSubmit(draft.value);
  };

  return (
    <Modal
      visible={visible}
      title={ruleSet ? '编辑规则集草稿' : '创建规则集草稿'}
      width={760}
      onCancel={onCancel}
      footer={[
        <Button key="cancel" onClick={onCancel} disabled={loading}>
          取消
        </Button>,
        <Button key="save" type="primary" theme="solid" loading={loading} onClick={submit}>
          保存草稿
        </Button>,
      ]}
    >
      <div className="rule-editor">
        <div className="rule-editor__lead">
          <div>
            <span className="section-kicker">POLICY LIBRARY / DRAFT</span>
            <strong>发布版本不可原地修改</strong>
            <p>保存后先执行服务端校验，再发布并按应用切换。支持直接粘贴批量词条。</p>
          </div>
          <Tag color="amber">草稿</Tag>
        </div>
        <label className="form-field">
          <span>规则集名称</span>
          <Input
            value={name}
            maxLength={100}
            onChange={setName}
            placeholder="例如：社区内容基础规则"
          />
        </label>
        <label className="form-field">
          <span>规则词条</span>
          <TextArea
            value={lines}
            onChange={setLines}
            autosize={{ minRows: 11, maxRows: 18 }}
            placeholder="black | gambling | 1 | 赌博"
          />
          <small>
            每行格式：类型 | 分类代码 | 权重 | 词条。类型可用 black、suspicious、white。
          </small>
        </label>
        {errors.length > 0 ? (
          <div className="rule-editor__errors" role="alert">
            <strong>请修正以下内容</strong>
            {errors.slice(0, 6).map((error) => (
              <span key={error}>{error}</span>
            ))}
          </div>
        ) : null}
      </div>
    </Modal>
  );
}
