// 把本地 data/files 下的对象全量迁移到对象存储（S3 协议：R2 / COS / OSS 通用）
//
// 用法（在 server/ 目录下）：
//   npm run migrate:s3              正常迁移，已存在的对象跳过
//   npm run migrate:s3 -- --dry     只预演，不真上传
//   npm run migrate:s3 -- --force   覆盖已存在的对象
//
// 配置读自 server/.env 的 S3_* 项。key 规则与运行期一致（posters/…、mods/…），
// 因此迁移后数据库里的 posterUrl 不需要任何改动。
import fs from 'node:fs';
import fsp from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { S3Client, PutObjectCommand, HeadObjectCommand, ListObjectsV2Command } from '@aws-sdk/client-s3';

const serverRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const dryRun = args.includes('--dry');
const force = args.includes('--force');

/** 极简 .env 解析：避免为脚本额外引入依赖；已存在的进程环境变量优先 */
function loadEnv() {
  const envPath = path.join(serverRoot, '.env');
  if (!fs.existsSync(envPath)) return {};
  const out = {};
  for (const line of fs.readFileSync(envPath, 'utf8').split(/\r?\n/)) {
    const m = /^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$/.exec(line);
    if (!m) continue;
    let v = m[2].trim();
    if ((v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'"))) {
      v = v.slice(1, -1);
    }
    out[m[1]] = v;
  }
  return out;
}

const env = { ...loadEnv(), ...process.env };

const endpoint = (env.S3_ENDPOINT || '').trim();
const bucket = (env.S3_BUCKET || '').trim();
const accessKeyId = (env.S3_ACCESS_KEY_ID || '').trim();
const secretAccessKey = (env.S3_SECRET_ACCESS_KEY || '').trim();
const region = (env.S3_REGION || 'auto').trim() || 'auto';
const forcePathStyle = String(env.S3_FORCE_PATH_STYLE || '').toLowerCase() === 'true';

if (!endpoint || !bucket || !accessKeyId || !secretAccessKey) {
  console.error('缺少 S3_ENDPOINT / S3_BUCKET / S3_ACCESS_KEY_ID / S3_SECRET_ACCESS_KEY，请先补全 server/.env');
  process.exit(1);
}

const dataDir = path.resolve(serverRoot, env.DATA_DIR || '../data');
const localRoot = path.join(dataDir, 'files');

if (!fs.existsSync(localRoot)) {
  console.error(`本地存储目录不存在：${localRoot}`);
  process.exit(1);
}

const CONTENT_TYPES = {
  '.zip': 'application/zip',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.gif': 'image/gif',
  '.webp': 'image/webp',
};

const client = new S3Client({
  region,
  endpoint,
  forcePathStyle,
  credentials: { accessKeyId, secretAccessKey },
});

/** 递归收集所有文件（跳过目录） */
async function walk(dir) {
  const out = [];
  for (const entry of await fsp.readdir(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...(await walk(full)));
    else if (entry.isFile()) out.push(full);
  }
  return out;
}

/**
 * 启动前自检：桶是否存在、密钥是否有效。
 * 不做这一步的话，凭证配错时会把每个文件都试一遍才发现，大文件还白传一遍。
 */
async function healthCheck() {
  try {
    const res = await client.send(new ListObjectsV2Command({ Bucket: bucket, MaxKeys: 1 }));
    return { ok: true, objectCount: typeof res.KeyCount === 'number' ? res.KeyCount : 0 };
  } catch (err) {
    return {
      ok: false,
      name: err?.name || 'UnknownError',
      status: err?.$metadata?.httpStatusCode,
      message: err?.message || String(err),
    };
  }
}

async function remoteExists(key) {
  try {
    await client.send(new HeadObjectCommand({ Bucket: bucket, Key: key }));
    return true;
  } catch {
    return false;
  }
}

(async () => {
  const health = await healthCheck();
  if (!health.ok) {
    console.error(`自检失败：无法访问桶 ${bucket}`);
    console.error(`  ${health.name}${health.status ? ` (HTTP ${health.status})` : ''}  ${health.message}`);
    console.error('  常见原因：S3_ENDPOINT 里的 AccountID 不对 / 密钥无效或权限不足 / 桶名写错');
    process.exit(1);
  }

  const files = await walk(localRoot);
  console.log(`自检通过：桶可访问、密钥有效（远端标记 ${health.objectCount} 个对象）`);
  console.log(`桶：${bucket}    endpoint：${endpoint}`);
  console.log(`本地对象：${files.length} 个（源目录 ${localRoot}）`);
  if (dryRun) console.log('模式：预演（不会真正上传）\n');

  let uploaded = 0;
  let skipped = 0;
  let totalBytes = 0;

  for (const file of files) {
    const key = path.relative(localRoot, file).split(path.sep).join('/');
    const size = (await fsp.stat(file)).size;

    if (!force && (await remoteExists(key))) {
      skipped++;
      console.log(`  跳过（远端已存在） ${key}`);
      continue;
    }

    if (dryRun) {
      console.log(`  [预演] ${key}  ${size} bytes`);
      totalBytes += size;
      continue;
    }

    await client.send(
      new PutObjectCommand({
        Bucket: bucket,
        Key: key,
        Body: fs.createReadStream(file),
        ContentType: CONTENT_TYPES[path.extname(file).toLowerCase()] || 'application/octet-stream',
        // 与运行期保持一致：内容不可变，允许 CDN 与浏览器永久缓存
        CacheControl: 'public, max-age=31536000, immutable',
      }),
    );

    uploaded++;
    totalBytes += size;
    console.log(`  已上传 ${key}  ${size} bytes`);
  }

  console.log(
    dryRun
      ? `\n预演结束：待上传 ${files.length - skipped} 个（${(totalBytes / 1024 / 1024).toFixed(2)} MB），已存在 ${skipped} 个`
      : `\n完成：上传 ${uploaded} 个（${(totalBytes / 1024 / 1024).toFixed(2)} MB），跳过 ${skipped} 个`,
  );
})().catch((err) => {
  console.error('\n迁移失败：', err?.message || err);
  process.exit(1);
});
