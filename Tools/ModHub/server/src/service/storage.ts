import fs from 'node:fs';
import fsp from 'node:fs/promises';
import path from 'node:path';
import { Readable } from 'node:stream';
import { ReadableStream as NodeWebReadableStream } from 'node:stream/web';
import { pipeline } from 'node:stream/promises';
import { config } from '../config';

/**
 * 存储与分发抽象层
 *
 * 【为什么要抽这一层】
 * 轻量服务器出口带宽仅 3–8Mbps，一个大模组就可能打满，详见 建设计划.md §6.1。
 * 所以静态文件（zip / 封面）必须能从"本地磁盘直出"无缝切到"对象存储 + CDN"，
 * 且切换时客户端零改动（已验证 UnityWebRequest 默认 redirectLimit=32 会跟随 302）。
 *
 * 切换方式（只改环境变量，不改代码）：
 *   冷启动：STORAGE_DRIVER=local, CDN_BASE_URL=   → 服务器直接吐文件
 *   有量后：STORAGE_DRIVER=s3,    CDN_BASE_URL=https://cdn.xx  → 统一 302 跳 CDN
 */

/** 请求范围内可能包含多个文件，先 Kilobyte 化可能影响流式下载，故范围以字节为单位 */
export interface RangeRequest {
  start: number;
  end: number; // 含端点
}

export interface ReadResult {
  stream: Readable;
  size: number; // 完整文件大小
  start: number;
  end: number; // 含端点
  contentLength: number;
}

export interface PutOptions {
  contentType?: string;
}

export interface StorageAdapter {
  /** 适配器类型，便于健康检查与日志观察当前形态 */
  readonly kind: 'local' | 's3';

  /** 把本地已有文件放进存储，返回对象字节数 */
  putFile(localPath: string, key: string, opts?: PutOptions): Promise<number>;

  /** 文件字节数，不存在返回 null */
  size(key: string): Promise<number | null>;

  /** 按给定范围读取（支持 Range 断点续传） */
  read(key: string, range?: RangeRequest): Promise<ReadResult>;

  remove(key: string): Promise<void>;

  /**
   * 可直接对外访问的地址（CDN 直链）。
   * 配置 CDN_BASE_URL 后返回地址；未配置返回 null，调用方回落到本地直出。
   * —— 这是日后切到 CDN 的唯一开关。
   */
  publicUrl(key: string): string | null;
}

/** key 安全校验：只允许 字母数字/下划线/短横/@/./斜杠，且不含 .. 路径段 */
export function assertSafeKey(key: string): void {
  if (!/^[A-Za-z0-9._@\-/]+$/.test(key)) {
    throw new Error(`非法的存储对象 key: ${key}`);
  }
  if (key.startsWith('/') || key.includes('..') || key.includes('//')) {
    throw new Error(`非法的存储对象 key: ${key}`);
  }
}

// ------------------------------------------------------------------
// 本地磁盘实现（冷启动默认）
// ------------------------------------------------------------------

class LocalDiskStorage implements StorageAdapter {
  readonly kind = 'local' as const;
  private readonly root: string;

  constructor(root: string) {
    this.root = root;
  }

  private resolve(key: string): string {
    assertSafeKey(key);
    const full = path.resolve(this.root, key);
    // 二次兜底：防止未来 key 规则放宽后发生路径穿越
    const rootWithSep = this.root.endsWith(path.sep) ? this.root : this.root + path.sep;
    if (!full.startsWith(rootWithSep)) {
      throw new Error('存储对象 key 越界');
    }
    return full;
  }

  async putFile(localPath: string, key: string): Promise<number> {
    const target = this.resolve(key);
    await fsp.mkdir(path.dirname(target), { recursive: true });
    try {
      // 同分区优先 rename，避免 200MB 大文件的拷贝开销
      await fsp.rename(localPath, target);
    } catch {
      await fsp.copyFile(localPath, target);
      await fsp.rm(localPath, { force: true });
    }
    const st = await fsp.stat(target);
    return st.size;
  }

  async size(key: string): Promise<number | null> {
    try {
      const st = await fsp.stat(this.resolve(key));
      return st.isFile() ? st.size : null;
    } catch {
      return null;
    }
  }

  async read(key: string, range?: RangeRequest): Promise<ReadResult> {
    const file = this.resolve(key);
    const st = await fsp.stat(file);
    if (!st.isFile()) throw new Error('对象不存在');

    const size = st.size;
    const start = range ? Math.max(0, Math.min(range.start, size - 1)) : 0;
    const end = range ? Math.max(start, Math.min(range.end, size - 1)) : size - 1;
    const stream = fs.createReadStream(file, { start, end });
    return { stream, size, start, end, contentLength: end - start + 1 };
  }

  async remove(key: string): Promise<void> {
    await fsp.rm(this.resolve(key), { force: true });
  }

  publicUrl(key: string): string | null {
    const cdn = config.storage.cdnBaseUrl;
    if (!cdn) return null;
    return `${cdn}/${key}`;
  }
}

// ------------------------------------------------------------------
// 对象存储实现（S3 协议：COS / OSS / R2 / MinIO 通用）
// 需要时执行 npm i @aws-sdk/client-s3 后即可启用
// ------------------------------------------------------------------

interface S3Like {
  send(command: unknown): Promise<unknown>;
}

