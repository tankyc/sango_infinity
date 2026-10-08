import fsp from 'node:fs/promises';
import path from 'node:path';
import { openPromise, Entry, ZipFile } from 'yauzl';
import { config } from '../config';
import { ApiError } from '../errors';

/**
 * zip 安全扫描 + 结构探测
 *
 * 对应 建设计划.md §9.2 入库流水线第 3 步。要点：
 *   - 模组 = 数据 + 资源，无可执行代码 → 可执行文件一律拒收
 *   - 只读中央目录 + 惰性遍历，不全量展开到内存
 *   - 检测 zip bomb / zip slip / 顶层目录是否等于 mod id
 */

export interface ZipEntry {
  path: string;
  size: number;
  compressedSize: number;
  isDir: boolean;
}

export interface ZipScanResult {
  entries: ZipEntry[];
  /** 文件（非目录）条目数 */
  fileCount: number;
  totalUncompressed: number;
  /** 唯一的顶层目录名（所有条目都在此目录下），否则为 null */
  topDir: string | null;
  /** 顶层目录名是否与 id 一致 */
  topDirMatchesId: boolean;
  /** zip 内 mod.info 的路径，未找到为 null */
  modInfoPath: string | null;
  /** 必须拒收的问题 */
  issues: string[];
  /** 提示性问题，不阻断 */
  warnings: string[];
}

/**
 * 把底层 zip 解析异常转成可预期的业务错误
 * yauzl 对包含 ".." / 反斜杠 / 绝对路径的条目名会直接报错，必须转成 400 而不是 500。
 */
function toScanError(e: unknown): ApiError {
  const msg = (e as Error)?.message ?? String(e);
  if (/invalid relative path|absolute path|backslash|malformed|too large/i.test(msg)) {
    return ApiError.badRequest('压缩包包含非法的文件名（路径穿越或绝对路径）', [msg]);
  }
  return ApiError.badRequest('无法读取压缩包，文件可能已损坏', [msg]);
}

/** 把 zip 内路径统一成 posix 分隔，用于比较 */
function normalizeEntryPath(raw: string): string {
  let p = raw.replace(/\\/g, '/');
  while (p.startsWith('./')) p = p.slice(2);
  return p;
}

function extLower(name: string): string {
  const base = name.slice(name.lastIndexOf('/') + 1);
  const dot = base.lastIndexOf('.');
  return dot <= 0 ? '' : base.slice(dot).toLowerCase();
}

function firstSegment(p: string): string {
  const idx = p.indexOf('/');
  return idx === -1 ? p : p.slice(0, idx);
}

/**
 * 扫描本地 zip 文件
 * @param filePath zip 在磁盘上的路径（已落盘的临时文件）
 * @param options.id 期望的模组 id，用于校验顶层目录名
 */
