/**
 * 文件名：types.ts
 * 描述：武将库前端类型定义，字段与游戏 PersonLib / PersonTypeEnum 保持一致。
 */

/** 武将（武将库中的一条记录） */
export interface Person {
  /** 固定 ID。基础武将库由游戏本体写死；自建武将库由服务端分配，永不改变/复用 */
  Id: number
  /** 姓名（由姓 + 名推导，作为同名判定依据） */
  Name: string
  /** 武将类型（对应游戏 PersonTypeEnum） */
  type: number
  /** 姓 */
  familyName: string
  /** 名 */
  giveName: string
  /** 字（基础武将库中可能不存在） */
  nickName?: string
  /** 列传 / 生平。自建库为字符串，基础库为列传编号（数值） */
  description: string | number
  /** 头像 ID */
  headIconID: number
  /** 立绘 ID（弃用）。自建库为字符串，基础库为数值 */
  imageID?: string | number | null
  /** 立绘 ID */
  image: string
  /** 旧的立绘字段 */
  image_old?: string | null
  /** 性别：0=男，1=女 */
  sex: number
  /**
   * 登场年份（对应游戏的 Person.appearance；
   * 旧版数据文件中的键名为 yearAvailable，已统一改名）
   */
  appearance: number
  /** 出生年 */
  yearBorn: number
  /** 死亡年 */
  yearDead: number
  /** 相性 */
  compatibility: number
  /** 身份 */
  state: number
  /** 性格 */
  personality: number
  /** 义理 */
  argumentation: number
  /** 音声（基础武将库中不存在） */
  voice?: number
  /** 语气（基础武将库中不存在） */
  tone?: number
  /** 汉室态度（基础武将库中不存在） */
  kanshitsu?: number
  /** 理想（基础武将库中不存在） */
  ideal?: number
  /** 才干（基础武将库中不存在） */
  talent?: number
  /** 功绩（基础武将库中不存在） */
  merit?: number
  /** 体力（基础武将库中不存在） */
  stamina?: number
  /** 等级 */
  Level: number
  /** 统御：[基础值, 成长类型Id, 经验, 万分比, 最终值] */
  command: number[]
  /** 武力：[基础值, 成长类型Id, 经验, 万分比, 最终值] */
  strength: number[]
  /** 智力：[基础值, 成长类型Id, 经验, 万分比, 最终值] */
  intelligence: number[]
  /** 政治：[基础值, 成长类型Id, 经验, 万分比, 最终值] */
  politics: number[]
  /** 魅力：[基础值, 成长类型Id, 经验, 万分比, 最终值] */
  glamour: number[]
  /** 血缘（基础武将库中不存在） */
  consanguinity?: number
  /** 父亲 ID（基础武将库中为可选字段） */
  Father?: number
  /** 母亲 ID（基础武将库中为可选字段） */
  Mother?: number
  /** 配偶 ID 列表（基础武将库中为可选字段） */
  SpouseList?: number[] | null
  /** 兄弟（基础武将库中为可选字段） */
  Brother?: number
  /** 兄弟 ID 列表（基础武将库中不存在） */
  BrotherList?: number[] | null
  /** 喜欢武将 ID 列表（基础武将库中为可选字段） */
  LikePersonList?: number[] | null
  /** 厌恶武将 ID 列表（基础武将库中为可选字段） */
  HatePersonList?: number[] | null
  /** 枪（矛）适性：[基础值(等级), 经验, 等级] */
  spearLv: number[]
  /** 戟适性：[基础值(等级), 经验, 等级] */
  halberdLv: number[]
  /** 弓弩适性：[基础值(等级), 经验, 等级] */
  crossbowLv: number[]
  /** 骑适性：[基础值(等级), 经验, 等级] */
  rideLv: number[]
  /** 水军适性：[基础值(等级), 经验, 等级] */
  waterLv: number[]
  /** 器械适性：[基础值(等级), 经验, 等级] */
  machineLv: number[]
  /** 经验（基础武将库中不存在） */
  Exp?: number
  /** 武将特性 ID 列表（基础武将库中为可选字段） */
  FeatureList?: number[] | null
  /** 是否存活（基础武将库中不存在） */
  IsAlive?: boolean
  /** 是否已行动完毕（基础武将库中不存在） */
  ActionOver?: boolean
  /** 武将标签（自建武将库专有，用于分类与按标签筛选；基础武将库中不存在） */
  tags?: string[]
  /** 最后修改时间（自建武将库专有，ISO 8601 UTC 字符串，由服务端写入，用于按修改时间排序） */
  updatedAt?: string

