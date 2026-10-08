/**
 * 文件名：ImportPersonDialog.tsx
 * 描述：武将批量导入弹窗（CustomPerson.json）。
 *
 *       流程（三段）：
 *       1) 选择文件：选择 / 拖入与 CustomPerson.json 结构一致的 JSON 文件；
 *       2) 导入预览：本地解析后分页列出全部武将，并通过勾选框决定哪些武将需要导入；
 *       3) 导入结果：展示新建 / 覆盖数量与未导入的同名记录，并可继续导入下一批。
 *
 *       勾选规则（导入不做任何改名）：
 *       - 默认只勾选「不重名」的武将；同名记录默认不勾选，不会导入；
 *       - 与「自建库既有武将」同名：勾选即代表「覆盖」——保留该武将的原 ID 与记录，
 *         只用导入数据替换其余字段，姓名保持文件中的写法（无论是否与基础库重名）；
 *       - 与基础武将库同名、或与本文件内其它记录同名：无法覆盖，**不可勾选、不会导入**；
 *       - 姓名为空的记录同样不可勾选，导入时会被跳过。
 *
 *       覆盖规则（仅针对同名的自建武将）：
 *       - 覆盖保留目标武将的 ID 与位置，不会新建武将，也不会追加 #1 后缀；
 *       - 自建库中存在历史遗留的「冯礼#1」这类后缀记录时，文件里的「冯礼」同样可以
 *         覆盖到它，覆盖后姓名恢复为文件中的写法，便于用母本文件清理旧后缀；
 *       - 库中存在多个同名武将（如 4 位「杜袭」）时，文件里的同名记录会按顺序一一对应覆盖；
 *       - 同一既有武将一次导入只能被覆盖一次：文件内重复的同名记录只有首条能覆盖，
 *         其余记录按「文件内同名」处理（不可导入），预览中会标注原因；
 *       - 可用「同名项全部覆盖」一键勾选全部可覆盖记录。
 *
 *       标签规则：预览页可填写「统一标签」，本次导入的所有武将（新建与被覆盖）都会
 *       追加这些标签（与文件中原有标签合并、去重），导入后可据此筛选与分类。
 *
 *       筛选规则：预览表可按重名情况筛选（全部武将 / 只看不重名 / 只看重名 / 只看可覆盖），
 *       筛选只影响展示范围，不会改变已勾选状态，也不会影响最终导入结果；
 *       配合「选中筛选结果」按钮可一次性勾选当前筛选出的武将。
 *
 *       分页规则：预览表按页渲染（默认每页 100 条，可切换每页条数），
 *       避免一次渲染上千行拖慢界面；勾选状态按文件中的序号保存，翻页不会丢失。
 *       分页基于「筛选后的记录」计算，避免记录被筛掉后出现大量空页。
 *
 *       ID 规则：忽略文件中的原 ID，由服务端从当前库的下一个可用 ID 起顺序分配
 *       （并跳过已被占用的 ID）；预览中的「新 ID」仅按已勾选的武将顺序推算，
 *       与实际导入结果一致；选择「覆盖」的记录不占用新 ID，沿用被覆盖武将的原 ID。
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { toast } from 'sonner'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { TagsField } from '@/components/form-fields'
import { ApiError, importPersons } from '@/lib/api'
import { cn } from '@/lib/utils'
import type { ImportResult, LibraryKey } from '@/lib/types'
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  FileUp,
  Loader2,
  RotateCcw,
  Upload,
} from 'lucide-react'

/** 导入步骤 */
type Step = 'pick' | 'preview' | 'done'

/** 预览表默认每页条数 */
const DEFAULT_PAGE_SIZE = 100

/** 预览表可选的每页条数 */
const PAGE_SIZE_OPTIONS = [50, 100, 200, 500]

/** 预览表的姓名筛选类型 */
type NameFilter = 'all' | 'clean' | 'duplicated' | 'overwritable'

/** 姓名筛选可选项（文案与筛选类型一一对应） */
const NAME_FILTER_OPTIONS: { value: NameFilter; label: string }[] = [
  { value: 'all', label: '全部武将' },
  { value: 'clean', label: '只看不重名' },
  { value: 'duplicated', label: '只看重名' },
  { value: 'overwritable', label: '只看可覆盖' },
]

/** 同名类型：none=不重名；custom=与自建库既有武将同名（勾选即覆盖）；base=与基础武将库同名（不可导入）；file=与本文件其它记录同名（不可导入） */
type ConflictKind = 'none' | 'custom' | 'base' | 'file'



/** 文件中单条武将的原始数据（结构未知，按普通对象处理） */
type RawPerson = Record<string, unknown>

/** 单条武将的导入计划 */
interface PlanEntry {
  /** 在文件中的序号（作为勾选标识） */
  index: number
  /** 文件中的原 ID（缺失为 null） */
  rawId: number | null
  /** 文件中的姓名（导入不做改名，最终写入的姓名与此一致） */
  from: string
  /** 最终写入的 ID：新建为分配的新 ID，覆盖为被覆盖武将的原 ID，未勾选或被跳过时为 -1 */
  id: number
  /** 是否因姓名为空将被跳过 */
  skipped: boolean
  /** 是否可以勾选（不重名，或与自建武将同名可覆盖） */
  selectable: boolean
  /** 是否勾选导入 */
  selected: boolean
  /** 同名类型 */
  conflict: ConflictKind
  /** 可覆盖的目标武将 Id（不可覆盖时为 null） */
  targetId: number | null
  /** 覆盖目标的当前姓名（用于展示替换前后的姓名差异） */
  targetName: string | null
  /** 是否覆盖（勾选且与自建库既有武将同名） */
  overwrite: boolean
}

