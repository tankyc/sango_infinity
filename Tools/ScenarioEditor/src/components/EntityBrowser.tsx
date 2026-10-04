/**
 * 集合浏览器
 *
 * 面向武将 / 都市 / 军团 / 势力的通用浏览与编辑界面，包含：
 * - 关键字搜索、归属筛选、仅看问题项、排序
 * - 虚拟滚动表格（桌面端）与卡片列表（移动端）
 * - 单元格快捷编辑（单击进入、Tab/Enter 连续跳转）
 * - 多选（支持 Shift 连选、全选筛选结果）与批量 / 统一编辑入口
 * - 列显示设置（本地持久化）
 */
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  AlertTriangle,
  ArrowDown,
  ArrowUp,
  CheckSquare,
  Columns3,
  Filter,
  Layers,
  LayoutGrid,
  ListChecks,
  Pencil,
  Plus,
  Rows3,
  Search,
  Square,
  Table2,
  Trash2,
  X,
} from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import type { CollectionKey, Entity, ValidationIssue } from '@/lib/types'
import { COLLECTION_META } from '@/lib/types'
import type { FieldDef } from '@/lib/schema'
import { COLLECTION_FIELDS, compactFields, resolveEnum } from '@/lib/schema'
import {
  buildFieldValue,
  buildNameLookup,
  abilityQualityColor,
  attrQualityColor,
  formatFieldValue,
  formatFieldValueFull,
  getAbilityLevel,
  getAttrBase,
  getScalarValue,
  type NameLookup,
} from '@/lib/fields'
import { buildForceColorMap, forceColorOf } from '@/lib/cityMap'
import { filterEntities, sortEntities, type SortState } from '@/lib/indexes'
import { buildScenarioNameMaps, entityDisplayName } from '@/lib/refResolver'
import { computeColumnWidths } from '@/lib/autoColumns'
import { useScenarioStore, READONLY_COLLECTIONS } from '@/state/store'
import { useVirtualList } from '@/hooks/useVirtualList'
import { useIsMobile } from '@/hooks/use-mobile'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Checkbox } from '@/components/ui/checkbox'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { EntityDetailSheet } from './EntityDetailSheet'
import { BatchEditDialog } from './BatchEditDialog'
import { MultiRefPicker, PairArrayEditor, arrayEditorKind, fieldArrayValues } from './MultiRefPicker'
import { CityMapView } from './CityMapView'
import { CityMapValueButton } from './CityMapPicker'

/** 行高（需与表格样式保持一致） */
const ROW_HEIGHT = 34
/** 移动端卡片行高 */
const CARD_ROW_HEIGHT = 92

/** 列宽配置存储键 */
const COLUMN_STORAGE_PREFIX = 'scenario-editor:columns:'

interface EntityBrowserProps {
  collection: CollectionKey
  /** 外部（命令面板 / 校验面板）要求打开某条记录的详情 */
  focusTarget?: { id: number; nonce: number } | null
}

/**
 * 集合浏览器。
 */
