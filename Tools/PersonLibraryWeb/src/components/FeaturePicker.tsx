/**
 * 文件名：FeaturePicker.tsx
 * 描述：武将特性（特技）多选器。以可搜索列表 + 复选框的方式选择特性，
 *       已选特性以标签形式展示。
 */

import { useMemo, useState } from 'react'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Checkbox } from '@/components/ui/checkbox'
import { ScrollArea } from '@/components/ui/scroll-area'
import type { FeatureOption } from '@/lib/types'
import { Plus, Sparkles, X } from 'lucide-react'

interface FeaturePickerProps {
  /** 已选特性 ID 列表 */
  value: number[] | null
  /** 全部特性定义 */
  features: FeatureOption[]
  /** 变更回调 */
  onChange: (value: number[] | null) => void
}

export function FeaturePicker({ value, features, onChange }: FeaturePickerProps) {
  const [open, setOpen] = useState(false)
  const [keyword, setKeyword] = useState('')
  const list = value ?? []

  /** 特性名称索引 */
  const featureMap = useMemo(() => {
    const map: Record<number, FeatureOption> = {}
    for (const f of features) map[f.id] = f
    return map
  }, [features])

  /** 按关键字过滤后的特性列表 */
  const filtered = useMemo(() => {
    const kw = keyword.trim()
    if (!kw) return features
    return features.filter(
      (f) => f.name.includes(kw) || String(f.id) === kw || (f.desc ?? '').includes(kw),
    )
  }, [features, keyword])

  /** 切换某个特性 */
  const toggle = (id: number) => {
    if (list.includes(id)) {
      const next = list.filter((v) => v !== id)
      onChange(next.length ? next : null)
    } else {
      onChange([...list, id].sort((a, b) => a - b))
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap gap-1.5">
        {list.length === 0 && <span className="text-sm text-muted-foreground">未设置特性</span>}
        {list.map((id) => (
          <Badge key={id} variant="secondary" className="gap-1 pr-1">
            <Sparkles className="size-3 text-primary" />
            <span>{featureMap[id]?.name ?? '未知特性'}</span>
            <button
              type="button"
              className="cursor-pointer rounded-full p-0.5 transition-colors hover:bg-destructive/30"
              title="移除"
              onClick={() => toggle(id)}
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
            添加特性（已选 {list.length}）
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-80 p-0">
          <div className="border-b p-2">
            <Input
              value={keyword}
              placeholder="搜索特性名称或效果..."
              onChange={(e) => setKeyword(e.target.value)}
            />
          </div>
          <ScrollArea className="h-72">
            <div className="flex flex-col p-1">
              {filtered.length === 0 && (
                <span className="py-6 text-center text-sm text-muted-foreground">未找到匹配特性</span>
              )}
              {filtered.map((f) => (
                <label
                  key={f.id}
                  className="flex cursor-pointer items-start gap-2 rounded-sm px-2 py-1.5 transition-colors hover:bg-accent"
                >
                  <Checkbox
                    checked={list.includes(f.id)}
                    onCheckedChange={() => toggle(f.id)}
                    className="mt-0.5"
                  />
                  <div className="flex flex-col">
                    <span className="text-sm">
                      {f.name}
                      <span className="ml-2 text-xs text-muted-foreground">#{f.id} · Lv{f.level}</span>
                    </span>
                    {f.desc && <span className="text-xs text-muted-foreground">{f.desc}</span>}
                  </div>
                </label>
              ))}
            </div>
          </ScrollArea>
        </PopoverContent>
      </Popover>
    </div>
  )
}
