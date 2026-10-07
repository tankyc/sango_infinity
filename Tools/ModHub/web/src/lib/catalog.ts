/**
 * 模组分类目录
 * 分类 key 与后端 CATEGORIES 严格对应（见 server/src/service/modService.ts），
 * 中文名与图标只在前端维护，改文案不需要动后端。
 */

export interface CategoryMeta {
  key: string;
  label: string;
  /** 列表页筛选栏的一句话说明 */
  hint: string;
}

export const CATEGORIES: CategoryMeta[] = [
  { key: 'scenario', label: '剧本', hint: '新增或改写剧本、势力与初始配置' },
  { key: 'person', label: '武将', hint: '自建武将、五维与特技调整' },
  { key: 'face', label: '头像', hint: '武将立绘、头像与界面素材' },
  { key: 'map', label: '地图', hint: '战役地图、城池地形与场景布局' },
  { key: 'model', label: '模型', hint: '武将、兵种与场景的模型资源' },
  { key: 'sound', label: '语音', hint: '战斗语音、音效与背景音乐' },
  { key: 'skill', label: '技能', hint: '战法、特技与 Buff 数值' },
  { key: 'ui', label: '界面', hint: 'UGUI 窗口与交互皮肤' },
  { key: 'mixed', label: '综合', hint: '同时包含多种内容的整包改动' },
];

const CATEGORY_MAP = new Map(CATEGORIES.map((c) => [c.key, c]));

/** 分类 key → 中文名（未知值原样返回，避免展示空白） */
export function categoryLabel(key: string | null | undefined): string {
  if (!key) return '未分类';
  return CATEGORY_MAP.get(key)?.label ?? key;
}

export function categoryHint(key: string | null | undefined): string {
  if (!key) return '';
  return CATEGORY_MAP.get(key)?.hint ?? '';
}

/** 排序 Tab（对应 建设计划.md §4.1 首页 Tab） */
export const SORT_TABS: { key: 'hot' | 'new' | 'updated' | 'trending'; label: string; hint: string }[] = [
  { key: 'hot', label: '最受欢迎', hint: '按订阅人数排序' },
  { key: 'new', label: '最新发布', hint: '按发布时间排序' },
  { key: 'updated', label: '最近更新', hint: '按内容更新时间排序' },
  { key: 'trending', label: '趋势榜', hint: '按累计下载量排序' },
];

/* ------------------------------ 管理后台元数据 ------------------------------ */

/** 用户角色（与后端 ROLES 对应） */
export const ROLES = [
  { key: 'user', label: '普通用户', variant: 'outline' as const },
  { key: 'reviewer', label: '审核员', variant: 'paper' as const },
  { key: 'admin', label: '管理员', variant: 'gold' as const },
];

export function roleLabel(key: string | null | undefined): string {
  return ROLES.find((r) => r.key === key)?.label ?? key ?? '未知';
}

export function roleVariant(key: string | null | undefined): 'outline' | 'paper' | 'gold' {
  return ROLES.find((r) => r.key === key)?.variant ?? 'outline';
}

/** 模组上架状态（与后端 STATUSES 对应） */
export const MOD_STATUSES = [
  { key: 'pending', label: '待审核', variant: 'gold' as const },
  { key: 'approved', label: '已上架', variant: 'bamboo' as const },
  { key: 'rejected', label: '已拒绝', variant: 'cinnabar' as const },
  { key: 'hidden', label: '已下架', variant: 'outline' as const },
];

export function modStatusMeta(key: string | null | undefined): { label: string; variant: 'gold' | 'bamboo' | 'cinnabar' | 'outline' } {
  return MOD_STATUSES.find((s) => s.key === key) ?? { label: key ?? '未知', variant: 'outline' };
}
