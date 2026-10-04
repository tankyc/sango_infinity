/**
 * Cloudflare R2 头像存储
 *
 * 头像是全项目最大的一块静态资源（2180 张、约 110 MB），而且**读多写极少**
 * （只有管理员上传自制头像时才写）。把它放 R2 的核心目的是**分发**：
 * R2 的出口流量免费，配合 Cloudflare 的 CDN，比让服务器自己反复吐 110 MB 划算得多。
 *
 * 三个关键取舍：
 *
 * 1. **本地目录仍是权威源，R2 只是分发层。**
 *    R2 未配置或不可用时，服务端继续用本地文件，功能完全不受影响；
 *    上传时双写，本地始终是完整副本。这样 R2 出问题不会让头像功能瘫痪。
 *
 * 2. **读走 302 重定向，不做服务端代理转发。**
 *    代理转发意味着每个头像请求都要经服务器中转，110 MB 的流量等于白饶了 CDN。
 *    302 之后浏览器直连 R2/CDN，服务器一个字节都不用出。
 *
 * 3. **上传时等 R2 写完再响应。**
 *    上传是低频的管理员操作，多等一两秒无所谓；但如果"本地已写、R2 还没写"就响应，
 *    用户刷新页面会被 302 到 R2 上一个还不存在的对象 —— 直接 404。
 *
 * 环境变量（全部可选，不配就退化为纯本地存储）：
 * ```
 * R2_PUBLIC_BASE     = https://face.example.com   # bucket 绑定的自定义域名（读所需）
 * R2_ACCOUNT_ID      = <Cloudflare 账号 ID>        # 以下四项写所需
 * R2_ACCESS_KEY_ID   = <R2 API 令牌的 Access Key>
 * R2_SECRET_ACCESS_KEY = <R2 API 令牌的 Secret>
 * R2_BUCKET          = sango-face
 * R2_FACE_PREFIX     = face                       # 对象前缀，默认 face
 * ```
 */
import { AwsClient } from 'aws4fetch'

/** 读取并裁剪环境变量 */
const readEnv = (key) => String(process.env[key] || '').trim()

/** 公开访问域名（去掉尾部斜杠） */
const PUBLIC_BASE = readEnv('R2_PUBLIC_BASE').replace(/\/+$/, '')
const ACCOUNT_ID = readEnv('R2_ACCOUNT_ID')
const ACCESS_KEY_ID = readEnv('R2_ACCESS_KEY_ID')
const SECRET_ACCESS_KEY = readEnv('R2_SECRET_ACCESS_KEY')
const BUCKET = readEnv('R2_BUCKET')
/** 对象前缀，便于一个 bucket 里放多类资源 */
const PREFIX = (readEnv('R2_FACE_PREFIX') || 'face').replace(/^\/+|\/+$/g, '')

/** 是否能生成公开读取地址 */
const canRead = PUBLIC_BASE !== ''
/** 是否具备写入能力 */
const canWrite = Boolean(ACCOUNT_ID && ACCESS_KEY_ID && SECRET_ACCESS_KEY && BUCKET)

/** 复用一个签名客户端 */
let client = null

/**
 * 取 S3 签名客户端。
 * @returns {AwsClient|null} 未配置写能力时返回 null
 */
function getClient() {
  if (!canWrite) return null
  if (!client) {
    client = new AwsClient({
      accessKeyId: ACCESS_KEY_ID,
      secretAccessKey: SECRET_ACCESS_KEY,
      service: 's3',
      // R2 要求 region 为 auto，与 AWS 的 us-east-1 不同
      region: 'auto',
    })
  }
  return client
}

/**
 * 拼出对象 key。
 * @param {string} fileName 文件名（形如 3000_1.png）
 * @returns {string} 带前缀的对象 key
 */
function objectKey(fileName) {
  return PREFIX ? `${PREFIX}/${fileName}` : fileName
}

/**
 * 拼出对象的公开访问地址。
 * @param {string} fileName 文件名
 * @returns {string} 公开 URL；未配置公开域名时返回空串
 */
export function publicUrl(fileName) {
  if (!canRead) return ''
  return `${PUBLIC_BASE}/${objectKey(fileName)}`
}

/**
 * 拼出 S3 接口地址。
 * @param {string} fileName 文件名
 * @returns {string} S3 URL
 */
function s3Url(fileName) {
  // key 里的 / 必须保留，因此用 encodeURI 而不是 encodeURIComponent
  return `https://${ACCOUNT_ID}.r2.cloudflarestorage.com/${BUCKET}/${encodeURI(objectKey(fileName))}`
}

/**
 * 当前存储状态（供启动日志与自检接口使用）。
 * @returns {object} 状态描述
 */
export function status() {
  return {
    enabled: canRead || canWrite,
    canRead,
    canWrite,
    publicBase: PUBLIC_BASE,
    bucket: BUCKET,
    prefix: PREFIX,
    hint: !canRead
      ? '未配置 R2_PUBLIC_BASE，头像仍由本服务直接提供'
      : canWrite
        ? ''
        : '未配置 R2 写凭证，新上传的头像只写在本地、不会同步到 R2',
  }
}

/**
 * 上传（或覆盖）一个头像文件。
 * @param {string} fileName 文件名
 * @param {Buffer|Uint8Array} body 文件内容
 * @param {string} [contentType] MIME 类型
 * @returns {Promise<{skipped:boolean, url?:string}>} 结果
 */
export async function putFace(fileName, body, contentType = 'image/png') {
  const c = getClient()
  if (!c) return { skipped: true }

  const res = await c.fetch(s3Url(fileName), {
    method: 'PUT',
    body,
    headers: { 'Content-Type': contentType },
  })
  if (!res.ok) {
    const detail = await res.text().catch(() => '')
    throw new Error(`R2 上传失败（HTTP ${res.status}）${detail ? `：${detail.slice(0, 200)}` : ''}`)
  }
  return { skipped: false, url: publicUrl(fileName) }
}

/**
 * 删除一个头像文件。
 * @param {string} fileName 文件名
 * @returns {Promise<{skipped:boolean}>} 结果
 */
export async function deleteFace(fileName) {
  const c = getClient()
  if (!c) return { skipped: true }

  const res = await c.fetch(s3Url(fileName), { method: 'DELETE' })
  // 对象本来就不存在时 S3 也返回 204，无需特殊处理
  if (!res.ok && res.status !== 404) {
    const detail = await res.text().catch(() => '')
    throw new Error(`R2 删除失败（HTTP ${res.status}）${detail ? `：${detail.slice(0, 200)}` : ''}`)
  }
  return { skipped: false }
}

/**
 * 检查一个头像文件是否已存在于 R2（同步自检用）。
 * @param {string} fileName 文件名
 * @returns {Promise<boolean|null>} 存在与否；未配置写能力时返回 null
 */
export async function hasFace(fileName) {
  const c = getClient()
  if (!c) return null
  try {
    const res = await c.fetch(s3Url(fileName), { method: 'HEAD' })
    return res.ok
  } catch {
    return false
  }
}

export const r2Config = { PUBLIC_BASE, ACCOUNT_ID, BUCKET, PREFIX, canRead, canWrite }
