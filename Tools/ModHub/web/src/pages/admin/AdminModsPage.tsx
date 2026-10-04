import { useCallback, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ArrowUpCircle, Ban, Check, RotateCcw, Search, Trash2, X } from 'lucide-react';
import {
  ApiRequestError,
  deleteAdminMod,
  fetchAdminMods,
  updateAdminMod,
  type AdminModRow,
  type Paged,
} from '../../lib/api';
import { CATEGORIES, MOD_STATUSES, categoryLabel, modStatusMeta, roleVariant } from '../../lib/catalog';
import { formatBytes, formatCount, formatDate } from '../../lib/format';
import { Pagination } from '../../components/admin/Pagination';
import { PosterImage } from '../../components/browse/PosterImage';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { ConfirmDialog } from '../../components/ui/ConfirmDialog';
import { ErrorState, Skeleton } from '../../components/ui/Feedback';
import { Field, Input, Select } from '../../components/ui/Field';
import { cn } from '../../lib/cn';

/**
 * 内容管理 /admin/mods
 *
 * 这是管理员的日常主战场：审核（待审核 → 上架/拒绝）、下架违规内容、必要时彻底删除。
 * 删除是唯一不可逆的操作，因此单独走「危险确认」，且默认先做下架。
 *
 * 支持从概览页/账号页带参跳入：/admin/mods?status=pending、/admin/mods?author=xxx
 */

const PAGE_SIZE = 20;

type SortKey = 'updated' | 'created' | 'downloads' | 'views' | 'subscribers';

/**
 * 模组行操作：桌面表格与移动卡片共用
 * 按当前状态只给「当下真正可做」的动作，避免出现两个按钮干同一件事
 * （例如已下架时既显示「上架」又显示「恢复」）。
 */
function ModRowActions({
  mod,
  busy,
  layout,
  onSetStatus,
  onDelete,
}: {
  mod: AdminModRow;
  busy: boolean;
  layout: 'table' | 'card';
  onSetStatus: (status: string, okText: string) => void;
  onDelete: () => void;
}) {
  return (
    <div className={cn('flex items-center gap-1.5', layout === 'table' ? 'justify-end' : 'flex-wrap justify-start')}>
      {mod.status === 'pending' ? (
        <>
          <Button
            variant="gold"
            size="sm"
            disabled={busy}
            title="审核通过并上架"
            onClick={() => onSetStatus('approved', `「${mod.name}」已上架`)}
            icon={<Check aria-hidden className="h-3.5 w-3.5" />}
          >
            通过
          </Button>
          <Button
            variant="cinnabar"
            size="sm"
            disabled={busy}
            title="拒绝该投稿"
            onClick={() => onSetStatus('rejected', `「${mod.name}」已拒绝`)}
            icon={<X aria-hidden className="h-3.5 w-3.5" />}
          >
            拒绝
          </Button>
        </>
      ) : null}

      {mod.status === 'approved' ? (
        <Button
          variant="outline"
          size="sm"
          disabled={busy}
          title="下架（可再上架）"
          onClick={() => onSetStatus('hidden', `「${mod.name}」已下架`)}
          icon={<Ban aria-hidden className="h-3.5 w-3.5" />}
        >
          下架
        </Button>
      ) : null}

      {mod.status === 'hidden' || mod.status === 'rejected' ? (
        <Button
          variant="gold"
          size="sm"
          disabled={busy}
          title="恢复到已上架"
          onClick={() => onSetStatus('approved', `「${mod.name}」已恢复上架`)}
          icon={<ArrowUpCircle aria-hidden className="h-3.5 w-3.5" />}
        >
          上架
        </Button>
      ) : null}

      <Button
        variant="ghost"
        size="sm"
        disabled={busy}
        title="彻底删除（含文件）"
        onClick={onDelete}
        icon={<Trash2 aria-hidden className="h-3.5 w-3.5" />}
      >
        删除
      </Button>
    </div>
  );
}

