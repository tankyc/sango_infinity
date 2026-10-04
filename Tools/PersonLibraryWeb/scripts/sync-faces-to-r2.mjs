/**
 * 把本地头像批量同步到 Cloudflare R2
 *
 * 本地 `server/face/` 是权威源（2180 张、约 110 MB），R2 只是分发层。
 * 第一次配置好 R2 之后需要跑一次全量同步；之后新增的自制头像由服务端自动双写，
 * 不需要再跑。
 *
 * 用法：
 *   node scripts/sync-faces-to-r2.mjs                 # 全量同步（已存在则跳过）
 *   node scripts/sync-faces-to-r2.mjs --force         # 覆盖已存在的对象
 *   node scripts/sync-faces-to-r2.mjs --only-custom   # 只同步自制头像（ID >= 3000）
 *   node scripts/sync-faces-to-r2.mjs --dry-run       # 只统计，不上传
 *
 * 需要先设置环境变量（与 server/r2.js 一致）：
 *   R2_ACCOUNT_ID / R2_ACCESS_KEY_ID / R2_SECRET_ACCESS_KEY / R2_BUCKET
 *   R2_PUBLIC_BASE（可选，仅用于打印最终可访问的地址）
 */
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'
import { AwsClient } from 'aws4fetch'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

/** 头像源目录 */
const FACE_DIR = path.join(__dirname, '..', 'server', 'face')

/** 自定义头像起始 ID（与 faceStore.js 保持一致） */
const CUSTOM_FACE_BASE_ID = 3000

/** 并发上传数：太高容易触发 R2 限流，8 是比较稳的值 */
const CONCURRENCY = 8

const readEnv = (key) => String(process.env[key] || '').trim()

const ACCOUNT_ID = readEnv('R2_ACCOUNT_ID')
const ACCESS_KEY_ID = readEnv('R2_ACCESS_KEY_ID')
const SECRET_ACCESS_KEY = readEnv('R2_SECRET_ACCESS_KEY')
const BUCKET = readEnv('R2_BUCKET')
const PUBLIC_BASE = readEnv('R2_PUBLIC_BASE').replace(/\/+$/, '')
const PREFIX = (readEnv('R2_FACE_PREFIX') || 'face').replace(/^\/+|\/+$/g, '')

const args = new Set(process.argv.slice(2))
const FORCE = args.has('--force')
const ONLY_CUSTOM = args.has('--only-custom')
const DRY_RUN = args.has('--dry-run')

/**
 * 打印用法并退出。
 * @param {number} code 退出码
 */
function bail(code = 1) {
  if (!ACCOUNT_ID || !ACCESS_KEY_ID || !SECRET_ACCESS_KEY || !BUCKET) {
    console.error('缺少 R2 配置。请先设置以下环境变量：')
    console.error('  R2_ACCOUNT_ID / R2_ACCESS_KEY_ID / R2_SECRET_ACCESS_KEY / R2_BUCKET')
    console.error('（R2_PUBLIC_BASE 可选，仅用于打印访问地址）')
  }
  process.exit(code)
}

/**
 * 把耗时格式化成可读文本。
 * @param {number} ms 毫秒
 * @returns {string} 展示文本
 */
function formatDuration(ms) {
  if (ms < 1000) return `${Math.round(ms)}ms`
  const s = ms / 1000
  if (s < 60) return `${s.toFixed(1)}s`
  return `${Math.floor(s / 60)}m${Math.round(s % 60)}s`
}

/**
 * 把字节数格式化成可读文本。
 * @param {number} bytes 字节
 * @returns {string} 展示文本
 */
function formatSize(bytes) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