export async function scanZip(
  filePath: string,
  options: { id?: string } = {},
): Promise<ZipScanResult> {
  const limits = config.zipScan;
  const entries: ZipEntry[] = [];
  const issues: string[] = [];
  const warnings: string[] = [];

  let zip: ZipFile;
  try {
    zip = await openPromise(filePath, { lazyEntries: true, validateEntrySizes: true });
  } catch (e) {
    throw toScanError(e);
  }

  try {
    await new Promise<void>((resolve, reject) => {
      zip.on('error', reject);
      zip.on('end', () => resolve());
      zip.on('entry', (entry: Entry) => {
        // 注意：lazyEntries 模式下必须先同步处理完当前条目，再触发下一条
        try {
          handleEntry(entry);
        } catch (e) {
          reject(e);
          return;
        }
        zip.readEntry();
      });
      zip.readEntry();
    });
  } catch (e) {
    // yauzl 自身会拒绝 ../、反斜杠、绝对路径等恶意条目名，这里统一转成可预期的业务错误
    throw toScanError(e);
  } finally {
    zip.close();
  }

  // 惰性遍历期间同步校验，抽成闭包便于在 'entry' 回调里复用
  function handleEntry(entry: Entry): void {
    const name = normalizeEntryPath(entry.fileName);

    if (name === '/' || name === '') return;

    // ---- zip slip / 绝对路径 ----
    if (name.startsWith('/') || /^[a-zA-Z]:/.test(name)) {
      issues.push(`条目使用绝对路径: ${name}`);
      return;
    }
    const segments = name.split('/').filter((s) => s.length > 0);
    if (segments.some((s) => s === '..')) {
      issues.push(`条目包含路径穿越 (..): ${name}`);
      return;
    }

    const isDir = name.endsWith('/');
    entries.push({
      path: isDir ? name : segments.join('/'),
      size: Number(entry.uncompressedSize ?? 0),
      compressedSize: Number(entry.compressedSize ?? 0),
      isDir,
    });
    if (isDir) return;

    // ---- 条目数量 ----
    if (entries.length > limits.maxEntries) {
      issues.push(`压缩包条目数超过上限 ${limits.maxEntries}`);
      return;
    }

    // ---- 可执行文件白名单外一律拒收（模组不得包含可执行代码）----
    const ext = extLower(name);
    if (limits.blockedExtensions.includes(ext)) {
      issues.push(`不允许的文件类型 ${ext}: ${name}`);
      return;
    }

    // ---- 单文件大小 ----
    if (entry.uncompressedSize > limits.maxSingleFileBytes) {
      issues.push(
        `单个文件超过上限 ${Math.floor(limits.maxSingleFileBytes / 1024 / 1024)}MB: ${name}`,
      );
      return;
    }

    if (entry.uncompressedSize === 0 && !isDir) {
      warnings.push(`存在 0 字节文件: ${name}`);
    }
  }

  const files = entries.filter((e) => !e.isDir);
  const fileCount = files.length;
  const totalUncompressed = entries.reduce((sum, e) => sum + e.size, 0);

  // ---- zip bomb ----
  const diskSize = (await fsp.stat(filePath)).size;
  if (diskSize > 0 && totalUncompressed / diskSize > limits.maxCompressionRatio) {
    issues.push(
      `压缩比异常（${Math.round(totalUncompressed / diskSize)}:1），疑似 zip bomb，已拒收`,
    );
  }

  // ---- 顶层目录探测：zip 内必须有且仅有一个顶层目录，且名字 == id ----
  const topSegments = new Set(files.map((e) => firstSegment(e.path)));
  const topDir = topSegments.size === 1 ? [...topSegments][0] : null;

  if (fileCount === 0) {
    issues.push('压缩包为空');
  } else if (topDir === null) {
    warnings.push('压缩包没有统一的顶层目录，入库时会自动补一层 {id}/');
  }

  const topDirMatchesId = topDir !== null && options.id != null && topDir === options.id;

  // ---- mod.info 定位（允许在顶层目录内或压缩包根）----
  const modInfoCandidates = files.filter(
    (e) => path.posix.basename(e.path).toLowerCase() === 'mod.info',
  );
  modInfoCandidates.sort((a, b) => a.path.split('/').length - b.path.split('/').length);
  const modInfoPath = modInfoCandidates.length > 0 ? modInfoCandidates[0].path : null;

  if (fileCount > 0 && modInfoPath === null) {
    issues.push('压缩包中找不到 mod.info（缺少该文件的模组无法被游戏加载）');
  }
  if (modInfoCandidates.length > 1) {
    warnings.push(`存在多个 mod.info，已取最浅的一个: ${modInfoPath}`);
  }

  return {
    entries,
    fileCount,
    totalUncompressed,
    topDir,
    topDirMatchesId,
    modInfoPath,
    issues,
    warnings,
  };
}

/** 读取 zip 内某个条目的全部字节（mod.info 等小文件用） */
export async function readZipEntry(filePath: string, entryPath: string): Promise<Buffer> {
  const zip: ZipFile = await openPromise(filePath, { lazyEntries: true });
  const chunks: Buffer[] = [];

  try {
    await new Promise<void>((resolve, reject) => {
      zip.on('error', reject);
      zip.on('end', () => resolve());
      zip.on('entry', (entry: Entry) => {
        const name = normalizeEntryPath(entry.fileName);
        if (name !== entryPath) {
          zip.readEntry();
          return;
        }
        zip.openReadStream(entry, (err, stream) => {
          if (err || !stream) {
            reject(err ?? new Error('读取 zip 条目失败'));
            return;
          }
          stream.on('data', (chunk: Buffer) => chunks.push(chunk));
          stream.on('end', () => {
            zip.readEntry();
          });
          stream.on('error', reject);
        });
      });
      zip.readEntry();
    });
  } finally {
    zip.close();
  }

  if (chunks.length === 0) {
    throw new Error(`zip 中不存在条目: ${entryPath}`);
  }
  return Buffer.concat(chunks);
}
