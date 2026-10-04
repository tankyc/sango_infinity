/**
 * 文件名：index.js
 * 描述：Sango Infinity 武将库（PersonLibrary）后端服务
 *       - 同时管理两个武将库：
 *         1) 基础武将库（PersonLibrary.json）：游戏内置武将，
 *            ID 与游戏本体绑定 —— 不可修改 ID、不可删除，允许修改其它字段；
 *         2) 自建武将库（CustomPerson.json）：玩家自建武将，
 *            允许新建、修改、删除，ID 自 BASE_ID 起固定分配，永不改变、永不复用。
 *       - 两个库均支持查询与下载；自建武将库额外支持批量导入（CustomPerson.json 格式）
 *       - 自建武将库以“姓 + 名”作为唯一关键值：保存时同时与基础武将库
 *         及自建库其它武将查重，若冲突则自动追加 #1、#2 …… 后缀解决重名
 *       - 武将新增 type 字段（对应游戏 PersonTypeEnum），标识武将来源类型
 *       - 账号与权限（详见 auth.js）：
 *         游客（未登录或 guest 角色）仅可浏览与下载，不能新增 / 修改 / 删除；
 *         编辑员可增删改；管理员在编辑员基础上还可管理账号。
 * 创建日期：2026-09-10
 */

import express from 'express'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'
import {
  PERMISSIONS,
  REGISTER_INFO,
  REMOTE_AUTH_INFO,
  ROLE_OPTIONS,
  ROLES,
  allowRegister,
  attachUser,
  createAccount,
  deleteAccount,
  ensureAccountForIdentity,
  hasPermission,
  initAccounts,
  issueToken,
  listAccounts,
  remoteLogin,
  remoteRegister,
  requirePermission,
  roleOptionsFor,
  toPublicAccount,
  updateAccount,
  validateNewAccount,
  verifyLogin,
} from './auth.js'
import { backupStatus, listBackups, restoreBackup, runBackup, startBackupScheduler } from './backup.js'
import {
  CUSTOM_FACE_BASE_ID,
  buildZip,
  collectFaceFiles,
  deleteCustomFace,
  exportCustomFaces,
  listCustomFaces,
  saveCustomFace,
} from './faceStore.js'
import * as r2 from './r2.js'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

/** 服务监听端口 */
const PORT = Number(process.env.PORT) || 3001

/** 自建武将库固定 ID 起始值（ID 从 1000 开始） */
const BASE_ID = 1000

/** 自建武将库容器 offset 字段，与游戏 SangoObjectOffSet 保持一致 */
const OFFSET = BASE_ID

/**
 * 武将库数据文件的结构版本标记键名（写在 PersonLibrary 容器级）。
 * 与游戏侧 PersonLibraryDataFormat.VersionKey 保持一致。
 */
const DATA_VERSION_KEY = 'dataVersion'

/**
 * 当前新版结构版本号（与游戏侧 PersonLibraryDataFormat.CurrentVersion 一致）。
 * 版本 2 的差异：
 *   · 五维为 [基础值, 成长类型Id, 经验, 万分比, 最终值] 数组；
 *   · 兵种适性为 [基础值(等级), 经验, 等级] 数组；
 *   · 登场年字段名由 yearAvailable 改为 appearance。
 */
const DATA_VERSION = 2

/** 五维能力字段（数组：[基础值, 成长类型Id, 经验, 万分比, 最终值]） */
const ABILITY_FIELDS = ['command', 'strength', 'intelligence', 'politics', 'glamour']

/** 兵种适性字段（数组：[基础值(等级), 经验, 等级]） */
const WEAPON_FIELDS = ['spearLv', 'halberdLv', 'crossbowLv', 'rideLv', 'waterLv', 'machineLv']

/** 能力数组长度 */
const ABILITY_ARRAY_LENGTH = 5

/** 适性数组长度 */
const WEAPON_ARRAY_LENGTH = 3

/** 未设置成长类型时写入的默认值（游戏内对应「5 普通型」） */
const DEFAULT_ATTRIBUTE_CHANGE_ID = 5

/** 能力「万分比」默认值（10000 = 100%） */
const DEFAULT_ATTRIBUTE_FACTER = 10000

/** 旧版登场年字段名（读取旧数据时迁移到 appearance） */
const LEGACY_APPEARANCE_KEY = 'yearAvailable'

/**
 * 武将类型枚举（与游戏 PersonTypeEnum 数值一一对应，禁止调整取值）。
 */
const PERSON_TYPES = {
  /** 历史 */
  Historical: 0,
  /** 古代 */
  Ancient: 1,
  /** 事件 */
  Event: 2,
  /** 自定义 */
  Custom: 3,
  /** 临时 */
  Temporary: 4,
  /** 未知 */
  Unknown: 5,
  /** NPC */
  NPC: 6,
  /** 生成 */
  Auto: 7,
  /** 枚举上限（非合法取值） */
  Max: 8,
}

/** type 字段的合法最小值（含） */
const PERSON_TYPE_MIN = PERSON_TYPES.Historical

/** type 字段的合法最大值（含，不含 Max） */
const PERSON_TYPE_MAX = PERSON_TYPES.Auto

/**
 * 武将库配置表。
 * 每个库的差异（是否能新建/删除、数据文件名、头像区间等）集中在此定义，
 * 便于新增更多类型的武将库。
 */
const LIB_CONFIG = {
  base: {
    key: 'base',
    /** 显示名称 */
    label: '基础武将库',
    /** 数据文件绝对路径 */
    file: path.join(__dirname, 'data', 'PersonLibrary.json'),
    /** 下载时的文件名 */
    fileName: 'PersonLibrary.json',
    /** 是否允许新建单个武将（基础库 ID 与游戏数据绑定，只能通过整体导入新增） */
    canCreate: false,
    /** 是否允许删除武将（支持单个删除、批量删除与清空整库） */
    canDelete: true,
    /** 是否允许修改武将 */
    canEdit: true,
    /** 是否允许整体导入（按文件中的 Id 合并或替换整库） */
    canBulkImport: true,
    /** 是否强制姓名唯一。基础武将库本身存在合法的同名武将，故不校验 */
    enforceUniqueName: false,
    /** 新建武将的默认类型（基础库不可新建，仅用于缺失时的缺省展示） */
    defaultType: PERSON_TYPES.Historical,
    /** 是否为“ID 由数据文件写死”的库（此类库不分配新 ID） */
    fixedId: true,
    /** 容器是否包含 offset 字段 */
    hasOffset: false,
    /** 头像（容貌）可选区间，sexType = -1 表示不限性别 */
    headRanges: [
      { name: '本体容貌', startId: 0, endId: 1999, sexType: -1 },
      { name: '自定义男性', startId: 2000, endId: 2099, sexType: 0 },
      { name: '自定义女性', startId: 2100, endId: 2173, sexType: 1 },
    ],
  },
  custom: {
    key: 'custom',
    label: '自建武将库',
    file: path.join(__dirname, 'data', 'CustomPerson.json'),
    fileName: 'CustomPerson.json',
    canCreate: true,
    canDelete: true,
    canEdit: true,
    /** 自建武将库强制姓名唯一，不允许同名武将 */
    enforceUniqueName: true,
    defaultType: PERSON_TYPES.Custom,
    fixedId: false,
    hasOffset: true,
    headRanges: [
      { name: '自定义男性', startId: 2000, endId: 2099, sexType: 0 },
      { name: '自定义女性', startId: 2100, endId: 2173, sexType: 1 },
    ],
  },
}

/** 默认武将库（未显式指定 lib 参数时使用） */
const DEFAULT_LIB = 'custom'

/** 编辑选项配置文件（特性、性格、义理、成长类型、适性等级等） */
const OPTIONS_FILE = path.join(__dirname, 'data', 'options.json')

/** 内置武将名称索引文件（基础武将库不可用时作为兜底） */
const REFERENCE_FILE = path.join(__dirname, 'data', 'referencePersons.json')

/**
 * 自建武将字段默认值模板。
 * 字段顺序与游戏序列化顺序保持一致，保证导出的 JSON 与游戏读写兼容。
 * @returns {object} 默认武将对象
 */
function createDefaultPerson() {
  return {
    Id: 0,
    Name: '',
    type: PERSON_TYPES.Custom,
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
    // 武将标签（仅自建库使用，用于分类与按标签筛选；基础武将库无此字段）
    tags: [],
    // 最后修改时间（ISO 8601 UTC 字符串，由服务端统一写入；基础武将库无此字段）
    updatedAt: '',
  }
}

/** 数值型字段集合（能力 / 适性为数组，见 ABILITY_FIELDS / WEAPON_FIELDS，不在此列） */
const INT_FIELDS = [
  'Id', 'type', 'headIconID', 'sex', 'appearance', 'yearBorn', 'yearDead', 'compatibility',
  'state', 'personality', 'argumentation', 'voice', 'tone', 'kanshitsu', 'ideal', 'talent',
  'merit', 'stamina', 'Level', 'consanguinity', 'Father', 'Mother', 'Brother', 'Exp',
  // 基础武将库专有字段
  'BelongForce', 'BelongCorps', 'BelongCity', 'Official', 'loyalty',
]

/** 字符串型字段集合 */
const STRING_FIELDS = ['familyName', 'giveName', 'nickName', 'description', 'image', 'updatedAt']

/** 可空字符串字段（游戏原文件中可能为 null） */
const NULLABLE_STRING_FIELDS = ['imageID', 'image_old']

/** 布尔型字段集合 */
const BOOL_FIELDS = ['IsAlive', 'ActionOver']

/** 数组型字段集合（武将 ID 数组，允许为空） */
const ARRAY_FIELDS = ['SpouseList', 'BrotherList', 'LikePersonList', 'HatePersonList', 'FeatureList']

/** 标签字段集合（字符串数组，仅自建库使用） */
const TAG_FIELDS = ['tags']

/** 指向其它武将 ID 的标量字段（偏移导出时同步平移） */
const PERSON_ID_FIELDS = ['Father', 'Mother', 'Brother']

/**
 * 指向其它武将 ID 的数组字段（偏移导出时同步平移）。
 * 注意：FeatureList 是特性 ID、headIconID / imageID 是头像 ID，都不属于武将 ID，不能平移。
 */
const PERSON_ID_LIST_FIELDS = ['SpouseList', 'BrotherList', 'LikePersonList', 'HatePersonList']

/** 单个标签的最大长度 */
const TAG_MAX_LENGTH = 20

/** 单个武将的标签数量上限 */
const TAG_MAX_COUNT = 20

/**
 * 将任意输入安全转换为整数。
 * @param {*} value 原始值
 * @param {number} fallback 解析失败时的默认值
 * @returns {number} 整数结果
 */
function toInt(value, fallback = 0) {
  const n = Number(value)
  return Number.isFinite(n) ? Math.trunc(n) : fallback
}

/**
 * 取能力 / 适性字段的「基础值」。
 * 新版数据是数组，历史数据可能是单个数字。
 * @param {*} value 字段值
 * @returns {number} 基础值
 */
function statBaseValue(value) {
  if (Array.isArray(value)) return toInt(value[0], 0)
  return toInt(value, 0)
}

/**
 * 取能力字段的「成长类型」Id；数据不是数组时返回 null。
 * @param {*} value 字段值
 * @returns {number|null} 成长类型 Id
 */
function statChangeIdOf(value) {
  if (!Array.isArray(value) || value.length < 2) return null
  return toInt(value[1], DEFAULT_ATTRIBUTE_CHANGE_ID)
}

