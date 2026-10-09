/**
 * 文件名：CustomFaceDialog.tsx
 * 描述：自定义头像「浏览与管理」弹窗（创建操作已拆到 CustomFaceCreateDialog，
 *       只有点击「制作新头像」时才会弹出）。
 *
 *       能力：
 *       - 按上传者 / 性别筛查，分页浏览；
 *       - 每个头像右下角显示小头像（{id}_2.png），点它即可「以半身像为准」重新裁剪；
 *       - 单张下载 / 按当前筛选批量下载（ZIP 自动附带 FaceConfig.json）；
 *       - 重新导入半身像、修改男女（按目标性别重新分配 ID）、删除；
 *       - 拖拽头像到空白占位格即可换位（只能与空白格互换，删除后位置保留为空白格）。
 *
 *       ID 分配规则：从 3000 起，每 1000 个为一段，
 *       段千位为奇数分配给男性（3000-3999、5000-5999 …），
 *       段千位为偶数分配给女性（4000-4999、6000-6999 …）。
 */

import { useEffect, useMemo, useState } from 'react'
import { toast } from 'sonner'
import {
  Dialog,
  DialogContent,
  DialogDescription,
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
import { Badge } from '@/components/ui/badge'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { CustomFaceCreateDialog } from '@/components/CustomFaceCreateDialog'
import { CustomFaceEditDialog } from '@/components/CustomFaceEditDialog'
import { CustomFaceUsageDialog } from '@/components/CustomFaceUsageDialog'
import { cn } from '@/lib/utils'
import {
  ApiError,
  changeCustomFaceSex,
  downloadCustomFaces,
  fetchCustomFaceUsage,
  moveCustomFace,
  removeCustomFace,
} from '@/lib/api'
import { headIconUrl } from '@/lib/enums'
import { blanksOfSex, nextFaceIdOf, sexOfFaceId } from '@/lib/faceRules'
import type { CustomFace, CustomFaceResult, FaceUsagePerson } from '@/lib/types'
import {
  AlertTriangle,
  ArrowLeftRight,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Crop,
  Download,
  Loader2,
  MoreVertical,
  Move,
  Sparkles,
  Trash2,
  Upload,
  Users,
} from 'lucide-react'

/** 自制头像列表每页可选条数 */
const FACE_PAGE_SIZE_OPTIONS = [24, 48, 96]

/** 自制头像列表默认每页条数 */
const DEFAULT_FACE_PAGE_SIZE = 48

/** 网格单元格：现成头像或空白占位格 */
type Cell =
  | { kind: 'face'; face: CustomFace; id: number }
  | { kind: 'blank'; id: number; sex: number }

interface CustomFaceDialogProps {
  /** 是否打开 */
  open: boolean
  /** 打开状态变化 */
  onOpenChange: (open: boolean) => void
  /** 是否具备制作权限（游客为 false） */
  canWrite: boolean
  /** 默认性别（沿用当前武将的性别） */
  defaultSex: number
  /** 自定义头像列表 */
  faces: CustomFaceResult | null
  /** 制作完成回调，回传新头像 ID */
  onCreated: (id: number) => void
  /** 列表发生变化（新增 / 删除 / 移动 / 改图）后的刷新回调 */
  onFacesChanged?: () => void
  /** 需要登录时的回调 */
  onRequireLogin: () => void
  /**
   * 从「引用武将」面板打开某位武将（切换库并打开其详情）；
   * 未提供时面板只做查看，不显示跳转按钮。
   */
  onOpenPerson?: (lib: string, personId: number) => void
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
  onOpenPerson,
}: CustomFaceDialogProps) {
  /** 上传者筛选：all=全部，none=未记录，其它为上传者账号 */
  const [uploaderFilter, setUploaderFilter] = useState('all')
  /** 性别筛选：null=全部 */
  const [sexFilter, setSexFilter] = useState<number | null>(null)
  const [facePage, setFacePage] = useState(1)
  const [facePageSize, setFacePageSize] = useState(DEFAULT_FACE_PAGE_SIZE)

  /** 创建弹窗 */
  const [createOpen, setCreateOpen] = useState(false)
  /** 引用武将信息面板的目标头像 */
  const [infoFace, setInfoFace] = useState<CustomFace | null>(null)
  /** 图片编辑弹窗（bust=重新导入半身像，face=重裁小头像） */
  const [editTarget, setEditTarget] = useState<{ face: CustomFace; mode: 'bust' | 'face' } | null>(null)
  /** 待删除的头像 ID */
  const [deleteTarget, setDeleteTarget] = useState<number | null>(null)
  /** 待移动的头像与目标空白格 */
  const [moveTarget, setMoveTarget] = useState<CustomFace | null>(null)
  const [moveTo, setMoveTo] = useState('')
  /** 待改性别的头像与目标性别 */
  const [sexTarget, setSexTarget] = useState<CustomFace | null>(null)
  const [targetSex, setTargetSex] = useState(0)

  const [working, setWorking] = useState(false)
  /** 正在拖拽的头像 */
  const [dragging, setDragging] = useState<CustomFace | null>(null)
  /** 拖拽悬停的空白格 */
  const [overId, setOverId] = useState<number | null>(null)
  /** 拖拽落点确认：该头像正被武将使用时先弹确认框（换位会同步改写这些武将的头像） */
  const [dropConfirm, setDropConfirm] = useState<{ face: CustomFace; to: number } | null>(null)
  /** 当前弹窗要展示的武将引用（删除 / 移动 / 改性别前查询，null 表示还没返回） */
  const [usage, setUsage] = useState<FaceUsagePerson[] | null>(null)
  const [usageLoading, setUsageLoading] = useState(false)
  /** 删除头像时对武将引用的处理方式 */
  const [deleteMode, setDeleteMode] = useState<'keep' | 'replace'>('replace')
  /** 删除头像时替换使用的头像 ID */
  const [deleteReplaceId, setDeleteReplaceId] = useState('2000')

  // 每次打开重置筛选与分页
  useEffect(() => {
    if (!open) return
    setUploaderFilter('all')
    setSexFilter(null)
    setFacePage(1)
    setDeleteTarget(null)
    setMoveTarget(null)
    setSexTarget(null)
    setDragging(null)
    setOverId(null)
    setDropConfirm(null)
    setUsage(null)
    setUsageLoading(false)
    setInfoFace(null)
  }, [open])

  const faceItems = faces?.items ?? []

  /** 按上传者 / 性别筛选后的列表 */
  const filteredFaces = useMemo(() => {
    let list = faceItems
    if (uploaderFilter === 'none') list = list.filter((item) => !item.uploader)
    else if (uploaderFilter !== 'all') list = list.filter((item) => item.uploader === uploaderFilter)
    if (sexFilter !== null) list = list.filter((item) => item.sex === sexFilter)
    return list
  }, [faceItems, uploaderFilter, sexFilter])

  /**
   * 网格单元格：现成头像 + 空白占位格（按 ID 混排在同一位置序列里）。
   * 按上传者筛选时不再混入空白格，避免误以为是别人的素材位。
   */
  const cells = useMemo<Cell[]>(() => {
    const list: Cell[] = filteredFaces.map((face) => ({ kind: 'face', face, id: face.id }))
    if (uploaderFilter === 'all' && faces) {
      const blanks = sexFilter === null ? faces.blankIds : blanksOfSex(faces.blankIds, sexFilter)
      for (const id of blanks) list.push({ kind: 'blank', id, sex: sexOfFaceId(id) })
    }
    return list.sort((a, b) => a.id - b.id)
  }, [filteredFaces, faces, uploaderFilter, sexFilter])

  const totalPages = Math.max(1, Math.ceil(cells.length / facePageSize))
  const currentPage = Math.min(facePage, totalPages)
  const pageCells = cells.slice((currentPage - 1) * facePageSize, currentPage * facePageSize)

  /** 统一错误处理：401 时收起弹窗并引导登录 */
  const handleError = (err: unknown, fallback: string) => {
    if (err instanceof ApiError && err.status === 401) {
      toast.error('登录状态已失效，请重新登录后再试')
      onOpenChange(false)
      onRequireLogin()
      return
    }
    toast.error(err instanceof Error ? err.message : fallback)
  }

  /** 下载指定头像（打包为 ZIP，内含自动生成的 FaceConfig.json） */
  const handleDownload = (ids: number[] | null, fileName: string) => {
    if (ids && ids.length === 0) {
      toast.error('当前筛选结果为空')
      return
    }
    downloadCustomFaces(ids, fileName)
    toast.success('已开始下载', {
      description: 'ZIP 内已附带 FaceConfig.json：图片放入游戏 Face 目录，配置放入 Data 目录',
    })
  }

  /**
   * 删除指定头像（ID 会被保留为空白占位格）。
   * 弹窗里已列出引用该头像的武将，这里按玩家选择同步处理：
   * 替换成其它头像，或保留原引用（这些武将会显示为空头像）。
   */
  const handleDelete = async () => {
    if (deleteTarget === null) return
    setWorking(true)
    try {
      const result = await removeCustomFace(
        deleteTarget,
        deleteMode === 'replace'
          ? { mode: 'replace', replaceId: Number(deleteReplaceId) || 0 }
          : { mode: 'keep' },
      )
      const parts = ['该 ID 会保留为空白占位格，可把其它头像拖过去换位']
      if (result.replaced) parts.push(`已同步把 ${result.replaced} 位武将的头像改为 ${result.replaceId}`)
      if (result.dangling) parts.push(`有 ${result.dangling} 位武将仍引用该头像，需要另行修改`)
      toast.success(`已删除头像 ${deleteTarget}`, { description: parts.join('；') })
      setDeleteTarget(null)
      setUsage(null)
      onFacesChanged?.()
    } catch (err) {
      handleError(err, '删除失败')
    } finally {
      setWorking(false)
    }
  }

  /** 移动到空白占位格 */
  const handleMove = async (from: number, to: number) => {
    if (working) return
    setWorking(true)
    try {
      const result = await moveCustomFace(from, to)
      toast.success(`已把头像 ${result.from} 移动到 ${result.to}`, {
        description: result.remapped > 0 ? `同步更新了 ${result.remapped} 位武将的头像引用` : undefined,
      })
      setMoveTarget(null)
      setMoveTo('')
      setDropConfirm(null)
      setUsage(null)
      onFacesChanged?.()
    } catch (err) {
      handleError(err, '移动失败')
    } finally {
      setWorking(false)
      setDragging(null)
      setOverId(null)
    }
  }

  /** 修改性别（按目标性别重新分配 ID） */
  const handleChangeSex = async () => {
    if (!sexTarget) return
    setWorking(true)
    try {
      const result = await changeCustomFaceSex(sexTarget.id, targetSex)
      toast.success(`已把头像 ${result.from} 改为${targetSex === 0 ? '男' : '女'}性，新 ID 为 ${result.to}`, {
        description: result.remapped > 0 ? `同步更新了 ${result.remapped} 位武将的头像引用` : undefined,
      })
      setSexTarget(null)
      onFacesChanged?.()
    } catch (err) {
      handleError(err, '修改性别失败')
    } finally {
      setWorking(false)
    }
  }

  /**
   * 查询某个头像被哪些武将引用（删除 / 改 ID 前提示玩家）。
   * 失败时按「无引用」处理，不阻断操作。
   * @param faceId 头像 ID
   */
  const loadUsage = async (faceId: number) => {
    setUsage(null)
    setUsageLoading(true)
    try {
      const result = await fetchCustomFaceUsage(faceId)
      setUsage(result.persons)
    } catch {
      setUsage([])
    } finally {
      setUsageLoading(false)
    }
  }

  /**
   * 打开删除弹窗：先查引用，并预选一个替换头像
   * （同性别自制头像优先，否则用默认头像 2000）。
   * @param face 目标头像
   */
  const openDeleteDialog = (face: CustomFace) => {
    const fallback = faceItems.find((item) => item.id !== face.id && item.sex === face.sex)
    setDeleteTarget(face.id)
    setDeleteMode(face.usedBy ? 'replace' : 'keep')
    setDeleteReplaceId(String(fallback ? fallback.id : 2000))
    void loadUsage(face.id)
  }

  /** 打开移动弹窗时预选第一个可用空白格，并查一次引用 */
  const openMoveDialog = (face: CustomFace) => {
    const blanks = faces ? blanksOfSex(faces.blankIds, face.sex) : []
    setMoveTarget(face)
    setMoveTo(blanks.length > 0 ? String(blanks[0]) : '')
    void loadUsage(face.id)
  }

  /** 打开改性别弹窗时预选另一个性别，并查一次引用 */
  const openSexDialog = (face: CustomFace) => {
    setSexTarget(face)
    setTargetSex(face.sex === 0 ? 1 : 0)
    void loadUsage(face.id)
  }

  /**
   * 渲染「武将引用」提示块（删除 / 移动 / 改性别弹窗共用）。
   * @param action 说明这些引用会被怎样处理
   * @returns 提示节点
   */
  const renderUsage = (action: string) => (
    <>
      {usageLoading && (
        <p className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
          <Loader2 className="size-3 animate-spin" />
          正在检查武将引用…
        </p>
      )}
      {!usageLoading && usage && usage.length === 0 && (
        <p className="text-[11px] text-muted-foreground">当前没有武将使用该头像。</p>
      )}
      {!usageLoading && usage && usage.length > 0 && (
        <div className="flex flex-col gap-1.5 rounded-md border border-gold/30 bg-gold/5 p-2.5">
          <p className="flex items-center gap-1.5 text-[11px] text-gold">
            <AlertTriangle className="size-3.5" />
            有 {usage.length} 位武将正在使用该头像
          </p>
          <p className="text-[11px] leading-relaxed text-muted-foreground">
            {usage
              .slice(0, 12)
              .map((item) => `${item.name}（${item.libLabel}）`)
              .join('、')}
            {usage.length > 12 && ` 等 ${usage.length} 位`}
          </p>
          <p className="text-[11px] text-muted-foreground">{action}</p>
        </div>
      )}
    </>
  )

  /** 上传者筛选项 */
  const uploaderOptions = useMemo(() => {
    const list = (faces?.uploaders ?? []).map((item) => ({
      value: item.username ?? 'none',
      label: `${item.name}（${item.count}）`,
    }))
    return list
  }, [faces])

  const loading = !faces

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        {/* 基类 DialogContent 带 sm:max-w-lg，需用同前缀的 sm:max-w-* 覆盖 */}
        <DialogContent className="flex max-h-[94vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-[min(76rem,calc(100vw-2rem))]">
          <DialogHeader className="border-b border-border px-5 py-4">
            <DialogTitle className="flex flex-wrap items-center gap-2 font-display text-lg">
              <Sparkles className="size-5 text-primary" />
              自制头像
              {faces && (
                <Badge variant="secondary" className="text-[11px]">
                  共 {faceItems.length} 个
                </Badge>
              )}
              <span className="ml-auto flex items-center gap-2">
                <Button
                  type="button"
                  size="sm"
                  disabled={!canWrite}
                  title={canWrite ? '打开创建界面' : '游客不可制作，请先登录'}
                  onClick={() => {
                    if (!canWrite) {
                      onRequireLogin()
                      return
                    }
                    setCreateOpen(true)
                  }}
                >
                  <Sparkles />
                  制作新头像
                </Button>
              </span>
            </DialogTitle>
            <DialogDescription className="text-xs">
              按上传者或性别筛查；拖拽头像到空白占位格即可换位（只能与空白格互换）；
              点右下角小头像可「以半身像为准」重新裁剪；单张下载走右上角「⋮」菜单，
              批量下载用右侧按钮；头像左上角显示被多少位武将使用，删除或改变 ID
              前会先列出受影响的武将并同步处理其头像引用。
            </DialogDescription>
          </DialogHeader>

          {/* 工具栏：筛选 / 每页条数 / 打包下载 */}
          <div className="flex flex-wrap items-center gap-2 border-b border-border px-5 py-3">
            <Select
              value={uploaderFilter}
              onValueChange={(v) => {
                setUploaderFilter(v)
                setFacePage(1)
              }}
            >
              <SelectTrigger className="h-8 w-[11rem] text-xs" aria-label="按上传者筛查">
                <SelectValue placeholder="全部上传者" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">全部上传者</SelectItem>
                {uploaderOptions.map((item) => (
                  <SelectItem key={item.value} value={item.value}>
                    {item.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select
              value={sexFilter === null ? 'all' : String(sexFilter)}
              onValueChange={(v) => {
                setSexFilter(v === 'all' ? null : Number(v))
                setFacePage(1)
              }}
            >
              <SelectTrigger className="h-8 w-[7rem] text-xs" aria-label="按性别筛查">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">全部性别</SelectItem>
                <SelectItem value="0">男性号段</SelectItem>
                <SelectItem value="1">女性号段</SelectItem>
              </SelectContent>
            </Select>

            <Select
              value={String(facePageSize)}
              onValueChange={(v) => {
                setFacePageSize(Number(v))
                setFacePage(1)
              }}
            >
              <SelectTrigger className="h-8 w-[7.5rem] text-xs" aria-label="每页条数">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {FACE_PAGE_SIZE_OPTIONS.map((size) => (
                  <SelectItem key={size} value={String(size)}>
                    每页 {size} 个
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <span className="text-[11px] text-muted-foreground">
              筛选出 {filteredFaces.length} 个
              {faces && faces.blankIds.length > 0 && ` · 空白占位格 ${faces.blankIds.length} 个`}
            </span>

            <Button
              type="button"
              size="sm"
              variant="outline"
              className="ml-auto"
              disabled={filteredFaces.length === 0}
              onClick={() =>
                handleDownload(
                  filteredFaces.map((item) => item.id),
                  uploaderFilter === 'all' && sexFilter === null
                    ? 'Face_Custom_All.zip'
                    : 'Face_Custom_Filtered.zip',
                )
              }
            >
              <Download />
              打包下载（{filteredFaces.length} 个）
            </Button>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto p-5">
            {loading && (
              <div className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
                <Loader2 className="size-4 animate-spin" />
                正在读取头像列表…
              </div>
            )}

            {!loading && cells.length === 0 && (
              <p className="py-16 text-center text-sm text-muted-foreground">
                没有符合条件的自制头像
                {canWrite && '，点右上角「制作新头像」开始制作'}
              </p>
            )}

            {!loading && cells.length > 0 && (
              <>
                {/* 分页网格：一页只渲染一页，避免上千个缩略图堆在一屏 */}
                <div className="grid grid-cols-4 gap-3 sm:grid-cols-6 lg:grid-cols-8">
                  {pageCells.map((cell) =>
                    cell.kind === 'blank' ? (
                      <div
                        key={`blank-${cell.id}`}
                        className={cn(
                          'relative flex aspect-square flex-col items-center justify-center gap-0.5 rounded-md border border-dashed transition-colors',
                          overId === cell.id
                            ? 'border-primary bg-primary/10'
                            : 'border-border/70 bg-secondary/10',
                        )}
                        title={`空白占位格 ${cell.id}（${cell.sex === 0 ? '男' : '女'}）：把同性别头像拖到这里即可换位`}
                        onDragOver={(e) => {
                          // 只有同性别头像才允许落在该空白格上（否则不阻止默认行为，显示禁止光标）
                          if (!canWrite || !dragging || dragging.sex !== cell.sex) return
                          e.preventDefault()
                          e.dataTransfer.dropEffect = 'move'
                          setOverId(cell.id)
                        }}
                        onDragLeave={() => setOverId((prev) => (prev === cell.id ? null : prev))}
                        onDrop={(e) => {
                          e.preventDefault()
                          setOverId(null)
                          if (!dragging || dragging.sex !== cell.sex) return
                          const face = dragging
                          setDragging(null)
                          // 被武将引用的头像换位会同步改写这些武将的头像，先让玩家确认
                          if ((face.usedBy ?? 0) > 0) {
                            setDropConfirm({ face, to: cell.id })
                            return
                          }
                          void handleMove(face.id, cell.id)
                        }}
                      >
                        <span className="text-[10px] text-muted-foreground">{cell.id}</span>
                        <span className="text-[9px] text-muted-foreground/70">空白</span>
                      </div>
                    ) : (
                      <div
                        key={`face-${cell.id}`}
                        className="group relative aspect-square overflow-hidden rounded-md border border-border bg-secondary/40 transition-colors hover:border-primary/60"
                      >
                        <img
                          src={`${headIconUrl(cell.id, 1)}?v=${cell.face.version}`}
                          alt={`自制头像 ${cell.id}`}
                          loading="lazy"
                          draggable={canWrite}
                          title={`ID ${cell.id}（${cell.face.sex === 0 ? '男' : '女'}）· 上传者 ${
                            cell.face.uploaderName || cell.face.uploader || '未记录'
                          } · 拖拽可换位，下载请用右上角菜单`}
                          className={cn(
                            'size-full object-cover object-top',
                            canWrite && 'cursor-grab active:cursor-grabbing',
                            dragging?.id === cell.id && 'opacity-50',
                          )}
                          onDragStart={(e) => {
                            e.dataTransfer.effectAllowed = 'move'
                            setDragging(cell.face)
                          }}
                          onDragEnd={() => {
                            setDragging(null)
                            setOverId(null)
                          }}
                          onError={(e) => {
                            e.currentTarget.style.visibility = 'hidden'
                          }}
                        />

                        {/* 引用标记：有武将正在使用该头像时给出提示（删除 / 改 ID 会再确认） */}
                        {(cell.face.usedBy ?? 0) > 0 && (
                          <button
                            type="button"
                            className="absolute left-1 top-1 flex items-center gap-0.5 rounded bg-background/85 px-1 text-[9px] leading-tight text-gold transition-colors hover:bg-background"
                            title={`被 ${cell.face.usedBy} 位武将使用，点击查看引用武将`}
                            onClick={(e) => {
                              e.stopPropagation()
                              setInfoFace(cell.face)
                            }}
                          >
                            <Users className="size-2.5" />
                            {cell.face.usedBy}
                          </button>
                        )}

                        {/* ID 标签 */}
                        <span className="pointer-events-none absolute bottom-0 left-0 rounded-tr bg-background/80 px-1 text-[9px] leading-tight text-muted-foreground">
                          {cell.id}
                        </span>

                        {/* 右下角小头像：点击即以半身像为准重新裁剪 */}
                        <button
                          type="button"
                          title={`小头像 ${cell.id}（点击以半身像为准重新裁剪）`}
                          className="absolute bottom-1 right-1 overflow-hidden rounded border border-background bg-background/80 shadow-sm transition-transform hover:scale-105"
                          onClick={(e) => {
                            e.stopPropagation()
                            if (!canWrite) {
                              onRequireLogin()
                              return
                            }
                            setEditTarget({ face: cell.face, mode: 'face' })
                          }}
                        >
                          <img
                            src={`${headIconUrl(cell.id, 2)}?v=${cell.face.version}`}
                            alt={`小头像 ${cell.id}`}
                            loading="lazy"
                            className="h-9 w-[1.8rem] object-cover"
                            onError={(e) => {
                              e.currentTarget.style.visibility = 'hidden'
                            }}
                          />
                        </button>

                        {/*
                          操作菜单（悬停显示）。
                          缩略图本身不再响应点击下载，下载统一走这里（游客也能下载单张）。
                        */}
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <button
                              type="button"
                              title="下载 / 管理该头像"
                              className="absolute right-1 top-1 rounded-full bg-background/80 p-1 text-foreground opacity-0 transition-opacity group-hover:opacity-100"
                            >
                              <MoreVertical className="size-3.5" />
                            </button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" className="text-xs">
                            <DropdownMenuLabel className="text-[11px] font-normal text-muted-foreground">
                              ID {cell.id} · {cell.face.uploaderName || cell.face.uploader || '未记录上传者'}
                              {(cell.face.usedBy ?? 0) > 0 && ` · 被 ${cell.face.usedBy} 位武将使用`}
                            </DropdownMenuLabel>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem onClick={() => setInfoFace(cell.face)}>
                              <Users />
                              查看引用武将…
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              onClick={() => handleDownload([cell.id], `Face_${cell.id}.zip`)}
                            >
                              <Download />
                              下载该头像
                            </DropdownMenuItem>
                            {canWrite && (
                              <>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  onClick={() => setEditTarget({ face: cell.face, mode: 'face' })}
                                >
                                  <Crop />
                                  编辑小头像（以半身像为准）
                                </DropdownMenuItem>
                                <DropdownMenuItem
                                  onClick={() => setEditTarget({ face: cell.face, mode: 'bust' })}
                                >
                                  <Upload />
                                  重新导入半身像
                                </DropdownMenuItem>
                                <DropdownMenuItem onClick={() => openMoveDialog(cell.face)}>
                                  <Move />
                                  移动到空白格…
                                </DropdownMenuItem>
                                <DropdownMenuItem onClick={() => openSexDialog(cell.face)}>
                                  <ArrowLeftRight />
                                  修改男女…
                                </DropdownMenuItem>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-destructive focus:text-destructive"
                                  onClick={() => openDeleteDialog(cell.face)}
                                >
                                  <Trash2 />
                                  删除（保留空白格）
                                </DropdownMenuItem>
                              </>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </div>
                    ),
                  )}
                </div>

                {/* 分页工具栏 */}
                <div className="mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-border pt-3 text-[11px] text-muted-foreground">
                  <span>
                    共 {cells.length} 格（含空白占位格），本页显示第{' '}
                    {(currentPage - 1) * facePageSize + 1} ~ {Math.min(currentPage * facePageSize, cells.length)} 个
                    {dragging && ' · 正在拖拽：放开到同性别空白格即可换位'}
                  </span>
                  <div className="flex flex-wrap items-center gap-1">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-7 px-2"
                      disabled={currentPage <= 1}
                      onClick={() => setFacePage(1)}
                      aria-label="第一页"
                    >
                      <ChevronsLeft />
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-7 px-2"
                      disabled={currentPage <= 1}
                      onClick={() => setFacePage(currentPage - 1)}
                      aria-label="上一页"
                    >
                      <ChevronLeft />
                    </Button>
                    <span className="px-2">
                      第 {currentPage} / {totalPages} 页
                    </span>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-7 px-2"
                      disabled={currentPage >= totalPages}
                      onClick={() => setFacePage(currentPage + 1)}
                      aria-label="下一页"
                    >
                      <ChevronRight />
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-7 px-2"
                      disabled={currentPage >= totalPages}
                      onClick={() => setFacePage(totalPages)}
                      aria-label="最后一页"
                    >
                      <ChevronsRight />
                    </Button>
                  </div>
                </div>
              </>
            )}
          </div>
        </DialogContent>

        {/* 删除确认：先检查武将引用，再让玩家决定是否同步替换 */}
        <AlertDialog
          open={deleteTarget !== null}
          onOpenChange={(o) => {
            if (!o) {
              setDeleteTarget(null)
              setUsage(null)
            }
          }}
        >
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>删除自制头像</AlertDialogTitle>
              <AlertDialogDescription>
                确定要删除 ID 为 {deleteTarget} 的自制头像吗？半身像与小头像文件都会被删除，该操作不可撤销。
                删除后该 ID 会保留为空白占位格，可把其它头像拖过去换位。
              </AlertDialogDescription>
            </AlertDialogHeader>

            <div className="flex flex-col gap-3">
              {renderUsage(
                deleteMode === 'replace'
                  ? '删除后这些武将的头像会同步替换为下面选择的头像。'
                  : '这些武将的头像字段会保留原 ID，游戏中将显示为空头像。',
              )}

              {usage && usage.length > 0 && (
                <div className="flex flex-col gap-2 border-t border-border pt-3">
                  <span className="text-[11px] text-muted-foreground">这些武将的头像如何处理</span>
                  <label className="flex cursor-pointer items-center gap-2 text-xs">
                    <input
                      type="radio"
                      name="delete-mode"
                      className="size-3.5 accent-primary"
                      checked={deleteMode === 'replace'}
                      onChange={() => setDeleteMode('replace')}
                    />
                    同步替换为其它头像
                  </label>
                  {deleteMode === 'replace' && (
                    <Select value={deleteReplaceId} onValueChange={setDeleteReplaceId}>
                      <SelectTrigger className="h-8 w-full text-xs" aria-label="替换使用的头像">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="2000">默认头像 2000</SelectItem>
                        {faceItems
                          .filter((item) => item.id !== deleteTarget)
                          .map((item) => (
                            <SelectItem key={item.id} value={String(item.id)}>
                              自制头像 {item.id}（{item.sex === 0 ? '男' : '女'}）
                            </SelectItem>
                          ))}
                      </SelectContent>
                    </Select>
                  )}
                  <label className="flex cursor-pointer items-center gap-2 text-xs">
                    <input
                      type="radio"
                      name="delete-mode"
                      className="size-3.5 accent-primary"
                      checked={deleteMode === 'keep'}
                      onChange={() => setDeleteMode('keep')}
                    />
                    保留原引用（这些武将会变成空头像）
                  </label>
                </div>
              )}
            </div>

            <AlertDialogFooter>
              <AlertDialogCancel disabled={working}>取消</AlertDialogCancel>
              <AlertDialogAction
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                disabled={working}
                onClick={(e) => {
                  e.preventDefault()
                  handleDelete()
                }}
              >
                {working && <Loader2 className="animate-spin" />}
                确认删除
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {/* 移动到空白格 */}
        <AlertDialog
          open={moveTarget !== null}
          onOpenChange={(o) => {
            if (!o) {
              setMoveTarget(null)
              setMoveTo('')
            }
          }}
        >
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>移动到空白占位格</AlertDialogTitle>
              <AlertDialogDescription>
                头像 {moveTarget?.id}（{moveTarget?.sex === 0 ? '男' : '女'}）只能移动到同性别号段的空白格；
                搬走后原位置会保留为新的空白格。武将的头像引用会自动同步到新 ID。
              </AlertDialogDescription>
            </AlertDialogHeader>
            <div className="flex flex-col gap-2">
              {renderUsage(`移动后这些武将的头像会同步改为 ${moveTo || '目标 ID'}。`)}
              <Select value={moveTo} onValueChange={setMoveTo}>
                <SelectTrigger className="h-8 w-full text-xs" aria-label="目标空白格">
                  <SelectValue placeholder="没有可用的空白占位格" />
                </SelectTrigger>
                <SelectContent>
                  {moveTarget &&
                    blanksOfSex(faces?.blankIds ?? [], moveTarget.sex).map((id) => (
                      <SelectItem key={id} value={String(id)}>
                        {id}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
              {moveTarget && (faces?.blankIds ?? []).filter((id) => sexOfFaceId(id) === moveTarget.sex).length === 0 && (
                <p className="text-[11px] text-destructive">
                  当前没有同性别空白格；可以先删除无用头像制造空白格。
                </p>
              )}
            </div>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={working}>取消</AlertDialogCancel>
              <AlertDialogAction
                disabled={working || !moveTo}
                onClick={(e) => {
                  e.preventDefault()
                  if (moveTarget && moveTo) void handleMove(moveTarget.id, Number(moveTo))
                }}
              >
                {working && <Loader2 className="animate-spin" />}
                确认移动
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {/* 修改男女 */}
        <AlertDialog
          open={sexTarget !== null}
          onOpenChange={(o) => {
            if (!o) setSexTarget(null)
          }}
        >
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>修改头像性别</AlertDialogTitle>
              <AlertDialogDescription>
                头像 ID 的千位决定性别，改性别后必须重新编号：图片会改名搬移到目标性别号段的下一个可用
                ID，武将中引用该头像的武将（headIconID）会一并更新。原 ID 保留为空白占位格。
              </AlertDialogDescription>
            </AlertDialogHeader>
            <div className="flex flex-col gap-3">
              {renderUsage(
                `改性别后这些武将的头像会同步改为新分配的 ID（${
                  nextFaceIdOf(faces, targetSex) || '号段已满'
                }）。`,
              )}
              <div className="flex gap-2">
                {[
                  { value: 0, label: '男性' },
                  { value: 1, label: '女性' },
                ].map((item) => (
                  <Button
                    key={item.value}
                    type="button"
                    variant={targetSex === item.value ? 'default' : 'outline'}
                    className="flex-1"
                    disabled={sexTarget?.sex === item.value}
                    onClick={() => setTargetSex(item.value)}
                  >
                    {item.label}
                  </Button>
                ))}
              </div>
              <p className="text-[11px] text-muted-foreground">
                {sexTarget && (
                  <>
                    {sexTarget.id} →{' '}
                    <span className="font-medium text-primary">
                      {nextFaceIdOf(faces, targetSex) || '（号段已满）'}
                    </span>
                    （按当前列表推算，实际以服务端分配为准）
                  </>
                )}
              </p>
            </div>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={working}>取消</AlertDialogCancel>
              <AlertDialogAction
                disabled={working || !sexTarget || sexTarget.sex === targetSex}
                onClick={(e) => {
                  e.preventDefault()
                  void handleChangeSex()
                }}
              >
                {working && <Loader2 className="animate-spin" />}
                确认修改
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {/* 拖拽换位确认：仅当该头像正被武将使用时弹出（避免无意识改动武将头像） */}
        <AlertDialog open={dropConfirm !== null} onOpenChange={(o) => !o && setDropConfirm(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>移动头像并同步武将引用</AlertDialogTitle>
              <AlertDialogDescription>
                头像 {dropConfirm?.face.id} 正在被 {dropConfirm?.face.usedBy} 位武将使用，
                移动到空白格 {dropConfirm?.to} 后，这些武将的头像会同步改为 {dropConfirm?.to}。
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={working}>取消</AlertDialogCancel>
              <AlertDialogAction
                disabled={working}
                onClick={(e) => {
                  e.preventDefault()
                  if (dropConfirm) void handleMove(dropConfirm.face.id, dropConfirm.to)
                }}
              >
                {working && <Loader2 className="animate-spin" />}
                确认移动
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </Dialog>

      {/* 创建界面：只有点击「制作新头像」才会出现 */}
      <CustomFaceCreateDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        canWrite={canWrite}
        defaultSex={defaultSex}
        faces={faces}
        onCreated={(id) => {
          onCreated(id)
          onFacesChanged?.()
        }}
        onFacesChanged={onFacesChanged}
        onRequireLogin={() => {
          setCreateOpen(false)
          onRequireLogin()
        }}
      />

      {/* 图片编辑（重新导入半身像 / 以半身像为准重裁小头像） */}
      <CustomFaceEditDialog
        open={editTarget !== null}
        onOpenChange={(o) => !o && setEditTarget(null)}
        face={editTarget?.face ?? null}
        mode={editTarget?.mode ?? 'face'}
        onSaved={onFacesChanged}
        onRequireLogin={() => {
          setEditTarget(null)
          onRequireLogin()
        }}
      />

      {/* 引用武将信息面板：查看该头像的完整信息与被哪些武将引用 */}
      <CustomFaceUsageDialog
        open={infoFace !== null}
        onOpenChange={(o) => !o && setInfoFace(null)}
        face={infoFace}
        onOpenPerson={
          onOpenPerson
            ? (libKey, personId) => {
                // 跳转到武将详情前先收起面板，避免两个弹窗叠在一起
                setInfoFace(null)
                onOpenPerson(libKey, personId)
              }
            : undefined
        }
      />
    </>
  )
}
