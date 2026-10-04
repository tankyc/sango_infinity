/**
 * 认证模块（复用武将库网站的账号体系）
 *
 * 武将库网站（Tools/PersonLibraryWeb）用的是「HMAC 签名的自包含令牌」：
 *
 *   token = base64url(JSON{u, exp}) + '.' + base64url(HMAC-SHA256(secret, payload))
 *
 * 密钥在网站的 `data/.token-secret`，账号（含角色）在 `data/accounts.json`。
 * 本模块直接读这两个文件独立校验，**不依赖武将库网站是否在线**，
 * 也不需要任何跨服务调用——两个工具天然共用一套身份，
 * 管理员在武将库网站登录后，把令牌带过来即可，不用二次登录。
 *
 * 注意令牌载荷里只有用户名与过期时间，**没有角色**：
 * 角色一律以 accounts.json 的实时数据为准，所以改了角色不需要重新登录。
 */
const fs = require('fs');
const crypto = require('crypto');
const { LIBRARY_SECRET_FILE, LIBRARY_ACCOUNTS_FILE } = require('./paths');

/** 角色等级：数值越大权限越高 */
const ROLE_RANK = { guest: 0, editor: 1, admin: 2, super: 3 };

/** 角色中文名 */
const ROLE_LABEL = { guest: '游客', editor: '编辑员', admin: '管理员', super: '超级管理员' };

/** 具备「上传模板 / 上传基准版」权限的角色 */
const ADMIN_ROLES = new Set(['admin', 'super']);

/** 文件缓存（按 mtime 失效），避免每个请求都读盘 */
const cache = {
  secret: { value: null, mtime: -1 },
  accounts: { value: [], mtime: -1 },
};

/** Base64URL 编码（与武将库网站保持一致） */
function base64url(input) {
  return Buffer.from(input)
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

/** 定长比较，避免时序侧信道 */
function safeEqual(a, b) {
  const bufA = Buffer.from(String(a));
  const bufB = Buffer.from(String(b));
  if (bufA.length !== bufB.length) return false;
  return crypto.timingSafeEqual(bufA, bufB);
}

/**
 * 读取令牌密钥。
 *
 * @returns {string|null} 密钥；文件缺失时返回 null（表示认证不可用）
 */
function readSecret() {
  try {
    const stat = fs.statSync(LIBRARY_SECRET_FILE);
    if (cache.secret.value !== null && cache.secret.mtime === stat.mtimeMs) return cache.secret.value;
    const value = fs.readFileSync(LIBRARY_SECRET_FILE, 'utf8').trim();
    cache.secret = { value: value || null, mtime: stat.mtimeMs };
    return cache.secret.value;
  } catch {
    cache.secret = { value: null, mtime: -1 };
    return null;
  }
}

/**
 * 读取账号列表（按文件 mtime 缓存）。
 *
 * @returns {Array<object>} 账号数组
 */
function readAccounts() {
  try {
    const stat = fs.statSync(LIBRARY_ACCOUNTS_FILE);
    if (cache.accounts.mtime === stat.mtimeMs) return cache.accounts.value;
    const parsed = JSON.parse(fs.readFileSync(LIBRARY_ACCOUNTS_FILE, 'utf8'));
    const list = parsed && Array.isArray(parsed.accounts) ? parsed.accounts : [];
    cache.accounts = { value: list, mtime: stat.mtimeMs };
    return list;
  } catch {
    cache.accounts = { value: [], mtime: -1 };
    return [];
  }
}

/**
 * 认证是否可用（密钥文件存在）。
 *
 * @returns {boolean} 可用性
 */
function isAuthAvailable() {
  return readSecret() !== null;
}

/**
 * 校验令牌并解出用户名。
 *
 * @param {string} token 令牌
 * @returns {string|null} 用户名；无效返回 null
 */
function parseToken(token) {
  const secret = readSecret();
  if (!secret || typeof token !== 'string' || !token) return null;

  const parts = token.split('.');
  if (parts.length !== 2) return null;
  const [payload, signature] = parts;

  const expected = base64url(crypto.createHmac('sha256', secret).update(payload).digest());
  if (!safeEqual(signature, expected)) return null;

  try {
    const data = JSON.parse(Buffer.from(payload, 'base64').toString('utf8'));
    if (!data || !data.u) return null;
    if (Number(data.exp) && Date.now() > Number(data.exp)) return null;
    return String(data.u);
  } catch {
    return null;
  }
}

/**
 * 从请求中提取令牌。
 *
 * 支持三种方式，与武将库网站一致：
 * `Authorization: Bearer`、`x-auth-token` 头、`?token=` 查询参数
 * （查询参数是给「下载」这类浏览器直链场景用的，无法自定义请求头）。
 *
 * @param {import('express').Request} req 请求对象
 * @returns {string} 令牌（无则空串）
 */
function extractToken(req) {
  const header = String((req.headers && req.headers.authorization) || '');
  if (header.toLowerCase().startsWith('bearer ')) return header.slice(7).trim();
  const custom = req.headers && req.headers['x-auth-token'];
  if (custom) return String(custom).trim();
  if (req.query && req.query.token) return String(req.query.token).trim();
  return '';
}

/**
 * 按用户名查账号。
 *
 * @param {string} username 用户名
 * @returns {object|null} 账号记录
 */
function findAccount(username) {
  const target = String(username || '').toLowerCase();
  if (!target) return null;
  return readAccounts().find((a) => String(a.username || '').toLowerCase() === target) || null;
}

/**
 * 解析请求对应的登录用户。
 *
 * @param {import('express').Request} req 请求对象
 * @returns {{username:string,displayName:string,role:string,roleLabel:string,isAdmin:boolean}|null}
 */
function resolveUser(req) {
  const username = parseToken(extractToken(req));
  if (!username) return null;
  const account = findAccount(username);
  if (!account) return null;

  const rawRole = String(account.role || 'guest');
  const role = Object.prototype.hasOwnProperty.call(ROLE_RANK, rawRole) ? rawRole : 'guest';
  return {
    username: String(account.username),
    displayName: String(account.displayName || account.username),
    role,
    roleLabel: ROLE_LABEL[role] || role,
    isAdmin: ADMIN_ROLES.has(role),
  };
}

/**
 * 认证中间件：把用户挂到 req.user 上，供后续路由使用。
 *
 * 校验失败不直接拒绝——是否允许匿名取决于具体路由，
 * 例如浏览剧本对游客开放，但保存 / 上传要求登录。
 *
 * @param {import('express').Request} req 请求对象
 * @param {import('express').Response} _res 响应对象
 * @param {import('express').NextFunction} next 下一个中间件
 */
function attachUser(req, _res, next) {
  req.user = resolveUser(req);
  next();
}

module.exports = {
  attachUser,
  resolveUser,
  parseToken,
  isAuthAvailable,
  findAccount,
  readAccounts,
  ROLE_RANK,
  ROLE_LABEL,
  ADMIN_ROLES,
};
