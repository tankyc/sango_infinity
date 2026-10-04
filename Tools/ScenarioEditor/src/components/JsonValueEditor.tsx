/**
 * 通用 JSON 值编辑器
 *
 * 公共数据表的 27 个文件结构差异很大（扁平配置、记录集合、嵌套数组、布尔开关…），
 * 逐个手写表单不现实，因此这里按「值的实际类型」递归生成编辑器：
 *
 * - 对象      -> 可折叠分组，逐键递归
 * - 记录数组  -> 可折叠的条目卡片列表，可增删
 * - 值数组    -> 行内标签列表，可增删（带引用时附加名称）
 * - 引用标量  -> 下拉选择，显示目标表的名称
 * - 普通标量  -> 数字 / 文本 / 布尔 对应控件
 *
 * 所有改动都通过 CommonEditContext 回调给 store，由 store 生成最小差异文本。
 */
import React, { createContext, useContext, useMemo, useState } from 'react'
import { ChevronDown, Plus, Trash2, TriangleAlert } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { RefSpec, CommonFileMeta } from '@/lib/commonMeta'
import {
  COMMON_ARRAY_ITEM_REFS,
  commonFieldLabel,
  commonFieldNote,
  commonFieldRef,
} from '@/lib/commonMeta'
import type { JsonPath } from '@/lib/jsoncEdit'
import { isPlainObject } from '@/lib/jsoncEdit'
import type { RefResolver } from '@/lib/refResolver'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'

/** 下拉候选项上限，避免超大表（如 833 名武将）拖慢下拉渲染 */
const MAX_REF_OPTIONS = 500

/** 编辑上下文 */
export interface CommonEditContextValue {
  fileName: string
  fileMeta?: CommonFileMeta
  resolver: RefResolver
  setValue: (path: JsonPath, value: unknown) => void
  removeValue: (path: JsonPath) => void
  pushValue: (arrayPath: JsonPath, value: unknown) => void
}

export const CommonEditContext = createContext<CommonEditContextValue | null>(null)

/** 取编辑上下文 */
export function useCommonEdit(): CommonEditContextValue {
  const ctx = useContext(CommonEditContext)
  if (!ctx) throw new Error('JsonValueEditor 必须在 CommonEditContext 内使用')
  return ctx
}

/** 取某字段的数组元素引用 */
function itemRefFor(fileName: string, key: string): RefSpec | null {
  return COMMON_ARRAY_ITEM_REFS[fileName]?.[key] ?? null
}

/** 两个路径是否相同 */
function samePath(a: JsonPath, b: JsonPath): boolean {
  return a.length === b.length && a.every((x, i) => String(x) === String(b[i]))
}

/**
 * 深拷贝新增模板，避免多条记录共享同一个对象引用。
 *
 * @param value 模板
 * @returns 副本
 */
function structuredCloneSafe(value: unknown): unknown {
  try {
    return JSON.parse(JSON.stringify(value))
  } catch {
    return {}
  }
}

/* ------------------------------------------------------------------ */
/* 基础控件                                                            */
/* ------------------------------------------------------------------ */

/** 可折叠分组容器 */
function GroupShell({
  label,
  hint,
  count,
  defaultOpen,
  onRemove,
  children,
}: {
  label: string
  hint?: string
  count?: string
  defaultOpen: boolean
  onRemove?: () => void
  children: React.ReactNode
}) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <Collapsible open={open} onOpenChange={setOpen} className="rounded-lg border border-border bg-card/40">
      <div className="flex items-center">
        <CollapsibleTrigger asChild>
          <button type="button" className="flex min-w-0 flex-1 items-center gap-2 px-2.5 py-1.5 text-left">
            <ChevronDown className={cn('h-3.5 w-3.5 shrink-0 transition-transform', !open && '-rotate-90')} />
            <span className="truncate text-[12px] font-medium">{label}</span>
            {count && <span className="shrink-0 text-[10px] tabular text-muted-foreground">{count}</span>}
            {hint && <span className="truncate text-[10px] text-muted-foreground">{hint}</span>}
          </button>
        </CollapsibleTrigger>
        {onRemove && (
          <Button
            variant="ghost"
            size="icon"
            className="mr-1 h-6 w-6 shrink-0 text-muted-foreground hover:text-destructive"
            title="删除该节点"
            onClick={onRemove}
          >
            <Trash2 className="h-3 w-3" />
          </Button>
        )}
      </div>
      <CollapsibleContent>
        <div className="border-t border-border px-2.5 py-2">{children}</div>
      </CollapsibleContent>
    </Collapsible>
  )
}

