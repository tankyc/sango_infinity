/**
 * 轻量 className 合并
 * 只做「过滤假值 + 拼接」这一件事，避免为这点需求额外引入 clsx / tailwind-merge。
 */
export function cn(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(' ');
}
