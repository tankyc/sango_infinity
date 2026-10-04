/**
 * JSONC 最小差异编辑工具
 *
 * 公共数据表里有三个含注释的文件（AIConfig.json、Personalities.json、
 * RecommendedTroopTeams.json），它们承载了大量中文说明，直接 JSON.stringify
 * 重新序列化会把注释全部抹掉。
 *
 * 因此这里的做法是：始终保留磁盘上的原文，只在用户改动某个节点时，
 * 用 jsonc-parser 的 modify 生成「最小差异」，再应用到原文上。
 * 这样注释、空行、字段顺序、缩进与换行符都能原样保留。
 */
import { applyEdits, modify, parse as parseJsonc, printParseErrorCode, type ParseError } from 'jsonc-parser'

/** 路径类型：对象键用字符串，数组下标用数字 */
export type JsonPath = (string | number)[]

/** 文本格式信息 */
export interface TextFormat {
  /** 缩进宽度 */
  tabSize: number
  /** 换行符 */
  eol: string
}

/**
 * 把某个路径上的值改写为 newValue，返回改写后的完整文本。
 *
 * @param raw 原始文本
 * @param path 目标路径；空数组表示整体替换
 * @param newValue 新值；传 undefined 表示删除该节点
 * @param format 文本格式
 * @returns 改写后的文本；改写失败时返回原文
 */
export function editTextAtPath(raw: string, path: JsonPath, newValue: unknown, format: TextFormat): string {
  try {
    const edits = modify(raw, path, newValue, {
      formattingOptions: {
        insertSpaces: true,
        tabSize: format.tabSize,
        eol: format.eol,
      },
      isArrayInsertion: false,
    })
    if (!edits || edits.length === 0) return raw
    return applyEdits(raw, edits)
  } catch {
    return raw
  }
}

/**
 * 在数组末尾追加一个元素。
 *
 * @param raw 原始文本
 * @param arrayPath 数组所在路径
 * @param value 新元素
 * @param format 文本格式
 * @returns 改写后的文本
 */
export function appendToArray(raw: string, arrayPath: JsonPath, value: unknown, format: TextFormat): string {
  try {
    const edits = modify(raw, [...arrayPath, Number.MAX_SAFE_INTEGER - 1], value, {
      formattingOptions: { insertSpaces: true, tabSize: format.tabSize, eol: format.eol },
    })
    if (!edits || edits.length === 0) return raw
    return applyEdits(raw, edits)
  } catch {
    return raw
  }
}

/** 解析结果 */
export interface JsoncParseResult {
  value: unknown
  errors: string[]
}

/**
 * 解析 JSONC 文本，返回解析结果与错误列表（不抛异常）。
 *
 * @param raw 文本
 * @returns 解析结果
 */
export function parseText(raw: string): JsoncParseResult {
  const errors: ParseError[] = []
  const value = parseJsonc(raw, errors, {
    allowTrailingComma: true,
    disallowComments: false,
    allowEmptyContent: true,
  })
  return {
    value,
    errors: errors.map((e) => `${printParseErrorCode(e.error)} @ offset ${e.offset}`),
  }
}

/**
 * 判断路径对应的值是否是「对象」。
 *
 * @param v 值
 * @returns 是否普通对象
 */
export function isPlainObject(v: unknown): v is Record<string, unknown> {
  return Boolean(v) && typeof v === 'object' && !Array.isArray(v)
}

/**
 * 不可变地写入某个路径上的值（用于同步内存中的解析结果）。
 *
 * 只克隆路径上的节点，其余节点保持引用共享，因此对 1000+ 条目的表也很轻量。
 *
 * @param root 根对象
 * @param path 路径
 * @param value 新值
 * @returns 新的根对象
 */
export function setAtPath<T>(root: T, path: JsonPath, value: unknown): T {
  if (path.length === 0) return value as T
  const [head, ...rest] = path
  if (Array.isArray(root)) {
    const index = Number(head)
    const copy = root.slice()
    copy[index] = setAtPath(root[index], rest, value)
    return copy as unknown as T
  }
  if (isPlainObject(root)) {
    const key = String(head)
    const copy: Record<string, unknown> = { ...root }
    copy[key] = setAtPath(root[key], rest, value)
    return copy as unknown as T
  }
  return root
}

/**
 * 不可变地删除某个路径上的节点。
 *
 * @param root 根对象
 * @param path 路径
 * @returns 新的根对象
 */
export function deleteAtPath<T>(root: T, path: JsonPath): T {
  if (path.length === 0) return root
  const [head, ...rest] = path
  if (Array.isArray(root)) {
    if (rest.length === 0) {
      const copy = root.slice()
      copy.splice(Number(head), 1)
      return copy as unknown as T
    }
    const copy = root.slice()
    copy[Number(head)] = deleteAtPath(root[Number(head)], rest)
    return copy as unknown as T
  }
  if (isPlainObject(root)) {
    const key = String(head)
    const copy = { ...root } as Record<string, unknown>
    if (rest.length === 0) {
      delete copy[key]
    } else {
      copy[key] = deleteAtPath((root as Record<string, unknown>)[key], rest)
    }
    return copy as unknown as T
  }
  return root
}

/**
 * 读取某个路径上的值。
 *
 * @param root 根对象
 * @param path 路径
 * @returns 值
 */
export function getAtPath(root: unknown, path: JsonPath): unknown {
  let cur: unknown = root
  for (const seg of path) {
    if (cur === null || cur === undefined) return undefined
    if (Array.isArray(cur)) cur = cur[Number(seg)]
    else if (isPlainObject(cur)) cur = cur[String(seg)]
    else return undefined
  }
  return cur
}

/**
 * 把 camelCase / snake_case 键名转成带空格的可读文本（标签字典未命中时的兜底）。
 *
 * @param key 键名
 * @returns 可读文本
 */
export function humanizeKey(key: string): string {
  return key
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim()
}
