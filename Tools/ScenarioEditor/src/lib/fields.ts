/**
 * 字段读写工具
 *
 * 剧本中的五维与兵种适性使用「数组编码」，且不同来源（剧本 / 武将库）的
 * 数组长度不一致。本模块负责在「数组编码」与「界面友好标量」之间转换，
 * 并保证写回时保留原始数组长度，避免破坏游戏侧的兜底解析逻辑。
 *
 * - 五维 command/strength/... ：[基础值, 成长类型Id, 经验, 万分比, 最终值]
 * - 适性 spearLv/...          ：[基础等级, 经验, 计算等级]
 */
import type { CollectionKey, Entity, OptionItem, Options } from './types'
import type { FieldDef, EnumEntry } from './schema'
import { resolveEnum } from './schema'

/** 读取五维的基础值 */
export function getAttrBase(value: unknown): number {
  if (Array.isArray(value)) {
    const n = Number(value[0])
    return Number.isFinite(n) ? n : 0
  }
  const n = Number(value)
  return Number.isFinite(n) ? n : 0
}

/**
 * 五维数值的评级配色。
 *
 * 数值越高越显眼，方便在一屏之内扫出强将：
 * `≥95` 红、`≥90` 暗金、`≥80` 紫、`≥70` 蓝、`≥60` 绿，其余保持默认色。
 *
 * @param value 五维基础值
 * @returns CSS 颜色；未达门槛返回 undefined
 */
export function attrQualityColor(value: number): string | undefined {
  if (!Number.isFinite(value)) return undefined
  if (value >= 95) return '#ef4444'
  if (value >= 90) return '#b8860b'
  if (value >= 80) return '#a855f7'
  if (value >= 70) return '#3b82f6'
  if (value >= 60) return '#22c55e'
  return undefined
}

/**
 * 兵种适性的档位配色。
 *
 * 按档位**名称**归类而非硬编码 Id，这样 `AbilityLevelTypes.json` 里
 * 新增或改名的档位（如 Ｓ１〜Ｓ４）能自动继承配色，无需改代码；
 * 同时全角 / 半角都认，避免数据源用不同字符时失配。
 *
 * - `Ｓ` / `Ｓ１` … `Ｓ４` → 红
 * - `Ａ` → 紫
 * - `Ｂ` → 绿
 * - `Ｃ` 及未列出的档位 → 保持默认色
 *
 * 颜色与五维评级色取自同一套语义：红 = 最强、紫 = 强、绿 = 中等，
 * 因此同一屏里看到红色既可能是「统率 95+」也可能是「骑兵适性 Ｓ」，含义一致。
 *
 * @param label 档位名称（如 `Ｓ`、`Ａ`、`B`）
 * @returns CSS 颜色；不配色时返回 undefined
 */
export function abilityQualityColor(label: string): string | undefined {
  const head = label.trim().charAt(0)
  if (head === 'Ｓ' || head === 'S') return '#ef4444'
  if (head === 'Ａ' || head === 'A') return '#a855f7'
  if (head === 'Ｂ' || head === 'B') return '#22c55e'
  return undefined
}

/** 读取五维的成长类型 Id（数组第 2 位；0 在游戏中等价于 5） */
export function getAttrChangeId(value: unknown): number {
  if (Array.isArray(value) && value.length > 1) {
    const n = Number(value[1])
    return Number.isFinite(n) ? n : 5
  }
  return 5
}

/**
 * 写回五维：保留原数组长度，仅更新前两位。
 *
 * @param value 原数组
 * @param base 基础值
 * @param changeId 成长类型 Id
 * @returns 新的数组
 */
export function setAttr(value: unknown, base: number, changeId: number): number[] {
  const arr = Array.isArray(value) ? [...(value as number[])] : [base, changeId]
  while (arr.length < 2) arr.push(0)
  arr[0] = base
  arr[1] = changeId
  return arr
}

