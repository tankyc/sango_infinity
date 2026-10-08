import { useState, type DragEvent, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import {
  CheckCircle2,
  ExternalLink,
  FileArchive,
  FolderOpen,
  ImagePlus,
  Loader2,
  Rocket,
  Upload,
  X,
} from 'lucide-react';
import { ApiRequestError, publishMod, type PublishResponse } from '../lib/api';
import { CATEGORIES } from '../lib/catalog';
import { formatBytes } from '../lib/format';
import { collectDroppedEntries, filterUsable, zipFolder, type FolderFile } from '../lib/modFolder';
import { useAuth } from '../hooks/useAuth';
import { Badge, SectionTitle } from '../components/ui/Badge';
import { Button, buttonClass } from '../components/ui/Button';
import { Field, Input, Select, Textarea } from '../components/ui/Field';
import { CopyButton } from '../components/ui/CopyButton';
import { ErrorState, LoadingBlock } from '../components/ui/Feedback';
import { cn } from '../lib/cn';

/**
 * 发布模组 /upload（对应 建设计划.md §9.1 游戏内一键发布 · 网页入口）
 *
 * 表单字段与游戏内 WWWForm 提交的字段名完全一致，
 * 因此网页与游戏内两条发布路径最终走的是同一个入库流水线（含安全扫描、顶层目录规整）。
 *
 * 前端只做「能立刻给出反馈」的校验（扩展名、体积上限），
 * 真正的校验（mod.info、zip 结构、安全扫描）一律交给服务端，避免两边规则打架。
 */

const MAX_ZIP_BYTES = 200 * 1024 * 1024;
const MAX_POSTER_BYTES = 5 * 1024 * 1024;
const POSTER_EXTS = ['.png', '.jpg', '.jpeg', '.webp'];

interface FormState {
  name: string;
  summary: string;
  description: string;
  category: string;
  tags: string;
  depends: string;
  gameVersion: string;
  version: string;
  changelog: string;
}

const EMPTY_FORM: FormState = {
  name: '',
  summary: '',
  description: '',
  category: 'mixed',
  tags: '',
  depends: '',
  gameVersion: '',
  version: '',
  changelog: '',
};

export default function UploadPage() {
  const { user, initializing } = useAuth();

  const [form, setForm] = useState<FormState>(EMPTY_FORM);
  const [zip, setZip] = useState<File | null>(null);
  /** 直接选文件夹时保留原始文件，提交时才打包（避免选完就长时间占着内存） */
  const [folderFiles, setFolderFiles] = useState<FolderFile[] | null>(null);
  const [packing, setPacking] = useState(false);
  const [packProgress, setPackProgress] = useState(0);
  const [poster, setPoster] = useState<File | null>(null);
  const [fileError, setFileError] = useState('');
  const [dragging, setDragging] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<{ message: string; details: string[] } | null>(null);
  const [result, setResult] = useState<PublishResponse | null>(null);

  function update<K extends keyof FormState>(key: K, value: string) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  function acceptZip(file: File | undefined) {
    if (!file) return;
    if (!file.name.toLowerCase().endsWith('.zip')) {
      setFileError('只接受 .zip 压缩包（游戏只能识别 zip 模组包）');
      return;
    }
    if (file.size > MAX_ZIP_BYTES) {
      setFileError(`压缩包为 ${formatBytes(file.size)}，超过上限 ${formatBytes(MAX_ZIP_BYTES)}`);
      return;
    }
    setFileError('');
    setZip(file);
    setFolderFiles(null);
  }

  /**
   * 直接选择（或拖入）整个文件夹
   *
   * 这里只记录文件，等点发布时再打包：选完就打包会让浏览器长时间占着几百 MB 内存，
   * 而作者很可能只是想先看看自己选对没有。
   */
  function acceptFolder(files: FolderFile[]) {
    const usable = filterUsable(files);
    if (usable.length === 0) {
      setFileError('这个文件夹里没有可用文件（可能只包含 .git 或系统文件）');
      return;
    }
    const total = usable.reduce((sum, f) => sum + f.file.size, 0);
    if (total > MAX_ZIP_BYTES) {
      setFileError(
        `文件夹共 ${formatBytes(total)}，打包后还会更大，已超过上限 ${formatBytes(MAX_ZIP_BYTES)}；请先精简，或自行压缩成 zip 后上传`,
      );
      return;
    }
    setFileError('');
    setFolderFiles(usable);
    setZip(null);
  }

  function acceptPoster(file: File | undefined) {
    if (!file) return;
    const lower = file.name.toLowerCase();
    if (!POSTER_EXTS.some((ext) => lower.endsWith(ext))) {
      setFileError(`封面仅支持 ${POSTER_EXTS.join(' / ')}`);
      return;
    }
    if (file.size > MAX_POSTER_BYTES) {
      setFileError(`封面为 ${formatBytes(file.size)}，超过上限 ${formatBytes(MAX_POSTER_BYTES)}`);
      return;
    }
    setFileError('');
    setPoster(file);
  }

  async function handleDrop(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setDragging(false);

    const dropped = Array.from(e.dataTransfer.files ?? []);
    // 单个 zip：沿用原来的直接上传方式
    if (dropped.length === 1 && dropped[0].name.toLowerCase().endsWith('.zip')) {
      acceptZip(dropped[0]);
      return;
    }

    // 其余情况按文件夹处理。注意：拖入目录时 dataTransfer.files 里只有那个目录项，
    // 拿不到里面的文件，必须用 webkitGetAsEntry 递归读取
    const entries = await collectDroppedEntries(e.dataTransfer);
    acceptFolder(entries);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setResult(null);

    if (!zip && !folderFiles) {
      setError({ message: '请先选择模组 zip，或直接选择模组文件夹', details: [] });
      return;
    }
    if (form.name.trim() === '') {
      setError({ message: '请填写模组名称', details: [] });
      return;
    }

    setSubmitting(true);
    setProgress(0);
    try {
      // 选的是文件夹：先在浏览器里打成标准 zip（包内缺 mod.info 时自动生成一个），
      // 之后走的是与上传 zip 完全相同的入库流水线
      let zipToUpload = zip;
      if (!zipToUpload && folderFiles) {
        setPacking(true);
        setPackProgress(0);
        zipToUpload = await zipFolder(folderFiles, {
          generateInfo: {
            name: form.name.trim(),
            description: form.summary.trim(),
            version: form.version.trim() || '1.0',
          },
          onProgress: setPackProgress,
        });
        setZip(zipToUpload);
        setPacking(false);
      }
      if (!zipToUpload) {
        // 正常走不到这里（上面已校验过），保留显式分支让类型收敛
        setError({ message: '请先选择模组 zip，或直接选择模组文件夹', details: [] });
        return;
      }

      const res = await publishMod(
        {
          zip: zipToUpload,
          poster,
          name: form.name.trim(),
          summary: form.summary.trim(),
          description: form.description,
          category: form.category,
          tags: form.tags,
          depends: form.depends,
          gameVersion: form.gameVersion.trim(),
          version: form.version.trim(),
          changelog: form.changelog,
        },
        setProgress,
      );
      setResult(res);
      setZip(null);
      update('changelog', '');
    } catch (err) {
      setError(
        err instanceof ApiRequestError
          ? { message: err.message, details: err.details }
          : { message: '发布失败，请稍后重试', details: [] },
      );
    } finally {
      setSubmitting(false);
      setPacking(false);
    }
  }

  if (initializing) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-16">
        <LoadingBlock text="正在确认登录状态…" />
      </div>
    );
  }

  if (!user) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-16">
        <div className="panel paper-grain p-8 text-center">
          <h1 className="font-serif text-2xl text-paper-100">发布模组需要先登录</h1>
          <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-paper-500">
            ModHub 与游戏内云存档共用账号：登录后即可发布模组、管理版本，并在游戏内直接订阅下载。
          </p>
          <Link to="/login" className={buttonClass('gold', 'lg', 'mt-5')}>
            <Upload aria-hidden className="h-4 w-4" />
            去登录 / 注册
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl px-4 py-6 sm:px-5">
      <header className="mb-5">
        <h1 className="font-serif text-2xl text-paper-100 sm:text-3xl">发布模组到创意工坊</h1>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-paper-400">
          上传你打包好的模组 zip 并填写信息即可发布。压缩包内必须有
          <code className="mx-1 font-mono text-gold-300">mod.info</code>
          且位于顶层 <code className="mx-1 font-mono text-gold-300">{'{id}/'}</code>
          目录下；目录名与 id 不一致时服务端会自动补齐。
        </p>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-paper-500">
          发布时，你在下方填写的<strong className="text-paper-300">名称、简介、版本号与作者（取你的账号名）</strong>
          会被写入包内的 <code className="font-mono text-gold-300">mod.info</code>；
          上传的<strong className="text-paper-300">封面</strong>则会以它的访问地址写入
          <code className="mx-1 font-mono text-gold-300">poster</code> 字段。
          因此游戏里读到的信息与网页上显示的一致——包内原有的这几项无需手动维护。
        </p>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-paper-500">
          模组 ID 由系统在发布时自动分配（8 位短 ID，全局唯一），你
          <strong className="text-paper-300">不需要也不应该自己编</strong>；包内原有的
          <code className="mx-1 font-mono text-gold-300">id</code>
          会被覆盖成系统分配的 ID。别人要引用你的模组作为前置依赖时，填的就是这个 ID。
        </p>
      </header>

      {result ? (
        <div className="panel mb-5 space-y-3 border-bamboo-400/50 p-5">
          <div className="flex items-center gap-2">
            <CheckCircle2 aria-hidden className="h-5 w-5 text-bamboo-400" />
            <h2 className="font-serif text-lg text-paper-100">
              发布成功：{result.mod.name} <span className="font-mono text-gold-300">{result.version.version}</span>
            </h2>
          </div>
          <div className="flex flex-wrap gap-2">
            <Badge variant={result.mod.status === 'approved' ? 'bamboo' : 'gold'}>
              {result.mod.status === 'approved' ? '已上架' : '待审核'}
            </Badge>
            <Badge variant="outline">{formatBytes(result.version.size)}</Badge>
          </div>
          <div className="flex flex-wrap items-center gap-2 rounded-md border border-ink-500/80 bg-ink-900/50 px-3 py-2">
            <span className="text-xs text-paper-500">本模组 ID</span>
            <span className="font-mono text-sm text-gold-300">{result.mod.id}</span>
            <CopyButton value={result.mod.id} label="复制" />
            <span className="text-xs text-paper-500">— 别人把它填进「前置模组」即可引用你这个模组</span>
          </div>
          {result.warnings.length > 0 ? (
            <div className="rounded-md border border-gold-700/50 bg-gold-500/5 p-3">
              <p className="mb-1 text-xs text-gold-300">服务端的自动修正与提示：</p>
              <ul className="list-inside list-disc space-y-0.5 text-xs text-paper-400">
                {result.warnings.map((w) => (
                  <li key={w}>{w}</li>
                ))}
              </ul>
            </div>
          ) : null}
          <div className="flex flex-wrap gap-2 pt-1">
            <Link
              to={`/sharedfiles/filedetails/?id=${encodeURIComponent(result.mod.id)}`}
              className={buttonClass('gold', 'md')}
            >
              查看模组详情页
            </Link>
            <a className={buttonClass('outline', 'md')} href={result.downloadUrl}>
              <ExternalLink aria-hidden className="h-4 w-4" />
              下载地址
            </a>
          </div>
        </div>
      ) : null}

      <form onSubmit={handleSubmit} className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_320px]">
        {/* 左：文件与正文 */}
        <div className="min-w-0 space-y-5">
          <section className="panel p-5">
            <SectionTitle>模组包</SectionTitle>

            <div
              onDragOver={(e) => {
                e.preventDefault();
                setDragging(true);
              }}
              onDragLeave={() => setDragging(false)}
              onDrop={handleDrop}
              className={cn(
                'rounded-xl border-2 border-dashed p-6 text-center transition-colors duration-200',
                dragging ? 'border-gold-500 bg-gold-500/10' : 'border-ink-500/80 hover:border-gold-700/70',
              )}
            >
              <input
                id="mod-zip"
                type="file"
                accept=".zip"
                className="hidden"
                onChange={(e) => acceptZip(e.target.files?.[0])}
              />
              <input
                id="mod-folder"
                type="file"
                className="hidden"
                multiple
                // webkitdirectory 会让浏览器选择整个文件夹，并把相对路径放进 webkitRelativePath；
                // TS 的类型里没有这个非标准属性，所以用 spread 绕过去
                {...({ webkitdirectory: '' } as Record<string, string>)}
                onChange={(e) => {
                  const picked = Array.from(e.target.files ?? []).map((file) => ({
                    file,
                    path: file.webkitRelativePath || file.name,
                  }));
                  acceptFolder(picked);
                  // 清空 value，否则再次选同一个文件夹不会触发 change
                  e.target.value = '';
                }}
              />

              {zip ? (
                <div className="flex flex-wrap items-center justify-center gap-3">
                  <FileArchive aria-hidden className="h-5 w-5 text-gold-400" />
                  <span className="font-mono text-sm text-paper-200">{zip.name}</span>
                  <span className="text-xs text-paper-500">{formatBytes(zip.size)}</span>
                  <Button
                    variant="ghost"
                    size="sm"
                    icon={<X aria-hidden className="h-3.5 w-3.5" />}
                    onClick={() => setZip(null)}
                  >
                    移除
                  </Button>
                </div>
              ) : folderFiles ? (
                <div className="flex flex-wrap items-center justify-center gap-3">
                  <FolderOpen aria-hidden className="h-5 w-5 text-gold-400" />
                  <span className="font-mono text-sm text-paper-200">
                    {folderFiles[0].path.split('/')[0] || '文件夹'}
                  </span>
                  <span className="text-xs text-paper-500">
                    {folderFiles.length} 个文件 ·{' '}
                    {formatBytes(folderFiles.reduce((sum, f) => sum + f.file.size, 0))}
                  </span>
                  <span className="text-xs text-bamboo-400">发布时自动打包</span>
                  <Button
                    variant="ghost"
                    size="sm"
                    icon={<X aria-hidden className="h-3.5 w-3.5" />}
                    onClick={() => setFolderFiles(null)}
                  >
                    移除
                  </Button>
                </div>
              ) : (
                <div className="flex flex-col items-center gap-3">
                  <FileArchive aria-hidden className="h-8 w-8 text-paper-500" />
                  <span className="text-sm text-paper-200">拖拽 zip 或整个模组文件夹到此处</span>
                  <div className="flex flex-wrap items-center justify-center gap-2">
                    <label htmlFor="mod-zip" className={buttonClass('outline', 'sm')}>
                      <FileArchive aria-hidden className="h-3.5 w-3.5" />
                      选择 zip
                    </label>
                    <label htmlFor="mod-folder" className={buttonClass('gold', 'sm')}>
                      <FolderOpen aria-hidden className="h-3.5 w-3.5" />
                      选择文件夹（自动打包）
                    </label>
                  </div>
                  <span className="text-xs text-paper-500">
                    选文件夹时会自动忽略 .git、__MACOSX 等系统文件，包内缺 mod.info 则用下方填写的信息生成一个；
                    上限 {formatBytes(MAX_ZIP_BYTES)}
                  </span>
                </div>
              )}
            </div>

            <div className="mt-4">
              <input
                id="mod-poster"
                type="file"
                accept={POSTER_EXTS.join(',')}
                className="hidden"
                onChange={(e) => acceptPoster(e.target.files?.[0])}
              />
              <div className="flex flex-wrap items-center gap-3">
                <label htmlFor="mod-poster" className={buttonClass('outline', 'sm')}>
                  <ImagePlus aria-hidden className="h-3.5 w-3.5" />
                  选择封面图
                </label>
                {poster ? (
                  <>
                    <span className="font-mono text-xs text-paper-300">{poster.name}</span>
                    <Button variant="ghost" size="sm" onClick={() => setPoster(null)}>
                      移除
                    </Button>
                  </>
                ) : (
                  <span className="text-xs text-paper-500">
                    建议 16:9（如 960×360），上限 {formatBytes(MAX_POSTER_BYTES)}，不填则卡片显示分类占位图
                  </span>
                )}
              </div>
            </div>

            {packing ? (
              <p className="mt-3 flex items-center gap-2 text-xs text-gold-300">
                <Loader2 aria-hidden className="h-3.5 w-3.5 animate-spin" />
                正在打包文件夹… {Math.round(packProgress * 100)}%
                <span className="text-paper-500">（大文件夹需要几秒，请勿关闭页面）</span>
              </p>
            ) : null}
            {fileError ? <p className="mt-3 text-xs text-cinnabar-400">{fileError}</p> : null}
          </section>

          <section className="panel space-y-4 p-5">
            <SectionTitle>内容介绍</SectionTitle>

            <Field label="模组名称" htmlFor="mod-name" required hint="显示在卡片与详情页标题上的名字">
              <Input
                id="mod-name"
                value={form.name}
                onChange={(e) => update('name', e.target.value)}
                placeholder="例如：我的自建武将包"
              />
            </Field>

            <Field label="一句话简介" htmlFor="mod-summary" hint="列表卡片显示两行，建议 60 字以内">
              <Input
                id="mod-summary"
                value={form.summary}
                onChange={(e) => update('summary', e.target.value)}
                placeholder="例如：200 名自建武将，含 12 个剧本"
              />
            </Field>

            <Field
              label="正文介绍"
              htmlFor="mod-description"
              hint="支持 Markdown 子集：标题、列表、引用、**粗体**、`代码`、[链接](url)"
            >
              <Textarea
                id="mod-description"
                rows={8}
                value={form.description}
                onChange={(e) => update('description', e.target.value)}
                placeholder={'# 内容说明\n\n- 新增 200 名武将\n- 调整了部分战法数值'}
              />
            </Field>

            <Field label="变更日志" htmlFor="mod-changelog" hint="本次版本改了什么，订阅者会在详情页看到">
              <Textarea
                id="mod-changelog"
                rows={4}
                value={form.changelog}
                onChange={(e) => update('changelog', e.target.value)}
                placeholder={'1.0 首发\n- 收录首批自建武将'}
              />
            </Field>
          </section>
        </div>

        {/* 右：分类与发布 */}
        <aside className="space-y-4">
          <div className="panel space-y-4 p-4">
            <SectionTitle>分类与版本</SectionTitle>

            <Field label="类型" htmlFor="mod-category">
              <Select id="mod-category" value={form.category} onChange={(e) => update('category', e.target.value)}>
                {CATEGORIES.map((c) => (
                  <option key={c.key} value={c.key}>
                    {c.label}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="标签" htmlFor="mod-tags" hint="逗号分隔，最多 8 个，供筛选栏使用">
              <Input
                id="mod-tags"
                value={form.tags}
                onChange={(e) => update('tags', e.target.value)}
                placeholder="武将,三国,历史"
              />
            </Field>

            <Field
              label="前置模组"
              htmlFor="mod-depends"
              hint="填对方的模组 ID（8 位短 ID，在对方详情页「基本信息」里可一键复制），多个用逗号或分号分隔"
            >
              <Input
                id="mod-depends"
                value={form.depends}
                onChange={(e) => update('depends', e.target.value)}
                placeholder="k7m3q9fd"
              />
            </Field>

            <Field label="兼容游戏版本" htmlFor="mod-gamever" hint="留空表示任意版本">
              <Input
                id="mod-gamever"
                value={form.gameVersion}
                onChange={(e) => update('gameVersion', e.target.value)}
                placeholder="1.2"
              />
            </Field>

            <Field label="版本号" htmlFor="mod-version" hint="留空则采用 mod.info 里的 version">
              <Input
                id="mod-version"
                value={form.version}
                onChange={(e) => update('version', e.target.value)}
                placeholder="1.0"
              />
            </Field>
          </div>

          {error ? <ErrorState message={error.message} details={error.details} /> : null}

          <div className="panel space-y-3 p-4">
            {submitting ? (
              <div aria-live="polite" className="space-y-2">
                <div className="flex items-center justify-between text-xs text-paper-400">
                  <span>正在上传…</span>
                  <span className="font-mono text-gold-300">{progress}%</span>
                </div>
                <div className="h-1.5 w-full overflow-hidden rounded-full bg-ink-700">
                  <div
                    className="h-full rounded-full bg-gold-500 transition-[width] duration-200"
                    style={{ width: `${progress}%` }}
                  />
                </div>
              </div>
            ) : null}

            <Button
              type="submit"
              variant="gold"
              size="lg"
              className="w-full"
              loading={submitting}
              icon={<Rocket aria-hidden className="h-4 w-4" />}
            >
              发布到创意工坊
            </Button>
            <p className="text-[11px] leading-relaxed text-paper-500">
              提交后服务端会执行安全扫描（可执行文件、路径穿越、zip 炸弹等），
              不符合规范的压缩包会被拒收，并按站内规则决定是否直接上架。
            </p>
          </div>
        </aside>
      </form>
    </div>
  );
}
