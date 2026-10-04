/**
 * 市场清单缓存
 *
 * /market/mod_list.txt 是客户端每次打开模组管理器都要拉的端点，缓存能显著减轻轻量服务器压力。
 * 发布新模组/新版本后必须立刻失效，否则客户端拉到的还是旧清单。
 */

interface CacheEntry {
  key: string;
  at: number;
  body: unknown;
}

let cache: CacheEntry | null = null;

export function getMarketCache(key: string, ttlMs: number): unknown | null {
  if (!cache) return null;
  if (cache.key !== key) return null;
  if (Date.now() - cache.at >= ttlMs) {
    cache = null;
    return null;
  }
  return cache.body;
}

export function setMarketCache(key: string, body: unknown): void {
  cache = { key, at: Date.now(), body };
}

export function clearMarketCache(): void {
  cache = null;
}
