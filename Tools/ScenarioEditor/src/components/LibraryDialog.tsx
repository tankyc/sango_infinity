/**
 * 武将库对话框
 *
 * 两种用途：
 * - 导入模式（targetId 为空）：从武将库网站挑选武将，追加进当前剧本的 personSet；
 * - 回填模式（targetId 指定）：用武将库中的同名武将数据，按分组回填到剧本中的某个武将。
 *
 * 数据来源优先级由后端决定：武将库网站接口 -> 网站本地数据文件 -> 游戏内置武将库。
 */
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Check, Database, Download, RefreshCw, Search, TriangleAlert, Wand2, X } from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import type { Entity, LibraryPerson, Options } from '@/lib/types'
import { getAttrChangeId, setAttr, setAbilityLevel } from '@/lib/fields'
import { useScenarioStore } from '@/state/store'
import { useVirtualList } from '@/hooks/useVirtualList'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Checkbox } from '@/components/ui/checkbox'
import { Separator } from '@/components/ui/separator'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Alert, AlertDescription } from '@/components/ui/alert'

/** 回填分组定义 */
const FILL_GROUPS: { key: string; label: string; fields: string[] }[] = [
  { key: 'name', label: '姓名 / 字', fields: ['Name', 'familyName', 'giveName', 'nickName'] },
  { key: 'attrs', label: '五维能力', fields: ['command', 'strength', 'intelligence', 'politics', 'glamour'] },
  {
    key: 'abilities',
    label: '兵种适性',
    fields: ['spearLv', 'halberdLv', 'crossbowLv', 'rideLv', 'waterLv', 'machineLv'],
  },
  {
    key: 'basic',
    label: '性别 / 生卒 / 相性 / 身分',
    fields: [
      'sex',
      'yearBorn',
      'yearDead',
      'appearance',
      'compatibility',
      'state',
      'personality',
      'argumentation',
      'type',
    ],
  },
  { key: 'image', label: '头像 / 立绘', fields: ['headIconID', 'imageID'] },
  {
    key: 'relations',
    label: '人际关系',
    fields: ['LikePersonList', 'HatePersonList', 'SpouseList', 'Father', 'Mother', 'Brother'],
  },
  { key: 'features', label: '特技', fields: ['FeatureList'] },
  { key: 'level', label: '等级 / 官职 / 忠诚', fields: ['Level', 'Official', 'loyalty'] },
]

/** 行高 */
const LIB_ROW_HEIGHT = 52

interface LibraryDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** 回填模式的目标武将 Id；为空表示导入模式 */
  targetId?: number | null
}

/**
 * 把武将库记录转换为可写入剧本的字段补丁。
 *
 * @param record 武将库记录
 * @param target 目标武将（用于保留原数组结构）
 * @param groups 需要回填的分组键
 * @param options 公共数据表
 * @param existingPersonIds 剧本中已存在的武将 Id（用于过滤无效引用）
 * @returns 字段补丁
 */
