/**
 * 文件名：CustomFaceDialog.tsx
 * 描述：自定义头像制作弹窗。
 *
 *       制作流程（四步）：
 *       1) 选择性别并挑选一张本地图片（移动端可直接从相册 / 相机选择）；
 *       2) 裁剪半身像（240 x 240）；
 *       3) 裁剪头像（64 x 80）；
 *       4) 保存至服务器并得到系统分配的头像 ID，可打包下载。
 *
 *       ID 分配规则：从 3000 起，每 1000 个为一段，
 *       段千位为奇数分配给男性（3000-3999、5000-5999 …），
 *       段千位为偶数分配给女性（4000-4999、6000-6999 …）。
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
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { ImageCropper, type ImageCropperHandle } from '@/components/ImageCropper'
import { cn } from '@/lib/utils'
import { ApiError, createCustomFace, downloadCustomFaces, removeCustomFace } from '@/lib/api'
import { headIconUrl } from '@/lib/enums'
import { headRangeText, nextFaceIdOf } from '@/lib/faceRules'
import type { CustomFaceResult } from '@/lib/types'
import {
  ArrowLeft,
  Check,
  Download,
  Image as ImageIcon,
  Loader2,
  RotateCcw,
  Sparkles,
  Trash2,
  Upload,
} from 'lucide-react'

/** 半身像输出尺寸 */
const BUST_SIZE = { width: 240, height: 240 }

/** 头像输出尺寸 */
const FACE_SIZE = { width: 64, height: 80 }

/** 制作步骤 */
type Step = 'pick' | 'bust' | 'face' | 'done'

/** 步骤顺序（用于顶部进度指示） */
const STEPS: Array<{ key: Step; label: string }> = [
  { key: 'pick', label: '选择图片' },
  { key: 'bust', label: '裁剪半身像' },
  { key: 'face', label: '裁剪头像' },
  { key: 'done', label: '完成' },
]

interface CustomFaceDialogProps {
  /** 是否打开 */
  open: boolean
  /** 打开状态变化 */
  onOpenChange: (open: boolean) => void
  /** 是否具备制作权限（游客为 false） */
  canWrite: boolean
  /** 默认性别（沿用当前武将的性别） */
  defaultSex: number
  /** 自定义头像列表（用于展示待分配 ID 与打包下载） */
  faces: CustomFaceResult | null
  /** 制作完成回调，回传新头像 ID */
  onCreated: (id: number) => void
  /** 列表发生变化（新增 / 删除）后的刷新回调 */
  onFacesChanged?: () => void
  /** 需要登录时的回调 */
  onRequireLogin: () => void
}

