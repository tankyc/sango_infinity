import type { Prisma } from '@prisma/client';
import { prisma } from '../db';
import { CATEGORIES, latestVersionString } from './modService';

/**
 * 浏览与筛选服务（对应 建设计划.md §4.7 分页与性能、§8.2 工坊业务 API）
 *
 * 设计要点：
 *   1. 统一游标分页（keyset pagination），不用 OFFSET —— 避免深翻页时"翻到后面越来越慢"
 *      与"翻页期间有新内容插入导致条目重复/漏出"两个经典问题。
 *   2. 每个排序都固定为「一级排序字段 DESC + id DESC」两级，游标里同时带上这两个值，
 *      翻页时用 (一级字段, id) < (上页末值, 上页末 id) 定位，结果稳定且可预期。
 *   3. 列表只返回卡片所需字段（封面、名称、作者、版本、大小、统计），
 *      详情（简介正文、版本历史明细、内容透视）一律留给详情接口，避免列表接口变重。
 */

/** 支持排序方式（对应首页 Tab：精选 / 最受欢迎 / 最新发布 / 最近更新 / 趋势榜） */
export const SORT_KEYS = ['hot', 'new', 'updated', 'trending'] as const;
export type SortKey = (typeof SORT_KEYS)[number];

/** 排序方式 → 一级排序字段（全部 DESC）。id 作为固定的二级排序键 */
const SORT_FIELD: Record<SortKey, 'subscribers' | 'createdAt' | 'updatedAt' | 'downloads'> = {
  hot: 'subscribers',
  new: 'createdAt',
  updated: 'updatedAt',
  trending: 'downloads',
};

/** 是否时间型字段（用于游标值还原） */
function isDateField(field: string): boolean {
  return field === 'createdAt' || field === 'updatedAt';
}

export function isSortKey(v: string | undefined): v is SortKey {
  return v != null && (SORT_KEYS as readonly string[]).includes(v);
}

export interface BrowseQuery {
  cursor?: string;
  limit?: number;
  q?: string;
  category?: string;
  tags?: string[];
  gameVersion?: string;
  sort?: SortKey;
}

export interface BrowseListItem {
  id: string;
  name: string;
  summary: string;
  category: string;
  posterUrl: string | null;
  gameVersion: string;
  tags: string[];
  author: { id: number; username: string; nickname: string; avatar: string | null };
  version: string | null;
  size: number;
  createdAt: Date;
  updatedAt: Date;
  stats: { subscribers: number; likes: number; views: number; downloads: number };
}

export interface BrowseResult {
  items: BrowseListItem[];
  nextCursor: string | null;
  total: number;
  sort: SortKey;
}

const DEFAULT_LIMIT = 24;
const MAX_LIMIT = 48;

function clampLimit(limit: number | undefined): number {
  if (limit == null || !Number.isFinite(limit)) return DEFAULT_LIMIT;
  return Math.min(MAX_LIMIT, Math.max(1, Math.floor(limit)));
}

/** 游标载荷：一级排序值 + id，另记排序方式防止换 Tab 后沿用旧游标 */
interface CursorPayload {
  sort: SortKey;
  v: string;
  id: string;
}

export function encodeCursor(payload: CursorPayload): string {
  return Buffer.from(JSON.stringify(payload), 'utf8').toString('base64url');
}

export function decodeCursor(raw: string | undefined): CursorPayload | null {
  if (!raw) return null;
  try {
    const parsed = JSON.parse(Buffer.from(raw, 'base64url').toString('utf8')) as CursorPayload;
    if (!parsed || typeof parsed.id !== 'string' || parsed.v == null) return null;
    return parsed;
  } catch {
    return null;
  }
}

/** 组装筛选条件（不含游标），搜索/分类/标签/游戏版本都进 AND 数组，避免与游标的 OR 冲突 */
function buildFilterClauses(query: BrowseQuery): Prisma.ModWhereInput[] {
  const clauses: Prisma.ModWhereInput[] = [];

  const q = (query.q ?? '').trim();
  if (q !== '') {
    clauses.push({
      OR: [
        { name: { contains: q } },
        { summary: { contains: q } },
        { id: { contains: q } },
      ],
    });
  }

  const category = (query.category ?? '').trim();
  if (category !== '' && (CATEGORIES as readonly string[]).includes(category)) {
    clauses.push({ category });
  }

  const tags = (query.tags ?? []).map((t) => t.trim()).filter((t) => t !== '');
  if (tags.length > 0) {
    // 多标签为「或」语义：命中任意一个即入选（Steam 的多标签筛选心智）
    clauses.push({ tags: { some: { tag: { in: tags } } } });
  }

  const gameVersion = (query.gameVersion ?? '').trim();
  if (gameVersion !== '' && gameVersion !== '*') {
    clauses.push({ gameVersion });
  }

  return clauses;
}

