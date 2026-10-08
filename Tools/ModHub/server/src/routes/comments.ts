import type { FastifyInstance } from 'fastify';
import { prisma } from '../db';
import { ApiError } from '../errors';
import { authenticate, optionalAuth } from '../middleware/auth';
import type { AuthUser } from '../middleware/auth';

/**
 * 模组留言（Comment）
 *
 * 几个取舍：
 *   1. 只做两层（顶层留言 + 回复）。更深的嵌套在详情页窄栏里读起来很痛苦，
 *      而"回复某条回复"在体验上等价于"继续在那条顶层下说" —— 所以回复的回复
 *      会被挂到同一条顶层留言下（见 POST 里的处理）。
 *   2. 翻页用 keyset（cursor = 上一页最后一条的 id），不用 offset：
 *      留言是持续新增的，offset 分页会让用户在翻页时看到重复内容。
 *   3. 最新在前 —— 反馈场景下"最近有没有人提到这个问题"比"最早的留言"更有用。
 */

const MAX_CONTENT_LEN = 500;
/** 同一用户在同一模组下的发言间隔。正常讨论不会触发，纯粹挡一下机器刷屏 */
const POST_COOLDOWN_MS = 10_000;
const DEFAULT_PAGE_SIZE = 20;
const MAX_PAGE_SIZE = 50;

const USER_SELECT = { id: true, username: true, nickname: true, avatar: true, role: true } as const;

interface ModParams {
  id: string;
}

interface CommentParams {
  id: string;
}

interface ListQuery {
  cursor?: string;
  limit?: string;
}

type SelectedUser = { id: number; username: string; nickname: string | null; avatar: string | null; role: string };
type CommentRow = {
  id: number;
  content: string;
  createdAt: Date;
  userId: number;
  user: SelectedUser;
  /** 点赞数走实时统计（原因见 schema 中 CommentLike 的注释） */
  _count?: { likes: number };
};

/** 新留言必然没人点赞，复用同一个空集合避免每次 new */
const EMPTY_LIKED = new Set<number>();

/** 能删这条留言的人：留言本人、该模组的作者、管理员 */
function canDelete(viewer: AuthUser | undefined, commentUserId: number, modAuthorId: number): boolean {
  if (!viewer) return false;
  return viewer.id === commentUserId || viewer.id === modAuthorId || viewer.role === 'admin';
}

function toView(
  row: CommentRow,
  modAuthorId: number,
  viewer: AuthUser | undefined,
  likedIds: Set<number>,
) {
  return {
    id: row.id,
    content: row.content,
    createdAt: row.createdAt,
    user: {
      id: row.user.id,
      username: row.user.username,
      nickname: row.user.nickname ?? row.user.username,
      avatar: row.user.avatar,
      role: row.user.role,
    },
    canDelete: canDelete(viewer, row.userId, modAuthorId),
    likes: row._count?.likes ?? 0,
    likedByMe: likedIds.has(row.id),
  };
}

/** 校验留言存在且其所属模组未下架（点赞与删除共用） */
async function resolveCommentId(raw: string): Promise<number> {
  const id = Number(raw);
  if (!Number.isInteger(id) || id <= 0) throw ApiError.badRequest('留言 id 不合法');

  const comment = await prisma.comment.findUnique({
    where: { id },
    select: { id: true, mod: { select: { status: true } } },
  });
  if (!comment || comment.mod.status === 'hidden') throw ApiError.notFound('留言不存在');
  return comment.id;
}

function clampLimit(raw: string | undefined): number {
  const n = Number(raw);
  if (!Number.isFinite(n)) return DEFAULT_PAGE_SIZE;
  return Math.min(MAX_PAGE_SIZE, Math.max(1, Math.floor(n)));
}

