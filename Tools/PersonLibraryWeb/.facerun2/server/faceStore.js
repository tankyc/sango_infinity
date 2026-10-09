/**
 * 文件名：faceStore.js
 * 描述：自定义头像（立绘）资源管理模块。
 *
 *       一、资源文件命名（与游戏 Face 目录保持一致）
 *           - {id}_1.png：半身像（立绘），240 x 240；
 *           - {id}_2.png：头像，64 x 80。
 *
 *       二、自定义头像 ID 分配规则（按用户需求实现）
 *           - 起始 ID 为 3000；
 *           - 每 1000 个 ID 为一段，段的千位数字为奇数时分配给男性，
 *             为偶数时分配给女性：
 *               男性段：3000-3999、5000-5999、7000-7999 ……
 *               女性段：4000-4999、6000-6999、8000-8999 ……
 *           - 因此 3000 为男性首个 ID，4000 为女性首个 ID，
 *             即“单数千位为男、双数千位为女”；
 *           - 某一段用满（到达 x999）后，下一位跳至本性别所属的下一个段
 *             （跨过异性段，号段起点增加 2000），始终满足奇偶性别规则。
 *
 *       三、对外能力
 *           - 列出全部自定义头像（含上传者、空白占位格、下一个可用 ID）；
 *           - 保存一张 / 批量保存多张自定义头像（半身像 + 头像，均为 PNG）；
 *           - 更新已有头像的图片（重新导入半身像、重新裁剪小头像）；
 *           - 删除指定自定义头像（**保留空白占位格**，便于把号段收拾成连续区间）；
 *           - 移动到空白占位格（仅限同性别号段）/ 修改男女（按目标性别重新编号）；
 *           - 将指定头像打包为 ZIP 供下载（ZIP 采用 store 存储，不二次压缩）；
 *             打包时会按实际导出的 ID 自动生成容貌配置 FaceConfig.json 一并放入 ZIP，
 *             省去手工编写 HeadIdRanges（放游戏 Data 目录即可生效）。
 *
 *       四、元数据（server/data/CustomFaceMeta.json）
 *           记录每个头像的上传者与时间（用于按上传者筛查、图片缓存版本号），
 *           以及被删除 / 搬走后需要保留的空白占位格 ID。图片本身仍以
 *           {id}_1.png（半身像 240x240）、{id}_2.png（头像 64x80）存放。
 *
 * 创建日期：2026-09-10
 */

import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'
import * as r2 from './r2.js'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

/** 头像资源目录（与 index.js 中的静态目录保持一致） */
const FACE_DIR = path.join(__dirname, 'face')

/** 打包导出时自动附带的容貌配置文件（放进游戏的 Data 目录即可生效） */
const FACE_CONFIG_NAME = 'FaceConfig.json'

/** 自定义头像起始 ID（男性首个 ID，女性首个 ID 为 4000） */
export const CUSTOM_FACE_BASE_ID = 3000

/** 性别常量：0=男，1=女（与游戏 Person.sex 一致） */
export const SEX_MALE = 0
export const SEX_FEMALE = 1

/** 每个号段容纳的 ID 数量（x000 - x999 共 1000 个） */
const SEGMENT_SIZE = 1000

/** 号段千位的搜索上限（防止异常情况下无限循环） */
const MAX_SEGMENT_INDEX = 98

/** 半身像尺寸（宽 x 高） */
const BUST_SIZE = { width: 240, height: 240 }

/** 头像尺寸（宽 x 高） */
const FACE_SIZE = { width: 64, height: 80 }

/** 单张图片允许的最大字节数（约 8MB，防止异常大图撑爆内存） */
const MAX_IMAGE_BYTES = 8 * 1024 * 1024

/**
 * 已存在的自定义头像 ID 缓存。
 * 首次访问时扫描磁盘建立，之后由保存 / 删除操作增量维护，避免每次请求都遍历目录。
 * @type {Set<number>|null}
 */
let customIdCache = null

/**
 * 依据 ID 推导性别。
 * 千位为奇数 => 男；千位为偶数 => 女。
 * @param {number} id 头像 ID
 * @returns {number} 0=男，1=女
 */
export function sexOfFaceId(id) {
  const segment = Math.floor(id / SEGMENT_SIZE)
  return segment % 2 === 1 ? SEX_MALE : SEX_FEMALE
}

/**
 * 判断某个 ID 是否属于自定义头像范围。
 * @param {number} id 头像 ID
 * @returns {boolean} 是否为自定义头像 ID
 */
export function isCustomFaceId(id) {
  return Number.isInteger(id) && id >= CUSTOM_FACE_BASE_ID
}

/**
 * 判断指定性别使用哪个号段。
 * @param {number} segment 号段序号（即 ID 的千位数字）
 * @param {number} sex 性别，0=男，1=女
 * @returns {boolean} 该号段是否属于该性别
 */
function segmentBelongsToSex(segment, sex) {
  const isMaleSegment = segment % 2 === 1
  return sex === SEX_MALE ? isMaleSegment : !isMaleSegment
}

/**
 * 扫描头像目录，收集全部自定义头像 ID。
 * @returns {Set<number>} 自定义头像 ID 集合
 */
