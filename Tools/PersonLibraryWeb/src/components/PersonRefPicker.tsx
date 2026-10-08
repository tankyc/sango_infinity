/**
 * 文件名：PersonRefPicker.tsx
 * 描述：武将引用选择器。用于人际关系（父/母/配偶/兄弟/好恶）等以武将 ID 为值的字段。
 *       - PersonRefPicker：单选
 *       - PersonRefMultiPicker：多选
 *       候选来源包含“本库自建武将”与“游戏内置武将”两类，支持按姓名或 ID 搜索。
 */

import { useMemo, useState } from 'react'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Button } from '@/components/ui/button'
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command'
import { Badge } from '@/components/ui/badge'
import type { Person } from '@/lib/types'
import { CheckIcon, Plus, UserRound, X } from 'lucide-react'

/** 候选项 */
export interface PersonOption {
  id: number
  name: string
  /** 来源：custom=本库自建武将，core=游戏内置武将 */
  source: 'custom' | 'core'
}

/**
 * 组装候选武将列表（自建武将优先，其次内置武将）。
 * @param persons 自建武将列表
 * @param referenceNames 内置武将名称索引
 * @returns 候选列表
 */
export function buildPersonOptions(
  persons: Person[],
  referenceNames: Record<string, string>,
): PersonOption[] {
  const options: PersonOption[] = []
  const seen = new Set<number>()
  for (const p of persons) {
    options.push({ id: p.Id, name: p.Name || `${p.familyName}${p.giveName}`, source: 'custom' })
    seen.add(p.Id)
  }
  for (const key of Object.keys(referenceNames)) {
    const id = Number(key)
    if (!Number.isFinite(id) || seen.has(id)) continue
    options.push({ id, name: referenceNames[key], source: 'core' })
  }
  return options
}

/** 姓名索引：Id -> 姓名 */
export function buildNameIndex(
  persons: Person[],
  referenceNames: Record<string, string>,
): Record<number, string> {
  const index: Record<number, string> = {}
  for (const p of persons) index[p.Id] = p.Name || `${p.familyName}${p.giveName}`
  for (const key of Object.keys(referenceNames)) {
    const id = Number(key)
    if (!(id in index)) index[id] = referenceNames[key]
  }
  return index
}

interface PersonRefPickerProps {
  /** 当前值（武将 ID，0 表示未设置） */
  value: number
  /** 候选列表 */
  options: PersonOption[]
  /** 显示占位文本 */
  placeholder?: string
  /** 变更回调 */
  onChange: (value: number) => void
}

