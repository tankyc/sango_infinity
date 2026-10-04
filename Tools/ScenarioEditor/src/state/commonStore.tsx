/**
 * 公共数据表编辑器状态中心
 *
 * 与前端的剧本 store 不同，公共数据表采用「原文 + 最小差异」模型：
 * - 载入时保留磁盘上的原始文本（含注释、空行、字段顺序）；
 * - 每次改动通过 jsonc-parser 的 modify 生成最小差异并应用到原文上，
 *   因此注释与排版永远不会因为一次保存而丢失；
 * - 内存中同时维护一份解析结果（结构共享更新），供表格与表单直接读取。
 */
import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { CommonBackupItem, CommonFileDoc, CommonFileInfo } from '@/lib/types'
import {
  fetchCommonBackups,
  fetchCommonFile,
  fetchCommonList,
  restoreCommonBackup,
  saveCommonFile,
} from '@/lib/api'
import { editTextAtPath, parseText, setAtPath, deleteAtPath, getAtPath, type JsonPath } from '@/lib/jsoncEdit'

/** 单次编辑的快照，用于撤销 */
interface Snapshot {
  raw: string
  value: unknown
}

/** 撤销栈上限（字节预算内） */
const UNDO_LIMIT = 40
const UNDO_BYTE_BUDGET = 24 * 1024 * 1024
/** 超过该长度视为大文件，编辑时跳过二次解析校验以保响应速度 */
const LARGE_FILE_LIMIT = 400 * 1024

/** 状态容器 */
interface CommonStoreValue {
  /** 目录路径 */
  dir: string | null
  /** 文件清单 */
  files: CommonFileInfo[]
  listStatus: 'idle' | 'loading' | 'ready' | 'error'
  listError: string | null
  loadList: (refresh?: boolean) => Promise<void>

  /** 当前文档 */
  doc: CommonFileDoc | null
  docStatus: 'idle' | 'loading' | 'ready' | 'error'
  docError: string | null
  loadingName: string | null
  openFile: (name: string) => Promise<void>
  closeFile: () => void

  /** 当前文本与解析结果 */
  raw: string
  value: unknown
  /** 是否与磁盘不一致 */
  dirty: boolean
  /** 已提交的改动次数 */
  changeCount: number

  saving: boolean
  save: () => Promise<void>
  reload: () => Promise<void>
  discard: () => void

  canUndo: boolean
  canRedo: boolean
  undo: () => void
  redo: () => void

  /** 改写某个路径的值 */
  setValue: (path: JsonPath, next: unknown) => void
  /** 删除某个路径 */
  removeValue: (path: JsonPath) => void
  /** 在数组末尾追加元素 */
  pushValue: (arrayPath: JsonPath, next: unknown) => void
  /** 读取某个路径的值 */
  getValue: (path: JsonPath) => unknown

  /** 备份 */
  backups: CommonBackupItem[]
  refreshBackups: (name?: string) => Promise<void>
  restore: (dir: string) => Promise<void>
}

const CommonContext = createContext<CommonStoreValue | null>(null)

/**
 * 公共数据表状态提供者。
 */