  /** 基础武将库专有：归属势力 */
  BelongForce?: number
  /** 基础武将库专有：归属军团 */
  BelongCorps?: number
  /** 基础武将库专有：归属城市 */
  BelongCity?: number
  /** 基础武将库专有：官职 */
  Official?: number
  /** 基础武将库专有：忠诚度 */
  loyalty?: number
}

// ─────────────────────────────────────────────────────────────
// 武将导入
// ─────────────────────────────────────────────────────────────

/** 因重名被改名的记录 */
export interface ImportRename {
  /** 文件中的原始姓名 */
  from: string
  /** 改名后的姓名（追加 #1 / #2 … 后缀） */
  to: string
}

/** 被覆盖的自建武将记录 */
export interface ImportOverwrite {
  /** 被覆盖武将的 ID（沿用原 ID，不变） */
  id: number
  /** 覆盖后的姓名 */
  name: string
}

/** 武将批量导入接口返回 */
export interface ImportResult {
  /** 新建的武将数量（覆盖既有武将不计入） */
  imported: number
  /** 被覆盖的自建武将记录 */
  overwritten: ImportOverwrite[]
  /** 因姓名为空被跳过的数量 */
  skipped: number
  /** 重名改名记录 */
  renamed: ImportRename[]
  /** 本次写入的武将列表（含新建与被覆盖，均含最终 ID 与姓名） */
  persons: Person[]
  /** 导入后库中武将总数 */
  count: number
  /** 导入后的下一个可用 ID */
  nextId: number
}

/** 武将批量删除接口返回 */
export interface BatchDeleteResult {
  /** 实际删除的数量 */
  deleted: number
  /** 被删除的武将 */
  persons: Person[]
  /** 删除后库中武将总数 */
  count: number
  /** 删除后的下一个可用 ID */
  nextId: number
}

/** 清空整库接口返回 */
export interface ClearResult {
  /** 被清空的武将数量 */
  removed: number
  /** 清空后库中武将总数 */
  count: number
}

/** 整体导入（基础武将库）接口返回 */
export interface BulkImportResult {
  /** 导入方式：merge=按 Id 合并，replace=替换整库 */
  mode: 'merge' | 'replace'
  /** 新增的武将数量 */
  added: number
  /** 按 Id 更新的武将数量 */
  updated: number
  /** 替换整库时被移除的武将数量 */
  removed: number
  /** 导入后库中武将总数 */
  count: number
}

/** 头像区间配置。sexType：0=男，1=女，-1=不限性别 */
export interface HeadRange {
  name: string
  startId: number
  endId: number
  sexType: number
}

/** 特性定义 */
export interface FeatureOption {
  id: number
  name: string
  desc: string
  level: number
  kind: number
}

/** 简单枚举选项 */
export interface SimpleOption {
  id: number
  name: string
}

/** 编辑选项配置 */
export interface OptionsConfig {
  features: FeatureOption[]
  personalities: SimpleOption[]
  argumentations: SimpleOption[]
  attributeChangeTypes: SimpleOption[]
  abilityLevels: SimpleOption[]
  headRanges: HeadRange[]
  /** 武将类型选项（对应 PersonTypeEnum） */
  personTypes: SimpleOption[]
}

/** 武将库标识：base=基础武将库，custom=自建武将库 */
export type LibraryKey = 'base' | 'custom'

/** 某个库允许的操作 */
export interface LibraryCapabilities {
  /** 是否允许新建 */
  create: boolean
  /** 是否允许修改 */
  edit: boolean
  /** 是否允许删除（支持单个、批量与清空整库） */
  delete: boolean
  /** 是否允许「整体导入」（基础库按 Id 合并 / 替换整库） */
  bulkImport: boolean
  /** ID 是否只读（基础武将库 ID 与游戏本体绑定，不可修改） */
  readOnlyId: boolean
  /** 是否强制姓名唯一（基础库本身存在合法的同名武将，故不校验） */
  enforceUniqueName: boolean
}

/** 武将库列表接口返回 */
export interface LibraryResponse {
  /** 库标识 */
  lib: LibraryKey
  /** 库显示名称 */
  label: string
  /** 容器 offset */
  offset: number
  /** 自建库起始 ID（基础库为 0） */
  baseId: number
  /** 下一个可用 ID（基础库为 0） */
  nextId: number
  /** 武将总数 */
  count: number
  /** 武将列表 */
  persons: Person[]
  /** 头像区间 */
  headRanges: HeadRange[]
  /** 该库中武将出现过的字段全集，用于决定编辑表单渲染哪些字段 */
  fieldKeys: string[]
  /** 容器级元数据（如新版本体数据的 type: 2），原样保留并回写 */
  containerExtras?: Record<string, unknown>
  /** 该库允许的操作（已结合当前登录身份，游客恒为只读） */
  capabilities: LibraryCapabilities
}