export function CustomFaceDialog({
  open,
  onOpenChange,
  canWrite,
  defaultSex,
  faces,
  onCreated,
  onFacesChanged,
  onRequireLogin,
}: CustomFaceDialogProps) {
  const [step, setStep] = useState<Step>('pick')
  /** 归属性别：0=男，1=女 */
  const [sex, setSex] = useState(defaultSex)
  /** 已加载的源图 */
  const [image, setImage] = useState<HTMLImageElement | null>(null)
  /**
   * 半身像裁剪结果（PNG dataURL）。
   * 切割步骤切换到面部步骤后半身像裁剪组件会被卸载，
   * 因此必须在离开该步骤前把结果缓存下来，否则保存时无法取回。
   */
  const [bustData, setBustData] = useState('')
  const [loadingImage, setLoadingImage] = useState(false)
  const [saving, setSaving] = useState(false)
  /** 制作成功后系统分配的头像 ID */
  const [createdId, setCreatedId] = useState<number | null>(null)
  /** 待删除的自定义头像 ID */
  const [deleteTarget, setDeleteTarget] = useState<number | null>(null)
  const [deleting, setDeleting] = useState(false)
  /** 原生文件控件引用 */
  const fileInputRef = useRef<HTMLInputElement>(null)

  const bustRef = useRef<ImageCropperHandle>(null)
  const faceRef = useRef<ImageCropperHandle>(null)

  // 每次打开重置到初始状态
  useEffect(() => {
    if (!open) return
    setStep('pick')
    setSex(defaultSex)
    setImage(null)
    setBustData('')
    setCreatedId(null)
    setSaving(false)
    setDeleteTarget(null)
    setDeleting(false)
  }, [open, defaultSex])

  /** 下一个可用 ID（按当前选择性别） */
  const nextId = faces ? nextFaceIdOf(faces, sex) : 0

  /**
   * 读取本地图片并进入裁剪步骤。
   * @param event 文件控件变更事件
   */
  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    // 清空取值，保证连续选择同一文件也能触发变更
    event.target.value = ''
    if (!file) return
    if (!file.type.startsWith('image/')) {
      toast.error('请选择图片文件（JPG / PNG / WebP 等）')
      return
    }
    setLoadingImage(true)
    const reader = new FileReader()
    reader.onload = () => {
      const img = new Image()
      img.onload = () => {
        setImage(img)
        setLoadingImage(false)
        setStep('bust')
      }
      img.onerror = () => {
        setLoadingImage(false)
        toast.error('图片解析失败，请更换一张图片')
      }
      img.src = String(reader.result)
    }
    reader.onerror = () => {
      setLoadingImage(false)
      toast.error('图片读取失败，请重试')
    }
    reader.readAsDataURL(file)
  }

  /**
   * 从「裁剪半身像」进入「裁剪头像」步骤。
   * 由于下一步半身像裁剪组件会被卸载，需在此处先行导出并缓存结果。
   */
  const handleGoFace = () => {
    const bust = bustRef.current?.toDataURL() || ''
    if (!bust) {
      toast.error('半身像尚未准备完成，请稍候重试')
      return
    }
    setBustData(bust)
    setStep('face')
  }

  /** 提交裁剪结果并保存到服务器 */
  const handleSave = async () => {
    const bust = bustData || bustRef.current?.toDataURL() || ''
    const face = faceRef.current?.toDataURL() || ''
    if (!bust || !face) {
      toast.error('图片尚未准备完成，请稍候重试')
      return
    }
    setSaving(true)
    try {
      const result = await createCustomFace({ sex, bust, face })
      setCreatedId(result.id)
      setStep('done')
      onCreated(result.id)
      toast.success(`自制头像制作完成，已分配 ID：${result.id}`)
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

  /** 下载指定头像（打包为 ZIP） */
  const handleDownload = (ids: number[] | null, fileName: string) => {
    downloadCustomFaces(ids, fileName)
    toast.success('已开始下载，解压后放入游戏 Face 目录即可')
  }

  /** 删除指定的自定义头像 */
  const handleDelete = async () => {
    if (deleteTarget === null) return
    setDeleting(true)
    try {
      await removeCustomFace(deleteTarget)
      toast.success(`已删除自制头像 ${deleteTarget}`)
      setDeleteTarget(null)
      onFacesChanged?.()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : '删除失败')
    } finally {
      setDeleting(false)
    }
  }

  /** 当前步骤在流程中的序号 */
  const stepIndex = STEPS.findIndex((item) => item.key === step)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/* 基类 DialogContent 带 sm:max-w-lg，需用同前缀的 sm:max-w-* 覆盖，w-* 无法突破其限制 */}
      <DialogContent className="flex max-h-[92vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-[min(56rem,calc(100vw-2rem))]">
        <DialogHeader className="border-b border-border px-5 py-4">
          <DialogTitle className="flex items-center gap-2 font-display text-lg">
            <Sparkles className="size-5 text-primary" />
            自制头像
          </DialogTitle>
          <DialogDescription className="text-xs">
            选择一张图片依次裁剪出半身像与头像，系统按规则自动分配 ID 并保存到服务器。
          </DialogDescription>
        </DialogHeader>

        {/* 步骤指示 */}
        <div className="flex items-center gap-1 border-b border-border px-5 py-3">
          {STEPS.map((item, index) => (
            <div key={item.key} className="flex flex-1 items-center gap-1">
              <span
                className={cn(
                  'flex size-5 shrink-0 items-center justify-center rounded-full text-[10px] font-medium',
                  index < stepIndex
                    ? 'bg-primary text-primary-foreground'
                    : index === stepIndex
                      ? 'bg-primary/20 text-primary ring-1 ring-primary'
                      : 'bg-secondary text-muted-foreground',
                )}
              >
                {index < stepIndex ? <Check className="size-3" /> : index + 1}
              </span>
              <span
                className={cn(
                  'hidden truncate text-[11px] sm:inline',
                  index <= stepIndex ? 'text-foreground' : 'text-muted-foreground',
                )}
              >
                {item.label}
              </span>
              {index < STEPS.length - 1 && <span className="h-px flex-1 bg-border" />}
            </div>
          ))}
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-5">
          {/* 第一步：选择性别与图片 */}
          {step === 'pick' && (
            <div className="flex flex-col gap-5">
              <div className="flex flex-col gap-2">
                <Label>头像归属性别</Label>
                <div className="flex gap-2">
                  {[
                    { value: 0, label: '男性' },
                    { value: 1, label: '女性' },
                  ].map((item) => (
                    <Button
                      key={item.value}
                      type="button"
                      variant={sex === item.value ? 'default' : 'outline'}
                      className="flex-1"
                      onClick={() => setSex(item.value)}
                    >
                      {item.label}
                    </Button>
                  ))}
                </div>
                <p className="text-[11px] text-muted-foreground">
                  {headRangeText(sex)}；本次将分配 ID
                  <span className="ml-1 font-medium text-primary">{nextId || '（号段已满）'}</span>
                </p>
              </div>

              <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed border-border bg-card/40 px-4 py-8">
                <div className="flex size-14 items-center justify-center rounded-full bg-secondary">
                  {loadingImage ? (
                    <Loader2 className="size-6 animate-spin text-primary" />
                  ) : (
                    <ImageIcon className="size-6 text-primary" />
                  )}
                </div>
                <div className="text-center">
                  <p className="text-sm font-medium">选择一张本地图片</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    手机上会打开相册 / 相机，建议选择人物正面、头部居中的图片
                  </p>
                </div>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={handleFileChange}
                />
                <Button type="button" onClick={() => fileInputRef.current?.click()}>
                  <Upload />
                  选择图片
                </Button>
                {!canWrite && (
                  <p className="text-xs text-destructive">当前为游客身份，登录后才能制作头像</p>
                )}
              </div>

              {faces && faces.items.length > 0 && (
                <div className="flex flex-col gap-3 rounded-lg border border-border bg-card/40 p-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="text-xs text-muted-foreground">
                      服务器已有 {faces.items.length} 个自制头像（点击可下载全部）
                    </span>
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={() => handleDownload(null, 'Face_Custom_All.zip')}
                    >
                      <Download />
                      打包下载全部
                    </Button>
                  </div>
                  <div className="grid max-h-56 grid-cols-4 gap-2 overflow-y-auto sm:grid-cols-8">
                    {faces.items.map((item) => (
                      <div
                        key={item.id}
                        className="group relative aspect-square overflow-hidden rounded-md border border-border bg-secondary/40"
                        title={`ID ${item.id}（${item.sex === 0 ? '男' : '女'}）`}
                      >
                        <img
                          src={headIconUrl(item.id, 1)}
                          alt={`自制头像 ${item.id}`}
                          loading="lazy"
                          className="size-full object-cover object-top"
                          onError={(e) => {
                            e.currentTarget.style.visibility = 'hidden'
                          }}
                        />
                        <span className="absolute bottom-0 left-0 right-0 bg-background/80 text-center text-[9px] leading-tight text-muted-foreground">
                          {item.id}
                        </span>
                        {canWrite && (
                          <button
                            type="button"
                            title="删除该头像"
                            className="absolute right-0.5 top-0.5 rounded-full bg-background/80 p-0.5 text-destructive opacity-0 transition-opacity group-hover:opacity-100"
                            onClick={() => setDeleteTarget(item.id)}
                          >
                            <Trash2 className="size-3" />
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* 第二步：裁剪半身像 */}
          {step === 'bust' && image && (
            <div className="flex flex-col items-center gap-4">
              <div className="text-center">
                <p className="text-sm font-medium">裁剪半身像</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  用于游戏中的全身立绘展示（{BUST_SIZE.width}×{BUST_SIZE.height}）
                </p>
              </div>
              <ImageCropper
                ref={bustRef}
                image={image}
                aspect={1}
                outputWidth={BUST_SIZE.width}
                outputHeight={BUST_SIZE.height}
                viewWidth={240}
              />
            </div>
          )}

          {/* 第三步：裁剪头像 */}
          {step === 'face' && image && (
            <div className="flex flex-col items-center gap-4">
              <div className="text-center">
                <p className="text-sm font-medium">裁剪头像</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  用于列表与对话框中的小头像展示（{FACE_SIZE.width}×{FACE_SIZE.height}）
                </p>
              </div>
              <ImageCropper
                ref={faceRef}
                image={image}
                aspect={FACE_SIZE.width / FACE_SIZE.height}
                outputWidth={FACE_SIZE.width}
                outputHeight={FACE_SIZE.height}
                viewWidth={180}
              />
            </div>
          )}

          {/* 第四步：完成 */}
          {step === 'done' && createdId !== null && (
            <div className="flex flex-col items-center gap-5">
              <div className="flex size-14 items-center justify-center rounded-full bg-primary/15">
                <Check className="size-7 text-primary" />
              </div>
              <div className="text-center">
                <p className="font-display text-lg">制作完成</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  已保存到服务器，将该 ID 填入武将的「头像」字段即可使用
                </p>
              </div>
              <Badge className="text-sm">头像 ID：{createdId}</Badge>

              <div className="flex items-end gap-4">
                <div className="flex flex-col items-center gap-1.5">
                  <img
                    src={headIconUrl(createdId, 1)}
                    alt={`半身像 ${createdId}`}
                    className="size-32 rounded-lg border border-border object-cover"
                  />
                  <span className="text-[11px] text-muted-foreground">半身像 240×240</span>
                </div>
                <div className="flex flex-col items-center gap-1.5">
                  <img
                    src={headIconUrl(createdId, 2)}
                    alt={`头像 ${createdId}`}
                    className="h-24 w-[4.8rem] rounded-lg border border-border object-cover"
                  />
                  <span className="text-[11px] text-muted-foreground">头像 64×80</span>
                </div>
              </div>

              <div className="flex flex-wrap justify-center gap-2">
                <Button
                  type="button"
                  onClick={() => handleDownload([createdId], `Face_${createdId}.zip`)}
                >
                  <Download />
                  下载这个头像
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => handleDownload(null, 'Face_Custom_All.zip')}
                >
                  <Download />
                  打包下载全部
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  onClick={() => {
                    setImage(null)
                    setBustData('')
                    setCreatedId(null)
                    setStep('pick')
                  }}
                >
                  <RotateCcw />
                  再做一个
                </Button>
              </div>
            </div>
          )}
        </div>

        <DialogFooter className="flex-row items-center justify-between gap-2 border-t border-border px-5 py-3 sm:justify-between">
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            {sex === 0 ? '男性头像' : '女性头像'}
            {nextId > 0 && step !== 'done' && <span>· 下一个 ID {nextId}</span>}
          </div>
          <div className="flex items-center gap-2">
            {step === 'bust' && (
              <Button type="button" variant="outline" onClick={() => setStep('pick')}>
                <ArrowLeft />
                上一步
              </Button>
            )}
            {step === 'face' && (
              <>
                <Button type="button" variant="outline" onClick={() => setStep('bust')}>
                  <ArrowLeft />
                  上一步
                </Button>
                <Button type="button" onClick={handleSave} disabled={saving}>
                  {saving ? <Loader2 className="animate-spin" /> : <Check />}
                  保存到服务器
                </Button>
              </>
            )}
            {step === 'bust' && (
              <Button type="button" onClick={handleGoFace}>
                下一步
              </Button>
            )}
            {step === 'done' && (
              <Button type="button" onClick={() => onOpenChange(false)}>
                完成
              </Button>
            )}
          </div>
        </DialogFooter>
      </DialogContent>

      {/* 删除自制头像确认 */}
      <AlertDialog open={deleteTarget !== null} onOpenChange={(o) => !o && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>删除自制头像</AlertDialogTitle>
            <AlertDialogDescription>
              确定要删除 ID 为 {deleteTarget} 的自制头像吗？半身像与头像文件都会被删除，该操作不可撤销。
              已使用该头像的武将需要另行修改头像字段。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleting}>取消</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              disabled={deleting}
              onClick={(e) => {
                e.preventDefault()
                handleDelete()
              }}
            >
              {deleting && <Loader2 className="animate-spin" />}
              确认删除
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Dialog>
  )
}

