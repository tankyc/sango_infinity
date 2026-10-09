/**
 * 文件名：CustomFaceEditDialog.tsx
 * 描述：自定义头像「图片修改」弹窗，两种模式：
 *
 *       1) mode = 'bust'：重新导入半身像（拖入或选择原图 → 裁 240×240），
 *          可选择同时按新半身像自动重新生成小头像；
 *       2) mode = 'face'：以半身像为准重新裁剪小头像（64×80）。
 *
 *       小头像裁剪的源图通过服务端同源接口 /api/faces/custom/raw 获取：
 *       直接用 /face/{id}_1.png 时，若配置了 R2 会被 302 到别的域名，
 *       画布跨域取图会污染、toDataURL 直接失败。
 */

import { useEffect, useRef, useState } from 'react'
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
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import { ImageCropper, type ImageCropperHandle } from '@/components/ImageCropper'
import { cn } from '@/lib/utils'
import { ApiError, fetchCustomFaceRaw, updateCustomFaceImages } from '@/lib/api'
import { headIconUrl } from '@/lib/enums'
import { BUST_SIZE, FACE_SIZE, autoHeadFromBust, loadImageFromFile, loadImageFromUrl } from '@/lib/faceImage'
import type { CustomFace } from '@/lib/types'
import { Check, Crop, Loader2, Upload } from 'lucide-react'

interface CustomFaceEditDialogProps {
  /** 是否打开 */
  open: boolean
  /** 打开状态变化 */
  onOpenChange: (open: boolean) => void
  /** 目标头像；为空时不渲染内容 */
  face: CustomFace | null
  /** 编辑模式：bust=重新导入半身像，face=以半身像为准重裁小头像 */
  mode: 'bust' | 'face'
  /** 保存成功回调（用于刷新列表） */
  onSaved?: () => void
  /** 需要登录时的回调 */
  onRequireLogin: () => void
}

