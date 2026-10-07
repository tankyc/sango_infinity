import { useEffect, useState, type DragEvent } from 'react';
import { FileArchive, FolderOpen, Loader2, Rocket, X } from 'lucide-react';
import { ApiRequestError, publishModVersion, type MyModRow, type PublishResponse } from '../../lib/api';
import { compareVersion, formatBytes } from '../../lib/format';
import { collectDroppedEntries, filterUsable, zipFolder, type FolderFile } from '../../lib/modFolder';
import { Button, buttonClass } from '../ui/Button';
import { Field, Input, Textarea } from '../ui/Field';
import { Modal } from '../ui/Modal';
import { ErrorState } from '../ui/Feedback';
import { cn } from '../../lib/cn';

/**
 * 发布新版本
 *
 * 与「发布新模组」的关键差别：zip 不能随便传 —— 包内 mod.info 的 id 必须与目标模组一致，
 * 服务端会校验，不一致直接拒收（否则等于往别的模组里塞东西）。
 * 因此这里必须把这条规则明确写出来，避免用户拿错包反复重试。
 */
const MAX_ZIP_BYTES = 200 * 1024 * 1024;

export interface PublishVersionDialogProps {
  mod: MyModRow | null;
  onClose: () => void;
  onPublished: (message: string) => void;
}

