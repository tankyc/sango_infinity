/**
 * 文件名：HeadIconPicker.tsx
 * 描述：头像（容貌）选择器。
 *       - 按性别筛选可选容貌区间（sexType = -1 表示不限性别）
 *       - 区间可能很大（本体容貌 0-1999），因此采用分页网格展示，避免一次性渲染过多图片
 *       - 支持直接手动输入容貌 ID
 *       - 追加服务端的“自制头像”候选，并提供自制头像制作入口
 *       - 预览优先展示半身像（{id}_1.png），缺失时回退为头像（{id}_2.png）
 */

import { useEffect, useMemo, useState } from 'react'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'
import { headIconUrl } from '@/lib/enums'
import { nextFaceIdOf } from '@/lib/faceRules'
import type { CustomFaceResult, HeadRange } from '@/lib/types'
import { CheckIcon, ChevronLeft, ChevronRight, Images, Sparkles } from 'lucide-react'

/** 每页展示的头像数量 */
const PAGE_SIZE = 96

/** 每行展示的头像数量（与 grid-cols 保持一致） */
const COLUMNS = 8

interface HeadIconPickerProps {
  /** 当前头像 ID */
  value: number
  /** 当前性别，用于筛选可选容貌区间 */
  sex: number
  /** 头像区间配置 */
  ranges: HeadRange[]
  /** 服务端的自制头像数据（用于追加候选与展示下一个可用 ID） */
  customFaces?: CustomFaceResult | null
  /** 是否具备自制头像的制作权限（游客为 false） */
  canCreateCustom?: boolean
  /** 变更回调 */
  onChange: (value: number) => void
  /** 打开自制头像制作弹窗 */
  onCreateCustom?: () => void
}

/**
 * 依据性别取得可选的头像 ID 列表。
 * sexType = -1 的区间（本体容貌）不限性别，始终可选。
 * @param ranges 区间配置
 * @param sex 性别
 * @param customIds 追加的自制头像 ID（已按性别过滤）
 * @returns 头像 ID 数组
 */
function buildHeadIds(ranges: HeadRange[], sex: number, customIds: number[] = []): number[] {
  const ids: number[] = []
  const seen = new Set<number>()
  const push = (id: number) => {
    if (seen.has(id)) return
    seen.add(id)
    ids.push(id)
  }
  for (const range of ranges) {
    if (range.sexType !== -1 && range.sexType !== sex) continue
    for (let id = range.startId; id <= range.endId; id++) push(id)
  }
  // 若未匹配到任何区间，回退为全部区间
  if (ids.length === 0) {
    for (const range of ranges) {
      for (let id = range.startId; id <= range.endId; id++) push(id)
    }
  }
  // 追加自制头像候选（含下一个可用 ID，便于提前预留号位）
  for (const id of customIds) push(id)
  return ids
}

