import crypto from 'node:crypto';
import path from 'node:path';
import fsp from 'node:fs/promises';
import { prisma } from '../db';
import { config } from '../config';
import { ApiError } from '../errors';
import { storage } from './storage';
import { scanZip, readZipEntry } from './zipScanner';
import { normalizeZip } from './zipRepacker';
import { parseModInfo, validateModInfo, MOD_ID_PATTERN, MOD_INFO_KEYS } from './modInfoParser';
import type { TempFile } from './upload';
import { removeTempFiles } from './upload';
import { compareVersion, maxVersion } from './version';

/** 模组类型（对应 Steam 创意工坊的分类筛选） */
export const CATEGORIES = ['scenario', 'person', 'face', 'sound', 'skill', 'ui', 'mixed'] as const;
export type Category = (typeof CATEGORIES)[number];

const ALLOWED_POSTER_EXT = ['.png', '.jpg', '.jpeg', '.webp'];
const MAX_TAGS = 8;
const MAX_TAG_LEN = 24;

export interface PublishMeta {
  name?: string;
  summary?: string;
  description?: string;
  category?: string;
  tags?: string[] | string;
  depends?: string[] | string;
  gameVersion?: string;
  version?: string;
  changelog?: string;
}

export interface PublishInput {
  userId: number;
  /**
   * 发布者账号名：会写进包内 mod.info 的 author 字段。
   * 以账号为准（而不是包内自填），避免作者栏为空或与站点显示不一致。
   */
  authorName?: string;
  /** 已落盘的 zip 临时文件 */
  zip: TempFile;
  /** 可选封面 */
  poster?: TempFile | null;
  meta: PublishMeta;
  /** true = 给已有模组发新版本；false = 发布新模组 */
  isNewVersion: boolean;
  /** 发新版本时必填 */
  modId?: string;
  isAdmin?: boolean;
}

export interface PublishResult {
  mod: {
    id: string;
    name: string;
    summary: string;
    category: string;
    status: string;
    posterUrl: string;
    authorId: number;
  };
  version: {
    version: string;
    size: number;
    changelog: string;
    fileHash: string;
    depends: string[];
  };
  /** 客户端可直接用于下载的直链（兼容 MakeUrl 规则） */
  downloadUrl: string;
  /** 网页详情页地址 */
  pageUrl: string;
  warnings: string[];
}

function normalizeList(value: string[] | string | undefined): string[] {
  if (value == null) return [];
  const raw = Array.isArray(value) ? value : String(value).split(/[,，;；]/);
  return Array.from(
    new Set(
      raw
        .map((s) => String(s).trim())
        .filter((s) => s !== '')
        .slice(0, MAX_TAGS)
        .map((s) => s.slice(0, MAX_TAG_LEN)),
    ),
  );
}

/**
 * 摘要纯文本化
 *
 * summary 会出现在网页列表卡片、详情页顶部，也会写进客户端市场清单的 description 字段，
 * 因此必须是纯文本：作者经常把带 `#`、`**` 的 Markdown 正文误填进「一句话简介」，
 * 入库时就剥掉记号，比在三个展示端各修一遍更可靠。
 *
 * 注意：正文 description 不做处理 —— 它由网页的 Markdown 渲染器负责排版。
 */
