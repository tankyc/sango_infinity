/**
 * 文件名：api.ts
 * 描述：武将库后端接口封装。
 *       - 所有武将相关接口均带 lib 参数，用于区分基础武将库与自建武将库；
 *       - 登录令牌保存在浏览器本地存储中，除登录接口外的所有请求都会自动携带；
 *       - 后端对写操作做权限校验：游客（未登录）访问会返回 401，
 *         身份不足会返回 403，前端据此提示登录或提示无权限。
 */

import type {
  AccountListResult,
  AuthUser,
  BackupEntry,
  BackupListResult,
  BackupStatus,
  BatchDeleteResult,
  BulkImportResult,
  ClearResult,
  CurrentUserResult,
  CustomFaceResult,
  FaceUsageResult,
  ImportResult,
  LibraryKey,
  LibraryResponse,
  LoginResult,
  OptionsConfig,
  Person,
  RestoreResult,
  Role,
} from './types'
import { API_PREFIX } from './siteLinks'

/** 登录令牌在本地存储中的键名 */
const TOKEN_KEY = 'sango.personlib.token'

/**
 * 带 HTTP 状态码的接口异常。
 * 便于上层区分「未登录（401）」与「无权限（403）」并给出不同提示。
 */
export class ApiError extends Error {
  /** HTTP 状态码 */
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/** 读取本地保存的登录令牌 */
export function getToken(): string {
  try {
    return localStorage.getItem(TOKEN_KEY) || ''
  } catch {
    return ''
  }
}

/**
 * 保存登录令牌。传入空值表示清除（退出登录）。
 * @param token 令牌
 */
export function setToken(token: string | null): void {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token)
    else localStorage.removeItem(TOKEN_KEY)
  } catch {
    // 本地存储不可用时忽略（例如隐私模式），此时保持游客状态
  }
}

/**
 * 组装请求头：写入令牌（若已登录）并合并调用方自定义头。
 * @param extra 额外的请求头
 * @returns 请求头对象
 */
function headers(extra?: Record<string, string>): Record<string, string> {
  const result: Record<string, string> = { ...(extra || {}) }
  const token = getToken()
  if (token) result.Authorization = `Bearer ${token}`
  return result
}

/**
 * 统一处理接口响应，非 2xx 时抛出带后端提示信息的异常。
 * @param res fetch 响应
 * @returns 解析后的 JSON
 */
async function handle<T>(res: Response): Promise<T> {
  const text = await res.text()
  let data: unknown = null
  if (text) {
    try {
      data = JSON.parse(text)
    } catch {
      data = null
    }
  }
  if (!res.ok) {
    const message = (data as { message?: string } | null)?.message
    throw new ApiError(res.status, message || `请求失败（${res.status}）`)
  }
  return data as T
}

/**
 * 带站点前缀的 fetch。
 *
 * 所有接口请求都经过这里，而不是把前缀散落到几十个调用点上：
 * 子路径部署（/personlib/）时前缀由 VITE_API_PREFIX 注入，本地开发为空串。
 * 内部刻意调用 window.fetch —— 这个函数自身也叫 fetch 的包装，
 * 用裸 fetch 会自己调自己。
 * @param pathname 以 / 开头的接口路径
 * @param init fetch 配置
 * @returns fetch 响应
 */
function apiFetch(pathname: string, init?: RequestInit): Promise<Response> {
  return window.fetch(`${API_PREFIX}${pathname}`, init)
}

/**
 * 拼接带库标识的接口地址。
 * @param pathname 接口路径
 * @param lib 库标识
 * @returns 完整地址
 */
function withLib(pathname: string, lib: LibraryKey): string {
  const sep = pathname.includes('?') ? '&' : '?'
  return `${pathname}${sep}lib=${encodeURIComponent(lib)}`
}

// ─────────────────────────────────────────────────────────────
// 账号与权限
// ─────────────────────────────────────────────────────────────

/**
 * 登录。
 * @param username 用户名
 * @param password 密码
 * @returns 登录结果（令牌与用户信息）
 */
export function login(username: string, password: string): Promise<LoginResult> {
  return apiFetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  }).then((r) => handle<LoginResult>(r))
}

/**
 * 玩家自助注册（无需登录）。
 * 注册成功即返回令牌，可直接作为登录状态使用。
 * @param payload 注册信息（用户名 / 密码 / 显示名可选）
 * @returns 令牌与用户信息
 */
