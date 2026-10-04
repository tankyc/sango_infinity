/**
 * 校验引擎
 *
 * 分为两层：
 * 1. validateEntity —— 单实体字段级校验（类型、范围、枚举、数组结构），
 *    可在编辑表单里逐次实时调用，开销极小；
 * 2. validateScenario —— 全量校验，在单实体校验基础上追加跨实体引用与一致性检查。
 *
 * 问题级别：
 * - error   一定会让游戏读取异常或数据自相矛盾（如引用了不存在的势力、五维不是数组）；
 * - warning 大概率不符合设计意图（如超出常规区间、关系单向、归属与身分冲突）；
 * - info    提示性信息（如未知字段、重名武将）。
 */
import type { ValidationIssue, Scenario, CollectionKey, Entity, Options } from './types'
import { COLLECTION_META, COLLECTION_ORDER } from './types'
import type { FieldDef, EnumEntry } from './schema'
import { COLLECTION_FIELDS, resolveEnum, findField } from './schema'
import { getAttrBase, getAttrChangeId, getAbilityLevel, getArray } from './fields'

/** 各集合的必需字段（缺失即 error） */
const REQUIRED_FIELDS: Record<CollectionKey, string[]> = {
  personSet: [
    'Id',
    'Name',
    'BelongForce',
    'BelongCorps',
    'BelongCity',
    'CurrentCity',
    'state',
    'sex',
    'command',
    'strength',
    'intelligence',
    'politics',
    'glamour',
    'spearLv',
    'halberdLv',
    'crossbowLv',
    'rideLv',
    'waterLv',
    'machineLv',
    'Level',
    'Official',
    'loyalty',
    'personality',
    'argumentation',
  ],
  citySet: ['Id', 'Name', 'BelongForce', 'BuildingType', 'CityLevelType', 'province', 'x', 'y'],
  corpsSet: ['Id', 'BelongForce', 'Comander', 'number', 'policy', 'policy_target'],
  forceSet: ['Id', 'Governor', 'Flag', 'Title'],
}

/** 全部集合的 id 索引缓存 */
interface Index {
  ids: Record<CollectionKey, Set<number>>
  byCollection: Record<CollectionKey, Entity[]>
}

/**
 * 构建 id 索引。
 *
 * @param scenario 剧本
 * @returns 索引对象
 */
function buildIndex(scenario: Scenario): Index {
  const ids = {} as Record<CollectionKey, Set<number>>
  const byCollection = {} as Record<CollectionKey, Entity[]>
  for (const key of COLLECTION_ORDER) {
    const set = (scenario[key] || {}) as Record<string, Entity>
    const list = Object.values(set).filter((x) => x && typeof x === 'object')
    byCollection[key] = list
    ids[key] = new Set(list.map((x) => Number(x.Id)))
  }
  return { ids, byCollection }
}

/**
 * 生成问题对象。
 */
function issue(
  level: ValidationIssue['level'],
  collection: ValidationIssue['collection'],
  entityId: number | null,
  field: string,
  message: string
): ValidationIssue {
  return { level, collection, entityId, field, message, key: `${collection}|${entityId ?? 'root'}|${field}|${message}` }
}

/**
 * 单实体字段级校验。
 *
 * @param collection 集合键
 * @param entity 实体
 * @param options 公共数据表
 * @param index 可选的全局索引（提供时同时校验引用有效性）
 * @returns 问题列表
 */
