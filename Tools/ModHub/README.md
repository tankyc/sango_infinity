# Sango ModHub（三国·无限 创意工坊）

A Steam 创意工坊风格的模组托管站点：网页浏览 + 游戏内浏览与订阅下载。

- 详细方案与里程碑见 [`建设计划.md`](./建设计划.md)
- 目录中的建议来自业界成熟产品：Steam 创意工坊、Nexus Mods、CurseForge

---

## 一、当前进度：M1 地基 + M2 浏览层（已完成 ✅）

### M1 地基（后端）

| 能力 | 状态 |
|---|---|
| Fastify + TypeScript 后端骨架 | ✅ |
| 用户注册 / 登录（**兼容游戏现有云存档服务的契约**） | ✅ |
| 创作发布接口（新模组 / 新版本） | ✅ |
| zip 安全扫描（可执行文件、zip slip、zip bomb、条目数、单文件大小） | ✅ |
| `mod.info` 解析与校验（含"值不能含 `=`"的坑） | ✅ |
| 顶层目录自动规整（缺 `{id}/` 会导致"下载成功但模组不显示"） | ✅ |
| `/market/mod_list.txt` 兼容端点（**客户端直连**） | ✅ |
| `/mods/{id}@{version}.zip` 直链下载（支持 Range 断点续传） | ✅ |
| 存储抽象层：本地直出 ⇄ 对象存储 + CDN，**切换只改环境变量** | ✅ |
| 端到端冒烟测试 44 项全通过 | ✅ |

### M2 浏览（前端网站）

| 能力 | 状态 |
|---|---|
| React 18 + Vite + TS + Tailwind 前端工程（`web/`） | ✅ |
| `/browse` 首页：左侧常驻筛选栏 + 排序 Tab + 精选大卡 + 无限滚动 | ✅ |
| 详情页 `/sharedfiles/filedetails/?id=`：左主栏 + 右侧吸顶信息栏（统计 / 标签 / 前置模组 / 游戏内订阅） | ✅ |
| 搜索：筛选条件全部由 URL 承载，可分享、可收藏、可前进后退 | ✅ |
| `/upload` 发布页：拖拽 zip **或整个模组文件夹** + 封面上传 + 上传进度 + 服务端 warnings 回显 | ✅ |
| 直接选文件夹发布：浏览器端自动打包（脏文件自动剔除，包内缺 `mod.info` 则按表单信息生成并补进包内） | ✅ |
| `/login` 登录注册（与游戏内云存档共用同一账号） | ✅ |
| `/api/browse`（keyset 游标分页 + 四类排序）与 `/api/filters` | ✅ |
| 详情页留言区：登录后可发表 / 回复（只做两层）/ 删除；删除权限由服务端判定（留言本人 / 模组作者 / 管理员），keyset 翻页 + 10 秒发言冷却 | ✅ |
| 留言点赞：乐观更新 + 失败回滚；`(commentId, userId)` 联合主键保证重复点赞幂等，点赞数实时统计 | ✅ |
| 封面缺失 / 加载失败占位兜底（分类图标，不破版） | ✅ |
| 三国水墨鎏金主题、全站 Lucide 图标（不使用 emoji）、响应式与键盘可达 | ✅ |
| `npm run seed` 本地演示数据脚本（6 个带封面的示例模组） | ✅ |

**未完成**：封面三档 webp 缩略图（需引入图像处理库）、X-Ray 内容透视、订阅同步与 `sango://` 深链、新留言通知、Docker 部署。

---

## 二、目录结构

