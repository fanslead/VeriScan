interface RailNode {
  label: string;
  value: string;
  tone: 'neutral' | 'teal' | 'red' | 'amber';
  detail: string;
}

interface DecisionRailProps {
  nodes: RailNode[];
  compact?: boolean;
}

export function DecisionRail({ nodes, compact = false }: DecisionRailProps) {
  return (
    <div className={`decision-rail${compact ? ' decision-rail--compact' : ''}`} aria-label="判定轨">
      {nodes.map((node, index) => (
        <div className="decision-rail__segment" key={node.label}>
          <div className={`decision-rail__node decision-rail__node--${node.tone}`}>
            <span className="decision-rail__marker" aria-hidden="true" />
            <div>
              <div className="decision-rail__label">{node.label}</div>
              <div className="decision-rail__value">{node.value}</div>
              <div className="decision-rail__detail">{node.detail}</div>
            </div>
          </div>
          {index < nodes.length - 1 ? (
            <div className="decision-rail__line" aria-hidden="true" />
          ) : null}
        </div>
      ))}
    </div>
  );
}
