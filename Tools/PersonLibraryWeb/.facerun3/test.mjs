/**
 * 临时回归脚本：头像「武将引用检查 + 提示 + 同步处理」。
 * 运行于沙箱副本（端口 3012），只操作沙箱内的文件，不接触线上数据。
 *
 * 覆盖点（断言全部基于基线增量，避免沙箱里既有数据干扰）：
 *   1) 列表下发 usedBy（被多少位武将引用），跨基础库与自建库统计；
 *   2) 引用明细接口返回武将姓名与所属库；
 *   3) 删除时 mode=keep 保留引用（返回 dangling 数量）；
 *   4) 删除时 mode=replace 把引用同步改到指定头像（返回 replaced 数量）；
 *   5) replace 缺少 replaceId 时先校验后执行，不会删掉头像；
 *   6) 移动 / 改性别仍然自动同步引用（回归上一轮能力）。
 */

import fs from 'fs'
import path from 'path'

/** 沙箱服务地址（可用 BASE_URL 覆盖） */
const BASE = process.env.BASE_URL || 'http://127.0.0.1:3012'

/** 沙箱内的头像目录 */
const FACE_DIR = process.env.FACE_DIR || '/root/.facerun3/server/face'

let failed = 0
/** 登录令牌 */
let token = ''

/**
 * 断言输出。
 * @param {string} name 用例名
 * @param {boolean} ok 是否通过
 * @param {string} extra 附加信息
 */
function check(name, ok, extra = '') {
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${extra ? '  ' + extra : ''}`)
  if (!ok) failed++
}

/**
 * 伪造一张「签名 + IHDR 尺寸」合法的 PNG（服务端只校验签名与尺寸）。
 * @param {number} width 宽
 * @param {number} height 高
 * @returns {Buffer} PNG 内容
 */
function fakePng(width, height) {
  const buf = Buffer.alloc(41)
  Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]).copy(buf, 0)
  buf.writeUInt32BE(13, 8)
  buf.write('IHDR', 12, 'ascii')
  buf.writeUInt32BE(width, 16)
  buf.writeUInt32BE(height, 20)
  buf[24] = 8
  buf[25] = 6
  buf.writeUInt32BE(0, 29)
  buf.write('IEND', 33, 'ascii')
  return buf
}

/**
 * 转成接口需要的 dataURL。
 * @param {Buffer} buffer 图片内容
 * @returns {string} dataURL
 */
function dataUrl(buffer) {
  return `data:image/png;base64,${buffer.toString('base64')}`
}

/**
 * 调用接口。
 * @param {string} pathname 接口路径
 * @param {object} [options] 选项
 * @returns {Promise<{status:number,json:any}>} 结果
 */
async function api(pathname, options = {}) {
  const { method = 'GET', body, auth = true } = options
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (auth && token) headers.Authorization = `Bearer ${token}`
  const res = await fetch(`${BASE}${pathname}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await res.text()
  let json = null
  try {
    json = JSON.parse(text)
  } catch {
    json = null
  }
  return { status: res.status, json }
}

/** 取头像列表 */
async function list() {
  return (await api('/api/faces/custom')).json
}

/**
 * 查某个头像的 usedBy。
 * @param {number} id 头像 ID
 * @returns {Promise<number>} 引用数量
 */
async function usedByOf(id) {
  return (await list()).items.find((item) => item.id === id)?.usedBy ?? -1
}

/**
 * 把某位武将的头像字段改成指定头像 ID。
 * @param {string} libKey 库标识
 * @param {number} index 武将下标
 * @param {number} headId 头像 ID
 * @returns {Promise<{libKey:string,id:number,name:string,status:number}>} 结果
 */
async function setPersonHead(libKey, index, headId) {
  const persons = (await api(`/api/persons?lib=${libKey}`)).json.persons
  const person = persons[index]
  const res = await api(`/api/persons/${person.Id}?lib=${libKey}`, {
    method: 'PUT',
    body: { ...person, headIconID: headId },
  })
  return { libKey, id: person.Id, name: person.Name, status: res.status }
}

