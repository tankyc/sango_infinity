import './env';

import Fastify, {
  type FastifyError,
  type FastifyInstance,
  type FastifyReply,
  type FastifyRequest,
} from 'fastify';
import multipart from '@fastify/multipart';
import formbody from '@fastify/formbody';
import { config, ensureRuntimeDirs } from './config';
import { prisma } from './db';
import { ApiError } from './errors';
import healthRoutes from './routes/health';
import authRoutes from './routes/auth';
import marketRoutes from './routes/market';
import browseRoutes from './routes/browse';
import modRoutes from './routes/mods';
import commentRoutes from './routes/comments';
import publishRoutes from './routes/publish';
import downloadRoutes from './routes/download';
import staticRoutes from './routes/static';
import adminRoutes from './routes/admin';
import mineRoutes from './routes/mine';
import { ensureSuperAdmin } from './service/adminBootstrap';

const MB = 1024 * 1024;

export async function buildServer(): Promise<FastifyInstance> {
  ensureRuntimeDirs();

  const app = Fastify({
    logger: {
      level: config.isProduction ? 'warn' : 'info',
    },
    // zip 最大 200MB + 表单开销
    bodyLimit: config.upload.maxZipBytes + 16 * MB,
    trustProxy: true,
  });

  // x-www-form-urlencoded（部分老工具/脚本用）
  await app.register(formbody);
  // multipart/form-data（Unity 的 WWWForm 就是 multipart）
  await app.register(multipart, {
    limits: {
      fields: 60,
      files: 3,
      parts: 80,
      fileSize: config.upload.maxZipBytes,
    },
    throwFileSizeLimit: true,
  });

  app.setErrorHandler((error: FastifyError, req: FastifyRequest, reply: FastifyReply) => {
    if (error instanceof ApiError) {
      return reply.status(error.statusCode).send({
        error: error.message,
        code: error.code,
        details: error.details,
      });
    }

    const statusCode = (error as { statusCode?: number }).statusCode ?? 500;
    const code = (error as { code?: string }).code;

    if (statusCode === 413 || code === 'FST_ERR_CTP_BODY_TOO_LARGE') {
      return reply
        .status(413)
        .send({ error: `请求体超过大小上限 ${Math.floor(config.upload.maxZipBytes / MB)}MB`, code: 'payload_too_large' });
    }

    if (statusCode >= 400 && statusCode < 500) {
      return reply.status(statusCode).send({ error: error.message, code: code ?? 'bad_request' });
    }

    req.log.error({ err: error }, '未处理的服务器异常');
    return reply.status(500).send({ error: '服务器内部错误', code: 'internal_error' });
  });

  app.setNotFoundHandler((req: FastifyRequest, reply: FastifyReply) => {
    return reply.status(404).send({ error: `接口不存在: ${req.method} ${req.url}`, code: 'not_found' });
  });

  // 自举超级管理员：保证「服务起来了就一定有人能进后台」，无需先连数据库手工建号
  const adminBootstrap = await ensureSuperAdmin();
  if (adminBootstrap.action !== 'unchanged') {
    const actionText =
      adminBootstrap.action === 'created'
        ? '已创建'
        : adminBootstrap.action === 'promoted'
          ? '已提升为管理员'
          : '密码已重置';
    app.log.info(`超级管理员 ${adminBootstrap.username} ${actionText}`);
  }
  if (adminBootstrap.usingDefaultPassword) {
    app.log.warn('超级管理员仍在使用默认密码，请尽快修改 .env 中的 SUPER_ADMIN_PASSWORD 并重启服务');
  }

  // 路由：兼容端点在前（/login /register 与现有云存档服务同路径同名）
  await app.register(healthRoutes);
  await app.register(authRoutes);
  await app.register(marketRoutes);
  await app.register(browseRoutes);
  await app.register(modRoutes);
  await app.register(commentRoutes);
  await app.register(publishRoutes);
  await app.register(mineRoutes);
  await app.register(downloadRoutes);
  await app.register(staticRoutes);
  await app.register(adminRoutes);

  app.get('/', async () => ({
    service: 'Sango ModHub',
    marketIndex: `${config.appBaseUrl}/market/mod_list.txt`,
    health: `${config.appBaseUrl}/health`,
  }));

  app.addHook('onClose', async () => {
    await prisma.$disconnect();
  });

  return app;
}

async function main(): Promise<void> {
  const app = await buildServer();
  try {
    await app.listen({ port: config.port, host: config.host });
    app.log.info(
      `Sango ModHub 已启动 http://${config.host}:${config.port}  存储=${config.storage.driver}  CDN=${
        config.storage.cdnBaseUrl || '未启用（本地直出）'
      }`,
    );
  } catch (e) {
    app.log.error({ err: e }, '启动失败');
    process.exit(1);
  }
}

/* istanbul ignore next */
if (require.main === module) {
  void main();
}
