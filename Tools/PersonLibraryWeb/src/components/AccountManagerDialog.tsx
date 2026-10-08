/**
 * 文件名：AccountManagerDialog.tsx
 * 描述：账号管理弹窗（仅管理员可见）。
 *       支持新建账号、调整角色、重置密码与删除账号。
 *       角色说明：
 *       - 管理员：可增删改武将，并可管理账号；
 *       - 编辑员：可增删改武将，不能管理账号；
 *       - 游客：仅可浏览，不能新增 / 修改 / 删除。
 */

import { useCallback, useEffect, useState } from 'react'
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
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  createAccount,
  deleteAccount,
  fetchAccounts,
  updateAccount,
} from '@/lib/api'
import { cn } from '@/lib/utils'
import type { AuthUser, Role, RoleOption } from '@/lib/types'
import { KeyRound, Loader2, RefreshCw, ShieldCheck, Trash2, UserPlus, Users } from 'lucide-react'

interface AccountManagerDialogProps {
  /** 是否打开 */
  open: boolean
  /** 打开状态变化 */
  onOpenChange: (open: boolean) => void
  /** 当前登录用户（用于禁止删除自己） */
  currentUser: AuthUser | null
  /** 账号数据变化后的回调（例如当前账号角色被修改） */
  onChanged?: () => void
}

/** 角色对应的徽章样式 */
const ROLE_BADGE: Record<Role, string> = {
  super: 'border-gold/50 bg-gold/15 text-gold',
  admin: 'border-primary/50 bg-primary/15 text-primary',
  editor: 'border-border bg-secondary text-secondary-foreground',
  guest: 'border-border bg-secondary/60 text-muted-foreground',
}

/** 新建账号表单默认值 */
const EMPTY_FORM = { username: '', displayName: '', password: '', role: 'editor' as Role }

