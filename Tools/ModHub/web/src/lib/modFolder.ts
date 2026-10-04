import { zip } from 'fflate';

/**
 * 把「选中的文件夹」在浏览器里直接打包成模组 zip
 *
 * 为什么放在前端做：服务端的整条入库流水线（安全扫描、顶层目录规整、mod.info 复写）
 * 都是围绕「一个 zip 文件」建的。前端先打成标准 zip，就能完全复用那条流水线，
 * 服务端一行都不用改；反过来让服务端接一堆原始文件，等于把流水线重写一遍。
 *
 * mod.info 的处理：包内已有就原样带上（服务端会按表单覆盖 id / 名称 / 作者 / 版本等权威字段）；
 * 没有就用表单信息生成一个 —— 这样「只有一个 Data 目录」的文件夹也能直接发布。
 */

/** 文件夹里的一个文件：file 是浏览器给的文件引用，path 是相对根目录的路径（用 / 分隔） */
export interface FolderFile {
  file: File;
  path: string;
}

/** 编辑器与系统产生的垃圾文件，没必要打进模组包 */
const IGNORED_NAMES = new Set(['.DS_Store', 'Thumbs.db', 'desktop.ini', '.gitignore', '.gitattributes']);
const IGNORED_DIR_SEGMENTS = new Set([
  '.git',
  '__MACOSX',
  '.idea',
  '.vs',
  '.vscode',
  'node_modules',
  'Library',
  'Temp',
  'obj',
]);

/** 这个路径是否应当跳过 */
export function isIgnoredPath(path: string): boolean {
  const segments = path.split('/');
  const name = segments[segments.length - 1] ?? '';
  if (IGNORED_NAMES.has(name)) return true;
  return segments.slice(0, -1).some((seg) => IGNORED_DIR_SEGMENTS.has(seg));
}

/** 过滤掉垃圾文件，保留可用于打包的条目 */
export function filterUsable(files: FolderFile[]): FolderFile[] {
  return files.filter((f) => f.path !== '' && !isIgnoredPath(f.path));
}

/**
 * 从拖拽事件里读出文件（支持直接拖入文件夹）
 *
 * 拖入目录时 dataTransfer.files 只有一个目录项、拿不到里面的文件，
 * 必须走 webkitGetAsEntry 递归读取。
 */
export async function collectDroppedEntries(dt: DataTransfer): Promise<FolderFile[]> {
  const entries: FileSystemEntry[] = [];
  for (const item of Array.from(dt.items ?? [])) {
    if (item.kind !== 'file') continue;
    const entry = item.webkitGetAsEntry?.();
    if (entry) entries.push(entry);
  }

  // 浏览器不支持目录 API 时退化成普通文件列表（此时拖入的文件夹读不到内容）
  if (entries.length === 0) {
    return Array.from(dt.files ?? []).map((file) => ({ file, path: file.name }));
  }

  const out: FolderFile[] = [];
  for (const entry of entries) {
    await walkEntry(entry, '', out);
  }
  return out;
}

function walkEntry(entry: FileSystemEntry, prefix: string, out: FolderFile[]): Promise<void> {
  return new Promise((resolve) => {
    if (entry.isFile) {
      (entry as FileSystemFileEntry).file(
        (file) => {
          out.push({ file, path: prefix + file.name });
          resolve();
        },
        () => resolve(),
      );
      return;
    }

    const reader = (entry as FileSystemDirectoryEntry).createReader();
    // readEntries 每次最多返回 100 条，必须反复读到返回空数组为止（Chrome 的已知行为），
    // 否则大文件夹会漏文件
    const readBatch = () => {
      reader.readEntries(
        (batch) => {
          if (batch.length === 0) {
            resolve();
            return;
          }
          void (async () => {
            for (const child of batch) {
              await walkEntry(child, `${prefix}${entry.name}/`, out);
            }
            readBatch();
          })();
        },
        () => resolve(),
      );
    };
    readBatch();
  });
}

export interface ZipFolderOptions {
  /**
   * 包内没有 mod.info 时用来生成一个。
   * 传 null 表示调用方确定包内已有，不需要生成。
   */
  generateInfo: { name: string; description?: string; version?: string } | null;
  /** 打包进度 0–1（读取阶段细分，压缩阶段是整体一次） */
  onProgress?: (ratio: number) => void;
}

/** 生成 mod.info 文本（与游戏侧 ModPacker 相同的 key=value 格式） */
function buildModInfo(info: { name: string; description?: string; version?: string }): string {
  const lines = [`name=${info.name}`];
  if (info.description) lines.push(`description=${info.description.replace(/\r?\n/g, ' ')}`);
  lines.push(`version=${info.version || '1.0'}`);
  return `${lines.join('\n')}\n`;
}

/** 取所有文件的公共顶层目录名（用户选中文件夹时 webkitRelativePath 的第一段就是它） */
function detectTopDir(files: FolderFile[]): string | null {
  if (files.length === 0) return null;
  const tops = new Set<string>();
  for (const f of files) {
    const idx = f.path.indexOf('/');
    // 有文件直接在根上（路径里没有 /）就无法确定单一顶层目录
    if (idx <= 0) return null;
    tops.add(f.path.slice(0, idx));
    if (tops.size > 1) return null;
  }
  return tops.values().next().value ?? null;
}

/**
 * 打包成 zip 文件（返回可直接走上传接口的 File）
 *
 * 压缩级别用 1 而不是默认的 6：模组包里大量是 png / wav 这类已经压缩过的二进制，
 * 提高级别几乎没有收益却慢很多；体积上的那点差别不值得让作者多等。
 */
export async function zipFolder(files: FolderFile[], options: ZipFolderOptions): Promise<File> {
  const usable = filterUsable(files);
  if (usable.length === 0) {
    throw new Error('选中的文件夹里没有可用文件（可能只包含 .git / 系统文件）');
  }

  const data: Record<string, Uint8Array> = {};
  let read = 0;
  for (const entry of usable) {
    data[entry.path] = new Uint8Array(await entry.file.arrayBuffer());
    read++;
    // 读取阶段占总进度前 60%，压缩阶段没法拿到细粒度进度
    options.onProgress?.((read / usable.length) * 0.6);
  }

  const hasModInfo = usable.some((f) => f.path.toLowerCase().endsWith('mod.info'));
  if (!hasModInfo && options.generateInfo) {
    const topDir = detectTopDir(usable);
    const infoPath = topDir ? `${topDir}/mod.info` : 'mod.info';
    data[infoPath] = new TextEncoder().encode(buildModInfo(options.generateInfo));
  }

  const bytes = await new Promise<Uint8Array>((resolve, reject) => {
    // consume: true 让 fflate 读取后立即释放输入，避免源数据与结果同时驻留内存
    zip(data, { level: 1, consume: true }, (err, out) => (err ? reject(err) : resolve(out)));
  });
  options.onProgress?.(1);

  const topDir = detectTopDir(usable);
  const baseName = (options.generateInfo?.name || topDir || 'mod').replace(/[\\/:*?"<>|]/g, '_');
  // TypeScript 5.7 起 Uint8Array 的 buffer 被视为 ArrayBufferLike（可能是 SharedArrayBuffer），
  // 而 BlobPart 只接受 ArrayBuffer。运行时这里一定是普通 ArrayBuffer，
  // 直接断言，避免为了绕过类型而多复制一份几百 MB 的数据。
  return new File([bytes as unknown as BlobPart], `${baseName}.zip`, { type: 'application/zip' });
}
