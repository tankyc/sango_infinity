import { useEffect, useRef, useState } from 'react';
import { Check, Palette } from 'lucide-react';
import {
  DARK_THEMES,
  DEFAULT_THEME_KEY,
  LIGHT_THEMES,
  THEMES,
  applyTheme,
  readStoredTheme,
  type ThemeKey,
} from '../../lib/theme';
import { cn } from '../../lib/cn';

/**
 * 配色切换器
 *
 * 放在顶栏而不是设置页：配色是最需要「随手试」的东西，
 * 点开即见四个色块预览，选完立刻生效并记住（localStorage）。
 * 首屏由 index.html 的内联脚本先行应用，避免刷新时闪一下默认色。
 */
export function ThemeSwitcher() {
  const [theme, setTheme] = useState<ThemeKey>(DEFAULT_THEME_KEY);
  const [open, setOpen] = useState(false);
  const boxRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    setTheme(readStoredTheme());
  }, []);

  // 点击外部或按 Esc 关闭
  useEffect(() => {
    if (!open) return;
    const onMouseDown = (e: MouseEvent) => {
      if (boxRef.current && !boxRef.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onMouseDown);
    window.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onMouseDown);
      window.removeEventListener('keydown', onKey);
    };
  }, [open]);

  function pick(key: ThemeKey) {
    setTheme(key);
    applyTheme(key);
    setOpen(false);
  }

  const current = THEMES.find((t) => t.key === theme) ?? THEMES[0];

  return (
    <div className="relative" ref={boxRef}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="listbox"
        aria-expanded={open}
        title={`切换配色（当前：${current.label}）`}
        className="flex h-10 cursor-pointer items-center gap-2 rounded-md px-2.5 text-sm text-paper-300 transition-colors duration-200 hover:bg-ink-700/70 hover:text-paper-100"
      >
        <Palette aria-hidden className="h-4 w-4 text-gold-400" />
        <span className="hidden xl:inline">{current.label}</span>
      </button>

      {open ? (
        <div
          role="listbox"
          aria-label="配色方案"
          // 主题较多：按 深色 / 浅色 分组、两列排布并允许滚动，避免面板长到屏幕外
          className="panel absolute right-0 z-50 mt-2 max-h-[72vh] w-96 max-w-[calc(100vw-2rem)] animate-fade-up overflow-y-auto p-2"
        >
          {[
            { title: '深色', items: DARK_THEMES },
            { title: '浅色', items: LIGHT_THEMES },
          ].map((group) => (
            <div key={group.title}>
              <p className="px-2 pb-1.5 pt-2 text-[11px] tracking-[0.2em] text-paper-500">
                {group.title}
                <span className="ml-2 font-mono text-paper-600">{group.items.length}</span>
              </p>
              <div className="grid grid-cols-2 gap-1">
                {group.items.map((t) => {
                  const active = t.key === theme;
                  return (
                    <button
                      key={t.key}
                      type="button"
                      role="option"
                      aria-selected={active}
                      // 两列后每格较窄，完整说明放进 title，鼠标悬停可看
                      title={t.hint}
                      onClick={() => pick(t.key)}
                      className={cn(
                        'flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-left transition-colors duration-200',
                        active ? 'bg-gold-500/12' : 'hover:bg-ink-700/70',
                      )}
                    >
                      {/* 预览色块：左半底色、右半强调色，一眼看出方案差异 */}
                      <span
                        aria-hidden
                        className="flex h-6 w-6 shrink-0 overflow-hidden rounded border border-ink-500/70"
                      >
                        <span className="h-full w-1/2" style={{ background: t.swatch[0] }} />
                        <span className="h-full w-1/2" style={{ background: t.swatch[1] }} />
                      </span>
                      <span className="min-w-0 flex-1 truncate text-xs text-paper-200">{t.label}</span>
                      {active ? <Check aria-hidden className="h-3.5 w-3.5 shrink-0 text-gold-400" /> : null}
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      ) : null}
    </div>
  );
}
