import type { ReactNode } from 'react';
import { IconLock } from '@douyinfe/semi-icons';
import { Button, Card } from '@douyinfe/semi-ui';
import { useNavigate } from 'react-router-dom';
import type { AdminCapability } from './permissions';
import { useAdminCapability } from './permissions';

export function CapabilityGuard({
  capability,
  children,
}: {
  capability: AdminCapability;
  children: ReactNode;
}) {
  const allowed = useAdminCapability(capability);
  const navigate = useNavigate();

  if (allowed) return children;

  return (
    <div className="page-stack permission-page">
      <Card className="panel permission-card">
        <span className="permission-card__icon" aria-hidden="true">
          <IconLock />
        </span>
        <div>
          <span className="section-kicker">访问受限</span>
          <h1>当前账号不能进行这项操作</h1>
          <p>你仍可以查看已有数据。如需继续，请联系工作区管理员调整职责。</p>
        </div>
        <Button type="primary" theme="solid" onClick={() => navigate('/', { replace: true })}>
          返回总览
        </Button>
      </Card>
    </div>
  );
}
