import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { PackageOpen, SearchX, X } from 'lucide-react';
import { fetchFilters, type FilterOptions, type SortKey } from '../lib/api';
import { categoryLabel } from '../lib/catalog';
import { formatCount } from '../lib/format';
import { useBrowse, type BrowseFilterState } from '../hooks/useBrowse';
import { FeaturedStrip } from '../components/browse/FeaturedStrip';
import { FilterSidebar } from '../components/browse/FilterSidebar';
import { ModCard } from '../components/browse/ModCard';
import { SortTabs } from '../components/browse/SortTabs';
import { Button } from '../components/ui/Button';
import { CardSkeletonGrid, EmptyState, ErrorState, Spinner } from '../components/ui/Feedback';

/**
 * 创意工坊首页 /browse（对应 建设计划.md §4.1）
 *
 * 设计核心：**筛选状态全部放在 URL 上**。
 *   /browse?q=武将&category=person&tag=三国&sort=new
 * 带来的好处：可分享、可收藏、可前进后退；组件本身因此变得很薄，
 * 只需「读 URL → 请求 → 渲染」，不需要在内存里维护一份容易与地址栏跑偏的状态。
 */

const SORT_VALUES: SortKey[] = ['hot', 'new', 'updated', 'trending'];

function readSort(raw: string | null): SortKey {
  return (SORT_VALUES as string[]).includes(raw ?? '') ? (raw as SortKey) : 'hot';
}

