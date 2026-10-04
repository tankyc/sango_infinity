/**
 * ModHub 后端接口封装
 *
 * 约定（与 server 侧契约一一对应）：
 *   1. 所有请求都走相对路径 —— 开发期由 Vite 代理转发到 8081，部署期由 Nginx 同域反代，
 *      两种形态下前端代码完全一致，无需 baseURL 配置。
 *   2. 登录态为 Bearer Token，存 localStorage；与游戏内 CloudSaveClient 的 token 同源可互换。
 *   3. 失败统一抛 ApiRequestError，页面据 message / details 展示中文提示。
 */

export type SortKey = 'hot' | 'new' | 'updated' | 'trending';

export interface Author {
  id: number;
  username: string;
  nickname: string;
  avatar: string | null;
}

export interface ModStats {
  subscribers: number;
  likes: number;
  views: number;
  downloads: number;
}

/** 列表卡片（对应后端 BrowseListItem） */
export interface ModListItem {
  id: string;
  name: string;
  summary: string;
  category: string;
  posterUrl: string | null;
  gameVersion: string;
  tags: string[];
  author: Author;
  version: string | null;
  size: number;
  createdAt: string;
  updatedAt: string;
  stats: ModStats;
}

export interface BrowseResponse {
  items: ModListItem[];
  nextCursor: string | null;
  total: number;
  sort: SortKey;
}

export interface ModVersionInfo {
  version: string;
  size: number;
  changelog: string;
  depends: string[];
  gameVersion: string;
  downloads: number;
  createdAt: string;
  downloadUrl: string;
}

/** 详情（对应后端 GET /api/mods/:id） */
export interface ModDetail {
  id: string;
  name: string;
  summary: string;
  description: string;
  category: string;
  tags: string[];
  posterUrl: string;
  gameVersion: string;
  status: string;
  author: Author;
  createdAt: string;
  updatedAt: string;
  stats: ModStats;
  latestVersion: string | null;
  latestSize: number;
  requiredItems: string[];
  commentCount: number;
  downloadUrl: string | null;
  pageUrl: string;
  versions: ModVersionInfo[];
}

export interface FilterOptions {
  categories: { key: string; count: number }[];
  tags: { tag: string; count: number }[];
  gameVersions: string[];
}

export interface AuthUser {
  id: number;
  username: string;
  nickname: string | null;
  avatar: string | null;
  role: string;
}

export interface LoginResponse {
  token: string;
  user: AuthUser;
}

export interface PublishResponse {
  mod: { id: string; name: string; summary: string; category: string; status: string; posterUrl: string };
  version: { version: string; size: number; changelog: string; depends: string[] };
  downloadUrl: string;
  pageUrl: string;
  warnings: string[];
}

/** 统一的接口异常 */
export class ApiRequestError extends Error {
  readonly status: number;
  readonly code: string;
  readonly details: string[];

  constructor(status: number, message: string, code = 'error', details: string[] = []) {
    super(message);
    this.name = 'ApiRequestError';
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

const TOKEN_KEY = 'sango_modhub_token';

/**
 * token 的内存副本
 *
 * 为什么不能只依赖 localStorage：IDE 内置浏览器、隐私模式等环境下 localStorage
 * 可能写入失败或被清空，那样会出现「页面看着已登录（内存里有 user），但发请求全 401」。
 * 因此以内存为准、localStorage 只作为持久化副本。
 */
let memoryToken: string | null = null;
let tokenLoadedFromStorage = false;

export function getToken(): string | null {
  if (!tokenLoadedFromStorage) {
    tokenLoadedFromStorage = true;
    try {
      memoryToken = window.localStorage.getItem(TOKEN_KEY);
    } catch {
      memoryToken = null;
    }
  }
  return memoryToken;
}

export function setToken(token: string | null): void {
  memoryToken = token;
  tokenLoadedFromStorage = true;
  try {
    if (token) window.localStorage.setItem(TOKEN_KEY, token);
    else window.localStorage.removeItem(TOKEN_KEY);
  } catch {
    /* localStorage 不可用时忽略：本次会话仍靠内存副本正常工作 */
  }
}

type QueryValue = string | number | boolean | string[] | null | undefined;

function buildQuery(params?: Record<string, QueryValue>): string {
  if (!params) return '';
  const sp = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value == null || value === '' || value === false) continue;
    if (Array.isArray(value)) {
      for (const v of value) if (v !== '') sp.append(key, String(v));
    } else {
      sp.append(key, String(value));
    }
  }
  const qs = sp.toString();
  return qs === '' ? '' : `?${qs}`;
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  const token = getToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);
  // 只在调用方没有显式指定时才补 JSON 头。
  // 登录/注册用的是 x-www-form-urlencoded（与游戏内 WWWForm 同款），
  // 若在这里无条件覆写成 application/json，后端会拿表单串当 JSON 解析，
  // 直接报 "Body is not valid JSON but content-type is set to 'application/json'"。
  if (init?.body != null && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  let res: Response;
  try {
    res = await fetch(path, { ...init, headers });
  } catch {
    throw new ApiRequestError(0, '无法连接到 ModHub 服务，请确认后端已启动', 'network_error');
  }

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  const data = text === '' ? null : safeJson(text);

  if (!res.ok) {
    const body = data as { error?: string; code?: string; details?: string[] } | null;
    throw new ApiRequestError(
      res.status,
      body?.error ?? `请求失败（HTTP ${res.status}）`,
      body?.code ?? 'error',
      body?.details ?? [],
    );
  }

  return data as T;
}

