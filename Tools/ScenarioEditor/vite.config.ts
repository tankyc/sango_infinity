import path from "path"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

/**
 * 前端开发服务器配置
 *
 * - 开发阶段通过 proxy 把 /api 转发到本地剧本编辑器后端（默认 3009 端口），
 *   避免跨域问题；生产阶段由该后端统一托管构建产物。
 * - 线上挂在创意工坊同机的子路径 /scenario/ 下（接口前缀见 .env.production）。
 */
export default defineConfig(({ mode }) => ({
  /**
   * 静态资源基准路径。
   *
   * 生产构建用绝对路径 `/scenario/` —— 线上由 nginx 以该前缀转发。
   * 不用相对路径（'./'）的原因：相对路径的解析结果取决于当前 URL 的层级，
   * 一旦出现 /scenario/xxx/yyy 这类多级地址，assets 就会解析到错误位置而 404。
   *
   * 本地 dev / preview 仍是 './'，行为与以前完全一致。
   */
  base: mode === "production" ? "/scenario/" : "./",
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
}))
