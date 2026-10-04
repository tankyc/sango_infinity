import crypto from 'node:crypto';
import fs from 'node:fs';
import fsp from 'node:fs/promises';
import path from 'node:path';
import { Readable } from 'node:stream';
import { Transform } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import type { MultipartFile } from '@fastify/multipart';
import { config } from '../config';
import { ApiError } from '../errors';

export interface TempFile {
  path: string;
  size: number;
  sha256: string;
  filename: string;
}

/**
 * 把上传流落盘到临时文件，同时计算 sha256 与字节数
 *
 * 为什么要落盘而不是一次性 Buffer：
 *   大模组（头像包可达 100MB+）整包驻留内存会打爆轻量服务器；
 *   同时 Unity 侧 GitDownloader 是全量内存下载，"服务器这边"至少要保证不会二次放大。
 *
 * @param stream 上传流
 * @param maxBytes 大小上限，超出直接 413
 * @param filename 原始文件名（仅用于后缀推断与日志）
 */
export async function saveStreamToTemp(
  stream: Readable,
  maxBytes: number,
  filename: string,
): Promise<TempFile> {
  await fsp.mkdir(config.tmpDir, { recursive: true });
  const tmpPath = path.join(config.tmpDir, `${crypto.randomUUID()}.part`);

  const hash = crypto.createHash('sha256');
  let size = 0;

  const meter = new Transform({
    transform(chunk: Buffer, _enc, cb) {
      size += chunk.length;
      if (size > maxBytes) {
        cb(new ApiError(413, `文件超过大小上限 ${Math.floor(maxBytes / 1024 / 1024)}MB`, 'payload_too_large'));
        return;
      }
      hash.update(chunk);
      cb(null, chunk);
    },
  });

  try {
    await pipeline(stream, meter, fs.createWriteStream(tmpPath));
  } catch (e) {
    await fsp.rm(tmpPath, { force: true });
    throw e;
  }

  return { path: tmpPath, size, sha256: hash.digest('hex'), filename };
}

/** 从 multipart 文件部件落盘 */
export function savePartToTemp(part: MultipartFile, maxBytes: number): Promise<TempFile> {
  return saveStreamToTemp(part.file as Readable, maxBytes, part.filename ?? '');
}

/** 删除临时文件（忽略不存在的情形） */
export async function removeTempFiles(...files: (string | null | undefined)[]): Promise<void> {
  await Promise.all(
    files
      .filter((f): f is string => !!f)
      .map((f) => fsp.rm(f, { force: true }).catch(() => undefined)),
  );
}
