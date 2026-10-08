/**
 * 概览面板
 *
 * 汇总剧本的基本信息、数据规模、校验状态与备份历史：
 * - 剧本信息（名称、描述、起止时间、地图类型等）可直接编辑
 * - 四类实体数量与问题分布一览
 * - 常见问题的快捷跳转
 * - 备份列表与一键回滚
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react'
import {
  AlertTriangle,
  ArchiveRestore,
  Building2,
  CalendarClock,
  CheckCircle2,
  Database,
  Download,
  FileJson,
  Flag,
  HardDriveDownload,
  RotateCcw,
  Save,
  Shield,
  Users,
} from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import { scenarioDownloadUrl } from '@/lib/api'
import type { CollectionKey, Entity } from '@/lib/types'
import { COLLECTION_META, COLLECTION_ORDER } from '@/lib/types'
import { INFO_FIELDS } from '@/lib/schema'
import { getScalarValue, buildFieldValue } from '@/lib/fields'
import { computeCollectionStats } from '@/lib/indexes'
import { useScenarioStore } from '@/state/store'
import { Button, buttonVariants } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
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
import { FieldInput, FieldRow } from './FieldInput'
import { restoreBackup } from '@/lib/api'

/** 集合图标 */
const COLLECTION_ICON: Record<CollectionKey, React.ElementType> = {
  personSet: Users,
  citySet: Building2,
  corpsSet: Shield,
  forceSet: Flag,
}

