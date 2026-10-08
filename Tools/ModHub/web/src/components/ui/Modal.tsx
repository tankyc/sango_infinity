import { useEffect, type ReactNode } from 'react';
import { X } from 'lucide-react';
import { Button } from './Button';
import { cn } from '../../lib/cn';

/**
 * 通用对话框
 *
 * 与 ConfirmDialog 的分工：ConfirmDialog 是"问一句话再确认"，本组件承载真正的表单
 * （编辑模组信息、发布新版本），因此提供标题栏、可滚动内容区与底部操作区。
 *
 * 可访问性：role=dialog + aria-modal，Esc 关闭，点击遮罩关闭（提交中禁止关闭），
 * 打开时锁定页面滚动，避免背景跟着滚。
 */
export interface ModalProps {
  open: boolean;
  title: string;
  description?: ReactNode;
  /** 底部操作区（通常是"取消 + 提交"） */
  footer?: ReactNode;
  onClose: () => void;
  /** 提交进行中：禁止关闭，防止请求已发出却看不到结果 */
  busy?: boolean;
  size?: 'md' | 'lg';
  children: ReactNode;
}

export function Modal({
  open,
  title,
  description,
  footer,
  onClose,
  busy = false,
  size = 'md',
  children,
}: ModalProps) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !busy) onClose();
    };
    window.addEventListener('keydown', onKey);
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      window.removeEventListener('keydown', onKey);
      document.body.style.overflow = prevOverflow;
    };
  }, [open, busy, onClose]);

  if (!open) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title}
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/60 p-4 backdrop-blur-sm sm:items-center"
      onClick={() => {
        if (!busy) onClose();
      }}
    >
      <div
        className={cn('panel my-auto w-full', size === 'lg' ? 'max-w-2xl' : 'max-w-lg')}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-3 border-b border-ink-700/70 p-4">
          <div className="min-w-0">
            <h2 className="font-serif text-lg text-paper-100">{title}</h2>
            {description ? <div className="mt-1 text-sm text-paper-500">{description}</div> : null}
          </div>
          <Button
            variant="ghost"
            size="sm"
            aria-label="关闭"
            disabled={busy}
            onClick={onClose}
            icon={<X aria-hidden className="h-4 w-4" />}
          />
        </div>

        <div className="max-h-[65vh] overflow-y-auto p-4">{children}</div>

        {footer ? <div className="flex justify-end gap-2 border-t border-ink-700/70 p-4">{footer}</div> : null}
      </div>
    </div>
  );
}
