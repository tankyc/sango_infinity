/**
 * 公共数据表编辑（大项）
 *
 * 覆盖 Build/Content/Data/Common 下的全部 27 个 json：
 * - 左侧按业务分组列出文件（含条目数与解析错误标记）；
 * - 右侧按文件结构自动切换「记录表 / 键值配置 / 值列表」三种视图；
 * - 文件含多个分区时（如 ai.json 的 Officials 与 ai）顶部提供分区切换；
 * - 保存采用「原文 + 最小差异」策略，含注释的 JSONC 文件不会丢注释；
 * - 每次保存前自动备份，支持按文件回滚。
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react'
import {
  ArchiveRestore,
  Database,
  Download,
  FileDiff,
  FileJson,
  GitCompare,
  Info,
  Loader2,
  Redo2,
  RefreshCw,
  RotateCcw,
  Save,
  Search,
  Trash2,
  TriangleAlert,
  Undo2,
  UploadCloud,
} from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import type { CommonSection } from '@/lib/types'
import {
  COMMON_GROUPS,
  COMMON_HIDDEN_LABELS,
  COMMON_HIDDEN_NAMES,
  COMMON_META_BY_NAME,
  COMMON_SECTION_NOTES,
  commonFieldLabel,
  type CommonFileMeta,
} from '@/lib/commonMeta'
import { commonFieldRef, COMMON_ARRAY_ITEM_REFS } from '@/lib/commonMeta'
import { buildRefResolver } from '@/lib/refResolver'
import { isPlainObject } from '@/lib/jsoncEdit'
import { useScenarioStore } from '@/state/store'
import { useCommonStore } from '@/state/commonStore'
import { useIsMobile } from '@/hooks/use-mobile'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  commonFileDownloadUrl,
  commonFilePatchUrl,
  commonPatchUrl,
  deleteBaseline,
  downloadPatch,
  fetchBaselines,
  uploadBaseline,
  type BaselineItem,
} from '@/lib/api'

/** 非 Button 元素（<a> / <label>）借用按钮外观时的类名 */
const buttonLikeCls =
  'inline-flex items-center rounded-md border border-input bg-background px-3 text-[13px] shadow-xs transition-colors hover:bg-accent hover:text-accent-foreground'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { CommonEditContext, JsonValueEditor, type CommonEditContextValue } from './JsonValueEditor'
import { CommonRecordTable } from './CommonRecordTable'

