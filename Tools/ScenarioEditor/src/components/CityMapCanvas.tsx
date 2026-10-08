/** 都市地图画布：按 x / y 网格坐标绘制，支持缩放、拖拽、相邻连线、悬停信息与关键字高亮 */
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link2, Maximize2, Minus, Palette, Plus, Signature, Tag } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Options } from '@/lib/types'
import {
  buildForceColorMap,
  cityMarkerShape,
  computeCityBounds,
  forceColorOf,
  formatCityNumber,
  matchCityNodes,
  type CityMapNode,
  type CityMarkerShape,
} from '@/lib/cityMap'
import { buildScenarioNameMaps } from '@/lib/refResolver'
import { useScenarioStore } from '@/state/store'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'

/** 四周留白 */
const PAD = 30
/** 默认格子边长 */
const DEFAULT_CELL = 14
const MIN_CELL = 6
const MAX_CELL = 34
/** 「适应窗口」时不低于该缩放，否则标记会糊成一片 */
const FIT_MIN_CELL = 10
/** 标记直径 = 格子边长 × 该系数（0.52 是早期版本，这里按需求放大 3 倍） */
const MARKER_SCALE = 1.56
/** 名称字号 */
const LABEL_FONT = 11

/**
 * 标记形状样式。
 *
 * 形状承担「建筑类型」的区分（都市=方、关所=圆、港=菱），
 * 颜色留给所属势力，因此同一势力的所有城市颜色一致。
 *
 * @param shape 形状
 * @returns CSS 样式
 */
function markerShape(shape: CityMarkerShape): React.CSSProperties {
  if (shape === 'circle') return { borderRadius: 9999 }
  if (shape === 'diamond') return { borderRadius: 3, transform: 'rotate(45deg)' }
  return { borderRadius: 2 }
}

export interface CityMapCanvasProps {
  nodes: CityMapNode[]
  options: Options | null
  /** 选中的都市 Id */
  selected: number[]
  onSelect: (id: number, additive: boolean) => void
  /** 双击进入编辑 */
  onActivate?: (id: number) => void
  /** 关键字：命中高亮，其余淡出 */
  query?: string
  /** 额外淡出的 Id（如被势力筛选排除的） */
  dimmedIds?: Set<number>
  className?: string
}

