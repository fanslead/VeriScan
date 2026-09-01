export function formatDate(
  value: string | null | undefined,
  options?: Intl.DateTimeFormatOptions,
  fallback = '暂无数据',
): string {
  if (!value) return fallback;
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? fallback
    : new Intl.DateTimeFormat('zh-CN', options).format(date);
}
