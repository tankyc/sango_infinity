import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react';
import { cn } from '../../lib/cn';

/**
 * 表单基础件
 * 所有输入控件都由外层 Field 提供 <label>，保证表单可访问性（点击标签可聚焦、屏幕阅读器可朗读）。
 */

const CONTROL = cn(
  'w-full rounded-md border border-ink-500/80 bg-ink-900/70 px-3 py-2 text-sm text-paper-100',
  'placeholder:text-paper-600 transition-colors duration-200',
  'hover:border-gold-700/70 focus:border-gold-500 focus:outline-none',
  'disabled:cursor-not-allowed disabled:opacity-50',
);

export interface FieldProps {
  label: string;
  /** 字段说明或校验提示 */
  hint?: ReactNode;
  required?: boolean;
  htmlFor?: string;
  children: ReactNode;
  className?: string;
}

export function Field({ label, hint, required, htmlFor, children, className }: FieldProps) {
  return (
    <div className={cn('space-y-1.5', className)}>
      <label htmlFor={htmlFor} className="flex items-center gap-1 text-sm text-paper-200">
        {label}
        {required ? (
          <span className="text-cinnabar-400" aria-hidden>
            *
          </span>
        ) : null}
      </label>
      {children}
      {hint ? <p className="text-xs text-paper-500">{hint}</p> : null}
    </div>
  );
}

export function Input({ className, ...rest }: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...rest} className={cn(CONTROL, className)} />;
}

export function Textarea({ className, ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...rest} className={cn(CONTROL, 'min-h-24 resize-y leading-relaxed', className)} />;
}

export function Select({ className, children, ...rest }: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select {...rest} className={cn(CONTROL, 'cursor-pointer appearance-none pr-8', className)}>
      {children}
    </select>
  );
}