export function validateEntity(
  collection: CollectionKey,
  entity: Entity,
  options: Options | null,
  index?: Index
): ValidationIssue[] {
  const issues: ValidationIssue[] = []
  const fields = COLLECTION_FIELDS[collection]
  const id = Number(entity.Id)

  // 1) 必需字段
  for (const key of REQUIRED_FIELDS[collection]) {
    if (!Object.prototype.hasOwnProperty.call(entity, key)) {
      issues.push(issue('error', collection, id, key, `缺少必需字段「${key}」`))
    }
  }

  // 2) Id 校验
  if (!Number.isFinite(id) || !Number.isInteger(id) || id <= 0) {
    issues.push(issue('error', collection, id, 'Id', `Id 必须为正整数，当前为 ${JSON.stringify(entity.Id)}`))
  }

  // 3) 逐字段校验
  for (const field of fields) {
    if (!Object.prototype.hasOwnProperty.call(entity, field.key)) continue
    const value = entity[field.key]
    if (value === undefined || value === null) {
      issues.push(issue('error', collection, id, field.key, `「${field.label}」值为空`))
      continue
    }
    validateFieldValue(collection, id, field, value, options, index, issues)
  }

  // 4) 未知字段检测（保留在扩展数据中的键）
  for (const key of Object.keys(entity)) {
    if (findField(fields, key)) continue
    issues.push(
      issue(
        'info',
        collection,
        id,
        key,
        `字段「${key}」不在字段表中（可能是原版遗留键或拼写错误），保存时会原样保留`
      )
    )
  }

  return issues
}

/**
 * 按控件类型校验单个字段值。
 */
