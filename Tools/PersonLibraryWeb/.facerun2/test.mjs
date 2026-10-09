/**
 * 临时回归脚本：自定义头像「上传者筛查 / 图片编辑 / 批量导入 / 拖拽换位 / 改性别 / 空白格」。
 * 运行于沙箱副本（端口 3009），只操作沙箱内的文件，不接触线上数据。
 *
 * 覆盖点：
 *   1) 上传者随上传记录、列表下发、未记录历史数据归入「未记录」；
 *   2) 批量导入：顺序分配 ID、整体回滚（不出现导入一半）；
 *   3) 图片更新：只改指定图片、失败还原、返回缓存版本号；
 *   4) 同源原图接口（供编辑器以半身像为准重裁）；
 *   5) 拖拽换位：仅同性别空白格、腾出位置保留为空白格、武将头像引用同步；
 *   6) 修改男女：按目标性别重新编号并搬移文件；
 *   7) 删除后保留空白占位格，且新头像仍会复用最小空闲 ID。
 */

import fs from 'fs'
import path from 'path'

/** 沙箱服务地址（可用 BASE_URL 覆盖） */
const BASE = process.env.BASE_URL || 'http://127.0.0.1:3009'

/** 沙箱内的头像目录（检查落盘结果用） */
const FACE_DIR = process.env.FACE_DIR || '/root/.facerun2/server/face'

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
 * 服务端只校验 PNG 签名与 IHDR 里的宽高（不解码像素），因此足够用于接口回归。
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
 * @returns {Promise<{status:number,json:any,res:Response}>} 结果
 */