function scanCustomIds() {
  const ids = new Set()
  if (!fs.existsSync(FACE_DIR)) return ids
  let files = []
  try {
    files = fs.readdirSync(FACE_DIR)
  } catch {
    return ids
  }
  for (const name of files) {
    // 仅统计半身像（_1）与头像（_2），忽略其它文件
    const matched = /^(\d+)_([12])\.png$/i.exec(name)
    if (!matched) continue
    const id = Number(matched[1])
    if (!isCustomFaceId(id)) continue
    ids.add(id)
  }
  return ids
}

/**
 * 取得自定义头像 ID 集合（带缓存）。
 * @returns {Set<number>} 自定义头像 ID 集合
 */
function getCustomIds() {
  if (!customIdCache) customIdCache = scanCustomIds()
  return customIdCache
}

/**
 * 强制使缓存失效，下次访问重新扫描磁盘。
 */
export function invalidateFaceCache() {
  customIdCache = null
}

// ─────────────────────────────────────────────────────────────
// 头像元数据（上传者、空白占位格、时间戳）
// ─────────────────────────────────────────────────────────────

/**
 * 头像元数据文件。
 * 结构：{ version: 1, faces: { [id]: { uploader, uploaderName, createdAt, updatedAt } }, blanks: number[] }
 *   - faces：上传者与时间，用于「按上传者筛查」以及图片缓存版本号；
 *   - blanks：被删除（或搬走后腾空）的 ID，前端据此保留空白占位格，
 *     允许把其它头像拖拽过去，只与空白格互换。
 */
const FACE_META_FILE = path.join(__dirname, 'data', 'CustomFaceMeta.json')

/** 元数据缓存（首次访问时从磁盘读取） */
let metaCache = null

/**
 * 读取头像元数据（带缓存）。
 * @returns {{version:number, faces:Record<string,object>, blanks:number[]}} 元数据
 */
function readMeta() {
  if (metaCache) return metaCache
  const empty = { version: 1, faces: {}, blanks: [] }
  try {
    const raw = JSON.parse(fs.readFileSync(FACE_META_FILE, 'utf8'))
    const faces = raw && typeof raw.faces === 'object' && raw.faces ? raw.faces : {}
    const blanks = Array.isArray(raw?.blanks) ? raw.blanks : []
    metaCache = {
      version: 1,
      faces,
      blanks: [
        ...new Set(blanks.map((n) => Number(n)).filter((n) => Number.isInteger(n) && n >= CUSTOM_FACE_BASE_ID)),
      ].sort((a, b) => a - b),
    }
  } catch {
    // 文件不存在或损坏时按空处理，不阻断头像功能
    metaCache = empty
  }
  return metaCache
}

/**
 * 写入头像元数据（失败只记日志，不影响图片本身已保存成功的事实）。
 */
function writeMeta() {
  try {
    fs.mkdirSync(path.dirname(FACE_META_FILE), { recursive: true })
    fs.writeFileSync(FACE_META_FILE, `${JSON.stringify(readMeta(), null, 2)}\n`, 'utf8')
  } catch (err) {
    console.warn(`[头像] 元数据写入失败：${err.message}`)
  }
}

/**
 * 取某个头像 ID 的元数据。
 * @param {number} id 头像 ID
 * @returns {object} 元数据（不存在时为空对象）
 */
function faceMetaOf(id) {
  return readMeta().faces[String(id)] || {}
}

/**
 * 合并写入某个头像 ID 的元数据并落盘。
 * @param {number} id 头像 ID
 * @param {object} patch 需要写入的字段
 */
function setFaceMeta(id, patch) {
  const meta = readMeta()
  meta.faces[String(id)] = { ...(meta.faces[String(id)] || {}), ...patch }
  writeMeta()
}

/**
 * 删除某个头像 ID 的元数据（原地写盘，不触发额外 IO）。
 * @param {number} id 头像 ID
 */
function dropFaceMeta(id) {
  delete readMeta().faces[String(id)]
}

/**
 * 标记某个 ID 为空白占位格（被删除 / 被搬走后保留位置）。
 * @param {number} id 头像 ID
 */
function markBlank(id) {
  const meta = readMeta()
  if (!meta.blanks.includes(id)) {
    meta.blanks.push(id)
    meta.blanks.sort((a, b) => a - b)
  }
}

/**
 * 取消某个 ID 的空白占位格标记（该 ID 重新有图后调用）。
 * @param {number} id 头像 ID
 */
function clearBlank(id) {
  const meta = readMeta()
  const index = meta.blanks.indexOf(id)
  if (index >= 0) meta.blanks.splice(index, 1)
}

/**
 * 由登录用户生成上传者信息。
 * @param {object|null} user 当前登录用户
 * @returns {{uploader?:string,uploaderName?:string}} 上传者字段
 */
function uploaderOf(user) {
  const username = user && user.username ? String(user.username) : ''
  if (!username) return {}
  return { uploader: username, uploaderName: String(user.displayName || username) }
}

/** 取 ISO 时间字符串 */
function nowIso() {
  return new Date().toISOString()
}

