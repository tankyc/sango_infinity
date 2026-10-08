import type { FastifyInstance } from 'fastify';
import type { Prisma } from '@prisma/client';
import { prisma } from '../db';
import { ApiError } from '../errors';
import { config } from '../config';
import { authenticate } from '../middleware/auth';
import { readMultipart } from '../service/form';
import { removeTempFiles } from '../service/upload';
import { CATEGORIES, latestVersionString, savePosterFile } from '../service/modService';
import { clearMarketCache } from '../service/marketCache';

/**
 * 「我的创作」路由（对应 建设计划.md §4.4 你的创意工坊）
 *
 * 作者自己管理已发布内容所需的最小集合：
 *   GET    /api/mine            我发布的全部模组（含待审核/已下架）与汇总统计
 *   PATCH  /api/mods/:id        改名称 / 简介 / 正文 / 分类 / 标签 / 兼容版本 / 封面
 *   DELETE /api/mods/:id        下架（只改状态，不删数据，可随时恢复）
 *
 * 发新版本复用已有的 POST /api/mods/:id/versions（在 routes/publish.ts）。
 *
 * 权限口径：**作者本人**可管理自己的内容；管理员另外走 /api/admin/*（那里的能力更大，
 * 比如彻底删除）。作者只能在自己的内容上切 approved ⇄ hidden，
 * 不能自行把状态改成 pending/rejected —— 否则等于绕过审核。
 */

const AUTHOR_STATUSES = ['approved', 'hidden'] as const;

function str(fields: Record<string, string>, key: string): string | undefined {
  const v = fields[key];
  return v != null && v.trim() !== '' ? v.trim() : undefined;
}

function splitTags(value: string): string[] {
  return Array.from(
    new Set(
      value
        .split(/[,，;；]/)
        .map((s) => s.trim())
        .filter((s) => s !== '')
        .slice(0, 8)
        .map((s) => s.slice(0, 24)),
    ),
  );
}

/** 取目标模组并校验「当前用户是作者本人或管理员」 */
async function loadOwnedMod(id: string, userId: number, role: string) {
  const mod = await prisma.mod.findUnique({
    where: { id },
    include: { tags: true, versions: { select: { version: true, size: true, createdAt: true } } },
  });
  if (!mod) throw ApiError.notFound('模组不存在');
  if (mod.authorId !== userId && role !== 'admin') {
    throw ApiError.forbidden('只能管理自己发布的模组');
  }
  return mod;
}

