import { useState } from 'react';
import { ChevronDown, RotateCcw, Tag } from 'lucide-react';
import type { FilterOptions } from '../../lib/api';
import { CATEGORIES } from '../../lib/catalog';
import type { BrowseFilterState } from '../../hooks/useBrowse';
import { Button } from '../ui/Button';
import { SectionTitle } from '../ui/Badge';
import { CategoryIcon } from '../ui/CategoryIcon';
import { Skeleton } from '../ui/Feedback';
import { cn } from '../../lib/cn';

/**
 * 左侧常驻筛选栏（对应 建设计划.md §4.1）
 *
 * 交互约定：
 *   类型  —— 单选（再点一次取消），对应 Steam 的「分类」
 *   标签  —— 多选，命中任意一个即入选（或语义）
 *   版本  —— 单选下拉，未知版本用「全部版本」表示
 * 筛选状态全部由 URL 承载（见 BrowsePage），因此本组件是纯受控组件，自身不持有筛选逻辑。
 */

const COLLAPSED_TAG_COUNT = 14;

export interface FilterSidebarProps {
  filters: BrowseFilterState;
  options: FilterOptions | null;
  optionsLoading: boolean;
  onChange: (patch: Partial<BrowseFilterState>) => void;
  onClear: () => void;
}

export function FilterSidebar({ filters, options, optionsLoading, onChange, onClear }: FilterSidebarProps) {
  const [showAllTags, setShowAllTags] = useState(false);

  const hasFilter =
    filters.q !== '' || filters.category !== '' || filters.tags.length > 0 || filters.gameVersion !== '';

  const visibleTags = showAllTags ? options?.tags ?? [] : (options?.tags ?? []).slice(0, COLLAPSED_TAG_COUNT);

  function toggleTag(tag: string) {
    const next = filters.tags.includes(tag)
      ? filters.tags.filter((t) => t !== tag)
      : [...filters.tags, tag];
    onChange({ tags: next });
  }

  return (
    <aside className="space-y-6" aria-label="筛选条件">
      {/* 类型 */}
      <section>
        <SectionTitle>类型</SectionTitle>
        <ul className="space-y-1">
          {CATEGORIES.map((cat) => {
            const count = options?.categories.find((c) => c.key === cat.key)?.count ?? 0;
            const active = filters.category === cat.key;
            return (
              <li key={cat.key}>
                <button
                  type="button"
                  title={cat.hint}
                  aria-pressed={active}
                  onClick={() => onChange({ category: active ? '' : cat.key })}
                  className={cn(
                    'flex w-full cursor-pointer items-center gap-2 rounded-md px-2.5 py-2 text-sm transition-colors duration-200',
                    active
                      ? 'bg-gold-500/12 text-gold-300 shadow-gold-sm'
                      : 'text-paper-300 hover:bg-ink-700/70 hover:text-paper-100',
                  )}
                >
                  <CategoryIcon category={cat.key} className="h-4 w-4" />
                  <span className="flex-1 text-left">{cat.label}</span>
                  <span className="font-mono text-[11px] text-paper-500">{count}</span>
                </button>
              </li>
            );
          })}
        </ul>
      </section>

      <div className="gold-rule" />

      {/* 标签 */}
      <section>
        <SectionTitle>标签</SectionTitle>
        {optionsLoading ? (
          <div className="flex flex-wrap gap-1.5">
            {Array.from({ length: 8 }).map((_, i) => (
              <Skeleton key={i} className="h-6 w-16 rounded" />
            ))}
          </div>
        ) : (options?.tags.length ?? 0) === 0 ? (
          <p className="text-xs text-paper-500">还没有人给模组打标签</p>
        ) : (
          <>
            <div className="flex flex-wrap gap-1.5">
              {visibleTags.map(({ tag, count }) => {
                const active = filters.tags.includes(tag);
                return (
                  <button
                    key={tag}
                    type="button"
                    aria-pressed={active}
                    onClick={() => toggleTag(tag)}
                    title={`${count} 个模组使用了该标签`}
                    className={cn(
                      'inline-flex cursor-pointer items-center gap-1 rounded border px-2 py-1 text-xs transition-colors duration-200',
                      active
                        ? 'border-gold-600/80 bg-gold-500/15 text-gold-300'
                        : 'border-ink-500/70 bg-ink-700/40 text-paper-400 hover:border-gold-700/70 hover:text-paper-100',
                    )}
                  >
                    <Tag aria-hidden className="h-3 w-3" />
                    {tag}
                    <span className="font-mono text-[10px] text-paper-500">{count}</span>
                  </button>
                );
              })}
            </div>
            {(options?.tags.length ?? 0) > COLLAPSED_TAG_COUNT ? (
              <button
                type="button"
                onClick={() => setShowAllTags((v) => !v)}
                className="mt-2 inline-flex cursor-pointer items-center gap-1 text-xs text-gold-400 transition-colors duration-200 hover:text-gold-300"
              >
                {showAllTags ? '收起' : `展开全部 ${options?.tags.length ?? 0} 个标签`}
                <ChevronDown aria-hidden className={cn('h-3 w-3 transition-transform duration-200', showAllTags && 'rotate-180')} />
              </button>
            ) : null}
          </>
        )}
      </section>

      {/* 兼容游戏版本 */}
      {(options?.gameVersions.length ?? 0) > 0 ? (
        <>
          <div className="gold-rule" />
          <section>
            <SectionTitle>兼容版本</SectionTitle>
            <select
              value={filters.gameVersion}
              onChange={(e) => onChange({ gameVersion: e.target.value })}
              className="w-full cursor-pointer rounded-md border border-ink-500/80 bg-ink-900/70 px-2.5 py-2 text-sm text-paper-200 transition-colors duration-200 hover:border-gold-700/70 focus:border-gold-500 focus:outline-none"
              aria-label="按兼容的游戏版本筛选"
            >
              <option value="">全部版本</option>
              {options?.gameVersions.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </select>
          </section>
        </>
      ) : null}

      {hasFilter ? (
        <Button
          variant="ghost"
          size="sm"
          className="w-full justify-center"
          icon={<RotateCcw aria-hidden className="h-3.5 w-3.5" />}
          onClick={onClear}
        >
          清除全部筛选
        </Button>
      ) : null}
    </aside>
  );
}