export default function BrowsePage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [options, setOptions] = useState<FilterOptions | null>(null);
  const [optionsLoading, setOptionsLoading] = useState(true);

  const filters = useMemo<BrowseFilterState>(
    () => ({
      q: searchParams.get('q') ?? '',
      category: searchParams.get('category') ?? '',
      gameVersion: searchParams.get('gameVersion') ?? '',
      sort: readSort(searchParams.get('sort')),
      tags: searchParams.getAll('tag'),
    }),
    [searchParams],
  );

  const { items, total, initialLoading, loadingMore, hasMore, error, loadMore, retry } = useBrowse(filters);

  // 筛选栏数据（类型计数 / 标签 / 版本）只在首次进入时拉一次
  useEffect(() => {
    const controller = new AbortController();
    fetchFilters(controller.signal)
      .then((res) => setOptions(res))
      .catch(() => undefined)
      .finally(() => {
        if (!controller.signal.aborted) setOptionsLoading(false);
      });
    return () => controller.abort();
  }, []);

  const patchFilters = useCallback(
    (patch: Partial<BrowseFilterState>) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          if (patch.q !== undefined) patch.q === '' ? next.delete('q') : next.set('q', patch.q);
          if (patch.category !== undefined)
            patch.category === '' ? next.delete('category') : next.set('category', patch.category);
          if (patch.gameVersion !== undefined)
            patch.gameVersion === '' ? next.delete('gameVersion') : next.set('gameVersion', patch.gameVersion);
          if (patch.sort !== undefined) patch.sort === 'hot' ? next.delete('sort') : next.set('sort', patch.sort);
          if (patch.tags !== undefined) {
            next.delete('tag');
            for (const t of patch.tags) next.append('tag', t);
          }
          return next;
        },
        { replace: true },
      );
    },
    [setSearchParams],
  );

  const clearFilters = useCallback(() => {
    setSearchParams(new URLSearchParams(), { replace: true });
  }, [setSearchParams]);

  const hasFilter =
    filters.q !== '' || filters.category !== '' || filters.tags.length > 0 || filters.gameVersion !== '';

  // 无限滚动：哨兵进入视口即取下一页（提前 400px 触发，滚动手感更顺）
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    const el = sentinelRef.current;
    if (!el || !hasMore) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) loadMore();
      },
      { rootMargin: '400px 0px' },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [hasMore, loadMore]);

  return (
    <div className="mx-auto max-w-[1600px] px-4 py-6 sm:px-5">
      {/* 工坊横幅：底图是水墨群山，再叠一层由左向右的渐变压暗，保证标题在任何配色下都读得清 */}
      <section className="panel paper-grain relative mb-5 overflow-hidden">
        <img
          src="/art/banner-mountains.jpg"
          alt=""
          aria-hidden
          className="pointer-events-none absolute inset-0 h-full w-full object-cover opacity-[var(--art-opacity)] [filter:brightness(var(--art-brightness))_saturate(1.05)]"
        />
        {/* 渐变压暗只重压左侧文字区，右侧留亮让山水的层次透出来 */}
        <div className="absolute inset-0 bg-gradient-to-r from-ink-950/80 via-ink-950/45 to-ink-950/15" />
        <div className="relative p-5 sm:p-7">
          <h1 className="font-serif text-2xl text-paper-100 sm:text-3xl">Sango Infinity 创意工坊</h1>
          <p className="mt-2 max-w-3xl text-sm leading-relaxed text-paper-400">
            浏览并订阅社区创作的剧本、武将、头像、语音与技能模组。模组只包含数据与资源，
            安装前会经过安全扫描；游戏内填写本站市场地址即可一键下载与更新。
          </p>
          <p className="mt-3 text-xs text-paper-500">
            共 <span className="font-mono text-gold-300">{formatCount(total)}</span> 个模组可供订阅
            {filters.q !== '' ? ` · 当前搜索「${filters.q}」` : ''}
          </p>
        </div>
      </section>

      <div className="grid gap-6 lg:grid-cols-[268px_minmax(0,1fr)]">
        {/* 左侧常驻筛选栏 */}
        <div className="lg:sticky lg:top-24 lg:self-start">
          <FilterSidebar
            filters={filters}
            options={options}
            optionsLoading={optionsLoading}
            onChange={patchFilters}
            onClear={clearFilters}
          />
        </div>

        {/* 右侧内容区 */}
        <div className="min-w-0 space-y-5">
          {hasFilter ? (
            <div className="flex flex-wrap items-center gap-2">
              {filters.q !== '' ? (
                <FilterChip label={`搜索：${filters.q}`} onRemove={() => patchFilters({ q: '' })} />
              ) : null}
              {filters.category !== '' ? (
                <FilterChip label={`类型：${categoryLabel(filters.category)}`} onRemove={() => patchFilters({ category: '' })} />
              ) : null}
              {filters.gameVersion !== '' ? (
                <FilterChip
                  label={`版本：${filters.gameVersion}`}
                  onRemove={() => patchFilters({ gameVersion: '' })}
                />
              ) : null}
              {filters.tags.map((tag) => (
                <FilterChip
                  key={tag}
                  label={`标签：${tag}`}
                  onRemove={() => patchFilters({ tags: filters.tags.filter((t) => t !== tag) })}
                />
              ))}
            </div>
          ) : null}

          <SortTabs value={filters.sort} onChange={(sort) => patchFilters({ sort })} />

          {error ? (
            <ErrorState message={error.message} details={error.details} onRetry={retry} />
          ) : initialLoading ? (
            <CardSkeletonGrid count={8} />
          ) : items.length === 0 ? (
            <EmptyState
              illustration
              title={filters.q !== '' ? '没有找到匹配的模组' : '这个条件下还没有模组'}
              description={
                filters.q !== ''
                  ? '换个关键词试试，或者清除筛选条件浏览全部内容。'
                  : '换个筛选条件看看，或者成为第一个在此分类发布模组的作者。'
              }
              icon={filters.q !== '' ? <SearchX aria-hidden className="h-8 w-8" /> : <PackageOpen aria-hidden className="h-8 w-8" />}
              action={
                hasFilter ? (
                  <Button variant="outline" size="sm" onClick={clearFilters}>
                    清除全部筛选
                  </Button>
                ) : undefined
              }
            />
          ) : (
            <>
              {/* 精选大卡只在「默认浏览状态」出现，避免干扰筛选结果的阅读 */}
              {!hasFilter && filters.sort === 'hot' ? <FeaturedStrip mods={items} /> : null}

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
                {items.map((mod) => (
                  <ModCard key={mod.id} mod={mod} />
                ))}
              </div>

              {/* 无限滚动哨兵与底部状态 */}
              <div ref={sentinelRef} aria-hidden className="h-1" />
              <div className="flex items-center justify-center py-6 text-sm text-paper-500">
                {loadingMore ? (
                  <span className="flex items-center gap-2">
                    <Spinner className="h-4 w-4" />
                    正在载入更多…
                  </span>
                ) : hasMore ? (
                  <Button variant="outline" size="sm" onClick={loadMore}>
                    载入更多
                  </Button>
                ) : (
                  <span>已经到底了 · 共 {formatCount(items.length)} 个模组</span>
                )}
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

/** 已选条件胶囊：点击即移除该条件 */
function FilterChip({ label, onRemove }: { label: string; onRemove: () => void }) {
  return (
    <span className="inline-flex items-center gap-1 rounded border border-gold-700/60 bg-gold-500/10 px-2 py-1 text-xs text-gold-300">
      {label}
      <button
        type="button"
        onClick={onRemove}
        aria-label={`移除筛选条件 ${label}`}
        className="cursor-pointer rounded p-0.5 transition-colors duration-200 hover:bg-gold-500/20"
      >
        <X aria-hidden className="h-3 w-3" />
      </button>
    </span>
  );
}
