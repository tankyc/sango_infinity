/**
 * 都市地图选择面板
 *
 * 供「所属都市 / 所在都市 / 相邻都市」这类引用字段在下拉搜索之外提供一个可视化入口：
 * 87 座城靠名字搜索很容易选错方位，而在图上能一眼看出方位、相邻关系与归属。
 */
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Map as MapIcon, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { CityMapNode } from '@/lib/cityMap'
import { buildCityNodes, fillCityTypeLabels } from '@/lib/cityMap'
import { useScenarioStore } from '@/state/store'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { CityMapCanvas } from './CityMapCanvas'

export interface CityMapButtonProps {
  /** 单选时的当前值 */
  value?: number | null
  /** 多选时的当前值 */
  values?: number[]
  /** 多选模式 */
  multiple?: boolean
  /** 选中变化回调 */
  onChange: (ids: number[]) => void
  title?: string
  className?: string
}

/**
 * 地图选择入口按钮。
 *
 * 放在引用下拉旁边作为第二入口：想按名字搜就用下拉，想看方位就用地图。
 */
export function CityMapButton({ value, values, multiple, onChange, title, className }: CityMapButtonProps) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <Button
        variant="ghost"
        size="icon"
        className={cn('h-7 w-7 shrink-0', className)}
        title={title ?? '在地图上选择都市'}
        onClick={() => setOpen(true)}
      >
        <MapIcon className="h-3.5 w-3.5" />
      </Button>
      <CityMapPicker
        open={open}
        onOpenChange={setOpen}
        value={value}
        values={values}
        multiple={multiple}
        onChange={onChange}
        title={title}
      />
    </>
  )
}

export interface CityMapValueButtonProps {
  /** 当前值（null 表示未设置） */
  value: number | null
  /** 当前值对应的都市名 */
  name?: string
  onChange: (value: number | null) => void
  disabled?: boolean
  placeholder?: string
  title?: string
  className?: string
}

/**
 * 都市引用字段的主控件。
 *
 * 所有指向都市的字段都默认「点击即弹出地图」——87 座城按名字搜很容易选错方位，
 * 而所属都市 / 所在都市这类字段本质上问的是「在哪」，地图比下拉更直接。
 * 名称搜索降级为旁边的次按钮，需要时仍可使用。
 */
export function CityMapValueButton({
  value,
  name,
  onChange,
  disabled,
  placeholder,
  title,
  className,
}: CityMapValueButtonProps) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button
        type="button"
        disabled={disabled}
        title={title ?? '点击打开地图选择都市'}
        onClick={() => setOpen(true)}
        className={cn(
          'flex h-8 min-w-0 flex-1 items-center justify-between gap-1 rounded-md border border-input px-2 text-left text-[12px] transition-colors hover:bg-accent/50 focus:outline-none focus:ring-1 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
          className
        )}
      >
        <span className="truncate">
          {value === null || value === undefined ? (
            <span className="text-muted-foreground">{placeholder ?? '未设置'}</span>
          ) : (
            <>
              <span className="tabular text-muted-foreground">{value}</span>
              <span className="ml-2">{name || '（不存在）'}</span>
            </>
          )}
        </span>
        <MapIcon className="h-3.5 w-3.5 shrink-0 opacity-70" />
      </button>
      <CityMapPicker
        open={open}
        onOpenChange={setOpen}
        value={value}
        onChange={(ids) => onChange(ids.length > 0 ? ids[0] : null)}
        title={title}
      />
    </>
  )
}

export interface CityMapPickerProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** 单选时的当前值 */
  value?: number | null
  /** 多选时的当前值 */
  values?: number[]
  /** 多选模式 */
  multiple?: boolean
  /** 选中变化回调（单选也返回数组，长度 0 或 1） */
  onChange: (ids: number[]) => void
  /** 弹窗标题 */
  title?: string
  /** 外部已输入的关键字 */
  query?: string
}

