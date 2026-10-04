/**
 * 字段元数据（Schema）
 *
 * 本文件是整个编辑器的「唯一事实来源」：表格列、编辑表单、批量编辑项、
 * 校验规则、搜索索引全部由这里驱动，新增字段只需在对应数组里补一条。
 *
 * 字段来源与语义依据（C# 侧）：
 * - Person      : Project/Assets/Sango/Scripts/Game/Object/Person/Person.cs
 * - City        : Project/Assets/Sango/Scripts/Game/Object/City/City.cs
 * - Corps       : Project/Assets/Sango/Scripts/Game/Object/Corps/Corps.cs
 * - Force       : Project/Assets/Sango/Scripts/Game/Object/Force/Force.cs
 * - ScenarioInfo: Project/Assets/Sango/Scripts/Game/Scenario/ScenarioInfo.cs
 *
 * 标记 legacy 的字段在 C# 中未声明（属扩展数据直通字段），因此只做弱校验。
 */
import type { CollectionKey, Options, OptionItem } from './types'

/** 字段控件类型 */
export type FieldKind =
  | 'int'
  | 'float'
  | 'string'
  | 'bool'
  | 'enum'
  | 'ref'
  | 'polyRef'
  | 'attr'
  | 'ability'
  | 'intArray'
  | 'pairArray'

/** 枚举映射项 */
export interface EnumEntry {
  value: number
  label: string
}

/** 字段定义 */
export interface FieldDef {
  /** 剧本 JSON 中的键名 */
  key: string
  /** 中文标签 */
  label: string
  /** 控件类型 */
  kind: FieldKind
  /** 分组名（用于编辑表单折叠面板） */
  group: string
  /** 硬性下限（越界即 error） */
  min?: number
  /** 硬性上限（越界即 error） */
  max?: number
  /** 建议区间，越界给 warning */
  softMin?: number
  /** 建议区间，越界给 warning */
  softMax?: number
  /** 建议区间的解释文本，用于提示信息 */
  softLabel?: string
  /** 数字步进 */
  step?: number
  /** 引用哪个集合（ref / polyRef） */
  ref?: CollectionKey
  /** 数组元素引用哪个集合 */
  itemRef?: CollectionKey
  /** 数组元素引用哪张公共数据表（与 itemRef 二选一） */
  itemOptionsKey?: keyof Options
  /** 0 是否表示「无」（引用类字段） */
  zeroMeansNone?: boolean
  /** 数组元素硬性范围 */
  itemMin?: number
  itemMax?: number
  /** 数组长度约束（数字表示固定长度，数组表示允许的长度集合） */
  arrayLen?: number | number[]
  /** 数组元素数量下限（仅校验下限） */
  arrayMinLen?: number
  /** 数组元素数量上限 */
  arrayMaxLen?: number
  /** 使用内置枚举映射（Options 中的键名） */
  optionsKey?: keyof Options
  /** 使用内置静态枚举映射 */
  enumMap?: EnumEntry[]
  /**
   * 数组元素的静态枚举映射。
   *
   * 例如 wordTac 是「舌战话术」数组，每个元素是 Rhetoric 枚举下标，
   * 有了它就能把 0~4 显示成「大喝 / 诡辩 / 无视 / 镇静 / 激昂」并给出勾选式编辑器。
   */
  itemEnumMap?: EnumEntry[]
  /**
   * 追加到候选列表最前面的补充项。
   *
   * 用于「候选表里没有、但业务上合法」的取值，例如装备槽位 -1 表示「无」，
   * 这样下拉框既能选装备也能显式置为空。
   */
  enumExtra?: EnumEntry[]
  /** 是否为原版遗留字段（弱校验） */
  legacy?: boolean
  /** 字段说明（鼠标悬停提示） */
  desc?: string
  /** 是否在表格中默认展示 */
  compact?: boolean
  /** 是否禁止参与批量编辑（如 Id） */
  noBatch?: boolean
  /** 数组元素的单位说明 */
  itemLabel?: string
}

/** 分组定义 */
export interface GroupDef {
  name: string
  desc?: string
}

/* ------------------------------------------------------------------ */
/* 静态枚举映射                                                        */
/* ------------------------------------------------------------------ */

/** 性别 */
export const SEX_MAP: EnumEntry[] = [
  { value: -1, label: '未设定' },
  { value: 0, label: '男' },
  { value: 1, label: '女' },
]

/** 身分（对应 C# PersonStateType） */
export const PERSON_STATE_MAP: EnumEntry[] = [
  { value: 0, label: '错误' },
  { value: 1, label: '君主' },
  { value: 2, label: '都督' },
  { value: 3, label: '太守' },
  { value: 4, label: '普通' },
  { value: 5, label: '在野' },
  { value: 6, label: '俘虏' },
  { value: 7, label: '未登场' },
  { value: 8, label: '未发现' },
  { value: 9, label: '死亡' },
]

/** 武将类型（对应 C# PersonTypeEnum，仅在 PersonLib 中声明） */
export const PERSON_TYPE_MAP: EnumEntry[] = [
  { value: 0, label: '历史' },
  { value: 1, label: '古代' },
  { value: 2, label: '事件' },
  { value: 3, label: '自定义' },
  { value: 4, label: '临时' },
  { value: 5, label: '未知' },
  { value: 6, label: 'NPC' },
  { value: 7, label: '生成' },
]

