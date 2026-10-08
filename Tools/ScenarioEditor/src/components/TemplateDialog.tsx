/**
 * 剧本模板库 / 登录对话框
 *
 * 一个地方收齐「需求 4」的全部动作，按身份分档展示：
 * - 游客：浏览模板、下载、用模板初始化自己的工作区
 * - 管理员：额外可上传模板、删除模板、上传剧本覆盖当前工作区
 *
 * 登录复用武将库网站的账号（后端代理转发），登录后自动重载数据——
 * 因为数据源会从「只读游客视图」切到「自己的工作区」。
 */
import { useCallback, useEffect, useState } from 'react'
import { toast } from 'sonner'
import {
  CloudUpload,
  FileUp,
  LayoutTemplate,
  Lock,
  LogIn,
  LogOut,
  RefreshCw,
  ShieldCheck,
  Trash2,
  TriangleAlert,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import {
  applyTemplate,
  createTemplate,
  deleteTemplate,
  fetchTemplates,
  templateDownloadUrl,
  uploadScenario,
  type TemplateItem,
} from '@/lib/api'
import { useScenarioStore } from '@/state/store'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog'

/** 组件属性 */
interface TemplateDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** 是否处于「工作区还没有剧本」的初始状态 */
  needInit?: boolean
}

/** 集合中文名，用于列表上显示规模 */
const COUNT_LABELS: Record<string, string> = {
  forceSet: '势力',
  corpsSet: '军团',
  citySet: '都市',
  personSet: '武将',
}

/**
 * 把字节数格式化成可读文本。
 *
 * @param size 字节
 * @returns 展示文本
 */
