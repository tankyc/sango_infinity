/**
 * 后端接口客户端
 *
 * 统一处理响应信封（{ ok, message, code }）与错误抛出，
 * 让上层组件只需面对 Promise 与异常。
 */
import type {
  BackupItem,
  CommonBackupItem,
  CommonFileDoc,
  CommonFileInfo,
  LibraryLibInfo,
  LibraryPerson,
  Options,
  Scenario,
  ScenarioMeta,
} from './types'

/** 后端返回的错误 */
export class ApiError extends Error {
  code: string
  details?: string[]
  status: number

  constructor(message: string, code = 'EUNKNOWN', status = 500, details?: string[]) {
    super(message)
    this.name = 'ApiError'
    this.code = code
    this.status = status
    this.details = details
  }
}

/* ------------------------------------------------------------------ */
/* 令牌管理                                                            */
/* ------------------------------------------------------------------ */

/** 令牌在本地存储中的键名 */
const TOKEN_KEY = 'scenario-editor:token'

/**
 * 读取本地保存的令牌。
 *
 * @returns 令牌；未登录返回空串
 */
export function getToken(): string {
  try {
    return localStorage.getItem(TOKEN_KEY) || ''
  } catch {
    return ''
  }
}

/**
 * 保存令牌（登录）。
 *
 * @param token 令牌
 */
export function setToken(token: string): void {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token)
    else localStorage.removeItem(TOKEN_KEY)
  } catch {
    /* 隐私模式下 localStorage 可能不可用，忽略即可 */
  }
}

/** 清除令牌（退出登录） */
export function clearToken(): void {
  setToken('')
}

/**
 * 给下载类直链补上令牌。
 *
 * 浏览器的 `<a download>` 无法自定义请求头，所以令牌只能走查询参数——
 * 后端 auth 模块对 `?token=` 是一等公民支持。
 *
 * @param url 原始地址
 * @returns 带令牌的地址
 */
export function withToken(url: string): string {
  const token = getToken()
  if (!token) return url
  return `${url}${url.includes('?') ? '&' : '?'}token=${encodeURIComponent(token)}`
}

/**
 * 统一请求封装。
 *
 * @param path 接口路径
 * @param init fetch 配置
 * @returns 解析后的 JSON
 */
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken()
  let res: Response
  try {
    res = await fetch(path, {
      ...init,
      headers: {
        // 默认按 JSON 发；上传剧本要发原始文本，调用方显式覆盖 Content-Type
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(init?.headers || {}),
      },
    })
  } catch (e) {
    throw new ApiError(`无法连接后端服务（${path}）：${(e as Error).message}`, 'ENETWORK', 0)
  }

  const text = await res.text()
  let data: unknown = null
  if (text) {
    try {
      data = JSON.parse(text)
    } catch {
      throw new ApiError(`后端返回了非 JSON 内容（HTTP ${res.status}）`, 'EBADRESPONSE', res.status)
    }
  }

  const body = data as Record<string, unknown> | null
  if (!res.ok || (body && body.ok === false)) {
    throw new ApiError(
      String(body?.message ?? `请求失败（HTTP ${res.status}）`),
      String(body?.code ?? 'EUNKNOWN'),
      res.status,
      body?.details as string[] | undefined
    )
  }
  return body as T
}

/**
 * 读取剧本。
 *
 * 多用户模式下，账号首次进入时工作区是空的，后端会返回 `needInit: true`
 * 且 `scenario` 为 null——此时前端应引导用户去模板库挑一个起点，
 * 而不是把它当成网络错误。
 *
 * @returns 剧本、元信息与「尚未初始化」标记
 */
export async function fetchScenario(): Promise<{
  scenario: Scenario | null
  meta: ScenarioMeta | null
  needInit: boolean
}> {
  const data = await request<{ scenario: Scenario | null; meta: ScenarioMeta | null; needInit?: boolean }>(
    '/api/scenario'
  )
  return { scenario: data.scenario, meta: data.meta, needInit: Boolean(data.needInit) }
}

/** 只读取元信息 */
export async function fetchScenarioMeta(): Promise<ScenarioMeta> {
  const data = await request<{ meta: ScenarioMeta }>('/api/scenario/meta')
  return data.meta
}

/** 保存剧本 */
export async function saveScenario(
  scenario: Scenario,
  baseRevision: string
): Promise<{ meta: ScenarioMeta; backup: string | null }> {
  return request<{ meta: ScenarioMeta; backup: string | null }>('/api/scenario', {
    method: 'PUT',
    body: JSON.stringify({ scenario, baseRevision }),
  })
}

/** 读取公共数据表 / 枚举选项 */
export async function fetchOptions(): Promise<Options> {
  const data = await request<{ options: Options }>('/api/options')
  return data.options
}

