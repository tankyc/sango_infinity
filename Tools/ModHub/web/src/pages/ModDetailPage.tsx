import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import {
  ChevronRight,
  Clock,
  Download,
  Eye,
  Heart,
  Link2,
  PackageCheck,
  UserRound,
  Users,
} from 'lucide-react';
import { ApiRequestError, fetchModDetail, type ModDetail } from '../lib/api';
import { categoryLabel } from '../lib/catalog';
import { formatBytes, formatCount, formatDate, formatRelative } from '../lib/format';
import { MiniMarkdown } from '../lib/miniMarkdown';
import { Badge, MetaRow, SectionTitle } from '../components/ui/Badge';
import { buttonClass } from '../components/ui/Button';
import { CategoryIcon } from '../components/ui/CategoryIcon';
import { CopyButton } from '../components/ui/CopyButton';
import { ErrorState, LoadingBlock } from '../components/ui/Feedback';
import { PosterImage } from '../components/browse/PosterImage';
import { modDetailPath } from '../components/browse/ModCard';
import { CommentSection } from '../components/comments/CommentSection';

/**
 * 模组详情页 /sharedfiles/filedetails/?id={id}（对应 建设计划.md §4.2）
 *
 * 布局照 Steam：左主栏（大封面 / 简介 / 变更日志 / 版本历史）+ 右信息栏（操作 / 统计 / 标签 / 依赖 / 游戏内订阅）。
 * 右栏在桌面端吸顶，滚动浏览长文时「下载」按钮始终在视野内。
 */