/**
 * 从导入数据中提取武将原始数据列表。
 * 兼容 { PersonLibrary: {...} }、{ PersonLibrary: [...] } 与纯数组三种结构。
 * @param input 已解析的 JSON 数据
 * @returns 武将列表或错误提示
 */
function extractImportPersons(input: unknown): { persons?: RawPerson[]; error?: string } {
  if (input === null || input === undefined) return { error: '文件内容为空' }
  let container: unknown = input
  if (!Array.isArray(container) && typeof container === 'object' && container !== null) {
    const lib = (container as Record<string, unknown>).PersonLibrary
    if (lib !== undefined && lib !== null) container = lib
  }

  if (Array.isArray(container)) {
    const persons = container.filter(
      (item): item is RawPerson => Boolean(item) && typeof item === 'object' && !Array.isArray(item),
    )
    return { persons }
  }
  if (typeof container !== 'object' || container === null) {
    return { error: '文件结构无法识别，请选择 CustomPerson.json 格式的文件' }
  }

  const persons: RawPerson[] = []
  for (const key of Object.keys(container as RawPerson)) {
    // offset 是容器元数据，不是武将
    if (key === 'offset') continue
    const value = (container as RawPerson)[key]
    if (!value || typeof value !== 'object' || Array.isArray(value)) continue
    persons.push(value as RawPerson)
  }
  return { persons }
}

/**
 * 按原 ID 升序排列武将（无 ID 时保持原顺序）。
 * 与服务端对对象容器的排序规则保持一致，保证 ID 分配结果可预期。
 * @param persons 武将列表
 * @returns 排序后的新数组
 */
function sortByRawId(persons: RawPerson[]): RawPerson[] {
  return [...persons].sort((a, b) => {
    const idA = Number(a.Id)
    const idB = Number(b.Id)
    const validA = Number.isFinite(idA) ? idA : Number.MAX_SAFE_INTEGER
    const validB = Number.isFinite(idB) ? idB : Number.MAX_SAFE_INTEGER
    return validA - validB
  })
}

/**
 * 取武将的展示姓名：优先使用文件中的 Name，缺失时由“姓 + 名”推导。
 * @param raw 原始武将数据
 * @returns 姓名（可能为空字符串）
 */
function displayNameOf(raw: RawPerson): string {
  const name = raw.Name === null || raw.Name === undefined ? '' : String(raw.Name).trim()
  if (name) return name
  return `${raw.familyName ?? ''}${raw.giveName ?? ''}`.trim()
}

/**
 * 把「统一标签」合并进导入记录的原始数据。
 * - 保留文件中原有的标签（兼容字符串数组与逗号分隔字符串两种写法）；
 * - 统一标签追加在原有标签之后并去重；
 * - 标签的长度 / 数量上限由服务端 toTagArray 统一裁剪。
 * @param raw 导入的原始武将数据
 * @param extraTags 统一标签
 * @returns 合并后的武将数据（无统一标签时原样返回）
 */
function withExtraTags(raw: RawPerson, extraTags: string[]): RawPerson {
  if (extraTags.length === 0) return raw

  const existing: string[] = []
  const pushTag = (value: unknown) => {
    const tag = String(value ?? '').trim()
    if (tag && !existing.includes(tag)) existing.push(tag)
  }
  const rawTags = raw.tags
  if (Array.isArray(rawTags)) {
    for (const item of rawTags) pushTag(item)
  } else if (typeof rawTags === 'string') {
    for (const item of rawTags.split(/[,，、;；\s]+/)) pushTag(item)
  }

  const merged = [...existing]
  for (const tag of extraTags) if (!merged.includes(tag)) merged.push(tag)
  return { ...raw, tags: merged }
}

/** 当前库既有武将的引用（用于同名判定与覆盖目标定位） */
export interface ExistingPersonRef {
  /** 武将 ID */
  id: number
  /** 武将姓名 */
  name: string
}

/** 重名判定上下文 */
interface NameContext {
  /** 基础武将库姓名集合（作为新建记录的查重范围） */
  baseNames: Set<string>
  /**
   * 同源姓名分组：同源名 -> 库中记录（按「无后缀优先、后缀序号、Id」升序）。
   * 同源指去掉末尾「#序号」后相同，因此同名武将（如 4 位「杜袭」）与
   * 历史遗留的「冯礼#1」都归入同一组，供导入记录按顺序一一对应。
   */
  sameNameGroups: Map<string, ExistingPersonRef[]>
  /** 当前库已占用的 ID 集合 */
  existingIds: number[]
  /** 初始占用姓名集合（基础库姓名 + 当前库既有武将姓名） */
  used: Set<string>
}

/** 匹配「姓名#序号」形式的重名后缀 */
const NAME_SUFFIX_PATTERN = /^(.+)#(\d+)$/

/**
 * 取姓名的同源名：去掉末尾的「#序号」后缀（「冯礼#1」->「冯礼」）。
 * @param name 姓名
 * @returns 同源名
 */
function prototypeNameOf(name: string): string {
  const trimmed = (name ?? '').trim()
  const matched = NAME_SUFFIX_PATTERN.exec(trimmed)
  return matched ? matched[1].trim() : trimmed
}

/**
 * 取姓名的重名后缀序号（无后缀为 0）。
 * @param name 姓名
 * @returns 后缀序号
 */
function suffixNumberOf(name: string): number {
  const matched = NAME_SUFFIX_PATTERN.exec((name ?? '').trim())
  return matched ? Number(matched[2]) : 0
}

/**
 * 构建重名判定上下文。
 * @param reservedNames 基础武将库姓名（跨库查重）
 * @param existingPersons 当前库既有武将（Id + 姓名）
 * @returns 重名判定上下文
 */