function validateFieldValue(
  collection: CollectionKey,
  id: number,
  field: FieldDef,
  value: unknown,
  options: Options | null,
  index: Index | undefined,
  issues: ValidationIssue[]
): void {
  const push = (level: ValidationIssue['level'], message: string) =>
    issues.push(issue(level, collection, id, field.key, message))

  switch (field.kind) {
    case 'string': {
      if (typeof value !== 'string') push('error', `「${field.label}」应为字符串，当前为 ${typeof value}`)
      return
    }
    case 'bool': {
      if (typeof value !== 'boolean') push('error', `「${field.label}」应为布尔值`)
      return
    }
    case 'int':
    case 'float': {
      if (typeof value !== 'number' || !Number.isFinite(value)) {
        push('error', `「${field.label}」应为数字，当前为 ${JSON.stringify(value)}`)
        return
      }
      if (field.kind === 'int' && !Number.isInteger(value)) {
        push('warning', `「${field.label}」应为整数，当前为 ${value}`)
      }
      checkRange(field, value, push)
      return
    }
    case 'enum':
    case 'ref': {
      if (typeof value !== 'number' || !Number.isFinite(value)) {
        push('error', `「${field.label}」应为数字，当前为 ${JSON.stringify(value)}`)
        return
      }
      checkRange(field, value, push)
      // 枚举取值检查
      const entries = resolveEnum(field, options)
      if (entries.length > 0 && !entries.some((e: EnumEntry) => e.value === value)) {
        push('warning', `「${field.label}」取值 ${value} 不在候选表（${entries.map((e) => e.value).join('/')}）中`)
      }
      // 引用有效性检查
      if (field.ref && index) {
        const isEmpty = field.zeroMeansNone && value === 0
        if (!isEmpty && !index.ids[field.ref].has(value)) {
          push(
            referenceLevel(field.ref, value),
            referenceMessage(field, field.ref, value)
          )
        }
      }
      return
    }
    case 'attr': {
      if (!Array.isArray(value)) {
        push('error', `「${field.label}」应为数组（[基础值, 成长类型Id]），当前为 ${typeof value}`)
        return
      }
      if (value.length < 2 && value.length !== 1) {
        push('warning', `「${field.label}」数组长度为 ${value.length}，常规应为 2 位（[基础值, 成长类型Id]）`)
      }
      const base = getAttrBase(value)
      if (typeof value[0] !== 'number') push('error', `「${field.label}」第 1 位（基础值）应为数字`)
      else checkRange(field, base, push)
      const changeId = getAttrChangeId(value)
      if (typeof value[1] !== 'number') push('error', `「${field.label}」第 2 位（成长类型Id）应为数字`)
      else if (options && options.attributeChangeTypes.length > 0) {
        const ok = options.attributeChangeTypes.some((t) => t.id === changeId)
        if (!ok) {
          push(
            'warning',
            `「${field.label}」成长类型 ${changeId} 不在成长类型表（${options.attributeChangeTypes
              .map((t) => t.id)
              .join('/')}）中`
          )
        }
      }
      return
    }
    case 'ability': {
      if (!Array.isArray(value)) {
        push('error', `「${field.label}」应为数组（[等级]），当前为 ${typeof value}`)
        return
      }
      if (value.length === 0) {
        push('error', `「${field.label}」数组不能为空`)
        return
      }
      if (typeof value[0] !== 'number') {
        push('error', `「${field.label}」第 1 位（适性等级）应为数字`)
        return
      }
      checkRange(field, getAbilityLevel(value), push)
      return
    }
    case 'intArray':
    case 'polyRef': {
      if (!Array.isArray(value)) {
        push('error', `「${field.label}」应为数组，当前为 ${typeof value}`)
        return
      }
      const arr = getArray(value)
      validateArrayShape(field, arr, push)
      if (field.itemMin !== undefined || field.itemMax !== undefined) {
        for (const n of arr) {
          if (field.itemMin !== undefined && n < field.itemMin) {
            push('error', `「${field.label}」元素 ${n} 小于下限 ${field.itemMin}`)
            break
          }
          if (field.itemMax !== undefined && n > field.itemMax) {
            push('error', `「${field.label}」元素 ${n} 超过上限 ${field.itemMax}`)
            break
          }
        }
      }
      if (field.itemRef && index) {
        const missing = arr.filter((n) => !index.ids[field.itemRef as CollectionKey].has(n))
        if (missing.length > 0) {
          push(
            referenceLevel(field.itemRef, missing[0]),
            `「${field.label}」引用了不存在的${COLLECTION_META[field.itemRef].label}：${missing.slice(0, 8).join('、')}${missing.length > 8 ? ' 等' : ''}${referenceHint(field.itemRef, missing[0])}`
          )
        }
      }
      if (field.itemOptionsKey && options) {
        const list = options[field.itemOptionsKey] as { id: number }[]
        if (Array.isArray(list) && list.length > 0) {
          const valid = new Set(list.map((x) => x.id))
          const missing = arr.filter((n) => !valid.has(n))
          if (missing.length > 0) {
            push('warning', `「${field.label}」取值 ${missing.slice(0, 8).join('、')} 不在数据表中`)
          }
        }
      }
      return
    }
    case 'pairArray': {
      if (!Array.isArray(value)) {
        push('error', `「${field.label}」应为数组，当前为 ${typeof value}`)
        return
      }
      if (value.length % 2 !== 0) {
        push('error', `「${field.label}」应为 [类型, 数量] 成对结构，当前长度为奇数 ${value.length}`)
      }
      for (const item of value) {
        if (typeof item !== 'number' || !Number.isFinite(item)) {
          push('error', `「${field.label}」存在非数字元素：${JSON.stringify(item)}`)
          break
        }
      }
      // 成对结构的键需要存在于候选表中（例如 itemStore 的键必须是合法的存放类别），
      // 数值位（数量）不参与校验。
      if (field.itemOptionsKey && options) {
        const list = (options as unknown as Record<string, { id: number }[]>)[field.itemOptionsKey]
        if (Array.isArray(list) && list.length > 0) {
          const valid = new Set(list.map((x) => Number(x.id)))
          const badKeys: number[] = []
          for (let i = 0; i + 1 < value.length; i += 2) {
            if (!valid.has(Number(value[i]))) badKeys.push(Number(value[i]))
          }
          if (badKeys.length > 0) {
            push(
              'warning',
              `「${field.label}」的键 ${badKeys.slice(0, 8).join('、')} 不在${field.itemLabel ?? '候选表'}中，游戏加载时该项会被忽略`
            )
          }
        }
      }
      return
    }
    default:
      return
  }
}

/**
 * 判断「引用的实体不存在」应归为哪一级别。
 *
 * 武将引用存在一个特例：官方剧本中存在少量指向 Id ≥ 1000 的引用（例如袁术、袁遗的
 * 父亲指向 2003），这些 Id 在剧本与武将库中都不存在，属于原版数据的占位引用。
 * 为免淹没真正的问题，这类引用降级为警告并给出解释；其余引用缺失一律视为错误。
 *
 * @param collection 被引用的集合
 * @param value 缺失的 Id
 * @returns 问题级别
 */
function referenceLevel(collection: CollectionKey, value: number): ValidationIssue['level'] {
  if (collection === 'personSet' && value >= 1000) return 'warning'
  return 'error'
}

