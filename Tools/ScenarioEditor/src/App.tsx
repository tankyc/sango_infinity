/**
 * 应用主框架
 *
 * 负责：
 * - 顶部工具条（保存 / 撤销 / 重做 / 校验 / 主题 / 命令面板）
 * - 集合页签切换（概览、武将、都市、军团、势力、校验）
 * - 全局快捷键（Ctrl+S 保存、Ctrl+Z 撤销、Ctrl+Y 重做、Ctrl+K 命令面板、Alt+1..6 切页）
 * - 未保存修改的离开提醒
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react'
import {
  AlertTriangle,
  BookUser,
  Building2,
  Command as CommandIcon,
  Database,
  Flag,
  LayoutDashboard,
  LayoutTemplate,
  Moon,
  Redo2,
  RefreshCw,
  Save,
  Shield,
  ShieldCheck,
  Store,
  Sun,
  Undo2,
  Users,
} from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import { PERSON_LIB_URL, WORKSHOP_URL } from '@/lib/siteLinks'
import type { CollectionKey } from '@/lib/types'
import { COLLECTION_META } from '@/lib/types'
import { useScenarioStore } from '@/state/store'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { EntityBrowser } from '@/components/EntityBrowser'
import { OverviewPanel } from '@/components/OverviewPanel'
import { ValidationPanel } from '@/components/ValidationPanel'
import { CommandPalette } from '@/components/CommandPalette'
import { LibraryDialog } from '@/components/LibraryDialog'
import { CommonEditor } from '@/components/CommonEditor'
import { TemplateDialog } from '@/components/TemplateDialog'

/** 页签标识 */
type TabKey = 'overview' | CollectionKey | 'common' | 'validation'

/** 主题存储键 */
const THEME_KEY = 'scenario-editor:theme'

/** 页签定义 */
const TABS: { key: TabKey; label: string; icon: React.ElementType }[] = [
  { key: 'overview', label: '概览', icon: LayoutDashboard },
  { key: 'personSet', label: '武将', icon: Users },
  { key: 'citySet', label: '都市', icon: Building2 },
  { key: 'corpsSet', label: '军团', icon: Shield },
  { key: 'forceSet', label: '势力', icon: Flag },
  { key: 'common', label: '公共数据', icon: Database },
  { key: 'validation', label: '校验', icon: ShieldCheck },
]

/**
 * 应用主体。
 */
