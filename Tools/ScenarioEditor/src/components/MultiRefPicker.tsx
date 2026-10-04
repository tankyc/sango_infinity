/**
 * 多选引用编辑器 / 成对数组编辑器
 *
 * 剧本里有不少「数组字段」其元素其实是别的表的 ID：
 * - polyRef：配偶、喜欢 / 厌恶武将、相邻都市、同盟势力、特技列表
 * - pairArray：随身物品 / 城市库存（[物品类型, 数量] 成对存放）
 *
 * 这类字段此前只能手敲一串逗号分隔的数字，容易写错且看不出含义。
 * 本模块把它们换成「按名称勾选」「按名称选择物品 + 数量」的交互，
 * 同时保留「手动输入」入口，便于批量粘贴或填写表中没有的 ID。
 */
import { useEffect, useMemo, useState } from 'react'
import { Check, ListPlus, Map as MapIcon, Plus, Search, Trash2, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { CollectionKey, Entity, Options, OptionItem } from '@/lib/types'
import { COLLECTION_META } from '@/lib/types'
import type { FieldDef } from '@/lib/schema'
import { resolveEnum } from '@/lib/schema'
import { getArray } from '@/lib/fields'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Checkbox } from '@/components/ui/checkbox'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { CityMapPicker } from './CityMapPicker'

/** 候选项 */
interface Candidate {
  id: number
  name: string
}

/** 下拉候选项上限 */
const MAX_CANDIDATES = 600

interface MultiRefPickerProps {
  field: FieldDef
  values: number[]
  onChange: (next: number[]) => void
  options: Options | null
  referenceNames?: Record<CollectionKey, Map<number, string>>
  disabled?: boolean
}

/**
 * 取字段的候选项列表。
 *
 * @param field 字段定义
 * @param options 公共数据表
 * @param referenceNames 剧本集合名称表
 * @returns 候选项数组
 */
function candidatesOf(
  field: FieldDef,
  options: Options | null,
  referenceNames?: Record<CollectionKey, Map<number, string>>
): Candidate[] {
  // 剧本集合引用
  const collection = field.ref ?? field.itemRef
  if (collection && referenceNames?.[collection]) {
    return [...referenceNames[collection].entries()]
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.id - b.id)
  }
  // 硬编码枚举
  const entries = resolveEnum(field, options)
  if (entries.length > 0) {
    return entries.map((e) => ({ id: Number(e.value), name: String(e.label) }))
  }
  // 公共数据表
  const optionKey = field.optionsKey ?? field.itemOptionsKey
  if (optionKey && options) {
    const list = (options as unknown as Record<string, unknown>)[optionKey] as OptionItem[] | undefined
    if (Array.isArray(list)) {
      return [...list]
        .map((x) => ({ id: Number(x.id), name: String(x.name ?? '') }))
        .filter((x) => Number.isFinite(x.id))
        .sort((a, b) => a.id - b.id)
    }
  }
  return []
}

/**
 * 多选引用编辑器。
 */
