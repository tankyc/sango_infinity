/**
 * 公共数据表目录管理模块
 *
 * 负责 Build/Content/Data/Common 目录下全部 *.json 的读取与保存：
 * 1. 列出文件清单（含结构摘要，供前端决定用「记录表」还是「键值表」呈现）；
 * 2. 读取原始文本与解析结果，保留注释与格式信息；
 * 3. 保存时先做 JSONC 语法与「注释策略」校验，再备份、冲突检测、落盘。
 *
 * 注意：本目录存在含注释的 JSONC 文件（AIConfig.json、Personalities.json、
 * RecommendedTroopTeams.json），因此保存采用「按原文改写」而非重新序列化，
 * 由前端借助 jsonc-parser 的 modify 生成最小差异文本，从而保留注释与格式。
 */
const fs = require('fs');
const path = require('path');
const { LOCAL_WORKSPACE } = require('./workspace');
const { stripJsonComments, parseJsonc } = require('./jsonc');
const { fingerprint } = require('./scenarioFile');

/**
 * 取工作区（缺省退回工程目录）。
 *
 * 与 scenarioFile 一样，目录不再来自模块级常量：公网部署时每个登录用户
 * 有自己的公共数据副本，路径必须按请求解析。
 *
 * @param {object} [ws] 工作区
 * @returns {object} 工作区
 */
function wsOf(ws) {
  return ws || LOCAL_WORKSPACE;
}

/**
 * 备份保留数量。
 *
 * 这里保持**全量**存储：公共数据的备份是「按文件」做的，单个文件几十 KB
 * （实测 29 份共 0.24 MB），做增量的收益远小于多一层差异链的维护成本。
 * 真正吃空间的是剧本备份（单份 1.67 MB），那部分已改增量。
 */
const BACKUP_KEEP = 10;

/**
 * 不在「公共数据」里管理的文件。
 *
 * - PersonLibrary.json（内置武将库，800+ 条、1.2MB）：它有独立的维护入口
 *   （Tools/PersonLibraryWeb 武将库网站 + 编辑器里的「从武将库导入 / 回填」），
 *   放在公共数据里既重复又拖慢清单解析，因此从列表中隐藏。
 *
 * 隐藏只影响「清单」，按名字直接读取/保存该文件的接口依然可用，
 * 且武将库接口（server/lib/library.js）走的是另一条路径，不受影响。
 */
const HIDDEN_FILES = new Set(['PersonLibrary.json']);

/**
 * 生成备份时间戳（本地时间 yyyyMMdd_HHmmss）。
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
 * 校验并解析文件名为目录内的合法文件。
 *
 * 防御路径穿越：只接受「不含路径分隔符、以 .json 结尾、且真实存在于工作区公共数据目录」的名字。
 *
 * @param {object} ws 工作区
 * @param {string} name 文件名
 * @returns {string} 绝对路径
 */
function resolveFile(ws, name) {
  const w = wsOf(ws);
  if (typeof name !== 'string' || name.length === 0) {
    const err = new Error('缺少文件名');
    err.code = 'EBADNAME';
    throw err;
  }
  if (name.includes('/') || name.includes('\\') || name.includes('..') || !name.toLowerCase().endsWith('.json')) {
    const err = new Error(`非法的文件名：${name}`);
    err.code = 'EBADNAME';
    throw err;
  }
  const full = path.join(w.commonDir, name);
  if (!fs.existsSync(full) || !fs.statSync(full).isFile()) {
    const err = new Error(`文件不存在：${name}`);
    err.code = 'ENOENT_FILE';
    throw err;
  }
  return full;
}

/**
 * 检测文本使用的换行符。
 *
 * @param {string} text 文本
 * @returns {string} '\r\n' 或 '\n'
 */
function detectEol(text) {
  const i = text.indexOf('\n');
  if (i > 0 && text[i - 1] === '\r') return '\r\n';
  return '\n';
}

/**
 * 检测缩进宽度（取第一个出现缩进的行的前导空格数）。
 *
 * @param {string} text 文本
 * @returns {number} 缩进空格数，默认 2
 */
function detectIndent(text) {
  const m = text.match(/\n([ \t]+)\S/);
  if (!m) return 2;
  return m[1].includes('\t') ? 2 : Math.max(1, m[1].length);
}

