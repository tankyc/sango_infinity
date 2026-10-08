/**
 * 索引与查询工具
 *
 * 面向 850+ 规模的集合做高效检索：一次性构建 id/名称索引，
 * 供表格筛选、引用选择器、命令面板等复用。
 */
import type { CollectionKey, Entity, OptionItem, Options, Scenario } from './types'
import { COLLECTION_ORDER } from './types'

/** 各集合的 id -> 名称 索引 */
export type ReferenceNames = Record<CollectionKey, Map<number, string>>

/**
 * 构建各集合的 id -> 名称 索引。
 *
 * @param scenario 剧本
 * @returns 索引表
 */
export function buildReferenceNames(scenario: Scenario | null): ReferenceNames {
  const result = {} as ReferenceNames
  for (const key of COLLECTION_ORDER) {
    const map = new Map<number, string>()
    if (scenario) {
      const set = (scenario[key] || {}) as Record<string, Entity>
      for (const item of Object.values(set)) {
        const id = Number(item?.Id)
        if (Number.isFinite(id)) map.set(id, String(item.Name ?? `#${id}`))
      }
    }
    result[key] = map
  }
  return result
}

/**
 * 把数据集里的条目转换为通用候选列表。
 *
 * @param list 数据表条目
 * @returns {id,name} 列表
 */
export function toItemList(list: OptionItem[] | undefined): { id: number; name: string }[] {
  if (!Array.isArray(list)) return []
  return list.map((x) => ({ id: x.id, name: x.name }))
}

/** 特技候选列表 */
export function featureItems(options: Options | null): { id: number; name: string }[] {
  return toItemList(options?.features)
}

/**
 * 取实体的关键字搜索文本（名称 + Id + 字）。
 *
 * @param entity 实体
 * @returns 小写搜索文本
 */
export function entitySearchText(entity: Entity): string {
  const parts: string[] = [String(entity.Id ?? '')]
  if (typeof entity.Name === 'string') parts.push(entity.Name)
  if (typeof entity.nickName === 'string') parts.push(entity.nickName)
  if (typeof entity.familyName === 'string') parts.push(entity.familyName)
  if (typeof entity.giveName === 'string') parts.push(entity.giveName)
  if (typeof entity.description === 'string') parts.push(entity.description)
  if (typeof entity.desc === 'string') parts.push(entity.desc)
  return parts.join(' ').toLowerCase()
}

/**
 * 按关键字筛选实体（空格分隔的多关键字为「与」关系）。
 *
 * @param entities 实体列表
 * @param keyword 关键字
 * @returns 命中的实体
 */
export function filterEntities(entities: Entity[], keyword: string): Entity[] {
  const trimmed = keyword.trim().toLowerCase()
  if (!trimmed) return entities
  const terms = trimmed.split(/\s+/)
  return entities.filter((e) => {
    const hay = entitySearchText(e)
    return terms.every((t) => hay.includes(t))
  })
}

/** 排序方向 */
export type SortDirection = 'asc' | 'desc'

/** 排序状态 */
export interface SortState {
  key: string
  direction: SortDirection
}

/**
 * 对标量值排序（数字优先，字符串按本地化比较）。
 *
 * @param entities 实体列表
 * @param key 字段键
 * @param direction 方向
 * @returns 新的排序数组
 */
export function sortEntities(entities: Entity[], key: string, direction: SortDirection): Entity[] {
  const factor = direction === 'asc' ? 1 : -1
  return [...entities].sort((a, b) => {
    const va = a[key]
    const vb = b[key]
    if (typeof va === 'number' && typeof vb === 'number') return (va - vb) * factor
    if (Array.isArray(va) && Array.isArray(vb)) {
      const na = Number(va[0])
      const nb = Number(vb[0])
      if (Number.isFinite(na) && Number.isFinite(nb)) return (na - nb) * factor
    }
    const sa = va === undefined || va === null ? '' : String(va)
    const sb = vb === undefined || vb === null ? '' : String(vb)
    return sa.localeCompare(sb, 'zh-Hans-CN') * factor
  })
}

/** 集合统计信息 */
export interface CollectionStats {
  total: number
  /** 有校验错误的实体数 */
  errorEntities: number
  /** 有校验警告的实体数 */
  warningEntities: number
}

/**
 * 计算某集合的统计信息。
 *
 * @param count 实体总数
 * @param issuesByEntity 按实体归组的问题
 * @param collection 集合键
 * @returns 统计信息
 */
export function computeCollectionStats(
  count: number,
  issuesByEntity: Map<string, { level: string }[]>,
  collection: CollectionKey
): CollectionStats {
  let errorEntities = 0
  let warningEntities = 0
  const prefix = `${collection}:`
  for (const [key, issues] of issuesByEntity) {
    if (!key.startsWith(prefix)) continue
    if (issues.some((i) => i.level === 'error')) errorEntities += 1
    else if (issues.some((i) => i.level === 'warning')) warningEntities += 1
  }
  return { total: count, errorEntities, warningEntities }
}