/* ------------------------------ 浏览与筛选 ------------------------------ */

export interface BrowseParams {
  cursor?: string;
  limit?: number;
  q?: string;
  category?: string;
  gameVersion?: string;
  sort?: SortKey;
  tags?: string[];
}

export function fetchBrowse(params: BrowseParams, signal?: AbortSignal): Promise<BrowseResponse> {
  return request<BrowseResponse>(`/api/browse${buildQuery(params as Record<string, QueryValue>)}`, { signal });
}

export function fetchFilters(signal?: AbortSignal): Promise<FilterOptions> {
  return request<FilterOptions>('/api/filters', { signal });
}

export function fetchModDetail(id: string, signal?: AbortSignal): Promise<ModDetail> {
  return request<ModDetail>(`/api/mods/${encodeURIComponent(id)}`, { signal });
}

/* ------------------------------ 留言 ------------------------------ */

export interface CommentAuthor {
  id: number;
  username: string;
  nickname: string;
  avatar: string | null;
  role: string;
}

export interface CommentItem {
  id: number;
  content: string;
  createdAt: string;
  user: CommentAuthor;
  /** 服务端算好的删除权限：留言本人 / 模组作者 / 管理员 */
  canDelete: boolean;
  /** 点赞数（服务端实时统计） */
  likes: number;
  /** 当前登录用户是否点过赞 */
  likedByMe: boolean;
  /** 只有顶层留言带 replies（只做两层） */
  replies?: CommentItem[];
}

export interface CommentPage {
  items: CommentItem[];
  /** 上一页最后一条的 id；为 null 表示没有更多了 */
  nextCursor: number | null;
  total: number;
  canPost: boolean;
}

export function fetchComments(
  modId: string,
  params: { cursor?: number; limit?: number } = {},
  signal?: AbortSignal,
): Promise<CommentPage> {
  return request<CommentPage>(
    `/api/mods/${encodeURIComponent(modId)}/comments${buildQuery(params as Record<string, QueryValue>)}`,
    { signal },
  );
}

export function postComment(modId: string, content: string, parentId?: number): Promise<{ comment: CommentItem }> {
  return request<{ comment: CommentItem }>(`/api/mods/${encodeURIComponent(modId)}/comments`, {
    method: 'POST',
    body: JSON.stringify({ content, parentId }),
  });
}

export function deleteComment(id: number): Promise<{ deleted: boolean; id: number }> {
  return request<{ deleted: boolean; id: number }>(`/api/comments/${id}`, { method: 'DELETE' });
}

/** 点赞（幂等：重复调用不会重复计数） */
export function likeComment(id: number): Promise<{ liked: boolean; likes: number }> {
  return request<{ liked: boolean; likes: number }>(`/api/comments/${id}/like`, { method: 'POST' });
}

/** 取消点赞（同样幂等） */
export function unlikeComment(id: number): Promise<{ liked: boolean; likes: number }> {
  return request<{ liked: boolean; likes: number }>(`/api/comments/${id}/like`, { method: 'DELETE' });
}

/* ------------------------------ 认证 ------------------------------ */

