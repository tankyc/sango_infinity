/**
 * 文件名：auth.js
 * 描述：武将库账号与权限模块。
 *       - 账号以 JSON 文件持久化（server/data/accounts.json）；
 *       - 密码使用 scrypt + 随机盐单向散列存储，文件中不保存明文；
 *       - 登录成功后签发 HMAC-SHA256 签名的无状态令牌，服务重启后依然有效；
 *         令牌仅携带用户名与过期时间，角色 / 权限每次请求都从账号文件实时读取，
 *         因此修改密码或调整角色后旧令牌会立即失效；
 *       - 角色分为 超级管理员(super) / 管理员(admin) / 编辑员(editor) / 游客(guest)：
 *         超级管理员在管理员基础上额外拥有「备份与还原」权限（backup），
 *         且只有超级管理员能授予 / 收回超级管理员角色；
 *         管理员可管理账号；编辑员可增删改武将；游客仅可浏览（读取）；
 *       - 未登录的访客视同游客，同样只读；
 *       - 支持玩家自助注册（POST /api/auth/register）：
 *         默认可用，可通过 REGISTER_ENABLED=0 关闭；
 *         注册后的默认角色由 REGISTER_ROLE 指定（默认 editor 编辑员，不允许为 super）；
 *       - 超级管理员保护：不能删除 / 降级最后一个超级管理员，非超管不能改动超管账号。
 * 创建日期：2026-09-10
 */

import crypto from 'crypto'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

/** 数据目录 */
const DATA_DIR = path.join(__dirname, 'data')

/** 账号数据文件 */
const ACCOUNTS_FILE = path.join(DATA_DIR, 'accounts.json')

/** 令牌签名密钥文件（首次启动自动生成） */
const SECRET_FILE = path.join(DATA_DIR, '.token-secret')

/** 令牌有效期（秒），默认 12 小时 */
const TOKEN_TTL = Number(process.env.TOKEN_TTL) || 12 * 60 * 60

/** 用户名合法格式：2 - 32 位字母、数字、下划线、中划线与点 */
const USERNAME_PATTERN = /^[A-Za-z0-9_\-.@]{2,32}$/

/** 密码最小长度 */
const PASSWORD_MIN_LENGTH = 6

/** 权限标识 */
export const PERMISSIONS = {
  /** 浏览（读取） */
  READ: 'read',
  /** 写入（新增 / 修改 / 删除） */
  WRITE: 'write',
  /** 账号管理 */
  ACCOUNT: 'account',
  /** 备份与还原（超级管理员专属） */
  BACKUP: 'backup',
}

/**
 * 角色定义。
 * permissions 为该角色拥有的权限集合，未列出的权限一律视为没有。
 * 键顺序即前端下拉展示顺序（权限从高到低）。
 */
export const ROLES = {
  super: {
    label: '超级管理员',
    permissions: [PERMISSIONS.READ, PERMISSIONS.WRITE, PERMISSIONS.ACCOUNT, PERMISSIONS.BACKUP],
  },
  admin: {
    label: '管理员',
    permissions: [PERMISSIONS.READ, PERMISSIONS.WRITE, PERMISSIONS.ACCOUNT],
  },
  editor: {
    label: '编辑员',
    permissions: [PERMISSIONS.READ, PERMISSIONS.WRITE],
  },
  guest: {
    label: '游客',
    permissions: [PERMISSIONS.READ],
  },
}

/** 默认角色（未指定或非法角色时使用，权限最小） */
export const DEFAULT_ROLE = 'guest'

/** 超级管理员角色标识 */
export const SUPER_ROLE = 'super'

/** 一个 admin 账号都没有时的兜底超管用户名（可用 SUPER_USERNAME 指定） */
const SUPER_USERNAME_ENV = String(process.env.SUPER_USERNAME || '').trim()

/** 角色选项列表（供前端下拉展示，避免前后端角色不一致） */
export const ROLE_OPTIONS = Object.keys(ROLES).map((key) => ({ id: key, name: ROLES[key].label }))