/** 伤病等级（对应 C# InjuryMaxLevel = 3） */
export const INJURY_MAP: EnumEntry[] = [
  { value: 0, label: '健康' },
  { value: 1, label: '轻伤' },
  { value: 2, label: '中伤' },
  { value: 3, label: '重伤' },
]

/** 语气（原版共 16 档） */
export const TONE_MAP: EnumEntry[] = Array.from({ length: 16 }, (_, i) => ({ value: i, label: `语气 ${i}` }))

/** 声音（原版共 8 档） */
export const VOICE_MAP: EnumEntry[] = Array.from({ length: 8 }, (_, i) => ({ value: i, label: `声音 ${i}` }))

/** 得意话题（对应舌战 Topic） */
export const WADAI_MAP: EnumEntry[] = [
  { value: 0, label: '故事' },
  { value: 1, label: '道理' },
  { value: 2, label: '时节' },
]

/** 话术（对应舌战 Rhetoric） */
export const RHETORIC_MAP: EnumEntry[] = [
  { value: 0, label: '大喝' },
  { value: 1, label: '诡辩' },
  { value: 2, label: '无视' },
  { value: 3, label: '镇静' },
  { value: 4, label: '激昂' },
]

/** 死亡方式 */
export const DEATH_TYPE_MAP: EnumEntry[] = [
  { value: 0, label: '通常' },
  { value: 1, label: '其它' },
]

/** 地方归属 */
export const LOCAL_AFFILIATION_MAP: EnumEntry[] = [
  { value: 0, label: '无' },
  { value: 1, label: '地方A' },
  { value: 2, label: '地方B' },
]

/** 汉室态度（概念对应 C# kanshitsu） */
export const HAN_LOYALTY_MAP: EnumEntry[] = [
  { value: 0, label: '无视' },
  { value: 1, label: '普通' },
  { value: 2, label: '重视' },
]

/** 体型 / 骨骼 */
export const SKELETON_MAP: EnumEntry[] = [
  { value: 0, label: '普通' },
  { value: 1, label: '体型一' },
  { value: 2, label: '体型二' },
  { value: 3, label: '体型三' },
]

/** 战略倾向 */
export const STRATEGIC_TENDENCY_MAP: EnumEntry[] = [
  { value: 0, label: '倾向 0' },
  { value: 1, label: '倾向 1' },
  { value: 2, label: '倾向 2' },
  { value: 3, label: '倾向 3' },
]

/** 野心 */
export const AMBITION_MAP: EnumEntry[] = [
  { value: 0, label: '0 · 无' },
  { value: 1, label: '1 · 低' },
  { value: 2, label: '2 · 中' },
  { value: 3, label: '3 · 高' },
  { value: 4, label: '4 · 极高' },
]

/** 仕官倾向 */
export const PROMOTION_MAP: EnumEntry[] = [
  { value: 0, label: '0' },
  { value: 1, label: '1' },
  { value: 2, label: '2' },
  { value: 3, label: '3' },
  { value: 4, label: '4' },
]

/** 世代 */
export const GENERATION_MAP: EnumEntry[] = [
  { value: 0, label: '0 世代' },
  { value: 1, label: '1 世代' },
  { value: 2, label: '2 世代' },
  { value: 3, label: '3 世代' },
  { value: 4, label: '4 世代' },
]

/** 都市特色（原版遗留，无 C# 枚举） */
export const CITY_SPECIALTY_MAP: EnumEntry[] = Array.from({ length: 7 }, (_, i) => ({
  value: i,
  label: `特色 ${i}`,
}))

/** 装备槽：-1 表示未装备 */
export const EQUIP_MAP_HINT = '-1 表示未装备'

/**
 * 军团委任类型。
 *
 * 直接对应 C# Corps.AppointType：None=0 / DestroyForce=1 / OccupyCity=2 / Auto=3。
 * 注意：`policy` / `policy_target` / `mid_objective` 这几个字段在 C# 中只是裸 int，
 * 没有任何枚举或读取逻辑，因此不再为它们编造标签。
 */
export const APPOINT_TYPE_MAP: EnumEntry[] = [
  { value: 0, label: '无' },
  { value: 1, label: '攻略势力' },
  { value: 2, label: '攻占城池' },
  { value: 3, label: '自动' },
]

/* ------------------------------------------------------------------ */
/* 武将字段                                                            */
/* ------------------------------------------------------------------ */

/** 武将编辑表单分组 */
export const PERSON_GROUPS: GroupDef[] = [
  { name: '基本', desc: '姓名、性别、身分等基础信息' },
  { name: '归属', desc: '势力 / 军团 / 都市的归属与所在' },
  { name: '能力', desc: '统率、武力、智力、政治、魅力五维' },
  { name: '兵种适性', desc: '枪、戟、弩、骑、水、兵器的适性等级' },
  { name: '性格与倾向', desc: '性格、义理、野心、汉室态度等' },
  { name: '生卒与登场', desc: '出生年、死亡年、登场年、寿命' },
  { name: '形象资源', desc: '头像、立绘、体型、语气、声音' },
  { name: '人际关系', desc: '父子、配偶、喜欢与厌恶的武将' },
  { name: '舌战', desc: '得意话题与话术' },
  { name: '装备与随身', desc: '武器、马匹与携带物品' },
  { name: '原版遗留', desc: 'C# 未声明、按扩展数据透传的字段，谨慎修改' },
]