export function login(username: string, password: string): Promise<LoginResponse> {
  const body = new URLSearchParams({ username, password });
  return request<LoginResponse>('/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  });
}

export function register(username: string, password: string): Promise<LoginResponse> {
  const body = new URLSearchParams({ username, password });
  return request<LoginResponse>('/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  });
}

export function fetchMe(signal?: AbortSignal): Promise<{ user: AuthUser }> {
  return request<{ user: AuthUser }>('/me', { signal });
}

/* ------------------------------ 发布 ------------------------------ */

export interface PublishForm {
  zip: File;
  poster?: File | null;
  name: string;
  summary?: string;
  description?: string;
  category: string;
  tags?: string;
  depends?: string;
  gameVersion?: string;
  version?: string;
  changelog?: string;
}

/**
 * 带上传进度的 multipart 提交
 *
 * 为什么不用 fetch：fetch 拿不到「上传」进度（只有下载进度），
 * 而模组包可能上百 MB，没有进度条用户会以为卡死。
 */
function uploadXhr<T>(path: string, fd: FormData, onProgress?: (percent: number) => void): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', path);
    const token = getToken();
    if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`);

    xhr.upload.onprogress = (e) => {
      if (onProgress && e.lengthComputable) onProgress(Math.round((e.loaded / e.total) * 100));
    };
    xhr.onload = () => {
      const data = safeJson(xhr.responseText) as
        | T
        | { error?: string; code?: string; details?: string[] }
        | null;
      if (xhr.status >= 200 && xhr.status < 300 && data) {
        resolve(data as T);
        return;
      }
      const err = data as { error?: string; code?: string; details?: string[] } | null;
      reject(
        new ApiRequestError(
          xhr.status,
          err?.error ?? `提交失败（HTTP ${xhr.status}）`,
          err?.code ?? 'error',
          err?.details ?? [],
        ),
      );
    };
    xhr.onerror = () => reject(new ApiRequestError(0, '上传过程中网络中断', 'network_error'));
    xhr.send(fd);
  });
}

/** 发布新模组（multipart 表单，与游戏内 WWWForm 提交的字段名保持一致） */
export function publishMod(form: PublishForm, onProgress?: (percent: number) => void): Promise<PublishResponse> {
  const fd = new FormData();
  fd.append('file', form.zip);
  if (form.poster) fd.append('poster', form.poster);
  fd.append('name', form.name);
  if (form.summary) fd.append('summary', form.summary);
  if (form.description) fd.append('description', form.description);
  fd.append('category', form.category);
  if (form.tags) fd.append('tags', form.tags);
  if (form.depends) fd.append('depends', form.depends);
  if (form.gameVersion) fd.append('gameVersion', form.gameVersion);
  if (form.version) fd.append('version', form.version);
  if (form.changelog) fd.append('changelog', form.changelog);

  return uploadXhr<PublishResponse>('/api/mods', fd, onProgress);
}

/* ------------------------------ 我的创作（作者侧） ------------------------------ */

/** 我发布的模组（对应后端 GET /api/mine） */
export interface MyModRow {
  id: string;
  name: string;
  summary: string;
  description: string;
  category: string;
  status: string;
  posterUrl: string | null;
  gameVersion: string;
  tags: string[];
  version: string | null;
  size: number;
  versionCount: number;
  stats: ModStats;
  createdAt: string;
  updatedAt: string;
}

export interface MyWorkshopResponse {
  summary: {
    total: number;
    approved: number;
    pending: number;
    hidden: number;
    rejected: number;
    downloads: number;
    subscribers: number;
    views: number;
  };
  items: MyModRow[];
}

export function fetchMyMods(signal?: AbortSignal): Promise<MyWorkshopResponse> {
  return request<MyWorkshopResponse>('/api/mine', { signal });
}

export interface MyModPatch {
  name?: string;
  summary?: string;
  description?: string;
  category?: string;
  tags?: string;
  gameVersion?: string;
  /** 作者只能在上架 / 下架之间切换（审核状态由管理员控制） */
  status?: 'approved' | 'hidden';
  poster?: File | null;
}

/** 修改自己已发布模组的信息；只提交填了的字段，其余保持原样 */
export function updateMyMod(id: string, patch: MyModPatch): Promise<{ mod: { id: string; status: string } }> {
  const fd = new FormData();
  if (patch.name !== undefined) fd.append('name', patch.name);
  if (patch.summary !== undefined) fd.append('summary', patch.summary);
  if (patch.description !== undefined) fd.append('description', patch.description);
  if (patch.category !== undefined) fd.append('category', patch.category);
  if (patch.tags !== undefined) fd.append('tags', patch.tags);
  if (patch.gameVersion !== undefined) fd.append('gameVersion', patch.gameVersion);
  if (patch.status !== undefined) fd.append('status', patch.status);
  if (patch.poster) fd.append('poster', patch.poster);

  return request<{ mod: { id: string; status: string } }>(`/api/mods/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: fd,
  });
}

/** 下架自己的模组（软下架，可随时重新上架；物理删除只有管理员能做） */
export function hideMyMod(id: string): Promise<{ changed: boolean }> {
  return request<{ changed: boolean }>(`/api/mods/${encodeURIComponent(id)}`, { method: 'DELETE' });
}

export interface PublishVersionForm {
  id: string;
  zip: File;
  /** 留空则采用 zip 内 mod.info 的版本号 */
  version?: string;
  changelog?: string;
}