/**
 * 依据当前身份返回可见的角色选项。
 * 非超级管理员看不到「超级管理员」选项，避免越权尝试。
 * @param {object|null} user 当前用户
 * @returns {{id:string,name:string}[]} 角色选项
 */
export function roleOptionsFor(user) {
  if (user && user.role === SUPER_ROLE) return ROLE_OPTIONS
  return ROLE_OPTIONS.filter((item) => item.id !== SUPER_ROLE)
}

/** 玩家自助注册是否开放（REGISTER_ENABLED=0 可关闭） */
export const REGISTER_ENABLED = String(process.env.REGISTER_ENABLED ?? '1') !== '0'

/**
 * 注册账号的默认角色（REGISTER_ROLE 指定，非法或为 super 时回退为 editor）。
 * 超级管理员只能由超管在「账号管理」中授予，不开放自助获得。
 */
export const REGISTER_ROLE = (() => {
  const raw = String(process.env.REGISTER_ROLE || 'editor').trim()
  if (!isValidRole(raw) || raw === SUPER_ROLE) return 'editor'
  return raw
})()

/** 单个 IP 每小时允许的注册次数（防止批量刷号） */
const REGISTER_LIMIT_PER_HOUR = Number(process.env.REGISTER_LIMIT ?? 5)

/** 首次启动时创建的默认管理员账号 */
const DEFAULT_ADMIN = { username: 'admin', password: 'admin123', role: 'admin', displayName: '管理员' }

/**
 * 判断角色标识是否合法。
 * @param {*} role 角色标识
 * @returns {boolean} 是否合法
 */
export function isValidRole(role) {
  return typeof role === 'string' && Object.prototype.hasOwnProperty.call(ROLES, role)
}

/**
 * 生成随机盐。
 * @returns {string} 十六进制盐值
 */
function createSalt() {
  return crypto.randomBytes(16).toString('hex')
}

/**
 * 使用 scrypt 计算密码散列。
 * @param {string} password 明文密码
 * @param {string} salt 盐值
 * @returns {string} 十六进制散列
 */
function hashPassword(password, salt) {
  return crypto.scryptSync(String(password), salt, 64).toString('hex')
}

/**
 * 恒定时间比较两段字符串，避免时序侧信道。
 * @param {string} a 字符串 A
 * @param {string} b 字符串 B
 * @returns {boolean} 是否相等
 */
function safeEqual(a, b) {
  const bufA = Buffer.from(String(a))
  const bufB = Buffer.from(String(b))
  if (bufA.length !== bufB.length) return false
  return crypto.timingSafeEqual(bufA, bufB)
}

/** 账号缓存（避免每次请求都读盘） */
let accountsCache = null
/** 账号缓存对应的文件修改时间 */
let accountsCacheMtime = 0

/**
 * 确保数据目录存在。
 */
function ensureDataDir() {
  if (!fs.existsSync(DATA_DIR)) fs.mkdirSync(DATA_DIR, { recursive: true })
}

/**
 * 读取令牌签名密钥，文件不存在时自动生成并保存。
 * @returns {string} 密钥（十六进制）
 */
function loadSecret() {
  ensureDataDir()
  if (fs.existsSync(SECRET_FILE)) {
    const text = fs.readFileSync(SECRET_FILE, 'utf8').trim()
    if (text) return text
  }
  const secret = crypto.randomBytes(32).toString('hex')
  fs.writeFileSync(SECRET_FILE, secret, 'utf8')
  return secret
}

/**
 * 规范化单条账号记录，过滤掉密码散列等敏感字段。
 * @param {object} raw 原始账号记录
 * @returns {object|null} 规范化结果（非法记录返回 null）
 */
function normalizeAccount(raw) {
  if (!raw || typeof raw !== 'object') return null
  const username = String(raw.username || '').trim()
  if (!username) return null
  const role = isValidRole(raw.role) ? raw.role : DEFAULT_ROLE
  return {
    username,
    displayName: String(raw.displayName || username).trim() || username,
    role,
    salt: String(raw.salt || ''),
    passwordHash: String(raw.passwordHash || ''),
    /** 统一账号后指向创意工坊的用户 id；空表示还是本站自有的老账号 */
    externalId: String(raw.externalId || ''),
    createdAt: String(raw.createdAt || new Date().toISOString()),
  }
}

