/**
 * 剧本编辑器 —— 类型定义
 *
 * 说明：剧本数据结构由 C# 侧 Newtonsoft 序列化产生，且大量「原版遗留字段」
 * 会被原样透传，因此这里采用「已知字段可选 + 索引签名兜底」的宽松建模，
 * 保证既能获得类型提示，又不会在读写时丢失未知字段。
 */

/** 通用实体：所有集合元素都至少拥有 Id */
export interface Entity {
  Id: number
  Name?: string
  [key: string]: unknown
}

/** 剧本基本信息（ScenarioInfo） */
export interface ScenarioInfo {
  id?: number
  name?: string
  type?: number
  tag?: string
  description?: string
  year?: number
  month?: number
  day?: number
  mapType?: string
  curForceId?: number
  curForceName?: string
  turnCount?: number
  priority?: number
  isSave?: boolean
  playerForceList?: number[]
  dateTime?: number
  [key: string]: unknown
}

/** 剧本根对象 */
export interface Scenario {
  Id: number
  Name: string
  Info: ScenarioInfo
  forceSet: Record<string, Entity>
  corpsSet: Record<string, Entity>
  citySet: Record<string, Entity>
  personSet: Record<string, Entity>
  [key: string]: unknown
}

/** 受管理的四大集合键名 */
export type CollectionKey = 'forceSet' | 'corpsSet' | 'citySet' | 'personSet'

/** 武将 / 都市 / 军团 / 势力的业务标签 */
export const COLLECTION_META: Record<
  CollectionKey,
  { key: CollectionKey; label: string; singular: string; entityLabel: string }
> = {
  forceSet: { key: 'forceSet', label: '势力', singular: '势力', entityLabel: '势力' },
  corpsSet: { key: 'corpsSet', label: '军团', singular: '军团', entityLabel: '军团' },
  citySet: { key: 'citySet', label: '都市', singular: '都市', entityLabel: '都市' },
  personSet: { key: 'personSet', label: '武将', singular: '武将', entityLabel: '武将' },
}

/** 集合展示顺序 */
export const COLLECTION_ORDER: CollectionKey[] = ['personSet', 'citySet', 'corpsSet', 'forceSet']

/** 剧本文件元信息 */
export interface ScenarioMeta {
  file: string
  size: number
  mtime: string
  revision: string
}

/** 选项表条目 */
export interface OptionItem {
  id: number
  name: string
  [key: string]: unknown
}

/** 后端下发的公共数据 / 枚举选项 */
export interface Options {
  generatedAt: string
  provinces: OptionItem[]
  regions: OptionItem[]
  titles: OptionItem[]
  officials: OptionItem[]
  personalities: OptionItem[]
  argumentations: OptionItem[]
  personLevels: OptionItem[]
  abilityLevelTypes: OptionItem[]
  attributeChangeTypes: OptionItem[]
  features: OptionItem[]
  cityLevelTypes: OptionItem[]
  buildingTypes: OptionItem[]
  flags: OptionItem[]
  techniques: OptionItem[]
  itemTypes: OptionItem[]
  skills: OptionItem[]
  jobTypes: OptionItem[]
  terrainTypes: OptionItem[]
  troopTypes: OptionItem[]
  buffs: OptionItem[]
  troopAnimations: OptionItem[]
  /** 兵装/道具的存放类别（ItemTypes.storeKind 聚合，itemStore 数组的键） */
  storeKinds: OptionItem[]
}

/**
 * 武将库分库信息。
 *
 * 武将库网站维护两个库：`base`（基础武将库）与 `custom`（自建武将库）。
 * 编辑器两个都拉，这里用于在界面上分组、显示来源与条数。
 */
export interface LibraryLibInfo {
  /** 库标识：base / custom */
  key: string
  /** 中文名，如「自建武将库」 */
  label: string
  /** 该库命中的武将条数 */
  count: number
  /** 实际数据来源描述（网站接口 / 降级文件） */
  origin: string
  /** 该库的取数错误（成功时为空串） */
  error: string
}

/** 武将库中的归一化武将记录 */
export interface LibraryPerson {
  Id: number
  /** 所属库标识：base / custom */
  lib: string
  Name: string
  familyName: string
  giveName: string
  nickName: string
  description: string
  sex?: number
  appearance?: number
  yearBorn?: number
  yearDead?: number
  compatibility?: number
  state?: number
  personality?: number
  argumentation?: number
  type?: number
  headIconID?: number
  /** 暴击图（立绘）相对路径 */
  image?: string
  /** 老年暴击图相对路径；为空表示与暴击图共用 */
  image_old?: string
  loyalty?: number
  Official?: number
  Level?: number
  command: number
  strength: number
  intelligence: number
  politics: number
  glamour: number
  spearLv: number
  halberdLv: number
  crossbowLv: number
  rideLv: number
  waterLv: number
  machineLv: number
  FeatureList?: number[]
  LikePersonList?: number[]
  HatePersonList?: number[]
  SpouseList?: number[]
  Father?: number
  Mother?: number
  Brother?: number
  [key: string]: unknown
}

/** 备份记录 */
export interface BackupItem {
  name: string
  at: string
  size: number
  reason: string
}

/* ------------------------------------------------------------------ */
/* 公共数据表（Build/Content/Data/Common）                             */
/* ------------------------------------------------------------------ */

/** 公共数据表中的一个分区（一个文件可能有多个分区，如 ai.json） */
export interface CommonSection {
  /** 定位键；null 表示根节点 */
  key: string | null
  /** records 记录表 / values 值列表 / config 键值配置 / value 单值 */
  kind: 'records' | 'values' | 'config' | 'value'
  /** 记录表的承载形式 */
  form: 'object' | 'array' | 'scalar'
  /** 条目数量 */
  entryCount: number
  /** 抽样字段名 */
  sampleFields: string[]
  /** kind === 'config' 且只覆盖根的一部分键时给出 */
  keys?: string[]
}

/** 公共数据表结构摘要 */
export interface CommonSummary {
  wrapper: string | null
  rootKeys: string[]
  sections: CommonSection[]
  kind: string
  entryCount: number
}

/** 公共数据表文件信息（列表用） */
export interface CommonFileInfo {
  name: string
  size: number
  mtime: string
  revision: string
  /** 是否含 JSONC 注释 */
  jsonc: boolean
  eol: string
  indent: number
  parseError: string | null
  summary: CommonSummary | null
}

/** 公共数据表文档（读取用） */
export interface CommonFileDoc {
  ok?: boolean
  name: string
  file: string
  raw: string
  value: unknown
  parseError: string | null
  jsonc: boolean
  eol: string
  indent: number
  revision: string
  size: number
  mtime: string
  summary: CommonSummary
}

/** 公共数据表备份项 */
export interface CommonBackupItem {
  dir: string
  name: string
  at: string
  reason: string
  size: number
}

/** 校验问题级别 */
export type IssueLevel = 'error' | 'warning' | 'info'

/** 单条校验问题 */
export interface ValidationIssue {
  level: IssueLevel
  collection: CollectionKey | 'root'
  entityId: number | null
  field: string
  message: string
  /** 稳定标识，用于去重与定位 */
  key: string
}
