/**
 * 文件名：ImageCropper.tsx
 * 描述：图片裁剪组件。
 *       - 采用 Canvas 实时绘制，源图按「铺满视口」为基准进行缩放与平移；
 *       - 支持鼠标拖拽、滚轮缩放，同时支持移动端触摸拖拽与双指捏合缩放；
 *       - 通过 ref 暴露 toDataURL()，按目标输出尺寸导出 PNG（dataURL）。
 *
 *       坐标约定：
 *       - offset 为「图片中心相对视口中心」的位移，单位为 CSS 像素；
 *       - 导出时按 输出宽度 / 视口宽度 的比例整体换算，保证所见即所得。
 */

import { useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react'

/** 允许的最小缩放倍数（1 = 刚好铺满视口） */
const MIN_ZOOM = 1

/** 允许的最大缩放倍数 */
const MAX_ZOOM = 8

/** 单指手势信息 */
interface DragGesture {
  mode: 'drag'
  startX: number
  startY: number
  startOffset: { x: number; y: number }
}

/** 双指手势信息 */
interface PinchGesture {
  mode: 'pinch'
  startDistance: number
  startZoom: number
  startOffset: { x: number; y: number }
}

type Gesture = DragGesture | PinchGesture

/** 组件对外暴露的方法 */
export interface ImageCropperHandle {
  /** 按输出尺寸导出 PNG（dataURL），失败返回空字符串 */
  toDataURL: () => string
  /** 重置为初始铺满状态 */
  reset: () => void
}

interface ImageCropperProps {
  /** 已加载完成的源图片 */
  image: HTMLImageElement
  /** 视口宽高比（宽 / 高），与输出宽高比保持一致 */
  aspect: number
  /** 输出宽度（像素） */
  outputWidth: number
  /** 输出高度（像素） */
  outputHeight: number
  /** 视口宽度（CSS 像素），高度由宽高比推导 */
  viewWidth?: number
  /** 外部 ref，用于获取导出方法 */
  ref?: React.Ref<ImageCropperHandle>
}

/**
 * 计算两个触点之间的距离。
 * @param a 触点 A
 * @param b 触点 B
 * @returns 距离
 */
function distanceOf(a: { x: number; y: number }, b: { x: number; y: number }): number {
  return Math.hypot(a.x - b.x, a.y - b.y)
}

/**
 * 将数值限制在指定区间内。
 * @param value 数值
 * @param min 下限
 * @param max 上限
 * @returns 限制后的数值
 */
function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

export function ImageCropper({
  image,
  aspect,
  outputWidth,
  outputHeight,
  viewWidth = 260,
  ref,
}: ImageCropperProps) {
  /** 视口高度（CSS 像素） */
  const viewHeight = Math.round(viewWidth / aspect)

  const [zoom, setZoom] = useState(MIN_ZOOM)
  const [offset, setOffset] = useState({ x: 0, y: 0 })

  const canvasRef = useRef<HTMLCanvasElement>(null)
  /** 当前按下的指针集合（用于识别单指 / 双指手势） */
  const pointersRef = useRef(new Map<number, { x: number; y: number }>())
  /** 当前手势基准数据 */
  const gestureRef = useRef<Gesture | null>(null)

  /** 源图在「铺满视口」时的基准缩放倍数 */
  const baseScale = useMemo(() => {
    const width = image?.naturalWidth || 0
    const height = image?.naturalHeight || 0
    if (!width || !height) return 1
    return Math.max(viewWidth / width, viewHeight / height)
  }, [image, viewWidth, viewHeight])

  /**
   * 限制位移，保证图片始终覆盖整个视口（不出现留白）。
   * @param next 目标位移
   * @param nextZoom 目标缩放倍数
   * @returns 限制后的位移
   */
  const clampOffset = useCallback(
    (next: { x: number; y: number }, nextZoom: number) => {
      const width = (image?.naturalWidth || 0) * baseScale * nextZoom
      const height = (image?.naturalHeight || 0) * baseScale * nextZoom
      const maxX = Math.max(0, (width - viewWidth) / 2)
      const maxY = Math.max(0, (height - viewHeight) / 2)
      return {
        x: clamp(next.x, -maxX, maxX),
        y: clamp(next.y, -maxY, maxY),
      }
    },
    [image, baseScale, viewWidth, viewHeight],
  )

  /** 重置为初始铺满状态 */
  const reset = useCallback(() => {
    setZoom(MIN_ZOOM)
    setOffset({ x: 0, y: 0 })
  }, [])

  // 源图变化时回到初始状态
  useEffect(() => {
    reset()
  }, [image, reset])

  // 实时绘制到视口画布
  useEffect(() => {
    const canvas = canvasRef.current
    const width = image?.naturalWidth || 0
    const height = image?.naturalHeight || 0
    if (!canvas || !width || !height) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return

    // 按设备像素比渲染，保证高分屏下的显示清晰度
    const dpr = Math.min(window.devicePixelRatio || 1, 2)
    canvas.width = Math.round(viewWidth * dpr)
    canvas.height = Math.round(viewHeight * dpr)

    const drawWidth = width * baseScale * zoom
    const drawHeight = height * baseScale * zoom
    const drawX = (viewWidth - drawWidth) / 2 + offset.x
    const drawY = (viewHeight - drawHeight) / 2 + offset.y

    ctx.clearRect(0, 0, canvas.width, canvas.height)
    ctx.imageSmoothingEnabled = true
    ctx.imageSmoothingQuality = 'high'
    ctx.drawImage(
      image,
      drawX * dpr,
      drawY * dpr,
      drawWidth * dpr,
      drawHeight * dpr,
    )
  }, [image, baseScale, zoom, offset, viewWidth, viewHeight])

  // 滚轮缩放（需使用非 passive 监听才能阻止页面滚动）
  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const onWheel = (event: WheelEvent) => {
      event.preventDefault()
      setZoom((prev) => {
        const next = clamp(prev * (event.deltaY < 0 ? 1.1 : 0.9), MIN_ZOOM, MAX_ZOOM)
        setOffset((current) => clampOffset(current, next))
        return next
      })
    }
    canvas.addEventListener('wheel', onWheel, { passive: false })
    return () => canvas.removeEventListener('wheel', onWheel)
  }, [clampOffset])

  /**
   * 按输出尺寸导出 PNG。
   * @returns PNG 的 dataURL；导出条件不满足时返回空字符串
   */
  const toDataURL = useCallback(() => {
    const width = image?.naturalWidth || 0
    const height = image?.naturalHeight || 0
    if (!width || !height) return ''
    const canvas = document.createElement('canvas')
    canvas.width = outputWidth
    canvas.height = outputHeight
    const ctx = canvas.getContext('2d')
    if (!ctx) return ''

    // 视口坐标 -> 输出坐标的换算比例
    const ratio = outputWidth / viewWidth
    const drawWidth = width * baseScale * zoom
    const drawHeight = height * baseScale * zoom
    const drawX = (viewWidth - drawWidth) / 2 + offset.x
    const drawY = (viewHeight - drawHeight) / 2 + offset.y

    ctx.imageSmoothingEnabled = true
    ctx.imageSmoothingQuality = 'high'
    ctx.drawImage(
      image,
      0,
      0,
      width,
      height,
      drawX * ratio,
      drawY * ratio,
      drawWidth * ratio,
      drawHeight * ratio,
    )
    return canvas.toDataURL('image/png')
  }, [image, baseScale, zoom, offset, viewWidth, outputWidth, outputHeight])

  useImperativeHandle(ref, () => ({ toDataURL, reset }), [toDataURL, reset])

  /**
   * 处理指针按下：单指进入拖拽，双指进入捏合缩放。
   * @param event 指针事件
   */
  const handlePointerDown = (event: React.PointerEvent<HTMLCanvasElement>) => {
    event.currentTarget.setPointerCapture(event.pointerId)
    pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY })

    if (pointersRef.current.size === 1) {
      gestureRef.current = {
        mode: 'drag',
        startX: event.clientX,
        startY: event.clientY,
        startOffset: { ...offset },
      }
    } else if (pointersRef.current.size === 2) {
      const points = [...pointersRef.current.values()]
      gestureRef.current = {
        mode: 'pinch',
        startDistance: Math.max(1, distanceOf(points[0], points[1])),
        startZoom: zoom,
        startOffset: { ...offset },
      }
    }
  }

  /**
   * 处理指针移动：按手势类型更新位移或缩放。
   * 每次均以手势起点为基准计算，避免累积误差。
   * @param event 指针事件
   */
  const handlePointerMove = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (!pointersRef.current.has(event.pointerId)) return
    pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY })
    const gesture = gestureRef.current
    if (!gesture) return

    if (gesture.mode === 'drag' && pointersRef.current.size === 1) {
      const next = {
        x: gesture.startOffset.x + (event.clientX - gesture.startX),
        y: gesture.startOffset.y + (event.clientY - gesture.startY),
      }
      setOffset(clampOffset(next, zoom))
      return
    }

    if (gesture.mode === 'pinch' && pointersRef.current.size === 2) {
      const points = [...pointersRef.current.values()]
      const current = Math.max(1, distanceOf(points[0], points[1]))
      const nextZoom = clamp(
        (gesture.startZoom * current) / gesture.startDistance,
        MIN_ZOOM,
        MAX_ZOOM,
      )
      setZoom(nextZoom)
      setOffset(clampOffset(gesture.startOffset, nextZoom))
    }
  }

  /**
   * 处理指针抬起：双指回到单指时以剩余触点重建拖拽基准。
   * @param event 指针事件
   */
  const handlePointerUp = (event: React.PointerEvent<HTMLCanvasElement>) => {
    pointersRef.current.delete(event.pointerId)
    if (pointersRef.current.size === 1) {
      const [remaining] = [...pointersRef.current.values()]
      gestureRef.current = {
        mode: 'drag',
        startX: remaining.x,
        startY: remaining.y,
        startOffset: { ...offset },
      }
    } else if (pointersRef.current.size === 0) {
      gestureRef.current = null
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <div
        className="relative overflow-hidden rounded-lg border border-border bg-secondary/40"
        style={{ width: viewWidth, height: viewHeight }}
      >
        <canvas
          ref={canvasRef}
          className="size-full cursor-grab touch-none active:cursor-grabbing"
          style={{ width: viewWidth, height: viewHeight }}
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
          onPointerCancel={handlePointerUp}
        />
        {/* 三分线辅助构图 */}
        <div className="pointer-events-none absolute inset-0">
          <div className="absolute left-1/3 top-0 h-full w-px bg-white/25" />
          <div className="absolute left-2/3 top-0 h-full w-px bg-white/25" />
          <div className="absolute left-0 top-1/3 h-px w-full bg-white/25" />
          <div className="absolute left-0 top-2/3 h-px w-full bg-white/25" />
        </div>
      </div>

      <div className="flex items-center gap-2">
        <span className="shrink-0 text-xs text-muted-foreground">缩放</span>
        <input
          type="range"
          className="h-1.5 w-full cursor-pointer appearance-none rounded-full bg-secondary accent-primary"
          min={MIN_ZOOM}
          max={MAX_ZOOM}
          step={0.01}
          value={zoom}
          onChange={(e) => {
            const next = clamp(Number(e.target.value), MIN_ZOOM, MAX_ZOOM)
            setZoom(next)
            setOffset((current) => clampOffset(current, next))
          }}
        />
        <span className="w-10 shrink-0 text-right text-xs tabular-nums text-muted-foreground">
          {zoom.toFixed(1)}x
        </span>
      </div>

      <p className="text-[11px] leading-relaxed text-muted-foreground">
        拖动图片调整位置，滚轮或双指捏合缩放；输出尺寸 {outputWidth}×{outputHeight} 像素。
      </p>
    </div>
  )
}