/** 武将字段定义 */
export const PERSON_FIELDS: FieldDef[] = [
  { key: 'Id', label: 'ID', kind: 'int', group: '基本', min: 1, max: 100000, noBatch: true, compact: true, desc: '武将唯一标识，同时作为 personSet 的键，修改会牵动大量引用' },
  { key: 'Name', label: '姓名', kind: 'string', group: '基本', compact: true, desc: '显示用全名，通常为「姓 + 名」' },
  { key: 'familyName', label: '姓', kind: 'string', group: '基本' },
  { key: 'giveName', label: '名', kind: 'string', group: '基本' },
  { key: 'nickName', label: '字', kind: 'string', group: '基本' },
  { key: 'sex', label: '性别', kind: 'enum', group: '基本', enumMap: SEX_MAP, compact: true },
  { key: 'state', label: '身分', kind: 'enum', group: '基本', enumMap: PERSON_STATE_MAP, min: 0, max: 9, compact: true },
  { key: 'type', label: '类型', kind: 'enum', group: '基本', enumMap: PERSON_TYPE_MAP, min: 0, max: 8, legacy: true, desc: '运行期 Person 未声明该字段，仅做透传' },
  { key: 'Level', label: '等级', kind: 'ref', group: '基本', optionsKey: 'personLevels', min: 0, max: 100 },
  { key: 'Official', label: '官职', kind: 'ref', group: '基本', optionsKey: 'officials', min: 0, max: 1000 },
  { key: 'merit', label: '功绩', kind: 'int', group: '基本', min: 0, softMax: 65535, compact: true },
  { key: 'loyalty', label: '忠诚', kind: 'int', group: '基本', min: 0, max: 255, compact: true, desc: '剧本中实际取值 0~255（120 / 150 / 255 通常表示义兄弟、君主等特殊忠诚）' },
  { key: 'stamina', label: '体力', kind: 'int', group: '基本', min: 0, softMax: 100, softLabel: '常规为 0~100', legacy: true },
  { key: 'injury', label: '伤病', kind: 'enum', group: '基本', enumMap: INJURY_MAP, min: 0, max: 3 },

  { key: 'BelongForce', label: '所属势力', kind: 'ref', group: '归属', ref: 'forceSet', zeroMeansNone: true, min: 0, compact: true },
  { key: 'BelongCorps', label: '所属军团', kind: 'ref', group: '归属', ref: 'corpsSet', zeroMeansNone: true, min: 0, compact: true },
  { key: 'BelongCity', label: '所属都市', kind: 'ref', group: '归属', ref: 'citySet', zeroMeansNone: true, min: 0, compact: true },
  { key: 'CurrentCity', label: '所在都市', kind: 'ref', group: '归属', ref: 'citySet', zeroMeansNone: true, min: 0, compact: true },

  { key: 'command', label: '统率', kind: 'attr', group: '能力', min: 0, max: 200, softMin: 1, softMax: 100, softLabel: '基础能力常规为 1~100', compact: true },
  { key: 'strength', label: '武力', kind: 'attr', group: '能力', min: 0, max: 200, softMin: 1, softMax: 100, softLabel: '基础能力常规为 1~100', compact: true },
  { key: 'intelligence', label: '智力', kind: 'attr', group: '能力', min: 0, max: 200, softMin: 1, softMax: 100, softLabel: '基础能力常规为 1~100', compact: true },
  { key: 'politics', label: '政治', kind: 'attr', group: '能力', min: 0, max: 200, softMin: 1, softMax: 100, softLabel: '基础能力常规为 1~100', compact: true },
  { key: 'glamour', label: '魅力', kind: 'attr', group: '能力', min: 0, max: 200, softMin: 1, softMax: 100, softLabel: '基础能力常规为 1~100', compact: true },

  { key: 'spearLv', label: '枪兵', kind: 'ability', group: '兵种适性', optionsKey: 'abilityLevelTypes', min: 0, max: 15, softMax: 7, softLabel: '适性等级表共 8 档（0~7）', compact: true },
  { key: 'halberdLv', label: '戟兵', kind: 'ability', group: '兵种适性', optionsKey: 'abilityLevelTypes', min: 0, max: 15, softMax: 7, softLabel: '适性等级表共 8 档（0~7）' },
  { key: 'crossbowLv', label: '弩兵', kind: 'ability', group: '兵种适性', optionsKey: 'abilityLevelTypes', min: 0, max: 15, softMax: 7, softLabel: '适性等级表共 8 档（0~7）' },
  { key: 'rideLv', label: '骑兵', kind: 'ability', group: '兵种适性', optionsKey: 'abilityLevelTypes', min: 0, max: 15, softMax: 7, softLabel: '适性等级表共 8 档（0~7）' },
  { key: 'waterLv', label: '水军', kind: 'ability', group: '兵种适性', optionsKey: 'abilityLevelTypes', min: 0, max: 15, softMax: 7, softLabel: '适性等级表共 8 档（0~7）' },
  { key: 'machineLv', label: '兵器', kind: 'ability', group: '兵种适性', optionsKey: 'abilityLevelTypes', min: 0, max: 15, softMax: 7, softLabel: '适性等级表共 8 档（0~7）' },

  { key: 'personality', label: '性格', kind: 'ref', group: '性格与倾向', optionsKey: 'personalities', min: 1, max: 100, compact: true },
  { key: 'argumentation', label: '义理', kind: 'ref', group: '性格与倾向', optionsKey: 'argumentations', min: 1, max: 100, compact: true },
  { key: 'ambition', label: '野心', kind: 'enum', group: '性格与倾向', enumMap: AMBITION_MAP, min: 0, max: 4, legacy: true },
  { key: 'hanLoyalty', label: '汉室态度', kind: 'enum', group: '性格与倾向', enumMap: HAN_LOYALTY_MAP, min: 0, max: 2, legacy: true, desc: '概念对应 C# kanshitsu 字段' },
  { key: 'strategic_tendency', label: '战略倾向', kind: 'enum', group: '性格与倾向', enumMap: STRATEGIC_TENDENCY_MAP, min: 0, max: 3, legacy: true },
  { key: 'promotion', label: '仕官倾向', kind: 'enum', group: '性格与倾向', enumMap: PROMOTION_MAP, min: 0, max: 4, legacy: true },
  { key: 'local_affiliation', label: '地方归属', kind: 'enum', group: '性格与倾向', enumMap: LOCAL_AFFILIATION_MAP, min: 0, max: 2, legacy: true },
  { key: 'compatibility', label: '相性', kind: 'int', group: '性格与倾向', min: 0, max: 255, softMax: 150, softLabel: '剧本内常见 0~149' },
  { key: 'birthplace', label: '出生州', kind: 'ref', group: '性格与倾向', optionsKey: 'provinces', min: 0, max: 100 },
  { key: 'generation', label: '世代', kind: 'enum', group: '性格与倾向', enumMap: GENERATION_MAP, min: 0, max: 4, legacy: true },

  { key: 'appearance', label: '登场年', kind: 'int', group: '生卒与登场', min: 0, max: 400, softMin: 100, softMax: 999, softLabel: '常规为公元年份' },
  { key: 'yearBorn', label: '出生年', kind: 'int', group: '生卒与登场', min: 0, max: 400, softMin: 100, softMax: 999, softLabel: '常规为公元年份' },
  { key: 'yearDead', label: '死亡年', kind: 'int', group: '生卒与登场', min: 0, max: 400, softMin: 100, softMax: 999, softLabel: '常规为公元年份' },
  { key: 'oldAge', label: '寿命', kind: 'int', group: '生卒与登场', min: 1, max: 400, softMax: 255, softLabel: '255 常作为「无限」哨兵值', legacy: true },
  { key: 'old_age', label: '寿命(旧)', kind: 'int', group: '生卒与登场', min: 1, max: 400, softMax: 255, legacy: true, desc: '与 oldAge 重复的旧键，通常保持同值' },
  { key: 'death_type', label: '死亡方式', kind: 'enum', group: '生卒与登场', enumMap: DEATH_TYPE_MAP, min: 0, max: 1, legacy: true },

  { key: 'headIconID', label: '头像 ID', kind: 'int', group: '形象资源', min: 0, max: 99999, desc: '对应 Assets/Face/{id}_N 的头像贴图编号' },
  {
    key: 'imageID',
    label: '立绘 ID',
    kind: 'int',
    group: '形象资源',
    min: 0,
    max: 99999,
    desc: 'C# 中声明为 string（imageID），剧本里写成数字，Newtonsoft 会自动转换',
  },
  { key: 'tone', label: '语气', kind: 'enum', group: '形象资源', enumMap: TONE_MAP, min: 0, max: 15 },
  { key: 'voice', label: '声音', kind: 'enum', group: '形象资源', enumMap: VOICE_MAP, min: 0, max: 7 },
  { key: 'skeleton', label: '体型', kind: 'enum', group: '形象资源', enumMap: SKELETON_MAP, min: 0, max: 3, legacy: true },
  { key: 'body', label: '身体部件', kind: 'intArray', group: '形象资源', arrayLen: 8, itemMin: -1, itemMax: 9999, legacy: true, desc: '原版 8 项部位资源索引，-1 表示无' },

  {
    key: 'ketsuen',
    label: '血缘',
    kind: 'int',
    group: '人际关系',
    min: 0,
    legacy: true,
    desc: '现状：C# 的 Person 类没有该字段，会落进 JsonExtensionData 且无人读取；语义疑似对应 consanguinity，但当前不映射',
  },
  { key: 'Father', label: '父亲', kind: 'ref', group: '人际关系', ref: 'personSet', zeroMeansNone: true, min: 0 },
  { key: 'Mother', label: '母亲', kind: 'ref', group: '人际关系', ref: 'personSet', zeroMeansNone: true, min: 0 },
  { key: 'Brother', label: '兄弟', kind: 'ref', group: '人际关系', ref: 'personSet', zeroMeansNone: true, min: 0 },
  { key: 'SpouseList', label: '配偶', kind: 'polyRef', group: '人际关系', itemRef: 'personSet', itemMin: 1, itemLabel: '武将 ID' },
  { key: 'LikePersonList', label: '喜欢武将', kind: 'polyRef', group: '人际关系', itemRef: 'personSet', itemMin: 1, itemLabel: '武将 ID' },
  { key: 'HatePersonList', label: '厌恶武将', kind: 'polyRef', group: '人际关系', itemRef: 'personSet', itemMin: 1, itemLabel: '武将 ID' },

  { key: 'wadai', label: '得意话题', kind: 'enum', group: '舌战', enumMap: WADAI_MAP, min: 0, max: 2, desc: '对应 C# 舌战 Topic 枚举：0 故事 / 1 道理 / 2 时节' },
  {
    key: 'wordTac',
    label: '舌战话术',
    kind: 'intArray',
    group: '舌战',
    itemEnumMap: RHETORIC_MAP,
    itemMin: 0,
    itemMax: 4,
    arrayMaxLen: 8,
    itemLabel: '话术',
    desc: '当前 C# 生效的字段，取值对应舌战 Rhetoric 枚举',
  },
  {
    key: 'wajutsu',
    label: '话术（旧）',
    kind: 'intArray',
    group: '舌战',
    itemMin: 0,
    itemMax: 99,
    arrayMaxLen: 8,
    legacy: true,
    itemLabel: '旧索引',
    desc: 'C# 的 Person 类没有该字段，当前不会被读取；其编码（剧本中为 3~7）与 wordTac 的 0~4 不是同一套，因此不做名称转换',
  },

  // 装备：C# 中生效的是下面三个字段；horse / left_weapon / right_weapon 是旧键，不会被读取。
  {
    key: 'EquippedWeapon',
    label: '装备·武器',
    kind: 'ref',
    group: '装备与随身',
    optionsKey: 'itemTypes',
    enumExtra: [{ value: -1, label: '无' }],
    zeroMeansNone: true,
    min: -1,
    desc: '当前 C# 生效的武器槽，值为 ItemTypes.Id，≤0 表示无装备',
  },
  {
    key: 'EquippedHorse',
    label: '装备·坐骑',
    kind: 'ref',
    group: '装备与随身',
    optionsKey: 'itemTypes',
    enumExtra: [{ value: -1, label: '无' }],
    zeroMeansNone: true,
    min: -1,
    desc: '当前 C# 生效的坐骑槽，值为 ItemTypes.Id，≤0 表示无装备',
  },
  {
    key: 'EquippedArmor',
    label: '装备·防具',
    kind: 'ref',
    group: '装备与随身',
    optionsKey: 'itemTypes',
    enumExtra: [{ value: -1, label: '无' }],
    zeroMeansNone: true,
    min: -1,
    desc: '当前 C# 生效的防具槽，值为 ItemTypes.Id，≤0 表示无装备',
  },
  { key: 'horse', label: '装备马（旧键）', kind: 'int', group: '装备与随身', min: -1, legacy: true, desc: 'C# 不读取该键；装备请写 EquippedHorse' },
  { key: 'left_weapon', label: '左手武器（旧键）', kind: 'int', group: '装备与随身', min: -1, legacy: true, desc: 'C# 不读取该键；当前只有单一武器槽 EquippedWeapon' },
  { key: 'right_weapon', label: '右手武器（旧键）', kind: 'int', group: '装备与随身', min: -1, legacy: true, desc: 'C# 不读取该键；当前只有单一武器槽 EquippedWeapon' },
  { key: 'FeatureList', label: '特技', kind: 'polyRef', group: '装备与随身', itemOptionsKey: 'features', itemMin: 1, itemMax: 1000, itemLabel: '特技 ID', desc: '引用公共数据表 Features' },
  {
    key: 'itemStore',
    label: '随身物品',
    kind: 'pairArray',
    group: '装备与随身',
    itemOptionsKey: 'storeKinds',
    itemMin: 0,
    itemLabel: '存放类别',
    desc: '成对存放 [存放类别, 数量]。键是 ItemTypes.storeKind（如枪=2、军马=5），不是物品 Id',
  },
]

