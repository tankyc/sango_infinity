/**
 * 快捷命令面板（Ctrl/Cmd + K）
 *
 * 聚合两类入口：
 * - 全局动作：保存、撤销、重做、重新加载、打开校验、切换集合
 * - 实体检索：跨四个集合按名称 / Id 搜索，回车直接跳转到对应实体
 *
 * 结果上限受控并做分组展示，保证在 850+ 武将规模下依旧即时响应。
 */
import { useEffect, useMemo, useState } from 'react'
import { Command as CommandIcon, Save, Undo2, Redo2, RefreshCw, ShieldCheck } from 'lucide-react'
import type { CollectionKey, Entity } from '@/lib/types'
import { COLLECTION_META, COLLECTION_ORDER } from '@/lib/types'
import { entitySearchText } from '@/lib/indexes'
import { useScenarioStore } from '@/state/store'
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from '@/components/ui/command'

interface CommandPaletteProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** 跳转到实体 */
  onNavigate: (collection: CollectionKey, entityId: number) => void
  /** 打开校验面板 */
  onGoValidation: () => void
}

/**
 * 快捷命令面板。
 */
export function CommandPalette({ open, onOpenChange, onNavigate, onGoValidation }: CommandPaletteProps) {
  const store = useScenarioStore()
  const { scenario, dirty, canUndo, canRedo, save, undo, redo, reload } = store
  const [keyword, setKeyword] = useState('')

  useEffect(() => {
    if (!open) setKeyword('')
  }, [open])

  /** 各集合的检索结果（每组最多 8 条，保证即时性） */
  const results = useMemo(() => {
    const kw = keyword.trim().toLowerCase()
    if (!scenario || kw.length === 0) return []
    const terms = kw.split(/\s+/)
    const out: { collection: CollectionKey; items: Entity[]; more: number }[] = []
    for (const key of COLLECTION_ORDER) {
      const set = Object.values((scenario[key] ?? {}) as Record<string, Entity>)
      const hits: Entity[] = []
      let total = 0
      for (const item of set) {
        const hay = entitySearchText(item)
        if (terms.every((t) => hay.includes(t))) {
          total += 1
          if (hits.length < 8) hits.push(item)
        }
      }
      if (hits.length > 0) out.push({ collection: key, items: hits, more: Math.max(0, total - hits.length) })
    }
    return out
  }, [keyword, scenario])

  const runAction = (fn: () => void) => {
    onOpenChange(false)
    // 关闭动画结束后再执行，避免焦点抢占
    window.setTimeout(fn, 60)
  }

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange}>
      <CommandInput
        placeholder="搜索武将 / 都市 / 军团 / 势力，或输入命令…"
        value={keyword}
        onValueChange={setKeyword}
      />
      <CommandList className="max-h-[420px]">
        <CommandEmpty>没有匹配的结果</CommandEmpty>

        {keyword.trim().length === 0 && (
          <>
            <CommandGroup heading="常用操作">
              <CommandItem
                value="action-save"
                onSelect={() => runAction(() => void save())}
                disabled={!dirty}
              >
                <Save className="mr-2 h-4 w-4" />
                保存剧本
                <CommandShortcut>{dirty ? 'Ctrl S' : '无改动'}</CommandShortcut>
              </CommandItem>
              <CommandItem value="action-undo" onSelect={() => runAction(undo)} disabled={!canUndo}>
                <Undo2 className="mr-2 h-4 w-4" />
                撤销
                <CommandShortcut>Ctrl Z</CommandShortcut>
              </CommandItem>
              <CommandItem value="action-redo" onSelect={() => runAction(redo)} disabled={!canRedo}>
                <Redo2 className="mr-2 h-4 w-4" />
                重做
                <CommandShortcut>Ctrl Y</CommandShortcut>
              </CommandItem>
              <CommandItem value="action-reload" onSelect={() => runAction(() => void reload())}>
                <RefreshCw className="mr-2 h-4 w-4" />
                从磁盘重新加载
              </CommandItem>
              <CommandItem value="action-validation" onSelect={() => runAction(onGoValidation)}>
                <ShieldCheck className="mr-2 h-4 w-4" />
                查看校验结果
                <CommandShortcut>{store.issueCounts.error + store.issueCounts.warning} 项</CommandShortcut>
              </CommandItem>
            </CommandGroup>
            <CommandSeparator />
            <CommandGroup heading="快速跳转">
              {COLLECTION_ORDER.map((key) => {
                const count = Object.keys((scenario?.[key] ?? {}) as Record<string, unknown>).length
                return (
                  <CommandItem
                    key={key}
                    value={`nav-${key}`}
                    onSelect={() => runAction(() => onNavigate(key, 0))}
                  >
                    <CommandIcon className="mr-2 h-4 w-4" />
                    {COLLECTION_META[key].label}列表
                    <CommandShortcut>{count} 条</CommandShortcut>
                  </CommandItem>
                )
              })}
            </CommandGroup>
          </>
        )}

        {results.map((group) => (
          <CommandGroup key={group.collection} heading={COLLECTION_META[group.collection].label}>
            {group.items.map((item) => (
              <CommandItem
                key={`${group.collection}-${item.Id}`}
                value={`${group.collection}-${item.Id}-${String(item.Name ?? '')}`}
                onSelect={() => runAction(() => onNavigate(group.collection, Number(item.Id)))}
              >
                <span className="tabular mr-2 w-12 shrink-0 text-muted-foreground">{String(item.Id)}</span>
                <span className="truncate">{String(item.Name ?? '（未命名）')}</span>
                {typeof item.nickName === 'string' && item.nickName && (
                  <span className="ml-2 shrink-0 text-[11px] text-muted-foreground">{item.nickName}</span>
                )}
                {group.more > 0 && group.items.indexOf(item) === group.items.length - 1 && (
                  <span className="ml-auto shrink-0 text-[10px] text-muted-foreground">
                    另有 {group.more} 条
                  </span>
                )}
              </CommandItem>
            ))}
          </CommandGroup>
        ))}
      </CommandList>
    </CommandDialog>
  )
}