function buildNameContext(
  reservedNames: string[],
  existingPersons: ExistingPersonRef[],
): NameContext {
  const baseNames = new Set<string>()
  for (const name of reservedNames) {
    const trimmed = (name ?? '').trim()
    if (trimmed) baseNames.add(trimmed)
  }

  const sameNameGroups = new Map<string, ExistingPersonRef[]>()
  const existingIds: number[] = []
  const used = new Set<string>(baseNames)
  for (const ref of existingPersons) {
    if (!ref) continue
    if (Number.isFinite(ref.id)) existingIds.push(ref.id)
    const name = (ref.name ?? '').trim()
    if (!name) continue
    used.add(name)
    const key = prototypeNameOf(name)
    const group = sameNameGroups.get(key)
    if (group) group.push(ref)
    else sameNameGroups.set(key, [ref])
  }
  // 组内排序：名单中不含后缀的记录优先，其次按后缀序号与 Id 升序；
  // 这样「文件里第 N 个杜袭」自然对应「库中第 N 条杜袭记录」
  for (const group of sameNameGroups.values()) {
    group.sort((a, b) => suffixNumberOf(a.name) - suffixNumberOf(b.name) || a.id - b.id)
  }
  return { baseNames, sameNameGroups, existingIds, used }
}

/**
 * 计算导入计划：按文件顺序逐条推算同名类型与最终 ID。
 * 规则（导入不做任何改名）：
 * - 不重名：可勾选，勾选后分配新 ID（跳过库中已占用的 ID，存在 ID 空洞时也不会重复）；
 * - 与自建库既有武将同名（含「冯礼#1」这类带后缀的同源记录、多个同名武将）：
 *   勾选即代表覆盖，沿用目标武将 ID 与文件中的姓名，不占用新 ID、不追加后缀；
 * - 与基础库同名、或与本文件内其它记录同名：不可勾选、不会导入；
 * - 同一既有武将一次导入只能被覆盖一次，被更早记录占用后按「文件内同名」处理。
 * @param persons 文件中的武将列表（已按原 ID 排序）
 * @param ctx 同名判定上下文
 * @param selected 已勾选的序号集合
 * @param baseId 当前库的下一个可用 ID
 * @returns 导入计划
 */
function buildPlan(
  persons: RawPerson[],
  ctx: NameContext,
  selected: Set<number>,
  baseId: number,
): PlanEntry[] {
  const used = new Set<string>(ctx.used)
  const usedIds = new Set<number>(ctx.existingIds)
  /** 已被本文件内记录占用的覆盖目标（同一既有武将一次导入只能覆盖一次） */
  const claimedTargets = new Set<number>()
  let nextAssignId = baseId

  /** 分配一个未被占用的新 ID */
  const allocateId = () => {
    while (usedIds.has(nextAssignId)) nextAssignId++
    usedIds.add(nextAssignId)
    return nextAssignId
  }

  return persons.map((raw, index) => {
    const base = displayNameOf(raw)
    const rawIdNum = Number(raw.Id)
    const rawId = Number.isFinite(rawIdNum) && rawIdNum > 0 ? rawIdNum : null
    const checked = selected.has(index)

    if (!base) {
      return {
        index,
        rawId,
        from: '',
        id: -1,
        skipped: true,
        selectable: false,
        selected: false,
        conflict: 'none',
        targetId: null,
        targetName: null,
        overwrite: false,
      }
    }

    // 覆盖目标判定：在同源姓名分组中取「尚未被本文件其它记录占用」的第一条记录
    // （优先精确同名，其次「冯礼#1」这类同源后缀记录）；
    // 同源分组让「文件里第 N 个杜袭」对应「库中第 N 条杜袭记录」，多同名武将也能一一覆盖
    let conflict: ConflictKind = 'none'
    let targetId: number | null = null
    let targetName: string | null = null
    const group = ctx.sameNameGroups.get(prototypeNameOf(base))
    if (group) {
      const picked =
        group.find((ref) => !claimedTargets.has(ref.id) && ref.name.trim() === base) ??
        group.find((ref) => !claimedTargets.has(ref.id)) ??
        null
      if (picked) {
        conflict = 'custom'
        targetId = picked.id
        targetName = picked.name
      }
    }
    if (conflict === 'none') {
      if (ctx.baseNames.has(base)) conflict = 'base'
      else if (used.has(base)) conflict = 'file'
    }

    // 覆盖：勾选即代表覆盖——沿用既有武将 ID，姓名保持文件中的写法（不占用新 ID、不追加后缀）。
    // 目标在同源分组中已按「未被占用」挑选，因此不会被重复覆盖
    if (conflict === 'custom' && targetId !== null && checked) {
      claimedTargets.add(targetId)
      return {
        index,
        rawId,
        from: base,
        id: targetId,
        skipped: false,
        selectable: true,
        selected: true,
        conflict,
        targetId,
        targetName,
        overwrite: true,
      }
    }

    // 不做改名：不重名可勾选新建；同名（自建同名但未勾选 / 基础库同名 / 文件内同名）只展示、不可勾选
    if (checked && conflict === 'none') used.add(base)
    return {
      index,
      rawId,
      from: base,
      id: checked && conflict === 'none' ? allocateId() : -1,
      skipped: false,
      selectable: conflict === 'none' || conflict === 'custom',
      selected: checked && conflict === 'none',
      conflict,
      targetId,
      targetName,
      overwrite: false,
    }
  })
}

/**
 * 计算默认勾选结果：把所有武将都视为导入，
 * 「不重名」的记录即为默认勾选项（同名记录需手动勾选，勾选即覆盖）。
 * @param persons 文件中的武将列表
 * @param ctx 同名判定上下文
 * @param baseId 当前库的下一个可用 ID
 * @returns 默认勾选的序号集合
 */
