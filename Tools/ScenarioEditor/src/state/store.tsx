/**
 * 剧本编辑器状态中心
 *
 * 采用「不可变更新 + 结构共享」的方式管理剧本数据：
 * 每次修改只克隆被触及的节点，其余节点复用引用，因此
 * 1.7MB 的剧本也能以极低成本实现撤销 / 重做与脏数据对比。
 *
 * 同时提供：
 * - 撤销 / 重做栈（上限 60 步）
 * - 校验结果的延迟计算（useDeferredValue，避免阻塞输入）
 * - 武将库（武将源）加载
 */
import React, {
  createContext,
  useCallback,
  useContext,
  useDeferredValue,
  useEffect,
  useMemo,
  useReducer,
  useRef,
  useState,
} from 'react'
import type {
  BackupItem,
  CollectionKey,
  Entity,
  LibraryLibInfo,
  LibraryPerson,
  Options,
  Scenario,
  ScenarioInfo,
  ScenarioMeta,
  ValidationIssue,
} from '../lib/types'
import { COLLECTION_META } from '../lib/types'
import {
  ApiError,
  clearToken,
  fetchAuthState,
  fetchBackups,
  fetchLibrary,
  fetchOptions,
  fetchScenario,
  loginRequest,
  saveScenario,
  setToken,
  type AuthState,
} from '../lib/api'
import { countIssues, groupIssuesByEntity, validateScenario } from '../lib/validate'

/** 撤销栈上限 */
const HISTORY_LIMIT = 60

/** 加载状态 */
export type LoadStatus = 'idle' | 'loading' | 'ready' | 'error'

/** 内部状态 */
interface StoreState {
  status: LoadStatus
  error: string | null
  scenario: Scenario | null
  /** 最近一次保存时的快照，用于「还原全部修改」与脏数据对比 */
  saved: Scenario | null
  meta: ScenarioMeta | null
  options: Options | null
  past: Scenario[]
  future: Scenario[]
  /** 距离上次保存后累计的修改次数 */
  changeCount: number
}

/** 动作定义 */
type Action =
  | { type: 'LOADING' }
  | { type: 'LOAD_FAILED'; message: string }
  | { type: 'LOADED'; scenario: Scenario; meta: ScenarioMeta }
  | { type: 'OPTIONS_LOADED'; options: Options }
  | { type: 'MUTATE'; update: (scenario: Scenario) => Scenario }
  | { type: 'UNDO' }
  | { type: 'REDO' }
  | { type: 'DISCARD' }
  | { type: 'SAVED'; meta: ScenarioMeta; scenario: Scenario }
  | { type: 'META_CHANGED'; meta: ScenarioMeta }

/** 初始状态 */
const initialState: StoreState = {
  status: 'idle',
  error: null,
  scenario: null,
  saved: null,
  meta: null,
  options: null,
  past: [],
  future: [],
  changeCount: 0,
}

/**
 * 状态归约器（必须保持纯函数）。
 *
 * @param state 当前状态
 * @param action 动作
 * @returns 新状态
 */
function reducer(state: StoreState, action: Action): StoreState {
  switch (action.type) {
    case 'LOADING':
      return { ...state, status: 'loading', error: null }
    case 'LOAD_FAILED':
      return { ...state, status: 'error', error: action.message }
    case 'LOADED':
      return {
        ...state,
        status: 'ready',
        error: null,
        scenario: action.scenario,
        saved: action.scenario,
        meta: action.meta,
        past: [],
        future: [],
        changeCount: 0,
      }
    case 'OPTIONS_LOADED':
      return { ...state, options: action.options }
    case 'MUTATE': {
      if (!state.scenario) return state
      const next = action.update(state.scenario)
      if (next === state.scenario) return state
      const past = [...state.past, state.scenario]
      if (past.length > HISTORY_LIMIT) past.splice(0, past.length - HISTORY_LIMIT)
      return {
        ...state,
        scenario: next,
        past,
        future: [],
        changeCount: state.changeCount + 1,
      }
    }
    case 'UNDO': {
      if (state.past.length === 0 || !state.scenario) return state
      const past = [...state.past]
      const prev = past.pop() as Scenario
      return {
        ...state,
        scenario: prev,
        past,
        future: [state.scenario, ...state.future],
        changeCount: Math.max(0, state.changeCount - 1),
      }
    }
    case 'REDO': {
      if (state.future.length === 0 || !state.scenario) return state
      const [next, ...rest] = state.future
      return {
        ...state,
        scenario: next,
        past: [...state.past, state.scenario],
        future: rest,
        changeCount: state.changeCount + 1,
      }
    }
    case 'DISCARD':
      if (!state.saved) return state
      return { ...state, scenario: state.saved, past: [], future: [], changeCount: 0 }
    case 'SAVED':
      return { ...state, scenario: action.scenario, saved: action.scenario, meta: action.meta, changeCount: 0 }
    case 'META_CHANGED':
      return { ...state, meta: action.meta }
    default:
      return state
  }
}

