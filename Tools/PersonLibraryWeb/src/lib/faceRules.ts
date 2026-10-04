/**
 * 文件名：faceRules.ts
 * 描述：自定义头像 ID 规则（前端侧，与后端 server/faceStore.js 保持一致）。
 *
 *       规则说明：
 *       - 自定义头像 ID 从 3000 起分配；
 *       - 每 1000 个 ID 为一段，段千位为奇数时归男性，为偶数时归女性：
 *           男性：3000-3999、5000-5999、7000-7999 ……
 *           女性：4000-4999、6000-6999、8000-8999 ……
 *       - 某一段用满（到达 x999）后，下一位跳至本性别所属的下一个号段，
 *         始终满足「单数千位为男、双数千位为女」。
 */

import type { CustomFaceResult } from './types'

/** 自定义头像起始 ID（男性首个 ID；女性首个 ID 为 4000） */
export const CUSTOM_FACE_BASE_ID = 3000

/** 每个号段容纳的 ID 数量 */
const SEGMENT_SIZE = 1000

/** 号段千位的搜索上限 */
const MAX_SEGMENT_INDEX = 98

/**
 * 判断号段是否归属指定性别。
 * @param segment 号段序号（ID 的千位数字）
 * @param sex 性别，0=男，1=女
 * @returns 是否归属
 */
function segmentBelongsToSex(segment: number, sex: number): boolean {
  const isMaleSegment = segment % 2 === 1
  return sex === 0 ? isMaleSegment : !isMaleSegment
}

/**
 * 依据头像 ID 推导性别。
 * @param id 头像 ID
 * @returns 0=男，1=女
 */
export function sexOfFaceId(id: number): number {
  return Math.floor(id / SEGMENT_SIZE) % 2 === 1 ? 0 : 1
}

/**
 * 本地推算指定性别的下一个可用 ID（后端未返回时兜底使用）。
 * @param sex 性别，0=男，1=女
 * @param used 已占用的 ID 集合
 * @returns 下一个可用 ID；号段耗尽返回 0
 */
export function nextFaceId(sex: number, used: Set<number>): number {
  const baseSegment = Math.floor(CUSTOM_FACE_BASE_ID / SEGMENT_SIZE)
  for (let segment = baseSegment; segment <= MAX_SEGMENT_INDEX; segment++) {
    if (!segmentBelongsToSex(segment, sex)) continue
    const start = segment * SEGMENT_SIZE
    for (let id = start; id < start + SEGMENT_SIZE; id++) {
      if (!used.has(id)) return id
    }
  }
  return 0
}

/**
 * 取得指定性别的下一个可用自定义头像 ID。
 * 优先采用服务端计算结果，缺失时本地兜底推算。
 * @param faces 自定义头像列表数据
 * @param sex 性别，0=男，1=女
 * @returns 下一个可用 ID；无法计算时返回 0
 */
export function nextFaceIdOf(faces: CustomFaceResult | null, sex: number): number {
  if (!faces) return 0
  const fromServer = sex === 0 ? faces.nextId?.male : faces.nextId?.female
  if (fromServer) return fromServer
  const used = new Set(faces.items.map((item) => item.id))
  return nextFaceId(sex, used)
}

/**
 * 生成性别号段的说明文字。
 * @param sex 性别，0=男，1=女
 * @returns 说明文字
 */
export function headRangeText(sex: number): string {
  return sex === 0
    ? '男性号段：3000-3999、5000-5999、7000-7999 ……'
    : '女性号段：4000-4999、6000-6999、8000-8999 ……'
}

/**
 * 判断是否为自定义头像 ID。
 * @param id 头像 ID
 * @returns 是否为自定义头像
 */
export function isCustomFaceId(id: number): boolean {
  return Number.isInteger(id) && id >= CUSTOM_FACE_BASE_ID
}
