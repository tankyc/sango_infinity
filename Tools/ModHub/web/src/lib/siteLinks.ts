/**
 * 文件名：siteLinks.ts
 * 描述：站外站点地址的集中配置。
 *
 *       用 Vite 的构建期环境变量注入（见 web/.env），而不是写死域名：
 *       同一份源码既要在本地跑、又要构建出线上产物，写死会导致本地点「武将库」
 *       跳到线上，而线上又可能指回 localhost。缺省值取本地开发地址，
 *       那样 clone 下来什么都不配也能互通。
 *
 *       注意 Vite 是「构建期内联」：线上要换地址必须改值后重新构建，运行时改环境变量无效。
 */

/** 武将库站点地址 */
export const PERSON_LIB_URL: string =
  import.meta.env.VITE_PERSON_LIB_URL ?? 'http://localhost:5174';

/** 剧本编辑器站点地址 */
export const SCENARIO_URL: string =
  import.meta.env.VITE_SCENARIO_URL ?? 'http://localhost:5273';
