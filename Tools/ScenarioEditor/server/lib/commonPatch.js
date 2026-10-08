/**
 * 公共数据基准版与差量补丁
 *
 * 用途：用户改了几个字段，希望导出一份「只含改动」的 JSON 去交付，
 * 而不是把整个文件（可能上万行）都发出去。
 *
 * 基准版由管理员上传，存在 `server/baseline/`，全体用户共用同一份基准；
 * 用户侧的每次改动都相对这份基准计算差异。
 *
 * 差异规则（刻意保持简单可预测）：
 * - **对象递归**：逐键比较，只输出真正变化的键；新增的键按「变化」处理；
 * - **数组与标量整体替换**：数组只要有一个元素不同就整段输出。
 *   逐元素 diff 在插入/删除时索引会错位，得不偿失；
 *   而公共数据表主流形态是「以 Id 为键的对象」（Features / Provinces / Skills…），
 *   对象递归已经覆盖了绝大多数实际改动。
 *
 * 补丁形态：
 * ```jsonc
 * {
 *   "_meta": { fileName, generatedAt, baseRevision, baseAt, fileRevision, changed, removed },
 *   "data": { /* 只含改动字段、保留原始层级 /* },
 *   "removed": [["Features", "12", "name"]]   // 被删掉的路径，用数组避免键名含点时的歧义
 * }
 * ```
 */
const fs = require('fs');
const path = require('path');
const { BASELINE_DIR } = require('./paths');
const { parseJsonc } = require('./jsonc');
const { fingerprint } = require('./scenarioFile');
const { diffValues, buildTree } = require('./jsonDiff');

/** 基准清单文件名 */
const MANIFEST = '_manifest.json';
/** 基准文件名后缀限制，防目录穿越 */
const NAME_PATTERN = /^[^/\\]+\.json$/i;

/**
 * 校验公共数据文件名。
 *
 * @param {string} name 文件名
 * @returns {string} 合法文件名
 */
function assertName(name) {
  if (typeof name !== 'string' || name.includes('..') || !NAME_PATTERN.test(name)) {
    const err = new Error(`非法的文件名：${name}`);
    err.code = 'EBADNAME';
    throw err;
  }
  return name;
}

/** 基准文件绝对路径 */
function baselineFile(name) {
  return path.join(BASELINE_DIR, assertName(name));
}

/* ------------------------------------------------------------------ */
/* 基准清单                                                            */
/* ------------------------------------------------------------------ */

/**
 * 读取基准清单（文件 -> 元信息）。
 *
 * @returns {Record<string, {revision:string, at:string, author:string, size:number}>}
 */
function readManifest() {
  const file = path.join(BASELINE_DIR, MANIFEST);
  if (!fs.existsSync(file)) return {};
  try {
    const parsed = JSON.parse(fs.readFileSync(file, 'utf8'));
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

/**
 * 写回基准清单。
 *
 * @param {object} manifest 清单
 */
function writeManifest(manifest) {
  fs.mkdirSync(BASELINE_DIR, { recursive: true });
  fs.writeFileSync(path.join(BASELINE_DIR, MANIFEST), JSON.stringify(manifest, null, 2), 'utf8');
}

/**
 * 列出全部基准文件。
 *
 * @returns {Array<{name:string, revision:string, at:string, author:string, size:number}>}
 */
function listBaseline() {
  const manifest = readManifest();
  return Object.entries(manifest)
    .map(([name, meta]) => ({ name, ...meta }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

/**
 * 读取某个文件的基准文本。
 *
 * @param {string} name 文件名
 * @returns {string|null} 基准文本；没有基准返回 null
 */
function readBaseline(name) {
  const file = baselineFile(name);
  if (!fs.existsSync(file)) return null;
  return fs.readFileSync(file, 'utf8');
}

/**
 * 把一份内容登记为基准。
 *
 * @param {string} name 文件名
 * @param {string} content 内容
 * @param {string} author 上传者
 * @returns {object} 基准元信息
 */
function writeBaseline(name, content, author = '') {
  assertName(name);
  if (typeof content !== 'string' || content.trim().length === 0) {
    const err = new Error('基准内容为空');
    err.code = 'EVALIDATION';
    throw err;
  }
  // 语法必须能解析，否则基准本身就是坏的，后续 diff 全会失真
  parseJsonc(content);

  fs.mkdirSync(BASELINE_DIR, { recursive: true });
  fs.writeFileSync(baselineFile(name), content, 'utf8');

  const manifest = readManifest();
  manifest[name] = {
    revision: fingerprint(content),
    at: new Date().toISOString(),
    author: String(author || ''),
    size: Buffer.byteLength(content, 'utf8'),
  };
  writeManifest(manifest);
  return { name, ...manifest[name] };
}

/**
 * 删除某个基准。
 *
 * @param {string} name 文件名
 * @returns {{deleted:string}}
 */
function deleteBaseline(name) {
  assertName(name);
  const file = baselineFile(name);
  if (!fs.existsSync(file)) {
    const err = new Error(`基准不存在：${name}`);
    err.code = 'ENOENT_FILE';
    throw err;
  }
  fs.rmSync(file, { force: true });
  const manifest = readManifest();
  delete manifest[name];
  writeManifest(manifest);
  return { deleted: name };
}

/* ------------------------------------------------------------------ */
/* 差异计算                                                            */
/* ------------------------------------------------------------------ */

/**
 * 计算某个文件的差量补丁。
 *
 * @param {string} name 文件名
 * @param {string} baseRaw 基准文本
 * @param {string} currentRaw 当前文本
 * @param {object} [meta] 额外元信息（基准时间等）
 * @returns {object} 补丁对象
 */
function diffFile(name, baseRaw, currentRaw, meta = {}) {
  const base = parseJsonc(baseRaw);
  const current = parseJsonc(currentRaw);
  const delta = diffValues(base, current);

  return {
    _meta: {
      fileName: name,
      generatedAt: new Date().toISOString(),
      baseRevision: fingerprint(baseRaw),
      baseAt: meta.at || '',
      fileRevision: fingerprint(currentRaw),
      changed: delta.changed.length,
      removed: delta.removed.length,
    },
    // 给用户看的补丁用嵌套树（可读性好）；备份用的扁平路径由 backupStore 直接用 diffValues
    data: buildTree(delta.changed),
    removed: delta.removed,
  };
}

/**
 * 判断补丁是否为空（没有任何改动）。
 *
 * @param {object} patch 补丁
 * @returns {boolean} 是否无改动
 */
function isEmptyPatch(patch) {
  return patch._meta.changed === 0 && patch._meta.removed === 0;
}

module.exports = {
  listBaseline,
  readBaseline,
  writeBaseline,
  deleteBaseline,
  diffFile,
  isEmptyPatch,
  buildTree,
  baselineFile,
};