/** 给自己已发布的模组发新版本（复用后端的 POST /api/mods/:id/versions） */
export function publishModVersion(
  form: PublishVersionForm,
  onProgress?: (percent: number) => void,
): Promise<PublishResponse> {
  const fd = new FormData();
  fd.append('file', form.zip);
  if (form.version) fd.append('version', form.version);
  if (form.changelog) fd.append('changelog', form.changelog);

  return uploadXhr<PublishResponse>(`/api/mods/${encodeURIComponent(form.id)}/versions`, fd, onProgress);
}

/* ------------------------------ 管理后台 ------------------------------ */

/** offset 分页响应（后台列表用，与前台浏览页的游标分页刻意不同） */
export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AdminUserRow {
  id: number;
  username: string;
  nickname: string;
  avatar: string | null;
  role: string;
  disabled: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  modCount: number;
  subscriptionCount: number;
  commentCount: number;
}

export interface AdminStats {
  users: { total: number; admins: number; disabled: number; recent7d: number };
  mods: {
    total: number;
    pending: number;
    approved: number;
    rejected: number;
    hidden: number;
    versions: number;
    storageBytes: number;
  };
  engagement: {
    downloads: number;
    views: number;
    subscribers: number;
    comments: number;
    likes: number;
    subscriptions: number;
  };
  recent: {
    users: { id: number; username: string; nickname: string | null; role: string; disabled: boolean; createdAt: string }[];
    pendingMods: {
      id: string;
      name: string;
      category: string;
      createdAt: string;
      author: { id: number; username: string; nickname: string | null };
    }[];
  };
}

export interface AdminModRow {
  id: string;
  name: string;
  summary: string;
  category: string;
  status: string;
  posterUrl: string | null;
  gameVersion: string;
  tags: string[];
  author: { id: number; username: string; nickname: string | null; disabled: boolean };
  version: string | null;
  size: number;
  versionCount: number;
  stats: { downloads: number; views: number; subscribers: number; likes: number };
  createdAt: string;
  updatedAt: string;
}

export function fetchAdminStats(signal?: AbortSignal): Promise<AdminStats> {
  return request<AdminStats>('/api/admin/stats', { signal });
}

export interface AdminUserQuery {
  q?: string;
  role?: string;
  disabled?: boolean;
  sort?: 'created' | 'lastLogin' | 'username' | 'mods';
  page?: number;
  pageSize?: number;
}

export function fetchAdminUsers(params: AdminUserQuery, signal?: AbortSignal): Promise<Paged<AdminUserRow>> {
  return request<Paged<AdminUserRow>>(`/api/admin/users${buildQuery(params as Record<string, QueryValue>)}`, { signal });
}

export interface AdminUserPatch {
  role?: string;
  nickname?: string;
  disabled?: boolean;
  password?: string;
}

export function updateAdminUser(id: number, patch: AdminUserPatch): Promise<{ user: AdminUserRow }> {
  return request<{ user: AdminUserRow }>(`/api/admin/users/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(patch),
  });
}

export function deleteAdminUser(id: number, force = false): Promise<{ deleted: boolean; removedMods: number }> {
  return request<{ deleted: boolean; removedMods: number }>(`/api/admin/users/${id}${force ? '?force=1' : ''}`, {
    method: 'DELETE',
  });
}

export interface AdminModQuery {
  q?: string;
  status?: string;
  category?: string;
  author?: string;
  sort?: 'updated' | 'created' | 'downloads' | 'views' | 'subscribers';
  page?: number;
  pageSize?: number;
}

export function fetchAdminMods(params: AdminModQuery, signal?: AbortSignal): Promise<Paged<AdminModRow>> {
  return request<Paged<AdminModRow>>(`/api/admin/mods${buildQuery(params as Record<string, QueryValue>)}`, { signal });
}

export interface AdminModPatch {
  status?: string;
  category?: string;
  name?: string;
  summary?: string;
  description?: string;
  tags?: string;
  gameVersion?: string;
}

export function updateAdminMod(id: string, patch: AdminModPatch): Promise<{ mod: unknown }> {
  return request<{ mod: unknown }>(`/api/admin/mods/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: JSON.stringify(patch),
  });
}

/** 不带 force 时后端只做「下架」（status=hidden），带 force 才是真删除 */
export function deleteAdminMod(id: string, force = false): Promise<{ hidden?: boolean; deleted?: boolean }> {
  return request<{ hidden?: boolean; deleted?: boolean }>(
    `/api/admin/mods/${encodeURIComponent(id)}${force ? '?force=1' : ''}`,
    { method: 'DELETE' },
  );
}
