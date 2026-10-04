/**
 * 增量备份存储
 *
 * 背景：剧本单份 1.67 MB，原先每次保存都全量复制一份、保留 60 份 ——
 * 单个用户的工作区光备份就能堆到 100 MB 量级，上云后这是持续增长的存储成本。
 *
 * 现在改为**链式增量**：
 * - 每份备份要么是 **full**（存 `Scenario.json` 原文），要么是 **delta**
 *   （存相对**前一份**的扁平差异 `delta.json`）；
 * - 每条 delta 只包含「这一次保存改了什么」，因此最小；
 * - delta 体积超过全量的 `FULL_RATIO` 时改存 full —— 补丁比原文还大就没意义了。
 *
 * 链式而非「锚点式」是踩过坑后的选择：锚点式（每条 delta 相对最近的全量锚点）
 * 创建与恢复都只需一次差异应用，但每次清理掉最老的锚点时，**依赖它的那一批
 * delta 全部要被重建成全量**（锚点间隔 5 时一次重建 5 份），增量收益被吃掉一半以上
 * （实测 10 份里 6 份是全量，只省 52.9%）。链式下清理只会让**下一条**重锚定，
 * 代价恒为 1 份。
 *
 * 创建时不需要重放链：上一次 createBackup 的入参内容就是前一份备份的内容，
 * 因此进程内缓存一份即可零成本算差异；缓存未命中（如刚重启）时保守落全量。
 * 恢复时需要重放链，但保留份数只有 10，实测重放成本在几十毫秒量级。
 *
 * 向后兼容：老备份没有 `kind` 字段，一律按 full 读取。
 */
'use strict';

const fs = require('fs');
const path = require('path');
const { diffValues, applyDelta, isDeltaEmpty } = require('./jsonDiff');

/** 备份目录名与文件名 */
const FULL_FILE = 'Scenario.json';
const DELTA_FILE = 'delta.json';
const META_FILE = 'meta.json';

/** delta 体积超过全量的这个比例时，改存全量 */
const FULL_RATIO = 0.6;

/** 默认保留份数 */
const DEFAULT_KEEP = 10;

/**
 * 上一次成功创建的备份内容（按备份目录区分）。
 *
 * 链式增量需要「前一份的内容」才能算差异，而它恰好就是上一次 createBackup
 * 拿到的 content——缓存下来就不必为了算差异去重放整条链。
 * 每项约等于一份剧本大小，只保留最近用到的几个目录，避免多用户下无限累积。
 */
const lastContentByDir = new Map();
/** 缓存目录数上限 */
const CACHE_LIMIT = 4;

/**
 * 生成备份名前缀（本地时间 yyyyMMdd_HHmmss）。
 *
 * @param {Date} [date] 时间点
 * @returns {string} 时间戳
 */
function timestamp(date = new Date()) {
  const pad = (n) => String(n).padStart(2, '0');
  return (
    `${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}` +
    `_${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}`
  );
}

/**
 * 在一个目录内生成备份名。
 *
 * 目录名同时承担「排序键」的职责（`listBackups` 靠字典序取时间序），
 * 因此**名字必须在时间戳内单调递增，绝不能回收复用**。
 *
 * 这里踩过一个会丢数据的坑：早先的实现是「从 1 开始找第一个不存在的名字」，
 * 于是清理掉 `auto_TS` 之后，同一秒内新建的备份又会叫 `auto_TS` ——
 * 而字符串序里 `auto_TS < auto_TS_9`，它会被排在最后**当成最老的一份**，
 * 下一次清理就把它删掉了（也就是刚存完的备份当场没了）。
 * 改为「取同秒内最大的序号 + 1」，名字只增不减。
 *
 * @param {string} dir 备份根目录
 * @returns {string} 备份名（形如 auto_20261004_202321 或 auto_20261004_202321_3）
 */
function uniqueName(dir) {
  const base = `auto_${timestamp()}`;
  let names = [];
  try {
    names = fs.readdirSync(dir);
  } catch {
    /* 目录还不存在，直接用 base */
  }

  const sameStamp = names.filter((n) => n === base || n.startsWith(`${base}_`));
  if (sameStamp.length === 0) return base;

  let maxSeq = 0;
  for (const n of sameStamp) {
    if (n === base) continue;
    const seq = Number(n.slice(base.length + 1));
    if (Number.isInteger(seq) && seq > maxSeq) maxSeq = seq;
  }
  return `${base}_${maxSeq + 1}`;
}