async function main() {
  if (!fs.existsSync(FACE_DIR)) {
    console.error(`头像目录不存在：${FACE_DIR}`)
    process.exit(1)
  }

  // 演练模式只统计本地文件规模，不需要凭证，方便先看要传多少
  if (!DRY_RUN && (!ACCOUNT_ID || !ACCESS_KEY_ID || !SECRET_ACCESS_KEY || !BUCKET)) bail(1)

  const client = new AwsClient({
    accessKeyId: ACCESS_KEY_ID,
    secretAccessKey: SECRET_ACCESS_KEY,
    service: 's3',
    // R2 要求 region 为 auto
    region: 'auto',
  })

  const urlOf = (name) => {
    const key = PREFIX ? `${PREFIX}/${name}` : name
    return `https://${ACCOUNT_ID}.r2.cloudflarestorage.com/${BUCKET}/${encodeURI(key)}`
  }

  // 收集待同步文件
  const all = fs
    .readdirSync(FACE_DIR)
    .filter((f) => f.toLowerCase().endsWith('.png'))
    .sort()

  const files = ONLY_CUSTOM ? all.filter((f) => Number(f.split('_')[0]) >= CUSTOM_FACE_BASE_ID) : all

  const totalBytes = files.reduce((sum, f) => sum + fs.statSync(path.join(FACE_DIR, f)).size, 0)

  console.log('=== 头像同步到 R2 ===')
  console.log(`  源目录   ${FACE_DIR}`)
  console.log(`  目标     r2://${BUCKET || '(未配置)'}/${PREFIX}/`)
  console.log(`  待处理   ${files.length} 个文件，共 ${formatSize(totalBytes)}`)
  console.log(`  模式     ${FORCE ? '强制覆盖' : '跳过已存在'}${ONLY_CUSTOM ? ' / 仅自制头像' : ''}${DRY_RUN ? ' / 演练' : ''}`)
  console.log('')

  if (DRY_RUN) {
    console.log('  演练模式：不做任何上传。')
    return
  }

  const started = Date.now()
  let uploaded = 0
  let skipped = 0
  let failed = 0
  const errors = []
  let done = 0

  /**
   * 同步单个文件。
   * @param {string} name 文件名
   */
  async function syncOne(name) {
    const file = path.join(FACE_DIR, name)
    try {
      // 已存在就跳过，避免重复上传 110 MB（--force 时跳过此检查）
      if (!FORCE) {
        const head = await client.fetch(urlOf(name), { method: 'HEAD' })
        if (head.ok) {
          skipped += 1
          return
        }
      }
      const body = fs.readFileSync(file)
      const res = await client.fetch(urlOf(name), {
        method: 'PUT',
        body,
        headers: { 'Content-Type': 'image/png' },
      })
      if (!res.ok) {
        const detail = await res.text().catch(() => '')
        throw new Error(`HTTP ${res.status} ${detail.slice(0, 120)}`)
      }
      uploaded += 1
    } catch (err) {
      failed += 1
      errors.push(`${name}: ${err.message}`)
    } finally {
      done += 1
      if (done % 100 === 0 || done === files.length) {
        const pct = ((done / files.length) * 100).toFixed(1)
        process.stdout.write(
          `\r  进度 ${pct}%  (${done}/${files.length})  上传 ${uploaded}  跳过 ${skipped}  失败 ${failed}`
        )
      }
    }
  }

  // 固定并发的任务池
  let cursor = 0
  async function worker() {
    while (cursor < files.length) {
      const name = files[cursor]
      cursor += 1
      await syncOne(name)
    }
  }
  await Promise.all(Array.from({ length: Math.min(CONCURRENCY, files.length) }, worker))

  console.log('\n')
  console.log('=== 完成 ===')
  console.log(`  上传 ${uploaded}  跳过 ${skipped}  失败 ${failed}`)
  console.log(`  耗时 ${formatDuration(Date.now() - started)}`)

  if (PUBLIC_BASE) {
    console.log(`  访问示例 ${PUBLIC_BASE}/${PREFIX ? PREFIX + '/' : ''}2_1.png`)
  }

  if (errors.length > 0) {
    console.log(`\n  失败明细（最多列 20 条）：`)
    for (const line of errors.slice(0, 20)) console.log(`    ${line}`)
    if (errors.length > 20) console.log(`    …另有 ${errors.length - 20} 条`)
    process.exit(1)
  }
}

main().catch((err) => {
  console.error(`同步失败：${err.message}`)
  process.exit(1)
})
