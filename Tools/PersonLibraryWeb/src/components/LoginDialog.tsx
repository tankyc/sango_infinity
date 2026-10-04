/**
 * 文件名：LoginDialog.tsx
 * 描述：登录 / 注册弹窗。
 *       未登录状态下所有访客都是「游客」，仅可浏览武将库；
 *       登录后依据账号角色获得相应权限（编辑员可增删改，管理员还可管理账号，
 *       超级管理员额外可做「备份与还原」）；
 *       后端开放注册时（REGISTER_ENABLED）此弹窗会多出「注册」标签页，
 *       注册成功即自动登录，默认角色由后端 REGISTER_ROLE 决定（默认编辑员）。
 */

import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { login, register as registerAccount, setToken } from '@/lib/api'
import type { AuthUser, RegisterInfo } from '@/lib/types'
import { AlertCircle, Eye, EyeOff, Loader2, LogIn, ShieldCheck, UserPlus, UserRound } from 'lucide-react'

interface LoginDialogProps {
  /** 是否打开 */
  open: boolean
  /** 打开状态变化 */
  onOpenChange: (open: boolean) => void
  /** 登录 / 注册成功回调 */
  onSuccess: (user: AuthUser) => void
  /** 注册状态（后端下发）；未开放注册时隐藏注册标签页 */
  registerInfo?: RegisterInfo | null
}

/**
 * 密码输入框（带明文切换）。
 * @param props.id 输入框 id
 * @param props.value 当前值
 * @param props.autoComplete 自动填充提示
 * @param props.placeholder 占位文案
 * @param props.show 是否明文显示
 * @param props.onToggleShow 切换明文显示
 * @param props.onChange 值变化回调
 * @returns 密码输入框
 */