/**
 * 计算指定性别的下一个可用自定义头像 ID。
 * 按号段顺序（男性 3、5、7 …… 千位；女性 4、6、8 …… 千位）查找首个未被占用的 ID。
 * @param {number} sex 性别，0=男，1=女
 * @param {Set<number>} [used] 已占用的 ID 集合，缺省时使用缓存
 * @returns {number} 下一个可用 ID；号段耗尽时返回 0
 */
export function nextFaceId(sex, used) {
  const occupied = used || getCustomIds()
  const baseSegment = Math.floor(CUSTOM_FACE_BASE_ID / SEGMENT_SIZE)
  for (let segment = baseSegment; segment <= MAX_SEGMENT_INDEX; segment++) {
    // 跳过不属于该性别的号段，保证“单数千位为男、双数千位为女”
    if (!segmentBelongsToSex(segment, sex)) continue
    const start = segment * SEGMENT_SIZE
    for (let id = start; id < start + SEGMENT_SIZE; id++) {
      if (!occupied.has(id)) return id
    }
  }
  return 0
}

/**
 * 解析前端上传的 PNG 图片。
 * 支持纯 base64 或 dataURL 两种形式，并校验 PNG 文件头与图像尺寸。
 * @param {string} input 图片内容（base64 或 dataURL）
 * @param {{width:number,height:number}} expect 期望尺寸
 * @param {string} label 字段名称（用于错误提示）
 * @returns {{buffer:Buffer}|{error:string}} 解析结果
 */
function decodePng(input, expect, label) {
  const raw = typeof input === 'string' ? input.trim() : ''
  if (!raw) return { error: `缺少${label}数据` }
  // 去掉 dataURL 前缀
  const base64 = raw.startsWith('data:') ? raw.slice(raw.indexOf(',') + 1) : raw
  let buffer
  try {
    buffer = Buffer.from(base64, 'base64')
  } catch {
    return { error: `${label}数据格式不正确` }
  }
  if (buffer.length === 0) return { error: `${label}数据为空` }
  if (buffer.length > MAX_IMAGE_BYTES) return { error: `${label}体积过大（上限 8MB）` }
  // 校验 PNG 文件头（137 80 78 71 13 10 26 10）
  const pngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]
  for (let i = 0; i < pngSignature.length; i++) {
    if (buffer[i] !== pngSignature[i]) return { error: `${label}必须为 PNG 格式` }
  }
  const size = readPngSize(buffer)
  if (!size) return { error: `${label}文件已损坏` }
  if (size.width !== expect.width || size.height !== expect.height) {
    return {
      error: `${label}尺寸必须为 ${expect.width}x${expect.height}，当前为 ${size.width}x${size.height}`,
    }
  }
  return { buffer }
}

/**
 * 读取 PNG 的像素尺寸（解析 IHDR 数据块）。
 * @param {Buffer} buffer PNG 文件内容
 * @returns {{width:number,height:number}|null} 尺寸信息，解析失败返回 null
 */
function readPngSize(buffer) {
  if (buffer.length < 24) return null
  // 8 字节签名 + 4 字节块长度 + 4 字节块类型（IHDR）后即为宽高
  const width = buffer.readUInt32BE(16)
  const height = buffer.readUInt32BE(20)
  if (!width || !height) return null
  return { width, height }
}

/**
 * 自定义头像文件路径。
 * @param {number} id 头像 ID
 * @param {1|2} type 1=半身像，2=头像
 * @returns {string} 绝对路径
 */
function faceFilePath(id, type) {
  return path.join(FACE_DIR, `${id}_${type}.png`)
}

/**
 * 确保头像目录存在。
 */
function ensureFaceDir() {
  if (!fs.existsSync(FACE_DIR)) fs.mkdirSync(FACE_DIR, { recursive: true })
}

/**
 * 列出自定义头像列表、空白占位格、上传者清单与下一个可用 ID。
 * @returns {{
 *   items:Array<{id:number,sex:number,hasBust:boolean,hasFace:boolean,uploader:string|null,uploaderName:string|null,createdAt:string|null,updatedAt:string|null,version:number}>,
 *   blankIds:number[],
 *   uploaders:Array<{username:string|null,name:string,count:number}>,
 *   nextId:{male:number,female:number}
 * }}
 */