```
Tools/ModHub/
├─ 建设计划.md              方案 / API / 风险 / 里程碑
├─ server/                  后端（M1）
│  ├─ prisma/schema.prisma   数据模型
│  ├─ src/
│  │  ├─ config.ts           配置中心（所有环境变量的唯一入口）
│  │  ├─ db.ts               Prisma 单例
│  │  ├─ errors.ts           统一 ApiError → { error, code, details }
│  │  ├─ index.ts            服务入口与全局错误/404 处理
│  │  ├─ middleware/auth.ts  Bearer JWT（CloudSaveClient 同款登录态）
│  │  ├─ routes/
│  │  │  ├─ auth.ts          /login /register /me
│  │  │  ├─ market.ts        /market/mod_list.txt（客户端协议）
│  │  │  ├─ browse.ts        /api/browse、/api/filters（M2 新增）
│  │  │  ├─ mods.ts          /api/mods/:id 详情
│  │  │  ├─ publish.ts       /api/mods、/api/mods/:id/versions
│  │  │  ├─ download.ts      /mods|/market/{id}@{version}.zip、/api/mods/:id/download
│  │  │  ├─ static.ts        /posters/:hash.ext
│  │  │  └─ health.ts        /health、/health/storage
│  │  └─ service/
│  │     ├─ storage.ts        存储抽象层（local / s3）+ CDN 直链
│  │     ├─ upload.ts         上传落盘 + sha256（不全量驻留内存）
│  │     ├─ zipScanner.ts     zip 安全扫描
│  │     ├─ zipRepacker.ts    顶层目录规整（流式重写）
│  │     ├─ modInfoParser.ts  mod.info 解析/校验/生成
│  │     ├─ modService.ts     入库流水线
│  │     ├─ browseService.ts  浏览查询：筛选 / 排序 / keyset 游标（M2 新增）
│  │     ├─ marketCache.ts    市场清单缓存（发布后即时失效）
│  │     ├─ version.ts        版本号比较
│  │     └─ form.ts           multipart / urlencoded / json 统一读取
│  ├─ scripts/
│  │  ├─ smoke.mjs           端到端冒烟测试
│  │  └─ seed-demo.mjs       本地演示数据（M2 新增）
│  └─ .env.example
├─ web/                     前端网站（M2）
│  ├─ src/
│  │  ├─ pages/              BrowsePage / ModDetailPage / UploadPage / LoginPage / NotFoundPage
│  │  ├─ components/
│  │  │  ├─ layout/          TopBar / Footer
│  │  │  ├─ browse/          FilterSidebar / SortTabs / ModCard / FeaturedStrip / PosterImage
│  │  │  └─ ui/              Button / Badge / Field / Feedback / CategoryIcon
│  │  ├─ hooks/              useBrowse（游标分页 + 无限滚动） / useAuth
│  │  ├─ lib/                api / format / catalog / miniMarkdown / cn
│  │  └─ index.css           主题底纹、滚动条、骨架动画、动效降级
│  ├─ vite.config.ts         开发期把 /api /posters /mods /market 代理到 8081
│  └─ tailwind.config.js     水墨鎏金主题（墨 / 金 / 朱砂 / 宣纸 / 竹青）
└─ data/                     运行期数据（gitignore）
```

---

## 三、快速开始

### 后端

```bash
cd server
npm install
cp .env.example .env          # Windows: Copy-Item .env.example .env
npm run prisma:migrate        # 初始化 SQLite（位于 ../data/modhub.db）
npm run dev                   # 开发模式 http://localhost:8081

# 生产构建
npm run build && npm start
```

冒烟测试（**需先启动服务**）：

```bash
npm run smoke                 # 44 项断言：注册→发布→市场→下载→发新版→安全校验
```

灌入演示数据（**需先启动服务**，仅用于开发库）：

```bash
npm run seed                  # 6 个带封面的示例模组，覆盖各分类与标签
```

### 前端

```bash
cd web
npm install
npm run dev                   # http://localhost:5173（/api 等已自动代理到 8081）
npm run build                 # 类型检查 + 打包到 web/dist
```

> 页面与接口要同时运行：`5173` 是页面，`8081` 是接口。
> 前端一律使用相对路径请求，开发期由 Vite 代理转发，部署期由 Nginx 同域反代，代码无需改动。

健康检查：`http://localhost:8081/health`

> 注意：`/health` 里的 **storage 与 cdn 字段会告诉你当前处于哪种分发形态**，排查带宽问题时先看它。

---

## 四、环境变量

