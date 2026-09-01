import { useEffect, useMemo, useState, type KeyboardEvent } from 'react';
import { Button, Checkbox, Input, Modal, Select, Tag, TextArea } from '@douyinfe/semi-ui';
import { IconChevronDown, IconDelete, IconPlus, IconSearch } from '@douyinfe/semi-icons';
import type {
  CombinationRuleDraftInput,
  RegexRuleDraftInput,
  RuleAction,
  RuleNormalizationProfile,
  RuleSet,
  RuleSetDraftInput,
  WordRuleDraftInput,
  WordRuleType,
} from '@/shared/api/types';
import {
  createEmptyRule,
  createRuleSetDraft,
  combinationRulesFromRuleSet,
  legacyTypeForAction,
  normalizeRuleType,
  parseKeywordLines,
  regexRulesFromRuleSet,
  ruleActionOptions,
  ruleCategoryOptions,
  ruleIdentity,
  rulesFromRuleSet,
  suspiciousStrengthOptions,
  universalRuleActionOptions,
} from './ruleSetFormModel';
import { AdvancedRuleSections } from './AdvancedRuleSections';

interface RuleSetEditorProps {
  visible: boolean;
  ruleSet: RuleSet | null;
  loading: boolean;
  onCancel: () => void;
  onSubmit: (input: RuleSetDraftInput) => void;
}

interface EditableRule extends WordRuleDraftInput {
  clientId: string;
}

const withClientId = (rule: WordRuleDraftInput): EditableRule => ({
  ...rule,
  clientId: crypto.randomUUID(),
});

const optionsForCategory = (category: string) =>
  ruleCategoryOptions.some((item) => item.value === category)
    ? ruleCategoryOptions
    : [...ruleCategoryOptions, { value: category, label: '其他分类' }];

const legacyActionForType = (type: WordRuleType): RuleAction =>
  type === 'black' ? 'hardReject' : type === 'white' ? 'contextException' : 'riskSignal';

const effectiveAction = (rule: WordRuleDraftInput): RuleAction =>
  rule.action ?? legacyActionForType(rule.type);

const actionMeta = (rule: WordRuleDraftInput) =>
  universalRuleActionOptions.find((item) => item.value === effectiveAction(rule)) ??
  universalRuleActionOptions[1];

