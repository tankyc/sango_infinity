import path from "path"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

// 前端开发服务器配置：
// - 开发时通过 proxy 将 /api 与 /face 转发到本地 Node 服务（默认 3001 端口）
// - 生产构建产物由 Node 服务统一托管
export default defineConfig({
  base: './',
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:3001",
        changeOrigin: true,
      },
      "/face": {
        target: "http://localhost:3001",
        changeOrigin: true,
      },
    },
  },
});