完整列表见 [`server/.env.example`](./server/.env.example)，关键项：

| 变量 | 默认 | 说明 |
|---|---|---|
| `PORT` | `8081` | 监听端口 |
| `APP_BASE_URL` | `http://localhost:8081` | 写进市场清单 `url` 字段的对外基址，**部署时必须改成公网地址** |
| `JWT_SECRET` | dev | **生产务必更换** |
| `DATABASE_URL` | `file:../../data/modhub.db` | 相对 `prisma/` 目录解析 |
| `DATA_DIR` | `../data` | 模组文件 / 封面 / 临时文件根目录 |
| `UPLOAD_MAX_ZIP_MB` | `200` | 单模组包上限 |
| `AUTO_APPROVE` | `true` | 发布后是否直接上架；`false` 则需管理员审核 |
| `MARKET_NAME` | `Sango Workshop` | 客户端模组管理器里显示的市场名 |
| `MARKET_CACHE_TTL` | `300` | 市场清单缓存秒数（发布会立即失效） |
| `STORAGE_DRIVER` | `local` | `local` \| `s3` |
| `CDN_BASE_URL` | 空 | 见下方"切换 CDN" |

---

## 五、存储与分发（重点）

> 轻量服务器出口带宽仅 3–8Mbps，**一个大模组就可能打满**，详见 `建设计划.md §6.1`。

抽象层在 `src/service/storage.ts`，对外只有一个契约。切换形态**只改环境变量，不改代码、不改游戏**：

| 场景 | `STORAGE_DRIVER` | `CDN_BASE_URL` | 下载行为 |
|---|---|---|---|
| **冷启动**（当前） | `local` | 空 | 服务器本地直出，支持 Range |
| 前置 CDN 或已迁入对象存储 | `local` / `s3` | `https://cdn.xx` | **302 跳 CDN**，不占轻量服务器带宽 |

**为什么客户端不用改**：`GitDownloader.cs:124` 用的是 `UnityWebRequest.Get()`，Unity 默认 `redirectLimit = 32` 会自动跟随 302；而 `NetModMarket.MakeUrl()` 拼出的 `{id}@{version}.zip` 路径格式保持不变。

已实测：

```
health.storage = local   health.cdn = https://cdn.example.com
下载响应  status  = 302
Location  = https://cdn.example.com/mods/smoke_mod_xxx/1.1.zip
Cache-Control = public, max-age=31536000, immutable
```

### 日后迁对象存储

```bash
npm i @aws-sdk/client-s3      # 未安装时不阻断 local 模式启动
```

```ini
STORAGE_DRIVER=s3
S3_ENDPOINT=https://cos.ap-guangzhou.myqcloud.com
S3_REGION=ap-guangzhou
S3_BUCKET=your-bucket
S3_ACCESS_KEY_ID=...
S3_SECRET_ACCESS_KEY=...
CDN_BASE_URL=https://cdn.example.com
```

> ⚠️ 上 CDN 后**必须先配"带宽封顶 + 日流量告警"**，详见 `建设计划.md §6.1`，否则一旦被盜链会产生天价账单。

---

## 六、HTTP API

### 兼容端点（客户端直连）

| 方法 | 路径 | 说明 |
|---|---|---|
| `GET` | `/market/mod_list.txt` | 市场清单；别名 `/mods/mod_list.txt` |
| `GET` | `/mods/{id}@{version}.zip` | 下载；别名 `/market/{id}@{version}.zip` |
| `POST` | `/login` `/register` | 与现有云存档服务同路径同契约：`{ token, user:{ id, username } }` |
| `GET` | `/me` | Bearer Token |

市场清单响应（`auther` 是协议里的历史拼写，**不能改**；`size` 必须是数字）：

```json
{
  "name": "Sango Workshop",
  "url": "https://host/market/mod_list.txt",
  "mods": [
    { "id": "official_rp", "name": "rp", "version": "1.0",
      "size": 88716, "auther": "官方", "description": "rp版mod.",
      "poster": "poster.jpg" }
  ]
}
```

