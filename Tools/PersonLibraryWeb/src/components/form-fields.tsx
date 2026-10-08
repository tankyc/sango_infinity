/**
 * 文件名：form-fields.tsx
 * 描述：武将编辑表单使用的通用字段组件集合。
 *       不同用途的字段使用不同的交互控件：文本框、多行文本、数字、
 *       滑块、下拉、按钮组（枚举）、开关、标签（多值文本）等。
 */

import { useState, type ReactNode } from 'react'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Slider } from '@/components/ui/slider'
import { Switch } from '@/components/ui/switch'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { cn } from '@/lib/utils'
import type { Choice } from '@/lib/enums'
import { X } from 'lucide-react'

/** 字段外壳：标题 + 提示 + 内容 */
export function FieldShell({
  label,
  hint,
  children,
  className,
}: {
  label: string
  hint?: string
  children: ReactNode
  className?: string
}) {
  return (
    <div className={cn('flex flex-col gap-2', className)}>
      <div className="flex items-baseline gap-2">
        <label className="text-sm font-medium text-foreground/90">{label}</label>
        {hint && <span className="text-xs text-muted-foreground">{hint}</span>}
      </div>
      {children}
    </div>
  )
}

/** 单行文本框 */
export function TextField({
  label,
  hint,
  value,
  placeholder,
  onChange,
  maxLength,
}: {
  label: string
  hint?: string
  value: string
  placeholder?: string
  onChange: (v: string) => void
  maxLength?: number
}) {
  return (
    <FieldShell label={label} hint={hint}>
      <Input
        value={value}
        placeholder={placeholder}
        maxLength={maxLength}
        onChange={(e) => onChange(e.target.value)}
      />
    </FieldShell>
  )
}

/** 多行文本（列传） */
export function TextAreaField({
  label,
  hint,
  value,
  placeholder,
  rows = 5,
  onChange,
}: {
  label: string
  hint?: string
  value: string
  placeholder?: string
  rows?: number
  onChange: (v: string) => void
}) {
  return (
    <FieldShell label={label} hint={hint}>
      <Textarea
        value={value}
        rows={rows}
        placeholder={placeholder}
        className="resize-y"
        onChange={(e) => onChange(e.target.value)}
      />
    </FieldShell>
  )
}

/** 数字输入框 */
export function NumberField({
  label,
  hint,
  value,
  min,
  max,
  step = 1,
  onChange,
}: {
  label: string
  hint?: string
  value: number
  min?: number
  max?: number
  step?: number
  onChange: (v: number) => void
}) {
  return (
    <FieldShell label={label} hint={hint}>
      <Input
        type="number"
        value={Number.isFinite(value) ? value : 0}
        min={min}
        max={max}
        step={step}
        onChange={(e) => {
          const n = Number(e.target.value)
          onChange(Number.isFinite(n) ? n : 0)
        }}
      />
    </FieldShell>
  )
}

/** 数值滑块（用于 0-100 的能力值） */
export function SliderField({
  label,
  value,
  min = 1,
  max = 100,
  accent = false,
  onChange,
}: {
  label: string
  value: number
  min?: number
  max?: number
  accent?: boolean
  onChange: (v: number) => void
}) {
  return (
    <div className="flex items-center gap-4">
      <span className="w-12 shrink-0 text-sm text-muted-foreground">{label}</span>
      <Slider
        value={[value]}
        min={min}
        max={max}
        step={1}
        onValueChange={(v) => onChange(v[0] ?? min)}
        className="flex-1"
      />
      <Input
        type="number"
        value={value}
        min={min}
        max={max}
        onChange={(e) => {
          const n = Number(e.target.value)
          onChange(Number.isFinite(n) ? n : min)
        }}
        className={cn('w-16 text-center font-medium', accent && 'text-gold')}
      />
    </div>
  )
}