/**
 * 生成引用缺失问题的补充说明。
 *
 * @param collection 被引用的集合
 * @param value 缺失的 Id
 * @returns 说明文本（无额外说明时返回空串）
 */
function referenceHint(collection: CollectionKey, value: number): string {
  if (collection === 'personSet' && value >= 1000) {
    return '（该 Id 可能是未在本剧本实例化的全局武将或原版占位引用）'
  }
  return ''
}

/** 生成引用缺失问题的完整描述 */
function referenceMessage(field: FieldDef, collection: CollectionKey, value: number): string {
  return `「${field.label}」引用了不存在的${COLLECTION_META[collection].label}：${value}${referenceHint(collection, value)}`
}

/** 校验数值范围 */
function checkRange(
  field: FieldDef,
  value: number,
  push: (level: ValidationIssue['level'], message: string) => void
): void {
  if (field.min !== undefined && value < field.min) {
    push('error', `「${field.label}」为 ${value}，小于允许下限 ${field.min}`)
    return
  }
  if (field.max !== undefined && value > field.max) {
    push('error', `「${field.label}」为 ${value}，超过允许上限 ${field.max}`)
    return
  }
  const hint = field.softLabel ? `（${field.softLabel}）` : ''
  if (field.softMin !== undefined && value < field.softMin) {
    push('warning', `「${field.label}」为 ${value}，低于常规区间 ${field.softMin}~${field.softMax ?? '∞'}${hint}`)
    return
  }
  if (field.softMax !== undefined && value > field.softMax) {
    push(
      'warning',
      `「${field.label}」为 ${value}，超出常规区间 ${field.softMin ?? '-∞'}~${field.softMax}${hint}`
    )
  }
}

/** 校验数组长度约束 */
function validateArrayShape(
  field: FieldDef,
  arr: number[],
  push: (level: ValidationIssue['level'], message: string) => void
): void {
  if (field.arrayLen !== undefined) {
    const allowed = Array.isArray(field.arrayLen) ? field.arrayLen : [field.arrayLen]
    if (!allowed.includes(arr.length)) {
      push('error', `「${field.label}」长度应为 ${allowed.join(' 或 ')}，当前为 ${arr.length}`)
    }
    return
  }
  if (field.arrayMinLen !== undefined && arr.length < field.arrayMinLen) {
    push('error', `「${field.label}」至少需要 ${field.arrayMinLen} 个元素，当前为 ${arr.length}`)
  }
  if (field.arrayMaxLen !== undefined && arr.length > field.arrayMaxLen) {
    push('warning', `「${field.label}」元素数量 ${arr.length} 超过建议上限 ${field.arrayMaxLen}`)
  }
}

/**
 * 全量校验：字段级 + 跨实体一致性。
 *
 * @param scenario 剧本
 * @param options 公共数据表
 * @returns 问题列表
 */
export function validateScenario(scenario: Scenario | null, options: Options | null): ValidationIssue[] {
  if (!scenario) return []
  const issues: ValidationIssue[] = []
  const index = buildIndex(scenario)

  // 根节点
  if (typeof scenario.Id !== 'number') {
    issues.push(issue('error', 'root', null, 'Id', '剧本根节点的 Id 缺失或不是数字'))
  }
  if (typeof scenario.Name !== 'string' || scenario.Name.trim() === '') {
    issues.push(issue('error', 'root', null, 'Name', '剧本名称不能为空'))
  }
  if (!scenario.Info || typeof scenario.Info !== 'object') {
    issues.push(issue('error', 'root', null, 'Info', '缺少 Info 节点'))
  }

  // 每个集合
  for (const key of COLLECTION_ORDER) {
    const set = (scenario[key] || {}) as Record<string, Entity>
    const label = COLLECTION_META[key].label
    if (Object.keys(set).length === 0) {
      issues.push(issue('warning', key, null, '', `${label}集合为空`))
    }
    for (const [rawKey, entity] of Object.entries(set)) {
      if (!entity || typeof entity !== 'object' || Array.isArray(entity)) {
        issues.push(issue('error', key, Number(rawKey), '', `键 ${rawKey} 对应的元素不是对象`))
        continue
      }
      // 键与 Id 一致性
      if (Number(entity.Id) !== Number(rawKey)) {
        issues.push(
          issue('error', key, Number(entity.Id), 'Id', `键名 ${rawKey} 与元素 Id=${entity.Id} 不一致（游戏按 Id 索引，必须一致）`)
        )
      }
      issues.push(...validateEntity(key, entity, options, index))
    }
  }

  issues.push(...validateCrossEntity(index))

  return issues
}

