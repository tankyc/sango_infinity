/**
 * 临时回归脚本：验证「自建武将库适配新版数据结构」。
 * 检查点：
 *   1) 读取旧结构数据（标量能力 / yearAvailable / 空数组）时按新结构归一化：
 *      五维 → [基础值, 成长类型Id, 经验, 万分比, 最终值]、适性 → [等级, 经验, 等级]、
 *      yearAvailable → appearance；
 *   2) 新结构的扩展字段（body / wordTac / birthplace / injury 等）与「数组形态、顺序」
 *      原样保留，不再被白名单模板丢弃；
 *   3) 已有数组中的「经验 / 万分比」不被重置；
 *   4) 任何一次写入都会把整个文件重写为新结构（容器带 dataVersion:2）；
 *   5) 新建武将同样产出新结构；偏移导出依然可用。
 * 运行于沙箱副本服务（端口 3009），不接触任何真实数据。
 */

import fs from 'fs'

const BASE = 'http://127.0.0.1:3009'
const CUSTOM_FILE = new URL('./server/data/CustomPerson.json', import.meta.url)

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

/** 读取沙箱数据文件中的自建库容器 */
const readFileLib = () => JSON.parse(fs.readFileSync(CUSTOM_FILE, 'utf8')).PersonLibrary || {}

// ── 准备：旧结构 + 新结构扩展字段的混合数据 ──
fs.writeFileSync(
  CUSTOM_FILE,
  `${JSON.stringify(
    {
      PersonLibrary: {
        offset: 1000,
        1000: {
          Id: 1000,
          Name: '旧甲',
          type: 3,
          familyName: '旧',
          giveName: '甲',
          yearAvailable: 205,
          command: 70,
          spearLv: 2,
          body: [72, 72, -1, 72],
          wordTac: [1, 2, 3],
          birthplace: 12,
          injury: 0,
          skeleton: 3,
          SpouseList: [1001, 1000],
          LikePersonList: [],
          FeatureList: [3],
          tags: '魏,核心',
          headIconID: 2000,
          imageID: null,
          updatedAt: '2026-09-01T00:00:00.000Z',
        },
        1001: { Id: 1001, Name: '旧乙', type: 3, command: [60, 7, 5, 9500, 60], crossbowLv: [1, 4, 1] },
      },
    },
    null,
    2,
  )}\n`,
)

