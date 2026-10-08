/**
 * 虚拟滚动 Hook
 *
 * 剧本中武将多达 850+ 条，若一次性渲染全部行会造成明显卡顿。
 * 该 Hook 只渲染视口内（含预渲染缓冲）的行，使滚动始终保持在 60fps。
 *
 * 两个关键实现细节：
 * 1. 滚动回调经 requestAnimationFrame 节流，避免高频 setState 导致掉帧；
 * 2. 容器使用「回调 ref + state」而非 useRef，因为该列表常被放在
 *    Dialog / Sheet 中，容器会随对话框开关而挂载与卸载，
 *    只有元素进入 state 才能让 ResizeObserver 正确地重新绑定。
 */
import { useCallback, useEffect, useRef, useState } from 'react'

export interface VirtualItem {
  index: number
  /** 相对内容顶部的偏移量 */
  offset: number
}

export interface VirtualListResult {
  /** 滚动容器回调 ref，必须挂到实际滚动元素上 */
  containerRef: (node: HTMLDivElement | null) => void
  /** 需要渲染的行 */
  virtualItems: VirtualItem[]
  /** 内容总高度（用于撑开滚动条） */
  totalHeight: number
  /** 滚动事件处理 */
  onScroll: (event: React.UIEvent<HTMLDivElement>) => void
  /** 滚动到指定行 */
  scrollToIndex: (index: number, behavior?: ScrollBehavior) => void
  /** 当前可视高度 */
  viewportHeight: number
}

export interface UseVirtualListOptions {
  /** 总行数 */
  count: number
  /** 行高（像素，必须固定） */
  itemHeight: number
  /** 上下预渲染行数 */
  overscan?: number
}

/**
 * 创建虚拟滚动列表。
 *
 * @param options 配置项
 * @returns 虚拟列表状态与方法
 */
export function useVirtualList({ count, itemHeight, overscan = 8 }: UseVirtualListOptions): VirtualListResult {
  const [element, setElement] = useState<HTMLDivElement | null>(null)
  const [scrollTop, setScrollTop] = useState(0)
  const [viewportHeight, setViewportHeight] = useState(480)
  const frameRef = useRef<number | null>(null)
  const pendingTopRef = useRef(0)

  /** 回调 ref：容器挂载/卸载时同步到 state，触发观察者重新绑定 */
  const containerRef = useCallback((node: HTMLDivElement | null) => {
    setElement(node)
  }, [])

  // 监听容器尺寸变化
  useEffect(() => {
    if (!element) return
    const update = () => setViewportHeight(element.clientHeight)
    update()
    const observer = new ResizeObserver(update)
    observer.observe(element)
    return () => observer.disconnect()
  }, [element])

  const onScroll = useCallback((event: React.UIEvent<HTMLDivElement>) => {
    pendingTopRef.current = event.currentTarget.scrollTop
    if (frameRef.current !== null) return
    frameRef.current = requestAnimationFrame(() => {
      frameRef.current = null
      setScrollTop(pendingTopRef.current)
    })
  }, [])

  useEffect(
    () => () => {
      if (frameRef.current !== null) cancelAnimationFrame(frameRef.current)
    },
    []
  )

  const start = Math.max(0, Math.floor(scrollTop / itemHeight) - overscan)
  const visibleCount = Math.ceil(viewportHeight / itemHeight) + overscan * 2
  const end = Math.min(count, start + visibleCount)

  const virtualItems: VirtualItem[] = []
  for (let i = start; i < end; i++) {
    virtualItems.push({ index: i, offset: i * itemHeight })
  }

  const scrollToIndex = useCallback(
    (index: number, behavior: ScrollBehavior = 'auto') => {
      if (!element) return
      const top = index * itemHeight
      const bottom = top + itemHeight
      if (top < element.scrollTop) {
        element.scrollTo({ top, behavior })
      } else if (bottom > element.scrollTop + element.clientHeight) {
        element.scrollTo({ top: bottom - element.clientHeight, behavior })
      }
    },
    [element, itemHeight]
  )

  return {
    containerRef,
    virtualItems,
    totalHeight: count * itemHeight,
    onScroll,
    scrollToIndex,
    viewportHeight,
  }
}
