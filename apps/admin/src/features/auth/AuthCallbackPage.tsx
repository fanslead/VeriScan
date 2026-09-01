import { useEffect, useRef, useState } from 'react';
import { Button, Typography } from '@douyinfe/semi-ui';
import { IconInfoCircle, IconShield } from '@douyinfe/semi-icons';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuthStore } from '@/shared/auth/authStore';
import { LoadingBlock } from '@/shared/ui/LoadingBlock';

export function AuthCallbackPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const completeCallback = useAuthStore((state) => state.completeCallback);
  const [error, setError] = useState(false);
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    void completeCallback(
      `${window.location.origin}${location.pathname}${location.search}${location.hash}`,
    )
      .then((returnPath) => navigate(returnPath ?? '/', { replace: true }))
      .catch(() => setError(true));
  }, [completeCallback, location.hash, location.pathname, location.search, navigate]);

  if (!error) {
    return (
      <div className="auth-screen" role="status" aria-label="正在完成登录">
        <div className="auth-panel auth-panel--loading">
          <span className="auth-mark" aria-hidden="true">
            <IconShield />
          </span>
          <Typography.Title heading={3}>正在确认登录</Typography.Title>
          <Typography.Text type="tertiary">马上就好，请不要关闭当前页面。</Typography.Text>
          <LoadingBlock rows={2} />
        </div>
      </div>
    );
  }

  return (
    <div className="auth-screen">
      <section className="auth-panel" role="alert" aria-labelledby="callback-error-title">
        <span className="auth-mark auth-mark--error" aria-hidden="true">
          <IconInfoCircle />
        </span>
        <Typography.Title heading={3} id="callback-error-title">
          登录没有完成
        </Typography.Title>
        <Typography.Text type="tertiary">请返回登录页重新尝试。</Typography.Text>
        <Button
          theme="solid"
          type="primary"
          onClick={() => navigate('/auth/login', { replace: true })}
        >
          返回登录
        </Button>
      </section>
    </div>
  );
}
