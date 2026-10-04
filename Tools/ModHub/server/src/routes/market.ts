import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { prisma } from '../db';
import { config } from '../config';
import { latestVersionString } from '../service/modService';
import { getMarketCache, setMarketCache } from '../service/marketCache';

/**
 * 市场清单兼容端点（游戏客户端直连）
 *
 * 协议详见 建设计划.md §2.4 / §8.5，两个易错点：
 *   1. 作者字段在协议里拼作 auther（历史拼写错误），必须原样输出，否则游戏内作者栏为空
 *   2. size 必须是数字（客户端是 long），不能用字符串
 *
 * 客户端还要读 url 字段并按 MakeUrl 规则拼下载地址：
 *   url 以 "/mod_list.txt" 结尾 → 截掉后拼 "/{id}@{version}.zip"
 * 所以这里把 url 写成  {APP_BASE_URL}/market/mod_list.txt，
 * 实际下载路径会是 {APP_BASE_URL}/market/{id}@{version}.zip —— download 路由两条路径都注册了。
 */

interface MarketQuery {
  category?: string;
  tag?: string;
  limit?: string;
}

async function buildMarketBody(query: MarketQuery): Promise<unknown> {
  const limitRaw = Number(query.limit ?? 200);
  const limit = Number.isFinite(limitRaw) ? Math.min(Math.max(Math.trunc(limitRaw), 1), 1000) : 200;

  const category = (query.category ?? '').trim();
  const tag = (query.tag ?? '').trim();

  const mods = await prisma.mod.findMany({
    where: {
      status: 'approved',
      ...(category ? { category } : {}),
      ...(tag ? { tags: { some: { tag } } } : {}),
    },
    include: {
      versions: true,
      tags: true,
      author: { select: { id: true, username: true, nickname: true } },
    },
    orderBy: { updatedAt: 'desc' },
    take: limit,
  });

  const mods_out = mods
    .map((mod) => {
      const version = latestVersionString(mod.versions);
      if (!version) return null;
      const versionRow = mod.versions.find((v) => v.version === version)!;
      return {
        id: mod.id,
        name: mod.name,
        version,
        size: versionRow.size,
        // ⚠ 协议里的历史拼写：auther
        auther: mod.author.nickname || mod.author.username,
        description: mod.summary || mod.description || '',
        poster: 'poster.jpg',
      };
    })
    .filter((v): v is NonNullable<typeof v> => v !== null);

  const indexUrl = `${config.appBaseUrl}/market/mod_list.txt`;
  return {
    name: config.market.name,
    url: indexUrl,
    mods: mods_out,
  };
}

async function marketHandler(req: FastifyRequest<{ Querystring: MarketQuery }>, reply: FastifyReply) {
  const key = JSON.stringify(req.query ?? {});
  const ttl = config.market.cacheTtlSec * 1000;

  const cached = getMarketCache(key, ttl);
  if (cached != null) {
    return reply.type('application/json').header('Cache-Control', 'public, max-age=60').send(cached);
  }

  const body = await buildMarketBody(req.query ?? {});
  setMarketCache(key, body);

  return reply
    .type('application/json')
    .header('Cache-Control', 'public, max-age=60')
    .send(body);
}

export default async function marketRoutes(app: FastifyInstance): Promise<void> {
  app.get('/market/mod_list.txt', marketHandler);
  // 别名：有些玩家手动填市场 URL 时会写成 /mods/mod_list.txt
  app.get('/mods/mod_list.txt', marketHandler);
}
