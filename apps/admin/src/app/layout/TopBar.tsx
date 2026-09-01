import { useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { IconBell, IconChevronDown, IconMenu, IconPlus } from '@douyinfe/semi-icons';
import { Button, Dropdown, Tooltip } from '@douyinfe/semi-ui';
import { useAuthStore } from '@/shared/auth/authStore';
import { isMockMode } from '@/shared/auth/oidc';

export function TopBar({ onMenu }: { onMenu: () => void }) {
  const location = useLocation();
  const navigate = useNavigate();
  const [notificationOpen, setNotificationOpen] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false);
  const user = useAuthStore((state) => state.user);
  const logout = useAuthStore((state) => state.logout);
  const profile = user?.profile as Record<string, unknown> | undefined;
  const profileName = isMockMode
    ? '林默'
    : String(profile?.preferred_username ?? profile?.name ?? '已登录用户');
  const crumbs = useMemo(() => {
    if (location.pathname.startsWith('/applications/new')) return ['应用', '创建应用'];
    if (location.pathname.startsWith('/applications/')) return ['应用', '应用详情'];
    if (location.pathname.startsWith('/records')) return ['审核记录'];
    if (location.pathname.startsWith('/ai-settings')) return ['AI 配置'];
    if (location.pathname.startsWith('/rules')) return ['规则与词库'];
    return ['总览'];
  }, [location.pathname]);

  return (
    <header className="topbar">
      <div className="topbar-left">
        <button className="mobile-menu-button" type="button" onClick={onMenu} aria-label="打开导航">
          <IconMenu />
        </button>
        <div className="breadcrumbs" aria-label="当前位置">
          <span>明鉴</span>
          {crumbs.map((crumb, index) => (
            <span
              key={`${crumb}-${index}`}
              className={index === crumbs.length - 1 ? 'is-current' : ''}
            >
              <i>/</i>
              {crumb}
            </span>
          ))}
        </div>
      </div>
      <div className="topbar-actions">
        <Button
          theme="borderless"
          type="tertiary"
          icon={<IconPlus />}
          onClick={() => navigate('/applications/new')}
        >
          新建应用
        </Button>
        {isMockMode ? (
          <Dropdown
            trigger="click"
            visible={notificationOpen}
            onVisibleChange={setNotificationOpen}
            position="bottomRight"
            render={
              <div className="notification-popover">
                <strong>运行提示</strong>
                <span>今日审核量较昨日同期上升 12.8%</span>
                <small>刚刚</small>
              </div>
            }
          >
            <Tooltip content="通知">
              <button className="icon-button" type="button" aria-label="查看通知">
                <IconBell />
              </button>
            </Tooltip>
          </Dropdown>
        ) : null}
        <div className="topbar-divider" aria-hidden="true" />
        <Dropdown
          trigger="click"
          visible={accountOpen}
          onVisibleChange={setAccountOpen}
          position="bottomRight"
          render={
            <div className="account-popover">
              <span>{profileName}</span>
              {!isMockMode ? (
                <button
                  type="button"
                  onClick={() => {
                    setAccountOpen(false);
                    void logout();
                  }}
                >
                  退出登录
                </button>
              ) : null}
            </div>
          }
        >
          <button className="profile-chip" type="button" aria-label="打开账户菜单">
            <span className="avatar">{profileName.slice(0, 1)}</span>
            <span className="profile-name">{profileName}</span>
            <IconChevronDown size="small" />
          </button>
        </Dropdown>
      </div>
    </header>
  );
}
