import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '../ui/Button';
import { formatCount } from '../../lib/format';

/**
 * 后台分页控件
 * 后台需要「共 N 条 / 第几页」这类精确信息，因此与前台浏览页的无限滚动不同，
 * 这里用最直白的上一页/下一页 + 页码显示，不做跳页输入（数据量小，翻两下就到）。
 */
export function Pagination({
  page,
  pageSize,
  total,
  onChange,
}: {
  page: number;
  pageSize: number;
  total: number;
  onChange: (page: number) => void;
}) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 pt-3 text-xs text-paper-500">
      <span>
        共 <span className="font-mono text-paper-300">{formatCount(total)}</span> 条
        {total > 0 ? (
          <>
            {' '}
            · 当前显示 {from}–{to}
          </>
        ) : null}
      </span>
      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          disabled={page <= 1}
          onClick={() => onChange(page - 1)}
          icon={<ChevronLeft aria-hidden className="h-3.5 w-3.5" />}
        >
          上一页
        </Button>
        <span className="font-mono">
          {page} / {totalPages}
        </span>
        <Button
          variant="outline"
          size="sm"
          disabled={page >= totalPages}
          onClick={() => onChange(page + 1)}
        >
          下一页
          <ChevronRight aria-hidden className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
}
