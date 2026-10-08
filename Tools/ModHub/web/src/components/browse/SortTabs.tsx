import type { SortKey } from '../../lib/api';
import { SORT_TABS } from '../../lib/catalog';
import { cn } from '../../lib/cn';

/**
 * 排序 Tab（Steam 式：精选 / 最受欢迎 / 最新发布 / 最近更新 / 趋势榜）
 * 用 tablist + tab 语义，键盘左右键切换由浏览器原生支持（button 默认焦点行为）。
 */
export function SortTabs({ value, onChange }: { value: SortKey; onChange: (v: SortKey) => void }) {
  return (
    <div role="tablist" aria-label="排序方式" className="flex items-center gap-1 overflow-x-auto border-b border-ink-700/80">
      {SORT_TABS.map((tab) => {
        const active = tab.key === value;
        return (
          <button
            key={tab.key}
            type="button"
            role="tab"
            aria-selected={active}
            title={tab.hint}
            onClick={() => onChange(tab.key)}
            className={cn(
              'relative cursor-pointer whitespace-nowrap px-3 py-2.5 text-sm transition-colors duration-200',
              active ? 'text-gold-300' : 'text-paper-400 hover:text-paper-100',
            )}
          >
            {tab.label}
            {active ? (
              <span aria-hidden className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-gold-500" />
            ) : null}
          </button>
        );
      })}
    </div>
  );
}