/** 批量编辑描述：字段 -> 新值（null 表示保持不变） */
export interface BatchPatch {
  [key: string]: number | string | number[] | null
}

/** 上下文暴露的 API */
export interface ScenarioStoreApi {
  status: LoadStatus
  error: string | null
  scenario: Scenario | null
  saved: Scenario | null
  meta: ScenarioMeta | null
  options: Options | null
  library: LibraryPerson[]
  librarySource: string
  /** 分库信息（基础 / 自建），用于在导入界面分组展示 */
  libraryLibs: LibraryLibInfo[]
  libraryLoading: boolean
  libraryError: string | null
  /** 登录状态（含权限），未取到时为 null */
  auth: AuthState | null
  /** 工作区是否为「还没有剧本」的初始状态（多用户模式首次进入） */
  needInit: boolean
  dirty: boolean
  canUndo: boolean
  canRedo: boolean
  changeCount: number
  saving: boolean
  issues: ValidationIssue[]
  issueCounts: { error: number; warning: number; info: number }
  issuesByEntity: Map<string, ValidationIssue[]>
  backups: BackupItem[]

  load: () => Promise<void>
  reload: () => Promise<void>
  save: () => Promise<void>
  undo: () => void
  redo: () => void
  discardAll: () => void
  refreshBackups: () => Promise<void>
  loadLibrary: (refresh?: boolean) => Promise<void>
  /** 重新拉取登录状态 */
  refreshAuth: () => Promise<void>
  /** 用武将库网站账号登录 */
  login: (username: string, password: string) => Promise<void>
  /** 退出登录 */
  logout: () => void

  patchRoot: (patch: Record<string, unknown>) => void
  patchInfo: (patch: Partial<ScenarioInfo>) => void
  patchEntity: (collection: CollectionKey, id: number, patch: Record<string, unknown>) => void
  batchPatchEntities: (collection: CollectionKey, ids: number[], patch: BatchPatch) => void
  replaceEntity: (collection: CollectionKey, id: number, entity: Entity) => void
  /** 按自定义规则批量更新实体（用于「设为 / 增加 / 减少」这类需要逐条计算的批量编辑） */
  batchUpdate: (collection: CollectionKey, ids: number[], updater: (entity: Entity) => Entity) => void
  addEntity: (collection: CollectionKey, entity: Entity) => number
  addEntities: (collection: CollectionKey, entities: Entity[]) => number[]
  removeEntities: (collection: CollectionKey, ids: number[]) => void
  nextEntityId: (collection: CollectionKey) => number
}

const StoreContext = createContext<ScenarioStoreApi | null>(null)

/**
 * 计算集合中下一个可用的 Id：**优先使用最小的空闲 Id**。
 *
 * 之前用「最大 Id + 1」，删除后留下的空缺会被永久浪费。改成从 1 起找第一个未占用的 Id，
 * 这样删除军团 / 势力后空出的 Id 会立刻回到可分配池里。
 *
 * @param scenario 剧本
 * @param collection 集合键
 * @returns 新 Id
 */
export function computeNextId(scenario: Scenario, collection: CollectionKey): number {
  const set = (scenario[collection] || {}) as Record<string, Entity>
  const used = new Set<number>()
  for (const item of Object.values(set)) {
    const n = Number(item?.Id)
    if (Number.isFinite(n)) used.add(n)
  }
  let candidate = 1
  while (used.has(candidate)) candidate += 1
  return candidate
}