export default async function commentRoutes(app: FastifyInstance): Promise<void> {
  // ---------- 留言列表 ----------
  app.get<{ Params: ModParams; Querystring: ListQuery }>(
    '/api/mods/:id/comments',
    { preHandler: optionalAuth },
    async (req, reply) => {
      const modId = (req.params.id ?? '').trim();
      const mod = await prisma.mod.findUnique({
        where: { id: modId },
        select: { id: true, status: true, authorId: true },
      });
      if (!mod || mod.status === 'hidden') throw ApiError.notFound('模组不存在');

      const limit = clampLimit(req.query.limit);
      const cursor = Number(req.query.cursor);
      const useCursor = Number.isInteger(cursor) && cursor > 0;

      // 顶层留言：多取一条用来判断还有没有下一页
      const topRows = await prisma.comment.findMany({
        where: { modId, parentId: null, ...(useCursor ? { id: { lt: cursor } } : {}) },
        orderBy: { id: 'desc' },
        take: limit + 1,
        include: { user: { select: USER_SELECT }, _count: { select: { likes: true } } },
      });
      const hasMore = topRows.length > limit;
      const tops = hasMore ? topRows.slice(0, limit) : topRows;

      // 这一页顶层留言的回复一次性取出来，避免每条留言查一次
      const replies = tops.length
        ? await prisma.comment.findMany({
            where: { parentId: { in: tops.map((c) => c.id) } },
            orderBy: { id: 'asc' },
            include: { user: { select: USER_SELECT }, _count: { select: { likes: true } } },
          })
        : [];

      // 当前用户给这一页里哪些留言点过赞 —— 一次查完，避免逐条判断
      const likedIds = new Set<number>();
      if (req.user) {
        const pageIds = [...tops.map((c) => c.id), ...replies.map((r) => r.id)];
        if (pageIds.length > 0) {
          const mine = await prisma.commentLike.findMany({
            where: { userId: req.user.id, commentId: { in: pageIds } },
            select: { commentId: true },
          });
          for (const row of mine) likedIds.add(row.commentId);
        }
      }

      const total = await prisma.comment.count({ where: { modId } });

      return reply.send({
        items: tops.map((top) => ({
          ...toView(top, mod.authorId, req.user, likedIds),
          replies: replies
            .filter((r) => r.parentId === top.id)
            .map((r) => toView(r, mod.authorId, req.user, likedIds)),
        })),
        nextCursor: hasMore ? tops[tops.length - 1].id : null,
        total,
        canPost: req.user != null,
      });
    },
  );

  // ---------- 发表留言 / 回复 ----------
  app.post<{ Params: ModParams; Body: { content?: string; parentId?: number } }>(
    '/api/mods/:id/comments',
    { preHandler: authenticate },
    async (req, reply) => {
      const viewer = req.user;
      if (!viewer) throw ApiError.unauthorized();

      const modId = (req.params.id ?? '').trim();
      const mod = await prisma.mod.findUnique({
        where: { id: modId },
        select: { id: true, status: true, authorId: true },
      });
      if (!mod || mod.status === 'hidden') throw ApiError.notFound('模组不存在');

      const content = String(req.body?.content ?? '').trim();
      if (content === '') throw ApiError.badRequest('留言内容不能为空');
      if (content.length > MAX_CONTENT_LEN) {
        throw ApiError.badRequest(`留言不能超过 ${MAX_CONTENT_LEN} 个字（当前 ${content.length}）`);
      }

      // 防刷：同一模组下同一用户的发言间隔
      const last = await prisma.comment.findFirst({
        where: { modId, userId: viewer.id },
        orderBy: { id: 'desc' },
        select: { createdAt: true },
      });
      if (last && Date.now() - last.createdAt.getTime() < POST_COOLDOWN_MS) {
        throw new ApiError(429, '发言太快了，请稍后再试', 'rate_limited');
      }

      // 回复：校验目标属于同一模组，并且始终挂在顶层留言下（只保留两层）
      let parentId: number | null = null;
      const rawParent = Number(req.body?.parentId);
      if (Number.isInteger(rawParent) && rawParent > 0) {
        const parent = await prisma.comment.findUnique({
          where: { id: rawParent },
          select: { id: true, modId: true, parentId: true },
        });
        if (!parent || parent.modId !== modId) throw ApiError.badRequest('要回复的留言不存在');
        parentId = parent.parentId ?? parent.id;
      }

      const created = await prisma.comment.create({
        data: { modId, userId: viewer.id, parentId, content },
        include: { user: { select: USER_SELECT } },
      });

      // 刚发出来的留言必然还没人点赞
      return reply.code(201).send({ comment: toView(created, mod.authorId, viewer, EMPTY_LIKED) });
    },
  );

  // ---------- 点赞 ----------
  app.post<{ Params: CommentParams }>(
    '/api/comments/:id/like',
    { preHandler: authenticate },
    async (req, reply) => {
      const viewer = req.user;
      if (!viewer) throw ApiError.unauthorized();

      const id = await resolveCommentId(req.params.id ?? '');
      // upsert：重复点赞是幂等的，不报错也不会重复计数
      await prisma.commentLike.upsert({
        where: { commentId_userId: { commentId: id, userId: viewer.id } },
        create: { commentId: id, userId: viewer.id },
        update: {},
      });

      const likes = await prisma.commentLike.count({ where: { commentId: id } });
      return reply.send({ liked: true, likes });
    },
  );

  // ---------- 取消点赞 ----------
  app.delete<{ Params: CommentParams }>(
    '/api/comments/:id/like',
    { preHandler: authenticate },
    async (req, reply) => {
      const viewer = req.user;
      if (!viewer) throw ApiError.unauthorized();

      const id = await resolveCommentId(req.params.id ?? '');
      // deleteMany 而不是 delete：没点过赞也应当安全返回，不能抛错
      await prisma.commentLike.deleteMany({ where: { commentId: id, userId: viewer.id } });

      const likes = await prisma.commentLike.count({ where: { commentId: id } });
      return reply.send({ liked: false, likes });
    },
  );

  // ---------- 删除留言 ----------
  app.delete<{ Params: CommentParams }>(
    '/api/comments/:id',
    { preHandler: authenticate },
    async (req, reply) => {
      const viewer = req.user;
      if (!viewer) throw ApiError.unauthorized();

      const id = Number(req.params.id);
      if (!Number.isInteger(id) || id <= 0) throw ApiError.badRequest('留言 id 不合法');

      const comment = await prisma.comment.findUnique({
        where: { id },
        include: { mod: { select: { authorId: true } } },
      });
      if (!comment) throw ApiError.notFound('留言不存在');
      if (!canDelete(viewer, comment.userId, comment.mod.authorId)) {
        throw ApiError.forbidden('只能删除自己的留言');
      }

      // parentId 只是普通列、不是外键，删顶层留言时不会自动带走回复，这里显式清理
      await prisma.$transaction([
        prisma.comment.deleteMany({ where: { parentId: id } }),
        prisma.comment.delete({ where: { id } }),
      ]);

      return reply.send({ deleted: true, id });
    },
  );
}
