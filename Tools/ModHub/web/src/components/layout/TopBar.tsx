import { useEffect, useState, type FormEvent } from 'react';
import { Link, NavLink, useNavigate, useSearchParams } from 'react-router-dom';
import { BookUser, Compass, FolderKanban, LogOut, ScrollText, Search, ShieldCheck, Upload, UserRound, type LucideIcon } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { ThemeSwitcher } from './ThemeSwitcher';
import { Button } from '../ui/Button';
import { cn } from '../../lib/cn';
import { PERSON_LIB_URL, SCENARIO_URL } from '../../lib/siteLinks';

/**
 * 顶部导航
 * Steam 工坊的信息架构：品牌在左、全局搜索居中、主导航与账号在右。
 * 搜索框与 URL 的 ?q= 双向同步 —— 这样浏览页的「当前搜索词」永远可从地址栏复现与分享。
 */

/** 导航项。external 为 true 时是站外链接，渲染成新窗口打开的 <a>，不参与路由激活判断 */
interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  external?: boolean;
}

const NAV_ITEMS: NavItem[] = [
  { to: '/browse', label: '浏览工坊', icon: Compass },
  { to: '/upload', label: '发布模组', icon: Upload },
  // 站外：武将库与剧本编辑器。放进主导航是因为「做武将 / 编剧本 → 发模组」
  // 本来就是同一条工作流，三站之间来回跳转是常态
  { to: PERSON_LIB_URL, label: '武将库', icon: BookUser, external: true },
  { to: SCENARIO_URL, label: '剧本编辑器', icon: ScrollText, external: true },
];

/** 仅登录用户可见：管理自己发布的模组 */
const MY_NAV_ITEM: NavItem = { to: '/my', label: '我的创作', icon: FolderKanban };

/** 仅管理员可见的入口：普通用户看不到「管理后台」，避免点了才发现没权限 */
const ADMIN_NAV_ITEM: NavItem = { to: '/admin', label: '管理后台', icon: ShieldCheck };

/** 导航项样式：NavLink 与站外 <a> 共用，免得一边改了一边忘 */
const NAV_ITEM_CLASS = 'flex h-10 items-center gap-2 rounded-md px-3 text-sm transition-colors duration-200';
const NAV_ITEM_MOBILE_CLASS = 'flex h-9 flex-1 items-center justify-center gap-2 rounded-md text-sm transition-colors duration-200';
/** 激活态 */
const NAV_ITEM_ACTIVE = 'bg-gold-500/12 text-gold-300';
/** 未激活态；站外链接没有激活概念，固定用它 */
const NAV_ITEM_IDLE = 'text-paper-300 hover:bg-ink-700/70 hover:text-paper-100';

export function TopBar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [keyword, setKeyword] = useState(searchParams.get('q') ?? '');

  // 地址栏变化（回退/前进、点了搜索建议）时同步输入框
  useEffect(() => {
    setKeyword(searchParams.get('q') ?? '');
  }, [searchParams]);

  function handleSearch(e: FormEvent) {
    e.preventDefault();
    const q = keyword.trim();
    navigate(q === '' ? '/browse' : `/browse?q=${encodeURIComponent(q)}`);
  }

  // 登录后出现「我的创作」；管理员再追加「管理后台」
  const navItems = [
    ...NAV_ITEMS,
    ...(user ? [MY_NAV_ITEM] : []),
    ...(user?.role === 'admin' ? [ADMIN_NAV_ITEM] : []),
  ];

  return (
    <header className="sticky top-0 z-40 border-b border-gold-900/60 bg-ink-850/95 backdrop-blur">
      <div className="mx-auto flex h-16 max-w-[1600px] items-center gap-3 px-3 sm:gap-4 sm:px-5">
        {/* 品牌：印章式方框，避免额外图片资源 */}
        <Link to="/browse" className="flex shrink-0 items-center gap-2.5" title="返回工坊首页">
          <span
            aria-hidden
            className="flex h-9 w-9 items-center justify-center rounded border border-gold-600/80 bg-gradient-to-br from-gold-500/25 to-transparent font-serif text-base font-bold text-gold-300"
          >
            三
          </span>
          <span className="hidden flex-col leading-tight sm:flex">
            <span className="font-serif text-base text-paper-100">创意工坊</span>
            <span className="text-[11px] tracking-wide text-paper-500">SANGO INFINITY</span>
          </span>
        </Link>

        {/* 全局搜索 */}
        <form onSubmit={handleSearch} className="relative min-w-0 flex-1 sm:max-w-lg" role="search">
          <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-paper-500" />
          <input
            type="search"
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            placeholder="搜索模组、作者或标签…"
            aria-label="搜索模组"
            className={cn(
              'h-10 w-full rounded-md border border-ink-500/80 bg-ink-900/80 pl-9 pr-3 text-sm text-paper-100',
              'placeholder:text-paper-600 transition-colors duration-200 hover:border-gold-700/70 focus:border-gold-500 focus:outline-none',
            )}
          />
        </form>

        {/* 主导航 */}
        <nav className="hidden items-center gap-1 md:flex">
          {navItems.map(({ to, label, icon: Icon, external }) =>
            external ? (
              <a
                key={to}
                href={to}
                target="_blank"
                rel="noreferrer noopener"
                title={`在新窗口打开${label}`}
                className={cn(NAV_ITEM_CLASS, NAV_ITEM_IDLE)}
              >
                <Icon aria-hidden className="h-4 w-4" />
                {label}
              </a>
            ) : (
              <NavLink
                key={to}
                to={to}
                className={({ isActive }) => cn(NAV_ITEM_CLASS, isActive ? NAV_ITEM_ACTIVE : NAV_ITEM_IDLE)}
              >
                <Icon aria-hidden className="h-4 w-4" />
                {label}
              </NavLink>
            ),
          )}
        </nav>

        {/* 账号区 */}
        <div className="ml-auto flex shrink-0 items-center gap-2">
          <ThemeSwitcher />
          {user ? (
            <>
              <span className="hidden items-center gap-2 text-sm text-paper-300 sm:flex" title={`账号：${user.username}`}>
                <UserRound aria-hidden className="h-4 w-4 text-gold-400" />
                <span className="max-w-24 truncate">{user.nickname ?? user.username}</span>
              </span>
              <Button
                variant="ghost"
                size="sm"
                icon={<LogOut className="h-3.5 w-3.5" aria-hidden />}
                onClick={logout}
                title="退出登录"
              >
                <span className="hidden sm:inline">退出</span>
              </Button>
            </>
          ) : (
            <Link to="/login">
              <Button variant="outline" size="sm" icon={<UserRound className="h-3.5 w-3.5" aria-hidden />}>
                登录
              </Button>
            </Link>
          )}
        </div>
      </div>
      {/* 移动端也要能到达主导航 */}
      <nav className="flex items-center gap-1 border-t border-ink-700/70 px-3 py-1.5 md:hidden">
        {navItems.map(({ to, label, icon: Icon, external }) =>
          external ? (
            <a
              key={to}
              href={to}
              target="_blank"
              rel="noreferrer noopener"
              title={`在新窗口打开${label}`}
              className={cn(NAV_ITEM_MOBILE_CLASS, NAV_ITEM_IDLE)}
            >
              <Icon aria-hidden className="h-4 w-4" />
              {label}
            </a>
          ) : (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                cn(NAV_ITEM_MOBILE_CLASS, isActive ? NAV_ITEM_ACTIVE : NAV_ITEM_IDLE)
              }
            >
              <Icon aria-hidden className="h-4 w-4" />
              {label}
            </NavLink>
          ),
        )}
      </nav>
    </header>
  );
}
