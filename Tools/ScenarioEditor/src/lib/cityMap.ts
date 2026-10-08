/**
 * 都市地图数据层
 *
 * 剧本里的每座都市都带 `x` / `y` 两个整数坐标，构成一张规整的网格地图
 * （当前剧本 87 座城，x ∈ 33~215、y ∈ 44~219，坐标互不重叠）。
 * 本模块负责把这些原始字段整理成画地图直接可用的节点，并提供着色与范围计算。
 *
 * 之所以单独抽出来，是为了让「地图选择面板」「都市地图视图」与取数解耦：
 * 前者只管交互与渲染，后者只管取数与配色。
 */
import type { Options, Scenario } from './types'

/** 地图上的一个都市节点 */
export interface CityMapNode {
  /** 都市 Id */
  id: number
  /** 都市名 */
  name: string
  /** 网格横坐标 */
  x: number
  /** 网格纵坐标 */
  y: number
  /** 建筑类型（对应 BuildingTypes.Id：1 都市 / 2 关所 / 3 港） */
  type: number
  /** 建筑类型名称（取自公共数据表） */
  typeLabel: string
  /** 所属势力 Id（0 表示无归属） */
  forceId: number
  /** 所属军团 Id */
  corpsId: number
  /** 驻军数量 */
  troops: number
  /**
   * 所属都市 Id。
   *
   * 关所与港没有 `NeighborList`，它们通过 `BelongCity` 挂在所属都市上
   *（实测 45 个关所/港全部有值，且都指向 BuildingType=1 的都市；都市自身为 0）。
   * 0 表示没有归属。
   */
  belongCityId: number
  /** 相邻都市 Id 列表（关所与港通常没有） */
  neighborIds: number[]
}

/** 地图范围 */
export interface CityMapBounds {
  minX: number
  maxX: number
  minY: number
  maxY: number
  /** 网格列数 */
  cols: number
  /** 网格行数 */
  rows: number
}

/**
 * 无归属势力的颜色：纯白。
 *
 * 地图背景在亮色主题下接近白色，所以这类标记始终保留 `border-black/25` 的深色描边，
 * 保证白点仍然可见；深色主题下白点本身就很醒目。
 */
const NEUTRAL_COLOR = '#ffffff'

/**
 * 取建筑类型的名称。
 *
 * @param type 建筑类型 Id
 * @param options 公共数据表
 * @returns 类型名称
 */
export function cityTypeLabel(type: number, options: Options | null): string {
  const list = options?.buildingTypes ?? []
  const hit = list.find((b) => Number(b.id) === type)
  return hit ? String(hit.name) : `类型 ${type}`
}

/**
 * 势力的兜底颜色：黄金角散列。
 *
 * 只在拿不到旗帜颜色时使用（例如旗帜数据缺失），保证同一势力颜色稳定。
 *
 * @param forceId 势力 Id
 * @returns CSS 颜色
 */
export function forceColor(forceId: number): string {
  if (!Number.isFinite(forceId) || forceId <= 0) return NEUTRAL_COLOR
  const hue = (forceId * 137.508) % 360
  return `hsl(${hue.toFixed(0)} 60% 52%)`
}

/**
 * 取势力颜色。
 *
 * 优先用「势力 → 旗帜 → Flags.json 的 color」，这样与游戏里旗帜颜色一致；
 * 拿不到时才用黄金角散列兜底，保证同一势力颜色稳定且彼此可区分。
 *
 * @param forceId 势力 Id
 * @param map 势力 -> 颜色表（由 buildForceColorMap 生成）
 * @returns CSS 颜色
 */
export function forceColorOf(forceId: number, map: Map<number, string> | null | undefined): string {
  if (!Number.isFinite(forceId) || forceId <= 0) return NEUTRAL_COLOR
  return map?.get(forceId) ?? forceColor(forceId)
}

/**
 * 构建「势力 Id -> 旗帜颜色」映射。
 *
 * 剧本的 forceSet 只有 `Flag`（旗帜 Id），颜色定义在公共数据表 Flags.json 里，
 * 两边要拼起来才能得到地图上该用的颜色。
 *
 * @param scenario 剧本数据
 * @param options 公共数据表
 * @returns 势力颜色表
 */
export function buildForceColorMap(
  scenario: Scenario | null,
  options: Options | null
): Map<number, string> {
  const map = new Map<number, string>()
  if (!scenario) return map

  const flagColor = new Map<number, string>()
  for (const f of options?.flags ?? []) {
    const id = Number(f.id)
    const color = String(f.color ?? '').trim()
    if (Number.isFinite(id) && color) flagColor.set(id, color)
  }

  const forces = (scenario.forceSet ?? {}) as Record<string, Record<string, unknown>>
  for (const force of Object.values(forces)) {
    const id = Number(force.Id)
    if (!Number.isFinite(id)) continue
    const color = flagColor.get(Number(force.Flag))
    map.set(id, color ?? forceColor(id))
  }
  return map
}

