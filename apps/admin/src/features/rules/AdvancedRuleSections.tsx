import { Button, Input, InputNumber, Select, Tag, TextArea } from '@douyinfe/semi-ui';
import { IconDelete, IconPlus } from '@douyinfe/semi-icons';
import type {
  CombinationRuleDraftInput,
  RegexRuleDraftInput,
  RuleAction,
} from '@/shared/api/types';
import {
  createEmptyCombinationRule,
  createEmptyRegexRule,
  formatRulePresets,
  ruleCategoryOptions,
  suspiciousStrengthOptions,
  universalRuleActionOptions,
} from './ruleSetFormModel';

interface AdvancedRuleSectionsProps {
  regexRules: RegexRuleDraftInput[];
  combinationRules: CombinationRuleDraftInput[];
  onRegexRulesChange: (rules: RegexRuleDraftInput[]) => void;
  onCombinationRulesChange: (rules: CombinationRuleDraftInput[]) => void;
}

const actionLabel = (action: RuleAction) =>
  universalRuleActionOptions.find((item) => item.value === action)?.label ?? '交给 AI 判断';

const presetForPattern = (pattern: string) =>
  formatRulePresets.find((item) => item.pattern && item.pattern === pattern)?.value ?? 'custom';

const windowOptions = [
  { value: 32, label: '紧挨着出现' },
  { value: 64, label: '同一句附近' },
  { value: 160, label: '同一段附近' },
  { value: 512, label: '整段内容内' },
];

