/**
 * 文件名：ExportModDialog.tsx
 * 描述：把选中的武将（连同他们用到的自制头像）打成一个可以直接上传到创意工坊的模组包。
 *
 *       包内结构由服务端生成：
 *         mod.info                 只写 name / version / description
 *         Data/CustomPerson.json   选中的武将，ID 已重编号到模组的 10001 号段
 *         Data/FaceConfig.json     登记用到的自制头像号段（否则游戏内"容貌选择"里选不到）
 *         Assets/Face/{id}_1.png   立绘
 *         Assets/Face/{id}_2.png   头像
 */

import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { Boxes, Download } from 'lucide-react'
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
import { exportModPack } from '@/lib/api'
import { CUSTOM_FACE_BASE_ID } from '@/lib/faceRules'
import { WORKSHOP_UPLOAD_URL } from '@/lib/siteLinks'
import type { LibraryKey, Person } from '@/lib/types'

/** 模组内武将 ID 号段的起点（与游戏端 ModScenarioAddon 的 offset 对应） */
const MOD_PERSON_ID_START = 10001

interface ExportModDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** 当前库标识 */
  lib: LibraryKey
  /** 当前库全部武将（用于取出选中项） */
  persons: Person[]
  /** 已选中的武将 ID */
  selectedIds: number[]
}

/**
 * 取武将引用的自制头像 ID。
 * headIconID 为头像编号：小于 3000 的是游戏本体容貌，不需要打进包里。
 * @param person 武将
 * @returns 自制头像 ID（非自制返回 0）
 */
function customFaceIdOf(person: Person): number {
  const id = Number(person.headIconID) || 0
  return id >= CUSTOM_FACE_BASE_ID ? id : 0
}

export function ExportModDialog({ open, onOpenChange, lib, persons, selectedIds }: ExportModDialogProps) {
  const [name, setName] = useState('')
  const [version, setVersion] = useState('1.0')
  const [description, setDescription] = useState('')
  const [busy, setBusy] = useState(false)

  // 每次打开重置：名称给个默认值，版本回到 1.0，避免沿用上次的输入
  useEffect(() => {
    if (open) {
      setName('自建武将包')
      setVersion('1.0')
      setDescription('')
      setBusy(false)
    }
  }, [open])

  const selected = persons.filter((p) => selectedIds.includes(p.Id))
  const faceIds = [...new Set(selected.map(customFaceIdOf).filter((id) => id > 0))]
  const valid = selected.length > 0 && name.trim() !== '' && version.trim() !== ''

  /** 生成并下载模组包 */
  const handleExport = async () => {
    if (!valid || busy) return
    setBusy(true)
    try {
      const blob = await exportModPack(lib, {
        ids: selected.map((p) => p.Id),
        name: name.trim(),
        version: version.trim(),
        description: description.trim(),
      })
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `${name.trim() || 'person_mod_pack'}.zip`
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      URL.revokeObjectURL(url)
      toast.success('模组包已生成，到创意工坊发布页上传它即可', {
        action: {
          label: '去上传',
          // 新窗口打开：不打断当前页面，用户可能还要接着导出下一批
          onClick: () => window.open(WORKSHOP_UPLOAD_URL, '_blank', 'noopener'),
        },
      })
      onOpenChange(false)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : '生成失败')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[min(34rem,calc(100vw-2rem))]">
        <DialogHeader className="border-b border-border pb-3">
          <DialogTitle className="flex items-center gap-2 font-display text-lg">
            <Boxes className="size-5 text-primary" />
            生成武将模组包
          </DialogTitle>
          <DialogDescription className="text-xs">
            把选中的武将连同他们用到的自制头像打包成 zip，直接上传到创意工坊即可发布成模组。
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4 py-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="mod-pack-name" className="text-xs text-muted-foreground">
              模组名称
            </Label>
            <Input
              id="mod-pack-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="例如：三国武将包 第一期"
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="mod-pack-version" className="text-xs text-muted-foreground">
              版本号
            </Label>
            <Input
              id="mod-pack-version"
              value={version}
              onChange={(e) => setVersion(e.target.value)}
              placeholder="1.0"
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="mod-pack-desc" className="text-xs text-muted-foreground">
              一句话简介（可选）
            </Label>
            <textarea
              id="mod-pack-desc"
              rows={2}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="会写进包内 mod.info 的 description，也会作为创意工坊上的简介初值"
              className="w-full resize-y rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            />
          </div>

          <div className="flex flex-col gap-2 rounded-lg border border-border bg-card/40 p-3 text-xs">
            <div className="flex flex-wrap items-center gap-2">
              <span className="w-16 text-muted-foreground">武将</span>
              <Badge variant="outline" className="font-mono text-[10px]">
                {selected.length} 位
              </Badge>
              <span className="text-muted-foreground">
                包内编号 {MOD_PERSON_ID_START} ~ {MOD_PERSON_ID_START + selected.length - 1}
              </span>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <span className="w-16 text-muted-foreground">自制头像</span>
              <Badge variant="outline" className="font-mono text-[10px]">
                {faceIds.length} 张
              </Badge>
              <span className="text-muted-foreground">
                {faceIds.length > 0
                  ? `连同立绘共 ${faceIds.length * 2} 个文件，并自动生成 FaceConfig`
                  : '这批武将没有用到自制头像'}
              </span>
            </div>

            <p className="text-[11px] leading-relaxed text-muted-foreground">
              包内为 mod.info + Data/CustomPerson.json
              {faceIds.length > 0 ? ' + Data/FaceConfig.json + Assets/Face/*.png' : ''}
              ；模组 ID、作者与封面会在创意工坊上传时按站点数据补全。
              武将之间的关系（父子 / 配偶 / 亲近 / 厌恶）只在选中的这批人内部保留，
              引用未选中的同库武将会被清空。
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            取消
          </Button>
          <Button disabled={!valid || busy} onClick={handleExport}>
            <Download />
            {busy ? '正在生成…' : '生成并下载'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
