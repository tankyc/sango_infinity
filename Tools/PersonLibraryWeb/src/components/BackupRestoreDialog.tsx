/**
 * 文件名：BackupRestoreDialog.tsx
 * 描述：数据备份与还原弹窗（超级管理员专属，需要 backup 权限）。
 *       - 展示每日自动备份的调度状态（每天几点执行、保留多少天）；
 *       - 列出历史备份（时间 / 原因 / 两库条数 / 体积），支持「立即备份」；
 *       - 支持用某份备份还原：会二次确认，且服务端在还原前自动创建一份
 *         「还原前快照」，因此还原操作本身可回滚。
 *       备份范围：两个武将库、账号、编辑选项、姓名索引与 ID 序列；
 *       自制头像图片（server/face）体积过大，不在备份范围内。
 */

import { useCallback, useEffect, useMemo, useState } from 'react'
import { toast } from 'sonner'
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { ApiError, createBackup, fetchBackups, restoreBackup } from '@/lib/api'
import type { BackupEntry, BackupStatus } from '@/lib/types'
import { AlertCircle, DatabaseBackup, History, Loader2, RefreshCw, RotateCcw } from 'lucide-react'

interface BackupRestoreDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** 还原完成回调（父组件据此重新加载武将库与身份信息） */
  onRestored?: () => void
  /** 未登录 / 无权限时的提示回调（例如 401 时弹出登录框） */
  onRequireLogin?: () => void
}

/** 备份原因显示文案 */
const REASON_LABELS: Record<string, string> = {
  daily: '每日自动',
  manual: '手动备份',
  'pre-restore': '还原前快照',
}

/**
 * 备份原因对应的徽章样式。
 * @param reason 备份原因
 * @returns 徽章 className
 */
function reasonClass(reason: string): string {
  if (reason === 'pre-restore') return 'border-gold/50 text-gold'
  if (reason === 'manual') return 'border-primary/50 text-primary'
  return 'border-border text-muted-foreground'
}

/**
 * 格式化文件体积。
 * @param bytes 字节数
 * @returns 可读体积
 */