/**
 * 把备份名解析成可比较的数值键。
 *
 * 名字形如 `auto_20261004_202321` 或 `auto_20261004_202321_3`（同一秒内的序号）。
 * **不能直接用字符串比较**：序号是数字，字典序里 `_11 < _9`，
 * 结果第 11 份会被排到第 9 份后面，当成更老的一份 —— 清理时就可能删错。
 *
 * @param {string} name 备份名
 * @returns {{stamp:string, seq:number}|null} 解析结果（非标准名返回 null）
 */
function nameKey(name) {
  const m = /^auto_(\d{8}_\d{6})(?:_(\d+))?$/.exec(name);
  if (!m) return null;
  return { stamp: m[1], seq: Number(m[2] || 0) };
}

/**
 * 读取某份备份的元信息。
 *
 * @param {string} dir 备份根目录
 * @param {string} name 备份名
 * @returns {object|null} 元信息（不存在返回 null）
 */
function readMeta(dir, name) {
  const file = path.join(dir, name, META_FILE);
  if (!fs.existsSync(file)) return null;
  try {
    const meta = JSON.parse(fs.readFileSync(file, 'utf8'));
    // 老备份没有 kind，按全量处理
    if (!meta.kind) meta.kind = 'full';
    return meta;
  } catch {
    return null;
  }
}

/**
 * 列出全部备份（按时间倒序，新的在前）。
 *
 * 目录名带时间戳前缀，因此字典序即时间序。
 *
 * @param {string} dir 备份根目录
 * @returns {Array<object>} 备份列表
 */
function listBackups(dir) {
  if (!dir || !fs.existsSync(dir)) return [];
  const result = [];
  for (const name of fs.readdirSync(dir)) {
    const dirPath = path.join(dir, name);
    let stat;
    try {
      stat = fs.statSync(dirPath);
    } catch {
      continue;
    }
    if (!stat.isDirectory()) continue;

    const fullFile = path.join(dirPath, FULL_FILE);
    const deltaFile = path.join(dirPath, DELTA_FILE);
    const hasFull = fs.existsSync(fullFile);
    const hasDelta = fs.existsSync(deltaFile);
    if (!hasFull && !hasDelta) continue;

    const meta = readMeta(dir, name) || {};
    const payload = hasFull ? fullFile : deltaFile;
    let size = stat.size;
    let deltaSize = 0;
    try {
      size = hasFull ? fs.statSync(fullFile).size : Number(meta.size) || 0;
      deltaSize = hasDelta ? fs.statSync(deltaFile).size : 0;
    } catch {
      /* 统计失败不影响列表 */
    }

    result.push({
      name,
      at: meta.at || stat.mtime.toISOString(),
      reason: meta.reason || '',
      kind: meta.kind || 'full',
      base: meta.base || null,
      revision: meta.revision || '',
      changed: Number(meta.changed) || 0,
      removed: Number(meta.removed) || 0,
      /** 还原后的剧本大小 */
      size,
      /** 实际占盘字节数（delta 时就是补丁大小） */
      storedSize: (() => {
        try {
          return fs.statSync(payload).size + (fs.existsSync(path.join(dirPath, META_FILE)) ? 200 : 0);
        } catch {
          return size;
        }
      })(),
      deltaSize,
    });
  }
  return result.sort((a, b) => {
    const ka = nameKey(a.name);
    const kb = nameKey(b.name);
    // 标准名按「时间戳降序 -> 同秒内序号降序」排；名字不符合规范时退回字符串比较
    if (ka && kb) {
      if (ka.stamp !== kb.stamp) return ka.stamp < kb.stamp ? 1 : -1;
      return kb.seq - ka.seq;
    }
    return a.name < b.name ? 1 : -1;
  });
}

/**
 * 读取一份备份还原后的内容。
 *
 * @param {string} dir 备份根目录
 * @param {string} name 备份名
 * @returns {string} 剧本文本
 */
