/**
 * 环境变量加载
 * 必须最先执行：config.ts 在模块加载期就会读取 process.env。
 */
import dotenv from 'dotenv';

dotenv.config();

export {};