export function EntityBrowser({ collection, focusTarget = null }: EntityBrowserProps) {
  const store = useScenarioStore()
  const { scenario, options, issuesByEntity, patchEntity, addEntity, removeEntities, nextEntityId } = store
  const isMobile = useIsMobile()
  const fields = COLLECTION_FIELDS[collection]
  const meta = COLLECTION_META[collection]

  const allColumns = useMemo(() => compactFields(fields), [fields])
  const [visibleKeys, setVisibleKeys] = useState<string[]>(() => {
    try {
      const saved = localStorage.getItem(COLUMN_STORAGE_PREFIX + collection)
      if (saved) {
        const parsed = JSON.parse(saved) as string[]
        const valid = parsed.filter((k) => allColumns.some((c) => c.key === k))
        if (valid.length > 0) return valid
      }
    } catch {
      /* 忽略损坏的本地设置 */
    }
    return allColumns.map((c) => c.key)
  })

  // 集合切换时重置列设置与视图模式
  useEffect(() => {
    setView('table')
    try {
      const saved = localStorage.getItem(COLUMN_STORAGE_PREFIX + collection)
      if (saved) {
        const parsed = (JSON.parse(saved) as string[]).filter((k) => allColumns.some((c) => c.key === k))
        if (parsed.length > 0) {
          setVisibleKeys(parsed)
          return
        }
      }
    } catch {
      /* 忽略 */
    }
    setVisibleKeys(allColumns.map((c) => c.key))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [collection])

  const columns = useMemo(
    () => allColumns.filter((c) => visibleKeys.includes(c.key)),
    [allColumns, visibleKeys],
  )

  const [keyword, setKeyword] = useState('')
  const [forceFilter, setForceFilter] = useState<string>('all')
  const [issueOnly, setIssueOnly] = useState(false)
  const [sort, setSort] = useState<SortState>({ key: 'Id', direction: 'asc' })
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [activeCell, setActiveCell] = useState<{
    id: number
    key: string
  } | null>(null)
  const [detailId, setDetailId] = useState<number | null>(null)
  /** 都市集合额外提供「地图」视图；其余集合恒为表格 */
  const [view, setView] = useState<'table' | 'map'>('table')
  const [batchTargets, setBatchTargets] = useState<number[] | null>(null)
  const lastClickedRef = useRef<number | null>(null)

  // 响应外部定位请求：直接打开对应记录的详情面板
  useEffect(() => {
    if (focusTarget && focusTarget.id > 0) {
      setDetailId(focusTarget.id)
    }
  }, [focusTarget])

  /* ---------------- 数据整理 ---------------- */

  const allRows = useMemo(() => {
    if (!scenario) return [] as Entity[]
    return Object.values((scenario[collection] || {}) as Record<string, Entity>).filter(
      (x) => x && typeof x === 'object',
    )
  }, [scenario, collection])

  const referenceNames = useMemo(() => buildScenarioNameMaps(scenario), [scenario])

  /** 引用名称查找表：剧本集合 + 公共数据表，供表格把 ID 显示成名称 */
  const nameLookup = useMemo<NameLookup>(
    () => buildNameLookup(referenceNames, options),
    [options, referenceNames],
  )

  /** 势力旗帜色：势力 Id -> Flags.json 的 color */
  const forceColors = useMemo(() => buildForceColorMap(scenario, options), [options, scenario])

  const filteredRows = useMemo(() => {
    let rows = filterEntities(allRows, keyword)
    if (issueOnly) {
      rows = rows.filter((r) => {
        const issues = issuesByEntity.get(`${collection}:${Number(r.Id)}`)
        return issues && issues.some((i) => i.level === 'error' || i.level === 'warning')
      })
    }
    // 势力筛选：势力集合本身没有归属字段，故仅在其它三个集合上生效
    if (forceFilter !== 'all' && collection !== 'forceSet') {
      rows = rows.filter((r) => String(r.BelongForce ?? '') === forceFilter)
    }
    return sortEntities(rows, sort.key, sort.direction)
  }, [allRows, keyword, issueOnly, forceFilter, sort, issuesByEntity, collection])

  const rowIndexById = useMemo(() => {
    const map = new Map<number, number>()
    filteredRows.forEach((r, i) => map.set(Number(r.Id), i))
    return map
  }, [filteredRows])

  /* ---------------- 虚拟滚动 ---------------- */

  const vlist = useVirtualList({
    count: filteredRows.length,
    itemHeight: isMobile ? CARD_ROW_HEIGHT : ROW_HEIGHT,
    overscan: 10,
  })

  /* ---------------- 选择逻辑 ---------------- */

  const handleSelectToggle = useCallback(
    (id: number, shiftKey: boolean) => {
      setSelected((prev) => {
        const next = new Set(prev)
        const index = rowIndexById.get(id) ?? -1
        if (shiftKey && lastClickedRef.current !== null) {
          const from = rowIndexById.get(lastClickedRef.current) ?? -1
          if (from >= 0 && index >= 0) {
            const [a, b] = from < index ? [from, index] : [index, from]
            const shouldSelect = !next.has(id)
            for (let i = a; i <= b; i++) {
              const rowId = Number(filteredRows[i]?.Id)
              if (!Number.isFinite(rowId)) continue
              if (shouldSelect) next.add(rowId)
              else next.delete(rowId)
            }
          }
        } else {
          if (next.has(id)) next.delete(id)
          else next.add(id)
        }
        lastClickedRef.current = id
        return next
      })
    },
    [filteredRows, rowIndexById],
  )

  const selectAllFiltered = useCallback(() => {
    setSelected(new Set(filteredRows.map((r) => Number(r.Id))))
  }, [filteredRows])

  const clearSelection = useCallback(() => setSelected(new Set()), [])

  const invertSelection = useCallback(() => {
    setSelected((prev) => {
      const next = new Set<number>()
      for (const r of filteredRows) {
        const id = Number(r.Id)
        if (!prev.has(id)) next.add(id)
      }
      return next
    })
  }, [filteredRows])

  /* ---------------- 单元格编辑 ---------------- */

  const editableColumns = useMemo(
    () => columns.filter((c) => c.kind !== 'polyRef' && c.kind !== 'intArray' && c.kind !== 'pairArray'),
    [columns],
  )

  const [draft, setDraft] = useState<string>('')
  const [draftOrigin, setDraftOrigin] = useState<{
    id: number
    key: string
  } | null>(null)

  useEffect(() => {
    if (!activeCell) return
    const index = rowIndexById.get(activeCell.id)
    if (index !== undefined) vlist.scrollToIndex(index)
    const cell = activeCell
    const raf = requestAnimationFrame(() => {
      const el = document.querySelector<HTMLElement>(`[data-cell="${cell.id}:${cell.key}"]`)
      if (el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement) {
        el.focus()
        el.select()
      } else {
        el?.focus()
      }
    })
    return () => cancelAnimationFrame(raf)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeCell])

  const activateCell = useCallback((entity: Entity, field: FieldDef) => {
    if (field.noBatch && field.kind === 'int') {
      setDetailId(Number(entity.Id))
      return
    }
    setActiveCell({ id: Number(entity.Id), key: field.key })
    setDraft(String(getScalarValue(entity, field) ?? ''))
    setDraftOrigin({ id: Number(entity.Id), key: field.key })
  }, [])

  const commitCell = useCallback(
    (entity: Entity, field: FieldDef, raw: string) => {
      const next = buildFieldValue(entity, field, raw)
      patchEntity(collection, Number(entity.Id), { [field.key]: next })
    },
    [collection, patchEntity],
  )

  /** 直接写入数组值（多选引用 / 成对数组编辑器用） */
  const commitArrayCell = useCallback(
    (entity: Entity, field: FieldDef, values: number[]) => {
      const current = entity[field.key]
      // 保持与其它数组字段一致的数组形态（剧本里都是纯数字数组）
      const next = Array.isArray(current) ? values : values
      patchEntity(collection, Number(entity.Id), { [field.key]: next })
    },
    [collection, patchEntity],
  )

  const moveCell = useCallback(
    (current: { id: number; key: string }, dRow: number, dCol: number) => {
      if (editableColumns.length === 0) return
      let rowIndex = rowIndexById.get(current.id) ?? 0
      let colIndex = editableColumns.findIndex((c) => c.key === current.key)
      if (colIndex < 0) colIndex = 0
      colIndex += dCol
      if (colIndex >= editableColumns.length) {
        colIndex = 0
        rowIndex += 1
      } else if (colIndex < 0) {
        colIndex = editableColumns.length - 1
        rowIndex -= 1
      }
      rowIndex += dRow
      if (rowIndex < 0 || rowIndex >= filteredRows.length) return
      const nextRow = filteredRows[rowIndex]
      const nextField = editableColumns[colIndex]
      setActiveCell({ id: Number(nextRow.Id), key: nextField.key })
      setDraft(String(getScalarValue(nextRow, nextField) ?? ''))
      setDraftOrigin({ id: Number(nextRow.Id), key: nextField.key })
    },
    [editableColumns, filteredRows, rowIndexById],
  )

  /* ---------------- 排序与列设置 ---------------- */

  const toggleSort = useCallback((key: string) => {
    setSort((prev) =>
      prev.key === key
        ? { key, direction: prev.direction === 'asc' ? 'desc' : 'asc' }
        : { key, direction: 'asc' },
    )
  }, [])

  const toggleColumn = useCallback(
    (key: string) => {
      setVisibleKeys((prev) => {
        const next = prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key]
        const ordered = allColumns.map((c) => c.key).filter((k) => next.includes(k))
        try {
          localStorage.setItem(COLUMN_STORAGE_PREFIX + collection, JSON.stringify(ordered))
        } catch {
          /* 忽略存储异常 */
        }
        return ordered
      })
    },
    [allColumns, collection],
  )

  /* ---------------- 新增 / 删除 ---------------- */

  const handleCreate = useCallback(() => {
    if (!scenario) return
    if (READONLY_COLLECTIONS.has(collection)) {
      toast.warning(`${meta.singular}不允许新增`)
      return
    }
    const id = nextEntityId(collection)
    const entity: Entity = { Id: id, Name: `新建${meta.singular}` }
    // 依据字段定义补全最小可用字段，避免产生大量缺少必需字段的错误
    for (const f of fields) {
      if (f.key === 'Id' || f.key === 'Name' || f.noBatch) continue
      if (f.ref || f.kind === 'ref') {
        if (f.zeroMeansNone) entity[f.key] = 0
      }
    }
    if (collection === 'personSet') {
      Object.assign(entity, {
        BelongForce: 0,
        BelongCorps: 0,
        BelongCity: 0,
        CurrentCity: 0,
        state: 8,
        sex: 0,
        command: [50, 5],
        strength: [50, 5],
        intelligence: [50, 5],
        politics: [50, 5],
        glamour: [50, 5],
        spearLv: [0],
        halberdLv: [0],
        crossbowLv: [0],
        rideLv: [0],
        waterLv: [0],
        machineLv: [0],
        Level: 0,
        Official: 0,
        loyalty: 0,
        personality: 3,
        argumentation: 3,
      })
    }
    if (collection === 'citySet') {
      Object.assign(entity, {
        BelongForce: 0,
        BuildingType: 1,
        CityLevelType: 1,
        province: 1,
        x: 0,
        y: 0,
      })
    }
    if (collection === 'corpsSet') {
      Object.assign(entity, {
        BelongForce: 0,
        Comander: 0,
        number: 1,
        policy: 1,
        policy_target: 0,
      })
    }
    if (collection === 'forceSet') {
      Object.assign(entity, { Governor: 0, Flag: 0, Title: 1 })
    }
    addEntity(collection, entity)
    setDetailId(id)
    toast.success(`已新增${meta.singular}（Id ${id}），请补全字段`)
  }, [addEntity, collection, fields, meta.singular, nextEntityId, scenario])

  const handleDelete = useCallback(
    (ids: number[]) => {
      if (ids.length === 0) return
      if (READONLY_COLLECTIONS.has(collection)) {
        toast.warning(`${meta.singular}不允许删除`)
        return
      }
      removeEntities(collection, ids)
      setSelected(new Set())
      toast.success(`已删除 ${ids.length} 个${meta.singular}（可用撤销恢复）`)
    },
    [collection, meta.singular, removeEntities],
  )

  /* ---------------- 渲染 ---------------- */

  /** 都市地图视图：隐藏表头与虚拟滚动列表 */
  const isMapView = collection === 'citySet' && view === 'map'
  /** 武将与城池不允许新增 / 删除（数据由武将库与地图决定，误删会破坏剧本） */
  const readOnlyCollection = READONLY_COLLECTIONS.has(collection)

  /**
   * 列宽方案。
   *
   * 表格宽度严格等于「勾选列 + 各内容列 + 操作列」之和，**不拉伸到容器宽度**。
   * 这样行分隔线与竖线都停在内容右边缘，右侧留白是干净的空白区，
   * 而不会因为行分隔线横穿空白区而看起来像多出一列空单元格。
   */
  const ACTION_WIDTH = 84
  /**
   * 列与列之间的留白。
   *
   * 没有间隔时相邻两列的文字只靠各自的 `px-2` 隔开，密度一高就糊成一片，
   * 例如「所属都市 / 所在都市」都是城市名时根本分不清谁是谁。
   * 用 grid 的 column-gap 而不是给列加 padding，是因为轨道宽度即列宽
   * （`columnWidth`），加 padding 会把文字挤窄；gap 则纯粹是额外空白。
   */
  const CELL_GAP = 6
  // 轨道数是「n 个数据列 + 1 个操作列」= n+1，因此 gap 数是 n 而不是 n-1
  const gapTotal = CELL_GAP * columns.length

  /**
   * 内容自适应列宽。
   *
   * 逐列量「表头」与「当前筛选结果里所有单元格文本」的最大宽度，
   * 所以列宽只够放下真实内容，不会像固定列宽那样凭白留出一大截。
   */
  const colWidths = useMemo(
    () => computeColumnWidths({ columns, rows: filteredRows, options, lookup: nameLookup }),
    [columns, filteredRows, options, nameLookup]
  )
  const widthOf = (field: FieldDef): number => colWidths[field.key] ?? 120

  const totalWidth =
    36 + columns.reduce((sum, c) => sum + widthOf(c), 0) + gapTotal + ACTION_WIDTH
  const gridTemplateColumns = `${columns.map((c) => `${widthOf(c)}px`).join(' ')} ${ACTION_WIDTH}px`

  const selectedIds = useMemo(() => [...selected], [selected])
  const batchRows = useMemo(
    () =>
      (batchTargets ? batchTargets : [])
        .map((id) => (scenario?.[collection] as Record<string, Entity>)?.[String(id)])
        .filter(Boolean) as Entity[],
    [batchTargets, collection, scenario],
  )

  const issueCountFor = (id: number): { error: number; warning: number } => {
    const list = issuesByEntity.get(`${collection}:${id}`) ?? []
    return {
      error: list.filter((i) => i.level === 'error').length,
      warning: list.filter((i) => i.level === 'warning').length,
    }
  }

  return (
    <div className="flex h-full min-h-0 flex-col">
      {/* 工具栏 */}
      <div className="flex flex-wrap items-center gap-2 border-b border-border px-3 py-2">
        <div className="relative min-w-[180px] flex-1 sm:max-w-[320px]">
          <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="h-8 pl-7"
            placeholder={`搜索${meta.singular}名称 / ID…`}
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
          />
          {keyword && (
            <button
              type="button"
              className="absolute right-1.5 top-1/2 -translate-y-1/2 rounded p-0.5 text-muted-foreground hover:text-foreground"
              onClick={() => setKeyword('')}
              aria-label="清空搜索"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          )}
        </div>

        {collection !== 'forceSet' && (
          <Select value={forceFilter} onValueChange={setForceFilter}>
            <SelectTrigger className="h-8 w-[132px] shrink-0">
              <SelectValue />
            </SelectTrigger>
            <SelectContent className="max-h-[300px]">
              <SelectItem value="all">全部势力</SelectItem>
              {[...referenceNames.forceSet.entries()]
                .sort((a, b) => a[0] - b[0])
                .map(([id, name]) => (
                  <SelectItem key={id} value={String(id)}>
                    <span className="tabular text-muted-foreground">{id}</span>
                    <span className="ml-2">{name}</span>
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        )}

        <Button
          variant={issueOnly ? 'default' : 'outline'}
          size="sm"
          className="h-8 shrink-0 gap-1.5"
          onClick={() => setIssueOnly((v) => !v)}
        >
          <Filter className="h-3.5 w-3.5" />
          仅看问题
          {store.issueCounts.error + store.issueCounts.warning > 0 && (
            <Badge variant="secondary" className="ml-0.5 h-4 px-1 text-[10px]">
              {store.issueCounts.error + store.issueCounts.warning}
            </Badge>
          )}
        </Button>

        <Popover>
          <PopoverTrigger asChild>
            <Button variant="outline" size="sm" className="h-8 shrink-0 gap-1.5">
              <Columns3 className="h-3.5 w-3.5" />列
            </Button>
          </PopoverTrigger>
          <PopoverContent className="w-56 p-2" align="start">
            <p className="px-1 pb-1.5 text-[11px] text-muted-foreground">勾选需要显示的列</p>
            <div className="max-h-[300px] space-y-0.5 overflow-y-auto">
              {allColumns.map((c) => (
                <label
                  key={c.key}
                  className="flex cursor-pointer items-center gap-2 rounded px-1.5 py-1 text-[12px] hover:bg-accent"
                >
                  <Checkbox
                    checked={visibleKeys.includes(c.key)}
                    onCheckedChange={() => toggleColumn(c.key)}
                  />
                  <span className="truncate">{c.label}</span>
                </label>
              ))}
            </div>
          </PopoverContent>
        </Popover>

        <div className="ml-auto flex items-center gap-2">
          {collection === 'citySet' && (
            <div className="flex overflow-hidden rounded-md border border-border">
              <Button
                variant={view === 'table' ? 'secondary' : 'ghost'}
                size="sm"
                className="h-8 gap-1 rounded-none border-0 px-2 text-[11px]"
                onClick={() => setView('table')}
              >
                <Rows3 className="h-3 w-3" />
                表格
              </Button>
              <Button
                variant={view === 'map' ? 'secondary' : 'ghost'}
                size="sm"
                className="h-8 gap-1 rounded-none border-0 border-l border-border px-2 text-[11px]"
                onClick={() => setView('map')}
              >
                <LayoutGrid className="h-3 w-3" />
                地图
              </Button>
            </div>
          )}
          <span className="hidden text-[11px] text-muted-foreground sm:block">
            共 <span className="tabular text-foreground">{allRows.length}</span> 条
            {(keyword || issueOnly || forceFilter !== 'all') && (
              <>
                ，筛选出 <span className="tabular text-foreground">{filteredRows.length}</span> 条
              </>
            )}
          </span>
          {!readOnlyCollection && (
            <Button size="sm" className="h-8 gap-1.5" onClick={handleCreate}>
              <Plus className="h-3.5 w-3.5" />
              新增
            </Button>
          )}
        </div>
      </div>

      {/* 选择工具栏 */}
      {selected.size > 0 && (
        <div className="flex flex-wrap items-center gap-2 border-b border-primary/30 bg-primary/10 px-3 py-1.5">
          <span className="text-[12px]">
            已选 <span className="tabular font-medium">{selected.size}</span> 个{meta.singular}
          </span>
          <Button variant="outline" size="sm" className="h-7 gap-1.5" onClick={selectAllFiltered}>
            <CheckSquare className="h-3.5 w-3.5" />
            全选筛选结果
          </Button>
          <Button variant="outline" size="sm" className="h-7 gap-1.5" onClick={invertSelection}>
            <Square className="h-3.5 w-3.5" />
            反选
          </Button>
          <Button
            variant="default"
            size="sm"
            className="h-7 gap-1.5"
            onClick={() => setBatchTargets(selectedIds)}
          >
            <Pencil className="h-3.5 w-3.5" />
            批量编辑
          </Button>
          <Button
            variant="outline"
            size="sm"
            className="h-7 gap-1.5"
            onClick={() => setBatchTargets(filteredRows.map((r) => Number(r.Id)))}
          >
            <Layers className="h-3.5 w-3.5" />
            统一编辑筛选结果（{filteredRows.length}）
          </Button>
          <Button
            variant="outline"
            size="sm"
            className="h-7 gap-1.5 text-destructive hover:text-destructive"
            onClick={() => handleDelete(selectedIds)}
            disabled={readOnlyCollection}
            title={readOnlyCollection ? `${meta.singular}不允许删除` : undefined}
          >
            <Trash2 className="h-3.5 w-3.5" />
            删除
          </Button>
          <Button variant="ghost" size="sm" className="ml-auto h-7" onClick={clearSelection}>
            取消选择
          </Button>
        </div>
      )}

      {/* 都市地图视图：按 x / y 坐标绘制，与上方搜索、势力筛选、仅看问题联动 */}
      {isMapView && (
        <CityMapView
          query={keyword}
          forceFilter={forceFilter}
          issueOnly={issueOnly}
          onOpenDetail={(id) => setDetailId(id)}
          view={view}
          onViewChange={setView}
        />
      )}

      {/* 列表主体：表头与数据放在同一个滚动容器里，水平方向共用一个滚动条 */}
      {!isMapView && (
        <div
          ref={vlist.containerRef}
          onScroll={vlist.onScroll}
          className="scroll-stable min-h-0 flex-1 overflow-auto"
        >
          {/* 粘性表头（桌面端）：与数据行同构，保证列永远对齐 */}
          {!isMobile && (
            <div
              className="sticky top-0 z-10 flex border-b border-border/40 bg-card"
              style={{ width: totalWidth }}
            >
              <div className="dense-cell flex h-8 w-9 shrink-0 items-center justify-center">
                <Checkbox
                  checked={
                    filteredRows.length > 0 && filteredRows.every((r) => selected.has(Number(r.Id)))
                      ? true
                      : selected.size > 0
                        ? 'indeterminate'
                        : false
                  }
                  onClick={(e) => {
                    e.preventDefault()
                    if (selected.size > 0) clearSelection()
                    else selectAllFiltered()
                  }}
                  aria-label="全选"
                />
              </div>
              {/* 表头不加 items-center，让每个列头撑满表头高度，列线才能上下贯穿 */}
              <div className="grid flex-1" style={{ gridTemplateColumns, columnGap: CELL_GAP }}>
                {columns.map((c) => (
                  <button
                    key={c.key}
                    type="button"
                    className="dense-cell flex h-8 min-w-0 items-center gap-1 truncate px-2 text-left text-[11px] font-medium text-muted-foreground transition-colors hover:text-foreground"
                    onClick={() => toggleSort(c.key)}
                    title={`${c.label}（${c.key}）${c.desc ? ` — ${c.desc}` : ''}`}
                  >
                    <span className="truncate">{c.label}</span>
                    {sort.key === c.key &&
                      (sort.direction === 'asc' ? (
                        <ArrowUp className="h-3 w-3 shrink-0" />
                      ) : (
                        <ArrowDown className="h-3 w-3 shrink-0" />
                      ))}
                  </button>
                ))}
                {/* 操作列表头：与数据行的最后一轨对应，补上右边界线封闭表格 */}
                <div className="dense-cell h-8" />
              </div>
            </div>
          )}
          {filteredRows.length === 0 ? (
            <div className="flex h-full flex-col items-center justify-center gap-2 py-16 text-muted-foreground">
              <Table2 className="h-8 w-8 opacity-40" />
              <p className="text-[13px]">没有匹配的{meta.singular}</p>
              {(keyword || issueOnly || forceFilter !== 'all') && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setKeyword('')
                    setIssueOnly(false)
                    setForceFilter('all')
                  }}
                >
                  清除筛选条件
                </Button>
              )}
            </div>
          ) : isMobile ? (
            <div style={{ height: vlist.totalHeight, position: 'relative' }}>
              {vlist.virtualItems.map((vi) => {
                const row = filteredRows[vi.index]
                if (!row) return null
                return (
                  <MobileCard
                    key={String(row.Id)}
                    entity={row}
                    fields={columns}
                    options={options}
                    top={vi.offset}
                    height={CARD_ROW_HEIGHT}
                    selected={selected.has(Number(row.Id))}
                    issues={issueCountFor(Number(row.Id))}
                    referenceNames={referenceNames}
                    lookup={nameLookup}
                    displayName={entityDisplayName(row, collection, referenceNames)}
                    onToggle={() => handleSelectToggle(Number(row.Id), false)}
                    onOpen={() => setDetailId(Number(row.Id))}
                  />
                )
              })}
            </div>
          ) : (
            <div
              className="dense-body"
              style={{
                height: vlist.totalHeight,
                position: 'relative',
                width: totalWidth,
              }}
            >
              {vlist.virtualItems.map((vi) => {
                const row = filteredRows[vi.index]
                if (!row) return null
                const id = Number(row.Id)
                const issues = issueCountFor(id)
                return (
                  <div
                    key={String(row.Id)}
                    className="dense-row absolute flex text-[12px]"
                    data-selected={selected.has(id)}
                    data-active={activeCell?.id === id}
                    style={{
                      top: vi.offset,
                      height: ROW_HEIGHT,
                      width: totalWidth,
                    }}
                  >
                    <div className="dense-cell flex w-9 shrink-0 items-center justify-center">
                      <Checkbox
                        checked={selected.has(id)}
                        onClick={(e) => {
                          e.preventDefault()
                          handleSelectToggle(id, e.shiftKey)
                        }}
                        aria-label={`选择 ${entityDisplayName(row, collection, referenceNames)}`}
                      />
                    </div>
                    {/* 不设 alignItems，让每个单元格撑满行高，列线才能贯穿整行 */}
                    <div className="grid flex-1" style={{ gridTemplateColumns, columnGap: CELL_GAP }}>
                      {columns.map((c) => (
                        /* 包裹层撑满行高（grid 默认 stretch），列线才能贯穿整行；
                         垂直居中交给这里的 items-center，不能用 grid 的 alignItems，
                         否则包裹层高度退化为内容高度，列线就会在行间断开 */
                        <div key={c.key} className="dense-cell flex min-w-0 items-center overflow-hidden">
                          <DataCell
                            entity={row}
                            field={c}
                            options={options}
                            referenceNames={referenceNames}
                            lookup={nameLookup}
                            forceColors={forceColors}
                            width={widthOf(c)}
                            active={activeCell?.id === id && activeCell?.key === c.key}
                            draft={draftOrigin?.id === id && draftOrigin?.key === c.key ? draft : null}
                            issues={issuesByEntity.get(`${collection}:${id}`) ?? []}
                            onClick={() => activateCell(row, c)}
                            onDraftChange={setDraft}
                            onCommit={(raw) => {
                              commitCell(row, c, raw)
                              setActiveCell(null)
                              setDraftOrigin(null)
                            }}
                            onCommitArray={(values) => {
                              commitArrayCell(row, c, values)
                              setActiveCell(null)
                              setDraftOrigin(null)
                            }}
                            onCancel={() => {
                              setActiveCell(null)
                              setDraftOrigin(null)
                            }}
                            onNavigate={(dRow, dCol) => {
                              if (draftOrigin) commitCell(row, c, draft)
                              moveCell({ id, key: c.key }, dRow, dCol)
                            }}
                          />
                        </div>
                      ))}
                      {/* 操作列：错误数 / 警告数 / 详情按钮，右边界封闭表格 */}
                      <div className="dense-cell flex items-center justify-end gap-1 pr-2">
                        {issues.error > 0 && (
                          <Badge variant="destructive" className="h-4 px-1 text-[10px] tabular">
                            {issues.error}
                          </Badge>
                        )}
                        {issues.warning > 0 && (
                          <Badge
                            variant="outline"
                            className="h-4 border-amber-500/60 px-1 text-[10px] tabular text-amber-500"
                          >
                            {issues.warning}
                          </Badge>
                        )}
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-6 w-6"
                          onClick={() => setDetailId(id)}
                          title="打开详情"
                        >
                          <Pencil className="h-3 w-3" />
                        </Button>
                      </div>
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>
      )}

      {/* 底部状态栏 */}
      {!isMapView && (
        <div className="flex shrink-0 items-center gap-3 border-t border-border px-3 py-1 text-[11px] text-muted-foreground">
          <span>
            显示 {vlist.virtualItems.length} / {filteredRows.length} 行（虚拟滚动）
          </span>
          <span className="flex items-center gap-1">
            <ListChecks className="h-3 w-3" />
            单击单元格进入编辑，Tab/Enter 连续跳转，Esc 取消
          </span>
        </div>
      )}

      {/* 详情抽屉 */}
      <EntityDetailSheet
        collection={collection}
        entityId={detailId}
        onClose={() => setDetailId(null)}
        onSelectEntity={setDetailId}
      />

      {/* 批量编辑对话框 */}
      <BatchEditDialog
        open={batchTargets !== null}
        onOpenChange={(v) => !v && setBatchTargets(null)}
        collection={collection}
        targetIds={batchTargets ?? []}
        filteredCount={filteredRows.length}
        onSwitchToFiltered={() => setBatchTargets(filteredRows.map((r) => Number(r.Id)))}
        sampleEntities={batchRows}
      />
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* 单元格                                                            */
/* ------------------------------------------------------------------ */

interface DataCellProps {
  entity: Entity
  field: FieldDef
  options: ReturnType<typeof useScenarioStore>['options']
  referenceNames: Record<CollectionKey, Map<number, string>>
  /** 引用名称查找表（剧本集合 + 公共数据表），用于把 ID 显示成名称 */
  lookup: NameLookup
  /** 势力 Id -> 旗帜色，用于给「所属势力」加色点 */
  forceColors: Map<number, string>
  /** 该列的内容自适应宽度（由 computeColumnWidths 统一算出） */
  width: number
  active: boolean
  draft: string | null
  issues: ValidationIssue[]
  onClick: () => void
  onDraftChange: (v: string) => void
  onCommit: (raw: string) => void
  /** 直接写入数组值（多选引用 / 成对数组） */
  onCommitArray: (values: number[]) => void
  onCancel: () => void
  onNavigate: (dRow: number, dCol: number) => void
}

/**
 * 数据单元格：非激活态展示文本，激活态切换为编辑控件。
 */
function DataCell({
  entity,
  field,
  options,
  referenceNames,
  lookup,
  forceColors,
  width,
  active,
  draft,
  issues,
  onClick,
  onDraftChange,
  onCommit,
  onCommitArray,
  onCancel,
  onNavigate,
}: DataCellProps) {
  const errorIssue = issues.find((i) => i.field === field.key && i.level === 'error')
  const warnIssue = issues.find((i) => i.field === field.key && i.level === 'warning')
  const text = formatFieldValue(entity, field, options, lookup)
  /** 悬浮提示展示「名称 (Id)」形式，便于核对原始值 */
  const fullText = formatFieldValueFull(entity, field, options, lookup)

  /**
   * 数值/档位评级色。
   *
   * 五维（统率 / 武力 / 智力 / 政治 / 魅力）按数值分级，
   * 兵种适性按档位名称分级（Ｓ 红 / Ａ 紫 / Ｂ 绿）；
   * 两者都让位于问题提示色——「数值高但有错」时更需要先看到错误。
   */
  const qualityColor = (() => {
    if (errorIssue || warnIssue) return undefined
    if (field.kind === 'attr') return attrQualityColor(getAttrBase(entity[field.key]))
    if (field.kind === 'ability') {
      const hit = resolveEnum(field, options).find((e) => e.value === getAbilityLevel(entity[field.key]))
      return hit ? abilityQualityColor(hit.label) : undefined
    }
    return undefined
  })()

  /** Tab/Enter 跳转时置位，用于跳过随后的 blur 提交，避免覆盖跳转结果 */
  const navigatingRef = useRef(false)

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Tab') {
      e.preventDefault()
      navigatingRef.current = true
      onNavigate(0, e.shiftKey ? -1 : 1)
    } else if (e.key === 'Enter') {
      e.preventDefault()
      navigatingRef.current = true
      onNavigate(1, 0)
    } else if (e.key === 'Escape') {
      e.preventDefault()
      navigatingRef.current = true
      onCancel()
    }
  }

  const handleBlur = () => {
    if (navigatingRef.current) {
      navigatingRef.current = false
      return
    }
    onCommit(draft ?? '')
  }

  // Id 列为只读：不响应点击，纯粹展示（放在所有 Hook 之后以遵守 Hook 规则）
  if (field.noBatch && field.kind === 'int') {
    return (
      <span className="tabular block truncate px-2 text-[12px] text-muted-foreground" style={{ width }}>
        {text}
      </span>
    )
  }

  if (active) {
    // 数组型的引用字段（配偶 / 特技 / 物品…）用勾选式编辑器，避免手敲 ID
    const arrayKind = arrayEditorKind(field)
    if (arrayKind === 'multiRef') {
      return (
        <div className="px-0.5" style={{ width }}>
          <MultiRefPicker
            field={field}
            values={fieldArrayValues(entity, field)}
            onChange={(next) => onCommitArray(next)}
            options={options}
            referenceNames={referenceNames}
          />
        </div>
      )
    }
    if (arrayKind === 'pair') {
      return (
        <div className="px-0.5" style={{ width }}>
          <PairArrayEditor
            field={field}
            values={fieldArrayValues(entity, field)}
            onChange={(next) => onCommitArray(next)}
            options={options}
          />
        </div>
      )
    }
    // 枚举 / 引用：直接给下拉框
    const entries = resolveEnum(field, options)
    const refMap = field.ref ? referenceNames[field.ref] : null
    if (field.kind === 'enum' && entries.length > 0) {
      return (
        <div className="px-0.5" style={{ width }}>
          <Select value={String(draft ?? '')} onValueChange={(v) => onCommit(v)}>
            <SelectTrigger
              className="h-7 w-full min-w-0 px-1.5 text-[12px]"
              data-cell={`${entity.Id}:${field.key}`}
              autoFocus
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent className="max-h-[280px]">
              {entries.map((en) => (
                <SelectItem key={en.value} value={String(en.value)}>
                  <span className="tabular text-muted-foreground">{en.value}</span>
                  <span className="ml-2">{en.label}</span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )
    }
    // 都市引用：点击直接弹地图，87 座城按名字搜容易选错方位
    if (field.kind === 'ref' && field.ref === 'citySet') {
      const current = Number(entity[field.key] ?? 0)
      return (
        <div className="px-0.5" style={{ width }}>
          <CityMapValueButton
            className="h-7"
            value={current}
            name={referenceNames.citySet.get(current) ?? ''}
            title="点击打开地图选择都市"
            onChange={(next) => {
              onCommit(next === null ? '0' : String(next))
            }}
          />
        </div>
      )
    }
    if ((field.kind === 'ref' || field.kind === 'enum') && refMap) {
      return (
        <div className="px-0.5" style={{ width }}>
          <Select value={String(draft ?? '')} onValueChange={(v) => onCommit(v)}>
            <SelectTrigger
              className="h-7 w-full min-w-0 px-1.5 text-[12px]"
              data-cell={`${entity.Id}:${field.key}`}
              autoFocus
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent className="max-h-[280px]">
              {field.zeroMeansNone && <SelectItem value="0">无</SelectItem>}
              {[...refMap.entries()]
                .sort((a, b) => a[0] - b[0])
                .slice(0, 500)
                .map(([id, name]) => (
                  <SelectItem key={id} value={String(id)}>
                    <span className="tabular text-muted-foreground">{id}</span>
                    <span className="ml-2">{name}</span>
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        </div>
      )
    }
    if (field.kind === 'ref' && field.optionsKey && entries.length > 0) {
      return (
        <div className="px-0.5" style={{ width }}>
          <Select value={String(draft ?? '')} onValueChange={(v) => onCommit(v)}>
            <SelectTrigger
              className="h-7 w-full min-w-0 px-1.5 text-[12px]"
              data-cell={`${entity.Id}:${field.key}`}
              autoFocus
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent className="max-h-[280px]">
              {entries.map((en) => (
                <SelectItem key={en.value} value={String(en.value)}>
                  <span className="tabular text-muted-foreground">{en.value}</span>
                  <span className="ml-2">{en.label}</span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )
    }
    return (
      <div className="px-0.5" style={{ width }}>
        <input
          data-cell={`${entity.Id}:${field.key}`}
          className="h-7 w-full rounded border border-primary bg-background px-1.5 text-[12px] tabular outline-none"
          value={draft ?? ''}
          inputMode={field.kind === 'string' ? 'text' : 'numeric'}
          onChange={(e) => onDraftChange(e.target.value)}
          onKeyDown={handleKeyDown}
          onBlur={handleBlur}
        />
      </div>
    )
  }

  return (
    <TooltipProvider delayDuration={400}>
      <Tooltip>
        <TooltipTrigger asChild>
          <button
            type="button"
            className={cn(
              'h-7 w-full truncate rounded px-2 text-left tabular transition-colors hover:bg-accent/70',
              field.kind === 'string' && 'text-left',
              // 问题提示色优先于五维评级色，避免「高数值但有错」时看不出问题
              (errorIssue || warnIssue) && (errorIssue ? 'text-destructive' : 'text-amber-500'),
            )}
            style={{ width, color: qualityColor }}
            onClick={onClick}
          >
            {/* 势力引用：前置色点，颜色取自该势力的旗帜 */}
            {field.ref === 'forceSet' &&
              (() => {
                const fid = Number(entity[field.key] ?? 0)
                if (fid <= 0) return null
                return (
                  <span
                    className="mr-1.5 inline-block h-2 w-2 shrink-0 rounded-full align-[1px] ring-1 ring-black/20"
                    style={{ background: forceColorOf(fid, forceColors) }}
                  />
                )
              })()}
            {text || <span className="text-muted-foreground/50">—</span>}
          </button>
        </TooltipTrigger>
        <TooltipContent side="bottom" className="max-w-[320px] text-[11px]">
          <p className="font-medium">
            {field.label} <span className="font-mono opacity-60">{field.key}</span>
          </p>
          <p className="opacity-80">当前值：{fullText || '（空）'}</p>
          {(errorIssue || warnIssue) && (
            <p className={cn('mt-1', errorIssue ? 'text-destructive' : 'text-amber-500')}>
              {errorIssue?.message ?? warnIssue?.message}
            </p>
          )}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}

/* ------------------------------------------------------------------ */
/* 移动端卡片                                                         */
/* ------------------------------------------------------------------ */

interface MobileCardProps {
  entity: Entity
  fields: FieldDef[]
  options: ReturnType<typeof useScenarioStore>['options']
  top: number
  height: number
  selected: boolean
  issues: { error: number; warning: number }
  referenceNames: Record<CollectionKey, Map<number, string>>
  lookup: NameLookup
  displayName: string
  onToggle: () => void
  onOpen: () => void
}

/**
 * 移动端卡片：以紧凑的两行摘要展示关键字段。
 */
function MobileCard({
  entity,
  fields,
  options,
  top,
  height,
  selected,
  issues,
  lookup,
  displayName,
  onToggle,
  onOpen,
}: MobileCardProps) {
  const [primary, ...rest] = fields
  const visible = rest.slice(0, 5)
  return (
    <div
      className={cn(
        'absolute left-0 right-0 mx-2 rounded-lg border border-border bg-card px-3 py-2',
        selected && 'border-primary bg-primary/10',
      )}
      style={{ top: top + 4, height: height - 8 }}
    >
      <div className="flex items-start gap-2">
        <Checkbox checked={selected} onCheckedChange={onToggle} className="mt-0.5" aria-label="选择" />
        <button type="button" className="min-w-0 flex-1 text-left" onClick={onOpen}>
          <div className="flex items-center gap-2">
            <span className="truncate text-[14px] font-medium">{displayName}</span>
            <span className="tabular text-[11px] text-muted-foreground">#{String(entity.Id)}</span>
            {issues.error > 0 && (
              <Badge variant="destructive" className="h-4 px-1 text-[10px]">
                {issues.error}
              </Badge>
            )}
            {issues.warning > 0 && (
              <Badge variant="outline" className="h-4 border-amber-500/60 px-1 text-[10px] text-amber-500">
                {issues.warning}
              </Badge>
            )}
          </div>
          <div className="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-[11px] text-muted-foreground">
            <span>
              {primary.label}
              <span className="ml-1 tabular text-foreground">
                {formatFieldValue(entity, primary, options, lookup) || '—'}
              </span>
            </span>
            {visible.map((f) => (
              <span key={f.key}>
                {f.label}
                <span className="ml-1 tabular text-foreground">
                  {formatFieldValue(entity, f, options, lookup) || '—'}
                </span>
              </span>
            ))}
          </div>
        </button>
        <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" onClick={onOpen} aria-label="编辑">
          <Pencil className="h-3.5 w-3.5" />
        </Button>
      </div>
      {issues.error > 0 && (
        <p className="mt-1 flex items-center gap-1 text-[10px] text-destructive">
          <AlertTriangle className="h-3 w-3" />
          存在校验错误，点击卡片查看
        </p>
      )}
    </div>
  )
}