/**
 * 构造五维能力数组 [基础值, 成长类型Id, 经验, 万分比, 最终值]。
 * @param {*} base 基础值
 * @param {*} changeId 成长类型 Id（缺省 5 普通型）
 * @returns {number[]} 能力数组
 */
function makeAbilityValue(base, changeId = DEFAULT_ATTRIBUTE_CHANGE_ID) {
  const value = toInt(base, 0)
  return [value, toInt(changeId, DEFAULT_ATTRIBUTE_CHANGE_ID), 0, DEFAULT_ATTRIBUTE_FACTER, value]
}

/**
 * 构造兵种适性数组 [基础值(等级), 经验, 等级]。
 * @param {*} level 适性等级
 * @returns {number[]} 适性数组
 */
function makeWeaponValue(level) {
  const value = toInt(level, 0)
  return [value, 0, value]
}

/**
 * 把任意形态的能力值归一化为完整的 5 元组 [基础值, 成长类型Id, 经验, 万分比, 最终值]。
 * 保留成长类型 / 经验 / 万分比（缺失时用默认值），并把「最终值」同步为基础值，
 * 与游戏 PersonAttributeValue 的读写口径保持一致。
 * @param {*} value 原始值（数组或历史数字）
 * @returns {number[]} 归一化后的能力数组
 */
function normalizeAbilityValue(value) {
  const base = statBaseValue(value)
  const changeId = statChangeIdOf(value)
  const exp = Array.isArray(value) ? toInt(value[2], 0) : 0
  const facter = Array.isArray(value) ? toInt(value[3], DEFAULT_ATTRIBUTE_FACTER) : DEFAULT_ATTRIBUTE_FACTER
  return [base, changeId === null ? DEFAULT_ATTRIBUTE_CHANGE_ID : changeId, exp, facter, base]
}

/**
 * 把任意形态的适性值归一化为完整的 3 元组 [基础值(等级), 经验, 等级]。
 * 经验原样保留（缺失时为 0），等级同步为基础值。
 * @param {*} value 原始值（数组或历史数字）
 * @returns {number[]} 归一化后的适性数组
 */
function normalizeWeaponValue(value) {
  const level = statBaseValue(value)
  const exp = Array.isArray(value) ? toInt(value[1], 0) : 0
  return [level, exp, level]
}

/**
 * 依字段名归一化能力 / 适性字段；不属于这两类的字段返回 undefined。
 * @param {string} key 字段名
 * @param {*} value 字段值
 * @returns {number[]|undefined} 归一化后的数组
 */
function normalizeStatField(key, value) {
  if (ABILITY_FIELDS.includes(key)) return normalizeAbilityValue(value)
  if (WEAPON_FIELDS.includes(key)) return normalizeWeaponValue(value)
  return undefined
}

/**
 * 兼容旧版武将数据：把已改名的字段迁移到新字段名。
 * 旧结构把登场年写在 yearAvailable 上，新结构统一为 appearance。
 * @param {object} src 原始数据（就地修改）
 */
function migrateLegacyFields(src) {
  if (!src || typeof src !== 'object') return
  if (!(LEGACY_APPEARANCE_KEY in src)) return
  // 新字段已有有效值时以新字段为准，否则用旧字段回填
  if (!(toInt(src.appearance, 0) > 0)) src.appearance = toInt(src[LEGACY_APPEARANCE_KEY], 0)
  delete src[LEGACY_APPEARANCE_KEY]
}

/**
 * 将任意输入安全转换为字符串。
 * @param {*} value 原始值
 * @returns {string} 字符串结果
 */
function toStr(value) {
  return value === null || value === undefined ? '' : String(value)
}

/**
 * 取当前时间的 ISO 8601 UTC 字符串（如 2026-09-11T15:30:12.345Z）。
 * 该格式的字典序与时间先后一致，前端可直接按字符串比较排序。
 * @returns {string} 时间字符串
 */
function nowIso() {
  return new Date().toISOString()
}

/**
 * 将任意输入安全转换为“武将标签数组”。
 * 兼容字符串数组与逗号分隔字符串两种写法（便于手工编辑数据文件），
 * 统一去除空白、开头的 # 号与重复项，并限制单个标签长度与总数量；
 * 始终返回数组（空值时返回 []），保证写入的 JSON 结构稳定、可直接用于筛选。
 * @param {*} value 原始值
 * @returns {string[]} 规范化后的标签数组
 */