export function HeadIconPicker({
  value,
  sex,
  ranges,
  customFaces = null,
  canCreateCustom = false,
  onChange,
  onCreateCustom,
}: HeadIconPickerProps) {
  const [open, setOpen] = useState(false)
  const [page, setPage] = useState(0)
  /** 预览加载阶段：0=立绘(_1)，1=头像(_2)，2=无资源 */
  const [stage, setStage] = useState(0)

  /** 当前性别可用的自制头像 ID（末尾附带下一个可用号位，便于提前预留） */
  const customIds = useMemo(() => {
    if (!customFaces) return []
    const used = customFaces.items
      .filter((item) => item.sex === sex)
      .map((item) => item.id)
      .sort((a, b) => a - b)
    const next = nextFaceIdOf(customFaces, sex)
    return next > 0 ? [...used, next] : used
  }, [customFaces, sex])

  const headIds = useMemo(() => buildHeadIds(ranges, sex, customIds), [ranges, sex, customIds])

  // 切换头像 ID 时重新尝试加载立绘
  useEffect(() => {
    setStage(0)
  }, [value])

  /** 总页数 */
  const pageCount = Math.max(1, Math.ceil(headIds.length / PAGE_SIZE))

  // 打开选择器时自动定位到当前值所在页
  useEffect(() => {
    if (!open) return
    const index = headIds.indexOf(value)
    setPage(index >= 0 ? Math.floor(index / PAGE_SIZE) : 0)
  }, [open, headIds, value])

  /** 当前页头像 */
  const pageIds = useMemo(
    () => headIds.slice(page * PAGE_SIZE, page * PAGE_SIZE + PAGE_SIZE),
    [headIds, page],
  )

  /** 区间说明文本 */
  const rangeText = useMemo(
    () =>
      ranges
        .map((r) => `${r.startId}-${r.endId}（${r.sexType === -1 ? '通用' : r.sexType === 0 ? '男' : '女'}）`)
        .join('、'),
    [ranges],
  )

  return (
    <div className="flex flex-col items-start gap-4 sm:flex-row sm:items-center">
      {/* 头像预览：优先使用立绘（_1），缺失时回退为头像（_2） */}
      <div className="relative size-28 shrink-0 overflow-hidden rounded-lg border border-border bg-secondary/40">
        {stage >= 2 ? (
          <div className="flex size-full items-center justify-center text-xs text-muted-foreground">
            无资源
          </div>
        ) : (
          <img
            key={stage}
            src={headIconUrl(value, stage === 0 ? 1 : 2)}
            alt={`头像 ${value}`}
            className="size-full object-cover object-top"
            onError={() => setStage((s) => s + 1)}
          />
        )}
      </div>

      <div className="flex flex-col gap-3">
        <div className="flex items-center gap-2">
          <Input
            type="number"
            className="w-32"
            value={value}
            min={0}
            onChange={(e) => onChange(Number(e.target.value) || 0)}
          />
          <Popover open={open} onOpenChange={setOpen}>
            <PopoverTrigger asChild>
              <Button type="button" variant="outline" size="sm">
                <Images />
                选择头像
              </Button>
            </PopoverTrigger>
            <PopoverContent align="start" className="w-[22rem] p-0 sm:w-[24rem]">
              <div className="flex items-center justify-between border-b px-3 py-2 text-xs text-muted-foreground">
                <span>共 {headIds.length} 个可选头像（{sex === 0 ? '男性' : '女性'}）</span>
                <span>
                  第 {page + 1} / {pageCount} 页
                </span>
              </div>
              <div
                className="grid max-h-72 gap-1.5 overflow-y-auto p-3"
                style={{ gridTemplateColumns: `repeat(${COLUMNS}, minmax(0, 1fr))` }}
              >
                {pageIds.map((id) => (
                  <button
                    key={id}
                    type="button"
                    title={`头像 ID ${id}`}
                    onClick={() => {
                      onChange(id)
                      setOpen(false)
                    }}
                    className={cn(
                      'relative aspect-square cursor-pointer overflow-hidden rounded-md border transition-colors duration-150',
                      value === id
                        ? 'border-primary ring-2 ring-primary/40'
                        : 'border-border hover:border-primary/60',
                    )}
                  >
                    <img
                      src={headIconUrl(id, 2)}
                      alt={`头像 ${id}`}
                      loading="lazy"
                      className="size-full object-cover"
                      onError={(e) => {
                        e.currentTarget.style.visibility = 'hidden'
                      }}
                    />
                    <span className="absolute bottom-0 left-0 right-0 bg-background/80 text-center text-[9px] leading-tight text-muted-foreground">
                      {id}
                    </span>
                    {value === id && (
                      <span className="absolute right-0.5 top-0.5 rounded-full bg-primary p-0.5 text-primary-foreground">
                        <CheckIcon className="size-2.5" />
                      </span>
                    )}
                  </button>
                ))}
              </div>
              {/* 分页控制 */}
              <div className="flex items-center justify-between border-t px-3 py-2">
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  disabled={page <= 0}
                  onClick={() => setPage((p) => Math.max(0, p - 1))}
                >
                  <ChevronLeft />
                  上一页
                </Button>
                <span className="text-xs text-muted-foreground">每页 {PAGE_SIZE} 个</span>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  disabled={page >= pageCount - 1}
                  onClick={() => setPage((p) => Math.min(pageCount - 1, p + 1))}
                >
                  下一页
                  <ChevronRight />
                </Button>
              </div>
            </PopoverContent>
          </Popover>
          <Button
            type="button"
            variant="outline"
            size="sm"
            title={canCreateCustom ? '从本地图片制作自定义头像' : '游客不可制作，请先登录'}
            onClick={onCreateCustom}
          >
            <Sparkles />
            自制头像
          </Button>
        </div>
        <p className="text-xs text-muted-foreground">容貌 ID 区间：{rangeText}</p>
        {customFaces && customFaces.items.length > 0 && (
          <p className="text-xs text-muted-foreground">
            自制头像：共 {customFaces.items.length} 个，当前性别下一个可用 ID 为
            <span className="ml-1 text-primary">{nextFaceIdOf(customFaces, sex)}</span>
          </p>
        )}
      </div>
    </div>
  )
}