/** 都市地图画布 */
export function CityMapCanvas({
  nodes,
  options,
  selected,
  onSelect,
  onActivate,
  query = '',
  dimmedIds,
  className,
}: CityMapCanvasProps) {
  const { scenario } = useScenarioStore()
  const [cell, setCell] = useState(DEFAULT_CELL)
  const [showLinks, setShowLinks] = useState(true)
  const [showLabels, setShowLabels] = useState(true)
  /** 名称避让：开启后只显示不与已放置名称重叠的那些，避免糊成一片 */
  const [avoidOverlap, setAvoidOverlap] = useState(true)
  const [hover, setHover] = useState<{ node: CityMapNode; rect: DOMRect } | null>(null)
  /** 容器尺寸：用于把画布撑到至少铺满可视区 */
  const [box, setBox] = useState({ w: 0, h: 0 })

  const boxRef = useRef<HTMLDivElement | null>(null)
  const dragRef = useRef<{ x: number; y: number; left: number; top: number; moved: number } | null>(null)
  const skipClickRef = useRef(false)

  const bounds = useMemo(() => computeCityBounds(nodes), [nodes])
  const byId = useMemo(() => new Map(nodes.map((n) => [n.id, n])), [nodes])
  const hit = useMemo(() => matchCityNodes(nodes, query), [nodes, query])
  /** 势力旗帜色：势力 -> Flags.json 的 color */
  const forceColors = useMemo(() => buildForceColorMap(scenario, options), [scenario, options])
  /** 势力名（用于图例） */
  const forceNames = useMemo(() => buildScenarioNameMaps(scenario).forceSet, [scenario])

  const width = Math.max(box.w, PAD * 2 + bounds.cols * cell)
  const height = Math.max(box.h, PAD * 2 + bounds.rows * cell)

  const posOf = useCallback(
    (n: CityMapNode) => ({
      left: PAD + (n.x - bounds.minX) * cell + cell / 2,
      top: PAD + (n.y - bounds.minY) * cell + cell / 2,
    }),
    [bounds, cell]
  )
  const colorOf = useCallback((n: CityMapNode) => forceColorOf(n.forceId, forceColors), [forceColors])

  /** 标记直径：随缩放变化，但始终不小于 12px */
  const markerSize = useMemo(() => Math.max(12, Math.min(46, cell * MARKER_SCALE)), [cell])

  /**
   * 需要绘制名称的都市。
   *
   * 先按「选中 > 悬停 > 相邻多 > 驻军多」排序，再贪心放置，
   * 与已放置名称重叠的跳过；关闭避让时全部显示。
   * 这样在低缩放下地图依然干净，放大后名称自然补齐。
   */
  const labelledIds = useMemo(() => {
    if (!showLabels) return new Set<number>()
    const force = new Set<number>([...(hover ? [hover.node.id] : []), ...selected])
    if (!avoidOverlap) return new Set(nodes.map((n) => n.id))

    const order = [...nodes].sort((a, b) => {
      const fa = force.has(a.id) ? 1 : 0
      const fb = force.has(b.id) ? 1 : 0
      if (fa !== fb) return fb - fa
      if (a.neighborIds.length !== b.neighborIds.length) return b.neighborIds.length - a.neighborIds.length
      if (a.troops !== b.troops) return b.troops - a.troops
      return a.id - b.id
    })

    const placed: { x1: number; y1: number; x2: number; y2: number }[] = []
    const out = new Set<number>()
    const half = markerSize / 2
    for (const n of order) {
      const p = posOf(n)
      const w = n.name.length * LABEL_FONT * 0.92 + 10
      const h = LABEL_FONT + 8
      // 与渲染保持一致：按钮宽 = size，名称从 size/2 + ml-1.5 处开始
      const box2 = { x1: p.left + half + 6, y1: p.top - h / 2, x2: p.left + half + 6 + w, y2: p.top + h / 2 }
      if (force.has(n.id) || !placed.some((r) => !(box2.x2 < r.x1 || box2.x1 > r.x2 || box2.y2 < r.y1 || box2.y1 > r.y2))) {
        placed.push(box2)
        out.add(n.id)
      }
    }
    return out
  }, [nodes, showLabels, avoidOverlap, selected, hover, posOf, markerSize])

  const zoom = useCallback((delta: number) => {
    setCell((c) => Math.min(MAX_CELL, Math.max(MIN_CELL, c + delta)))
  }, [])

  // 容器尺寸：画布至少铺满可视区，同时记住可视区大小供「适应窗口」使用
  useEffect(() => {
    const el = boxRef.current
    if (!el) return
    const read = () => setBox({ w: el.clientWidth, h: el.clientHeight })
    read()
    const ro = new ResizeObserver(read)
    ro.observe(el)
    return () => ro.disconnect()
  }, [])

  /** 缩放到「看得全貌」；容器尚未布局完成时返回 false */
  const fit = useCallback(() => {
    const el = boxRef.current
    if (!el) return false
    const w = el.clientWidth - PAD * 2
    const h = el.clientHeight - PAD * 2
    if (w <= 0 || h <= 0) return false
    setCell(Math.min(MAX_CELL, Math.max(FIT_MIN_CELL, Math.floor(Math.min(w / bounds.cols, h / bounds.rows)))))
    return true
  }, [bounds])

  // 首次拿到数据且容器已完成布局后自动适应一次
  const fittedRef = useRef(false)
  useEffect(() => {
    if (fittedRef.current || nodes.length === 0) return
    if (fit()) fittedRef.current = true
  }, [fit, nodes.length])

  // 拖拽平移：仅鼠标，触摸留给原生滚动
  useEffect(() => {
    const el = boxRef.current
    if (!el) return
    const down = (e: PointerEvent) => {
      if (e.pointerType !== 'mouse' || e.button !== 0) return
      dragRef.current = { x: e.clientX, y: e.clientY, left: el.scrollLeft, top: el.scrollTop, moved: 0 }
    }
    const move = (e: PointerEvent) => {
      const d = dragRef.current
      if (!d) return
      const dx = e.clientX - d.x
      const dy = e.clientY - d.y
      d.moved = Math.max(d.moved, Math.abs(dx) + Math.abs(dy))
      if (d.moved > 4) {
        skipClickRef.current = true
        el.scrollLeft = d.left - dx
        el.scrollTop = d.top - dy
      }
    }
    const up = () => {
      dragRef.current = null
    }
    el.addEventListener('pointerdown', down)
    window.addEventListener('pointermove', move)
    window.addEventListener('pointerup', up)
    return () => {
      el.removeEventListener('pointerdown', down)
      window.removeEventListener('pointermove', move)
      window.removeEventListener('pointerup', up)
    }
  }, [])

  const legend = useMemo(() => {
    const counts = new Map<number, number>()
    let unowned = 0
    for (const n of nodes) {
      if (n.forceId > 0) counts.set(n.forceId, (counts.get(n.forceId) ?? 0) + 1)
      else unowned += 1
    }
    const list = [...counts.entries()]
      .sort((a, b) => b[1] - a[1])
      .slice(0, 12)
      .map(([id, count]) => ({
        key: String(id),
        label: `${forceNames.get(id) ?? `势力 ${id}`}（${count}）`,
        color: forceColorOf(id, forceColors),
      }))
    if (unowned > 0) {
      list.push({ key: 'none', label: `无归属（${unowned}）`, color: forceColorOf(0, forceColors) })
    }
    return list
  }, [nodes, forceNames, forceColors])

  /** 形状图例：形状是建筑类型的唯一视觉编码 */
  const shapeLegend = useMemo(() => {
    const types = new Map<number, CityMapNode>()
    for (const n of nodes) if (!types.has(n.type)) types.set(n.type, n)
    return [...types.values()].map((n) => ({
      key: String(n.type),
      label: n.typeLabel || `类型 ${n.type}`,
      shape: cityMarkerShape(n.type, options),
    }))
  }, [nodes, options])

  if (nodes.length === 0) {
    return (
      <div className={cn('flex h-full items-center justify-center text-[12px] text-muted-foreground', className)}>
        当前剧本没有带坐标的都市
      </div>
    )
  }

  return (
    <div className={cn('relative', className)}>
      <div ref={boxRef} className="h-full w-full cursor-grab overflow-auto active:cursor-grabbing">
        <div className="relative" style={{ width, height }}>
          {showLinks && (
            <svg className="pointer-events-none absolute inset-0" width={width} height={height}>
              {/* 归属连线：关所 / 港 -> 所属都市（虚线） */}
              {nodes.map((n) => {
                if (n.belongCityId <= 0) return null
                const owner = byId.get(n.belongCityId)
                if (!owner) return null
                const a = posOf(n)
                const b = posOf(owner)
                const on = selected.includes(n.id) || selected.includes(owner.id)
                return (
                  <line
                    key={`own-${n.id}`}
                    x1={a.left}
                    y1={a.top}
                    x2={b.left}
                    y2={b.top}
                    stroke={on ? 'rgb(var(--primary))' : 'rgba(148,163,184,0.5)'}
                    strokeWidth={on ? 1.6 : 1}
                    strokeDasharray="4 3"
                  />
                )
              })}
              {/* 相邻连线：实线，只画一次（id 较大的一侧不重复画） */}
              {nodes.map((n) =>
                n.neighborIds
                  .filter((id) => id > n.id)
                  .map((id) => {
                    const other = byId.get(id)
                    if (!other) return null
                    const a = posOf(n)
                    const b = posOf(other)
                    const on = selected.includes(n.id) || selected.includes(id)
                    return (
                      <line
                        key={`nb-${n.id}-${id}`}
                        x1={a.left}
                        y1={a.top}
                        x2={b.left}
                        y2={b.top}
                        stroke={on ? 'rgb(var(--primary))' : 'rgba(148,163,184,0.35)'}
                        strokeWidth={on ? 1.8 : 1}
                      />
                    )
                  })
              )}
            </svg>
          )}

          {nodes.map((n) => {
            const { left, top } = posOf(n)
            const on = selected.includes(n.id)
            const dim = (query.trim().length > 0 && !hit.has(n.id)) || dimmedIds?.has(n.id) === true
            const size = markerSize
            const showName = labelledIds.has(n.id)
            const fill = colorOf(n)
            // 白色填充（无归属）时描边必须保持深色，否则选中态会糊成一团
            const isWhite = fill.toLowerCase() === '#ffffff'
            const borderClass = isWhite ? 'border-black/35' : on ? 'border-white' : 'border-black/25'
            return (
              <button
                key={n.id}
                type="button"
                className="absolute -translate-x-1/2 -translate-y-1/2 outline-none"
                style={{ left, top }}
                title={`${n.name}（${n.x}, ${n.y}）`}
                onClick={(e) => {
                  if (skipClickRef.current) {
                    skipClickRef.current = false
                    return
                  }
                  onSelect(n.id, e.shiftKey || e.ctrlKey || e.metaKey)
                }}
                onDoubleClick={() => onActivate?.(n.id)}
                onMouseEnter={(e) => setHover({ node: n, rect: e.currentTarget.getBoundingClientRect() })}
                onMouseLeave={() => setHover(null)}
              >
                {on && (
                  <span
                    className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-primary"
                    style={{ width: size + 9, height: size + 9 }}
                  />
                )}
                <span
                  className={cn('block border transition-opacity', dim && 'opacity-25', borderClass)}
                  style={{ width: size, height: size, background: fill, ...markerShape(cityMarkerShape(n.type, options)) }}
                />
                {showName && (
                  <span
                    className={cn(
                      'pointer-events-none absolute left-full top-1/2 ml-1.5 -translate-y-1/2 whitespace-nowrap rounded px-1 py-0.5 leading-none',
                      on ? 'bg-primary text-primary-foreground' : 'bg-background/85 text-foreground',
                      dim && 'opacity-60'
                    )}
                    style={{ fontSize: LABEL_FONT }}
                  >
                    {n.name}
                  </span>
                )}
              </button>
            )
          })}
        </div>
      </div>

      <div className="absolute right-2 top-2 flex items-center gap-1 rounded-md border border-border bg-card/90 p-1 shadow-sm backdrop-blur">
        <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => zoom(-2)} title="缩小">
          <Minus className="h-3.5 w-3.5" />
        </Button>
        <span className="w-10 text-center text-[10px] tabular text-muted-foreground">
          {Math.round((cell / DEFAULT_CELL) * 100)}%
        </span>
        <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => zoom(2)} title="放大">
          <Plus className="h-3.5 w-3.5" />
        </Button>
        <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => fit()} title="适应窗口">
          <Maximize2 className="h-3.5 w-3.5" />
        </Button>
        <Separator orientation="vertical" className="mx-0.5 h-4" />
        <Button variant={showLinks ? 'secondary' : 'ghost'} size="icon" className="h-7 w-7" onClick={() => setShowLinks((v) => !v)} title="相邻连线">
          <Link2 className="h-3.5 w-3.5" />
        </Button>
        <Button
          variant={showLabels ? 'secondary' : 'ghost'}
          size="icon"
          className="h-7 w-7"
          onClick={() => setShowLabels((v) => !v)}
          title="都市名称"
        >
          <Tag className="h-3.5 w-3.5" />
        </Button>
        <Button
          variant={avoidOverlap ? 'secondary' : 'ghost'}
          size="icon"
          className="h-7 w-7"
          onClick={() => setAvoidOverlap((v) => !v)}
          title={avoidOverlap ? '名称避让已开启（点击显示全部）' : '名称避让已关闭（点击开启避让）'}
        >
          <Signature className="h-3.5 w-3.5" />
        </Button>
      </div>

      {legend.length > 0 && (
        <Popover>
          <PopoverTrigger asChild>
            <Button
              variant="secondary"
              size="icon"
              className="absolute bottom-2 left-2 h-8 w-8 shadow-sm"
              title="图例：势力颜色 / 建筑类型 / 连线含义"
            >
              <Palette className="h-4 w-4" />
            </Button>
          </PopoverTrigger>
          <PopoverContent align="start" side="top" className="w-[268px] p-2.5">
            <p className="mb-1 text-[9px] uppercase tracking-wide text-muted-foreground">所属势力（旗帜色）</p>
            <div className="max-h-[190px] space-y-1 overflow-y-auto pr-1">
              {legend.map((it) => (
                <div key={it.key} className="flex items-center gap-1.5 text-[11px]">
                  <span
                    className="h-2.5 w-2.5 shrink-0 rounded-[2px] border border-black/20"
                    style={{ background: it.color }}
                  />
                  <span className="min-w-0 flex-1 truncate">{it.label}</span>
                </div>
              ))}
            </div>

            {shapeLegend.length > 0 && (
              <>
                <Separator className="my-2" />
                <p className="mb-1 text-[9px] uppercase tracking-wide text-muted-foreground">建筑类型（形状）</p>
                <div className="space-y-1">
                  {shapeLegend.map((it) => (
                    <div key={it.key} className="flex items-center gap-1.5 text-[11px]">
                      <span
                        className="h-2.5 w-2.5 shrink-0 border border-black/25 bg-muted-foreground/70"
                        style={markerShape(it.shape)}
                      />
                      <span className="min-w-0 flex-1 truncate">{it.label}</span>
                    </div>
                  ))}
                </div>
              </>
            )}

            <Separator className="my-2" />
            <p className="mb-1 text-[9px] uppercase tracking-wide text-muted-foreground">连线</p>
            <div className="space-y-1 text-[11px] text-muted-foreground">
              <p className="flex items-center gap-1.5">
                <svg width="18" height="8" className="shrink-0">
                  <line x1="0" y1="4" x2="18" y2="4" stroke="rgba(148,163,184,0.6)" strokeWidth="1.4" />
                </svg>
                相邻都市（NeighborList）
              </p>
              <p className="flex items-center gap-1.5">
                <svg width="18" height="8" className="shrink-0">
                  <line
                    x1="0"
                    y1="4"
                    x2="18"
                    y2="4"
                    stroke="rgba(148,163,184,0.7)"
                    strokeWidth="1.4"
                    strokeDasharray="4 3"
                  />
                </svg>
                关所 / 港 → 所属都市
              </p>
            </div>
          </PopoverContent>
        </Popover>
      )}

      {hover && <CityMapTooltip node={hover.node} rect={hover.rect} color={colorOf(hover.node)} />}
    </div>
  )
}

