/**
 * 批量 / 统一编辑对话框
 *
 * 支持三种编辑范围：
 * - 已勾选的记录
 * - 当前筛选结果（即「统一编辑」）
 * - 当前集合的全部记录
 *
 * 每个被选中的字段可指定运算方式：
 * - 设为：直接覆盖为新值
 * - 增加 / 减少：在当前值基础上做增减（适用于忠诚、能力、技巧点等数值）
 *
 * 所有改动只在点击「应用」后以一个事务写入，可整批撤销。
 */
import React, { useCallback, useMemo, useState } from 'react'
import { ArrowRight, Check, Layers, Search, Sparkles, Wand2 } from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import type { CollectionKey, Entity } from '@/lib/types'
import { COLLECTION_META } from '@/lib/types'
import type { FieldDef } from '@/lib/schema'
import { COLLECTION_FIELDS, COLLECTION_GROUPS, findField } from '@/lib/schema'
import {
  buildFieldValue,
  getAbilityLevel,
  getAttrBase,
  getAttrChangeId,
  getScalarValue,
  setAbilityLevel,
  setAttr,
} from '@/lib/fields'
import { useScenarioStore } from '@/state/store'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { FieldInput } from './FieldInput'

/** 运算方式 */
type BatchMode = 'set' | 'add' | 'sub'

/** 单个字段的批量配置 */
interface BatchConfig {
  key: string
  mode: BatchMode
  value: number | string
}

interface BatchEditDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  collection: CollectionKey
  /** 当前选中的记录 id */
  targetIds: number[]
  /** 当前筛选结果条数（用于「统一编辑」选项展示） */
  filteredCount: number
  /** 切换到「筛选结果」范围 */
  onSwitchToFiltered: () => void
  /** 已选记录样本（用于预览） */
  sampleEntities: Entity[]
}

/**
 * 批量编辑对话框。
 */