/**
 * 读取全部账号（带文件修改时间缓存）。
 * @param {boolean} [force] 是否强制重新读盘
 * @returns {object[]} 账号列表
 */
function readAccounts(force = false) {
  ensureDataDir()
  let mtime = 0
  try {
    mtime = fs.existsSync(ACCOUNTS_FILE) ? fs.statSync(ACCOUNTS_FILE).mtimeMs : 0
  } catch {
    mtime = 0
  }
  if (!force && accountsCache && mtime === accountsCacheMtime) return accountsCache

  let list = []
  try {
    if (fs.existsSync(ACCOUNTS_FILE)) {
      const json = JSON.parse(fs.readFileSync(ACCOUNTS_FILE, 'utf8'))
      const rawList = Array.isArray(json) ? json : Array.isArray(json?.accounts) ? json.accounts : []
      list = rawList.map(normalizeAccount).filter(Boolean)
    }
  } catch (err) {
    console.error('[账号] 读取账号文件失败：', err.message)
  }

  accountsCache = list
  accountsCacheMtime = mtime
  return list
}

/**
 * 将账号列表写回文件（先写临时文件再重命名，避免写入中断损坏数据）。
 * @param {object[]} list 账号列表
 */
function writeAccounts(list) {
  ensureDataDir()
  const tmp = `${ACCOUNTS_FILE}.tmp`
  const content = `${JSON.stringify({ accounts: list }, null, 2)}\n`
  fs.writeFileSync(tmp, content, 'utf8')
  fs.renameSync(tmp, ACCOUNTS_FILE)
  // 写入后刷新缓存，保证后续请求立即读到最新数据
  accountsCache = list
  try {
    accountsCacheMtime = fs.statSync(ACCOUNTS_FILE).mtimeMs
  } catch {
    accountsCacheMtime = 0
  }
}

/**
 * 依据用户名查找账号。
 * @param {string} username 用户名
 * @returns {object|null} 账号记录（含密码散列）
 */
function findAccount(username) {
  const key = String(username || '').trim().toLowerCase()
  if (!key) return null
  return readAccounts().find((a) => a.username.toLowerCase() === key) || null
}

/**
 * 将账号记录转换为对外返回结构（剔除密码散列等敏感字段）。
 * @param {object} account 账号记录
 * @returns {object} 对外账号信息
 */
function toPublicAccount(account) {
  return {
    username: account.username,
    displayName: account.displayName,
    role: account.role,
    roleLabel: ROLES[account.role]?.label || account.role,
    createdAt: account.createdAt,
  }
}

/**
 * 确保系统中存在超级管理员。
 * 规则：没有任何超级管理员时，把「最早创建的管理员」提升为超级管理员
 * （也可用环境变量 SUPER_USERNAME 指定具体账号），避免「备份与还原」无人可用。
 * 已有超级管理员时不做任何改动。
 * @returns {string} 被提升的用户名（无需提升时为空字符串）
 */
function promoteSuperIfNeeded() {
  const list = readAccounts(true)
  if (list.some((a) => a.role === SUPER_ROLE)) return ''
  const admins = list
    .filter((a) => a.role === 'admin')
    .sort((a, b) => String(a.createdAt).localeCompare(String(b.createdAt)))
  const target =
    (SUPER_USERNAME_ENV &&
      admins.find((a) => a.username.toLowerCase() === SUPER_USERNAME_ENV.toLowerCase())) ||
    admins[0]
  if (!target) {
    console.warn(
      '[账号] 未找到任何管理员账号，无法自动提升为超级管理员；请手动编辑 accounts.json 或设置 SUPER_USERNAME 后重启。',
    )
    return ''
  }
  writeAccounts(
    list.map((a) =>
      a.username.toLowerCase() === target.username.toLowerCase() ? { ...a, role: SUPER_ROLE } : a,
    ),
  )
  return target.username
}

/**
 * 初始化账号数据：文件不存在时创建默认管理员账号。
 * 无论是否首次创建，都会确保系统中存在超级管理员（见 promoteSuperIfNeeded）。
 * @returns {{created:boolean, username:string, password:string, promoted:string}} 初始化结果
 */
