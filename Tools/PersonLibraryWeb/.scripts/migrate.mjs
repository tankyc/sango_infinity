/**
 * 线上自建库迁移脚本：把 CustomPerson.json 由旧结构升级为新结构。
 * 流程：迁移前体检 → 备份 → 通过服务端下载接口取「新结构序列化内容」
 *      → 逐条校验（条数 / ID / 姓名 / 字段不丢失 / 数组形态 / 基础值不变）
 *      → 校验全部通过才替换数据文件 → 复核输出。
 * 任一校验失败即中止，数据文件保持不变。
 * 用法：node migrate.mjs <数据文件路径> <备份文件路径>
 */

import fs from 'fs'

const DATA_FILE = process.argv[2] || '/root/deploy_20260910192453/server/data/CustomPerson.json'
const BACKUP_FILE = process.argv[3] || `/root/customperson_backup_${Date.now()}.json`
const API = 'http://127.0.0.1:3001/api/download?lib=custom'
const TMP_FILE = '/tmp/CustomPerson_migrated.json'

let failed = 0

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

/** 取容器中的武将键（排除 offset / dataVersion 等元数据） */
const personsOf = (lib) =>
  Object.keys(lib).filter((k) => lib[k] && typeof lib[k] === 'object' && !Array.isArray(lib[k]))

/** 取能力基础值（兼容数组与旧标量） */
const baseOf = (value) => {
  const n = Number(Array.isArray(value) ? value[0] : value)
  return Number.isFinite(n) ? Math.trunc(n) : null
}

// ── 迁移前体检 ──
const before = JSON.parse(fs.readFileSync(DATA_FILE, 'utf8')).PersonLibrary || {}
const beforeIds = personsOf(before)
const beforeArrays = beforeIds.filter((id) => Array.isArray(before[id].command)).length
const beforeLegacy = beforeIds.filter((id) => 'yearAvailable' in before[id]).length
console.log('== 迁移前 ==')
console.log(`条数: ${beforeIds.length} | offset: ${before.offset} | dataVersion: ${before.dataVersion}`)
console.log(`五维为数组: ${beforeArrays} | 仍含 yearAvailable: ${beforeLegacy}`)

// ── 备份 ──
fs.copyFileSync(DATA_FILE, BACKUP_FILE)
check('已备份原文件', fs.existsSync(BACKUP_FILE), BACKUP_FILE)

// ── 取新结构内容（走服务端自身的序列化，保证与写库结果一致） ──
const res = await fetch(API)
check('下载接口返回 200', res.status === 200, String(res.status))
const text = await res.text()
fs.writeFileSync(TMP_FILE, text)
const after = JSON.parse(text).PersonLibrary || {}
const afterIds = personsOf(after)

// ── 校验 ──
check('条数一致', beforeIds.length === afterIds.length, `${beforeIds.length} -> ${afterIds.length}`)
check('容器带 dataVersion = 2', after.dataVersion === 2, String(after.dataVersion))
check('offset 不变', after.offset === before.offset, `${before.offset} -> ${after.offset}`)

const idDiff = beforeIds.filter((id) => !afterIds.includes(id))
check('武将 ID 全部保留', idDiff.length === 0, idDiff.slice(0, 5).join(','))

const nameDiff = beforeIds.filter(
  (id) => String(before[id].Name ?? '') !== String(after[id]?.Name ?? ''),
)
check(
  '姓名全部一致',
  nameDiff.length === 0,
  nameDiff
    .slice(0, 5)
    .map((id) => `${id}:${before[id].Name}->${after[id]?.Name}`)
    .join(','),
)

const lost = []
for (const id of beforeIds) {
  for (const key of Object.keys(before[id])) {
    if (key === 'yearAvailable') continue // 已迁移为 appearance
    if (!(key in after[id])) lost.push(`${id}.${key}`)
  }
}
check('字段无丢失（yearAvailable 除外）', lost.length === 0, lost.slice(0, 5).join(','))

const afterArrays = afterIds.filter((id) => Array.isArray(after[id].command)).length
const afterLegacy = afterIds.filter((id) => 'yearAvailable' in after[id]).length
check('五维全部为 5 元组数组', afterArrays === afterIds.length, `${afterArrays}/${afterIds.length}`)
check('旧键 yearAvailable 已清除', afterLegacy === 0, String(afterLegacy))
check(
  '适性全部为 3 元组数组',
  afterIds.every((id) => Array.isArray(after[id].spearLv) && after[id].spearLv.length === 3),
)

const baseDiff = beforeIds.filter((id) => {
  if (!('command' in before[id])) return false
  const b = baseOf(before[id].command)
  const a = baseOf(after[id]?.command)
  return b !== null && b !== a
})
check(
  '能力基础值未被改动',
  baseDiff.length === 0,
  baseDiff
    .slice(0, 5)
    .map((id) => `${id}:${baseOf(before[id].command)}->${baseOf(after[id]?.command)}`)
    .join(','),
)

if (failed > 0) {
  console.log(`\n${failed} 项校验失败：已中止，数据文件未修改（备份位于 ${BACKUP_FILE}）`)
  process.exit(1)
}

// ── 替换数据文件 ──
fs.copyFileSync(TMP_FILE, DATA_FILE)
const final = JSON.parse(fs.readFileSync(DATA_FILE, 'utf8')).PersonLibrary || {}
const finalIds = personsOf(final)
const sample = final[finalIds[0]]
console.log('\n== 迁移后 ==')
console.log(`条数: ${finalIds.length} | offset: ${final.offset} | dataVersion: ${final.dataVersion}`)
console.log(`样例: ${sample?.Name} | command=${JSON.stringify(sample?.command)} | appearance=${sample?.appearance}`)
console.log(`备份: ${BACKUP_FILE}`)
console.log('\n迁移完成，全部校验通过')
