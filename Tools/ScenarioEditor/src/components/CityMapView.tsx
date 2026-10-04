/**
 * 都市地图视图（都市列表页的「地图」模式）
 *
 * 与表格视图共用同一套搜索与筛选状态：搜索关键字会让不匹配的都市淡出，
 * 势力筛选同样会让被排除的都市淡出，因此「找某座城」与「看某势力的地盘」都顺手。
 */
import { useMemo, useState } from 'react'
import { Flag, LayoutGrid, Pencil, Rows3 } from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import type { ValidationIssue } from '@/lib/types'
import { buildCityNodes, fillCityTypeLabels, formatCityNumber } from '@/lib/cityMap'
import { buildScenarioNameMaps } from '@/lib/refResolver'
import { useScenarioStore } from '@/state/store'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { CityMapCanvas } from './CityMapCanvas'

interface CityMapViewProps {
  /** 搜索关键字 */
  query: string
  /** 势力筛选（'all' 表示不限） */
  forceFilter: string
  /** 仅看有问题的 */
  issueOnly: boolean
  /** 打开某座都市的编辑面板 */
  onOpenDetail: (id: number) => void
  /** 视图切换（由父组件托管，便于放进工具条） */
  view: 'table' | 'map'
  onViewChange: (view: 'table' | 'map') => void
}

/** 都市地图视图 */
export function CityMapView({ query, forceFilter, issueOnly, onOpenDetail, view, onViewChange }: CityMapViewProps) {
  const { scenario, options, issuesByEntity } = useScenarioStore()
  const [selected, setSelected] = useState<number[]>([])

  const nodes = useMemo(() => fillCityTypeLabels(buildCityNodes(scenario), options), [scenario, options])
  const names = useMemo(() => buildScenarioNameMaps(scenario), [scenario])

  /** 被势力筛选或「仅看问题」排除的都市 */
  const dimmedIds = useMemo(() => {
    const dim = new Set<number>()
    for (const n of nodes) {
      if (forceFilter !== 'all' && String(n.forceId) !== forceFilter) dim.add(n.id)
      if (issueOnly) {
        const issues = issuesByEntity.get(`citySet:${n.id}`)
        if (!issues || !issues.some((i) => i.level === 'error' || i.level === 'warning')) dim.add(n.id)
      }
    }
    return dim
  }, [nodes, forceFilter, issueOnly, issuesByEntity])

  const stats = useMemo(() => {
    const total = nodes.length
    const shown = nodes.filter((n) => !dimmedIds.has(n.id)).length
    const links = nodes.reduce((s, n) => s + n.neighborIds.filter((id) => id > n.id).length, 0)
    const owned = nodes.filter((n) => n.belongCityId > 0).length
    return { total, shown, links, owned }
  }, [nodes, dimmedIds])

  const current = selected.length > 0 ? nodes.find((n) => n.id === selected[selected.length - 1]) ?? null : null
  const currentIssues: ValidationIssue[] = current
    ? (issuesByEntity.get(`citySet:${current.id}`) ?? []).filter((i) => i.level !== 'info')
    : []

  return (
    <div className="flex h-full min-h-0 flex-col">
      {/* 工具条 */}
      <div className="flex shrink-0 flex-wrap items-center gap-2 border-b border-border px-3 py-2">
        <div className="flex overflow-hidden rounded-md border border-border">
          <Button
            variant={view === 'table' ? 'secondary' : 'ghost'}
            size="sm"
            className="h-7 gap-1 rounded-none border-0 px-2 text-[11px]"
            onClick={() => onViewChange('table')}
          >
            <Rows3 className="h-3 w-3" />
            表格
          </Button>
          <Button
            variant={view === 'map' ? 'secondary' : 'ghost'}
            size="sm"
            className="h-7 gap-1 rounded-none border-0 border-l border-border px-2 text-[11px]"
            onClick={() => onViewChange('map')}
          >
            <LayoutGrid className="h-3 w-3" />
            地图
          </Button>
        </div>

        <Badge variant="secondary" className="h-5 gap-1 text-[10px]">
          <Flag className="h-3 w-3" />
          按势力旗帜色
        </Badge>

        <Badge variant="secondary" className="h-5 tabular text-[10px]">
          {stats.shown} / {stats.total} 座
        </Badge>
        <Badge variant="outline" className="h-5 tabular text-[10px]">
          {stats.links} 条相邻 · {stats.owned} 条归属
        </Badge>
        <span className="hidden text-[10px] text-muted-foreground md:inline">
          滚轮缩放用右下角按钮（拖动可平移，触摸可直接滑动）
        </span>
      </div>

      {/* 地图 */}
      <div className="min-h-0 flex-1">
        <CityMapCanvas
          nodes={nodes}
          options={options}
          selected={selected}
          onSelect={(id, additive) =>
            setSelected((prev) => (additive ? (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]) : [id]))
          }
          onActivate={(id) => {
            setSelected([id])
            onOpenDetail(id)
          }}
          query={query}
          dimmedIds={dimmedIds}
          className="h-full w-full"
        />
      </div>

      {/* 选中都市的信息条 */}
      <div
        className={cn(
          'flex shrink-0 flex-wrap items-center gap-x-3 gap-y-1 border-t border-border px-3 py-1.5 text-[11px]',
          current ? 'text-foreground' : 'text-muted-foreground'
        )}
      >
        {current ? (
          <>
            <span className="font-medium">{current.name}</span>
            <span className="tabular text-muted-foreground">#{current.id}</span>
            <span className="text-muted-foreground">
              坐标 <span className="tabular text-foreground">{current.x}, {current.y}</span>
            </span>
            <span className="text-muted-foreground">{current.typeLabel}</span>
            <span className="text-muted-foreground">
              势力{' '}
              <span className="text-foreground">
                {current.forceId > 0 ? names.forceSet.get(current.forceId) ?? `#${current.forceId}` : '无'}
              </span>
            </span>
            <span className="text-muted-foreground">
              军团{' '}
              <span className="text-foreground">
                {current.corpsId > 0 ? names.corpsSet.get(current.corpsId) ?? `#${current.corpsId}` : '无'}
              </span>
            </span>
            <span className="text-muted-foreground">
              驻军 <span className="tabular text-foreground">{formatCityNumber(current.troops)}</span>
            </span>
            <span className="text-muted-foreground">
              相邻{' '}
              <span className="tabular text-foreground">
                {current.neighborIds.length > 0
                  ? current.neighborIds.map((id) => nodes.find((n) => n.id === id)?.name ?? `#${id}`).join('、')
                  : '无'}
              </span>
            </span>
            {current.belongCityId > 0 && (
              <span className="text-muted-foreground">
                所属都市{' '}
                <span className="text-foreground">
                  {nodes.find((n) => n.id === current.belongCityId)?.name ?? `#${current.belongCityId}`}
                </span>
              </span>
            )}
            {currentIssues.length > 0 && (
              <Badge variant="destructive" className="h-4 px-1 text-[10px]">
                {currentIssues.length} 个问题
              </Badge>
            )}
            <Button
              size="sm"
              className="ml-auto h-6 gap-1 px-2 text-[11px]"
              onClick={() => {
                if (current) {
                  onOpenDetail(current.id)
                  toast.info(`正在编辑 ${current.name}`)
                }
              }}
            >
              <Pencil className="h-3 w-3" />
              编辑这座都市
            </Button>
          </>
        ) : (
          <span>点击地图上的圆点查看都市详情，双击直接进入编辑。</span>
        )}
      </div>
    </div>
  )
}
