import { hashSync } from 'bcryptjs';
import { prisma } from '../db';
import { config } from '../config';

/**
 * 超级管理员自举
 *
 * 为什么放在服务启动时而不是命令行脚本：全新部署（尤其是只给了云服务器的场景）
 * 不该要求运维先 SSH 进来执行一条「建管理员」的命令，服务自己保证「至少有一个可用的管理员账号」
 * 才是最省事的做法。整个流程是幂等的，重复启动不会重复创建。
 *
 * 行为：
 *   1. 账号不存在 → 创建，角色 admin
 *   2. 账号存在但不是 admin → 提升为 admin（例如早前用普通账号占用了该用户名）
 *   3. SUPER_ADMIN_FORCE_PASSWORD=true → 顺带把密码重置为配置值（忘记密码时的应急手段）
 *
 * 安全前提：注册接口会拒绝占用该保留用户名（见 routes/auth.ts），
 * 否则任何人都能抢注超级管理员名字、等着下次重启被自动提权。
 */

const BCRYPT_ROUNDS = 10;

export type BootstrapAction = 'created' | 'promoted' | 'password-reset' | 'unchanged';

export interface BootstrapResult {
  username: string;
  action: BootstrapAction;
  /** 是否仍在使用默认密码（用于启动时提醒） */
  usingDefaultPassword: boolean;
}

/** 默认密码：仅在未显式配置 SUPER_ADMIN_PASSWORD 时使用，启动日志会提示尽快修改 */
const DEFAULT_SUPER_PASSWORD = 'sango@admin';

export async function ensureSuperAdmin(): Promise<BootstrapResult> {
  const username = config.admin.superUsername;
  const usingDefaultPassword = config.admin.superPassword === DEFAULT_SUPER_PASSWORD;
  const existing = await prisma.user.findUnique({ where: { username } });

  if (!existing) {
    await prisma.user.create({
      data: {
        username,
        password: hashSync(config.admin.superPassword, BCRYPT_ROUNDS),
        nickname: '超级管理员',
        role: 'admin',
      },
    });
    return { username, action: 'created', usingDefaultPassword };
  }

  const needPromote = existing.role !== 'admin';
  const needPassword = config.admin.forcePassword;

  if (needPromote || needPassword) {
    await prisma.user.update({
      where: { id: existing.id },
      data: {
        ...(needPromote ? { role: 'admin' } : {}),
        ...(needPassword ? { password: hashSync(config.admin.superPassword, BCRYPT_ROUNDS) } : {}),
      },
    });
    return {
      username,
      action: needPromote ? 'promoted' : 'password-reset',
      usingDefaultPassword,
    };
  }

  return { username, action: 'unchanged', usingDefaultPassword };
}
