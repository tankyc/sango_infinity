import { useEffect, useState } from 'react';
import { FileArchive, Rocket, X } from 'lucide-react';
import { ApiRequestError, publishModVersion, type MyModRow, type PublishResponse } from '../../lib/api';
import { compareVersion, formatBytes } from '../../lib/format';
import { Button } from '../ui/Button';
import { Field, Input, Textarea } from '../ui/Field';
import { Modal } from '../ui/Modal';
import { ErrorState } from '../ui/Feedback';

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
  const [version, setVersion] = useState('');
  const [changelog, setChangelog] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<{ message: string; details: string[] } | null>(null);

  useEffect(() => {
    if (!mod) return;
    setZip(null);
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
  }

  async function handleSubmit() {
    if (!mod) return;
    setError(null);

    if (!zip) {
      setError({ message: '请选择要发布的模组包（zip）', details: [] });
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
      const res: PublishResponse = await publishModVersion(
        { id: mod.id, zip, version: version.trim(), changelog },
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
          压缩包内 <code className="font-mono text-gold-300">mod.info</code> 的
          <code className="mx-1 font-mono text-gold-300">id</code>
          必须是 <code className="font-mono text-gold-300">{mod?.id}</code>，且顶层目录名与之一致；
          否则服务端会拒收（避免把内容发到别的模组上）。
          <span className="mt-1 block text-paper-500">
            发布后，服务端会把这里的版本号、作者（你的账号名）与站点上的名称/简介写回包内 mod.info；
            <code className="mx-1 font-mono text-gold-300">poster</code>
            则写入该模组当前封面的访问地址（换封面请在「编辑信息」里改，下次发版本会同步）。
          </span>
        </div>

        <Field label="模组包" htmlFor="ver-zip" required hint={`单个压缩包上限 ${formatBytes(MAX_ZIP_BYTES)}`}>
          <div className="flex flex-wrap items-center gap-3">
            <input
              id="ver-zip"
              type="file"
              accept=".zip"
              className="hidden"
              onChange={(e) => acceptZip(e.target.files?.[0])}
            />
            <label
              htmlFor="ver-zip"
              className="inline-flex h-9 cursor-pointer items-center gap-2 rounded-md border border-gold-700/70 px-3 text-sm text-gold-300 transition-colors duration-200 hover:border-gold-500 hover:bg-gold-500/10"
            >
              <FileArchive aria-hidden className="h-3.5 w-3.5" />
              选择 zip
            </label>
            {zip ? (
              <>
                <span className="font-mono text-xs text-paper-300">{zip.name}</span>
                <span className="text-xs text-paper-500">{formatBytes(zip.size)}</span>
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={submitting}
                  onClick={() => setZip(null)}
                  icon={<X aria-hidden className="h-3.5 w-3.5" />}
                >
                  移除
                </Button>
              </>
            ) : (
              <span className="text-xs text-paper-500">尚未选择文件</span>
            )}
          </div>
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