function computeDefaultSelection(
  persons: RawPerson[],
  ctx: NameContext,
  baseId: number,
): Set<number> {
  const allSelected = new Set(persons.map((_, index) => index))
  const preview = buildPlan(persons, ctx, allSelected, baseId)
  return new Set(
    preview.filter((item) => !item.skipped && item.conflict === 'none').map((item) => item.index),
  )
}

interface ImportPersonDialogProps {
  /** 是否打开 */
  open: boolean
  /** 打开状态变化 */
  onOpenChange: (open: boolean) => void
  /** 是否具备写入权限（游客为 false） */
  canWrite: boolean
  /** 目标库标识 */
  lib: LibraryKey
  /** 当前库的下一个可用 ID（用于预览） */
  nextId: number
  /** 当前库既有武将（Id + 姓名），用于判定重名与覆盖目标 */
  existingPersons: ExistingPersonRef[]
  /** 基础武将库姓名（跨库查重） */
  reservedNames: string[]
  /** 当前库已使用的标签（统一标签输入时的候选） */
  existingTags: string[]
  /** 导入成功回调（用于刷新列表） */
  onImported: (result: ImportResult) => void
  /** 需要登录时的回调 */
  onRequireLogin: () => void
}

export function ImportPersonDialog({
  open,
  onOpenChange,
  canWrite,
  lib,
  nextId,
  existingPersons,
  reservedNames,
  existingTags,
  onImported,
  onRequireLogin,
}: ImportPersonDialogProps) {
  const [step, setStep] = useState<Step>('pick')
  const [fileName, setFileName] = useState('')
  const [persons, setPersons] = useState<RawPerson[]>([])
  /** 勾选导入的武将序号 */
  const [selected, setSelected] = useState<Set<number>>(() => new Set())
  /** 默认勾选结果（「仅选未重名」按钮用） */
  const [defaultSelection, setDefaultSelection] = useState<Set<number>>(() => new Set())
  /** 统一标签：本批导入的武将（含被覆盖的）统一追加这些标签 */
  const [batchTags, setBatchTags] = useState<string[]>([])
  const [error, setError] = useState('')
  const [importing, setImporting] = useState(false)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [dragging, setDragging] = useState(false)
  /** 预览表当前页码（从 1 开始） */
  const [page, setPage] = useState(1)
  /** 预览表每页条数 */
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE)
  /** 预览表的姓名筛选（默认展示全部） */
  const [nameFilter, setNameFilter] = useState<NameFilter>('all')
  const fileInputRef = useRef<HTMLInputElement>(null)
  /** 预览表滚动容器（翻页后回到顶部） */
  const tableScrollRef = useRef<HTMLDivElement>(null)

  // 每次打开弹窗都回到初始状态，避免残留上一次的文件与结果
  useEffect(() => {
    if (!open) return
    setStep('pick')
    setFileName('')
    setPersons([])
    setSelected(new Set())
    setDefaultSelection(new Set())
    setBatchTags([])
    setError('')
    setImporting(false)
    setResult(null)
    setDragging(false)
    setPage(1)
    setPageSize(DEFAULT_PAGE_SIZE)
    setNameFilter('all')
  }, [open])

  /** 同名判定上下文（基础库姓名 / 自建库姓名->Id / 已占用 ID） */
  const nameContext = useMemo(
    () => buildNameContext(reservedNames, existingPersons),
    [reservedNames, existingPersons],
  )

  /**
   * 导入计划：按文件顺序逐条推算同名类型与最终 ID。
   * 只有被勾选的武将会占用 ID，因此未勾选的武将不会影响已勾选武将的编号；
   * 与自建库同名的记录勾选即覆盖，沿用被覆盖武将的原 ID。
   */
  const plan = useMemo<PlanEntry[]>(
    () => buildPlan(persons, nameContext, selected, nextId),
    [persons, nameContext, selected, nextId],
  )

  /** 可勾选的序号（不重名，或与自建武将同名可覆盖） */
  const selectableIndexes = useMemo(
    () => plan.filter((item) => item.selectable).map((item) => item.index),
    [plan],
  )

  /** 预览统计（ID 区间只统计新建记录：覆盖记录沿用既有 ID，不占用新号段） */
  const stats = useMemo(() => {
    const picked = plan.filter((item) => item.selected)
    const inserted = picked.filter((item) => !item.overwrite)
    return {
      total: plan.length,
      skipped: plan.filter((item) => item.skipped).length,
      selected: picked.length,
      /** 同名且无法覆盖（与基础库同名 / 与本文件同名）→ 不可导入 */
      unimportable: plan.filter((item) => !item.skipped && !item.selectable).length,
      overwritten: picked.filter((item) => item.overwrite).length,
      firstId: inserted.length ? inserted[0].id : 0,
      lastId: inserted.length ? inserted[inserted.length - 1].id : 0,
    }
  }, [plan])

  /**
   * 按同名情况筛选后的记录。
   * 以「同名类型」为依据，因此结果不受勾选影响：
   * 筛选仅用于展示，不改变勾选状态，也不参与 ID 推算。
   */
  const filteredPlan = useMemo(() => {
    if (nameFilter === 'clean') return plan.filter((item) => !item.skipped && item.conflict === 'none')
    if (nameFilter === 'duplicated') return plan.filter((item) => !item.skipped && item.conflict !== 'none')
    if (nameFilter === 'overwritable') return plan.filter((item) => item.conflict === 'custom')
    return plan
  }, [plan, nameFilter])

  /** 各筛选条件下的记录数（用于筛选下拉的计数展示） */
  const filterCounts = useMemo(
    () => ({
      all: plan.length,
      clean: plan.filter((item) => !item.skipped && item.conflict === 'none').length,
      duplicated: plan.filter((item) => !item.skipped && item.conflict !== 'none').length,
      overwritable: plan.filter((item) => item.conflict === 'custom').length,
    }),
    [plan],
  )

  /** 筛选结果中可勾选的记录数 */
  const filterSelectableCount = useMemo(
    () => filteredPlan.filter((item) => item.selectable).length,
    [filteredPlan],
  )

  /** 预览总页数（至少 1 页，便于统一展示「第 x / y 页」） */
  const totalPages = Math.max(1, Math.ceil(filteredPlan.length / pageSize))

  /** 当前页码（裁剪到合法范围，避免切换每页条数或筛选条件后越界） */
  const currentPage = Math.min(page, totalPages)

  /** 当前页需要渲染的记录（基于筛选后的记录分页） */
  const pageEntries = useMemo(
    () => filteredPlan.slice((currentPage - 1) * pageSize, currentPage * pageSize),
    [filteredPlan, currentPage, pageSize],
  )

  /**
   * 切换预览页码。
   * 页码会被裁剪到 [1, 总页数] 区间，并让表格滚动回顶部，便于连续浏览。
   * @param next 目标页码
   */
  const goToPage = (next: number) => {
    setPage(Math.min(Math.max(1, next), totalPages))
    tableScrollRef.current?.scrollTo({ top: 0 })
  }

  const allSelected =
    selectableIndexes.length > 0 && selectableIndexes.every((index) => selected.has(index))
  const someSelected = selectableIndexes.some((index) => selected.has(index))

  /** 读取并解析所选文件 */
  const readFile = async (file: File) => {
    setError('')
    try {
      const text = await file.text()
      const parsed = JSON.parse(text.replace(/^\uFEFF/, ''))
      const extracted = extractImportPersons(parsed)
      if (extracted.error) {
        setError(extracted.error)
        return
      }
      const list = sortByRawId(extracted.persons || [])
      if (list.length === 0) {
        setError('文件中没有可导入的武将')
        return
      }
      // 默认只勾选不会重名的武将；重名项需手动勾选才会导入（自建武将重名还可选择覆盖）
      const defaults = computeDefaultSelection(
        list,
        buildNameContext(reservedNames, existingPersons),
        nextId,
      )
      setFileName(file.name)
      setPersons(list)
      setDefaultSelection(defaults)
      setSelected(new Set(defaults))
      setPage(1)
      setStep('preview')
    } catch (err) {
      setError(err instanceof Error ? `JSON 解析失败：${err.message}` : '文件读取失败')
    }
  }

  /** 文件选择框变化 */
  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    // 允许重复选择同一个文件
    event.target.value = ''
    if (file) readFile(file)
  }

  /** 拖拽释放 */
  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    setDragging(false)
    const file = event.dataTransfer.files?.[0]
    if (file) readFile(file)
  }

  /**
   * 更新勾选集合。
   * 覆盖不再单独记录：勾选「与自建武将同名」的记录即代表覆盖，
   * 因此勾选集合是唯一事实来源（预览、提交都只看它）。
   * @param next 新的勾选集合
   */
  const applySelection = (next: Set<number>) => {
    setSelected(next)
  }

  /** 勾选 / 取消勾选单个武将 */
  const toggleOne = (index: number) => {
    const next = new Set(selected)
    if (next.has(index)) next.delete(index)
    else next.add(index)
    applySelection(next)
  }

  /** 全选 / 全不选（仅影响可勾选记录） */
  const toggleAll = () => {
    applySelection(allSelected ? new Set() : new Set(selectableIndexes))
  }

  /**
   * 把当前筛选结果并入勾选集合。
   * 采用「并集」而不是替换，避免清掉用户在其它筛选条件下已勾选的记录。
   */
  const selectFiltered = () => {
    const next = new Set(selected)
    for (const item of filteredPlan) if (item.selectable) next.add(item.index)
    applySelection(next)
  }

  /**
   * 一键覆盖：把所有「与自建武将同名」的可覆盖记录全部勾选。
   * 覆盖沿用被覆盖武将的原 ID，不占用新号段。
   */
  const selectAllOverwritable = () => {
    const next = new Set(selected)
    for (const item of plan) {
      if (item.conflict === 'custom' && item.targetId !== null) next.add(item.index)
    }
    applySelection(next)
  }

  /**
   * 切换姓名筛选：页码回到第一页，并让表格滚动回顶部。
   * @param value 目标筛选类型
   */
  const changeFilter = (value: NameFilter) => {
    setNameFilter(value)
    setPage(1)
    tableScrollRef.current?.scrollTo({ top: 0 })
  }

  /** 确认导入（只提交已勾选的武将） */
  const handleImport = async () => {
    if (!canWrite) {
      onOpenChange(false)
      onRequireLogin()
      toast.info('游客仅可浏览，登录后才能导入武将')
      return
    }
    // 组装提交数据：payload 的顺序就是服务端的处理顺序，
    // 覆盖映射必须使用 payload 下标（而非文件下标），否则勾选子集时下标会整体错位，
    // 导致覆盖映射失效（该行被当成新建）甚至覆盖到别的武将上
    const payload: RawPerson[] = []
    const overwrites: Record<string, number> = {}
    for (const item of plan) {
      if (!item.selected) continue
      const payloadIndex = payload.length
      if (item.overwrite && item.targetId !== null) {
        overwrites[String(payloadIndex)] = item.targetId
      }
      payload.push(withExtraTags(persons[item.index], batchTags))
    }
    if (payload.length === 0) {
      toast.error('请至少勾选一位要导入的武将')
      return
    }
    setImporting(true)
    try {
      // 以数组形式提交，服务端保持顺序并依次分配 ID（与预览一致）
      const data = await importPersons(lib, { data: payload, overwrites })
      setResult(data)
      setStep('done')
      onImported(data)
      const overwrittenTip = data.overwritten.length
        ? `，覆盖 ${data.overwritten.length} 位自建武将`
        : ''
      const conflictTip = data.conflicts?.length ? `，${data.conflicts.length} 位同名未导入` : ''
      toast.success(`成功导入 ${data.imported} 位武将${overwrittenTip}${conflictTip}`)
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        toast.error('登录状态已失效，请重新登录后再试')
        onOpenChange(false)
        onRequireLogin()
        return
      }
      setError(err instanceof Error ? err.message : '导入失败')
    } finally {
      setImporting(false)
    }
  }

  /** 回到选择文件步骤 */
  const backToPick = () => {
    setStep('pick')
    setFileName('')
    setPersons([])
    setSelected(new Set())
    setDefaultSelection(new Set())
    setBatchTags([])
    setPage(1)
    setNameFilter('all')
    setResult(null)
    setError('')
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[90vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-[min(56rem,calc(100vw-2rem))]">
        <DialogHeader className="border-b border-border px-5 py-4">
          <DialogTitle className="flex items-center gap-2 font-display text-lg">
            <FileUp className="size-5 text-primary" />
            导入武将
          </DialogTitle>
          <DialogDescription className="text-xs">
            选择与 CustomPerson.json 结构一致的 JSON 文件。默认只勾选不会重名的武将；与自建武将重名时可选择覆盖，其它重名需手动勾选后改名导入。
          </DialogDescription>
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-y-auto p-5">
          {/* 第一步：选择文件 */}
          {step === 'pick' && (
            <div className="flex flex-col gap-4">
              <div
                className={cn(
                  'flex cursor-pointer flex-col items-center gap-3 rounded-lg border border-dashed px-4 py-10 transition-colors',
                  dragging ? 'border-primary bg-primary/10' : 'border-border bg-card/40',
                )}
                onClick={() => fileInputRef.current?.click()}
                onDragOver={(e) => {
                  e.preventDefault()
                  setDragging(true)
                }}
                onDragLeave={() => setDragging(false)}
                onDrop={handleDrop}
              >
                <div className="flex size-14 items-center justify-center rounded-full bg-secondary">
                  <Upload className="size-6 text-primary" />
                </div>
                <div className="text-center">
                  <p className="text-sm font-medium">点击选择，或把文件拖到此处</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    仅支持 .json 文件（CustomPerson.json / PersonLibrary.json 结构）
                  </p>
                </div>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".json,application/json"
                  className="hidden"
                  onChange={handleFileChange}
                />
                <Button type="button" onClick={() => fileInputRef.current?.click()}>
                  <FileUp />
                  选择 JSON 文件
                </Button>
              </div>

              {error && (
                <div className="flex items-start gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
                  <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
                  <span>{error}</span>
                </div>
              )}

              {!canWrite && (
                <p className="text-xs text-destructive">当前为游客身份，登录后才能导入武将</p>
              )}

              <div className="rounded-lg border border-border bg-card/40 px-4 py-3 text-[11px] leading-relaxed text-muted-foreground">
                <p className="mb-1 font-medium text-foreground">导入规则</p>
                <p>1. 文件中的原 ID 会被忽略，从当前下一个可用 ID（{nextId}）起顺序分配（跳过已占用的 ID）。</p>
                <p>2. 姓名为空的记录会被跳过；单次最多导入 2000 位武将。</p>
                <p>3. **导入不做任何改名**：姓名与基础武将库、本库既有武将查重。</p>
                <p>4. 下一步会分页列出全部武将（可切换每页条数），默认只勾选不重名的记录；同名记录默认不导入。</p>
                <p>5. 与「自建武将」同名：勾选即代表覆盖——只更新该武将的属性（保留其原 ID 与记录，绝不会新建武将），姓名按文件中的写法，不追加 #1。</p>
                <p>6. 与「基础武将库」同名、或与本文件内其它记录同名：无法覆盖，不可勾选、不会导入（预览中会标注原因）。</p>
                <p>7. 库中若是历史上被改名的「名字#1」记录，文件里的「名字」也能覆盖到它，覆盖后姓名恢复为文件写法。</p>
                <p>8. 库中存在多个同名武将（如 4 位「杜袭」）时，文件里的同名记录会按顺序一一对应覆盖；超出的同名记录不可导入。</p>
                <p>9. 预览支持按同名情况筛选（只看不重名 / 只看同名 / 只看可覆盖），筛选只影响展示，不影响勾选状态与导入结果。</p>
                <p>10. 预览页可填写「统一标签」，本批导入（含被覆盖）的武将都会追加该标签，便于导入后按标签筛选。</p>
              </div>
            </div>
          )}

          {/* 第二步：导入预览 + 勾选 */}
          {step === 'preview' && (
            <div className="flex flex-col gap-4">
              <div className="grid grid-cols-2 gap-2 sm:grid-cols-5">
                <PreviewTile label="文件武将数" value={stats.total} />
                <PreviewTile label="已勾选" value={stats.selected} />
                <PreviewTile
                  label="覆盖自建"
                  value={stats.overwritten}
                  highlight={stats.overwritten > 0}
                />
                <PreviewTile
                  label="同名不可导入"
                  value={stats.unimportable}
                  highlight={stats.unimportable > 0}
                />
                <PreviewTile label="将跳过" value={stats.skipped} highlight={stats.skipped > 0} />
              </div>

              <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-card/40 px-3 py-2 text-xs">
                <Badge variant="outline" className="max-w-[16rem] truncate text-[10px]">
                  {fileName || '未命名文件'}
                </Badge>
                <span className="text-muted-foreground">
                  新建 ID：
                  <span className="ml-1 font-medium text-primary">
                    {stats.firstId > 0 ? `${stats.firstId} ~ ${stats.lastId}` : '无'}
                  </span>
                </span>
                <span className="text-muted-foreground">
                  覆盖既有武将：
                  <span className="ml-1 font-medium text-gold">{stats.overwritten} 位</span>
                </span>
              </div>

              {/* 统一标签：本批导入的武将（含被覆盖的）统一追加该标签 */}
              <div className="rounded-lg border border-border bg-card/40 px-3 py-2">
                <TagsField
                  label="统一标签"
                  hint="tags（本批导入的武将统一追加，保留文件中原有标签）"
                  value={batchTags}
                  suggestions={existingTags}
                  onChange={setBatchTags}
                />
              </div>

              {/* 勾选工具栏 */}
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex flex-wrap items-center gap-3">
                  <label className="flex cursor-pointer items-center gap-2 text-xs">
                    <Checkbox
                      checked={allSelected ? true : someSelected ? 'indeterminate' : false}
                      disabled={selectableIndexes.length === 0}
                      onCheckedChange={toggleAll}
                      aria-label="全选"
                    />
                    全选
                  </label>
                  <Select value={nameFilter} onValueChange={(v) => changeFilter(v as NameFilter)}>
                    <SelectTrigger className="h-7 w-[11.5rem] text-xs" aria-label="姓名筛选">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {NAME_FILTER_OPTIONS.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}（{filterCounts[option.value]}）
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="flex flex-wrap items-center gap-1">
                  {nameFilter !== 'all' && (
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={filterSelectableCount === 0}
                      onClick={selectFiltered}
                    >
                      选中筛选结果（{filterSelectableCount}）
                    </Button>
                  )}
                  <Button
                    variant="ghost"
                    size="sm"
                    className="text-gold"
                    disabled={filterCounts.overwritable === 0}
                    onClick={selectAllOverwritable}
                    title="与自建武将同名的记录全部勾选（覆盖，保留原 ID）"
                  >
                    同名项全部覆盖（{filterCounts.overwritable}）
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => applySelection(new Set(defaultSelection))}
                  >
                    仅选不重名
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => applySelection(new Set(selectableIndexes))}
                  >
                    全部选中
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => applySelection(new Set())}>
                    全不选
                  </Button>
                </div>
              </div>

              {error && (
                <div className="flex items-start gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
                  <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
                  <span>{error}</span>
                </div>
              )}

              <div
                ref={tableScrollRef}
                className="max-h-80 overflow-y-auto rounded-lg border border-border"
              >
                <table className="w-full border-collapse text-xs">
                  <thead className="sticky top-0 z-10 bg-secondary/90 backdrop-blur">
                    <tr className="text-left text-muted-foreground">
                      <th className="w-10 px-3 py-2 font-medium">选</th>
                      <th className="w-14 px-3 py-2 font-medium">序号</th>
                      <th className="px-3 py-2 font-medium">原 ID</th>
                      <th className="px-3 py-2 font-medium">原始姓名</th>
                      <th className="px-3 py-2 font-medium">新 ID</th>
                      <th className="px-3 py-2 font-medium">处理方式</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pageEntries.length === 0 && (
                      <tr className="border-t border-border/60">
                        <td colSpan={6} className="px-3 py-6 text-center text-muted-foreground">
                          {nameFilter === 'all'
                            ? '文件中没有可预览的武将'
                            : '当前筛选条件下没有记录，可切换为「全部武将」查看'}
                        </td>
                      </tr>
                    )}
                    {pageEntries.map((item) => (
                      <tr
                        key={item.index}
                        className={cn(
                          'border-t border-border/60',
                          !item.selectable && 'opacity-60',
                          item.selectable && 'cursor-pointer hover:bg-secondary/40',
                          item.selected && 'bg-primary/5',
                          item.overwrite && 'bg-gold/10',
                        )}
                        onClick={() => item.selectable && toggleOne(item.index)}
                      >
                        <td className="px-3 py-1.5">
                          <Checkbox
                            checked={item.selected}
                            disabled={!item.selectable}
                            onCheckedChange={() => item.selectable && toggleOne(item.index)}
                            onClick={(e) => e.stopPropagation()}
                            aria-label={`选择 ${item.from || '未命名'}`}
                          />
                        </td>
                        <td className="px-3 py-1.5 text-muted-foreground">{item.index + 1}</td>
                        <td className="px-3 py-1.5 text-muted-foreground">{item.rawId ?? '—'}</td>
                        <td className="px-3 py-1.5">{item.from || '（空）'}</td>
                        <td className="px-3 py-1.5 font-medium">
                          {item.skipped ? (
                            <span className="text-muted-foreground">跳过</span>
                          ) : item.overwrite ? (
                            <span className="text-gold">{item.id}</span>
                          ) : item.selected ? (
                            <span className="text-primary">{item.id}</span>
                          ) : (
                            <span className="text-muted-foreground">—</span>
                          )}
                        </td>
                        <td className="px-3 py-1.5">
                          {item.skipped ? (
                            <span className="text-destructive">姓名缺失</span>
                          ) : (
                            <span className="flex flex-wrap items-center gap-1.5">
                              {item.conflict === 'custom' &&
                                item.targetName &&
                                item.targetName !== item.from && (
                                  <>
                                    <span className="text-muted-foreground line-through">
                                      {item.targetName}
                                    </span>
                                    <span className="text-muted-foreground">→</span>
                                  </>
                                )}
                              <span
                                className={cn(
                                  item.overwrite
                                    ? 'text-gold'
                                    : item.selected
                                      ? 'text-foreground'
                                      : 'text-muted-foreground',
                                )}
                              >
                                {item.from}
                              </span>
                              {item.conflict === 'custom' ? (
                                <Badge
                                  variant="outline"
                                  className={cn(
                                    'text-[10px]',
                                    item.selected
                                      ? 'border-gold/50 bg-gold/15 text-gold'
                                      : 'border-border text-muted-foreground',
                                  )}
                                >
                                  {item.selected ? `覆盖 #${item.targetId}` : '同名·勾选即覆盖'}
                                </Badge>
                              ) : item.conflict === 'base' || item.conflict === 'file' ? (
                                <Badge
                                  variant="outline"
                                  className="border-destructive/40 text-[10px] text-destructive"
                                >
                                  {item.conflict === 'base'
                                    ? '与基础武将库同名·不可导入'
                                    : '与本文件其它记录同名·不可导入'}
                                </Badge>
                              ) : (
                                <Badge variant="outline" className="text-[10px]">
                                  {item.selected ? '新建导入' : '未选择'}
                                </Badge>
                              )}
                            </span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* 分页工具栏：可翻页浏览文件中的全部武将，勾选状态跨页保留 */}
              <div className="flex flex-wrap items-center justify-between gap-2 text-[11px] text-muted-foreground">
                <span>
                  共 {plan.length} 条
                  {nameFilter !== 'all' && `，筛选出 ${filteredPlan.length} 条`}
                  ，当前显示第 {filteredPlan.length ? (currentPage - 1) * pageSize + 1 : 0} ~{' '}
                  {Math.min(currentPage * pageSize, filteredPlan.length)} 条（批量按钮作用于全部记录）
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
                  <Select
                    value={String(pageSize)}
                    onValueChange={(value) => {
                      setPageSize(Number(value))
                      setPage(1)
                      tableScrollRef.current?.scrollTo({ top: 0 })
                    }}
                  >
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
            </div>
          )}

          {/* 第三步：导入结果 */}
          {step === 'done' && result && (
            <div className="flex flex-col gap-4">
              <div className="flex flex-col items-center gap-2 rounded-lg border border-primary/30 bg-primary/5 px-4 py-6">
                <CheckCircle2 className="size-8 text-primary" />
                <p className="font-display text-base">导入完成</p>
                <p className="text-xs text-muted-foreground">
                  新建 {result.imported} 位、覆盖 {result.overwritten.length} 位，库中现有{' '}
                  {result.count} 位
                </p>
              </div>

              <div className="grid grid-cols-2 gap-2 sm:grid-cols-5">
                <PreviewTile label="新建武将" value={result.imported} />
                <PreviewTile
                  label="覆盖自建"
                  value={result.overwritten.length}
                  highlight={result.overwritten.length > 0}
                />
                <PreviewTile label="跳过" value={result.skipped} highlight={result.skipped > 0} />
                <PreviewTile
                  label="同名未导入"
                  value={result.conflicts?.length ?? 0}
                  highlight={(result.conflicts?.length ?? 0) > 0}
                />
                <PreviewTile label="下一个 ID" value={result.nextId} />
              </div>

              {result.overwritten.length > 0 && (
                <div className="flex flex-col gap-2 rounded-lg border border-gold/30 bg-gold/5 p-3">
                  <span className="text-xs font-medium">
                    已覆盖的自建武将（{result.overwritten.length}，均保留原 ID）
                  </span>
                  <div className="flex max-h-40 flex-col gap-1 overflow-y-auto">
                    {result.overwritten.map((item) => (
                      <div key={item.id} className="flex items-center gap-2 text-xs">
                        <span className="font-mono text-muted-foreground">#{item.id}</span>
                        <span className="text-gold">{item.name}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {(result.conflicts?.length ?? 0) > 0 && (
                <div className="flex flex-col gap-2 rounded-lg border border-border bg-card/40 p-3">
                  <span className="text-xs font-medium">
                    因同名未导入的武将（{result.conflicts.length}）
                  </span>
                  <div className="flex max-h-40 flex-col gap-1 overflow-y-auto">
                    {result.conflicts.map((item, index) => (
                      <div key={`${item.name}-${index}`} className="flex items-center gap-2 text-xs">
                        <span>{item.name}</span>
                        <span className="text-[10px] text-muted-foreground">{item.reason}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

        <DialogFooter className="border-t border-border px-5 py-4">
          {step === 'pick' && (
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              取消
            </Button>
          )}

          {step === 'preview' && (
            <>
              <Button variant="outline" onClick={backToPick} disabled={importing}>
                <ArrowLeft />
                重新选择
              </Button>
              <Button onClick={handleImport} disabled={importing || stats.selected === 0}>
                {importing ? <Loader2 className="animate-spin" /> : <FileUp />}
                确认导入 {stats.selected} 位
                {stats.overwritten > 0 && `（覆盖 ${stats.overwritten} 位）`}
              </Button>
            </>
          )}

          {step === 'done' && (
            <>
              <Button variant="outline" onClick={backToPick}>
                <RotateCcw />
                继续导入
              </Button>
              <Button onClick={() => onOpenChange(false)}>完成</Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** 预览统计小卡片 */
function PreviewTile({
  label,
  value,
  highlight = false,
}: {
  label: string
  value: number
  highlight?: boolean
}) {
  return (
    <div className="flex flex-col rounded-lg border border-border bg-card/60 px-3 py-2">
      <span className="text-[11px] text-muted-foreground">{label}</span>
      <span
        className={cn('font-display text-lg leading-tight', highlight ? 'text-primary' : 'text-gold')}
      >
        {value}
      </span>
    </div>
  )
}
