/**
 * 校验结果面板
 *
 * 汇总全剧本的校验问题，按级别与集合筛选，点击任意问题可直接跳转到对应实体。
 * 列表使用虚拟滚动，即使出现上千条问题也能流畅浏览。
 */
import React, { useCallback, useMemo, useState } from 'react'
import { AlertTriangle, CheckCircle2, CircleAlert, Info, RefreshCw, Search } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { CollectionKey, IssueLevel } from '@/lib/types'
import { COLLECTION_META, COLLECTION_ORDER } from '@/lib/types'
import { useScenarioStore } from '@/state/store'
import { useVirtualList } from '@/hooks/useVirtualList'
import { buildScenarioNameMaps } from '@/lib/refResolver'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { Progress } from '@/components/ui/progress'

/** 行高 */
const ISSUE_ROW_HEIGHT = 56

interface ValidationPanelProps {
  /** 跳转到某个实体 */
  onNavigate: (collection: CollectionKey, entityId: number) => void
}

/** 级别样式 */
const LEVEL_STYLE: Record<IssueLevel, { label: string; className: string; icon: React.ElementType }> = {
  error: { label: '错误', className: 'text-destructive', icon: CircleAlert },
  warning: { label: '警告', className: 'text-amber-500', icon: AlertTriangle },
  info: { label: '提示', className: 'text-sky-500', icon: Info },
}

/**
 * 校验结果面板。
 */