export default function AdminModsPage() {
  const [searchParams] = useSearchParams();

  const [keywordInput, setKeywordInput] = useState(searchParams.get('q') ?? '');
  const [q, setQ] = useState(searchParams.get('q') ?? '');
  const [status, setStatus] = useState(searchParams.get('status') ?? '');
  const [category, setCategory] = useState(searchParams.get('category') ?? '');
  const [author, setAuthor] = useState(searchParams.get('author') ?? '');
  const [sort, setSort] = useState<SortKey>('updated');
  const [page, setPage] = useState(1);

  const [data, setData] = useState<Paged<AdminModRow> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ message: string; details: string[] } | null>(null);
  const [notice, setNotice] = useState<{ tone: 'ok' | 'err'; text: string } | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<AdminModRow | null>(null);
  const [dialogLoading, setDialogLoading] = useState(false);

  const load = useCallback(
    (signal?: AbortSignal) => {
      setLoading(true);
      setError(null);
      return fetchAdminMods(
        {
          q: q === '' ? undefined : q,
          status: status === '' ? undefined : status,
          category: category === '' ? undefined : category,
          author: author === '' ? undefined : author,
          sort,
          page,
          pageSize: PAGE_SIZE,
        },
        signal,
      )
        .then((res) => setData(res))
        .catch((e: unknown) => {
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
    },
    [q, status, category, author, sort, page],
  );

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  async function runAction(id: string, action: () => Promise<unknown>, okText: string) {
    setBusyId(id);
    setNotice(null);
    try {
      await action();
      setNotice({ tone: 'ok', text: okText });
      await load();
    } catch (e) {
      setNotice({ tone: 'err', text: e instanceof ApiRequestError ? e.message : '操作失败，请稍后重试' });
    } finally {
      setBusyId(null);
    }
  }

  /** 审核/上下架统一入口（后端会按状态变化失效市场清单缓存） */
  function changeStatus(mod: AdminModRow, next: string, okText: string) {
    return runAction(mod.id, () => updateAdminMod(mod.id, { status: next }), okText);
  }

  async function submitDelete() {
    if (!deleteTarget) return;
    setDialogLoading(true);
    try {
      await deleteAdminMod(deleteTarget.id, true);
      setNotice({ tone: 'ok', text: `已彻底删除「${deleteTarget.name}」及其全部版本文件` });
      setDeleteTarget(null);
      await load();
    } catch (e) {
      setNotice({ tone: 'err', text: e instanceof ApiRequestError ? e.message : '删除失败' });
    } finally {
      setDialogLoading(false);
    }
  }

  const items = data?.items ?? [];

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="font-serif text-2xl text-paper-100">内容管理</h1>
          <p className="mt-1.5 text-sm text-paper-500">
            审核与上下架社区模组。只有「已上架」的模组会出现在客户端市场清单里。
          </p>
        </div>
        <Button variant="ghost" size="sm" icon={<RotateCcw aria-hidden className="h-3.5 w-3.5" />} onClick={() => void load()}>
          刷新
        </Button>
      </header>

      <section className="panel p-4">
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            setPage(1);
            setQ(keywordInput.trim());
          }}
        >
          <Field label="搜索" htmlFor="mod-q" className="min-w-[200px] flex-1">
            <div className="relative">
              <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-paper-500" />
              <Input
                id="mod-q"
                value={keywordInput}
                onChange={(e) => setKeywordInput(e.target.value)}
                placeholder="模组名称或 id"
                className="pl-9"
              />
            </div>
          </Field>

          <Field label="状态" htmlFor="mod-status" className="w-32">
            <Select
              id="mod-status"
              value={status}
              onChange={(e) => {
                setPage(1);
                setStatus(e.target.value);
              }}
            >
              <option value="">全部状态</option>
              {MOD_STATUSES.map((s) => (
                <option key={s.key} value={s.key}>
                  {s.label}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="分类" htmlFor="mod-category" className="w-28">
            <Select
              id="mod-category"
              value={category}
              onChange={(e) => {
                setPage(1);
                setCategory(e.target.value);
              }}
            >
              <option value="">全部分类</option>
              {CATEGORIES.map((c) => (
                <option key={c.key} value={c.key}>
                  {c.label}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="作者" htmlFor="mod-author" className="w-36">
            <Input
              id="mod-author"
              value={author}
              onChange={(e) => {
                setPage(1);
                setAuthor(e.target.value.trim());
              }}
              placeholder="作者用户名"
            />
          </Field>

          <Field label="排序" htmlFor="mod-sort" className="w-32">
            <Select
              id="mod-sort"
              value={sort}
              onChange={(e) => {
                setPage(1);
                setSort(e.target.value as SortKey);
              }}
            >
              <option value="updated">最近更新</option>
              <option value="created">发布时间</option>
              <option value="downloads">下载量</option>
              <option value="subscribers">订阅数</option>
              <option value="views">浏览量</option>
            </Select>
          </Field>

          <Button type="submit" variant="outline">
            查询
          </Button>
        </form>
      </section>

      {notice ? (
        <p
          className={cn(
            'rounded-md border px-3 py-2 text-sm',
            notice.tone === 'ok'
              ? 'border-bamboo-400/50 bg-bamboo-500/10 text-bamboo-400'
              : 'border-cinnabar-500/50 bg-cinnabar-500/10 text-cinnabar-400',
          )}
        >
          {notice.text}
        </p>
      ) : null}

      {error ? (
        <ErrorState message={error.message} details={error.details} onRetry={() => void load()} />
      ) : (
        <section className="panel overflow-hidden">
          {/* 桌面端：表格（操作列吸右，横向滚动时也始终可见） */}
          <div className="hidden overflow-x-auto md:block">
            <table className="w-full min-w-[1100px] text-sm">
              <thead className="bg-ink-850/80 text-xs text-paper-500">
                <tr>
                  <th className="px-3 py-2.5 text-left font-normal">模组</th>
                  <th className="px-3 py-2.5 text-left font-normal">作者</th>
                  <th className="px-3 py-2.5 text-left font-normal">分类</th>
                  <th className="px-3 py-2.5 text-left font-normal">状态</th>
                  <th className="px-3 py-2.5 text-left font-normal">版本</th>
                  <th className="px-3 py-2.5 text-right font-normal">大小</th>
                  <th className="px-3 py-2.5 text-right font-normal">下载</th>
                  <th className="px-3 py-2.5 text-right font-normal">订阅</th>
                  <th className="px-3 py-2.5 text-left font-normal">更新时间</th>
                  <th className="sticky right-0 z-10 border-l border-ink-600/70 bg-ink-850 px-3 py-2.5 text-right font-normal">
                    操作
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-ink-700/70">
                {loading && items.length === 0
                  ? Array.from({ length: 5 }).map((_, i) => (
                      <tr key={i}>
                        <td colSpan={10} className="px-3 py-3">
                          <Skeleton className="h-5 w-full" />
                        </td>
                      </tr>
                    ))
                  : items.map((m) => {
                      const busy = busyId === m.id;
                      const meta = modStatusMeta(m.status);
                      return (
                        <tr key={m.id} className={cn('group transition-colors duration-200 hover:bg-ink-700/30', busy && 'opacity-60')}>
                          <td className="px-3 py-2.5">
                            <div className="flex items-center gap-3">
                              <PosterImage
                                src={m.posterUrl}
                                alt={`${m.name} 的封面`}
                                category={m.category}
                                className="h-10 w-[71px] shrink-0 rounded border border-ink-600/70"
                              />
                              <div className="min-w-0">
                                <Link
                                  to={`/sharedfiles/filedetails/?id=${encodeURIComponent(m.id)}`}
                                  className="block max-w-[260px] truncate text-paper-200 transition-colors duration-200 hover:text-gold-300"
                                >
                                  {m.name}
                                </Link>
                                <p className="mt-0.5 truncate font-mono text-xs text-paper-500">{m.id}</p>
                              </div>
                            </div>
                          </td>
                          <td className="px-3 py-2.5">
                            <Link
                              to={`/admin/users?q=${encodeURIComponent(m.author.username)}`}
                              className="text-xs text-paper-300 transition-colors duration-200 hover:text-gold-300"
                            >
                              {m.author.nickname ?? m.author.username}
                            </Link>
                            {m.author.disabled ? (
                              <div className="mt-1">
                                <Badge variant={roleVariant('user')}>作者已封禁</Badge>
                              </div>
                            ) : null}
                          </td>
                          <td className="px-3 py-2.5 text-xs text-paper-400">{categoryLabel(m.category)}</td>
                          <td className="px-3 py-2.5">
                            <Badge variant={meta.variant}>{meta.label}</Badge>
                          </td>
                          <td className="px-3 py-2.5 font-mono text-xs text-paper-300">
                            {m.version ?? '—'}
                            {m.versionCount > 1 ? <span className="text-paper-600"> (+{m.versionCount - 1})</span> : null}
                          </td>
                          <td className="px-3 py-2.5 text-right font-mono text-xs text-paper-400">{formatBytes(m.size)}</td>
                          <td className="px-3 py-2.5 text-right font-mono text-xs text-paper-400">
                            {formatCount(m.stats.downloads)}
                          </td>
                          <td className="px-3 py-2.5 text-right font-mono text-xs text-paper-400">
                            {formatCount(m.stats.subscribers)}
                          </td>
                          <td className="px-3 py-2.5 text-xs text-paper-500">{formatDate(m.updatedAt)}</td>
                          <td className="sticky right-0 z-10 border-l border-ink-600/70 bg-ink-800 px-3 py-2.5 transition-colors duration-200 group-hover:bg-ink-700">
                            <ModRowActions
                              mod={m}
                              busy={busy}
                              layout="table"
                              onSetStatus={(status, okText) => void changeStatus(m, status, okText)}
                              onDelete={() => setDeleteTarget(m)}
                            />
                          </td>
                        </tr>
                      );
                    })}
              </tbody>
            </table>
          </div>

          {/* 移动端：卡片式。表格在窄屏需要横向滚动，操作列会被推出屏幕，因此这里换成卡片、按钮完整铺开 */}
          <div className="space-y-3 p-3 md:hidden">
            {loading && items.length === 0
              ? Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-40 w-full rounded-xl" />)
              : items.map((m) => {
                  const busy = busyId === m.id;
                  const meta = modStatusMeta(m.status);
                  return (
                    <div
                      key={m.id}
                      className={cn('rounded-xl border border-gold-900/60 bg-ink-850/60 p-4', busy && 'opacity-60')}
                    >
                      <div className="flex items-start gap-3">
                        <PosterImage
                          src={m.posterUrl}
                          alt={`${m.name} 的封面`}
                          category={m.category}
                          className="h-12 w-[85px] shrink-0 rounded border border-ink-600/70"
                        />
                        <div className="min-w-0 flex-1">
                          <Link
                            to={`/sharedfiles/filedetails/?id=${encodeURIComponent(m.id)}`}
                            className="block truncate text-paper-200 transition-colors duration-200 hover:text-gold-300"
                          >
                            {m.name}
                          </Link>
                          <p className="mt-0.5 truncate font-mono text-xs text-paper-500">{m.id}</p>
                          <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                            <Badge variant={meta.variant}>{meta.label}</Badge>
                            <Badge variant="outline">{categoryLabel(m.category)}</Badge>
                            {m.author.disabled ? <Badge variant="cinnabar">作者已封禁</Badge> : null}
                          </div>
                        </div>
                      </div>

                      <dl className="mt-3 grid grid-cols-3 gap-y-2 text-xs">
                        <div>
                          <dt className="text-paper-500">版本</dt>
                          <dd className="mt-0.5 font-mono text-paper-300">
                            {m.version ?? '—'}
                            {m.versionCount > 1 ? ` (+${m.versionCount - 1})` : ''}
                          </dd>
                        </div>
                        <div>
                          <dt className="text-paper-500">大小</dt>
                          <dd className="mt-0.5 font-mono text-paper-300">{formatBytes(m.size)}</dd>
                        </div>
                        <div>
                          <dt className="text-paper-500">下载</dt>
                          <dd className="mt-0.5 font-mono text-paper-300">{formatCount(m.stats.downloads)}</dd>
                        </div>
                        <div className="col-span-3">
                          <dt className="text-paper-500">作者 / 更新时间</dt>
                          <dd className="mt-0.5 text-paper-400">
                            <Link
                              to={`/admin/users?q=${encodeURIComponent(m.author.username)}`}
                              className="transition-colors duration-200 hover:text-gold-300"
                            >
                              {m.author.nickname ?? m.author.username}
                            </Link>
                            <span className="ml-2 font-mono">{formatDate(m.updatedAt)}</span>
                          </dd>
                        </div>
                      </dl>

                      <div className="mt-3 border-t border-ink-700/70 pt-3">
                        <ModRowActions
                          mod={m}
                          busy={busy}
                          layout="card"
                          onSetStatus={(status, okText) => void changeStatus(m, status, okText)}
                          onDelete={() => setDeleteTarget(m)}
                        />
                      </div>
                    </div>
                  );
                })}
          </div>

          {!loading && items.length === 0 ? (
            <p className="px-3 py-10 text-center text-sm text-paper-500">没有符合条件的模组。</p>
          ) : null}

          <div className="px-3 pb-3">
            <Pagination page={data?.page ?? 1} pageSize={PAGE_SIZE} total={data?.total ?? 0} onChange={setPage} />
          </div>
        </section>
      )}

      <ConfirmDialog
        open={deleteTarget !== null}
        title={`彻底删除「${deleteTarget?.name ?? ''}」`}
        description={`将删除该模组的全部 ${deleteTarget?.versionCount ?? 0} 个版本记录、订阅关系、评论，以及磁盘上的 zip 包与封面。此操作不可恢复，如需临时隐藏请改用「下架」。`}
        danger
        confirmText="确认删除"
        loading={dialogLoading}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => void submitDelete()}
      />
    </div>
  );
}