/** 下拉选择 */
export function SelectField({
  label,
  hint,
  value,
  choices,
  placeholder = '请选择',
  className,
  onChange,
}: {
  label: string
  hint?: string
  value: number
  choices: Choice[]
  placeholder?: string
  className?: string
  onChange: (v: number) => void
}) {
  return (
    <FieldShell label={label} hint={hint}>
      <Select value={String(value)} onValueChange={(v) => onChange(Number(v))}>
        <SelectTrigger className={cn('w-full', className)}>
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {choices.map((c) => (
            <SelectItem key={c.value} value={String(c.value)}>
              {c.label}
              <span className="ml-2 text-xs text-muted-foreground">({c.value})</span>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </FieldShell>
  )
}

/** 按钮组选择（对应游戏中的 Toggle 组，适合选项较少的枚举） */
export function PillsField({
  label,
  hint,
  value,
  choices,
  onChange,
}: {
  label: string
  hint?: string
  value: number
  choices: Choice[]
  onChange: (v: number) => void
}) {
  return (
    <FieldShell label={label} hint={hint}>
      <div className="flex flex-wrap gap-1.5">
        {choices.map((c) => (
          <button
            key={c.value}
            type="button"
            onClick={() => onChange(c.value)}
            className={cn(
              'cursor-pointer rounded-md border px-3 py-1.5 text-sm transition-colors duration-150',
              value === c.value
                ? 'border-primary bg-primary/15 text-primary'
                : 'border-border text-muted-foreground hover:border-primary/50 hover:text-foreground',
            )}
          >
            {c.label}
          </button>
        ))}
      </div>
    </FieldShell>
  )
}

/** 开关（布尔字段） */
export function SwitchField({
  label,
  hint,
  value,
  onChange,
}: {
  label: string
  hint?: string
  value: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <div className="flex items-center justify-between rounded-md border border-border px-3 py-2.5">
      <div className="flex flex-col">
        <span className="text-sm font-medium text-foreground/90">{label}</span>
        {hint && <span className="text-xs text-muted-foreground">{hint}</span>}
      </div>
      <Switch checked={value} onCheckedChange={onChange} />
    </div>
  )
}

/** 区块标题 */
export function SectionTitle({ title, description }: { title: string; description?: string }) {
  return (
    <div className="mb-4 border-b border-border pb-3">
      <h3 className="font-display text-lg text-gold">{title}</h3>
      {description && <p className="mt-1 text-xs text-muted-foreground">{description}</p>}
    </div>
  )
}

/**
 * 整数数组字段编辑（新版本体数据的数组字段，如 body / wajutsu / wordTac）。
 * 数组的每一项渲染一个输入框，长度与顺序保持不变：
 * 不去重、不丢弃 -1 等负值，避免破坏本体数据。
 */
export function IntArrayField({
  label,
  hint,
  value,
  elementLabels,
  disabled = false,
  onChange,
}: {
  label: string
  hint?: string
  value: number[]
  /** 各元素的说明文字（缺省显示序号） */
  elementLabels?: string[]
  /** 只读（游客浏览时禁用编辑） */
  disabled?: boolean
  onChange: (next: number[]) => void
}) {
  const list = Array.isArray(value) ? value : []

  /** 改写第 index 项（保持数组长度不变） */
  const setAt = (index: number, raw: number) => {
    const next = list.map((item, i) =>
      i === index ? (Number.isFinite(raw) ? Math.trunc(raw) : 0) : item,
    )
    onChange(next)
  }

  return (
    <FieldShell label={`${label}（${list.length} 项）`} hint={hint}>
      <div className="flex flex-wrap gap-2">
        {list.map((item, index) => (
          <div key={index} className="flex flex-col items-center gap-1">
            <span className="text-[10px] text-muted-foreground">
              {elementLabels?.[index] ?? `#${index + 1}`}
            </span>
            <Input
              type="number"
              value={Number.isFinite(item) ? item : 0}
              disabled={disabled}
              onChange={(e) => setAt(index, Number(e.target.value))}
              className="w-20 text-center"
            />
          </div>
        ))}
        {list.length === 0 && (
          <span className="text-xs text-muted-foreground">该字段为空数组</span>
        )}
      </div>
    </FieldShell>
  )
}

/** 单个标签的最大长度（与服务端 TAG_MAX_LENGTH 保持一致） */
const TAG_MAX_LENGTH = 20

/** 单个武将的标签数量上限（与服务端 TAG_MAX_COUNT 保持一致） */
const TAG_MAX_COUNT = 20

/**
 * 标签编辑（多值文本）。
 * 用于给自建武将打标签，供列表按标签筛选；标签以 chips 形式展示，
 * 支持回车 / 逗号快速添加、点击「已有标签」直接复用，避免同义标签写法不一致。
 */
export function TagsField({
  label,
  hint,
  value,
  suggestions = [],
  placeholder = '输入标签后回车添加',
  disabled = false,
  maxCount = TAG_MAX_COUNT,
  onChange,
}: {
  label: string
  hint?: string
  value: string[]
  /** 库中其它武将已使用的标签（快捷点选，避免重复造词） */
  suggestions?: string[]
  placeholder?: string
  /** 只读（游客浏览时禁用编辑） */
  disabled?: boolean
  maxCount?: number
  onChange: (tags: string[]) => void
}) {
  /** 待添加的标签输入内容 */
  const [draft, setDraft] = useState('')

  /** 归一化单个标签：去空白、去开头 # 号并限制长度（与后端 toTagArray 一致） */
  const normalizeTag = (raw: string) => raw.trim().replace(/^#+/, '').slice(0, TAG_MAX_LENGTH)

  /** 追加一个标签（空值、重复、超出数量上限时忽略） */
  const addTag = (raw: string) => {
    const tag = normalizeTag(raw)
    setDraft('')
    if (!tag || value.includes(tag) || value.length >= maxCount) return
    onChange([...value, tag])
  }

  /** 删除一个标签 */
  const removeTag = (tag: string) => onChange(value.filter((t) => t !== tag))

  /** 可快捷选择的标签（排除已选中的） */
  const available = suggestions.filter((tag) => !value.includes(tag))
  const full = value.length >= maxCount

  return (
    <FieldShell label={label} hint={hint}>
      <div className="flex flex-col gap-2">
        <div className="flex gap-2">
          <Input
            value={draft}
            placeholder={full ? `标签数量已达上限（${maxCount}）` : placeholder}
            disabled={disabled || full}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => {
              // 回车或逗号即确认添加，避免录入多个标签时反复点按钮
              if (e.key === 'Enter' || e.key === ',') {
                e.preventDefault()
                addTag(draft)
              }
            }}
          />
          <Button
            type="button"
            variant="outline"
            disabled={disabled || full || !normalizeTag(draft)}
            onClick={() => addTag(draft)}
          >
            添加
          </Button>
        </div>

        {value.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {value.map((tag) => (
              <span
                key={tag}
                className="flex items-center gap-1 rounded-md border border-gold/40 bg-gold/10 px-2 py-0.5 text-xs text-gold"
              >
                #{tag}
                {!disabled && (
                  <button
                    type="button"
                    title="移除该标签"
                    className="cursor-pointer text-muted-foreground transition-colors hover:text-destructive"
                    onClick={() => removeTag(tag)}
                  >
                    <X className="size-3" />
                  </button>
                )}
              </span>
            ))}
          </div>
        )}

        {!disabled && !full && available.length > 0 && (
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="text-[11px] text-muted-foreground">已有标签：</span>
            {available.slice(0, 16).map((tag) => (
              <button
                key={tag}
                type="button"
                className="cursor-pointer rounded border border-border px-1.5 py-0.5 text-[11px] text-muted-foreground transition-colors hover:border-primary/50 hover:text-foreground"
                onClick={() => addTag(tag)}
              >
                {tag}
              </button>
            ))}
          </div>
        )}

        <p className="text-[11px] text-muted-foreground">
          最多 {maxCount} 个标签，单个不超过 {TAG_MAX_LENGTH} 个字；用于列表按标签筛选。
        </p>
      </div>
    </FieldShell>
  )
}