export function listCustomFaces() {
  const used = getCustomIds()
  const ids = [...used].sort((a, b) => a - b)
  const items = ids.map((id) => {
    const meta = faceMetaOf(id)
    return {
      id,
      sex: sexOfFaceId(id),
      hasBust: fs.existsSync(faceFilePath(id, 1)),
      hasFace: fs.existsSync(faceFilePath(id, 2)),
      /** 上传者账号（历史数据可能为空） */
      uploader: meta.uploader || null,
      /** 上传者显示名 */
      uploaderName: meta.uploaderName || null,
      createdAt: meta.createdAt || null,
      updatedAt: meta.updatedAt || null,
      /** 图片缓存破坏参数（毫秒时间戳，0 表示无需破坏） */
      version: meta.updatedAt ? Date.parse(meta.updatedAt) || 0 : 0,
    }
  })

  // 空白占位格 = 显式记录的已删除 / 已搬走 ID + 各性别「首尾之间」的断号。
  // 只取同性别最小与最大 ID 之间的断号：号段尾部的大片空闲 ID 不算占位格，
  // 否则（例如男性 3000-3132、女性 4000）会把 3133-3999 全算成空白格，把网格撑爆。
  const blankSet = new Set(readMeta().blanks.filter((id) => !used.has(id)))
  for (const sex of [SEX_MALE, SEX_FEMALE]) {
    const sexIds = ids.filter((id) => sexOfFaceId(id) === sex)
    if (sexIds.length === 0) continue
    for (let id = sexIds[0]; id <= sexIds[sexIds.length - 1]; id++) {
      if (!used.has(id)) blankSet.add(id)
    }
  }

  // 上传者清单（用于前端按上传者筛查）
  const uploaderMap = new Map()
  for (const item of items) {
    const key = item.uploader || ''
    const entry = uploaderMap.get(key) || {
      username: item.uploader,
      name: item.uploaderName || item.uploader || '未记录上传者',
      count: 0,
    }
    entry.count++
    uploaderMap.set(key, entry)
  }

  return {
    items,
    /** 空白占位格 ID（按升序） */
    blankIds: [...blankSet].sort((a, b) => a - b),
    /** 上传者清单（按数量倒序） */
    uploaders: [...uploaderMap.values()].sort((a, b) => b.count - a.count),
    /** 各性别的下一个可用 ID（男 / 女分别独立排号） */
    nextId: {
      male: nextFaceId(SEX_MALE, used),
      female: nextFaceId(SEX_FEMALE, used),
    },
  }
}

/**
 * 保存一张自定义头像（半身像 + 头像）。
 * @param {object} payload 请求数据
 * @param {number} payload.sex 性别，0=男，1=女
 * @param {string} payload.bust 半身像 PNG（base64 或 dataURL）
 * @param {string} payload.face 头像 PNG（base64 或 dataURL）
 * @param {object|null} [user] 当前登录用户，用于记录上传者
 * @returns {Promise<{id:number,storage:string}|{error:string}>} 保存结果
 */
export async function saveCustomFace(payload, user = null) {
  const body = payload && typeof payload === 'object' ? payload : {}
  const sex = Number(body.sex)
  if (sex !== SEX_MALE && sex !== SEX_FEMALE) {
    return { error: '请选择性别（男 / 女）' }
  }

  const bust = decodePng(body.bust, BUST_SIZE, '半身像')
  if (bust.error) return { error: bust.error }
  const face = decodePng(body.face, FACE_SIZE, '头像')
  if (face.error) return { error: face.error }

  const used = getCustomIds()
  const id = nextFaceId(sex, used)
  if (!id) return { error: '自定义头像 ID 已用完，请先清理无用头像' }

  const bustName = `${id}_1.png`
  const faceName = `${id}_2.png`

  ensureFaceDir()
  try {
    fs.writeFileSync(faceFilePath(id, 1), bust.buffer)
    fs.writeFileSync(faceFilePath(id, 2), face.buffer)
  } catch (err) {
    removeFaceFiles(id)
    return { error: `保存失败：${err.message}` }
  }

  // R2 启用时双写，并且**要么都成功、要么都回滚**：
  // 若只写成功本地，读路径会把请求 302 到 R2 上并不存在的对象，直接 404。
  try {
    await r2.putFace(bustName, bust.buffer)
    await r2.putFace(faceName, face.buffer)
  } catch (err) {
    removeFaceFiles(id)
    return { error: `已写入本地但同步到 R2 失败，已回滚：${err.message}` }
  }

  used.add(id)
  const stamp = nowIso()
  clearBlank(id)
  setFaceMeta(id, { ...uploaderOf(user), createdAt: stamp, updatedAt: stamp })
  return { id, storage: r2.status().canWrite ? 'local+r2' : 'local' }
}

/** 单次批量导入的最大张数（每张含半身像与头像两张 PNG，避免请求体过大） */
const BATCH_SAVE_LIMIT = 40

/**
 * 批量保存自定义头像（批量导入半身像用）。
 * 与单张保存的区别：一次分配多个 ID、统一落盘；任一步失败回滚本批全部写入，
 * 不会出现「导入到一半」的中间状态。
 * @param {object} payload 请求数据
 * @param {number} payload.sex 性别，0=男，1=女
 * @param {Array<{bust:string,face:string}>} payload.items 待导入的图片对（半身像 + 头像）
 * @param {object|null} [user] 当前登录用户，用于记录上传者
 * @returns {Promise<{ids:number[],count:number,storage:string}|{error:string}>} 导入结果
 */