/* ------------------------------------------------------------------ */
/* 都市字段                                                            */
/* ------------------------------------------------------------------ */

/** 都市编辑表单分组 */
export const CITY_GROUPS: GroupDef[] = [
  { name: '基本', desc: '名称与建筑类型' },
  { name: '归属', desc: '势力 / 军团归属' },
  { name: '规模与州', desc: '城市等级、所属州、特色' },
  { name: '内政数值', desc: '民心、治安、商业、农业等' },
  { name: '资源与兵力', desc: '资金、兵粮、兵力、人口' },
  { name: '上限与产出', desc: '各类上限与基础收益' },
  { name: '坐标与地图', desc: '坐标、朝向、模型与相邻都市' },
  { name: '原版遗留', desc: 'C# 未声明、按扩展数据透传的字段' },
]

/** 都市字段定义 */
export const CITY_FIELDS: FieldDef[] = [
  { key: 'Id', label: 'ID', kind: 'int', group: '基本', min: 1, max: 9999, noBatch: true, compact: true, desc: '同时作为 citySet 的键' },
  { key: 'Name', label: '名称', kind: 'string', group: '基本', compact: true },
  { key: 'BuildingType', label: '建筑类型', kind: 'ref', group: '基本', optionsKey: 'buildingTypes', min: 1, max: 999, compact: true, desc: '1 都市 / 2 关所 / 3 港口' },

  { key: 'BelongForce', label: '所属势力', kind: 'ref', group: '归属', ref: 'forceSet', zeroMeansNone: true, min: 0, compact: true },
  { key: 'BelongCorps', label: '所属军团', kind: 'ref', group: '归属', ref: 'corpsSet', zeroMeansNone: true, min: 0 },
  { key: 'BelongCity', label: '所属都市', kind: 'ref', group: '归属', ref: 'citySet', zeroMeansNone: true, min: 0 },

  { key: 'CityLevelType', label: '城市规模', kind: 'ref', group: '规模与州', optionsKey: 'cityLevelTypes', min: 1, max: 100, compact: true },
  { key: 'province', label: '所属州', kind: 'ref', group: '规模与州', optionsKey: 'provinces', min: 1, max: 100, compact: true },
  {
    key: 'specialty',
    label: '都市特色',
    kind: 'int',
    group: '原版遗留',
    min: 0,
    max: 99,
    legacy: true,
    desc: 'C# 的 City 类没有该字段（specialtyId 已被注释），当前不会被读取，取值含义也无从确认（剧本中出现 0~6）',
  },

  { key: 'popularSupport', label: '民心', kind: 'int', group: '内政数值', min: 0, max: 100, compact: true },
  { key: 'security', label: '治安', kind: 'int', group: '内政数值', min: 0, softMax: 100, softLabel: '常规为 0~100', compact: true },
  { key: 'morale', label: '士气', kind: 'int', group: '内政数值', min: 0, softMax: 150, softLabel: '常规为 0~100' },
  { key: 'energy', label: '战意', kind: 'int', group: '内政数值', min: 0, softMax: 150, softLabel: '常规为 0~100' },
  { key: 'commerce', label: '商业', kind: 'int', group: '内政数值', min: 0 },
  { key: 'agriculture', label: '农业', kind: 'int', group: '内政数值', min: 0 },
  { key: 'commerceLimit', label: '商业上限', kind: 'int', group: '上限与产出', min: 0 },
  { key: 'agricultureLimit', label: '农业上限', kind: 'int', group: '上限与产出', min: 0 },

  { key: 'gold', label: '资金', kind: 'int', group: '资源与兵力', min: 0, compact: true },
  { key: 'food', label: '兵粮', kind: 'int', group: '资源与兵力', min: 0, compact: true },
  { key: 'troops', label: '兵力', kind: 'int', group: '资源与兵力', min: 0, compact: true },
  { key: 'woundedTroops', label: '伤兵', kind: 'int', group: '资源与兵力', min: 0 },
  { key: 'population', label: '人口', kind: 'int', group: '资源与兵力', min: 0 },
  { key: 'troopPopulation', label: '兵役人口', kind: 'int', group: '资源与兵力', min: 0 },
  { key: 'hasBusiness', label: '商人兑换率', kind: 'int', group: '资源与兵力', min: 0, max: 100, legacy: true },

  { key: 'goldLimit', label: '资金上限', kind: 'int', group: '上限与产出', min: 0 },
  { key: 'foodLimit', label: '兵粮上限', kind: 'int', group: '上限与产出', min: 0 },
  { key: 'storeLimit', label: '库存上限', kind: 'int', group: '上限与产出', min: 0 },
  { key: 'troopsLimit', label: '兵力上限', kind: 'int', group: '上限与产出', min: 0 },
  { key: 'durabilityLimit', label: '耐久上限', kind: 'int', group: '上限与产出', min: 0 },
  { key: 'baseGainGold', label: '基础金收入', kind: 'int', group: '上限与产出', min: 0 },
  { key: 'baseGainFood', label: '基础粮收入', kind: 'int', group: '上限与产出', min: 0 },

  { key: 'x', label: 'X 坐标', kind: 'int', group: '坐标与地图', min: 0, max: 999, compact: true },
  { key: 'y', label: 'Y 坐标', kind: 'int', group: '坐标与地图', min: 0, max: 999, compact: true },
  { key: 'rot', label: '朝向', kind: 'float', group: '坐标与地图', min: -7, max: 7, legacy: true, desc: '弧度制，游戏中取 0 / 1.5708 / 3.1416 / 4.7124' },
  { key: 'heightOffset', label: '高度偏移', kind: 'int', group: '坐标与地图', min: -1000, max: 1000, legacy: true },
  { key: 'model', label: '模型路径', kind: 'string', group: '坐标与地图', legacy: true },
  { key: 'NeighborList', label: '相邻都市', kind: 'polyRef', group: '坐标与地图', itemRef: 'citySet', itemMin: 1, itemLabel: '都市 ID', desc: '应为双向关系：A 的列表含 B，则 B 的列表也应含 A' },

  {
    key: 'itemStore',
    label: '库存',
    kind: 'pairArray',
    group: '原版遗留',
    itemOptionsKey: 'storeKinds',
    itemMin: 0,
    itemLabel: '存放类别',
    desc: '成对存放 [存放类别, 数量]，键是 ItemTypes.storeKind（枪=2、戟=3、弓=4、军马=5、器械=6/7、船=8），不是物品 Id',
  },
  { key: 'insideSlot', label: '城内槽位', kind: 'int', group: '原版遗留', min: 0, legacy: true },
  { key: 'outsideSlot', label: '城外槽位', kind: 'int', group: '原版遗留', min: 0, legacy: true },
  { key: 'villageSlot', label: '村庄槽位', kind: 'int', group: '原版遗留', min: 0, legacy: true },
  { key: 'event', label: '事件', kind: 'int', group: '原版遗留', min: 0, legacy: true },
  { key: 'actionFlag', label: '行动标记', kind: 'int', group: '原版遗留', min: 0, legacy: true },
]