function toTagArray(value) {
  if (value === null || value === undefined) return []
  let list = value
  if (typeof list === 'string') list = list.split(/[,，、;；\s]+/)
  if (!Array.isArray(list)) return []
  const result = []
  for (const item of list) {
    const tag = toStr(item).trim().replace(/^#+/, '').slice(0, TAG_MAX_LENGTH)
    if (!tag || result.includes(tag)) continue
    result.push(tag)
    if (result.length >= TAG_MAX_COUNT) break
  }
  return result
}

/**
 * 将任意输入安全转换为“武将 ID 数组”。
 * 过滤掉非法值（<=0）并去重，空数组统一返回 null（与游戏自建库格式一致）。
 * @param {*} value 原始值（数组或逗号分隔字符串）
 * @param {boolean} [sort] 是否按数字升序排序。基础武将库需保持原有顺序，传 false
 * @returns {number[]|null} 结果数组或 null
 */
function toIdArray(value, sort = true) {
  if (value === null || value === undefined) return null
  let list = value
  if (typeof list === 'string') {
    list = list.split(/[,，\s]+/).filter(Boolean)
  }
  if (!Array.isArray(list)) return null
  const result = []
  for (const item of list) {
    const n = toInt(item, 0)
    if (n > 0 && !result.includes(n)) result.push(n)
  }
  if (result.length === 0) return null
  if (sort) result.sort((a, b) => a - b)
  return result
}

/**
 * 规范化 type 字段取值。
 * 非法值（越界、非数字）一律回退为默认类型，保证写入的 JSON 始终合法。
 * @param {*} value 原始值
 * @param {number} fallback 默认类型
 * @returns {number} 合法类型值
 */
function normalizeType(value, fallback) {
  const n = toInt(value, fallback)
  if (n < PERSON_TYPE_MIN || n > PERSON_TYPE_MAX) return fallback
  return n
}

/**
 * 依据“原始值类型”做同类型转换。
 * 基础武将库中 description/imageID 为数值、其它为字符串，
 * 按原值类型还原可避免破坏本体数据结构。
 * @param {*} value 新值
 * @param {*} sample 原始值（用于推断目标类型）
 * @returns {*} 转换结果
 */
function coerceLike(value, sample) {
  // 数组：保持原有顺序，且始终以数组形态回写（避免 [] 被写成 null）
  if (Array.isArray(sample)) {
    const list = toIdArray(value, false)
    return list === null ? [] : list
  }
  if (typeof sample === 'number') return toInt(value, sample)
  if (typeof sample === 'boolean') return Boolean(value)
  if (typeof sample === 'string') {
    if (value === null || value === undefined) return ''
    return String(value)
  }
  if (sample === null || sample === undefined) {
    // 原值为空时按输入类型原样保留
    if (value === undefined) return null
    if (Array.isArray(value)) return toIdArray(value)
    if (typeof value === 'number') return toInt(value, 0)
    return value
  }
  return value
}

/**
 * 规范化自建武将对象。
 * 与基础武将库保持一致（适配新版数据结构）：
 *   - **保留全部提交字段**，包括新结构的扩展字段（body / wordTac / birthplace / injury 等），
 *     未列入字段表的字段按原值形态还原，避免丢失本体数据；
 *   - 能力 / 适性统一归一化为新版数组（五维 5 元组、适性 3 元组）；
 *   - Id 列表字段（配偶 / 兄弟 / 亲近 / 厌恶 / 特性）保持数组形态与原有顺序；
 *   - 缺失字段用模板补齐，保证新建的武将对象结构完整。
 * 自建库独有规则：
 *   - type 缺省为「自定义」；
 *   - 名称由“姓 + 名”推导；当姓 / 名未改动时保留库中已有的 Name，
 *     以便重名时追加的 #1 后缀能够持久保存；
 *   - tags（标签）始终为字符串数组。
 * @param {object} raw 原始武将数据
 * @param {object} [original] 库中已有的原始武将数据（编辑场景）
 * @returns {object} 规范化后的武将对象
 */
function normalizeCustomPerson(raw, original) {
  const src = raw && typeof raw === 'object' ? raw : {}
  // 兼容旧版提交：登场年可能还是 yearAvailable
  migrateLegacyFields(src)
  const template = createDefaultPerson()
  const result = { ...template }

  // 逐字段按用途归一化：Id / Name 在函数末尾单独处理
  for (const key of Object.keys(src)) {
    if (key === 'Id' || key === 'Name') continue
    // 能力 / 适性：统一归一化为新版数组（旧数据传单个数字时也能正确写入）
    const stat = normalizeStatField(key, src[key])
    if (stat) {
      result[key] = stat
      continue
    }
    if (TAG_FIELDS.includes(key)) {
      result[key] = toTagArray(src[key])
      continue
    }
    if (ARRAY_FIELDS.includes(key)) {
      // Id 列表：保留原有顺序与数组形态（空数组不写成 null），兼容逗号分隔字符串
      result[key] =
        typeof src[key] === 'string'
          ? toIdArray(src[key], false) || []
          : coerceLike(src[key], src[key])
      continue
    }
    if (BOOL_FIELDS.includes(key)) {
      result[key] = Boolean(src[key])
      continue
    }
    if (NULLABLE_STRING_FIELDS.includes(key)) {
      const v = src[key]
      result[key] = v === null || v === undefined || v === '' ? null : String(v)
      continue
    }
    if (STRING_FIELDS.includes(key)) {
      result[key] = toStr(src[key])
      continue
    }
    if (INT_FIELDS.includes(key)) {
      result[key] = toInt(src[key], key in template ? template[key] : 0)
      continue
    }
    // 未列入字段表的字段（新结构的扩展字段等）：按原值形态还原，避免丢失
    result[key] = coerceBaseValue(src[key])
  }

  result.Id = toInt(src.Id, 0)
  // type 字段必须落在合法枚举区间内
  result.type = normalizeType(src.type, template.type)
  // 名称：姓 / 名改动时按“姓 + 名”重算，否则沿用库中已有的 Name
  // （重名时追加的 #1 后缀依赖该字段持久保存）
  const derived = `${result.familyName}${result.giveName}`.trim()
  const orig = original && typeof original === 'object' ? original : null
  if (orig) {
    const nameChanged =
      toStr(result.familyName) !== toStr(orig.familyName) ||
      toStr(result.giveName) !== toStr(orig.giveName)
    result.Name = nameChanged ? derived || toStr(orig.Name) : toStr(orig.Name) || derived
  } else {
    result.Name = toStr(src.Name) || derived
  }
  return result
}

/** 基础库中属于「武将 Id 列表」的字段：需要去重并剔除非法值 */
const BASE_ID_LIST_FIELDS = new Set([
  'SpouseList',
  'BrotherList',
  'LikePersonList',
  'HatePersonList',
  'FeatureList',
])

/**
 * 按原值形态还原基础库字段取值。
 * 与 coerceLike 的关键区别：数组一律「原样保留」——长度、顺序、重复项以及 -1 等负值都不修改。
 * 原因：新版本体数据把能力值存成 [基础值, 成长类型Id]（command: [65,5]，第二位为
 * AttributeChangeType，0 视为 5 普通型）、兵种适性（crossbowLv: [0]）、
 * body（8 个部位数值，未设置用 -1 表示）、wajutsu / wordTac 等都以数组保存，
 * 任何去重或过滤（例如丢掉 -1、把 [70,70] 合并成 [70]）都会破坏本体数据。
 * @param {*} value 原始值
 * @returns {*} 还原后的值
 */
function coerceBaseValue(value) {
  if (Array.isArray(value)) return value.map((item) => toInt(item, 0))
  if (typeof value === 'number') return toInt(value, 0)
  if (typeof value === 'boolean') return Boolean(value)
  if (typeof value === 'string') return value
  if (value === null || value === undefined) return null
  return value
}

/**
 * 规范化基础武将对象。
 * 与自建库不同，基础库保留了 BelongForce / BelongCorps / BelongCity /
 * Official / loyalty 等本体专有字段，因此这里“保留全部既有字段”，
 * 仅在原字段类型基础上做同类型转换，避免破坏游戏本体数据。
 * 兼容新旧两版本体数据结构：
 *   - 旧版：能力 / 适性为单个数字，含 description / image / yearAvailable 等字段；
 *   - 新版：能力 / 适性为数组（[基础值, 成长类型Id]，如 command: [65,5]、crossbowLv: [2]；
 *     另有 body / appearance / horse / skeleton / wajutsu 等扩展字段，
 *     且不再包含 description / image / image_old / yearAvailable。
 * 同时：基础库存在合法的同名武将（如两位「张南」），且 Name 与
 * “姓 + 名”未必一致（异体字），因此只有姓/名被改动时才重算 Name。
 * @param {object} raw 请求提交的武将数据
 * @param {object} [original] 库中已有的原始武将数据（编辑场景）
 * @returns {object} 规范化后的武将对象
 */
function normalizeBasePerson(raw, original) {
  const src = raw && typeof raw === 'object' ? raw : {}
  // 兼容旧版数据：登场年可能还是 yearAvailable
  migrateLegacyFields(src)
  const orig = original && typeof original === 'object' ? original : null
  const result = {}

  // 保留原有字段（含本体专有字段）：
  // 能力 / 适性归一化为新版数组；Id 列表字段做去重 / 过滤；
  // 其余字段（含 body 等数组）原样还原
  for (const key of Object.keys(src)) {
    const stat = normalizeStatField(key, src[key])
    if (stat) {
      result[key] = stat
      continue
    }
    result[key] = BASE_ID_LIST_FIELDS.has(key)
      ? coerceLike(src[key], src[key])
      : coerceBaseValue(src[key])
  }

  // ID 固定不可修改：始终沿用库中已有 ID，忽略请求体中的 Id
  if (orig) result.Id = toInt(orig.Id, 0)
  else result.Id = toInt(src.Id, 0)

  // 字符串字段做类型规范（仅处理已存在的字段，避免改变字段顺序）
  if ('familyName' in result) result.familyName = toStr(result.familyName)
  if ('giveName' in result) result.giveName = toStr(result.giveName)
  if ('nickName' in result) result.nickName = toStr(result.nickName)
  if ('image' in result) result.image = toStr(result.image)
  if ('image_old' in result) {
    result.image_old = result.image_old === null || result.image_old === '' ? null : String(result.image_old)
  }

  // 姓名处理：基础武将库以原有 Name 为准（本体存在异体字，Name 与“姓 + 名”未必一致），
  // 仅在姓 / 名被改动时按“姓 + 名”重算
  const derived = `${toStr(result.familyName)}${toStr(result.giveName)}`.trim()
  if (orig) {
    const nameChanged =
      toStr(result.familyName) !== toStr(orig.familyName) ||
      toStr(result.giveName) !== toStr(orig.giveName)
    result.Name = nameChanged ? derived || toStr(orig.Name) : toStr(orig.Name) || derived
  } else {
    result.Name = toStr(src.Name) || derived
  }

  // 补充 type 字段（基础库缺失时按“历史”处理）
  result.type = normalizeType(src.type, LIB_CONFIG.base.defaultType)
  return result
}

/**
 * 按库类型规范化单个武将对象。
 * @param {object} raw 原始数据
 * @param {string} libKey 库标识
 * @param {object} [original] 库中已有的原始武将数据（编辑场景）
 * @returns {object} 规范化结果
 */
function normalizePerson(raw, libKey, original) {
  return libKey === 'base' ? normalizeBasePerson(raw, original) : normalizeCustomPerson(raw, original)
}

/**
 * 各库容器级元数据缓存（如新版本体数据 PersonLibrary.type = 2）。
 * 读取时收集、写回时原样输出，避免丢失数据文件中的库类型等信息。
 * 每条写入路径都会先调用 readLibrary，因此缓存始终对应最新数据。
 */
const libContainerExtras = {}

/**
 * 读取指定武将库的数据文件。
 * 文件不存在或损坏时返回空库，不抛出异常。
 * @param {string} libKey 库标识（base / custom）
 * @returns {{offset:number, persons:object[], extras:object}} 库数据（extras 为容器级元数据）
 */
function readLibrary(libKey) {
  const cfg = LIB_CONFIG[libKey] || LIB_CONFIG[DEFAULT_LIB]
  const fallbackOffset = cfg.hasOffset ? OFFSET : 0
  try {
    if (!fs.existsSync(cfg.file)) {
      libContainerExtras[cfg.key] = {}
      return { offset: fallbackOffset, persons: [], extras: {} }
    }
    const json = JSON.parse(fs.readFileSync(cfg.file, 'utf8'))
    const lib = json && json.PersonLibrary ? json.PersonLibrary : {}
    // 结构版本判定：带新版标记即按新结构读取，缺失或偏旧则按旧结构兼容读取
    // （五维 / 适性为单个数字、登场年为 yearAvailable，由各自的 normalize 逻辑转换）
    const version = toInt(lib[DATA_VERSION_KEY], 0)
    if (version < DATA_VERSION) {
      console.warn(
        `[武将库] ${cfg.label}未带新版标记[${DATA_VERSION_KEY}]（读到 ${version}，当前 ${DATA_VERSION}），已按旧结构兼容读取；建议重新导出为新格式。`,
      )
    }
    const offset = cfg.hasOffset && Number.isFinite(Number(lib.offset)) ? Number(lib.offset) : fallbackOffset
    const persons = []
    const extras = {}
    for (const key of Object.keys(lib)) {
      if (key === 'offset') continue
      const value = lib[key]
      if (!value || typeof value !== 'object' || Array.isArray(value)) {
        // 容器级元数据（如新版本体数据的 "type": 2）：不是武将，原样保留
        extras[key] = value
        continue
      }
      persons.push(normalizePerson(value, cfg.key))
    }
    libContainerExtras[cfg.key] = extras
    persons.sort((a, b) => a.Id - b.Id)
    return { offset, persons, extras }
  } catch (err) {
    console.error(`[武将库] 读取${cfg.label}失败：`, err)
    libContainerExtras[cfg.key] = {}
    return { offset: fallbackOffset, persons: [], extras: {} }
  }
}

/**
 * 将武将库序列化为与游戏一致的 JSON 文本。
 * @param {string} libKey 库标识
 * @param {object[]} persons 武将列表
 * @returns {string} JSON 文本
 */
function serializeLibrary(libKey, persons) {
  const cfg = LIB_CONFIG[libKey]
  const lib = cfg.hasOffset ? { offset: OFFSET } : {}
  // 容器级元数据（如 "type": 2）原样写回，保证与本体数据文件结构一致
  for (const [key, value] of Object.entries(libContainerExtras[libKey] || {})) {
    lib[key] = value
  }
  // 新版结构标记：始终写当前版本（旧文件缺标记时自动补齐）
  lib[DATA_VERSION_KEY] = DATA_VERSION
  const sorted = [...persons].sort((a, b) => a.Id - b.Id)
  for (const person of sorted) {
    lib[String(person.Id)] = person
  }
  return `${JSON.stringify({ PersonLibrary: lib }, null, 2)}\n`
}

/**
 * 读取数据文件的最后修改时间（毫秒）。
 * 读取失败（文件不存在等）时返回 0，用于判断缓存是否仍然有效。
 * @param {string} file 文件绝对路径
 * @returns {number} 修改时间毫秒数，读取失败为 0
 */
function fileMtime(file) {
  try {
    return fs.statSync(file).mtimeMs
  } catch {
    return 0
  }
}

/**
 * 清空由基础武将库派生出来的姓名索引缓存。
 * 基础武将的姓名一旦被改动（编辑保存、删除或直接替换数据文件），
 * 依赖该索引的跨库查重与姓名展示必须重新构建，
 * 否则自建库仍会按旧姓名判断重名。
 */
function invalidateBaseIndexes() {
  baseNameCache = null
  baseNameCacheMtime = -1
  referenceNames = null
  referenceNamesMtime = -1
}

/**
 * 将武将库写回数据文件。
 * 先写临时文件再重命名，避免写入中断导致数据损坏。
 * 基础武将库写入后立即失效姓名索引缓存，保证后续查重使用最新的姓名。
 * @param {string} libKey 库标识
 * @param {object[]} persons 武将列表
 */
function writeLibrary(libKey, persons) {
  const cfg = LIB_CONFIG[libKey]
  const dir = path.dirname(cfg.file)
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true })
  const tmp = `${cfg.file}.tmp`
  fs.writeFileSync(tmp, serializeLibrary(libKey, persons), 'utf8')
  fs.renameSync(tmp, cfg.file)
  // 基础库改名会直接影响自建库的重名判断，写入后必须让索引缓存失效
  if (libKey === 'base') invalidateBaseIndexes()
}

/**
 * 统计某个库中所有武将出现过的字段全集（保持首次出现顺序）。
 * 前端据此决定哪些字段需要渲染，从而对基础库与自建库给出不同的编辑展示。
 * @param {object[]} persons 武将列表
 * @returns {string[]} 字段名数组
 */
function collectFieldKeys(persons) {
  const keys = []
  const seen = new Set()
  for (const person of persons) {
    for (const key of Object.keys(person)) {
      if (seen.has(key)) continue
      seen.add(key)
      keys.push(key)
    }
  }
  return keys
}

/**
 * 计算下一个可用的 ID（仅自建库使用）。
 * 取不小于起始编号的最小空闲 ID：删除武将后其编号会被重新启用，
 * 优先填补 ID 空洞，保证编号紧凑且不与现有武将冲突。
 * 注意：若旧武将的编号仍被其它武将的关系字段（父亲 / 配偶 / 喜欢武将等）引用，
 * 复用该编号后这些引用会指向新武将，必要时请先清理相关关系数据。
 * @param {object[]} persons 现有武将列表
 * @returns {number} 可用的最小空闲 ID
 */
function nextId(persons) {
  const used = new Set(persons.map((person) => toInt(person && person.Id, 0)))
  let id = BASE_ID
  while (used.has(id)) id++
  return id
}

/**
 * 收集武将列表中已占用的姓名集合。
 * @param {object[]} persons 武将列表
 * @param {number} [excludeId] 需要排除的武将 ID（编辑场景排除自身）
 * @returns {Set<string>} 姓名集合
 */
function collectNames(persons, excludeId = 0) {
  const names = new Set()
  for (const person of persons) {
    if (!person || person.Id === excludeId) continue
    const name = toStr(person.Name).trim()
    if (name) names.add(name)
  }
  return names
}

