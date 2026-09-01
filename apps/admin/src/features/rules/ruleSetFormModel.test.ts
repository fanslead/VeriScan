import { describe, expect, it } from 'vitest';
import {
  createEmptyRule,
  createRuleSetDraft,
  normalizeRuleType,
  parseKeywordLines,
  ruleIdentity,
} from './ruleSetFormModel';

describe('普通用户规则编辑模型', () => {
  it('按每行一个关键词解析批量输入并自动去重', () => {
    expect(parseKeywordLines('诈骗\n 赌博 \nＧＡＭＢＬＩＮＧ\ngambling\n诈骗\n')).toEqual({
      terms: ['诈骗', '赌博', 'ＧＡＭＢＬＩＮＧ'],
      errors: [],
    });
  });

  it('重复身份与服务端一样同时考虑规范化关键词和分类', () => {
    expect(ruleIdentity('ＶｅｒｉＳｃａｎ', 'product')).toBe(ruleIdentity('veriscan', 'product'));
    expect(ruleIdentity('诈骗', 'fraud')).not.toBe(ruleIdentity('诈骗', 'contact'));
  });

  it('切换处理方式时自动使用安全的默认强度', () => {
    const rule = createEmptyRule();
    expect(normalizeRuleType(rule, 'black').weight).toBe(1);
    expect(normalizeRuleType(rule, 'white').weight).toBe(0.1);
    expect(normalizeRuleType(rule, 'suspicious').weight).toBe(0.6);
  });

  it('在具体规则上提示缺失、重复和无效分类', () => {
    const result = createRuleSetDraft('基础规则', [
      { term: '诈骗', type: 'black', category: 'fraud', weight: 1 },
      { term: '诈骗', type: 'suspicious', category: 'fraud', weight: 0.6 },
      { term: '伪造', type: 'suspicious', category: '中文分类', weight: 0.6 },
      { term: '', type: 'white', category: 'product', weight: 0.1 },
    ]);

    expect(result.value).toBeUndefined();
    expect(result.rowErrors[1]).toContain('与第 1 条同分类关键词重复');
    expect(result.rowErrors[2]).toContain('风险分类无效，请重新选择');
    expect(result.rowErrors[3]).toContain('请填写关键词');
  });

  it('允许相同关键词用于不同风险分类', () => {
    const result = createRuleSetDraft('基础规则', [
      { term: '引流', type: 'suspicious', category: 'contact', weight: 0.6 },
      { term: '引流', type: 'suspicious', category: 'fraud', weight: 0.6 },
    ]);

    expect(result.errors).toEqual([]);
    expect(result.value?.rules).toHaveLength(2);
  });
});
