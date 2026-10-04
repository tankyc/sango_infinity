import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import {
  ArrowUpCircle,
  Ban,
  ExternalLink,
  FolderKanban,
  Package,
  Pencil,
  RefreshCw,
  Rocket,
  Upload,
} from 'lucide-react';
import {
  ApiRequestError,
  fetchMyMods,
  hideMyMod,
  updateMyMod,
  type MyModRow,
  type MyWorkshopResponse,
} from '../lib/api';
import { categoryLabel, modStatusMeta } from '../lib/catalog';
import { formatBytes, formatCount, formatRelative } from '../lib/format';
import { stripMarkdown } from '../lib/miniMarkdown';
import { useAuth } from '../hooks/useAuth';
import { PosterImage } from '../components/browse/PosterImage';
import { modDetailPath } from '../components/browse/ModCard';
import { EditModDialog } from '../components/my/EditModDialog';
import { PublishVersionDialog } from '../components/my/PublishVersionDialog';
import { Badge, SectionTitle } from '../components/ui/Badge';
import { Button, buttonClass } from '../components/ui/Button';
import { ErrorState, LoadingBlock, Skeleton } from '../components/ui/Feedback';
import { cn } from '../lib/cn';

/**
 * 我的创作 /my（对应 建设计划.md §4.4 你的创意工坊）
 *
 * 作者在这里能看到自己发布的全部内容（含待审核与已下架）与累计数据，并做三件事：
 *   改信息（元数据） / 发新版本（内容变更的唯一途径） / 上架下架
 * 物理删除刻意不给作者：内容一旦有人订阅，删除是不可逆的社区资产损失，
 * 需要彻底清除时由管理员在后台执行。
 */
