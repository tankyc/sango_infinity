import fs from 'node:fs';
import path from 'node:path';

/**
 * 配置中心
 * 所有运行期参数集中在此，切换部署形态（本地直出 / 对象存储 + CDN）只改环境变量，不改代码。
 */

const MB = 1024 * 1024;

/** server/ 目录（兼容 tsx 直跑 src/ 与 build 后跑 dist/ 两种形态） */
export const SERVER_ROOT = path.resolve(__dirname, '..');

/** prisma/schema.prisma 所在目录，SQLite 相对路径以此为基准，与 prisma CLI 保持一致 */
export const PRISMA_DIR = path.join(SERVER_ROOT, 'prisma');

function envStr(name: string, def: string): string {
  const v = process.env[name];
  return v != null && v.trim() !== '' ? v.trim() : def;
}

function envInt(name: string, def: number): number {
  const v = process.env[name];
  if (v == null || v.trim() === '') return def;
  const n = Number(v);
  return Number.isFinite(n) ? n : def;
}

function envBool(name: string, def: boolean): boolean {
  const v = process.env[name];
  if (v == null || v.trim() === '') return def;
  return ['1', 'true', 'yes', 'on'].includes(v.trim().toLowerCase());
}

/** 相对 server/ 解析的路径 */
function resolvePath(p: string): string {
  return path.isAbsolute(p) ? p : path.resolve(SERVER_ROOT, p);
}

/**
 * 把 SQLite 的 file: 相对路径解析成绝对路径
 * prisma CLI 以 schema 所在目录为基准解析相对路径，应用这边也统一按 PRISMA_DIR 解析，
 * 保证 "migrate 的库" 与 "运行时读写的库" 是同一个文件。
 */
function normalizeSqliteUrl(raw: string): string {
  if (!raw.startsWith('file:')) return raw;
  const p = raw.slice('file:'.length);
  // 已经是绝对路径（/xxx 或 C:/xxx）则原样返回
  if (p.startsWith('/') || /^[a-zA-Z]:[\\/]/.test(p)) return raw;
  const abs = path.resolve(PRISMA_DIR, p);
  return 'file:' + abs.split(path.sep).join('/');
}

const rawDataDir = resolvePath(envStr('DATA_DIR', '../data'));

export const config = {
  nodeEnv: envStr('NODE_ENV', 'development'),
  get isProduction(): boolean {
    return this.nodeEnv === 'production';
  },

  host: envStr('HOST', '0.0.0.0'),
  port: envInt('PORT', 8081),

  /** 运行期数据根目录 */
  dataDir: rawDataDir,
  /** 临时目录：上传的 zip 先落盘再处理，避免整包驻留内存 */
  tmpDir: path.join(rawDataDir, 'tmp'),

  databaseUrl: normalizeSqliteUrl(envStr('DATABASE_URL', 'file:../../data/modhub.db')),

  /**
   * 对外访问基址，写进 /market/mod_list.txt 的 url 字段。
   * 客户端 MakeUrl 规则：截掉结尾 "/mod_list.txt" 后拼 "/{id}@{version}.zip"，
   * 故实际下载地址为 {APP_BASE_URL}/market/{id}@{version}.zip（服务端两条路径都注册了）。
   */
  appBaseUrl: envStr('APP_BASE_URL', 'http://localhost:8081').replace(/\/+$/, ''),

  jwt: {
    secret: envStr('JWT_SECRET', 'sango-modhub-dev-secret-please-change'),
    expiresIn: envStr('JWT_EXPIRES_IN', '30d'),
  },

  upload: {
    maxZipBytes: envInt('UPLOAD_MAX_ZIP_MB', 200) * MB,
    maxPosterBytes: envInt('UPLOAD_MAX_POSTER_MB', 5) * MB,
  },

  /**
   * zip 安全扫描阈值，对应 建设计划.md §9.2 入库流水线
   */
  zipScan: {
    maxEntries: envInt('ZIP_MAX_ENTRIES', 20000),
    maxSingleFileBytes: envInt('ZIP_MAX_SINGLE_FILE_MB', 100) * MB,
    maxCompressionRatio: envInt('ZIP_MAX_COMPRESSION_RATIO', 200),
    blockedExtensions: [
      '.dll', '.exe', '.so', '.a', '.dylib', '.sh', '.bat', '.cmd', '.ps1',
      '.apk', '.jar', '.com', '.scr', '.deb', '.rpm', '.py', '.vbs',
    ],
    /** 顶层目录不规范时重写 zip 的大小上限，超过则拒收 */
    normalizeMaxBytes: envInt('ZIP_NORMALIZE_MAX_MB', 300) * MB,
  },

  market: {
    name: envStr('MARKET_NAME', 'Sango Workshop'),
    /** 市场清单缓存秒数 */
    cacheTtlSec: envInt('MARKET_CACHE_TTL', 300),
  },

  /** 发布后是否直接上架；false 则需管理员审核后才进市场清单 */
  autoApprove: envBool('AUTO_APPROVE', true),

  /**
   * 超级管理员账号
   * 服务启动时自动确保该账号存在且角色为 admin（幂等），
   * 这样全新部署后无需连数据库就能进后台；生产环境务必修改默认密码。
   */
  admin: {
    superUsername: envStr('SUPER_ADMIN_USERNAME', 'superadmin'),
    superPassword: envStr('SUPER_ADMIN_PASSWORD', 'sango@admin'),
    /** 置 true 时每次启动都把超管密码重置为上面的值（忘记密码时的应急手段） */
    forcePassword: envBool('SUPER_ADMIN_FORCE_PASSWORD', false),
  },

  /** 下载计数去重窗口（秒），同一 IP + 模组版本在窗口内只计一次 */
  downloadDedupeWindowSec: envInt('DOWNLOAD_DEDUPE_WINDOW', 3600),

  /**
   * 存储与分发（详见 建设计划.md §6.1）
   * local = 轻量服务器本地磁盘直出（冷启动默认）
   * s3    = 对象存储（COS/OSS/R2 等 S3 协议）
   * CDN_BASE_URL 一旦设置，所有静态文件统一 302 跳 CDN —— 客户端已验证会自动跟随。
   */
  storage: {
    driver: envStr('STORAGE_DRIVER', 'local') as 'local' | 's3',
    localRoot: path.join(rawDataDir, 'files'),
    cdnBaseUrl: envStr('CDN_BASE_URL', '').replace(/\/+$/, ''),
    s3: {
      endpoint: envStr('S3_ENDPOINT', ''),
      region: envStr('S3_REGION', ''),
      bucket: envStr('S3_BUCKET', ''),
      accessKeyId: envStr('S3_ACCESS_KEY_ID', ''),
      secretAccessKey: envStr('S3_SECRET_ACCESS_KEY', ''),
      forcePathStyle: envBool('S3_FORCE_PATH_STYLE', false),
    },
  },
};

/** 确保运行期需要的目录都存在 */
export function ensureRuntimeDirs(): void {
  const dbUrl = config.databaseUrl;
  const dirs = [
    config.dataDir,
    config.tmpDir,
    config.storage.localRoot,
    path.join(config.storage.localRoot, 'mods'),
    path.join(config.storage.localRoot, 'posters'),
  ];
  if (dbUrl.startsWith('file:')) {
    dirs.push(path.dirname(dbUrl.slice('file:'.length)));
  }
  for (const d of dirs) {
    if (d) fs.mkdirSync(d, { recursive: true });
  }
}
