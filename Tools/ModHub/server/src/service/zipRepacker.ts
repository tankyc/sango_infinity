import fs from 'node:fs';
import fsp from 'node:fs/promises';
import path from 'node:path';
import { openPromise, Entry, ZipFile as YauzlZipFile } from 'yauzl';
import { ZipFile as YazlZipFile } from 'yazl';
import { config } from '../config';
import { ApiError } from '../errors';
import type { ZipScanResult } from './zipScanner';
import { serializeModInfo } from './modInfoParser';

/**
 * zip 入库规整（顶层目录 + mod.info 内容）
 *
 * 【为什么需要顶层目录】
 * 客户端 Mod.Download() 解压到 Mods/ 后还要再拼一层 "/{Id}" 去找 mod.info，
 * 所以 zip 内 **必须** 有一个名字等于 id 的顶层目录。缺了这一层的结果是
 * 「下载成功但模组列表里不显示」，且玩家与作者都难以自查（见 建设计划.md §2.4 / §13）。
 *
 * 【为什么要写回 mod.info】
 * 游戏内模组管理器读的是包内 mod.info，而玩家在网页/客户端上填的 id、作者、简介、版本号
 * 只落在站点数据库里 —— 两者不落回同一处，就会出现「网页写着 1.2，游戏里显示 1.0」这类
 * 对不上的情况。因此入库时以服务端为准重写 mod.info，保证：
 *   zip 内 mod.info.id == zip 顶层目录名 == 数据库 mod.id
 *   mod.info 的 name / description / version / author 与站点上显示的一致
 *
 * 【策略】
 *   - 顶层目录已等于 id 且 mod.info 无需改写 → 原样返回（最常见，零开销）
 *   - 否则流式重写一份（不全量入内存）；超过 ZIP_NORMALIZE_MAX_MB 则拒收
 */

export interface NormalizeOptions {
  /** 最终确定的模组 id（= 顶层目录名 = mod.info 的 id） */
  id: string;
  /**
   * 需要写回 zip 内 mod.info 的字段（服务端权威值）。
   * 由调用方判断「是否与包内现有值不同」，不同才传 —— 避免每次上传都白重写一遍大文件。
   */
  modInfo?: Record<string, string>;
}

export interface NormalizeResult {
  /** 规整后的文件路径；无需改动时等于入参路径 */
  path: string;
  /** 是否真的重写过（调用方据此决定临时文件清理） */
  rewritten: boolean;
}

export async function normalizeZip(
  srcPath: string,
  destPath: string,
  opts: NormalizeOptions,
  scan: ZipScanResult,
): Promise<NormalizeResult> {
  const needTopLevel = scan.topDir !== opts.id;
  const needInfo = opts.modInfo != null;

  if (!needTopLevel && !needInfo) {
    return { path: srcPath, rewritten: false };
  }

  // 仅「补顶层目录」时需要体积兜底：重写一个几十上百 MB 的包很费磁盘 IO
  if (needTopLevel) {
    const srcSize = (await fsp.stat(srcPath)).size;
    if (srcSize > config.zipScan.normalizeMaxBytes) {
      throw ApiError.badRequest(
        `压缩包顶层目录名与模组 id 不一致，且体积超过 ${Math.floor(
          config.zipScan.normalizeMaxBytes / 1024 / 1024,
        )}MB，服务器无法自动补一层。请把内容打包进一个名为 "${opts.id}" 的顶层目录后重试。`,
      );
    }
  }

  return { path: await rewriteZip(srcPath, destPath, opts, scan), rewritten: true };
}

async function rewriteZip(
  srcPath: string,
  destPath: string,
  opts: NormalizeOptions,
  scan: ZipScanResult,
): Promise<string> {
  // ⚠ autoClose 必须为 false：addReadStreamLazy 是"按需索取流"的，
  // yazl 往往在 yauzl 遍历完所有条目（触发 end）之后才开始读取内容，
  // 若此时 yauzl 已自动关闭文件句柄，openReadStream 会失败并最终导致整个过程卡死。
  const srcZip: YauzlZipFile = await openPromise(srcPath, { lazyEntries: true, autoClose: false });
  const outZip = new YazlZipFile();

  await fsp.mkdir(path.dirname(destPath), { recursive: true });
  const writeStream = fs.createWriteStream(destPath);
  const writeDone = new Promise<void>((resolve, reject) => {
    writeStream.on('error', reject);
    writeStream.on('finish', () => resolve());
    outZip.outputStream.on('error', reject);
    outZip.outputStream.pipe(writeStream);
  });

  const topLevelModInfo = `${opts.id}/mod.info`;
  const modInfoBuffer =
    opts.modInfo != null ? Buffer.from(serializeModInfo(opts.modInfo), 'utf8') : null;

  const oldTopDir = scan.topDir;
  const toNewName = (entryPath: string): string | null => {
    let rel = entryPath;
    if (oldTopDir != null) {
      if (rel === oldTopDir) return null;
      if (rel.startsWith(oldTopDir + '/')) {
        rel = rel.slice(oldTopDir.length + 1);
        if (rel === '') return null;
      }
    }
    return `${opts.id}/${rel}`;
  };

  try {
    await new Promise<void>((resolve, reject) => {
      srcZip.on('error', reject);
      outZip.on('error', reject);
      srcZip.on('end', () => {
        outZip.end();
        resolve();
      });
      srcZip.on('entry', (entry: Entry) => {
        try {
          const raw = entry.fileName.replace(/\\/g, '/');
          const newName = toNewName(raw);
          if (newName === null) {
            srcZip.readEntry();
            return;
          }
          if (raw.endsWith('/')) {
            outZip.addEmptyDirectory(newName);
            srcZip.readEntry();
            return;
          }

          // 顶层 mod.info 用服务端权威内容替换（内容很小，直接入内存；不必再开源流）
          if (modInfoBuffer != null && newName === topLevelModInfo) {
            outZip.addBuffer(modInfoBuffer, newName);
            srcZip.readEntry();
            return;
          }

          // addReadStreamLazy：yazl 会在准备好写入时才索取流，天然串行，内存占用恒定
          outZip.addReadStreamLazy(
            newName,
            { size: Number(entry.uncompressedSize ?? 0), compress: true },
            (cb) => {
              srcZip.openReadStream(entry, (err, stream) => {
                if (err || !stream) {
                  cb(err ?? new Error('打开 zip 条目失败'), stream as NodeJS.ReadableStream);
                  return;
                }
                cb(null, stream);
              });
            },
          );
          srcZip.readEntry();
        } catch (e) {
          reject(e);
        }
      });
      srcZip.readEntry();
    });

    await writeDone;
  } catch (e) {
    writeStream.destroy();
    await fsp.rm(destPath, { force: true });
    throw new ApiError(500, `重写模组包失败: ${(e as Error).message}`, 'zip_rewrite_failed');
  } finally {
    // 所有惰性流都已被 yazl 消费完毕，此时才能安全关闭源文件句柄
    srcZip.close();
  }

  return destPath;
}