export function MultiRefPicker({
  field,
  values,
  onChange,
  options,
  referenceNames,
  disabled,
}: MultiRefPickerProps) {
  const [open, setOpen] = useState(false)
  const [keyword, setKeyword] = useState('')
  const [draft, setDraft] = useState<number[]>(values)
  const [manual, setManual] = useState(false)
  const [manualText, setManualText] = useState(values.join(', '))
  const [mapOpen, setMapOpen] = useState(false)

  /** 元素是都市时额外提供地图勾选入口 */
  const cityRef = (field.ref ?? field.itemRef) === 'citySet'

  useEffect(() => {
    if (open) {
      setDraft(values)
      setManualText(values.join(', '))
      setKeyword('')
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const candidates = useMemo(() => candidatesOf(field, options, referenceNames), [field, options, referenceNames])
  const nameMap = useMemo(() => new Map(candidates.map((c) => [c.id, c.name])), [candidates])

  const filtered = useMemo(() => {
    const kw = keyword.trim().toLowerCase()
    const list = kw
      ? candidates.filter((c) => String(c.id).includes(kw) || c.name.toLowerCase().includes(kw))
      : candidates
    return list.slice(0, MAX_CANDIDATES)
  }, [candidates, keyword])

  /** 长度上限：arrayLen 既可能是固定长度数字，也可能是允许长度的数组 */
  const limit =
    typeof field.arrayLen === 'number'
      ? field.arrayLen
      : field.arrayMaxLen ?? (Array.isArray(field.arrayLen) ? Math.max(...field.arrayLen) : undefined)

  const toggle = (id: number) => {
    setDraft((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]))
  }

  const commitManual = () => {
    const next = manualText
      .split(/[,，\s;；、]+/)
      .map((s) => Number(s.trim()))
      .filter((n) => Number.isFinite(n))
    setDraft(next)
    onChange(next)
    setOpen(false)
  }

  /** 已选值的展示文本 */
  const display = values.length === 0 ? '（空）' : values.map((v) => nameMap.get(v) ?? String(v)).join('、')

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          disabled={disabled}
          className={cn(
            'flex h-7 w-full items-center gap-1 overflow-hidden rounded border border-transparent px-1.5 text-left text-[12px] transition-colors',
            'hover:border-border hover:bg-accent/60',
            disabled && 'cursor-not-allowed opacity-50'
          )}
          title={display}
        >
          <ListPlus className="h-3 w-3 shrink-0 text-muted-foreground" />
          <span className="min-w-0 flex-1 truncate">{display}</span>
          {values.length > 0 && (
            <span className="shrink-0 text-[10px] tabular text-muted-foreground">{values.length}</span>
          )}
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-[340px] p-0">
        <div className="flex items-center gap-1.5 border-b border-border px-2 py-1.5">
          <span className="truncate text-[12px] font-medium">{field.label}</span>
          <Badge variant="outline" className="h-4 shrink-0 px-1 text-[10px] tabular">
            {field.key}
          </Badge>
          {cityRef && (
            <Button
              variant="ghost"
              size="sm"
              className="h-6 gap-1 px-1.5 text-[10px]"
              title="在地图上按方位勾选"
              onClick={() => setMapOpen(true)}
            >
              <MapIcon className="h-3 w-3" />
              地图选择
            </Button>
          )}
          <button
            type="button"
            className={cn(
              'ml-auto shrink-0 rounded px-1.5 py-0.5 text-[10px] transition-colors',
              manual ? 'bg-primary/15 text-foreground' : 'text-muted-foreground hover:text-foreground'
            )}
            onClick={() => setManual((v) => !v)}
          >
            {manual ? '选择模式' : '手动输入'}
          </button>
        </div>

        {manual ? (
          <div className="space-y-2 p-2">
            <p className="text-[11px] text-muted-foreground">
              用逗号或空格分隔多个 {field.itemLabel ?? 'ID'}，例如：1, 5, 12
            </p>
            <Input
              className="h-8 font-mono text-[12px]"
              value={manualText}
              onChange={(e) => setManualText(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') commitManual()
              }}
              autoFocus
            />
            <div className="flex justify-end gap-1.5">
              <Button variant="outline" size="sm" className="h-7" onClick={() => setOpen(false)}>
                取消
              </Button>
              <Button size="sm" className="h-7" onClick={commitManual}>
                应用
              </Button>
            </div>
          </div>
        ) : (
          <>
            <div className="relative border-b border-border px-2 py-1.5">
              <Search className="pointer-events-none absolute left-3.5 top-1/2 h-3 w-3 -translate-y-1/2 text-muted-foreground" />
              <Input
                className="h-7 pl-6 text-[12px]"
                placeholder="搜索名称或 ID…"
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
              />
            </div>
            <div className="max-h-[240px] overflow-y-auto p-1">
              {filtered.length === 0 ? (
                <p className="py-6 text-center text-[11px] text-muted-foreground">
                  {candidates.length === 0 ? '该字段没有可选候选表，请用手动输入' : '没有匹配项'}
                </p>
              ) : (
                filtered.map((c) => (
                  <label
                    key={c.id}
                    className="flex cursor-pointer items-center gap-2 rounded px-1.5 py-1 text-[12px] transition-colors hover:bg-accent"
                  >
                    <Checkbox checked={draft.includes(c.id)} onCheckedChange={() => toggle(c.id)} />
                    <span className="tabular w-10 shrink-0 text-muted-foreground">{c.id}</span>
                    <span className="min-w-0 flex-1 truncate">{c.name}</span>
                  </label>
                ))
              )}
              {candidates.length > MAX_CANDIDATES && (
                <p className="px-1.5 py-1 text-[10px] text-muted-foreground">
                  候选项过多，仅显示前 {MAX_CANDIDATES} 项，其余请手动输入
                </p>
              )}
            </div>
            <div className="flex flex-wrap items-center gap-1 border-t border-border px-2 py-1.5">
              {draft.slice(0, 6).map((id) => (
                <span
                  key={id}
                  className="inline-flex items-center gap-1 rounded bg-muted px-1.5 py-0.5 text-[10px]"
                >
                  {nameMap.get(id) ?? id}
                  <button
                    type="button"
                    className="text-muted-foreground hover:text-destructive"
                    onClick={() => toggle(id)}
                  >
                    <X className="h-2.5 w-2.5" />
                  </button>
                </span>
              ))}
              {draft.length > 6 && <span className="text-[10px] text-muted-foreground">…共 {draft.length} 个</span>}
              {limit !== undefined && draft.length > limit && (
                <span className="text-[10px] text-amber-500">超出长度上限 {limit}</span>
              )}
              <div className="ml-auto flex gap-1.5">
                <Button variant="ghost" size="sm" className="h-7 px-2 text-[11px]" onClick={() => setDraft([])}>
                  清空
                </Button>
                <Button
                  size="sm"
                  className="h-7 gap-1 text-[11px]"
                  onClick={() => {
                    onChange(draft)
                    setOpen(false)
                  }}
                >
                  <Check className="h-3 w-3" />
                  应用（{draft.length}）
                </Button>
              </div>
            </div>
          </>
        )}
      </PopoverContent>
      {cityRef && (
        <CityMapPicker
          open={mapOpen}
          onOpenChange={setMapOpen}
          multiple
          values={draft}
          onChange={(ids) => {
            setDraft(ids)
            onChange(ids)
          }}
          title={`在地图上选择${field.label}`}
        />
      )}
    </Popover>
  )
}

interface PairArrayEditorProps {
  field: FieldDef
  values: number[]
  onChange: (next: number[]) => void
  options: Options | null
  disabled?: boolean
}

/**
 * 成对数组编辑器（[物品类型, 数量]）。
 */
export function PairArrayEditor({ field, values, onChange, options, disabled }: PairArrayEditorProps) {
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<number[]>(values)

  useEffect(() => {
    if (open) setDraft(values)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const candidates = useMemo(() => candidatesOf(field, options), [field, options])
  const nameMap = useMemo(() => new Map(candidates.map((c) => [c.id, c.name])), [candidates])

  /** 把扁平的成对数组拆成 { id, count } 行 */
  const rows = useMemo(() => {
    const list: { id: number; count: number }[] = []
    for (let i = 0; i + 1 < draft.length; i += 2) list.push({ id: draft[i], count: draft[i + 1] })
    if (draft.length % 2 === 1) list.push({ id: draft[draft.length - 1], count: 0 })
    return list
  }, [draft])

  const flatten = (list: { id: number; count: number }[]) => list.flatMap((r) => [r.id, r.count])

  const update = (index: number, patch: Partial<{ id: number; count: number }>) => {
    const next = rows.map((r, i) => (i === index ? { ...r, ...patch } : r))
    setDraft(flatten(next))
  }

  const display =
    rows.length === 0 ? '（空）' : rows.map((r) => `${nameMap.get(r.id) ?? r.id}×${r.count}`).join('、')

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          disabled={disabled}
          className={cn(
            'flex h-7 w-full items-center gap-1 overflow-hidden rounded border border-transparent px-1.5 text-left text-[12px] transition-colors',
            'hover:border-border hover:bg-accent/60',
            disabled && 'cursor-not-allowed opacity-50'
          )}
          title={display}
        >
          <ListPlus className="h-3 w-3 shrink-0 text-muted-foreground" />
          <span className="min-w-0 flex-1 truncate">{display}</span>
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-[360px] p-0">
        <div className="flex items-center gap-1.5 border-b border-border px-2 py-1.5">
          <span className="text-[12px] font-medium">{field.label}</span>
          <Badge variant="outline" className="h-4 px-1 text-[10px] tabular">
            {field.key}
          </Badge>
          <span className="ml-auto text-[10px] text-muted-foreground">成对存放 [物品, 数量]</span>
        </div>
        <div className="max-h-[260px] space-y-1.5 overflow-y-auto p-2">
          {rows.length === 0 && <p className="py-4 text-center text-[11px] text-muted-foreground">暂无条目</p>}
          {rows.map((r, i) => (
            <div key={i} className="flex items-center gap-1.5">
              <Select value={String(r.id)} onValueChange={(v) => update(i, { id: Number(v) })}>
                <SelectTrigger className="h-8 min-w-0 flex-1 text-[12px]">
                  <SelectValue placeholder="选择物品" />
                </SelectTrigger>
                <SelectContent className="max-h-[280px]">
                  {candidates.length === 0 && <SelectItem value={String(r.id)}>{`#${r.id}`}</SelectItem>}
                  {candidates.map((c) => (
                    <SelectItem key={c.id} value={String(c.id)}>
                      <span className="tabular text-muted-foreground">{c.id}</span>
                      <span className="ml-2">{c.name}</span>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <input
                type="number"
                className="tabular h-8 w-[86px] shrink-0 rounded border border-border bg-background px-1.5 text-[12px] outline-none focus:border-primary"
                value={String(r.count)}
                onChange={(e) => update(i, { count: Number(e.target.value) || 0 })}
              />
              <Button
                variant="ghost"
                size="icon"
                className="h-7 w-7 shrink-0 text-muted-foreground hover:text-destructive"
                onClick={() => setDraft(flatten(rows.filter((_, j) => j !== i)))}
              >
                <Trash2 className="h-3 w-3" />
              </Button>
            </div>
          ))}
        </div>
        <div className="flex items-center gap-1.5 border-t border-border px-2 py-1.5">
          <Button
            variant="outline"
            size="sm"
            className="h-7 gap-1 text-[11px]"
            onClick={() => setDraft(flatten([...rows, { id: candidates[0]?.id ?? 0, count: 1 }]))}
          >
            <Plus className="h-3 w-3" />
            添加一行
          </Button>
          <div className="ml-auto flex gap-1.5">
            <Button variant="ghost" size="sm" className="h-7 px-2 text-[11px]" onClick={() => setDraft([])}>
              清空
            </Button>
            <Button
              size="sm"
              className="h-7 gap-1 text-[11px]"
              onClick={() => {
                onChange(draft)
                setOpen(false)
              }}
            >
              <Check className="h-3 w-3" />
              应用
            </Button>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  )
}

/**
 * 判断字段是否适合用多选引用 / 成对数组编辑器。
 *
 * @param field 字段定义
 * @returns 编辑器类型；'plain' 表示用普通输入
 */
export function arrayEditorKind(field: FieldDef): 'multiRef' | 'pair' | 'plain' {
  if (field.kind === 'pairArray' && (field.itemOptionsKey || field.itemRef)) return 'pair'
  if (field.kind === 'polyRef') return 'multiRef'
  if (field.kind === 'intArray' && (field.itemOptionsKey || field.itemRef || field.itemEnumMap)) return 'multiRef'
  return 'plain'
}

/** 把字段的数组值规范化成数字数组 */
export function fieldArrayValues(entity: Entity, field: FieldDef): number[] {
  return getArray(entity[field.key]).map((n) => Number(n)).filter((n) => Number.isFinite(n))
}

/** 把引用目标翻译成中文集合名（用于提示） */
export function refTargetLabel(field: FieldDef): string {
  const collection = field.ref ?? field.itemRef
  if (collection) return COLLECTION_META[collection].label
  return field.itemLabel ?? 'ID'
}