// ─────────────────────────────────────────────────────────────
// 账号与权限
// ─────────────────────────────────────────────────────────────

/**
 * 角色标识。
 * - super：超级管理员，在管理员基础上额外拥有「备份与还原」权限；
 * - admin：管理员，可增删改并可管理账号；
 * - editor：编辑员，可增删改；
 * - guest：游客，仅可浏览。
 */
export type Role = 'super' | 'admin' | 'editor' | 'guest'

/** 权限标识 */
export type Permission = 'read' | 'write' | 'account' | 'backup'

/** 登录用户信息 */
export interface AuthUser {
  /** 用户名 */
  username: string
  /** 显示名称 */
  displayName: string
  /** 角色 */
  role: Role
  /** 角色显示名称 */
  roleLabel?: string
  /** 创建时间 */
  createdAt?: string
}

/** 角色下拉选项 */
export interface RoleOption {
  id: Role
  name: string
}

/** 登录结果 */
export interface LoginResult {
  /** 访问令牌 */
  token: string
  /** 登录用户信息 */
  user: AuthUser
}

/** 玩家注册状态（由后端下发，用于决定是否展示注册入口） */
export interface RegisterInfo {
  /** 是否开放注册 */
  enabled: boolean
  /** 注册后的默认角色 */
  role: Role
  /** 默认角色显示名 */
  roleLabel: string
}

/** 当前身份接口返回 */
export interface CurrentUserResult {
  /** 登录用户；未登录为 null（即游客） */
  user: AuthUser | null
  /** 当前身份具备的权限集合 */
  permissions: Permission[]
  /** 可选角色列表（非超级管理员不包含「超级管理员」） */
  roles: RoleOption[]
  /** 玩家注册状态 */
  register?: RegisterInfo
}

/** 账号列表接口返回 */
export interface AccountListResult {
  accounts: AuthUser[]
  roles: RoleOption[]
}

/** 单份备份的元信息 */
export interface BackupEntry {
  /** 备份标识（目录名，YYYY-MM-DD_HHmmss） */
  id: string
  /** 创建时间（ISO 8601） */
  createdAt: string
  /** 备份原因：daily=每日自动 / manual=手动 / pre-restore=还原前快照 */
  reason: string
  /** 备份文件清单 */
  files: { name: string; size: number }[]
  /** 总大小（字节） */
  totalSize: number
  /** 两个武将库的条数与结构版本 */
  counts: {
    base?: { count: number; dataVersion: number | null }
    custom?: { count: number; dataVersion: number | null }
  }
}

/** 备份调度状态 */
export interface BackupStatus {
  /** 每日自动备份时刻（0-23 点） */
  hour: number
  /** 备份保留天数 */
  keepDays: number
  /** 备份份数上限 */
  maxSets: number
  /** 当前备份总数 */
  total: number
  /** 最新备份标识 */
  latestId: string
}

/** 备份列表接口返回 */
export interface BackupListResult {
  backups: BackupEntry[]
  status: BackupStatus
}

/** 还原备份接口返回 */
export interface RestoreResult {
  /** 被还原的备份标识 */
  id: string
  /** 实际还原的文件清单 */
  restored: string[]
  /** 自动创建的「还原前快照」标识 */
  snapshotId: string
  /** 服务端提示信息 */
  message?: string
}

// ─────────────────────────────────────────────────────────────
// 自定义头像
// ─────────────────────────────────────────────────────────────

/**
 * 自定义头像项。
 * ID 分配规则：从 3000 起，每 1000 个为一段，
 * 段千位为奇数分配给男性（3000-3999、5000-5999 …），
 * 段千位为偶数分配给女性（4000-4999、6000-6999 …）。
 */
export interface CustomFace {
  /** 头像 ID */
  id: number
  /** 归属性别：0=男，1=女 */
  sex: number
  /** 是否已有半身像（{id}_1.png） */
  hasBust: boolean
  /** 是否已有头像（{id}_2.png） */
  hasFace: boolean
}

/** 自定义头像列表接口返回 */
export interface CustomFaceResult {
  /** 起始 ID */
  baseId: number
  /** 已存在的自定义头像 */
  items: CustomFace[]
  /** 各性别的下一个可用 ID */
  nextId: { male: number; female: number }
}