/**
 * 生成唯一姓名。
 * 若期望姓名已被占用，则依次追加 #1、#2 …… 直到不再重复。
 * @param {string} name 期望姓名
 * @param {Set<string>} usedNames 已占用的姓名集合
 * @returns {string} 唯一姓名
 */
function makeUniqueName(name, usedNames) {
  const base = toStr(name).trim()
  if (!base) return base
  if (!usedNames.has(base)) return base
  let index = 1
  while (usedNames.has(`${base}#${index}`)) index++
  return `${base}#${index}`
}

/**
 * 去掉姓名末尾的重名后缀（「冯礼#1」->「冯礼」）。
 * @param {string} name 姓名
 * @returns {string} 同源名
 */
function stripNameSuffix(name) {
  const trimmed = toStr(name).trim()
  const matched = /^(.+)#\d+$/.exec(trimmed)
  return matched ? matched[1].trim() : trimmed
}

/**
 * 取导入记录的姓名（优先 Name，其次「姓 + 名」）。
 * 与 normalizeCustomPerson 的取名规则保持一致，用于覆盖目标的姓名校验。
 * @param {object} raw 导入的原始武将数据
 * @returns {string} 姓名（可能为空字符串）
 */
function importNameOf(raw) {
  const src = raw && typeof raw === 'object' ? raw : {}
  const name = toStr(src.Name).trim()
  if (name) return name
  return `${toStr(src.familyName)}${toStr(src.giveName)}`.trim()
}

/** 单次导入的武将数量上限（防止超大文件拖垮服务） */
const IMPORT_LIMIT = 2000

/** 单次批量删除的武将数量上限（防止误操作一次清空整库） */
const BATCH_DELETE_LIMIT = 2000

/** 单次整体导入的武将数量上限（基础库整体替换时可能包含数千位武将） */
const BULK_IMPORT_LIMIT = 20000

/**
 * 解析导入请求中的「覆盖映射」。
 * 结构为 { "下标": 覆盖目标Id }，下标指向 data 数组中的第几条记录
 * （即提交顺序 / 前端预览中已勾选记录的顺序），目标 Id 为自建库中既有武将的 Id。
 * 这里只做基本格式过滤；下标越界、重复目标、目标不存在或姓名不匹配
 * 都由接口层校验并明确报错，避免「预览显示覆盖、结果却新建」这类静默偏差。
 * @param {*} raw 请求体中的 overwrites 字段
 * @returns {Map<number, number>} 源下标 -> 覆盖目标 Id
 */
function parseOverwriteMap(raw) {
  const result = new Map()
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return result
  for (const key of Object.keys(raw)) {
    const index = toInt(key, -1)
    const targetId = toInt(raw[key], -1)
    if (index < 0 || targetId <= 0) continue
    result.set(index, targetId)
  }
  return result
}

/**
 * 创建顺序 ID 分配器：从库中下一个可用 ID 起依次分配，并跳过所有已占用的 ID。
 * 库中存在 ID 空洞（删除过武将）时，连续自增会与既有武将 ID 冲突，
 * 因此这里按「占用集合」逐个取用，保证导入的 ID 始终唯一。
 * @param {object[]} persons 现有武将列表
 * @returns {() => number} 分配函数
 */
function createIdAllocator(persons) {
  const used = new Set(persons.map((p) => p.Id))
  let id = nextId(persons)
  return () => {
    while (used.has(id)) id++
    used.add(id)
    return id
  }
}

/**
 * 从导入数据中提取武将原始数据列表。
 * 兼容以下结构（均为游戏 / 本服务导出过的形态）：
 *   1) { PersonLibrary: { "1000": {...}, ..., offset: 1000 } } —— 标准文件结构；
 *   2) { PersonLibrary: [ {...}, {...} ] }                     —— 数组容器；
 *   3) [ {...}, {...} ]                                        —— 纯数组。
 * 结果按 Id 升序排列（Id 缺失或非数字的记录排在末尾，与前端预览一致）；
 * 排序是稳定的，相同 Id 的记录保持文件中的原有顺序。
 * @param {*} input 已解析的 JSON 数据
 * @returns {{persons?: object[], error?: string}} 结果
 */
function importSortKey(person) {
  const value = Number(person && person.Id)
  return Number.isFinite(value) ? value : Number.MAX_SAFE_INTEGER
}

/**
 * 从导入数据中提取武将原始数据列表（含排序）。
 */
function extractImportPersons(input) {
  if (input === null || input === undefined) return { error: '文件内容为空' }
  let container = input
  if (!Array.isArray(container) && typeof container === 'object' && container.PersonLibrary) {
    container = container.PersonLibrary
  }
  if (Array.isArray(container)) {
    const persons = container.filter((item) => item && typeof item === 'object' && !Array.isArray(item))
    return { persons }
  }
  if (typeof container !== 'object' || container === null) {
    return { error: '文件结构无法识别，请选择 CustomPerson.json 格式的文件' }
  }
  const persons = []
  for (const key of Object.keys(container)) {
    // offset 是容器元数据，不是武将
    if (key === 'offset') continue
    const value = container[key]
    if (!value || typeof value !== 'object' || Array.isArray(value)) continue
    persons.push(value)
  }
  persons.sort((a, b) => importSortKey(a) - importSortKey(b))
  return { persons }
}

/**
 * 从导入数据中提取容器级元数据（非武将对象的键，如新版本体数据的 "type": 2）。
 * 数组容器与纯数组结构没有容器元数据，返回空对象。
 * @param {*} input 已解析的 JSON 数据
 * @returns {Record<string, *>} 容器级元数据
 */
function extractContainerExtras(input) {
  const extras = {}
  if (input === null || input === undefined || Array.isArray(input) || typeof input !== 'object') {
    return extras
  }
  const container =
    input.PersonLibrary !== undefined && input.PersonLibrary !== null ? input.PersonLibrary : input
  if (!container || typeof container !== 'object' || Array.isArray(container)) return extras
  for (const key of Object.keys(container)) {
    if (key === 'offset') continue
    const value = container[key]
    if (value && typeof value === 'object' && !Array.isArray(value)) continue
    extras[key] = value
  }
  return extras
}

/**
 * 解析导入请求体，得到待导入的武将原始数据列表与容器级元数据。
 * 兼容两种提交方式：直接提交已解析对象（data），或提交文件原始文本（content）。
 * @param {*} body 请求体
 * @returns {{persons?: object[], extras?: Record<string, *>, error?: string}} 结果
 */
function parseImportPayload(body) {
  const src = body && typeof body === 'object' ? body : {}
  let parsed = src.data
  if (parsed === undefined || parsed === null) {
    // 去除 BOM，避免首字符导致 JSON.parse 失败
    const content = toStr(src.content).replace(/^\uFEFF/, '').trim()
    if (!content) return { error: '请选择要导入的 JSON 文件' }
    try {
      parsed = JSON.parse(content)
    } catch (err) {
      return { error: `JSON 解析失败：${err.message}` }
    }
  }
  return { ...extractImportPersons(parsed), extras: extractContainerExtras(parsed) }
}

/** 基础武将库姓名集合缓存（自建库跨库查重用） */
let baseNameCache = null

/** 基础武将库姓名集合缓存对应的数据文件修改时间（-1 表示缓存已失效） */
let baseNameCacheMtime = -1

/**
 * 加载基础武将库的全部姓名，供自建库跨库查重。
 * 结果按数据文件的修改时间缓存：基础武将改名（接口保存或直接替换文件）后
 * 修改时间变化，缓存会自动重建，避免用旧姓名判重。
 * @returns {Set<string>} 基础武将库姓名集合
 */
function loadBaseNames() {
  const mtime = fileMtime(LIB_CONFIG.base.file)
  if (baseNameCache && baseNameCacheMtime === mtime) return baseNameCache
  const names = new Set()
  try {
    const { persons } = readLibrary('base')
    for (const person of persons) {
      const name = toStr(person.Name).trim()
      if (name) names.add(name)
    }
  } catch (err) {
    console.warn('[武将库] 加载基础库姓名索引失败：', err.message)
  }
  baseNameCache = names
  baseNameCacheMtime = mtime
  return baseNameCache
}

/**
 * 计算某个库对当前访问者开放的操作能力。
 * 需要同时满足「库本身允许该操作」与「访问者具备写入权限」，
 * 因此游客拿到的 create / edit / delete 恒为 false（仅可浏览）。
 * @param {object} cfg 库配置
 * @param {boolean} canWrite 访问者是否具备写入权限
 * @returns {object} 能力开关
 */
function buildCapabilities(cfg, canWrite) {
  return {
    create: canWrite && cfg.canCreate,
    edit: canWrite && cfg.canEdit,
    delete: canWrite && cfg.canDelete,
    /** 是否允许「整体导入」（基础库按 Id 合并 / 替换整库） */
    bulkImport: canWrite && Boolean(cfg.canBulkImport),
    /** ID 是否只读（基础库 ID 与游戏本体绑定，不可修改） */
    readOnlyId: cfg.fixedId,
    /** 是否强制姓名唯一 */
    enforceUniqueName: cfg.enforceUniqueName,
  }
}

/**
 * 组装返回给前端的库信息（含能力开关、字段全集与头像区间）。
 * @param {string} libKey 库标识
 * @param {{offset:number, persons:object[]}} lib 库数据
 * @param {boolean} [canWrite] 访问者是否具备写入权限
 * @returns {object} 接口响应体
 */
function buildLibraryResponse(libKey, lib, canWrite = false) {
  const cfg = LIB_CONFIG[libKey]
  const { offset, persons } = lib
  return {
    lib: cfg.key,
    label: cfg.label,
    offset,
    baseId: cfg.fixedId ? 0 : BASE_ID,
    nextId: cfg.fixedId ? 0 : nextId(persons),
    count: persons.length,
    persons,
    headRanges: cfg.headRanges,
    fieldKeys: collectFieldKeys(persons),
    /** 该库允许的操作（已结合访问者身份） */
    capabilities: buildCapabilities(cfg, canWrite),
  }
}

/**
 * 统计某个库中既有的同名武将数量（仅库内部同名，不再自动改名）。
 * 为何不自动改名：姓名是用户可见数据，游戏本体武将库本身也存在合法的同名武将
 * （如两位「张南」），启动时静默改名会把用户特意保留的姓名（例如覆盖导入后的原名、
 * 用母本文件洗回的名字）再次改掉。同名武将的合并 / 覆盖由用户在导入预览中自行决定。
 * 仅对强制姓名唯一的库生效。
 * @param {string} libKey 库标识
 * @returns {number} 同名武将数量（每组同名中「第 2 个及之后」的个数）
 */
function countDuplicateNames(libKey) {
  const cfg = LIB_CONFIG[libKey]
  if (!cfg.enforceUniqueName) return 0
  const { persons } = readLibrary(libKey)
  const seen = new Set()
  let duplicated = 0
  for (const person of [...persons].sort((a, b) => a.Id - b.Id)) {
    const name = toStr(person.Name).trim()
    if (!name) continue
    if (seen.has(name)) duplicated++
    else seen.add(name)
  }
  return duplicated
}

// ─────────────────────────────────────────────────────────────
// 服务初始化
// ─────────────────────────────────────────────────────────────

const app = express()
app.use(express.json({ limit: '16mb' }))

// 解析登录身份并挂载到 req.user（未登录为 null，视同游客）
app.use(attachUser)

