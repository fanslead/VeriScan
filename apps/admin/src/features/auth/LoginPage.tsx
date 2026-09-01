import { useState } from 'react';
import { Button, Typography } from '@douyinfe/semi-ui';
import { IconArrowRight, IconInfoCircle, IconShield } from '@douyinfe/semi-icons';
import { useLocation } from 'react-router-dom';
import { useAuthStore } from '@/shared/auth/authStore';

export function LoginPage({ unavailable = false }: { unavailable?: boolean }) {
  const location = useLocation();
  const startLogin = useAuthStore((state) => state.startLogin);
  const authError = useAuthStore((state) => state.error);
  const [pending, setPending] = useState(false);

  const login = async () => {
    setPending(true);
    await startLogin(`${location.pathname}${location.search}`);
    setPending(false);
  };

  return (
    <div className="auth-screen">
      <section className="auth-panel" aria-labelledby="auth-title">
        <div className="auth-panel__brand">
          <span className="auth-mark" aria-hidden="true">
            <IconShield />
          </span>
          <span>
            <strong>明鉴</strong>
            <small>VERISCAN</small>
          </span>
        </div>
        <div className="auth-panel__copy">
          <span className="eyebrow">内容安全运营台</span>
          <Typography.Title heading={1} id="auth-title">
            {unavailable ? '管理入口暂不可用' : '进入管理后台'}
          </Typography.Title>
          <Typography.Text type="tertiary">
            {unavailable
              ? '请联系系统管理员检查访问权限，稍后再试。'
              : '使用组织账号登录，查看应用状态与审核结果。'}
          </Typography.Text>
        </div>
        {authError && !unavailable ? (
          <div className="auth-message" role="alert">
            <IconInfoCircle />
            <span>{authError}</span>
          </div>
        ) : null}
        {!unavailable ? (
          <Button
            theme="solid"
            type="primary"
            size="large"
            block
            loading={pending}
            icon={<IconArrowRight />}
            iconPosition="right"
            onClick={login}
          >
            使用组织账号登录
          </Button>
        ) : null}
        <p className="auth-panel__note">仅授权的运营与安全成员可以访问此空间。</p>
      </section>
    </div>
  );
}
