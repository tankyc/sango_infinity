import { useEffect, type ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Button } from './Button';
import { cn } from '../../lib/cn';

/**
 * 确认对话框
 *
 * 管理后台的每个破坏性操作（封禁、删除、重置密码）都要过一次确认，
 * 原因写清楚、按钮文案写明后果，避免管理员在列表里误点。
 *
 * 可访问性：role=dialog + aria-modal，Esc 关闭，打开时焦点落在确认按钮上，
 * 点击遮罩关闭（但确认进行中不允许关闭，防止请求已发出却看不到结果）。
 */
export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description?: ReactNode;
  confirmText?: string;
  cancelText?: string;
  /** 危险操作：确认按钮转为朱砂色 */
  danger?: boolean;
  loading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  children?: ReactNode;
}

export function ConfirmDialog({
  open,
  title,
  description,
  confirmText = '确认',
  cancelText = '取消',
  danger = false,
  loading = false,
  onConfirm,
  onCancel,
  children,
}: ConfirmDialogProps) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !loading) onCancel();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, loading, onCancel]);

  if (!open) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title}
      // 遮罩固定用半透明黑：不能跟随主题（浅色主题下 ink-950 会变成白色，遮罩就失去"压暗"作用）
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
      onClick={() => {
        if (!loading) onCancel();
      }}
    >
      <div className="panel w-full max-w-md p-5" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-start gap-3">
          {danger ? <AlertTriangle aria-hidden className="mt-0.5 h-5 w-5 shrink-0 text-cinnabar-400" /> : null}
          <div className="min-w-0 flex-1">
            <h2 className="font-serif text-lg text-paper-100">{title}</h2>
            {description ? <div className="mt-2 text-sm leading-relaxed text-paper-400">{description}</div> : null}
            {children}
          </div>
        </div>

        <div className="mt-5 flex justify-end gap-2">
          <Button variant="ghost" onClick={onCancel} disabled={loading}>
            {cancelText}
          </Button>
          <Button
            autoFocus
            variant={danger ? 'cinnabar' : 'gold'}
            loading={loading}
            onClick={onConfirm}
            className={cn(danger && 'border-cinnabar-500/70')}
          >
            {confirmText}
          </Button>
        </div>
      </div>
    </div>
  );
}
