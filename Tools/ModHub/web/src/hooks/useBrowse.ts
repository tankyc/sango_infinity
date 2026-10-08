import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiRequestError, fetchBrowse, type ModListItem, type SortKey } from '../lib/api';

/**
 * 浏览列表数据源：游标分页 + 无限加载
 *
 * 关键取舍：
 *   1. 筛选条件变化即「重置并从第一页重来」，不做增量修补 —— 逻辑简单且不会出现脏数据；
 *   2. loadMore 用 ref 记录 in-flight 状态，避免 IntersectionObserver 连续触发造成重复请求；
 *   3. 首屏用 loading 骨架、后续页用底部 loading 条，两者状态严格区分，避免整页闪烁。
 */

export interface BrowseFilterState {
  q: string;
  category: string;
  tags: string[];
  gameVersion: string;
  sort: SortKey;
}

export interface BrowseError {
  message: string;
  details: string[];
}

export interface BrowseState {
  items: ModListItem[];
  total: number;
  initialLoading: boolean;
  loadingMore: boolean;
  hasMore: boolean;
  error: BrowseError | null;
  loadMore: () => void;
  retry: () => void;
}

const PAGE_SIZE = 24;

function toBrowseError(e: unknown): BrowseError {
  if (e instanceof ApiRequestError) return { message: e.message, details: e.details };
  return { message: '载入失败，请稍后重试', details: [] };
}

export function useBrowse(filters: BrowseFilterState): BrowseState {
  const [items, setItems] = useState<ModListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [cursor, setCursor] = useState<string | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [initialLoading, setInitialLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<BrowseError | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  // 用 ref 持有最新筛选条件：loadMore 不该因为筛选对象的引用变化而重建
  const filtersRef = useRef(filters);
  filtersRef.current = filters;

  const filterKey = JSON.stringify(filters);

  useEffect(() => {
    const controller = new AbortController();
    setInitialLoading(true);
    setError(null);
    setItems([]);
    setCursor(null);
    setHasMore(false);

    fetchBrowse({ ...filtersRef.current, limit: PAGE_SIZE }, controller.signal)
      .then((res) => {
        setItems(res.items);
        setTotal(res.total);
        setCursor(res.nextCursor);
        setHasMore(res.nextCursor != null);
      })
      .catch((e: unknown) => {
        if (!controller.signal.aborted) setError(toBrowseError(e));
      })
      .finally(() => {
        if (!controller.signal.aborted) setInitialLoading(false);
      });

    return () => controller.abort();
    // filterKey 已完整覆盖 filters 的内容变化
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKey, reloadToken]);

  const loadingMoreRef = useRef(false);

  const loadMore = useCallback(() => {
    if (loadingMoreRef.current || initialLoading || cursor == null) return;
    loadingMoreRef.current = true;
    setLoadingMore(true);

    fetchBrowse({ ...filtersRef.current, cursor, limit: PAGE_SIZE })
      .then((res) => {
        // 追加而不是替换；同 id 去重，防止翻页边界出现重复卡片
        setItems((prev) => {
          const seen = new Set(prev.map((m) => m.id));
          return [...prev, ...res.items.filter((m) => !seen.has(m.id))];
        });
        setTotal(res.total);
        setCursor(res.nextCursor);
        setHasMore(res.nextCursor != null);
      })
      .catch((e: unknown) => setError(toBrowseError(e)))
      .finally(() => {
        loadingMoreRef.current = false;
        setLoadingMore(false);
      });
  }, [cursor, initialLoading]);

  const retry = useCallback(() => setReloadToken((v) => v + 1), []);

  return { items, total, initialLoading, loadingMore, hasMore, error, loadMore, retry };
}
