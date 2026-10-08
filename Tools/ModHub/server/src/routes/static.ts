import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { ApiError } from '../errors';
import { storage, IMMUTABLE_CACHE } from '../service/storage';

/**
 * 静态文件（封面图）
 *
 * 封面以内容哈希命名 → 天然不可变 → 可永久缓存，且将来切 CDN 后 302 跳转即可，
 * 与 zip 下载共用同一套分发策略（见 建设计划.md §6.1）。
 */

const POSTER_PATTERN = /^[a-f0-9]{64}\.(png|jpe?g|webp)$/i;

const MIME: Record<string, string> = {
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
};

interface PosterParams {
  file: string;
}

export default async function staticRoutes(app: FastifyInstance): Promise<void> {
  app.get('/posters/:file', async (req: FastifyRequest<{ Params: PosterParams }>, reply: FastifyReply) => {
    const file = (req.params.file ?? '').toLowerCase();
    if (!POSTER_PATTERN.test(file)) {
      throw ApiError.notFound('封面不存在');
    }
    const key = `posters/${file}`;
    if ((await storage.size(key)) == null) {
      throw ApiError.notFound('封面不存在');
    }

    const cdnUrl = storage.publicUrl(key);
    if (cdnUrl) {
      return reply
        .code(302)
        .header('Location', cdnUrl)
        .header('Cache-Control', IMMUTABLE_CACHE)
        .send();
    }

    const result = await storage.read(key);
    const ext = file.slice(file.lastIndexOf('.'));
    return reply
      .header('Content-Type', MIME[ext] ?? 'application/octet-stream')
      .header('Content-Length', String(result.contentLength))
      .header('Cache-Control', IMMUTABLE_CACHE)
      .send(result.stream);
  });
}