export default async function mineRoutes(app: FastifyInstance): Promise<void> {
  /* ---------------- 我发布的模组 ---------------- */

  app.get('/api/mine', { preHandler: authenticate }, async (req, reply) => {
    const authorId = req.user!.id;

    const mods = await prisma.mod.findMany({
      where: { authorId },
      orderBy: { updatedAt: 'desc' },
      include: {
        tags: true,
        versions: { select: { version: true, size: true, createdAt: true }, orderBy: { createdAt: 'desc' } },
      },
    });

    const summary = {
      total: mods.length,
      approved: mods.filter((m) => m.status === 'approved').length,
      pending: mods.filter((m) => m.status === 'pending').length,
      hidden: mods.filter((m) => m.status === 'hidden').length,
      rejected: mods.filter((m) => m.status === 'rejected').length,
      downloads: mods.reduce((n, m) => n + m.downloads, 0),
      subscribers: mods.reduce((n, m) => n + m.subscribers, 0),
      views: mods.reduce((n, m) => n + m.views, 0),
    };

    return reply.send({
      summary,
      items: mods.map((m) => {
        const latest = latestVersionString(m.versions);
        const latestRow = m.versions.find((v) => v.version === latest);
        return {
          id: m.id,
          name: m.name,
          summary: m.summary,
          description: m.description,
          category: m.category,
          status: m.status,
          posterUrl: m.posterUrl === '' ? null : m.posterUrl,
          gameVersion: m.gameVersion,
          tags: m.tags.map((t) => t.tag),
          version: latest ?? null,
          size: latestRow?.size ?? 0,
          versionCount: m.versions.length,
          stats: {
            downloads: m.downloads,
            subscribers: m.subscribers,
            views: m.views,
            likes: m.likes,
          },
          createdAt: m.createdAt,
          updatedAt: m.updatedAt,
        };
      }),
    });
  });

  /* ---------------- 改信息 ---------------- */

  app.patch<{ Params: { id: string } }>(
    '/api/mods/:id',
    { preHandler: authenticate },
    async (req, reply) => {
      const user = req.user!;
      const mod = await loadOwnedMod((req.params.id ?? '').trim(), user.id, user.role);

      const { fields, files } = await readMultipart(req, {
        fileFields: { poster: config.upload.maxPosterBytes },
        maxFields: 20,
      });

      const data: Prisma.ModUpdateInput = {};
      let statusChanged = false;

      if (fields.name !== undefined) {
        const name = fields.name.trim();
        if (name === '') throw ApiError.badRequest('模组名称不能为空');
        data.name = name.slice(0, 80);
      }

      if (fields.summary !== undefined) data.summary = fields.summary.trim().slice(0, 200);
      if (fields.description !== undefined) data.description = fields.description;

      if (fields.category !== undefined) {
        const category = fields.category.trim().toLowerCase();
        if (!(CATEGORIES as readonly string[]).includes(category)) {
          throw ApiError.badRequest(`分类只能是 ${CATEGORIES.join(' / ')}`);
        }
        data.category = category;
      }

      if (fields.gameVersion !== undefined) data.gameVersion = fields.gameVersion.trim() || '*';

      if (fields.tags !== undefined) {
        const tags = splitTags(fields.tags);
        data.tags = { deleteMany: {}, create: tags.map((tag) => ({ tag })) };
      }

      // 作者只允许在「上架 / 下架」之间切换，不能把自己改成待审核或已拒绝
      if (fields.status !== undefined) {
        const status = fields.status.trim();
        if (!(AUTHOR_STATUSES as readonly string[]).includes(status)) {
          throw ApiError.badRequest('作者只能把状态改为 approved（上架）或 hidden（下架）');
        }
        if (status !== mod.status) statusChanged = true;
        data.status = status;
      }

      const poster = files.poster ?? null;
      if (poster) {
        try {
          data.posterUrl = await savePosterFile(poster);
        } finally {
          await removeTempFiles(poster.path);
        }
      }

      if (Object.keys(data).length === 0) throw ApiError.badRequest('没有需要更新的字段');

      const updated = await prisma.mod.update({
        where: { id: mod.id },
        data,
        include: { tags: true },
      });

      // 上架状态变化会影响客户端市场清单，必须立刻失效缓存
      if (statusChanged) clearMarketCache();

      return reply.send({
        mod: {
          id: updated.id,
          name: updated.name,
          summary: updated.summary,
          description: updated.description,
          category: updated.category,
          status: updated.status,
          posterUrl: updated.posterUrl,
          gameVersion: updated.gameVersion,
          tags: updated.tags.map((t) => t.tag),
          updatedAt: updated.updatedAt,
        },
      });
    },
  );

  /* ---------------- 下架（作者侧，软下架） ---------------- */

  app.delete<{ Params: { id: string } }>(
    '/api/mods/:id',
    { preHandler: authenticate },
    async (req, reply) => {
      const user = req.user!;
      const mod = await loadOwnedMod((req.params.id ?? '').trim(), user.id, user.role);

      if (mod.status === 'hidden') {
        return reply.send({ mod: { id: mod.id, status: mod.status }, changed: false });
      }

      const updated = await prisma.mod.update({
        where: { id: mod.id },
        data: { status: 'hidden' },
        select: { id: true, status: true },
      });
      clearMarketCache();

      return reply.send({ mod: updated, changed: true });
    },
  );
}
