import path from "path"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

/**
 * 前端开发服务器配置
 *
 * - 开发阶段通过 proxy 把 /api 转发到本地剧本编辑器后端（默认 3009 端口），
 *   避免跨域问题；生产阶段由该后端统一托管构建产物。
 */
export default defineConfig({
  base: './',
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    port: 5273,
    // 监听所有网卡，便于用手机通过局域网 IP 访问同一份开发服务
    host: true,
    proxy: {
      "/api": {
        target: "http://localhost:3009",
        changeOrigin: true,
      },
    },
  },
  build: {
    // 剧本体积较大，放宽 chunk 体积告警阈值
    chunkSizeWarningLimit: 1500,
  },
})
