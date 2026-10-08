import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  ApiRequestError,
  fetchMe,
  login as apiLogin,
  register as apiRegister,
  setToken,
  getToken,
  type AuthUser,
} from '../lib/api';

/**
 * 登录态
 *
 * Token 与游戏内 CloudSaveClient 使用同一套账号体系（同一份 JWT），
 * 因此在网页登录后拿到的 token 也可以直接喂给游戏客户端使用。
 * 页面刷新时用 /me 校验一次，token 失效则静默登出，不打断浏览。
 */

interface AuthContextValue {
  user: AuthUser | null;
  /** 首次校验登录态是否仍在进行 */
  initializing: boolean;
  login: (username: string, password: string) => Promise<void>;
  register: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [initializing, setInitializing] = useState(true);

  useEffect(() => {
    if (!getToken()) {
      setInitializing(false);
      return;
    }
    const controller = new AbortController();
    fetchMe(controller.signal)
      .then((res) => setUser(res.user))
      .catch((e: unknown) => {
        // 【关键】组件卸载 / StrictMode 的"挂载→卸载→再挂载"会中止这次请求，
        // 这属于正常现象，绝不能当成登录失效——否则刚拿到的 token 会被误清，
        // 表现为"页面看着已登录，但后续请求全部 401（缺少或无效的 Bearer Token）"。
        if (controller.signal.aborted) return;

        // 只有明确的鉴权失败才登出；网络不可用（服务未启动、断网）时保留登录态，
        // 避免服务重启期间把用户踢下线。
        if (e instanceof ApiRequestError && (e.status === 401 || e.status === 403)) {
          setToken(null);
          setUser(null);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setInitializing(false);
      });
    return () => controller.abort();
  }, []);

  const login = useCallback(async (username: string, password: string) => {
    const res = await apiLogin(username, password);
    setToken(res.token);
    setUser(res.user);
  }, []);

  const register = useCallback(async (username: string, password: string) => {
    const res = await apiRegister(username, password);
    setToken(res.token);
    setUser(res.user);
  }, []);

  const logout = useCallback(() => {
    setToken(null);
    setUser(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ user, initializing, login, register, logout }),
    [user, initializing, login, register, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth 必须在 AuthProvider 内使用');
  return ctx;
}
