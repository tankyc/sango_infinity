import { useEffect, useState } from 'react';
import { Save } from 'lucide-react';
import { ApiRequestError, updateMyMod, type MyModRow } from '../../lib/api';
import { CATEGORIES } from '../../lib/catalog';
import { formatBytes } from '../../lib/format';
import { Button } from '../ui/Button';
import { Field, Input, Select, Textarea } from '../ui/Field';
import { Modal } from '../ui/Modal';
import { ErrorState } from '../ui/Feedback';

/**
 * 编辑已发布模组的信息
 *
 * 只允许改「元数据」：名称、简介、正文、分类、标签、兼容版本、封面。
 * 模组包内容本身不能改——内容变更必须走「发布新版本」，
 * 否则已订阅的玩家会拿到与版本号不符的文件，版本号也就失去意义了。
 */
const MAX_POSTER_BYTES = 5 * 1024 * 1024;
const POSTER_EXTS = ['.png', '.jpg', '.jpeg', '.webp'];

export interface EditModDialogProps {
  mod: MyModRow | null;
  onClose: () => void;
  onSaved: (message: string) => void;
}

export function EditModDialog({ mod, onClose, onSaved }: EditModDialogProps) {
  const [name, setName] = useState('');
  const [summary, setSummary] = useState('');
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState('mixed');
  const [tags, setTags] = useState('');
  const [gameVersion, setGameVersion] = useState('');
  const [poster, setPoster] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<{ message: string; details: string[] } | null>(null);

  // 打开时用该模组的当前值初始化表单
  useEffect(() => {
    if (!mod) return;
    setName(mod.name);
    setSummary(mod.summary);
    setDescription(mod.description);
    setCategory(mod.category);
    setTags(mod.tags.join(','));
    setGameVersion(mod.gameVersion === '*' ? '' : mod.gameVersion);
    setPoster(null);
    setError(null);
  }, [mod]);

  async function handleSubmit() {
    if (!mod) return;
    setError(null);

    if (name.trim() === '') {
      setError({ message: '模组名称不能为空', details: [] });
      return;
    }

    setSubmitting(true);
    try {
      await updateMyMod(mod.id, {
        name: name.trim(),
        summary: summary.trim(),
        description,
        category,
        tags,
        gameVersion: gameVersion.trim(),
        poster,
      });
      onSaved(`「${name.trim()}」的信息已更新`);
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiRequestError
          ? { message: e.message, details: e.details }
          : { message: '保存失败，请稍后重试', details: [] },
      );
    } finally {
      setSubmitting(false);
    }
  }

  function acceptPoster(file: File | undefined) {
    if (!file) return;
    const lower = file.name.toLowerCase();
    if (!POSTER_EXTS.some((ext) => lower.endsWith(ext))) {
      setError({ message: `封面仅支持 ${POSTER_EXTS.join(' / ')}`, details: [] });
      return;
    }
    if (file.size > MAX_POSTER_BYTES) {
      setError({ message: `封面为 ${formatBytes(file.size)}，超过上限 ${formatBytes(MAX_POSTER_BYTES)}`, details: [] });
      return;
    }
    setError(null);
    setPoster(file);
  }

  return (
    <Modal
      open={mod !== null}
      title={`编辑「${mod?.name ?? ''}」`}
      description="模组包内容无法在此修改；内容有变化请用「发布新版本」，否则订阅者会拿到与版本号不符的文件。"
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
            icon={<Save aria-hidden className="h-4 w-4" />}
          >
            保存修改
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="模组名称" htmlFor="edit-name" required>
          <Input id="edit-name" value={name} onChange={(e) => setName(e.target.value)} />
        </Field>

        <Field label="一句话简介" htmlFor="edit-summary" hint="显示在列表卡片上，纯文本，建议 60 字以内">
          <Input id="edit-summary" value={summary} onChange={(e) => setSummary(e.target.value)} />
        </Field>

        <Field
          label="正文介绍"
          htmlFor="edit-desc"
          hint="支持 Markdown 子集：标题、列表、引用、**粗体**、`代码`、[链接](url)"
        >
          <Textarea id="edit-desc" rows={8} value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="类型" htmlFor="edit-category">
            <Select id="edit-category" value={category} onChange={(e) => setCategory(e.target.value)}>
              {CATEGORIES.map((c) => (
                <option key={c.key} value={c.key}>
                  {c.label}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="兼容游戏版本" htmlFor="edit-gamever" hint="留空表示任意版本">
            <Input
              id="edit-gamever"
              value={gameVersion}
              onChange={(e) => setGameVersion(e.target.value)}
              placeholder="1.2"
            />
          </Field>
        </div>

        <Field label="标签" htmlFor="edit-tags" hint="逗号分隔，最多 8 个，用于筛选栏">
          <Input id="edit-tags" value={tags} onChange={(e) => setTags(e.target.value)} placeholder="武将,三国,历史" />
        </Field>

        <Field
          label="更换封面"
          htmlFor="edit-poster"
          hint="不选则沿用当前封面；建议 16:9。包内 mod.info 的 poster 会在下次「发布新版本」时同步为新封面地址"
        >
          <div className="flex flex-wrap items-center gap-3">
            <input
              id="edit-poster"
              type="file"
              accept={POSTER_EXTS.join(',')}
              className="hidden"
              onChange={(e) => acceptPoster(e.target.files?.[0])}
            />
            <label
              htmlFor="edit-poster"
              className="inline-flex h-9 cursor-pointer items-center gap-2 rounded-md border border-gold-700/70 px-3 text-sm text-gold-300 transition-colors duration-200 hover:border-gold-500 hover:bg-gold-500/10"
            >
              选择图片
            </label>
            {poster ? (
              <>
                <span className="font-mono text-xs text-paper-300">{poster.name}</span>
                <Button variant="ghost" size="sm" onClick={() => setPoster(null)}>
                  移除
                </Button>
              </>
            ) : (
              <span className="text-xs text-paper-500">当前封面将保持不变</span>
            )}
          </div>
        </Field>

        {error ? <ErrorState message={error.message} details={error.details} /> : null}
      </div>
    </Modal>
  );
}