/**
 * 读取某位武将当前的头像 ID。
 * @param {string} libKey 库标识
 * @param {number} personId 武将 ID
 * @returns {Promise<number>} 头像 ID
 */
async function getPersonHead(libKey, personId) {
  const persons = (await api(`/api/persons?lib=${libKey}`)).json.persons
  return Number((persons.find((p) => p.Id === personId) || {}).headIconID)
}

/** 创建一张新头像，返回分配到的 ID */
async function createFace() {
  const res = await api('/api/faces/custom', {
    method: 'POST',
    body: { sex: 0, bust: dataUrl(fakePng(240, 240)), face: dataUrl(fakePng(64, 80)) },
  })
  return res.json?.id
}

// ── 准备初始素材（脚本可重复运行） ──
fs.mkdirSync(FACE_DIR, { recursive: true })
for (const name of fs.readdirSync(FACE_DIR)) {
  if (/^\d+_[12]\.png$/i.test(name)) fs.rmSync(path.join(FACE_DIR, name), { force: true })
}
for (const id of [3000, 3001, 4000]) {
  fs.writeFileSync(path.join(FACE_DIR, `${id}_1.png`), fakePng(240, 240))
  fs.writeFileSync(path.join(FACE_DIR, `${id}_2.png`), fakePng(64, 80))
}
fs.rmSync(path.join(path.dirname(FACE_DIR), 'data', 'CustomFaceMeta.json'), { force: true })

const login = await api('/api/auth/login', {
  method: 'POST',
  auth: false,
  body: { username: 'admin', password: 'admin123' },
})
token = login.json?.token || login.json?.accessToken || ''
check('登录取得令牌', Boolean(token))

// 基线：沙箱里既有武将可能已经在用某些头像，后续断言一律看增量
const baselineList = await list()
const baseline = (id) => baselineList.items.find((item) => item.id === id)?.usedBy ?? 0

// ── 1. 准备：两位武将（一个自建库、一个基础库）引用 3000 ──
const customPersons = (await api('/api/persons?lib=custom')).json.persons
const useBase = customPersons.length < 2
const first = await setPersonHead(useBase ? 'base' : 'custom', 0, 3000)
const second = await setPersonHead('base', 1, 3000)
check('准备：两位武将引用头像 3000', first.status === 200 && second.status === 200, `${first.libKey}/${second.libKey}`)

// ── 2. 列表下发 usedBy（跨库统计） ──
check(
  '列表下发 usedBy 且随引用增加（跨基础库与自建库）',
  (await usedByOf(3000)) === baseline(3000) + 2 && (await usedByOf(3001)) === baseline(3001),
  `3000: ${baseline(3000)} -> ${await usedByOf(3000)}`,
)

// ── 3. 引用明细接口 ──
const usage3000 = await api('/api/faces/custom/3000/usage')
const usageIds = (usage3000.json?.persons || []).map((p) => `${p.lib}:${p.id}`)
check(
  '引用明细返回数量与武将信息',
  usage3000.status === 200 &&
    usage3000.json.count === baseline(3000) + 2 &&
    usage3000.json.persons.length === usage3000.json.count &&
    usage3000.json.persons.every((p) => p.name && p.libLabel && Number.isInteger(p.id)),
  JSON.stringify(usage3000.json),
)
check(
  '引用明细包含本次设置的两条记录，并覆盖两个库',
  usageIds.includes(`${first.libKey}:${first.id}`) &&
    usageIds.includes(`base:${second.id}`) &&
    new Set(usage3000.json.persons.map((p) => p.lib)).size === 2,
  JSON.stringify(usageIds),
)
const freshFace = await createFace()
check('未被引用的头像 usedBy 为 0 且明细为空', (await usedByOf(freshFace)) === 0)
check('非法头像 ID 的引用查询被拒绝', (await api('/api/faces/custom/100/usage')).status === 400)

