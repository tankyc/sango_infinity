import { useState } from 'react';
import type { ComponentProps } from 'react';
import { Check, Copy } from 'lucide-react';
import { copyText } from '../../lib/clipboard';
import { Button } from './Button';

interface CopyButtonProps {
  /** 要复制的内容 */
  value: string;
  /** 按钮文字；不传则只显示图标（适合塞进一行信息里） */
  label?: string;
  variant?: ComponentProps<typeof Button>['variant'];
  size?: ComponentProps<typeof Button>['size'];
  className?: string;
}

/**
 * 一键复制按钮
 *
 * 模组 ID 是前置依赖要填写的东西，作者需要把它交给别人；
 * 8 位短 ID 虽然已去掉易混字符，手抄仍然容易错，所以详情页与发布结果都提供复制。
 */
export function CopyButton({ value, label, variant = 'outline', size = 'sm', className }: CopyButtonProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    const ok = await copyText(value);
    if (!ok) return;
    setCopied(true);
    window.setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Button
      variant={variant}
      size={size}
      className={className}
      onClick={handleCopy}
      title={copied ? '已复制' : '复制'}
      icon={copied ? <Check aria-hidden className="h-3.5 w-3.5" /> : <Copy aria-hidden className="h-3.5 w-3.5" />}
    >
      {label ? (copied ? '已复制' : label) : null}
    </Button>
  );
}
