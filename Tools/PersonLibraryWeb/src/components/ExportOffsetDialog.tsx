/**
 * 文件名：ExportOffsetDialog.tsx
 * 描述：自建武将库的「偏移导出」弹窗。
 *       把自建武将的 ID 整体平移到指定号段后导出（原起始号 → 新起始号）：
 *       例如当前 ID 1000 ~ 1649、起始 ID 填 10000，则导出为 10000 ~ 10649，
 *       容器 offset 也改为 10000，便于把武将放入目标 Mod 的 ID 区间，
 *       避免与目标环境已有的 ID 冲突。
 *
 *       关系字段（父亲 / 母亲 / 兄弟 / 配偶 / 兄弟列表 / 亲近 / 厌恶）会同步平移，
 *       保证导出后人物关系仍然正确；指向基础武将库的 ID（ID < 1000）
 *       与特性 ID、头像 ID 保持不变。
 */

import { useEffect, useMemo, useState } from 'react'
import { toast } from 'sonner'
import { ArrowRight, Download } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { downloadLibrary } from '@/lib/api'
import { PERSON_REF_FIELDS, PERSON_REF_LIST_FIELDS } from '@/lib/enums'
import type { Person } from '@/lib/types'

interface ExportOffsetDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** 当前自建库的武将列表 */
  persons: Person[]
  /** 当前自建库起始 ID（容器 offset） */
  baseId: number
  /** 导出文件名（不含 .json 后缀），偏移导出时会追加 _起始ID */
  fileBaseName: string
}

/**
 * 读取武将的字段值（字段名可变，这里统一按字符串索引访问）。
 * @param person 武将
 * @param key 字段名
 * @returns 字段值
 */
function valueOf(person: Person, key: string): unknown {
  return (person as unknown as Record<string, unknown>)[key]
}

export function ExportOffsetDialog({
  open,
  onOpenChange,
  persons,
  baseId,
  fileBaseName,
}: ExportOffsetDialogProps) {
  /** 目标起始 ID（默认与当前起始号一致，即不改变号段） */
  const [startText, setStartText] = useState(String(baseId))

  // 每次打开都回到当前起始号，避免沿用上一次的输入
  useEffect(() => {
    if (open) setStartText(String(baseId))
  }, [open, baseId])

  const startId = Number(startText.trim())
  const valid = Number.isInteger(startId) && startId > 0

  /** 导出预览：号段、偏移量、会同步平移关系的武将数 */
  const stats = useMemo(() => {
    if (persons.length === 0) return null
    const ids = new Set(persons.map((p) => p.Id))
    const minId = Math.min(...ids)
    const maxId = Math.max(...ids)
    const delta = valid ? startId - baseId : 0
    // 只有「指向本库武将」的关系才需要平移，故按引用是否落在本库内统计
    const refCount = persons.filter((p) => {
      const hasScalar = PERSON_REF_FIELDS.some((key) => ids.has(Number(valueOf(p, key))))
      const hasList = PERSON_REF_LIST_FIELDS.some((key) => {
        const value = valueOf(p, key)
        return Array.isArray(value) && value.some((item) => ids.has(Number(item)))
      })
      return hasScalar || hasList
    }).length
    return {
      count: persons.length,
      minId,
      maxId,
      delta,
      newMin: minId + delta,
      newMax: maxId + delta,
      refCount,
    }
  }, [persons, baseId, startId, valid])

  /** 导出文件名：号段不变时沿用原文件名 */
  const fileName =
    valid && stats && stats.delta !== 0 ? `${fileBaseName}_${startId}.json` : `${fileBaseName}.json`

  /** 开始下载 */
  const handleDownload = () => {
    if (!valid) return
    downloadLibrary('custom', fileName, startId)
    toast.success(`已开始下载 ${fileName}`)
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[min(34rem,calc(100vw-2rem))]">
        <DialogHeader className="border-b border-border pb-3">
          <DialogTitle className="flex items-center gap-2 font-display text-lg">
            <Download className="size-5 text-primary" />
            自建武将库 · 偏移导出
          </DialogTitle>
          <DialogDescription className="text-xs">
            把自建武将的 ID 整体平移到指定号段后导出，便于放入目标 Mod 的 ID 区间。
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4 py-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="export-start-id" className="text-xs text-muted-foreground">
              导出起始 ID（当前起始号为 {baseId}，编号最小的武将将被编为这个值）
            </Label>
            <Input
              id="export-start-id"
              inputMode="numeric"
              value={startText}
              placeholder={String(baseId)}
              onChange={(e) => setStartText(e.target.value.replace(/[^\d]/g, ''))}
            />
            {!valid && <p className="text-[11px] text-destructive">请输入大于 0 的整数</p>}
          </div>

          {stats ? (
            <div className="flex flex-col gap-2.5 rounded-lg border border-border bg-card/40 p-3 text-xs">
              <div className="flex flex-wrap items-center gap-2">
                <span className="w-16 text-muted-foreground">武将 ID</span>
                <Badge variant="outline" className="font-mono text-[10px]">
                  {stats.minId} ~ {stats.maxId}
                </Badge>
                <ArrowRight className="size-3 text-muted-foreground" />
                <Badge variant="outline" className="border-primary/50 font-mono text-[10px] text-primary">
                  {valid ? `${stats.newMin} ~ ${stats.newMax}` : '—'}
                </Badge>
                <span className="text-muted-foreground">共 {stats.count} 位</span>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <span className="w-16 text-muted-foreground">容器 offset</span>
                <Badge variant="outline" className="font-mono text-[10px]">
                  {baseId}
                </Badge>
                <ArrowRight className="size-3 text-muted-foreground" />
                <Badge variant="outline" className="border-primary/50 font-mono text-[10px] text-primary">
                  {valid ? startId : '—'}
                </Badge>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <span className="w-16 text-muted-foreground">文件名</span>
                <span className="font-mono text-[11px]">{fileName}</span>
              </div>

              <p className="text-[11px] leading-relaxed text-muted-foreground">
                其中 {stats.refCount} 位武将的关系字段（父亲 / 母亲 / 兄弟 / 配偶 / 亲近 /
                厌恶）会同步偏移；指向基础武将库的 ID、特性 ID 与头像 ID 保持不变。
              </p>

              {stats.delta === 0 && (
                <p className="text-[11px] text-muted-foreground">
                  起始 ID 与当前一致，导出的编号不会变化。
                </p>
              )}
              {stats.delta !== 0 && valid && stats.newMin < 1000 && (
                <p className="text-[11px] text-gold">
                  目标号段小于 1000，会与基础武将库的 ID 区间重叠，请确认目标环境。
                </p>
              )}
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">
              当前库还没有武将，导出的是空库（仅包含容器 offset）。
            </p>
          )}
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            取消
          </Button>
          <Button disabled={!valid} onClick={handleDownload}>
            <Download />
            下载 JSON
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
