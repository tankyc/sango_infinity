/**
 * 列宽自适应
 *
 * 表格原先按字段类型给固定列宽（多数列统一 130px），结果「性别」这种只放一个字的列
 * 也占掉一整条 130px，整张表被撑得又宽又稀，列与列之间看上去「离得太远」。
 *
 * 这里改为**按实际内容测量**：逐列取
 * 「表头所需宽度」与「该列所有单元格文本所需宽度」的最大值，
 * 再用上下限夹住，既紧凑又不至于窄到看不清。
 *
 * 测量走 canvas 的 `measureText`（带缓存），比按字符数估算准；
 * 重复值（「男」在几百行里出现）只会真正测量一次。
 */
import type { Entity, Options } from './types'
import type { FieldDef } from './schema'
import { formatFieldValue, type NameLookup } from './fields'

/** 单元格字体（与表格单元格的 text-[12px] 保持一致） */
const CELL_FONT = '12px ui-sans-serif, system-ui, sans-serif'
/** 表头字体（与表头的 text-[11px] 保持一致） */
const HEADER_FONT = '11px ui-sans-serif, system-ui, sans-serif'
/** 表头字号回落值 */
const HEADER_FALLBACK_SIZE = 11
/** 单元格字号回落值 */
const CELL_FALLBACK_SIZE = 12
/**
 * 单元格左右内边距合计。
 *
 * 对应 `px-2` 的 16px，另留 2px 余量：canvas 的测量结果与实际排版之间
 * 可能存在零点几像素的差异，留一点余量可以避免文字正好贴着边界。
 */
const CELL_PADDING_X = 18
/** 表头额外占位：`gap-1` 的 4px + 排序图标 `h-3` 的 12px */
const HEADER_EXTRA = 16
/** 列分隔线占位 */
const BORDER_WIDTH = 1
/** 绝对下限：再窄就点不中，也放不下表头 */
const ABSOLUTE_MIN = 40

/**
 * 创建带缓存的文本测量器。
 *
 * canvas 不可用时（单元测试 / 非浏览器环境）回落到按字符估算：
 * CJK 及全角标点约 1em，ASCII 约 0.55em。估算偏保守，仅作兜底。
 *
 * @param font canvas 字体描述
 * @param fallbackSize 估算用的字号
 * @returns 测量函数
 */
function createMeasurer(font: string, fallbackSize: number): (text: string) => number {
  const cache = new Map<string, number>()
  let ctx: CanvasRenderingContext2D | null | undefined

  return (text: string): number => {
    if (text === '') return 0
    const hit = cache.get(text)
    if (hit !== undefined) return hit

    // 只探测一次画布；不可用时置 null，后续全部走估算分支
    if (ctx === undefined) {
      ctx = typeof document === 'undefined' ? null : document.createElement('canvas').getContext('2d')
      if (ctx) ctx.font = font
    }

    let width: number
    if (ctx) {
      width = ctx.measureText(text).width
    } else {
      let em = 0
      for (const ch of text) em += /[\u2E80-\uFFFF]/.test(ch) ? 1 : 0.55
      width = em * fallbackSize
    }

    cache.set(text, width)
    return width
  }
}

/**
 * 取某字段的列宽上下限。
 *
 * 下限保证表头与常见值放得下，上限防止个别超长值（或长字段标签）把整列拉爆。
 *
 * @param field 字段定义
 * @returns 宽度区间（px）
 */
export function columnBounds(field: FieldDef): { min: number; max: number } {
  switch (field.kind) {
    case 'string':
      // 姓名列值都短但必读，给一个够放 3~4 字的宽度
      return field.key === 'Name' ? { min: 72, max: 190 } : { min: 64, max: 240 }
    case 'polyRef':
    case 'intArray':
    case 'pairArray':
      // 数组型显示为「甲、乙、丙 等 N 个」，需要更宽
      return { min: 76, max: 260 }
    case 'attr':
    case 'ability':
      // 五维是两位数、适性是「Ｃ/Ａ/Ｓ１」，都很窄
      return { min: 40, max: 72 }
    case 'enum':
      return { min: 56, max: 150 }
    case 'float':
      return { min: 56, max: 110 }
    default:
      // int
      return { min: 52, max: 140 }
  }
}

/** 计算列宽的入参 */
export interface AutoColumnInput {
  /** 参与显示的列 */
  columns: FieldDef[]
  /** 参与测量的行（通常是当前筛选结果） */
  rows: Entity[]
  /** 公共数据表 */
  options: Options | null
  /** 引用名称查找表，用于把 ID 还原成实际显示的文本 */
  lookup?: NameLookup | null
}

/**
 * 逐列算出内容自适应宽度。
 *
 * @param input 计算入参
 * @returns 字段 key -> 宽度（px）
 */
export function computeColumnWidths({ columns, rows, options, lookup }: AutoColumnInput): Record<string, number> {
  const cellMeasure = createMeasurer(CELL_FONT, CELL_FALLBACK_SIZE)
  const headerMeasure = createMeasurer(HEADER_FONT, HEADER_FALLBACK_SIZE)
  const result: Record<string, number> = {}

  for (const field of columns) {
    // 表头本身就是宽度下限之一：标签 + 排序箭头 + 内边距
    let need = headerMeasure(field.label) + HEADER_EXTRA + CELL_PADDING_X

    for (const row of rows) {
      // 用与单元格完全相同的格式化函数，保证「量什么就显示什么」
      const text = formatFieldValue(row, field, options, lookup)
      if (!text) continue
      const width = cellMeasure(text) + CELL_PADDING_X
      if (width > need) need = width
    }

    const { min, max } = columnBounds(field)
    const clamped = Math.min(Math.max(Math.ceil(need) + BORDER_WIDTH, min, ABSOLUTE_MIN), max)
    // 量化到 2px 网格，减少编辑内容时列宽细碎跳动
    result[field.key] = Math.ceil(clamped / 2) * 2
  }

  return result
}
