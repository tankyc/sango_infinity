/**
 * 公共数据表 · 记录表视图
 *
 * 用于「以 id 为键的对象集合」或「对象数组」形态的数据表（绝大多数 Common 文件）。
 * 特点：
 * - 列取自全部记录键的并集，Id / Name 固定在最前，点击表头可排序；
 * - 虚拟滚动，833 条的内置武将库也能流畅浏览；
 * - 单元格内联编辑，引用字段直接下拉选名称；
 * - 对象形态下修改 Id 会同步重命名集合键，保持「键 == Id」的文件约定；
 * - 行详情用通用 JSON 编辑器完整展开，覆盖嵌套数组与对象。
 */
import { useCallback, useEffect, useMemo, useState } from 'react'
import { ArrowDownUp, ChevronRight, Copy, Plus, Search, Table2, Trash2, TriangleAlert, X } from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import type { CommonSection } from '@/lib/types'
import type { CommonFileMeta, RefSpec } from '@/lib/commonMeta'
import { commonFieldLabel, commonFieldNote, commonFieldRef } from '@/lib/commonMeta'
import type { JsonPath } from '@/lib/jsoncEdit'
import { isPlainObject } from '@/lib/jsoncEdit'
import { useCommonStore } from '@/state/commonStore'
import { useVirtualList } from '@/hooks/useVirtualList'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
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
import { JsonCellEditor, JsonValueEditor } from './JsonValueEditor'

/** 行高 */
const ROW_HEIGHT = 34
/** 采集列时最多扫描的记录数 */
const SCAN_LIMIT = 400
/** 列数上限 */
const MAX_COLUMNS = 60

/** 一条记录 */
export interface RecordEntry {
  /** 集合键（对象形态为字符串键，数组形态为下标字符串） */
  key: string
  /** 数组下标；对象形态为 null */
  index: number | null
  /** 记录本身 */
  record: Record<string, unknown>
}

interface CommonRecordTableProps {
  section: CommonSection
  fileMeta?: CommonFileMeta
}

/**
 * 记录表视图。
 */
