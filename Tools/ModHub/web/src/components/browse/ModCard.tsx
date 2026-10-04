import { Link } from 'react-router-dom';
import { HardDrive, Heart, UserRound, Users } from 'lucide-react';
import type { ModListItem } from '../../lib/api';
import { formatBytes, formatCount } from '../../lib/format';
import { stripMarkdown } from '../../lib/miniMarkdown';
import { PosterImage } from './PosterImage';

/**
 * 模组卡片（列表网格的基本单元）
 * 信息取舍照 Steam：封面 → 名称 → 简介 → 作者/版本 → 订阅/好评/大小。
 * 整卡可点：外层是 Link，hover 时仅改变颜色与描边，不做位移，避免网格抖动。
 */

/** 详情页路由（刻意沿用 Steam 的路径形态，便于社区口口相传） */
export function modDetailPath(id: string): string {
  return `/sharedfiles/filedetails/?id=${encodeURIComponent(id)}`;
}

export function ModCard({ mod }: { mod: ModListItem }) {
  return (
    <Link
      to={modDetailPath(mod.id)}
      className="group flex cursor-pointer flex-col overflow-hidden rounded-xl border border-gold-900/60 bg-ink-800/80 transition-all duration-200 hover:border-gold-600/70 hover:shadow-gold"
    >
      <PosterImage
        src={mod.posterUrl}
        alt={`${mod.name} 的封面`}
        category={mod.category}
        className="aspect-video w-full"
      />

      <div className="flex flex-1 flex-col gap-2 p-3">
        <h3 className="clamp-2 font-serif text-[15px] leading-snug text-paper-100 transition-colors duration-200 group-hover:text-gold-300">
          {mod.name}
        </h3>
        {/* 摘要位只放纯文本：作者若把 Markdown 填进「一句话简介」，这里先剥掉记号再显示 */}
        <p className="clamp-2 text-xs leading-relaxed text-paper-500">
          {stripMarkdown(mod.summary) || '作者还没有填写简介'}
        </p>

        <div className="mt-auto space-y-1.5 pt-1.5">
          <div className="flex items-center gap-1.5 text-[11px] text-paper-500">
            <UserRound aria-hidden className="h-3 w-3" />
            <span className="truncate">{mod.author.nickname}</span>
            <span aria-hidden className="text-paper-600">
              ·
            </span>
            <span className="font-mono text-paper-400">{mod.version ?? '—'}</span>
          </div>

          <div className="flex items-center gap-3 text-[11px] text-paper-500">
            <span className="flex items-center gap-1" title="订阅人数">
              <Users aria-hidden className="h-3 w-3 text-gold-500" />
              {formatCount(mod.stats.subscribers)}
            </span>
            <span className="flex items-center gap-1" title="好评数">
              <Heart aria-hidden className="h-3 w-3 text-cinnabar-400" />
              {formatCount(mod.stats.likes)}
            </span>
            <span className="ml-auto flex items-center gap-1" title="模组包大小">
              <HardDrive aria-hidden className="h-3 w-3" />
              {formatBytes(mod.size)}
            </span>
          </div>
        </div>
      </div>
    </Link>
  );
}
