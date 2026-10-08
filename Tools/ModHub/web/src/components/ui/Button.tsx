import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { Loader2 } from 'lucide-react';
import { cn } from '../../lib/cn';

/**
 * 按钮
 * 变体只有四种，避免工坊里出现五花八门的按钮：
 *   gold     —— 页面主操作（订阅 / 发布），全站唯一的高饱和色块
 *   outline  —— 次级操作（下载、更新）
 *   ghost    —— 导航与工具栏
 *   cinnabar —— 危险操作（退订、删除）
 */

export type ButtonVariant = 'gold' | 'outline' | 'ghost' | 'cinnabar';
export type ButtonSize = 'sm' | 'md' | 'lg';

const VARIANTS: Record<ButtonVariant, string> = {
  // 金底上的文字用 onaccent：深色主题下是近黑色，浅色主题下自动变白
  gold: 'border border-gold-600 bg-gold-500 font-semibold text-onaccent hover:bg-gold-400 active:bg-gold-600',
  outline: 'border border-gold-700/70 text-gold-300 hover:border-gold-500 hover:bg-gold-500/10',
  ghost: 'text-paper-300 hover:bg-ink-700/70 hover:text-paper-100',
  cinnabar: 'border border-cinnabar-500/60 text-cinnabar-400 hover:bg-cinnabar-500/15',
};

const SIZES: Record<ButtonSize, string> = {
  sm: 'h-8 gap-1.5 px-3 text-xs',
  md: 'h-10 gap-2 px-4 text-sm',
  lg: 'h-12 gap-2 px-6 text-base',
};

const BASE =
  'inline-flex cursor-pointer items-center justify-center whitespace-nowrap rounded-md transition-colors duration-200 disabled:cursor-not-allowed disabled:opacity-50';

/**
 * 生成按钮样式
 * 供 `<a>` 形态的按钮复用（下载这类本质是超链接的操作，应当保留浏览器原生的
 * 新标签打开、右键另存为能力，因此不能用 button + onClick 代替）。
 */
export function buttonClass(variant: ButtonVariant = 'gold', size: ButtonSize = 'md', className?: string): string {
  return cn(BASE, VARIANTS[variant], SIZES[size], className);
}

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** 加载中：按钮禁用并显示转圈，防止重复提交 */
  loading?: boolean;
  icon?: ReactNode;
}

export function Button({
  variant = 'gold',
  size = 'md',
  loading = false,
  icon,
  className,
  children,
  disabled,
  type = 'button',
  ...rest
}: ButtonProps) {
  return (
    <button
      {...rest}
      type={type}
      disabled={disabled || loading}
      className={buttonClass(variant, size, cn('disabled:hover:bg-transparent', className))}
    >
      {loading ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : icon}
      {children}
    </button>
  );
}