export function CommonProvider({ children }: { children: React.ReactNode }) {
  const [dir, setDir] = useState<string | null>(null)
  const [files, setFiles] = useState<CommonFileInfo[]>([])
  const [listStatus, setListStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [listError, setListError] = useState<string | null>(null)

  const [doc, setDoc] = useState<CommonFileDoc | null>(null)
  const [docStatus, setDocStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [docError, setDocError] = useState<string | null>(null)
  const [loadingName, setLoadingName] = useState<string | null>(null)

  const [raw, setRaw] = useState('')
  const [value, setValue] = useState<unknown>(null)
  const [changeCount, setChangeCount] = useState(0)
  const [saving, setSaving] = useState(false)

  const [backups, setBackups] = useState<CommonBackupItem[]>([])

  const undoRef = useRef<Snapshot[]>([])
  const redoRef = useRef<Snapshot[]>([])
  const undoBytesRef = useRef(0)
  const [stackVersion, setStackVersion] = useState(0)

  /** 原文（磁盘版），用于判断 dirty */
  const originalRawRef = useRef('')

  /* ---------------- 文件清单 ---------------- */

  const loadList = useCallback(async (refresh = false) => {
    setListStatus('loading')
    try {
      const data = await fetchCommonList(refresh)
      setDir(data.dir)
      setFiles(data.files)
      setListStatus('ready')
      setListError(null)
    } catch (e) {
      setListStatus('error')
      setListError((e as Error).message)
    }
  }, [])

  useEffect(() => {
    if (listStatus === 'idle') void loadList()
  }, [listStatus, loadList])

  /* ---------------- 打开文件 ---------------- */

  const openFile = useCallback(async (name: string) => {
    setLoadingName(name)
    setDocStatus('loading')
    try {
      const data = await fetchCommonFile(name)
      setDoc(data)
      setRaw(data.raw)
      setValue(data.value)
      originalRawRef.current = data.raw
      undoRef.current = []
      redoRef.current = []
      undoBytesRef.current = 0
      setChangeCount(0)
      setStackVersion((v) => v + 1)
      setDocStatus('ready')
      setDocError(null)
      void fetchCommonBackups(name)
        .then(setBackups)
        .catch(() => setBackups([]))
    } catch (e) {
      setDoc(null)
      setDocStatus('error')
      setDocError((e as Error).message)
    } finally {
      setLoadingName(null)
    }
  }, [])

  const closeFile = useCallback(() => {
    setDoc(null)
    setDocStatus('idle')
    setDocError(null)
    setRaw('')
    setValue(null)
    originalRawRef.current = ''
    undoRef.current = []
    redoRef.current = []
    undoBytesRef.current = 0
    setChangeCount(0)
    setStackVersion((v) => v + 1)
  }, [])

  /* ---------------- 撤销栈 ---------------- */

  const pushUndo = useCallback((snapshot: Snapshot) => {
    const stack = undoRef.current
    stack.push(snapshot)
    undoBytesRef.current += snapshot.raw.length
    while (stack.length > UNDO_LIMIT || (undoBytesRef.current > UNDO_BYTE_BUDGET && stack.length > 1)) {
      const removed = stack.shift()
      if (removed) undoBytesRef.current -= removed.raw.length
    }
    redoRef.current = []
    setStackVersion((v) => v + 1)
  }, [])

  const undo = useCallback(() => {
    const stack = undoRef.current
    if (!doc || stack.length === 0) return
    const snapshot = stack.pop()
    if (!snapshot) return
    undoBytesRef.current -= snapshot.raw.length
    redoRef.current.push({ raw, value })
    setRaw(snapshot.raw)
    setValue(snapshot.value)
    setChangeCount((c) => Math.max(0, c - 1))
    setStackVersion((v) => v + 1)
  }, [doc, raw, value])

  const redo = useCallback(() => {
    const stack = redoRef.current
    if (!doc || stack.length === 0) return
    const snapshot = stack.pop()
    if (!snapshot) return
    undoRef.current.push({ raw, value })
    undoBytesRef.current += raw.length
    setRaw(snapshot.raw)
    setValue(snapshot.value)
    setChangeCount((c) => c + 1)
    setStackVersion((v) => v + 1)
  }, [doc, raw, value])

  /* ---------------- 编辑 ---------------- */

  const setValueAt = useCallback(
    (path: JsonPath, next: unknown) => {
      if (!doc) return
      const before = getAtPath(value, path)
      if (JSON.stringify(before) === JSON.stringify(next)) return
      const nextRaw = editTextAtPath(raw, path, next, { tabSize: doc.indent, eol: doc.eol })
      if (nextRaw === raw) return
      // 文本改写必须仍可解析，否则整体放弃（防御 jsonc-parser 的罕见边界）。
      // 超大文件（内置武将库 1MB+）跳过二次解析，避免每次提交多花上百毫秒。
      if (nextRaw.length < LARGE_FILE_LIMIT && parseText(nextRaw).errors.length > 0) return
      pushUndo({ raw, value })
      setRaw(nextRaw)
      setValue(setAtPath(value, path, next))
      setChangeCount((c) => c + 1)
    },
    [doc, pushUndo, raw, value]
  )

  const removeValue = useCallback(
    (path: JsonPath) => {
      if (!doc || path.length === 0) return
      const nextRaw = editTextAtPath(raw, path, undefined, { tabSize: doc.indent, eol: doc.eol })
      if (nextRaw === raw) return
      if (nextRaw.length < LARGE_FILE_LIMIT && parseText(nextRaw).errors.length > 0) return
      pushUndo({ raw, value })
      setRaw(nextRaw)
      setValue(deleteAtPath(value, path))
      setChangeCount((c) => c + 1)
    },
    [doc, pushUndo, raw, value]
  )

  const pushValue = useCallback(
    (arrayPath: JsonPath, next: unknown) => {
      if (!doc) return
      const arr = getAtPath(value, arrayPath)
      const index = Array.isArray(arr) ? arr.length : 0
      setValueAt([...arrayPath, index], next)
    },
    [doc, setValueAt, value]
  )

  const getValue = useCallback((path: JsonPath) => getAtPath(value, path), [value])

  /* ---------------- 保存 / 重载 / 放弃 ---------------- */

  const save = useCallback(async () => {
    if (!doc) return
    setSaving(true)
    try {
      const result = await saveCommonFile(doc.name, raw, doc.revision)
      setDoc({
        ...doc,
        raw,
        value,
        revision: result.meta.revision,
        size: result.meta.size,
        mtime: result.meta.mtime,
        summary: doc.summary,
      })
      originalRawRef.current = raw
      setChangeCount(0)
      undoRef.current = []
      redoRef.current = []
      undoBytesRef.current = 0
      setStackVersion((v) => v + 1)
      await loadList(true)
      void fetchCommonBackups(doc.name)
        .then(setBackups)
        .catch(() => setBackups([]))
    } finally {
      setSaving(false)
    }
  }, [doc, loadList, raw, value])

  const reload = useCallback(async () => {
    if (doc) await openFile(doc.name)
  }, [doc, openFile])

  const discard = useCallback(() => {
    if (!doc) return
    setRaw(originalRawRef.current)
    setValue(parseText(originalRawRef.current).value)
    undoRef.current = []
    redoRef.current = []
    undoBytesRef.current = 0
    setChangeCount(0)
    setStackVersion((v) => v + 1)
  }, [doc])

  /* ---------------- 备份 ---------------- */

  const refreshBackups = useCallback(async (name?: string) => {
    try {
      setBackups(await fetchCommonBackups(name))
    } catch {
      setBackups([])
    }
  }, [])

  const restore = useCallback(
    async (backupDir: string) => {
      const result = await restoreCommonBackup(backupDir)
      if (doc && result.name === doc.name) await openFile(doc.name)
      await loadList(true)
    },
    [doc, loadList, openFile]
  )

  const dirty = doc !== null && raw !== originalRawRef.current

  const api = useMemo<CommonStoreValue>(
    () => ({
      dir,
      files,
      listStatus,
      listError,
      loadList,
      doc,
      docStatus,
      docError,
      loadingName,
      openFile,
      closeFile,
      raw,
      value,
      dirty,
      changeCount,
      saving,
      save,
      reload,
      discard,
      canUndo: undoRef.current.length > 0,
      canRedo: redoRef.current.length > 0,
      undo,
      redo,
      setValue: setValueAt,
      removeValue,
      pushValue,
      getValue,
      backups,
      refreshBackups,
      restore,
    }),
    // stackVersion 参与依赖以刷新 canUndo / canRedo
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [
      dir,
      files,
      listStatus,
      listError,
      loadList,
      doc,
      docStatus,
      docError,
      loadingName,
      openFile,
      closeFile,
      raw,
      value,
      dirty,
      changeCount,
      saving,
      save,
      reload,
      discard,
      undo,
      redo,
      setValueAt,
      removeValue,
      pushValue,
      getValue,
      backups,
      refreshBackups,
      restore,
      stackVersion,
    ]
  )

  return <CommonContext.Provider value={api}>{children}</CommonContext.Provider>
}

/**
 * 读取公共数据表状态。
 */
export function useCommonStore(): CommonStoreValue {
  const ctx = useContext(CommonContext)
  if (!ctx) throw new Error('useCommonStore 必须在 CommonProvider 内使用')
  return ctx
}