/**
 * 解析请求中的库标识，非法值回退为默认库。
 * @param {import('express').Request} req 请求对象
 * @returns {string} 库标识
 */
function resolveLib(req) {
  const raw = toStr(req.query.lib || (req.body && req.body.lib)) || DEFAULT_LIB
  return LIB_CONFIG[raw] ? raw : DEFAULT_LIB
}

/**
 * 判断当前请求是否具备写入权限（新增 / 修改 / 删除）。
 * @param {import('express').Request} req 请求对象
 * @returns {boolean} 是否可写
 */
function canWrite(req) {
  return hasPermission(req.user, PERMISSIONS.WRITE)
}

/** 内置武将名称索引缓存 */
let referenceNames = null

/** 内置武将名称索引缓存对应的数据文件修改时间（-1 表示缓存已失效） */
let referenceNamesMtime = -1

/**
 * 加载内置武将名称索引（仅用于前端显示关系武将姓名与跨库查重）。
 * 优先从基础武将库文件构建；基础库为空时回退到独立索引文件。
 * 结果按数据文件修改时间缓存，基础武将改名后自动重建，
 * 保证前端拿到的姓名与库中数据一致。
 * @returns {Record<string,string>} Id -> 姓名
 */
function loadReferenceNames() {
  const mtime = fileMtime(LIB_CONFIG.base.file)
  if (referenceNames && referenceNamesMtime === mtime) return referenceNames
  const index = {}
  try {
    const { persons } = readLibrary('base')
    for (const p of persons) {
      if (p.Id > 0 && p.Name) index[String(p.Id)] = p.Name
    }
  } catch (err) {
    console.warn('[武将库] 构建内置武将名称索引失败：', err.message)
  }
  // 基础库不可用（无数据）时回退到独立索引文件
  if (Object.keys(index).length === 0 && fs.existsSync(REFERENCE_FILE)) {
    try {
      Object.assign(index, JSON.parse(fs.readFileSync(REFERENCE_FILE, 'utf8')))
    } catch (err) {
      console.warn('[武将库] 读取内置武将名称索引失败：', err.message)
    }
  }
  referenceNames = index
  referenceNamesMtime = mtime
  return referenceNames
}

/** 可选武将类型列表（供前端下拉展示，避免前后端枚举不一致） */
const PERSON_TYPE_OPTIONS = [
  { id: PERSON_TYPES.Historical, name: '历史' },
  { id: PERSON_TYPES.Ancient, name: '古代' },
  { id: PERSON_TYPES.Event, name: '事件' },
  { id: PERSON_TYPES.Custom, name: '自定义' },
  { id: PERSON_TYPES.Temporary, name: '临时' },
  { id: PERSON_TYPES.Unknown, name: '未知' },
  { id: PERSON_TYPES.NPC, name: 'NPC' },
  { id: PERSON_TYPES.Auto, name: '生成' },
]

// ─────────────────────────────────────────────────────────────
// 账号与权限
// ─────────────────────────────────────────────────────────────

/**
 * 登录。
 *
 * 统一账号后，校验优先交给创意工坊；本站只负责把身份对应到本地的「档案」
 * （角色与权限仍以本站为准），最后签发本站令牌 —— 前端拿到的东西与以前完全一样，
 * 因此登录界面无需任何改动。
 *
 * 远程明确否认时仍会回落本站老账号：统一过程中不能让已有用户登不进来。
 */
app.post('/api/auth/login', async (req, res) => {
  const body = req.body && typeof req.body === 'object' ? req.body : {}
  const username = toStr(body.username).trim()
  const password = toStr(body.password)
  if (!username || !password) {
    return res.status(400).json({ message: '请输入用户名与密码' })
  }

  // ① 创意工坊账号（主路径）
  const remote = await remoteLogin(username, password)
  if (remote.identity) {
    const account = ensureAccountForIdentity(remote.identity)
    return res.json({
      token: issueToken(account.username),
      user: toPublicAccount(account),
    })
  }

  // ② 本站老账号（过渡期兼容）
  const legacy = verifyLogin(username, password)
  if (legacy) {
    return res.json({
      token: issueToken(legacy.username),
      user: toPublicAccount(legacy),
    })
  }

  // ③ 都不通过。注意区分「密码错」与「账号服务不可用」——
  //    后者提示成密码错误，会让人白白怀疑自己记错了密码。
  const status = remote.status === 503 ? 503 : 401
  return res.status(status).json({ message: remote.error || '用户名或密码错误' })
})

/**
 * 退出登录。
 * 令牌为无状态签名令牌，服务端仅做记录，实际失效由前端清除本地令牌完成。
 */
app.post('/api/auth/logout', (req, res) => {
  res.json({ ok: true })
})

/**
 * 获取当前登录身份与可选角色列表。
 * 未登录时 user 为 null（即游客）。
 * register 字段用于前端决定是否展示「玩家注册」入口及其默认角色。
 */
app.get('/api/auth/me', (req, res) => {
  res.json({
    user: req.user,
    /** 当前身份具备的权限，便于前端统一判断按钮是否可用 */
    permissions: req.user ? [...(ROLES[req.user.role]?.permissions || [])] : [],
    roles: roleOptionsFor(req.user),
    register: REGISTER_INFO,
    /** 账号来源（统一到创意工坊后，注册与登录都在那边） */
    remote: REMOTE_AUTH_INFO,
  })
})

/**
 * 玩家自助注册（无需登录）。
 * 默认开放，可用 REGISTER_ENABLED=0 关闭；默认角色由 REGISTER_ROLE 指定。
 * 注册成功即签发令牌，前端可直接登录使用。
 */
app.post('/api/auth/register', async (req, res) => {
  if (!REGISTER_INFO.enabled) {
    return res.status(403).json({ message: '当前未开放注册，请联系管理员开通账号' })
  }
  if (!allowRegister(req.ip || req.socket?.remoteAddress || '')) {
    return res.status(429).json({ message: '注册过于频繁，请稍后再试' })
  }

  const body = req.body && typeof req.body === 'object' ? req.body : {}
  const username = toStr(body.username).trim()
  const password = toStr(body.password)

  // 账号统一到创意工坊：注册也在那边完成，本站只建档（默认只读角色）
  const remote = await remoteRegister(username, password)
  if (!remote.identity) {
    return res.status(remote.status || 400).json({ message: remote.error || '注册失败' })
  }

  const account = ensureAccountForIdentity(remote.identity)
  res.status(201).json({
    token: issueToken(account.username),
    user: toPublicAccount(account),
  })
})

/** 账号列表（仅管理员；非超级管理员看不到「超级管理员」选项） */
app.get('/api/accounts', requirePermission(PERMISSIONS.ACCOUNT), (req, res) => {
  res.json({ accounts: listAccounts(), roles: roleOptionsFor(req.user) })
})

/** 新建账号（仅管理员；超级管理员角色仅超管可授予） */
app.post('/api/accounts', requirePermission(PERMISSIONS.ACCOUNT), (req, res) => {
  const checked = validateNewAccount(req.body, req.user)
  if (checked.error) return res.status(400).json({ message: checked.error })
  const account = createAccount(checked)
  res.status(201).json({ account })
})

/** 修改账号（仅管理员，可改显示名 / 角色 / 密码） */
app.put('/api/accounts/:username', requirePermission(PERMISSIONS.ACCOUNT), (req, res) => {
  const result = updateAccount(req.params.username, req.body, req.user)
  if (result.error) return res.status(400).json({ message: result.error })
  res.json({ account: result.account })
})

/** 删除账号（仅管理员；超管账号仅超管可删） */
app.delete('/api/accounts/:username', requirePermission(PERMISSIONS.ACCOUNT), (req, res) => {
  const result = deleteAccount(req.params.username, req.user)
  if (result.error) return res.status(400).json({ message: result.error })
  res.json({ account: result.account })
})

// ─────────────────────────────────────────────────────────────
// 数据备份与还原（超级管理员专属，需要 backup 权限）
// ─────────────────────────────────────────────────────────────

/** 备份列表与调度状态（仅超级管理员） */
app.get('/api/backups', requirePermission(PERMISSIONS.BACKUP), (req, res) => {
  res.json({ backups: listBackups(), status: backupStatus() })
})

/** 立即执行一次手动备份（仅超级管理员） */
app.post('/api/backups', requirePermission(PERMISSIONS.BACKUP), (req, res) => {
  const meta = runBackup('manual')
  res.status(201).json({ backup: meta, status: backupStatus() })
})

/**
 * 还原指定备份（仅超级管理员，需超级管理员权限）。
 * 请求体必须带 confirm: true，避免误触导致数据被覆盖；
 * 还原前会自动创建一份「还原前快照」，可再次用该快照还原回来。
 */
app.post('/api/backups/:id/restore', requirePermission(PERMISSIONS.BACKUP), (req, res) => {
  if (!(req.body && req.body.confirm === true)) {
    return res.status(400).json({ message: '还原操作需要二次确认' })
  }
  const result = restoreBackup(req.params.id)
  if (result.error) return res.status(400).json({ message: result.error })
  res.json({
    ...result,
    message: '还原完成，请刷新页面；若账号信息发生变化，需要重新登录',
  })
})

// ─────────────────────────────────────────────────────────────
// 武将库接口
// ─────────────────────────────────────────────────────────────

/** 获取武将库摘要信息 */
app.get('/api/meta', (req, res) => {
  const libKey = resolveLib(req)
  const lib = readLibrary(libKey)
  const cfg = LIB_CONFIG[libKey]
  res.json({
    lib: cfg.key,
    label: cfg.label,
    offset: lib.offset,
    baseId: cfg.fixedId ? 0 : BASE_ID,
    nextId: cfg.fixedId ? 0 : nextId(lib.persons),
    count: lib.persons.length,
    headRanges: cfg.headRanges,
    capabilities: buildCapabilities(cfg, canWrite(req)),
  })
})

/** 获取指定库的全部武将（游客可浏览） */
app.get('/api/persons', (req, res) => {
  const libKey = resolveLib(req)
  const lib = readLibrary(libKey)
  res.json({
    ...buildLibraryResponse(libKey, lib, canWrite(req)),
    /** 容器级元数据（如新版本体数据的 type: 2），供前端展示库类型 */
    containerExtras: lib.extras,
  })
})

/** 获取内置武将名称索引 */
app.get('/api/reference', (req, res) => {
  res.json(loadReferenceNames())
})

/** 获取编辑选项配置（特性、性格、义理、成长类型、适性等级、头像区间等） */
app.get('/api/options', (req, res) => {
  try {
    const text = fs.readFileSync(OPTIONS_FILE, 'utf8')
    const data = JSON.parse(text)
    // 武将类型由后端统一提供，保证与 PersonTypeEnum 一致
    data.personTypes = PERSON_TYPE_OPTIONS
    res.json(data)
  } catch (err) {
    console.error('[武将库] 读取选项配置失败：', err)
    res.json({
      features: [],
      personalities: [],
      argumentations: [],
      attributeChangeTypes: [],
      abilityLevels: [],
      headRanges: LIB_CONFIG.base.headRanges,
      personTypes: PERSON_TYPE_OPTIONS,
    })
  }
})

