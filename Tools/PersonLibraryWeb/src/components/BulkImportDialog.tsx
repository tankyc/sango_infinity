/**
 * 文件名：BulkImportDialog.tsx
 * 描述：基础武将库「一键导入」弹窗（整体导入）。
 *
 *       与自建库的批量导入不同：
 *       - 保留文件中的武将 ID（基础库 ID 与游戏数据绑定，不重新编号）；
 *       - 不做重名改名（基础库本身存在合法的同名武将）；
 *       - 提供两种导入方式：
 *         merge   合并导入：文件中 ID 已存在的武将按其 ID 更新，其余作为新武将写入；
 *         replace 替换整库：先清空基础库，再写入文件中的武将。
 *
 *       文件的解析与校验以服务端为准（提交文件原始文本），
 *       本地解析仅用于预览「文件武将数 / 将新增 / 将更新 / 将移除」的统计。
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { toast } from 'sonner'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { ApiError, bulkImportPersons } from '@/lib/api'
import { cn } from '@/lib/utils'
import type { BulkImportResult, LibraryKey } from '@/lib/types'
import { AlertCircle, CheckCircle2, FileUp, Loader2, RotateCcw, Upload } from 'lucide-react'

/** 文件中单条武将的原始数据（结构未知，按普通对象处理） */
type RawPerson = Record<string, unknown>

/** 导入方式选项 */
const IMPORT_MODES: { value: 'merge' | 'replace'; label: string }[] = [
  { value: 'merge', label: '合并导入（按 ID 更新 / 新增）' },
  { value: 'replace', label: '替换整库（先清空再写入）' },
]

/**
 * 从已解析的 JSON 中提取武将原始数据（仅用于预览统计，权威解析由服务端完成）。
 * 兼容 { PersonLibrary: {...} }、{ PersonLibrary: [...] } 与纯数组三种结构。
 * @param input 已解析的 JSON 数据
 * @returns 武将列表
 */
function extractRawPersons(input: unknown): RawPerson[] {
  if (input === null || input === undefined) return []
  let container: unknown = input
  if (!Array.isArray(container) && typeof container === 'object' && container !== null) {
    const lib = (container as Record<string, unknown>).PersonLibrary
    if (lib !== undefined && lib !== null) container = lib
  }
  if (Array.isArray(container)) {
    return container.filter(
      (item): item is RawPerson => Boolean(item) && typeof item === 'object' && !Array.isArray(item),
    )
  }
  if (typeof container !== 'object' || container === null) return []
  const persons: RawPerson[] = []
  for (const key of Object.keys(container as RawPerson)) {
    // offset 是容器元数据，不是武将
    if (key === 'offset') continue
    const value = (container as RawPerson)[key]
    if (!value || typeof value !== 'object' || Array.isArray(value)) continue
    persons.push(value as RawPerson)
  }
  return persons
}

interface BulkImportDialogProps {
  /** 是否打开 */
  open: boolean
  /** 打开状态变化 */
  onOpenChange: (open: boolean) => void
  /** 是否具备写入权限（游客为 false） */
  canWrite: boolean
  /** 目标库标识（当前仅基础武将库支持整体导入） */
  lib: LibraryKey
  /** 当前库既有武将的 ID（用于统计新增 / 更新数量） */
  existingIds: number[]
  /** 当前库武将总数 */
  existingCount: number
  /** 导入成功回调（用于刷新列表） */
  onImported: (result: BulkImportResult) => void
  /** 需要登录时的回调 */
  onRequireLogin: () => void
}

