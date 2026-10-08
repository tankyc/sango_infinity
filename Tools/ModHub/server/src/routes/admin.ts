import type { FastifyInstance } from 'fastify';
import type { Prisma } from '@prisma/client';
import { hashSync } from 'bcryptjs';
import { prisma } from '../db';
import { ApiError } from '../errors';
import { requireAdmin } from '../middleware/auth';
import { CATEGORIES, latestVersionString, removeModFiles } from '../service/modService';
import { clearMarketCache } from '../service/marketCache';
import { readMultipart } from '../service/form';
import { publicUser } from './auth';

/**
 * 管理后台接口（对应 建设计划.md §8 管理侧 API、§3.0 的审核机制）
 *
 * 统一约定：
 *   1. 本文件内所有路由都要求 admin 角色 —— 用 plugin 级 preHandler 一次性挂上，
 *      避免逐个路由漏挂鉴权（这是后台接口最常见的事故来源）。
 *   2. 列表一律 offset 分页：后台数据量小，需要「共 N 条 / 跳到第几页」这类信息，
 *      而游标分页给不了；与前台浏览页的 keyset 分页刻意不同。
 *   3. 任何会改变「市场清单可见性」的操作（审核、下架、删除）都要清市场缓存，
 *      否则客户端要等 TTL 过期才能看到变化。
 *   4. 危险操作一律加护栏：不能改自己的角色、不能删自己、不能封禁自己、
 *      不能把最后一个管理员降级或删掉。
 */

const ROLES = ['user', 'reviewer', 'admin'] as const;
const STATUSES = ['pending', 'approved', 'rejected', 'hidden'] as const;
const DEFAULT_PAGE_SIZE = 20;
const MAX_PAGE_SIZE = 100;

type Role = (typeof ROLES)[number];
type ModStatus = (typeof STATUSES)[number];

/* ----------------------------- 参数解析辅助 ----------------------------- */

function truthy(value: unknown): boolean {
  if (typeof value === 'boolean') return value;
  return ['1', 'true', 'yes', 'on'].includes(String(value ?? '').trim().toLowerCase());
}

function readInt(value: unknown, def: number, min: number, max: number): number {
  if (value == null || String(value).trim() === '') return def;
  const n = Number(value);
  if (!Number.isFinite(n)) return def;
  return Math.min(max, Math.max(min, Math.floor(n)));
}

function requireId(raw: string | undefined, label: string): number {
  const n = Number(raw);
  if (!Number.isInteger(n) || n <= 0) throw ApiError.badRequest(`${label}不合法`);
  return n;
}

function ensureOneOf<T extends string>(value: string, allowed: readonly T[], label: string): T {
  const v = value.trim();
  if (!(allowed as readonly string[]).includes(v)) {
    throw ApiError.badRequest(`${label}只能是 ${allowed.join(' / ')}`);
  }
  return v as T;
}

/** 标签输入：兼容「逗号分隔字符串」与「数组」，去重并限制长度 */
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

function pageParams(page: unknown, pageSize: unknown): { page: number; pageSize: number; skip: number; take: number } {
  const p = readInt(page, 1, 1, 100000);
  const size = readInt(pageSize, DEFAULT_PAGE_SIZE, 1, MAX_PAGE_SIZE);
  return { page: p, pageSize: size, skip: (p - 1) * size, take: size };
}

/* ----------------------------- 路由 ----------------------------- */