export default function ModDetailPage() {
  const [searchParams] = useSearchParams();
  const id = (searchParams.get('id') ?? '').trim();

  const [detail, setDetail] = useState<ModDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ message: string; details: string[] } | null>(null);

  useEffect(() => {
    if (id === '') {
      setLoading(false);
      setError({ message: '缺少模组 id 参数', details: ['正确地址形如 /sharedfiles/filedetails/?id=your_mod_id'] });
      return;
    }

    const controller = new AbortController();
    setLoading(true);
    setError(null);
    setDetail(null);

    fetchModDetail(id, controller.signal)
      .then((res) => setDetail(res))
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setError(
          e instanceof ApiRequestError
            ? { message: e.message, details: e.details }
            : { message: '载入失败，请稍后重试', details: [] },
        );
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });

    return () => controller.abort();
  }, [id]);

  if (loading) {
    return (
      <div className="mx-auto max-w-[1400px] px-4 py-10 sm:px-5">
        <LoadingBlock text="正在载入模组详情…" />
      </div>
    );
  }

  if (error || !detail) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-10">
        <ErrorState message={error?.message ?? '模组不存在'} details={error?.details} />
        <div className="mt-4 text-center">
          <Link to="/browse" className={buttonClass('outline', 'md')}>
            返回工坊首页
          </Link>
        </div>
      </div>
    );
  }

  const latest = detail.versions.find((v) => v.version === detail.latestVersion) ?? detail.versions[0] ?? null;
  const marketUrl = `${window.location.origin}/market/mod_list.txt`;

  return (
    <div className="mx-auto max-w-[1400px] px-4 py-6 sm:px-5">
      {/* 面包屑 */}
      <nav aria-label="面包屑" className="mb-3 flex items-center gap-1.5 text-xs text-paper-500">
        <Link to="/browse" className="transition-colors duration-200 hover:text-gold-300">
          创意工坊
        </Link>
        <ChevronRight aria-hidden className="h-3 w-3" />
        <Link
          to={`/browse?category=${encodeURIComponent(detail.category)}`}
          className="transition-colors duration-200 hover:text-gold-300"
        >
          {categoryLabel(detail.category)}
        </Link>
        <ChevronRight aria-hidden className="h-3 w-3" />
        <span className="truncate text-paper-400">{detail.name}</span>
      </nav>

      <header className="mb-4">
        <h1 className="font-serif text-2xl leading-snug text-paper-100 sm:text-3xl">{detail.name}</h1>
        <div className="mt-2.5 flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-paper-500">
          <span className="flex items-center gap-1.5">
            <UserRound aria-hidden className="h-3.5 w-3.5 text-gold-500" />
            {detail.author.nickname}
          </span>
          <span className="flex items-center gap-1.5">
            <Clock aria-hidden className="h-3.5 w-3.5" />
            更新于 {formatRelative(detail.updatedAt)}
          </span>
          <span className="font-mono text-paper-600">{detail.id}</span>
          <Badge variant="gold" icon={<CategoryIcon category={detail.category} className="h-3 w-3" />}>
            {categoryLabel(detail.category)}
          </Badge>
          {detail.status !== 'approved' ? <Badge variant="cinnabar">未上架</Badge> : null}
        </div>
      </header>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
        {/* ---------------- 左主栏 ---------------- */}
        <div className="min-w-0 space-y-5">
          <div className="panel overflow-hidden">
            <PosterImage
              src={detail.posterUrl || null}
              alt={`${detail.name} 的封面`}
              category={detail.category}
              className="aspect-video w-full"
            />
          </div>

          <section className="panel p-5">
            <SectionTitle>模组介绍</SectionTitle>
            <MiniMarkdown text={detail.description} />
          </section>

          {latest && latest.changelog.trim() !== '' ? (
            <section className="panel p-5">
              <SectionTitle action={<span className="font-mono text-xs text-gold-300">{latest.version}</span>}>
                变更日志
              </SectionTitle>
              <MiniMarkdown text={latest.changelog} />
            </section>
          ) : null}

          <section className="panel p-5">
            <SectionTitle>版本历史</SectionTitle>
            <ul className="divide-y divide-ink-700/70">
              {detail.versions.map((v) => (
                <li key={v.version} className="flex flex-wrap items-center gap-x-3 gap-y-2 py-3">
                  <span className="font-mono text-sm text-gold-300">{v.version}</span>
                  {v.version === detail.latestVersion ? <Badge variant="bamboo">最新</Badge> : null}
                  <span className="text-xs text-paper-500">{formatDate(v.createdAt)}</span>
                  <span className="text-xs text-paper-500">{formatBytes(v.size)}</span>
                  <span className="hidden text-xs text-paper-600 sm:inline">
                    {formatCount(v.downloads)} 次下载
                  </span>
                  <a
                    className={buttonClass('outline', 'sm', 'ml-auto')}
                    href={`/mods/${encodeURIComponent(detail.id)}@${encodeURIComponent(v.version)}.zip`}
                  >
                    <Download aria-hidden className="h-3.5 w-3.5" />
                    下载此版本
                  </a>
                </li>
              ))}
            </ul>
          </section>

          {/* 留言：放在主栏最下方，玩家看完介绍与版本历史后顺手反馈问题 */}
          <CommentSection modId={detail.id} />
        </div>

        {/* ---------------- 右信息栏 ---------------- */}
        <aside className="space-y-4 lg:sticky lg:top-24 lg:self-start">
          {/* 主操作 */}
          <div className="panel space-y-3 p-4">
            {latest ? (
              <a
                className={buttonClass('gold', 'lg', 'w-full')}
                href={`/api/mods/${encodeURIComponent(detail.id)}/download`}
              >
                <Download aria-hidden className="h-4 w-4" />
                下载模组 {latest.version}
              </a>
            ) : (
              <p className="text-sm text-paper-400">该模组还没有可下载的版本</p>
            )}

            <p className="text-[11px] leading-relaxed text-paper-500">
              网页下载得到的是标准模组包，解压后放入游戏目录 <code className="font-mono text-paper-400">Mods/</code>
              即可。想要自动更新，请在游戏内订阅（见下方市场地址）。
            </p>
          </div>

          {/* 统计 */}
          <div className="panel p-4">
            <SectionTitle>统计</SectionTitle>
            <MetaRow label="订阅者">
              <span className="flex items-center gap-1.5">
                <Users aria-hidden className="h-3.5 w-3.5 text-gold-500" />
                {formatCount(detail.stats.subscribers)}
              </span>
            </MetaRow>
            <MetaRow label="好评">
              <span className="flex items-center gap-1.5">
                <Heart aria-hidden className="h-3.5 w-3.5 text-cinnabar-400" />
                {formatCount(detail.stats.likes)}
              </span>
            </MetaRow>
            <MetaRow label="浏览">
              <span className="flex items-center gap-1.5">
                <Eye aria-hidden className="h-3.5 w-3.5 text-paper-500" />
                {formatCount(detail.stats.views)}
              </span>
            </MetaRow>
            <MetaRow label="下载">
              <span className="flex items-center gap-1.5">
                <Download aria-hidden className="h-3.5 w-3.5 text-paper-500" />
                {formatCount(detail.stats.downloads)}
              </span>
            </MetaRow>
          </div>

          {/* 基本信息 */}
          <div className="panel p-4">
            <SectionTitle>基本信息</SectionTitle>
            <MetaRow label="模组 ID">
              <span className="flex items-center gap-2">
                <span className="font-mono text-paper-300">{detail.id}</span>
                <CopyButton value={detail.id} />
              </span>
            </MetaRow>
            <MetaRow label="大小">{latest ? formatBytes(latest.size) : '—'}</MetaRow>
            <MetaRow label="发布于">{formatDate(detail.createdAt)}</MetaRow>
            <MetaRow label="更新于">{formatDate(detail.updatedAt)}</MetaRow>
            <MetaRow label="需要游戏">
              <span className="font-mono">{detail.gameVersion === '*' ? '任意版本' : detail.gameVersion}</span>
            </MetaRow>
            {detail.tags.length > 0 ? (
              <div className="mt-3">
                <p className="mb-1.5 text-xs text-paper-500">标签</p>
                <div className="flex flex-wrap gap-1.5">
                  {detail.tags.map((tag) => (
                    <Link key={tag} to={`/browse?tag=${encodeURIComponent(tag)}`}>
                      <Badge variant="outline">#{tag}</Badge>
                    </Link>
                  ))}
                </div>
              </div>
            ) : null}
          </div>

          {/* Required Items */}
          {detail.requiredItems.length > 0 ? (
            <div className="panel p-4">
              <SectionTitle>前置模组</SectionTitle>
              <p className="mb-2 text-xs text-paper-500">
                作者声明需要先安装以下模组（填的是模组 ID），否则可能无法正常生效。点击可查看对应模组。
              </p>
              <ul className="space-y-1.5">
                {detail.requiredItems.map((dep) => (
                  <li key={dep}>
                    <Link
                      to={modDetailPath(dep)}
                      className="flex items-center gap-2 text-sm text-gold-300 transition-colors duration-200 hover:text-gold-200"
                    >
                      <PackageCheck aria-hidden className="h-3.5 w-3.5" />
                      <span className="font-mono">{dep}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          {/* 游戏内订阅 */}
          <div className="panel p-4">
            <SectionTitle>游戏内订阅</SectionTitle>
            <p className="text-xs leading-relaxed text-paper-500">
              在游戏「模组管理器 → 在线市场」填入下方地址，即可在游戏内浏览、下载并检查全部模组更新。
            </p>
            <div className="mt-2.5 flex gap-2">
              <input
                readOnly
                value={marketUrl}
                aria-label="市场地址"
                onFocus={(e) => e.currentTarget.select()}
                className="min-w-0 flex-1 rounded-md border border-ink-500/80 bg-ink-900/70 px-2.5 py-1.5 font-mono text-[11px] text-paper-300 focus:border-gold-500 focus:outline-none"
              />
              <CopyButton value={marketUrl} label="复制" />
            </div>
            <a
              href="/market/mod_list.txt"
              className="mt-2.5 inline-flex items-center gap-1.5 text-xs text-paper-500 transition-colors duration-200 hover:text-gold-300"
            >
              <Link2 aria-hidden className="h-3.5 w-3.5" />
              查看原始市场清单（mod_list.txt）
            </a>
          </div>
        </aside>
      </div>
    </div>
  );
}