/** 字节数格式化 */
function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`
}

/**
 * 公共数据表编辑主界面。
 */
export function CommonEditor() {
  const scenarioStore = useScenarioStore()
  const store = useCommonStore()
  const isMobile = useIsMobile()

  const [keyword, setKeyword] = useState('')
  const [sectionIndex, setSectionIndex] = useState(0)
  const [backupOpen, setBackupOpen] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)
  const [restoreDir, setRestoreDir] = useState<string | null>(null)

  // 基准版与差量补丁
  const [baselines, setBaselines] = useState<BaselineItem[]>([])
  const [baselineOpen, setBaselineOpen] = useState(false)
  const [baselineLoading, setBaselineLoading] = useState(false)
  const [patchBusy, setPatchBusy] = useState(false)

  const { files, listStatus, listError, doc, docStatus, docError, value, dirty, changeCount, saving } = store
  const fileMeta: CommonFileMeta | undefined = doc ? COMMON_META_BY_NAME[doc.name] : undefined
  /** 是否具备管理员权限（上传基准版需要） */
  const authCanAdmin = Boolean(scenarioStore.auth?.canAdmin)

  const resolver = useMemo(
    () => buildRefResolver(scenarioStore.scenario, scenarioStore.options),
    [scenarioStore.scenario, scenarioStore.options]
  )

  /* ---------------- 文件分组 ---------------- */

  /**
   * 去掉已隐藏的文件。
   *
   * 服务端清单已排除，这里再过滤一次，避免服务端未重启时旧缓存把内置武将库带出来。
   */
  const visibleFiles = useMemo(() => files.filter((f) => !COMMON_HIDDEN_NAMES.has(f.name)), [files])

  const grouped = useMemo(() => {
    const kw = keyword.trim().toLowerCase()
    return COMMON_GROUPS.map((group) => {
      const items = visibleFiles.filter((f) => {
        const meta = COMMON_META_BY_NAME[f.name]
        const g = meta?.group ?? '未分组'
        if (g !== group) return false
        if (!kw) return true
        return (
          f.name.toLowerCase().includes(kw) ||
          (meta?.label ?? '').toLowerCase().includes(kw) ||
          (meta?.desc ?? '').toLowerCase().includes(kw)
        )
      })
      return { group, items }
    }).filter((g) => g.items.length > 0)
  }, [visibleFiles, keyword])

  /** 未在元数据中登记的文件（新增文件时仍可编辑） */
  const unlisted = useMemo(
    () =>
      visibleFiles.filter(
        (f) => !COMMON_META_BY_NAME[f.name] && (!keyword || f.name.toLowerCase().includes(keyword.toLowerCase()))
      ),
    [visibleFiles, keyword]
  )

  /* ---------------- 分区 ---------------- */

  const sections = doc?.summary.sections ?? []
  const section: CommonSection | null = sections[sectionIndex] ?? sections[0] ?? null

  useEffect(() => {
    setSectionIndex(0)
  }, [doc?.name])

  /* ---------------- 编辑上下文 ---------------- */

  const editContext = useMemo<CommonEditContextValue | null>(
    () =>
      doc
        ? {
            fileName: doc.name,
            fileMeta,
            resolver,
            setValue: store.setValue,
            removeValue: store.removeValue,
            pushValue: store.pushValue,
          }
        : null,
    [doc, fileMeta, resolver, store]
  )

  /* ---------------- 基准版与差量补丁 ---------------- */

  const refreshBaselines = useCallback(async () => {
    setBaselineLoading(true)
    try {
      setBaselines(await fetchBaselines())
    } catch (e) {
      toast.error(`读取基准清单失败：${(e as Error).message}`)
    } finally {
      setBaselineLoading(false)
    }
  }, [])

  /** 基准状态汇总：有基准的文件数、其中有改动的数量 */
  const baselineStats = useMemo(
    () => ({
      total: baselines.length,
      changed: baselines.filter((b) => !b.upToDate).length,
    }),
    [baselines],
  )

  /**
   * 导出差量补丁。
   *
   * @param name 指定文件名则导出单个文件的差量；传 null 导出整个 Common 目录的差量
   */
  const handlePatch = useCallback(
    async (name: string | null) => {
      setPatchBusy(true)
      try {
        const url = name ? commonFilePatchUrl(name) : commonPatchUrl()
        const fileName = name ? name.replace(/\.json$/i, '.patch.json') : 'common.patch.json'
        const result = await downloadPatch(url, fileName)
        if (result.kind === 'saved') {
          toast.success(name ? `已导出 ${fileName}` : '已导出全部差量补丁', {
            description: '补丁只包含改动过的字段，未改动的字段不会出现。',
          })
        } else if (result.kind === 'empty') {
          toast.info(name ? `「${name}」与基准版一致，没有改动` : '所有文件都与基准版一致，没有改动')
        } else {
          toast.error(result.message || '导出补丁失败')
        }
      } finally {
        setPatchBusy(false)
      }
    },
    [],
  )

  /** 把当前工作区的全部公共数据登记为基准（管理员） */
  const handleSnapshotAll = useCallback(async () => {
    if (!authCanAdmin) return
    if (
      !window.confirm(
        `将把当前工作区的 ${files.length} 个公共数据表全部登记为基准版（覆盖已有基准）。\n基准是全体用户计算差量的参照，请确认当前内容就是「官方版」。`,
      )
    ) {
      return
    }
    setBaselineLoading(true)
    try {
      const r = await uploadBaseline()
      toast.success(`已登记 ${r.count} 个基准版`)
      await refreshBaselines()
    } catch (e) {
      toast.error(`登记基准失败：${(e as Error).message}`)
    } finally {
      setBaselineLoading(false)
    }
  }, [authCanAdmin, files.length, refreshBaselines])

  /** 把单个文件登记为基准（管理员） */
  const handleSnapshotOne = useCallback(
    async (name: string) => {
      if (!authCanAdmin) return
      setBaselineLoading(true)
      try {
        await uploadBaseline([name])
        toast.success(`已把「${name}」登记为基准版`)
        await refreshBaselines()
      } catch (e) {
        toast.error(`登记基准失败：${(e as Error).message}`)
      } finally {
        setBaselineLoading(false)
      }
    },
    [authCanAdmin, refreshBaselines],
  )

  /** 删除基准（管理员） */
  const handleDeleteBaseline = useCallback(
    async (name: string) => {
      if (!authCanAdmin) return
      if (!window.confirm(`删除「${name}」的基准版？删除后该文件无法再计算差量。`)) return
      setBaselineLoading(true)
      try {
        await deleteBaseline(name)
        toast.success(`已删除「${name}」的基准版`)
        await refreshBaselines()
      } catch (e) {
        toast.error((e as Error).message)
      } finally {
        setBaselineLoading(false)
      }
    },
    [authCanAdmin, refreshBaselines],
  )

  useEffect(() => {
    void refreshBaselines()
  }, [refreshBaselines])

  /* ---------------- 保存 ---------------- */

  const handleSave = useCallback(async () => {
    try {
      await store.save()
      toast.success(`已保存 ${doc?.name}（已生成备份）`)
    } catch (e) {
      toast.error((e as Error).message, { duration: 8000 })
    }
  }, [doc?.name, store])

  const handleRestore = useCallback(
    async (dir: string) => {
      try {
        await store.restore(dir)
        toast.success('已从备份恢复该数据表')
      } catch (e) {
        toast.error((e as Error).message)
      }
      setRestoreDir(null)
      setBackupOpen(false)
    },
    [store]
  )

  /* ---------------- 快捷键 ---------------- */

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null
      const editable =
        target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)
      const mod = e.ctrlKey || e.metaKey
      if (mod && e.key.toLowerCase() === 's' && doc) {
        e.preventDefault()
        void handleSave()
        return
      }
      if (!editable && mod && e.key.toLowerCase() === 'z') {
        e.preventDefault()
        if (e.shiftKey) store.redo()
        else store.undo()
        return
      }
      if (!editable && mod && e.key.toLowerCase() === 'y') {
        e.preventDefault()
        store.redo()
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [doc, handleSave, store])

  /* ---------------- 渲染 ---------------- */

  /** 当前分区的可编辑根路径 */
  const sectionPath = useMemo(() => (section?.key == null ? [] : [section.key]), [section])

  /** 分区内容 */
  const renderSection = () => {
    if (!doc || !section) {
      return (
        <div className="flex h-full flex-col items-center justify-center gap-2 text-muted-foreground">
          <Database className="h-8 w-8 opacity-40" />
          <p className="text-[13px]">请从左侧选择一个公共数据表</p>
        </div>
      )
    }
    if (section.kind === 'records') {
      return <CommonRecordTable section={section} fileMeta={fileMeta} />
    }

    // config / values / value：交给通用 JSON 编辑器
    let body: unknown = value
    for (const seg of sectionPath) {
      body = Array.isArray(body) ? body[Number(seg)] : isPlainObject(body) ? body[String(seg)] : undefined
    }

    if (section.kind === 'config' && section.keys && section.keys.length > 0 && isPlainObject(body)) {
      return (
        <div className="space-y-2">
          {section.keys.map((k) => (
            <JsonValueEditor
              key={k}
              label={commonFieldLabel(k, fileMeta)}
              value={body[k]}
              path={[...sectionPath, k]}
              depth={1}
              refSpec={commonFieldRef(doc.name, k)}
              itemRef={COMMON_ARRAY_ITEM_REFS[doc.name]?.[k] ?? null}
              newItem={(fileMeta?.template as Record<string, unknown> | undefined)?.[k]}
            />
          ))}
        </div>
      )
    }

    return (
      <JsonValueEditor
        label={section.key ?? '根节点'}
        value={body}
        path={sectionPath}
        depth={0}
        removable={false}
        newItem={fileMeta?.template}
      />
    )
  }

  return (
    <div className="flex h-full min-h-0">
      {/* 左侧文件列表（桌面） */}
      {!isMobile && (
        <aside className="flex w-[268px] shrink-0 flex-col border-r border-border">
          <div className="shrink-0 space-y-2 border-b border-border px-3 py-2">
            <div className="relative">
              <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input
                className="h-8 pl-7"
                placeholder="搜索数据表…"
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
              />
            </div>
            <div className="flex items-center gap-2 text-[10px] text-muted-foreground">
              <span>
                共 <span className="tabular text-foreground">{visibleFiles.length}</span> 个文件
              </span>
              <button
                type="button"
                className="ml-auto hover:text-foreground"
                onClick={() => void store.loadList(true)}
              >
                刷新清单
              </button>
            </div>
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto px-2 py-2">
            {listStatus === 'loading' && files.length === 0 ? (
              <div className="space-y-2 p-2">
                {Array.from({ length: 8 }).map((_, i) => (
                  <Skeleton key={i} className="h-8 w-full" />
                ))}
              </div>
            ) : listStatus === 'error' ? (
              <Alert variant="destructive" className="m-2 py-2">
                <TriangleAlert className="h-4 w-4" />
                <AlertDescription className="text-[11px]">{listError}</AlertDescription>
              </Alert>
            ) : (
              <>
                {grouped.map(({ group, items }) => (
                  <div key={group} className="mb-2">
                    <p className="px-2 py-1 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                      {group}
                    </p>
                    <div className="space-y-0.5">
                      {items.map((f) => (
                        <FileButton
                          key={f.name}
                          file={f}
                          active={doc?.name === f.name}
                          onClick={() => void store.openFile(f.name)}
                        />
                      ))}
                    </div>
                  </div>
                ))}
                {unlisted.length > 0 && (
                  <div className="mb-2">
                    <p className="px-2 py-1 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                      未登记（可直接编辑）
                    </p>
                    <div className="space-y-0.5">
                      {unlisted.map((f) => (
                        <FileButton
                          key={f.name}
                          file={f}
                          active={doc?.name === f.name}
                          onClick={() => void store.openFile(f.name)}
                        />
                      ))}
                    </div>
                  </div>
                )}
                {grouped.length === 0 && unlisted.length === 0 && (
                  <p className="py-8 text-center text-[12px] text-muted-foreground">没有匹配的数据表</p>
                )}
              </>
            )}
          </div>
          {COMMON_HIDDEN_LABELS.length > 0 && (
            <p className="shrink-0 border-t border-border px-3 py-2 text-[10px] leading-relaxed text-muted-foreground">
              已隐藏 {COMMON_HIDDEN_LABELS.join('、')}
              <br />
              它有独立的维护入口（武将库网站 / 「从武将库导入」），不在公共数据里重复编辑。
            </p>
          )}
        </aside>
      )}

      {/* 右侧编辑区 */}
      <div className="flex min-w-0 flex-1 flex-col">
        {/* 工具条 */}
        <div className="shrink-0 border-b border-border px-3 py-2">
          <div className="flex flex-wrap items-center gap-2">
            {isMobile ? (
              <Select value={doc?.name ?? ''} onValueChange={(v) => void store.openFile(v)}>
                <SelectTrigger className="h-8 min-w-[180px] flex-1">
                  <SelectValue placeholder="选择数据表…" />
                </SelectTrigger>
                <SelectContent className="max-h-[320px]">
                  {COMMON_GROUPS.map((group) => (
                    <React.Fragment key={group}>
                      {visibleFiles
                        .filter((f) => (COMMON_META_BY_NAME[f.name]?.group ?? '未分组') === group)
                        .map((f) => (
                          <SelectItem key={f.name} value={f.name}>
                            {COMMON_META_BY_NAME[f.name]?.label ?? f.name}
                          </SelectItem>
                        ))}
                    </React.Fragment>
                  ))}
                </SelectContent>
              </Select>
            ) : (
              <div className="flex min-w-0 items-center gap-2">
                <FileJson className="h-4 w-4 shrink-0 text-primary" />
                <span className="truncate text-[13px] font-medium">
                  {fileMeta?.label ?? doc?.name ?? '未选择'}
                </span>
                <span className="truncate font-mono text-[10px] text-muted-foreground">{doc?.name}</span>
              </div>
            )}

            <div className="ml-auto flex flex-wrap items-center gap-1.5">
              {doc?.jsonc && (
                <Badge variant="outline" className="shrink-0 text-[10px] text-emerald-500" title="保存时保留注释与原有排版">
                  保留注释
                </Badge>
              )}
              {doc && (
                <Badge variant="outline" className="shrink-0 tabular text-[10px]">
                  {formatSize(doc.size)} · {doc.eol === '\r\n' ? 'CRLF' : 'LF'}
                </Badge>
              )}
              {doc?.parseError && (
                <Badge variant="destructive" className="shrink-0 text-[10px]">
                  解析异常
                </Badge>
              )}
              {dirty && (
                <Badge className="shrink-0 bg-amber-500 text-[10px] text-black">{changeCount} 处未保存</Badge>
              )}
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                disabled={!store.canUndo}
                title="撤销（Ctrl+Z）"
                onClick={store.undo}
              >
                <Undo2 className="h-4 w-4" />
              </Button>
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                disabled={!store.canRedo}
                title="重做（Ctrl+Y）"
                onClick={store.redo}
              >
                <Redo2 className="h-4 w-4" />
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="h-8 gap-1.5"
                disabled={!doc}
                title="从磁盘重新加载"
                onClick={() => void store.reload()}
              >
                <RefreshCw className="h-3.5 w-3.5" />
                <span className="hidden sm:inline">重载</span>
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="h-8 gap-1.5"
                disabled={!doc}
                onClick={() => {
                  setBackupOpen(true)
                  void store.refreshBackups(doc?.name)
                }}
              >
                <ArchiveRestore className="h-3.5 w-3.5" />
                <span className="hidden sm:inline">备份</span>
              </Button>

              {/* 下载当前文件的完整内容 */}
              <a
                href={doc ? commonFileDownloadUrl(doc.name) : '#'}
                download
                className={cn(
                  buttonLikeCls,
                  'h-8 gap-1.5',
                  (!doc || dirty) && 'pointer-events-none opacity-50'
                )}
                title={
                  dirty ? '有未保存的改动，请先保存再下载' : '下载当前数据表的完整内容（含注释与原始格式）'
                }
              >
                <Download className="h-3.5 w-3.5" />
                <span className="hidden md:inline">下载</span>
              </a>

              {/* 下载与基准版的差量：只含改动字段 */}
              <Button
                variant="outline"
                size="sm"
                className="h-8 gap-1.5"
                disabled={!doc || dirty || patchBusy}
                title={
                  dirty
                    ? '有未保存的改动，请先保存再导出补丁'
                    : '导出与基准版的差量补丁，只包含改动过的字段'
                }
                onClick={() => void handlePatch(doc?.name ?? null)}
              >
                <FileDiff className="h-3.5 w-3.5" />
                <span className="hidden md:inline">差量</span>
              </Button>

              {/* 基准版管理 */}
              <Popover
                open={baselineOpen}
                onOpenChange={(v) => {
                  setBaselineOpen(v)
                  if (v) void refreshBaselines()
                }}
              >
                <PopoverTrigger asChild>
                  <Button variant="outline" size="sm" className="h-8 gap-1.5" title="管理差量计算的基准版">
                    <GitCompare className="h-3.5 w-3.5" />
                    <span className="hidden md:inline">基准</span>
                    {baselineStats.changed > 0 && (
                      <Badge variant="secondary" className="h-4 px-1 text-[10px] tabular">
                        {baselineStats.changed}
                      </Badge>
                    )}
                  </Button>
                </PopoverTrigger>
                <BaselinePanel
                  items={baselines}
                  loading={baselineLoading}
                  canAdmin={authCanAdmin}
                  currentFile={doc?.name ?? null}
                  onRefresh={() => void refreshBaselines()}
                  onSnapshotAll={() => void handleSnapshotAll()}
                  onSnapshotOne={(name: string) => void handleSnapshotOne(name)}
                  onDelete={(name: string) => void handleDeleteBaseline(name)}
                  onExportAll={() => void handlePatch(null)}
                />
              </Popover>
              <Button
                variant="outline"
                size="sm"
                className="h-8 gap-1.5"
                disabled={!doc || !dirty}
                title="放弃全部未保存修改"
                onClick={() => setConfirmDiscard(true)}
              >
                <RotateCcw className="h-3.5 w-3.5" />
                <span className="hidden md:inline">放弃</span>
              </Button>
              <Button
                size="sm"
                className="h-8 gap-1.5"
                disabled={!doc || !dirty || saving}
                onClick={() => void handleSave()}
              >
                <Save className="h-3.5 w-3.5" />
                <span className="hidden sm:inline">{saving ? '保存中…' : '保存'}</span>
              </Button>
            </div>
          </div>

          {/* 说明与分区切换 */}
          {doc && (
            <div className="mt-1.5 flex flex-wrap items-center gap-2">
              {fileMeta?.desc && <span className="text-[11px] text-muted-foreground">{fileMeta.desc}</span>}
              {sections.length > 1 && (
                <div className="flex flex-wrap gap-1">
                  {sections.map((s, i) => (
                    <button
                      key={`${s.key}-${i}`}
                      type="button"
                      onClick={() => setSectionIndex(i)}
                      className={cn(
                        'rounded-md border px-2 py-0.5 text-[11px] transition-colors',
                        sectionIndex === i
                          ? 'border-primary bg-primary/10'
                          : 'border-border text-muted-foreground hover:text-foreground'
                      )}
                    >
                      {s.key ?? '根节点'}
                      <span className="ml-1 tabular opacity-70">{s.entryCount}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        {/* 内容 */}
        <div className="min-h-0 flex-1 overflow-hidden">
          {docStatus === 'loading' ? (
            <div className="space-y-2 p-4">
              <Skeleton className="h-6 w-52" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-3/4" />
            </div>
          ) : docStatus === 'error' ? (
            <div className="p-4">
              <Alert variant="destructive">
                <TriangleAlert className="h-4 w-4" />
                <AlertDescription className="text-[12px]">{docError}</AlertDescription>
              </Alert>
            </div>
          ) : !doc ? (
            renderSection()
          ) : section?.kind === 'records' ? (
            <CommonEditContext.Provider value={editContext!}>
              <div className="flex h-full min-h-0 flex-col">
                <NotesBanner fileName={doc.name} fileMeta={fileMeta} sectionKey={section.key} />
                <div className="min-h-0 flex-1 overflow-hidden">{renderSection()}</div>
              </div>
            </CommonEditContext.Provider>
          ) : (
            <CommonEditContext.Provider value={editContext!}>
              <div className="h-full overflow-y-auto px-3 py-3 sm:px-4">
                <NotesBanner fileName={doc.name} fileMeta={fileMeta} sectionKey={section?.key ?? null} />
                <div className="space-y-2">{renderSection()}</div>
                <Separator className="my-4" />
                <p className="pb-4 text-[11px] text-muted-foreground">
                  提示：本视图按值的实际类型递归生成控件，未在字典中登记的字段会直接显示英文键名。
                </p>
              </div>
            </CommonEditContext.Provider>
          )}
        </div>
      </div>

      {/* 备份对话框 */}
      <Dialog open={backupOpen} onOpenChange={setBackupOpen}>
        {/* sm:max-w-* 用来覆盖 DialogContent 自带的 `sm:max-w-lg`（512px），否则宽屏下会被截住 */}
        <DialogContent className="max-h-[80vh] w-[min(94vw,620px)] overflow-hidden sm:max-w-[620px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-[15px]">
              <ArchiveRestore className="h-4 w-4 text-primary" />
              备份历史
            </DialogTitle>
            <DialogDescription className="text-[12px]">
              每次保存前会自动生成一份备份，最多保留最近 40 份。回滚会把当前文件另存后再覆盖。
            </DialogDescription>
          </DialogHeader>
          <div className="max-h-[52vh] space-y-1 overflow-y-auto">
            {store.backups.length === 0 ? (
              <p className="py-6 text-center text-[12px] text-muted-foreground">
                {doc ? `「${doc.name}」暂无备份` : '请先选择数据表'}
              </p>
            ) : (
              store.backups.map((b) => (
                <div
                  key={b.dir}
                  className="flex items-center gap-2 rounded border border-border px-2 py-1.5 text-[11px]"
                >
                  <span className="shrink-0 tabular">{new Date(b.at).toLocaleString('zh-CN', { hour12: false })}</span>
                  <span className="min-w-0 flex-1 truncate text-muted-foreground">{b.reason}</span>
                  <span className="shrink-0 tabular text-muted-foreground">{formatSize(b.size)}</span>
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-6 shrink-0 gap-1 text-[10px]"
                    onClick={() => setRestoreDir(b.dir)}
                  >
                    <ArchiveRestore className="h-3 w-3" />
                    回滚
                  </Button>
                </div>
              ))
            )}
          </div>
        </DialogContent>
      </Dialog>

      <AlertDialog open={restoreDir !== null} onOpenChange={(v) => !v && setRestoreDir(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>确认回滚到该备份？</AlertDialogTitle>
            <AlertDialogDescription>
              当前文件会先被另存为一份新备份，然后用该备份覆盖。回滚后内存中的未保存修改将丢失。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground"
              onClick={() => restoreDir && void handleRestore(restoreDir)}
            >
              确认回滚
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* 离开前提醒 */}
      <AlertDialog open={confirmDiscard} onOpenChange={setConfirmDiscard}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>放弃未保存修改？</AlertDialogTitle>
            <AlertDialogDescription>将还原为最近一次加载的内容。</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                store.discard()
                setConfirmDiscard(false)
              }}
            >
              确认放弃
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* 未保存时的浏览器离开拦截 */}
      <BeforeUnloadGuard dirty={dirty} />
    </div>
  )
}

/**
 * 提示条：合并「文件级说明」与「分区级提醒」。
 *
 * 分区提醒用于标注「C# 不读取该分区」「与另一个文件写入同一张表」这类容易踩坑的情况。
 *
 * @param props.fileName 文件名
 * @param props.fileMeta 文件元数据
 * @param props.sectionKey 当前分区键（根节点为 null）
 */
function NotesBanner({
  fileName,
  fileMeta,
  sectionKey,
}: {
  fileName: string
  fileMeta?: CommonFileMeta
  sectionKey: string | null
}) {
  const sectionNotes =
    COMMON_SECTION_NOTES[`${fileName}:${sectionKey ?? 'root'}`] ?? COMMON_SECTION_NOTES[`${fileName}:*`] ?? []
  const fileNotes = fileMeta?.notes ?? []
  if (fileNotes.length === 0 && sectionNotes.length === 0) return null
  return (
    <Alert className="shrink-0 rounded-none border-x-0 border-t-0 py-2">
      <Info className="h-3.5 w-3.5" />
      <AlertDescription className="text-[11px] leading-relaxed">
        {fileNotes.map((n, i) => (
          <span key={`f${i}`} className="block">
            · {n}
          </span>
        ))}
        {sectionNotes.map((n, i) => (
          <span key={`s${i}`} className="block text-amber-500">
            · {n}
          </span>
        ))}
      </AlertDescription>
    </Alert>
  )
}

/** 左侧文件按钮 */
function FileButton({
  file,
  active,
  onClick,
}: {
  file: { name: string; size: number; summary: { entryCount: number; kind: string } | null; parseError: string | null }
  active: boolean
  onClick: () => void
}) {
  const meta = COMMON_META_BY_NAME[file.name]
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left transition-colors',
        active ? 'bg-primary/15' : 'hover:bg-accent'
      )}
      title={meta?.desc ?? file.name}
    >
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1.5">
          <span className="truncate text-[12px]">{meta?.label ?? file.name}</span>
          {file.parseError && <span className="shrink-0 text-[10px] text-destructive">!</span>}
        </div>
        <div className="flex items-center gap-1 text-[10px] text-muted-foreground">
          <span className="truncate font-mono">{file.name}</span>
        </div>
      </div>
      {file.summary && (
        <span className="shrink-0 text-[10px] tabular text-muted-foreground">{file.summary.entryCount}</span>
      )}
    </button>
  )
}

/** 未保存修改时的浏览器离开提醒 */
function BeforeUnloadGuard({ dirty }: { dirty: boolean }) {
  useEffect(() => {
    if (!dirty) return
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault()
      e.returnValue = ''
    }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [dirty])
  return null
}

/** 基准版面板的属性 */
interface BaselinePanelProps {
  items: BaselineItem[]
  loading: boolean
  /** 是否可管理基准（管理员） */
  canAdmin: boolean
  /** 当前打开的文件名，用于「把本文件登记为基准」 */
  currentFile: string | null
  onRefresh: () => void
  onSnapshotAll: () => void
  onSnapshotOne: (name: string) => void
  onDelete: (name: string) => void
  onExportAll: () => void
}

/**
 * 基准版管理弹层。
 *
 * 基准是「全体用户计算差量的参照」，因此上传动作只对管理员开放；
 * 普通用户仍可看到哪些文件有基准、自己改动了哪些。
 */
function BaselinePanel({
  items,
  loading,
  canAdmin,
  currentFile,
  onRefresh,
  onSnapshotAll,
  onSnapshotOne,
  onDelete,
  onExportAll,
}: BaselinePanelProps) {
  const changed = items.filter((b) => !b.upToDate)

  return (
    <PopoverContent align="end" className="w-[min(92vw,420px)] p-0">
      <div className="flex items-center gap-2 border-b border-border px-3 py-2">
        <GitCompare className="h-3.5 w-3.5 text-primary" />
        <span className="text-[12px] font-medium">基准版</span>
        {loading && <Loader2 className="h-3 w-3 animate-spin text-muted-foreground" />}
        <Badge variant="secondary" className="h-4 px-1 text-[10px] tabular">
          {items.length} 个
        </Badge>
        {changed.length > 0 && (
          <Badge variant="outline" className="h-4 border-amber-500/60 px-1 text-[10px] tabular text-amber-500">
            {changed.length} 个有改动
          </Badge>
        )}
        <Button variant="ghost" size="icon" className="ml-auto h-6 w-6" onClick={onRefresh} title="刷新">
          <RefreshCw className="h-3 w-3" />
        </Button>
      </div>

      <div className="border-b border-border px-3 py-2 text-[11px] text-muted-foreground">
        差量补丁是相对这份基准计算的：补丁里只出现改动过的字段，未改动的字段一律不包含。
      </div>

      <ScrollArea className="max-h-[46vh]">
        <div className="divide-y divide-border/60">
          {items.length === 0 && (
            <p className="px-3 py-6 text-center text-[11px] text-muted-foreground">
              还没有任何基准版。
              {canAdmin ? '点下方「把当前全部登记为基准」即可建立。' : '请联系管理员上传基准。'}
            </p>
          )}
          {items.map((b) => (
            <div key={b.name} className="flex items-center gap-2 px-3 py-2">
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-1.5">
                  <span className="truncate font-mono text-[11px]">{b.name}</span>
                  {b.upToDate ? (
                    <span className="shrink-0 text-[10px] text-emerald-500">一致</span>
                  ) : (
                    <span className="shrink-0 text-[10px] text-amber-500">有改动</span>
                  )}
                  {currentFile === b.name && (
                    <span className="shrink-0 text-[10px] text-muted-foreground">· 当前</span>
                  )}
                </div>
                <p className="text-[10px] text-muted-foreground">
                  {formatBaselineTime(b.at)}
                  {b.author ? ` · ${b.author}` : ''}
                </p>
              </div>
              {canAdmin && (
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6 shrink-0 text-muted-foreground hover:text-destructive"
                  title="删除该基准"
                  onClick={() => onDelete(b.name)}
                >
                  <Trash2 className="h-3 w-3" />
                </Button>
              )}
            </div>
          ))}
        </div>
      </ScrollArea>

      <div className="space-y-1.5 border-t border-border p-3">
        <Button variant="outline" size="sm" className="h-8 w-full gap-1.5" onClick={onExportAll}>
          <FileDiff className="h-3.5 w-3.5" />
          导出全部差量补丁
        </Button>
        {canAdmin && (
          <>
            {currentFile && (
              <Button
                variant="outline"
                size="sm"
                className="h-8 w-full gap-1.5"
                disabled={loading}
                onClick={() => onSnapshotOne(currentFile)}
              >
                <UploadCloud className="h-3.5 w-3.5" />
                把「{currentFile}」登记为基准
              </Button>
            )}
            <Button size="sm" className="h-8 w-full gap-1.5" disabled={loading} onClick={onSnapshotAll}>
              <UploadCloud className="h-3.5 w-3.5" />
              把当前全部登记为基准
            </Button>
          </>
        )}
      </div>
    </PopoverContent>
  )
}

/**
 * 把 ISO 时间格式化成「MM-DD HH:mm」。
 *
 * @param iso ISO 时间
 * @returns 展示文本
 */
function formatBaselineTime(iso: string): string {
  if (!iso) return '未知时间'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}
