import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

/**
 * 开发期把后端接口与静态资源统一代理到 ModHub 服务（默认 8081），
 * 前端一律使用相对路径请求。这样「开发 / 部署（Nginx 同域反代）」两种形态下
 * 代码完全一致，不需要任何 baseURL 切换逻辑。
 */
const API_TARGET = process.env.VITE_PROXY_TARGET ?? 'http://localhost:8081';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // 监听所有网卡：方便用手机连同一局域网的 IP 真机验证移动端布局
    host: true,
    proxy: Object.fromEntries(
      ['/api', '/posters', '/mods', '/market', '/login', '/register', '/me', '/health'].map((p) => [
        p,
        { target: API_TARGET, changeOrigin: true },
      ]),
    ),
  },
  build: {
    outDir: 'dist',
    sourcemap: false,
  },
});