class S3Storage implements StorageAdapter {
  readonly kind = 's3' as const;
  private client!: S3Like;
  private readonly bucket: string;

  constructor() {
    const s3 = config.storage.s3;
    this.bucket = s3.bucket;
    if (!s3.bucket || !s3.accessKeyId || !s3.secretAccessKey) {
      throw new Error('STORAGE_DRIVER=s3 但未配置 S3_BUCKET / S3_ACCESS_KEY_ID / S3_SECRET_ACCESS_KEY');
    }
  }

  /** 延迟加载 SDK，未安装时给出明确的安装提示，不影响 local 模式启动 */
  private ensureClient(): S3Like {
    if (this.client) return this.client;
    let mod: any;
    try {
      // eslint-disable-next-line @typescript-eslint/no-var-requires
      mod = require('@aws-sdk/client-s3');
    } catch {
      throw new Error('使用 STORAGE_DRIVER=s3 前请先安装依赖：npm i @aws-sdk/client-s3');
    }
    const s3 = config.storage.s3;
    this.client = new mod.S3Client({
      region: s3.region || 'auto',
      endpoint: s3.endpoint || undefined,
      forcePathStyle: s3.forcePathStyle,
      credentials: { accessKeyId: s3.accessKeyId, secretAccessKey: s3.secretAccessKey },
    });
    return this.client;
  }

  async putFile(localPath: string, key: string, opts?: PutOptions): Promise<number> {
    assertSafeKey(key);
    const mod = require('@aws-sdk/client-s3');
    const st = await fsp.stat(localPath);
    await this.ensureClient().send(
      new mod.PutObjectCommand({
        Bucket: this.bucket,
        Key: key,
        Body: fs.createReadStream(localPath),
        ContentType: opts?.contentType ?? 'application/zip',
        // 内容不可变 → 允许 CDN 与浏览器永久缓存（详见 §6.1）
        CacheControl: 'public, max-age=31536000, immutable',
      }),
    );
    return st.size;
  }

  async size(key: string): Promise<number | null> {
    assertSafeKey(key);
    const mod = require('@aws-sdk/client-s3');
    try {
      const res: any = await this.ensureClient().send(
        new mod.HeadObjectCommand({ Bucket: this.bucket, Key: key }),
      );
      return typeof res.ContentLength === 'number' ? res.ContentLength : null;
    } catch {
      return null;
    }
  }

  async read(key: string, range?: RangeRequest): Promise<ReadResult> {
    assertSafeKey(key);
    const mod = require('@aws-sdk/client-s3');
    const head: any = await this.ensureClient().send(
      new mod.HeadObjectCommand({ Bucket: this.bucket, Key: key }),
    );
    const size = Number(head.ContentLength ?? 0);
    const start = range ? Math.max(0, Math.min(range.start, size - 1)) : 0;
    const end = range ? Math.max(start, Math.min(range.end, size - 1)) : Math.max(0, size - 1);

    const res: any = await this.ensureClient().send(
      new mod.GetObjectCommand({
        Bucket: this.bucket,
        Key: key,
        Range: range ? `bytes=${start}-${end}` : undefined,
      }),
    );
    const body = res.Body;
    const stream =
      typeof body?.pipe === 'function'
        ? (body as Readable)
        : Readable.fromWeb(body as NodeWebReadableStream);

    return { stream, size, start, end, contentLength: end - start + 1 };
  }

  async remove(key: string): Promise<void> {
    assertSafeKey(key);
    const mod = require('@aws-sdk/client-s3');
    await this.ensureClient().send(new mod.DeleteObjectCommand({ Bucket: this.bucket, Key: key }));
  }

  publicUrl(key: string): string | null {
    const cdn = config.storage.cdnBaseUrl;
    if (cdn) return `${cdn}/${key}`;
    // 无 CDN 时给出对象存储的公开访问地址（需要 bucket 为公开读或用签名 URL）
    const s3 = config.storage.s3;
    if (s3.endpoint) return `${s3.endpoint.replace(/\/+$/, '')}/${this.bucket}/${key}`;
    return null;
  }
}

// ------------------------------------------------------------------

function createAdapter(): StorageAdapter {
  if (config.storage.driver === 's3') return new S3Storage();
  return new LocalDiskStorage(config.storage.localRoot);
}

/** 全局存储实例。s3 适配器在缺少依赖时延迟到首次调用才报错 */
export const storage: StorageAdapter = (() => {
  try {
    return createAdapter();
  } catch (e) {
    if (config.storage.driver === 's3') {
      // 缺失依赖或配置时不阻断启动，交由具体路由报错
      return new Proxy({} as StorageAdapter, {
        get() {
          throw new Error(
            `对象存储未就绪：${(e as Error).message}（当前 STORAGE_DRIVER=s3，可先改回 local 启动）`,
          );
        },
      });
    }
    throw e;
  }
})();

/** 静态文件的缓存头（内容不可变，永远按版本/哈希定址） */
export const IMMUTABLE_CACHE = 'public, max-age=31536000, immutable';

/**
 * 把某个 key 的内容直接流式写入本地文件（给打包/临时用途使用）
 */
export async function downloadToFile(key: string, target: string): Promise<void> {
  const res = await storage.read(key);
  await fsp.mkdir(path.dirname(target), { recursive: true });
  await pipeline(res.stream, fs.createWriteStream(target));
}
