import path from 'node:path';
import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { config } from '../config';
import { ApiError } from '../errors';
import { authenticate } from '../middleware/auth';
import { readMultipart } from '../service/form';
import { removeTempFiles } from '../service/upload';
import { publish, type PublishInput } from '../service/modService';
import { clearMarketCache } from '../service/marketCache';

/**
 * 创作发布路由
 *
 * multipart 字段沿用云存档的习惯：文件字段叫 file（与 CloudSaveClient.UploadSave 一致）。
 *   POST /api/mods            发布新模组
 *   POST /api/mods/:id/versions   给已有模组发新版本
 *
 * 表单字段：
 *   file        模组 zip（必需）
 *   poster      封面图（可选，png/jpg/webp）
 *   name/summary/description/category/tags/depends/gameVersion
 *   version     版本号（可选，缺省取 mod.info 的 version）
 *   changelog   变更日志
 */

interface VersionParams {
  id: string;
}

function str(fields: Record<string, string>, key: string): string | undefined {
  const v = fields[key];
  return v != null && v.trim() !== '' ? v.trim() : undefined;
}

export default async function publishRoutes(app: FastifyInstance): Promise<void> {
  const handle = async (
    req: FastifyRequest,
    reply: FastifyReply,
    modId: string | null,
  ): Promise<FastifyReply> => {
    if (!req.user) throw ApiError.unauthorized();

    const { fields, files } = await readMultipart(req, {
      fileFields: {
        file: config.upload.maxZipBytes,
        poster: config.upload.maxPosterBytes,
      },
    });

    const zip = files.file;
    if (!zip) throw ApiError.badRequest('缺少模组压缩包（multipart 字段名应为 file）');
    if (zip.size === 0) throw ApiError.badRequest('上传的压缩包为空文件');

    const ext = path.extname(zip.filename).toLowerCase();
    if (ext !== '' && ext !== '.zip') {
      throw ApiError.badRequest(`只支持 .zip 格式的模组包，收到的是 ${ext}`);
    }

    const poster = files.poster ?? null;
    if (poster && poster.size > config.upload.maxPosterBytes) {
      await removeTempFiles(zip.path, poster.path);
      throw ApiError.payloadTooLarge(
        `封面图超过 ${Math.floor(config.upload.maxPosterBytes / 1024 / 1024)}MB`,
      );
    }

    const input: PublishInput = {
      userId: req.user.id,
      // 作者以账号为准写进包内 mod.info，避免作者栏为空或与站点显示不一致
      authorName: req.user.username,
      zip,
      poster,
      meta: {
        name: str(fields, 'name'),
        summary: str(fields, 'summary'),
        description: str(fields, 'description'),
        category: str(fields, 'category'),
        tags: str(fields, 'tags'),
        depends: str(fields, 'depends'),
        gameVersion: str(fields, 'gameVersion'),
        version: str(fields, 'version'),
        changelog: str(fields, 'changelog'),
      },
      isNewVersion: modId !== null,
      modId: modId ?? undefined,
      isAdmin: req.user.role === 'admin',
    };

    try {
      const result = await publish(input);
      // 市场清单是客户端每次打开模组管理器都要拉的端点，发布后必须立刻失效
      clearMarketCache();
      return reply.code(modId === null ? 201 : 200).send(result);
    } catch (e) {
      // publish 内部已清理它拿到的临时文件；这里兜底防止异常路径泄漏
      await removeTempFiles(zip.path, poster?.path);
      throw e;
    }
  };

  app.post('/api/mods', { preHandler: authenticate }, async (req: FastifyRequest, reply: FastifyReply) => {
    return handle(req, reply, null);
  });

  app.post<{ Params: VersionParams }>(
    '/api/mods/:id/versions',
    { preHandler: authenticate },
    async (req, reply) => {
      return handle(req, reply, req.params.id);
    },
  );
}