export function AdvancedRuleSections({
  regexRules,
  combinationRules,
  onRegexRulesChange,
  onCombinationRulesChange,
}: AdvancedRuleSectionsProps) {
  const updateRegexRule = (index: number, patch: Partial<RegexRuleDraftInput>) => {
    onRegexRulesChange(
      regexRules.map((rule, ruleIndex) => (ruleIndex === index ? { ...rule, ...patch } : rule)),
    );
  };
  const updateCombinationRule = (index: number, patch: Partial<CombinationRuleDraftInput>) => {
    onCombinationRulesChange(
      combinationRules.map((rule, ruleIndex) =>
        ruleIndex === index ? { ...rule, ...patch } : rule,
      ),
    );
  };

  return (
    <div className="rule-builder-groups">
      <section className="rule-builder-group" aria-labelledby="format-rules-title">
        <div className="rule-builder-group__head">
          <div>
            <span className="section-kicker">识别格式</span>
            <strong id="format-rules-title">识别手机号、邮箱和链接</strong>
            <p>选择常见格式即可，不需要编写规则代码。</p>
          </div>
          <Button
            icon={<IconPlus />}
            onClick={() => onRegexRulesChange([...regexRules, createEmptyRegexRule()])}
          >
            添加格式
          </Button>
        </div>

        {regexRules.length === 0 ? (
          <button
            type="button"
            className="rule-builder-empty"
            onClick={() => onRegexRulesChange([createEmptyRegexRule()])}
          >
            <IconPlus />
            <strong>添加需要识别的内容格式</strong>
            <span>例如手机号、电子邮箱、网页链接或身份证号。</span>
          </button>
        ) : (
          <div className="rule-builder-list">
            {regexRules.map((rule, index) => {
              const preset = presetForPattern(rule.pattern);
              const formatLabel =
                formatRulePresets.find((item) => item.value === preset)?.label ?? '自定义格式';
              return (
                <article className="rule-builder-card" key={`format-${index}`}>
                  <div className="rule-builder-card__summary">
                    <span className="rule-builder-card__number">{index + 1}</span>
                    <div>
                      <strong>{formatLabel}</strong>
                      <span>
                        {actionLabel(rule.action)} ·{' '}
                        {ruleCategoryOptions.find((item) => item.value === rule.category)?.label ??
                          rule.category}
                      </span>
                    </div>
                    <Tag color="cyan">格式识别</Tag>
                    <Button
                      type="danger"
                      theme="borderless"
                      icon={<IconDelete />}
                      aria-label={`删除第 ${index + 1} 条格式规则`}
                      onClick={() =>
                        onRegexRulesChange(regexRules.filter((_, ruleIndex) => ruleIndex !== index))
                      }
                    />
                  </div>
                  <div className="rule-builder-card__grid">
                    <label className="form-field">
                      <span>识别什么格式</span>
                      <Select
                        value={preset}
                        optionList={formatRulePresets.map(({ value, label }) => ({ value, label }))}
                        onChange={(value) => {
                          const nextPreset = formatRulePresets.find(
                            (item) => item.value === String(value),
                          );
                          if (nextPreset && nextPreset.value !== 'custom') {
                            updateRegexRule(index, { pattern: nextPreset.pattern });
                          } else if (preset !== 'custom') {
                            updateRegexRule(index, { pattern: '' });
                          }
                        }}
                      />
                    </label>
                    <label className="form-field">
                      <span>命中后如何处理</span>
                      <Select
                        value={rule.action}
                        optionList={universalRuleActionOptions.map(({ value, label }) => ({
                          value,
                          label,
                        }))}
                        onChange={(value) =>
                          updateRegexRule(index, { action: value as RuleAction })
                        }
                      />
                    </label>
                    <label className="form-field">
                      <span>风险分类</span>
                      <Select
                        value={rule.category}
                        optionList={ruleCategoryOptions}
                        onChange={(value) => updateRegexRule(index, { category: String(value) })}
                      />
                    </label>
                  </div>
                  {preset === 'custom' ? (
                    <label className="form-field rule-builder-card__custom">
                      <span>自定义识别表达式</span>
                      <Input
                        value={rule.pattern}
                        maxLength={2048}
                        placeholder="仅建议熟悉正则表达式的人员填写"
                        onChange={(pattern) => updateRegexRule(index, { pattern })}
                      />
                      <small>保存后服务端会检查表达式安全性和执行时限。</small>
                    </label>
                  ) : null}
                  {rule.action === 'riskSignal' ? (
                    <label className="form-field rule-builder-card__strength">
                      <span>关注程度</span>
                      <Select
                        value={rule.weight}
                        optionList={suspiciousStrengthOptions.map(({ value, label }) => ({
                          value,
                          label,
                        }))}
                        onChange={(value) => updateRegexRule(index, { weight: Number(value) })}
                      />
                    </label>
                  ) : null}
                  <details className="rule-builder-advanced">
                    <summary>更多适用范围与安全限制</summary>
                    <div className="rule-builder-card__grid">
                      <label className="form-field">
                        <span>适用业务场景（可选）</span>
                        <Input
                          value={rule.scene ?? ''}
                          placeholder="例如：comment"
                          onChange={(scene) => updateRegexRule(index, { scene })}
                        />
                      </label>
                      <label className="form-field">
                        <span>最长检查字符数</span>
                        <InputNumber
                          value={rule.maxInputLength}
                          min={1}
                          max={65_536}
                          onNumberChange={(maxInputLength) =>
                            updateRegexRule(index, { maxInputLength })
                          }
                        />
                      </label>
                      <label className="form-field">
                        <span>单次检查时限（毫秒）</span>
                        <InputNumber
                          value={rule.timeoutMs}
                          min={1}
                          max={2_000}
                          onNumberChange={(timeoutMs) => updateRegexRule(index, { timeoutMs })}
                        />
                      </label>
                    </div>
                  </details>
                </article>
              );
            })}
          </div>
        )}
      </section>

      <section className="rule-builder-group" aria-labelledby="combination-rules-title">
        <div className="rule-builder-group__head">
          <div>
            <span className="section-kicker">组合条件</span>
            <strong id="combination-rules-title">多个词同时出现时触发</strong>
            <p>适合“优惠 + 加微信”这类单个词正常、组合后需要关注的场景。</p>
          </div>
          <Button
            icon={<IconPlus />}
            onClick={() =>
              onCombinationRulesChange([...combinationRules, createEmptyCombinationRule()])
            }
          >
            添加组合
          </Button>
        </div>

        {combinationRules.length === 0 ? (
          <button
            type="button"
            className="rule-builder-empty"
            onClick={() => onCombinationRulesChange([createEmptyCombinationRule()])}
          >
            <IconPlus />
            <strong>添加一组组合条件</strong>
            <span>每行一个关键词，至少填写两个。</span>
          </button>
        ) : (
          <div className="rule-builder-list">
            {combinationRules.map((rule, index) => (
              <article className="rule-builder-card" key={`combination-${index}`}>
                <div className="rule-builder-card__summary">
                  <span className="rule-builder-card__number">{index + 1}</span>
                  <div>
                    <strong>{rule.name.trim() || '未命名组合'}</strong>
                    <span>
                      {rule.terms.filter((term) => term.trim()).join(' + ') || '等待填写关键词'}
                    </span>
                  </div>
                  <Tag color="amber">同时出现</Tag>
                  <Button
                    type="danger"
                    theme="borderless"
                    icon={<IconDelete />}
                    aria-label={`删除第 ${index + 1} 条组合规则`}
                    onClick={() =>
                      onCombinationRulesChange(
                        combinationRules.filter((_, ruleIndex) => ruleIndex !== index),
                      )
                    }
                  />
                </div>
                <div className="rule-builder-card__grid">
                  <label className="form-field">
                    <span>给这组条件起个名称</span>
                    <Input
                      value={rule.name}
                      maxLength={128}
                      placeholder="例如：站外导流"
                      onChange={(name) => updateCombinationRule(index, { name })}
                    />
                  </label>
                  <label className="form-field">
                    <span>这些词需要离多近</span>
                    <Select
                      value={rule.windowSize}
                      optionList={
                        windowOptions.some((item) => item.value === rule.windowSize)
                          ? windowOptions
                          : [...windowOptions, { value: rule.windowSize, label: '沿用原有范围' }]
                      }
                      onChange={(value) =>
                        updateCombinationRule(index, { windowSize: Number(value) })
                      }
                    />
                  </label>
                  <label className="form-field">
                    <span>命中后如何处理</span>
                    <Select
                      value={rule.action}
                      optionList={universalRuleActionOptions.map(({ value, label }) => ({
                        value,
                        label,
                      }))}
                      onChange={(value) =>
                        updateCombinationRule(index, { action: value as RuleAction })
                      }
                    />
                  </label>
                </div>
                <label className="form-field rule-builder-card__terms">
                  <span>需要同时出现的关键词</span>
                  <TextArea
                    value={rule.terms.join('\n')}
                    autosize={{ minRows: 3, maxRows: 8 }}
                    placeholder={'优惠\n加微信'}
                    onChange={(value) =>
                      updateCombinationRule(index, { terms: value.split(/\r?\n/) })
                    }
                  />
                  <small>每行一个，至少两个；不用输入加号或其他分隔符。</small>
                </label>
                <div className="rule-builder-card__grid rule-builder-card__grid--compact">
                  <label className="form-field">
                    <span>风险分类</span>
                    <Select
                      value={rule.category}
                      optionList={ruleCategoryOptions}
                      onChange={(value) =>
                        updateCombinationRule(index, { category: String(value) })
                      }
                    />
                  </label>
                  {rule.action === 'riskSignal' ? (
                    <label className="form-field">
                      <span>关注程度</span>
                      <Select
                        value={rule.weight}
                        optionList={suspiciousStrengthOptions.map(({ value, label }) => ({
                          value,
                          label,
                        }))}
                        onChange={(value) =>
                          updateCombinationRule(index, { weight: Number(value) })
                        }
                      />
                    </label>
                  ) : null}
                  <label className="form-field">
                    <span>适用业务场景（可选）</span>
                    <Input
                      value={rule.scene ?? ''}
                      placeholder="例如：comment"
                      onChange={(scene) => updateCombinationRule(index, { scene })}
                    />
                  </label>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
