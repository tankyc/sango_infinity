/**
 * 文件名：CustomFaceCreateDialog.tsx
 * 描述：自定义头像「创建」弹窗（与浏览管理界面拆开，只有点了「制作新头像」才会弹出）。
 *
 *       两种方式：
 *       1) 单张制作：选择性别 → 拖入 / 选择一张原图 → 裁半身像（240×240）
 *          → 裁小头像（64×80）→ 保存；
 *       2) 批量导入：选择性别 → 拖入 / 选择多张原图，前端自动按「顶部对齐铺满」
 *          裁出半身像，并由半身像自动生成小头像，按批提交（失败自动重试提示）。
 *
 *       ID 由服务端按性别规则分配（男 3000-3999、5000-5999…，女 4000-4999、6000-6999…）。
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
import { Badge } from '@/components/ui/badge'
import { ImageCropper, type ImageCropperHandle } from '@/components/ImageCropper'
import { cn } from '@/lib/utils'
import { ApiError, createCustomFace, createCustomFacesBatch } from '@/lib/api'
import { headIconUrl } from '@/lib/enums'
import { headRangeText, nextFaceIdOf } from '@/lib/faceRules'
import {
  BUST_SIZE,
  FACE_SIZE,
  autoHeadFromBust,
  coverCropToDataUrl,
  isUsableImageFile,
  loadImageFromFile,
  loadImageFromUrl,
} from '@/lib/faceImage'
import type { CustomFaceResult } from '@/lib/types'
import { ArrowLeft, Check, FileImage, Loader2, Sparkles, Trash2, Upload, X } from 'lucide-react'

/** 制作步骤（单张模式） */
type Step = 'pick' | 'bust' | 'face' | 'done'

/** 创建方式 */
type Mode = 'single' | 'batch'

/** 步骤顺序（用于顶部进度指示） */
const STEPS: Array<{ key: Step; label: string }> = [
  { key: 'pick', label: '选择图片' },
  { key: 'bust', label: '裁剪半身像' },
  { key: 'face', label: '裁剪头像' },
  { key: 'done', label: '完成' },
]

/** 批量导入单次最多处理的张数 */
const BATCH_MAX_FILES = 100

/** 批量导入每次提交给服务端的张数（控制请求体大小） */
const BATCH_CHUNK = 8

/** 批量导入的待处理文件 */
interface PendingFile {
  /** 原始文件 */
  file: File
  /** 预览用的 object URL（移除时需 revoke） */
  url: string
}

interface CustomFaceCreateDialogProps {
  /** 是否打开 */
  open: boolean
  /** 打开状态变化 */
  onOpenChange: (open: boolean) => void
  /** 是否具备制作权限（游客为 false） */
  canWrite: boolean
  /** 默认性别（沿用当前武将的性别） */
  defaultSex: number
  /** 自定义头像列表（用于展示即将分配的 ID） */
  faces: CustomFaceResult | null
  /** 单张制作完成回调（回传新头像 ID） */
  onCreated: (id: number) => void
  /** 列表发生变化（新增）后的刷新回调 */
  onFacesChanged?: () => void
  /** 需要登录时的回调 */
  onRequireLogin: () => void
}