export function register(payload: {
  username: string
  password: string
  displayName?: string
}): Promise<LoginResult> {
  return apiFetch('/api/auth/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  }).then((r) => handle<LoginResult>(r))
}

/**
 * 退出登录（通知服务端并清除本地令牌）。
 */
export async function logout(): Promise<void> {
  try {
    await apiFetch('/api/auth/logout', { method: 'POST', headers: headers() })
  } catch {
    // 退出登录失败不阻塞前端清除本地状态
  }
  setToken(null)
}

/**
 * 获取当前登录身份与可选角色列表。
 * 未登录时 user 为 null，即游客。
 * @returns 当前身份信息
 */
export function fetchCurrentUser(): Promise<CurrentUserResult> {
  return apiFetch('/api/auth/me', { headers: headers() }).then((r) => handle<CurrentUserResult>(r))
}

/**
 * 获取账号列表（仅管理员）。
 * @returns 账号列表与角色选项
 */
export function fetchAccounts(): Promise<AccountListResult> {
  return apiFetch('/api/accounts', { headers: headers() }).then((r) => handle<AccountListResult>(r))
}

/**
 * 新建账号（仅管理员）。
 * @param payload 账号信息
 * @returns 新建的账号
 */
export function createAccount(payload: {
  username: string
  password: string
  role: Role
  displayName: string
}): Promise<{ account: AuthUser }> {
  return apiFetch('/api/accounts', {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(payload),
  }).then((r) => handle<{ account: AuthUser }>(r))
}

/**
 * 修改账号（仅管理员，可改显示名 / 角色 / 密码）。
 * @param username 目标用户名
 * @param payload 需要修改的字段
 * @returns 修改后的账号
 */
export function updateAccount(
  username: string,
  payload: { displayName?: string; role?: Role; password?: string },
): Promise<{ account: AuthUser }> {
  return apiFetch(`/api/accounts/${encodeURIComponent(username)}`, {
    method: 'PUT',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(payload),
  }).then((r) => handle<{ account: AuthUser }>(r))
}

/**
 * 删除账号（仅管理员）。
 * @param username 目标用户名
 * @returns 被删除的账号
 */
export function deleteAccount(username: string): Promise<{ account: AuthUser }> {
  return apiFetch(`/api/accounts/${encodeURIComponent(username)}`, {
    method: 'DELETE',
    headers: headers(),
  }).then((r) => handle<{ account: AuthUser }>(r))
}

// ─────────────────────────────────────────────────────────────
// 数据备份与还原（超级管理员）
// ─────────────────────────────────────────────────────────────

/**
 * 获取备份列表与自动备份调度状态（仅超级管理员）。
 * @returns 备份列表与状态
 */
export function fetchBackups(): Promise<BackupListResult> {
  return apiFetch('/api/backups', { headers: headers() }).then((r) => handle<BackupListResult>(r))
}

/**
 * 立即执行一次手动备份（仅超级管理员）。
 * @returns 备份元信息与最新状态
 */
export function createBackup(): Promise<{ backup: BackupEntry; status: BackupStatus }> {
  return apiFetch('/api/backups', { method: 'POST', headers: headers() }).then((r) =>
    handle<{ backup: BackupEntry; status: BackupStatus }>(r),
  )
}

/**
 * 还原指定备份（仅超级管理员）。
 * 服务端会先自动创建一份「还原前快照」，并需要二次确认。
 * @param id 备份标识
 * @returns 还原结果（含还原前快照标识）
 */
export function restoreBackup(id: string): Promise<RestoreResult> {
  return apiFetch(`/api/backups/${encodeURIComponent(id)}/restore`, {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify({ confirm: true }),
  }).then((r) => handle<RestoreResult>(r))
}

// ─────────────────────────────────────────────────────────────
// 武将库
// ─────────────────────────────────────────────────────────────

/**
 * 获取指定武将库的全部数据。
 * @param lib 库标识
 * @returns 库数据
 */
export function fetchLibrary(lib: LibraryKey): Promise<LibraryResponse> {
  return apiFetch(withLib('/api/persons', lib), { headers: headers() }).then((r) =>
    handle<LibraryResponse>(r),
  )
}

/** 获取编辑选项配置 */
export function fetchOptions(): Promise<OptionsConfig> {
  return apiFetch('/api/options', { headers: headers() }).then((r) => handle<OptionsConfig>(r))
}

/** 获取内置武将名称索引（Id -> 姓名），用于人际关系显示与候选 */
export function fetchReferenceNames(): Promise<Record<string, string>> {
  return apiFetch('/api/reference', { headers: headers() }).then((r) =>
    handle<Record<string, string>>(r),
  )
}

/**
 * 新建武将（仅自建武将库可用，需写入权限）。
 * @param person 武将数据
 * @param lib 库标识
 * @returns 新建结果
 */
export function createPerson(
  person: Partial<Person>,
  lib: LibraryKey,
): Promise<{ person: Person; nextId: number }> {
  return apiFetch(withLib('/api/persons', lib), {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(person),
  }).then((r) => handle<{ person: Person; nextId: number }>(r))
}

/**
 * 修改武将（ID 固定不可变更，需写入权限）。
 * @param id 武将 ID
 * @param person 武将数据
 * @param lib 库标识
 * @returns 修改结果
 */
export function updatePerson(
  id: number,
  person: Partial<Person>,
  lib: LibraryKey,
): Promise<{ person: Person }> {
  return apiFetch(withLib(`/api/persons/${id}`, lib), {
    method: 'PUT',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(person),
  }).then((r) => handle<{ person: Person }>(r))
}

/**
 * 删除武将（仅自建武将库可用，需写入权限）。
 * @param id 武将 ID
 * @param lib 库标识
 * @returns 删除结果
 */
export function deletePerson(id: number, lib: LibraryKey): Promise<{ person: Person }> {
  return apiFetch(withLib(`/api/persons/${id}`, lib), {
    method: 'DELETE',
    headers: headers(),
  }).then((r) => handle<{ person: Person }>(r))
}

/**
 * 批量删除武将（仅自建武将库可用，需写入权限）。
 * 不存在的 ID 会被服务端忽略；删除后其 ID 不会被重新分配给新武将。
 * @param ids 武将 ID 列表
 * @param lib 库标识
 * @returns 删除结果（实际删除数量与删除后的库信息）
 */
export function deletePersons(ids: number[], lib: LibraryKey): Promise<BatchDeleteResult> {
  return apiFetch(withLib('/api/persons/batch-delete', lib), {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify({ ids }),
  }).then((r) => handle<BatchDeleteResult>(r))
}

/**
 * 清空指定武将库的全部武将（需写入权限）。
 * 服务端要求二次确认，调用前请先用「下载」导出备份。
 * @param lib 库标识
 * @returns 清空结果
 */
export function clearPersons(lib: LibraryKey): Promise<ClearResult> {
  return apiFetch(withLib('/api/persons/clear', lib), {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify({ confirm: true }),
  }).then((r) => handle<ClearResult>(r))
}

/**
 * 整体导入武将（基础库专用）。
 * 保留文件中的武将 ID：merge 模式按 ID 更新 / 新增，replace 模式先清空整库再写入。
 * @param lib 库标识
 * @param payload 文件原始文本（content）或已解析对象（data），以及导入方式
 * @returns 导入结果
 */
export function bulkImportPersons(
  lib: LibraryKey,
  payload: { data?: unknown; content?: string; mode: 'merge' | 'replace' },
): Promise<BulkImportResult> {
  return apiFetch(withLib('/api/persons/bulk-import', lib), {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(payload),
  }).then((r) => handle<BulkImportResult>(r))
}

/**
 * 下载武将库 JSON 文件。
 * 浏览器直链无法自定义请求头，因此令牌通过 query 参数传递。
 * 传入 startId 时为「偏移导出」：自建武将的 ID 整体平移到以 startId 起始的号段
 * （新 Id = 原 Id - 当前起始号 + startId），关系字段（父子 / 配偶 / 兄弟 / 亲近 / 厌恶）
 * 与容器 offset 一并同步。
 * @param lib 库标识
 * @param fileName 下载文件名
 * @param startId 偏移导出的起始 ID（不传则按原编号导出）
 */
export function downloadLibrary(lib: LibraryKey, fileName: string, startId?: number): void {
  const token = getToken()
  const params = new URLSearchParams()
  if (token) params.set('token', token)
  if (startId !== undefined && Number.isFinite(startId)) params.set('start', String(startId))
  const query = params.toString()
  const url = withLib('/api/download', lib) + (query ? `&${query}` : '')
  const a = document.createElement('a')
  // 直链下载也要带站点前缀，否则会打到同机根路径上的另一个站点
  a.href = API_PREFIX + url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
}

/**
 * 批量导入武将（仅自建武将库可用，需写入权限）。
 * 文件中的原 ID 会被忽略，服务端从当前下一个可用 ID 起顺序分配；
 * 重名时自动追加 #1 / #2 …… 后缀。
 * 传入 overwrites 时，命中下标的记录不新建，而是覆盖对应既有武将
 * （保留其原 ID，其余字段以导入数据为准）。
 * @param lib 库标识
 * @param payload 导入数据：文件原始文本（content）或已解析对象（data）；
 *                overwrites 为「源下标 -> 覆盖目标 ID」映射
 * @returns 导入结果
 */
export function importPersons(
  lib: LibraryKey,
  payload: { content?: string; data?: unknown; overwrites?: Record<string, number> },
): Promise<ImportResult> {
  return apiFetch(withLib('/api/persons/import', lib), {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(payload),
  }).then((r) => handle<ImportResult>(r))
}

// ─────────────────────────────────────────────────────────────
// 自定义头像
// ─────────────────────────────────────────────────────────────

/**
 * 获取自定义头像列表与下一个可用 ID。
 * @returns 自定义头像列表
 */
export function fetchCustomFaces(): Promise<CustomFaceResult> {
  return apiFetch('/api/faces/custom', { headers: headers() }).then((r) => handle<CustomFaceResult>(r))
}

/**
 * 保存一张自定义头像（半身像 + 头像，均为 PNG base64）。
 * 服务端按 ID 规则分配新 ID，图片写入 Face 资源目录。
 * @param payload 头像数据
 * @returns 新分配的头像 ID
 */
export function createCustomFace(payload: {
  /** 归属性别：0=男，1=女 */
  sex: number
  /** 半身像（240x240 PNG，dataURL 或 base64） */
  bust: string
  /** 头像（64x80 PNG，dataURL 或 base64） */
  face: string
}): Promise<{ id: number }> {
  return apiFetch('/api/faces/custom', {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(payload),
  }).then((r) => handle<{ id: number }>(r))
}

/**
 * 删除自定义头像（同时删除半身像与头像）。
 * 服务端会保留该 ID 作为「空白占位格」，列表里仍显示位置，便于后续拖拽换位。
 * @param id 头像 ID
 * @param options 引用同步方式
 * @returns 删除结果；keep 模式返回 dangling（未处理的引用数），replace 模式返回 replaced/replaceId
 */
export function removeCustomFace(
  id: number,
  options: { mode?: 'keep' | 'replace'; replaceId?: number } = {},
): Promise<{ id: number; dangling?: number; replaced?: number; replaceId?: number }> {
  const payload: Record<string, unknown> = { mode: options.mode === 'replace' ? 'replace' : 'keep' }
  if (options.replaceId !== undefined) payload.replaceId = options.replaceId
  return apiFetch(`/api/faces/custom/${id}`, {
    method: 'DELETE',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(payload),
  }).then((r) => handle<{ id: number; dangling?: number; replaced?: number; replaceId?: number }>(r))
}

/**
 * 查询某个自定义头像被哪些武将引用（删除 / 改 ID 前提示玩家）。
 * @param id 头像 ID
 * @returns 引用该头像的武将列表
 */
export function fetchCustomFaceUsage(id: number): Promise<FaceUsageResult> {
  return apiFetch(`/api/faces/custom/${id}/usage`, { headers: headers() }).then((r) =>
    handle<FaceUsageResult>(r),
  )
}

/**
 * 批量导入自定义头像（批量导入半身像）。
 * @param payload 性别与图片对列表（半身像 + 自动生成的小头像）
 * @returns 新分配的 ID 列表
 */
export function createCustomFacesBatch(payload: {
  /** 归属性别：0=男，1=女 */
  sex: number
  /** 图片对（240x240 半身像 + 64x80 头像，dataURL 或 base64） */
  items: Array<{ bust: string; face: string }>
}): Promise<{ ids: number[]; count: number }> {
  return apiFetch('/api/faces/custom/batch', {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(payload),
  }).then((r) => handle<{ ids: number[]; count: number }>(r))
}

/**
 * 更新已有自定义头像的图片。
 * 用于「重新导入半身像」与「以半身像为准重新裁剪小头像」。
 * @param id 头像 ID
 * @param payload 需要更新的图片（bust / face 至少传一个）
 * @returns 更新结果（含缓存版本号）
 */
export function updateCustomFaceImages(
  id: number,
  payload: { bust?: string; face?: string },
): Promise<{ id: number; updated: string[]; updatedAt: string; version: number }> {
  return apiFetch(`/api/faces/custom/${id}`, {
    method: 'PUT',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(payload),
  }).then((r) => handle<{ id: number; updated: string[]; updatedAt: string; version: number }>(r))
}

/**
 * 把自定义头像移动到空白占位格（仅限同性别号段）。
 * 武将的 headIconID 引用会由服务端一并改到新 ID。
 * @param id 原头像 ID
 * @param to 目标空白占位格 ID
 * @returns 移动结果（remapped 为同步更新的武将条数）
 */
export function moveCustomFace(
  id: number,
  to: number,
): Promise<{ from: number; to: number; remapped: number; warnings?: string[] }> {
  return apiFetch(`/api/faces/custom/${id}/move`, {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify({ to }),
  }).then((r) => handle<{ from: number; to: number; remapped: number; warnings?: string[] }>(r))
}

/**
 * 修改自定义头像性别：服务端按目标性别重新分配 ID 并把图片改名搬移。
 * @param id 头像 ID
 * @param sex 目标性别，0=男，1=女
 * @returns 结果（to 为新分配的头像 ID）
 */
export function changeCustomFaceSex(
  id: number,
  sex: number,
): Promise<{ from: number; to: number; sex: number; remapped: number; warnings?: string[] }> {
  return apiFetch(`/api/faces/custom/${id}/sex`, {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify({ sex }),
  }).then((r) => handle<{ from: number; to: number; sex: number; remapped: number; warnings?: string[] }>(r))
}

/**
 * 取自定义头像的原始 PNG，返回可直接交给 <img> / 画布的 object URL。
 *
 * 走服务端同源接口而不是 /face/{id}_{type}.png：
 * 配置了 R2 时后者会被 302 到另一域名，画布跨域取图会被污染，
 * toDataURL() 会直接抛错，无法用于「以半身像为准重新裁剪小头像」。
 * 调用方负责在不再使用时 URL.revokeObjectURL。
 * @param id 头像 ID
 * @param type 1=半身像，2=头像
 * @returns object URL
 */
export async function fetchCustomFaceRaw(id: number, type: 1 | 2): Promise<string> {
  const res = await apiFetch(`/api/faces/custom/raw?id=${id}&type=${type}`, { headers: headers() })
  if (!res.ok) throw new ApiError(res.status, `读取头像 ${id} 的图片失败（${res.status}）`)
  return URL.createObjectURL(await res.blob())
}

/**
 * 打包下载自定义头像（ZIP）。
 * 浏览器直链无法自定义请求头，因此令牌通过 query 参数传递。
 * @param ids 需要打包的头像 ID；传 null 表示打包全部
 * @param fileName 下载文件名
 */
export function downloadCustomFaces(ids: number[] | null, fileName: string): void {
  const params = new URLSearchParams()
  if (ids && ids.length > 0) params.set('ids', ids.join(','))
  const token = getToken()
  if (token) params.set('token', token)
  const query = params.toString()
  const a = document.createElement('a')
  // 同上：直链下载同样需要前缀
  a.href = API_PREFIX + '/api/faces/custom/export' + (query ? '?' + query : '')
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
}

/**
 * 生成「武将模组素材包」（ZIP），产出可直接上传到创意工坊的模组包。
 *
 * 这里用 POST + Blob 而不是像另两个下载接口那样走直链：
 * 选中的 ID 数量不定，塞进 query 有长度风险，也没必要把令牌暴露在 URL 里。
 * @param lib 库标识
 * @param payload 选中的武将 ID 与要写进 mod.info 的元信息
 * @returns ZIP 二进制内容
 */
export async function exportModPack(
  lib: LibraryKey,
  payload: { ids: number[]; name?: string; version?: string; description?: string },
): Promise<Blob> {
  const res = await apiFetch(withLib('/api/persons/export-mod', lib), {
    method: 'POST',
    headers: headers({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(payload),
  })
  if (!res.ok) {
    // 失败时后端给的是 JSON 错误体，交给 handle 抛出带状态码的异常
    await handle<unknown>(res)
  }
  return res.blob()
}