/** 读取武将库（武将源） */
export async function fetchLibrary(refresh = false): Promise<{
  source: string
  api: string
  count: number
  persons: LibraryPerson[]
  /** 分库信息（基础 / 自建），用于分组展示 */
  libs?: LibraryLibInfo[]
  error?: string
}> {
  return request(`/api/library${refresh ? '?refresh=1' : ''}`)
}

/**
 * 剧本文件下载地址。
 *
 * 走浏览器原生下载（后端带 Content-Disposition），
 * 因此不需要 fetch + Blob 那套处理。已带上令牌。
 *
 * @returns 下载 URL
 */
export function scenarioDownloadUrl(): string {
  return withToken('/api/scenario/download')
}

/* ------------------------------------------------------------------ */
/* 登录状态                                                            */
/* ------------------------------------------------------------------ */

/** 当前登录状态 */
export interface AuthState {
  /** 是否已接入账号体系（找不到密钥文件时为 false，按单机模式运行） */
  authAvailable: boolean
  /** 是否多用户隔离模式 */
  multiUser: boolean
  /** 当前用户；未登录为 null */
  user: { username: string; displayName: string; role: string; roleLabel: string; isAdmin: boolean } | null
  /** 是否可写 */
  canWrite: boolean
  /** 是否具备管理员权限（可上传模板 / 剧本） */
  canAdmin: boolean
}

/** 读取当前登录状态 */
export async function fetchAuthState(): Promise<AuthState> {
  return request<AuthState>('/api/auth/me')
}

/**
 * 用武将库网站的账号登录（由后端代理转发，浏览器不必处理跨域）。
 *
 * @param username 用户名
 * @param password 密码
 * @returns 令牌与用户信息
 */
export async function loginRequest(
  username: string,
  password: string
): Promise<{ token: string; user: AuthState['user'] }> {
  return request<{ token: string; user: AuthState['user'] }>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  })
}

/* ------------------------------------------------------------------ */
/* 剧本模板                                                            */
/* ------------------------------------------------------------------ */

/** 模板列表项 */
export interface TemplateItem {
  id: string
  name: string
  description: string
  author: string
  createdAt: string
  size: number
  revision: string
  scenarioName: string
  counts: Record<string, number> | null
}

/** 模板列表 */
export async function fetchTemplates(): Promise<TemplateItem[]> {
  const data = await request<{ templates: TemplateItem[] }>('/api/templates')
  return data.templates
}

/** 模板下载地址 */
export function templateDownloadUrl(id: string): string {
  return withToken(`/api/templates/${encodeURIComponent(id)}/download`)
}

/**
 * 新增模板（需管理员）。
 *
 * 不传 content 表示「把当前工作区的剧本存成模板」。
 *
 * @param input 名称、说明与可选内容
 */
export async function createTemplate(input: {
  name: string
  description?: string
  content?: string
}): Promise<TemplateItem> {
  const data = await request<{ template: TemplateItem }>('/api/templates', {
    method: 'POST',
    body: JSON.stringify(input),
  })
  return data.template
}

/** 删除模板（需管理员） */
export async function deleteTemplate(id: string): Promise<void> {
  await request(`/api/templates/${encodeURIComponent(id)}`, { method: 'DELETE' })
}

/**
 * 从模板开始：把模板内容写进当前工作区。
 *
 * @param id 模板 id
 * @param force 已有剧本时是否强制覆盖
 */
export async function applyTemplate(id: string, force = false): Promise<{ revision: string }> {
  return request<{ revision: string }>(
    `/api/templates/${encodeURIComponent(id)}/apply${force ? '?force=1' : ''}`,
    { method: 'POST' }
  )
}

/* ------------------------------------------------------------------ */
/* 公共数据：下载 / 基准版 / 差量补丁                                  */
/* ------------------------------------------------------------------ */

/** 单个公共数据表的完整下载地址 */
export function commonFileDownloadUrl(name: string): string {
  return withToken(`/api/common/file/download?name=${encodeURIComponent(name)}`)
}

/** 单个公共数据表的差量补丁地址（差量可能为空，建议走 fetch 以便提示） */
export function commonFilePatchUrl(name: string): string {
  return withToken(`/api/common/file/patch?name=${encodeURIComponent(name)}`)
}

/** 整个 Common 目录的差量补丁地址 */
export function commonPatchUrl(): string {
  return withToken('/api/common/patch')
}

/** 基准版条目 */
export interface BaselineItem {
  name: string
  revision: string
  at: string
  author: string
  size: number
  /** 当前工作区该文件的指纹 */
  currentRevision: string
  /** 是否与基准一致（无改动） */
  upToDate: boolean
}

/** 基准版清单 */
export async function fetchBaselines(): Promise<BaselineItem[]> {
  const data = await request<{ baselines: BaselineItem[] }>('/api/common/baseline')
  return data.baselines
}