export function CustomFaceCreateDialog({
  open,
  onOpenChange,
  canWrite,
  defaultSex,
  faces,
  onCreated,
  onFacesChanged,
  onRequireLogin,
}: CustomFaceCreateDialogProps) {
  const [mode, setMode] = useState<Mode>('single')
  const [step, setStep] = useState<Step>('pick')
  /** 归属性别：0=男，1=女 */
  const [sex, setSex] = useState(defaultSex)

  // ── 单张制作 ──
  const [image, setImage] = useState<HTMLImageElement | null>(null)
  /** 半身像裁剪结果（切换步骤后裁剪组件会被卸载，需先缓存） */
  const [bustData, setBustData] = useState('')
  const [loadingImage, setLoadingImage] = useState(false)
  const [saving, setSaving] = useState(false)
  const [createdId, setCreatedId] = useState<number | null>(null)
  const [dragActive, setDragActive] = useState(false)

  // ── 批量导入 ──
  const [pending, setPending] = useState<PendingFile[]>([])
  const [importing, setImporting] = useState(false)
  const [progress, setProgress] = useState({ done: 0, total: 0 })
  const [batchResult, setBatchResult] = useState<{
    ids: number[]
    failed: Array<{ name: string; reason: string }>
  } | null>(null)

  const singleInputRef = useRef<HTMLInputElement>(null)
  const batchInputRef = useRef<HTMLInputElement>(null)
  const bustRef = useRef<ImageCropperHandle>(null)
  const faceRef = useRef<ImageCropperHandle>(null)

  // 每次打开重置
  useEffect(() => {
    if (!open) return
    setMode('single')
    setStep('pick')
    setSex(defaultSex)
    setImage(null)
    setBustData('')
    setCreatedId(null)
    setLoadingImage(false)
    setSaving(false)
    setDragActive(false)
    setImporting(false)
    setProgress({ done: 0, total: 0 })
    setBatchResult(null)
    setPending((prev) => {
      for (const item of prev) URL.revokeObjectURL(item.url)
      return []
    })
  }, [open, defaultSex])

  /** 即将分配的头像 ID（按当前性别推算） */
  const nextId = faces ? nextFaceIdOf(faces, sex) : 0

  /**
   * 把一批文件加入批量导入列表。
   * @param incoming 文件列表
   */
  const addPendingFiles = (incoming: File[]) => {
    const accepted = incoming.filter((file) => isUsableImageFile(file))
    if (accepted.length === 0) {
      toast.error('请拖入图片文件（单张不超过 8MB）')
      return
    }
    if (accepted.length < incoming.length) {
      toast.error(`已忽略 ${incoming.length - accepted.length} 个非图片或体积过大的文件`)
    }
    setPending((prev) => {
      const merged = [...prev, ...accepted.map((file) => ({ file, url: URL.createObjectURL(file) }))]
      if (merged.length > BATCH_MAX_FILES) {
        for (const item of merged.slice(BATCH_MAX_FILES)) URL.revokeObjectURL(item.url)
        toast.error(`单次最多导入 ${BATCH_MAX_FILES} 张，多余文件已忽略`)
        return merged.slice(0, BATCH_MAX_FILES)
      }
      return merged
    })
    setBatchResult(null)
  }

  /**
   * 读取单张原图并进入半身像裁剪步骤。
   * @param file 图片文件
   */
  const useSingleFile = async (file: File) => {
    if (!isUsableImageFile(file)) {
      toast.error('请选择图片文件（单张不超过 8MB）')
      return
    }
    setLoadingImage(true)
    try {
      const img = await loadImageFromFile(file)
      setImage(img)
      setStep('bust')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : '图片读取失败，请重试')
    } finally {
      setLoadingImage(false)
    }
  }

  /** 从「裁剪半身像」进入「裁剪头像」步骤（先缓存半身像，下一步组件会卸载） */
  const handleGoFace = () => {
    const bust = bustRef.current?.toDataURL() || ''
    if (!bust) {
      toast.error('半身像尚未准备完成，请稍候重试')
      return
    }
    setBustData(bust)
    setStep('face')
  }

  /** 单张保存到服务器 */
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
      onFacesChanged?.()
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

  /** 批量导入：逐张自动裁切后按批提交 */
  const handleBatchImport = async () => {
    if (pending.length === 0) {
      toast.error('请先拖入或选择要导入的图片')
      return
    }
    setImporting(true)
    setBatchResult(null)
    setProgress({ done: 0, total: pending.length })

    const ids: number[] = []
    const failed: Array<{ name: string; reason: string }> = []
    /** 处理失败的待导入项下标（处理完把它们留在列表里，便于修正后重试） */
    const failedIndexes = new Set<number>()
    /** 本次已经处理好、等待一起提交的图片 */
    let buffer: Array<{ bust: string; face: string }> = []
    let bufferNames: string[] = []
    let bufferIndexes: number[] = []

    /** 提交缓冲区中的图片 */
    const flush = async () => {
      if (buffer.length === 0) return
      try {
        const res = await createCustomFacesBatch({ sex, items: buffer })
        ids.push(...res.ids)
      } catch (err) {
        if (err instanceof ApiError && err.status === 401) throw err
        const reason = err instanceof Error ? err.message : '上传失败'
        for (const [index, name] of bufferNames.map((n, i) => [bufferIndexes[i], n] as const)) {
          failed.push({ name, reason })
          failedIndexes.add(index)
        }
      } finally {
        buffer = []
        bufferNames = []
        bufferIndexes = []
      }
    }

    try {
      for (let index = 0; index < pending.length; index++) {
        const item = pending[index]
        try {
          const img = await loadImageFromFile(item.file)
          const bust = coverCropToDataUrl(img, BUST_SIZE.width, BUST_SIZE.height, 'top')
          if (!bust) throw new Error('半身像裁切失败')
          const bustImage = await loadImageFromUrl(bust)
          const face = autoHeadFromBust(bustImage)
          if (!face) throw new Error('小头像生成失败')
          buffer.push({ bust, face })
          bufferNames.push(item.file.name)
          bufferIndexes.push(index)
        } catch (err) {
          failed.push({ name: item.file.name, reason: err instanceof Error ? err.message : '处理失败' })
          failedIndexes.add(index)
        }
        setProgress((prev) => ({ ...prev, done: index + 1 }))
        if (buffer.length >= BATCH_CHUNK) await flush()
      }
      await flush()
      setBatchResult({ ids, failed })
      if (ids.length > 0) {
        onFacesChanged?.()
        toast.success(`已导入 ${ids.length} 张，ID：${ids[0]} ~ ${ids[ids.length - 1]}`)
      }
      if (failed.length > 0) toast.error(`${failed.length} 张导入失败，已保留在列表中`)
      // 处理成功的从列表里移除，失败的留着便于重试
      setPending((prev) => {
        const remained = prev.filter((_, index) => failedIndexes.has(index))
        for (const item of prev) {
          if (!remained.includes(item)) URL.revokeObjectURL(item.url)
        }
        return remained
      })
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        toast.error('登录状态已失效，请重新登录后再试')
        onOpenChange(false)
        onRequireLogin()
        return
      }
      toast.error(err instanceof Error ? err.message : '批量导入失败')
    } finally {
      setImporting(false)
      setProgress({ done: 0, total: 0 })
    }
  }

  /** 当前步骤在流程中的序号 */
  const stepIndex = STEPS.findIndex((item) => item.key === step)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[94vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-3xl">
        <DialogHeader className="border-b border-border px-5 py-4">
          <DialogTitle className="flex items-center gap-2 font-display text-lg">
            <Sparkles className="size-5 text-primary" />
            制作自制头像
          </DialogTitle>
          <DialogDescription className="text-xs">
            {mode === 'single'
              ? '拖入或选择一张原图，依次裁剪半身像与小头像，保存后由系统分配 ID。'
              : '拖入或选择多张原图，自动裁成半身像并生成小头像后批量保存（小头像可事后单独微调）。'}
          </DialogDescription>
        </DialogHeader>

        {/* 创建方式切换 */}
        <div className="flex items-center gap-2 border-b border-border px-5 py-3">
          {(
            [
              { key: 'single', label: '单张制作' },
              { key: 'batch', label: '批量导入半身像' },
            ] as Array<{ key: Mode; label: string }>
          ).map((item) => (
            <Button
              key={item.key}
              type="button"
              size="sm"
              variant={mode === item.key ? 'default' : 'outline'}
              disabled={importing}
              onClick={() => setMode(item.key)}
            >
              {item.label}
            </Button>
          ))}
          <span className="ml-auto text-[11px] text-muted-foreground">
            {sex === 0 ? '男性' : '女性'}号段 · 下一个 ID{' '}
            <span className="font-medium text-primary">{nextId || '（已满）'}</span>
          </span>
        </div>

        {/* 单张制作的步骤指示 */}
        {mode === 'single' && (
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
        )}

        <div className="min-h-0 flex-1 overflow-y-auto p-5">
          {/* 性别选择（两种模式共用） */}
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
                  disabled={importing}
                  onClick={() => setSex(item.value)}
                >
                  {item.label}
                </Button>
              ))}
            </div>
            <p className="text-[11px] text-muted-foreground">{headRangeText(sex)}</p>
          </div>

          {/* 单张：选择图片 */}
          {mode === 'single' && step === 'pick' && (
            <div className="mt-5 flex flex-col gap-3">
              <div
                className={cn(
                  'flex flex-col items-center gap-3 rounded-lg border border-dashed px-4 py-8 transition-colors',
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
                  if (file) void useSingleFile(file)
                }}
              >
                <div className="flex size-14 items-center justify-center rounded-full bg-secondary">
                  {loadingImage ? (
                    <Loader2 className="size-6 animate-spin text-primary" />
                  ) : (
                    <Upload className="size-6 text-primary" />
                  )}
                </div>
                <div className="text-center">
                  <p className="text-sm font-medium">把原图拖到这里，或点击选择</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    手机上会打开相册 / 相机，建议人物正面、头部居中
                  </p>
                </div>
                <input
                  ref={singleInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={(e) => {
                    const file = e.target.files?.[0]
                    e.target.value = ''
                    if (file) void useSingleFile(file)
                  }}
                />
                <Button type="button" onClick={() => singleInputRef.current?.click()}>
                  <Upload />
                  选择图片
                </Button>
                {!canWrite && (
                  <p className="text-xs text-destructive">当前为游客身份，登录后才能制作头像</p>
                )}
              </div>
            </div>
          )}

          {/* 单张：裁剪半身像 */}
          {mode === 'single' && step === 'bust' && image && (
            <div className="mt-5 flex flex-col items-center gap-4">
              <div className="text-center">
                <p className="text-sm font-medium">裁剪半身像</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  用于游戏中的立绘展示（{BUST_SIZE.width}×{BUST_SIZE.height}）
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

          {/* 单张：裁剪小头像 */}
          {mode === 'single' && step === 'face' && image && (
            <div className="mt-5 flex flex-col items-center gap-4">
              <div className="text-center">
                <p className="text-sm font-medium">裁剪小头像</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  用于列表与对话框展示（{FACE_SIZE.width}×{FACE_SIZE.height}）
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

          {/* 单张：完成 */}
          {mode === 'single' && step === 'done' && createdId !== null && (
            <div className="mt-5 flex flex-col items-center gap-5">
              <div className="flex size-14 items-center justify-center rounded-full bg-primary/15">
                <Check className="size-7 text-primary" />
              </div>
              <div className="text-center">
                <p className="font-display text-lg">制作完成</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  把该 ID 填入武将的「头像」字段即可使用
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
                  variant="outline"
                  onClick={() => {
                    setImage(null)
                    setBustData('')
                    setCreatedId(null)
                    setStep('pick')
                  }}
                >
                  <Sparkles />
                  再做一个
                </Button>
              </div>
            </div>
          )}

          {/* 批量导入 */}
          {mode === 'batch' && (
            <div className="mt-5 flex flex-col gap-4">
              <div
                className={cn(
                  'flex flex-col items-center gap-3 rounded-lg border border-dashed px-4 py-8 transition-colors',
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
                  addPendingFiles(Array.from(e.dataTransfer.files || []))
                }}
              >
                <div className="flex size-14 items-center justify-center rounded-full bg-secondary">
                  <FileImage className="size-6 text-primary" />
                </div>
                <div className="text-center">
                  <p className="text-sm font-medium">把多张原图拖到这里，或点击选择</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    自动按「顶部对齐铺满」裁成 {BUST_SIZE.width}×{BUST_SIZE.height}{' '}
                    半身像，并由半身像自动生成小头像；单次最多 {BATCH_MAX_FILES} 张
                  </p>
                </div>
                <input
                  ref={batchInputRef}
                  type="file"
                  accept="image/*"
                  multiple
                  className="hidden"
                  onChange={(e) => {
                    addPendingFiles(Array.from(e.target.files || []))
                    e.target.value = ''
                  }}
                />
                <Button
                  type="button"
                  disabled={importing}
                  onClick={() => batchInputRef.current?.click()}
                >
                  <Upload />
                  选择多张图片
                </Button>
                {!canWrite && (
                  <p className="text-xs text-destructive">当前为游客身份，登录后才能导入</p>
                )}
              </div>

              {pending.length > 0 && (
                <div className="flex flex-col gap-2 rounded-lg border border-border bg-card/40 p-3">
                  <div className="flex items-center justify-between text-xs text-muted-foreground">
                    <span>待导入 {pending.length} 张</span>
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      className="h-6 px-2 text-xs"
                      disabled={importing}
                      onClick={() => {
                        for (const item of pending) URL.revokeObjectURL(item.url)
                        setPending([])
                        setBatchResult(null)
                      }}
                    >
                      <Trash2 />
                      清空
                    </Button>
                  </div>
                  <div className="grid grid-cols-5 gap-2 sm:grid-cols-8">
                    {pending.map((item, index) => (
                      <div key={`${item.file.name}-${index}`} className="group relative">
                        <img
                          src={item.url}
                          alt={item.file.name}
                          className="aspect-square w-full rounded border border-border object-cover object-top"
                        />
                        <button
                          type="button"
                          title="移除"
                          disabled={importing}
                          className="absolute right-0.5 top-0.5 rounded-full bg-background/80 p-0.5 text-destructive opacity-0 transition-opacity group-hover:opacity-100"
                          onClick={() => {
                            URL.revokeObjectURL(item.url)
                            setPending((prev) => prev.filter((_, i) => i !== index))
                          }}
                        >
                          <X className="size-3" />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {importing && (
                <div className="rounded-lg border border-border bg-card/40 p-3 text-xs text-muted-foreground">
                  正在处理 {progress.done} / {progress.total} 张…
                </div>
              )}

              {batchResult && (
                <div className="flex flex-col gap-2 rounded-lg border border-border bg-card/40 p-3 text-xs">
                  <p className="text-foreground">成功导入 {batchResult.ids.length} 张</p>
                  {batchResult.ids.length > 0 && (
                    <p className="text-muted-foreground">
                      ID：{batchResult.ids.join('、')}
                    </p>
                  )}
                  {batchResult.failed.length > 0 && (
                    <div className="text-destructive">
                      <p>{batchResult.failed.length} 张失败：</p>
                      <ul className="mt-1 flex flex-col gap-0.5">
                        {batchResult.failed.slice(0, 8).map((item) => (
                          <li key={item.name}>
                            {item.name} —— {item.reason}
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </div>

        <DialogFooter className="flex-row items-center justify-between gap-2 border-t border-border px-5 py-3 sm:justify-between">
          <span className="text-xs text-muted-foreground">
            {mode === 'single' ? '单张制作' : `批量导入 · 已选 ${pending.length} 张`}
          </span>
          <div className="flex items-center gap-2">
            {mode === 'single' && step === 'bust' && (
              <>
                <Button type="button" variant="outline" onClick={() => setStep('pick')}>
                  <ArrowLeft />
                  上一步
                </Button>
                <Button type="button" onClick={handleGoFace}>
                  下一步
                </Button>
              </>
            )}
            {mode === 'single' && step === 'face' && (
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
            {mode === 'single' && step === 'done' && (
              <Button type="button" onClick={() => onOpenChange(false)}>
                完成
              </Button>
            )}
            {mode === 'batch' && (
              <>
                <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={importing}>
                  关闭
                </Button>
                <Button
                  type="button"
                  disabled={importing || pending.length === 0 || !canWrite}
                  onClick={handleBatchImport}
                >
                  {importing ? <Loader2 className="animate-spin" /> : <Upload />}
                  开始导入
                </Button>
              </>
            )}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