const login = await (
  await fetch(`${BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: 'admin', password: 'admin123' }),
  })
).json()
check('管理员登录', Boolean(login.token))
const H = { 'Content-Type': 'application/json', Authorization: `Bearer ${login.token}` }

// ── A. 读取路径：旧结构 → 新结构 ──
const read = await (await fetch(`${BASE}/api/persons?lib=custom`, { headers: H })).json()
const p0 = read.persons.find((p) => p.Id === 1000)
const p1 = read.persons.find((p) => p.Id === 1001)

check('读取：登场年迁移为 appearance', p0?.appearance === 205, String(p0?.appearance))
check('读取：旧键 yearAvailable 不再存在', !('yearAvailable' in (p0 ?? {})), Object.keys(p0 ?? {}).join(','))
check(
  '读取：五维归一化为 5 元组',
  JSON.stringify(p0?.command) === '[70,5,0,10000,70]',
  JSON.stringify(p0?.command),
)
check(
  '读取：适性归一化为 3 元组',
  JSON.stringify(p0?.spearLv) === '[2,0,2]',
  JSON.stringify(p0?.spearLv),
)
check('读取：扩展字段 body 原样保留（含 -1）', JSON.stringify(p0?.body) === '[72,72,-1,72]', JSON.stringify(p0?.body))
check('读取：扩展字段 wordTac 保留', JSON.stringify(p0?.wordTac) === '[1,2,3]', JSON.stringify(p0?.wordTac))
check(
  '读取：扩展标量字段保留（birthplace / skeleton）',
  p0?.birthplace === 12 && p0?.skeleton === 3,
  `${p0?.birthplace} / ${p0?.skeleton}`,
)
check(
  '读取：Id 列表保持原顺序与空数组',
  JSON.stringify(p0?.SpouseList) === '[1001,1000]' && JSON.stringify(p0?.LikePersonList) === '[]',
  `${JSON.stringify(p0?.SpouseList)} / ${JSON.stringify(p0?.LikePersonList)}`,
)
check('读取：标签字符串被拆分为数组', JSON.stringify(p0?.tags) === '["魏","核心"]', JSON.stringify(p0?.tags))
check(
  '读取：已有数组的经验 / 万分比不被重置',
  JSON.stringify(p1?.command) === '[60,7,5,9500,60]' &&
    JSON.stringify(p1?.crossbowLv) === '[1,4,1]',
  `${JSON.stringify(p1?.command)} / ${JSON.stringify(p1?.crossbowLv)}`,
)

// ── B. 写入路径：整库重写为新结构 ──
await fetch(`${BASE}/api/persons/1000?lib=custom`, {
  method: 'PUT',
  headers: H,
  body: JSON.stringify(p0),
})
const file = readFileLib()
const f0 = file['1000']
const f1 = file['1001']

check('文件：写入容器标记 dataVersion = 2', file.dataVersion === 2, String(file.dataVersion))
check('文件：offset 保持 1000', file.offset === 1000, String(file.offset))
check('文件：五维为 5 元组', JSON.stringify(f0?.command) === '[70,5,0,10000,70]', JSON.stringify(f0?.command))
check('文件：适性为 3 元组', JSON.stringify(f0?.spearLv) === '[2,0,2]', JSON.stringify(f0?.spearLv))
check('文件：登场年为 appearance 且旧键已移除', f0?.appearance === 205 && !('yearAvailable' in f0), `${f0?.appearance} / ${'yearAvailable' in f0}`)
check('文件：body 原样保留', JSON.stringify(f0?.body) === '[72,72,-1,72]', JSON.stringify(f0?.body))
check('文件：wordTac / birthplace / skeleton 保留', JSON.stringify(f0?.wordTac) === '[1,2,3]' && f0?.birthplace === 12 && f0?.skeleton === 3)
check('文件：Id 列表顺序与空数组保留', JSON.stringify(f0?.SpouseList) === '[1001,1000]' && JSON.stringify(f0?.LikePersonList) === '[]')
check('文件：标签为数组', JSON.stringify(f0?.tags) === '["魏","核心"]', JSON.stringify(f0?.tags))
check(
  '文件：姓名保留、修改时间被刷新为保存时刻',
  f0?.Name === '旧甲' &&
    /^\d{4}-\d{2}-\d{2}T/.test(String(f0?.updatedAt)) &&
    f0.updatedAt !== '2026-09-01T00:00:00.000Z',
  `${f0?.Name} / ${f0?.updatedAt}`,
)
check(
  '文件：未编辑的武将同样升级为新结构且经验保留',
  JSON.stringify(f1?.command) === '[60,7,5,9500,60]' && JSON.stringify(f1?.crossbowLv) === '[1,4,1]',
  `${JSON.stringify(f1?.command)} / ${JSON.stringify(f1?.crossbowLv)}`,
)

// ── C. 新建武将 ──
const created = await (
  await fetch(`${BASE}/api/persons?lib=custom`, {
    method: 'POST',
    headers: H,
    body: JSON.stringify({ familyName: '新', giveName: '丙', sex: 0 }),
  })
).json()
const newPerson = created.person
check('新建：返回 5 元组能力', JSON.stringify(newPerson?.command) === '[50,5,0,10000,50]', JSON.stringify(newPerson?.command))
check('新建：返回 3 元组适性', JSON.stringify(newPerson?.spearLv) === '[0,0,0]', JSON.stringify(newPerson?.spearLv))
check('新建：appearance 有默认值', newPerson?.appearance === 190, String(newPerson?.appearance))
const fileAfterCreate = readFileLib()
check(
  '新建后：整库仍为新结构且老武将扩展字段未丢',
  fileAfterCreate.dataVersion === 2 && JSON.stringify(fileAfterCreate['1000']?.body) === '[72,72,-1,72]',
  String(fileAfterCreate.dataVersion),
)

// ── D. 偏移导出依旧可用（新结构下平移 ID） ──
const shifted = (await (await fetch(`${BASE}/api/download?lib=custom&start=10000`)).json()).PersonLibrary
check('偏移导出：offset 改为 10000', shifted.offset === 10000, String(shifted.offset))
check('偏移导出：容器仍带 dataVersion', shifted.dataVersion === 2, String(shifted.dataVersion))
check(
  '偏移导出：ID 平移到 10000+ 且能力仍为数组',
  JSON.stringify(shifted['10000']?.command) === '[70,5,0,10000,70]',
  JSON.stringify(shifted['10000']?.command),
)
check(
  '偏移导出：关系字段同步平移',
  JSON.stringify(shifted['10000']?.SpouseList) === '[10001,10000]',
  JSON.stringify(shifted['10000']?.SpouseList),
)

console.log(failed === 0 ? '\n全部通过' : `\n${failed} 项失败`)
process.exit(failed === 0 ? 0 : 1)
