import { Card } from '@douyinfe/semi-ui';
import { DecisionRail } from '@/shared/ui/DecisionRail';

interface DecisionRailNode {
  label: string;
  value: string;
  tone: 'neutral' | 'teal' | 'red' | 'amber';
  detail: string;
}

export function ApplicationDecisionCard({
  nodes,
  policyVersion,
}: {
  nodes: DecisionRailNode[];
  policyVersion: string | null;
}) {
  return (
    <Card
      className="panel detail-panel"
      title={
        <div className="panel-heading">
          <div>
            <span className="section-kicker">处理链路</span>
            <h2>应用判定轨</h2>
          </div>
          <span className="panel-meta">策略 {policyVersion || '暂无版本'}</span>
        </div>
      }
    >
      <DecisionRail nodes={nodes} />
      <div className="detail-callout">
        <span className="detail-callout__icon">i</span>
        <p>
          这枚应用的机器结论会原样返回调用方；“建议复核”只表示结果处于边界，不会在明鉴后台生成待办。
        </p>
      </div>
    </Card>
  );
}
