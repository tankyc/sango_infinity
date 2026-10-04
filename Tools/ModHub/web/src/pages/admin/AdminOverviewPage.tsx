import { useEffect, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import {
  CheckCircle2,
  Clock,
  Database,
  Download,
  Eye,
  HardDrive,
  Heart,
  MessageSquare,
  Package,
  ShieldAlert,
  UserPlus,
  Users,
} from 'lucide-react';
import { ApiRequestError, fetchAdminStats, type AdminStats } from '../../lib/api';
import { MOD_STATUSES, categoryLabel, roleLabel, roleVariant } from '../../lib/catalog';
import { formatBytes, formatCount, formatDate, formatRelative } from '../../lib/format';
import { Badge, SectionTitle } from '../../components/ui/Badge';
import { ErrorState, LoadingBlock } from '../../components/ui/Feedback';
import { cn } from '../../lib/cn';

/**
 * 后台概览 /admin
 *
 * 只回答三个问题：现在有多少人、多少内容、有多少活儿等着干。
 * 「待审核」永远显示在最显眼的位置——这是管理员每天进来唯一必须处理的事。
 */
export default function AdminOverviewPage() {
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ message: string; details: string[] } | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    fetchAdminStats(controller.signal)
      .then(setStats)
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setError(
          e instanceof ApiRequestError
            ? { message: e.message, details: e.details }
            : { message: '载入失败，请稍后重试', details: [] },
        );
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [reloadToken]);

  if (loading) return <LoadingBlock text="正在载入概览…" />;
  if (error) return <ErrorState message={error.message} details={error.details} onRetry={() => setReloadToken((v) => v + 1)} />;
  if (!stats) return null;

  return (
    <div className="space-y-5">
      <header>
        <h1 className="font-serif text-2xl text-paper-100">概览</h1>
        <p className="mt-1.5 text-sm text-paper-500">账号与内容的总览，所有管理操作都会立即生效。</p>
      </header>

      {/* 第一行：待办优先 */}
      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          icon={<Clock aria-hidden className="h-4 w-4" />}
          label="待审核模组"
          value={formatCount(stats.mods.pending)}
          hint={stats.mods.pending > 0 ? '需要处理' : '暂无待办'}
          tone={stats.mods.pending > 0 ? 'warn' : 'normal'}
        />
        <StatCard
          icon={<Users aria-hidden className="h-4 w-4" />}
          label="用户总数"
          value={formatCount(stats.users.total)}
          hint={`近 7 天新增 ${formatCount(stats.users.recent7d)} · 管理员 ${stats.users.admins}`}
        />
        <StatCard
          icon={<Package aria-hidden className="h-4 w-4" />}
          label="模组总数"
          value={formatCount(stats.mods.total)}
          hint={`已上架 ${formatCount(stats.mods.approved)} · 已下架 ${formatCount(stats.mods.hidden)}`}
        />
        <StatCard
          icon={<Download aria-hidden className="h-4 w-4" />}
          label="累计下载"
          value={formatCount(stats.engagement.downloads)}
          hint={`订阅关系 ${formatCount(stats.engagement.subscriptions)}`}
        />
      </section>

      <div className="grid gap-5 lg:grid-cols-2">
        {/* 待审核清单 */}
        <section className="panel p-5">
          <SectionTitle
            action={
              <Link
                to="/admin/mods?status=pending"
                className="text-xs text-gold-400 transition-colors duration-200 hover:text-gold-300"
              >
                去处理 →
              </Link>
            }
          >
            最新待审核
          </SectionTitle>

          {stats.recent.pendingMods.length === 0 ? (
            <p className="flex items-center gap-2 py-6 text-sm text-paper-500">
              <CheckCircle2 aria-hidden className="h-4 w-4 text-bamboo-400" />
              没有待审核的模组，队列是空的。
            </p>
          ) : (
            <ul className="divide-y divide-ink-700/70">
              {stats.recent.pendingMods.map((m) => (
                <li key={m.id} className="flex items-center gap-3 py-2.5">
                  <div className="min-w-0 flex-1">
                    <Link
                      to={`/sharedfiles/filedetails/?id=${encodeURIComponent(m.id)}`}
                      className="block truncate text-sm text-paper-200 transition-colors duration-200 hover:text-gold-300"
                    >
                      {m.name}
                    </Link>
                    <p className="mt-0.5 truncate text-xs text-paper-500">
                      {m.author.nickname ?? m.author.username} · {categoryLabel(m.category)} · {formatRelative(m.createdAt)}
                    </p>
                  </div>
                  <Badge variant="gold">{MOD_STATUSES[0].label}</Badge>
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* 最新注册用户 */}
        <section className="panel p-5">
          <SectionTitle
            action={
              <Link
                to="/admin/users"
                className="text-xs text-gold-400 transition-colors duration-200 hover:text-gold-300"
              >
                账号管理 →
              </Link>
            }
          >
            最新注册用户
          </SectionTitle>

          {stats.recent.users.length === 0 ? (
            <p className="py-6 text-sm text-paper-500">还没有用户。</p>
          ) : (
            <ul className="divide-y divide-ink-700/70">
              {stats.recent.users.map((u) => (
                <li key={u.id} className="flex items-center gap-3 py-2.5">
                  <UserPlus aria-hidden className="h-4 w-4 shrink-0 text-paper-600" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm text-paper-200">{u.nickname ?? u.username}</p>
                    <p className="mt-0.5 truncate font-mono text-xs text-paper-500">
                      {u.username} · {formatDate(u.createdAt)}
                    </p>
                  </div>
                  <Badge variant={roleVariant(u.role)}>{roleLabel(u.role)}</Badge>
                  {u.disabled ? <Badge variant="cinnabar">已封禁</Badge> : null}
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      {/* 运营数据 */}
      <section className="panel p-5">
        <SectionTitle>运营数据</SectionTitle>
        <div className="grid gap-x-6 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">
          <Metric icon={<Eye aria-hidden className="h-4 w-4" />} label="模组浏览" value={formatCount(stats.engagement.views)} />
          <Metric
            icon={<Users aria-hidden className="h-4 w-4" />}
            label="订阅者合计"
            value={formatCount(stats.engagement.subscribers)}
          />
          <Metric icon={<Heart aria-hidden className="h-4 w-4" />} label="好评" value={formatCount(stats.engagement.likes)} />
          <Metric
            icon={<MessageSquare aria-hidden className="h-4 w-4" />}
            label="评论"
            value={formatCount(stats.engagement.comments)}
          />
          <Metric
            icon={<HardDrive aria-hidden className="h-4 w-4" />}
            label="内容体积"
            value={formatBytes(stats.mods.storageBytes)}
          />
          <Metric
            icon={<Database aria-hidden className="h-4 w-4" />}
            label="版本总数"
            value={formatCount(stats.mods.versions)}
          />
        </div>
        {stats.users.disabled > 0 ? (
          <p className="mt-4 flex items-center gap-2 text-xs text-cinnabar-400">
            <ShieldAlert aria-hidden className="h-3.5 w-3.5" />
            当前有 {stats.users.disabled} 个账号处于封禁状态
          </p>
        ) : null}
      </section>
    </div>
  );
}

function StatCard({
  icon,
  label,
  value,
  hint,
  tone = 'normal',
}: {
  icon: ReactNode;
  label: string;
  value: string;
  hint?: string;
  tone?: 'normal' | 'warn';
}) {
  return (
    <div className={cn('panel p-4', tone === 'warn' && 'border-gold-600/60')}>
      <div className="flex items-center gap-2 text-xs text-paper-500">
        <span className={cn(tone === 'warn' ? 'text-gold-400' : 'text-paper-500')}>{icon}</span>
        {label}
      </div>
      <p className="mt-2 font-mono text-2xl text-paper-100">{value}</p>
      {hint ? <p className="mt-1 text-xs text-paper-500">{hint}</p> : null}
    </div>
  );
}

function Metric({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <div className="flex items-center gap-3 border-b border-ink-700/50 py-1.5">
      <span className="text-paper-600">{icon}</span>
      <span className="flex-1 text-sm text-paper-400">{label}</span>
      <span className="font-mono text-sm text-paper-200">{value}</span>
    </div>
  );
}