/** 标记形状：形状是「建筑类型」的唯一视觉编码，配色留给所属势力 */
export type CityMarkerShape = 'square' | 'circle' | 'diamond'

/** 按建筑类型名称取形状（名称来自公共数据表，避免写死 Id） */
const SHAPE_BY_TYPE_NAME: Record<string, CityMarkerShape> = {
  都市: 'square',
  关所: 'circle',
  港: 'diamond',
}

/** 名称对不上时按 Id 兜底 */
const SHAPE_BY_TYPE_ID: Record<number, CityMarkerShape> = { 1: 'square', 2: 'circle', 3: 'diamond' }

/**
 * 取建筑类型对应的标记形状。
 *
 * 都市=方形、关所=圆形、港=菱形；其余类型按方形处理。
 *
 * @param type 建筑类型 Id
 * @param options 公共数据表
 * @returns 形状
 */
export function cityMarkerShape(type: number, options: Options | null): CityMarkerShape {
  const byName = SHAPE_BY_TYPE_NAME[cityTypeLabel(type, options)]
  if (byName) return byName
  return SHAPE_BY_TYPE_ID[type] ?? 'square'
}

/**
 * 由剧本数据构建地图节点。
 *
 * 坐标缺失或非数字的都市会被跳过（地图按网格绘制，无法定位）。
 *
 * @param scenario 剧本数据
 * @returns 节点列表
 */
export function buildCityNodes(scenario: Scenario | null): CityMapNode[] {
  if (!scenario) return []
  const set = (scenario.citySet ?? {}) as Record<string, Record<string, unknown>>
  const nodes: CityMapNode[] = []
  for (const raw of Object.values(set)) {
    if (!raw || typeof raw !== 'object') continue
    const x = Number(raw.x)
    const y = Number(raw.y)
    if (!Number.isFinite(x) || !Number.isFinite(y)) continue
    nodes.push({
      id: Number(raw.Id),
      name: String(raw.Name ?? `#${raw.Id}`),
      x,
      y,
      type: Number(raw.BuildingType) || 0,
      typeLabel: '',
      forceId: Number(raw.BelongForce) || 0,
      corpsId: Number(raw.BelongCorps) || 0,
      troops: Number(raw.troops) || 0,
      belongCityId: Number(raw.BelongCity) || 0,
      neighborIds: Array.isArray(raw.NeighborList)
        ? (raw.NeighborList as unknown[]).map((v) => Number(v)).filter((v) => Number.isFinite(v))
        : [],
    })
  }
  nodes.sort((a, b) => a.id - b.id)
  return nodes
}

/**
 * 补全节点上的建筑类型名称。
 *
 * 单独提供是因为 Options（公共数据表）可能在剧本之后才加载完成。
 *
 * @param nodes 节点列表（原地补全 typeLabel）
 * @param options 公共数据表
 * @returns 同一个数组，便于链式使用
 */
export function fillCityTypeLabels(nodes: CityMapNode[], options: Options | null): CityMapNode[] {
  for (const node of nodes) {
    if (!node.typeLabel) node.typeLabel = cityTypeLabel(node.type, options)
  }
  return nodes
}

/**
 * 计算地图范围。
 *
 * @param nodes 节点列表
 * @returns 范围；节点为空时返回空范围
 */
export function computeCityBounds(nodes: CityMapNode[]): CityMapBounds {
  if (nodes.length === 0) return { minX: 0, maxX: 0, minY: 0, maxY: 0, cols: 1, rows: 1 }
  let minX = Infinity
  let maxX = -Infinity
  let minY = Infinity
  let maxY = -Infinity
  for (const n of nodes) {
    if (n.x < minX) minX = n.x
    if (n.x > maxX) maxX = n.x
    if (n.y < minY) minY = n.y
    if (n.y > maxY) maxY = n.y
  }
  return { minX, maxX, minY, maxY, cols: maxX - minX + 1, rows: maxY - minY + 1 }
}

/**
 * 数字格式化（兵力等大数展示用）。
 *
 * @param value 数值
 * @returns 文本
 */
export function formatCityNumber(value: number): string {
  if (!Number.isFinite(value) || value === 0) return '0'
  if (Math.abs(value) >= 10000) return `${(value / 10000).toFixed(1)}万`
  return String(Math.round(value))
}

/**
 * 按关键字过滤节点（名称或 Id 命中）。
 *
 * @param nodes 节点列表
 * @param query 关键字
 * @returns 命中的节点集合
 */
export function matchCityNodes(nodes: CityMapNode[], query: string): Set<number> {
  const kw = query.trim().toLowerCase()
  if (!kw) return new Set(nodes.map((n) => n.id))
  const hit = new Set<number>()
  for (const n of nodes) {
    if (String(n.id).includes(kw) || n.name.toLowerCase().includes(kw)) hit.add(n.id)
  }
  return hit
}
