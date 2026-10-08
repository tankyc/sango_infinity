import { useState } from 'react';
import { CategoryIcon } from '../ui/CategoryIcon';
import { categoryLabel } from '../../lib/catalog';
import { cn } from '../../lib/cn';

/**
 * 封面图
 * 三重兜底：无封面 / 加载失败 / 图片为空 → 一律退化为「分类图标 + 分类名」的占位块，
 * 保证卡片网格不会出现破图或高度塌陷。
 *
 * 性能：一律 loading="lazy" + decoding="async"，
 * 列表页一次可能渲染数十张封面，必须避免同步解码阻塞主线程。
 */
export function PosterImage({
  src,
  alt,
  category,
  className,
}: {
  src: string | null | undefined;
  alt: string;
  category: string;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);
  const usable = typeof src === 'string' && src !== '' && !failed;

  return (
    <div className={cn('relative overflow-hidden bg-ink-700', className)}>
      {usable ? (
        <img
          src={src}
          alt={alt}
          loading="lazy"
          decoding="async"
          onError={() => setFailed(true)}
          className="h-full w-full object-cover"
        />
      ) : (
        <div className="paper-grain flex h-full w-full flex-col items-center justify-center gap-1.5 text-paper-600">
          <CategoryIcon category={category} className="h-7 w-7" />
          <span className="font-serif text-xs">{categoryLabel(category)}</span>
        </div>
      )}
    </div>
  );
}