export default function MyWorkshopPage() {
  const { user, initializing } = useAuth();
  const location = useLocation();

  const [data, setData] = useState<MyWorkshopResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ message: string; details: string[] } | null>(null);
  const [notice, setNotice] = useState<{ tone: 'ok' | 'err'; text: string } | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [editTarget, setEditTarget] = useState<MyModRow | null>(null);
  const [versionTarget, setVersionTarget] = useState<MyModRow | null>(null);

  const load = useCallback((signal?: AbortSignal) => {
    setLoading(true);
    setError(null);
    return fetchMyMods(signal)
      .then((res) => setData(res))
      .catch((e: unknown) => {
        // 请求被中止（组件卸载 / StrictMode 重挂载）不是错误
        if (signal?.aborted) return;
        setError(
          e instanceof ApiRequestError
            ? { message: e.message, details: e.details }
            : { message: '载入失败，请稍后重试', details: [] },
        );
      })
      .finally(() => {
        if (!signal?.aborted) setLoading(false);
      });
  }, []);

  useEffect(() => {
    if (initializing || !user) {
      setLoading(false);
      return;
    }
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [initializing, user, load]);

  async function toggleHidden(mod: MyModRow) {
    setBusyId(mod.id);
    setNotice(null);
    try {
      if (mod.status === 'hidden') {
        await updateMyMod(mod.id, { status: 'approved' });
        setNotice({ tone: 'ok', text: `「${mod.name}」已重新上架` });
      } else {
        await hideMyMod(mod.id);
        setNotice({ tone: 'ok', text: `「${mod.name}」已下架，随时可以重新上架` });
      }
      await load();
    } catch (e) {
      setNotice({ tone: 'err', text: e instanceof ApiRequestError ? e.message : '操作失败，请稍后重试' });
    } finally {
      setBusyId(null);
    }
  }

  if (initializing) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-16">
        <LoadingBlock text="正在确认登录状态…" />
      </div>
    );
  }

  if (!user) {
    const next = encodeURIComponent(location.pathname);
    return (
      <div className="mx-auto max-w-lg px-4 py-20 text-center">
        <FolderKanban aria-hidden className="mx-auto h-8 w-8 text-gold-500" />
        <h1 className="mt-3 font-serif text-xl text-paper-100">我的创作需要先登录</h1>
        <p className="mt-2 text-sm text-paper-500">登录后即可管理你发布的模组、修改信息并发布新版本。</p>
        <div className="mt-5 flex flex-wrap justify-center gap-2">
          <Link to={`/login?next=${next}`} className={buttonClass('gold', 'md')}>
            去登录
          </Link>
          <Link to="/browse" className={buttonClass('outline', 'md')}>
            先去逛逛
          </Link>
        </div>
      </div>
    );
  }

  const items = data?.items ?? [];
  const summary = data?.summary;

  return (
    <div className="mx-auto max-w-[1200px] px-4 py-6 sm:px-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="font-serif text-2xl text-paper-100">我的创作</h1>
          <p className="mt-1.5 text-sm text-paper-500">
            修改已发布模组的信息，或以「发布新版本」的方式更新内容——只有后者会让订阅者收到更新提示。
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            icon={<RefreshCw aria-hidden className="h-3.5 w-3.5" />}
            onClick={() => void load()}
          >
            刷新
          </Button>
          <Link to="/upload" className={buttonClass('gold', 'sm')}>
            <Upload aria-hidden className="h-3.5 w-3.5" />
            发布新模组
          </Link>
        </div>
      </header>

      {notice ? (
        <p
          className={cn(
            'mt-4 rounded-md border px-3 py-2 text-sm',
            notice.tone === 'ok'
              ? 'border-bamboo-400/50 bg-bamboo-500/10 text-bamboo-400'
              : 'border-cinnabar-500/50 bg-cinnabar-500/10 text-cinnabar-400',
          )}
        >
          {notice.text}
        </p>
      ) : null}

      {error ? (
        <div className="mt-4">
          <ErrorState message={error.message} details={error.details} onRetry={() => void load()} />
        </div>
      ) : loading ? (
        <div className="mt-5 space-y-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-28 w-full rounded-xl" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="mt-6 flex flex-col items-center gap-3 rounded-xl border border-dashed border-ink-500/70 px-6 py-16 text-center">
          <Package aria-hidden className="h-8 w-8 text-paper-600" />
          <p className="font-serif text-lg text-paper-200">你还没有发布过模组</p>
          <p className="max-w-md text-sm text-paper-500">
            把游戏里编辑好的武将、剧本打包成 zip 上传，就能被其他玩家浏览与订阅。
          </p>
          <Link to="/upload" className={buttonClass('gold', 'md', 'mt-1')}>
            <Upload aria-hidden className="h-4 w-4" />
            去发布第一个模组
          </Link>
        </div>
      ) : (
        <>
          {/* 汇总 */}
          {summary ? (
            <section className="panel mt-5 p-4">
              <SectionTitle>累计数据</SectionTitle>
              <div className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm sm:grid-cols-3 lg:grid-cols-5">
                <Stat label="已发布" value={`${summary.approved} / ${summary.total}`} hint="已上架 / 全部" />
                <Stat label="待审核" value={String(summary.pending)} />
                <Stat label="已下架" value={String(summary.hidden)} />
                <Stat label="累计下载" value={formatCount(summary.downloads)} />
                <Stat
                  label="订阅 / 浏览"
                  value={`${formatCount(summary.subscribers)} / ${formatCount(summary.views)}`}
                />
              </div>
              {summary.rejected > 0 ? (
                <p className="mt-3 text-xs text-cinnabar-400">
                  有 {summary.rejected} 个模组被管理员拒绝上架，可编辑后重新上架或联系管理员了解原因。
                </p>
              ) : null}
            </section>
          ) : null}

          {/* 我发布的模组 */}
          <ul className="mt-5 space-y-3">
            {items.map((mod) => {
              const meta = modStatusMeta(mod.status);
              const busy = busyId === mod.id;
              return (
                <li
                  key={mod.id}
                  className={cn('panel p-4 transition-opacity duration-200', busy && 'opacity-60')}
                >
                  <div className="flex flex-col gap-4 sm:flex-row">
                    <PosterImage
                      src={mod.posterUrl}
                      alt={`${mod.name} 的封面`}
                      category={mod.category}
                      className="aspect-video w-full shrink-0 rounded-lg border border-ink-600/70 sm:w-44"
                    />

                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <Link
                          to={modDetailPath(mod.id)}
                          className="font-serif text-base text-paper-100 transition-colors duration-200 hover:text-gold-300"
                        >
                          {mod.name}
                        </Link>
                        <Badge variant={meta.variant}>{meta.label}</Badge>
                        <Badge variant="outline">{categoryLabel(mod.category)}</Badge>
                      </div>

                      <p className="clamp-2 mt-1 text-xs leading-relaxed text-paper-500">
                        {stripMarkdown(mod.summary) || '未填写简介'}
                      </p>

                      <dl className="mt-2.5 grid grid-cols-2 gap-x-5 gap-y-1.5 text-xs sm:grid-cols-4">
                        <Row label="最新版本">
                          <span className="font-mono text-paper-300">{mod.version ?? '—'}</span>
                          {mod.versionCount > 1 ? (
                            <span className="text-paper-600">（共 {mod.versionCount} 个版本）</span>
                          ) : null}
                        </Row>
                        <Row label="包大小">
                          <span className="font-mono text-paper-300">{formatBytes(mod.size)}</span>
                        </Row>
                        <Row label="下载">
                          <span className="font-mono text-paper-300">{formatCount(mod.stats.downloads)}</span>
                        </Row>
                        <Row label="订阅 / 浏览">
                          <span className="font-mono text-paper-300">
                            {formatCount(mod.stats.subscribers)} / {formatCount(mod.stats.views)}
                          </span>
                        </Row>
                      </dl>

                      <p className="mt-2 text-[11px] text-paper-600">
                        更新于 {formatRelative(mod.updatedAt)} · id: <span className="font-mono">{mod.id}</span>
                      </p>
                    </div>

                    {/* 操作区：移动端铺开，桌面端竖排贴右 */}
                    <div className="flex flex-wrap items-center gap-2 sm:w-32 sm:flex-col sm:items-stretch">
                      <Button
                        variant="gold"
                        size="sm"
                        disabled={busy}
                        onClick={() => setVersionTarget(mod)}
                        icon={<Rocket aria-hidden className="h-3.5 w-3.5" />}
                      >
                        发布新版本
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={busy}
                        onClick={() => setEditTarget(mod)}
                        icon={<Pencil aria-hidden className="h-3.5 w-3.5" />}
                      >
                        编辑信息
                      </Button>
                      <Button
                        variant={mod.status === 'hidden' ? 'outline' : 'ghost'}
                        size="sm"
                        disabled={busy}
                        onClick={() => void toggleHidden(mod)}
                        icon={
                          mod.status === 'hidden' ? (
                            <ArrowUpCircle aria-hidden className="h-3.5 w-3.5" />
                          ) : (
                            <Ban aria-hidden className="h-3.5 w-3.5" />
                          )
                        }
                      >
                        {mod.status === 'hidden' ? '重新上架' : '下架'}
                      </Button>
                      <Link to={modDetailPath(mod.id)} className={buttonClass('ghost', 'sm')}>
                        <ExternalLink aria-hidden className="h-3.5 w-3.5" />
                        查看详情
                      </Link>
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>

          <p className="mt-4 text-xs text-paper-600">
            需要彻底删除某个模组（连同文件）时，请联系管理员在后台处理——作者侧只提供下架。
          </p>
        </>
      )}

      <EditModDialog
        mod={editTarget}
        onClose={() => setEditTarget(null)}
        onSaved={(msg) => {
          setNotice({ tone: 'ok', text: msg });
          void load();
        }}
      />

      <PublishVersionDialog
        mod={versionTarget}
        onClose={() => setVersionTarget(null)}
        onPublished={(msg) => {
          setNotice({ tone: 'ok', text: msg });
          void load();
        }}
      />
    </div>
  );
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div>
      <p className="text-xs text-paper-500">{label}</p>
      <p className="mt-0.5 font-mono text-base text-paper-100">{value}</p>
      {hint ? <p className="text-[11px] text-paper-600">{hint}</p> : null}
    </div>
  );
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-center gap-1.5">
      <dt className="text-paper-500">{label}</dt>
      <dd className="text-paper-300">{children}</dd>
    </div>
  );
}