export async function saveCustomFacesBatch(payload, user = null) {
  const body = payload && typeof payload === 'object' ? payload : {}
  const sex = Number(body.sex)
  if (sex !== SEX_MALE && sex !== SEX_FEMALE) return { error: '请选择性别（男 / 女）' }
  const items = Array.isArray(body.items) ? body.items : []
  if (items.length === 0) return { error: '请选择要导入的半身像图片' }
  if (items.length > BATCH_SAVE_LIMIT) {
    return { error: `单次最多导入 ${BATCH_SAVE_LIMIT} 张，当前 ${items.length} 张` }
  }

  // 先整体校验，再统一写入，避免中途失败留下脏数据
  const decoded = []
  for (let i = 0; i < items.length; i++) {
    const bust = decodePng(items[i]?.bust, BUST_SIZE, `第 ${i + 1} 张半身像`)
    if (bust.error) return { error: bust.error }
    const face = decodePng(items[i]?.face, FACE_SIZE, `第 ${i + 1} 张头像`)
    if (face.error) return { error: face.error }
    decoded.push({ bust: bust.buffer, face: face.buffer })
  }

  const used = getCustomIds()
  /** 本批已写入的项（含分配的 ID 与图片数据，便于 R2 同步与回滚） */
  const written = []
  /** 回滚本批：删掉已写文件并撤销缓存占用 */
  const rollback = () => {
    for (const item of written) {
      removeFaceFiles(item.id)
      used.delete(item.id)
    }
  }

  ensureFaceDir()
  try {
    for (const item of decoded) {
      const id = nextFaceId(sex, used)
      if (!id) {
        rollback()
        return { error: `本批第 ${written.length + 1} 张没有可用 ID，已回滚本批` }
      }
      fs.writeFileSync(faceFilePath(id, 1), item.bust)
      fs.writeFileSync(faceFilePath(id, 2), item.face)
      used.add(id)
      written.push({ id, bust: item.bust, face: item.face })
    }
  } catch (err) {
    rollback()
    return { error: `保存失败，已回滚本批：${err.message}` }
  }

  try {
    for (const item of written) {
      await r2.putFace(`${item.id}_1.png`, item.bust)
      await r2.putFace(`${item.id}_2.png`, item.face)
    }
  } catch (err) {
    rollback()
    return { error: `已写入本地但同步到 R2 失败，已回滚本批：${err.message}` }
  }

  const stamp = nowIso()
  const uploader = uploaderOf(user)
  const meta = readMeta()
  for (const item of written) {
    clearBlank(item.id)
    meta.faces[String(item.id)] = { ...uploader, createdAt: stamp, updatedAt: stamp }
  }
  writeMeta()

  return {
    ids: written.map((item) => item.id),
    count: written.length,
    storage: r2.status().canWrite ? 'local+r2' : 'local',
  }
}

/**
 * 更新已有自定义头像的图片。
 * 用于「重新导入半身像」与「以半身像为准重新裁剪小头像」：
 *   - 只更新传入的图片（bust / face 至少传一个）；
 *   - 更新前备份原文件，任何一步失败整体还原，避免半新半旧；
 *   - 成功后写入 updatedAt，前端给图片地址带版本参数，
 *     绕过浏览器与 CDN 的强缓存（自定义头像地址原本是 immutable 长缓存）。
 * @param {number} id 头像 ID
 * @param {{bust?:string,face?:string}} payload 待更新的图片
 * @param {object|null} [user] 当前登录用户
 * @returns {Promise<{id:number,updated:string[],updatedAt:string,version:number}|{error:string}>} 更新结果
 */
export async function updateCustomFace(id, payload, user = null) {
  const target = Number(id)
  if (!isCustomFaceId(target)) return { error: '仅允许修改自定义头像（ID ≥ 3000）' }
  if (!getCustomIds().has(target)) return { error: `未找到 ID 为 ${target} 的自定义头像` }

  const body = payload && typeof payload === 'object' ? payload : {}
  /** @type {Record<number, Buffer>} 下标 1=半身像，2=头像 */
  const next = {}
  if (body.bust !== undefined && body.bust !== null && body.bust !== '') {
    const bust = decodePng(body.bust, BUST_SIZE, '半身像')
    if (bust.error) return { error: bust.error }
    next[1] = bust.buffer
  }
  if (body.face !== undefined && body.face !== null && body.face !== '') {
    const face = decodePng(body.face, FACE_SIZE, '头像')
    if (face.error) return { error: face.error }
    next[2] = face.buffer
  }
  const types = Object.keys(next).map(Number)
  if (types.length === 0) return { error: '没有需要更新的图片' }

  // 备份原文件，失败时整体还原
  const backups = {}
  for (const type of types) {
    const file = faceFilePath(target, type)
    if (fs.existsSync(file)) backups[type] = fs.readFileSync(file)
  }

  ensureFaceDir()
  try {
    for (const type of types) fs.writeFileSync(faceFilePath(target, type), next[type])
    for (const type of types) await r2.putFace(`${target}_${type}.png`, next[type])
  } catch (err) {
    for (const type of types) {
      if (backups[type]) fs.writeFileSync(faceFilePath(target, type), backups[type])
      else fs.rmSync(faceFilePath(target, type), { force: true })
    }
    return { error: `更新失败，已还原原图：${err.message}` }
  }

  const stamp = nowIso()
  const meta = faceMetaOf(target)
  setFaceMeta(target, {
    ...(meta.uploader ? {} : uploaderOf(user)),
    createdAt: meta.createdAt || stamp,
    updatedAt: stamp,
  })
  return {
    id: target,
    updated: types.map((type) => (type === 1 ? 'bust' : 'face')),
    updatedAt: stamp,
    version: Date.parse(stamp) || 0,
  }
}