export function PublishVersionDialog({ mod, onClose, onPublished }: PublishVersionDialogProps) {
  const [zip, setZip] = useState<File | null>(null);
  /** 直接选文件夹时保留原始文件，提交时才打包（避免选完就长时间占着内存） */
  const [folderFiles, setFolderFiles] = useState<FolderFile[] | null>(null);
  const [packing, setPacking] = useState(false);
  const [packProgress, setPackProgress] = useState(0);
  const [dragging, setDragging] = useState(false);
  const [version, setVersion] = useState('');
  const [changelog, setChangelog] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<{ message: string; details: string[] } | null>(null);

  useEffect(() => {
    if (!mod) return;
    setZip(null);
    setFolderFiles(null);
    setPacking(false);
    setPackProgress(0);
    setDragging(false);
    setVersion('');
    setChangelog('');
    setProgress(0);
    setError(null);
  }, [mod]);

  function acceptZip(file: File | undefined) {
    if (!file) return;
    if (!file.name.toLowerCase().endsWith('.zip')) {
      setError({ message: '只接受 .zip 压缩包', details: [] });
      return;
    }
    if (file.size > MAX_ZIP_BYTES) {
      setError({ message: `压缩包为 ${formatBytes(file.size)}，超过上限 ${formatBytes(MAX_ZIP_BYTES)}`, details: [] });
      return;
    }
    setError(null);
    setZip(file);
    setFolderFiles(null);
  }

  /**
   * 直接选择（或拖入）整个文件夹。
   *
   * 与「发布新模组」保持完全一致：这里只记录文件，等点发布时再打包。
   * 选完就打会让浏览器长时间占着几百 MB 内存，而作者往往只是想先确认选对没有。
   */
  function acceptFolder(files: FolderFile[]) {
    const usable = filterUsable(files);
    if (usable.length === 0) {
      setError({ message: '这个文件夹里没有可用文件（可能只包含 .git 或系统文件）', details: [] });
      return;
    }
    const total = usable.reduce((sum, f) => sum + f.file.size, 0);
    if (total > MAX_ZIP_BYTES) {
      setError({
        message: `文件夹共 ${formatBytes(total)}，打包后还会更大，已超过上限 ${formatBytes(MAX_ZIP_BYTES)}`,
        details: ['请先精简内容，或自行压缩成 zip 后再上传。'],
      });
      return;
    }
    setError(null);
    setFolderFiles(usable);
    setZip(null);
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

    // 其余情况按文件夹处理。拖入目录时 dataTransfer.files 里只有那个目录项，
    // 拿不到里面的文件，必须用 webkitGetAsEntry 递归读取
    const entries = await collectDroppedEntries(e.dataTransfer);
    acceptFolder(entries);
  }

  /** 清空已选内容 */
  function clearSelection() {
    setZip(null);
    setFolderFiles(null);
  }

  async function handleSubmit() {
    if (!mod) return;
    setError(null);

    if (!zip && !folderFiles) {
      setError({ message: '请选择要发布的模组包（zip），或直接选择模组文件夹', details: [] });
      return;
    }
    if (version.trim() !== '' && mod.version && compareVersion(version.trim(), mod.version) <= 0) {
      setError({
        message: `新版本号需高于当前最新版本 ${mod.version}`,
        details: ['低于或等于当前版本号时，订阅者不会收到更新提示。'],
      });
      return;
    }

    setSubmitting(true);
    setProgress(0);
    try {
      // 选的是文件夹：先在浏览器里打成标准 zip，之后走与上传 zip 完全相同的入库流水线。
      // 包内缺 mod.info 时用模组现有信息生成一个，并带上目标 id，
      // 这样「只有一个 Data 目录」的文件夹也能直接发新版本。
      let zipToUpload = zip;
      if (!zipToUpload && folderFiles) {
        setPacking(true);
        setPackProgress(0);
        zipToUpload = await zipFolder(folderFiles, {
          generateInfo: {
            id: mod.id,
            name: mod.name,
            version: version.trim() || mod.version || '1.0',
          },
          onProgress: setPackProgress,
        });
        setPacking(false);
      }
      if (!zipToUpload) {
        // 正常走不到这里（上面已校验过），保留显式分支让类型收敛
        setError({ message: '请选择要发布的模组包（zip），或直接选择模组文件夹', details: [] });
        return;
      }

      const res: PublishResponse = await publishModVersion(
        { id: mod.id, zip: zipToUpload, version: version.trim(), changelog },
        setProgress,
      );
      onPublished(`「${mod.name}」已发布新版本 ${res.version.version}`);
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiRequestError
          ? { message: e.message, details: e.details }
          : { message: '发布失败，请稍后重试', details: [] },
      );
    } finally {
      setSubmitting(false);
      setPacking(false);
    }
  }

  return (
    <Modal
      open={mod !== null}
      title={`为「${mod?.name ?? ''}」发布新版本`}
      description={`当前最新版本：${mod?.version ?? '—'}`}
      busy={submitting}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={submitting}>
            取消
          </Button>
          <Button
            variant="gold"
            loading={submitting}
            onClick={() => void handleSubmit()}
            icon={<Rocket aria-hidden className="h-4 w-4" />}
          >
            发布新版本
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="rounded-md border border-gold-700/50 bg-gold-500/5 p-3 text-xs leading-relaxed text-paper-400">
          这个新版本会被并入模组 <code className="font-mono text-gold-300">{mod?.id}</code>。
          包内 <code className="mx-1 font-mono text-gold-300">mod.info</code> 的
          <code className="mx-1 font-mono text-gold-300">id</code>
          与目标不一致时，服务端会按目标模组重写并给出提示（不会因此拒收）。
          <span className="mt-1 block text-paper-500">
            也可以直接选择模组文件夹，浏览器会自动打成标准 zip；包内缺
            <code className="mx-1 font-mono text-gold-300">mod.info</code>
            时，会用下面的版本号与站点上的名称自动生成一个。
          </span>
          <span className="mt-1 block text-paper-500">
            发布后，服务端会把这里的版本号、作者（你的账号名）与站点上的名称/简介写回包内 mod.info；
            <code className="mx-1 font-mono text-gold-300">poster</code>
            则写入该模组当前封面的访问地址（换封面请在「编辑信息」里改，下次发版本会同步）。
          </span>
        </div>

        <Field label="模组包" required hint={`单个压缩包上限 ${formatBytes(MAX_ZIP_BYTES)}`}>
          <div
            onDragOver={(e) => {
              e.preventDefault();
              setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={handleDrop}
            className={cn(
              'rounded-xl border-2 border-dashed p-4 text-center transition-colors duration-200',
              dragging ? 'border-gold-500 bg-gold-500/10' : 'border-ink-500/80 hover:border-gold-700/70',
            )}
          >
            <input
              id="ver-zip"
              type="file"
              accept=".zip"
              className="hidden"
              onChange={(e) => acceptZip(e.target.files?.[0])}
            />
            <input
              id="ver-folder"
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
                  disabled={submitting}
                  onClick={clearSelection}
                  icon={<X aria-hidden className="h-3.5 w-3.5" />}
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
                  disabled={submitting}
                  onClick={clearSelection}
                  icon={<X aria-hidden className="h-3.5 w-3.5" />}
                >
                  移除
                </Button>
              </div>
            ) : (
              <div className="flex flex-col items-center gap-3">
                <FileArchive aria-hidden className="h-7 w-7 text-paper-500" />
                <span className="text-sm text-paper-200">拖拽 zip 或整个模组文件夹到此处</span>
                <div className="flex flex-wrap items-center justify-center gap-2">
                  <label htmlFor="ver-zip" className={buttonClass('outline', 'sm')}>
                    <FileArchive aria-hidden className="h-3.5 w-3.5" />
                    选择 zip
                  </label>
                  <label htmlFor="ver-folder" className={buttonClass('gold', 'sm')}>
                    <FolderOpen aria-hidden className="h-3.5 w-3.5" />
                    选择文件夹（自动打包）
                  </label>
                </div>
                <span className="text-xs text-paper-500">
                  选文件夹时会自动忽略 .git、__MACOSX 等系统文件，包内缺 mod.info 则自动生成一个
                </span>
              </div>
            )}
          </div>

          {packing ? (
            <p className="mt-3 flex items-center gap-2 text-xs text-gold-300">
              <Loader2 aria-hidden className="h-3.5 w-3.5 animate-spin" />
              正在打包文件夹… {Math.round(packProgress * 100)}%
              <span className="text-paper-500">（大文件夹需要几秒，请勿关闭页面）</span>
            </p>
          ) : null}
        </Field>

        <Field label="版本号" htmlFor="ver-version" hint={`留空则采用包内 mod.info 的版本；需高于 ${mod?.version ?? '当前版本'}`}>
          <Input
            id="ver-version"
            value={version}
            onChange={(e) => setVersion(e.target.value)}
            placeholder="例如 1.1"
          />
        </Field>

        <Field label="变更日志" htmlFor="ver-changelog" hint="订阅者在详情页会看到这段说明">
          <Textarea
            id="ver-changelog"
            rows={5}
            value={changelog}
            onChange={(e) => setChangelog(e.target.value)}
            placeholder={'1.1\n- 修正了当阳桥事件的触发条件\n- 赵云初始兵力调整为 3000'}
          />
        </Field>

        {submitting ? (
          <div aria-live="polite" className="space-y-2">
            <div className="flex items-center justify-between text-xs text-paper-400">
              <span>正在上传…</span>
              <span className="font-mono text-gold-300">{progress}%</span>
            </div>
            <div className="h-1.5 w-full overflow-hidden rounded-full bg-ink-700">
              <div className="h-full rounded-full bg-gold-500 transition-[width] duration-200" style={{ width: `${progress}%` }} />
            </div>
          </div>
        ) : null}

        {error ? <ErrorState message={error.message} details={error.details} /> : null}
      </div>
    </Modal>
  );
}