/** 引用下拉选择 */
function RefSelect({
  spec,
  value,
  onChange,
  className,
}: {
  spec: RefSpec
  value: number
  onChange: (v: number) => void
  /**
   * 追加到触发器上的类名。
   *
   * 宽度必须由调用方决定：在表单里希望它至少有 150px，但在固定列宽（如 130px）
   * 的表格单元格里，这个下限会把下拉撑出格子压到右列。默认值会被 twMerge 覆盖，
   * 因此调用方传 `w-full min-w-0` 即可完全接管宽度。
   */
  className?: string
}) {
  const { resolver } = useCommonEdit()
  const map = resolver.getMap(spec)

  const entries = useMemo(() => {
    const list = [...map.entries()].sort((a, b) => a[0] - b[0]).slice(0, MAX_REF_OPTIONS)
    // 当前值不在候选表中（例如引用了未实例化的武将）时补一项，保证下拉能正常显示
    return map.has(value) ? list : ([[value, `（表中无此 ID：${value}）`] as [number, string], ...list])
  }, [map, value])

  // 触发器的文字会被截断，完整内容放到 title 里，悬停可核对
  const currentName = map.get(value) ?? `（表中无此 ID：${value}）`

  return (
    <Select value={String(value)} onValueChange={(v) => onChange(Number(v))}>
      <SelectTrigger
        className={cn('h-8 min-w-[150px] text-[12px]', className)}
        title={`${value} ${currentName}`}
      >
        <SelectValue />
      </SelectTrigger>
      <SelectContent className="max-h-[300px]">
        {entries.map(([id, name]) => (
          <SelectItem key={id} value={String(id)}>
            <span className="tabular text-muted-foreground">{id}</span>
            <span className="ml-2">{name}</span>
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

/** 标量输入（数字 / 文本 / 布尔 / 引用） */
function ScalarInput({
  value,
  path,
  label,
  refSpec,
}: {
  value: unknown
  path: JsonPath
  label: string
  refSpec: RefSpec | null
}) {
  const { setValue, resolver } = useCommonEdit()

  if (refSpec && typeof value === 'number') {
    const name = resolver.getName(refSpec, value)
    return (
      <div className="flex items-center gap-2">
        <span className="min-w-[92px] shrink-0 truncate text-[11px] text-muted-foreground" title={label}>
          {label}
        </span>
        <RefSelect spec={refSpec} value={value} onChange={(v) => setValue(path, v)} />
        {name && <span className="min-w-0 flex-1 truncate text-[11px]">{name}</span>}
      </div>
    )
  }

  if (typeof value === 'boolean') {
    return (
      <label className="flex cursor-pointer items-center gap-2">
        <span className="min-w-[92px] shrink-0 truncate text-[11px] text-muted-foreground" title={label}>
          {label}
        </span>
        <input
          type="checkbox"
          className="h-4 w-4 shrink-0 accent-primary"
          checked={value}
          onChange={(e) => setValue(path, e.target.checked)}
        />
      </label>
    )
  }

  if (typeof value === 'number') {
    return (
      <label className="flex items-center gap-2">
        <span className="min-w-[92px] shrink-0 truncate text-[11px] text-muted-foreground" title={label}>
          {label}
        </span>
        <input
          type="number"
          step={Number.isInteger(value) ? 1 : 'any'}
          className="tabular h-7 min-w-0 flex-1 rounded border border-border bg-background px-1.5 text-[12px] outline-none focus:border-primary"
          defaultValue={value}
          onBlur={(e) => {
            const next = Number(e.target.value)
            if (Number.isFinite(next) && next !== value) setValue(path, next)
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter') (e.target as HTMLInputElement).blur()
          }}
        />
      </label>
    )
  }

  const text = value === null ? '' : String(value)
  const isLong = text.length > 60 || text.includes('\n')
  return (
    <label className="flex items-start gap-2">
      <span
        className="min-w-[92px] shrink-0 truncate pt-1 text-[11px] text-muted-foreground"
        title={label}
      >
        {label}
      </span>
      {isLong ? (
        <textarea
          className="min-h-[64px] min-w-0 flex-1 rounded border border-border bg-background px-2 py-1 text-[12px] outline-none focus:border-primary"
          defaultValue={text}
          rows={3}
          onBlur={(e) => {
            if (e.target.value !== text) setValue(path, e.target.value)
          }}
        />
      ) : (
        <input
          type="text"
          className="h-7 min-w-0 flex-1 rounded border border-border bg-background px-1.5 text-[12px] outline-none focus:border-primary"
          defaultValue={text}
          placeholder={value === null ? '（null）' : undefined}
          onBlur={(e) => {
            if (e.target.value !== text) setValue(path, e.target.value)
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter') (e.target as HTMLInputElement).blur()
          }}
        />
      )}
    </label>
  )
}

/** 值数组编辑器（元素为标量或引用） */
function ValuesArray({
  values,
  path,
  itemRef,
  newItem,
}: {
  values: unknown[]
  path: JsonPath
  itemRef: RefSpec | null
  newItem?: unknown
}) {
  const { setValue, removeValue, pushValue, resolver } = useCommonEdit()

  const indexed = Boolean(itemRef?.indexed)

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {values.map((v, i) => {
        // 逐项索引型数组显示「下标名称」，引用型数组显示「值对应的名称」
        const name =
          itemRef && typeof v === 'number' ? resolver.getName(itemRef, indexed ? i : v) : undefined
        return (
          <span
            key={i}
            className="inline-flex items-center gap-1 rounded border border-border bg-background px-1.5 py-0.5"
            title={name ? `${name}${indexed ? `（第 ${i} 项）` : ''}` : undefined}
          >
            <input
              type={typeof v === 'number' ? 'number' : 'text'}
              className="tabular w-[72px] bg-transparent text-[12px] outline-none"
              defaultValue={String(v ?? '')}
              onBlur={(e) => {
                const next = typeof v === 'number' ? Number(e.target.value) : e.target.value
                if (String(next) !== String(v)) setValue([...path, i], next)
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter') (e.target as HTMLInputElement).blur()
              }}
            />
            {name && <span className="max-w-[140px] truncate text-[10px] text-muted-foreground">{name}</span>}
            <button
              type="button"
              className="text-muted-foreground transition-colors hover:text-destructive"
              title="删除该元素"
              onClick={() => removeValue([...path, i])}
            >
              <Trash2 className="h-3 w-3" />
            </button>
          </span>
        )
      })}
      <Button
        variant="outline"
        size="sm"
        className="h-6 gap-1 px-1.5 text-[11px]"
        onClick={() => pushValue(path, typeof newItem === 'number' ? newItem : 0)}
      >
        <Plus className="h-3 w-3" />
        添加
      </Button>
    </div>
  )
}

/**
 * 字段级提示：用于标注「C# 不读取该键」这类容易踩坑的字段。
 *
 * @param props.fileName 文件名
 * @param props.fieldKey 字段键
 */
function FieldNote({ fileName, fieldKey }: { fileName: string; fieldKey: string }) {
  const note = commonFieldNote(fileName, fieldKey)
  if (!note) return null
  return (
    <p className="mt-0.5 pl-[100px] text-[10px] leading-snug text-amber-500/90">
      <TriangleAlert className="mr-1 inline h-2.5 w-2.5 align-[-1px]" />
      {note}
    </p>
  )
}

/** 为对象新增一个键 */
function AddKeyRow({ path, existing }: { path: JsonPath; existing: string[] }) {
  const { setValue } = useCommonEdit()
  const [key, setKey] = useState('')
  const valid = key.trim().length > 0 && !existing.includes(key.trim())
  const commit = () => {
    if (!valid) return
    setValue([...path, key.trim()], '')
    setKey('')
  }
  return (
    <div className="mt-2 flex items-center gap-1.5">
      <input
        className="h-6 min-w-[120px] flex-1 rounded border border-dashed border-border bg-transparent px-1.5 text-[11px] outline-none focus:border-primary"
        placeholder="新增字段名（英文键）"
        value={key}
        onChange={(e) => setKey(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') commit()
        }}
      />
      <Button variant="outline" size="sm" className="h-6 gap-1 px-1.5 text-[11px]" disabled={!valid} onClick={commit}>
        <Plus className="h-3 w-3" />
        添加字段
      </Button>
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* 表格单元格内联编辑器                                                */
/* ------------------------------------------------------------------ */

/**
 * 表格单元格用的紧凑编辑器（无标签）。
 *
 * 数组与对象不在此处展开，交由详情面板处理，仅显示摘要文本。
 *
 * @param props.value 当前值
 * @param props.path 当前路径
 * @param props.refSpec 引用目标
 * @param props.onOpenDetail 复杂值的「打开详情」回调
 */
export function JsonCellEditor({
  value,
  path,
  refSpec = null,
  onOpenDetail,
}: {
  value: unknown
  path: JsonPath
  refSpec?: RefSpec | null
  onOpenDetail?: () => void
}) {
  const { setValue, resolver } = useCommonEdit()

  if (Array.isArray(value) || isPlainObject(value)) {
    const count = Array.isArray(value) ? value.length : Object.keys(value).length
    const preview = summarizeValue(value, refSpec ?? null, resolver)
    return (
      <button
        type="button"
        className="h-7 w-full truncate rounded px-1.5 text-left text-[12px] text-muted-foreground transition-colors hover:bg-accent/70"
        title={preview}
        onClick={onOpenDetail}
      >
        {preview}
        <span className="ml-1 text-[10px] opacity-70">({count})</span>
      </button>
    )
  }

  if (refSpec && typeof value === 'number') {
    // 单元格宽度固定且很窄，这里必须让下拉撑满格子并允许收缩，
    // 否则内部的长文本（如「（表中无此 ID：0）」）会把触发器顶宽、压到右列去
    return (
      <div className="flex w-full min-w-0 items-center gap-1">
        <RefSelect
          spec={refSpec}
          value={value}
          onChange={(v) => setValue(path, v)}
          className="h-7 w-full min-w-0 px-1.5"
        />
      </div>
    )
  }

  if (typeof value === 'boolean') {
    return (
      <div className="flex h-7 items-center px-1.5">
        <input
          type="checkbox"
          className="h-4 w-4 accent-primary"
          checked={value}
          onChange={(e) => setValue(path, e.target.checked)}
        />
      </div>
    )
  }

  if (typeof value === 'number') {
    return (
      <input
        type="number"
        step={Number.isInteger(value) ? 1 : 'any'}
        className="tabular h-7 w-full rounded border border-transparent bg-transparent px-1.5 text-[12px] outline-none hover:border-border focus:border-primary"
        defaultValue={value}
        onBlur={(e) => {
          const next = Number(e.target.value)
          if (Number.isFinite(next) && next !== value) setValue(path, next)
        }}
        onKeyDown={(e) => {
          if (e.key === 'Enter') (e.target as HTMLInputElement).blur()
        }}
      />
    )
  }

  const text = value === null ? '' : String(value)
  return (
    <input
      type="text"
      className="h-7 w-full truncate rounded border border-transparent bg-transparent px-1.5 text-[12px] outline-none hover:border-border focus:border-primary"
      defaultValue={text}
      placeholder={value === null ? '（null）' : undefined}
      onBlur={(e) => {
        if (e.target.value !== text) setValue(path, e.target.value)
      }}
      onKeyDown={(e) => {
        if (e.key === 'Enter') (e.target as HTMLInputElement).blur()
      }}
    />
  )
}

/**
 * 把数组 / 对象值压缩成一句可读摘要。
 *
 * @param value 值
 * @param itemRef 数组元素的引用目标
 * @param resolver 引用解析器
 * @returns 摘要文本
 */
export function summarizeValue(value: unknown, itemRef: RefSpec | null, resolver: RefResolver): string {
  if (Array.isArray(value)) {
    if (value.length === 0) return '（空）'
    const first = value[0]
    if (isPlainObject(first) || Array.isArray(first)) return `数组（${value.length} 项）`
    // 逐项索引型数组（如兵种的 moveCost 按地形 Id 排列）：名称来自「下标」而不是「值」
    const indexed = Boolean(itemRef?.indexed)
    const parts = value.slice(0, 3).map((v, i) => {
      const name = itemRef && typeof v === 'number' ? resolver.getName(itemRef, indexed ? i : v) : undefined
      if (!name) return String(v)
      return indexed ? `${name}=${v}` : `${name}(${v})`
    })
    return parts.join('、') + (value.length > 3 ? ` 等 ${value.length} 项` : '')
  }
  if (isPlainObject(value)) {
    const keys = Object.keys(value)
    return keys.slice(0, 3).map((k) => `${k}=${String(value[k])}`).join(' ') + (keys.length > 3 ? ' …' : '')
  }
  return String(value ?? '')
}

/* ------------------------------------------------------------------ */
/* 递归编辑器                                                          */
/* ------------------------------------------------------------------ */

/** 递归值编辑器属性 */
export interface JsonValueEditorProps {
  /** 字段标签 */
  label: string
  /** 当前值 */
  value: unknown
  /** 当前路径 */
  path: JsonPath
  /** 深度（用于控制默认折叠） */
  depth?: number
  /** 该字段的引用目标 */
  refSpec?: RefSpec | null
  /** 该字段（数组）元素的引用目标 */
  itemRef?: RefSpec | null
  /** 是否允许删除 */
  removable?: boolean
  /** 新增数组元素时使用的模板（缺省为 {} 或 0） */
  newItem?: unknown
}

/**
 * 递归渲染一个 JSON 节点。
 */
export function JsonValueEditor({
  label,
  value,
  path,
  depth = 0,
  refSpec = null,
  itemRef = null,
  removable = true,
  newItem,
}: JsonValueEditorProps) {
  const ctx = useCommonEdit()
  const { removeValue, pushValue } = ctx

  if (isPlainObject(value)) {
    const keys = Object.keys(value)
    return (
      <GroupShell
        label={label}
        count={`${keys.length} 项`}
        defaultOpen={depth < 2}
        onRemove={removable && !samePath(path, []) ? () => removeValue(path) : undefined}
      >
        <div className="space-y-1.5">
          {keys.map((k) => (
            <div key={k}>
              <JsonValueEditor
                label={commonFieldLabel(k, ctx.fileMeta)}
                value={value[k]}
                path={[...path, k]}
                depth={depth + 1}
                refSpec={commonFieldRef(ctx.fileName, k)}
                itemRef={itemRefFor(ctx.fileName, k)}
              />
              <FieldNote fileName={ctx.fileName} fieldKey={k} />
            </div>
          ))}
        </div>
        <AddKeyRow path={path} existing={keys} />
      </GroupShell>
    )
  }

  if (Array.isArray(value)) {
    // 空数组时用模板判断应呈现为「记录列表」还是「值列表」
    const isRecordArray = value.some(isPlainObject) || (value.length === 0 && isPlainObject(newItem))
    if (isRecordArray) {
      return (
        <GroupShell
          label={label}
          count={`${value.length} 条`}
          defaultOpen={depth < 2}
          onRemove={removable ? () => removeValue(path) : undefined}
        >
          <div className="space-y-2">
            {value.map((item, i) => (
              <JsonValueEditor
                key={i}
                label={`#${i}${isPlainObject(item) && typeof item.name === 'string' ? ` · ${item.name}` : ''}`}
                value={item}
                path={[...path, i]}
                depth={depth + 1}
              />
            ))}
            <Button
              variant="outline"
              size="sm"
              className="h-7 w-full gap-1 text-[11px]"
              onClick={() => pushValue(path, isPlainObject(newItem) ? structuredCloneSafe(newItem) : {})}
            >
              <Plus className="h-3 w-3" />
              添加条目
            </Button>
          </div>
        </GroupShell>
      )
    }
    return (
      <div className="rounded-md border border-border bg-card/30 px-2 py-1.5">
        <div className="mb-1 flex items-center gap-2">
          <span className="min-w-[92px] truncate text-[11px] text-muted-foreground" title={label}>
            {label}
          </span>
          <span className="text-[10px] tabular text-muted-foreground">{value.length} 项</span>
          {removable && (
            <button
              type="button"
              className="ml-auto text-muted-foreground hover:text-destructive"
              title="删除该数组"
              onClick={() => removeValue(path)}
            >
              <Trash2 className="h-3 w-3" />
            </button>
          )}
        </div>
        <ValuesArray values={value} path={path} itemRef={itemRef} newItem={newItem} />
      </div>
    )
  }

  return (
    <div className="flex items-center gap-1.5">
      <div className="min-w-0 flex-1">
        <ScalarInput value={value} path={path} label={label} refSpec={refSpec} />
      </div>
      {removable && !samePath(path, []) && (
        <TooltipProvider delayDuration={400}>
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                type="button"
                className="shrink-0 text-muted-foreground transition-colors hover:text-destructive"
                onClick={() => removeValue(path)}
              >
                <Trash2 className="h-3 w-3" />
              </button>
            </TooltipTrigger>
            <TooltipContent>删除该字段</TooltipContent>
          </Tooltip>
        </TooltipProvider>
      )}
    </div>
  )
}
