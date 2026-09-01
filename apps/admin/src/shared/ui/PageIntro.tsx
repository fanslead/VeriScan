import { Button, Typography } from '@douyinfe/semi-ui';
import type { ReactNode } from 'react';

interface PageIntroProps {
  eyebrow: string;
  title: string;
  description: string;
  actions?: ReactNode;
}

export function PageIntro({ eyebrow, title, description, actions }: PageIntroProps) {
  return (
    <div className="page-intro">
      <div>
        <div className="eyebrow">{eyebrow}</div>
        <Typography.Title heading={1}>{title}</Typography.Title>
        <Typography.Text type="tertiary">{description}</Typography.Text>
      </div>
      {actions ? <div className="page-actions">{actions}</div> : null}
    </div>
  );
}

export function TextAction({ children, onClick }: { children: ReactNode; onClick: () => void }) {
  return (
    <Button theme="borderless" type="tertiary" onClick={onClick}>
      {children}
    </Button>
  );
}