export default function App() {
  const store = useScenarioStore()
  const {
    status,
    error,
    scenario,
    meta,
    dirty,
    canUndo,
    canRedo,
    save,
    undo,
    redo,
    reload,
    saving,
    issueCounts,
    auth,
    needInit,
  } = store

  const [tab, setTab] = useState<TabKey>('overview')
  /** 模板库 / 登录对话框 */
  const [templateOpen, setTemplateOpen] = useState(false)
  // 主题直接从本地存储惰性初始化，避免首帧闪烁与额外的副作用
  const [theme, setTheme] = useState<'dark' | 'light'>(() => {
    const saved = typeof localStorage === 'undefined' ? null : localStorage.getItem(THEME_KEY)
    return saved === 'light' ? 'light' : 'dark'
  })
  const [paletteOpen, setPaletteOpen] = useState(false)
  const [libraryOpen, setLibraryOpen] = useState(false)
  const [focusTarget, setFocusTarget] = useState<{ collection: CollectionKey; id: number; nonce: number } | null>(
    null
  )

  /* ---------------- 主题 ---------------- */

  useEffect(() => {
    document.documentElement.classList.toggle('dark', theme === 'dark')
    localStorage.setItem(THEME_KEY, theme)
  }, [theme])

  /* ---------------- 导航 ---------------- */

  const navigate = useCallback((collection: CollectionKey, entityId: number) => {
    setTab(collection)
    if (entityId > 0) {
      setFocusTarget({ collection, id: entityId, nonce: Date.now() })
    }
  }, [])

  /* ---------------- 离开提醒 ---------------- */

  useEffect(() => {
    if (!dirty) return
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault()
      e.returnValue = ''
    }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [dirty])

  /* ---------------- 全局快捷键 ---------------- */

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null
      const editable =
        target &&
        (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)
      const mod = e.ctrlKey || e.metaKey

      // 公共数据页签下，保存 / 撤销 / 重做由该面板自己接管，避免误改剧本
      if (tab !== 'common' && mod && e.key.toLowerCase() === 's') {
        e.preventDefault()
        void save()
        return
      }
      if (mod && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        setPaletteOpen(true)
        return
      }
      // 在输入框内不劫持撤销/重做，保留浏览器原生行为
      if (tab !== 'common' && !editable && mod && e.key.toLowerCase() === 'z') {
        e.preventDefault()
        if (e.shiftKey) redo()
        else undo()
        return
      }
      if (tab !== 'common' && !editable && mod && e.key.toLowerCase() === 'y') {
        e.preventDefault()
        redo()
        return
      }
      if (e.altKey && !mod) {
        const index = Number(e.key)
        if (Number.isInteger(index) && index >= 1 && index <= TABS.length) {
          e.preventDefault()
          setTab(TABS[index - 1].key)
        }
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [redo, save, tab, undo])

  const handleSave = useCallback(async () => {
    try {
      await save()
      toast.success('已保存到 Scenario.json，并生成了一份自动备份')
    } catch (e) {
      const message = (e as Error).message
      toast.error(message, { duration: 8000 })
    }
  }, [save])

  const handleReload = useCallback(async () => {
    await reload()
    toast.success('已从磁盘重新加载剧本')
  }, [reload])

  const collectionTabs = useMemo(
    () =>
      Object.keys(COLLECTION_META).map((key) => ({
        key: key as CollectionKey,
        count: scenario ? Object.keys((scenario[key] ?? {}) as Record<string, unknown>).length : 0,
      })),
    [scenario]
  )

  const countOf = (key: TabKey): number | null => {
    const hit = collectionTabs.find((c) => c.key === key)
    return hit ? hit.count : null
  }

  /* ---------------- 加载 / 错误状态 ---------------- */

  if (status === 'loading' || status === 'idle') {
    return (
      <div className="app-shell flex h-full items-center justify-center">
        <div className="w-[min(90vw,420px)] space-y-3">
          <Skeleton className="h-6 w-40" />
          <Skeleton className="h-3 w-full" />
          <Skeleton className="h-3 w-3/4" />
          <p className="pt-2 text-center text-[12px] text-muted-foreground">正在加载剧本与公共数据表…</p>
        </div>
      </div>
    )
  }

  if (status === 'error') {
    // 多用户模式下「工作区还没有剧本」不是故障，而是一个需要引导的初始状态：
    // 直接给「打开模板库」的入口，别让人对着一段报错发呆。
    const initPrompt = needInit
    return (
      <div className="app-shell flex h-full items-center justify-center p-4">
        <Alert variant={initPrompt ? 'default' : 'destructive'} className="w-[min(94vw,560px)]">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>{initPrompt ? '还没有剧本' : '无法加载剧本'}</AlertTitle>
          <AlertDescription className="space-y-2 text-[12px]">
            <p className="break-all">{error}</p>
            {initPrompt ? (
              <p className="text-muted-foreground">
                从模板库挑一个模板作为起点，内容会复制到你的工作区；也可以让管理员上传一份剧本给你。
              </p>
            ) : import.meta.env.DEV ? (
              // 本机开发：给出可直接照做的启动命令
              <p className="text-muted-foreground">
                请在 <code className="rounded bg-muted px-1">Tools/ScenarioEditor</code> 目录下执行{' '}
                <code className="rounded bg-muted px-1">npm run server</code> 启动后端服务（默认端口 3009），
                然后重试。
              </p>
            ) : (
              // 线上：访问者不是开发者，让他们去执行 npm 命令毫无意义
              <p className="text-muted-foreground">
                服务暂时不可用，请稍后刷新页面重试；若持续失败，请联系管理员。
              </p>
            )}
            <div className="mt-2 flex gap-2">
              {initPrompt && (
                <Button size="sm" className="gap-1.5" onClick={() => setTemplateOpen(true)}>
                  <LayoutTemplate className="h-3.5 w-3.5" />
                  打开模板库
                </Button>
              )}
              <Button size="sm" variant="outline" className="gap-1.5" onClick={() => void reload()}>
                <RefreshCw className="h-3.5 w-3.5" />
                重试
              </Button>
            </div>
          </AlertDescription>
        </Alert>

        <TemplateDialog open={templateOpen} onOpenChange={setTemplateOpen} needInit={initPrompt} />
      </div>
    )
  }

  /* ---------------- 主界面 ---------------- */

  return (
    <div className="app-shell flex h-full min-h-0 flex-col">
      {/* 顶部工具条 */}
      <header className="shrink-0 border-b border-border bg-card/60 backdrop-blur">
        <div className="flex items-center gap-2 px-3 py-2">
          <div className="flex min-w-0 items-center gap-2">
            <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-primary/15">
              <ShieldCheck className="h-4 w-4 text-primary" />
            </div>
            <div className="min-w-0">
              <h1 className="font-display truncate text-[14px] font-semibold leading-tight">剧本编辑器</h1>
              <p className="truncate text-[10px] leading-tight text-muted-foreground">
                {String(scenario?.Name ?? '')}
              </p>
            </div>
          </div>

          {dirty && (
            <Badge className="shrink-0 bg-amber-500 text-[10px] text-black" title="存在未保存的修改">
              未保存
            </Badge>
          )}

          <div className="ml-auto flex items-center gap-1.5">
            <Button
              variant="outline"
              size="icon"
              className="h-8 w-8"
              onClick={undo}
              disabled={!canUndo}
              title="撤销（Ctrl+Z）"
            >
              <Undo2 className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              className="h-8 w-8"
              onClick={redo}
              disabled={!canRedo}
              title="重做（Ctrl+Y）"
            >
              <Redo2 className="h-4 w-4" />
            </Button>

            <Button
              variant="outline"
              size="sm"
              className="hidden h-8 gap-1.5 sm:flex"
              onClick={() => setPaletteOpen(true)}
              title="快捷命令（Ctrl+K）"
            >
              <CommandIcon className="h-3.5 w-3.5" />
              <kbd className="rounded border border-border px-1 text-[9px]">Ctrl K</kbd>
            </Button>
            <Button
              variant="outline"
              size="icon"
              className="h-8 w-8 sm:hidden"
              onClick={() => setPaletteOpen(true)}
              title="快捷命令"
            >
              <CommandIcon className="h-4 w-4" />
            </Button>

            <Button
              variant="outline"
              size="icon"
              className="h-8 w-8"
              onClick={() => setTheme((t) => (t === 'dark' ? 'light' : 'dark'))}
              title={theme === 'dark' ? '切换到亮色主题' : '切换到暗色主题'}
            >
              {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
            </Button>

            <Separator orientation="vertical" className="mx-0.5 h-6" />

            <Button
              variant={issueCounts.error > 0 ? 'destructive' : 'outline'}
              size="sm"
              className="h-8 gap-1.5"
              onClick={() => setTab('validation')}
              title="查看校验结果"
            >
              <ShieldCheck className="h-3.5 w-3.5" />
              <span className="tabular">{issueCounts.error}</span>
              <span className="hidden sm:inline">错误</span>
              {issueCounts.warning > 0 && (
                <span className="tabular text-amber-500">
                  {issueCounts.warning}
                  <span className="hidden sm:inline">警告</span>
                </span>
              )}
            </Button>

            <Button size="sm" className="h-8 gap-1.5" onClick={() => void handleSave()} disabled={saving || !dirty}>
              <Save className="h-3.5 w-3.5" />
              <span className="hidden sm:inline">{saving ? '保存中…' : '保存'}</span>
            </Button>

            <Separator orientation="vertical" className="mx-0.5 h-6" />

            {/*
              站外跳转：与同机的武将库、创意工坊三站互链。
              「做武将 → 编剧本 → 发模组」本来就是同一条创作流程，来回跳转是常态。
              窄屏只留图标：顶栏按钮已经不少，文字换行会把工具条撑高。
            */}
            <Button
              asChild
              variant="outline"
              size="sm"
              className="h-8 gap-1.5"
              title={`在新窗口打开武将库：${PERSON_LIB_URL}`}
            >
              <a href={PERSON_LIB_URL} target="_blank" rel="noreferrer noopener">
                <BookUser className="h-3.5 w-3.5" />
                <span className="hidden sm:inline">武将库</span>
              </a>
            </Button>
            <Button
              asChild
              variant="outline"
              size="sm"
              className="h-8 gap-1.5"
              title={`在新窗口打开创意工坊：${WORKSHOP_URL}`}
            >
              <a href={WORKSHOP_URL} target="_blank" rel="noreferrer noopener">
                <Store className="h-3.5 w-3.5" />
                <span className="hidden sm:inline">创意工坊</span>
              </a>
            </Button>
          </div>
        </div>

        {/*
          页签：用换行而不是横向滚动。
          手机上没有可见的滚动条，overflow-x-auto 会把右侧几个页签（公共数据 / 校验）
          完全藏起来且毫无提示，用户根本不知道还能往右划。
          换行后无论屏幕多窄，每个页签都看得见。
        */}
        <div className="flex flex-wrap items-center gap-x-1 gap-y-1 px-2 pb-1.5">
          {TABS.map((t) => {
            const count = countOf(t.key)
            const isActive = tab === t.key
            const hasError = t.key === 'validation' && issueCounts.error > 0
            return (
              <button
                key={t.key}
                type="button"
                onClick={() => setTab(t.key)}
                className={cn(
                  // 小屏收紧内边距并隐藏图标：7 个页签才能在窄屏上少占一行
                  'flex shrink-0 items-center gap-1.5 rounded-md px-2 py-1.5 text-[12px] transition-colors sm:px-2.5',
                  isActive
                    ? 'bg-primary/15 text-foreground'
                    : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                  hasError && !isActive && 'text-destructive'
                )}
              >
                <t.icon className="hidden h-3.5 w-3.5 sm:block" />
                {t.label}
                {count !== null && <span className="tabular text-[10px] opacity-70">{count}</span>}
              </button>
            )
          })}

          <div className="ml-auto flex items-center gap-1.5 sm:pl-2">
            {/* 模板库兼登录入口：登录状态直接做在按钮上，省掉一个单独的账号区 */}
            <Button
              variant="outline"
              size="sm"
              className="h-7 gap-1.5 text-[11px]"
              onClick={() => setTemplateOpen(true)}
              title={
                auth?.user
                  ? `已登录：${auth.user.displayName}（${auth.user.roleLabel}）`
                  : '打开剧本模板库 / 登录'
              }
            >
              <LayoutTemplate className="h-3 w-3" />
              <span className="hidden sm:inline">模板库</span>
              {auth?.user && (
                <span className="max-w-[80px] truncate text-muted-foreground">{auth.user.displayName}</span>
              )}
              {auth?.user?.isAdmin && <ShieldCheck className="h-3 w-3 text-primary" />}
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="h-7 gap-1.5 text-[11px]"
              onClick={() => void handleReload()}
              title="从磁盘重新加载"
            >
              <RefreshCw className="h-3 w-3" />
              重新加载
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="h-7 gap-1.5 text-[11px]"
              onClick={() => setLibraryOpen(true)}
            >
              <Users className="h-3 w-3" />
              <span className="hidden sm:inline">从武将库导入</span>
              <span className="sm:hidden">武将库</span>
            </Button>
          </div>
        </div>
      </header>

      {/* 内容区 */}
      <main className="min-h-0 flex-1 overflow-hidden">
        {tab === 'overview' && <OverviewPanel onNavigate={navigate} onGoValidation={() => setTab('validation')} />}
        {tab === 'validation' && <ValidationPanel onNavigate={navigate} />}
        {tab === 'common' && <CommonEditor />}
        {(tab === 'personSet' || tab === 'citySet' || tab === 'corpsSet' || tab === 'forceSet') && (
          <EntityBrowser
            key={tab}
            collection={tab}
            focusTarget={focusTarget && focusTarget.collection === tab ? focusTarget : null}
          />
        )}
      </main>

      {/* 底部信息条 */}
      <footer className="flex shrink-0 items-center gap-3 border-t border-border px-3 py-1 text-[10px] text-muted-foreground">
        <span className="truncate font-mono" title={meta?.file}>
          {meta?.file}
        </span>
        <span className="ml-auto shrink-0 tabular">修订号 {meta?.revision}</span>
        <span className="hidden shrink-0 sm:inline">Alt+1~7 切换页签</span>
      </footer>

      {/* 全局对话框 */}
      <CommandPalette
        open={paletteOpen}
        onOpenChange={setPaletteOpen}
        onNavigate={navigate}
        onGoValidation={() => setTab('validation')}
      />
      <LibraryDialog open={libraryOpen} onOpenChange={setLibraryOpen} />
      <TemplateDialog open={templateOpen} onOpenChange={setTemplateOpen} />
    </div>
  )
}
