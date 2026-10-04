import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { CornerDownRight, Heart, Send } from 'lucide-react';
import {
  ApiRequestError,
  deleteComment,
  fetchComments,
  likeComment,
  postComment,
  unlikeComment,
  type CommentItem,
} from '../../lib/api';
import { formatRelative } from '../../lib/format';
import { useAuth } from '../../hooks/useAuth';
import { cn } from '../../lib/cn';
import { Badge, SectionTitle } from '../ui/Badge';
import { Button } from '../ui/Button';
import { LoadingBlock } from '../ui/Feedback';

const PAGE_SIZE = 20;
const MAX_LEN = 500;

/**
 * 模组留言区（详情页左主栏底部）
 *
 * 只渲染两层：顶层留言 + 回复。回复的回复由服务端挂到同一条顶层留言下，
 * 所以这里不需要处理任意深度的嵌套。
 *
 * 权限判定放在服务端（每条留言带 canDelete），前端只负责按标记显示按钮 ——
 * "模组作者也能删自己模组下的留言"这条规则涉及三方身份，不适合在前端推断。
 */
export function CommentSection({ modId }: { modId: string }) {
  const { user } = useAuth();

  const [items, setItems] = useState<CommentItem[]>([]);
  const [total, setTotal] = useState(0);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState('');

  const [content, setContent] = useState('');
  const [replyTo, setReplyTo] = useState<{ id: number; name: string } | null>(null);
  const [posting, setPosting] = useState(false);
  const [postError, setPostError] = useState('');
  const [deletingId, setDeletingId] = useState<number | null>(null);

  const loadFirstPage = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const page = await fetchComments(modId, { limit: PAGE_SIZE });
      setItems(page.items);
      setTotal(page.total);
      setNextCursor(page.nextCursor);
    } catch (e) {
      setError(e instanceof ApiRequestError ? e.message : '留言加载失败');
    } finally {
      setLoading(false);
    }
  }, [modId]);

  useEffect(() => {
    void loadFirstPage();
  }, [loadFirstPage]);

  async function loadMore() {
    if (nextCursor == null || loadingMore) return;
    setLoadingMore(true);
    setError('');
    try {
      const page = await fetchComments(modId, { cursor: nextCursor, limit: PAGE_SIZE });
      setItems((prev) => [...prev, ...page.items]);
      setNextCursor(page.nextCursor);
    } catch (e) {
      setError(e instanceof ApiRequestError ? e.message : '加载更早的留言失败');
    } finally {
      setLoadingMore(false);
    }
  }

  async function handlePost() {
    const text = content.trim();
    if (text === '' || posting) return;

    setPosting(true);
    setPostError('');
    try {
      const { comment } = await postComment(modId, text, replyTo?.id);
      setContent('');
      setTotal((n) => n + 1);

      if (replyTo) {
        setItems((prev) =>
          prev.map((item) =>
            item.id === replyTo.id ? { ...item, replies: [...(item.replies ?? []), comment] } : item,
          ),
        );
        setReplyTo(null);
      } else {
        // 列表是最新在前，新留言插到最上面 —— 否则用户会以为没发出去
        setItems((prev) => [{ ...comment, replies: [] }, ...prev]);
      }
    } catch (e) {
      setPostError(e instanceof ApiRequestError ? e.message : '发送失败，请稍后重试');
    } finally {
      setPosting(false);
    }
  }

  async function handleDelete(target: CommentItem, isReply: boolean) {
    if (deletingId != null) return;
    if (!window.confirm('确定删除这条留言吗？删除后无法恢复。')) return;

    setDeletingId(target.id);
    setPostError('');
    try {
      await deleteComment(target.id);
      // 删顶层留言时服务端会连带删掉它的回复，计数要按实际删除条数减
      const removed = isReply ? 1 : 1 + (target.replies?.length ?? 0);
      setTotal((n) => Math.max(0, n - removed));

      if (isReply) {
        setItems((prev) =>
          prev.map((item) => ({
            ...item,
            replies: (item.replies ?? []).filter((r) => r.id !== target.id),
          })),
        );
      } else {
        setItems((prev) => prev.filter((item) => item.id !== target.id));
      }
    } catch (e) {
      setPostError(e instanceof ApiRequestError ? e.message : '删除失败，请稍后重试');
    } finally {
      setDeletingId(null);
    }
  }

  /** 局部更新某条留言（顶层或回复），点赞与失败回滚都走它 */
  function patchItem(id: number, isReply: boolean, patch: (c: CommentItem) => CommentItem) {
    setItems((prev) =>
      prev.map((item) => {
        if (isReply) {
          return { ...item, replies: (item.replies ?? []).map((r) => (r.id === id ? patch(r) : r)) };
        }
        return item.id === id ? patch(item) : item;
      }),
    );
  }

  async function handleLike(target: CommentItem, isReply: boolean) {
    if (!user) {
      setPostError('登录后可以点赞');
      return;
    }

    const nextLiked = !target.likedByMe;
    // 乐观更新：点赞必须立刻有反馈，失败再回滚
    patchItem(target.id, isReply, (c) => ({
      ...c,
      likedByMe: nextLiked,
      likes: Math.max(0, c.likes + (nextLiked ? 1 : -1)),
    }));

    try {
      const res = nextLiked ? await likeComment(target.id) : await unlikeComment(target.id);
      // 以服务端返回的计数为准 —— 期间可能还有别人点赞
      patchItem(target.id, isReply, (c) => ({ ...c, likedByMe: res.liked, likes: res.likes }));
    } catch (e) {
      patchItem(target.id, isReply, (c) => ({ ...c, likedByMe: target.likedByMe, likes: target.likes }));
      setPostError(e instanceof ApiRequestError ? e.message : '操作失败，请稍后重试');
    }
  }

  return (
    <section className="panel p-5" id="comments">
      <SectionTitle
        action={<span className="text-xs text-paper-500">{total > 0 ? `共 ${total} 条` : '还没有留言'}</span>}
      >
        留言
      </SectionTitle>

      {loading ? (
        <LoadingBlock text="正在加载留言…" />
      ) : (
        <>
          {/* 发表框 / 未登录提示 */}
          {user ? (
            <div className="mb-5">
              {replyTo ? (
                <div className="mb-2 flex items-center gap-2 text-xs text-paper-500">
                  <CornerDownRight aria-hidden className="h-3.5 w-3.5 text-gold-500" />
                  正在回复 <span className="text-gold-300">{replyTo.name}</span>
                  <button
                    type="button"
                    onClick={() => setReplyTo(null)}
                    className="cursor-pointer text-paper-500 underline-offset-2 transition-colors duration-200 hover:text-paper-300 hover:underline"
                  >
                    取消
                  </button>
                </div>
              ) : null}

              <textarea
                value={content}
                onChange={(e) => setContent(e.target.value)}
                maxLength={MAX_LEN}
                rows={3}
                aria-label="留言内容"
                placeholder={
                  replyTo
                    ? `回复 ${replyTo.name}…`
                    : '遇到问题、发现 bug 或有建议，都可以在这里告诉作者（支持换行）'
                }
                className="w-full resize-y rounded-md border border-ink-500/80 bg-ink-900/70 px-3 py-2 text-sm leading-relaxed text-paper-200 placeholder:text-paper-600 focus:border-gold-500 focus:outline-none"
              />

              <div className="mt-2 flex flex-wrap items-center gap-3">
                <Button
                  variant="gold"
                  size="sm"
                  loading={posting}
                  onClick={handlePost}
                  icon={<Send aria-hidden className="h-3.5 w-3.5" />}
                >
                  发表留言
                </Button>
                <span className="text-xs text-paper-600">
                  {content.length} / {MAX_LEN}
                </span>
                {postError ? <span className="text-xs text-cinnabar-400">{postError}</span> : null}
              </div>
            </div>
          ) : (
            <p className="mb-5 rounded-md border border-ink-500/80 bg-ink-900/50 px-3 py-2.5 text-sm text-paper-400">
              <Link to="/login" className="text-gold-300 transition-colors duration-200 hover:text-gold-200">
                登录
              </Link>
              {' '}后可以留言反馈问题；模组作者与管理员可以删除不当留言。
            </p>
          )}

          {/* 留言列表 */}
          {items.length === 0 ? (
            <p className="py-6 text-center text-sm text-paper-500">还没有人留言，来提第一个问题吧。</p>
          ) : (
            <ul className="space-y-5">
              {items.map((item) => (
                <li key={item.id}>
                  <CommentRow
                    item={item}
                    deleting={deletingId === item.id}
                    onReply={(target) =>
                      setReplyTo({ id: item.id, name: target.user.nickname || target.user.username })
                    }
                    onDelete={(target) => handleDelete(target, false)}
                    onLike={(target) => handleLike(target, false)}
                  />

                  {item.replies && item.replies.length > 0 ? (
                    <ul className="ml-6 mt-3 space-y-3 border-l border-ink-700/70 pl-3 sm:ml-9">
                      {item.replies.map((reply) => (
                        <li key={reply.id}>
                          <CommentRow
                            item={reply}
                            isReply
                            deleting={deletingId === reply.id}
                            onReply={(target) =>
                              setReplyTo({ id: item.id, name: target.user.nickname || target.user.username })
                            }
                            onDelete={(target) => handleDelete(target, true)}
                            onLike={(target) => handleLike(target, true)}
                          />
                        </li>
                      ))}
                    </ul>
                  ) : null}
                </li>
              ))}
            </ul>
          )}

          {nextCursor != null ? (
            <div className="mt-5 text-center">
              <Button variant="outline" size="sm" loading={loadingMore} onClick={loadMore}>
                加载更早的留言
              </Button>
            </div>
          ) : null}

          {error ? <p className="mt-3 text-xs text-cinnabar-400">{error}</p> : null}
        </>
      )}
    </section>
  );
}