/**
 * 把某个头像的两张图片整体搬到另一个 ID（内部实现，不做性别校验）。
 * 用于「拖拽到空白占位格」与「修改男女后按目标性别重新编号」。
 * @param {number} from 原 ID
 * @param {number} to 目标 ID（必须是空白格）
 * @param {object} [options] 可选参数
 * @param {(from:number,to:number)=>number} [options.remapRef] 同步武将头像引用的回调，返回改动条数
 * @returns {Promise<{from:number,to:number,remapped:number,warnings:string[]}|{error:string}>} 结果
 */
async function relocateFace(from, to, options = {}) {
  const files = []
  for (const type of [1, 2]) {
    const file = faceFilePath(from, type)
    if (fs.existsSync(file)) files.push({ type, data: fs.readFileSync(file) })
  }
  if (files.length === 0) return { error: `ID ${from} 没有可移动的图片文件` }

  // 先写目标，目标写成功后再清源，避免中途失败两头皆空
  ensureFaceDir()
  try {
    for (const item of files) fs.writeFileSync(faceFilePath(to, item.type), item.data)
    for (const item of files) await r2.putFace(`${to}_${item.type}.png`, item.data)
  } catch (err) {
    for (const item of files) fs.rmSync(faceFilePath(to, item.type), { force: true })
    return { error: `移动到 ID ${to} 失败，已回滚：${err.message}` }
  }

  for (const item of files) fs.rmSync(faceFilePath(from, item.type), { force: true })
  const warnings = []
  for (const item of files) {
    try {
      await r2.deleteFace(`${from}_${item.type}.png`)
    } catch (err) {
      warnings.push(`${from}_${item.type}.png：${err.message}`)
      console.warn(`[头像] R2 删除失败 ${from}_${item.type}.png：${err.message}`)
    }
  }

  const used = getCustomIds()
  used.delete(from)
  used.add(to)
  const meta = faceMetaOf(from)
  dropFaceMeta(from)
  // 腾出来的位置保留为空白占位格，方便继续用它做排列
  markBlank(from)
  clearBlank(to)
  setFaceMeta(to, { ...meta, updatedAt: nowIso() })

  let remapped = 0
  if (typeof options.remapRef === 'function') {
    try {
      remapped = Number(options.remapRef(from, to)) || 0
    } catch (err) {
      warnings.push(`武将头像引用更新失败：${err.message}`)
    }
  }
  return { from, to, remapped, warnings }
}

/**
 * 把自定义头像移动到指定空白占位格。
 * 仅允许同性别号段内互换：ID 的千位决定性别，跨性别搬会导致头像与性别不符。
 * @param {number} fromId 原 ID
 * @param {number} toId 目标 ID（必须为空白格）
 * @param {object} [options] 同 relocateFace
 * @returns {Promise<object>} 结果
 */
export async function moveCustomFace(fromId, toId, options = {}) {
  const from = Number(fromId)
  const to = Number(toId)
  if (!isCustomFaceId(from) || !isCustomFaceId(to)) {
    return { error: `ID 必须在自定义号段内（≥ ${CUSTOM_FACE_BASE_ID}）` }
  }
  if (from === to) return { error: '目标 ID 与当前 ID 相同' }
  if (!getCustomIds().has(from)) return { error: `未找到 ID 为 ${from} 的自定义头像` }
  if (getCustomIds().has(to)) return { error: `ID ${to} 已有头像，只能移动到空白占位格` }
  if (sexOfFaceId(to) !== sexOfFaceId(from)) {
    const label = sexOfFaceId(to) === SEX_MALE ? '男性' : '女性'
    return { error: `ID ${to} 属于${label}号段，与当前头像性别不同；如需改性别请用「修改男女」` }
  }
  return relocateFace(from, to, options)
}

/**
 * 修改自定义头像的性别：按目标性别重新分配 ID，两张图片一并改名搬移。
 * @param {number} id 头像 ID
 * @param {number} sex 目标性别，0=男，1=女
 * @param {object} [options] 同 relocateFace
 * @returns {Promise<object>} 结果（含新分配的 ID）
 */
export async function changeCustomFaceSex(id, sex, options = {}) {
  const from = Number(id)
  const next = Number(sex)
  if (!isCustomFaceId(from)) return { error: '仅允许修改自定义头像（ID ≥ 3000）' }
  if (next !== SEX_MALE && next !== SEX_FEMALE) return { error: '性别只能是男或女' }
  if (!getCustomIds().has(from)) return { error: `未找到 ID 为 ${from} 的自定义头像` }
  if (sexOfFaceId(from) === next) {
    return { error: `ID ${from} 已经是${next === SEX_MALE ? '男性' : '女性'}头像，无需修改` }
  }
  const to = nextFaceId(next, getCustomIds())
  if (!to) return { error: `自定义头像的${next === SEX_MALE ? '男性' : '女性'}号段已满，无法改性别` }
  const result = await relocateFace(from, to, options)
  if (result.error) return result
  return { ...result, sex: next }
}

/**
 * 删除指定自定义头像（同时删除半身像与头像）。
 *
 * 远端删除失败只记日志、不回滚：本地已经删掉，残留的 R2 对象不会被任何页面引用，
 * 反倒是因为一次网络抖动就让删除操作整体失败更让人困惑。
 *
 * @param {number} id 头像 ID
 * @returns {Promise<{id:number,warnings?:string[]}|{error:string}>} 删除结果
 */