export function AccountManagerDialog({
  open,
  onOpenChange,
  currentUser,
  onChanged,
}: AccountManagerDialogProps) {
  const [accounts, setAccounts] = useState<AuthUser[]>([])
  const [roles, setRoles] = useState<RoleOption[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const [form, setForm] = useState(EMPTY_FORM)
  const [creating, setCreating] = useState(false)

  /** 重置密码目标账号 */
  const [pwdTarget, setPwdTarget] = useState<AuthUser | null>(null)
  const [newPassword, setNewPassword] = useState('')
  const [savingPwd, setSavingPwd] = useState(false)

  /** 删除目标账号 */
  const [deleteTarget, setDeleteTarget] = useState<AuthUser | null>(null)
  const [deleting, setDeleting] = useState(false)

  /** 正在提交的角色变更账号名 */
  const [roleSaving, setRoleSaving] = useState('')

  /** 加载账号列表 */
  const load = useCallback(async () => {
    setLoading(true)
    try {
      const data = await fetchAccounts()
      setAccounts(data.accounts)
      setRoles(data.roles)
      setError('')
    } catch (err) {
      setError(err instanceof Error ? err.message : '账号列表加载失败')
    } finally {
      setLoading(false)
    }
  }, [])

  // 打开时加载账号列表并重置表单
  useEffect(() => {
    if (!open) return
    setForm(EMPTY_FORM)
    setPwdTarget(null)
    setDeleteTarget(null)
    setNewPassword('')
    load()
  }, [open, load])

  /** 判断是否为当前登录账号 */
  const isSelf = (account: AuthUser) =>
    Boolean(currentUser) && account.username === currentUser?.username

  /** 当前登录者是否为超级管理员 */
  const isSuperViewer = currentUser?.role === 'super'

  /**
   * 是否为「不可改动的超级管理员账号」。
   * 超级管理员账号只能由超级管理员本人管理，其它管理员只读。
   * @param account 目标账号
   * @returns 是否只读
   */
  const isLockedSuper = (account: AuthUser) => account.role === 'super' && !isSuperViewer

  /** 新建账号 */
  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!form.username.trim() || !form.password) {
      toast.error('请填写用户名与密码')
      return
    }
    setCreating(true)
    try {
      await createAccount({
        username: form.username.trim(),
        displayName: form.displayName.trim() || form.username.trim(),
        password: form.password,
        role: form.role,
      })
      toast.success(`已创建账号「${form.username.trim()}」`)
      setForm(EMPTY_FORM)
      await load()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : '创建账号失败')
    } finally {
      setCreating(false)
    }
  }

  /** 修改角色 */
  const handleRoleChange = async (account: AuthUser, role: Role) => {
    setRoleSaving(account.username)
    try {
      await updateAccount(account.username, { role })
      toast.success(`已将「${account.username}」的角色调整为 ${roleLabel(role)}`)
      await load()
      onChanged?.()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : '角色修改失败')
    } finally {
      setRoleSaving('')
    }
  }

  /** 重置密码 */
  const handleResetPassword = async () => {
    if (!pwdTarget) return
    if (newPassword.length < 6) {
      toast.error('密码长度不能少于 6 位')
      return
    }
    setSavingPwd(true)
    try {
      await updateAccount(pwdTarget.username, { password: newPassword })
      toast.success(`已重置「${pwdTarget.username}」的密码`)
      setPwdTarget(null)
      setNewPassword('')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : '密码重置失败')
    } finally {
      setSavingPwd(false)
    }
  }

  /** 删除账号 */
  const handleDelete = async () => {
    if (!deleteTarget) return
    setDeleting(true)
    try {
      await deleteAccount(deleteTarget.username)
      toast.success(`已删除账号「${deleteTarget.username}」`)
      setDeleteTarget(null)
      await load()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : '删除账号失败')
    } finally {
      setDeleting(false)
    }
  }

  /** 角色显示名 */
  function roleLabel(role: Role) {
    return roles.find((r) => r.id === role)?.name || role
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/*
        基类 DialogContent 带 sm:max-w-lg，必须用同前缀的 sm:max-w-* 才能覆盖，否则宽度会被限制在 512px。
        高度用 grid 的两行来分：标题 auto，内容 minmax(0,1fr)。
        这里必须让这一行能收缩到 0，内容区才会在账号变多时变成可滚动的独立区域；
        用 flex-1 的话行高会按内容撑开，整个弹窗溢出视口且滚不动。
      */}
      <DialogContent className="grid max-h-[88vh] grid-rows-[auto_minmax(0,1fr)] gap-0 overflow-hidden p-0 sm:max-w-[min(68rem,calc(100vw-2rem))]">
        <DialogHeader className="border-b border-border px-6 py-4">
          <DialogTitle className="flex items-center gap-2 font-display text-xl">
            <Users className="size-5 text-primary" />
            账号管理
          </DialogTitle>
          <DialogDescription>
            管理员可维护账号与角色。游客仅可浏览武将库，编辑员可增删改武将。
          </DialogDescription>
        </DialogHeader>

        {/*
          滚动区：账号多起来时只让这一块滚，标题栏与关闭按钮始终留在原位。
          这里用原生 overflow-y-auto 而不是 Radix 的 ScrollArea ——
          ScrollArea 的视口是 height:100%，需要父级有确定高度；在受 max-h 约束的
          弹窗里它拿不到，结果是整块内容把弹窗撑破且滚不动。
        */}
        <div className="min-h-0 overflow-y-auto overscroll-contain [scrollbar-width:thin] [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-border [&::-webkit-scrollbar]:w-2 [&::-webkit-scrollbar-track]:bg-transparent">
          <div className="flex flex-col gap-6 p-6">
            {/* 新建账号 */}
            <section className="rounded-lg border border-border bg-card/40 p-4">
              <h3 className="mb-3 flex items-center gap-2 text-sm font-medium">
                <UserPlus className="size-4 text-primary" />
                新建账号
              </h3>
              <form className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5" onSubmit={handleCreate}>
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="new-username">用户名</Label>
                  <Input
                    id="new-username"
                    placeholder="字母 / 数字，2-32 位"
                    value={form.username}
                    onChange={(e) => setForm((p) => ({ ...p, username: e.target.value }))}
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="new-display">显示名称</Label>
                  <Input
                    id="new-display"
                    placeholder="默认与用户名相同"
                    value={form.displayName}
                    onChange={(e) => setForm((p) => ({ ...p, displayName: e.target.value }))}
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="new-password">密码</Label>
                  <Input
                    id="new-password"
                    type="password"
                    placeholder="不少于 6 位"
                    value={form.password}
                    onChange={(e) => setForm((p) => ({ ...p, password: e.target.value }))}
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label>角色</Label>
                  <Select
                    value={form.role}
                    onValueChange={(v) => setForm((p) => ({ ...p, role: v as Role }))}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {roles.map((r) => (
                        <SelectItem key={r.id} value={r.id}>
                          {r.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="flex items-end">
                  <Button type="submit" className="w-full" disabled={creating}>
                    {creating ? <Loader2 className="animate-spin" /> : <UserPlus />}
                    创建
                  </Button>
                </div>
              </form>
            </section>

            {/* 账号列表 */}
            <section className="flex flex-col gap-3">
              <div className="flex items-center justify-between">
                <h3 className="flex items-center gap-2 text-sm font-medium">
                  <ShieldCheck className="size-4 text-primary" />
                  账号列表（{accounts.length}）
                </h3>
                <Button variant="ghost" size="sm" onClick={load} disabled={loading}>
                  {loading ? <Loader2 className="animate-spin" /> : <RefreshCw />}
                  刷新
                </Button>
              </div>

              {error && <p className="text-sm text-destructive">{error}</p>}

              <div className="overflow-x-auto rounded-lg border border-border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>用户名</TableHead>
                      <TableHead>显示名称</TableHead>
                      <TableHead className="w-40">角色</TableHead>
                      <TableHead>创建时间</TableHead>
                      <TableHead className="w-40 text-right">操作</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {accounts.length === 0 && (
                      <TableRow>
                        <TableCell colSpan={5} className="h-20 text-center text-muted-foreground">
                          {loading ? '正在加载...' : '暂无账号'}
                        </TableCell>
                      </TableRow>
                    )}
                    {accounts.map((account) => (
                      <TableRow key={account.username}>
                        <TableCell className="font-medium">
                          <span className="flex items-center gap-2">
                            {account.username}
                            {isSelf(account) && (
                              <Badge variant="outline" className="text-[10px]">
                                当前账号
                              </Badge>
                            )}
                          </span>
                        </TableCell>
                        <TableCell className="text-muted-foreground">{account.displayName}</TableCell>
                        <TableCell>
                          <Select
                            value={account.role}
                            disabled={roleSaving === account.username || isLockedSuper(account)}
                            onValueChange={(v) => handleRoleChange(account, v as Role)}
                          >
                            <SelectTrigger
                              className={cn('h-8', ROLE_BADGE[account.role])}
                              title={isLockedSuper(account) ? '超级管理员账号仅超级管理员可修改' : undefined}
                            >
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {roles.map((r) => (
                                <SelectItem key={r.id} value={r.id}>
                                  {r.name}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {formatTime(account.createdAt)}
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-1">
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              title={
                                isLockedSuper(account) ? '超级管理员账号仅超级管理员可修改' : '重置密码'
                              }
                              disabled={isLockedSuper(account)}
                              onClick={() => {
                                setPwdTarget(account)
                                setNewPassword('')
                              }}
                            >
                              <KeyRound />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              title={
                                isSelf(account)
                                  ? '不能删除当前账号'
                                  : isLockedSuper(account)
                                    ? '超级管理员账号仅超级管理员可删除'
                                    : '删除账号'
                              }
                              disabled={isSelf(account) || isLockedSuper(account)}
                              onClick={() => setDeleteTarget(account)}
                            >
                              <Trash2 className="text-destructive" />
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </section>
          </div>
        </div>
      </DialogContent>

      {/* 重置密码 */}
      <AlertDialog open={Boolean(pwdTarget)} onOpenChange={(o) => !o && setPwdTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>重置密码</AlertDialogTitle>
            <AlertDialogDescription>
              为账号「{pwdTarget?.username}」设置新密码。重置后该账号需要使用新密码重新登录。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <div className="flex flex-col gap-2">
            <Label htmlFor="reset-password">新密码</Label>
            <Input
              id="reset-password"
              type="password"
              placeholder="不少于 6 位"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
            />
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={savingPwd}>取消</AlertDialogCancel>
            <AlertDialogAction
              disabled={savingPwd}
              onClick={(e) => {
                e.preventDefault()
                handleResetPassword()
              }}
            >
              {savingPwd && <Loader2 className="animate-spin" />}
              确认重置
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* 删除确认 */}
      <AlertDialog open={Boolean(deleteTarget)} onOpenChange={(o) => !o && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>删除账号</AlertDialogTitle>
            <AlertDialogDescription>
              确定要删除账号「{deleteTarget?.username}」吗？该操作不可撤销。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleting}>取消</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              disabled={deleting}
              onClick={(e) => {
                e.preventDefault()
                handleDelete()
              }}
            >
              {deleting && <Loader2 className="animate-spin" />}
              确认删除
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Dialog>
  )
}

/**
 * 格式化创建时间。
 * @param value ISO 时间字符串
 * @returns 本地可读时间（解析失败时原样返回）
 */
function formatTime(value?: string): string {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleString('zh-CN', { hour12: false })
}
