/**
 * 文件名：CustomFaceUsageDialog.tsx
 * 描述：自定义头像「引用武将」信息面板。
 *
 *       展示某个自定义头像的完整信息（半身像 / 小头像 / ID / 性别号段 / 上传者 / 时间）
 *       以及正在引用它的武将清单：
 *       - 可按所属库筛选、按姓名或 ID 搜索；
 *       - 可逐个「打开武将」（需要外部提供跳转回调），便于删改头像前确认影响范围；
 *       - 可一键复制名单。
 *
 *       引用口径：武将的 headIconID 字段，基础武将库与自建武将库合并统计。
 */

import { useEffect, useMemo, useState } from 'react'
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
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { fetchCustomFaceUsage } from '@/lib/api'
import { headIconUrl } from '@/lib/enums'
import { headRangeText } from '@/lib/faceRules'
import type { CustomFace, FaceUsagePerson } from '@/lib/types'
import { Copy, ExternalLink, Loader2, Users } from 'lucide-react'

/**
 * 复制文本到剪贴板。
 * http 站点下 navigator.clipboard 不可用（非安全上下文），因此保留 execCommand 兜底。
 * @param text 待复制文本
 * @returns 是否复制成功
 */
async function copyText(text: string): Promise<boolean> {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text)
      return true
    }
  } catch {
    // 继续走兜底方案
  }
  try {
    const area = document.createElement('textarea')
    area.value = text
    area.style.position = 'fixed'
    area.style.top = '-1000px'
    area.style.opacity = '0'
    document.body.appendChild(area)
    area.select()
    const ok = document.execCommand('copy')
    document.body.removeChild(area)
    return ok
  } catch {
    return false
  }
}

/**
 * 把 ISO 时间格式化为本地可读字符串。
 * @param value ISO 时间
 * @returns 显示文本
 */
function formatTime(value: string | null | undefined): string {
  if (!value) return '—'
  const time = new Date(value)
  if (Number.isNaN(time.getTime())) return '—'
  return time.toLocaleString()
}

interface CustomFaceUsageDialogProps {
  /** 是否打开 */
  open: boolean
  /** 打开状态变化 */
  onOpenChange: (open: boolean) => void
  /** 目标头像；为空时不渲染内容 */
  face: CustomFace | null
  /**
   * 「打开武将」回调；未提供时隐藏该按钮
   * （从武将编辑界面打开头像时没有跳转上下文，只做查看）。
   */
  onOpenPerson?: (lib: string, personId: number) => void
}