async function api(pathname, options = {}) {
  const { method = 'GET', body, auth = true, raw = false } = options
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (auth && token) headers.Authorization = `Bearer ${token}`
  const res = await fetch(`${BASE}${pathname}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (raw) return { status: res.status, json: null, res }
  const text = await res.text()
  let json = null
  try {
    json = JSON.parse(text)
  } catch {
    json = null
  }
  return { status: res.status, json, res }
}

/** 取头像列表 */
async function list() {
  const res = await api('/api/faces/custom')
  return res.json
}

/** 判断头像文件是否存在 */
function fileExists(id, type) {
  return fs.existsSync(path.join(FACE_DIR, `${id}_${type}.png`))
}

/** 读取头像文件内容（不存在返回 null） */
function readFile(id, type) {
  const file = path.join(FACE_DIR, `${id}_${type}.png`)
  return fs.existsSync(file) ? fs.readFileSync(file) : null
}

// ── 准备初始素材：3000 / 3001（男）、4000（女），其它号段留空 ──
// 每次都清理干净，保证脚本可重复运行
fs.mkdirSync(FACE_DIR, { recursive: true })
for (const name of fs.readdirSync(FACE_DIR)) {
  if (/^\d+_[12]\.png$/i.test(name)) fs.rmSync(path.join(FACE_DIR, name), { force: true })
}
for (const id of [3000, 3001, 4000]) {
  fs.writeFileSync(path.join(FACE_DIR, `${id}_1.png`), fakePng(240, 240))
  fs.writeFileSync(path.join(FACE_DIR, `${id}_2.png`), fakePng(64, 80))
}
fs.rmSync(path.join(path.dirname(FACE_DIR), 'data', 'CustomFaceMeta.json'), { force: true })

// ── 登录 ──
const login = await api('/api/auth/login', {
  method: 'POST',
  auth: false,
  body: { username: 'admin', password: 'admin123' },
})
token = login.json?.token || login.json?.accessToken || ''
check('登录取得令牌', Boolean(token), Object.keys(login.json || {}).join(','))

// ── 1. 列表、上传者字段、空白格基线 ──
const base = await list()
check('列表返回 3 个初始头像', base.items.length === 3, `items=${base.items.map((i) => i.id).join(',')}`)
check(
  '历史数据上传者标记为未记录',
  base.items.every((i) => i.uploader === null) &&
    base.uploaders.some((u) => u.username === null && u.count === 3),
  JSON.stringify(base.uploaders),
)
check('初始无空白占位格', base.blankIds.length === 0, JSON.stringify(base.blankIds))
check(
  '列表项带缓存版本号字段',
  base.items.every((i) => typeof i.version === 'number'),
  JSON.stringify(base.items[0]),
)

// 上传者随上传记录
const created = await api('/api/faces/custom', {
  method: 'POST',
  body: { sex: 0, bust: dataUrl(fakePng(240, 240)), face: dataUrl(fakePng(64, 80)) },
})
check('新建头像成功并分配最小空闲 ID', created.status === 201 && created.json?.id === 3002, JSON.stringify(created.json))
const afterCreate = await list()
const createdItem = afterCreate.items.find((i) => i.id === 3002)
check(
  '新建头像记录了上传者',
  createdItem?.uploader === 'admin' && createdItem?.uploaderName === '管理员',
  JSON.stringify(createdItem),
)
check(
  '上传者清单新增 admin',
  afterCreate.uploaders.some((u) => u.username === 'admin' && u.count === 1),
  JSON.stringify(afterCreate.uploaders),
)
const noAuth = await api('/api/faces/custom', {
  method: 'POST',
  auth: false,
  body: { sex: 0, bust: dataUrl(fakePng(240, 240)), face: dataUrl(fakePng(64, 80)) },
})
check('未登录不能制作头像', noAuth.status === 401, String(noAuth.status))

// ── 2. 批量导入 ──
const batch = await api('/api/faces/custom/batch', {
  method: 'POST',
  body: {
    sex: 0,
    items: [
      { bust: dataUrl(fakePng(240, 240)), face: dataUrl(fakePng(64, 80)) },
      { bust: dataUrl(fakePng(240, 240)), face: dataUrl(fakePng(64, 80)) },
    ],
  },
})
check(
  '批量导入顺序分配 ID',
  batch.status === 201 && batch.json?.ids?.join(',') === '3003,3004',
  JSON.stringify(batch.json),
)
const beforeBad = (await list()).items.length
const badBatch = await api('/api/faces/custom/batch', {
  method: 'POST',
  body: {
    sex: 0,
    items: [
      { bust: dataUrl(fakePng(240, 240)), face: dataUrl(fakePng(64, 80)) },
      { bust: dataUrl(fakePng(100, 100)), face: dataUrl(fakePng(64, 80)) },
    ],
  },
})
check('批量导入含非法图片时报错', badBatch.status === 400, JSON.stringify(badBatch.json))
check('批量导入失败不留脏数据', (await list()).items.length === beforeBad, `${beforeBad} -> ${(await list()).items.length}`)
check(
  '批量导入为空时被拒绝',
  (await api('/api/faces/custom/batch', { method: 'POST', body: { sex: 0, items: [] } })).status === 400,
)

// ── 3. 图片更新（重新导入半身像 / 重裁小头像） ──
const newFacePng = fakePng(64, 80)
newFacePng[39] = 0x7f
const updated = await api('/api/faces/custom/3002', {
  method: 'PUT',
  body: { face: dataUrl(newFacePng) },
})
check(
  '只更新小头像成功并返回版本号',
  updated.status === 200 && updated.json?.updated?.join(',') === 'face' && updated.json?.version > 0,
  JSON.stringify(updated.json),
)
check('小头像文件已替换', readFile(3002, 2)?.equals(newFacePng) === true)
const beforeBust = readFile(3002, 1)
const newBustPng = fakePng(240, 240)
newBustPng[39] = 0x5a
const bustOnly = await api('/api/faces/custom/3002', { method: 'PUT', body: { bust: dataUrl(newBustPng) } })
check('只更新半身像成功', bustOnly.status === 200 && bustOnly.json?.updated?.join(',') === 'bust')
check(
  '半身像文件已替换',
  readFile(3002, 1)?.equals(newBustPng) === true && beforeBust?.equals(newBustPng) === false,
)
const badSize = await api('/api/faces/custom/3002', { method: 'PUT', body: { face: dataUrl(fakePng(32, 40)) } })
check('尺寸不符时拒绝更新', badSize.status === 400, JSON.stringify(badSize.json))
check('拒绝后原小头像未被破坏', readFile(3002, 2)?.equals(newFacePng) === true)

// ── 4. 同源原图接口 ──
check('未登录不能读取原图', (await api('/api/faces/custom/raw?id=3002&type=1', { auth: false })).status === 401)
const rawRes = await api('/api/faces/custom/raw?id=3002&type=1', { raw: true })
check(
  '同源原图接口返回 PNG',
  rawRes.status === 200 && rawRes.res.headers.get('content-type') === 'image/png',
  `${rawRes.status} ${rawRes.res.headers.get('content-type')}`,
)
check('原图接口拒绝非法类型', (await api('/api/faces/custom/raw?id=3002&type=9')).status === 400)

// ── 5. 拖拽换位（与空白格互换） ──
// 先让一位武将引用 3002，验证搬移后引用同步
const lib = await api('/api/persons?lib=custom')
const libKey = (lib.json?.persons || []).length > 0 ? 'custom' : 'base'
const person = ((await api(`/api/persons?lib=${libKey}`)).json?.persons || [])[0]
const refSet = await api(`/api/persons/${person.Id}?lib=${libKey}`, {
  method: 'PUT',
  body: { ...person, headIconID: 3002 },
})
check('准备：某位武将引用头像 3002', refSet.status === 200, `${libKey} ${String(refSet.status)}`)

const moved = await api('/api/faces/custom/3002/move', { method: 'POST', body: { to: 3005 } })
check(
  '移动到空白格成功',
  moved.status === 200 && moved.json?.from === 3002 && moved.json?.to === 3005,
  JSON.stringify(moved.json),
)
check('移动后引用同步更新', moved.json?.remapped === 1, `remapped=${moved.json?.remapped}`)
check('目标文件已生成、源文件已清除', fileExists(3005, 1) && fileExists(3005, 2) && !fileExists(3002, 1))
const afterMove = await list()
check(
  '腾出的位置保留为空白占位格',
  afterMove.blankIds.includes(3002) && !afterMove.items.some((i) => i.id === 3002),
  JSON.stringify(afterMove.blankIds),
)
check(
  '元数据（上传者）随头像一起搬移',
  afterMove.items.find((i) => i.id === 3005)?.uploader === 'admin',
  JSON.stringify(afterMove.items.find((i) => i.id === 3005)),
)
const personAfter = (await api(`/api/persons?lib=${libKey}`)).json.persons.find((p) => p.Id === person.Id)
check('武将 headIconID 已指向新 ID', personAfter?.headIconID === 3005, String(personAfter?.headIconID))
check(
  '移动到已占用 ID 被拒绝',
  (await api('/api/faces/custom/3003/move', { method: 'POST', body: { to: 3004 } })).status === 400,
)
check(
  '跨性别移动被拒绝',
  (await api('/api/faces/custom/3003/move', { method: 'POST', body: { to: 4000 } })).status === 400,
)
check(
  '移动到自定义号段之外被拒绝',
  (await api('/api/faces/custom/3003/move', { method: 'POST', body: { to: 100 } })).status === 400,
)

// ── 6. 修改男女（按目标性别重新编号） ──
const sexChanged = await api('/api/faces/custom/3003/sex', { method: 'POST', body: { sex: 1 } })
check(
  '修改性别后重新分配女性 ID',
  sexChanged.status === 200 && sexChanged.json?.to === 4001 && sexChanged.json?.sex === 1,
  JSON.stringify(sexChanged.json),
)
check('改性别后文件已改名搬移', fileExists(4001, 1) && fileExists(4001, 2) && !fileExists(3003, 1))
const afterSex = await list()
check(
  '改性别后列表性别与空白格正确',
  afterSex.items.find((i) => i.id === 4001)?.sex === 1 && afterSex.blankIds.includes(3003),
  JSON.stringify({ blanks: afterSex.blankIds }),
)
check(
  '改性别为当前性别时被拒绝',
  (await api('/api/faces/custom/4001/sex', { method: 'POST', body: { sex: 1 } })).status === 400,
)

// ── 7. 删除保留空白格，新头像复用最小空闲 ID ──
const deleted = await api('/api/faces/custom/3004', { method: 'DELETE' })
check('删除成功', deleted.status === 200, JSON.stringify(deleted.json))
check('删除后文件已清除', !fileExists(3004, 1) && !fileExists(3004, 2))
const afterDelete = await list()
check(
  '删除后保留空白占位格且不再出现在列表项中',
  afterDelete.blankIds.includes(3004) && !afterDelete.items.some((i) => i.id === 3004),
  JSON.stringify(afterDelete.blankIds),
)
const reuse = await api('/api/faces/custom', {
  method: 'POST',
  body: { sex: 0, bust: dataUrl(fakePng(240, 240)), face: dataUrl(fakePng(64, 80)) },
})
check(
  '新头像复用最小空闲 ID（含被删空白格）',
  [3002, 3003, 3004].includes(reuse.json?.id),
  JSON.stringify(reuse.json),
)
const afterReuse = await list()
check(
  '被复用的空白格从占位格中移除',
  !afterReuse.blankIds.includes(reuse.json?.id) && afterReuse.items.some((i) => i.id === reuse.json?.id),
  JSON.stringify({ id: reuse.json?.id, blanks: afterReuse.blankIds }),
)
check(
  '列表项与空白格不重叠',
  afterReuse.items.every((i) => !afterReuse.blankIds.includes(i.id)),
)

// ── 8. 空白格不会把号段尾部的大片空闲 ID 算进来 ──
check(
  '空白格数量在合理范围（未把整段空闲 ID 计入）',
  afterReuse.blankIds.length <= 5,
  `${afterReuse.blankIds.length}: ${JSON.stringify(afterReuse.blankIds)}`,
)

// ── 9. 打包下载仍自动附带 FaceConfig.json ──
const zipRes = await api('/api/faces/custom/export', { raw: true })
const zipBuffer = Buffer.from(await zipRes.res.arrayBuffer())
const zipNames = []
{
  let offset = 0
  while (offset + 30 <= zipBuffer.length && zipBuffer.readUInt32LE(offset) === 0x04034b50) {
    const nameLen = zipBuffer.readUInt16LE(offset + 26)
    const extraLen = zipBuffer.readUInt16LE(offset + 28)
    const size = zipBuffer.readUInt32LE(offset + 18)
    zipNames.push(zipBuffer.toString('utf8', offset + 30, offset + 30 + nameLen))
    offset += 30 + nameLen + extraLen + size
  }
}
check('打包下载仍包含 FaceConfig.json', zipNames.includes('FaceConfig.json'), zipNames.join(','))

console.log(failed === 0 ? '\n全部通过' : `\n${failed} 项失败`)
process.exit(failed === 0 ? 0 : 1)