/* ------------------------------------------------------------------ */
/* 军团字段                                                            */
/* ------------------------------------------------------------------ */

/** 军团编辑表单分组 */
export const CORPS_GROUPS: GroupDef[] = [
  { name: '基本', desc: '军团番号与指挥官' },
  { name: '方针', desc: '军团政策与目标' },
]

/** 军团字段定义 */
export const CORPS_FIELDS: FieldDef[] = [
  { key: 'Id', label: 'ID', kind: 'int', group: '基本', min: 1, max: 9999, noBatch: true, compact: true, desc: '同时作为 corpsSet 的键' },
  { key: 'BelongForce', label: '所属势力', kind: 'ref', group: '基本', ref: 'forceSet', zeroMeansNone: true, min: 0, compact: true },
  { key: 'Comander', label: '军团长', kind: 'ref', group: '基本', ref: 'personSet', min: 0, compact: true },
  { key: 'number', label: '军团番号', kind: 'int', group: '基本', min: 1, max: 50, compact: true, desc: '1 为主力军团' },
  { key: 'policy', label: '政策类型', kind: 'int', group: '方针', min: 0, max: 99, compact: true, desc: 'C# 中只是裸 int，没有枚举也没有读取逻辑，取值含义未在代码中定义（剧本中为 1 与 4）' },
  { key: 'policy_target', label: '政策目标', kind: 'int', group: '方针', min: -1, desc: 'C# 未解析该字段，无法确认是势力 ID 还是都市 ID（剧本中为 -1~5）' },
  { key: 'mid_objective', label: '中期目标类型', kind: 'int', group: '方针', min: 0, max: 99, desc: 'C# 未解析该字段（剧本中恒为 0）' },
  { key: 'mid_objective_target', label: '中期目标', kind: 'int', group: '方针', min: -1, desc: 'C# 未解析该字段（剧本中恒为 -1）' },
  {
    key: 'appoint',
    label: '委任',
    kind: 'enum',
    group: '方针',
    enumMap: APPOINT_TYPE_MAP,
    min: 0,
    max: 3,
    desc: 'C# 中由 AppointType 驱动，是当前真正生效的目标字段；本剧本未定义该键',
  },
  {
    key: 'appoint_target',
    label: '委任目标',
    kind: 'int',
    group: '方针',
    min: -1,
    zeroMeansNone: true,
    desc: '多态引用：委任为「攻略势力」时是势力 ID，为「攻占城池」时是都市 ID（见 Corps.CheckTargetIsAppointTarget）',
  },
]

