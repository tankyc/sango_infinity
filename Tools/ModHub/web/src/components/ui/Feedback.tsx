import type { ReactNode } from 'react';
import { AlertTriangle, Inbox, Loader2, RefreshCw } from 'lucide-react';
import { Button } from './Button';
import { cn } from '../../lib/cn';

/**
 * 状态反馈组件
 * 工坊里「加载中 / 空结果 / 请求失败」出现频率极高，统一成三个组件，
 * 保证每种状态的视觉与文案口径一致，也避免各页面各写一套。
 */

export function Spinner({ className }: { className?: string }) {
  return <Loader2 aria-hidden className={cn('h-5 w-5 animate-spin text-gold-400', className)} />;
}

export function LoadingBlock({ text = '正在载入…' }: { text?: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-sm text-paper-500">
      <Spinner />
      {text}
    </div>
  );
}

/** 骨架屏：列表首屏与详情页复用，避免加载时布局跳动 */
export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('skeleton', className)} aria-hidden />;
}

export function CardSkeletonGrid({ count = 8 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="panel overflow-hidden">
          <Skeleton className="aspect-video w-full rounded-none" />
          <div className="space-y-2 p-3">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-3 w-1/2" />
          </div>
        </div>
      ))}
    </div>
  );
}

export function EmptyState({
  title,
  description,
  action,
  icon,
  illustration = false,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
  icon?: ReactNode;
  /** 用卷轴水墨插画代替默认图标（主列表这类大面积空状态才值得用） */
  illustration?: boolean;
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed border-ink-500/70 px-6 py-16 text-center">
      {illustration ? (
        <img
          src="/art/empty-scroll.jpg"
          alt=""
          aria-hidden
          className="mb-1 h-28 w-28 rounded-xl object-cover opacity-[var(--art-opacity)] [filter:brightness(var(--art-brightness))_saturate(1.05)] sm:h-36 sm:w-36"
        />
      ) : (
        <span className="text-paper-600">{icon ?? <Inbox aria-hidden className="h-8 w-8" />}</span>
      )}
      <p className="font-serif text-lg text-paper-200">{title}</p>
      {description ? <p className="max-w-md text-sm text-paper-500">{description}</p> : null}
      {action}
    </div>
  );
}

export function ErrorState({
  message,
  details,
  onRetry,
}: {
  message: string;
  details?: string[];
  onRetry?: () => void;
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border border-cinnabar-500/40 bg-cinnabar-500/5 px-6 py-10 text-center">
      <AlertTriangle aria-hidden className="h-7 w-7 text-cinnabar-400" />
      <p className="text-sm text-cinnabar-400">{message}</p>
      {details && details.length > 0 ? (
        <ul className="max-w-lg list-inside list-disc space-y-1 text-left text-xs text-paper-400">
          {details.map((d) => (
            <li key={d}>{d}</li>
          ))}
        </ul>
      ) : null}
      {onRetry ? (
        <Button variant="outline" size="sm" icon={<RefreshCw className="h-3.5 w-3.5" aria-hidden />} onClick={onRetry}>
          重试
        </Button>
      ) : null}
    </div>
  );
}