export function CustomFaceEditDialog({
  open,
  onOpenChange,
  face,
  mode,
  onSaved,
  onRequireLogin,
}: CustomFaceEditDialogProps) {
  /** 当前裁剪源图（bust 模式为本地新选的图，face 模式为服务器上的半身像） */
  const [image, setImage] = useState<HTMLImageElement | null>(null)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  /** 是否同时按新半身像重新生成小头像（仅 bust 模式） */
  const [regenHead, setRegenHead] = useState(true)
  const [dragActive, setDragActive] = useState(false)

  const fileInputRef = useRef<HTMLInputElement>(null)
  const cropperRef = useRef<ImageCropperHandle>(null)

  // 打开时准备源图；关闭时清理
  useEffect(() => {
    if (!open || !face) {
      setImage(null)
      return
    }
    setRegenHead(true)
    setDragActive(false)
    let objectUrl = ''
    let cancelled = false

    /** 加载裁剪源图 */
    const prepare = async () => {
      setLoading(true)
      try {
        if (mode === 'face') {
          objectUrl = await fetchCustomFaceRaw(face.id, 1)
          const img = await loadImageFromUrl(objectUrl)
          if (!cancelled) setImage(img)
        } else {
          setImage(null)
        }
      } catch (err) {
        if (!cancelled) {
          if (err instanceof ApiError && err.status === 401) {
            toast.error('登录状态已失效，请重新登录后再试')
            onOpenChange(false)
            onRequireLogin()
          } else {
            toast.error(err instanceof Error ? err.message : '读取半身像失败')
          }
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    void prepare()

    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [open, face, mode, onRequireLogin, onOpenChange])

  /**
   * 读取本地图片作为新的半身像源图。
   * @param file 图片文件
   */
  const useLocalFile = async (file: File) => {
    if (!file.type.startsWith('image/')) {
      toast.error('请选择图片文件（JPG / PNG / WebP 等）')
      return
    }
    try {
      const img = await loadImageFromFile(file)
      setImage(img)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : '图片读取失败，请重试')
    }
  }

  /** 保存修改 */
  const handleSave = async () => {
    if (!face) return
    setSaving(true)
    try {
      if (mode === 'face') {
        const data = cropperRef.current?.toDataURL() || ''
        if (!data) {
          toast.error('图片尚未准备完成，请稍候重试')
          return
        }
        await updateCustomFaceImages(face.id, { face: data })
        toast.success(`已更新头像 ${face.id} 的小头像`)
      } else {
        const bust = cropperRef.current?.toDataURL() || ''
        if (!bust) {
          toast.error('图片尚未准备完成，请稍候重试')
          return
        }
        let faceData: string | undefined
        if (regenHead) {
          const bustImage = await loadImageFromUrl(bust)
          faceData = autoHeadFromBust(bustImage) || undefined
        }
        await updateCustomFaceImages(face.id, { bust, face: faceData })
        toast.success(`已更新头像 ${face.id} 的半身像${faceData ? '与小头像' : ''}`)
      }
      onSaved?.()
      onOpenChange(false)
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        toast.error('登录状态已失效，请重新登录后再试')
        onOpenChange(false)
        onRequireLogin()
        return
      }
      toast.error(err instanceof Error ? err.message : '保存失败')
    } finally {
      setSaving(false)
    }
  }

  const isFaceMode = mode === 'face'

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[94vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-2xl">
        <DialogHeader className="border-b border-border px-5 py-4">
          <DialogTitle className="flex items-center gap-2 font-display text-lg">
            <Crop className="size-5 text-primary" />
            {isFaceMode ? '编辑小头像' : '重新导入半身像'}
            {face && <span className="text-sm text-muted-foreground">ID {face.id}</span>}
          </DialogTitle>
          <DialogDescription className="text-xs">
            {isFaceMode
              ? '以当前半身像为基准框选头部区域，生成 64×80 的小头像（用于列表与对话框展示）。'
              : '选择一张新的原图并裁剪成 240×240 半身像，替换现有立绘。'}
          </DialogDescription>
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-y-auto p-5">
          <div className="flex flex-col items-center gap-4">
            {isFaceMode && face && (
              <div className="flex items-center gap-3 rounded-lg border border-border bg-card/40 px-3 py-2">
                <img
                  src={`${headIconUrl(face.id, 2)}?v=${face.version}`}
                  alt={`当前小头像 ${face.id}`}
                  className="h-14 w-[2.8rem] rounded border border-border object-cover"
                />
                <span className="text-[11px] text-muted-foreground">
                  当前小头像（保存后会被替换）
                </span>
              </div>
            )}

            {!isFaceMode && !image && (
              <div
                className={cn(
                  'flex w-full flex-col items-center gap-3 rounded-lg border border-dashed px-4 py-8 transition-colors',
                  dragActive ? 'border-primary bg-primary/5' : 'border-border bg-card/40',
                )}
                onDragOver={(e) => {
                  e.preventDefault()
                  setDragActive(true)
                }}
                onDragLeave={() => setDragActive(false)}
                onDrop={(e) => {
                  e.preventDefault()
                  setDragActive(false)
                  const file = e.dataTransfer.files?.[0]
                  if (file) void useLocalFile(file)
                }}
              >
                <div className="flex size-14 items-center justify-center rounded-full bg-secondary">
                  <Upload className="size-6 text-primary" />
                </div>
                <p className="text-sm font-medium">把新的原图拖到这里，或点击选择</p>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={(e) => {
                    const file = e.target.files?.[0]
                    e.target.value = ''
                    if (file) void useLocalFile(file)
                  }}
                />
                <Button type="button" onClick={() => fileInputRef.current?.click()}>
                  <Upload />
                  选择图片
                </Button>
              </div>
            )}

            {loading && (
              <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
                <Loader2 className="size-4 animate-spin" />
                正在读取半身像…
              </div>
            )}

            {image && (
              <>
                <ImageCropper
                  ref={cropperRef}
                  image={image}
                  aspect={isFaceMode ? FACE_SIZE.width / FACE_SIZE.height : 1}
                  outputWidth={isFaceMode ? FACE_SIZE.width : BUST_SIZE.width}
                  outputHeight={isFaceMode ? FACE_SIZE.height : BUST_SIZE.height}
                  viewWidth={isFaceMode ? 200 : 240}
                />
                {!isFaceMode && (
                  <div className="flex items-center gap-2">
                    <Checkbox
                      id="regen-head"
                      checked={regenHead}
                      onCheckedChange={(v) => setRegenHead(v === true)}
                    />
                    <Label htmlFor="regen-head" className="text-xs font-normal">
                      同时按新半身像自动生成小头像
                    </Label>
                  </div>
                )}
                {!isFaceMode && (
                  <Button type="button" variant="outline" size="sm" onClick={() => setImage(null)}>
                    重新选择图片
                  </Button>
                )}
              </>
            )}
          </div>
        </div>

        <DialogFooter className="flex-row items-center justify-between gap-2 border-t border-border px-5 py-3 sm:justify-between">
          <span className="text-xs text-muted-foreground">
            {isFaceMode ? '小头像 64×80' : '半身像 240×240'}
          </span>
          <div className="flex items-center gap-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
              取消
            </Button>
            <Button type="button" onClick={handleSave} disabled={saving || !image}>
              {saving ? <Loader2 className="animate-spin" /> : <Check />}
              保存
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
