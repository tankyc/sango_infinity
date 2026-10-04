/**
 * 文件名：enums.ts
 * 描述：武将编辑用到的固定枚举与工具方法（取值与游戏保持一致）
 */

import type { Person } from './types'
import { withPrefix } from './siteLinks'

/** 通用下拉选项 */
export interface Choice {
  value: number
  label: string
}

/** 性别：0=男，1=女 */
export const SEX_CHOICES: Choice[] = [
  { value: 0, label: '男' },
  { value: 1, label: '女' },
]

/** 武将类型（对应游戏 PersonTypeEnum，数值不可修改） */
export const PERSON_TYPE_CHOICES: Choice[] = [
  { value: 0, label: '历史' },
  { value: 1, label: '古代' },
  { value: 2, label: '事件' },
  { value: 3, label: '自定义' },
  { value: 4, label: '临时' },
  { value: 5, label: '未知' },
  { value: 6, label: 'NPC' },
  { value: 7, label: '生成' },
]

/** 武将类型枚举值（与游戏 PersonTypeEnum 一一对应） */
export const PersonTypeEnum = {
  Historical: 0,
  Ancient: 1,
  Event: 2,
  Custom: 3,
  Temporary: 4,
  Unknown: 5,
  NPC: 6,
  Auto: 7,
  Max: 8,
} as const

/** 身份 */
export const STATE_CHOICES: Choice[] = [
  { value: 0, label: '（未设置）' },
  { value: 1, label: '君主' },
  { value: 2, label: '都督' },
  { value: 3, label: '太守' },
  { value: 4, label: '一般' },
  { value: 5, label: '在野' },
  { value: 6, label: '俘虏' },
  { value: 7, label: '未登场' },
  { value: 8, label: '未发现' },
  { value: 9, label: '已死亡' },
]

/** 音声 */
export const VOICE_CHOICES: Choice[] = [
  { value: 0, label: '男鲁莽' },
  { value: 1, label: '男刚胆' },
  { value: 2, label: '男冷静' },
  { value: 3, label: '男小心' },
  { value: 4, label: '女刚胆' },
  { value: 5, label: '女冷静' },
  { value: 6, label: '吕布' },
  { value: 7, label: '诸葛亮' },
]

/** 语气 */
export const TONE_CHOICES: Choice[] = [
  { value: 0, label: '恭敬' },
  { value: 1, label: '普通' },
  { value: 2, label: '威严' },
  { value: 3, label: '自大' },
  { value: 4, label: '蛮族' },
]

/** 汉室态度 */
export const KANSHITSU_CHOICES: Choice[] = [
  { value: 0, label: '无视' },
  { value: 1, label: '普通' },
  { value: 2, label: '重视' },
]

/** 理想 */
export const IDEAL_CHOICES: Choice[] = [
  { value: 0, label: '霸道' },
  { value: 1, label: '王道' },
  { value: 2, label: '我道' },
  { value: 3, label: '割据' },
  { value: 4, label: '义侠' },
]

/** 才干 */
export const TALENT_CHOICES: Choice[] = [
  { value: 0, label: '王佐' },
  { value: 1, label: '出世' },
  { value: 2, label: '安全' },
  { value: 3, label: '隐遁' },
]

/**
 * 指向其它武将 ID 的标量字段（偏移导出时需同步平移）。
 * 与服务端 PERSON_ID_FIELDS 保持一致。
 */
export const PERSON_REF_FIELDS = ['Father', 'Mother', 'Brother'] as const

/**
 * 指向其它武将 ID 的数组字段（偏移导出时需同步平移）。
 * 注意：FeatureList 为特性 ID、headIconID / imageID 为头像 ID，不属于武将 ID，不能平移。
 * 与服务端 PERSON_ID_LIST_FIELDS 保持一致。
 */
export const PERSON_REF_LIST_FIELDS = [
  'SpouseList',
  'BrotherList',
  'LikePersonList',
  'HatePersonList',
] as const

/** 五维能力字段 */
export const ABILITY_FIELDS = [
  { key: 'command', label: '统御' },
  { key: 'strength', label: '武力' },
  { key: 'intelligence', label: '智力' },
  { key: 'politics', label: '政治' },
  { key: 'glamour', label: '魅力' },
] as const

/** 兵种适性字段 */
export const WEAPON_FIELDS = [
  { key: 'spearLv', label: '矛' },
  { key: 'halberdLv', label: '戟' },
  { key: 'crossbowLv', label: '弓弩' },
  { key: 'rideLv', label: '骑' },
  { key: 'waterLv', label: '水军' },
  { key: 'machineLv', label: '器械' },
] as const

/**
 * 五维能力数组结构：[基础值, 成长类型Id, 经验, 万分比, 最终值]
 * 与游戏 PersonAttributeValue 的序列化顺序一致（PersonAttributeValueConverter）。
 */