/** 都市地图选择面板 */
export function CityMapPicker({
  open,
  onOpenChange,
  value = null,
  values = [],
  multiple = false,
  onChange,
  title = '在地图上选择都市',
  query = '',
}: CityMapPickerProps) {
  const { scenario, options } = useScenarioStore()
  const [draft, setDraft] = useState<number[]>([])

  // 打开时用当前值初始化草稿，取消时不污染原值
  useEffect(() => {
    if (open) setDraft(multiple ? values : value !== null ? [value] : [])
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const nodes = useMemo(() => fillCityTypeLabels(buildCityNodes(scenario), options), [scenario, options])

  const handleSelect = useCallback(
    (id: number, additive: boolean) => {
      if (multiple || additive) {
        setDraft((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]))
      } else {
        setDraft([id])
      }
    },
    [multiple]
  )

  const rangeText = useMemo(() => {
    if (nodes.length === 0) return '—'
    const xs = nodes.map((n) => n.x)
    const ys = nodes.map((n) => n.y)
    return `${Math.min(...xs)}~${Math.max(...xs)} × ${Math.min(...ys)}~${Math.max(...ys)}`
  }, [nodes])

  const draftNodes = useMemo(
    () => draft.map((id) => nodes.find((n) => n.id === id)).filter((n): n is CityMapNode => Boolean(n)),
    [draft, nodes]
  )

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/*
        sm:max-w-* 覆盖 DialogContent 自带的 `sm:max-w-lg`（512px）。
        地图弹窗受这个影响最严重：不覆盖的话宽屏下地图只能画在 512px 里。
      */}
      <DialogContent className="flex h-[min(92vh,760px)] w-[min(96vw,1080px)] flex-col gap-0 overflow-hidden p-0 sm:max-w-[1080px]">
        <DialogHeader className="shrink-0 border-b border-border px-5 py-3">
          <DialogTitle className="flex items-center gap-2 text-[15px]">
            <MapIcon className="h-4 w-4 text-primary" />
            {title}
          </DialogTitle>
          <DialogDescription className="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-[11px]">
            <span>
              共 {nodes.length} 座，坐标范围 {rangeText}
            </span>
            <span className="text-muted-foreground">
              {multiple ? '点击可多选（Shift 追加），确认后写入' : '点击选中，Shift 追加，双击可打开编辑'}
            </span>
          </DialogDescription>
        </DialogHeader>

        <div className="min-h-0 flex-1">
          <CityMapCanvas
            nodes={nodes}
            options={options}
            selected={draft}
            onSelect={handleSelect}
            query={query}
            className="h-full w-full"
          />
        </div>

        <DialogFooter className="shrink-0 flex-col items-stretch gap-2 border-t border-border px-5 py-2.5 sm:flex-row sm:items-center">
          <div className="flex min-w-0 flex-1 flex-wrap items-center gap-1.5">
            <span className="shrink-0 text-[11px] text-muted-foreground">已选 {draft.length} 座：</span>
            {draftNodes.length === 0 && <span className="text-[11px] text-muted-foreground">（无）</span>}
            {draftNodes.slice(0, 14).map((n) => (
              <Badge key={n.id} variant="secondary" className="h-5 gap-1 px-1.5 text-[10px]">
                {n.name}
                <button
                  type="button"
                  className="text-muted-foreground hover:text-destructive"
                  title="移除"
                  onClick={() => setDraft((prev) => prev.filter((x) => x !== n.id))}
                >
                  <X className="h-2.5 w-2.5" />
                </button>
              </Badge>
            ))}
            {draftNodes.length > 14 && (
              <span className="text-[10px] text-muted-foreground">…共 {draftNodes.length} 座</span>
            )}
          </div>
          <div className="flex shrink-0 items-center gap-1.5">
            <Button variant="outline" size="sm" className="h-8" onClick={() => setDraft([])}>
              清空
            </Button>
            <Button variant="outline" size="sm" className="h-8" onClick={() => onOpenChange(false)}>
              取消
            </Button>
            <Button
              size="sm"
              className="h-8"
              disabled={draft.length === 0}
              onClick={() => {
                onChange(draft)
                onOpenChange(false)
              }}
            >
              确定{draft.length > 0 ? `（${draft.length}）` : ''}
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