function toPlainSummary(raw: string): string {
  return raw
    .replace(/\r\n/g, '\n')
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/^#{1,6}\s+/gm, '')
    .replace(/^\s*[-*+]\s+/gm, '')
    .replace(/^\s*\d+[.)]\s+/gm, '')
    .replace(/^>\s?/gm, '')
    .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ')
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
    .replace(/\*\*([^*]+)\*\*/g, '$1')
    .replace(/`([^`]+)`/g, '$1')
    .replace(/~~([^~]+)~~/g, '$1')
    .replace(/\s+/g, ' ')
    .trim();
}

function resolveCategory(raw: string | undefined): Category {
  const v = (raw ?? '').trim().toLowerCase();
  return (CATEGORIES as readonly string[]).includes(v) ? (v as Category) : 'mixed';
}

/** 判定某模组的最新版本号（用于市场清单与"有更新"判定） */
export function latestVersionString(versions: { version: string }[]): string | undefined {
  return maxVersion(versions.map((v) => v.version));
}

/**
 * 模组 ID 字母表：去掉了 0 / 1 / l / i / o 等易混字符
 *
 * 前置模组要人工填写这个 ID，抄错一个字符就会引用失败，所以宁可牺牲一点熵。
 * 31^8 ≈ 8.5e11 种组合，配数据库主键唯一约束 + 生成时重试，实际不可能撞。
 */
const MOD_ID_ALPHABET = '23456789abcdefghjkmnpqrstuvwxyz';
const MOD_ID_LENGTH = 8;

/** 生成一个 8 位短 ID（不查库，仅供 generateModId 使用） */
function randomModId(): string {
  const bytes = crypto.randomBytes(MOD_ID_LENGTH);
  let out = '';
  for (let i = 0; i < MOD_ID_LENGTH; i++) {
    out += MOD_ID_ALPHABET[bytes[i] % MOD_ID_ALPHABET.length];
  }
  return out;
}

/**
 * 生成全局唯一的模组 ID
 *
 * 模组 ID 一律由系统分配，作者不再自填：自填容易出现好 ID 被抢注、重名冲突，
 * 而它同时是下载地址与玩家本地的模组目录名，一旦发布出去就不应该再变。
 */
export async function generateModId(): Promise<string> {
  for (let attempt = 0; attempt < 10; attempt++) {
    const id = randomModId();
    const hit = await prisma.mod.findUnique({ where: { id }, select: { id: true } });
    if (!hit) return id;
  }
  throw new ApiError(500, '分配模组 ID 失败（连续多次碰撞），请重试', 'mod_id_generate_failed');
}

/**
 * 模组入库流水线
 * 对应 建设计划.md §9.2：接收 → 扫描 → 校验 → 规整顶层目录 → 落存储 → 写库
 */
export async function publish(input: PublishInput): Promise<PublishResult> {
  const { zip, poster, meta, userId, isNewVersion, modId } = input;
  const warnings: string[] = [];
  const cleanup: string[] = [zip.path];
  if (poster) cleanup.push(poster.path);

  try {
    // ---------- 1. 安全扫描 ----------
    const scan = await scanZip(zip.path);
    if (scan.issues.length > 0) {
      throw ApiError.badRequest('模组包安全检查未通过', scan.issues);
    }
    warnings.push(...scan.warnings);

    // ---------- 2. 读取并校验 mod.info ----------
    if (!scan.modInfoPath) {
      throw ApiError.badRequest('压缩包中找不到 mod.info，缺少该文件的模组无法被游戏加载');
    }
    const infoText = (await readZipEntry(zip.path, scan.modInfoPath)).toString('utf8');
    // 模组 id 由服务端分配，包内 id 会被覆盖 → 这里不要求它存在
    const parsed = validateModInfo(parseModInfo(infoText), undefined, { requireId: false });
    if (!parsed.ok || !parsed.info) {
      throw ApiError.badRequest('mod.info 校验未通过', parsed.errors);
    }
    const info = parsed.info;

    // ---------- 3. 目标模组与幂等判定 ----------
    // 同一作者重复上传同一份包（文件哈希相同）视为重复提交，直接返回已有模组。
    // 这一步在分配新 id 之前做 —— 否则每次上传都会新建一个副本，
    // 把"手滑点了两次发布"变成站点里的两个模组。
    if (!isNewVersion) {
      const sameFile = await prisma.modVersion.findFirst({
        where: { fileHash: zip.sha256, mod: { authorId: userId } },
        include: { mod: { include: { versions: true } } },
      });
      if (sameFile) {
        warnings.push(`这份包此前已发布过（模组 ${sameFile.modId}），未重复创建`);
        return buildResult(sameFile.mod, sameFile, warnings);
      }
    }

    // 模组 id 一律由系统分配：作者不再自填，避免抢注与重名；
    // 它同时是下载地址与玩家本地目录名，发布后不应再变（发新版本时沿用同一个 id）。
    let targetId: string;
    if (isNewVersion) {
      targetId = (modId ?? '').trim();
      if (!MOD_ID_PATTERN.test(targetId)) {
        throw ApiError.badRequest(`模组 id 不合法: ${targetId}`);
      }
      if (info.id !== '' && info.id !== targetId) {
        warnings.push(
          `包内 mod.info 的 id(${info.id}) 与目标模组(${targetId}) 不一致，已按系统分配的 id 重写`,
        );
      }
    } else {
      targetId = await generateModId();
      if (info.id !== '' && info.id !== targetId) {
        warnings.push(`包内 mod.info 的 id(${info.id}) 已由系统分配的 id(${targetId}) 替代`);
      }
    }

    if (scan.topDir != null && scan.topDir !== targetId) {
      warnings.push(`压缩包顶层目录名(${scan.topDir}) 已按模组 id 规整为 ${targetId}/`);
    }

    const existing = await prisma.mod.findUnique({
      where: { id: targetId },
      include: { versions: true },
    });

    if (!isNewVersion && existing) {
      throw ApiError.conflict(
        `模组 id "${targetId}" 已存在（由其他作者发布），请更换一个独一无二的 id，或为已有模组发新版本`,
      );
    }
    if (isNewVersion && !existing) {
      throw ApiError.notFound(`模组 ${targetId} 不存在`);
    }
    if (existing && existing.authorId !== userId && !input.isAdmin) {
      throw ApiError.forbidden('只能为自己发布的模组发新版本');
    }

    const version = (meta.version ?? info.version).trim();
    const existingVersion = existing?.versions.find((v) => v.version === version);
    if (existingVersion) {
      // 相同版本的同一份文件视为重复提交，直接返回，便于客户端重试
      if (existingVersion.fileHash === zip.sha256) {
        return buildResult(existing!, existingVersion, warnings);
      }
      throw ApiError.conflict(`版本 ${version} 已存在，请提升版本号后再发布`);
    }

    const known = existing?.versions ?? [];
    const latest = latestVersionString(known);
    if (latest && compareVersion(version, latest) < 0) {
      warnings.push(`新版本号 ${version} 低于当前最新版本 ${latest}，订阅者不会被提示更新`);
    }

    // ---------- 4. 保存封面 ----------
    // 必须放在规整 zip 之前：mod.info 的 poster 要写入这张封面的访问地址，
    // 因此得先把图存好、拿到它的对外路径。
    let posterUrl = existing?.posterUrl ?? '';
    if (poster) {
      posterUrl = await savePosterFile(poster);
    }

    // ---------- 5. 组装服务端权威的元数据 ----------
    // 这些值既要写数据库，也要写回 zip 内 mod.info，所以必须在这里一次算好
    const depends = normalizeList(meta.depends ?? info.depends);
    // 前置模组填的是模组 ID。引用不到的只提示不阻断（对方可能还没发布），
    // 但作者能立刻发现"抄错 ID"—— 这是该字段最容易出错的地方。
    if (depends.length > 0) {
      const found = await prisma.mod.findMany({
        where: { id: { in: depends } },
        select: { id: true },
      });
      const foundIds = new Set(found.map((m) => m.id));
      const missing = depends.filter((d) => !foundIds.has(d));
      if (missing.length > 0) {
        warnings.push(`前置模组 ID 尚未在站点找到：${missing.join('、')}（若对方还没发布可忽略）`);
      }
    }
    const tags = normalizeList(meta.tags);
    const category = resolveCategory(meta.category);
    const changelog = (meta.changelog ?? '').trim();
    // 一句话简介：优先取表单，其次取 mod.info 的 description；一律纯文本单行
    const summary =
      toPlainSummary(meta.summary ?? '') || toPlainSummary(info.description ?? '').slice(0, 120);
    const gameVersion = (meta.gameVersion ?? '').trim() || '*';
    const displayName = (meta.name?.trim() || info.name).slice(0, 80);
    const authorName = (input.authorName ?? info.author ?? '').trim();

    // ---------- 5. 规整 zip：补顶层目录 + 把权威值写回 mod.info ----------
    // 游戏内模组管理器读的是包内 mod.info。若不同步，就会出现
    // 「网页写着 1.2、游戏里显示 1.0」或作者栏为空这类对不上的问题。
    const desiredInfo: Record<string, string> = {
      // 模组 ID 由服务端分配，必须复写进包内 mod.info：
      // 它是客户端识别模组、解析前置依赖的唯一依据，也要与顶层目录名、数据库 id 三者一致
      id: targetId,
      name: displayName,
      description: summary, // mod.info 的 description 是单行短文本，只放一句话简介
      version,
      author: authorName,
      depends: depends.join(';'),
      // 上传了封面就写它的绝对地址，游戏内可直接按 URL 取图；
      // 没上传则保留包内原值，最后退回约定的 poster.jpg
      poster: posterUrl !== '' ? absoluteAssetUrl(posterUrl) : info.poster || 'poster.jpg',
    };
    const currentValues = parseModInfo(infoText);
    const needInfoRewrite = MOD_INFO_KEYS.some(
      (k) => (currentValues[k] ?? '') !== (desiredInfo[k] ?? ''),
    );
    if (needInfoRewrite) {
      warnings.push('已按站点分配 / 填写的 id / 名称 / 作者 / 简介 / 版本号重写包内 mod.info');
    }

    const repacked = await normalizeZip(
      zip.path,
      zip.path.replace(/\.part$/, '') + '.normalized.zip',
      { id: targetId, modInfo: needInfoRewrite ? desiredInfo : undefined },
      scan,
    );
    if (repacked.path !== zip.path) cleanup.push(repacked.path);

    // ---------- 6. 写入存储（local 直出 / 对象存储，由配置决定）----------
    const zipKey = `mods/${targetId}/${version}.zip`;
    const size = await storage.putFile(repacked.path, zipKey, { contentType: 'application/zip' });

    // 封面与元数据已在第 4、5 步统一处理（depends / tags / category / changelog / summary / gameVersion / posterUrl）
    const status = config.autoApprove ? 'approved' : existing?.status ?? 'pending';

    // ---------- 7. 写库 ----------
    const versionData = {
      version,
      changelog,
      size,
      fileKey: zipKey,
      fileHash: zip.sha256,
      depends: depends.join(';'),
      gameVersion,
    };

    let modRecord;
    if (existing) {
      await prisma.modVersion.create({ data: { ...versionData, modId: targetId } });
      modRecord = await prisma.mod.update({
        where: { id: targetId },
        data: {
          updatedAt: new Date(),
          posterUrl: posterUrl || existing.posterUrl,
          ...(meta.name ? { name: meta.name } : {}),
          ...(meta.description ? { description: meta.description } : {}),
          ...(meta.category ? { category } : {}),
          ...(meta.gameVersion ? { gameVersion } : {}),
        },
      });
    } else {
      modRecord = await prisma.mod.create({
        data: {
          id: targetId,
          authorId: userId,
          name: displayName,
          summary,
          description: meta.description?.trim() || info.description || '',
          category,
          posterUrl,
          gameVersion,
          status,
          tags: tags.length > 0 ? { create: tags.map((tag) => ({ tag })) } : undefined,
          versions: { create: versionData },
        },
      });
    }

    const versionRecord =
      existing?.versions.find((v) => v.version === version) ??
      (await prisma.modVersion.findUniqueOrThrow({
        where: { modId_version: { modId: targetId, version } },
      }));

    return buildResult(modRecord, versionRecord, warnings);
  } catch (e) {
    throw e;
  } finally {
    await removeTempFiles(...cleanup);
  }
}

function buildResult(
  mod: {
    id: string;
    name: string;
    summary: string;
    category: string;
    status: string;
    posterUrl: string;
    authorId: number;
  },
  version: { version: string; size: number; changelog: string; fileHash: string; depends: string },
  warnings: string[],
): PublishResult {
  return {
    mod: {
      id: mod.id,
      name: mod.name,
      summary: mod.summary,
      category: mod.category,
      status: mod.status,
      posterUrl: mod.posterUrl,
      authorId: mod.authorId,
    },
    version: {
      version: version.version,
      size: version.size,
      changelog: version.changelog,
      fileHash: version.fileHash,
      depends: version.depends ? version.depends.split(';').filter(Boolean) : [],
    },
    downloadUrl: `${config.appBaseUrl}/mods/${mod.id}@${version.version}.zip`,
    pageUrl: `${config.appBaseUrl}/sharedfiles/filedetails/?id=${mod.id}`,
    warnings,
  };
}

/**
 * 保存封面并返回它的对外访问路径（形如 /posters/<sha256>.png）
 *
 * 封面以内容哈希命名 → 同一张图重复上传只会占一份空间，且天然不可变、可永久缓存。
 * 发布新模组与「我的创作里改封面」两处都走这里，保证命名规则一致。
 */
export async function savePosterFile(poster: TempFile): Promise<string> {
  const ext = path.extname(poster.filename).toLowerCase();
  if (!ALLOWED_POSTER_EXT.includes(ext)) {
    throw ApiError.badRequest(`封面图格式不支持，仅允许 ${ALLOWED_POSTER_EXT.join(' / ')}`);
  }
  const posterKey = `posters/${poster.sha256}${ext}`;
  await storage.putFile(poster.path, posterKey, { contentType: `image/${ext.slice(1)}` });
  return `/posters/${poster.sha256}${ext}`;
}

/**
 * 把站内相对路径（形如 /posters/xxx.png）转成客户端可直接访问的绝对地址
 *
 * 为什么必须是绝对地址：mod.info 会随模组包被解压到玩家本地的 Mods/{id}/ 目录，
 * 那时已经脱离了站点上下文 —— 相对路径在那里没有任何 base 可以拼接，游戏端没法取图。
 * 配了 CDN 时优先用 CDN 域名（封面走 CDN，不占应用服务器带宽）。
 * 已经是完整 URL 的原样返回。
 */
export function absoluteAssetUrl(pathname: string): string {
  if (pathname === '') return '';
  if (/^https?:\/\//i.test(pathname)) return pathname;
  const base = (config.storage.cdnBaseUrl || config.appBaseUrl || '').replace(/\/+$/, '');
  if (base === '') return pathname;
  return `${base}${pathname.startsWith('/') ? '' : '/'}${pathname}`;
}

/** 生成临时文件名（给调用方创建规整后的目标路径用） */
export function tempName(prefix: string): string {
  return `${prefix}_${crypto.randomUUID()}`;
}

/** 删除模组的所有文件（下架/清理用） */
export async function removeModFiles(modId: string): Promise<void> {
  const versions = await prisma.modVersion.findMany({ where: { modId }, select: { fileKey: true } });
  await Promise.all(versions.map((v) => storage.remove(v.fileKey).catch(() => undefined)));
  await fsp
    .rm(path.join(config.storage.localRoot, 'mods', modId), { recursive: true, force: true })
    .catch(() => undefined);
}