export function CustomFaceUsageDialog({
  open,
  onOpenChange,
  face,
  onOpenPerson,
}: CustomFaceUsageDialogProps) {
  /** 引用该头像的武将（null 表示尚未返回） */
  const [usage, setUsage] = useState<FaceUsagePerson[] | null>(null)
  const [loading, setLoading] = useState(false)
  /** 库筛选：all / base / custom */
  const [libFilter, setLibFilter] = useState('all')
  /** 姓名或 ID 关键字 */
  const [keyword, setKeyword] = useState('')

  useEffect(() => {
    if (!open || !face) {
      setUsage(null)
      return
    }
    setLibFilter('all')
    setKeyword('')
    let cancelled = false
    const load = async () => {
      setLoading(true)
      try {
        const result = await fetchCustomFaceUsage(face.id)
        if (!cancelled) setUsage(result.persons)
      } catch (err) {
        if (!cancelled) {
          toast.error(err instanceof Error ? err.message : '读取引用武将失败')
          setUsage([])
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    void load()
    return () => {
      cancelled = true
    }
  }, [open, face])

  /** 按库与关键字筛选后的武将列表 */
  const persons = useMemo(() => {
    const list = usage ?? []
    const text = keyword.trim().toLowerCase()
    return list.filter((item) => {
      if (libFilter !== 'all' && item.lib !== libFilter) return false
      if (!text) return true
      return item.name.toLowerCase().includes(text) || String(item.id).includes(text)
    })
  }, [usage, libFilter, keyword])

  /** 各库的数量统计（用于筛选下拉展示） */
  const counts = useMemo(() => {
    const list = usage ?? []
    return {
      all: list.length,
      base: list.filter((item) => item.lib === 'base').length,
      custom: list.filter((item) => item.lib === 'custom').length,
    }
  }, [usage])

  /** 复制当前筛选结果（姓名 + ID + 所属库） */
  const handleCopy = async () => {
    if (persons.length === 0) return
    const text = persons.map((item) => `${item.name}\t${item.id}\t${item.libLabel}`).join('\n')
    const ok = await copyText(text)
    if (ok) toast.success(`已复制 ${persons.length} 位武将名单`)
    else toast.error('复制失败，请手动选择文本复制')
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[92vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-2xl">
        <DialogHeader className="border-b border-border px-5 py-4">
          <DialogTitle className="flex items-center gap-2 font-display text-lg">
            <Users className="size-5 text-primary" />
            引用该头像的武将
            {face && <span className="text-sm text-muted-foreground">ID {face.id}</span>}
          </DialogTitle>
          <DialogDescription className="text-xs">
            按武将的头像字段（headIconID）统计，基础武将库与自建武将库合并展示；
            删除或改变该头像 ID 前，可在这里确认会影响到哪些武将。
          </DialogDescription>
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-y-auto p-5">
          {/* 头像信息 */}
          {face && (
            <div className="flex flex-wrap items-start gap-4 rounded-lg border border-border bg-card/40 p-4">
              <div className="flex items-end gap-3">
                <div className="flex flex-col items-center gap-1">
                  <img
                    src={`${headIconUrl(face.id, 1)}?v=${face.version}`}
                    alt={`半身像 ${face.id}`}
                    className="size-24 rounded-md border border-border object-cover object-top"
                  />
                  <span className="text-[10px] text-muted-foreground">半身像 240×240</span>
                </div>
                <div className="flex flex-col items-center gap-1">
                  <img
                    src={`${headIconUrl(face.id, 2)}?v=${face.version}`}
                    alt={`小头像 ${face.id}`}
                    className="h-20 w-[4rem] rounded-md border border-border object-cover"
                  />
                  <span className="text-[10px] text-muted-foreground">小头像 64×80</span>
                </div>
              </div>

              <div className="grid min-w-[15rem] flex-1 grid-cols-2 gap-x-4 gap-y-1.5 text-[11px]">
                <span className="text-muted-foreground">头像 ID</span>
                <span className="font-medium text-foreground">{face.id}</span>
                <span className="text-muted-foreground">性别号段</span>
                <span className="text-foreground">
                  {face.sex === 0 ? '男性' : '女性'}
                  <span className="ml-1 text-muted-foreground">（{headRangeText(face.sex)}）</span>
                </span>
                <span className="text-muted-foreground">上传者</span>
                <span className="text-foreground">
                  {face.uploaderName || face.uploader || '未记录上传者'}
                </span>
                <span className="text-muted-foreground">创建时间</span>
                <span className="text-foreground">{formatTime(face.createdAt)}</span>
                <span className="text-muted-foreground">最后修改</span>
                <span className="text-foreground">{formatTime(face.updatedAt)}</span>
                <span className="text-muted-foreground">被引用</span>
                <span className="font-medium text-gold">
                  {loading ? '统计中…' : `${counts.all} 位武将`}
                </span>
              </div>
            </div>
          )}

          {/* 筛选 */}
          <div className="mt-4 flex flex-wrap items-center gap-2">
            <Select value={libFilter} onValueChange={setLibFilter}>
              <SelectTrigger className="h-8 w-[11rem] text-xs" aria-label="按所属库筛选">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">全部库（{counts.all}）</SelectItem>
                <SelectItem value="base">基础武将库（{counts.base}）</SelectItem>
                <SelectItem value="custom">自建武将库（{counts.custom}）</SelectItem>
              </SelectContent>
            </Select>
            <Input
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              placeholder="搜索姓名或武将 ID"
              className="h-8 flex-1 text-xs"
            />
            <span className="text-[11px] text-muted-foreground">显示 {persons.length} 位</span>
          </div>

          {/* 武将清单 */}
          <div className="mt-3 flex flex-col gap-1.5">
            {loading && (
              <p className="flex items-center gap-2 py-6 text-xs text-muted-foreground">
                <Loader2 className="size-3.5 animate-spin" />
                正在统计引用武将…
              </p>
            )}

            {!loading && (usage?.length ?? 0) === 0 && (
              <p className="py-6 text-center text-xs text-muted-foreground">
                暂无武将引用该头像，删除或改变其 ID 不会影响任何武将。
              </p>
            )}

            {!loading && (usage?.length ?? 0) > 0 && persons.length === 0 && (
              <p className="py-6 text-center text-xs text-muted-foreground">没有符合条件的武将</p>
            )}

            {!loading &&
              persons.map((item) => (
                <div
                  key={`${item.lib}-${item.id}`}
                  className={cn(
                    'flex items-center gap-3 rounded-md border border-border bg-card/40 px-3 py-2',
                    'transition-colors hover:border-primary/50',
                  )}
                >
                  <span className="w-12 shrink-0 font-mono text-[11px] text-muted-foreground">
                    {item.id}
                  </span>
                  <span className="min-w-0 flex-1 truncate text-sm">{item.name || '（未命名）'}</span>
                  <Badge variant="secondary" className="shrink-0 text-[10px]">
                    {item.sex === 0 ? '男' : item.sex === 1 ? '女' : '性别未知'}
                  </Badge>
                  <Badge variant="outline" className="shrink-0 text-[10px]">
                    {item.libLabel}
                  </Badge>
                  {onOpenPerson && (
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      className="h-7 shrink-0 px-2 text-[11px]"
                      title={`打开「${item.name}」的详情`}
                      onClick={() => onOpenPerson(item.lib, item.id)}
                    >
                      <ExternalLink />
                      打开武将
                    </Button>
                  )}
                </div>
              ))}
          </div>
        </div>

        <DialogFooter className="flex-row items-center justify-between gap-2 border-t border-border px-5 py-3 sm:justify-between">
          <span className="text-xs text-muted-foreground">
            {loading ? '统计中…' : `共 ${counts.all} 位武将引用该头像`}
          </span>
          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={persons.length === 0}
              onClick={handleCopy}
            >
              <Copy />
              复制名单
            </Button>
            <Button type="button" size="sm" onClick={() => onOpenChange(false)}>
              关闭
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