/**
 * 判断文本是否含有 JSONC 注释。
 *
 * @param {string} text 文本
 * @returns {boolean} 是否含注释
 */
function hasComments(text) {
  const stripped = stripJsonComments(text);
  // 剥离后会去掉注释行，长度差异即代表存在注释
  return stripped.replace(/\r?\n\s*/g, '\n').trim() !== text.replace(/\r?\n\s*/g, '\n').trim();
}

/**
 * 判断是否为「普通对象」（排除 null 与数组）。
 *
 * @param {any} v 值
 * @returns {boolean}
 */
function isPlainObject(v) {
  return Boolean(v) && typeof v === 'object' && !Array.isArray(v);
}

/**
 * 判定一个值属于哪种可编辑形态。
 *
 * - records：以 id 为键的对象集合，或对象数组 —— 适合用「记录表」编辑
 * - values：原始值数组 —— 适合用「值列表」编辑
 * - config：键值配置对象 —— 适合用「分组键值表」编辑
 * - value：单个原始值
 *
 * @param {any} v 值
 * @returns {{kind:string, form:string, entryCount:number, sampleFields:string[]}}
 */
function classifyValue(v) {
  if (Array.isArray(v)) {
    const first = v.find((x) => isPlainObject(x));
    if (first) {
      return { kind: 'records', form: 'array', entryCount: v.length, sampleFields: Object.keys(first) };
    }
    return { kind: 'values', form: 'array', entryCount: v.length, sampleFields: [] };
  }
  if (isPlainObject(v)) {
    const keys = Object.keys(v);
    const first = v[keys[0]];
    if (isPlainObject(first)) {
      return { kind: 'records', form: 'object', entryCount: keys.length, sampleFields: Object.keys(first) };
    }
    return { kind: 'config', form: 'object', entryCount: keys.length, sampleFields: keys.slice(0, 80) };
  }
  return { kind: 'value', form: 'scalar', entryCount: 0, sampleFields: [] };
}

/**
 * 推断文件的结构摘要，供前端选择呈现方式。
 *
 * 一个文件可能包含多个「分区」（例如 ai.json 顶层就有 Officials 与 ai 两张记录表，
 * debatePersonBehaviour.json 则是 behaviours 记录表 + _comment 说明文本）。
 * 因此返回 sections 数组，由前端渲染分区切换。
 *
 * @param {any} value 解析结果
 * @returns {object} 结构摘要
 */
function summarize(value) {
  // 根就是数组
  if (Array.isArray(value)) {
    const cls = classifyValue(value);
    return {
      wrapper: null,
      rootKeys: [],
      sections: [{ key: null, ...cls }],
      kind: cls.kind,
      entryCount: cls.entryCount,
    };
  }

  if (!isPlainObject(value)) {
    return { wrapper: null, rootKeys: [], sections: [], kind: 'value', entryCount: 0 };
  }

  const rootKeys = Object.keys(value);

  // 单键包装（Provinces.json -> Provinces、RecommendedTroopTeams.json -> teams）
  if (rootKeys.length === 1) {
    const k = rootKeys[0];
    const cls = classifyValue(value[k]);
    return {
      wrapper: k,
      rootKeys,
      sections: [{ key: k, ...cls }],
      kind: cls.kind,
      entryCount: cls.entryCount,
    };
  }

  // 多键：记录集合型键各自成为一个分区，其余键归入「根配置」分区
  const collections = [];
  const others = [];
  for (const k of rootKeys) {
    const cls = classifyValue(value[k]);
    if (cls.kind === 'records' && cls.entryCount > 0) collections.push({ key: k, ...cls });
    else others.push(k);
  }

  const sections = [...collections];
  if (collections.length === 0) {
    sections.push({
      key: null,
      kind: 'config',
      form: 'object',
      entryCount: rootKeys.length,
      sampleFields: rootKeys.slice(0, 80),
    });
  } else if (others.length > 0) {
    sections.push({
      key: null,
      kind: 'config',
      form: 'object',
      entryCount: others.length,
      sampleFields: others.slice(0, 80),
      keys: others,
    });
  }

  return {
    wrapper: null,
    rootKeys,
    sections,
    kind: sections[0] ? sections[0].kind : 'value',
    entryCount: sections[0] ? sections[0].entryCount : 0,
  };
}