export default async function adminRoutes(app: FastifyInstance): Promise<void> {
  // 本插件内所有路由统一要求管理员，逐个路由再挂一遍反而容易漏
  app.addHook('preHandler', requireAdmin);

  /* ---------------- 概览 ---------------- */

  app.get('/api/admin/stats', async (_req, reply) => {
    const weekAgo = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000);

    const [
      userTotal,
      userAdmins,
      userDisabled,
      userRecent,
      modTotal,
      modPending,
      modApproved,
      modRejected,
      modHidden,
      modAgg,
      versionAgg,
      commentTotal,
      likeTotal,
      subscriptionTotal,
      latestUsers,
      pendingMods,
    ] = await Promise.all([
      prisma.user.count(),
      prisma.user.count({ where: { role: 'admin' } }),
      prisma.user.count({ where: { disabled: true } }),
      prisma.user.count({ where: { createdAt: { gte: weekAgo } } }),
      prisma.mod.count(),
      prisma.mod.count({ where: { status: 'pending' } }),
      prisma.mod.count({ where: { status: 'approved' } }),
      prisma.mod.count({ where: { status: 'rejected' } }),
      prisma.mod.count({ where: { status: 'hidden' } }),
      prisma.mod.aggregate({ _sum: { downloads: true, views: true, subscribers: true } }),
      prisma.modVersion.aggregate({ _sum: { size: true }, _count: { _all: true } }),
      prisma.comment.count(),
      prisma.like.count(),
      prisma.subscription.count(),
      prisma.user.findMany({
        orderBy: { createdAt: 'desc' },
        take: 5,
        select: { id: true, username: true, nickname: true, role: true, disabled: true, createdAt: true },
      }),
      prisma.mod.findMany({
        where: { status: 'pending' },
        orderBy: { createdAt: 'desc' },
        take: 5,
        select: {
          id: true,
          name: true,
          category: true,
          createdAt: true,
          author: { select: { id: true, username: true, nickname: true } },
        },
      }),
    ]);

    return reply.send({
      users: { total: userTotal, admins: userAdmins, disabled: userDisabled, recent7d: userRecent },
      mods: {
        total: modTotal,
        pending: modPending,
        approved: modApproved,
        rejected: modRejected,
        hidden: modHidden,
        versions: versionAgg._count._all,
        storageBytes: versionAgg._sum.size ?? 0,
      },
      engagement: {
        downloads: modAgg._sum.downloads ?? 0,
        views: modAgg._sum.views ?? 0,
        subscribers: modAgg._sum.subscribers ?? 0,
        comments: commentTotal,
        likes: likeTotal,
        subscriptions: subscriptionTotal,
      },
      recent: { users: latestUsers, pendingMods },
    });
  });

  /* ---------------- 账号管理 ---------------- */

  app.get<{ Querystring: { q?: string; role?: string; disabled?: string; sort?: string; page?: string; pageSize?: string } }>(
    '/api/admin/users',
    async (req, reply) => {
      const { q, role, disabled, sort } = req.query;
      const { page, pageSize, skip, take } = pageParams(req.query.page, req.query.pageSize);

      const keyword = (q ?? '').trim();
      const roleFilter = (role ?? '').trim();
      if (roleFilter !== '' && !(ROLES as readonly string[]).includes(roleFilter)) {
        throw ApiError.badRequest('role 参数不合法');
      }

      const where: Prisma.UserWhereInput = {
        ...(keyword === ''
          ? {}
          : { OR: [{ username: { contains: keyword } }, { nickname: { contains: keyword } }] }),
        ...(roleFilter === '' ? {} : { role: roleFilter }),
        ...(disabled == null || disabled === '' ? {} : { disabled: truthy(disabled) }),
      };

      const orderBy: Prisma.UserOrderByWithRelationInput =
        sort === 'lastLogin'
          ? { lastLoginAt: 'desc' }
          : sort === 'username'
            ? { username: 'asc' }
            : sort === 'mods'
              ? { mods: { _count: 'desc' } }
              : { createdAt: 'desc' };

      const [total, rows] = await Promise.all([
        prisma.user.count({ where }),
        prisma.user.findMany({
          where,
          orderBy,
          skip,
          take,
          select: {
            id: true,
            username: true,
            nickname: true,
            avatar: true,
            role: true,
            disabled: true,
            createdAt: true,
            lastLoginAt: true,
            _count: { select: { mods: true, subscriptions: true, comments: true } },
          },
        }),
      ]);

      return reply.send({
        total,
        page,
        pageSize,
        items: rows.map((u) => ({
          ...publicUser(u),
          disabled: u.disabled,
          createdAt: u.createdAt,
          lastLoginAt: u.lastLoginAt,
          modCount: u._count.mods,
          subscriptionCount: u._count.subscriptions,
          commentCount: u._count.comments,
        })),
      });
    },
  );

  app.patch<{ Params: { id: string } }>('/api/admin/users/:id', async (req, reply) => {
    const actor = req.user!;
    const targetId = requireId(req.params.id, '用户 id');

    const target = await prisma.user.findUnique({ where: { id: targetId } });
    if (!target) throw ApiError.notFound('用户不存在');

    const { fields } = await readMultipart(req, { fileFields: {}, maxFields: 10 });
    const data: Prisma.UserUpdateInput = {};

    // 角色：改自己、或把最后一个管理员降级，都拦下来
    if (fields.role !== undefined) {
      const role = ensureOneOf(fields.role, ROLES, '角色') as Role;
      if (target.id === actor.id) throw ApiError.badRequest('不能修改自己的角色');
      if (target.role === 'admin' && role !== 'admin') {
        const admins = await prisma.user.count({ where: { role: 'admin' } });
        if (admins <= 1) throw ApiError.badRequest('系统至少需要保留一个管理员');
      }
      data.role = role;
    }

    if (fields.nickname !== undefined) {
      data.nickname = fields.nickname.trim() === '' ? null : fields.nickname.trim().slice(0, 32);
    }

    // 封禁：同样不允许作用到自己身上，也不允许直接封禁管理员（需先降级）
    if (fields.disabled !== undefined) {
      const next = truthy(fields.disabled);
      if (target.id === actor.id) throw ApiError.badRequest('不能封禁自己');
      if (next && target.role === 'admin') throw ApiError.badRequest('请先取消其管理员角色，再执行封禁');
      data.disabled = next;
    }

    // 重置密码（管理员代改，用于帮玩家找回账号）
    if (fields.password !== undefined && fields.password !== '') {
      if (fields.password.length < 6 || fields.password.length > 128) {
        throw ApiError.badRequest('密码长度需为 6–128 位');
      }
      data.password = hashSync(fields.password, 10);
    }

    if (Object.keys(data).length === 0) throw ApiError.badRequest('没有需要更新的字段');

    const updated = await prisma.user.update({
      where: { id: targetId },
      data,
      select: {
        id: true,
        username: true,
        nickname: true,
        avatar: true,
        role: true,
        disabled: true,
        createdAt: true,
        lastLoginAt: true,
        _count: { select: { mods: true, subscriptions: true, comments: true } },
      },
    });

    return reply.send({
      user: {
        ...publicUser(updated),
        disabled: updated.disabled,
        createdAt: updated.createdAt,
        lastLoginAt: updated.lastLoginAt,
        modCount: updated._count.mods,
        subscriptionCount: updated._count.subscriptions,
        commentCount: updated._count.comments,
      },
    });
  });

  app.delete<{ Params: { id: string }; Querystring: { force?: string } }>(
    '/api/admin/users/:id',
    async (req, reply) => {
      const actor = req.user!;
      const targetId = requireId(req.params.id, '用户 id');
      if (targetId === actor.id) throw ApiError.badRequest('不能删除自己');

      const target = await prisma.user.findUnique({ where: { id: targetId } });
      if (!target) throw ApiError.notFound('用户不存在');
      if (target.role === 'admin') {
        const admins = await prisma.user.count({ where: { role: 'admin' } });
        if (admins <= 1) throw ApiError.badRequest('系统至少需要保留一个管理员');
      }

      const ownedMods = await prisma.mod.findMany({ where: { authorId: targetId }, select: { id: true } });
      const force = truthy(req.query.force);

      // 默认拒绝「连人带内容一起删」，强迫管理员先确认内容归属，避免误删社区资产
      if (ownedMods.length > 0 && !force) {
        throw ApiError.conflict(
          `该用户名下还有 ${ownedMods.length} 个模组，请先下架/删除这些模组，或确认后使用强制删除`,
        );
      }

      // 先删文件再删记录：数据库删得掉，磁盘上的 zip/封面不会自动消失
      for (const mod of ownedMods) {
        await removeModFiles(mod.id);
      }
      if (ownedMods.length > 0) {
        // 级联清掉这些模组的版本/标签/评论/订阅（schema 中已声明 onDelete: Cascade）
        await prisma.mod.deleteMany({ where: { authorId: targetId } });
      }
      await prisma.user.delete({ where: { id: targetId } });

      if (ownedMods.length > 0) clearMarketCache();
      return reply.send({ deleted: true, removedMods: ownedMods.length });
    },
  );

  /* ---------------- 内容管理 ---------------- */

  app.get<{
    Querystring: {
      q?: string;
      status?: string;
      category?: string;
      author?: string;
      sort?: string;
      page?: string;
      pageSize?: string;
    };
  }>('/api/admin/mods', async (req, reply) => {
    const { q, status, category, author, sort } = req.query;
    const { page, pageSize, skip, take } = pageParams(req.query.page, req.query.pageSize);

    const keyword = (q ?? '').trim();
    const statusFilter = (status ?? '').trim();
    const categoryFilter = (category ?? '').trim();
    const authorFilter = (author ?? '').trim();

    if (statusFilter !== '' && !(STATUSES as readonly string[]).includes(statusFilter)) {
      throw ApiError.badRequest('status 参数不合法');
    }
    if (categoryFilter !== '' && !(CATEGORIES as readonly string[]).includes(categoryFilter)) {
      throw ApiError.badRequest('category 参数不合法');
    }

    const where: Prisma.ModWhereInput = {
      ...(keyword === '' ? {} : { OR: [{ name: { contains: keyword } }, { id: { contains: keyword } }] }),
      ...(statusFilter === '' ? {} : { status: statusFilter }),
      ...(categoryFilter === '' ? {} : { category: categoryFilter }),
      ...(authorFilter === '' ? {} : { author: { username: authorFilter } }),
    };

    const orderBy: Prisma.ModOrderByWithRelationInput =
      sort === 'downloads'
        ? { downloads: 'desc' }
        : sort === 'views'
          ? { views: 'desc' }
          : sort === 'subscribers'
            ? { subscribers: 'desc' }
            : sort === 'created'
              ? { createdAt: 'desc' }
              : { updatedAt: 'desc' };

    const [total, rows] = await Promise.all([
      prisma.mod.count({ where }),
      prisma.mod.findMany({
        where,
        orderBy,
        skip,
        take,
        include: {
          tags: true,
          author: { select: { id: true, username: true, nickname: true, disabled: true } },
          versions: { select: { version: true, size: true, createdAt: true }, orderBy: { createdAt: 'desc' } },
        },
      }),
    ]);

    return reply.send({
      total,
      page,
      pageSize,
      items: rows.map((m) => {
        const latest = latestVersionString(m.versions);
        const latestRow = m.versions.find((v) => v.version === latest);
        return {
          id: m.id,
          name: m.name,
          summary: m.summary,
          category: m.category,
          status: m.status,
          posterUrl: m.posterUrl === '' ? null : m.posterUrl,
          gameVersion: m.gameVersion,
          tags: m.tags.map((t) => t.tag),
          author: m.author,
          version: latest ?? null,
          size: latestRow?.size ?? 0,
          versionCount: m.versions.length,
          stats: {
            downloads: m.downloads,
            views: m.views,
            subscribers: m.subscribers,
            likes: m.likes,
          },
          createdAt: m.createdAt,
          updatedAt: m.updatedAt,
        };
      }),
    });
  });

  app.patch<{ Params: { id: string } }>('/api/admin/mods/:id', async (req, reply) => {
    const id = (req.params.id ?? '').trim();
    const mod = await prisma.mod.findUnique({ where: { id } });
    if (!mod) throw ApiError.notFound('模组不存在');

    const { fields } = await readMultipart(req, { fileFields: {}, maxFields: 10 });
    const data: Prisma.ModUpdateInput = {};
    let statusChanged = false;

    if (fields.status !== undefined) {
      const status = ensureOneOf(fields.status, STATUSES, '状态') as ModStatus;
      if (status !== mod.status) statusChanged = true;
      data.status = status;
    }

    if (fields.category !== undefined) {
      data.category = ensureOneOf(fields.category, CATEGORIES, '分类');
    }

    if (fields.name !== undefined) {
      const name = fields.name.trim();
      if (name === '') throw ApiError.badRequest('模组名称不能为空');
      data.name = name.slice(0, 80);
    }

    if (fields.summary !== undefined) data.summary = fields.summary.trim().slice(0, 200);
    if (fields.description !== undefined) data.description = fields.description;
    if (fields.gameVersion !== undefined) data.gameVersion = fields.gameVersion.trim() || '*';

    if (fields.tags !== undefined) {
      const tags = splitTags(fields.tags);
      // 覆盖式更新：先清空再写入（标签数量少，不值得做增量 diff）
      data.tags = { deleteMany: {}, create: tags.map((tag) => ({ tag })) };
    }

    if (Object.keys(data).length === 0) throw ApiError.badRequest('没有需要更新的字段');

    const updated = await prisma.mod.update({
      where: { id },
      data,
      include: { tags: true },
    });

    // 上架状态变化会影响客户端市场清单，必须立刻失效缓存
    if (statusChanged) clearMarketCache();

    return reply.send({
      mod: { ...updated, tags: updated.tags.map((t) => t.tag) },
    });
  });

  app.delete<{ Params: { id: string }; Querystring: { force?: string } }>(
    '/api/admin/mods/:id',
    async (req, reply) => {
      const id = (req.params.id ?? '').trim();
      const mod = await prisma.mod.findUnique({ where: { id }, select: { id: true, status: true } });
      if (!mod) throw ApiError.notFound('模组不存在');

      // 默认只做「下架」：删除是不可逆的，必须显式加 force=1
      if (!truthy(req.query.force)) {
        const hidden = await prisma.mod.update({ where: { id }, data: { status: 'hidden' } });
        clearMarketCache();
        return reply.send({ hidden: true, mod: { id: hidden.id, status: hidden.status } });
      }

      await removeModFiles(id);
      await prisma.mod.delete({ where: { id } });
      clearMarketCache();
      return reply.send({ deleted: true, id });
    },
  );
}