export const ABILITY_ARRAY_LENGTH = 5

/**
 * 兵种适性数组结构：[基础值(等级), 经验, 等级]
 * 与游戏 PersonAbilityValue 的序列化顺序一致（PersonAbilityValueConverter）。
 */
export const WEAPON_ARRAY_LENGTH = 3

/**
 * 未设置成长类型时使用的默认值（对应游戏的「5 普通型」）。
 * 游戏内把 changeId = 0 也按 5 普通型处理，这里统一显式写 5。
 */
export const DEFAULT_ATTRIBUTE_CHANGE_ID = 5

/** 能力值的「万分比」默认值（10000 = 100%） */
export const DEFAULT_ATTRIBUTE_FACTER = 10000

/**
 * 取能力 / 适性字段的「基础值」。
 * 字段在游戏里是数组（能力 [基础值, 成长类型Id, 经验, 万分比, 最终值]、
 * 适性 [基础值, 经验, 等级]），历史数据可能是单个数字；
 * 这里统一取首元素，供卡片展示、排序与表单绑定使用。
 * @param value 字段值（数字或数字数组）
 * @returns 基础值（无法解析时为 0）
 */
export function statBaseValue(value: unknown): number {
  if (Array.isArray(value)) {
    const first = Number(value[0])
    return Number.isFinite(first) ? first : 0
  }
  const num = Number(value)
  return Number.isFinite(num) ? num : 0
}

/**
 * 取能力字段的「成长类型」Id（AttributeChangeType）。
 * 该 Id 指向 ScenarioCommonData.AttributeChangeTypes
 * （1 超持续型 / 2 持续型 / 3 早熟型 / 4 早熟持续型 / 5 普通型 /
 *  6 普通持续型 / 7 晚成型 / 8 超晚成型 / 9 张飞型）。
 * 数据不是数组时返回 null（历史数据没有成长类型）。
 * @param value 字段值
 * @returns 成长类型 Id；不是数组或没有第二项时返回 null
 */
export function statChangeIdOf(value: unknown): number | null {
  if (!Array.isArray(value) || value.length < 2) return null
  const second = Number(value[1])
  return Number.isFinite(second) ? second : null
}

/**
 * 构造五维能力数组。
 * @param base 基础值
 * @param changeId 成长类型 Id（缺省为 5 普通型）
 * @returns [基础值, 成长类型Id, 经验, 万分比, 最终值]
 */
export function makeAbilityValue(base: number, changeId: number = DEFAULT_ATTRIBUTE_CHANGE_ID): number[] {
  const value = toInt(base, 0)
  return [value, toInt(changeId, DEFAULT_ATTRIBUTE_CHANGE_ID), 0, DEFAULT_ATTRIBUTE_FACTER, value]
}

/**
 * 构造兵种适性数组。
 * @param level 适性等级（0-7）
 * @returns [基础值(等级), 经验, 等级]
 */
export function makeWeaponValue(level: number): number[] {
  const value = toInt(level, 0)
  return [value, 0, value]
}

/**
 * 改写五维能力的「基础值」。
 * 结果会归一化为完整的 5 元组：保留成长类型 Id、清零经验、
 * 保留万分比并把「最终值」同步为基础值，保证与游戏读写口径一致。
 * @param value 原字段值（数组或历史数字）
 * @param base 新的基础值
 * @returns 归一化后的能力数组
 */
export function withAbilityBase(value: unknown, base: number): number[] {
  const changeId = statChangeIdOf(value) ?? DEFAULT_ATTRIBUTE_CHANGE_ID
  return makeAbilityValue(base, changeId)
}

/**
 * 改写五维能力的「成长类型」Id，其余元素保持不变。
 * @param value 原字段值
 * @param changeId 新的成长类型 Id
 * @returns 归一化后的能力数组
 */
export function withAbilityChangeId(value: unknown, changeId: number): number[] {
  return makeAbilityValue(statBaseValue(value), changeId)
}

/**
 * 把任意输入安全转换为整数（非数字按 fallback 处理）。
 * @param value 原始值
 * @param fallback 解析失败时的默认值
 * @returns 整数结果
 */
function toInt(value: unknown, fallback: number): number {
  const num = Number(value)
  return Number.isFinite(num) ? Math.trunc(num) : fallback
}

/**
 * 本体系列扩展字段（新版本体数据特有，旧版没有）。
 * array=true 表示该字段是整数数组（按原长度编辑，不去重、不丢弃 -1）。
 * 注意：登场年 appearance 已是正式字段（与游戏 Person.appearance 对应），
 * 由「生卒登场」分区编辑，不再列在这里。
 */