/** 读取兵种适性等级 */
export function getAbilityLevel(value: unknown): number {
  if (Array.isArray(value)) {
    const n = Number(value[0])
    return Number.isFinite(n) ? n : 0
  }
  const n = Number(value)
  return Number.isFinite(n) ? n : 0
}

/**
 * 写回兵种适性：保留原数组长度，仅更新第 1 位等级。
 *
 * @param value 原数组
 * @param level 适性等级
 * @returns 新的数组
 */
export function setAbilityLevel(value: unknown, level: number): number[] {
  const arr = Array.isArray(value) ? [...(value as number[])] : [level]
  if (arr.length === 0) arr.push(level)
  arr[0] = level
  return arr
}

/** 读取数组型字段（非数组返回空数组） */
export function getArray(value: unknown): number[] {
  if (Array.isArray(value)) return value.map((x) => (typeof x === 'number' ? x : Number(x) || 0))
  return []
}

/**
 * 取字段在界面上的「标量展示值」。
 *
 * @param entity 实体
 * @param field 字段定义
 * @returns 标量值（字符串或数字）
 */
export function getScalarValue(entity: Entity, field: FieldDef): number | string {
  const raw = entity[field.key]
  switch (field.kind) {
    case 'attr':
      return getAttrBase(raw)
    case 'ability':
      return getAbilityLevel(raw)
    case 'pairArray':
    case 'intArray':
    case 'polyRef':
      return getArray(raw).join(',')
    case 'string':
      return typeof raw === 'string' ? raw : raw === undefined || raw === null ? '' : String(raw)
    case 'bool':
      return raw ? 1 : 0
    default: {
      if (typeof raw === 'number') return raw
      if (raw === undefined || raw === null || raw === '') return ''
      const n = Number(raw)
      return Number.isFinite(n) ? n : String(raw)
    }
  }
}

/**
 * 生成写回实体用的字段值。
 *
 * @param entity 原实体（用于读取原数组长度等上下文）
 * @param field 字段定义
 * @param input 用户输入（字符串或数字）
 * @returns 可直接写入实体的值
 */
export function buildFieldValue(
  entity: Entity,
  field: FieldDef,
  input: string | number
): unknown {
  const raw = entity[field.key]
  switch (field.kind) {
    case 'attr': {
      const base = toInt(input, getAttrBase(raw))
      return setAttr(raw, base, getAttrChangeId(raw))
    }
    case 'ability': {
      const level = toInt(input, getAbilityLevel(raw))
      return setAbilityLevel(raw, level)
    }
    case 'string':
      return typeof input === 'string' ? input : String(input)
    case 'bool':
      return Boolean(input)
    case 'intArray':
    case 'polyRef':
      return parseNumberList(input)
    case 'pairArray':
      return parseNumberList(input)
    default:
      return toInt(input, Number(raw) || 0)
  }
}

/** 把输入安全转换为整数 */
export function toInt(input: string | number, fallback = 0): number {
  if (typeof input === 'number') return Number.isFinite(input) ? Math.trunc(input) : fallback
  const trimmed = input.trim()
  if (trimmed === '' || trimmed === '-') return fallback
  const n = Number(trimmed)
  return Number.isFinite(n) ? Math.trunc(n) : fallback
}

/**
 * 解析逗号（或空格/顿号）分隔的数字列表。
 *
 * @param input 输入字符串或数字
 * @returns 数字数组
 */
export function parseNumberList(input: string | number): number[] {
  if (typeof input === 'number') return [Math.trunc(input)]
  return input
    .split(/[,，\s;；、]+/)
    .map((s) => s.trim())
    .filter((s) => s !== '')
    .map((s) => Math.trunc(Number(s)))
    .filter((n) => Number.isFinite(n))
}

/** 名称映射表：id -> 名称 */
export type NameMap = Map<number, string>

