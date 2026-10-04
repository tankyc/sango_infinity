import crypto from 'node:crypto';
import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { prisma } from '../db';
import { config } from '../config';
import { ApiError } from '../errors';
import { storage, IMMUTABLE_CACHE, type RangeRequest } from '../service/storage';
import { latestVersionString } from '../service/modService';
import { optionalAuth } from '../middleware/auth';

/**
 * 下载路由
 *
 * 路径规则来自客户端 NetModMarket.MakeUrl()：{marketUrl 去掉 /mod_list.txt}/{id}@{version}.zip
 * 若市场 url 写成 {base}/market/mod_list.txt，则客户端会请求 {base}/market/{id}@{version}.zip，
 * 所以 /mods/ 与 /market/ 两个前缀都注册，避免不同来源的市场 URL 打空。
 *
 * 【分发策略，见 建设计划.md §6.1】
 *   CDN_BASE_URL 未配置（冷启动）→ 服务器本地直出，支持 Range 断点续传
 *   CDN_BASE_URL 已配置        → 302 跳 CDN，不占轻量服务器带宽
 *   客户端已验证 UnityWebRequest 默认 redirectLimit=32 会自动跟随 302，无需改游戏。
 */

const ZIP_NAME_PATTERN = /^([A-Za-z0-9_-]+)@([0-9A-Za-z.\-]+)\.zip$/;

interface ZipParams {
  file: string;
}

interface DetailParams {
  id: string;
}

interface DownloadQuery {
  version?: string;
}

/** 解析 Range 头；格式不合法时忽略，返回整包 */
function parseRangeHeader(header: string | undefined): RangeRequest | null {
  if (!header) return null;
  const m = /^bytes=(\d*)-(\d*)$/.exec(header.trim());
  if (!m) return null;
  const [, s, e] = m;
  if (s === '' && e === '') return null;
  if (s === '') {
    // bytes=-N 表示最后 N 字节
    const n = Number(e);
    if (!Number.isFinite(n) || n <= 0) return null;
    return { start: Math.max(0, -n), end: Number.MAX_SAFE_INTEGER };
  }
  const start = Number(s);
  const end = e === '' ? Number.MAX_SAFE_INTEGER : Number(e);
  if (!Number.isFinite(start) || !Number.isFinite(end) || end < start) return null;
  return { start, end };
}

/**
 * 记录一次下载（去重窗口内的同一 IP 只计一次，避免刷新页面刷榜）
 */
async function recordDownload(
  modId: string,
  version: string,
  req: FastifyRequest,
): Promise<void> {
  try {
    const ipHash = crypto
      .createHmac('sha256', config.jwt.secret)
      .update(String(req.ip ?? ''))
      .digest('hex')
      .slice(0, 32);
    const since = new Date(Date.now() - config.downloadDedupeWindowSec * 1000);

    const dup = await prisma.downloadLog.findFirst({
      where: { modId, version, ipHash, createdAt: { gte: since } },
      select: { id: true },
    });
    if (dup) {
      await prisma.downloadLog.update({ where: { id: dup.id }, data: { createdAt: new Date() } });
      return;
    }

    await prisma.$transaction([
      prisma.downloadLog.create({
        data: {
          modId,
          version,
          userId: req.user?.id ?? null,
          ipHash,
          userAgent: String(req.headers['user-agent'] ?? '').slice(0, 255),
        },
      }),
      prisma.modVersion.update({
        where: { modId_version: { modId, version } },
        data: { downloads: { increment: 1 } },
      }),
      prisma.mod.update({ where: { id: modId }, data: { downloads: { increment: 1 } } }),
    ]);
  } catch {
    // 统计失败不能影响下载本身
  }
}

async function serveVersionFile(
  modId: string,
  version: string,
  req: FastifyRequest,
  reply: FastifyReply,
): Promise<void> {
  const versionRow = await prisma.modVersion.findUnique({
    where: { modId_version: { modId, version } },
    include: { mod: { select: { status: true, authorId: true } } },
  });

  if (!versionRow || versionRow.mod.status === 'hidden') {
    throw ApiError.notFound('模组或该版本不存在');
  }
  const mod = versionRow.mod;
  const isPrivileged =
    req.user != null && (req.user.id === mod.authorId || req.user.role === 'admin');
  if (mod.status !== 'approved' && !isPrivileged) {
    throw ApiError.notFound('模组或该版本不存在');
  }

  await recordDownload(modId, version, req);

  // ---- 已启用 CDN → 302 跳转，服务器不再承担流量 ----
  const cdnUrl = storage.publicUrl(versionRow.fileKey);
  if (cdnUrl) {
    reply
      .code(302)
      .header('Location', cdnUrl)
      .header('Cache-Control', IMMUTABLE_CACHE)
      .send();
    return;
  }

  // ---- 冷启动：本地直出，支持 Range 断点续传 ----
  const range = parseRangeHeader(req.headers.range);
  let result;
  try {
    result = await storage.read(versionRow.fileKey, range ?? undefined);
  } catch {
    throw ApiError.notFound('文件已从存储中丢失，请联系作者重新发布该版本');
  }

  reply
    .header('Content-Type', 'application/zip')
    .header('Accept-Ranges', 'bytes')
    .header('Content-Length', String(result.contentLength))
    .header('Cache-Control', IMMUTABLE_CACHE)
    .header('Content-Disposition', `attachment; filename="${modId}@${version}.zip"`);

  if (range) {
    reply
      .code(206)
      .header('Content-Range', `bytes ${result.start}-${result.end}/${result.size}`);
  }

  reply.send(result.stream);
}

export default async function downloadRoutes(app: FastifyInstance): Promise<void> {
  // ---- 客户端协议直链：{id}@{version}.zip ----
  const byFileName = async (req: FastifyRequest<{ Params: ZipParams }>, reply: FastifyReply) => {
    const m = ZIP_NAME_PATTERN.exec(req.params.file ?? '');
    if (!m) {
      throw ApiError.notFound('下载地址格式应为 {模组id}@{版本号}.zip');
    }
    await serveVersionFile(m[1], m[2], req, reply);
    return reply;
  };

  app.get<{ Params: ZipParams }>('/mods/:file', { preHandler: optionalAuth }, byFileName);
  app.get<{ Params: ZipParams }>('/market/:file', { preHandler: optionalAuth }, byFileName);

  // ---- 网页备用下载入口：可省略 version 取最新版，并统一计数 ----
  app.get<{ Params: DetailParams; Querystring: DownloadQuery }>(
    '/api/mods/:id/download',
    { preHandler: optionalAuth },
    async (req, reply) => {
      const modId = req.params.id;
      let version = (req.query?.version ?? '').trim();

      if (version === '') {
        const versions = await prisma.modVersion.findMany({
          where: { modId },
          select: { version: true },
        });
        const latest = latestVersionString(versions);
        if (!latest) throw ApiError.notFound('该模组还没有任何可下载的版本');
        version = latest;
      }

      await serveVersionFile(modId, version, req, reply);
      return reply;
    },
  );
}