export const EXTENDED_FIELD_LABELS: { key: string; label: string; array?: boolean }[] = [
  { key: 'skeleton', label: '体型骨骼' },
  { key: 'body', label: '身体尺寸', array: true },
  { key: 'birthplace', label: '出身地' },
  { key: 'generation', label: '世代' },
  { key: 'ambition', label: '野心' },
  { key: 'strategic_tendency', label: '战略倾向' },
  { key: 'hanLoyalty', label: '汉室忠诚' },
  { key: 'local_affiliation', label: '地方归属' },
  { key: 'promotion', label: '晋升倾向' },
  { key: 'death_type', label: '死亡类型' },
  { key: 'ketsuen', label: '血缘' },
  { key: 'horse', label: '坐骑' },
  { key: 'left_weapon', label: '左手武器' },
  { key: 'right_weapon', label: '右手武器' },
  { key: 'injury', label: '负伤' },
  { key: 'wadai', label: '话题' },
  { key: 'wajutsu', label: '话术', array: true },
  { key: 'wordTac', label: '言辞', array: true },
  { key: 'CurrentCity', label: '当前城市' },
  { key: 'oldAge', label: '老年值（oldAge）' },
  { key: 'old_age', label: '老年值（old_age）' },
]

/** 生成头像图片地址（1=大图，2=头像） */
export function headIconUrl(id: number, type: 1 | 2 = 2): string {
  // 带上站点前缀：子路径部署时头像实际位于 /personlib/face/ 下
  return withPrefix('/face/' + id + '_' + type + '.png')
}

/**
 * 取得选项标签，找不到时回退为原始数值。
 * @param choices 选项列表
 * @param value 数值
 * @returns 显示文本
 */
export function labelOf(choices: Choice[], value: number): string {
  const hit = choices.find((c) => c.value === value)
  return hit ? hit.label : String(value)
}

/**
 * 依据适性等级数值取得显示文本（C / B / A / S / S1 ~ S4）。
 * @param level 等级
 * @returns 显示文本
 */
export function abilityLevelLabel(level: number): string {
  const names = ['C', 'B', 'A', 'S', 'S1', 'S2', 'S3', 'S4']
  return names[level] ?? String(level)
}

/**
 * 生成唯一姓名：若与已占用姓名冲突，则依次追加 #1、#2 …… 直到不重复。
 * 与后端 makeUniqueName 逻辑保持一致，用于保存前的姓名预览。
 * @param name 期望姓名
 * @param usedNames 已占用的姓名集合
 * @returns 唯一姓名
 */
export function makeUniqueName(name: string, usedNames: Set<string>): string {
  const base = (name ?? '').trim()
  if (!base) return base
  if (!usedNames.has(base)) return base
  let index = 1
  while (usedNames.has(`${base}#${index}`)) index++
  return `${base}#${index}`
}

/** 新建武将时使用的默认值（与后端默认模板保持一致） */
export function createDefaultPerson(): Person {
  return {
    Id: 0,
    Name: '',
    type: PersonTypeEnum.Custom,
    familyName: '',
    giveName: '',
    nickName: '',
    description: '',
    headIconID: 2000,
    imageID: null,
    image: '',
    image_old: null,
    sex: 0,
    appearance: 190,
    yearBorn: 190,
    yearDead: 289,
    compatibility: 0,
    state: 5,
    personality: 1,
    argumentation: 0,
    voice: 0,
    tone: 0,
    kanshitsu: 0,
    ideal: 0,
    talent: 0,
    merit: 0,
    stamina: 0,
    Level: 0,
    command: makeAbilityValue(50),
    strength: makeAbilityValue(50),
    intelligence: makeAbilityValue(50),
    politics: makeAbilityValue(50),
    glamour: makeAbilityValue(50),
    consanguinity: 0,
    Father: 0,
    Mother: 0,
    SpouseList: null,
    Brother: 0,
    BrotherList: null,
    LikePersonList: null,
    HatePersonList: null,
    spearLv: makeWeaponValue(0),
    halberdLv: makeWeaponValue(0),
    crossbowLv: makeWeaponValue(0),
    rideLv: makeWeaponValue(0),
    waterLv: makeWeaponValue(0),
    machineLv: makeWeaponValue(0),
    Exp: 0,
    FeatureList: null,
    IsAlive: true,
    ActionOver: false,
    tags: [],
    updatedAt: '',
  }
}

/**
 * 格式化最后修改时间。
 * 服务端保存的是 ISO 8601 UTC 字符串，这里转换为浏览器本地时区展示。
 * @param value ISO 8601 UTC 字符串
 * @returns 形如「2026-09-11 15:30」的文本（无法解析时原样返回）
 */
export function formatUpdatedAt(value: string | undefined | null): string {
  const text = (value ?? '').trim()
  if (!text) return ''
  const time = new Date(text)
  if (Number.isNaN(time.getTime())) return text
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${time.getFullYear()}-${pad(time.getMonth() + 1)}-${pad(time.getDate())} ${pad(
    time.getHours(),
  )}:${pad(time.getMinutes())}`
}