function formatSize(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 KB'
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`
}

/**
 * 格式化备份时间（按浏览器本地时区）。
 * @param iso ISO 8601 时间
 * @returns 可读时间（解析失败时原样返回）
 */
function formatTime(iso: string): string {
  const time = new Date(iso)
  if (Number.isNaN(time.getTime())) return iso
  return time.toLocaleString('zh-CN', { hour12: false })
}

export function BackupRestoreDialog({
  open,
  onOpenChange,
  onRestored,
  onRequireLogin,
}: BackupRestoreDialogProps) {
  const [backups, setBackups] = useState<BackupEntry[]>([])
  const [status, setStatus] = useState<BackupStatus | null>(null)
  const [loading, setLoading] = useState(false)
  const [backingUp, setBackingUp] = useState(false)
  /** 待还原的备份 */
  const [restoreTarget, setRestoreTarget] = useState<BackupEntry | null>(null)
  const [restoring, setRestoring] = useState(false)
  const [error, setError] = useState('')

  /**
   * 加载备份列表。
   */
  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const data = await fetchBackups()
      setBackups(data.backups)
      setStatus(data.status)
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        onOpenChange(false)
        onRequireLogin?.()
        return
      }
      setError(err instanceof Error ? err.message : '加载备份列表失败')
    } finally {
      setLoading(false)
    }
  }, [onOpenChange, onRequireLogin])

  // 每次打开时刷新列表
  useEffect(() => {
    if (open) void load()
  }, [open, load])

  /** 备份调度说明文案 */
  const scheduleText = useMemo(() => {
    if (!status) return ''
    return `每天 ${String(status.hour).padStart(2, '0')}:00 后自动备份一次，保留最近 ${status.keepDays} 天（最多 ${status.maxSets} 份），当前 ${status.total} 份`
  }, [status])

  /** 立即备份 */
  const handleBackup = async () => {
    setBackingUp(true)
    try {
      const data = await createBackup()
      setStatus(data.status)
      toast.success(
        `已创建备份 ${data.backup.id}（基础库 ${data.backup.counts.base?.count ?? 0} / 自建库 ${data.backup.counts.custom?.count ?? 0}）`,
      )
      await load()
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        onOpenChange(false)
        onRequireLogin?.()
        return
      }
      toast.error(err instanceof Error ? err.message : '备份失败')
    } finally {
      setBackingUp(false)
    }
  }

  /** 确认还原 */
  const handleRestore = async () => {
    if (!restoreTarget) return
    setRestoring(true)
    try {
      const result = await restoreBackup(restoreTarget.id)
      toast.success(`已还原 ${restoreTarget.id}（还原前快照：${result.snapshotId}）`, {
        description: result.message,
      })
      setRestoreTarget(null)
      await load()
      onRestored?.()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : '还原失败')
    } finally {
      setRestoring(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[min(52rem,calc(100vw-2rem))]">
        <DialogHeader className="border-b border-border pb-3">
          <DialogTitle className="flex items-center gap-2 font-display text-lg">
            <DatabaseBackup className="size-5 text-primary" />
            备份与还原
          </DialogTitle>
          <DialogDescription className="text-xs">
            {scheduleText || '加载中…'}
            ；备份包含两个武将库、账号、编辑选项与索引；自制头像图片（face 目录）需单独备份。
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3 py-1">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <History className="size-4" />
              共 {backups.length} 份备份
              {status?.latestId && <span className="font-mono text-[11px]">（最新：{status.latestId}）</span>}
            </div>
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" onClick={() => void load()} disabled={loading}>
                {loading ? <Loader2 className="animate-spin" /> : <RefreshCw />}
                刷新
              </Button>
              <Button size="sm" onClick={handleBackup} disabled={backingUp}>
                {backingUp ? <Loader2 className="animate-spin" /> : <DatabaseBackup />}
                立即备份
              </Button>
            </div>
          </div>

          {error && (
            <div className="flex items-center gap-2 rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              <AlertCircle className="size-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <ScrollArea className="h-[22rem] rounded-lg border border-border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[11rem]">备份时间</TableHead>
                  <TableHead className="w-[6.5rem]">来源</TableHead>
                  <TableHead>两库条数</TableHead>
                  <TableHead className="w-[5.5rem]">体积</TableHead>
                  <TableHead className="w-[6rem] text-right">操作</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {backups.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} className="py-8 text-center text-xs text-muted-foreground">
                      {loading ? '正在加载备份列表…' : '暂无备份，可点击「立即备份」创建第一份'}
                    </TableCell>
                  </TableRow>
                )}
                {backups.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell>
                      <div className="flex flex-col">
                        <span className="text-xs">{formatTime(item.createdAt)}</span>
                        <span className="font-mono text-[10px] text-muted-foreground">{item.id}</span>
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline" className={`text-[10px] ${reasonClass(item.reason)}`}>
                        {REASON_LABELS[item.reason] || item.reason}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      基础 {item.counts?.base?.count ?? '—'} / 自建 {item.counts?.custom?.count ?? '—'}
                      {item.counts?.base?.dataVersion ? (
                        <span className="ml-1 text-[10px]">（结构 v{item.counts.base.dataVersion}）</span>
                      ) : null}
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {formatSize(item.totalSize)}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-gold"
                        onClick={() => setRestoreTarget(item)}
                      >
                        <RotateCcw />
                        还原
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </ScrollArea>
        </div>

        {/* 还原二次确认 */}
        <AlertDialog open={Boolean(restoreTarget)} onOpenChange={(v) => !v && setRestoreTarget(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>还原备份 {restoreTarget?.id}</AlertDialogTitle>
              <AlertDialogDescription>
                将用这份备份覆盖当前的武将库、账号与选项配置（自制头像图片不受影响）。
                服务端会先自动创建一份「还原前快照」{restoreTarget ? `（${formatTime(restoreTarget.createdAt)}）` : ''}，
                万一还原结果不符合预期，可以用该快照再还原回来。
                {restoreTarget?.counts?.custom?.count !== undefined && (
                  <>
                    <br />
                    该备份内容：基础库 {restoreTarget?.counts?.base?.count ?? '—'} 位、自建库{' '}
                    {restoreTarget?.counts?.custom?.count ?? '—'} 位。
                  </>
                )}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={restoring}>取消</AlertDialogCancel>
              <AlertDialogAction
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                disabled={restoring}
                onClick={(e) => {
                  e.preventDefault()
                  void handleRestore()
                }}
              >
                {restoring ? <Loader2 className="animate-spin" /> : <RotateCcw />}
                确认还原
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </DialogContent>
    </Dialog>
  )
}
