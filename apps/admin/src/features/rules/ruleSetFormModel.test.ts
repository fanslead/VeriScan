import { describe, expect, it } from 'vitest';
import { createRuleSetDraft, parseRuleLines } from './ruleSetFormModel';

describe('规则集批量输入', () => {
  it('解析中英文类型并忽略注释和空行', () => {
    const result = parseRuleLines(`
# 类型 | 分类 | 权重 | 词条
black | gambling | 1 | 赌博
可疑词 | contact | 0.6 | 加微信
white | product | 0.1 | 明鉴
`);

    expect(result.errors).toEqual([]);
    expect(result.rules).toEqual([
      { type: 'black', category: 'gambling', weight: 1, term: '赌博' },
      { type: 'suspicious', category: 'contact', weight: 0.6, term: '加微信' },
      { type: 'white', category: 'product', weight: 0.1, term: '明鉴' },
    ]);
  });

  it('逐行返回格式、分类与权重错误', () => {
    const result = createRuleSetDraft('x', 'unknown | 中文分类 | 2 | test\nmissing');

    expect(result.value).toBeUndefined();
    expect(result.errors).toContain('规则集名称长度必须在 2 到 100 个字符之间');
    expect(result.errors.some((error) => error.includes('类型无效'))).toBe(true);
    expect(result.errors.some((error) => error.includes('分类代码无效'))).toBe(true);
    expect(result.errors.some((error) => error.includes('权重必须'))).toBe(true);
    expect(result.errors.some((error) => error.includes('需要 4 列'))).toBe(true);
  });
});