/** 引用名称查找表：集合 / 选项表 -> id -> 名称 */
export interface NameLookup {
  /** 剧本集合的名称表 */
  scenario?: Partial<Record<CollectionKey, NameMap>>
  /** 公共数据表的名称表 */
  optionsKey?: Partial<Record<string, NameMap>>
}

/**
 * 由「剧本集合名称表 + 公共数据表选项」构建引用名称查找表。
 *
 * 公共数据表的每一项都会被转成 id -> 名称 的 Map，
 * 因此字段只要声明了 optionsKey / itemOptionsKey 就能自动显示名称。
 *
 * @param scenarioNames 剧本集合名称表
 * @param options 公共数据表选项
 * @returns 名称查找表
 */
export function buildNameLookup(
  scenarioNames: Record<CollectionKey, NameMap> | null | undefined,
  options: Options | null
): NameLookup {
  const optionsKey: Record<string, NameMap> = {}
  if (options) {
    for (const [key, value] of Object.entries(options as unknown as Record<string, unknown>)) {
      if (!Array.isArray(value)) continue
      const map = new Map<number, string>()
      for (const item of value as OptionItem[]) {
        const id = Number(item?.id)
        if (Number.isFinite(id)) map.set(id, String(item?.name ?? ''))
      }
      optionsKey[key] = map
    }
  }
  return { scenario: scenarioNames ?? undefined, optionsKey }
}

/**
 * 查一个引用 id 对应的名称。
 *
 * @param field 字段定义
 * @param id 引用 id
 * @param lookup 名称查找表
 * @param options 公共数据表（用于回退到 optionsKey）
 * @returns 名称；找不到返回 undefined
 */
export function lookupRefName(
  field: FieldDef,
  id: number,
  lookup?: NameLookup | null,
  options?: Options | null
): string | undefined {
  if (field.ref && lookup?.scenario?.[field.ref]) {
    return lookup.scenario[field.ref]?.get(id)
  }
  if (field.itemRef && lookup?.scenario?.[field.itemRef]) {
    return lookup.scenario[field.itemRef]?.get(id)
  }
  const optionKey = field.optionsKey ?? field.itemOptionsKey
  if (optionKey) {
    const direct = lookup?.optionsKey?.[optionKey]?.get(id)
    if (direct) return direct
    const list = options ? ((options as unknown as Record<string, unknown>)[optionKey] as OptionItem[] | undefined) : undefined
    if (Array.isArray(list)) {
      const hit = list.find((x) => Number(x.id) === id)
      if (hit) return String(hit.name ?? '')
    }
  }
  return undefined
}

/**
 * 把数组型字段格式化为紧凑文本。
 *
 * - pairArray 以 `名称x数量` 形式展示（无名称时退化为 `类型x数量`）
 * - polyRef / 带 itemOptionsKey 的数组优先展示名称，名称过多时截断
 *
 * @param value 原值
 * @param field 字段定义
 * @param lookup 名称查找表
 * @param options 公共数据表
 * @returns 展示文本
 */
export function formatArrayValue(
  value: unknown,
  field: FieldDef,
  lookup?: NameLookup | null,
  options?: Options | null
): string {
  const arr = getArray(value)
  const nameOf = (id: number) => lookupRefName(field, id, lookup, options)

  // 静态枚举数组（如舌战话术）直接把下标翻译成名称
  if (field.itemEnumMap && field.itemEnumMap.length > 0) {
    return arr.map((n) => field.itemEnumMap?.find((e) => e.value === n)?.label ?? String(n)).join('、')
  }

  if (field.kind === 'pairArray') {
    const parts: string[] = []
    for (let i = 0; i + 1 < arr.length; i += 2) {
      const label = nameOf(arr[i])
      parts.push(`${label ?? arr[i]}×${arr[i + 1]}`)
    }
    if (arr.length % 2 === 1) parts.push(`${arr[arr.length - 1]}`)
    return parts.join('、')
  }

  const hasNames = arr.some((n) => nameOf(n) !== undefined)
  if (!hasNames) return arr.join(',')

  const MAX_SHOW = 4
  const shown = arr.slice(0, MAX_SHOW).map((n) => nameOf(n) ?? String(n))
  return shown.join('、') + (arr.length > MAX_SHOW ? ` 等 ${arr.length} 个` : '')
}

