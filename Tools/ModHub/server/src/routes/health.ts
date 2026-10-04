import type { FastifyInstance } from 'fastify';
import { config } from '../config';
import { storage } from '../service/storage';

/** 健康检查与运行形态自检 */
export default async function healthRoutes(app: FastifyInstance): Promise<void> {
  app.get('/health', async () => {
    return {
      ok: true,
      service: 'sango-modhub',
      env: config.nodeEnv,
      storage: config.storage.driver,
      /** CDN 是否已启用（启用后下载会 302 跳转，不占轻量服务器带宽） */
      cdn: config.storage.cdnBaseUrl || null,
      time: new Date().toISOString(),
    };
  });

  app.get('/health/storage', async () => {
    let reachable = true;
    let message = 'ok';
    try {
      await storage.size('__healthcheck__');
    } catch (e) {
      reachable = false;
      message = (e as Error).message;
    }
    return {
      driver: config.storage.driver,
      cdn: config.storage.cdnBaseUrl || null,
      reachable,
      message,
    };
  });
}
