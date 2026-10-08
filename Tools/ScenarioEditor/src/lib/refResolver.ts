/**
 * 引用解析器
 *
 * 把「字段值是某个 ID」统一解析成人类可读的名称。数据来源有两类：
 * - 剧本内的集合（势力 / 军团 / 都市 / 武将）—— 会随编辑实时变化；
 * - 公共数据表的选项列表（技巧、特技、物品、建筑…）—— 来自后端 /api/options。
 *
 * 汇总成一个 resolver 后，表格、详情表单、公共数据表编辑器都能共用同一套名称转换，
 * 避免各处重复实现导致显示口径不一致。
 */
import type { CollectionKey, Entity, Options, Scenario } from './types'
import type { RefSpec } from './commonMeta'

/**
 * 取实体的展示名。
 *
 * 势力与军团在剧本里没有 Name 字段，展示名需要由君主 / 军团长推导，
 * 否则列表里只会出现一串数字。这里统一兜底。
 *
 * @param entity 实体
 * @param collection 所属集合
 * @param names 名称表
 * @returns 展示名
 */
export function entityDisplayName(
  entity: Entity,
  collection: CollectionKey,
  names: Record<CollectionKey, Map<number, string>>
): string {
  const own = entity.Name
  if (typeof own === 'string' && own.trim().length > 0) return own
  const derived = names[collection]?.get(Number(entity.Id))
  if (derived && derived.trim().length > 0) return derived
  return `#${entity.Id}`
}

/** 引用解析器 */
export interface RefResolver {
  /** 取某个引用目标的 id -> 名称 映射 */
  getMap: (spec: RefSpec) => Map<number, string>
  /** 取某个引用的名称 */
  getName: (spec: RefSpec, id: number) => string | undefined
  /** 剧本集合的 id -> 名称（表格里最常用） */
  scenario: Record<CollectionKey, Map<number, string>>
}

/** 空实现的解析器（数据尚未就绪时使用） */
const EMPTY_MAP = new Map<number, string>()

/**
 * 构建四个集合的 id -> 名称 表。
 *
 * 有两个坑需要在这里统一处理：
 * - 势力（forceSet）在剧本里没有 Name 字段，游戏用「君主名」作为势力名；
 * - 军团（corpsSet）同样没有 Name，游戏用「番号 / 军团长」表示。
 * 若这里不兜底，表格会把势力列显示成孤零零的数字。
 *
 * @param scenario 剧本数据
 * @returns 四个集合的名称表
 */
export function buildScenarioNameMaps(scenario: Scenario | null): Record<CollectionKey, Map<number, string>> {
  const personName = new Map<number, string>()
  const cityName = new Map<number, string>()
  const corpsName = new Map<number, string>()
  const forceName = new Map<number, string>()

  if (scenario) {
    const persons = (scenario.personSet ?? {}) as Record<string, Entity>
    for (const item of Object.values(persons)) {
      personName.set(Number(item.Id), String(item.Name ?? `武将${item.Id}`))
    }
    for (const item of Object.values((scenario.citySet ?? {}) as Record<string, Entity>)) {
      cityName.set(Number(item.Id), String(item.Name ?? `都市${item.Id}`))
    }
    for (const item of Object.values((scenario.corpsSet ?? {}) as Record<string, Entity>)) {
      const id = Number(item.Id)
      const commander = personName.get(Number(item.Comander))
      const number = item.number === undefined ? '' : `${item.number}军`
      corpsName.set(id, String(item.Name ?? (commander ? `${number}·${commander}` : `军团${id}`)))
    }
    for (const item of Object.values((scenario.forceSet ?? {}) as Record<string, Entity>)) {
      const id = Number(item.Id)
      const governor = personName.get(Number(item.Governor))
      forceName.set(id, String(item.Name ?? governor ?? `势力${id}`))
    }
  }

  return { personSet: personName, citySet: cityName, corpsSet: corpsName, forceSet: forceName }
}

/**
 * 构建引用解析器。
 *
 * @param scenario 剧本数据（可为空）
 * @param options 公共数据表选项（可为空）
 * @returns 解析器
 */
export function buildRefResolver(scenario: Scenario | null, options: Options | null): RefResolver {
  const scenarioMaps = buildScenarioNameMaps(scenario)

  const optionsCache = new Map<string, Map<number, string>>()
  const getOptionsMap = (key: string): Map<number, string> => {
    const cached = optionsCache.get(key)
    if (cached) return cached
    const list = options ? (options as unknown as Record<string, unknown>)[key] : undefined
    const map = new Map<number, string>()
    if (Array.isArray(list)) {
      for (const item of list as { id: number; name: string }[]) {
        if (item && Number.isFinite(Number(item.id))) map.set(Number(item.id), String(item.name ?? ''))
      }
    }
    optionsCache.set(key, map)
    return map
  }

  /** 静态枚举映射缓存 */
  const staticCache = new Map<string, Map<number, string>>()

  const getMap = (spec: RefSpec): Map<number, string> => {
    if (!spec) return EMPTY_MAP
    if (spec.source === 'scenario') return scenarioMaps[spec.collection] ?? EMPTY_MAP
    if (spec.source === 'static') {
      const key = spec.entries.map((e) => `${e.id}:${e.name}`).join('|')
      let map = staticCache.get(key)
      if (!map) {
        map = new Map(spec.entries.map((e) => [e.id, e.name]))
        staticCache.set(key, map)
      }
      return map
    }
    return getOptionsMap(spec.key)
  }

  return {
    scenario: scenarioMaps,
    getMap,
    getName: (spec, id) => getMap(spec).get(id),
  }
}