/**
 * 上传基准版（需管理员）。
 *
 * 不传 files 表示把当前工作区的全部公共数据快照为基准。
 *
 * @param files 要登记为基准的文件名列表
 */
export async function uploadBaseline(files?: string[]): Promise<{ count: number }> {
  return request<{ count: number }>('/api/common/baseline', {
    method: 'POST',
    body: JSON.stringify(files && files.length > 0 ? { files } : {}),
  })
}

/** 删除某个基准版（需管理员） */
export async function deleteBaseline(name: string): Promise<void> {
  await request(`/api/common/baseline?name=${encodeURIComponent(name)}`, { method: 'DELETE' })
}

/**
 * 下载一个可能为「空/无基准」的差量补丁。
 *
 * 差量接口会用 204 表示「没有改动」、409 表示「还没有基准」，
 * 这两种都不是文件，不能直接用 `<a download>`——否则会把错误 JSON 存成文件。
 * 这里走 fetch，按状态给出明确提示，只有真的有补丁才触发保存。
 *
 * @param url 补丁地址
 * @param fallbackName 兜底文件名
 * @returns 结果说明（用于提示）
 */
export async function downloadPatch(
  url: string,
  fallbackName: string
): Promise<{ kind: 'saved' | 'empty' | 'error'; message?: string }> {
  const res = await fetch(url, { headers: getToken() ? { Authorization: `Bearer ${getToken()}` } : {} })

  if (res.status === 204) return { kind: 'empty' }
  if (!res.ok) {
    const text = await res.text()
    let message = `下载失败（HTTP ${res.status}）`
    try {
      message = JSON.parse(text).message || message
    } catch {
      /* 非 JSON 时用默认提示 */
    }
    return { kind: 'error', message }
  }

  const blob = await res.blob()
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = fallbackName
  document.body.appendChild(link)
  link.click()
  link.remove()
  // 交给浏览器读完再释放，立即 revoke 在部分浏览器上会拿到空文件
  setTimeout(() => URL.revokeObjectURL(link.href), 10_000)
  return { kind: 'saved' }
}

/**
 * 上传剧本覆盖当前工作区。
 *
 * 以 `text/plain` 直发原始 JSON 文本，避免 1.7MB 内容被二次转义。
 *
 * @param content 剧本文本
 * @param force 已有剧本时是否强制覆盖
 */
export async function uploadScenario(
  content: string,
  force = false
): Promise<{ name: string; revision: string; replaced: boolean }> {
  return request<{ name: string; revision: string; replaced: boolean }>(
    `/api/scenario/upload${force ? '?force=1' : ''}`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'text/plain' },
      body: content,
    }
  )
}

/** 读取备份列表 */
export async function fetchBackups(): Promise<BackupItem[]> {
  const data = await request<{ backups: BackupItem[] }>('/api/backups')
  return data.backups
}

/** 从备份恢复 */
export async function restoreBackup(name: string): Promise<{ meta: ScenarioMeta }> {
  return request<{ meta: ScenarioMeta }>(`/api/backups/${encodeURIComponent(name)}/restore`, {
    method: 'POST',
  })
}

/* ------------------------------------------------------------------ */
/* 公共数据表（Build/Content/Data/Common）                             */
/* ------------------------------------------------------------------ */

/** 读取公共数据表目录清单 */
export async function fetchCommonList(refresh = false): Promise<{ dir: string; files: CommonFileInfo[] }> {
  const data = await request<{ dir: string; files: CommonFileInfo[] }>(
    `/api/common${refresh ? '?refresh=1' : ''}`
  )
  return { dir: data.dir, files: data.files }
}

/** 读取单个公共数据表 */
export async function fetchCommonFile(name: string): Promise<CommonFileDoc> {
  const data = await request<CommonFileDoc>(`/api/common/file?name=${encodeURIComponent(name)}`)
  return data
}

/** 保存单个公共数据表 */
export async function saveCommonFile(
  name: string,
  raw: string,
  baseRevision: string
): Promise<{ name: string; meta: ScenarioMeta; backup: string | null }> {
  return request(`/api/common/file`, {
    method: 'PUT',
    body: JSON.stringify({ name, raw, baseRevision }),
  })
}

/** 读取公共数据表备份 */
export async function fetchCommonBackups(name?: string): Promise<CommonBackupItem[]> {
  const data = await request<{ backups: CommonBackupItem[] }>(
    `/api/common/backups${name ? `?name=${encodeURIComponent(name)}` : ''}`
  )
  return data.backups
}

/** 从备份恢复公共数据表 */
export async function restoreCommonBackup(dir: string): Promise<{ name: string; meta: ScenarioMeta }> {
  return request(`/api/common/backups/${encodeURIComponent(dir)}/restore`, { method: 'POST' })
}
