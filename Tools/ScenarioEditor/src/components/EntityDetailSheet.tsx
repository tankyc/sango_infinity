/**
 * 实体详情编辑面板
 *
 * 以侧边抽屉的形式呈现单个实体的全部字段，按 schema 分组折叠展示。
 * 特点：
 * - 字段即时保存（无需点确定），并实时显示该字段的校验问题
 * - 支持上下条快速切换（键盘 J/K 或按钮）
 * - 支持显示/隐藏「未定义字段」，用于补全或清理透传键
 * - 武将实体额外支持「从武将库回填」
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react'
import { ChevronDown, ChevronLeft, ChevronRight, Copy, Database, Plus, Trash2, X } from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import type { CollectionKey, Entity, ValidationIssue } from '@/lib/types'
import { COLLECTION_META } from '@/lib/types'
import type { FieldDef } from '@/lib/schema'
import { COLLECTION_FIELDS, COLLECTION_GROUPS } from '@/lib/schema'
import { buildFieldValue, getScalarValue } from '@/lib/fields'
import { buildScenarioNameMaps, entityDisplayName } from '@/lib/refResolver'
import { useScenarioStore, READONLY_COLLECTIONS } from '@/state/store'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
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
import { LibraryDialog } from './LibraryDialog'

interface EntityDetailSheetProps {
  collection: CollectionKey
  entityId: number | null
  onClose: () => void
  onSelectEntity: (id: number) => void
}

/**
 * 单个实体的编辑抽屉。
 */