/** 字节数格式化 */
function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`
}

/** 时间格式化 */
function formatTime(iso: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleString('zh-CN', { hour12: false })
}

interface OverviewPanelProps {
  onNavigate: (collection: CollectionKey, entityId: number) => void
  onGoValidation: () => void
}

/**
 * 概览面板。
 */
export function OverviewPanel({ onNavigate, onGoValidation }: OverviewPanelProps) {
  const store = useScenarioStore()
  const {
    scenario,
    meta,
    options,
    issueCounts,
    issuesByEntity,
    issues,
    patchInfo,
    patchRoot,
    refreshBackups,
    backups,
    reload,
    discardAll,
    save,
    dirty,
    saving,
    changeCount,
  } = store

  const [confirmRestore, setConfirmRestore] = useState<string | null>(null)
  const [confirmDiscard, setConfirmDiscard] = useState(false)

  useEffect(() => {
    void refreshBackups()
  }, [refreshBackups])

  const stats = useMemo(() => {
    if (!scenario) return []
    return COLLECTION_ORDER.map((key) => {
      const count = Object.keys((scenario[key] ?? {}) as Record<string, unknown>).length
      return { key, ...computeCollectionStats(count, issuesByEntity, key) }
    })
  }, [issuesByEntity, scenario])

  const infoIssues = useMemo(() => issues.filter((i) => i.collection === 'root'), [issues])

  /** 问题最多的实体，便于快速定位 */
  const topProblemEntities = useMemo(() => {
    const list: { collection: CollectionKey; id: number; name: string; error: number; warning: number }[] = []
    for (const [key, items] of issuesByEntity) {
      const [collection, idText] = key.split(':')
      if (!COLLECTION_ORDER.includes(collection as CollectionKey)) continue
      const error = items.filter((i) => i.level === 'error').length
      const warning = items.filter((i) => i.level === 'warning').length
      if (error === 0 && warning === 0) continue
      const entity = ((scenario?.[collection as CollectionKey] ?? {}) as Record<string, Entity>)[idText]
      list.push({
        collection: collection as CollectionKey,
        id: Number(idText),
        name: String(entity?.Name ?? `#${idText}`),
        error,
        warning,
      })
    }
    return list.sort((a, b) => b.error * 100 + b.warning - (a.error * 100 + a.warning)).slice(0, 8)
  }, [issuesByEntity, scenario])

  const handleRestore = useCallback(
    async (name: string) => {
      try {
        await restoreBackup(name)
        await reload()
        await refreshBackups()
        toast.success(`已从备份「${name}」恢复剧本`)
      } catch (e) {
        toast.error(`恢复失败：${(e as Error).message}`)
      }
      setConfirmRestore(null)
    },
    [reload, refreshBackups]
  )

  if (!scenario) return null

  const infoFields = INFO_FIELDS

  return (
    <div className="h-full overflow-y-auto px-3 py-3 sm:px-4">
      {/* 顶部状态条 */}
      <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-card px-3 py-2">
        <FileJson className="h-4 w-4 shrink-0 text-primary" />
        <span className="min-w-0 flex-1 truncate font-mono text-[11px] text-muted-foreground" title={meta?.file}>
          {meta?.file}
        </span>
        <Badge variant="outline" className="shrink-0 tabular text-[10px]">
          修订号 {meta?.revision}
        </Badge>
        <Badge variant="outline" className="shrink-0 tabular text-[10px]">
          {formatSize(meta?.size ?? 0)}
        </Badge>
        <Badge variant="outline" className="shrink-0 text-[10px]">
          修改于 {meta ? formatTime(meta.mtime) : '—'}
        </Badge>
        {dirty ? (
          <Badge className="shrink-0 bg-amber-500 text-[10px] text-black">
            有 {changeCount} 处未保存的修改
          </Badge>
        ) : (
          <Badge variant="secondary" className="shrink-0 text-[10px]">
            与磁盘一致
          </Badge>
        )}
      </div>

      {/* 统计卡片 */}
      <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
        {stats.map((s) => {
          const Icon = COLLECTION_ICON[s.key]
          return (
            <button
              key={s.key}
              type="button"
              className="stat-card items-start text-left transition-colors hover:border-primary/50"
              onClick={() => onNavigate(s.key, 0)}
            >
              <div className="flex w-full items-center gap-1.5 text-[11px] text-muted-foreground">
                <Icon className="h-3.5 w-3.5" />
                {COLLECTION_META[s.key].label}
                <span className="ml-auto tabular text-[16px] font-semibold text-foreground">{s.total}</span>
              </div>
              <div className="flex items-center gap-2 text-[10px]">
                {s.errorEntities > 0 ? (
                  <span className="text-destructive">{s.errorEntities} 条有错误</span>
                ) : s.warningEntities > 0 ? (
                  <span className="text-amber-500">{s.warningEntities} 条有警告</span>
                ) : (
                  <span className="text-emerald-500">全部通过</span>
                )}
              </div>
            </button>
          )
        })}
      </div>

      <div className="mt-3 grid grid-cols-1 gap-3 lg:grid-cols-3">
        {/* 剧本信息 */}
        <Card className="lg:col-span-2">
          <CardHeader className="flex-row items-center gap-2 space-y-0 pb-3">
            <CardTitle className="flex items-center gap-2 text-[14px]">
              <CalendarClock className="h-4 w-4 text-primary" />
              剧本基本信息
            </CardTitle>
            <span className="ml-auto text-[11px] text-muted-foreground">修改即时生效，保存后写入文件</span>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="grid grid-cols-1 gap-x-3 gap-y-3 sm:grid-cols-2">
              <div className="sm:col-span-2">
                <FieldRow field={{ key: 'Name', label: '剧本名称（顶层）', kind: 'string', group: '基本' }}>
                  <FieldInput
                    field={{ key: 'Name', label: '剧本名称', kind: 'string', group: '基本' }}
                    entity={scenario}
                    value={getScalarValue(scenario, { key: 'Name', label: '', kind: 'string', group: '' })}
                    onChange={(v) => patchRoot({ Name: String(v ?? '') })}
                    options={options}
                    issues={infoIssues.filter((i) => i.field === 'Name')}
                  />
                </FieldRow>
              </div>
              {infoFields.map((field) => {
                const info = (scenario.Info ?? {}) as unknown as Entity
                const defined = Object.prototype.hasOwnProperty.call(info, field.key)
                const isWide = field.key === 'description'
                return (
                  <div key={field.key} className={cn(isWide && 'sm:col-span-2')}>
                    <FieldRow
                      field={field}
                      issues={infoIssues.filter((i) => i.field === field.key)}
                      hint={!defined ? '当前未定义，填写后写入' : undefined}
                    >
                      <FieldInput
                        field={field}
                        entity={info}
                        value={defined ? getScalarValue(info, field) : null}
                        onChange={(v) => {
                          if (v === null) return
                          patchInfo({ [field.key]: buildFieldValue(info, field, v) })
                        }}
                        options={options}
                        issues={infoIssues.filter((i) => i.field === field.key)}
                      />
                    </FieldRow>
                  </div>
                )
              })}
            </div>
          </CardContent>
        </Card>

        {/* 校验与操作 */}
        <div className="space-y-3">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="flex items-center gap-2 text-[14px]">
                {issueCounts.error + issueCounts.warning === 0 ? (
                  <CheckCircle2 className="h-4 w-4 text-emerald-500" />
                ) : (
                  <AlertTriangle className="h-4 w-4 text-amber-500" />
                )}
                校验状态
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              <div className="flex items-center gap-3 text-[12px]">
                <span className="text-destructive">
                  错误 <span className="tabular">{issueCounts.error}</span>
                </span>
                <span className="text-amber-500">
                  警告 <span className="tabular">{issueCounts.warning}</span>
                </span>
                <span className="text-sky-500">
                  提示 <span className="tabular">{issueCounts.info}</span>
                </span>
              </div>
              {topProblemEntities.length > 0 && (
                <div className="space-y-1">
                  <p className="text-[11px] text-muted-foreground">问题最多的条目</p>
                  {topProblemEntities.map((t) => (
                    <button
                      key={`${t.collection}:${t.id}`}
                      type="button"
                      className="flex w-full items-center gap-2 rounded border border-border px-2 py-1 text-left text-[11px] transition-colors hover:border-primary/50"
                      onClick={() => onNavigate(t.collection, t.id)}
                    >
                      <span className="shrink-0 text-muted-foreground">{COLLECTION_META[t.collection].label}</span>
                      <span className="min-w-0 flex-1 truncate">{t.name}</span>
                      {t.error > 0 && <span className="tabular text-destructive">{t.error}</span>}
                      {t.warning > 0 && <span className="tabular text-amber-500">{t.warning}</span>}
                    </button>
                  ))}
                </div>
              )}
              <Button variant="outline" size="sm" className="w-full" onClick={onGoValidation}>
                查看全部校验结果
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="flex items-center gap-2 text-[14px]">
                <Database className="h-4 w-4 text-primary" />
                操作
              </CardTitle>
            </CardHeader>
            {/* 左列都是「产出 / 保存」，右列都是「还原」，按语义分列比按按钮数量排更清楚 */}
            <CardContent className="grid grid-cols-2 gap-2">
              <Button size="sm" className="gap-1.5" onClick={() => void save()} disabled={saving || !dirty}>
                <Save className="h-3.5 w-3.5" />
                保存
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="gap-1.5"
                title="放弃全部未保存修改"
                disabled={!dirty}
                onClick={() => setConfirmDiscard(true)}
              >
                <HardDriveDownload className="h-3.5 w-3.5" />
                放弃修改
              </Button>
              {/*
                下载剧本：走浏览器原生下载，直接下磁盘原件（含 CRLF 与 2 空格缩进）。
                未保存的改动不在其中，所以有未保存改动时先拦住并提示保存。
              */}
              <a
                href={scenarioDownloadUrl()}
                download="Scenario.json"
                className={cn(
                  buttonVariants({ variant: 'outline', size: 'sm' }),
                  'gap-1.5',
                  dirty && 'opacity-60'
                )}
                title={
                  dirty
                    ? '当前有未保存的改动，下载的是磁盘上的已保存版本；建议先保存'
                    : '下载磁盘上的 Scenario.json 原件'
                }
                onClick={(e) => {
                  if (dirty) {
                    e.preventDefault()
                    toast.warning('有未保存的修改，请先保存后再下载', {
                      description: '下载的是磁盘上的已保存版本，不会包含当前改动。',
                    })
                  }
                }}
              >
                <Download className="h-3.5 w-3.5" />
                下载剧本
              </a>
              <Button variant="outline" size="sm" className="gap-1.5" onClick={() => void reload()}>
                <RotateCcw className="h-3.5 w-3.5" />
                重新加载
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>

      {/* 备份 */}
      <Card className="mt-3">
        <CardHeader className="flex-row items-center gap-2 space-y-0 pb-3">
          <CardTitle className="flex items-center gap-2 text-[14px]">
            <ArchiveRestore className="h-4 w-4 text-primary" />
            备份历史
          </CardTitle>
          <Badge variant="secondary" className="text-[10px] tabular">
            最近 {backups.length} 份
          </Badge>
          <Button variant="outline" size="sm" className="ml-auto h-7" onClick={() => void refreshBackups()}>
            刷新
          </Button>
        </CardHeader>
        <CardContent>
          {backups.length === 0 ? (
            <p className="py-4 text-center text-[12px] text-muted-foreground">
              暂无备份。每次保存前会自动生成一份备份，最多保留最近 60 份。
            </p>
          ) : (
            <div className="max-h-[280px] space-y-1 overflow-y-auto">
              {backups.map((b) => (
                <div
                  key={b.name}
                  className="flex items-center gap-2 rounded border border-border px-2 py-1.5 text-[11px]"
                >
                  <span className="shrink-0 tabular">{formatTime(b.at)}</span>
                  <span className="min-w-0 flex-1 truncate text-muted-foreground">
                    {b.reason || b.name}
                  </span>
                  <span className="shrink-0 tabular text-muted-foreground">{formatSize(b.size)}</span>
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-6 shrink-0 gap-1 text-[10px]"
                    onClick={() => setConfirmRestore(b.name)}
                  >
                    <ArchiveRestore className="h-3 w-3" />
                    回滚
                  </Button>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Separator className="my-4" />
      <p className="pb-4 text-[11px] leading-relaxed text-muted-foreground">
        提示：保存会先对剧本文本做一次完整备份，再以「2 空格缩进 + CRLF」的原始格式写回
        Scenario.json，保证与游戏侧的读写风格一致。若磁盘文件在此期间被其它程序修改，保存会被拒绝以
        避免覆盖他人改动，此时请先「重新加载」。
      </p>

      <AlertDialog open={confirmRestore !== null} onOpenChange={(v) => !v && setConfirmRestore(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>确认回滚到该备份？</AlertDialogTitle>
            <AlertDialogDescription>
              当前剧本文件会先被另存为一份新备份，然后用「{confirmRestore}」覆盖。回滚后内存中的未保存
              修改将被丢弃。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground"
              onClick={() => confirmRestore && void handleRestore(confirmRestore)}
            >
              确认回滚
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={confirmDiscard} onOpenChange={setConfirmDiscard}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>放弃全部未保存修改？</AlertDialogTitle>
            <AlertDialogDescription>
              内存中的 {changeCount} 处修改将被还原为最近一次加载/保存时的内容。撤销栈也会被清空。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                discardAll()
                setConfirmDiscard(false)
                toast.success('已放弃全部未保存修改')
              }}
            >
              确认放弃
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
