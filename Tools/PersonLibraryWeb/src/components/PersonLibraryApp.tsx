/**
 * 文件名：PersonLibraryApp.tsx
 * 描述：武将库主界面。负责数据加载、库切换（基础武将库 / 自建武将库）、
 *       检索筛选、列表展示，以及新建 / 编辑 / 删除 / 下载等操作入口。
 *       - 基础武将库：ID 与游戏本体绑定，不可修改、不可删除，其它字段可修改；
 *       - 自建武将库：可新建、修改、删除，ID 自 1000 起固定分配。
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Toaster } from '@/components/ui/sonner'
import { PersonCard } from '@/components/PersonCard'
import { PersonFormDialog } from '@/components/PersonFormDialog'
import { LoginDialog } from '@/components/LoginDialog'
import { AccountManagerDialog } from '@/components/AccountManagerDialog'
import { CustomFaceDialog } from '@/components/CustomFaceDialog'
import { ImportPersonDialog, type ExistingPersonRef } from '@/components/ImportPersonDialog'
import { BulkImportDialog } from '@/components/BulkImportDialog'
import { ExportOffsetDialog } from '@/components/ExportOffsetDialog'
import { ExportModDialog } from '@/components/ExportModDialog'
import { SCENARIO_URL, WORKSHOP_URL } from '@/lib/siteLinks'
import { BackupRestoreDialog } from '@/components/BackupRestoreDialog'
import { buildNameIndex, buildPersonOptions } from '@/components/PersonRefPicker'
import { ABILITY_FIELDS, PERSON_TYPE_CHOICES, SEX_CHOICES, statBaseValue } from '@/lib/enums'
import {
  ApiError,
  clearPersons,
  deletePerson,
  deletePersons,
  downloadLibrary,
  fetchCurrentUser,
  fetchCustomFaces,
  fetchLibrary,
  fetchOptions,
  fetchReferenceNames,
  logout,
  setToken,
} from '@/lib/api'
import { cn } from '@/lib/utils'
import type {
  AuthUser,
  CustomFaceResult,
  HeadRange,
  LibraryCapabilities,
  LibraryKey,
  OptionsConfig,
  Permission,
  Person,
  RegisterInfo,
  Role,
  RoleOption,
} from '@/lib/types'
import {
  BookUser,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  DatabaseBackup,
  Download,
  Eye,
  Boxes,
  ExternalLink,
  FileUp,
  Layers,
  Loader2,
  LogIn,
  LogOut,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
  Sparkles,
  Trash2,
  UserCog,
  Users,
  X,
} from 'lucide-react'

/** 排序方式：id=ID 升序，idDesc=ID 倒序，updated=最后修改时间（最近优先），name=姓名，ability=能力合计 */
type SortKey = 'id' | 'idDesc' | 'updated' | 'name' | 'ability'

/** 列表默认每页条数（分页渲染，避免一次渲染上千张卡片导致卡顿） */
const DEFAULT_PAGE_SIZE = 60

/** 列表可选的每页条数 */
const PAGE_SIZE_OPTIONS = [30, 60, 120, 240]

/**
 * 按最后修改时间排序：最近修改的排前面。
 * 服务端写入的是 ISO 8601 UTC 字符串，字典序与时间先后一致，可直接比较；
 * 没有修改时间的武将排在最后，彼此之间按 ID 倒序（新武将优先）。
 * @param a 武将 A
 * @param b 武将 B
 * @returns 排序结果
 */
function compareByUpdatedAt(a: Person, b: Person): number {
  const av = (a.updatedAt ?? '').trim()
  const bv = (b.updatedAt ?? '').trim()
  if (!av && !bv) return b.Id - a.Id
  if (!av) return 1
  if (!bv) return -1
  return bv.localeCompare(av)
}

/** 库切换选项 */
const LIB_TABS: { key: LibraryKey; label: string; desc: string; file: string }[] = [
  { key: 'custom', label: '自建武将库', desc: '可新建 / 修改 / 删除', file: 'CustomPerson.json' },
  { key: 'base', label: '基础武将库', desc: '本体数据，可修改 / 一键导入', file: 'PersonLibrary.json' },
]

/** 默认能力（加载完成前，保守地按只读处理） */
const DEFAULT_CAPABILITIES: LibraryCapabilities = {
  create: false,
  edit: false,
  delete: false,
  bulkImport: false,
  readOnlyId: true,
  enforceUniqueName: true,
}

/**
 * 角色显示名。
 * @param roles 角色选项列表
 * @param role 角色标识
 * @returns 显示名称（未知角色回退为标识本身）
 */
function roleLabelOf(roles: RoleOption[], role: Role): string {
  return roles.find((r) => r.id === role)?.name || role
}

