import { BookOpen, Box, Boxes, Map, MonitorSmartphone, Music, Palette, ScrollText, Swords, type LucideIcon } from 'lucide-react';
import { cn } from '../../lib/cn';

/**
 * 分类图标
 * 图标与分类一一对应，供筛选栏、卡片封面占位、详情页标签共用；
 * 全站图标只来自 lucide-react，绝不使用 emoji。
 */
const ICONS: Record<string, LucideIcon> = {
  scenario: ScrollText,
  person: Swords,
  face: Palette,
  map: Map,
  model: Box,
  sound: Music,
  skill: BookOpen,
  ui: MonitorSmartphone,
  mixed: Boxes,
};

export function CategoryIcon({ category, className }: { category: string | null | undefined; className?: string }) {
  const Icon = ICONS[category ?? ''] ?? Boxes;
  return <Icon aria-hidden className={cn('h-4 w-4', className)} />;
}