/* ------------------------------------------------------------------ */
/* 势力字段                                                            */
/* ------------------------------------------------------------------ */

/** 势力编辑表单分组 */
export const FORCE_GROUPS: GroupDef[] = [
  { name: '基本', desc: '君主、军师与旗帜爵位' },
  { name: '科技', desc: '技巧点与已研究的技巧' },
  { name: '外交与描述', desc: '同盟、方针与势力介绍' },
  { name: '原版遗留', desc: 'C# 未声明、按扩展数据透传的字段' },
]

/** 势力字段定义 */
export const FORCE_FIELDS: FieldDef[] = [
  { key: 'Id', label: 'ID', kind: 'int', group: '基本', min: 1, max: 9999, noBatch: true, compact: true, desc: '同时作为 forceSet 的键' },
  { key: 'Governor', label: '君主', kind: 'ref', group: '基本', ref: 'personSet', min: 0, compact: true },
  { key: 'Counsellor', label: '军师', kind: 'ref', group: '基本', ref: 'personSet', zeroMeansNone: true, min: 0, compact: true },
  { key: 'Flag', label: '旗帜', kind: 'ref', group: '基本', optionsKey: 'flags', min: 0, max: 999, compact: true },
  { key: 'Title', label: '爵位', kind: 'ref', group: '基本', optionsKey: 'titles', min: 1, max: 100, compact: true },

  { key: 'TechniquePoint', label: '技巧点', kind: 'int', group: '科技', min: 0, compact: true },
  { key: 'Techniques', label: '已研究技巧', kind: 'intArray', group: '科技', itemOptionsKey: 'techniques', itemMin: 1, itemLabel: '技巧' },
  { key: 'InitTechniques', label: '初期技巧', kind: 'intArray', group: '科技', itemOptionsKey: 'techniques', itemMin: 1, itemLabel: '技巧' },

  {
    key: 'PolicyType',
    label: '势力方针',
    kind: 'int',
    group: '外交与描述',
    min: 0,
    max: 99,
    desc: 'C# 中生效的势力方针字段；本剧本未定义该键',
  },
  {
    key: 'allies',
    label: '同盟势力（旧键）',
    kind: 'intArray',
    group: '外交与描述',
    itemMin: 0,
    legacy: true,
    desc: 'C# 的 Force 类没有该成员，当前不会被读取，引用目标也无从确认（剧本中恒为空数组）',
  },
  { key: 'policy', label: '势力方针（旧键）', kind: 'int', group: '外交与描述', min: 0, max: 99, legacy: true, desc: 'C# 的 Force 类没有该成员，当前不会被读取；生效的是 PolicyType' },
  { key: 'policy_target', label: '方针目标（旧键）', kind: 'int', group: '外交与描述', min: -1, legacy: true, desc: 'C# 的 Force 类没有该成员，当前不会被读取' },
  { key: 'desc', label: '势力介绍', kind: 'string', group: '外交与描述', legacy: true },
  {
    key: 'like',
    label: '势力好感表（旧键）',
    kind: 'intArray',
    group: '原版遗留',
    arrayLen: 47,
    itemMin: 0,
    itemMax: 999,
    legacy: true,
    desc: 'C# 的 Force 类没有该成员，当前不会被读取；形状像「对 47 个势力的好感度表」，剧本中取值 1 / 51 / 61 / 81 / 101',
  },
]