/** 新建武将（仅自建库可用；需写入权限，游客不可用） */
app.post('/api/persons', requirePermission(PERMISSIONS.WRITE), (req, res) => {
  const libKey = resolveLib(req)
  const cfg = LIB_CONFIG[libKey]
  if (!cfg.canCreate) {
    return res.status(403).json({ message: `${cfg.label}不允许新建武将` })
  }

  const { persons } = readLibrary(libKey)
  const person = normalizeCustomPerson(req.body)

  if (!person.familyName && !person.giveName) {
    return res.status(400).json({ message: '姓与名不能同时为空' })
  }
  if (!person.Name) {
    return res.status(400).json({ message: '武将姓名不能为空' })
  }

  // 姓名去重：与基础武将库及本库其它武将比较，冲突时自动追加 #1 / #2 ……
  person.Id = nextId(persons)
  // 最后修改时间由服务端统一写入，忽略请求体中的值
  person.updatedAt = nowIso()
  const usedNames = collectNames(persons)
  for (const name of loadBaseNames()) usedNames.add(name)
  const wantedName = person.Name
  person.Name = makeUniqueName(wantedName, usedNames)

  persons.push(person)
  writeLibrary(libKey, persons)
  res.status(201).json({
    person,
    nextId: nextId(persons),
    /** 因重名被改名前使用的姓名（未改名时为空字符串） */
    renamedFrom: person.Name === wantedName ? '' : wantedName,
  })
})

/**
 * 批量导入武将（仅自建库可用；需写入权限，游客不可用）。
 * 规则：
 *   - 文件结构与 CustomPerson.json 一致（{ PersonLibrary: { ... } }）；
 *   - 忽略文件中的原 ID，从当前库的下一个可用 ID 起顺序分配（并跳过已占用的 ID）；
 *   - 姓名与基础武将库、本库既有武将以及本次已导入武将一并查重，
 *     冲突时依次追加 #1、#2 …… 后缀；
 *   - 本批写入（新建与被覆盖）的武将统一写入同一个修改时间（updatedAt）；
 *   - 请求体可携带 overwrites 覆盖映射（源下标 -> 既有武将 Id）：
 *     命中的记录不新建，而是用导入数据替换该武将的字段并保留其原 Id 与姓名
 *     （姓名不做重名后缀处理），便于用新版数据覆盖旧版自建武将，
 *     且不破坏其它数据对该武将 ID 的引用。
 * 请求体：{ data?: object[], content?: string, overwrites?: Record<string, number> }
 */
app.post('/api/persons/import', requirePermission(PERMISSIONS.WRITE), (req, res) => {
  const libKey = resolveLib(req)
  const cfg = LIB_CONFIG[libKey]
  if (!cfg.canCreate) {
    return res.status(403).json({ message: `${cfg.label}不允许导入武将` })
  }

  const parsed = parseImportPayload(req.body)
  if (parsed.error) return res.status(400).json({ message: parsed.error })
  const source = parsed.persons || []
  if (source.length === 0) {
    return res.status(400).json({ message: '文件中没有可导入的武将' })
  }
  if (source.length > IMPORT_LIMIT) {
    return res
      .status(400)
      .json({ message: `单次最多导入 ${IMPORT_LIMIT} 位武将，当前文件含 ${source.length} 位` })
  }

  const { persons } = readLibrary(libKey)
  // 覆盖映射：提交数据下标 -> 既有武将 Id（下标即 data 数组下标，与前端预览顺序一致）
  const overwriteMap = parseOverwriteMap(req.body && req.body.overwrites)
  // 查重范围：基础武将库 + 本库既有武将 + 本次已导入武将
  const baseNames = loadBaseNames()
  const usedNames = collectNames(persons)
  for (const name of baseNames) usedNames.add(name)

  // 覆盖校验（在写入任何数据之前完成，杜绝「预览是覆盖、结果却新建或覆盖错人」）：
  //   - 下标必须落在本次提交的数据范围内；
  //   - 目标武将必须仍然存在；
  //   - 同一目标只能被覆盖一次；
  //   - 导入记录与目标的姓名必须同源（完全相同，或目标为「姓名#序号」形式）。
  // 校验不通过一律报错，而不是把这行当成新建或直接忽略。
  const targetIndexes = new Map()
  for (const [srcIndex, targetId] of overwriteMap) {
    if (srcIndex >= source.length) {
      return res.status(409).json({
        message: `覆盖映射下标越界（${srcIndex} ≥ ${source.length}），请重新打开导入窗口后再试`,
      })
    }
    if (targetIndexes.has(targetId)) {
      return res
        .status(409)
        .json({ message: `覆盖目标重复（Id ${targetId}），请重新打开导入窗口后再试` })
    }
    const index = persons.findIndex((p) => p.Id === targetId)
    if (index < 0) {
      return res
        .status(409)
        .json({ message: `覆盖目标已不存在（Id ${targetId}），请重新打开导入窗口后再试` })
    }
    const wantedName = importNameOf(source[srcIndex])
    const targetName = toStr(persons[index].Name).trim()
    // 姓名为空的记录会被跳过，无需校验姓名
    if (wantedName && targetName !== wantedName && stripNameSuffix(targetName) !== wantedName) {
      return res.status(409).json({
        message: `覆盖目标姓名不匹配（Id ${targetId} 为「${targetName}」，导入记录为「${wantedName}」），请重新打开导入窗口后再试`,
      })
    }
    targetIndexes.set(targetId, index)
  }

  const allocateId = createIdAllocator(persons)
  // 同一批导入使用同一个修改时间，便于按修改时间排序时整批聚在一起
  const batchUpdatedAt = nowIso()
  const written = []
  const overwritten = []
  const renamed = []
  let inserted = 0
  let skipped = 0

  source.forEach((raw, index) => {
    const person = normalizeCustomPerson(raw)
    if (!person.Name) {
      skipped++
      return
    }

    const targetId = overwriteMap.get(index)
    if (targetId === undefined) {
      // 新建：分配新 ID，重名时追加 #1 后缀
      person.Id = allocateId()
      person.updatedAt = batchUpdatedAt
      const wantedName = person.Name
      const finalName = makeUniqueName(wantedName, usedNames)
      if (finalName !== wantedName) renamed.push({ from: wantedName, to: finalName })
      person.Name = finalName
      usedNames.add(finalName)
      persons.push(person)
      written.push(person)
      inserted++
      return
    }

    // 覆盖：沿用既有武将 Id（其它数据对该武将的引用不受影响），其余字段以导入数据为准。
    // 姓名一律保持导入文件中的写法，绝不追加 #1 后缀 —— 即使它与基础武将库重名，
    // 因为覆盖的语义是「更新这名武将的数据」，而不是新建一个同名武将。
    const targetIndex = targetIndexes.get(targetId)
    const target = persons[targetIndex]
    const targetName = toStr(target.Name).trim()
    // 目标原有的姓名让位，避免后续记录把它当作「已被占用」而追加后缀
    if (targetName) usedNames.delete(targetName)
    person.Id = targetId
    person.updatedAt = batchUpdatedAt
    usedNames.add(person.Name)
    persons[targetIndex] = person
    overwritten.push({ id: person.Id, name: person.Name })
    written.push(person)
  })

  if (written.length === 0) {
    return res.status(400).json({ message: '文件中没有可导入的武将（姓名均为空）' })
  }

  writeLibrary(libKey, persons)
  res.status(201).json({
    /** 新建的武将数量（覆盖既有武将不计入） */
    imported: inserted,
    /** 被覆盖的自建武将（保留原 Id） */
    overwritten,
    skipped,
    renamed,
    persons: written,
    count: persons.length,
    nextId: nextId(persons),
  })
})

/**
 * 批量删除武将（仅自建库可用；需写入权限，游客不可用）。
 * 规则：
 *   - 请求体 { ids: number[] }，重复 ID 自动去重；
 *   - 列表中不存在的 ID 会被忽略，返回实际删除数量与被删除的武将；
 *   - 删除后编号会空出来，下次新建武将按最小空闲 ID 分配（见 nextId），
 *     因此旧编号可能被新武将复用，关系字段中的旧引用需自行清理。
 */
app.post('/api/persons/batch-delete', requirePermission(PERMISSIONS.WRITE), (req, res) => {
  const libKey = resolveLib(req)
  const cfg = LIB_CONFIG[libKey]
  if (!cfg.canDelete) {
    return res.status(403).json({ message: `${cfg.label}不允许删除武将` })
  }

  const rawList = req.body && req.body.ids
  if (!Array.isArray(rawList)) {
    return res.status(400).json({ message: '请提供要删除的武将 ID 列表' })
  }
  const ids = new Set()
  for (const item of rawList) {
    const id = toInt(item, 0)
    if (id > 0) ids.add(id)
  }
  if (ids.size === 0) {
    return res.status(400).json({ message: '请至少选择一位要删除的武将' })
  }
  if (ids.size > BATCH_DELETE_LIMIT) {
    return res
      .status(400)
      .json({ message: `单次最多删除 ${BATCH_DELETE_LIMIT} 位武将，当前选择了 ${ids.size} 位` })
  }

  const { persons } = readLibrary(libKey)
  const removed = persons.filter((p) => ids.has(p.Id))
  if (removed.length === 0) {
    return res.status(404).json({ message: '所选武将已不存在，请刷新后重试' })
  }

  const kept = persons.filter((p) => !ids.has(p.Id))
  writeLibrary(libKey, kept)
  res.json({
    /** 实际删除的数量 */
    deleted: removed.length,
    /** 被删除的武将（便于前端提示） */
    persons: removed,
    /** 删除后库中武将总数 */
    count: kept.length,
    /** 删除后的下一个可用 ID */
    nextId: nextId(kept),
  })
})

/**
 * 清空某个库的全部武将（需写入权限）。
 * 请求体必须带 confirm: true，避免误调用导致整库被清空；
 * 清空前建议先用「下载」导出当前数据做备份。
 * 仅清空武将记录，库结构（offset 等）保持不变。
 */
app.post('/api/persons/clear', requirePermission(PERMISSIONS.WRITE), (req, res) => {
  const libKey = resolveLib(req)
  const cfg = LIB_CONFIG[libKey]
  if (!cfg.canDelete) {
    return res.status(403).json({ message: `${cfg.label}不允许删除武将` })
  }
  if (!(req.body && req.body.confirm === true)) {
    return res.status(400).json({ message: '清空整库需要二次确认' })
  }

  const { persons } = readLibrary(libKey)
  if (persons.length === 0) {
    return res.status(400).json({ message: '库中已没有武将' })
  }

  writeLibrary(libKey, [])
  res.json({
    /** 被清空的武将数量 */
    removed: persons.length,
    /** 清空后库中武将总数 */
    count: 0,
  })
})

/**
 * 整体导入武将（基础库专用：按文件中的 Id 合并或替换整库；需写入权限）。
 * 与自建库的批量导入不同：
 *   - 保留文件中的原 Id（缺失时分配当前库的最小空闲 Id），因为基础库 Id 与游戏数据绑定；
 *   - 不参与重名改名（基础库本身存在合法的同名武将）；
 *   - mode=merge：Id 已存在则更新该武将，不存在则新增；
 *   - mode=replace：先清空整库，再写入文件中的武将。
 * 请求体：{ data?: object[], content?: string, mode?: 'merge' | 'replace' }
 */
