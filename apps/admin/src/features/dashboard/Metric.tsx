interface MetricProps {
  label: string;
  value: string;
  unit?: string;
  delta: number | null;
  tone: 'teal' | 'red' | 'amber' | 'neutral';
}

export function Metric({ label, value, unit, delta, tone }: MetricProps) {
  return (
    <div className="metric-cell">
      <div className="metric-label">{label}</div>
      <div className={`metric-value metric-value--${tone}`}>
        {value}
        {value !== '暂无统计' ? <small>{unit}</small> : null}
      </div>
      {delta === null ? (
        <div className="metric-delta">
          <span>—</span>
          暂无数据
        </div>
      ) : (
        <div className={`metric-delta ${delta < 0 ? 'is-good' : ''}`}>
          <span>{delta < 0 ? '↓' : '↑'}</span>
          {Math.abs(delta)}% <em>较昨日同期</em>
        </div>
      )}
    </div>
  );
}