/** 悬停信息卡（fixed 定位，避免被画布裁剪） */
function CityMapTooltip({ node, rect, color }: { node: CityMapNode; rect: DOMRect; color: string }) {
  const { scenario } = useScenarioStore()
  const names = useMemo(() => buildScenarioNameMaps(scenario), [scenario])
  /** 所属都市名（关所 / 港） */
  const ownerName = (id: number) => names.citySet.get(id) ?? `#${id}`
  return (
    <div
      className="pointer-events-none fixed z-50 w-[232px] rounded-md border border-border bg-popover px-2.5 py-2 text-[11px] shadow-lg"
      style={{
        left: rect.left > window.innerWidth - 250 ? rect.left - 240 : rect.left + 14,
        top: Math.max(8, Math.min(rect.top - 40, window.innerHeight - 170)),
      }}
    >
      <div className="flex items-center gap-1.5">
        <span className="h-2.5 w-2.5 shrink-0 rounded-full border border-black/20" style={{ background: color }} />
        <span className="truncate text-[12px] font-medium">{node.name}</span>
        <span className="ml-auto shrink-0 tabular text-[10px] text-muted-foreground">#{node.id}</span>
      </div>
      <div className="mt-1 grid grid-cols-2 gap-x-2 gap-y-0.5 text-[10px] text-muted-foreground">
        <span>坐标</span>
        <span className="tabular text-foreground">{node.x}, {node.y}</span>
        <span>类型</span>
        <span className="truncate text-foreground">{node.typeLabel || `类型 ${node.type}`}</span>
        <span>势力</span>
        <span className="truncate text-foreground">{node.forceId > 0 ? names.forceSet.get(node.forceId) ?? `#${node.forceId}` : '无'}</span>
        <span>军团</span>
        <span className="truncate text-foreground">{node.corpsId > 0 ? names.corpsSet.get(node.corpsId) ?? `#${node.corpsId}` : '无'}</span>
        <span>驻军</span>
        <span className="tabular text-foreground">{formatCityNumber(node.troops)}</span>
        <span>相邻</span>
        <span className="tabular text-foreground">{node.neighborIds.length} 座</span>
        {node.belongCityId > 0 && (
          <>
            <span>所属都市</span>
            <span className="truncate text-foreground">{ownerName(node.belongCityId)}</span>
          </>
        )}
      </div>
    </div>
  )
}
