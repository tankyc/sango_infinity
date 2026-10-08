import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
import { LayoutDashboard, Package, ShieldAlert, Users } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { roleLabel } from '../../lib/catalog';
import { buttonClass } from '../ui/Button';
import { LoadingBlock } from '../ui/Feedback';
import { cn } from '../../lib/cn';

/**
 * 管理后台外壳：权限守卫 + 侧边导航 + 内容出口
 *
 * 前端守卫只是「体验」层面的（避免普通用户点进来看到一堆 403），
 * 真正的权限判定在后端每个 /api/admin/* 上，前端永远不可信。
 */

const ADMIN_NAV = [
  { to: '/admin', label: '概览', icon: LayoutDashboard, end: true },
  { to: '/admin/users', label: '账号管理', icon: Users, end: false },
  { to: '/admin/mods', label: '内容管理', icon: Package, end: false },
];

export default function AdminShell() {
  const { user, initializing } = useAuth();
  const location = useLocation();
  // 带上来源路径，登录后能直接回到刚才想去的后台页面
  const loginLink = `/login?next=${encodeURIComponent(location.pathname + location.search)}`;

  if (initializing) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-20">
        <LoadingBlock text="正在校验权限…" />
      </div>
    );
  }

  if (!user) {
    return (
      <div className="mx-auto max-w-lg px-4 py-20 text-center">
        <ShieldAlert aria-hidden className="mx-auto h-8 w-8 text-gold-500" />
        <h1 className="mt-3 font-serif text-xl text-paper-100">管理后台需要登录</h1>
        <p className="mt-2 text-sm text-paper-500">请使用管理员账号登录后再访问。</p>
        <Link to={loginLink} className={buttonClass('gold', 'md', 'mt-5')}>
          去登录
        </Link>
      </div>
    );
  }

  if (user.role !== 'admin') {
    return (
      <div className="mx-auto max-w-lg px-4 py-20 text-center">
        <ShieldAlert aria-hidden className="mx-auto h-8 w-8 text-cinnabar-400" />
        <h1 className="mt-3 font-serif text-xl text-paper-100">没有管理员权限</h1>
        <p className="mt-2 text-sm text-paper-500">
          当前账号「{user.username}」的角色是「{roleLabel(user.role)}」，如需管理权限请联系管理员。
        </p>
        <Link to="/browse" className={buttonClass('outline', 'md', 'mt-5')}>
          返回工坊
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-[1600px] px-4 py-6 sm:px-5">
      <div className="grid gap-6 lg:grid-cols-[212px_minmax(0,1fr)]">
        <aside className="lg:sticky lg:top-24 lg:self-start">
          <div className="panel p-2.5">
            <p className="px-2.5 pb-2 pt-1 text-[11px] tracking-[0.2em] text-gold-500">管理后台</p>
            <nav className="space-y-1" aria-label="后台导航">
              {ADMIN_NAV.map(({ to, label, icon: Icon, end }) => (
                <NavLink
                  key={to}
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm transition-colors duration-200',
                      isActive
                        ? 'bg-gold-500/12 text-gold-300 shadow-gold-sm'
                        : 'text-paper-300 hover:bg-ink-700/70 hover:text-paper-100',
                    )
                  }
                >
                  <Icon aria-hidden className="h-4 w-4" />
                  {label}
                </NavLink>
              ))}
            </nav>
            <div className="gold-rule my-2" />
            <div className="px-2.5 pb-1 text-xs text-paper-500">
              当前身份
              <p className="mt-0.5 truncate text-paper-300">{user.nickname ?? user.username}</p>
            </div>
          </div>
        </aside>

        <div className="min-w-0">
          <Outlet />
        </div>
      </div>
    </div>
  );
}