export function BulkImportDialog({
  open,
  onOpenChange,
  canWrite,
  lib,
  existingIds,
  existingCount,
  onImported,
  onRequireLogin,
}: BulkImportDialogProps) {
  const [fileName, setFileName] = useState('')
  /** 文件原始文本（提交给服务端解析，保证与服务端解析结果一致） */
  const [fileText, setFileText] = useState('')
  /** 本地解析的武将列表（仅用于预览统计） */
  const [persons, setPersons] = useState<RawPerson[]>([])
  const [mode, setMode] = useState<'merge' | 'replace'>('merge')
  const [error, setError] = useState('')
  const [importing, setImporting] = useState(false)
  const [dragging, setDragging] = useState(false)
  const [result, setResult] = useState<BulkImportResult | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  // 每次打开弹窗都回到初始状态，避免残留上一次的文件与结果
  useEffect(() => {
    if (!open) return
    setFileName('')
    setFileText('')
    setPersons([])
    setMode('merge')
    setError('')
    setImporting(false)
    setDragging(false)
    setResult(null)
  }, [open])

  /** 当前库既有武将的 ID 集合（用于统计新增 / 更新数量） */
  const existingIdSet = useMemo(() => new Set(existingIds), [existingIds])

  /** 预览统计：文件武将数、将新增 / 将更新（替换整库时为将写入 / 将移除） */
  const preview = useMemo(() => {
    let added = 0
    let updated = 0
    for (const raw of persons) {
      const id = Number(raw.Id)
      if (Number.isFinite(id) && id > 0 && existingIdSet.has(id)) updated++
      else added++
    }
    return { total: persons.length, added, updated }
  }, [persons, existingIdSet])

  /** 读取并解析所选文件（本地仅做预览统计） */
  const readFile = async (file: File) => {
    setError('')
    setResult(null)
    try {
      const text = await file.text()
      const parsed = JSON.parse(text.replace(/^\uFEFF/, ''))
      const list = extractRawPersons(parsed)
      if (list.length === 0) {
        setError('文件中没有可导入的武将')
        return
      }
      setFileName(file.name)
      setFileText(text)
      setPersons(list)
    } catch (err) {
      setError(err instanceof Error ? `JSON 解析失败：${err.message}` : '文件读取失败')
    }
  }

  /** 文件选择框变化（允许重复选择同一个文件） */
  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (file) readFile(file)
  }

  /** 拖拽释放 */
  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    setDragging(false)
    const file = event.dataTransfer.files?.[0]
    if (file) readFile(file)
  }

  /** 清空已选文件（继续导入下一批） */
  const resetFile = () => {
    setFileName('')
    setFileText('')
    setPersons([])
    setResult(null)
    setError('')
  }

  /** 确认导入 */
  const handleImport = async () => {
    if (!canWrite) {
      onOpenChange(false)
      onRequireLogin()
      toast.info('游客仅可浏览，登录后才能导入武将')
      return
    }
    if (persons.length === 0 || !fileText) {
      toast.error('请先选择要导入的 JSON 文件')
      return
    }

    setError('')
    setImporting(true)
    try {
      const data = await bulkImportPersons(lib, { content: fileText, mode })
      setResult(data)
      onImported(data)
      const tip =
        data.mode === 'replace'
          ? `已替换整库：写入 ${data.added} 位、移除 ${data.removed} 位`
          : `新增 ${data.added} 位、更新 ${data.updated} 位`
      toast.success(tip)
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        toast.error('登录状态已失效，请重新登录后再试')
        onOpenChange(false)
        onRequireLogin()
        return
      }
      setError(err instanceof Error ? err.message : '导入失败')
    } finally {
      setImporting(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[min(42rem,calc(100vw-2rem))]">
        <DialogHeader className="border-b border-border pb-3">
          <DialogTitle className="flex items-center gap-2 font-display text-lg">
            <FileUp className="size-5 text-primary" />
            基础武将库 · 一键导入
          </DialogTitle>
          <DialogDescription className="text-xs">
            选择与 PersonLibrary.json 结构一致的 JSON 文件，按文件中的武将 ID
            导入（不重新编号、不改名）。建议先用「下载」导出当前数据做备份。
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4 py-3">
          {/* 文件选择 */}
          <div
            className={cn(
              'flex cursor-pointer flex-col items-center gap-2 rounded-lg border border-dashed px-4 py-6 transition-colors',
              dragging ? 'border-primary bg-primary/10' : 'border-border bg-card/40',
            )}
            onClick={() => fileInputRef.current?.click()}
            onDragOver={(e) => {
              e.preventDefault()
              setDragging(true)
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={handleDrop}
          >
            <Upload className="size-6 text-primary" />
            <p className="text-sm font-medium">{fileName || '点击选择，或把文件拖到此处'}</p>
            <p className="text-center text-xs text-muted-foreground">
              仅支持 .json 文件；当前基础库共 {existingCount} 位武将，导入时按武将 ID 比对
            </p>
            <input
              ref={fileInputRef}
              type="file"
              accept=".json,application/json"
              className="hidden"
              onChange={handleFileChange}
            />
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={(e) => {
                e.stopPropagation()
                fileInputRef.current?.click()
              }}
            >
              <FileUp />
              选择 JSON 文件
            </Button>
          </div>

          {error && (
            <div className="flex items-start gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
              <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {/* 导入方式 */}
          <div className="flex flex-col gap-2 rounded-lg border border-border bg-card/40 px-3 py-2.5">
            <span className="text-xs font-medium">导入方式</span>
            <div className="flex flex-wrap gap-1.5">
              {IMPORT_MODES.map((item) => (
                <button
                  key={item.value}
                  type="button"
                  disabled={importing}
                  onClick={() => setMode(item.value)}
                  className={cn(
                    'cursor-pointer rounded-md border px-3 py-1.5 text-xs transition-colors duration-150',
                    mode === item.value
                      ? item.value === 'replace'
                        ? 'border-destructive bg-destructive/10 text-destructive'
                        : 'border-primary bg-primary/15 text-primary'
                      : 'border-border text-muted-foreground hover:border-primary/50 hover:text-foreground',
                  )}
                >
                  {item.label}
                </button>
              ))}
            </div>
            <p
              className={cn(
                'text-[11px] leading-relaxed',
                mode === 'replace' ? 'text-destructive' : 'text-muted-foreground',
              )}
            >
              {mode === 'replace'
                ? `将先清空基础库现有 ${existingCount} 位武将，再写入文件中的武将；操作不可撤销，请确认已备份。`
                : '文件中 ID 已存在的武将按其 ID 更新，其余作为新武将写入；现有武将不会被移除。'}
            </p>
          </div>

          {/* 预览统计 */}
          {preview.total > 0 && !result && (
            <div className="grid grid-cols-3 gap-2">
              <StatTile label="文件武将数" value={preview.total} />
              <StatTile
                label={mode === 'replace' ? '将写入' : '将新增'}
                value={mode === 'replace' ? preview.total : preview.added}
              />
              <StatTile
                label={mode === 'replace' ? '将移除' : '将更新'}
                value={mode === 'replace' ? existingCount : preview.updated}
                highlight={mode === 'replace' && existingCount > 0}
              />
            </div>
          )}

          {/* 导入结果 */}
          {result && (
            <div className="flex flex-col gap-3 rounded-lg border border-primary/30 bg-primary/5 px-4 py-4">
              <div className="flex items-center gap-2">
                <CheckCircle2 className="size-5 text-primary" />
                <span className="font-display text-sm">
                  {result.mode === 'replace' ? '整库替换完成' : '导入完成'}
                </span>
              </div>
              <div className="grid grid-cols-3 gap-2">
                <StatTile label="新增" value={result.added} />
                <StatTile label="更新" value={result.updated} />
                <StatTile label="移除" value={result.removed} highlight={result.removed > 0} />
              </div>
              <p className="text-xs text-muted-foreground">
                {result.mode === 'replace' ? '替换后' : '导入后'}
                基础库共 {result.count} 位武将。
              </p>
            </div>
          )}
        </div>

        <DialogFooter className="border-t border-border pt-3">
          {result ? (
            <>
              <Button variant="outline" onClick={resetFile}>
                <RotateCcw />
                继续导入
              </Button>
              <Button onClick={() => onOpenChange(false)}>完成</Button>
            </>
          ) : (
            <>
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                取消
              </Button>
              <Button
                variant={mode === 'replace' ? 'destructive' : 'default'}
                disabled={importing || persons.length === 0}
                onClick={handleImport}
              >
                {importing ? <Loader2 className="animate-spin" /> : <FileUp />}
                {mode === 'replace'
                  ? `替换整库（写入 ${preview.total} 位）`
                  : `合并导入 ${preview.total} 位`}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** 统计小卡片 */
function StatTile({
  label,
  value,
  highlight = false,
}: {
  label: string
  value: number
  highlight?: boolean
}) {
  return (
    <div className="flex flex-col rounded-lg border border-border bg-card/60 px-3 py-2">
      <span className="text-[11px] text-muted-foreground">{label}</span>
      <span
        className={cn(
          'font-display text-lg leading-tight',
          highlight ? 'text-primary' : 'text-gold',
        )}
      >
        {value}
      </span>
    </div>
  )
}
