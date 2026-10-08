/**
 * 通用字段编辑器
 *
 * 由 schema 驱动的「万能控件」：根据字段的 kind 自动渲染数字框、下拉框、
 * 可搜索引用选择器、数组输入等。编辑表单、批量编辑对话框、统一编辑面板
 * 全部复用该组件，保证交互与校验提示完全一致。
 */
import React, { useMemo, useState } from 'react'
import { Check, ChevronsUpDown, Minus } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { CollectionKey, Entity, OptionItem, Options, ValidationIssue } from '@/lib/types'
import { COLLECTION_META } from '@/lib/types'
import type { FieldDef } from '@/lib/schema'
import { resolveEnum } from '@/lib/schema'
import { getAttrChangeId, getArray } from '@/lib/fields'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command'
import { MultiRefPicker, PairArrayEditor, arrayEditorKind } from './MultiRefPicker'
import { CityMapValueButton } from './CityMapPicker'

/** 字段输入的取值类型 */
export type FieldInputValue = number | string | null

export interface FieldInputProps {
  /** 字段定义 */
  field: FieldDef
  /** 当前值（标量形式；null 表示「保持不变」，仅批量模式使用） */
  value: FieldInputValue
  /** 取值变化回调 */
  onChange: (value: FieldInputValue) => void
  /** 公共数据表 */
  options: Options | null
  /** 用于读取原数组长度等上下文的原实体 */
  entity?: Entity | null
  /** 该字段相关的校验问题 */
  issues?: ValidationIssue[]
  /** attr 字段专用：成长类型变化回调 */
  onChangeId?: (changeId: number) => void
  /** 批量编辑模式：允许选择「保持不变」 */
  allowUnchanged?: boolean
  /** 是否禁用 */
  disabled?: boolean
  /** 占位文本 */
  placeholder?: string
  /** 引用候选自动带上所属集合 */
  referenceNames?: Record<CollectionKey, Map<number, string>>
}

/** 校验问题级别对应的小圆点颜色 */
function issueDotClass(level: ValidationIssue['level']): string {
  if (level === 'error') return 'bg-destructive'
  if (level === 'warning') return 'bg-amber-500'
  return 'bg-sky-500'
}

/**
 * 数字输入框（带步进按钮）。
 */
function NumberInput({
  value,
  onChange,
  min,
  max,
  step,
  disabled,
  placeholder,
  integer = true,
}: {
  value: number | null
  onChange: (v: number | null) => void
  min?: number
  max?: number
  step?: number
  disabled?: boolean
  placeholder?: string
  integer?: boolean
}) {
  const stepValue = step ?? 1

  const clamp = (n: number) => {
    let v = n
    if (min !== undefined && v < min) v = min
    if (max !== undefined && v > max) v = max
    return integer ? Math.trunc(v) : v
  }

  return (
    <div className="flex items-center gap-1">
      <Button
        type="button"
        variant="outline"
        size="icon"
        className="h-8 w-8 shrink-0"
        disabled={disabled || value === null}
        onClick={() => onChange(clamp((value ?? 0) - stepValue))}
        aria-label="减小"
      >
        <Minus className="h-3.5 w-3.5" />
      </Button>
      <Input
        className="h-8 text-center tabular"
        inputMode={integer ? 'numeric' : 'decimal'}
        value={value === null ? '' : String(value)}
        placeholder={placeholder ?? '—'}
        disabled={disabled}
        onChange={(e) => {
          const raw = e.target.value.trim()
          if (raw === '' || raw === '-') {
            onChange(null)
            return
          }
          const n = Number(raw)
          if (!Number.isFinite(n)) return
          onChange(integer ? Math.trunc(n) : n)
        }}
        onBlur={(e) => {
          const raw = e.target.value.trim()
          if (raw === '' || raw === '-') return
          const n = Number(raw)
          if (Number.isFinite(n)) onChange(clamp(n))
        }}
      />
      <Button
        type="button"
        variant="outline"
        size="icon"
        className="h-8 w-8 shrink-0"
        disabled={disabled || value === null}
        onClick={() => onChange(clamp((value ?? 0) + stepValue))}
        aria-label="增大"
      >
        <span className="text-base leading-none">+</span>
      </Button>
    </div>
  )
}

/**
 * 枚举下拉框。
 */