export function initAccounts() {
  ensureDataDir()
  loadSecret()
  if (fs.existsSync(ACCOUNTS_FILE)) {
    readAccounts(true)
    return { created: false, username: '', password: '', promoted: promoteSuperIfNeeded() }
  }
  const salt = createSalt()
  const account = {
    username: DEFAULT_ADMIN.username,
    displayName: DEFAULT_ADMIN.displayName,
    role: DEFAULT_ADMIN.role,
    salt,
    passwordHash: hashPassword(DEFAULT_ADMIN.password, salt),
    createdAt: new Date().toISOString(),
  }
  writeAccounts([account])
  return {
    created: true,
    username: DEFAULT_ADMIN.username,
    password: DEFAULT_ADMIN.password,
    promoted: promoteSuperIfNeeded(),
  }
}

/**
 * 校验登录凭据。
 * @param {string} username 用户名
 * @param {string} password 明文密码
 * @returns {object|null} 登录成功返回账号记录，失败返回 null
 */
export function verifyLogin(username, password) {
  const account = findAccount(username)
  if (!account || !account.salt || !account.passwordHash) return null
  const hash = hashPassword(password, account.salt)
  return safeEqual(hash, account.passwordHash) ? account : null
}

// ===================================================================
// 远程身份：账号统一到创意工坊（ModHub）
// ===================================================================

/**
 * 统一账号的做法：**登录校验交给创意工坊，本站只保留「档案」**
 * （显示名、角色、权限）。
 *
 * 为什么站内还要留一份档案：本站的角色模型（guest / editor / admin / super）
 * 与创意工坊（user / reviewer / admin）并不一样，而且**写权限必须由本站管理员单独授予**。
 * 外部身份只回答「你是谁」，权限一律以本站为准 —— 这叫「身份统一、权限分散」。
 */

/** 创意工坊地址（同机部署时走回环，不经过公网） */
const REMOTE_AUTH_BASE = String(process.env.MODHUB_BASE_URL || 'http://127.0.0.1:8081').replace(/\/+$/, '')

/** 远程调用超时（毫秒） */
const REMOTE_AUTH_TIMEOUT = Number(process.env.MODHUB_TIMEOUT_MS) || 6000

/** 角色等级，用于「只升不降」的比较 */
const ROLE_RANK = { guest: 0, editor: 1, admin: 2, super: 3 }

/** 取两个角色中权限更高的那个：绑定外部身份只能提升，绝不能降低本站已有权限 */
function higherRole(a, b) {
  return (ROLE_RANK[a] ?? 0) >= (ROLE_RANK[b] ?? 0) ? a : b
}

/**
 * 外部角色 → 本站角色。
 *
 * 只有对方的 admin 映射为本站 admin，其余一律只读 ——
 * 否则创意工坊里任何一个注册用户都能改武将数据。
 * 本站的 editor（可增删改）必须由本站管理员在「账号管理」里单独授予。
 */
function mapRemoteRole(remoteRole) {
  return String(remoteRole || '').toLowerCase() === 'admin' ? 'admin' : DEFAULT_ROLE
}

/**
 * 调用创意工坊的账号接口。
 *
 * 返回值里区分 ok / unreachable，是为了让「密码错」与「账号服务挂了」能给出不同提示：
 * 两者都显示"账号或密码错误"会让人白白怀疑自己记错了密码。
 */