export function ValidationPanel({ onNavigate }: ValidationPanelProps) {
  const { issues, issueCounts, scenario, options, meta, reload } = useScenarioStore()

  const [levelFilter, setLevelFilter] = useState<IssueLevel[]>(['error', 'warning'])
  const [collectionFilter, setCollectionFilter] = useState<CollectionKey | 'all'>('all')
  const [keyword, setKeyword] = useState('')

  /** id -> 名称 索引，用于展示更友好的定位信息（势力/军团会由君主/军团长推导） */
  const nameIndex = useMemo(() => {
    const map = new Map<string, string>()
    if (!scenario) return map
    const names = buildScenarioNameMaps(scenario)
    for (const key of COLLECTION_ORDER) {
      for (const [id, name] of names[key]) map.set(`${key}:${id}`, name)
    }
    return map
  }, [scenario])

  const filtered = useMemo(() => {
    const kw = keyword.trim().toLowerCase()
    return issues.filter((i) => {
      if (!levelFilter.includes(i.level)) return false
      if (collectionFilter !== 'all' && i.collection !== collectionFilter) return false
      if (kw) {
        const name = i.entityId !== null ? nameIndex.get(`${i.collection}:${i.entityId}`) ?? '' : ''
        const hay = `${name} ${i.field} ${i.message} ${i.entityId ?? ''}`.toLowerCase()
        if (!hay.includes(kw)) return false
      }
      return true
    })
  }, [collectionFilter, issues, keyword, levelFilter, nameIndex])

  const vlist = useVirtualList({ count: filtered.length, itemHeight: ISSUE_ROW_HEIGHT, overscan: 8 })

  const toggleLevel = useCallback((level: IssueLevel) => {
    setLevelFilter((prev) => (prev.includes(level) ? prev.filter((l) => l !== level) : [...prev, level]))
  }, [])

  /** 每个集合的问题数 */
  const perCollection = useMemo(() => {
    const map = new Map<CollectionKey | 'root', number>()
    for (const i of issues) {
      if (!levelFilter.includes(i.level)) continue
      map.set(i.collection, (map.get(i.collection) ?? 0) + 1)
    }
    return map
  }, [issues, levelFilter])

  const total = issueCounts.error + issueCounts.warning + issueCounts.info

  return (
    <div className="flex h-full min-h-0 flex-col">
      {/* 概览 */}
      <div className="shrink-0 border-b border-border px-3 py-3">
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2">
            {total === 0 ? (
              <>
                <CheckCircle2 className="h-5 w-5 text-emerald-500" />
                <div>
                  <p className="text-[13px] font-medium">未发现问题</p>
                  <p className="text-[11px] text-muted-foreground">所有字段的取值、引用与一致性检查均已通过</p>
                </div>
              </>
            ) : (
              <>
                <AlertTriangle className={cn('h-5 w-5', issueCounts.error > 0 ? 'text-destructive' : 'text-amber-500')} />
                <div>
                  <p className="text-[13px] font-medium">
                    共发现 <span className="tabular">{total}</span> 个问题
                  </p>
                  <p className="text-[11px] text-muted-foreground">
                    错误 {issueCounts.error} · 警告 {issueCounts.warning} · 提示 {issueCounts.info}
                  </p>
                </div>
              </>
            )}
          </div>
          <div className="ml-auto flex items-center gap-2">
            <Button variant="outline" size="sm" className="h-8 gap-1.5" onClick={() => void reload()}>
              <RefreshCw className="h-3.5 w-3.5" />
              重新加载剧本
            </Button>
          </div>
        </div>

        {total > 0 && (
          <div className="mt-2.5 space-y-1">
            <Progress
              value={
                (issueCounts.error / Math.max(1, issueCounts.error + issueCounts.warning + issueCounts.info)) * 100
              }
              className="h-1.5"
            />
            <div className="flex flex-wrap gap-3 text-[10px] text-muted-foreground">
              <span>错误（阻塞性）：{issueCounts.error}</span>
              <span>警告（可能不符合设计意图）：{issueCounts.warning}</span>
              <span>提示（可忽略）：{issueCounts.info}</span>
            </div>
          </div>
        )}

        <Separator className="my-2.5" />

        {/* 筛选 */}
        <div className="flex flex-wrap items-center gap-2">
          {(Object.keys(LEVEL_STYLE) as IssueLevel[]).map((level) => {
            const style = LEVEL_STYLE[level]
            const active = levelFilter.includes(level)
            const count = issueCounts[level]
            return (
              <button
                key={level}
                type="button"
                onClick={() => toggleLevel(level)}
                className={cn(
                  'flex items-center gap-1.5 rounded-md border px-2.5 py-1 text-[11px] transition-colors',
                  active ? 'border-primary bg-primary/10' : 'border-border text-muted-foreground hover:text-foreground'
                )}
              >
                <style.icon className={cn('h-3.5 w-3.5', style.className)} />
                {style.label}
                <span className="tabular">{count}</span>
              </button>
            )
          })}

          <div className="mx-1 h-4 w-px bg-border" />

          <button
            type="button"
            onClick={() => setCollectionFilter('all')}
            className={cn(
              'rounded-md border px-2.5 py-1 text-[11px] transition-colors',
              collectionFilter === 'all'
                ? 'border-primary bg-primary/10'
                : 'border-border text-muted-foreground hover:text-foreground'
            )}
          >
            全部集合
          </button>
          {COLLECTION_ORDER.map((key) => {
            const count = perCollection.get(key) ?? 0
            return (
              <button
                key={key}
                type="button"
                onClick={() => setCollectionFilter(key)}
                className={cn(
                  'rounded-md border px-2.5 py-1 text-[11px] transition-colors',
                  collectionFilter === key
                    ? 'border-primary bg-primary/10'
                    : 'border-border text-muted-foreground hover:text-foreground'
                )}
              >
                {COLLECTION_META[key].label}
                {count > 0 && <span className="ml-1 tabular">{count}</span>}
              </button>
            )
          })}

          <div className="relative ml-auto min-w-[180px]">
            <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="h-8 pl-7"
              placeholder="搜索武将名 / 字段 / 问题描述…"
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
            />
          </div>
        </div>
      </div>

      {/* 列表 */}
      <div ref={vlist.containerRef} onScroll={vlist.onScroll} className="scroll-stable min-h-0 flex-1 overflow-y-auto">
        {filtered.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center gap-2 py-16 text-muted-foreground">
            <CheckCircle2 className="h-8 w-8 opacity-40" />
            <p className="text-[13px]">当前筛选条件下没有问题</p>
          </div>
        ) : (
          <div style={{ height: vlist.totalHeight, position: 'relative' }}>
            {vlist.virtualItems.map((vi) => {
              const item = filtered[vi.index]
              if (!item) return null
              const style = LEVEL_STYLE[item.level]
              const name =
                item.entityId !== null ? nameIndex.get(`${item.collection}:${item.entityId}`) ?? '' : ''
              const collectionLabel =
                item.collection === 'root' ? '剧本' : COLLECTION_META[item.collection as CollectionKey].label
              return (
                <button
                  key={item.key + vi.index}
                  type="button"
                  className="absolute left-0 right-0 flex items-start gap-2.5 border-b border-border/50 px-3 py-2 text-left transition-colors hover:bg-accent/60"
                  style={{ top: vi.offset, height: ISSUE_ROW_HEIGHT }}
                  onClick={() => {
                    if (item.collection !== 'root' && item.entityId !== null) {
                      onNavigate(item.collection as CollectionKey, item.entityId)
                    }
                  }}
                >
                  <style.icon className={cn('mt-0.5 h-4 w-4 shrink-0', style.className)} />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-1.5 text-[11px] text-muted-foreground">
                      <Badge variant="outline" className="h-4 px-1 text-[10px]">
                        {collectionLabel}
                      </Badge>
                      {item.entityId !== null && (
                        <span>
                          {name || '（未命名）'}
                          <span className="ml-1 tabular">#{item.entityId}</span>
                        </span>
                      )}
                      {item.field && <span className="font-mono">{item.field}</span>}
                    </div>
                    <p className={cn('mt-0.5 truncate text-[12px]', item.level === 'error' && 'text-destructive')}>
                      {item.message}
                    </p>
                  </div>
                  {item.collection !== 'root' && item.entityId !== null && (
                    <span className="shrink-0 self-center text-[10px] text-muted-foreground">点击跳转 ›</span>
                  )}
                </button>
              )
            })}
          </div>
        )}
      </div>

      <div className="flex shrink-0 items-center gap-3 border-t border-border px-3 py-1 text-[11px] text-muted-foreground">
        <span>
          显示 {filtered.length} / {issues.length} 条问题
        </span>
        {options && <span>校验依据：{options.generatedAt.slice(0, 19).replace('T', ' ')} 加载的公共数据表</span>}
        {meta && <span>剧本修订号 {meta.revision}</span>}
      </div>
    </div>
  )
}
