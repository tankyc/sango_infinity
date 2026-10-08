import { Link } from 'react-router-dom';
import { Sparkles } from 'lucide-react';
import type { ModListItem } from '../../lib/api';
import { categoryLabel } from '../../lib/catalog';
import { formatCount } from '../../lib/format';
import { stripMarkdown } from '../../lib/miniMarkdown';
import { Badge } from '../ui/Badge';
import { CategoryIcon } from '../ui/CategoryIcon';
import { PosterImage } from './PosterImage';
import { modDetailPath } from './ModCard';

/**
 * 精选大卡区（对应 建设计划.md §4.1 首页「精选大卡 ×3」）
 * 取当前排序结果的前三条：大封面 + 底部渐变压字，
 * 用较少的视觉预算把「最值得点的内容」推到首屏。
 */
export function FeaturedStrip({ mods }: { mods: ModListItem[] }) {
  const featured = mods.slice(0, 3);
  if (featured.length === 0) return null;

  return (
    <section aria-label="精选推荐">
      <div className="mb-3 flex items-center gap-2">
        <Sparkles aria-hidden className="h-4 w-4 text-gold-400" />
        <h2 className="font-serif text-lg text-paper-100">精选推荐</h2>
        <span className="hidden text-xs text-paper-500 sm:inline">
          当前筛选下订阅与好评综合最高的三个模组
        </span>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        {featured.map((mod) => (
          <Link
            key={mod.id}
            to={modDetailPath(mod.id)}
            className="group relative block cursor-pointer overflow-hidden rounded-xl border border-gold-900/70 transition-all duration-200 hover:border-gold-600/70 hover:shadow-gold"
          >
            <PosterImage
              src={mod.posterUrl}
              alt={`${mod.name} 的封面`}
              category={mod.category}
              className="aspect-video w-full"
            />
            <div className="absolute inset-x-0 bottom-0 bg-ink-fade p-3 pt-10">
              <div className="flex items-center gap-2">
                <Badge variant="gold" icon={<CategoryIcon category={mod.category} className="h-3 w-3" />}>
                  {categoryLabel(mod.category)}
                </Badge>
                <span className="text-[11px] text-paper-400">
                  {formatCount(mod.stats.subscribers)} 人订阅
                </span>
              </div>
              <h3 className="clamp-2 mt-1.5 font-serif text-lg leading-snug text-paper-100 transition-colors duration-200 group-hover:text-gold-300">
                {mod.name}
              </h3>
              <p className="clamp-2 mt-1 text-xs leading-relaxed text-paper-400">
                {stripMarkdown(mod.summary) || '作者还没有填写简介'}
              </p>
            </div>
          </Link>
        ))}
      </div>
    </section>
  );
}
