import type { ReactNode } from 'react';
import { cn } from '../../lib/cn';

/**
 * 徽章
 * 用于分类、标签、状态（已订阅 / 有更新 / 未启用）。
 * 状态类徽章一律「颜色 + 图标 + 文字」三重表达，不依赖颜色单独传达信息（可访问性要求）。
 */

export type BadgeVariant = 'gold' | 'paper' | 'bamboo' | 'cinnabar' | 'outline';

const VARIANTS: Record<BadgeVariant, string> = {
  gold: 'border-gold-600/70 bg-gold-500/15 text-gold-300',
  // 用 paper-500（中间调）做底：深色与浅色主题下都能形成可见的淡底
  paper: 'border-paper-500/40 bg-paper-500/15 text-paper-200',
  bamboo: 'border-bamboo-400/60 bg-bamboo-500/15 text-bamboo-400',
  cinnabar: 'border-cinnabar-500/60 bg-cinnabar-500/15 text-cinnabar-400',
  outline: 'border-ink-500/70 bg-ink-700/50 text-paper-400',
};

export interface BadgeProps {
  variant?: BadgeVariant;
  icon?: ReactNode;
  className?: string;
  children: ReactNode;
}

export function Badge({ variant = 'outline', icon, className, children }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded border px-2 py-0.5 text-xs leading-5',
        VARIANTS[variant],
        className,
      )}
    >
      {icon}
      {children}
    </span>
  );
}

/** 键值对展示（详情页右信息栏大量使用） */
export function MetaRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-3 py-1.5 text-sm">
      <span className="shrink-0 text-paper-500">{label}</span>
      <span className="text-right text-paper-200">{children}</span>
    </div>
  );
}

/** 区块小标题：左侧短金线 + 文字，详情页与筛选栏统一使用 */
export function SectionTitle({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <div className="mb-3 flex items-center justify-between gap-2">
      <h3 className="flex items-center gap-2 font-serif text-base text-paper-100">
        <span aria-hidden className="h-3.5 w-0.5 rounded-full bg-gold-500" />
        {children}
      </h3>
      {action}
    </div>
  );
}
