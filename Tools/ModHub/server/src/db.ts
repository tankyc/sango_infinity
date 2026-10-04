import { PrismaClient } from '@prisma/client';
import { config } from './config';

/**
 * Prisma 单例
 * datasource url 显式由 config 提供，确保与 prisma CLI 迁移的库是同一个。
 */
export const prisma = new PrismaClient({
  datasources: { db: { url: config.databaseUrl } },
  log: config.nodeEnv === 'production' ? ['error'] : ['warn', 'error'],
});

export default prisma;
