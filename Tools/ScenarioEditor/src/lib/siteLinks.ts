/**
 * 文件名：siteLinks.ts
 * 描述：站外站点地址的集中配置。
 *
 *       与武将库、创意工坊用同一套约定：地址由 Vite 的构建期环境变量注入
 *       （见 .env 与 .env.production），而不是写死域名 —— 同一份源码既要在本地跑、
 *       又要构建出线上产物，写死会让本地点「创意工坊」跳到线上，而线上又可能指回 localhost。
 *       缺省值取本地开发地址，那样 clone 下来什么都不配也能互通。
 *
 *       注意 Vite 是构建期内联：线上要换地址必须改值后重新构建，运行时改环境变量无效。
 */

/** 创意工坊站点地址 */
export const WORKSHOP_URL: string = import.meta.env.VITE_WORKSHOP_URL ?? 'http://localhost:5173'

/** 武将库站点地址 */
export const PERSON_LIB_URL: string = import.meta.env.VITE_PERSON_LIB_URL ?? 'http://localhost:5174'