export function BatchEditDialog({
  open,
  onOpenChange,
  collection,
  targetIds,
  filteredCount,
  onSwitchToFiltered,
  sampleEntities,
}: BatchEditDialogProps) {
  const store = useScenarioStore()
  const { scenario, options, batchUpdate } = store
  const meta = COLLECTION_META[collection]
  const fields = COLLECTION_FIELDS[collection]
  const groups = COLLECTION_GROUPS[collection]

  const allRows = useMemo(
    () => Object.values((scenario?.[collection] ?? {}) as Record<string, Entity>),
    [scenario, collection]
  )
  const allCount = allRows.length

  const [scope, setScope] = useState<'selection' | 'filtered' | 'all'>('selection')
  const [fieldSearch, setFieldSearch] = useState('')
  const [selectedKeys, setSelectedKeys] = useState<string[]>([])
  const [configs, setConfigs] = useState<Record<string, BatchConfig>>({})

  // 打开时同步一次范围
  React.useEffect(() => {
    if (open) {
      setScope(targetIds.length > 0 ? 'selection' : 'filtered')
      setSelectedKeys([])
      setConfigs({})
      setFieldSearch('')
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  /** 解析实际生效的记录 id */
  const resolvedIds = useMemo(() => {
    if (scope === 'selection') return targetIds
    if (scope === 'all') return allRows.map((r) => Number(r.Id))
    return targetIds
  }, [allRows, scope, targetIds])

  const resolvedRows = useMemo(() => {
    const set = (scenario?.[collection] ?? {}) as Record<string, Entity>
    if (scope === 'selection') return targetIds.map((id) => set[String(id)]).filter(Boolean)
    if (scope === 'all') return allRows
    return targetIds.map((id) => set[String(id)]).filter(Boolean)
  }, [allRows, collection, scenario, scope, targetIds])

  const previewRows = scope === 'selection' ? sampleEntities.slice(0, 3) : resolvedRows.slice(0, 3)

  /** 可批量编辑的字段（排除 Id 等） */
  const editableFields = useMemo(
    () => fields.filter((f) => !f.noBatch),
    [fields]
  )

  const filteredFields = useMemo(() => {
    const kw = fieldSearch.trim().toLowerCase()
    if (!kw) return editableFields
    return editableFields.filter(
      (f) => f.label.toLowerCase().includes(kw) || f.key.toLowerCase().includes(kw)
    )
  }, [editableFields, fieldSearch])

  const toggleField = useCallback((field: FieldDef) => {
    setSelectedKeys((prev) => {
      if (prev.includes(field.key)) {
        const next = prev.filter((k) => k !== field.key)
        setConfigs((c) => {
          const copy = { ...c }
          delete copy[field.key]
          return copy
        })
        return next
      }
      const defaultValue: number | string =
        field.kind === 'string'
          ? ''
          : field.kind === 'enum'
            ? ((field.enumMap ?? [])[0]?.value ?? 0)
            : 0
      setConfigs((c) => ({ ...c, [field.key]: { key: field.key, mode: 'set', value: defaultValue } }))
      return [...prev, field.key]
    })
  }, [])

  /**
   * 计算某条记录应用后的新值。
   *
   * @param entity 原实体
   * @param field 字段
   * @param config 配置
   * @returns 新的字段值（null 表示不修改）
   */
  const computeValue = useCallback(
    (entity: Entity, field: FieldDef, config: BatchConfig): unknown => {
      const delta = Number(config.value) || 0
      if (config.mode === 'set') {
        return buildFieldValue(entity, field, config.value)
      }
      const signedDelta = config.mode === 'add' ? delta : -delta
      switch (field.kind) {
        case 'attr': {
          const raw = entity[field.key]
          const base = getAttrBase(raw) + signedDelta
          return setAttr(raw, base, getAttrChangeId(raw))
        }
        case 'ability': {
          const raw = entity[field.key]
          return setAbilityLevel(raw, getAbilityLevel(raw) + signedDelta)
        }
        case 'int':
        case 'float':
        case 'ref':
        case 'enum': {
          const scalar = getScalarValue(entity, field)
          const base = typeof scalar === 'number' ? scalar : 0
          return buildFieldValue(entity, field, base + signedDelta)
        }
        default:
          // 字符串与数组不支持增减
          return buildFieldValue(entity, field, config.value)
      }
    },
    []
  )

  /** 应用的改动条目数（预估） */
  const changePreview = useMemo(() => {
    if (resolvedRows.length === 0 || selectedKeys.length === 0) return { entities: 0, fields: 0 }
    let affected = 0
    for (const entity of resolvedRows) {
      let changed = false
      for (const key of selectedKeys) {
        const field = findField(fields, key)
        const config = configs[key]
        if (!field || !config) continue
        const value = computeValue(entity, field, config)
        if (JSON.stringify(entity[key]) !== JSON.stringify(value)) {
          changed = true
          break
        }
      }
      if (changed) affected += 1
    }
    return { entities: affected, fields: selectedKeys.length }
  }, [computeValue, configs, fields, resolvedRows, selectedKeys])

  const handleApply = useCallback(() => {
    const active = selectedKeys
      .map((k) => ({ field: findField(fields, k), config: configs[k] }))
      .filter((x) => x.field && x.config) as { field: FieldDef; config: BatchConfig }[]

    if (active.length === 0) {
      toast.warning('请先选择要修改的字段')
      return
    }
    if (resolvedIds.length === 0) {
      toast.warning('编辑范围为 0 条记录')
      return
    }

    batchUpdate(collection, resolvedIds, (entity) => {
      const next: Entity = { ...entity }
      let changed = false
      for (const { field, config } of active) {
        const value = computeValue(entity, field, config)
        if (JSON.stringify(next[field.key]) !== JSON.stringify(value)) {
          next[field.key] = value as number | string
          changed = true
        }
      }
      return changed ? next : entity
    })

    const summary = active
      .map(({ field, config }) =>
        config.mode === 'set'
          ? `${field.label} = ${String(config.value)}`
          : `${field.label} ${config.mode === 'add' ? '+' : '-'} ${String(config.value)}`
      )
      .join('，')
    toast.success(`已对 ${resolvedIds.length} 个${meta.singular}应用：${summary}`)
    onOpenChange(false)
  }, [batchUpdate, collection, computeValue, configs, fields, meta.singular, onOpenChange, resolvedIds, selectedKeys])

  const scopeOptions = [
    { value: 'selection' as const, label: `已勾选的 ${targetIds.length} 条`, icon: Check, disabled: targetIds.length === 0 },
    { value: 'filtered' as const, label: `当前筛选结果 ${filteredCount} 条`, icon: Search, disabled: false },
    { value: 'all' as const, label: `全部 ${allCount} 条`, icon: Layers, disabled: false },
  ]

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/* sm:max-w-* 用来覆盖 DialogContent 自带的 `sm:max-w-lg`（512px），否则宽屏下会被截住 */}
      <DialogContent className="flex max-h-[92vh] w-[min(96vw,860px)] flex-col gap-0 overflow-hidden p-0 sm:max-w-[860px]">
        <DialogHeader className="shrink-0 border-b border-border px-5 py-3">
          <DialogTitle className="flex items-center gap-2 text-[16px]">
            <Wand2 className="h-4 w-4 text-primary" />
            批量编辑{meta.singular}
          </DialogTitle>
          <DialogDescription className="text-[12px]">
            一次修改多条记录的多个字段，整批操作可一次性撤销
          </DialogDescription>
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">
          {/* 1. 编辑范围 */}
          <section className="space-y-2">
            <h3 className="text-[13px] font-medium">
              <Badge variant="secondary" className="mr-2 h-5 px-1.5 tabular">
                1
              </Badge>
              编辑范围
            </h3>
            <div className="flex flex-wrap gap-2">
              {scopeOptions.map((opt) => (
                <button
                  key={opt.value}
                  type="button"
                  disabled={opt.disabled}
                  onClick={() => {
                    setScope(opt.value)
                    if (opt.value === 'filtered') onSwitchToFiltered()
                  }}
                  className={cn(
                    'flex items-center gap-1.5 rounded-md border px-3 py-1.5 text-[12px] transition-colors',
                    scope === opt.value
                      ? 'border-primary bg-primary/10 text-foreground'
                      : 'border-border text-muted-foreground hover:border-primary/50 hover:text-foreground',
                    opt.disabled && 'cursor-not-allowed opacity-40'
                  )}
                >
                  <opt.icon className="h-3.5 w-3.5" />
                  {opt.label}
                </button>
              ))}
            </div>
            <p className="text-[11px] text-muted-foreground">
              本次将作用于 <span className="tabular text-foreground">{resolvedIds.length}</span> 条记录
            </p>
          </section>

          {/* 2. 选择字段 */}
          <section className="mt-5 space-y-2">
            <h3 className="text-[13px] font-medium">
              <Badge variant="secondary" className="mr-2 h-5 px-1.5 tabular">
                2
              </Badge>
              选择要修改的字段
            </h3>
            <div className="relative">
              <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input
                className="h-8 pl-7"
                placeholder="搜索字段名，例如：忠诚、统率、BelongForce"
                value={fieldSearch}
                onChange={(e) => setFieldSearch(e.target.value)}
              />
            </div>
            <div className="max-h-[190px] overflow-y-auto rounded-md border border-border p-2">
              {groups.map((group) => {
                const groupFields = filteredFields.filter((f) => f.group === group.name)
                if (groupFields.length === 0) return null
                return (
                  <div key={group.name} className="mb-2 last:mb-0">
                    <p className="px-1 py-1 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                      {group.name}
                    </p>
                    <div className="flex flex-wrap gap-1.5">
                      {groupFields.map((f) => {
                        const active = selectedKeys.includes(f.key)
                        return (
                          <button
                            key={f.key}
                            type="button"
                            onClick={() => toggleField(f)}
                            className={cn(
                              'flex items-center gap-1 rounded border px-2 py-1 text-[11px] transition-colors',
                              active
                                ? 'border-primary bg-primary/10 text-foreground'
                                : 'border-border text-muted-foreground hover:border-primary/50 hover:text-foreground'
                            )}
                            title={f.desc ?? f.key}
                          >
                            {active && <Check className="h-3 w-3" />}
                            {f.label}
                          </button>
                        )
                      })}
                    </div>
                  </div>
                )
              })}
              {filteredFields.length === 0 && (
                <p className="py-6 text-center text-[12px] text-muted-foreground">没有匹配的字段</p>
              )}
            </div>
          </section>

          {/* 3. 设置新值 */}
          <section className="mt-5 space-y-2">
            <h3 className="text-[13px] font-medium">
              <Badge variant="secondary" className="mr-2 h-5 px-1.5 tabular">
                3
              </Badge>
              设置新值
              {selectedKeys.length > 0 && (
                <span className="ml-2 text-[11px] font-normal text-muted-foreground">
                  共 {selectedKeys.length} 个字段
                </span>
              )}
            </h3>
            {selectedKeys.length === 0 ? (
              <p className="rounded-md border border-dashed border-border py-6 text-center text-[12px] text-muted-foreground">
                请在上方选择要修改的字段
              </p>
            ) : (
              <div className="space-y-2.5">
                {selectedKeys.map((key) => {
                  const field = findField(fields, key)
                  const config = configs[key]
                  if (!field || !config) return null
                  const supportsArithmetic =
                    field.kind === 'int' || field.kind === 'float' || field.kind === 'attr' || field.kind === 'ability'
                  return (
                    <div
                      key={key}
                      className="grid grid-cols-1 items-start gap-2 rounded-md border border-border bg-card/40 p-2.5 sm:grid-cols-[150px_110px_1fr]"
                    >
                      <div className="flex items-center gap-1.5 pt-1">
                        <span className="text-[12px] font-medium">{field.label}</span>
                        <span className="font-mono text-[10px] text-muted-foreground">{field.key}</span>
                      </div>
                      <Select
                        value={config.mode}
                        onValueChange={(v) => setConfigs((c) => ({ ...c, [key]: { ...c[key], mode: v as BatchMode } }))}
                      >
                        <SelectTrigger className="h-8">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="set">设为</SelectItem>
                          <SelectItem value="add" disabled={!supportsArithmetic}>
                            增加
                          </SelectItem>
                          <SelectItem value="sub" disabled={!supportsArithmetic}>
                            减少
                          </SelectItem>
                        </SelectContent>
                      </Select>
                      <div className="min-w-0">
                        <FieldInput
                          field={field}
                          entity={resolvedRows[0] ?? null}
                          value={config.value}
                          onChange={(v) =>
                            setConfigs((c) => ({
                              ...c,
                              [key]: { ...c[key], value: (v ?? '') as number | string },
                            }))
                          }
                          options={options}
                          placeholder={config.mode === 'set' ? '新值' : '增减量'}
                        />
                      </div>
                    </div>
                  )
                })}
              </div>
            )}
          </section>

          {/* 4. 预览 */}
          {selectedKeys.length > 0 && (
            <section className="mt-5 space-y-2">
              <h3 className="text-[13px] font-medium">
                <Badge variant="secondary" className="mr-2 h-5 px-1.5 tabular">
                  4
                </Badge>
                效果预览
              </h3>
              <div className="rounded-md border border-border bg-muted/30 p-3">
                <p className="flex flex-wrap items-center gap-2 text-[12px]">
                  <Sparkles className="h-3.5 w-3.5 text-primary" />
                  将实际改动
                  <span className="tabular font-medium text-foreground">{changePreview.entities}</span>
                  条{meta.singular}的
                  <span className="tabular font-medium text-foreground">{changePreview.fields}</span>
                  个字段
                  {changePreview.entities < resolvedIds.length && (
                    <span className="text-muted-foreground">
                      （其余 {resolvedIds.length - changePreview.entities} 条内容与原值相同，将被跳过）
                    </span>
                  )}
                </p>
                {previewRows.length > 0 && (
                  <div className="mt-2 space-y-1">
                    {previewRows.slice(0, 3).map((row) => (
                      <div key={String(row.Id)} className="flex flex-wrap items-center gap-1.5 text-[11px]">
                        <span className="shrink-0 text-muted-foreground">
                          {String(row.Name ?? `#${row.Id}`)}
                        </span>
                        {selectedKeys.slice(0, 3).map((key) => {
                          const field = findField(fields, key)
                          const config = configs[key]
                          if (!field || !config) return null
                          const before = getScalarValue(row, field)
                          const after = computeValue(row, field, config)
                          const afterText = Array.isArray(after)
                            ? after.join(',')
                            : String(after ?? '')
                          return (
                            <span
                              key={key}
                              className="flex items-center gap-1 rounded bg-background px-1.5 py-0.5 font-mono"
                            >
                              <span className="text-muted-foreground">{field.label}</span>
                              <span>{String(before)}</span>
                              <ArrowRight className="h-3 w-3 text-primary" />
                              <span className="text-primary">{afterText}</span>
                            </span>
                          )
                        })}
                        {selectedKeys.length > 3 && (
                          <span className="text-muted-foreground">…</span>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </section>
          )}
        </div>

        <DialogFooter className="shrink-0 border-t border-border px-5 py-3">
          <div className="mr-auto text-[11px] text-muted-foreground">
            整批修改仅占用一步撤销栈
          </div>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            取消
          </Button>
          <Button
            onClick={handleApply}
            disabled={selectedKeys.length === 0 || resolvedIds.length === 0}
            className="gap-1.5"
          >
            <Check className="h-4 w-4" />
            应用到 {resolvedIds.length} 条
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