/** 单武将选择器 */
export function PersonRefPicker({ value, options, placeholder = '未设置', onChange }: PersonRefPickerProps) {
  const [open, setOpen] = useState(false)
  const current = useMemo(() => options.find((o) => o.id === value), [options, value])

  return (
    <div className="flex items-center gap-2">
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            className="min-w-40 justify-start font-normal"
          >
            <UserRound className="text-muted-foreground" />
            {current ? (
              <span className="flex items-center gap-2">
                <span>{current.name}</span>
                <span className="text-xs text-muted-foreground">#{current.id}</span>
              </span>
            ) : (
              <span className="text-muted-foreground">{value > 0 ? `#${value}` : placeholder}</span>
            )}
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-72 p-0">
          <Command>
            <CommandInput placeholder="搜索武将姓名或 ID..." />
            <CommandList>
              <CommandEmpty>未找到匹配武将</CommandEmpty>
              <CommandGroup heading="当前库武将">
                {options
                  .filter((o) => o.source === 'custom')
                  .map((o) => (
                    <CommandItem
                      key={`c-${o.id}`}
                      value={`${o.name} ${o.id} custom`}
                      onSelect={() => {
                        onChange(o.id)
                        setOpen(false)
                      }}
                    >
                      <span className="flex-1">{o.name}</span>
                      <span className="text-xs text-muted-foreground">#{o.id}</span>
                      {value === o.id && <CheckIcon className="size-4 text-primary" />}
                    </CommandItem>
                  ))}
              </CommandGroup>
              <CommandGroup heading="其它武将">
                {options
                  .filter((o) => o.source === 'core')
                  .map((o) => (
                    <CommandItem
                      key={`k-${o.id}`}
                      value={`${o.name} ${o.id} core`}
                      onSelect={() => {
                        onChange(o.id)
                        setOpen(false)
                      }}
                    >
                      <span className="flex-1">{o.name}</span>
                      <span className="text-xs text-muted-foreground">#{o.id}</span>
                      {value === o.id && <CheckIcon className="size-4 text-primary" />}
                    </CommandItem>
                  ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
      {value > 0 && (
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          title="清除"
          onClick={() => onChange(0)}
        >
          <X />
        </Button>
      )}
    </div>
  )
}

interface PersonRefMultiPickerProps {
  /** 当前值（武将 ID 数组） */
  value: number[] | null
  /** 候选列表 */
  options: PersonOption[]
  /** 姓名索引，用于展示已选项 */
  nameIndex: Record<number, string>
  /** 变更回调 */
  onChange: (value: number[] | null) => void
}

/** 多武将选择器 */
export function PersonRefMultiPicker({ value, options, nameIndex, onChange }: PersonRefMultiPickerProps) {
  const [open, setOpen] = useState(false)
  const list = value ?? []

  /** 添加一个武将 ID */
  const add = (id: number) => {
    if (list.includes(id)) {
      onChange(list.filter((v) => v !== id))
    } else {
      onChange([...list, id].sort((a, b) => a - b))
    }
  }

  /** 移除一个武将 ID */
  const remove = (id: number) => {
    const next = list.filter((v) => v !== id)
    onChange(next.length ? next : null)
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap gap-1.5">
        {list.length === 0 && <span className="text-sm text-muted-foreground">未设置</span>}
        {list.map((id) => (
          <Badge key={id} variant="secondary" className="gap-1 pr-1">
            <span>{nameIndex[id] ?? `未知武将`}</span>
            <span className="text-muted-foreground">#{id}</span>
            <button
              type="button"
              className="cursor-pointer rounded-full p-0.5 transition-colors hover:bg-destructive/30"
              title="移除"
              onClick={() => remove(id)}
            >
              <X className="size-3" />
            </button>
          </Badge>
        ))}
      </div>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button type="button" variant="outline" size="sm" className="w-fit">
            <Plus />
            添加武将
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-72 p-0">
          <Command>
            <CommandInput placeholder="搜索武将姓名或 ID..." />
            <CommandList>
              <CommandEmpty>未找到匹配武将</CommandEmpty>
              <CommandGroup heading="当前库武将">
                {options
                  .filter((o) => o.source === 'custom')
                  .map((o) => (
                    <CommandItem
                      key={`c-${o.id}`}
                      value={`${o.name} ${o.id} custom`}
                      onSelect={() => add(o.id)}
                    >
                      <span className="flex-1">{o.name}</span>
                      <span className="text-xs text-muted-foreground">#{o.id}</span>
                      {list.includes(o.id) && <CheckIcon className="size-4 text-primary" />}
                    </CommandItem>
                  ))}
              </CommandGroup>
              <CommandGroup heading="其它武将">
                {options
                  .filter((o) => o.source === 'core')
                  .map((o) => (
                    <CommandItem
                      key={`k-${o.id}`}
                      value={`${o.name} ${o.id} core`}
                      onSelect={() => add(o.id)}
                    >
                      <span className="flex-1">{o.name}</span>
                      <span className="text-xs text-muted-foreground">#{o.id}</span>
                      {list.includes(o.id) && <CheckIcon className="size-4 text-primary" />}
                    </CommandItem>
                  ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
    </div>
  )
}