function loadBackup(dir, name) {
  // 目标本身就是全量时直接返回原文：省一次解析 + 序列化，也保住「原文」语义
  const targetFull = path.join(dir, name, FULL_FILE);
  if (fs.existsSync(targetFull)) return fs.readFileSync(targetFull, 'utf8');

  // 1) 从目标往上回溯，收集出「全量 -> 目标」的链
  const chain = [];
  const seen = new Set();
  let cursor = name;

  while (cursor) {
    if (seen.has(cursor)) {
      const err = new Error(`备份链存在循环，已损坏：${name}`);
      err.code = 'EBAD_BACKUP';
      throw err;
    }
    seen.add(cursor);

    const dirPath = path.join(dir, cursor);
    if (fs.existsSync(path.join(dirPath, FULL_FILE))) {
      chain.push(cursor);
      break;
    }

    const meta = readMeta(dir, cursor);
    if (!meta || !meta.base || !fs.existsSync(path.join(dirPath, DELTA_FILE))) {
      const err = new Error(`备份内容缺失或已损坏：${cursor}`);
      err.code = 'EBAD_BACKUP';
      throw err;
    }
    chain.push(cursor);
    cursor = meta.base;
  }

  // 2) 反转成「全量在前」，依次把差异应用回去
  chain.reverse();
  let obj = JSON.parse(fs.readFileSync(path.join(dir, chain[0], FULL_FILE), 'utf8'));
  for (let i = 1; i < chain.length; i += 1) {
    const delta = JSON.parse(fs.readFileSync(path.join(dir, chain[i], DELTA_FILE), 'utf8'));
    obj = applyDelta(obj, delta);
  }

  // 只用于 JSON.parse 回对象，紧凑序列化即可，格式由调用方统一决定
  return JSON.stringify(obj);
}

/**
 * 创建一份备份（自动决定存全量还是差异）。
 *
 * @param {object} input 入参
 * @param {string} input.dir 备份根目录
 * @param {string} input.content 当前内容（将被备份的文本）
 * @param {string} [input.reason] 备份原因
 * @param {string} [input.file] 源文件路径（仅记录在元信息里）
 * @param {string} [input.revision] 内容指纹
 * @returns {{name:string, kind:string, base:string|null, changed:number, removed:number, ratio:number}}
 */
function createBackup({ dir, content, reason = '', file = '', revision = '' }) {
  fs.mkdirSync(dir, { recursive: true });
  const name = uniqueName(dir);
  const dirPath = path.join(dir, name);
  fs.mkdirSync(dirPath, { recursive: true });

  let kind = 'full';
  let base = null;
  let delta = null;

  // 链式增量：基准是「上一次创建的备份」，其内容就是上一次的入参，直接取缓存
  const prev = lastContentByDir.get(dir);
  if (prev && prev.name && fs.existsSync(path.join(dir, prev.name))) {
    const prevMeta = readMeta(dir, prev.name);
    // 校验缓存与磁盘上那份备份确实对应同一次内容，避免基准错位算出错误差异
    const consistent =
      prevMeta && (!prev.revision || !prevMeta.revision || prevMeta.revision === prev.revision);

    if (consistent) {
      try {
        const d = diffValues(JSON.parse(prev.content), JSON.parse(content));
        const deltaText = JSON.stringify(d);
        // 空差异或无意义的差异（补丁比原文还大）一律落全量
        if (!isDeltaEmpty(d) && deltaText.length < content.length * FULL_RATIO) {
          kind = 'delta';
          base = prev.name;
          delta = d;
        }
      } catch {
        // 任一侧解析失败时保守落全量，绝不产出无法还原的备份
        kind = 'full';
        base = null;
        delta = null;
      }
    }
  }

  const changed = delta ? delta.changed.length : 0;
  const removed = delta ? delta.removed.length : 0;

  if (kind === 'delta') {
    fs.writeFileSync(path.join(dirPath, DELTA_FILE), JSON.stringify(delta), 'utf8');
  } else {
    fs.writeFileSync(path.join(dirPath, FULL_FILE), content, 'utf8');
  }

  fs.writeFileSync(
    path.join(dirPath, META_FILE),
    JSON.stringify(
      {
        reason,
        at: new Date().toISOString(),
        file,
        kind,
        base,
        revision,
        /** 还原后的剧本大小，便于界面展示 */
        size: Buffer.byteLength(content, 'utf8'),
        changed,
        removed,
      },
      null,
      2
    ),
    'utf8'
  );

  // 缓存本次内容，供下一次算差异（LRU：多用户下只保留最近用到的几个目录）
  lastContentByDir.delete(dir);
  lastContentByDir.set(dir, { name, content, revision });
  while (lastContentByDir.size > CACHE_LIMIT) {
    const oldest = lastContentByDir.keys().next().value;
    lastContentByDir.delete(oldest);
  }

  return { name, kind, base, changed, removed };
}