function PasswordInput({
  id,
  value,
  autoComplete,
  placeholder,
  show,
  onToggleShow,
  onChange,
}: {
  id: string
  value: string
  autoComplete: string
  placeholder: string
  show: boolean
  onToggleShow: () => void
  onChange: (value: string) => void
}) {
  return (
    <div className="relative">
      <Input
        id={id}
        type={show ? 'text' : 'password'}
        autoComplete={autoComplete}
        placeholder={placeholder}
        className="pr-10"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      <button
        type="button"
        title={show ? '隐藏密码' : '显示密码'}
        className="absolute right-2 top-1/2 -translate-y-1/2 cursor-pointer text-muted-foreground hover:text-foreground"
        onClick={onToggleShow}
      >
        {show ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
      </button>
    </div>
  )
}

/**
 * 表单提示条。
 * @param props.tag 标签文案
 * @param props.children 内容
 * @returns 提示条
 */
function HintBar({ tag, children }: { tag: string; children: ReactNode }) {
  return (
    <div className="flex items-center gap-2 rounded-md border border-border bg-secondary/30 px-3 py-2 text-xs text-muted-foreground">
      <Badge variant="secondary" className="text-[10px]">
        {tag}
      </Badge>
      <span>{children}</span>
    </div>
  )
}

/**
 * 错误提示条。
 * @param props.message 错误信息
 * @returns 错误提示条（无错误时为 null）
 */
function ErrorBar({ message }: { message: string }) {
  if (!message) return null
  return (
    <div className="flex items-center gap-2 rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
      <AlertCircle className="size-4 shrink-0" />
      <span>{message}</span>
    </div>
  )
}

export function LoginDialog({ open, onOpenChange, onSuccess, registerInfo }: LoginDialogProps) {
  /** 当前标签页：登录 / 注册 */
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [username, setUsername] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  /** 密码是否明文显示 */
  const [showPassword, setShowPassword] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  /** 注册入口是否可用 */
  const canRegister = Boolean(registerInfo?.enabled)

  // 每次打开重置表单状态
  useEffect(() => {
    if (!open) return
    setMode('login')
    setUsername('')
    setDisplayName('')
    setPassword('')
    setConfirmPassword('')
    setShowPassword(false)
    setError('')
  }, [open])

  /** 切换标签页时清空密码与错误提示 */
  const changeMode = (value: string) => {
    setMode(value === 'register' ? 'register' : 'login')
    setError('')
    setPassword('')
    setConfirmPassword('')
  }

  /**
   * 提交登录。
   * @param e 表单事件
   */
  const handleLogin = async (e: FormEvent) => {
    e.preventDefault()
    if (!username.trim() || !password) {
      setError('请输入用户名与密码')
      return
    }
    setError('')
    setSubmitting(true)
    try {
      const result = await login(username.trim(), password)
      setToken(result.token)
      onSuccess(result.user)
      onOpenChange(false)
    } catch (err) {
      setError(err instanceof Error ? err.message : '登录失败')
    } finally {
      setSubmitting(false)
    }
  }

  /**
   * 提交注册（成功后自动登录）。
   * @param e 表单事件
   */
  const handleRegister = async (e: FormEvent) => {
    e.preventDefault()
    if (!username.trim()) {
      setError('请输入用户名')
      return
    }
    if (password.length < 6) {
      setError('密码长度不能少于 6 位')
      return
    }
    if (password !== confirmPassword) {
      setError('两次输入的密码不一致')
      return
    }
    setError('')
    setSubmitting(true)
    try {
      const result = await registerAccount({
        username: username.trim(),
        password,
        displayName: displayName.trim(),
      })
      setToken(result.token)
      onSuccess(result.user)
      onOpenChange(false)
    } catch (err) {
      setError(err instanceof Error ? err.message : '注册失败')
    } finally {
      setSubmitting(false)
    }
  }

  /** 登录表单 */
  const loginForm = (
    <form className="flex flex-col gap-4" onSubmit={handleLogin}>
      <div className="flex flex-col gap-2">
        <Label htmlFor="login-username">用户名</Label>
        <div className="relative">
          <UserRound className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            id="login-username"
            className="pl-9"
            autoComplete="username"
            placeholder="请输入用户名"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
          />
        </div>
      </div>

      <div className="flex flex-col gap-2">
        <Label htmlFor="login-password">密码</Label>
        <PasswordInput
          id="login-password"
          value={password}
          autoComplete="current-password"
          placeholder="请输入密码"
          show={showPassword}
          onToggleShow={() => setShowPassword((v) => !v)}
          onChange={setPassword}
        />
      </div>

      <ErrorBar message={error} />

      <HintBar tag="提示">
        首次启动会自动创建管理员账号 <span className="text-primary">admin / admin123</span>，
        请登录后及时修改密码。
      </HintBar>

      <DialogFooter className="gap-2 sm:justify-between">
        <Button
          type="button"
          variant="ghost"
          onClick={() => onOpenChange(false)}
          disabled={submitting}
        >
          游客浏览
        </Button>
        <Button type="submit" disabled={submitting}>
          {submitting ? <Loader2 className="animate-spin" /> : <LogIn />}
          登录
        </Button>
      </DialogFooter>
    </form>
  )

  /** 注册表单 */
  const registerForm = (
    <form className="flex flex-col gap-4" onSubmit={handleRegister}>
      <div className="flex flex-col gap-2">
        <Label htmlFor="register-username">用户名</Label>
        <div className="relative">
          <UserRound className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            id="register-username"
            className="pl-9"
            autoComplete="username"
            placeholder="2 - 32 位字母、数字、下划线、中划线或点"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
          />
        </div>
      </div>

      <div className="flex flex-col gap-2">
        <Label htmlFor="register-display">显示名称（可选）</Label>
        <Input
          id="register-display"
          placeholder="不填则使用用户名"
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
        />
      </div>

      <div className="flex flex-col gap-2">
        <Label htmlFor="register-password">密码</Label>
        <PasswordInput
          id="register-password"
          value={password}
          autoComplete="new-password"
          placeholder="至少 6 位"
          show={showPassword}
          onToggleShow={() => setShowPassword((v) => !v)}
          onChange={setPassword}
        />
      </div>

      <div className="flex flex-col gap-2">
        <Label htmlFor="register-confirm">确认密码</Label>
        <Input
          id="register-confirm"
          type={showPassword ? 'text' : 'password'}
          autoComplete="new-password"
          placeholder="再次输入密码"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
        />
      </div>

      <ErrorBar message={error} />

      <HintBar tag="说明">
        注册后的默认身份为
        <span className="text-primary"> {registerInfo?.roleLabel || '编辑员'} </span>
        （可新建 / 修改武将），注册成功即自动登录。
      </HintBar>

      <DialogFooter className="gap-2 sm:justify-between">
        <Button
          type="button"
          variant="ghost"
          onClick={() => onOpenChange(false)}
          disabled={submitting}
        >
          游客浏览
        </Button>
        <Button type="submit" disabled={submitting}>
          {submitting ? <Loader2 className="animate-spin" /> : <UserPlus />}
          注册并登录
        </Button>
      </DialogFooter>
    </form>
  )

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 font-display text-xl">
            <ShieldCheck className="size-5 text-primary" />
            {canRegister && mode === 'register' ? '玩家注册' : '账号登录'}
          </DialogTitle>
          <DialogDescription>
            {canRegister && mode === 'register'
              ? '注册后即可编辑武将库；请牢记密码，账号暂不支持自助找回。'
              : '登录后可编辑武将库；未登录将以游客身份浏览，不能新增、修改或删除。'}
          </DialogDescription>
        </DialogHeader>

        {canRegister ? (
          <Tabs value={mode} onValueChange={changeMode}>
            <TabsList className="grid w-full grid-cols-2">
              <TabsTrigger value="login">登录</TabsTrigger>
              <TabsTrigger value="register">注册</TabsTrigger>
            </TabsList>
            <TabsContent value="login" className="mt-4">
              {loginForm}
            </TabsContent>
            <TabsContent value="register" className="mt-4">
              {registerForm}
            </TabsContent>
          </Tabs>
        ) : (
          loginForm
        )}
      </DialogContent>
    </Dialog>
  )
}
