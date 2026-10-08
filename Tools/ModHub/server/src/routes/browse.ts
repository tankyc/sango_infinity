import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { optionalAuth } from '../middleware/auth';
import { ApiError } from '../errors';
import { browseMods, listFilterOptions, isSortKey, type SortKey } from '../service/browseService';

/**
 * 浏览路由（对应 建设计划.md §4.1 创意工坊首页）
 *
 *   GET /api/browse   筛选 + 排序 + 游标分页
 *   GET /api/filters  左侧筛选栏数据（类型计数 / 热门标签 / 游戏版本）
 *
 * 两个接口都允许匿名访问；带 Bearer Token 时沿用可选鉴权，
 * 便于后续在列表上直接标出「已订阅 / 有更新」状态而不用额外请求。
 */

/** ?tag=a&tag=b 与 ?tag[]=a&tag[]=b 都要兼容（不同前端库的数组序列化风格不同） */
function readArray(raw: unknown): string[] {
  if (raw == null) return [];
  if (Array.isArray(raw)) return raw.map((v) => String(v));
  return String(raw)
    .split(',')
    .map((s) => s.trim())
    .filter((s) => s !== '');
}

interface BrowseQs {
  cursor?: string;
  limit?: string | number;
  q?: string;
  category?: string;
  gameVersion?: string;
  sort?: string;
  tag?: unknown;
  'tag[]'?: unknown;
}

export default async function browseRoutes(app: FastifyInstance): Promise<void> {
  app.get<{ Querystring: BrowseQs }>(
    '/api/browse',
    { preHandler: optionalAuth },
    async (req, reply) => {
      const qs = req.query ?? {};

      const rawSort = (qs.sort ?? '').trim();
      if (rawSort !== '' && !isSortKey(rawSort)) {
        throw ApiError.badRequest(`不支持的排序方式: ${rawSort}（可选 hot / new / updated / trending）`);
      }

      const limitRaw = qs.limit == null || qs.limit === '' ? undefined : Number(qs.limit);
      if (limitRaw != null && !Number.isFinite(limitRaw)) {
        throw ApiError.badRequest('limit 必须是数字');
      }

      const result = await browseMods({
        cursor: (qs.cursor ?? '').trim() || undefined,
        limit: limitRaw,
        q: qs.q,
        category: qs.category,
        gameVersion: qs.gameVersion,
        sort: (rawSort === '' ? undefined : rawSort) as SortKey | undefined,
        tags: [...readArray(qs.tag), ...readArray(qs['tag[]'])],
      });

      return reply.send(result);
    },
  );

  app.get('/api/filters', async (_req: FastifyRequest, reply: FastifyReply) => {
    return reply.send(await listFilterOptions());
  });
}