/**
 * 清理旧备份，只保留最近若干份。
 *
 * 删除前必须处理「保留集里的 delta 依赖于即将被删的锚点」：
 * 先用锚点内容把那个 delta 重建成全量，再删锚点，
 * 否则留下的备份会变成无法还原的孤儿。
 *
 * @param {object} input 入参
 * @param {string} input.dir 备份根目录
 * @param {number} [input.keep] 保留份数
 * @returns {{deleted:string[], reanchored:string[]}} 删除与重建结果
 */
function pruneBackups({ dir, keep = DEFAULT_KEEP }) {
  const all = listBackups(dir);
  if (all.length <= keep) return { deleted: [], reanchored: [] };

  const surviving = all.slice(0, keep);
  const doomed = all.slice(keep);
  const doomedSet = new Set(doomed.map((b) => b.name));

  const reanchored = [];
  for (const item of surviving) {
    if (item.kind !== 'delta' || !item.base || !doomedSet.has(item.base)) continue;
    const baseFile = path.join(dir, item.base, FULL_FILE);
    if (!fs.existsSync(baseFile)) continue;
    try {
      const baseObj = JSON.parse(fs.readFileSync(baseFile, 'utf8'));
      const delta = JSON.parse(fs.readFileSync(path.join(dir, item.name, DELTA_FILE), 'utf8'));
      const restored = JSON.stringify(applyDelta(baseObj, delta));
      fs.writeFileSync(path.join(dir, item.name, FULL_FILE), restored, 'utf8');
      fs.rmSync(path.join(dir, item.name, DELTA_FILE), { force: true });

      const meta = readMeta(dir, item.name) || {};
      meta.kind = 'full';
      meta.base = null;
      meta.reanchoredAt = new Date().toISOString();
      fs.writeFileSync(path.join(dir, item.name, META_FILE), JSON.stringify(meta, null, 2), 'utf8');
      reanchored.push(item.name);
    } catch {
      // 重建失败就把这份一起删掉，宁可少一份备份也不留无法还原的孤儿
      doomedSet.add(item.name);
    }
  }

  const deleted = [];
  for (const name of doomedSet) {
    try {
      fs.rmSync(path.join(dir, name), { recursive: true, force: true });
      deleted.push(name);
    } catch {
      /* 清理失败不影响主流程 */
    }
  }

  // 缓存若指向已被删除的备份就丢掉，避免下一次算差异时基准错位
  const cached = lastContentByDir.get(dir);
  if (cached && !fs.existsSync(path.join(dir, cached.name))) lastContentByDir.delete(dir);

  return { deleted, reanchored };
}

/**
 * 统计备份目录的实际占盘与「若全量存储」的对比，用于向用户说明节省效果。
 *
 * @param {string} dir 备份根目录
 * @returns {{count:number, stored:number, fullSize:number, saved:number}}
 */
function backupStats(dir) {
  const items = listBackups(dir);
  let stored = 0;
  let fullSize = 0;
  for (const b of items) {
    stored += b.storedSize;
    fullSize += b.size;
  }
  return {
    count: items.length,
    stored,
    fullSize,
    saved: Math.max(0, fullSize - stored),
  };
}

module.exports = {
  listBackups,
  loadBackup,
  createBackup,
  pruneBackups,
  backupStats,
  readMeta,
  DEFAULT_KEEP,
  FULL_FILE,
  DELTA_FILE,
  META_FILE,
};