export function RuleSetEditor({
  visible,
  ruleSet,
  loading,
  onCancel,
  onSubmit,
}: RuleSetEditorProps) {
  const [name, setName] = useState('');
  const [rules, setRules] = useState<EditableRule[]>([]);
  const [regexRules, setRegexRules] = useState<RegexRuleDraftInput[]>([]);
  const [combinationRules, setCombinationRules] = useState<CombinationRuleDraftInput[]>([]);
  const [normalizationProfile, setNormalizationProfile] =
    useState<RuleNormalizationProfile>('default');
  const [errors, setErrors] = useState<string[]>([]);
  const [rowErrors, setRowErrors] = useState<Record<number, string[]>>({});
  const [showBatch, setShowBatch] = useState(false);
  const [batchSource, setBatchSource] = useState('');
  const [batchType, setBatchType] = useState<WordRuleType>('suspicious');
  const [batchCategory, setBatchCategory] = useState('contact');
  const [batchWeight, setBatchWeight] = useState(0.6);
  const [batchErrors, setBatchErrors] = useState<string[]>([]);
  const [expandedRuleId, setExpandedRuleId] = useState<string | null>(null);
  const [ruleSearch, setRuleSearch] = useState('');

  useEffect(() => {
    if (!visible) return;
    setName(ruleSet?.name ?? '');
    const nextRules = rulesFromRuleSet(ruleSet).map(withClientId);
    setRules(nextRules);
    setRegexRules(regexRulesFromRuleSet(ruleSet));
    setCombinationRules(combinationRulesFromRuleSet(ruleSet));
    setNormalizationProfile(ruleSet?.normalizationProfile ?? 'default');
    setExpandedRuleId(nextRules[0]?.clientId ?? null);
    setRuleSearch('');
    setErrors([]);
    setRowErrors({});
    setShowBatch(false);
    setBatchSource('');
    setBatchErrors([]);
  }, [ruleSet, visible]);

  const counts = useMemo(
    () =>
      universalRuleActionOptions.map((option) => ({
        ...option,
        count: rules.filter((rule) => effectiveAction(rule) === option.value).length,
      })),
    [rules],
  );

  const visibleRules = useMemo(() => {
    const keyword = ruleSearch.trim().toLocaleUpperCase('zh-CN');
    return rules
      .map((rule, index) => ({ rule, index }))
      .filter(({ rule }) => {
        if (!keyword) return true;
        const action = actionMeta(rule);
        const category = optionsForCategory(rule.category).find(
          (option) => option.value === rule.category,
        );
        return [rule.term, action.label, category?.label ?? rule.category].some((value) =>
          value.toLocaleUpperCase('zh-CN').includes(keyword),
        );
      });
  }, [ruleSearch, rules]);

  const updateRule = (clientId: string, patch: Partial<WordRuleDraftInput>) => {
    setRules((current) =>
      current.map((rule) => (rule.clientId === clientId ? { ...rule, ...patch } : rule)),
    );
  };

  const changeRuleAction = (clientId: string, action: RuleAction) => {
    const type = legacyTypeForAction(action);
    setRules((current) =>
      current.map((rule) =>
        rule.clientId === clientId ? { ...normalizeRuleType(rule, type), action, clientId } : rule,
      ),
    );
  };

  const moveRuleTypeSelection = (
    event: KeyboardEvent<HTMLButtonElement>,
    clientId: string,
    currentAction: RuleAction,
  ) => {
    const currentIndex = universalRuleActionOptions.findIndex(
      (option) => option.value === currentAction,
    );
    let nextIndex: number | undefined;
    if (event.key === 'ArrowRight' || event.key === 'ArrowDown') {
      nextIndex = (currentIndex + 1) % universalRuleActionOptions.length;
    } else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') {
      nextIndex =
        (currentIndex - 1 + universalRuleActionOptions.length) % universalRuleActionOptions.length;
    } else if (event.key === 'Home') {
      nextIndex = 0;
    } else if (event.key === 'End') {
      nextIndex = universalRuleActionOptions.length - 1;
    }
    if (nextIndex === undefined) return;
    event.preventDefault();
    const nextAction = universalRuleActionOptions[nextIndex].value;
    changeRuleAction(clientId, nextAction);
    requestAnimationFrame(() => {
      document
        .querySelector<HTMLButtonElement>(`[data-rule-action="${clientId}-${nextAction}"]`)
        ?.focus();
    });
  };

  const addRule = () => {
    const nextRule = withClientId(createEmptyRule());
    setRules((current) => [...current, nextRule]);
    setExpandedRuleId(nextRule.clientId);
    setRuleSearch('');
    setTimeout(() => {
      document.querySelector<HTMLElement>('.rule-entry-card:last-of-type input')?.focus();
    });
  };

  const addBatch = () => {
    const parsed = parseKeywordLines(batchSource);
    setBatchErrors(parsed.errors);
    if (parsed.errors.length) return;
    const existingRules = new Set(
      rules
        .filter((rule) => rule.term.trim())
        .map((rule) => ruleIdentity(rule.term, rule.category)),
    );
    const duplicateTerms = parsed.terms.filter((term) =>
      existingRules.has(ruleIdentity(term, batchCategory)),
    );
    if (duplicateTerms.length > 0) {
      setBatchErrors([
        `当前分类中已有 ${duplicateTerms.length} 个相同关键词，请删除重复项后再添加`,
      ]);
      return;
    }
    const weight = batchType === 'black' ? 1 : batchType === 'white' ? 0.1 : batchWeight;
    const addedRules = parsed.terms.map((term) =>
      withClientId({
        term,
        type: batchType,
        action: legacyActionForType(batchType),
        category: batchCategory,
        weight,
      }),
    );
    setRules((current) => [...current, ...addedRules]);
    setExpandedRuleId(addedRules[0]?.clientId ?? null);
    setRuleSearch('');
    setBatchSource('');
    setBatchErrors([]);
    setShowBatch(false);
  };

  const submit = () => {
    const draft = createRuleSetDraft(
      name,
      rules.map(({ clientId, ...rule }) => {
        void clientId;
        return rule;
      }),
      normalizationProfile,
      regexRules,
      combinationRules,
    );
    setErrors(draft.errors);
    setRowErrors(draft.rowErrors);
    if (draft.value) onSubmit(draft.value);
  };

  return (
    <Modal
      visible={visible}
      title={ruleSet ? '编辑规则草稿' : '创建规则草稿'}
      width="min(960px, calc(100vw - 24px))"
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
            <strong>用业务语言说明“遇到什么词，系统怎么处理”</strong>
            <p>每条规则都能预览命中结果。保存后仍需校验和发布，当前线上版本不会被直接修改。</p>
          </div>
          <Tag color="amber">草稿</Tag>
        </div>

        <label className="form-field rule-editor__name">
          <span>规则集名称</span>
          <Input
            value={name}
            maxLength={100}
            onChange={setName}
            placeholder="例如：社区内容基础规则"
          />
        </label>

        <div className="rule-editor__summary" aria-label="规则处理方式统计">
          {counts.map((item) => (
            <span key={item.value} className={`rule-count rule-count--${item.value}`}>
              <strong>{item.count}</strong>
              {item.shortLabel}
            </span>
          ))}
          <span className="rule-editor__summary-total">
            共 {rules.length + regexRules.length + combinationRules.length} 条规则
          </span>
        </div>

        <label className="rule-normalization-option">
          <Checkbox
            checked={normalizationProfile === 'traditionalSimplified'}
            onChange={(event) =>
              setNormalizationProfile(event.target.checked ? 'traditionalSimplified' : 'default')
            }
          />
          <span>
            <strong>同时识别常见繁体写法</strong>
            <small>例如“賭博”和“赌博”按同一个词处理；不影响原文记录。</small>
          </span>
        </label>

        <div className="rule-editor__toolbar">
          <div>
            <strong>关键词规则</strong>
            <span>先填写关键词，再选择命中后的处理方式。</span>
          </div>
          <div>
            <Button icon={<IconPlus />} onClick={addRule}>
              添加一条
            </Button>
            <Button theme="light" onClick={() => setShowBatch((current) => !current)}>
              {showBatch ? '收起批量添加' : '批量添加'}
            </Button>
          </div>
        </div>

        {rules.length > 4 ? (
          <div className="rule-editor__search">
            <Input
              prefix={<IconSearch />}
              value={ruleSearch}
              onChange={setRuleSearch}
              showClear
              aria-label="查找规则"
              placeholder="查找关键词、处理方式或风险分类"
            />
            <span>
              显示 {visibleRules.length} / {rules.length} 条
            </span>
          </div>
        ) : null}

        {showBatch ? (
          <section className="rule-batch-panel" aria-label="批量添加关键词">
            <div className="rule-batch-panel__head">
              <div>
                <strong>一次添加多个关键词</strong>
                <p>每行填写一个关键词，下面选择的处理方式会应用到本次全部关键词。</p>
              </div>
              <Tag color="cyan">无需特殊格式</Tag>
            </div>
            <div className="rule-batch-panel__grid">
              <label className="form-field">
                <span id="rule-batch-type-label">处理方式</span>
                <Select
                  aria-labelledby="rule-batch-type-label"
                  value={batchType}
                  optionList={ruleActionOptions.map((item) => ({
                    value: item.value,
                    label: item.label,
                  }))}
                  onChange={(value) => setBatchType(value as WordRuleType)}
                />
              </label>
              <label className="form-field">
                <span id="rule-batch-category-label">风险分类</span>
                <Select
                  aria-labelledby="rule-batch-category-label"
                  value={batchCategory}
                  optionList={ruleCategoryOptions}
                  onChange={(value) => setBatchCategory(String(value))}
                />
              </label>
              {batchType === 'suspicious' ? (
                <label className="form-field">
                  <span id="rule-batch-strength-label">关注程度</span>
                  <Select
                    aria-labelledby="rule-batch-strength-label"
                    value={batchWeight}
                    optionList={suspiciousStrengthOptions.map((item) => ({
                      value: item.value,
                      label: item.label,
                    }))}
                    onChange={(value) => setBatchWeight(Number(value))}
                  />
                </label>
              ) : null}
            </div>
            <TextArea
              aria-label="批量关键词，每行一个"
              value={batchSource}
              onChange={setBatchSource}
              autosize={{ minRows: 5, maxRows: 10 }}
              placeholder={'诈骗\n虚假中奖\n冒充客服'}
            />
            {batchErrors.map((error) => (
              <span className="field-error" key={error}>
                {error}
              </span>
            ))}
            <div className="rule-batch-panel__action">
              <Button type="primary" theme="solid" onClick={addBatch}>
                添加到规则列表
              </Button>
            </div>
          </section>
        ) : null}

        <div className="rule-entry-list">
          {visibleRules.map(({ rule, index }) => {
            const action = actionMeta(rule);
            const expanded = expandedRuleId === rule.clientId;
            const categoryName =
              optionsForCategory(rule.category).find((option) => option.value === rule.category)
                ?.label ?? rule.category;
            const currentStrength = suspiciousStrengthOptions.some(
              (item) => item.value === rule.weight,
            )
              ? suspiciousStrengthOptions
              : [
                  ...suspiciousStrengthOptions,
                  { value: rule.weight, label: '沿用原有强度', hint: '来自已保存规则' },
                ];
            return (
              <article
                key={rule.clientId}
                className={`rule-entry-card rule-entry-card--${rule.type}${rowErrors[index] ? ' has-error' : ''}`}
              >
                <div className="rule-entry-card__head">
                  <span className="rule-entry-card__number">
                    {String(index + 1).padStart(2, '0')}
                  </span>
                  <div>
                    <strong>{rule.term.trim() || '未填写关键词'}</strong>
                    <span>
                      {action.label} · {categoryName}
                    </span>
                  </div>
                  <Button
                    theme="borderless"
                    type="tertiary"
                    icon={
                      <IconChevronDown
                        className={expanded ? 'rule-chevron is-open' : 'rule-chevron'}
                      />
                    }
                    aria-expanded={expanded}
                    onClick={() => setExpandedRuleId(expanded ? null : rule.clientId)}
                  >
                    {expanded ? '收起' : '编辑'}
                  </Button>
                  <Button
                    type="danger"
                    theme="borderless"
                    icon={<IconDelete />}
                    aria-label={`删除第 ${index + 1} 条规则`}
                    onClick={() => {
                      setRules((current) => current.filter((item) => item !== rule));
                      if (expanded) setExpandedRuleId(null);
                    }}
                  />
                </div>

                {expanded ? (
                  <div className="rule-entry-card__body">
                    <label className="form-field rule-entry-card__term">
                      <span>关键词或短语</span>
                      <Input
                        value={rule.term}
                        maxLength={200}
                        placeholder="例如：加微信"
                        onChange={(term) => updateRule(rule.clientId, { term })}
                      />
                    </label>

                    <div
                      className="rule-action-picker"
                      role="radiogroup"
                      aria-label="命中后如何处理"
                    >
                      <span>命中后如何处理</span>
                      <div>
                        {universalRuleActionOptions.map((option) => (
                          <button
                            key={option.value}
                            type="button"
                            role="radio"
                            aria-checked={effectiveAction(rule) === option.value}
                            tabIndex={effectiveAction(rule) === option.value ? 0 : -1}
                            data-rule-action={`${rule.clientId}-${option.value}`}
                            className={effectiveAction(rule) === option.value ? 'is-selected' : ''}
                            onClick={() => changeRuleAction(rule.clientId, option.value)}
                            onKeyDown={(event) =>
                              moveRuleTypeSelection(event, rule.clientId, option.value)
                            }
                          >
                            <strong>{option.label}</strong>
                            <small>{option.description}</small>
                          </button>
                        ))}
                      </div>
                    </div>

                    <div className="rule-entry-card__settings">
                      <label className="form-field">
                        <span id={`${rule.clientId}-category-label`}>风险分类</span>
                        <Select
                          aria-labelledby={`${rule.clientId}-category-label`}
                          value={rule.category}
                          optionList={optionsForCategory(rule.category)}
                          onChange={(value) =>
                            updateRule(rule.clientId, { category: String(value) })
                          }
                        />
                      </label>
                      {effectiveAction(rule) === 'riskSignal' ? (
                        <label className="form-field">
                          <span id={`${rule.clientId}-strength-label`}>关注程度</span>
                          <Select
                            aria-labelledby={`${rule.clientId}-strength-label`}
                            value={rule.weight}
                            optionList={currentStrength.map((item) => ({
                              value: item.value,
                              label: item.label,
                            }))}
                            onChange={(value) =>
                              updateRule(rule.clientId, { weight: Number(value) })
                            }
                          />
                          <small>
                            {currentStrength.find((item) => item.value === rule.weight)?.hint}
                          </small>
                        </label>
                      ) : (
                        <div className="rule-fixed-setting">
                          <span>{action.label}</span>
                          <small>{action.description}，无需再设置关注程度。</small>
                        </div>
                      )}
                    </div>
                  </div>
                ) : null}

                {expanded ? (
                  <div className="rule-decision-preview">
                    <span>结果预览</span>
                    <strong>命中“{rule.term.trim() || '这个关键词'}”</strong>
                    <i aria-hidden="true">→</i>
                    <strong>{action.preview}</strong>
                  </div>
                ) : null}

                {expanded && rowErrors[index]?.length ? (
                  <div className="rule-entry-card__errors" role="alert">
                    {rowErrors[index].map((error) => (
                      <span key={error}>{error}</span>
                    ))}
                  </div>
                ) : null}
              </article>
            );
          })}
        </div>

        {rules.length > 0 && visibleRules.length === 0 ? (
          <div className="rule-editor__no-result">
            <strong>没有找到匹配的规则</strong>
            <span>换一个关键词，或清除搜索后继续编辑。</span>
            <Button onClick={() => setRuleSearch('')}>清除搜索</Button>
          </div>
        ) : null}

        {rules.length === 0 ? (
          <button className="rule-editor__empty" type="button" onClick={addRule}>
            <IconPlus />
            <strong>添加第一条规则</strong>
            <span>从一个关键词开始，随后选择系统的处理方式。</span>
          </button>
        ) : null}

        <AdvancedRuleSections
          regexRules={regexRules}
          combinationRules={combinationRules}
          onRegexRulesChange={setRegexRules}
          onCombinationRulesChange={setCombinationRules}
        />

        {errors.length > 0 ? (
          <div className="rule-editor__errors" role="alert">
            <strong>还有内容需要完善</strong>
            {errors.map((error) => (
              <span key={error}>{error}</span>
            ))}
          </div>
        ) : null}
      </div>
    </Modal>
  );
}