function EnumSelect({
  value,
  onChange,
  entries,
  disabled,
  placeholder,
  allowUnchanged,
  noValueLabel = '保持不变',
}: {
  value: number | null
  onChange: (v: number | null) => void
  entries: { value: number; label: string }[]
  disabled?: boolean
  placeholder?: string
  allowUnchanged?: boolean
  noValueLabel?: string
}) {
  const NONE = '__none__'
  return (
    <Select
      value={value === null ? NONE : String(value)}
      onValueChange={(v) => onChange(v === NONE ? null : Number(v))}
      disabled={disabled}
    >
      <SelectTrigger className="h-8">
        <SelectValue placeholder={placeholder ?? '请选择'} />
      </SelectTrigger>
      <SelectContent className="max-h-[320px]">
        {allowUnchanged && (
          <SelectItem value={NONE}>
            <span className="text-muted-foreground">{noValueLabel}</span>
          </SelectItem>
        )}
        {entries.map((e) => (
          <SelectItem key={e.value} value={String(e.value)}>
            <span className="tabular">{e.value}</span>
            <span className="ml-2 text-muted-foreground">{e.label}</span>
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

/**
 * 可搜索的引用选择器（用于大列表：武将、都市、特技、官职等）。
 */
function RefCombobox({
  value,
  onChange,
  items,
  disabled,
  placeholder,
  allowUnchanged,
  emptyText,
  iconOnly,
}: {
  value: number | null
  onChange: (v: number | null) => void
  items: { id: number; name: string }[]
  disabled?: boolean
  placeholder?: string
  allowUnchanged?: boolean
  emptyText?: string
  /** 只显示图标（作为次要入口，与地图主控件并排） */
  iconOnly?: boolean
}) {
  const [open, setOpen] = useState(false)
  const current = value === null ? undefined : items.find((x) => x.id === value)

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          disabled={disabled}
          className={cn(
            'h-8 justify-between px-2 font-normal',
            iconOnly ? 'w-8 shrink-0' : 'w-full'
          )}
          title={iconOnly ? '按名称搜索' : undefined}
        >
          {iconOnly ? (
            <ChevronsUpDown className="h-3.5 w-3.5 opacity-70" />
          ) : (
            <>
              <span className="truncate">
                {value === null ? (
                  <span className="text-muted-foreground">{allowUnchanged ? '保持不变' : placeholder ?? '未设置'}</span>
                ) : (
                  <>
                    <span className="tabular text-muted-foreground">{value}</span>
                    <span className="ml-2">{current?.name ?? '（不存在）'}</span>
                  </>
                )}
              </span>
              <ChevronsUpDown className="ml-1 h-3.5 w-3.5 shrink-0 opacity-60" />
            </>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[min(92vw,340px)] p-0" align="start">
        <Command
          filter={(itemValue, search) => {
            const item = items.find((x) => String(x.id) === itemValue)
            if (!item) return 0
            const hay = `${item.id} ${item.name}`.toLowerCase()
            return hay.includes(search.toLowerCase()) ? 1 : 0
          }}
        >
          <CommandInput placeholder="搜索 ID 或名称…" />
          <CommandList className="max-h-[280px]">
            <CommandEmpty>{emptyText ?? '无匹配项'}</CommandEmpty>
            <CommandGroup>
              {allowUnchanged && (
                <CommandItem
                  value="__unchanged__"
                  onSelect={() => {
                    onChange(null)
                    setOpen(false)
                  }}
                >
                  <span className="text-muted-foreground">保持不变</span>
                </CommandItem>
              )}
              {items.slice(0, 400).map((item) => (
                <CommandItem
                  key={item.id}
                  value={String(item.id)}
                  onSelect={() => {
                    onChange(item.id)
                    setOpen(false)
                  }}
                >
                  <Check className={cn('mr-2 h-3.5 w-3.5', value === item.id ? 'opacity-100' : 'opacity-0')} />
                  <span className="tabular w-12 text-muted-foreground">{item.id}</span>
                  <span className="truncate">{item.name}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}

/**
 * 数组字段输入（逗号分隔，实时显示解析结果）。
 */
function ArrayInput({
  value,
  onChange,
  disabled,
  placeholder,
  itemLabel,
  hint,
}: {
  value: string
  onChange: (v: string) => void
  disabled?: boolean
  placeholder?: string
  itemLabel?: string
  hint?: string
}) {
  const parsed = useMemo(
    () =>
      value
        .split(/[,，\s;；、]+/)
        .map((s) => s.trim())
        .filter((s) => s !== ''),
    [value]
  )
  const invalid = parsed.filter((s) => !/^-?\d+$/.test(s))
  return (
    <div className="space-y-1">
      <Input
        className="h-8 font-mono text-[12px]"
        value={value}
        placeholder={placeholder ?? '以逗号分隔，例如 1,2,3'}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
      />
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px] text-muted-foreground">
        <span>
          共 <span className="tabular text-foreground">{parsed.length}</span> 项
        </span>
        {hint && <span className="truncate">{hint}</span>}
        {parsed.length > 0 && (
          <span className="truncate font-mono">
            {itemLabel ? `${itemLabel}: ` : ''}
            {parsed.slice(0, 12).join(',')}
            {parsed.length > 12 ? ' …' : ''}
          </span>
        )}
        {invalid.length > 0 && <span className="text-destructive">存在非数字项：{invalid.join(',')}</span>}
      </div>
    </div>
  )
}

/**
 * 字段编辑器主体。
 */
export function FieldInput(props: FieldInputProps) {
  const {
    field,
    value,
    onChange,
    onChangeId,
    options,
    entity,
    allowUnchanged,
    disabled,
    placeholder,
    referenceNames,
  } = props

  /** 取候选列表 */
  const candidates = useMemo(() => {
    if (field.ref && referenceNames) {
      const map = referenceNames[field.ref]
      return [...map.entries()]
        .map(([id, name]) => ({ id, name }))
        .sort((a, b) => a.id - b.id)
    }
    if (field.optionsKey && options) {
      const list = (options[field.optionsKey] as OptionItem[] | undefined) ?? []
      return list.map((x) => ({ id: x.id, name: x.name }))
    }
    if (field.itemOptionsKey && options) {
      const list = (options[field.itemOptionsKey] as OptionItem[] | undefined) ?? []
      return list.map((x) => ({ id: x.id, name: x.name }))
    }
    return []
  }, [field, options, referenceNames])

  const enumEntries = useMemo(() => resolveEnum(field, options), [field, options])

  /* ---------------- 按 kind 渲染 ---------------- */

  if (field.kind === 'bool') {
    return (
      <Switch
        checked={value === 1 || value === '1'}
        disabled={disabled}
        onCheckedChange={(v) => onChange(v ? 1 : 0)}
      />
    )
  }

  if (field.kind === 'string') {
    const text = value === null ? '' : String(value)
    const isLong = field.key === 'description' || field.key === 'desc'
    if (isLong) {
      return (
        <textarea
          className="min-h-[88px] w-full resize-y rounded-md border border-input bg-transparent px-2.5 py-1.5 text-[13px] leading-relaxed outline-none transition-colors focus:border-primary"
          value={text}
          disabled={disabled}
          placeholder={placeholder}
          onChange={(e) => onChange(e.target.value)}
        />
      )
    }
    return (
      <Input
        className="h-8"
        value={text}
        disabled={disabled}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
      />
    )
  }

  if (field.kind === 'attr') {
    const base = value === null ? null : Number(value)
    const changeId = entity ? getAttrChangeId(entity[field.key]) : 5
    return (
      <div className="flex items-center gap-2">
        <NumberInput
          value={base}
          onChange={(v) => onChange(v)}
          min={field.min}
          max={field.max}
          disabled={disabled}
          placeholder={placeholder}
        />
        <Select
          value={String(changeId)}
          disabled={disabled || !onChangeId}
          onValueChange={(v) => onChangeId?.(Number(v))}
        >
          <SelectTrigger className="h-8 w-[130px] shrink-0">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(options?.attributeChangeTypes ?? []).map((t) => (
              <SelectItem key={t.id} value={String(t.id)}>
                {t.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    )
  }

  if (field.kind === 'ability') {
    const level = value === null ? null : Number(value)
    const name = level === null ? '' : candidates.find((c) => c.id === level)?.name ?? ''
    return (
      <div className="flex items-center gap-2">
        <NumberInput
          value={level}
          onChange={(v) => onChange(v)}
          min={field.min}
          max={field.max}
          disabled={disabled}
          placeholder={placeholder}
        />
        {name && <Badge variant="secondary" className="shrink-0">{name}</Badge>}
      </div>
    )
  }

  if (field.kind === 'intArray' || field.kind === 'polyRef' || field.kind === 'pairArray') {
    const raw = entity ? entity[field.key] : undefined
    const arr = getArray(raw)
      .map((n) => Number(n))
      .filter((n) => Number.isFinite(n))

    // 引用型数组：改用勾选 / 选择式编辑器，避免手敲一串 ID
    const editorKind = arrayEditorKind(field)
    if (editorKind === 'multiRef' && entity) {
      return (
        <MultiRefPicker
          field={field}
          values={arr}
          onChange={(next) => onChange(next.join(','))}
          options={options}
          referenceNames={referenceNames}
          disabled={disabled}
        />
      )
    }
    if (editorKind === 'pair' && entity) {
      return (
        <PairArrayEditor
          field={field}
          values={arr}
          onChange={(next) => onChange(next.join(','))}
          options={options}
          disabled={disabled}
        />
      )
    }

    const text = value === null ? '' : typeof value === 'string' ? value : String(value)
    const hint =
      field.kind === 'pairArray'
        ? '成对输入：类型,数量,类型,数量…'
        : field.itemRef
          ? `引用的${COLLECTION_META[field.itemRef as CollectionKey].label}：${
              arr
                .slice(0, 8)
                .map((id) => `${id}(${referenceNames?.[field.itemRef as CollectionKey]?.get(id) ?? '?'})`)
                .join('、') || '无'
            }`
          : undefined
    return (
      <ArrayInput
        value={text}
        onChange={(v) => onChange(v)}
        disabled={disabled}
        placeholder={placeholder}
        itemLabel={field.itemLabel}
        hint={hint}
      />
    )
  }

  /* 枚举：静态枚举表较小，直接下拉 */
  if (field.kind === 'enum' && enumEntries.length > 0 && enumEntries.length <= 20) {
    return (
      <EnumSelect
        value={value === null ? null : Number(value)}
        onChange={onChange}
        entries={enumEntries}
        disabled={disabled}
        allowUnchanged={allowUnchanged}
      />
    )
  }

  /* 引用 / 大枚举：可搜索选择器 */
  if ((field.kind === 'ref' || field.kind === 'enum') && candidates.length > 0) {
    const list = field.zeroMeansNone ? [{ id: 0, name: '无' }, ...candidates] : candidates

    // 都市引用：主控件直接弹地图，名称搜索降级为次按钮
    if (field.ref === 'citySet') {
      const num = value === null ? null : Number(value)
      return (
        <div className="flex items-center gap-1">
          <CityMapValueButton
            value={num}
            name={num === null ? '' : list.find((c) => c.id === num)?.name ?? ''}
            onChange={(v) => onChange(v)}
            disabled={disabled}
            placeholder={placeholder}
            title={`在地图上选择${field.label}`}
          />
          <RefCombobox
            iconOnly
            value={num}
            onChange={onChange}
            items={list}
            disabled={disabled}
            allowUnchanged={allowUnchanged}
            emptyText={`未找到匹配的${field.label}`}
          />
        </div>
      )
    }

    if (list.length <= 20) {
      return (
        <EnumSelect
          value={value === null ? null : Number(value)}
          onChange={onChange}
          entries={list.map((c) => ({ value: c.id, label: c.name }))}
          disabled={disabled}
          allowUnchanged={allowUnchanged}
        />
      )
    }
    return (
      <RefCombobox
        value={value === null ? null : Number(value)}
        onChange={onChange}
        items={list}
        disabled={disabled}
        placeholder={placeholder}
        allowUnchanged={allowUnchanged}
        emptyText={`未找到匹配的${field.label}`}
      />
    )
  }

  /* 兜底：数字输入 */
  return (
    <NumberInput
      value={value === null ? null : Number(value)}
      onChange={onChange}
      min={field.min}
      max={field.max}
      step={field.step}
      disabled={disabled}
      placeholder={placeholder}
      integer={field.kind !== 'float'}
    />
  )
}

/**
 * 带标签、说明与校验提示的字段行。
 */
export function FieldRow({
  field,
  children,
  issues = [],
  hint,
}: {
  field: FieldDef
  children: React.ReactNode
  issues?: ValidationIssue[]
  hint?: string
}) {
  const errorIssue = issues.find((i) => i.level === 'error')
  const warnIssue = issues.find((i) => i.level === 'warning')
  return (
    <div className="space-y-1">
      <div className="flex items-center gap-1.5">
        <Label className="text-[12px] font-medium text-foreground">{field.label}</Label>
        <span className="font-mono text-[10px] text-muted-foreground">{field.key}</span>
        {field.legacy && (
          <Badge variant="outline" className="h-4 px-1 text-[10px] text-muted-foreground">
            遗留
          </Badge>
        )}
        {(errorIssue || warnIssue) && (
          <span className={cn('h-1.5 w-1.5 rounded-full', issueDotClass((errorIssue ?? warnIssue)!.level))} />
        )}
      </div>
      {children}
      {(errorIssue || warnIssue) && (
        <p className={cn('text-[11px] leading-snug', errorIssue ? 'text-destructive' : 'text-amber-500')}>
          {(errorIssue ?? warnIssue)!.message}
        </p>
      )}
      {hint && !errorIssue && !warnIssue && <p className="text-[11px] text-muted-foreground">{hint}</p>}
    </div>
  )
}

/** 供外部复用的小工具 */
export { NumberInput, EnumSelect, RefCombobox, ArrayInput }