/** 用游标生成 keyset 定位条件： (一级字段, id) < (上页末值, 上页末id) */
function buildCursorClause(cursor: CursorPayload, sort: SortKey): Prisma.ModWhereInput {
  const field = SORT_FIELD[sort];
  const value: string | number | Date = isDateField(field) ? new Date(cursor.v) : Number(cursor.v);

  return {
    OR: [
      { [field]: { lt: value } },
      { AND: [{ [field]: value }, { id: { lt: cursor.id } }] },
    ],
  } as Prisma.ModWhereInput;
}

/** 单条记录 → 列表卡片 DTO */
function toListItem(mod: {
  id: string;
  name: string;
  summary: string;
  category: string;
  posterUrl: string;
  gameVersion: string;
  subscribers: number;
  likes: number;
  views: number;
  downloads: number;
  createdAt: Date;
  updatedAt: Date;
  tags: { tag: string }[];
  author: { id: number; username: string; nickname: string | null; avatar: string | null };
  versions: { version: string; size: number; createdAt: Date }[];
}): BrowseListItem {
  const latest = latestVersionString(mod.versions);
  const latestRow = mod.versions.find((v) => v.version === latest);

  return {
    id: mod.id,
    name: mod.name,
    summary: mod.summary,
    category: mod.category,
    posterUrl: mod.posterUrl === '' ? null : mod.posterUrl,
    gameVersion: mod.gameVersion,
    tags: mod.tags.map((t) => t.tag),
    author: {
      id: mod.author.id,
      username: mod.author.username,
      nickname: mod.author.nickname ?? mod.author.username,
      avatar: mod.author.avatar,
    },
    version: latest ?? null,
    size: latestRow?.size ?? 0,
    createdAt: mod.createdAt,
    updatedAt: mod.updatedAt,
    stats: {
      subscribers: mod.subscribers,
      likes: mod.likes,
      views: mod.views,
      downloads: mod.downloads,
    },
  };
}

/** 浏览查询：筛选 + 排序 + 游标分页 */
export async function browseMods(query: BrowseQuery): Promise<BrowseResult> {
  const sort: SortKey = isSortKey(query.sort) ? query.sort : 'hot';
  const limit = clampLimit(query.limit);
  const field = SORT_FIELD[sort];

  const clauses = buildFilterClauses(query);
  const baseWhere: Prisma.ModWhereInput = { status: 'approved', AND: clauses };

  const where: Prisma.ModWhereInput = { ...baseWhere };
  const cursor = decodeCursor(query.cursor);
  // 换了排序方式就不认旧游标（否则定位条件与排序字段不匹配，会翻出错误结果）
  if (cursor && cursor.sort === sort) {
    where.AND = [...clauses, buildCursorClause(cursor, sort)];
  }

  const orderBy = [{ [field]: 'desc' }, { id: 'desc' }] as Prisma.ModOrderByWithRelationInput[];

  const [rows, total] = await Promise.all([
    prisma.mod.findMany({
      where,
      orderBy,
      // 多取一条用于判断是否还有下一页，避免额外一次 count
      take: limit + 1,
      include: {
        tags: true,
        author: { select: { id: true, username: true, nickname: true, avatar: true } },
        versions: {
          select: { version: true, size: true, createdAt: true },
          orderBy: { createdAt: 'desc' },
        },
      },
    }),
    prisma.mod.count({ where: baseWhere }),
  ]);

  const hasMore = rows.length > limit;
  const pageRows = hasMore ? rows.slice(0, limit) : rows;
  const last = pageRows[pageRows.length - 1];

  let nextCursor: string | null = null;
  if (hasMore && last) {
    const lastValue = last[field];
    nextCursor = encodeCursor({
      sort,
      v: lastValue instanceof Date ? lastValue.toISOString() : String(lastValue),
      id: last.id,
    });
  }

  return {
    items: pageRows.map(toListItem),
    nextCursor,
    total,
    sort,
  };
}

export interface FilterOptions {
  categories: { key: string; count: number }[];
  tags: { tag: string; count: number }[];
  gameVersions: string[];
}

/** 筛选栏数据：类型计数 + 热门标签 + 可选的游戏版本 */
export async function listFilterOptions(): Promise<FilterOptions> {
  const [categoryGroups, tagGroups, versionRows] = await Promise.all([
    prisma.mod.groupBy({
      by: ['category'],
      where: { status: 'approved' },
      _count: { _all: true },
    }),
    prisma.modTag.groupBy({
      by: ['tag'],
      _count: { _all: true },
      orderBy: { _count: { tag: 'desc' } },
      take: 60,
    }),
    prisma.mod.findMany({
      where: { status: 'approved' },
      select: { gameVersion: true },
      distinct: ['gameVersion'],
    }),
  ]);

  const countMap = new Map(categoryGroups.map((g) => [g.category, g._count._all]));

  return {
    // 固定按 CATEGORIES 顺序输出，未出现过的类型计数补 0，保证前端筛选栏顺序稳定
    categories: CATEGORIES.map((key) => ({ key, count: countMap.get(key) ?? 0 })),
    tags: tagGroups.map((g) => ({ tag: g.tag, count: g._count._all })),
    gameVersions: versionRows
      .map((r) => r.gameVersion)
      .filter((v) => v !== '' && v !== '*')
      .sort(),
  };
}
