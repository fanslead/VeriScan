import type { ReviewSource } from '@/shared/api/types';

export function reviewSourceLabel(source: ReviewSource | null): string {
  if (!source) return '规则明确判定';
  const labelsBySource: Record<ReviewSource, string> = {
    model_ambiguous: '模型判断存在歧义',
    policy_required: '策略要求结合上下文',
    provider_refusal: '模型未提供可用结论',
    ai_failure_fallback: '模型服务异常降级',
  };
  return labelsBySource[source];
}