export function EntityDetailSheet({ collection, entityId, onClose, onSelectEntity }: EntityDetailSheetProps) {
  const store = useScenarioStore()
  const { scenario, options, issuesByEntity, patchEntity, replaceEntity, removeEntities } = store
  const meta = COLLECTION_META[collection]
  const fields = COLLECTION_FIELDS[collection]
  const groups = COLLECTION_GROUPS[collection]

  const [showUndefined, setShowUndefined] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [libraryOpen, setLibraryOpen] = useState(false)

  const set = useMemo(
    () => (scenario?.[collection] ?? {}) as Record<string, Entity>,
    [scenario, collection]
  )
  const entity = entityId !== null ? set[String(entityId)] ?? null : null

  const sortedIds = useMemo(() => Object.values(set).map((e) => Number(e.Id)).sort((a, b) => a - b), [set])
  const currentIndex = entityId !== null ? sortedIds.indexOf(entityId) : -1

  const referenceNames = useMemo(() => buildScenarioNameMaps(scenario), [scenario])

  const issues: ValidationIssue[] = useMemo(
    () => (entityId !== null ? issuesByEntity.get(`${collection}:${entityId}`) ?? [] : []),
    [collection, entityId, issuesByEntity]
  )

  /** 取某个字段的问题列表（useMemo 结果缓存，避免每行重复过滤） */
  const issuesByField = useMemo(() => {
    const map = new Map<string, ValidationIssue[]>()
    for (const item of issues) {
      const list = map.get(item.field)
      if (list) list.push(item)
      else map.set(item.field, [item])
    }
    return map
  }, [issues])

  const issuesFor = useCallback((key: string) => issuesByField.get(key) ?? [], [issuesByField])

  /** 未在 schema 中定义的键 */
  const undefinedKeys = useMemo(() => {
    if (!entity) return []
    const known = new Set(fields.map((f) => f.key))
    return Object.keys(entity).filter((k) => !known.has(k))
  }, [entity, fields])

  /** 未定义的 schema 字段 */
  const missingFields = useMemo(() => {
    if (!entity) return []
    return fields.filter((f) => !Object.prototype.hasOwnProperty.call(entity, f.key))
  }, [entity, fields])

  const go = useCallback(
    (delta: number) => {
      if (currentIndex < 0) return
      const next = currentIndex + delta
      if (next < 0 || next >= sortedIds.length) return
      onSelectEntity(sortedIds[next])
    },
    [currentIndex, onSelectEntity, sortedIds]
  )

  // 键盘快捷键：J/K 切换上下条，Esc 关闭
  useEffect(() => {
    if (entityId === null) return
    const handler = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null
      const typing =
        target &&
        (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)
      if (typing) {
        if (e.key === 'Escape') (target as HTMLElement).blur()
        return
      }
      if (e.key === 'j') go(1)
      else if (e.key === 'k') go(-1)
      else if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [entityId, go, onClose])

  const handleFieldChange = useCallback(
    (field: FieldDef, raw: number | string | null) => {
      if (!entity || raw === null) return
      const value = buildFieldValue(entity, field, raw)
      patchEntity(collection, Number(entity.Id), { [field.key]: value })
    },
    [collection, entity, patchEntity]
  )

  const handleChangeId = useCallback(
    (field: FieldDef, changeId: number) => {
      if (!entity) return
      const raw = entity[field.key]
      const arr = Array.isArray(raw) ? [...(raw as number[])] : [0, 5]
      while (arr.length < 2) arr.push(0)
      arr[1] = changeId
      patchEntity(collection, Number(entity.Id), { [field.key]: arr })
    },
    [collection, entity, patchEntity]
  )

  const handleClone = useCallback(() => {
    if (!entity) return
    const newId = store.nextEntityId(collection)
    const copy: Entity = { ...entity, Id: newId }
    if (typeof entity.Name === 'string') copy.Name = `${entity.Name}（副本）`
    store.addEntity(collection, copy)
    toast.success(`已复制为 Id ${newId}`)
    onSelectEntity(newId)
  }, [collection, entity, onSelectEntity, store])

  const handleDelete = useCallback(() => {
    if (!entity) return
    if (READONLY_COLLECTIONS.has(collection)) {
      setConfirmDelete(false)
      toast.warning(`${meta.singular}不允许删除`)
      return
    }
    const id = Number(entity.Id)
    removeEntities(collection, [id])
    setConfirmDelete(false)
    onClose()
    toast.success(`已删除 ${meta.singular} #${id}（可用撤销恢复）`)
  }, [collection, entity, meta.singular, onClose, removeEntities])

  /** 武将与城池不允许删除 */
  const readOnly = READONLY_COLLECTIONS.has(collection)

  const errorCount = issues.filter((i) => i.level === 'error').length
  const warningCount = issues.filter((i) => i.level === 'warning').length

  const open = entityId !== null && entity !== null

  return (
    <>
      <Sheet open={open} onOpenChange={(v) => !v && onClose()}>
        <SheetContent
          side="right"
          className="flex w-full flex-col gap-0 p-0 sm:max-w-[560px] md:max-w-[620px]"
        >
          <SheetHeader className="shrink-0 space-y-2 border-b border-border px-4 py-3">
            <div className="flex items-start gap-2">
              <div className="min-w-0 flex-1">
                <SheetTitle className="flex items-center gap-2 truncate text-[16px]">
                  <span className="truncate">
                    {entity ? entityDisplayName(entity, collection, referenceNames) : `#${entityId}`}
                  </span>
                  <Badge variant="secondary" className="shrink-0 tabular">
                    #{String(entityId)}
                  </Badge>
                </SheetTitle>
                <div className="mt-1 flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground">
                  <span>{meta.label}编辑</span>
                  {errorCount > 0 && (
                    <span className="text-destructive">
                      {errorCount} 个错误
                    </span>
                  )}
                  {warningCount > 0 && <span className="text-amber-500">{warningCount} 个警告</span>}
                  <span className="tabular">
                    第 {currentIndex + 1} / {sortedIds.length} 条
                  </span>
                </div>
              </div>
              <Button variant="ghost" size="icon" className="h-8 w-8" onClick={onClose} aria-label="关闭">
                <X className="h-4 w-4" />
              </Button>
            </div>
            <div className="flex flex-wrap items-center gap-1.5">
              <Button
                variant="outline"
                size="sm"
                className="h-7 gap-1"
                disabled={currentIndex <= 0}
                onClick={() => go(-1)}
              >
                <ChevronLeft className="h-3.5 w-3.5" />
                上一条
                <kbd className="ml-1 hidden rounded border border-border px-1 text-[9px] sm:inline">K</kbd>
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="h-7 gap-1"
                disabled={currentIndex < 0 || currentIndex >= sortedIds.length - 1}
                onClick={() => go(1)}
              >
                下一条
                <kbd className="ml-1 hidden rounded border border-border px-1 text-[9px] sm:inline">J</kbd>
                <ChevronRight className="h-3.5 w-3.5" />
              </Button>
              <div className="mx-0.5 h-5 w-px bg-border" />
              <Button variant="outline" size="sm" className="h-7 gap-1" onClick={handleClone}>
                <Copy className="h-3.5 w-3.5" />
                复制
              </Button>
              {collection === 'personSet' && (
                <Button
                  variant="outline"
                  size="sm"
                  className="h-7 gap-1"
                  onClick={() => setLibraryOpen(true)}
                >
                  <Database className="h-3.5 w-3.5" />
                  武将库回填
                </Button>
              )}
              <Button
                variant="outline"
                size="sm"
                className="ml-auto h-7 gap-1 text-destructive hover:text-destructive"
                onClick={() => setConfirmDelete(true)}
                disabled={readOnly}
                title={readOnly ? `${meta.singular}不允许删除` : undefined}
              >
                <Trash2 className="h-3.5 w-3.5" />
                删除
              </Button>
            </div>
          </SheetHeader>

          <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
            {entity && (
              <div className="space-y-3">
                {groups.map((group) => {
                  // Id 不需要在表单里编辑：它已经在标题栏显示，且不允许改动
                  const groupFields = fields.filter((f) => f.group === group.name && f.key !== 'Id')
                  if (groupFields.length === 0) return null
                  const presentCount = groupFields.filter((f) =>
                    Object.prototype.hasOwnProperty.call(entity, f.key)
                  ).length
                  const groupIssueCount = issues.filter((i) =>
                    groupFields.some((f) => f.key === i.field)
                  ).length
                  return (
                    <FieldGroup
                      key={group.name}
                      title={group.name}
                      desc={group.desc}
                      badge={`${presentCount}/${groupFields.length}`}
                      issueCount={groupIssueCount}
                      defaultOpen={groupIssueCount > 0 || group.name === '基本'}
                    >
                      <div className="grid grid-cols-1 gap-x-3 gap-y-3 sm:grid-cols-2">
                        {groupFields.map((field) => {
                          const defined = Object.prototype.hasOwnProperty.call(entity, field.key)
                          if (!defined && !showUndefined) return null
                          const isWide = field.kind === 'string' && (field.key === 'description' || field.key === 'desc')
                          return (
                            <div key={field.key} className={cn(isWide && 'sm:col-span-2')}>
                              <FieldRow
                                field={field}
                                issues={issuesFor(field.key)}
                                hint={!defined ? '该字段当前未定义，填写后将被写入' : undefined}
                              >
                                <FieldInput
                                  field={field}
                                  entity={entity}
                                  value={defined ? getScalarValue(entity, field) : null}
                                  onChange={(v) => handleFieldChange(field, v)}
                                  onChangeId={(v) => handleChangeId(field, v)}
                                  options={options}
                                  issues={issuesFor(field.key)}
                                  referenceNames={referenceNames}
                                  placeholder={defined ? undefined : '点击填写以添加该字段'}
                                />
                              </FieldRow>
                            </div>
                          )
                        })}
                      </div>
                    </FieldGroup>
                  )
                })}

                {undefinedKeys.length > 0 && (
                  <FieldGroup
                    title="未定义的透传字段"
                    desc="这些键不在字段表中，通常是原版遗留或拼写错误。保存时会原样保留，可在此查看"
                    badge={String(undefinedKeys.length)}
                    issueCount={0}
                    defaultOpen={false}
                  >
                    <div className="space-y-2">
                      {undefinedKeys.map((key) => (
                        <div
                          key={key}
                          className="flex items-center gap-2 rounded-md border border-border bg-muted/30 px-2 py-1.5"
                        >
                          <span className="font-mono text-[11px] text-muted-foreground">{key}</span>
                          <span className="min-w-0 flex-1 truncate font-mono text-[11px]">
                            {JSON.stringify(entity[key])}
                          </span>
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-6 w-6 text-destructive"
                            title="删除该键"
                            onClick={() => {
                              const next = { ...entity }
                              delete next[key]
                              replaceEntity(collection, Number(entity.Id), next)
                              toast.success(`已删除字段「${key}」`)
                            }}
                          >
                            <Trash2 className="h-3 w-3" />
                          </Button>
                        </div>
                      ))}
                    </div>
                  </FieldGroup>
                )}

                {showUndefined && missingFields.length > 0 && (
                  <div className="flex flex-wrap items-center gap-1.5 rounded-md border border-dashed border-border p-2">
                    <span className="text-[11px] text-muted-foreground">可添加的未定义字段：</span>
                    {missingFields.map((f) => (
                      <button
                        key={f.key}
                        type="button"
                        className="inline-flex items-center gap-1 rounded border border-border px-1.5 py-0.5 text-[11px] transition-colors hover:border-primary hover:text-primary"
                        onClick={() => {
                          const entity2 = { ...entity }
                          if (f.kind === 'string') entity2[f.key] = ''
                          else if (f.kind === 'attr') entity2[f.key] = [0, 5]
                          else if (f.kind === 'ability') entity2[f.key] = [0]
                          else if (
                            f.kind === 'intArray' ||
                            f.kind === 'polyRef' ||
                            f.kind === 'pairArray'
                          )
                            entity2[f.key] = []
                          else entity2[f.key] = 0
                          replaceEntity(collection, Number(entity.Id), entity2)
                        }}
                      >
                        <Plus className="h-3 w-3" />
                        {f.label}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>

          <div className="flex shrink-0 items-center gap-2 border-t border-border px-4 py-2">
            <Button
              variant={showUndefined ? 'default' : 'outline'}
              size="sm"
              className="h-8 gap-1.5"
              onClick={() => setShowUndefined((v) => !v)}
            >
              <Plus className="h-3.5 w-3.5" />
              {showUndefined ? '隐藏未定义字段' : '显示未定义字段'}
            </Button>
            <span className="text-[11px] text-muted-foreground">
              修改会立即写入内存，点击顶部「保存」后写入剧本文件
            </span>
          </div>
        </SheetContent>
      </Sheet>

      <AlertDialog open={confirmDelete} onOpenChange={setConfirmDelete}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>确认删除{meta.singular}？</AlertDialogTitle>
            <AlertDialogDescription>
              将删除 <span className="font-medium text-foreground">{String(entity?.Name ?? '')}</span>（Id{' '}
              {String(entityId)}）。删除后其它实体对它的引用会变成无效引用，可在保存前用撤销恢复。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete} className="bg-destructive text-destructive-foreground">
              确认删除
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {collection === 'personSet' && entity && (
        <LibraryDialog open={libraryOpen} onOpenChange={setLibraryOpen} targetId={Number(entity.Id)} />
      )}
    </>
  )
}

/**
 * 可折叠的字段分组。
 */
function FieldGroup({
  title,
  desc,
  badge,
  issueCount,
  defaultOpen,
  children,
}: {
  title: string
  desc?: string
  badge?: string
  issueCount: number
  defaultOpen?: boolean
  children: React.ReactNode
}) {
  const [open, setOpen] = useState(Boolean(defaultOpen))
  return (
    <Collapsible open={open} onOpenChange={setOpen} className="rounded-lg border border-border bg-card/40">
      <CollapsibleTrigger asChild>
        <button
          type="button"
          className="flex w-full items-center gap-2 px-3 py-2 text-left transition-colors hover:bg-accent/50"
        >
          <ChevronDown className={cn('h-3.5 w-3.5 shrink-0 transition-transform', !open && '-rotate-90')} />
          <span className="text-[13px] font-medium">{title}</span>
          {badge && (
            <Badge variant="outline" className="h-4 px-1 text-[10px] tabular text-muted-foreground">
              {badge}
            </Badge>
          )}
          {issueCount > 0 && (
            <Badge variant="destructive" className="h-4 px-1 text-[10px] tabular">
              {issueCount}
            </Badge>
          )}
          {desc && <span className="ml-auto hidden truncate text-[11px] text-muted-foreground md:block">{desc}</span>}
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <Separator />
        <div className="px-3 py-3">{children}</div>
      </CollapsibleContent>
    </Collapsible>
  )
}