async function callModHub(path, username, password) {
  if (typeof fetch !== 'function') {
    // Node 18 以下没有全局 fetch；这里明确报出来，比抛一个难以理解的错误好
    return { ok: false, status: 0, data: null, unreachable: true, reason: 'node-too-old' }
  }
  try {
    const res = await fetch(`${REMOTE_AUTH_BASE}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ username, password }).toString(),
      signal: AbortSignal.timeout(REMOTE_AUTH_TIMEOUT),
    })
    let data = null
    try {
      data = await res.json()
    } catch {
      /* 非 JSON 响应（例如网关错误页），按无数据继续 */
    }
    return { ok: res.ok, status: res.status, data, unreachable: false }
  } catch {
    return { ok: false, status: 0, data: null, unreachable: true }
  }
}

/** 从创意工坊的响应里取出统一的身份结构 */
function toRemoteIdentity(data) {
  const user = data && typeof data === 'object' ? data.user : null
  if (!user || !user.username) return null
  return {
    id: String(user.id ?? ''),
    username: String(user.username),
    nickname: String(user.nickname || user.username),
    role: String(user.role || 'user'),
  }
}

/**
 * 用创意工坊账号密码登录。
 * @param {string} username 用户名
 * @param {string} password 密码
 * @returns {Promise<{identity:object}|{error:string,status:number}>} 结果
 */
export async function remoteLogin(username, password) {
  const res = await callModHub('/login', username, password)
  if (res.unreachable) {
    return {
      error:
        res.reason === 'node-too-old'
          ? '账号服务需要 Node 18 及以上版本'
          : '账号服务暂时不可用，请稍后再试',
      status: 503,
    }
  }
  if (!res.ok) {
    const message = res.data && res.data.error ? String(res.data.error) : '账号或密码错误'
    return { error: message, status: res.status || 401 }
  }
  const identity = toRemoteIdentity(res.data)
  if (!identity) return { error: '账号服务返回了无法识别的响应', status: 502 }
  return { identity }
}

/** 在创意工坊注册账号（注册成功即视为通过身份校验，可直接登录） */
export async function remoteRegister(username, password) {
  const res = await callModHub('/register', username, password)
  if (res.unreachable) {
    return {
      error:
        res.reason === 'node-too-old'
          ? '账号服务需要 Node 18 及以上版本'
          : '账号服务暂时不可用，请稍后再试',
      status: 503,
    }
  }
  if (!res.ok) {
    const message = res.data && res.data.error ? String(res.data.error) : '注册失败'
    return { error: message, status: res.status || 400 }
  }
  const identity = toRemoteIdentity(res.data)
  if (!identity) return { error: '账号服务返回了无法识别的响应', status: 502 }
  return { identity }
}

/**
 * 把外部身份对应到一个本站账号（没有就建档）。
 *
 * 三种情况：
 *   1. 已绑定过（externalId 命中）→ 直接用，角色完全以本站为准；
 *   2. 首次登录但本站已有同名账号 → 认领它并绑定 externalId，
 *      角色取「较高者」，这样老的编辑员账号不会因为绑定了外部身份而掉权限；
 *   3. 全新的外部身份 → 建档，默认只读（guest），写权限由本站管理员另行授予。
 *
 * @param {{id:string,username:string,nickname:string,role:string}} identity 外部身份
 * @returns {object} 本站账号记录
 */
export function ensureAccountForIdentity(identity) {
  const list = [...readAccounts()]
  const externalId = String(identity.id || '')
  const lowerName = identity.username.toLowerCase()

  // 1. 已绑定过
  if (externalId) {
    const bound = list.find((a) => String(a.externalId || '') === externalId)
    if (bound) return bound
  }

  // 2. 认领同名老账号
  const index = list.findIndex((a) => a.username.toLowerCase() === lowerName)
  if (index >= 0) {
    const claimed = { ...list[index] }
    claimed.externalId = externalId
    claimed.role = higherRole(claimed.role, mapRemoteRole(identity.role))
    list[index] = claimed
    writeAccounts(list)
    return claimed
  }

  // 3. 全新身份：建档，默认只读
  const account = {
    username: identity.username,
    displayName: identity.nickname || identity.username,
    role: mapRemoteRole(identity.role),
    salt: '',
    passwordHash: '',
    externalId,
    createdAt: new Date().toISOString(),
  }
  list.push(account)
  writeAccounts(list)
  return account
}

/** 外部身份服务信息（下发给前端，便于提示账号来源） */
export const REMOTE_AUTH_INFO = {
  enabled: true,
  base: REMOTE_AUTH_BASE,
}

/** Base64URL 编码 */
function base64url(input) {
  return Buffer.from(input)
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

/**
 * 为指定账号签发访问令牌。
 * 载荷仅包含用户名与过期时间，角色以账号文件中的实时数据为准。
 * @param {string} username 用户名
 * @returns {string} 令牌字符串
 */
export function issueToken(username) {
  const payload = base64url(
    JSON.stringify({ u: username, exp: Date.now() + TOKEN_TTL * 1000 }),
  )
  const signature = base64url(crypto.createHmac('sha256', loadSecret()).update(payload).digest())
  return `${payload}.${signature}`
}

/**
 * 校验令牌并解析出用户名。
 * @param {string} token 令牌字符串
 * @returns {string|null} 校验通过返回用户名，否则返回 null
 */
function parseToken(token) {
  if (!token || typeof token !== 'string') return null
  const parts = token.split('.')
  if (parts.length !== 2) return null
  const [payload, signature] = parts
  const expected = base64url(crypto.createHmac('sha256', loadSecret()).update(payload).digest())
  if (!safeEqual(signature, expected)) return null
  try {
    const data = JSON.parse(Buffer.from(payload, 'base64').toString('utf8'))
    if (!data || !data.u) return null
    if (Number(data.exp) && Date.now() > Number(data.exp)) return null
    return String(data.u)
  } catch {
    return null
  }
}

/**
 * 从请求中提取令牌。
 * 支持三种传递方式：Authorization 头（推荐）、x-auth-token 头、query 参数
 * （query 用于「下载 JSON」等无法自定义请求头的浏览器直链场景）。
 * @param {import('express').Request} req 请求对象
 * @returns {string} 令牌（无则返回空字符串）
 */
function extractToken(req) {
  const header = String(req.headers?.authorization || '')
  if (header.toLowerCase().startsWith('bearer ')) return header.slice(7).trim()
  const custom = req.headers?.['x-auth-token']
  if (custom) return String(custom).trim()
  if (req.query && req.query.token) return String(req.query.token).trim()
  return ''
}

/**
 * 解析请求对应的登录用户。
 * @param {import('express').Request} req 请求对象
 * @returns {object|null} 登录用户信息（未登录返回 null，即游客）
 */
export function resolveUser(req) {
  const username = parseToken(extractToken(req))
  if (!username) return null
  const account = findAccount(username)
  if (!account) return null
  return toPublicAccount(account)
}

/**
 * 判断用户是否具备指定权限。
 * @param {object|null} user 用户信息
 * @param {string} permission 权限标识
 * @returns {boolean} 是否具备
 */
export function hasPermission(user, permission) {
  if (!user) return false
  const role = ROLES[user.role]
  if (!role) return false
  return role.permissions.includes(permission)
}

/**
 * 中间件：解析登录用户并挂载到 req.user（未登录为 null）。
 * @param {import('express').Request} req 请求对象
 * @param {import('express').Response} res 响应对象
 * @param {import('express').NextFunction} next 下一个中间件
 */
export function attachUser(req, res, next) {
  req.user = resolveUser(req)
  next()
}

/**
 * 中间件工厂：要求登录并具备指定权限。
 * 未登录返回 401（前端据此弹出登录框），已登录但无权限返回 403。
 * @param {string} permission 权限标识
 * @returns {import('express').RequestHandler} 中间件
 */
export function requirePermission(permission) {
  return (req, res, next) => {
    if (!req.user) {
      return res.status(401).json({ message: '请先登录后再执行该操作' })
    }
    if (!hasPermission(req.user, permission)) {
      return res.status(403).json({
        message: `当前身份为「${ROLES[req.user.role]?.label || req.user.role}」，仅可浏览，无权执行该操作`,
      })
    }
    next()
  }
}

/**
 * 校验新增账号的入参。
 * 只有超级管理员可以创建超级管理员账号。
 * @param {object} body 请求体
 * @param {object|null} operator 操作者（当前登录用户，含 role）
 * @returns {{error:string}|{username:string,password:string,role:string,displayName:string}} 校验结果
 */
export function validateNewAccount(body, operator) {
  const src = body && typeof body === 'object' ? body : {}
  const username = String(src.username || '').trim()
  const password = String(src.password || '')
  const role = isValidRole(src.role) ? src.role : DEFAULT_ROLE
  const displayName = String(src.displayName || username).trim() || username

  if (!USERNAME_PATTERN.test(username)) {
    return { error: '用户名需为 2 - 32 位字母、数字、下划线、中划线或点' }
  }
  if (password.length < PASSWORD_MIN_LENGTH) {
    return { error: `密码长度不能少于 ${PASSWORD_MIN_LENGTH} 位` }
  }
  if (findAccount(username)) {
    return { error: `用户名「${username}」已存在` }
  }
  if (role === SUPER_ROLE && (!operator || operator.role !== SUPER_ROLE)) {
    return { error: '只有超级管理员可以创建超级管理员账号' }
  }
  return { username, password, role, displayName }
}

/** 注册限流记录：IP -> 最近一小时的注册时间戳 */
const registerHits = new Map()

/**
 * 判断某客户端是否超出「每小时注册次数」限制。
 * @param {string} ip 客户端 IP
 * @returns {boolean} 是否允许本次注册
 */
export function allowRegister(ip) {
  const key = String(ip || 'unknown')
  const now = Date.now()
  const hits = (registerHits.get(key) || []).filter((t) => now - t < 60 * 60 * 1000)
  if (REGISTER_LIMIT_PER_HOUR > 0 && hits.length >= REGISTER_LIMIT_PER_HOUR) {
    registerHits.set(key, hits)
    return false
  }
  hits.push(now)
  registerHits.set(key, hits)
  return true
}

/**
 * 玩家自助注册（无需登录）。
 * 角色固定为 REGISTER_ROLE（默认编辑员），不允许自助获得超级管理员。
 * @param {object} body 请求体（username / password / displayName）
 * @returns {{error:string}|{account:object}} 结果
 */
export function registerAccount(body) {
  if (!REGISTER_ENABLED) {
    return { error: '当前未开放注册，请联系管理员开通账号' }
  }
  const src = body && typeof body === 'object' ? body : {}
  const username = String(src.username || '').trim()
  const password = String(src.password || '')
  const displayName = String(src.displayName || '').trim() || username

  if (!USERNAME_PATTERN.test(username)) {
    return { error: '用户名需为 2 - 32 位字母、数字、下划线、中划线或点' }
  }
  if (password.length < PASSWORD_MIN_LENGTH) {
    return { error: `密码长度不能少于 ${PASSWORD_MIN_LENGTH} 位` }
  }
  if (findAccount(username)) {
    return { error: `用户名「${username}」已存在` }
  }
  return { account: createAccount({ username, password, displayName, role: REGISTER_ROLE }) }
}

/** 注册相关的对外信息（供 /api/auth/me 下发给前端） */
export const REGISTER_INFO = {
  enabled: REGISTER_ENABLED,
  role: REGISTER_ROLE,
  roleLabel: ROLES[REGISTER_ROLE]?.label || REGISTER_ROLE,
}

/**
 * 新增账号。
 * @param {{username:string,password:string,role:string,displayName:string}} input 账号信息
 * @returns {object} 新建账号的对外信息
 */
export function createAccount(input) {
  const list = [...readAccounts()]
  const salt = createSalt()
  const account = {
    username: input.username,
    displayName: input.displayName,
    role: input.role,
    salt,
    passwordHash: hashPassword(input.password, salt),
    createdAt: new Date().toISOString(),
  }
  list.push(account)
  writeAccounts(list)
  return toPublicAccount(account)
}

/**
 * 删除账号。
 * 不允许删除自己，也不允许删除最后一个管理员 / 超级管理员；
 * 超级管理员账号只能由超级管理员删除。
 * @param {string} username 目标用户名
 * @param {object|null} operator 操作者（当前登录用户，含 username / role）
 * @returns {{error:string}|{account:object}} 执行结果
 */
export function deleteAccount(username, operator) {
  const account = findAccount(username)
  if (!account) return { error: `未找到账号「${username}」` }
  const operatorName = String(operator?.username || '')
  const isSuper = operator?.role === SUPER_ROLE
  if (account.username.toLowerCase() === operatorName.toLowerCase()) {
    return { error: '不能删除当前登录的账号' }
  }
  const list = readAccounts()
  if (account.role === SUPER_ROLE) {
    if (!isSuper) return { error: '只有超级管理员可以删除超级管理员账号' }
    if (list.filter((a) => a.role === SUPER_ROLE).length <= 1) {
      return { error: '系统至少需要保留一个超级管理员账号' }
    }
  }
  const adminCount = list.filter((a) => a.role === 'admin' || a.role === SUPER_ROLE).length
  if ((account.role === 'admin' || account.role === SUPER_ROLE) && adminCount <= 1) {
    return { error: '系统至少需要保留一个管理员账号' }
  }
  writeAccounts(list.filter((a) => a.username.toLowerCase() !== account.username.toLowerCase()))
  return { account: toPublicAccount(account) }
}

/**
 * 修改账号（角色 / 显示名 / 密码，均为可选）。
 * 保护规则：
 *   - 超级管理员账号只能由超级管理员修改（避免管理员把超管降级或改密）；
 *   - 只有超级管理员可以授予超级管理员角色；
 *   - 不允许把自己降级为「无账号管理权限」的角色，避免自锁；
 *   - 系统至少保留一个超级管理员、一个管理员。
 * @param {string} username 目标用户名
 * @param {object} body 请求体
 * @param {object|null} operator 操作者（当前登录用户，含 username / role）
 * @returns {{error:string}|{account:object}} 执行结果
 */
export function updateAccount(username, body, operator) {
  const src = body && typeof body === 'object' ? body : {}
  const account = findAccount(username)
  if (!account) return { error: `未找到账号「${username}」` }

  const operatorName = String(operator?.username || '')
  const isSuper = operator?.role === SUPER_ROLE
  const isSelf = account.username.toLowerCase() === operatorName.toLowerCase()
  const list = [...readAccounts()]
  const index = list.findIndex((a) => a.username.toLowerCase() === account.username.toLowerCase())
  const target = list[index]

  if (target.role === SUPER_ROLE && !isSuper) {
    return { error: '只有超级管理员可以修改超级管理员账号' }
  }

  if (src.displayName !== undefined) {
    const name = String(src.displayName || '').trim()
    if (!name) return { error: '显示名称不能为空' }
    target.displayName = name
  }

  if (src.role !== undefined) {
    if (!isValidRole(src.role)) return { error: '角色不合法' }
    // 只有超级管理员可以授予超级管理员角色
    if (src.role === SUPER_ROLE && !isSuper) {
      return { error: '只有超级管理员可以授予超级管理员角色' }
    }
    // 不允许把自己降级为「无账号管理权限」的角色，避免自锁
    if (isSelf && !ROLES[src.role].permissions.includes(PERMISSIONS.ACCOUNT)) {
      return { error: '不能降低当前登录账号的权限' }
    }
    if (
      target.role === SUPER_ROLE &&
      src.role !== SUPER_ROLE &&
      list.filter((a) => a.role === SUPER_ROLE).length <= 1
    ) {
      return { error: '系统至少需要保留一个超级管理员账号' }
    }
    const adminCount = list.filter((a) => a.role === 'admin' || a.role === SUPER_ROLE).length
    if (
      (target.role === 'admin' || target.role === SUPER_ROLE) &&
      src.role !== 'admin' &&
      src.role !== SUPER_ROLE &&
      adminCount <= 1
    ) {
      return { error: '系统至少需要保留一个管理员账号' }
    }
    target.role = src.role
  }

  if (src.password !== undefined && String(src.password) !== '') {
    const password = String(src.password)
    if (password.length < PASSWORD_MIN_LENGTH) {
      return { error: `密码长度不能少于 ${PASSWORD_MIN_LENGTH} 位` }
    }
    target.salt = createSalt()
    target.passwordHash = hashPassword(password, target.salt)
  }

  list[index] = target
  writeAccounts(list)
  return { account: toPublicAccount(target) }
}

/**
 * 获取全部账号（对外结构，按创建时间排序）。
 * @returns {object[]} 账号列表
 */
export function listAccounts() {
  return [...readAccounts()]
    .sort((a, b) => a.createdAt.localeCompare(b.createdAt))
    .map(toPublicAccount)
}

export { toPublicAccount }
