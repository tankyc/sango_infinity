/**
 * 文件名：backup.js
 * 描述：武将库数据备份与还原模块。
 *
 *       一、备份范围（server/data 下的数据文件）
 *           - PersonLibrary.json   基础武将库
 *           - CustomPerson.json    自建武将库
 *           - accounts.json        账号与角色
 *           - options.json         编辑选项（特性 / 性格 / 成长类型等）
 *           - referencePersons.json 内置姓名索引
 *           - .id-sequence.json    ID 序列
 *         注意：自制头像图片（server/face，数千个 png）不在备份范围内，
 *         体积过大，需要单独备份该目录。
 *
 *       二、自动备份
 *           - 每天在 BACKUP_HOUR（默认 04:00）之后执行一次；
 *             服务停机错过时，会在启动后补做当天的备份（当天已备份则跳过）；
 *           - 保留最近 BACKUP_KEEP_DAYS 天（默认 60 天），更早的自动清理；
 *             另设 BACKUP_MAX_SETS 上限（默认 200 份）作为磁盘安全阀。
 *
 *       三、还原
 *           - 还原前会自动创建一份「还原前快照」（reason=pre-restore），
 *             因此还原操作本身可回滚；
 *           - 只覆盖备份中包含的文件，未包含的文件保持不动。
 * 创建日期：2026-09-30
 */

import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

/** 数据目录（与 auth.js / index.js 保持一致） */
const DATA_DIR = path.join(__dirname, 'data')

/** 备份根目录：server/backups/ */
export const BACKUP_ROOT = path.join(__dirname, 'backups')

/** 参与备份的数据文件（相对 server/data 的文件名） */
const BACKUP_FILES = [
  'PersonLibrary.json',
  'CustomPerson.json',
  'accounts.json',
  'options.json',
  'referencePersons.json',
  '.id-sequence.json',
]

/** 参与备份的两个武将库文件（用于统计条数） */
const LIB_FILES = { base: 'PersonLibrary.json', custom: 'CustomPerson.json' }

/** 每日自动备份的执行时刻（0-23 点，默认 4 点） */
const BACKUP_HOUR = (() => {
  const raw = Number(process.env.BACKUP_HOUR ?? 4)
  if (!Number.isFinite(raw)) return 4
  return Math.min(23, Math.max(0, Math.trunc(raw)))
})()

/** 备份保留天数（默认 60 天） */
const BACKUP_KEEP_DAYS = (() => {
  const raw = Number(process.env.BACKUP_KEEP_DAYS ?? 60)
  if (!Number.isFinite(raw) || raw <= 0) return 60
  return Math.trunc(raw)
})()

/** 备份份数上限（磁盘安全阀，默认 200 份） */
const BACKUP_MAX_SETS = (() => {
  const raw = Number(process.env.BACKUP_MAX_SETS ?? 200)
  if (!Number.isFinite(raw) || raw <= 0) return 200
  return Math.trunc(raw)
})()

/** 定时检查间隔：10 分钟探测一次「今天是否已备份」 */
const CHECK_INTERVAL_MS = 10 * 60 * 1000

/** 备份目录名格式：YYYY-MM-DD_HHmmss（可选 -N 后缀保证同一秒内唯一） */
const BACKUP_ID_PATTERN = /^\d{4}-\d{2}-\d{2}_\d{6}(-\d+)?$/

/** 调度器是否已启动 */
let schedulerStarted = false

/**
 * 两位补零。
 * @param {number} value 数值
 * @returns {string} 补零字符串
 */
function pad(value) {
  return String(value).padStart(2, '0')
}

/**
 * 生成日期键（YYYY-MM-DD，本地时区）。
 * @param {Date} [date] 日期
 * @returns {string} 日期键
 */
