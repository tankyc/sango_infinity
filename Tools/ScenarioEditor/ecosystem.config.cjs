/**
 * PM2 进程配置
 *
 * 由 deploy/ 里的部署脚本复制到安装目录后调用；也可手动：
 *   pm2 start ecosystem.config.cjs
 *
 * 本文件放在 Tools/ScenarioEditor/ 下，因此 __dirname 就是 TOOL_ROOT
 * （后端 server/lib/paths.js 用同样的方式上溯两级定位工程根）。
 *
 * 注意 cwd 必须指向 server 目录：后端用「当前工作目录」做基准解析相对路径，
 * 且 server/package.json 声明了 CommonJS 作用域，脱离该目录启动会改变模块语义。
 */
const path = require('node:path');

const TOOL_DIR = __dirname;

module.exports = {
  apps: [
    {
      name: 'sango-scenario',
      cwd: path.join(TOOL_DIR, 'server'),
      script: 'index.js',
      instances: 1,
      exec_mode: 'fork',
      // 剧本整份读写 + 公共数据差量计算，给足内存余量并在超限时自动重启
      max_memory_restart: '600M',
      autorestart: true,
      // 启动 10 秒内崩溃超过 5 次则停止重启，避免配置错误时无限重启打满 CPU
      min_uptime: '10s',
      max_restarts: 5,
      env: {
        NODE_ENV: 'production',
        PORT: 3011,
        // 线上多人共用同一份服务：游客只读，登录用户各写自己的工作区
        // （工作区落在 server/workspaces/<用户名>/ 下）
        WORKSPACE_MODE: 'multi',
        // 同机武将库（nginx 把 /personlib/ 剥掉前缀后转发到 3001）。
        // 这里直接连后端端口，绕过 nginx 少一跳，也避免依赖 Host 头。
        LIBRARY_API: 'http://127.0.0.1:3001',
      },
      out_file: path.join(TOOL_DIR, 'logs', 'scenario-out.log'),
      error_file: path.join(TOOL_DIR, 'logs', 'scenario-err.log'),
      merge_logs: true,
      time: true,
    },
  ],
};