/* ------------------------------------------------------------------ */
/* 剧本信息字段                                                        */
/* ------------------------------------------------------------------ */

/** 剧本信息字段定义 */
export const INFO_FIELDS: FieldDef[] = [
  { key: 'name', label: '剧本名', kind: 'string', group: '基本' },
  { key: 'id', label: '剧本 ID', kind: 'int', group: '基本', min: -1, max: 9999 },
  { key: 'tag', label: '标签', kind: 'string', group: '基本' },
  { key: 'type', label: '类型', kind: 'int', group: '基本', min: 0, max: 99 },
  { key: 'mapType', label: '地图类型', kind: 'string', group: '基本' },
  { key: 'year', label: '年份', kind: 'int', group: '时间', min: 0, max: 999 },
  { key: 'month', label: '月份', kind: 'int', group: '时间', min: 1, max: 12 },
  { key: 'day', label: '日', kind: 'int', group: '时间', min: 1, max: 31 },
  { key: 'description', label: '剧本描述', kind: 'string', group: '描述' },
]

/** 集合 -> 字段定义 */
export const COLLECTION_FIELDS: Record<CollectionKey, FieldDef[]> = {
  personSet: PERSON_FIELDS,
  citySet: CITY_FIELDS,
  corpsSet: CORPS_FIELDS,
  forceSet: FORCE_FIELDS,
}

