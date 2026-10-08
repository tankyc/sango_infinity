/**
 * 基础武将库容器标记补齐脚本。
 * 背景：早前版本的「一键导入」会丢掉容器级元数据，导致 PersonLibrary.json
 *      缺少新版结构标记 dataVersion，游戏与工具都会按旧结构兼容读取并给出告警。
 * 本脚本只做一件事：在确认数据本身已是新结构（五维 5 元组、无 yearAvailable）后，
 * 往容器里补上 "dataVersion": 2，并逐条校验武将数据完全未变。
 * 用法：node fix-base-version.mjs <personLibrary.json> [备份路径]
 */

import fs from 'fs'

const FILE = process.argv[2]
const BACKUP = process.argv[3] || `${FILE}.bak`

if (!FILE || !fs.existsSync(FILE)) {
  console.error('用法：node fix-base-version.mjs <personLibrary.json> [备份路径]')
  process.exit(1)
}

const lib = JSON.parse(fs.readFileSync(FILE, 'utf8')).PersonLibrary || {}
const persons = {}
for (const [key, value] of Object.entries(lib)) {
  if (value && typeof value === 'object' && !Array.isArray(value)) persons[key] = value
}
const ids = Object.keys(persons)
// 新结构的判据：五维为数组（游戏对少于 5 项的精简数组也有兜底，故不强制长度）
const withCommand = ids.filter((id) => 'command' in persons[id])
const arrays = withCommand.filter((id) => Array.isArray(persons[id].command)).length
const full = withCommand.filter((id) => (persons[id].command || []).length === 5).length
const legacy = ids.filter((id) => 'yearAvailable' in persons[id]).length
console.log(
  `条数: ${ids.length} | 含能力字段: ${withCommand.length} | 五维为数组: ${arrays}（其中 5 元组 ${full}）| ` +
    `仍含 yearAvailable: ${legacy} | 当前 dataVersion: ${lib.dataVersion}`,
)

if (ids.length === 0) {
  console.log('❌ 未解析到武将数据，已中止')
  process.exit(1)
}
if (arrays !== withCommand.length || withCommand.length === 0) {
  console.log('❌ 并非全部为新结构（五维应为数组），已中止')
  process.exit(1)
}
if (legacy !== 0) {
  console.log('❌ 仍存在旧键 yearAvailable，已中止')
  process.exit(1)
}
if (lib.dataVersion === 2) {
  console.log('✅ 已带 dataVersion = 2，无需修改')
  process.exit(0)
}

fs.copyFileSync(FILE, BACKUP)
// 标记写在容器最前面，武将条目的字段与顺序完全不变
const next = { dataVersion: 2, ...lib }
fs.writeFileSync(FILE, `${JSON.stringify({ PersonLibrary: next }, null, 2)}\n`)

const after = JSON.parse(fs.readFileSync(FILE, 'utf8')).PersonLibrary
const keysAfter = Object.keys(after)
const samePersons = ids.every((id) => JSON.stringify(persons[id]) === JSON.stringify(after[id]))
const ok = samePersons && keysAfter.length === ids.length + 1 && after.dataVersion === 2
console.log(ok ? '✅ 校验通过：仅新增 dataVersion，武将数据逐条一致' : '❌ 校验失败，请用备份还原')
console.log(`备份: ${BACKUP}`)
console.log(`标记: dataVersion = ${after.dataVersion}`)
