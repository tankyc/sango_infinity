import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { KeyRound, LogIn, UserPlus } from 'lucide-react';
import { ApiRequestError } from '../lib/api';
import { useAuth } from '../hooks/useAuth';
import { Button } from '../components/ui/Button';
import { Field, Input } from '../components/ui/Field';
import { ErrorState } from '../components/ui/Feedback';
import { cn } from '../lib/cn';

/**
 * 登录 / 注册
 *
 * 账号体系与游戏内云存档服务共用同一份契约（POST /login、POST /register），
 * 因此这里注册的账号可以直接在游戏内登录，反之亦然 —— 这是打通「网页订阅 + 游戏内下载」的前提。
 */
export default function LoginPage() {
  const { user, login, register } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  // 登录后回到来源页：受保护页面（如管理后台）会带 ?next= 过来，
  // 只接受站内绝对路径，避免被构造成跳转到外部站点的钓鱼链接。
  const rawNext = searchParams.get('next') ?? '';
  const nextPath = rawNext.startsWith('/') && !rawNext.startsWith('//') ? rawNext : '/browse';

  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<{ message: string; details: string[] } | null>(null);

  // 已登录用户不应停留在登录页
  useEffect(() => {
    if (user) navigate(nextPath, { replace: true });
  }, [user, navigate, nextPath]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    const name = username.trim();
    if (name === '' || password === '') {
      setError({ message: '请填写账号与密码', details: [] });
      return;
    }
    if (mode === 'register' && password !== confirm) {
      setError({ message: '两次输入的密码不一致', details: [] });
      return;
    }

    setSubmitting(true);
    try {
      if (mode === 'login') await login(name, password);
      else await register(name, password);
      navigate(nextPath, { replace: true });
    } catch (err) {
      if (err instanceof ApiRequestError) {
        setError({ message: err.message, details: err.details });
      } else {
        setError({ message: '操作失败，请稍后重试', details: [] });
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="relative mx-auto max-w-md px-4 py-12">
      {/* 装饰：策马武将剪影作页面底图，登录卡片浮在其上 */}
      <img
        src="/art/warrior.jpg"
        alt=""
        aria-hidden
        className="pointer-events-none absolute left-1/2 top-2 w-[min(560px,92vw)] -translate-x-1/2 rounded-xl opacity-[var(--art-opacity)] [filter:brightness(var(--art-brightness))_saturate(1.05)]"
      />
      <div className="panel paper-grain relative p-6 sm:p-8">
        <h1 className="font-serif text-2xl text-paper-100">Sango 账号</h1>
        <p className="mt-1.5 text-sm leading-relaxed text-paper-500">
          与游戏内云存档服务共用同一账号：网页登录后即可发布模组，游戏内使用同一账号即可同步订阅与下载。
        </p>

        {/* 登录 / 注册切换 */}
        <div role="tablist" aria-label="登录或注册" className="mt-5 grid grid-cols-2 gap-1 rounded-md border border-ink-500/70 p-1">
          {(
            [
              { key: 'login', label: '登录', icon: LogIn },
              { key: 'register', label: '注册', icon: UserPlus },
            ] as const
          ).map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              type="button"
              role="tab"
              aria-selected={mode === key}
              onClick={() => {
                setMode(key);
                setError(null);
              }}
              className={cn(
                'flex h-9 cursor-pointer items-center justify-center gap-2 rounded text-sm transition-colors duration-200',
                mode === key ? 'bg-gold-500/15 text-gold-300' : 'text-paper-400 hover:text-paper-100',
              )}
            >
              <Icon aria-hidden className="h-4 w-4" />
              {label}
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit} className="mt-5 space-y-4">
          <Field label="账号" htmlFor="username" required hint="3–32 位字母、数字、下划线、点或短横线">
            <Input
              id="username"
              name="username"
              autoComplete="username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="例如 taoge75"
            />
          </Field>

          <Field
            label="密码"
            htmlFor="password"
            required
            hint={mode === 'register' ? '6–128 位，建议与游戏内账号保持一致' : undefined}
          >
            <Input
              id="password"
              name="password"
              type="password"
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </Field>

          {mode === 'register' ? (
            <Field label="确认密码" htmlFor="confirm" required>
              <Input
                id="confirm"
                name="confirm"
                type="password"
                autoComplete="new-password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
              />
            </Field>
          ) : null}

          {error ? <ErrorState message={error.message} details={error.details} /> : null}

          <Button
            type="submit"
            variant="gold"
            size="lg"
            loading={submitting}
            className="w-full"
            icon={<KeyRound aria-hidden className="h-4 w-4" />}
          >
            {mode === 'login' ? '登录' : '注册并登录'}
          </Button>
        </form>
      </div>
    </div>
  );
}
