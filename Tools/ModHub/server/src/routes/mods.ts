import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { prisma } from '../db';
import { config } from '../config';
import { ApiError } from '../errors';
import { optionalAuth } from '../middleware/auth';
import { latestVersionString } from '../service/modService';
import { publicUser } from './auth';

/**
 * 模组详情（M2 网页详情页用；M1 先给出最小可用版本）
 */
interface DetailParams {
  id: string;
}

export default async function modRoutes(app: FastifyInstance): Promise<void> {
  app.get<{ Params: DetailParams }>(
    '/api/mods/:id',
    { preHandler: optionalAuth },
    async (req, reply) => {
      const id = (req.params.id ?? '').trim();
      const mod = await prisma.mod.findUnique({
        where: { id },
        include: {
          versions: { orderBy: { createdAt: 'desc' } },
          tags: true,
          author: { select: { id: true, username: true, nickname: true, avatar: true, role: true } },
        },
      });

      if (!mod || mod.status === 'hidden') throw ApiError.notFound('模组不存在');

      const isPrivileged =
        req.user != null && (req.user.id === mod.authorId || req.user.role === 'admin');
      if (mod.status !== 'approved' && !isPrivileged) {
        throw ApiError.notFound('模组不存在或未上架');
      }

      // 浏览数：异步累加，失败不影响主流程
      if (mod.status === 'approved') {
        void prisma.mod.update({ where: { id }, data: { views: { increment: 1 } } }).catch(() => undefined);
      }

      const latest = latestVersionString(mod.versions);
      const latestRow = mod.versions.find((v) => v.version === latest);
      // 留言数：详情页用来显示「留言 N 条」，让作者和玩家一眼看出有没有反馈在等
      const commentCount = await prisma.comment.count({ where: { modId: id } });

      return reply.send({
        id: mod.id,
        name: mod.name,
        summary: mod.summary,
        description: mod.description,
        category: mod.category,
        tags: mod.tags.map((t) => t.tag),
        posterUrl: mod.posterUrl,
        gameVersion: mod.gameVersion,
        status: mod.status,
        author: publicUser(mod.author),
        createdAt: mod.createdAt,
        updatedAt: mod.updatedAt,
        stats: {
          subscribers: mod.subscribers,
          likes: mod.likes,
          views: mod.views,
          downloads: mod.downloads,
        },
        latestVersion: latest ?? null,
        latestSize: latestRow?.size ?? 0,
        requiredItems: latestRow?.depends ? latestRow.depends.split(';').filter(Boolean) : [],
        commentCount,
        downloadUrl: latest ? `${config.appBaseUrl}/mods/${mod.id}@${latest}.zip` : null,
        pageUrl: `${config.appBaseUrl}/sharedfiles/filedetails/?id=${mod.id}`,
        versions: mod.versions.map((v) => ({
          version: v.version,
          size: v.size,
          changelog: v.changelog,
          depends: v.depends ? v.depends.split(';').filter(Boolean) : [],
          gameVersion: v.gameVersion,
          downloads: v.downloads,
          createdAt: v.createdAt,
          downloadUrl: `${config.appBaseUrl}/mods/${mod.id}@${v.version}.zip`,
        })),
      });
    },
  );
}