/**
 * 生成字段的展示文本。
 *
 * 解析顺序（先能确定的先转换）：
 * 1. 剧本集合引用（势力 / 军团 / 都市 / 武将）-> 名称；
 * 2. 公共数据表引用（技巧 / 特技 / 官职 / 都市规模…）-> 名称；
 * 3. 硬编码枚举 -> 枚举标签；
 * 4. 兜底 -> 原始值。
 *
 * @param entity 实体
 * @param field 字段定义
 * @param options 公共数据表
 * @param lookup 名称查找表
 * @returns 展示文本
 */
export function formatFieldValue(
  entity: Entity,
  field: FieldDef,
  options: Options | null,
  lookup?: NameLookup | null
): string {
  const raw = entity[field.key]
  if (raw === undefined || raw === null) return ''
  if (field.kind === 'string') return String(raw)
  if (field.kind === 'attr') return String(getAttrBase(raw))
  if (field.kind === 'ability') {
    const level = getAbilityLevel(raw)
    const map = resolveEnum(field, options)
    const hit = map.find((m: EnumEntry) => m.value === level)
    return hit ? hit.label : String(level)
  }
  if (field.kind === 'intArray' || field.kind === 'polyRef' || field.kind === 'pairArray') {
    return formatArrayValue(raw, field, lookup, options)
  }
  const num = Number(raw)
  if (Number.isFinite(num)) {
    // 0 表示「无」的引用字段直接给出中文，避免出现孤零零的 0
    if (field.zeroMeansNone && num === 0) return '无'
    // 引用优先：剧本集合 -> 公共数据表
    const refName = lookupRefName(field, num, lookup, options)
    if (refName) return refName
    const map = resolveEnum(field, options)
    const hit = map.find((m: EnumEntry) => m.value === num)
    if (hit) return hit.label
  }
  return String(raw)
}

/**
 * 生成「名称 (Id)」形式的完整展示文本，用于悬浮提示。
 *
 * @param entity 实体
 * @param field 字段定义
 * @param options 公共数据表
 * @param lookup 名称查找表
 * @returns 完整文本
 */
export function formatFieldValueFull(
  entity: Entity,
  field: FieldDef,
  options: Options | null,
  lookup?: NameLookup | null
): string {
  const name = formatFieldValue(entity, field, options, lookup)
  const raw = entity[field.key]
  if (raw === undefined || raw === null) return ''
  if (field.kind === 'string' || field.kind === 'attr' || field.kind === 'ability') return name
  if (Array.isArray(raw)) {
    const arr = getArray(raw)
    if (field.kind === 'pairArray') {
      const parts: string[] = []
      for (let i = 0; i + 1 < arr.length; i += 2) parts.push(`${arr[i]}:${arr[i + 1]}`)
      return `${name}（原始：${parts.join(',')}）`
    }
    return `${name}（原始：${arr.join(',')}）`
  }
  const num = Number(raw)
  if (!Number.isFinite(num) || name === String(raw)) return name
  return `${name} (${num})`
}

/**
 * 判断实体是否包含某个字段（用于区分「未定义」与「值为空」）。
 *
 * @param entity 实体
 * @param key 字段键
 */
export function hasField(entity: Entity, key: string): boolean {
  return Object.prototype.hasOwnProperty.call(entity, key)
}

/**
 * 取字段的完整展示名（含单位提示）。
 *
 * @param field 字段定义
 * @param options 公共数据表
 */
export function fieldEnumEntries(field: FieldDef, options: Options | null): EnumEntry[] {
  return resolveEnum(field, options)
}