/**
 * 列出目录下全部公共数据表及结构摘要（带缓存，保存后失效）。
 *
 * @param {boolean} [noCache] 是否强制刷新
 * @returns {Array<object>} 文件清单
 */
/** 清单缓存，按公共数据目录分别缓存（多用户下各工作区互不干扰） */
const listCache = new Map();
function listFiles(ws, noCache = false) {
  const w = wsOf(ws);
  const cached = listCache.get(w.commonDir);
  if (cached && !noCache) return cached;
  const result = [];
  let names = [];
  try {
    names = fs.readdirSync(w.commonDir).filter((f) => f.toLowerCase().endsWith('.json'));
  } catch (e) {
    const err = new Error(`无法读取公共数据表目录：${w.commonDir}`);
    err.code = 'ENOENT_COMMON_DIR';
    throw err;
  }
  names = names.filter((n) => !HIDDEN_FILES.has(n));
  names.sort((a, b) => a.localeCompare(b));
  for (const name of names) {
    const full = path.join(w.commonDir, name);
    const stat = fs.statSync(full);
    const raw = fs.readFileSync(full, 'utf8');
    const item = {
      name,
      size: stat.size,
      mtime: stat.mtime.toISOString(),
      revision: fingerprint(raw),
      jsonc: hasComments(raw),
      eol: detectEol(raw) === '\r\n' ? 'CRLF' : 'LF',
      indent: detectIndent(raw),
      parseError: null,
      summary: null,
    };
    try {
      item.summary = summarize(parseJsonc(raw));
    } catch (e) {
      item.parseError = e.message;
    }
    result.push(item);
  }
  listCache.set(w.commonDir, result);
  return result;
}

/**
 * 读取单个公共数据表。
 *
 * @param {object} ws 工作区
 * @param {string} name 文件名
 * @returns {object} 文件内容与元信息
 */
function readCommonFile(ws, name) {
  const full = resolveFile(ws, name);
  const raw = fs.readFileSync(full, 'utf8');
  const stat = fs.statSync(full);
  let value = null;
  let parseError = null;
  try {
    value = parseJsonc(raw);
  } catch (e) {
    parseError = e.message;
  }
  return {
    name,
    file: full,
    raw,
    value,
    parseError,
    jsonc: hasComments(raw),
    eol: detectEol(raw),
    indent: detectIndent(raw),
    revision: fingerprint(raw),
    size: stat.size,
    mtime: stat.mtime.toISOString(),
    summary: value === null ? null : summarize(value),
  };
}

/**
 * 列出某个文件的备份（按时间倒序）。
 *
 * @param {object} ws 工作区
 * @param {string} [name] 仅列出该文件的备份；不传则列出全部
 * @returns {Array<object>} 备份列表
 */
function listBackups(ws, name) {
  const w = wsOf(ws);
  if (!fs.existsSync(w.commonBackupDir)) return [];
  const result = [];
  for (const dirName of fs.readdirSync(w.commonBackupDir)) {
    const dir = path.join(w.commonBackupDir, dirName);
    if (!fs.statSync(dir).isDirectory()) continue;
    const metaFile = path.join(dir, 'meta.json');
    let meta = {};
    if (fs.existsSync(metaFile)) {
      try {
        meta = JSON.parse(fs.readFileSync(metaFile, 'utf8'));
      } catch {
        /* 元信息损坏时忽略 */
      }
    }
    if (name && meta.fileName !== name) continue;
    const at = meta.at || fs.statSync(dir).mtime.toISOString();
    result.push({ dir: dirName, name: meta.fileName || '', at, reason: meta.reason || '', size: meta.size || 0 });
  }
  return result.sort((a, b) => (a.dir < b.dir ? 1 : -1));
}

/**
 * 清理过期备份。
 *
 * @param {object} ws 工作区
 * @param {number} keep 保留数量
 */
function pruneBackups(ws, keep) {
  const w = wsOf(ws);
  const all = listBackups(w);
  for (const item of all.slice(keep)) {
    try {
      fs.rmSync(path.join(w.commonBackupDir, item.dir), { recursive: true, force: true });
    } catch {
      /* 清理失败不影响主流程 */
    }
  }
}

/**
 * 保存公共数据表。
 *
 * 流程：语法校验 -> 注释策略校验 -> 冲突检测 -> 备份 -> 落盘。
 *
 * @param {object} ws 工作区
 * @param {object} payload 请求体 { name, raw, baseRevision }
 * @returns {object} 保存结果
 */