function buildPatch(
  record: LibraryPerson,
  target: Entity,
  groups: string[],
  options: Options | null,
  existingPersonIds: Set<number>
): Record<string, unknown> {
  const patch: Record<string, unknown> = {}
  const enabled = new Set(groups)
  const has = (key: string) => enabled.has(key)
  const defined = (v: unknown) => v !== undefined && v !== null

  if (has('name')) {
    if (defined(record.Name)) patch.Name = record.Name
    if (defined(record.familyName)) patch.familyName = record.familyName
    if (defined(record.giveName)) patch.giveName = record.giveName
    if (defined(record.nickName)) patch.nickName = record.nickName
  }

  if (has('attrs')) {
    for (const key of ['command', 'strength', 'intelligence', 'politics', 'glamour'] as const) {
      const value = record[key]
      if (typeof value === 'number') {
        patch[key] = setAttr(target[key], value, getAttrChangeId(target[key]))
      }
    }
  }

  if (has('abilities')) {
    for (const key of ['spearLv', 'halberdLv', 'crossbowLv', 'rideLv', 'waterLv', 'machineLv'] as const) {
      const value = record[key]
      if (typeof value === 'number') patch[key] = setAbilityLevel(target[key], value)
    }
  }

  if (has('basic')) {
    for (const key of [
      'sex',
      'yearBorn',
      'yearDead',
      'appearance',
      'compatibility',
      'state',
      'personality',
      'argumentation',
      'type',
    ]) {
      const value = record[key]
      if (defined(value) && typeof value === 'number') patch[key] = value
    }
  }

  if (has('image')) {
    if (defined(record.headIconID) && record.headIconID !== 0) patch.headIconID = record.headIconID
    if (defined(record.imageID) && record.imageID !== 0) patch.imageID = record.imageID
  }

  if (has('level')) {
    for (const key of ['Level', 'Official', 'loyalty']) {
      const value = record[key]
      if (defined(value) && typeof value === 'number') patch[key] = value
    }
  }

  if (has('features') && Array.isArray(record.FeatureList) && record.FeatureList.length > 0) {
    const valid = new Set((options?.features ?? []).map((f) => f.id))
    const list = record.FeatureList.filter((id) => valid.size === 0 || valid.has(id))
    if (list.length > 0) patch.FeatureList = list
  }

  if (has('relations')) {
    for (const key of ['LikePersonList', 'HatePersonList', 'SpouseList'] as const) {
      const value = record[key]
      if (Array.isArray(value)) {
        const list = value.filter((id) => existingPersonIds.has(id))
        if (list.length > 0) patch[key] = list
      }
    }
    for (const key of ['Father', 'Mother', 'Brother'] as const) {
      const value = record[key]
      if (typeof value === 'number' && existingPersonIds.has(value)) patch[key] = value
    }
  }

  return patch
}

/**
 * 武将库对话框。
 */
