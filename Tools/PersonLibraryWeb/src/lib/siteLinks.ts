/**
 * 文件名：siteLinks.ts
 * 描述：站外站点地址的集中配置。
 *
 *       用 Vite 的构建期环境变量注入（见项目根目录的 .env），而不是写死域名：
 *       同一份源码既要在本地跑、又要构建出线上产物，写死会导致本地点「创意工坊」
 *       跳到线上，而线上又可能指回 localhost。缺省值取本地开发地址，
 *       那样 clone 下来什么都不配也能互通。
 *
 *       注意 Vite 是构建期内联：线上要换地址必须改值后重新构建，运行时改环境变量无效。
 */

/** 创意工坊站点地址 */
export const WORKSHOP_URL: string = import.meta.env.VITE_WORKSHOP_URL ?? 'http://localhost:5173';

/** 剧本编辑器站点地址 */
export const SCENARIO_URL: string = import.meta.env.VITE_SCENARIO_URL ?? 'http://localhost:5273';

/**
 * 本站接口与静态资源的路径前缀（不带尾斜杠）。
 *
 * 子路径部署时（站点挂在 http://host/personlib/），浏览器发起的每个请求都要带上这段前缀，
 * 否则会打到同机根路径上的另一个站点去。nginx 会在转发给后端时把这层前缀剥掉，
 * 所以后端本身不需要感知子路径。
 *
 * 本地开发留空：dev server 的 proxy 已经把 /api 与 /face 转到了 3001。
 */
export const API_PREFIX: string = (import.meta.env.VITE_API_PREFIX ?? '').replace(/\/+$/, '');

/**
 * 给站内绝对路径（以 / 开头）补上站点前缀。
 * @param pathname 站内路径，如 /face/3001_2.png
 * @returns 可直接用于 src / href 的地址
 */
export function withPrefix(pathname: string): string {
  return `${API_PREFIX}${pathname}`;
}

/** 创意工坊的「发布模组」页地址（导出模组包后，下一步就是去这里上传） */
// 用字符串拼接而不是模板字符串：这里要剥掉地址末尾的斜杠，
// 拼接写法少一层嵌套，读起来更直白
export const WORKSHOP_UPLOAD_URL: string = WORKSHOP_URL.replace(/\/+$/, '') + '/upload';