function formatSize(size: number): string {
  if (size < 1024) return `${size} B`
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(0)} KB`
  return `${(size / 1024 / 1024).toFixed(1)} MB`
}

/**
 * 把 ISO 时间格式化为本地可读文本。
 *
 * @param iso ISO 时间
 * @returns 展示文本
 */
function formatTime(iso: string): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function TemplateDialog({ open, onOpenChange, needInit = false }: TemplateDialogProps) {
  const { auth, login, logout, reload, refreshAuth } = useScenarioStore()

  const [templates, setTemplates] = useState<TemplateItem[]>([])
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState('')

  // 登录表单
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [logging, setLogging] = useState(false)

  // 上传模板表单
  const [newName, setNewName] = useState('')
  const [newDesc, setNewDesc] = useState('')

  // 二次确认
  const [confirmApply, setConfirmApply] = useState<TemplateItem | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<TemplateItem | null>(null)

  const canAdmin = Boolean(auth?.canAdmin)
  const userName = auth?.user?.displayName || auth?.user?.username || ''

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      setTemplates(await fetchTemplates())
    } catch (e) {
      toast.error(`读取模板列表失败：${(e as Error).message}`)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (open) void refresh()
  }, [open, refresh])

  /** 登录 */
  const handleLogin = useCallback(async () => {
    if (!username.trim() || !password) {
      toast.warning('请填写账号与密码')
      return
    }
    setLogging(true)
    try {
      await login(username.trim(), password)
      setPassword('')
      toast.success('登录成功')
      void refreshAuth()
    } catch (e) {
      toast.error(`登录失败：${(e as Error).message}`)
    } finally {
      setLogging(false)
    }
  }, [login, password, refreshAuth, username])

  /** 套用模板（已有剧本时先弹确认） */
  const doApply = useCallback(
    async (t: TemplateItem, force: boolean) => {
      setBusy(t.id)
      try {
        await applyTemplate(t.id, force)
        toast.success(`已套用模板「${t.name}」`)
        setConfirmApply(null)
        onOpenChange(false)
        await reload()
      } catch (e) {
        toast.error((e as Error).message)
      } finally {
        setBusy('')
      }
    },
    [onOpenChange, reload]
  )

  /** 把当前剧本存成模板 */
  const handleCreate = useCallback(async () => {
    if (!newName.trim()) {
      toast.warning('请填写模板名称')
      return
    }
    setBusy('create')
    try {
      const t = await createTemplate({ name: newName.trim(), description: newDesc.trim() })
      toast.success(`模板「${t.name}」已上传`)
      setNewName('')
      setNewDesc('')
      await refresh()
    } catch (e) {
      toast.error(`上传失败：${(e as Error).message}`)
    } finally {
      setBusy('')
    }
  }, [newDesc, newName, refresh])

  /** 上传本地剧本文件覆盖当前工作区 */
  const handleUploadFile = useCallback(
    async (file: File, force: boolean) => {
      setBusy('upload')
      try {
        const text = await file.text()
        const result = await uploadScenario(text, force)
        // 剧本可能没有顶层 Name（从游戏或模组里抠出来的剧本很常见，这也已不再被拒绝），
        // 这时退回文件名，避免弹出「已上传《》」这种没有主语的成功提示
        const title = result.name.trim() || file.name.replace(/\.json$/i, '') || '未命名剧本'
        toast.success(`已上传《${title}》${result.replaced ? '（已覆盖原剧本，原文件已备份）' : ''}`)
        onOpenChange(false)
        await reload()
      } catch (e) {
        const err = e as Error & { code?: string }
        if (err.code === 'EEXISTS') {
          if (window.confirm('当前工作区已有剧本，上传会覆盖它（原文件会另存备份）。确定继续？')) {
            await handleUploadFile(file, true)
          }
          return
        }
        toast.error(`上传失败：${err.message}`)
      } finally {
        setBusy('')
      }
    },
    [onOpenChange, reload]
  )

  /** 删除模板 */
  const doDelete = useCallback(
    async (t: TemplateItem) => {
      setBusy(t.id)
      try {
        await deleteTemplate(t.id)
        toast.success(`已删除模板「${t.name}」`)
        setConfirmDelete(null)
        await refresh()
      } catch (e) {
        toast.error((e as Error).message)
      } finally {
        setBusy('')
      }
    },
    [refresh]
  )

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        {/* sm:max-w-* 用来覆盖 DialogContent 自带的 `sm:max-w-lg`（512px），否则宽屏下会被截住 */}
        <DialogContent className="flex max-h-[92vh] w-[min(96vw,900px)] flex-col gap-0 overflow-hidden p-0 sm:max-w-[900px]">
          <DialogHeader className="shrink-0 border-b border-border px-5 py-3">
            <DialogTitle className="flex items-center gap-2 text-[16px]">
              <LayoutTemplate className="h-4 w-4 text-primary" />
              剧本模板库
            </DialogTitle>
            <DialogDescription className="flex flex-wrap items-center gap-2 text-[12px]">
              <span>挑一个模板作为起点，内容会复制到你的工作区，之后的修改不影响他人</span>
              <Badge variant="secondary" className="tabular">
                {templates.length} 个
              </Badge>
              <Button
                variant="ghost"
                size="sm"
                className="h-6 gap-1 px-1.5 text-[11px]"
                onClick={() => void refresh()}
                disabled={loading}
              >
                <RefreshCw className={cn('h-3 w-3', loading && 'animate-spin')} />
                刷新
              </Button>
            </DialogDescription>
          </DialogHeader>

          {needInit && (
            <div className="shrink-0 px-5 pt-3">
              <Alert className="py-2">
                <TriangleAlert className="h-4 w-4" />
                <AlertDescription className="text-[11px]">
                  你的工作区还没有剧本。从下面挑一个模板「套用」，即可开始编辑。
                </AlertDescription>
              </Alert>
            </div>
          )}

          <div className="grid min-h-0 flex-1 grid-cols-1 gap-0 md:grid-cols-[1fr_280px]">
            {/* 模板列表 */}
            <ScrollArea className="min-h-0">
              <div className="space-y-2 p-4">
                {loading && templates.length === 0 && (
                  <p className="py-8 text-center text-[12px] text-muted-foreground">正在读取模板…</p>
                )}
                {!loading && templates.length === 0 && (
                  <div className="py-10 text-center text-[12px] text-muted-foreground">
                    <p>模板库还是空的。</p>
                    <p className="mt-1">
                      {canAdmin ? '管理员可在右侧把当前剧本上传为模板。' : '请联系管理员上传模板。'}
                    </p>
                  </div>
                )}

                {templates.map((t) => (
                  <div
                    key={t.id}
                    className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-card/40 p-3"
                  >
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <span className="truncate text-[13px] font-medium">{t.name}</span>
                        {t.scenarioName && (
                          <span className="truncate text-[11px] text-muted-foreground">《{t.scenarioName}》</span>
                        )}
                      </div>
                      {t.description && (
                        <p className="mt-0.5 line-clamp-2 text-[11px] text-muted-foreground">{t.description}</p>
                      )}
                      <div className="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-[10px] text-muted-foreground">
                        <span className="tabular">{formatTime(t.createdAt)}</span>
                        <span className="tabular">{formatSize(t.size)}</span>
                        {t.author && <span>由 {t.author} 上传</span>}
                        {t.counts && (
                          <span className="tabular">
                            {Object.entries(t.counts)
                              .map(([k, v]) => `${COUNT_LABELS[k] ?? k} ${v}`)
                              .join(' / ')}
                          </span>
                        )}
                      </div>
                    </div>

                    <div className="flex shrink-0 items-center gap-1.5">
                      <Button
                        size="sm"
                        className="h-7 gap-1"
                        disabled={Boolean(busy)}
                        onClick={() => (needInit ? void doApply(t, true) : setConfirmApply(t))}
                      >
                        <LayoutTemplate className="h-3 w-3" />
                        套用
                      </Button>
                      <a
                        href={templateDownloadUrl(t.id)}
                        download
                        className={cn(buttonLikeCls, 'h-7 gap-1')}
                        title="下载该模板的 Scenario.json"
                      >
                        <FileUp className="h-3 w-3 rotate-180" />
                        下载
                      </a>
                      {canAdmin && (
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-7 w-7 text-muted-foreground hover:text-destructive"
                          disabled={Boolean(busy)}
                          title="删除该模板"
                          onClick={() => setConfirmDelete(t)}
                        >
                          <Trash2 className="h-3 w-3" />
                        </Button>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </ScrollArea>

            {/* 右侧：账号与上传 */}
            <div className="min-h-0 space-y-4 overflow-y-auto border-l border-border p-4">
              {/* 账号 */}
              <div className="space-y-2">
                <h3 className="flex items-center gap-1.5 text-[12px] font-medium">
                  <ShieldCheck className="h-3.5 w-3.5 text-primary" />
                  账号
                </h3>

                {auth?.user ? (
                  <div className="space-y-2">
                    <div className="flex items-center gap-2 rounded-md border border-border bg-card/40 px-2.5 py-2">
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-[12px] font-medium">{userName}</p>
                        <p className="text-[10px] text-muted-foreground">
                          {auth.user.roleLabel}
                          {auth.multiUser ? '' : '（单机模式）'}
                        </p>
                      </div>
                      <Button variant="outline" size="sm" className="h-6 gap-1 text-[11px]" onClick={logout}>
                        <LogOut className="h-3 w-3" />
                        退出
                      </Button>
                    </div>
                  </div>
                ) : (
                  <div className="space-y-2">
                    <p className="text-[11px] text-muted-foreground">
                      {auth?.authAvailable === false
                        ? '未接入账号体系，当前按单机模式运行，可直接编辑。'
                        : '用武将库网站的账号登录，登录后拥有自己的工作区。'}
                    </p>
                    {auth?.authAvailable !== false && (
                      <>
                        <Input
                          className="h-8 text-[12px]"
                          placeholder="用户名"
                          value={username}
                          autoComplete="username"
                          onChange={(e) => setUsername(e.target.value)}
                        />
                        <Input
                          className="h-8 text-[12px]"
                          type="password"
                          placeholder="密码"
                          value={password}
                          autoComplete="current-password"
                          onChange={(e) => setPassword(e.target.value)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter') void handleLogin()
                          }}
                        />
                        <Button
                          size="sm"
                          className="h-8 w-full gap-1.5"
                          disabled={logging}
                          onClick={() => void handleLogin()}
                        >
                          {logging ? (
                            <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                          ) : (
                            <LogIn className="h-3.5 w-3.5" />
                          )}
                          {logging ? '登录中…' : '登录'}
                        </Button>
                      </>
                    )}
                  </div>
                )}
              </div>

              {canAdmin && <Separator />}

              {/* 管理员操作 */}
              {canAdmin && (
                <div className="space-y-3">
                  <h3 className="flex items-center gap-1.5 text-[12px] font-medium">
                    <CloudUpload className="h-3.5 w-3.5 text-primary" />
                    管理员操作
                  </h3>

                  <div className="space-y-2 rounded-md border border-border bg-card/40 p-2.5">
                    <p className="text-[11px] font-medium">上传当前剧本为模板</p>
                    <Input
                      className="h-8 text-[12px]"
                      placeholder="模板名称"
                      value={newName}
                      onChange={(e) => setNewName(e.target.value)}
                    />
                    <Input
                      className="h-8 text-[12px]"
                      placeholder="说明（可选）"
                      value={newDesc}
                      onChange={(e) => setNewDesc(e.target.value)}
                    />
                    <Button
                      size="sm"
                      className="h-8 w-full gap-1.5"
                      disabled={busy === 'create'}
                      onClick={() => void handleCreate()}
                    >
                      <CloudUpload className="h-3.5 w-3.5" />
                      上传为模板
                    </Button>
                    <p className="text-[10px] text-muted-foreground">取当前工作区的剧本内容作为模板。</p>
                  </div>

                  <div className="space-y-2 rounded-md border border-border bg-card/40 p-2.5">
                    <p className="text-[11px] font-medium">上传剧本文件替换当前剧本</p>
                    <label
                      className={cn(
                        buttonLikeCls,
                        'h-8 w-full cursor-pointer justify-center gap-1.5',
                        busy === 'upload' && 'opacity-60'
                      )}
                    >
                      <FileUp className="h-3.5 w-3.5" />
                      选择 Scenario.json
                      <input
                        type="file"
                        accept=".json,application/json"
                        className="hidden"
                        disabled={busy === 'upload'}
                        onChange={(e) => {
                          const file = e.target.files?.[0]
                          e.target.value = ''
                          if (file) void handleUploadFile(file, false)
                        }}
                      />
                    </label>
                    <p className="text-[10px] text-muted-foreground">
                      会覆盖当前工作区的剧本，原文件另存为 .bak 备份。
                    </p>
                  </div>
                </div>
              )}

              {!canAdmin && auth?.authAvailable !== false && (
                <div className="flex items-start gap-1.5 rounded-md border border-border bg-card/40 p-2.5 text-[10px] text-muted-foreground">
                  <Lock className="mt-0.5 h-3 w-3 shrink-0" />
                  <span>上传模板与剧本需要管理员权限。当前账号只能浏览、下载与套用模板。</span>
                </div>
              )}
            </div>
          </div>
        </DialogContent>
      </Dialog>

      {/* 套用前确认：会覆盖当前工作区的剧本 */}
      <AlertDialog open={Boolean(confirmApply)} onOpenChange={(v) => !v && setConfirmApply(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>套用模板会覆盖当前剧本</AlertDialogTitle>
            <AlertDialogDescription className="text-[12px]">
              当前工作区已有剧本，套用「{confirmApply?.name}」会把它替换掉。
              原文件会自动备份，可在「概览 → 备份历史」中回滚。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction onClick={() => confirmApply && void doApply(confirmApply, true)}>
              确认覆盖
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* 删除确认 */}
      <AlertDialog open={Boolean(confirmDelete)} onOpenChange={(v) => !v && setConfirmDelete(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>删除模板「{confirmDelete?.name}」？</AlertDialogTitle>
            <AlertDialogDescription className="text-[12px]">
              将从模板库中彻底移除该模板，已套用过它的工作区不受影响。此操作不可撤销。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => confirmDelete && void doDelete(confirmDelete)}
            >
              删除
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}

/** 按钮外观（用于 <a> / <label> 这类非 Button 元素） */
const buttonLikeCls =
  'inline-flex items-center rounded-md border border-input bg-background px-3 text-[12px] shadow-xs transition-colors hover:bg-accent hover:text-accent-foreground'
