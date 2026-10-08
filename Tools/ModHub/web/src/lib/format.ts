/**
 * 展示层格式化工具
 * 数字与时间在工坊里出现频率极高（大小 / 订阅数 / 更新日期），统一在这里处理，
 * 保证列表、详情、上传页三处的呈现口径完全一致。
 */

const UNITS = ['B', 'KB', 'MB', 'GB'];

/** 字节数 → 人类可读（如 1.93 MB）。列表里空间紧张，故保留两位有效小数 */
export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '—';
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < UNITS.length - 1) {
    value /= 1024;
    unit += 1;
  }
  const digits = value >= 100 || unit === 0 ? 0 : value >= 10 ? 1 : 2;
  return `${value.toFixed(digits)} ${UNITS[unit]}`;
}

/** 计数 → 千分位；上万后用「万」，符合中文阅读习惯 */
export function formatCount(n: number): string {
  if (!Number.isFinite(n) || n <= 0) return '0';
  if (n >= 10000) {
    const w = n / 10000;
    return `${w >= 100 ? w.toFixed(0) : w.toFixed(1)} 万`;
  }
  return n.toLocaleString('zh-CN');
}

/** ISO 字符串 → YYYY-MM-DD */
export function formatDate(iso: string | Date | null | undefined): string {
  if (!iso) return '—';
  const d = typeof iso === 'string' ? new Date(iso) : iso;
  if (Number.isNaN(d.getTime())) return '—';
  const y = d.getFullYear();
  const m = `${d.getMonth() + 1}`.padStart(2, '0');
  const day = `${d.getDate()}`.padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/** ISO 字符串 → 相对时间（刚刚 / 3 小时前 / 5 天前 / 具体日期） */
export function formatRelative(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';

  const diffMs = Date.now() - d.getTime();
  if (diffMs < 60_000) return '刚刚';
  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 60) return `${minutes} 分钟前`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} 小时前`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days} 天前`;
  return formatDate(d);
}

/** 版本号比较：相等返回 0，a > b 返回 1，a < b 返回 -1（仅用于前端判断"有更新"提示） */
export function compareVersion(a: string, b: string): number {
  const pa = a.split(/[.+\-]/);
  const pb = b.split(/[.+\-]/);
  const len = Math.max(pa.length, pb.length);
  for (let i = 0; i < len; i += 1) {
    const na = Number.parseInt(pa[i] ?? '0', 10);
    const nb = Number.parseInt(pb[i] ?? '0', 10);
    const va = Number.isFinite(na) ? na : 0;
    const vb = Number.isFinite(nb) ? nb : 0;
    if (va !== vb) return va > vb ? 1 : -1;
  }
  return 0;
}