app.post('/api/persons/bulk-import', requirePermission(PERMISSIONS.WRITE), (req, res) => {
  const libKey = resolveLib(req)
  const cfg = LIB_CONFIG[libKey]
  if (!cfg.canBulkImport) {
    return res.status(403).json({ message: `${cfg.label}不支持整体导入` })
  }

  const parsed = parseImportPayload(req.body)
  if (parsed.error) return res.status(400).json({ message: parsed.error })
  const source = parsed.persons || []
  if (source.length === 0) {
    return res.status(400).json({ message: '文件中没有可导入的武将' })
  }
  if (source.length > BULK_IMPORT_LIMIT) {
    return res
      .status(400)
      .json({ message: `单次最多导入 ${BULK_IMPORT_LIMIT} 位武将，当前文件含 ${source.length} 位` })
  }

  const mode = req.body && req.body.mode === 'replace' ? 'replace' : 'merge'
  const current = readLibrary(libKey).persons
  // 容器级元数据（如新版本体数据的 "type": 2）：
  // 替换整库时以导入文件为准，合并导入时只补齐当前库缺失的键，
  // 避免「导入整体数据后库类型等元信息丢失」
  const incomingExtras = parsed.extras || {}
  const extraTargets = libContainerExtras[libKey] || {}
  for (const [key, value] of Object.entries(incomingExtras)) {
    if (mode === 'replace' || extraTargets[key] === undefined) extraTargets[key] = value
  }
  libContainerExtras[libKey] = extraTargets
  const persons = mode === 'replace' ? [] : [...current]
  const indexOfId = new Map(persons.map((person, index) => [person.Id, index]))

  // 空闲 Id 分配：先把文件中显式出现的 Id 全部预占，避免新分配的 Id 与后续记录冲突
  const usedIds = new Set(persons.map((person) => person.Id))
  for (const raw of source) {
    const wantedId = toInt(raw && raw.Id, 0)
    if (wantedId > 0) usedIds.add(wantedId)
  }
  let freeId = 1
  const allocateId = () => {
    while (usedIds.has(freeId)) freeId++
    usedIds.add(freeId)
    return freeId
  }

  let added = 0
  let updated = 0
  for (const raw of source) {
    const person = normalizePerson(raw, libKey)
    const wantedId = toInt(raw && raw.Id, 0)
    person.Id = wantedId > 0 ? wantedId : allocateId()
    usedIds.add(person.Id)

    const index = indexOfId.get(person.Id)
    if (index === undefined) {
      indexOfId.set(person.Id, persons.length)
      persons.push(person)
      added++
    } else {
      persons[index] = person
      updated++
    }
  }

  writeLibrary(libKey, persons)
  res.json({
    /** 模式：merge=按 Id 合并，replace=替换整库 */
    mode,
    /** 新增的武将数量 */
    added,
    /** 按 Id 更新的武将数量 */
    updated,
    /** 替换整库时被移除的武将数量 */
    removed: mode === 'replace' ? current.length : 0,
    /** 导入后库中武将总数 */
    count: persons.length,
  })
})

/** 修改武将（ID 固定不可变更；基础库与自建库均可修改；需写入权限） */
app.put('/api/persons/:id', requirePermission(PERMISSIONS.WRITE), (req, res) => {
  const libKey = resolveLib(req)
  const cfg = LIB_CONFIG[libKey]
  if (!cfg.canEdit) {
    return res.status(403).json({ message: `${cfg.label}不允许修改` })
  }

  const id = toInt(req.params.id, -1)
  const { persons } = readLibrary(libKey)
  const index = persons.findIndex((p) => p.Id === id)
  if (index < 0) {
    return res.status(404).json({ message: `未找到 ID 为 ${id} 的武将` })
  }

  const original = persons[index]
  const person = normalizePerson(req.body, libKey, original)
  if (!person.Name) {
    return res.status(400).json({ message: '武将姓名不能为空' })
  }
  // 基础武将库本身就存在合法的同名武将（本体数据即如此），只对强制唯一的库去重；
  // 冲突时不报错，而是自动追加 #1 / #2 …… 保证姓名唯一
  let renamedFrom = ''
  if (cfg.enforceUniqueName) {
    const usedNames = collectNames(persons, id)
    for (const name of loadBaseNames()) usedNames.add(name)
    const wantedName = person.Name
    person.Name = makeUniqueName(wantedName, usedNames)
    if (person.Name !== wantedName) renamedFrom = wantedName
  }

  // ID 固定：始终沿用原有 ID，忽略请求体中的 Id 值
  person.Id = id
  // 最后修改时间由服务端统一写入，忽略请求体中的值
  person.updatedAt = nowIso()
  persons[index] = person
  writeLibrary(libKey, persons)
  res.json({ person, renamedFrom })
})

/** 删除武将（仅自建库可用；基础库武将不可删除；需写入权限） */
app.delete('/api/persons/:id', requirePermission(PERMISSIONS.WRITE), (req, res) => {
  const libKey = resolveLib(req)
  const cfg = LIB_CONFIG[libKey]
  if (!cfg.canDelete) {
    return res.status(403).json({ message: `${cfg.label}中的武将不允许删除，仅可修改` })
  }

  const id = toInt(req.params.id, -1)
  const { persons } = readLibrary(libKey)
  const index = persons.findIndex((p) => p.Id === id)
  if (index < 0) {
    return res.status(404).json({ message: `未找到 ID 为 ${id} 的武将` })
  }
  const [removed] = persons.splice(index, 1)
  writeLibrary(libKey, persons)
  res.json({ person: removed })
})

/**
 * 把武将 ID 整体平移到新的号段（偏移导出用）。
 * 规则：新 Id = 原 Id - 原起始号 + 新起始号
 * （例如原 1000 ~ 1649、起始号 1000 → 10000，则导出为 10000 ~ 10649）；
 * 同时同步平移「指向本库武将」的关系字段（父亲 / 母亲 / 兄弟 / 配偶 / 兄弟列表 / 亲近 / 厌恶），
 * 保证导出后的人物关系仍然正确；指向基础武将库的 ID（不在本库内）保持不变。
 * @param {object[]} persons 武将列表
 * @param {number} fromBase 原起始号（容器 offset）
 * @param {number} toBase 新起始号
 * @returns {object[]} 平移后的武将列表（不修改原对象）
 */
function shiftPersonIds(persons, fromBase, toBase) {
  const delta = toBase - fromBase
  if (!Number.isFinite(delta) || delta === 0) return persons
  const mapping = new Map(persons.map((p) => [p.Id, p.Id + delta]))
  /** 平移单个 ID：只处理指向本库武将的 ID，其它（基础库 ID / 0 / -1）原样保留 */
  const shiftOne = (value) => {
    const id = Number(value)
    return mapping.has(id) ? mapping.get(id) : value
  }
  /** 平移 ID 数组 */
  const shiftList = (value) => (Array.isArray(value) ? value.map(shiftOne) : value)
  return persons.map((person) => {
    const next = { ...person, Id: shiftOne(person.Id) }
    for (const key of PERSON_ID_FIELDS) {
      if (key in next) next[key] = shiftOne(next[key])
    }
    for (const key of PERSON_ID_LIST_FIELDS) {
      if (key in next) next[key] = shiftList(next[key])
    }
    return next
  })
}

// ─────────────────────────────────────────────────────────────
// 武将模组素材包（导出到创意工坊发布）
// ─────────────────────────────────────────────────────────────

/**
 * 模组内武将的 ID 号段。
 *
 * 游戏端把每个模组的 Data/CustomPerson.json 装进独立的 ModScenarioAddon，
 * 号段固定从 10000 起（见 GameCustomEdit 的 `ModScenarioAddon.PersonLibrary.offset = 10000`）。
 * 所以模组包里的武将 ID 必须落在这里，沿用网站自建库的 2 万段会与本体、其它模组撞号。
 */
const MOD_PERSON_ID_START = 10001

/** mod.info 的值里不能出现 '='（游戏端按第一个 '=' 截断），顺手压掉换行 */
function sanitizeModInfoValue(value) {
  return String(value).replace(/[=\r\n]+/g, ' ').trim()
}

/**
 * 拼 mod.info 文本。
 * 只写作者在这里能填的字段：id 由创意工坊分配，author 取账号名，poster 由上传的封面决定，
 * 那几项都会被创意工坊入库时按站点数据重写，写在这里没有意义。
 */
function buildModInfoText({ name, version, description }) {
  const lines = [`name=${name}`, `version=${version}`]
  if (description) lines.push(`description=${description}`)
  return `${lines.join('\n')}\n`
}

/** 自制头像的性别由千位奇偶决定（与 faceStore 号段规则一致：奇数千位为男） */
function faceSexType(id) {
  return Math.floor(id / 1000) % 2 === 1 ? 0 : 1
}

/**
 * 生成模组包内的 FaceConfig.json：把用到的头像登记成"容貌选择"里的可选区间。
 *
 * 游戏端会把本体与各模组的 Data/FaceConfig.json 合并成可选头像列表，
 * 而 3000 段不在本体配置里 —— 不带这份文件的话，头像文件在包里、脸也显示得出来，
 * 但在新建武将的容貌列表里挑不到这几张。按号段分男女各出一道区间即可。
 */
function buildFaceConfig(faceIds) {
  const bySex = new Map()
  for (const id of faceIds) {
    const sexType = faceSexType(id)
    const cur = bySex.get(sexType)
    if (cur) {
      cur.startId = Math.min(cur.startId, id)
      cur.endId = Math.max(cur.endId, id)
    } else {
      bySex.set(sexType, { startId: id, endId: id })
    }
  }
  return {
    HeadIdRanges: [...bySex.entries()].map(([sexType, range]) => ({
      name: sexType === 1 ? '武将包 · 自制女性' : '武将包 · 自制男性',
      startId: range.startId,
      endId: range.endId,
      sexType,
    })),
  }
}

/**
 * 把选中的武将重编号到模组号段，并清洗关系引用。
 *
 * 与偏移导出（shiftPersonIds）的关键差别：那边是整库平移，引用不会跑出集合；
 * 这里只带走一部分人 —— 如果某位武将被引用了却没被选中，重编号后会指向一个空号、
 * 甚至撞上别的武将，所以这类引用要清成 0（游戏里的"无"）。
 * 指向基础武将库（本体自带那些）的引用不属于本库，保持原样。
 */
function remapForModPack(selected, allPersons, startId) {
  const mapping = new Map(selected.map((p, index) => [p.Id, startId + index]))
  const libraryIds = new Set(allPersons.map((p) => p.Id))
  const remapOne = (value) => {
    const id = Number(value)
    if (mapping.has(id)) return mapping.get(id)
    if (libraryIds.has(id)) return 0
    return value
  }
  const remapList = (value) =>
    Array.isArray(value) ? value.map(remapOne).filter((id) => Number(id) > 0) : value

  return selected.map((person) => {
    const next = { ...person, Id: mapping.get(person.Id) }
    for (const key of PERSON_ID_FIELDS) {
      if (key in next) next[key] = remapOne(next[key])
    }
    for (const key of PERSON_ID_LIST_FIELDS) {
      if (key in next) next[key] = remapList(next[key])
    }
    return next
  })
}

