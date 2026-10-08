/**
 * 临时回归脚本：验证「打包下载自制头像时自动附带 FaceConfig.json」。
 * 检查点：
 *   1) ZIP 内包含 FaceConfig.json，且图片文件照旧齐全；
 *   2) 号段按性别分组：男性（sexType 0）在前、女性（sexType 1）在后；
 *   3) 同一性别内「连续 ID 合并为一段、断号另起一段」（不产生空号段）；
 *   4) 单选 / 多选导出时，配置与所选内容一致；
 *   5) 未选到任何有效头像时仍返回 404。
 * 运行于沙箱副本服务（端口 3009），不接触任何真实数据。
 */

let failed = 0

const BASE = 'http://127.0.0.1:3009'

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
 * 解析 store 模式 ZIP（本地文件头顺序读取，无需解压）。
 * @param {Buffer} buffer ZIP 内容
 * @returns {Array<{name:string,data:Buffer}>} 条目列表
 */
function readZipEntries(buffer) {
  const entries = []
  let offset = 0
  while (offset + 30 <= buffer.length && buffer.readUInt32LE(offset) === 0x04034b50) {
    const nameLen = buffer.readUInt16LE(offset + 26)
    const extraLen = buffer.readUInt16LE(offset + 28)
    const size = buffer.readUInt32LE(offset + 18)
    const name = buffer.toString('utf8', offset + 30, offset + 30 + nameLen)
    const dataStart = offset + 30 + nameLen + extraLen
    entries.push({ name, data: buffer.subarray(dataStart, dataStart + size) })
    offset = dataStart + size
  }
  return entries
}

/**
 * 请求导出接口并解析 ZIP。
 * @param {string} query 查询串（如 '?ids=3000'）
 * @returns {Promise<{status:number, entries:Array<{name:string,data:Buffer}>}>} 结果
 */
async function exportZip(query = '') {
  const res = await fetch(`${BASE}/api/faces/custom/export${query}`)
  if (res.status !== 200) return { status: res.status, entries: [] }
  const buffer = Buffer.from(await res.arrayBuffer())
  return { status: res.status, entries: readZipEntries(buffer) }
}

/**
 * 从 ZIP 条目中取出并解析 FaceConfig.json。
 * @param {Array<{name:string,data:Buffer}>} entries ZIP 条目
 * @returns {object|null} 配置对象
 */
function configOf(entries) {
  const entry = entries.find((item) => item.name === 'FaceConfig.json')
  if (!entry) return null
  try {
    return JSON.parse(entry.data.toString('utf8'))
  } catch {
    return null
  }
}

// ── 全部导出 ──
const all = await exportZip()
check('导出接口返回 200', all.status === 200, String(all.status))
const allConfig = configOf(all.entries)
check('ZIP 内含 FaceConfig.json', Boolean(allConfig))
check(
  'ZIP 内图片齐全（6 个 ID × 2 张 + 配置 = 13 项）',
  all.entries.length === 13,
  `${all.entries.length}: ${all.entries.map((e) => e.name).join(',')}`,
)
check(
  '图片条目命名不变',
  ['3000_1.png', '3000_2.png', '5000_1.png', '4000_2.png'].every((n) =>
    all.entries.some((e) => e.name === n),
  ),
)

const ranges = allConfig?.HeadIdRanges ?? []
check(
  '号段数量正确（男 3000-3002 与 5000 两段，女 4000-4001 一段）',
  ranges.length === 3,
  JSON.stringify(ranges),
)
check(
  '男性号段排在女性之前',
  ranges.filter((r) => r.sexType === 0).length === 2 &&
    ranges.filter((r) => r.sexType === 1).length === 1 &&
    ranges.findIndex((r) => r.sexType === 1) === 2,
  JSON.stringify(ranges.map((r) => r.sexType)),
)
check(
  '连续 ID 合并为一段（3000-3002）',
  JSON.stringify(ranges[0]) ===
    JSON.stringify({ name: ranges[0]?.name, startId: 3000, endId: 3002, sexType: 0 }),
  JSON.stringify(ranges[0]),
)
check(
  '断号另起一段（5000 单独一段）',
  ranges[1]?.startId === 5000 && ranges[1]?.endId === 5000 && ranges[1]?.sexType === 0,
  JSON.stringify(ranges[1]),
)
check(
  '女性号段正确（4000-4001）',
  ranges[2]?.startId === 4000 && ranges[2]?.endId === 4001 && ranges[2]?.sexType === 1,
  JSON.stringify(ranges[2]),
)
check(
  '号段名称区分性别',
  ranges[0]?.name?.includes('自定义男性') && ranges[2]?.name?.includes('自定义女性'),
  `${ranges[0]?.name} / ${ranges[2]?.name}`,
)
check(
  '与本体 FaceConfig 字段名一致（name/startId/endId/sexType）',
  ranges.every(
    (r) => Object.keys(r).sort().join(',') === 'endId,name,sexType,startId',
  ),
  JSON.stringify(Object.keys(ranges[0] || {})),
)

// ── 单个头像导出 ──
const single = await exportZip('?ids=4001')
const singleConfig = configOf(single.entries)
check(
  '单个头像导出：包含 2 张图片 + 配置',
  single.entries.length === 3,
  single.entries.map((e) => e.name).join(','),
)
check(
  '单个头像导出：号段为单项且性别正确',
  singleConfig?.HeadIdRanges?.length === 1 &&
    singleConfig.HeadIdRanges[0].startId === 4001 &&
    singleConfig.HeadIdRanges[0].endId === 4001 &&
    singleConfig.HeadIdRanges[0].sexType === 1,
  JSON.stringify(singleConfig?.HeadIdRanges),
)

// ── 多选导出（跨性别 + 断号） ──
const multi = await exportZip('?ids=5000,3001')
const multiConfig = configOf(multi.entries)
check(
  '多选导出：两个男性号段、无女性号段',
  multiConfig?.HeadIdRanges?.length === 2 &&
    multiConfig.HeadIdRanges.every((r) => r.sexType === 0) &&
    multiConfig.HeadIdRanges[0].startId === 3001 &&
    multiConfig.HeadIdRanges[1].startId === 5000,
  JSON.stringify(multiConfig?.HeadIdRanges),
)

// ── 无效 ID ──
const none = await fetch(`${BASE}/api/faces/custom/export?ids=9999`)
check('未选到有效头像时返回 404', none.status === 404, String(none.status))

console.log(failed === 0 ? '\n全部通过' : `\n${failed} 项失败`)
process.exit(failed === 0 ? 0 : 1)