/**
 * 跨实体一致性校验。
 *
 * @param index 已构建的集合索引
 * @returns 问题列表
 */
function validateCrossEntity(index: Index): ValidationIssue[] {
  const issues: ValidationIssue[] = []
  const persons = index.byCollection.personSet
  const cities = index.byCollection.citySet
  const corps = index.byCollection.corpsSet
  const forces = index.byCollection.forceSet

  const personById = new Map(persons.map((p) => [Number(p.Id), p]))
  const corpsById = new Map(corps.map((c) => [Number(c.Id), c]))
  const cityById = new Map(cities.map((c) => [Number(c.Id), c]))

  // 武将：归属链条与生卒逻辑
  const nameCount = new Map<string, number[]>()
  for (const p of persons) {
    const id = Number(p.Id)
    const belongForce = Number(p.BelongForce) || 0
    const belongCorps = Number(p.BelongCorps) || 0
    const belongCity = Number(p.BelongCity) || 0
    const currentCity = Number(p.CurrentCity) || 0
    const state = Number(p.state)

    const corpsEntity = corpsById.get(belongCorps)

    // 军团隶属势力应与武将一致
    if (corpsEntity && Number(corpsEntity.BelongForce) !== belongForce) {
      issues.push(
        issue(
          'warning',
          'personSet',
          id,
          'BelongCorps',
          `武将所属势力为 ${belongForce}，但其所属军团 ${belongCorps} 属于势力 ${corpsEntity.BelongForce}，两者不一致`
        )
      )
    }

    // 君主/都督/太守等在职身分必须有所属势力
    if ((state === 1 || state === 2 || state === 3) && belongForce === 0) {
      issues.push(
        issue('warning', 'personSet', id, 'state', `身分为「${state === 1 ? '君主' : state === 2 ? '都督' : '太守'}」但未归属任何势力`)
      )
    }
    if (state === 5 && belongForce !== 0) {
      issues.push(issue('warning', 'personSet', id, 'state', '身分为「在野」但仍有归属势力'))
    }
    if (state === 9 && belongForce !== 0) {
      issues.push(issue('warning', 'personSet', id, 'state', '身分为「死亡」但仍有归属势力'))
    }

    if (belongCity !== 0 && currentCity !== 0 && belongCity !== currentCity) {
      issues.push(
        issue('info', 'personSet', id, 'CurrentCity', `所在都市（${currentCity}）与所属都市（${belongCity}）不同，若非出征可忽略`)
      )
    }

    const born = Number(p.yearBorn)
    const dead = Number(p.yearDead)
    const appear = Number(p.appearance)
    if (Number.isFinite(born) && Number.isFinite(dead) && born > 0 && dead > 0 && born > dead) {
      issues.push(issue('error', 'personSet', id, 'yearBorn', `出生年 ${born} 晚于死亡年 ${dead}`))
    }
    if (Number.isFinite(appear) && Number.isFinite(dead) && appear > dead) {
      issues.push(issue('warning', 'personSet', id, 'appearance', `登场年 ${appear} 晚于死亡年 ${dead}`))
    }
    if (Number.isFinite(appear) && Number.isFinite(born) && appear > 0 && born > 0 && appear < born) {
      issues.push(issue('warning', 'personSet', id, 'appearance', `登场年 ${appear} 早于出生年 ${born}`))
    }

    // 重名统计
    const name = String(p.Name ?? '').trim()
    if (name) {
      const arr = nameCount.get(name) || []
      arr.push(id)
      nameCount.set(name, arr)
    }

    // 血缘自引用
    if (Number(p.ketsuen) === id) {
      // 剧本中普遍是自身 id，属正常；仅当为 0 时提示
    } else if (Number(p.ketsuen) === 0) {
      issues.push(issue('info', 'personSet', id, 'ketsuen', '血缘为 0，通常应指向自身或家族代表的武将 Id'))
    }
  }

  for (const [name, ids] of nameCount) {
    if (ids.length > 1) {
      issues.push(
        issue('info', 'personSet', ids[1], 'Name', `武将姓名「${name}」重复（另有 Id ${ids.filter((x) => x !== ids[1]).join('、')}）`)
      )
    }
  }

  // 都市：相邻关系双向性
  for (const c of cities) {
    const id = Number(c.Id)
    const neighbors = getArray(c.NeighborList)
    for (const n of neighbors) {
      const other = cityById.get(n)
      if (!other) continue
      const back = getArray(other.NeighborList)
      if (!back.includes(id)) {
        issues.push(
          issue('warning', 'citySet', id, 'NeighborList', `与都市 ${n} 的相邻关系不是双向的（${n} 的相邻列表中缺少 ${id}）`)
        )
      }
    }
  }

  // 势力：君主 / 军师的归属一致性
  for (const f of forces) {
    const id = Number(f.Id)
    const governor = Number(f.Governor) || 0
    const counsellor = Number(f.Counsellor) || 0
    if (governor === 0) {
      issues.push(issue('warning', 'forceSet', id, 'Governor', '势力未设置君主'))
    } else {
      const g = personById.get(governor)
      if (g && Number(g.BelongForce) !== id) {
        issues.push(
          issue('warning', 'forceSet', id, 'Governor', `君主（武将 ${governor}）的所属势力为 ${Number(g.BelongForce)}，与本势力不一致`)
        )
      }
    }
    if (counsellor !== 0) {
      const c = personById.get(counsellor)
      if (c && Number(c.BelongForce) !== id) {
        issues.push(
          issue('warning', 'forceSet', id, 'Counsellor', `军师（武将 ${counsellor}）的所属势力为 ${Number(c.BelongForce)}，与本势力不一致`)
        )
      }
    }
    // 一个君主只能属于一个势力
    const dupGov = forces.filter((x) => Number(x.Governor) === governor && governor !== 0)
    if (dupGov.length > 1 && dupGov[0] === f) {
      issues.push(issue('error', 'forceSet', id, 'Governor', `武将 ${governor} 被 ${dupGov.length} 个势力同时设为君主`))
    }
  }

  // 军团：军团长归属一致性
  for (const c of corps) {
    const id = Number(c.Id)
    const commander = Number(c.Comander) || 0
    if (commander === 0) {
      issues.push(issue('warning', 'corpsSet', id, 'Comander', '军团未设置军团长'))
      continue
    }
    const p = personById.get(commander)
    if (p && Number(p.BelongCorps) !== id) {
      issues.push(
        issue('warning', 'corpsSet', id, 'Comander', `军团长（武将 ${commander}）的所属军团为 ${Number(p.BelongCorps)}，与本军团不一致`)
      )
    }
  }

  // 一个武将不应同时是多个军团/势力的君主
  const governorIds = new Map<number, number[]>()
  for (const f of forces) {
    const g = Number(f.Governor) || 0
    if (!g) continue
    const arr = governorIds.get(g) || []
    arr.push(Number(f.Id))
    governorIds.set(g, arr)
  }

  return issues
}

/**
 * 汇总问题数量。
 *
 * @param issues 问题列表
 */
export function countIssues(issues: ValidationIssue[]): { error: number; warning: number; info: number } {
  const result = { error: 0, warning: 0, info: 0 }
  for (const i of issues) result[i.level] += 1
  return result
}

/**
 * 把问题列表按实体归组，便于表格行内显示与快速跳转。
 *
 * @param issues 问题列表
 * @returns 形如 `personSet:12` -> 问题数组 的映射
 */
export function groupIssuesByEntity(issues: ValidationIssue[]): Map<string, ValidationIssue[]> {
  const map = new Map<string, ValidationIssue[]>()
  for (const i of issues) {
    if (i.entityId === null) continue
    const key = `${i.collection}:${i.entityId}`
    const arr = map.get(key)
    if (arr) arr.push(i)
    else map.set(key, [i])
  }
  return map
}

/** 供其它模块复用的索引类型 */
export type { Index as ValidationIndex }