function writeCommonFile(ws, payload) {
  const w = wsOf(ws);
  const { name, raw, baseRevision } = payload || {};
  const full = resolveFile(w, name);

  if (typeof raw !== 'string' || raw.trim().length === 0) {
    const err = new Error('内容为空，拒绝写入');
    err.code = 'EVALIDATION';
    throw err;
  }

  // 语法校验：必须是可解析的 JSON/JSONC
  try {
    parseJsonc(raw);
  } catch (e) {
    const err = new Error(`内容不是合法的 JSON：${e.message}`);
    err.code = 'EVALIDATION';
    err.details = [e.message];
    throw err;
  }

  const current = fs.readFileSync(full, 'utf8');
  const originalJsonc = hasComments(current);
  if (!originalJsonc && hasComments(raw)) {
    const err = new Error('该文件原本不含注释，写入注释可能导致游戏侧解析失败，已拒绝保存');
    err.code = 'EVALIDATION';
    err.details = ['如需添加注释，请先确认游戏加载器支持（本工具不主动为该文件引入注释）'];
    throw err;
  }

  const currentRevision = fingerprint(current);
  if (baseRevision && baseRevision !== currentRevision) {
    const err = new Error('磁盘上的文件已被外部修改，请先重新加载再保存');
    err.code = 'ECONFLICT';
    err.currentRevision = currentRevision;
    throw err;
  }

  // 备份目录以「时间戳」命名；同一秒内多次保存会重名，追加序号避免互相覆盖
  const ts = timestamp();
  let dirName = ts;
  let seq = 1;
  while (fs.existsSync(path.join(w.commonBackupDir, dirName))) {
    dirName = `${ts}_${seq}`;
    seq += 1;
  }
  const dir = path.join(w.commonBackupDir, dirName);
  fs.mkdirSync(dir, { recursive: true });
  fs.copyFileSync(full, path.join(dir, name));
  fs.writeFileSync(
    path.join(dir, 'meta.json'),
    JSON.stringify(
      {
        reason: '自动备份（公共数据表保存前）',
        fileName: name,
        at: new Date().toISOString(),
        size: Buffer.byteLength(current, 'utf8'),
      },
      null,
      2
    ),
    'utf8'
  );
  pruneBackups(w, BACKUP_KEEP);

  fs.writeFileSync(full, raw, 'utf8');
  const stat = fs.statSync(full);
  listCache.delete(w.commonDir);

  return {
    name,
    backup: dirName,
    meta: {
      file: full,
      size: stat.size,
      mtime: stat.mtime.toISOString(),
      revision: fingerprint(raw),
    },
  };
}

/**
 * 从备份恢复某个公共数据表（恢复前把当前文件另存一份备份）。
 *
 * @param {object} ws 工作区
 * @param {string} dir 备份目录名
 * @returns {object} 恢复结果
 */
function restoreBackup(ws, dir) {
  const w = wsOf(ws);
  if (typeof dir !== 'string' || dir.includes('/') || dir.includes('\\') || dir.includes('..')) {
    const err = new Error(`非法的备份名：${dir}`);
    err.code = 'EBADNAME';
    throw err;
  }
  const metaFile = path.join(w.commonBackupDir, dir, 'meta.json');
  if (!fs.existsSync(metaFile)) {
    const err = new Error(`备份不存在：${dir}`);
    err.code = 'ENOENT_BACKUP';
    throw err;
  }
  const meta = JSON.parse(fs.readFileSync(metaFile, 'utf8'));
  const backupFile = path.join(w.commonBackupDir, dir, meta.fileName);
  if (!fs.existsSync(backupFile)) {
    const err = new Error(`备份内容缺失：${meta.fileName}`);
    err.code = 'ENOENT_BACKUP';
    throw err;
  }
  const raw = fs.readFileSync(backupFile, 'utf8');
  const result = writeCommonFile(w, { name: meta.fileName, raw, baseRevision: null });
  return { ...result, restored: dir };
}

module.exports = {
  resolveFile,
  listFiles,
  readCommonFile,
  writeCommonFile,
  listBackups,
  restoreBackup,
  hasComments,
  detectEol,
  detectIndent,
  summarize,
};