### 站点 API

| 方法 | 路径 | 鉴权 | 说明 |
|---|---|---|---|
| `GET` | `/api/browse` | 可选 | 浏览列表：`cursor` `limit` `q` `category` `tag`（可重复） `gameVersion` `sort=hot\|new\|updated\|trending` |
| `GET` | `/api/filters` | - | 筛选栏数据：各类型计数、热门标签、兼容游戏版本 |
| `GET` | `/api/mods/:id/comments` | 可选 | 留言列表（`cursor` `limit`；keyset 翻页，返回顶层留言及其回复） |
| `POST` | `/api/mods/:id/comments` | Bearer | 发表留言 / 回复（JSON：`content` + 可选 `parentId`；500 字上限，10 秒冷却） |
| `DELETE` | `/api/comments/:id` | Bearer | 删除留言（留言本人 / 模组作者 / 管理员；删顶层时连带删除其回复） |
| `POST` | `/api/comments/:id/like` | Bearer | 留言点赞（幂等，重复点赞不重复计数） |
| `DELETE` | `/api/comments/:id/like` | Bearer | 取消留言点赞（同样幂等） |
| `POST` | `/api/mods` | Bearer | 发布新模组（multipart：`file` + 元数据 + `poster`） |
| `POST` | `/api/mods/:id/versions` | Bearer | 发新版本 |
| `GET` | `/api/mods/:id` | 可选 | 详情（含版本列表、Required Items、统计） |
| `GET` | `/api/mods/:id/download?version=` | 可选 | 网页下载入口，省略 version 取最新版 |
| `GET` | `/posters/:hash.ext` | - | 封面 |
| `GET` | `/health` `/health/storage` | - | 健康检查 |

错误统一为 `{ "error": "…", "code": "…", "details": ["…"] }`，与 Unity 侧 `CloudSaveApiError` 可直接反序列化。

### 发布示例（Unity 侧用 WWWForm 提交同样的字段）

```bash
curl -X POST http://localhost:8081/api/mods \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@mymod.zip" \
  -F "poster=@cover.png" \
  -F "name=新武将包" \
  -F "summary=一句话简介" \
  -F "description=# 正文" \
  -F "category=person" \
  -F "tags=武将,头像" \
  -F "depends=official_rp" \
  -F "gameVersion=1.2" \
  -F "changelog=首发"
```

响应：

```json
{
  "mod": { "id": "new_persons", "name": "新武将包", "status": "approved", ... },
  "version": { "version": "1.0", "size": 1234567, "depends": ["official_rp"], ... },
  "downloadUrl": "http://localhost:8081/mods/new_persons@1.0.zip",
  "pageUrl": "http://localhost:8081/sharedfiles/filedetails/?id=new_persons",
  "warnings": []
}
```

> `warnings` 里会提示诸如"压缩包顶层目录名与 id 不一致，已自动重写"这类**已帮你兜好**的问题，不要放过不看。

---

## 七、客户端接入

在游戏内模组管理器添加市场地址即可（`/market/mod_list.txt`）。

客户端已发现的坑，服务端都做了兜底或校验：

1. **`auther` 拼写**：少写这个字段，游戏内作者栏为空
2. **`size` 必须是数字**：客户端是 `long`
3. **zip 必须有 `{id}/` 顶层目录**：否则下载成功但模组不显示（服务端会自动重写）
4. **`mod.info` 值不能含 `=`**：游戏按 `=` 截断（服务端入库时拦截）
5. **`GitDownloader` 全量内存下载 + 无断点续传**：建议后续换 `DownloadHandlerFile`（排期 M5）

---

## 八、常见操作

```bash
# 查看数据
npm run prisma:studio
npm run build              # 类型检查 + 编译到 dist/
npm run smoke              # 端到端回归（含安全负向用例）
```

数据库先用 SQLite；迁 MySQL 8 只需改 `prisma/schema.prisma` 的 `provider` 并把 `DATABASE_URL` 换成 `mysql://…`，然后 `npm run prisma:migrate`，业务代码无需改动。