export function LibraryDialog({ open, onOpenChange, targetId = null }: LibraryDialogProps) {
  const store = useScenarioStore()
  const {
    library,
    librarySource,
    libraryLibs,
    libraryLoading,
    libraryError,
    loadLibrary,
    scenario,
    options,
    patchEntity,
    addEntities,
  } = store

  const [keyword, setKeyword] = useState('')
  /** 库筛选：'all' 或某个分库 key（base / custom） */
  const [libFilter, setLibFilter] = useState('all')
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set())
  const [activeId, setActiveId] = useState<number | null>(null)
  const [groups, setGroups] = useState<string[]>(FILL_GROUPS.map((g) => g.key))

  const isFillMode = targetId !== null

  const targetEntity = useMemo(() => {
    if (targetId === null || !scenario) return null
    return ((scenario.personSet ?? {}) as Record<string, Entity>)[String(targetId)] ?? null
  }, [scenario, targetId])

  const existingPersonIds = useMemo(() => {
    const set = new Set<number>()
    for (const p of Object.values((scenario?.personSet ?? {}) as Record<string, Entity>)) {
      set.add(Number(p.Id))
    }
    return set
  }, [scenario])

  useEffect(() => {
    if (open) {
      setKeyword('')
      setLibFilter('all')
      setSelectedIds(new Set())
      setActiveId(null)
      setGroups(FILL_GROUPS.map((g) => g.key))
      if (library.length === 0 && !libraryLoading) void loadLibrary()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const filtered = useMemo(() => {
    // 先按库筛选，再按关键字；两步都做才能让「自建武将」这个高频诉求一键可见
    const byLib = libFilter === 'all' ? library : library.filter((p) => p.lib === libFilter)
    const kw = keyword.trim().toLowerCase()
    if (!kw) return byLib
    const terms = kw.split(/\s+/)
    return byLib.filter((p) => {
      const hay = `${p.Id} ${p.Name} ${p.nickName} ${p.familyName} ${p.giveName}`.toLowerCase()
      return terms.every((t) => hay.includes(t))
    })
  }, [keyword, library, libFilter])

  const vlist = useVirtualList({
    count: filtered.length,
    itemHeight: LIB_ROW_HEIGHT,
    overscan: 6,
  })

  const activeRecord = useMemo(
    () => (activeId === null ? null : library.find((p) => p.Id === activeId) ?? null),
    [activeId, library]
  )

  const previewPatch = useMemo(() => {
    if (!activeRecord || !targetEntity) return null
    return buildPatch(activeRecord, targetEntity, groups, options, existingPersonIds)
  }, [activeRecord, existingPersonIds, groups, options, targetEntity])

  const handleFill = useCallback(() => {
    if (!targetEntity || !previewPatch) return
    const keys = Object.keys(previewPatch)
    if (keys.length === 0) {
      toast.warning('所选分组中没有可写入的字段')
      return
    }
    patchEntity('personSet', Number(targetEntity.Id), previewPatch)
    toast.success(`已用武将库数据回填 ${keys.length} 个字段（${activeRecord?.Name ?? ''}）`)
    onOpenChange(false)
  }, [activeRecord, onOpenChange, patchEntity, previewPatch, targetEntity])

  const handleImport = useCallback(() => {
    if (selectedIds.size === 0) {
      toast.warning('请先选择要导入的武将')
      return
    }
    const records = library.filter((p) => selectedIds.has(p.Id))
    const alreadyInScenario = records.filter((r) => existingPersonIds.has(r.Id))
    const entities: Entity[] = records.map((r) => {
      const used = existingPersonIds.has(r.Id)
      const base: Entity = {
        Id: used ? 0 : r.Id,
        Name: r.Name,
        familyName: r.familyName || r.Name,
        giveName: r.giveName || '',
        nickName: r.nickName || '',
        BelongForce: 0,
        BelongCorps: 0,
        BelongCity: 0,
        CurrentCity: 0,
        state: typeof r.state === 'number' ? r.state : 8,
        sex: typeof r.sex === 'number' ? r.sex : 0,
        compatibility: typeof r.compatibility === 'number' ? r.compatibility : 0,
        personality: typeof r.personality === 'number' ? r.personality : 3,
        argumentation: typeof r.argumentation === 'number' ? r.argumentation : 3,
        type: typeof r.type === 'number' ? r.type : 0,
        appearance: typeof r.appearance === 'number' ? r.appearance : 200,
        yearBorn: typeof r.yearBorn === 'number' ? r.yearBorn : 160,
        yearDead: typeof r.yearDead === 'number' ? r.yearDead : 220,
        oldAge: 255,
        old_age: 255,
        loyalty: typeof r.loyalty === 'number' ? r.loyalty : 0,
        merit: 0,
        stamina: 100,
        injury: 0,
        Level: typeof r.Level === 'number' ? r.Level : 0,
        Official: typeof r.Official === 'number' ? r.Official : 0,
        headIconID: r.headIconID ?? 0,
        imageID: r.imageID ?? 0,
        birthplace: 1,
        command: [r.command, 5],
        strength: [r.strength, 5],
        intelligence: [r.intelligence, 5],
        politics: [r.politics, 5],
        glamour: [r.glamour, 5],
        spearLv: [r.spearLv],
        halberdLv: [r.halberdLv],
        crossbowLv: [r.crossbowLv],
        rideLv: [r.rideLv],
        waterLv: [r.waterLv],
        machineLv: [r.machineLv],
        horse: -1,
        left_weapon: -1,
        right_weapon: -1,
        ambition: 2,
        tone: 0,
        voice: 1,
        wadai: 0,
        wajutsu: [],
        wordTac: [0, 0],
        generation: 1,
        promotion: 1,
        local_affiliation: 0,
        hanLoyalty: 1,
        skeleton: 0,
        death_type: 0,
        strategic_tendency: 2,
        body: [0, 0, 0, 0, 0, 0, -1, -1],
      }
      base.ketsuen = used ? 0 : r.Id
      if (Array.isArray(r.FeatureList) && r.FeatureList.length > 0) base.FeatureList = [...r.FeatureList]
      return base
    })
    // 保留库中原始 Id（若剧本中未占用），保证引用关系稳定
    const ids = addEntities(
      'personSet',
      entities.map((e) => (Number(e.Id) > 0 ? e : { ...e, Id: 0 }))
    )
    toast.success(
      `已导入 ${ids.length} 名武将${alreadyInScenario.length > 0 ? `（其中 ${alreadyInScenario.length} 名因 Id 已占用而重新分配）` : ''}`
    )
    onOpenChange(false)
  }, [addEntities, existingPersonIds, library, onOpenChange, selectedIds])

  const toggleGroup = useCallback((key: string) => {
    setGroups((prev) => (prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key]))
  }, [])

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/*
        必须显式覆盖 sm:max-w-lg：
        shadcn 的 DialogContent 基础样式里带 `sm:max-w-lg`（512px），而 `w-[…]` 设的是 width、
        与 max-width 不是同一个属性，twMerge 不会合并它们——结果在 ≥640px 时
        max-width 反把 width 截住，对话框只剩 512px。
        这里右侧详情栏固定占 340px，若对话框只有 512px，左侧列表就只剩 150 出头，
        武将名会被挤成一个字。宽屏下要留出足够宽度给列表。
      */}
      <DialogContent className="flex h-[92vh] w-[min(96vw,1060px)] flex-col gap-0 overflow-hidden p-0 sm:max-w-[1060px]">
        <DialogHeader className="shrink-0 border-b border-border px-5 py-3">
          <DialogTitle className="flex items-center gap-2 text-[16px]">
            <Database className="h-4 w-4 text-primary" />
            {isFillMode ? '从武将库回填' : '从武将库导入武将'}
          </DialogTitle>
          <DialogDescription className="flex flex-wrap items-center gap-2 text-[12px]">
            <span>数据来源：{librarySource || '加载中…'}</span>
            <Badge variant="secondary" className="tabular">
              {library.length} 名
            </Badge>
            {/* 分库筛选：自建武将是高频诉求，给一个一键入口而不是让人去搜名字 */}
            {libraryLibs.length > 1 && (
              <span className="flex overflow-hidden rounded-md border border-border">
                {[{ key: 'all', label: '全部', count: library.length }, ...libraryLibs].map((l, i) => (
                  <button
                    key={l.key}
                    type="button"
                    onClick={() => setLibFilter(l.key)}
                    title={'origin' in l ? l.origin || '无可用数据源' : '不区分库'}
                    className={cn(
                      'h-6 px-2 text-[11px] transition-colors',
                      i > 0 && 'border-l border-border',
                      libFilter === l.key
                        ? 'bg-primary/15 text-foreground'
                        : 'text-muted-foreground hover:bg-accent'
                    )}
                  >
                    {l.label}
                    <span className="ml-1 tabular opacity-70">{l.count}</span>
                  </button>
                ))}
              </span>
            )}
            {isFillMode && targetEntity && (
              <span>
                目标武将：
                <span className="font-medium text-foreground">{String(targetEntity.Name ?? '')}</span>（Id{' '}
                {String(targetEntity.Id)}）
              </span>
            )}
            <Button
              variant="ghost"
              size="sm"
              className="h-6 gap-1 px-1.5 text-[11px]"
              onClick={() => void loadLibrary(true)}
              disabled={libraryLoading}
            >
              <RefreshCw className={cn('h-3 w-3', libraryLoading && 'animate-spin')} />
              重新拉取
            </Button>
          </DialogDescription>
        </DialogHeader>

        {libraryError && (
          <div className="shrink-0 px-5 pt-3">
            <Alert variant="destructive" className="py-2">
              <TriangleAlert className="h-4 w-4" />
              <AlertDescription className="text-[11px]">
                {libraryError}
                <br />
                已自动降级到本地武将库文件，功能不受影响。
              </AlertDescription>
            </Alert>
          </div>
        )}

        <div className="flex min-h-0 flex-1 flex-col md:flex-row">
          {/* 左侧：列表 */}
          <div className="flex min-h-0 flex-1 flex-col border-b border-border md:border-b-0 md:border-r">
            <div className="shrink-0 px-3 py-2">
              <div className="relative">
                <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
                <Input
                  className="h-8 pl-7"
                  placeholder="搜索武将姓名或 ID…"
                  value={keyword}
                  onChange={(e) => setKeyword(e.target.value)}
                />
                {keyword && (
                  <button
                    type="button"
                    className="absolute right-1.5 top-1/2 -translate-y-1/2 rounded p-0.5 text-muted-foreground hover:text-foreground"
                    onClick={() => setKeyword('')}
                    aria-label="清空"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                )}
              </div>
              {!isFillMode && (
                <div className="mt-1.5 flex items-center gap-2 text-[11px] text-muted-foreground">
                  <span>
                    已选 <span className="tabular text-foreground">{selectedIds.size}</span> 名
                  </span>
                  <button
                    type="button"
                    className="hover:text-foreground"
                    onClick={() => setSelectedIds(new Set(filtered.map((p) => p.Id)))}
                  >
                    全选结果
                  </button>
                  <button type="button" className="hover:text-foreground" onClick={() => setSelectedIds(new Set())}>
                    清空
                  </button>
                </div>
              )}
            </div>
            <div ref={vlist.containerRef} onScroll={vlist.onScroll} className="min-h-0 flex-1 overflow-y-auto">
              <div style={{ height: vlist.totalHeight, position: 'relative' }}>
                {vlist.virtualItems.map((vi) => {
                  const p = filtered[vi.index]
                  if (!p) return null
                  const isActive = activeId === p.Id
                  return (
                    <div
                      key={p.Id}
                      className={cn(
                        'absolute left-0 right-0 mx-2 flex cursor-pointer items-center gap-2 rounded-md border px-2 py-1.5 transition-colors',
                        isActive
                          ? 'border-primary bg-primary/10'
                          : 'border-transparent hover:border-border hover:bg-accent/50'
                      )}
                      style={{ top: vi.offset + 2, height: LIB_ROW_HEIGHT - 4 }}
                      onClick={() => setActiveId(p.Id)}
                    >
                      {!isFillMode && (
                        <span onClick={(e) => e.stopPropagation()}>
                          <Checkbox
                            checked={selectedIds.has(p.Id)}
                            onClick={(e) => {
                              e.preventDefault()
                              setSelectedIds((prev) => {
                                const next = new Set(prev)
                                if (next.has(p.Id)) next.delete(p.Id)
                                else next.add(p.Id)
                                return next
                              })
                            }}
                            aria-label={`选择 ${p.Name}`}
                          />
                        </span>
                      )}
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-1.5">
                          <span className="truncate text-[13px] font-medium">{p.Name}</span>
                          <span className="tabular text-[10px] text-muted-foreground">#{p.Id}</span>
                          {/* 自建武将的来源标记：筛选到「全部」时也能一眼区分 */}
                          {p.lib === 'custom' && (
                            <Badge
                              variant="outline"
                              className="h-4 border-primary/50 px-1 text-[10px] text-primary"
                              title="来自自建武将库"
                            >
                              自建
                            </Badge>
                          )}
                          {existingPersonIds.has(p.Id) && (
                            <Badge variant="outline" className="h-4 px-1 text-[10px] text-amber-500">
                              剧本已存在
                            </Badge>
                          )}
                        </div>
                        <div className="flex gap-2 text-[10px] text-muted-foreground">
                          <span className="tabular">统{p.command}</span>
                          <span className="tabular">武{p.strength}</span>
                          <span className="tabular">智{p.intelligence}</span>
                          <span className="tabular">政{p.politics}</span>
                          <span className="tabular">魅{p.glamour}</span>
                        </div>
                      </div>
                      {isActive && <Check className="h-3.5 w-3.5 shrink-0 text-primary" />}
                    </div>
                  )
                })}
              </div>
              {filtered.length === 0 && (
                <p className="py-10 text-center text-[12px] text-muted-foreground">
                  {libraryLoading ? '正在加载武将库…' : '没有匹配的武将'}
                </p>
              )}
            </div>
          </div>

          {/* 右侧：详情与操作。宽度固定，把剩余空间全部让给左侧列表 */}
          <div className="flex w-full shrink-0 flex-col md:w-[340px] md:max-w-[340px]">
            {isFillMode ? (
              <div className="min-h-0 flex-1 overflow-y-auto p-3">
                <p className="text-[12px] font-medium">选择要回填的分组</p>
                <div className="mt-2 space-y-1">
                  {FILL_GROUPS.map((g) => (
                    <label
                      key={g.key}
                      className="flex cursor-pointer items-center gap-2 rounded border border-border px-2 py-1.5 text-[12px] transition-colors hover:bg-accent/50"
                    >
                      <Checkbox checked={groups.includes(g.key)} onCheckedChange={() => toggleGroup(g.key)} />
                      <span className="flex-1">{g.label}</span>
                      <span className="font-mono text-[10px] text-muted-foreground">{g.fields.length}</span>
                    </label>
                  ))}
                </div>

                <Separator className="my-3" />

                <p className="text-[12px] font-medium">将要写入的内容</p>
                {!activeRecord ? (
                  <p className="mt-2 text-[11px] text-muted-foreground">请在左侧选择一名武将</p>
                ) : !previewPatch || Object.keys(previewPatch).length === 0 ? (
                  <p className="mt-2 text-[11px] text-muted-foreground">
                    所选分组中没有可写入的字段（可能是库中缺该数据，或引用的武将不在本剧本中）
                  </p>
                ) : (
                  <div className="mt-2 space-y-1">
                    {Object.entries(previewPatch).map(([key, value]) => (
                      <div key={key} className="flex items-start gap-2 text-[11px]">
                        <span className="w-[92px] shrink-0 truncate font-mono text-muted-foreground">{key}</span>
                        <span className="min-w-0 flex-1 break-all font-mono">
                          {Array.isArray(value) ? value.join(',') : String(value)}
                        </span>
                      </div>
                    ))}
                  </div>
                )}

                <Alert className="mt-3 py-2">
                  <AlertDescription className="text-[11px]">
                    人际关系只会写入剧本中已存在的武将 Id，避免产生无效引用。
                  </AlertDescription>
                </Alert>
              </div>
            ) : (
              <div className="min-h-0 flex-1 overflow-y-auto p-3">
                <p className="text-[12px] font-medium">导入说明</p>
                <ul className="mt-2 space-y-1.5 text-[11px] leading-relaxed text-muted-foreground">
                  <li>
                    勾选左侧武将后点击「导入」。若库中 Id 在剧本中已被占用，将自动分配一个新的空闲 Id，避免覆盖现有武将。
                  </li>
                  <li>导入的武将默认归属势力 / 军团 / 都市为「无」，身分继承武将库设置，需要在剧本中另行分配。</li>
                  <li>五维与兵种适性会按剧本的数组编码写入（[基础值, 成长类型] / [等级]）。</li>
                  <li>导入后可正常使用撤销（Ctrl+Z）整批回退。</li>
                </ul>
                <Separator className="my-3" />
                <p className="text-[12px] font-medium">当前选择</p>
                {selectedIds.size === 0 ? (
                  <p className="mt-2 text-[11px] text-muted-foreground">尚未选择武将</p>
                ) : (
                  <div className="mt-2 flex flex-wrap gap-1">
                    {[...selectedIds].slice(0, 40).map((id) => {
                      const p = library.find((x) => x.Id === id)
                      return (
                        <Badge key={id} variant="secondary" className="text-[10px]">
                          {p?.Name ?? `#${id}`}
                        </Badge>
                      )
                    })}
                    {selectedIds.size > 40 && (
                      <Badge variant="outline" className="text-[10px]">
                        …共 {selectedIds.size} 名
                      </Badge>
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        <DialogFooter className="shrink-0 border-t border-border px-5 py-3">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            关闭
          </Button>
          {isFillMode ? (
            <Button
              onClick={handleFill}
              disabled={!previewPatch || Object.keys(previewPatch).length === 0}
              className="gap-1.5"
            >
              <Wand2 className="h-4 w-4" />
              回填 {previewPatch ? Object.keys(previewPatch).length : 0} 个字段
            </Button>
          ) : (
            <Button onClick={handleImport} disabled={selectedIds.size === 0} className="gap-1.5">
              <Download className="h-4 w-4" />
              导入 {selectedIds.size} 名武将
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