function dateKeyOf(date = new Date()) {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/**
 * 生成备份目录名（YYYY-MM-DD_HHmmss，本地时区）。
 * @param {Date} [date] 日期
 * @returns {string} 备份标识
 */
function backupIdOf(date = new Date()) {
  return `${dateKeyOf(date)}_${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}`
}

/**
 * 确保备份根目录存在。
 */
function ensureBackupRoot() {
  if (!fs.existsSync(BACKUP_ROOT)) fs.mkdirSync(BACKUP_ROOT, { recursive: true })
}

/**
 * 列出全部备份标识（按时间倒序，最新在前）。
 * @returns {string[]} 备份标识列表
 */
function listBackupIds() {
  ensureBackupRoot()
  try {
    return fs
      .readdirSync(BACKUP_ROOT, { withFileTypes: true })
      .filter((item) => item.isDirectory() && BACKUP_ID_PATTERN.test(item.name))
      .map((item) => item.name)
      .sort((a, b) => b.localeCompare(a))
  } catch (err) {
    console.error('[备份] 读取备份目录失败：', err.message)
    return []
  }
}

/**
 * 读取备份的元信息（meta.json 缺失时按目录内容兜底生成）。
 * @param {string} id 备份标识
 * @returns {object|null} 元信息
 */
function readMeta(id) {
  const dir = path.join(BACKUP_ROOT, id)
  if (!fs.existsSync(dir)) return null
  try {
    const metaFile = path.join(dir, 'meta.json')
    if (fs.existsSync(metaFile)) {
      const meta = JSON.parse(fs.readFileSync(metaFile, 'utf8'))
      return { ...meta, id }
    }
  } catch (err) {
    console.error(`[备份] 读取 ${id}/meta.json 失败：`, err.message)
  }
  // 兜底：按目录内容生成
  let totalSize = 0
  const files = []
  for (const name of BACKUP_FILES) {
    const file = path.join(dir, name)
    if (!fs.existsSync(file)) continue
    const size = fs.statSync(file).size
    totalSize += size
    files.push({ name, size })
  }
  return { id, createdAt: id, reason: 'unknown', files, totalSize, counts: {} }
}

/**
 * 统计一个武将库文件的条数与结构版本（顺带校验 JSON 合法性）。
 * @param {string} file 文件绝对路径
 * @returns {{count:number, dataVersion:number|null}} 统计结果
 */
function countLibrary(file) {
  try {
    if (!fs.existsSync(file)) return { count: 0, dataVersion: null }
    const lib = JSON.parse(fs.readFileSync(file, 'utf8')).PersonLibrary || {}
    let count = 0
    for (const value of Object.values(lib)) {
      if (value && typeof value === 'object' && !Array.isArray(value)) count++
    }
    return { count, dataVersion: Number.isFinite(Number(lib.dataVersion)) ? Number(lib.dataVersion) : null }
  } catch (err) {
    console.error(`[备份] 解析 ${path.basename(file)} 失败：`, err.message)
    return { count: 0, dataVersion: null }
  }
}

/**
 * 清理超出保留策略的备份。
 * 规则：只保留「最近 BACKUP_KEEP_DAYS 天」内的备份，且最多保留 BACKUP_MAX_SETS 份。
 * @returns {string[]} 被删除的备份标识
 */
function pruneBackups() {
  const ids = listBackupIds()
  const cutoff = new Date()
  cutoff.setHours(0, 0, 0, 0)
  cutoff.setDate(cutoff.getDate() - (BACKUP_KEEP_DAYS - 1))
  const cutoffKey = dateKeyOf(cutoff)

  const removed = []
  ids.forEach((id, index) => {
    const expired = id.slice(0, 10) < cutoffKey
    const overflow = index >= BACKUP_MAX_SETS
    if (!expired && !overflow) return
    try {
      fs.rmSync(path.join(BACKUP_ROOT, id), { recursive: true, force: true })
      removed.push(id)
    } catch (err) {
      console.error(`[备份] 清理 ${id} 失败：`, err.message)
    }
  })
  if (removed.length > 0) {
    console.log(`[备份] 已清理 ${removed.length} 份过期备份（保留最近 ${BACKUP_KEEP_DAYS} 天）`)
  }
  return removed
}

/**
 * 执行一次备份。
 * @param {string} [reason] 备份原因：daily（每日自动）/ manual（手动）/ pre-restore（还原前快照）
 * @returns {object} 备份元信息
 */
export function runBackup(reason = 'manual') {
  ensureBackupRoot()
  const now = new Date()
  let id = backupIdOf(now)
  let suffix = 1
  while (fs.existsSync(path.join(BACKUP_ROOT, id))) {
    id = `${backupIdOf(now)}-${suffix++}`
  }
  const dir = path.join(BACKUP_ROOT, id)
  fs.mkdirSync(dir, { recursive: true })

  const files = []
  let totalSize = 0
  for (const name of BACKUP_FILES) {
    const src = path.join(DATA_DIR, name)
    if (!fs.existsSync(src)) continue
    const dest = path.join(dir, name)
    fs.copyFileSync(src, dest)
    const size = fs.statSync(dest).size
    totalSize += size
    files.push({ name, size })
  }

  const counts = {}
  for (const [key, file] of Object.entries(LIB_FILES)) {
    counts[key] = countLibrary(path.join(DATA_DIR, file))
  }

  const meta = {
    id,
    createdAt: now.toISOString(),
    reason,
    files,
    totalSize,
    counts,
  }
  fs.writeFileSync(path.join(dir, 'meta.json'), `${JSON.stringify(meta, null, 2)}\n`, 'utf8')
  pruneBackups()

  const mb = (totalSize / 1024 / 1024).toFixed(2)
  console.log(
    `[备份] 已完成${reason === 'daily' ? '每日' : reason === 'pre-restore' ? '还原前' : '手动'}备份：${id}` +
      `（${files.length} 个文件，${mb} MB，基础库 ${counts.base.count} / 自建库 ${counts.custom.count}）`,
  )
  return meta
}

/**
 * 列出备份（含元信息，按时间倒序）。
 * @returns {object[]} 备份列表
 */
export function listBackups() {
  return listBackupIds()
    .map((id) => readMeta(id))
    .filter(Boolean)
}

/**
 * 还原指定备份。
 * 会先自动创建一份「还原前快照」，再覆盖备份中包含的数据文件。
 * @param {string} id 备份标识
 * @returns {{error:string}|{id:string,restored:string[],snapshotId:string}} 结果
 */
export function restoreBackup(id) {
  const clean = String(id || '').trim()
  if (!BACKUP_ID_PATTERN.test(clean)) {
    return { error: '备份标识不合法' }
  }
  const dir = path.join(BACKUP_ROOT, clean)
  if (!fs.existsSync(dir)) {
    return { error: '该备份不存在或已被清理' }
  }

  const available = BACKUP_FILES.filter((name) => fs.existsSync(path.join(dir, name)))
  if (available.length === 0) {
    return { error: '该备份中没有可用的数据文件' }
  }

  // 还原前快照：保证还原操作本身可回滚
  const snapshot = runBackup('pre-restore')

  const restored = []
  for (const name of available) {
    try {
      fs.copyFileSync(path.join(dir, name), path.join(DATA_DIR, name))
      restored.push(name)
    } catch (err) {
      console.error(`[备份] 还原 ${name} 失败：`, err.message)
      return { error: `还原 ${name} 失败：${err.message}`, restored, snapshotId: snapshot.id }
    }
  }

  console.log(`[备份] 已从 ${clean} 还原 ${restored.length} 个文件（还原前快照：${snapshot.id}）`)
  return { id: clean, restored, snapshotId: snapshot.id }
}

/**
 * 备份调度状态（供接口展示）。
 * @returns {object} 状态信息
 */
export function backupStatus() {
  const list = listBackups()
  return {
    hour: BACKUP_HOUR,
    keepDays: BACKUP_KEEP_DAYS,
    maxSets: BACKUP_MAX_SETS,
    total: list.length,
    latestId: list[0]?.id || '',
  }
}

/**
 * 判断某个日期是否已有备份。
 * @param {string} dateKey 日期键（YYYY-MM-DD）
 * @returns {boolean} 是否已备份
 */
function hasBackupForDay(dateKey) {
  return listBackupIds().some((id) => id.slice(0, 10) === dateKey)
}

/**
 * 启动每日自动备份调度。
 * 启动时先检查一次（补做当天备份），之后每 10 分钟检查一次；
 * 每天只会产生一份「daily」备份。
 */
export function startBackupScheduler() {
  if (schedulerStarted) return
  schedulerStarted = true

  const tick = () => {
    try {
      const now = new Date()
      if (now.getHours() < BACKUP_HOUR) return
      const today = dateKeyOf(now)
      if (hasBackupForDay(today)) return
      runBackup('daily')
    } catch (err) {
      console.error('[备份] 定时备份失败：', err.message)
    }
  }

  tick()
  setInterval(tick, CHECK_INTERVAL_MS)
  console.log(
    `[备份] 已启用每日自动备份：每天 ${pad(BACKUP_HOUR)}:00 后执行一次，保留最近 ${BACKUP_KEEP_DAYS} 天` +
      `（最多 ${BACKUP_MAX_SETS} 份），目录：${BACKUP_ROOT}`,
  )
}
