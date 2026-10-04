import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { compareSync, hashSync } from 'bcryptjs';
import { Prisma } from '@prisma/client';
import { prisma } from '../db';
import { ApiError } from '../errors';
import { config } from '../config';
import { authenticate, signToken } from '../middleware/auth';
import { readCredentials } from '../service/form';

/**
 * 认证路由
 *
 * 刻意沿用现有云存档服务的契约，保证游戏侧 CloudSaveClient 一行不用改：
 *   POST /login    WWWForm(username, password) → { token, user:{ id, username } }
 *   POST /register 同上，注册成功即自动登录
 *   GET  /me       Bearer Token → 当前用户
 */

const USERNAME_PATTERN = /^[A-Za-z0-9_.-]{3,32}$/;
const BCRYPT_ROUNDS = 10;

export function publicUser(user: {
  id: number;
  username: string;
  nickname: string | null;
  avatar: string | null;
  role: string;
}) {
  return {
    id: user.id,
    username: user.username,
    nickname: user.nickname ?? user.username,
    avatar: user.avatar,
    role: user.role,
  };
}

function issueTokenReply(reply: FastifyReply, user: {
  id: number;
  username: string;
  nickname: string | null;
  avatar: string | null;
  role: string;
}) {
  return reply.send({
    token: signToken(user),
    user: { id: user.id, username: user.username, nickname: user.nickname, avatar: user.avatar, role: user.role },
  });
}

export default async function authRoutes(app: FastifyInstance): Promise<void> {
  // 登录
  app.post('/login', async (req: FastifyRequest, reply: FastifyReply) => {
    const { username, password } = await readCredentials(req);

    const user = await prisma.user.findUnique({ where: { username } });
    if (!user || !compareSync(password, user.password)) {
      throw ApiError.unauthorized('用户名或密码错误');
    }

    // 被封禁的账号：明确告知，避免玩家以为是自己密码错而反复尝试
    if (user.disabled) {
      throw ApiError.forbidden('账号已被管理员封禁，如有疑问请到工坊页面联系管理员');
    }

    // 记录最后登录时间（纯统计用途，失败不影响登录）
    void prisma.user
      .update({ where: { id: user.id }, data: { lastLoginAt: new Date() } })
      .catch(() => undefined);

    return issueTokenReply(reply, user);
  });

  // 注册（成功后直接返回 token，行为与云存档服务一致）
  app.post('/register', async (req: FastifyRequest, reply: FastifyReply) => {
    const { username, password } = await readCredentials(req);

    if (!USERNAME_PATTERN.test(username)) {
      throw ApiError.badRequest('用户名需为 3–32 位字母、数字、下划线、点或短横线');
    }
    if (password.length < 6 || password.length > 128) {
      throw ApiError.badRequest('密码长度需为 6–128 位');
    }

    // 保留用户名：服务启动时会把该名字的账号自动提升为管理员，
    // 若允许注册，任何人都能抢注这个名字、等着下次重启被自动提权。
    if (username.toLowerCase() === config.admin.superUsername.toLowerCase()) {
      throw ApiError.conflict('该用户名为系统保留，请换一个');
    }

    const existed = await prisma.user.findUnique({ where: { username } });
    if (existed) {
      throw ApiError.conflict('用户名已被注册');
    }

    try {
      const user = await prisma.user.create({
        data: {
          username,
          password: hashSync(password, BCRYPT_ROUNDS),
          nickname: username,
        },
      });
      return issueTokenReply(reply, user);
    } catch (e) {
      // 并发注册时的唯一键冲突
      if (e instanceof Prisma.PrismaClientKnownRequestError && e.code === 'P2002') {
        throw ApiError.conflict('用户名已被注册');
      }
      throw e;
    }
  });

  // 当前登录用户
  app.get('/me', { preHandler: authenticate }, async (req: FastifyRequest, reply: FastifyReply) => {
    if (!req.user) throw ApiError.unauthorized();
    const user = await prisma.user.findUnique({
      where: { id: req.user.id },
      select: { id: true, username: true, nickname: true, avatar: true, role: true },
    });
    if (!user) throw ApiError.unauthorized('账号不存在或已被删除');
    return reply.send({ user: publicUser(user) });
  });
}
