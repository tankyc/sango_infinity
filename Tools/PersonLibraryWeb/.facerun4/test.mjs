/**
 * 临时回归脚本：头像「引用武将信息面板」依赖的接口行为。
 * 运行于沙箱副本（端口 3014），只操作沙箱内的文件，不接触线上数据。
 *
 * 覆盖点：
 *   1) 引用明细带性别字段，可用于面板区分同名男女武将；
 *   2) 明细里每条的 id/name/lib/libLabel 齐备，且与列表的 usedBy 一致；
 *   3) 只被基础库武将引用时，明细只出现基础库记录；
 *   4) 面板查询为只读接口，游客（无令牌）也能查看。
 */

import fs from 'fs'
import path from 'path'

/** 沙箱服务地址 */
const BASE = process.env.BASE_URL || 'http://127.0.0.1:3014'

/** 沙箱内的头像目录 */
const FACE_DIR = process.env.FACE_DIR || '/root/.facerun4/server/face'

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
 * 伪造一张「签名 + IHDR 尺寸」合法的 PNG。
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

// ── 准备初始素材（脚本可重复运行） ──
fs.mkdirSync(FACE_DIR, { recursive: true })
for (const name of fs.readdirSync(FACE_DIR)) {
  if (/^\d+_[12]\.png$/i.test(name)) fs.rmSync(path.join(FACE_DIR, name), { force: true })
}
for (const id of [3000, 3001]) {
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

// ── 准备引用：自建库一位、基础库一位都引用 3000；基础库另一位引用 3001 ──
const customPersons = (await api('/api/persons?lib=custom')).json.persons
const customRef = await setPersonHead(customPersons.length > 0 ? 'custom' : 'base', 0, 3000)
const baseRefA = await setPersonHead('base', 0, 3000)
const baseRefB = await setPersonHead('base', 1, 3001)
check(
  '准备：三位武将分别引用 3000 / 3000 / 3001',
  customRef.status === 200 && baseRefA.status === 200 && baseRefB.status === 200,
)

// ── 1. 明细字段齐备且带性别 ──
const usage3000 = (await api('/api/faces/custom/3000/usage')).json
check(
  '引用明细带性别字段',
  usage3000.persons.every((p) => typeof p.sex === 'number' && p.sex >= -1),
  JSON.stringify(usage3000.persons.map((p) => [p.name, p.sex])),
)
check(
  '引用明细字段齐备（id / name / lib / libLabel）',
  usage3000.persons.every(
    (p) => Number.isInteger(p.id) && typeof p.name === 'string' && p.lib && p.libLabel,
  ),
  JSON.stringify(usage3000.persons),
)
check(
  '引用明细包含准备的三条记录中的两条',
  [customRef, baseRefA].every((ref) =>
    usage3000.persons.some((p) => p.lib === ref.libKey && p.id === ref.id),
  ),
  JSON.stringify(usage3000.persons.map((p) => `${p.lib}:${p.id}`)),
)

// ── 2. 明细数量与列表 usedBy 一致 ──
const list = (await api('/api/faces/custom')).json
const usedBy3000 = list.items.find((item) => item.id === 3000)?.usedBy
check(
  '明细数量与列表 usedBy 一致',
  usedBy3000 === usage3000.count && usage3000.count === (await api('/api/faces/custom/3000/usage')).json.persons.length,
  `usedBy=${usedBy3000} count=${usage3000.count}`,
)

// ── 3. 只被基础库引用时，明细里只出现基础库 ──
const usage3001 = (await api('/api/faces/custom/3001/usage')).json
check(
  '仅基础库引用时明细只含基础库记录',
  usage3001.count >= 1 && usage3001.persons.every((p) => p.lib === 'base'),
  JSON.stringify(usage3001.persons.map((p) => `${p.lib}:${p.id}`)),
)

// ── 4. 只读接口：游客也能查看 ──
const guestUsage = await api('/api/faces/custom/3000/usage', { auth: false })
check(
  '游客可查看引用明细（只读接口）',
  guestUsage.status === 200 && guestUsage.json.count === usage3000.count,
  `${guestUsage.status} count=${guestUsage.json?.count}`,
)
check('非法头像 ID 仍被拒绝', (await api('/api/faces/custom/1/usage', { auth: false })).status === 400)

console.log(failed === 0 ? '\n全部通过' : `\n${failed} 项失败`)
process.exit(failed === 0 ? 0 : 1)
