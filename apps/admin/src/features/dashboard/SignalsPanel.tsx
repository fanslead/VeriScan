import { Button, Card } from '@douyinfe/semi-ui';
import { IconArrowRight, IconRefresh } from '@douyinfe/semi-icons';
import { Link } from 'react-router-dom';

export function SignalsPanel({ onRefresh }: { onRefresh: () => void }) {
  return (
    <Card
      className="panel"
      title={
        <div className="panel-heading">
          <div>
            <span className="section-kicker">今日 07:00 至今</span>
            <h2>需要关注的变化</h2>
          </div>
          <Button
            theme="borderless"
            icon={<IconRefresh />}
            onClick={onRefresh}
            aria-label="刷新变化"
          >
            刷新
          </Button>
        </div>
      }
    >
      <div className="signal-list">
        <div className="signal-item signal-item--amber">
          <span className="signal-indicator" />
          <div>
            <strong>建议复核比例上升 0.6%</strong>
            <p>远方旅行助手的安全类讨论增多，建议检查该应用的调用方处理情况。</p>
          </div>
          <Link to="/records?status=review">
            查看记录 <IconArrowRight />
          </Link>
        </div>
        <div className="signal-item signal-item--teal">
          <span className="signal-indicator" />
          <div>
            <strong>整体时延下降 8.2%</strong>
            <p>过去一小时外部模型响应稳定，当前 P95 为 37ms。</p>
          </div>
          <Link to="/applications">
            查看应用 <IconArrowRight />
          </Link>
        </div>
      </div>
    </Card>
  );
}