export function CommonRecordTable({ section, fileMeta }: CommonRecordTableProps) {
  const { value, setValue, removeValue, pushValue, doc } = useCommonStore()

  const [keyword, setKeyword] = useState('')
  const [sortKey, setSortKey] = useState<string | null>(null)
  const [sortAsc, setSortAsc] = useState(true)
  const [confirmDelete, setConfirmDelete] = useState<RecordEntry | null>(null)
  /** 详情抽屉只保存键，记录本身每次渲染从最新数据重新取，避免显示过期值 */
  const [detailKey, setDetailKey] = useState<string | null>(null)

  const idField = fileMeta?.idField ?? 'Id'
  const nameField = fileMeta?.nameField ?? 'Name'

  const basePath = useMemo<JsonPath>(() => (section.key === null ? [] : [section.key]), [section.key])
  const isObjectForm = section.form === 'object'

  /** 原始记录集合 */
  const entries = useMemo<RecordEntry[]>(() => {
    let body: unknown = value
    for (const seg of basePath) {
      if (isPlainObject(body)) body = body[String(seg)]
      else if (Array.isArray(body)) body = body[Number(seg)]
    }
    if (Array.isArray(body)) {
      const list: RecordEntry[] = []
      body.forEach((item, i) => {
        if (isPlainObject(item)) list.push({ key: String(i), index: i, record: item })
      })
      return list
    }
    if (isPlainObject(body)) {
      const list: RecordEntry[] = []
      for (const [k, v] of Object.entries(body)) {
        if (isPlainObject(v)) list.push({ key: k, index: null, record: v })
      }
      return list
    }
    return []
  }, [basePath, value])

  /** 列：Id / Name 优先，其余按出现顺序 */
  const columns = useMemo(() => {
    const seen: string[] = []
    const seenSet = new Set<string>()
    for (const entry of entries.slice(0, SCAN_LIMIT)) {
      for (const k of Object.keys(entry.record)) {
        if (!seenSet.has(k)) {
          seenSet.add(k)
          seen.push(k)
        }
      }
      if (seen.length > MAX_COLUMNS * 2) break
    }
    const priority: string[] = []
    for (const k of [idField, nameField, 'name']) {
      if (seenSet.has(k) && !priority.includes(k)) priority.push(k)
    }
    return [...priority, ...seen.filter((k) => !priority.includes(k))].slice(0, MAX_COLUMNS)
  }, [entries, idField, nameField])

  /** 过滤 + 排序 */
  const rows = useMemo(() => {
    const kw = keyword.trim().toLowerCase()
    const nameOf = (e: RecordEntry) => String(e.record[nameField] ?? e.record.name ?? '')
    let list = entries
    if (kw) {
      list = entries.filter((e) => {
        if (e.key.toLowerCase().includes(kw)) return true
        if (nameOf(e).toLowerCase().includes(kw)) return true
        const text = Object.values(e.record)
          .filter((v) => !isPlainObject(v) && !Array.isArray(v))
          .slice(0, 40)
          .join(' ')
          .toLowerCase()
        return text.includes(kw)
      })
    }
    if (sortKey) {
      const dir = sortAsc ? 1 : -1
      list = [...list].sort((a, b) => {
        const va = a.record[sortKey]
        const vb = b.record[sortKey]
        if (typeof va === 'number' && typeof vb === 'number') return (va - vb) * dir
        return String(va ?? '').localeCompare(String(vb ?? ''), 'zh-Hans-CN') * dir
      })
    }
    return list
  }, [entries, keyword, sortKey, sortAsc, nameField])

  const vlist = useVirtualList({ count: rows.length, itemHeight: ROW_HEIGHT, overscan: 10 })

  useEffect(() => {
    setKeyword('')
    setSortKey(null)
    setDetailKey(null)
  }, [section.key])

  /** 记录路径 */
  const pathOf = useCallback(
    (entry: RecordEntry): JsonPath =>
      isObjectForm || entry.index === null ? [...basePath, entry.key] : [...basePath, entry.index],
    [basePath, isObjectForm]
  )

  const detailEntry = useMemo(
    () => (detailKey === null ? null : entries.find((e) => e.key === detailKey) ?? null),
    [detailKey, entries]
  )

  /** 新增记录 */
  const handleAdd = useCallback(() => {
    const template = (fileMeta?.template ?? {}) as Record<string, unknown>
    if (isObjectForm) {
      const used = new Set(entries.map((e) => e.key))
      let nextId = 1
      const numericKeys = [...used].map(Number).filter((n) => Number.isFinite(n))
      if (numericKeys.length > 0) nextId = Math.max(...numericKeys) + 1
      while (used.has(String(nextId))) nextId += 1
      const record: Record<string, unknown> = { ...template }
      if (record[idField] === undefined) record[idField] = nextId
      if (record[nameField] === undefined && idField === 'Id') record.Name = `条目${nextId}`
      setValue([...basePath, String(nextId)], record)
      setDetailKey(String(nextId))
      toast.success(`已新增条目 ${nextId}，请补充字段`)
    } else {
      const count = entries.length
      pushValue(basePath, Object.keys(template).length > 0 ? template : {})
      setDetailKey(String(count))
      toast.success('已在末尾追加一条空记录，请补充字段')
    }
  }, [basePath, entries, fileMeta, idField, isObjectForm, nameField, pushValue, setValue])

  /** 修改 Id 时同步集合键 */
  const handleIdChange = useCallback(
    (entry: RecordEntry, newId: number) => {
      const newKey = String(newId)
      if (newKey === entry.key) {
        setValue([...basePath, entry.key, idField], newId)
        return
      }
      if (entries.some((e) => e.key === newKey)) {
        toast.error(`键 ${newKey} 已存在，无法改名`)
        return
      }
      setValue([...basePath, newKey], { ...entry.record, [idField]: newId })
      removeValue([...basePath, entry.key])
      setDetailKey(newKey)
      toast.success(`已重命名集合键 ${entry.key} → ${newKey}`)
    },
    [basePath, entries, idField, removeValue, setValue]
  )

  const handleDelete = useCallback(
    (entry: RecordEntry) => {
      removeValue([...basePath, entry.index === null ? entry.key : entry.index])
      setConfirmDelete(null)
      if (detailKey === entry.key) setDetailKey(null)
      toast.success(`已删除条目 ${entry.key}（可用撤销恢复）`)
    },
    [basePath, detailKey, removeValue]
  )

  const handleCopy = useCallback(
    (entry: RecordEntry) => {
      const copy = JSON.parse(JSON.stringify(entry.record)) as Record<string, unknown>
      if (isObjectForm) {
        const used = new Set(entries.map((e) => e.key))
        let nextId = Number(copy[idField]) || 1
        while (used.has(String(nextId))) nextId += 1
        copy[idField] = nextId
        if (typeof copy[nameField] === 'string') copy[nameField] = `${copy[nameField]}（副本）`
        setValue([...basePath, String(nextId)], copy)
        setDetailKey(String(nextId))
      } else {
        pushValue(basePath, copy)
        setDetailKey(String(entries.length))
      }
      toast.success('已复制为一条新记录')
    },
    [basePath, entries, idField, isObjectForm, nameField, pushValue, setValue]
  )

  return (
    <div className="flex h-full min-h-0 flex-col">
      {/* 工具条 */}
      <div className="flex shrink-0 flex-wrap items-center gap-2 border-b border-border px-3 py-2">
        <div className="relative min-w-[170px] flex-1 sm:max-w-[320px]">
          <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="h-8 pl-7"
            placeholder="搜索任意字段值…"
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
          />
          {keyword && (
            <button
              type="button"
              className="absolute right-1.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
              onClick={() => setKeyword('')}
              aria-label="清空"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
        <Badge variant="secondary" className="tabular text-[10px]">
          {rows.length} / {entries.length} 条
        </Badge>
        <Button variant="outline" size="sm" className="h-7 gap-1.5 text-[11px]" onClick={handleAdd}>
          <Plus className="h-3 w-3" />
          新增条目
        </Button>
        <span className="hidden text-[10px] text-muted-foreground md:inline">
          {isObjectForm ? `键即 ${idField}，改 ${idField} 会自动同步键名` : '数组形态，删除会改变下标'}
        </span>
      </div>

      {/* 表体 */}
      {rows.length === 0 ? (
        <div className="flex flex-1 flex-col items-center justify-center gap-2 text-muted-foreground">
          <Table2 className="h-8 w-8 opacity-40" />
          <p className="text-[13px]">{entries.length === 0 ? '该分区暂无条目' : '没有匹配的记录'}</p>
          {keyword && (
            <Button variant="outline" size="sm" onClick={() => setKeyword('')}>
              清除搜索
            </Button>
          )}
        </div>
      ) : (
        <div ref={vlist.containerRef} onScroll={vlist.onScroll} className="scroll-stable min-h-0 flex-1 overflow-auto">
          <div className="sticky top-0 z-10 flex w-max min-w-full border-b border-border bg-card/95 backdrop-blur">
            {columns.map((key) => {
              const note = commonFieldNote(doc?.name ?? '', key)
              return (
                <button
                  key={key}
                  type="button"
                  className={cn(
                    'flex h-8 shrink-0 items-center gap-1 border-r border-border/60 px-2 text-left text-[11px] font-medium transition-colors hover:bg-accent/60',
                    key === idField ? 'w-[72px]' : key === nameField ? 'w-[136px]' : 'w-[130px]',
                    sortKey === key && 'text-primary'
                  )}
                  title={`${commonFieldLabel(key, fileMeta)} (${key})${note ? `\n⚠ ${note}` : ''}`}
                  onClick={() => {
                    if (sortKey === key) setSortAsc((v) => !v)
                    else {
                      setSortKey(key)
                      setSortAsc(true)
                    }
                  }}
                >
                  <span className="truncate">{commonFieldLabel(key, fileMeta)}</span>
                  {note && <TriangleAlert className="h-2.5 w-2.5 shrink-0 text-amber-500" />}
                  {sortKey === key ? (
                    <ArrowDownUp className={cn('h-3 w-3 shrink-0', !sortAsc && 'rotate-180')} />
                  ) : (
                    <span className="truncate font-mono text-[9px] text-muted-foreground">{key}</span>
                  )}
                </button>
              )
            })}
            <div className="h-8 w-[80px] shrink-0" />
          </div>

          <div style={{ height: vlist.totalHeight, position: 'relative' }}>
            {vlist.virtualItems.map((vi) => {
              const entry = rows[vi.index]
              if (!entry) return null
              const outOfSync =
                isObjectForm && entry.record[idField] !== undefined && String(entry.record[idField]) !== entry.key
              return (
                <div
                  key={`${entry.key}-${entry.index ?? 'o'}`}
                  className="group absolute flex w-max min-w-full items-center border-b border-border/40 hover:bg-accent/30"
                  style={{ top: vi.offset, height: ROW_HEIGHT }}
                >
                  {columns.map((key) => {
                    const spec: RefSpec | null = commonFieldRef(doc?.name ?? '', key)
                    const defined = Object.prototype.hasOwnProperty.call(entry.record, key)
                    return (
                      <div
                        key={key}
                        className={cn(
                          // overflow-hidden 是兜底：列宽固定，任何控件都不该越界到右列去
                          // （下拉浮层渲染在 Portal 里，不会被这里裁掉）
                          'shrink-0 overflow-hidden border-r border-border/30 px-0.5',
                          key === idField ? 'w-[72px]' : key === nameField ? 'w-[136px]' : 'w-[130px]'
                        )}
                      >
                        {key === idField && isObjectForm ? (
                          <input
                            type="number"
                            className={cn(
                              'tabular h-7 w-full rounded border bg-transparent px-1.5 text-[12px] outline-none focus:border-primary',
                              outOfSync ? 'border-amber-500' : 'border-transparent hover:border-border'
                            )}
                            title={outOfSync ? `集合键 ${entry.key} 与 ${idField} 不一致` : undefined}
                            defaultValue={String(entry.record[idField] ?? '')}
                            onBlur={(e) => {
                              const next = Number(e.target.value)
                              if (Number.isFinite(next) && String(next) !== entry.key) handleIdChange(entry, next)
                            }}
                            onKeyDown={(e) => {
                              if (e.key === 'Enter') (e.target as HTMLInputElement).blur()
                            }}
                          />
                        ) : defined ? (
                          <JsonCellEditor
                            value={entry.record[key]}
                            path={[...pathOf(entry), key]}
                            refSpec={spec}
                            onOpenDetail={() => setDetailKey(entry.key)}
                          />
                        ) : (
                          <button
                            type="button"
                            className="h-7 w-full rounded px-1.5 text-left text-[11px] text-muted-foreground/60 hover:bg-accent/60"
                            title="该记录没有这个字段，点击补全为空字符串"
                            onClick={() => setValue([...pathOf(entry), key], '')}
                          >
                            —
                          </button>
                        )}
                      </div>
                    )
                  })}
                  <div className="flex w-[80px] shrink-0 items-center justify-center gap-0.5 opacity-0 transition-opacity group-hover:opacity-100">
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-6 w-6"
                      title="详情"
                      onClick={() => setDetailKey(entry.key)}
                    >
                      <ChevronRight className="h-3.5 w-3.5" />
                    </Button>
                    <Button variant="ghost" size="icon" className="h-6 w-6" title="复制" onClick={() => handleCopy(entry)}>
                      <Copy className="h-3 w-3" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-6 w-6 text-muted-foreground hover:text-destructive"
                      title="删除"
                      onClick={() => setConfirmDelete(entry)}
                    >
                      <Trash2 className="h-3 w-3" />
                    </Button>
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      )}

      {/* 详情抽屉 */}
      <RecordDetail
        entry={detailEntry}
        fileName={doc?.name ?? ''}
        fileMeta={fileMeta}
        recordPath={detailEntry ? pathOf(detailEntry) : []}
        onClose={() => setDetailKey(null)}
        onCopy={handleCopy}
        onDelete={(e) => setConfirmDelete(e)}
      />

      <AlertDialog open={confirmDelete !== null} onOpenChange={(v) => !v && setConfirmDelete(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>确认删除该条目？</AlertDialogTitle>
            <AlertDialogDescription>
              将删除键为 <span className="font-medium text-foreground">{confirmDelete?.key}</span> 的条目。
              删除后引用它的其它数据可能失效，保存前可用撤销恢复。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground"
              onClick={() => confirmDelete && handleDelete(confirmDelete)}
            >
              确认删除
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

/** 详情抽屉 */
function RecordDetail({
  entry,
  fileName,
  fileMeta,
  recordPath,
  onClose,
  onCopy,
  onDelete,
}: {
  entry: RecordEntry | null
  fileName: string
  fileMeta?: CommonFileMeta
  recordPath: JsonPath
  onClose: () => void
  onCopy: (entry: RecordEntry) => void
  onDelete: (entry: RecordEntry) => void
}) {
  const idField = fileMeta?.idField ?? 'Id'
  const nameField = fileMeta?.nameField ?? 'Name'
  const title = String(entry?.record[nameField] ?? entry?.record.name ?? entry?.key ?? '')
  const outOfSync = entry !== null && isObjectFormKey(entry, idField)

  return (
    <Sheet open={entry !== null} onOpenChange={(v) => !v && onClose()}>
      <SheetContent side="right" className="flex w-full flex-col gap-0 p-0 sm:max-w-[600px]">
        <SheetHeader className="shrink-0 space-y-2 border-b border-border px-4 py-3">
          <div className="flex items-start gap-2">
            <div className="min-w-0 flex-1">
              <SheetTitle className="flex items-center gap-2 truncate text-[15px]">
                <span className="truncate">{title || `条目 ${entry?.key ?? ''}`}</span>
                <Badge variant="secondary" className="shrink-0 tabular text-[10px]">
                  #{entry?.key}
                </Badge>
              </SheetTitle>
              <p className="mt-1 text-[11px] text-muted-foreground">
                {fileMeta?.label ?? ''} · 修改立即写入内存，保存后落盘
              </p>
            </div>
            <Button variant="ghost" size="icon" className="h-8 w-8" onClick={onClose} aria-label="关闭">
              <X className="h-4 w-4" />
            </Button>
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            <Button variant="outline" size="sm" className="h-7 gap-1" onClick={() => entry && onCopy(entry)}>
              <Copy className="h-3.5 w-3.5" />
              复制为一条
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="ml-auto h-7 gap-1 text-destructive hover:text-destructive"
              onClick={() => entry && onDelete(entry)}
            >
              <Trash2 className="h-3.5 w-3.5" />
              删除
            </Button>
          </div>
        </SheetHeader>
        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
          {entry && (
            <div className="space-y-1.5">
              {outOfSync && (
                <p className="rounded border border-amber-500/40 bg-amber-500/10 px-2 py-1 text-[11px] text-amber-500">
                  注意：集合键是「{entry.key}」，而 {idField} 是「{String(entry.record[idField])}」，两者不一致。
                  建议在表格中直接改 {idField} 以自动同步键名。
                </p>
              )}
              <Separator />
              {Object.keys(entry.record).map((k) => {
                const note = commonFieldNote(fileName, k)
                return (
                  <div key={k}>
                    <JsonValueEditor
                      label={commonFieldLabel(k, fileMeta)}
                      value={entry.record[k]}
                      path={[...recordPath, k]}
                      depth={1}
                    />
                    {note && (
                      <p className="mt-0.5 pl-[100px] text-[10px] leading-snug text-amber-500/90">
                        <TriangleAlert className="mr-1 inline h-2.5 w-2.5 align-[-1px]" />
                        {note}
                      </p>
                    )}
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </SheetContent>
    </Sheet>
  )
}

/** 判断记录键与 Id 字段是否不一致 */
function isObjectFormKey(entry: RecordEntry, idField: string): boolean {
  if (entry.index !== null) return false
  const value = entry.record[idField]
  return value !== undefined && String(value) !== entry.key
}
