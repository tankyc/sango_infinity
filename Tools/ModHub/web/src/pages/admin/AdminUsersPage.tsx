import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Ban, KeyRound, RotateCcw, Search, ShieldCheck, Trash2 } from 'lucide-react';
import {
  ApiRequestError,
  deleteAdminUser,
  fetchAdminUsers,
  updateAdminUser,
  type AdminUserRow,
  type Paged,
} from '../../lib/api';
import { ROLES, roleLabel, roleVariant } from '../../lib/catalog';
import { formatDate, formatRelative } from '../../lib/format';
import { useAuth } from '../../hooks/useAuth';
import { Pagination } from '../../components/admin/Pagination';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { ConfirmDialog } from '../../components/ui/ConfirmDialog';
import { ErrorState, Skeleton } from '../../components/ui/Feedback';
import { Field, Input, Select } from '../../components/ui/Field';
import { cn } from '../../lib/cn';

/**
 * 账号管理 /admin/users
 *
 * 管理员在这里能做的事：改角色、封禁/解封、重置密码、删除账号。
 * 全部护栏都在后端（不能改自己、不能封自己、不能删自己、不能把最后一个管理员降级或删除），
 * 前端同时把「针对自己的操作」置灰，让规则在界面上就能被看见。
 */

const PAGE_SIZE = 20;

type SortKey = 'created' | 'lastLogin' | 'username' | 'mods';

/**
 * 账号行操作按钮组
 * 桌面表格与移动卡片共用同一套按钮与禁用规则，避免「表格里能点、卡片里点不动」这类走偏。
 * layout=table 时右对齐（表格末列），layout=card 时换行左对齐（窄屏卡片底部）。
 */
function UserRowActions({
  user,
  isMe,
  busy,
  layout,
  onResetPassword,
  onToggleDisabled,
  onDelete,
}: {
  user: AdminUserRow;
  isMe: boolean;
  busy: boolean;
  layout: 'table' | 'card';
  onResetPassword: () => void;
  onToggleDisabled: () => void;
  onDelete: () => void;
}) {
  return (
    <div className={cn('flex items-center gap-1.5', layout === 'table' ? 'justify-end' : 'flex-wrap justify-start')}>
      <Button
        variant="outline"
        size="sm"
        disabled={busy}
        title="重置密码"
        onClick={onResetPassword}
        icon={<KeyRound aria-hidden className="h-3.5 w-3.5" />}
      >
        密码
      </Button>
      <Button
        variant={user.disabled ? 'outline' : 'cinnabar'}
        size="sm"
        disabled={busy || isMe}
        title={isMe ? '不能封禁自己' : user.disabled ? '解除封禁' : '封禁账号'}
        onClick={onToggleDisabled}
        icon={user.disabled ? <ShieldCheck aria-hidden className="h-3.5 w-3.5" /> : <Ban aria-hidden className="h-3.5 w-3.5" />}
      >
        {user.disabled ? '解封' : '封禁'}
      </Button>
      <Button
        variant="ghost"
        size="sm"
        disabled={busy || isMe}
        title={isMe ? '不能删除自己' : '删除账号'}
        onClick={onDelete}
        icon={<Trash2 aria-hidden className="h-3.5 w-3.5" />}
      >
        删除
      </Button>
    </div>
  );
}