/** 集合 -> 分组定义 */
export const COLLECTION_GROUPS: Record<CollectionKey, GroupDef[]> = {
  personSet: PERSON_GROUPS,
  citySet: CITY_GROUPS,
  corpsSet: CORPS_GROUPS,
  forceSet: FORCE_GROUPS,
}

/** 各集合的默认列（表格 compact 列） */
export function compactFields(fields: FieldDef[]): FieldDef[] {
  return fields.filter((f) => f.compact)
}

/** 取字段定义 */
export function findField(fields: FieldDef[], key: string): FieldDef | undefined {
  return fields.find((f) => f.key === key)
}

/**
 * 解析字段的枚举选项。
 *
 * 优先级：静态 enumMap > optionsKey 指向的公共数据表 > 空。
 *
 * @param field 字段定义
 * @param options 公共数据表
 * @returns 枚举选项数组
 */
export function resolveEnum(field: FieldDef, options: Options | null): EnumEntry[] {
  const base = baseEnum(field, options)
  if (base.length === 0 && field.itemEnumMap && field.itemEnumMap.length > 0) {
    return field.itemEnumMap
  }
  const extra = field.enumExtra
  if (!extra || extra.length === 0) return base
  const known = new Set(base.map((e) => e.value))
  return [...extra.filter((e) => !known.has(e.value)), ...base]
}

/**
 * 取字段的基础候选列表（不含 enumExtra）。
 *
 * @param field 字段定义
 * @param options 公共数据表
 * @returns 候选列表
 */
function baseEnum(field: FieldDef, options: Options | null): EnumEntry[] {
  if (field.enumMap) return field.enumMap
  const key = field.optionsKey ?? field.itemOptionsKey
  if (key && options) {
    const list = (options as unknown as Record<string, unknown>)[key] as OptionItem[] | undefined
    if (Array.isArray(list)) return list.map((x) => ({ value: x.id, label: x.name }))
  }
  return []
}