/** 下载武将库 JSON 文件（结构与游戏对应文件一致） */
app.get('/api/download', (req, res) => {
  const libKey = resolveLib(req)
  const cfg = LIB_CONFIG[libKey]
  const { offset, persons } = readLibrary(libKey)

  // 偏移导出：?start=10000 时把自建武将的 ID 整体平移到新号段（原起始号 offset → start）
  const rawStart = toStr(req.query.start).trim()
  const start = rawStart ? toInt(rawStart, -1) : 0
  if (rawStart && start <= 0) {
    return res.status(400).json({ message: '起始 ID 必须是大于 0 的整数' })
  }
  if (rawStart && !cfg.hasOffset) {
    return res.status(400).json({ message: `${cfg.label}不支持偏移导出，仅自建武将库可指定起始 ID` })
  }

  const shifted = rawStart ? shiftPersonIds(persons, offset, start) : persons
  const lib = cfg.hasOffset ? { offset: rawStart ? start : offset } : {}
  // 容器级元数据（如 "type": 2）一并保留，避免下载的文件丢失库类型
  for (const [key, value] of Object.entries(libContainerExtras[libKey] || {})) {
    lib[key] = value
  }
  // 新版结构标记：始终写当前版本，保证下载给游戏的文件能被识别为新结构
  lib[DATA_VERSION_KEY] = DATA_VERSION
  for (const person of shifted) {
    lib[String(person.Id)] = person
  }

  const json = { PersonLibrary: lib }
  const fileName = rawStart ? cfg.fileName.replace(/\.json$/i, `_${start}.json`) : cfg.fileName
  res.setHeader('Content-Type', 'application/json; charset=utf-8')
  res.setHeader('Content-Disposition', `attachment; filename="${fileName}"`)
  res.send(`${JSON.stringify(json, null, 2)}\n`)
})

/**
 * 生成「武将模组素材包」：把选中的武将连同他们用到的自制头像打成一个 zip，
 * 直接拖进创意工坊的发布页上传即可成为一个模组。
 *
 * 包内结构：
 *   mod.info                 只写 name / version / description（id 由创意工坊分配）
 *   Data/CustomPerson.json   选中的武将（ID 已重编号到模组号段）
 *   Data/FaceConfig.json     登记用到的自制头像号段
 *   Assets/Face/{id}_1.png   立绘
 *   Assets/Face/{id}_2.png   头像
 *
 * 刻意不套顶层目录：创意工坊入库时会按分配的模组 ID 自动补一层，并顺带规整目录名。
 */
app.post('/api/persons/export-mod', (req, res) => {
  const body = req.body && typeof req.body === 'object' ? req.body : {}
  const libKey = resolveLib(req)
  const cfg = LIB_CONFIG[libKey]
  const { persons } = readLibrary(libKey)

  const wanted = new Set(Array.isArray(body.ids) ? body.ids.map((v) => toInt(v, 0)) : [])
  const selected = persons.filter((p) => wanted.has(p.Id))
  if (selected.length === 0) {
    return res.status(400).json({ message: '请先选择要打进模组的武将' })
  }

  const name = sanitizeModInfoValue(toStr(body.name).trim() || `${cfg.label}模组`)
  const version = sanitizeModInfoValue(toStr(body.version).trim() || '1.0')
  const description = sanitizeModInfoValue(toStr(body.description).trim())

  // 1. 重编号到模组号段，并清洗指向"没被选中的同库武将"的引用
  const remapped = remapForModPack(selected, persons, MOD_PERSON_ID_START)

  // 2. 收集这些武将用到的自制头像（headIconID >= 3000 才是自制，本体容貌不需要打包）
  const faceIds = [
    ...new Set(selected.map((p) => toInt(p.headIconID, 0)).filter((id) => id >= CUSTOM_FACE_BASE_ID)),
  ]

  // 3. 组装数据文件。模组包内不写 offset —— 游戏端按固定号段加载它
  const lib = { [DATA_VERSION_KEY]: DATA_VERSION }
  for (const person of remapped) {
    lib[String(person.Id)] = person
  }

  const entries = [
    { name: 'mod.info', data: Buffer.from(buildModInfoText({ name, version, description }), 'utf8') },
    {
      name: 'Data/CustomPerson.json',
      data: Buffer.from(`${JSON.stringify({ PersonLibrary: lib }, null, 2)}\n`, 'utf8'),
    },
  ]

  if (faceIds.length > 0) {
    entries.push({
      name: 'Data/FaceConfig.json',
      data: Buffer.from(`${JSON.stringify(buildFaceConfig(faceIds), null, 2)}\n`, 'utf8'),
    })
    for (const file of collectFaceFiles(faceIds)) {
      entries.push({ name: `Assets/Face/${file.name}`, data: file.data })
    }
  }

  res.setHeader('Content-Type', 'application/zip')
  res.setHeader('Content-Disposition', 'attachment; filename="person_mod_pack.zip"')
  res.send(buildZip(entries))
})

// ─────────────────────────────────────────────────────────────
// 自定义头像（制作 / 管理 / 打包下载）
// ─────────────────────────────────────────────────────────────

/**
 * 自定义头像列表。
 * 返回全部自制头像及其下一个可用 ID，游客可浏览。
 */
app.get('/api/faces/custom', (req, res) => {
  res.json({ baseId: CUSTOM_FACE_BASE_ID, ...listCustomFaces() })
})

/**
 * 打包下载自定义头像（ZIP）。
 * 请求示例：/api/faces/custom/export?ids=3000,3001（不传 ids 表示导出全部）
 */
app.get('/api/faces/custom/export', (req, res) => {
  const raw = toStr(req.query.ids).trim()
  const ids = raw ? raw.split(/[,，\s]+/).filter(Boolean) : null
  const result = exportCustomFaces(ids)
  if (result.error) return res.status(404).json({ message: result.error })
  const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')
  res.setHeader('Content-Type', 'application/zip')
  res.setHeader('Content-Disposition', `attachment; filename="Face_Custom_${stamp}.zip"`)
  res.send(result.buffer)
})

/** 制作并保存自定义头像（需写入权限，游客不可用） */
app.post('/api/faces/custom', requirePermission(PERMISSIONS.WRITE), async (req, res) => {
  const result = await saveCustomFace(req.body)
  if (result.error) return res.status(400).json({ message: result.error })
  res.status(201).json(result)
})

/** 删除自定义头像（需写入权限，游客不可用） */
app.delete('/api/faces/custom/:id', requirePermission(PERMISSIONS.WRITE), async (req, res) => {
  const result = await deleteCustomFace(toInt(req.params.id, -1))
  if (result.error) return res.status(400).json({ message: result.error })
  res.json(result)
})

/**
 * 头像存储状态自检。
 *
 * 用来回答「R2 到底配上了没有」「本地有多少张、R2 上同步了多少张」这类问题，
 * 免得对着配置猜。
 */
app.get('/api/faces/storage', async (req, res) => {
  const info = r2.status()
  let localCount = 0
  try {
    localCount = fs.readdirSync(path.join(__dirname, 'face')).filter((f) => f.toLowerCase().endsWith('.png')).length
  } catch {
    /* 目录不存在时按 0 计 */
  }

  // 抽样检查远端：只在配置了写凭证时做，避免给出误导性的结论
  let probe = null
  if (info.canWrite && localCount > 0) {
    try {
      const sample = fs
        .readdirSync(path.join(__dirname, 'face'))
        .filter((f) => f.toLowerCase().endsWith('.png'))
        .slice(0, 3)
      probe = []
      for (const name of sample) {
        probe.push({ name, exists: await r2.hasFace(name) })
      }
    } catch (err) {
      probe = [{ name: '(抽样失败)', exists: false, error: err.message }]
    }
  }

  res.json({ ...info, localCount, probe })
})

// ─────────────────────────────────────────────────────────────
// 静态资源
// ─────────────────────────────────────────────────────────────

/** 头像文件所在的本地目录（权威源） */
const FACE_DIR = path.join(__dirname, 'face')

/**
 * 头像请求：配置了 R2 公开域名就 302 过去，否则本地提供。
 *
 * 之所以用重定向而不是服务端代理转发：头像是 110 MB 的读多写少资源，
 * 代理转发等于把 CDN 的作用抵消掉，每个请求仍要经过本服务。
 * 302 之后浏览器直连 R2/CDN，本服务一个字节都不用出。
 *
 * 重定向本身也带长缓存，浏览器后续连 302 都不用再问。
 * 注意：头像 ID 永不复用，所以可以放心给长缓存。
 */
app.get('/face/:name', (req, res, next) => {
  const name = String(req.params.name || '')
  // 只放行形如 1234_1.png 的文件名，避免任何路径穿越
  if (!/^\d+_[12]\.png$/i.test(name)) return next()

  const target = r2.publicUrl(name)
  if (!target) return next() // 未配置 R2，交给下面的本地静态中间件

  res.setHeader('Cache-Control', 'public, max-age=604800, immutable')
  res.redirect(302, target)
})

/**
 * 本地头像目录（R2 未配置或未命中的兜底）。
 * 图片体积较大且内容稳定（自定义头像 ID 永不复用），因此开启长缓存。
 */
app.use(
  '/face',
  express.static(FACE_DIR, {
    maxAge: '7d',
    etag: true,
    lastModified: true,
  }),
)

/** 前端构建产物目录 */
const DIST_DIR = path.join(__dirname, '..', 'dist')
if (fs.existsSync(DIST_DIR)) {
  app.use(express.static(DIST_DIR))
  // 单页应用回退
  app.get(/^\/(?!api|face).*/, (req, res) => {
    res.sendFile(path.join(DIST_DIR, 'index.html'))
  })
}

app.listen(PORT, () => {
  console.log(`[武将库] 服务已启动： http://localhost:${PORT}`)
  console.log(`[武将库] 基础武将库： ${LIB_CONFIG.base.file}`)
  console.log(`[武将库] 自建武将库： ${LIB_CONFIG.custom.file}`)
  console.log(`[武将库] 自建武将起始 ID： ${BASE_ID}`)

  // 头像存储：配了 R2 就把请求 302 到 CDN，否则由本服务直接提供本地文件
  const storage = r2.status()
  if (!storage.enabled) {
    console.log('[头像] 存储： 本地目录（未配置 R2）')
  } else {
    console.log(`[头像] 读取： 302 -> ${storage.publicBase}`)
    console.log(
      `[头像] 写入： ${storage.canWrite ? `本地 + R2 双写（bucket：${storage.bucket}）` : '仅本地（未配置 R2 写凭证）'}`
    )
    if (storage.hint) console.log(`[头像] 提示： ${storage.hint}`)
  }
  // 初始化账号数据（首次启动会创建默认管理员账号，并确保存在超级管理员）
  const init = initAccounts()
  if (init.created) {
    console.log(`[账号] 已创建默认管理员账号： ${init.username} / ${init.password}`)
    console.log('[账号] 请登录后尽快在「账号管理」中修改密码')
  } else {
    console.log(`[账号] 账号文件： ${path.join(__dirname, 'data', 'accounts.json')}`)
  }
  if (init.promoted) {
    console.log(`[账号] 已将最早的管理员「${init.promoted}」提升为超级管理员（可用「备份与还原」）`)
  }
  console.log('[账号] 游客（未登录）仅可浏览，不能新增 / 修改 / 删除武将')
  console.log(
    REGISTER_INFO.enabled
      ? `[账号] 已开放玩家自助注册：注册后的默认角色为「${REGISTER_INFO.roleLabel}」（REGISTER_ENABLED=0 可关闭）`
      : '[账号] 玩家自助注册已关闭（如需开启请设置 REGISTER_ENABLED=1）',
  )
  // 启动每日自动备份（每天一次，保留最近若干天）
  startBackupScheduler()
  // 启动时提示自建库中的同名武将（不再自动改名，避免擅自改动用户数据）
  const duplicated = countDuplicateNames('custom')
  if (duplicated > 0) {
    console.log(
      `[武将库] 提示：自建库存在 ${duplicated} 位与其它武将同名的记录（不会自动改名，可在导入预览中选择「覆盖」更新/合并）`,
    )
  }
})