export function PersonLibraryApp() {
  /** 当前库标识 */
  const [lib, setLib] = useState<LibraryKey>('custom')
  /** 当前库显示名称 */
  const [libLabel, setLibLabel] = useState('自建武将库')
  const [persons, setPersons] = useState<Person[]>([])
  const [baseId, setBaseId] = useState(1000)
  const [nextId, setNextId] = useState(1000)
  /** 当前库武将的字段全集 */
  const [fieldKeys, setFieldKeys] = useState<string[]>([])
  /** 当前库允许的操作 */
  const [capabilities, setCapabilities] = useState<LibraryCapabilities>(DEFAULT_CAPABILITIES)
  /** 当前库的头像区间 */
  const [headRanges, setHeadRanges] = useState<HeadRange[]>([])
  /** 当前库的容器级元数据（如新版本体数据的 type: 2），读取与写回均原样保留 */
  const [containerExtras, setContainerExtras] = useState<Record<string, unknown>>({})

  const [options, setOptions] = useState<OptionsConfig | null>(null)
  const [referenceNames, setReferenceNames] = useState<Record<string, string>>({})

  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')

  /** 当前登录用户；为 null 表示游客（未登录，仅可浏览） */
  const [user, setUser] = useState<AuthUser | null>(null)
  /** 当前身份具备的权限集合 */
  const [permissions, setPermissions] = useState<Permission[]>([])
  /** 可选角色列表 */
  const [roles, setRoles] = useState<RoleOption[]>([])
  /** 玩家注册状态（后端下发，决定登录弹窗是否展示注册标签页） */
  const [registerInfo, setRegisterInfo] = useState<RegisterInfo | null>(null)
  /** 登录弹窗 */
  const [loginOpen, setLoginOpen] = useState(false)
  /** 账号管理弹窗（仅管理员） */
  const [accountOpen, setAccountOpen] = useState(false)
  /** 备份与还原弹窗（仅超级管理员） */
  const [backupOpen, setBackupOpen] = useState(false)
  /** 自制头像制作弹窗（主界面独立入口） */
  const [customFaceOpen, setCustomFaceOpen] = useState(false)
  /** 武将批量导入弹窗 */
  const [importOpen, setImportOpen] = useState(false)
  /** 服务端已有的自制头像列表 */
  const [customFaces, setCustomFaces] = useState<CustomFaceResult | null>(null)

  const [keyword, setKeyword] = useState('')
  const [sexFilter, setSexFilter] = useState<string>('all')
  const [typeFilter, setTypeFilter] = useState<string>('all')
  /** 标签筛选：all=不限，__none__=仅未打标签的武将，其它为指定标签 */
  const [tagFilter, setTagFilter] = useState<string>('all')
  const [sortKey, setSortKey] = useState<SortKey>('id')

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<Person | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<Person | null>(null)
  const [deleting, setDeleting] = useState(false)

  /** 批量删除模式（卡片显示勾选框，点击卡片为勾选） */
  const [batchMode, setBatchMode] = useState(false)
  /** 批量删除模式下已勾选的武将 ID */
  const [batchSelected, setBatchSelected] = useState<Set<number>>(() => new Set())
  /** 批量删除确认弹窗 */
  const [batchDeleteOpen, setBatchDeleteOpen] = useState(false)
  const [batchDeleting, setBatchDeleting] = useState(false)
  /** 清空整库确认弹窗 */
  const [clearAllOpen, setClearAllOpen] = useState(false)
  const [clearing, setClearing] = useState(false)
  /** 基础武将库一键导入（整体导入）弹窗 */
  const [bulkImportOpen, setBulkImportOpen] = useState(false)
  /** 自建库偏移导出弹窗（把 ID 平移到目标号段后导出） */
  const [offsetExportOpen, setOffsetExportOpen] = useState(false)
  /** 生成武将模组包弹窗（连同自制头像导出，用于在创意工坊发布） */
  const [exportModOpen, setExportModOpen] = useState(false)

  /** 列表分页：当前页码（从 1 开始）与每页条数 */
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE)
  /** 列表容器（翻页后滚回列表顶部） */
  const listTopRef = useRef<HTMLDivElement>(null)

  /**
   * 加载指定武将库数据。
   * 同时刷新「基础武将库姓名索引」：基础武将改名后，自建库的重名判断
   * 与关系武将姓名展示必须立刻使用新姓名，否则会按旧姓名判重。
   */
  const loadLibrary = useCallback(async (key: LibraryKey) => {
    try {
      const [data, refs] = await Promise.all([
        fetchLibrary(key),
        fetchReferenceNames().catch(() => null),
      ])
      // 索引接口异常时保留现有索引，不影响库数据的展示
      if (refs) setReferenceNames(refs)
      setPersons(data.persons)
      setBaseId(data.baseId ?? data.offset ?? 1000)
      setNextId(data.nextId)
      setLibLabel(data.label)
      setFieldKeys(data.fieldKeys ?? [])
      setCapabilities(data.capabilities ?? DEFAULT_CAPABILITIES)
      setHeadRanges(data.headRanges ?? [])
      setContainerExtras(data.containerExtras ?? {})
      setLoadError('')
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : '加载失败')
    }
  }, [])

  // 首屏加载编辑选项（基础武将库姓名索引随库数据一起加载，见 loadLibrary）
  useEffect(() => {
    let mounted = true
    ;(async () => {
      const opts = await fetchOptions().catch(() => null)
      if (!mounted) return
      setOptions(
        opts ?? {
          features: [],
          personalities: [],
          argumentations: [],
          attributeChangeTypes: [],
          abilityLevels: [],
          headRanges: [],
          personTypes: [],
        },
      )
    })()
    return () => {
      mounted = false
    }
  }, [])

  // 搜索 / 筛选 / 排序 / 每页条数 / 库切换时回到第一页，避免停留在越界页码
  useEffect(() => {
    setPage(1)
  }, [keyword, sexFilter, typeFilter, tagFilter, sortKey, pageSize, lib])

  // 切换库时重新加载数据并重置筛选条件
  useEffect(() => {
    let mounted = true
    ;(async () => {
      setLoading(true)
      setKeyword('')
      setSexFilter('all')
      setTypeFilter('all')
      setTagFilter('all')
      // 排序方式依赖库（按修改时间仅自建库可用），切换库时回到默认的 ID 升序
      setSortKey('id')
      setDeleteTarget(null)
      setEditing(null)
      setBatchMode(false)
      setBatchSelected(new Set())
      await loadLibrary(lib)
      if (mounted) setLoading(false)
    })()
    return () => {
      mounted = false
    }
  }, [lib, loadLibrary])

  /** 加载当前登录身份（未登录时返回游客权限） */
  const loadIdentity = useCallback(async () => {
    try {
      const data = await fetchCurrentUser()
      setUser(data.user)
      setPermissions(data.permissions ?? [])
      setRoles(data.roles ?? [])
      setRegisterInfo(data.register ?? null)
    } catch {
      // 身份接口不可用时按游客处理（最小权限）
      setUser(null)
      setPermissions(['read'])
      setRoles([])
    }
  }, [])

  // 首屏加载登录身份
  useEffect(() => {
    loadIdentity()
  }, [loadIdentity])

  /** 加载服务端已有的自制头像列表 */
  const loadCustomFaces = useCallback(async () => {
    try {
      const data = await fetchCustomFaces()
      setCustomFaces(data)
      return data
    } catch {
      // 自制头像接口异常时不影响主流程，仅不展示相关数据
      setCustomFaces(null)
      return null
    }
  }, [])

  // 首屏加载自制头像列表
  useEffect(() => {
    loadCustomFaces()
  }, [loadCustomFaces])

  /** 是否具备写入权限（游客为 false，只能浏览） */
  const canWrite = permissions.includes('write')
  /** 是否具备账号管理权限（仅管理员） */
  const canManageAccounts = permissions.includes('account')
  /** 是否具备备份还原权限（仅超级管理员） */
  const canManageBackups = permissions.includes('backup')

  /**
   * 统一的写操作异常提示。
   * 登录态失效（401）时清除本地令牌并弹出登录框。
   * @param err 异常对象
   * @param fallback 兜底提示
   */
  const reportWriteError = useCallback(
    (err: unknown, fallback: string) => {
      if (err instanceof ApiError && err.status === 401) {
        setToken(null)
        setUser(null)
        setPermissions(['read'])
        setLoginOpen(true)
        toast.error('登录状态已失效，请重新登录')
        return
      }
      toast.error(err instanceof Error ? err.message : fallback)
    },
    [],
  )

  /** 登录成功后刷新身份与当前库数据（能力开关随身份变化） */
  const handleLoginSuccess = useCallback(
    async (loggedIn: AuthUser) => {
      setUser(loggedIn)
      await loadIdentity()
      await loadLibrary(lib)
      toast.success(
        `已登录：${loggedIn.displayName || loggedIn.username}（${roleLabelOf(roles, loggedIn.role)}）`,
      )
    },
    [lib, loadIdentity, loadLibrary, roles],
  )

  /** 退出登录，回到游客只读状态 */
  const handleLogout = useCallback(async () => {
    await logout()
    await loadIdentity()
    await loadLibrary(lib)
    setAccountOpen(false)
    toast.success('已退出登录，当前为游客身份（仅可浏览）')
  }, [lib, loadIdentity, loadLibrary])

  /** 人际关系的候选武将列表 */
  const personOptions = useMemo(
    () => buildPersonOptions(persons, referenceNames),
    [persons, referenceNames],
  )

  /** 武将姓名索引 */
  const nameIndex = useMemo(
    () => buildNameIndex(persons, referenceNames),
    [persons, referenceNames],
  )

  /** 基础武将库全部姓名（自建库跨库查重用） */
  const baseNames = useMemo(() => Object.values(referenceNames), [referenceNames])

  /**
   * 当前库既有武将（Id + 姓名）。
   * 导入预览据此判定「与自建武将重名」（可选择覆盖）还是「与基础武将重名」（只能改名），
   * 缓存后传入可避免每次渲染都重建数组导致导入计划重算。
   */
  const existingPersons = useMemo<ExistingPersonRef[]>(
    () => persons.map((p) => ({ id: p.Id, name: p.Name })),
    [persons],
  )

  /** 编辑弹窗使用的选项配置（头像区间以当前库为准） */
  const formOptions = useMemo<OptionsConfig | null>(() => {
    if (!options) return null
    return { ...options, headRanges: headRanges.length ? headRanges : options.headRanges }
  }, [options, headRanges])

  /** 当前库中使用中的标签（按使用次数降序，用于标签筛选下拉与标签编辑候选） */
  const tagOptions = useMemo(() => {
    const counter = new Map<string, number>()
    for (const p of persons) {
      for (const tag of p.tags ?? []) counter.set(tag, (counter.get(tag) ?? 0) + 1)
    }
    return [...counter.entries()].sort(
      (a, b) => b[1] - a[1] || a[0].localeCompare(b[0], 'zh-Hans-CN'),
    )
  }, [persons])

  /** 检索 / 筛选 / 排序后的列表 */
  const visiblePersons = useMemo(() => {
    const kw = keyword.trim().toLowerCase()
    let list = persons.filter((p) => {
      if (sexFilter !== 'all' && String(p.sex) !== sexFilter) return false
      if (typeFilter !== 'all' && String(p.type ?? 0) !== typeFilter) return false
      // 标签筛选：无标签选项只保留未打标签的武将
      const tags = p.tags ?? []
      if (tagFilter === '__none__' && tags.length > 0) return false
      if (tagFilter !== 'all' && tagFilter !== '__none__' && !tags.includes(tagFilter)) return false
      if (!kw) return true
      const name = (p.Name || `${p.familyName}${p.giveName}`).toLowerCase()
      return (
        name.includes(kw) ||
        (p.nickName || '').toLowerCase().includes(kw) ||
        String(p.Id).includes(kw) ||
        // 关键字同样匹配标签，便于一次搜索多个相关武将
        tags.some((tag) => tag.toLowerCase().includes(kw))
      )
    })
    list = [...list]
    if (sortKey === 'id') list.sort((a, b) => a.Id - b.Id)
    if (sortKey === 'idDesc') list.sort((a, b) => b.Id - a.Id)
    if (sortKey === 'updated') list.sort(compareByUpdatedAt)
    if (sortKey === 'name') list.sort((a, b) => (a.Name || '').localeCompare(b.Name || '', 'zh-Hans-CN'))
    if (sortKey === 'ability')
      list.sort(
        (a, b) =>
          ABILITY_FIELDS.reduce((s, f) => s + statBaseValue(b[f.key]), 0) -
          ABILITY_FIELDS.reduce((s, f) => s + statBaseValue(a[f.key]), 0),
      )
    return list
  }, [persons, keyword, sexFilter, typeFilter, tagFilter, sortKey])

  /** 列表总页数（至少 1 页） */
  const totalPages = Math.max(1, Math.ceil(visiblePersons.length / pageSize))

  /** 当前页码（裁剪到合法范围，避免筛选后越界） */
  const currentPage = Math.min(page, totalPages)

  /** 当前页展示的武将：分页渲染，避免一次渲染上千张卡片导致卡顿 */
  const pagePersons = useMemo(
    () => visiblePersons.slice((currentPage - 1) * pageSize, currentPage * pageSize),
    [visiblePersons, currentPage, pageSize],
  )

  /**
   * 切换列表页码：裁剪到合法范围，并滚动回列表顶部。
   * @param next 目标页码
   */
  const goToPage = (next: number) => {
    setPage(Math.min(Math.max(1, next), totalPages))
    listTopRef.current?.scrollIntoView({ block: 'start', behavior: 'smooth' })
  }

  /** 打开新建弹窗（需写入权限，游客会被拦截并提示登录） */
  const openCreate = () => {
    if (!canWrite) {
      setLoginOpen(true)
      toast.info('游客仅可浏览，登录后才能新建武将')
      return
    }
    setEditing(null)
    setFormOpen(true)
  }

  /** 打开武将详情：有写入权限时为编辑，游客为只读查看 */
  const openPerson = (person: Person) => {
    setEditing(person)
    setFormOpen(true)
  }

  /**
   * 从「头像引用武将」面板打开某位武将的详情。
   * 目标武将可能属于另一个库，此时先切库并等库数据就绪再打开详情，
   * 否则编辑弹窗里的关系武将列表仍是旧库的。
   * @param libKey 目标库标识（base / custom）
   * @param personId 武将 ID
   */
  const openPersonFromFace = async (libKey: string, personId: number) => {
    const key: LibraryKey = libKey === 'base' ? 'base' : 'custom'
    try {
      setCustomFaceOpen(false)
      let target: Person | undefined
      if (key === lib) {
        target = persons.find((p) => p.Id === personId)
      } else {
        const data = await fetchLibrary(key)
        target = data.persons.find((p) => p.Id === personId)
        setLib(key)
        await loadLibrary(key)
      }
      if (!target) {
        toast.error(`未找到 ID 为 ${personId} 的武将`)
        return
      }
      setEditing(target)
      setFormOpen(true)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : '打开武将失败')
    }
  }

  /** 确认删除 */
  const confirmDelete = async () => {
    if (!deleteTarget) return
    if (!canWrite) {
      setDeleteTarget(null)
      reportWriteError(new ApiError(403, '游客仅可浏览，无权删除武将'), '删除失败')
      return
    }
    setDeleting(true)
    try {
      await deletePerson(deleteTarget.Id, lib)
      toast.success(`已删除武将「${deleteTarget.Name || '未命名'}」`)
      setDeleteTarget(null)
      await loadLibrary(lib)
    } catch (err) {
      reportWriteError(err, '删除失败')
    } finally {
      setDeleting(false)
    }
  }

  /**
   * 勾选 / 取消勾选一位武将（批量删除模式）。
   * @param person 目标武将
   */
  const toggleBatchSelect = (person: Person) => {
    setBatchSelected((prev) => {
      const next = new Set(prev)
      if (next.has(person.Id)) next.delete(person.Id)
      else next.add(person.Id)
      return next
    })
  }

  /** 确认批量删除所选武将 */
  const confirmBatchDelete = async () => {
    const ids = [...batchSelected]
    if (ids.length === 0) return
    if (!canWrite) {
      setBatchDeleteOpen(false)
      reportWriteError(new ApiError(403, '游客仅可浏览，无权删除武将'), '批量删除失败')
      return
    }
    setBatchDeleting(true)
    try {
      const result = await deletePersons(ids, lib)
      toast.success(`已删除 ${result.deleted} 位武将`)
      setBatchDeleteOpen(false)
      setBatchSelected(new Set())
      await loadLibrary(lib)
    } catch (err) {
      reportWriteError(err, '批量删除失败')
    } finally {
      setBatchDeleting(false)
    }
  }

  /** 确认清空整库（不可撤销，服务端要求二次确认） */
  const confirmClearAll = async () => {
    if (!canWrite) {
      setClearAllOpen(false)
      reportWriteError(new ApiError(403, '游客仅可浏览，无权删除武将'), '清空失败')
      return
    }
    setClearing(true)
    try {
      const result = await clearPersons(lib)
      toast.success(`已清空 ${result.removed} 位武将`)
      setClearAllOpen(false)
      setBatchSelected(new Set())
      setBatchMode(false)
      await loadLibrary(lib)
    } catch (err) {
      reportWriteError(err, '清空失败')
    } finally {
      setClearing(false)
    }
  }

  /** 保存成功回调 */
  const handleSaved = async (person: Person) => {
    const name = person?.Name
    toast.success(`${editing ? '武将修改成功' : '武将创建成功'}${name ? `：${name}` : ''}`)
    await loadLibrary(lib)
  }

  /** 下载当前库的 JSON 文件 */
  const handleDownload = () => {
    const current = LIB_TABS.find((t) => t.key === lib)
    downloadLibrary(lib, current?.file ?? 'PersonLibrary.json')
    toast.success(`已开始下载 ${current?.file ?? 'PersonLibrary.json'}`)
  }

  return (
    <div className="app-shell min-h-screen">
      <Toaster position="top-center" richColors />

      {/* 顶部栏 */}
      <header className="sticky top-0 z-20 border-b border-border/80 bg-background/80 backdrop-blur">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6">
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-lg border border-primary/30 bg-primary/10">
              <BookUser className="size-5 text-primary" />
            </div>
            <div>
              <h1 className="font-display text-xl leading-tight tracking-wide">武将库</h1>
              <p className="text-[11px] text-muted-foreground">
                Sango Infinity · {libLabel}（{LIB_TABS.find((t) => t.key === lib)?.file}）
              </p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {/* 当前身份 */}
            <div className="flex items-center gap-2 rounded-lg border border-border bg-card/50 px-2.5 py-1.5">
              <span className="flex size-7 items-center justify-center rounded-full bg-secondary text-primary">
                {user ? <ShieldCheck className="size-4" /> : <Eye className="size-4" />}
              </span>
              <span className="flex flex-col leading-tight">
                <span className="text-xs font-medium">
                  {user ? user.displayName || user.username : '游客'}
                </span>
                <span className="text-[10px] text-muted-foreground">
                  {user ? roleLabelOf(roles, user.role) : '仅可浏览'}
                </span>
              </span>
            </div>

            <Button variant="ghost" size="icon" title="刷新" onClick={() => loadLibrary(lib)}>
              <RefreshCw />
            </Button>
            <Button variant="outline" onClick={handleDownload}>
              <Download />
              下载 JSON
            </Button>

            {/* 偏移导出（仅自建库：可把 ID 平移到目标 Mod 的号段） */}
            {lib === 'custom' && (
              <Button
                variant="outline"
                title="按指定起始 ID 导出（武将 ID 与关系字段同步平移）"
                onClick={() => setOffsetExportOpen(true)}
              >
                <Download />
                偏移导出
              </Button>
            )}

            {capabilities.create && (
              <Button
                variant="outline"
                title="从 CustomPerson.json 批量导入武将"
                onClick={() => setImportOpen(true)}
              >
                <FileUp />
                导入 JSON
              </Button>
            )}

            <Button
              variant="outline"
              title={canWrite ? '从本地图片制作自定义头像' : '游客不可制作，请先登录'}
              onClick={() => {
                if (!canWrite) {
                  setLoginOpen(true)
                  toast.info('游客仅可浏览，登录后才能制作头像')
                  return
                }
                setCustomFaceOpen(true)
              }}
            >
              <Sparkles />
              自制头像
            </Button>

            {canManageAccounts && (
              <Button variant="outline" onClick={() => setAccountOpen(true)}>
                <UserCog />
                账号管理
              </Button>
            )}

            {canManageBackups && (
              <Button
                variant="outline"
                title="查看每日备份、立即备份或还原历史数据"
                onClick={() => setBackupOpen(true)}
              >
                <DatabaseBackup />
                备份与还原
              </Button>
            )}

            {/* 站外跳转：创意工坊。武将做完可以导成模组包，去那边发布 */}
            <Button asChild variant="outline" title={`在新窗口打开创意工坊：${WORKSHOP_URL}`}>
              <a href={WORKSHOP_URL} target="_blank" rel="noreferrer noopener">
                <ExternalLink />
                创意工坊
              </a>
            </Button>

            {/* 站外跳转：剧本编辑器。自建武将最终要落进剧本，这里给出入口 */}
            <Button asChild variant="outline" title={`在新窗口打开剧本编辑器：${SCENARIO_URL}`}>
              <a href={SCENARIO_URL} target="_blank" rel="noreferrer noopener">
                <ExternalLink />
                剧本编辑器
              </a>
            </Button>

            {user ? (
              <Button variant="outline" onClick={handleLogout}>
                <LogOut />
                退出登录
              </Button>
            ) : (
              <Button variant="outline" onClick={() => setLoginOpen(true)}>
                <LogIn />
                登录
              </Button>
            )}

            {capabilities.create && (
              <Button onClick={openCreate}>
                <Plus />
                新建武将
              </Button>
            )}
          </div>
        </div>

        {/* 库切换 */}
        <div className="mx-auto flex max-w-7xl gap-2 px-4 pb-3 sm:px-6">
          {LIB_TABS.map((tab) => (
            <button
              key={tab.key}
              type="button"
              onClick={() => setLib(tab.key)}
              className={cn(
                'flex cursor-pointer flex-col rounded-lg border px-3.5 py-1.5 text-left transition-colors duration-150',
                lib === tab.key
                  ? 'border-primary/60 bg-primary/10 text-primary'
                  : 'border-border bg-card/40 text-muted-foreground hover:border-primary/30 hover:text-foreground',
              )}
            >
              <span className="flex items-center gap-1.5 text-sm font-medium">
                <Layers className="size-3.5" />
                {tab.label}
              </span>
              <span className="text-[10px] opacity-80">{tab.desc}</span>
            </button>
          ))}
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6">
        {/* 概览 */}
        <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatTile label="武将总数" value={persons.length} icon={<Users className="size-4" />} />
          <StatTile
            label="起始 ID"
            value={lib === 'custom' ? baseId : '固定'}
            icon={<BookUser className="size-4" />}
          />
          <StatTile
            label="下一个 ID"
            value={lib === 'custom' ? nextId : '不分配'}
            icon={<Plus className="size-4" />}
          />
          <StatTile
            label="当前筛选结果"
            value={visiblePersons.length}
            icon={<Search className="size-4" />}
          />
        </div>

        {/* 游客只读提示 */}
        {!canWrite && (
          <div className="mb-4 flex flex-wrap items-center gap-2 rounded-lg border border-primary/30 bg-primary/5 px-4 py-2.5 text-xs">
            <Badge variant="outline" className="border-primary/40 text-[10px] text-primary">
              游客模式
            </Badge>
            <span className="text-muted-foreground">
              当前为游客身份，仅可浏览与下载，不能新建 / 修改 / 删除武将。
            </span>
            <Button
              size="sm"
              variant="ghost"
              className="h-6 gap-1 px-2 text-primary"
              onClick={() => setLoginOpen(true)}
            >
              <LogIn className="size-3" />
              登录
            </Button>
          </div>
        )}

        {/* 库限制说明 */}
        <div className="mb-5 flex flex-wrap items-center gap-2 rounded-lg border border-border bg-card/40 px-4 py-2.5 text-xs text-muted-foreground">
          {lib === 'base' ? (
            <>
              <Badge variant="secondary" className="text-[10px]">
                本体数据
              </Badge>
              {containerExtras.type !== undefined && (
                <Badge variant="outline" className="border-gold/40 text-[10px] text-gold">
                  库类型 type={String(containerExtras.type)}
                </Badge>
              )}
              <span>
                ID 与游戏本体绑定不可修改、不支持单个新建；可编辑字段、单个 / 批量删除，并支持「一键导入」
                （按 ID 合并或替换整库）{canWrite ? '。' : '；当前为游客身份，仅可浏览。'}
              </span>
            </>
          ) : canWrite ? (
            <>
              <Badge variant="secondary" className="text-[10px]">
                可增删改
              </Badge>
              <span>
                自建武将 ID 自 {baseId} 起固定分配，永不改变、永不复用；姓名与基础武将库及本库其它武将查重，
                冲突时自动追加 #1 后缀。
              </span>
            </>
          ) : (
            <>
              <Badge variant="secondary" className="text-[10px]">
                仅可浏览
              </Badge>
              <span>
                自建武将 ID 自 {baseId} 起固定分配且永不复用；游客不能新建、修改或删除武将，登录后即可编辑。
              </span>
            </>
          )}
        </div>

        {/* 工具栏 */}
        <div className="mb-5 flex flex-wrap items-center gap-3">
          <div className="relative min-w-56 flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={keyword}
              placeholder="搜索姓名、字、标签或 ID..."
              className="pl-9"
              onChange={(e) => setKeyword(e.target.value)}
            />
            {keyword && (
              <button
                type="button"
                title="清空"
                className="absolute right-2 top-1/2 -translate-y-1/2 cursor-pointer text-muted-foreground hover:text-foreground"
                onClick={() => setKeyword('')}
              >
                <X className="size-4" />
              </button>
            )}
          </div>

          <Select value={sexFilter} onValueChange={setSexFilter}>
            <SelectTrigger className="w-28">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">全部性别</SelectItem>
              {SEX_CHOICES.map((s) => (
                <SelectItem key={s.value} value={String(s.value)}>
                  {s.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={typeFilter} onValueChange={setTypeFilter}>
            <SelectTrigger className="w-32">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">全部类型</SelectItem>
              {PERSON_TYPE_CHOICES.map((t) => (
                <SelectItem key={t.value} value={String(t.value)}>
                  {t.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          {/* 标签筛选（标签为自建武将库专有字段） */}
          {lib === 'custom' && (
            <Select value={tagFilter} onValueChange={setTagFilter}>
              <SelectTrigger className="w-36">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">全部标签</SelectItem>
                {tagOptions.map(([tag, count]) => (
                  <SelectItem key={tag} value={tag}>
                    #{tag}（{count}）
                  </SelectItem>
                ))}
                <SelectItem value="__none__">无标签</SelectItem>
              </SelectContent>
            </Select>
          )}

          <Select value={sortKey} onValueChange={(v) => setSortKey(v as SortKey)}>
            <SelectTrigger className="w-32">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="id">按 ID 升序</SelectItem>
              <SelectItem value="idDesc">按 ID 倒序（新→旧）</SelectItem>
              {lib === 'custom' && (
                <SelectItem value="updated">按修改时间（最近优先）</SelectItem>
              )}
              <SelectItem value="name">按姓名排序</SelectItem>
              <SelectItem value="ability">按能力排序</SelectItem>
            </SelectContent>
          </Select>

          {/* 基础武将库一键导入（整体导入：按 ID 合并或替换整库） */}
          {capabilities.bulkImport && (
            <Button variant="outline" onClick={() => setBulkImportOpen(true)}>
              <FileUp />
              一键导入
            </Button>
          )}

          {/* 批量删除入口（游客只读不展示） */}
          {capabilities.delete && (
            <Button
              variant={batchMode ? 'secondary' : 'outline'}
              onClick={() => {
                setBatchMode((v) => !v)
                setBatchSelected(new Set())
              }}
            >
              {batchMode ? <X /> : <Trash2 />}
              {batchMode ? '退出批量' : '批量操作'}
            </Button>
          )}
        </div>


        {/* 批量操作条：勾选后可导出模组包，或批量删除 */}
        {batchMode && (
          <div className="mb-4 flex flex-wrap items-center gap-3 rounded-lg border border-primary/40 bg-primary/5 px-4 py-2.5 text-xs">
            <Badge variant="outline" className="border-primary/40 text-[10px] text-primary">
              批量操作
            </Badge>
            <span className="text-muted-foreground">
              已选 <span className="font-medium text-foreground">{batchSelected.size}</span> 位（当前筛选结果共{' '}
              {visiblePersons.length} 位）
            </span>
            <div className="ml-auto flex flex-wrap items-center gap-1">
              <Button
                variant="ghost"
                size="sm"
                disabled={visiblePersons.length === 0}
                onClick={() => setBatchSelected(new Set(visiblePersons.map((p) => p.Id)))}
              >
                全选筛选结果
              </Button>
              <Button
                variant="ghost"
                size="sm"
                disabled={batchSelected.size === 0}
                onClick={() => setBatchSelected(new Set())}
              >
                清空选择
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="border-destructive/50 text-destructive hover:bg-destructive/10 hover:text-destructive"
                disabled={persons.length === 0}
                onClick={() => setClearAllOpen(true)}
              >
                清空整库（{persons.length}）
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={batchSelected.size === 0}
                onClick={() => setExportModOpen(true)}
              >
                <Boxes />
                生成模组包 {batchSelected.size > 0 ? batchSelected.size : ''}
              </Button>
              <Button
                variant="destructive"
                size="sm"
                disabled={batchSelected.size === 0}
                onClick={() => setBatchDeleteOpen(true)}
              >
                <Trash2 />
                删除所选 {batchSelected.size} 位
              </Button>
            </div>
          </div>
        )}

        {/* 列表 */}
        {loading ? (
          <div className="flex h-64 flex-col items-center justify-center gap-3 text-muted-foreground">
            <Loader2 className="size-6 animate-spin text-primary" />
            <span className="text-sm">正在加载{libLabel}...</span>
          </div>
        ) : loadError ? (
          <div className="flex h-64 flex-col items-center justify-center gap-3">
            <p className="text-sm text-destructive">{loadError}</p>
            <Button variant="outline" onClick={() => loadLibrary(lib)}>
              重试
            </Button>
          </div>
        ) : persons.length === 0 ? (
          lib === 'base' ? (
            <div className="flex h-64 flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-border">
              <BookUser className="size-6 text-muted-foreground" />
              <p className="text-sm text-muted-foreground">基础武将库数据为空或文件缺失</p>
            </div>
          ) : (
            <EmptyState canWrite={canWrite} onCreate={openCreate} onLogin={() => setLoginOpen(true)} />
          )
        ) : visiblePersons.length === 0 ? (
          <div className="flex h-48 flex-col items-center justify-center gap-2 text-muted-foreground">
            <Search className="size-6" />
            <p className="text-sm">没有符合条件的武将</p>
          </div>
        ) : (
          <div ref={listTopRef} className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-3">
            {pagePersons.map((p) => (
              <PersonCard
                key={p.Id}
                person={p}
                canEdit={canWrite}
                canDelete={capabilities.delete}
                onOpen={openPerson}
                onDelete={setDeleteTarget}
                selectable={batchMode}
                selected={batchSelected.has(p.Id)}
                onToggleSelect={toggleBatchSelect}
              />
            ))}
          </div>
        )}

        {/* 分页：只渲染当前页的卡片，避免一次渲染上千张导致卡顿 */}
        {!loading && !loadError && visiblePersons.length > 0 && (
          <div className="mt-6 flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
            <span>
              共 {visiblePersons.length} 位（库中合计 {persons.length} 位），当前显示第{' '}
              {(currentPage - 1) * pageSize + 1} ~{' '}
              {Math.min(currentPage * pageSize, visiblePersons.length)} 位
            </span>
            <div className="flex flex-wrap items-center gap-1">
              <Button
                variant="outline"
                size="sm"
                className="h-7 px-2"
                disabled={currentPage <= 1}
                onClick={() => goToPage(1)}
                aria-label="第一页"
              >
                <ChevronsLeft />
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="h-7 px-2"
                disabled={currentPage <= 1}
                onClick={() => goToPage(currentPage - 1)}
              >
                <ChevronLeft />
                上一页
              </Button>
              <span className="px-1 text-foreground">
                {currentPage} / {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                className="h-7 px-2"
                disabled={currentPage >= totalPages}
                onClick={() => goToPage(currentPage + 1)}
              >
                下一页
                <ChevronRight />
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="h-7 px-2"
                disabled={currentPage >= totalPages}
                onClick={() => goToPage(totalPages)}
                aria-label="最后一页"
              >
                <ChevronsRight />
              </Button>
              <Select value={String(pageSize)} onValueChange={(value) => setPageSize(Number(value))}>
                <SelectTrigger className="h-7 w-[7.5rem] text-xs" aria-label="每页条数">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PAGE_SIZE_OPTIONS.map((size) => (
                    <SelectItem key={size} value={String(size)}>
                      每页 {size} 条
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
        )}
      </main>

      {/* 新建 / 编辑弹窗 */}
      {formOptions && (
        <PersonFormDialog
          open={formOpen}
          onOpenChange={setFormOpen}
          initial={editing}
          persons={persons}
          personOptions={personOptions}
          nameIndex={nameIndex}
          options={formOptions}
          lib={lib}
          capabilities={capabilities}
          fieldKeys={fieldKeys}
          reservedNames={lib === 'custom' ? baseNames : []}
          readOnly={!canWrite}
          customFaces={customFaces}
          onCustomFacesChanged={loadCustomFaces}
          onRequireLogin={() => setLoginOpen(true)}
          onSaved={handleSaved}
        />
      )}

      {/* 删除确认 */}
      <AlertDialog open={Boolean(deleteTarget)} onOpenChange={(o) => !o && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>删除武将</AlertDialogTitle>
            <AlertDialogDescription>
              确定要删除武将「{deleteTarget?.Name || '未命名'}」（ID #{deleteTarget?.Id}）吗？
              该操作不可撤销，被删除的 ID 也不会再被复用。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleting}>取消</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              disabled={deleting}
              onClick={(e) => {
                e.preventDefault()
                confirmDelete()
              }}
            >
              {deleting && <Loader2 className="animate-spin" />}
              确认删除
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* 批量删除确认 */}
      <AlertDialog open={batchDeleteOpen} onOpenChange={setBatchDeleteOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>批量删除武将</AlertDialogTitle>
            <AlertDialogDescription>
              确定要删除所选的 {batchSelected.size} 位武将吗？该操作不可撤销，
              被删除的 ID 也不会再被复用。
              {batchSelected.size > 20 && '（数量较多，删除后无法通过界面一次性恢复）'}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={batchDeleting}>取消</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              disabled={batchDeleting}
              onClick={(e) => {
                e.preventDefault()
                confirmBatchDelete()
              }}
            >
              {batchDeleting && <Loader2 className="animate-spin" />}
              确认删除 {batchSelected.size} 位
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* 清空整库确认（不可撤销） */}
      <AlertDialog open={clearAllOpen} onOpenChange={setClearAllOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>清空{libLabel}全部武将</AlertDialogTitle>
            <AlertDialogDescription>
              确定要删除{libLabel}中的全部 {persons.length} 位武将吗？该操作不可撤销，
              建议先用「下载」导出备份。被删除的 ID 也不会再被复用。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={clearing}>取消</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              disabled={clearing}
              onClick={(e) => {
                e.preventDefault()
                confirmClearAll()
              }}
            >
              {clearing && <Loader2 className="animate-spin" />}
              确认清空 {persons.length} 位
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* 自制头像制作弹窗（主界面入口，制作完成后刷新列表） */}
      <CustomFaceDialog
        open={customFaceOpen}
        onOpenChange={setCustomFaceOpen}
        canWrite={canWrite}
        defaultSex={0}
        faces={customFaces}
        onCreated={() => {
          loadCustomFaces()
        }}
        onFacesChanged={loadCustomFaces}
        onRequireLogin={() => setLoginOpen(true)}
        onOpenPerson={openPersonFromFace}
      />

      {/* 武将批量导入弹窗（导入完成后刷新列表） */}
      <ImportPersonDialog
        open={importOpen}
        onOpenChange={setImportOpen}
        canWrite={canWrite}
        lib={lib}
        nextId={nextId}
        existingPersons={existingPersons}
        reservedNames={baseNames}
        existingTags={tagOptions.map(([tag]) => tag)}
        onImported={() => {
          loadLibrary(lib)
        }}
        onRequireLogin={() => setLoginOpen(true)}
      />

      {/* 基础武将库一键导入（整体导入：按 ID 合并或替换整库） */}
      <BulkImportDialog
        open={bulkImportOpen}
        onOpenChange={setBulkImportOpen}
        canWrite={canWrite}
        lib={lib}
        existingIds={persons.map((p) => p.Id)}
        existingCount={persons.length}
        onImported={() => {
          loadLibrary(lib)
        }}
        onRequireLogin={() => setLoginOpen(true)}
      />

      {/* 自建库偏移导出（把 ID 与关系字段平移到目标号段） */}
      <ExportOffsetDialog
        open={offsetExportOpen}
        onOpenChange={setOffsetExportOpen}
        persons={persons}
        baseId={baseId}
        fileBaseName="CustomPerson"
      />

      {/* 生成武将模组包（连同自制头像，导出给创意工坊发布） */}
      <ExportModDialog
        open={exportModOpen}
        onOpenChange={setExportModOpen}
        lib={lib}
        persons={persons}
        selectedIds={[...batchSelected]}
      />

      {/* 登录 / 注册弹窗 */}
      <LoginDialog
        open={loginOpen}
        onOpenChange={setLoginOpen}
        onSuccess={handleLoginSuccess}
        registerInfo={registerInfo}
      />

      {/* 账号管理弹窗（仅管理员） */}
      {canManageAccounts && (
        <AccountManagerDialog
          open={accountOpen}
          onOpenChange={setAccountOpen}
          currentUser={user}
          onChanged={loadIdentity}
        />
      )}

      {/* 备份与还原弹窗（仅超级管理员） */}
      {canManageBackups && (
        <BackupRestoreDialog
          open={backupOpen}
          onOpenChange={setBackupOpen}
          onRequireLogin={() => setLoginOpen(true)}
          onRestored={() => {
            // 还原后数据与账号都可能变化，重新加载当前库与身份信息
            void loadLibrary(lib)
            void loadIdentity()
          }}
        />
      )}
    </div>
  )
}

/** 概览统计卡片 */
function StatTile({
  label,
  value,
  icon,
}: {
  label: string
  value: number | string
  icon: React.ReactNode
}) {
  return (
    <div className="flex items-center gap-3 rounded-lg border border-border bg-card/60 px-4 py-3">
      <span className="flex size-8 items-center justify-center rounded-md bg-secondary text-primary">
        {icon}
      </span>
      <div className="flex flex-col">
        <span className="text-[11px] text-muted-foreground">{label}</span>
        <span className="font-display text-lg leading-tight text-gold">{value}</span>
      </div>
    </div>
  )
}

/** 空库提示 */
function EmptyState({
  canWrite,
  onCreate,
  onLogin,
}: {
  /** 是否具备写入权限（游客只能看到登录入口） */
  canWrite: boolean
  /** 新建武将 */
  onCreate: () => void
  /** 前往登录 */
  onLogin: () => void
}) {
  return (
    <div className="flex h-72 flex-col items-center justify-center gap-4 rounded-xl border border-dashed border-border bg-card/40">
      <div className="flex size-14 items-center justify-center rounded-full bg-secondary">
        <BookUser className="size-6 text-primary" />
      </div>
      <div className="text-center">
        <p className="font-display text-lg">武将库还是空的</p>
        <p className="mt-1 text-sm text-muted-foreground">
          {canWrite
            ? '点击下方按钮创建第一位武将，ID 将从 1000 起自动分配'
            : '当前为游客身份，登录后可创建第一位武将'}
        </p>
      </div>
      {canWrite ? (
        <Button onClick={onCreate}>
          <Plus />
          新建武将
        </Button>
      ) : (
        <Button onClick={onLogin}>
          <LogIn />
          登录
        </Button>
      )}
      <Badge variant="outline" className="text-[10px]">
        姓名与基础库冲突时自动追加 #1
      </Badge>
    </div>
  )
}