export async function deleteCustomFace(id) {
  const target = Number(id)
  if (!isCustomFaceId(target)) return { error: '仅允许删除自定义头像（ID ≥ 3000）' }
  if (!getCustomIds().has(target)) return { error: `未找到 ID 为 ${target} 的自定义头像` }

  try {
    fs.rmSync(faceFilePath(target, 1), { force: true })
    fs.rmSync(faceFilePath(target, 2), { force: true })
  } catch (err) {
    return { error: `删除失败：${err.message}` }
  }
  getCustomIds().delete(target)
  // 删除后保留空白占位格：列表里仍显示该位置的虚框，可把其它头像拖拽过来
  dropFaceMeta(target)
  markBlank(target)
  writeMeta()

  const warnings = []
  for (const name of [`${target}_1.png`, `${target}_2.png`]) {
    try {
      await r2.deleteFace(name)
    } catch (err) {
      warnings.push(`${name}：${err.message}`)
      console.warn(`[头像] R2 删除失败 ${name}：${err.message}`)
    }
  }

  return warnings.length > 0 ? { id: target, warnings } : { id: target }
}

/**
 * 清掉某个头像 ID 对应的两个本地文件（保存失败时回滚用）。
 * @param {number} id 头像 ID
 */
function removeFaceFiles(id) {
  try {
    fs.rmSync(faceFilePath(id, 1), { force: true })
    fs.rmSync(faceFilePath(id, 2), { force: true })
  } catch {
    /* 回滚失败忽略 */
  }
}

/**
 * 读取指定自定义头像的图片数据。
 * @param {number[]} ids 头像 ID 列表
 * @returns {Array<{name:string,data:Buffer}>} 待打包的文件列表
 */
export function collectFaceFiles(ids) {
  const entries = []
  for (const raw of ids) {
    const id = Number(raw)
    if (!isCustomFaceId(id)) continue
    for (const type of [1, 2]) {
      const file = faceFilePath(id, type)
      if (!fs.existsSync(file)) continue
      entries.push({ name: `${id}_${type}.png`, data: fs.readFileSync(file) })
    }
  }
  return entries
}

// ─────────────────────────────────────────────────────────────
// ZIP 打包（store 存储模式）
// ─────────────────────────────────────────────────────────────

/** CRC32 查表，首次使用时构建 */
let crcTable = null

/**
 * 构建 CRC32 查表。
 * @returns {Int32Array} 查表数组
 */
function getCrcTable() {
  if (crcTable) return crcTable
  const table = new Int32Array(256)
  for (let i = 0; i < 256; i++) {
    let c = i
    for (let k = 0; k < 8; k++) {
      c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
    }
    table[i] = c
  }
  crcTable = table
  return table
}

/**
 * 计算缓冲区 CRC32 校验值（ZIP 文件头要求）。
 * @param {Buffer} buffer 数据
 * @returns {number} 无符号 CRC32
 */
function crc32(buffer) {
  const table = getCrcTable()
  let crc = -1
  for (let i = 0; i < buffer.length; i++) {
    crc = (crc >>> 8) ^ table[(crc ^ buffer[i]) & 0xff]
  }
  return (crc ^ -1) >>> 0
}

/**
 * 将 Date 转换为 DOS 格式的日期与时间。
 * @param {Date} date 时间
 * @returns {{time:number, date:number}} DOS 时间与日期
 */
function toDosDateTime(date) {
  const year = Math.max(1980, date.getFullYear())
  return {
    time: (date.getHours() << 11) | (date.getMinutes() << 5) | Math.floor(date.getSeconds() / 2),
    date: ((year - 1980) << 9) | ((date.getMonth() + 1) << 5) | date.getDate(),
  }
}

/**
 * 生成 ZIP 压缩包（store 模式，PNG 本身已压缩无需二次压缩）。
 * @param {Array<{name:string,data:Buffer}>} entries 文件列表
 * @returns {Buffer} ZIP 文件内容
 */
