import { Button, Card, Tag, Typography } from '@douyinfe/semi-ui';
import { IconArrowRight, IconSetting, IconShield, IconTickCircle } from '@douyinfe/semi-icons';
import { useNavigate } from 'react-router-dom';
import { apiMode } from '@/shared/api/services';

interface WorkspaceInfoPageProps {
  kind: 'ai' | 'rules';
}

export function WorkspaceInfoPage({ kind }: WorkspaceInfoPageProps) {
  const navigate = useNavigate();
  const isAi = kind === 'ai';
  const isMock = apiMode === 'mock';
  return (
    <div className="page-stack settings-page">
      <div className="page-intro">
        <div>
          <div className="eyebrow">GOVERNANCE / {isAi ? 'MODEL ROUTING' : 'POLICY LIBRARY'}</div>
          <Typography.Title heading={1}>{isAi ? 'AI 配置' : '规则与词库'}</Typography.Title>
          <Typography.Text type="tertiary">
            {isAi
              ? '管理外部判定能力的使用边界与当前生效策略。'
              : '维护规则策略、词库版本与判定依据。'}
          </Typography.Text>
        </div>
      </div>
      <Card className="panel settings-overview">
        <div className="settings-overview__icon">{isAi ? <IconShield /> : <IconSetting />}</div>
        <div>
          <Tag color={isMock ? 'green' : 'grey'}>{isMock ? '基础策略已生效' : '暂无策略数据'}</Tag>
          <Typography.Title heading={4}>
            {isMock ? (isAi ? '社区基础策略' : '当前规则集') : '暂无策略信息'}
          </Typography.Title>
          <Typography.Text type="tertiary">
            {isMock ? '版本 2026.08 · 最近更新今天 09:20' : '当前没有可展示的策略更新记录。'}
          </Typography.Text>
        </div>
        <Button onClick={() => navigate('/applications')} icon={<IconArrowRight />}>
          查看应用使用情况
        </Button>
      </Card>
      <div className="settings-grid">
        <Card className="panel settings-card">
          <div className="section-kicker">ACTIVE SCOPE</div>
          <h2>
            {isMock ? (isAi ? '应用正在使用统一判定策略' : '规则筛查保持稳定运行') : '暂无策略数据'}
          </h2>
          <p>
            {isMock
              ? isAi
                ? '当前所有运行中应用都遵循已发布的边界策略。按应用差异化配置可以在应用详情中查看。'
                : '规则命中会作为快速判定依据，并在审核记录中保留可追溯证据。'
              : '当前没有可展示的策略内容。'}
          </p>
          {isMock ? (
            <div className="setting-check">
              <IconTickCircle />
              已生效
            </div>
          ) : null}
        </Card>
        <Card className="panel settings-card">
          <div className="section-kicker">NEXT REVIEW</div>
          <h2>策略检查提醒</h2>
          <p>
            {isMock
              ? '建议每周查看一次建议复核比例与规则命中变化，确保策略仍符合业务场景。'
              : '暂无策略检查提醒。'}
          </p>
          <Button theme="borderless" type="tertiary" onClick={() => navigate('/records')}>
            打开审核记录 <IconArrowRight />
          </Button>
        </Card>
      </div>
    </div>
  );
}
