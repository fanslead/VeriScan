import { Link, NavLink } from 'react-router-dom';
import {
  IconAppCenter,
  IconChevronDown,
  IconClose,
  IconFile,
  IconHome,
  IconSetting,
  IconShield,
} from '@douyinfe/semi-icons';
import { useUiStore } from '@/shared/state/uiStore';
import { isMockMode } from '@/shared/auth/oidc';
import { useAdminCapability } from '@/shared/auth/permissions';

const primaryNavigation = [
  { label: '总览', to: '/', icon: <IconHome size="default" /> },
  { label: '应用', to: '/applications', icon: <IconAppCenter size="default" /> },
  { label: '审核记录', to: '/records', icon: <IconFile size="default" /> },
];

const systemNavigation = [
  { label: 'AI 配置', to: '/ai-settings', icon: <IconShield size="default" /> },
  { label: '规则与词库', to: '/rules', icon: <IconSetting size="default" /> },
];

function NavGroup({
  label,
  items,
  onNavigate,
}: {
  label: string;
  items: typeof primaryNavigation;
  onNavigate: () => void;
}) {
  return (
    <div className="nav-group">
      <div className="nav-group-label">{label}</div>
      {items.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.to === '/'}
          onClick={onNavigate}
          className={({ isActive }) => `nav-item${isActive ? ' is-active' : ''}`}
        >
          <span className="nav-item-icon">{item.icon}</span>
          <span className="nav-item-label">{item.label}</span>
          {item.label === '审核记录' && isMockMode ? <span className="nav-count">24</span> : null}
        </NavLink>
      ))}
    </div>
  );
}

export function Sidebar({ mobileOpen, onClose }: { mobileOpen: boolean; onClose: () => void }) {
  const collapsed = useUiStore((state) => state.sidebarCollapsed);
  const toggleSidebar = useUiStore((state) => state.toggleSidebar);
  const canView = useAdminCapability('view');
  const canAudit = useAdminCapability('audit');
  const governanceNavigation = canAudit
    ? [...systemNavigation, { label: '审计日志', to: '/audit', icon: <IconFile size="default" /> }]
    : systemNavigation;

  return (
    <>
      <div
        className={`sidebar-scrim${mobileOpen ? ' is-visible' : ''}`}
        onClick={onClose}
        aria-hidden="true"
      />
      <aside
        className={`app-sidebar${collapsed ? ' is-collapsed' : ''}${mobileOpen ? ' is-mobile-open' : ''}`}
        aria-label="主导航"
      >
        <div className="brand-lockup">
          <Link to="/" className="brand-link" onClick={onClose} aria-label="返回总览">
            <span className="brand-mark" aria-hidden="true">
              <IconShield size="small" />
            </span>
            <span className="brand-copy">
              <strong>明鉴</strong>
              <small>VERISCAN</small>
            </span>
          </Link>
          <button className="sidebar-close" type="button" onClick={onClose} aria-label="关闭导航">
            <IconClose />
          </button>
        </div>

        <div className="sidebar-context">
          <span className="context-pulse" aria-hidden="true" />
          <span className="context-copy">
            <small>工作区</small>
            <strong>内容安全中台</strong>
          </span>
          <IconChevronDown className="context-chevron" size="small" />
        </div>

        <nav className="nav-groups">
          {canView ? (
            <>
              <NavGroup label="工作台" items={primaryNavigation} onNavigate={onClose} />
              <NavGroup label="治理" items={governanceNavigation} onNavigate={onClose} />
            </>
          ) : null}
        </nav>

        <div className="sidebar-footer">
          <div className="system-health">
            <span className="health-dot" aria-hidden="true" />
            <span>{isMockMode ? '服务运行正常' : '管理端已连接'}</span>
            <small>{isMockMode ? '延迟 37ms' : '实时数据'}</small>
          </div>
          <button
            className="collapse-button"
            type="button"
            onClick={toggleSidebar}
            aria-label={collapsed ? '展开导航' : '收起导航'}
          >
            <IconChevronDown className={collapsed ? 'rotate-90' : 'rotate-minus-90'} size="small" />
            <span>{collapsed ? '展开导航' : '收起导航'}</span>
          </button>
        </div>
      </aside>
    </>
  );
}