export function buildZip(entries) {
  const now = new Date()
  const { time, date } = toDosDateTime(now)
  const localParts = []
  const centralParts = []
  let offset = 0

  for (const entry of entries) {
    const nameBuffer = Buffer.from(entry.name, 'utf8')
    const data = entry.data
    const crc = crc32(data)

    // 本地文件头
    const local = Buffer.alloc(30 + nameBuffer.length)
    local.writeUInt32LE(0x04034b50, 0)
    local.writeUInt16LE(20, 4)
    // 标志位 0x0800：文件名使用 UTF-8 编码
    local.writeUInt16LE(0x0800, 6)
    local.writeUInt16LE(0, 8)
    local.writeUInt16LE(time, 10)
    local.writeUInt16LE(date, 12)
    local.writeUInt32LE(crc, 14)
    local.writeUInt32LE(data.length, 18)
    local.writeUInt32LE(data.length, 22)
    local.writeUInt16LE(nameBuffer.length, 26)
    local.writeUInt16LE(0, 28)
    nameBuffer.copy(local, 30)

    localParts.push(local, data)

    // 中央目录项
    const central = Buffer.alloc(46 + nameBuffer.length)
    central.writeUInt32LE(0x02014b50, 0)
    central.writeUInt16LE(20, 4)
    central.writeUInt16LE(20, 6)
    central.writeUInt16LE(0x0800, 8)
    central.writeUInt16LE(0, 10)
    central.writeUInt16LE(time, 12)
    central.writeUInt16LE(date, 14)
    central.writeUInt32LE(crc, 16)
    central.writeUInt32LE(data.length, 20)
    central.writeUInt32LE(data.length, 24)
    central.writeUInt16LE(nameBuffer.length, 28)
    central.writeUInt16LE(0, 30)
    central.writeUInt16LE(0, 32)
    central.writeUInt16LE(0, 34)
    central.writeUInt16LE(0, 36)
    central.writeUInt32LE(0, 38)
    central.writeUInt32LE(offset, 42)
    nameBuffer.copy(central, 46)

    centralParts.push(central)
    offset += local.length + data.length
  }

  const centralBuffer = Buffer.concat(centralParts)
  // 结束记录
  const end = Buffer.alloc(22)
  end.writeUInt32LE(0x06054b50, 0)
  end.writeUInt16LE(0, 4)
  end.writeUInt16LE(0, 6)
  end.writeUInt16LE(entries.length, 8)
  end.writeUInt16LE(entries.length, 10)
  end.writeUInt32LE(centralBuffer.length, 12)
  end.writeUInt32LE(offset, 16)
  end.writeUInt16LE(0, 20)

  return Buffer.concat([...localParts, centralBuffer, end])
}

/**
 * 生成游戏的容貌配置（FaceConfig.json 的内容）。
 * 规则与本体文件的 HeadIdRanges 一致：
 *   - 按性别分两组，男性（sexType 0）排在前面：游戏先遍历男性区间
 *     以确定 femaleStartIndex，顺序与本体文件保持一致；
 *   - 同一性别内按 ID 升序，把**连续**的 ID 合并为一段（startId ~ endId）；
 *     遇到断号另起一段 —— 游戏会逐段展开 startId..endId 的每个 ID，
 *     若跨过缺失的 ID 会凭空多出没有图片的空头像；
 *   - name 仅用于人工识别，游戏不参与逻辑判断。
 * @param {number[]} ids 需要写入配置的头像 ID
 * @returns {{HeadIdRanges: Array<{name:string,startId:number,endId:number,sexType:number}>}} 配置对象
 */
export function buildFaceConfig(ids) {
  const sorted = [...new Set((ids || []).map((n) => Number(n)).filter((n) => isCustomFaceId(n)))].sort(
    (a, b) => a - b,
  )
  const headIdRanges = []
  for (const sex of [SEX_MALE, SEX_FEMALE]) {
    const list = sorted.filter((id) => sexOfFaceId(id) === sex)
    const label = sex === SEX_MALE ? '自定义男性' : '自定义女性'
    let start = null
    let prev = null
    /** 结束当前连续段并写入结果 */
    const flush = () => {
      if (start === null || prev === null) return
      headIdRanges.push({
        name: start === prev ? `${label} ${start}` : `${label} ${start}-${prev}`,
        startId: start,
        endId: prev,
        sexType: sex,
      })
    }
    for (const id of list) {
      if (start === null) {
        start = id
        prev = id
        continue
      }
      if (id === prev + 1) {
        prev = id
        continue
      }
      flush()
      start = id
      prev = id
    }
    flush()
  }
  return { HeadIdRanges: headIdRanges }
}

/**
 * 打包下载指定自定义头像。
 * ZIP 内除图片外还会自动附带 **FaceConfig.json**（容貌 ID 区间配置），
 * 由实际打包的头像 ID 推导生成，可直接放进 Mod 的 Data 目录使用。
 * @param {number[]|null} ids 需要打包的头像 ID；为空表示打包全部自定义头像
 * @returns {{buffer:Buffer,count:number,faceConfig:object,ranges:number}|{error:string}} 打包结果
 */
export function exportCustomFaces(ids) {
  const all = [...getCustomIds()].sort((a, b) => a - b)
  let targets = null
  if (Array.isArray(ids) && ids.length > 0) {
    targets = ids.map((n) => Number(n)).filter((n) => all.includes(n))
  } else {
    targets = all
  }
  if (!targets || targets.length === 0) {
    return { error: '没有可打包的自定义头像' }
  }
  const entries = collectFaceFiles(targets)
  if (entries.length === 0) {
    return { error: '没有可打包的头像文件' }
  }
  const count = entries.length

  // 自动附带容貌配置：只统计确实有图片文件的 ID，避免配置里出现空号段
  const configIds = targets.filter(
    (id) => fs.existsSync(faceFilePath(id, 1)) || fs.existsSync(faceFilePath(id, 2)),
  )
  const faceConfig = buildFaceConfig(configIds)
  entries.push({
    name: FACE_CONFIG_NAME,
    data: Buffer.from(`${JSON.stringify(faceConfig, null, 2)}\n`, 'utf8'),
  })

  return { buffer: buildZip(entries), count, faceConfig, ranges: faceConfig.HeadIdRanges.length }
}