/** 不允许新增 / 删除的集合：武将与城池由数据源决定，误删会破坏剧本 */
export const READONLY_COLLECTIONS: ReadonlySet<CollectionKey> = new Set<CollectionKey>(['personSet', 'citySet'])

/**
 * 生成替换某个集合的新剧本对象（利用结构共享）。
 *
 * @param scenario 剧本
 * @param collection 集合键
 * @param set 新的集合对象
 * @returns 新剧本对象
 */
function withCollection(scenario: Scenario, collection: CollectionKey, set: Record<string, Entity>): Scenario {
  return { ...scenario, [collection]: set }
}

/** 状态提供者 */
export function ScenarioProvider({ children }: { children: React.ReactNode }) {
  const [state, dispatch] = useReducer(reducer, initialState)
  const [library, setLibrary] = useState<LibraryPerson[]>([])
  const [librarySource, setLibrarySource] = useState('')
  const [libraryLibs, setLibraryLibs] = useState<LibraryLibInfo[]>([])
  const [auth, setAuth] = useState<AuthState | null>(null)
  const [needInit, setNeedInit] = useState(false)
  const [libraryLoading, setLibraryLoading] = useState(false)
  const [libraryError, setLibraryError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [backups, setBackups] = useState<BackupItem[]>([])
  const libraryRequested = useRef(false)

  const { scenario, options, saved, meta } = state

  /* ---------------- 加载 ---------------- */

  const load = useCallback(async () => {
    dispatch({ type: 'LOADING' })
    try {
      const [{ scenario: sc, meta: mt, needInit: init }, opts] = await Promise.all([
        fetchScenario(),
        fetchOptions(),
      ])
      dispatch({ type: 'OPTIONS_LOADED', options: opts })
      if (init || !sc || !mt) {
        // 工作区还没有剧本：引导去模板库挑起点，复用错误页但给可操作的按钮
        setNeedInit(true)
        dispatch({ type: 'LOAD_FAILED', message: '当前工作区还没有剧本，请从模板库选择一个模板作为起点' })
        return
      }
      setNeedInit(false)
      dispatch({ type: 'LOADED', scenario: sc, meta: mt })
    } catch (e) {
      const message =
        e instanceof ApiError
          ? e.code === 'ENETWORK'
            ? '无法连接后端服务，请先在 Tools/ScenarioEditor 目录执行 npm run server 启动服务'
            : e.message
          : (e as Error).message
      dispatch({ type: 'LOAD_FAILED', message })
    }
  }, [])

  const reload = useCallback(async () => {
    await load()
  }, [load])

  useEffect(() => {
    void load()
  }, [load])

  /* ---------------- 保存 ---------------- */

  const save = useCallback(async () => {
    if (!scenario || !meta) return
    setSaving(true)
    try {
      const result = await saveScenario(scenario, meta.revision)
      dispatch({ type: 'SAVED', meta: result.meta, scenario })
      void fetchBackups().then(setBackups).catch(() => undefined)
    } finally {
      setSaving(false)
    }
  }, [scenario, meta])

  const refreshBackups = useCallback(async () => {
    try {
      setBackups(await fetchBackups())
    } catch {
      setBackups([])
    }
  }, [])

  /* ---------------- 武将库 ---------------- */

  const loadLibrary = useCallback(async (refresh = false) => {
    setLibraryLoading(true)
    setLibraryError(null)
    try {
      const data = await fetchLibrary(refresh)
      setLibrary(data.persons)
      setLibrarySource(data.source)
      setLibraryLibs(data.libs ?? [])
      if (data.error) setLibraryError(data.error)
    } catch (e) {
      setLibraryError((e as Error).message)
      setLibrary([])
      setLibraryLibs([])
    } finally {
      setLibraryLoading(false)
    }
  }, [])

  useEffect(() => {
    if (libraryRequested.current) return
    libraryRequested.current = true
    void loadLibrary()
  }, [loadLibrary])

  /* ---------------- 登录状态 ---------------- */

  const refreshAuth = useCallback(async () => {
    try {
      setAuth(await fetchAuthState())
    } catch {
      // 认证接口不可用时退化为「单机、可写、非管理员」，界面照常可用
      setAuth({
        authAvailable: false,
        multiUser: false,
        user: null,
        canWrite: true,
        canAdmin: true,
      })
    }
  }, [])

  const login = useCallback(
    async (username: string, password: string) => {
      const data = await loginRequest(username, password)
      setToken(data.token)
      setAuth({
        authAvailable: true,
        multiUser: true,
        user: data.user,
        canWrite: true,
        canAdmin: Boolean(data.user && data.user.isAdmin),
      })
      // 登录后数据源会从「只读游客」切到「自己的工作区」，必须整体重载
      await reload()
    },
    [reload],
  )

  const logout = useCallback(() => {
    clearToken()
    void refreshAuth()
    void reload()
  }, [refreshAuth, reload])

  useEffect(() => {
    void refreshAuth()
  }, [refreshAuth])

  /* ---------------- 修改操作 ---------------- */

  const mutate = useCallback((update: (s: Scenario) => Scenario) => {
    dispatch({ type: 'MUTATE', update })
  }, [])

  const patchRoot = useCallback(
    (patch: Record<string, unknown>) => {
      mutate((s) => ({ ...s, ...patch }))
    },
    [mutate]
  )

  const patchInfo = useCallback(
    (patch: Partial<ScenarioInfo>) => {
      mutate((s) => ({ ...s, Info: { ...s.Info, ...patch } }))
    },
    [mutate]
  )

  const patchEntity = useCallback(
    (collection: CollectionKey, id: number, patch: Record<string, unknown>) => {
      mutate((s) => {
        const set = s[collection] as Record<string, Entity>
        const key = String(id)
        const current = set[key]
        if (!current) return s
        let changed = false
        for (const k of Object.keys(patch)) {
          if (current[k] !== patch[k]) {
            changed = true
            break
          }
        }
        if (!changed) return s
        return withCollection(s, collection, { ...set, [key]: { ...current, ...patch } })
      })
    },
    [mutate]
  )

  const batchPatchEntities = useCallback(
    (collection: CollectionKey, ids: number[], patch: BatchPatch) => {
      const keys = Object.keys(patch).filter((k) => patch[k] !== null && patch[k] !== undefined)
      if (keys.length === 0 || ids.length === 0) return
      const idSet = new Set(ids.map((x) => String(x)))
      mutate((s) => {
        const set = s[collection] as Record<string, Entity>
        const next: Record<string, Entity> = { ...set }
        let touched = false
        for (const key of idSet) {
          const current = next[key]
          if (!current) continue
          const merged: Entity = { ...current }
          for (const k of keys) {
            const v = patch[k]
            merged[k] = Array.isArray(v) ? [...v] : (v as number | string)
          }
          next[key] = merged
          touched = true
        }
        if (!touched) return s
        return withCollection(s, collection, next)
      })
    },
    [mutate]
  )

  const replaceEntity = useCallback(
    (collection: CollectionKey, id: number, entity: Entity) => {
      mutate((s) => {
        const set = s[collection] as Record<string, Entity>
        return withCollection(s, collection, { ...set, [String(id)]: entity })
      })
    },
    [mutate]
  )

  const batchUpdate = useCallback(
    (collection: CollectionKey, ids: number[], updater: (entity: Entity) => Entity) => {
      if (ids.length === 0) return
      const idSet = new Set(ids.map((x) => String(x)))
      mutate((s) => {
        const set = s[collection] as Record<string, Entity>
        const next: Record<string, Entity> = { ...set }
        let touched = false
        for (const key of idSet) {
          const current = next[key]
          if (!current) continue
          const updated = updater(current)
          if (updated !== current) {
            next[key] = updated
            touched = true
          }
        }
        if (!touched) return s
        return withCollection(s, collection, next)
      })
    },
    [mutate]
  )

  const addEntity = useCallback(
    (collection: CollectionKey, entity: Entity) => {
      if (READONLY_COLLECTIONS.has(collection)) return 0
      let newId = Number(entity.Id)
      mutate((s) => {
        newId = Number(entity.Id) || computeNextId(s, collection)
        const set = s[collection] as Record<string, Entity>
        return withCollection(s, collection, { ...set, [String(newId)]: { ...entity, Id: newId } })
      })
      return newId
    },
    [mutate]
  )

  const addEntities = useCallback(
    (collection: CollectionKey, entities: Entity[]) => {
      const ids: number[] = []
      mutate((s) => {
        const set = { ...(s[collection] as Record<string, Entity>) }
        let cursor = computeNextId(s, collection)
        for (const entity of entities) {
          const id = Number(entity.Id) > 0 && !set[String(entity.Id)] ? Number(entity.Id) : cursor++
          set[String(id)] = { ...entity, Id: id }
          ids.push(id)
        }
        return withCollection(s, collection, set)
      })
      return ids
    },
    [mutate]
  )

  const removeEntities = useCallback(
    (collection: CollectionKey, ids: number[]) => {
      if (ids.length === 0) return
      if (READONLY_COLLECTIONS.has(collection)) return
      const idSet = new Set(ids.map((x) => String(x)))
      mutate((s) => {
        const set = s[collection] as Record<string, Entity>
        const next: Record<string, Entity> = {}
        let removed = 0
        for (const [k, v] of Object.entries(set)) {
          if (idSet.has(k)) {
            removed += 1
            continue
          }
          next[k] = v
        }
        if (removed === 0) return s
        return withCollection(s, collection, next)
      })
    },
    [mutate]
  )

  const nextEntityId = useCallback(
    (collection: CollectionKey) => (scenario ? computeNextId(scenario, collection) : 1),
    [scenario]
  )

  /* ---------------- 校验（延迟计算） ---------------- */

  const deferredScenario = useDeferredValue(scenario)
  const issues = useMemo(() => validateScenario(deferredScenario, options), [deferredScenario, options])
  const issueCounts = useMemo(() => countIssues(issues), [issues])
  const issuesByEntity = useMemo(() => groupIssuesByEntity(issues), [issues])
  const validating = deferredScenario !== scenario

  /* ---------------- 派生 ---------------- */

  const dirty = state.changeCount > 0

  const value = useMemo<ScenarioStoreApi>(
    () => ({
      status: state.status,
      error: state.error,
      scenario,
      saved,
      meta,
      options,
      library,
      librarySource,
      libraryLibs,
      libraryLoading,
      libraryError,
      auth,
      needInit,
      dirty,
      canUndo: state.past.length > 0,
      canRedo: state.future.length > 0,
      changeCount: state.changeCount,
      saving,
      issues,
      issueCounts,
      issuesByEntity,
      backups,
      load,
      reload,
      save,
      undo: () => dispatch({ type: 'UNDO' }),
      redo: () => dispatch({ type: 'REDO' }),
      discardAll: () => dispatch({ type: 'DISCARD' }),
      refreshBackups,
      loadLibrary,
      refreshAuth,
      login,
      logout,
      patchRoot,
      patchInfo,
      patchEntity,
      batchPatchEntities,
      replaceEntity,
      batchUpdate,
      addEntity,
      addEntities,
      removeEntities,
      nextEntityId,
    }),
    [
      state.status,
      state.error,
      state.past.length,
      state.future.length,
      scenario,
      saved,
      meta,
      options,
      library,
      librarySource,
      libraryLibs,
      libraryLoading,
      libraryError,
      auth,
      needInit,
      dirty,
      state.changeCount,
      saving,
      issues,
      issueCounts,
      issuesByEntity,
      backups,
      load,
      reload,
      save,
      refreshBackups,
      loadLibrary,
      refreshAuth,
      login,
      logout,
      patchRoot,
      patchInfo,
      patchEntity,
      batchPatchEntities,
      replaceEntity,
      batchUpdate,
      addEntity,
      addEntities,
      removeEntities,
      nextEntityId,
    ]
  )

  // 校验进行中提示（通过 data 属性暴露给界面）
  void validating

  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>
}

/**
 * 读取状态中心。
 *
 * @returns 状态 API
 */
export function useScenarioStore(): ScenarioStoreApi {
  const ctx = useContext(StoreContext)
  if (!ctx) throw new Error('useScenarioStore 必须在 ScenarioProvider 内部使用')
  return ctx
}

/** 集合中文名快捷方法 */
export function collectionLabel(collection: CollectionKey): string {
  return COLLECTION_META[collection].label
}