// ── 4. 删除：保留引用（keep） ──
const keepDelete = await api('/api/faces/custom/3000', { method: 'DELETE', body: { mode: 'keep' } })
check(
  '删除时 keep 模式返回未处理的引用数',
  keepDelete.status === 200 && keepDelete.json?.dangling === baseline(3000) + 2,
  JSON.stringify(keepDelete.json),
)
check(
  'keep 模式不改动武将头像字段',
  (await getPersonHead(first.libKey, first.id)) === 3000 && (await getPersonHead('base', second.id)) === 3000,
)
check('删除后该头像成为空白占位格', (await list()).blankIds.includes(3000))

// ── 5. 删除：同步替换（replace） ──
const refForReplace = await setPersonHead(useBase ? 'base' : 'custom', 0, freshFace)
check('准备：武将引用待删除头像', refForReplace.status === 200, String(refForReplace.status))
const before4000 = await usedByOf(4000)
const replaceDelete = await api(`/api/faces/custom/${freshFace}`, {
  method: 'DELETE',
  body: { mode: 'replace', replaceId: 4000 },
})
check(
  '删除时 replace 模式同步改引用并返回数量',
  replaceDelete.status === 200 && replaceDelete.json?.replaced === 1 && replaceDelete.json?.replaceId === 4000,
  JSON.stringify(replaceDelete.json),
)
check(
  'replace 后武将头像已改为替换 ID',
  (await getPersonHead(refForReplace.libKey, refForReplace.id)) === 4000,
  String(await getPersonHead(refForReplace.libKey, refForReplace.id)),
)
check('替换后新头像的 usedBy 已增加', (await usedByOf(4000)) === before4000 + 1, `${before4000} -> ${await usedByOf(4000)}`)

// ── 6. replace 缺少 replaceId：先校验后执行 ──
const missingIdFace = await createFace()
const missingIdDelete = await api(`/api/faces/custom/${missingIdFace}`, {
  method: 'DELETE',
  body: { mode: 'replace' },
})
check('replace 缺少 replaceId 被拒绝', missingIdDelete.status === 400, JSON.stringify(missingIdDelete.json))
check(
  '校验失败时头像未被删除',
  (await list()).items.some((item) => item.id === missingIdFace),
  String(missingIdFace),
)

// ── 7. 移动 / 改性别仍自动同步引用（回归） ──
const refForMove = await setPersonHead(useBase ? 'base' : 'custom', 1, missingIdFace)
check('准备：武将引用待移动头像', refForMove.status === 200, String(refForMove.status))
// 注意：remapped 可能大于 1 —— 前面 keep 模式遗留的引用也会在这次移动里被同步
const moved = await api(`/api/faces/custom/${missingIdFace}/move`, { method: 'POST', body: { to: 3002 } })
check(
  '移动时同步引用并返回数量',
  moved.status === 200 && moved.json?.remapped >= 1 && moved.json?.to === 3002,
  JSON.stringify(moved.json),
)
check(
  '移动后武将头像指向新 ID',
  (await getPersonHead(refForMove.libKey, refForMove.id)) === 3002,
  String(await getPersonHead(refForMove.libKey, refForMove.id)),
)
const sexChange = await api('/api/faces/custom/3002/sex', { method: 'POST', body: { sex: 1 } })
check(
  '改性别时同步引用并返回数量',
  sexChange.status === 200 && sexChange.json?.remapped >= 1 && sexChange.json?.to > 4000,
  JSON.stringify(sexChange.json),
)
check(
  '改性别后武将头像指向新 ID',
  (await getPersonHead(refForMove.libKey, refForMove.id)) === sexChange.json?.to,
  String(await getPersonHead(refForMove.libKey, refForMove.id)),
)

console.log(failed === 0 ? '\n全部通过' : `\n${failed} 项失败`)
process.exit(failed === 0 ? 0 : 1)
