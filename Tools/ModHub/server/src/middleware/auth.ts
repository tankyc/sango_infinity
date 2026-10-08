import type { FastifyReply, FastifyRequest } from 'fastify';
import jwt, { type SignOptions } from 'jsonwebtoken';
import { config } from '../config';
import { prisma } from '../db';
import { ApiError } from '../errors';

/**
 * Bearer JWT 鉴权
 *
 * 登录态与现有云存档服务打通：CloudSaveClient 把 token 存进 PlayerPrefs["CloudSave_Token"]，
 * 并通过 Authorization: Bearer <token> 发起请求，这里直接沿用。
 */

export interface AuthUser {
  id: number;
  username: string;
  nickname: string;
  avatar: string | null;
  role: string;
}

declare module 'fastify' {
  interface FastifyRequest {
    user?: AuthUser;
  }
}

interface TokenPayload {
  uid: number;
  username: string;
  nickname?: string;
  role?: string;
}

export function signToken(user: {
  id: number;
  username: string;
  nickname?: string | null;
  role?: string;
}): string {
  const payload: TokenPayload = {
    uid: user.id,
    username: user.username,
    nickname: user.nickname ?? undefined,
    role: user.role ?? 'user',
  };
  return jwt.sign(payload, config.jwt.secret, {
    expiresIn: config.jwt.expiresIn as SignOptions['expiresIn'],
  });
}

function parseAuthHeader(req: FastifyRequest): AuthUser | null {
  const header = req.headers.authorization;
  if (!header) return null;

  const [scheme, token] = header.split(' ');
  if (!token || scheme.toLowerCase() !== 'bearer') return null;

  try {
    const payload = jwt.verify(token.trim(), config.jwt.secret) as TokenPayload;
    if (typeof payload?.uid !== 'number') return null;
    return {
      id: payload.uid,
      username: payload.username,
      nickname: payload.nickname ?? payload.username,
      avatar: null,
      role: payload.role ?? 'user',
    };
  } catch {
    return null;
  }
}

/** 可选鉴权：有 token 就解析，没有也放行（用于"我已订阅"这类要按用户定制的公开接口） */
export function optionalAuth(req: FastifyRequest, _reply: FastifyReply, done: (err?: Error) => void): void {
  const user = parseAuthHeader(req);
  if (user) req.user = user;
  done();
}

/** 要求当前用户是本人或管理员 */
export function requireSelfOrAdmin(userId: number, user: AuthUser | undefined): void {
  if (!user) throw ApiError.unauthorized();
  if (user.id !== userId && user.role !== 'admin') throw ApiError.forbidden();
}

/**
 * 由 Bearer Token 解析出「当前仍然有效的用户」
 *
 * 与 optionalAuth 的关键差别是**回查数据库**，不直接信任 JWT 载荷里的角色与状态：
 * 否则封禁账号、删除账号、撤销管理员之后，对方手上的旧 token 在过期前（默认 30 天）
 * 依然畅通无阻。后台权限尤其不能有这种窗口期。
 *
 * 代价是每次鉴权多一次主键查询 —— 鉴权只发生在写操作与后台入口上，频率低，完全值得；
 * 公开只读接口（浏览、详情）仍走轻量的 optionalAuth。
 */
async function resolveAuthUser(req: FastifyRequest): Promise<AuthUser | null> {
  const fromToken = parseAuthHeader(req);
  if (!fromToken) return null;

  const row = await prisma.user.findUnique({
    where: { id: fromToken.id },
    select: { id: true, username: true, nickname: true, avatar: true, role: true, disabled: true },
  });
  // 账号不存在（已删除）或已封禁，一律视为未登录
  if (!row || row.disabled) return null;

  return {
    id: row.id,
    username: row.username,
    nickname: row.nickname ?? row.username,
    avatar: row.avatar,
    role: row.role,
  };
}

/** 强制鉴权（写操作入口：发布、改信息、我的创作等） */
export async function authenticate(req: FastifyRequest, _reply: FastifyReply): Promise<void> {
  const user = await resolveAuthUser(req);
  if (!user) {
    throw new ApiError(401, '缺少或无效的 Bearer Token，请重新登录', 'unauthorized');
  }
  req.user = user;
}

/**
 * 强制要求管理员身份（管理后台的所有接口都挂这个）
 *
 * 同样是回查数据库的鉴权：撤销管理员权限、封禁或删除账号后**立即生效**，
 * 不需要等 token 过期，也不需要对方重新登录。
 */
export async function requireAdmin(req: FastifyRequest, _reply: FastifyReply): Promise<void> {
  const user = await resolveAuthUser(req);
  if (!user) {
    throw new ApiError(401, '缺少或无效的 Bearer Token，请重新登录', 'unauthorized');
  }
  if (user.role !== 'admin') {
    throw new ApiError(403, '需要管理员权限', 'forbidden');
  }
  req.user = user;
}
