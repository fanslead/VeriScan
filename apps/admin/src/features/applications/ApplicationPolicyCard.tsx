import { Button, Card, Tag } from '@douyinfe/semi-ui';
import { IconSetting } from '@douyinfe/semi-icons';
import type { Application } from '@/shared/api/types';

export function ApplicationPolicyCard({
  application,
  onOpenRules,
}: {
  application: Application;
  onOpenRules: () => void;
}) {
  return (
    <Card
      className="panel detail-panel"
      title={
        <div className="panel-heading">
          <div>
            <span className="section-kicker">POLICY</span>
            <h2>当前策略</h2>
          </div>
          <Button theme="borderless" icon={<IconSetting />} onClick={onOpenRules}>
            查看策略
          </Button>
        </div>
      }
    >
      <div className="policy-row">
        <div className="policy-icon">P</div>
        <div>
          <strong>{application.policyName || '暂无策略'}</strong>
          <span>
            {application.policyVersion ? `版本 ${application.policyVersion}` : '暂无版本'}
          </span>
        </div>
        <Tag color={application.policyVersion ? 'green' : 'grey'}>
          {application.policyVersion ? '生效中' : '未配置'}
        </Tag>
      </div>
      <div className="policy-facts">
        <div>
          <span>规则筛查</span>
          <strong>{application.policyVersion ? '已启用' : '未配置'}</strong>
        </div>
        <div>
          <span>边界判定</span>
          <strong>{application.policyVersion ? '版本固化' : '未配置'}</strong>
        </div>
        <div>
          <span>外部模型</span>
          <strong>按全局路由</strong>
        </div>
      </div>
    </Card>
  );
}