export default function AdminUsersPage() {
  const { user: me } = useAuth();

  const [keywordInput, setKeywordInput] = useState('');
  const [q, setQ] = useState('');
  const [role, setRole] = useState('');
  const [statusFilter, setStatusFilter] = useState(''); // '' | 'active' | 'disabled'
  const [sort, setSort] = useState<SortKey>('created');
  const [page, setPage] = useState(1);

  const [data, setData] = useState<Paged<AdminUserRow> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ message: string; details: string[] } | null>(null);
  const [notice, setNotice] = useState<{ tone: 'ok' | 'err'; text: string } | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  const [resetTarget, setResetTarget] = useState<AdminUserRow | null>(null);
  const [newPassword, setNewPassword] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<AdminUserRow | null>(null);
  const [deleteForce, setDeleteForce] = useState(false);
  const [dialogLoading, setDialogLoading] = useState(false);

  const load = useCallback(
    (signal?: AbortSignal) => {
      setLoading(true);
      setError(null);
      return fetchAdminUsers(
        {
          q: q === '' ? undefined : q,
          role: role === '' ? undefined : role,
          disabled: statusFilter === '' ? undefined : statusFilter === 'disabled',
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
    [q, role, statusFilter, sort, page],
  );

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  /** 统一的行内操作封装：忙碌标记 + 结果提示 + 刷新列表 */
  async function runAction(id: number, action: () => Promise<unknown>, okText: string) {
    setBusyId(id);
    setNotice(null);
    try {
      await action();
      setNotice({ tone: 'ok', text: okText });
      await load();
    } catch (e) {
      setNotice({
        tone: 'err',
        text: e instanceof ApiRequestError ? e.message : '操作失败，请稍后重试',
      });
    } finally {
      setBusyId(null);
    }
  }

  async function submitResetPassword() {
    if (!resetTarget) return;
    if (newPassword.length < 6) {
      setNotice({ tone: 'err', text: '密码至少 6 位' });
      return;
    }
    setDialogLoading(true);
    try {
      await updateAdminUser(resetTarget.id, { password: newPassword });
      setNotice({ tone: 'ok', text: `已重置「${resetTarget.username}」的密码` });
      setResetTarget(null);
      setNewPassword('');
    } catch (e) {
      setNotice({ tone: 'err', text: e instanceof ApiRequestError ? e.message : '重置失败' });
    } finally {
      setDialogLoading(false);
    }
  }

  async function submitDelete() {
    if (!deleteTarget) return;
    setDialogLoading(true);
    try {
      const res = await deleteAdminUser(deleteTarget.id, deleteForce);
      setNotice({
        tone: 'ok',
        text: `已删除账号「${deleteTarget.username}」${res.removedMods > 0 ? `，同时移除其 ${res.removedMods} 个模组` : ''}`,
      });
      setDeleteTarget(null);
      setDeleteForce(false);
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
          <h1 className="font-serif text-2xl text-paper-100">账号管理</h1>
          <p className="mt-1.5 text-sm text-paper-500">
            与游戏内云存档共用账号体系；封禁后网页与游戏内都将无法登录。
          </p>
        </div>
        <Button
          variant="ghost"
          size="sm"
          icon={<RotateCcw aria-hidden className="h-3.5 w-3.5" />}
          onClick={() => void load()}
        >
          刷新
        </Button>
      </header>

      {/* 筛选 */}
      <section className="panel p-4">
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            setPage(1);
            setQ(keywordInput.trim());
          }}
        >
          <Field label="搜索" htmlFor="user-q" className="min-w-[220px] flex-1">
            <div className="relative">
              <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-paper-500" />
              <Input
                id="user-q"
                value={keywordInput}
                onChange={(e) => setKeywordInput(e.target.value)}
                placeholder="用户名或昵称"
                className="pl-9"
              />
            </div>
          </Field>

          <Field label="角色" htmlFor="user-role" className="w-32">
            <Select
              id="user-role"
              value={role}
              onChange={(e) => {
                setPage(1);
                setRole(e.target.value);
              }}
            >
              <option value="">全部角色</option>
              {ROLES.map((r) => (
                <option key={r.key} value={r.key}>
                  {r.label}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="状态" htmlFor="user-status" className="w-32">
            <Select
              id="user-status"
              value={statusFilter}
              onChange={(e) => {
                setPage(1);
                setStatusFilter(e.target.value);
              }}
            >
              <option value="">全部状态</option>
              <option value="active">正常</option>
              <option value="disabled">已封禁</option>
            </Select>
          </Field>

          <Field label="排序" htmlFor="user-sort" className="w-36">
            <Select
              id="user-sort"
              value={sort}
              onChange={(e) => {
                setPage(1);
                setSort(e.target.value as SortKey);
              }}
            >
              <option value="created">注册时间</option>
              <option value="lastLogin">最后登录</option>
              <option value="username">用户名</option>
              <option value="mods">模组数量</option>
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
            <table className="w-full min-w-[1000px] text-sm">
              <thead className="bg-ink-850/80 text-xs text-paper-500">
                <tr>
                  <th className="px-3 py-2.5 text-left font-normal">账号</th>
                  <th className="px-3 py-2.5 text-left font-normal">角色</th>
                  <th className="px-3 py-2.5 text-left font-normal">状态</th>
                  <th className="px-3 py-2.5 text-right font-normal">模组</th>
                  <th className="px-3 py-2.5 text-right font-normal">订阅</th>
                  <th className="px-3 py-2.5 text-right font-normal">评论</th>
                  <th className="px-3 py-2.5 text-left font-normal">注册时间</th>
                  <th className="px-3 py-2.5 text-left font-normal">最后登录</th>
                  <th className="sticky right-0 z-10 border-l border-ink-600/70 bg-ink-850 px-3 py-2.5 text-right font-normal">
                    操作
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-ink-700/70">
                {loading && items.length === 0
                  ? Array.from({ length: 5 }).map((_, i) => (
                      <tr key={i}>
                        <td colSpan={9} className="px-3 py-3">
                          <Skeleton className="h-5 w-full" />
                        </td>
                      </tr>
                    ))
                  : items.map((u) => {
                      const isMe = u.id === me?.id;
                      const busy = busyId === u.id;
                      return (
                        <tr key={u.id} className={cn('group transition-colors duration-200 hover:bg-ink-700/30', busy && 'opacity-60')}>
                          <td className="px-3 py-3">
                            <div className="flex items-center gap-2">
                              <div className="min-w-0">
                                <p className="truncate text-paper-200">
                                  {u.nickname ?? u.username}
                                  {isMe ? <span className="ml-1.5 text-xs text-gold-400">（你）</span> : null}
                                </p>
                                <p className="truncate font-mono text-xs text-paper-500">{u.username}</p>
                              </div>
                            </div>
                          </td>
                          <td className="px-3 py-3">
                            {isMe ? (
                              <Badge variant={roleVariant(u.role)}>{roleLabel(u.role)}</Badge>
                            ) : (
                              <Select
                                aria-label={`修改 ${u.username} 的角色`}
                                value={u.role}
                                disabled={busy}
                                onChange={(e) =>
                                  void runAction(
                                    u.id,
                                    () => updateAdminUser(u.id, { role: e.target.value }),
                                    `已将「${u.username}」的角色改为「${roleLabel(e.target.value)}」`,
                                  )
                                }
                                className="h-8 w-28 py-0 text-xs"
                              >
                                {ROLES.map((r) => (
                                  <option key={r.key} value={r.key}>
                                    {r.label}
                                  </option>
                                ))}
                              </Select>
                            )}
                          </td>
                          <td className="px-3 py-3">
                            {u.disabled ? <Badge variant="cinnabar">已封禁</Badge> : <Badge variant="bamboo">正常</Badge>}
                          </td>
                          <td className="px-3 py-3 text-right">
                            <Link
                              to={`/admin/mods?author=${encodeURIComponent(u.username)}`}
                              className="font-mono text-paper-300 transition-colors duration-200 hover:text-gold-300"
                            >
                              {u.modCount}
                            </Link>
                          </td>
                          <td className="px-3 py-3 text-right font-mono text-paper-400">{u.subscriptionCount}</td>
                          <td className="px-3 py-3 text-right font-mono text-paper-400">{u.commentCount}</td>
                          <td className="px-3 py-3 text-xs text-paper-500">{formatDate(u.createdAt)}</td>
                          <td className="px-3 py-3 text-xs text-paper-500">
                            {u.lastLoginAt ? formatRelative(u.lastLoginAt) : '从未登录'}
                          </td>
                          <td className="sticky right-0 z-10 border-l border-ink-600/70 bg-ink-800 px-3 py-3 transition-colors duration-200 group-hover:bg-ink-700">
                            <UserRowActions
                              user={u}
                              isMe={isMe}
                              busy={busy}
                              layout="table"
                              onResetPassword={() => {
                                setResetTarget(u);
                                setNewPassword('');
                              }}
                              onToggleDisabled={() =>
                                void runAction(
                                  u.id,
                                  () => updateAdminUser(u.id, { disabled: !u.disabled }),
                                  u.disabled ? `已解除「${u.username}」的封禁` : `已封禁「${u.username}」`,
                                )
                              }
                              onDelete={() => {
                                setDeleteTarget(u);
                                setDeleteForce(false);
                              }}
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
              ? Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-32 w-full rounded-xl" />)
              : items.map((u) => {
                  const isMe = u.id === me?.id;
                  const busy = busyId === u.id;
                  return (
                    <div
                      key={u.id}
                      className={cn('rounded-xl border border-gold-900/60 bg-ink-850/60 p-4', busy && 'opacity-60')}
                    >
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0">
                          <p className="truncate text-paper-200">
                            {u.nickname ?? u.username}
                            {isMe ? <span className="ml-1.5 text-xs text-gold-400">（你）</span> : null}
                          </p>
                          <p className="truncate font-mono text-xs text-paper-500">{u.username}</p>
                        </div>
                        <div className="flex shrink-0 flex-col items-end gap-1">
                          <Badge variant={roleVariant(u.role)}>{roleLabel(u.role)}</Badge>
                          {u.disabled ? <Badge variant="cinnabar">已封禁</Badge> : <Badge variant="bamboo">正常</Badge>}
                        </div>
                      </div>

                      <dl className="mt-3 grid grid-cols-3 gap-y-2 text-xs">
                        <div>
                          <dt className="text-paper-500">模组</dt>
                          <dd className="mt-0.5 font-mono text-paper-300">{u.modCount}</dd>
                        </div>
                        <div>
                          <dt className="text-paper-500">订阅</dt>
                          <dd className="mt-0.5 font-mono text-paper-300">{u.subscriptionCount}</dd>
                        </div>
                        <div>
                          <dt className="text-paper-500">评论</dt>
                          <dd className="mt-0.5 font-mono text-paper-300">{u.commentCount}</dd>
                        </div>
                        <div className="col-span-3">
                          <dt className="text-paper-500">注册 / 最后登录</dt>
                          <dd className="mt-0.5 font-mono text-paper-400">
                            {formatDate(u.createdAt)} · {u.lastLoginAt ? formatRelative(u.lastLoginAt) : '从未登录'}
                          </dd>
                        </div>
                      </dl>

                      <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-ink-700/70 pt-3">
                        {isMe ? (
                          <Badge variant={roleVariant(u.role)}>{roleLabel(u.role)}</Badge>
                        ) : (
                          <Select
                            aria-label={`修改 ${u.username} 的角色`}
                            value={u.role}
                            disabled={busy}
                            onChange={(e) =>
                              void runAction(
                                u.id,
                                () => updateAdminUser(u.id, { role: e.target.value }),
                                `已将「${u.username}」的角色改为「${roleLabel(e.target.value)}」`,
                              )
                            }
                            className="h-8 min-w-[120px] py-0 text-xs"
                          >
                            {ROLES.map((r) => (
                              <option key={r.key} value={r.key}>
                                {r.label}
                              </option>
                            ))}
                          </Select>
                        )}
                        <UserRowActions
                          user={u}
                          isMe={isMe}
                          busy={busy}
                          layout="card"
                          onResetPassword={() => {
                            setResetTarget(u);
                            setNewPassword('');
                          }}
                          onToggleDisabled={() =>
                            void runAction(
                              u.id,
                              () => updateAdminUser(u.id, { disabled: !u.disabled }),
                              u.disabled ? `已解除「${u.username}」的封禁` : `已封禁「${u.username}」`,
                            )
                          }
                          onDelete={() => {
                            setDeleteTarget(u);
                            setDeleteForce(false);
                          }}
                        />
                      </div>
                    </div>
                  );
                })}
          </div>

          {!loading && items.length === 0 ? (
            <p className="px-3 py-10 text-center text-sm text-paper-500">没有符合条件的账号。</p>
          ) : null}

          <div className="px-3 pb-3">
            <Pagination page={data?.page ?? 1} pageSize={PAGE_SIZE} total={data?.total ?? 0} onChange={setPage} />
          </div>
        </section>
      )}

      {/* 重置密码 */}
      <ConfirmDialog
        open={resetTarget !== null}
        title={`重置「${resetTarget?.username ?? ''}」的密码`}
        description="重置后请通过站外渠道告知对方新密码；旧密码立即失效。"
        confirmText="确认重置"
        loading={dialogLoading}
        onCancel={() => {
          setResetTarget(null);
          setNewPassword('');
        }}
        onConfirm={() => void submitResetPassword()}
      >
        <div className="mt-3">
          <Field label="新密码" htmlFor="new-password" hint="6–128 位">
            <Input
              id="new-password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              placeholder="输入新密码"
            />
          </Field>
        </div>
      </ConfirmDialog>

      {/* 删除账号 */}
      <ConfirmDialog
        open={deleteTarget !== null}
        title={`删除账号「${deleteTarget?.username ?? ''}」`}
        danger
        confirmText={deleteForce ? '确认强制删除' : '确认删除'}
        loading={dialogLoading}
        onCancel={() => {
          setDeleteTarget(null);
          setDeleteForce(false);
        }}
        onConfirm={() => void submitDelete()}
      >
        <div className="mt-3 space-y-2 text-sm text-paper-400">
          <p>
            该用户名下有 <span className="font-mono text-paper-200">{deleteTarget?.modCount ?? 0}</span> 个模组。
          </p>
          {(deleteTarget?.modCount ?? 0) > 0 ? (
            <label className="flex cursor-pointer items-start gap-2 text-xs">
              <input
                type="checkbox"
                checked={deleteForce}
                onChange={(e) => setDeleteForce(e.target.checked)}
                className="mt-0.5 h-4 w-4 cursor-pointer accent-cinnabar-500"
              />
              <span>
                同时删除其全部模组及其磁盘文件（不可恢复）。不勾选则删除会被拒绝，需要你先单独处理这些模组。
              </span>
            </label>
          ) : null}
        </div>
      </ConfirmDialog>
    </div>
  );
}