interface CommentRowProps {
  item: CommentItem;
  isReply?: boolean;
  deleting: boolean;
  onReply: (target: CommentItem) => void;
  onDelete: (target: CommentItem) => void;
  onLike: (target: CommentItem) => void;
}

function CommentRow({ item, isReply = false, deleting, onReply, onDelete, onLike }: CommentRowProps) {
  const name = item.user.nickname || item.user.username;

  return (
    <div className="flex gap-3">
      <span
        aria-hidden
        className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-ink-700 font-mono text-xs text-gold-300"
      >
        {name.slice(0, 1).toUpperCase()}
      </span>

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
          <span className="text-paper-300">{name}</span>
          {item.user.role === 'admin' ? <Badge variant="bamboo">管理员</Badge> : null}
          <span className="text-paper-600">{formatRelative(item.createdAt)}</span>

          <span className="ml-auto flex items-center gap-3">
            <button
              type="button"
              onClick={() => onLike(item)}
              title={item.likedByMe ? '取消点赞' : '点赞'}
              aria-pressed={item.likedByMe}
              className={cn(
                'flex cursor-pointer items-center gap-1 transition-colors duration-200',
                item.likedByMe
                  ? 'text-cinnabar-400 hover:text-cinnabar-500'
                  : 'text-paper-500 hover:text-cinnabar-400',
              )}
            >
              <Heart aria-hidden className={cn('h-3.5 w-3.5', item.likedByMe && 'fill-current')} />
              {item.likes > 0 ? item.likes : null}
              <span className="sr-only">点赞</span>
            </button>
            {isReply ? null : (
              <button
                type="button"
                onClick={() => onReply(item)}
                className="cursor-pointer text-paper-500 transition-colors duration-200 hover:text-gold-300"
              >
                回复
              </button>
            )}
            {item.canDelete ? (
              <button
                type="button"
                onClick={() => onDelete(item)}
                disabled={deleting}
                className="cursor-pointer text-paper-500 transition-colors duration-200 hover:text-cinnabar-400 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {deleting ? '删除中…' : '删除'}
              </button>
            ) : null}
          </span>
        </div>

        <p className="mt-1 whitespace-pre-wrap break-words text-sm leading-relaxed text-paper-200">
          {item.content}
        </p>
      </div>
    </div>
  );
}
