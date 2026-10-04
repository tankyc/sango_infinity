/**
 * 剧本模板库
 *
 * 模板是「一份完整的 Scenario.json」，由管理员上传后存在服务端、全体共享。
 * 用户挑一个模板作为起点，内容会被复制到自己的工作区，之后互不影响。
 *
 * 目录结构（每个模板一个子目录，避免文件名与中文名互相牵制）：
 *
 *   server/templates/<模板id>/Scenario.json
 *   server/templates/<模板id>/meta.json
 *
 * id 由「时间戳 + 短随机串」生成，天然有序且不会撞名；
 * 中文名、说明、上传者等放在 meta.json 里，改名不影响目录。
 */
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { TEMPLATE_DIR } = require('./paths');
const { fingerprint, serializeScenario, validateStructure } = require('./scenarioFile');

/** 模板说明与名称的长度上限 */
const NAME_MAX = 60;
const DESC_MAX = 500;

/**
 * 生成模板 id（yyyyMMdd_HHmmss + 4 位随机，便于按时间排序又是唯一目录名）。
 *
 * @param {Date} [date] 时间点
 * @returns {string} 模板 id
 */
function makeId(date = new Date()) {
  const pad = (n) => String(n).padStart(2, '0');
  const stamp =
    `${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}` +
    `_${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}`;
  return `${stamp}_${crypto.randomBytes(2).toString('hex')}`;
}

/**
 * 校验模板 id，防路径穿越。
 *
 * @param {string} id 模板 id
 * @returns {string} 合法 id
 */
function assertId(id) {
  if (typeof id !== 'string' || !/^[A-Za-z0-9_]+$/.test(id)) {
    const err = new Error(`非法的模板 id：${id}`);
    err.code = 'EBADNAME';
    throw err;
  }
  return id;
}

/** 模板目录路径 */
function dirOf(id) {
  return path.join(TEMPLATE_DIR, assertId(id));
}

/**
 * 解析并校验上传/传入的剧本文本。
 *
 * @param {string} content JSON 文本
 * @returns {{scenario:object, text:string}} 解析结果与规范化文本
 */
function parseScenarioContent(content) {
  if (typeof content !== 'string' || content.trim().length === 0) {
    const err = new Error('剧本内容为空');
    err.code = 'EVALIDATION';
    throw err;
  }
  let scenario;
  try {
    scenario = JSON.parse(content);
  } catch (e) {
    const err = new Error(`不是合法的 JSON：${e.message}`);
    err.code = 'EVALIDATION';
    throw err;
  }
  const errors = validateStructure(scenario);
  if (errors.length > 0) {
    const err = new Error(`剧本结构校验未通过：\n- ${errors.slice(0, 10).join('\n- ')}`);
    err.code = 'EVALIDATION';
    err.details = errors;
    throw err;
  }
  // 统一按项目风格落盘，避免不同来源的缩进/换行符混进来
  const text = serializeScenario(scenario);
  return { scenario, text };
}

/**
 * 列出全部模板（按创建时间倒序）。
 *
 * @returns {Array<object>} 模板列表
 */
function listTemplates() {
  if (!fs.existsSync(TEMPLATE_DIR)) return [];
  const result = [];
  for (const id of fs.readdirSync(TEMPLATE_DIR)) {
    const dir = path.join(TEMPLATE_DIR, id);
    try {
      if (!fs.statSync(dir).isDirectory()) continue;
      const scenarioFile = path.join(dir, 'Scenario.json');
      if (!fs.existsSync(scenarioFile)) continue;

      let meta = {};
      const metaFile = path.join(dir, 'meta.json');
      if (fs.existsSync(metaFile)) {
        try {
          meta = JSON.parse(fs.readFileSync(metaFile, 'utf8'));
        } catch {
          /* 元信息损坏时用文件名兜底 */
        }
      }
      const stat = fs.statSync(scenarioFile);
      result.push({
        id,
        name: meta.name || id,
        description: meta.description || '',
        author: meta.author || '',
        createdAt: meta.createdAt || stat.mtime.toISOString(),
        size: stat.size,
        revision: meta.revision || '',
        /** 汇总信息，列表里直接展示，不必为每行去解析 1.7MB 的剧本 */
        counts: meta.counts || null,
        scenarioName: meta.scenarioName || '',
      });
    } catch {
      /* 单个模板损坏不影响整体列表 */
    }
  }
  return result.sort((a, b) => (a.id < b.id ? 1 : -1));
}

/**
 * 读取模板元信息。
 *
 * @param {string} id 模板 id
 * @returns {object} 元信息
 */
function readTemplate(id) {
  const dir = dirOf(id);
  const scenarioFile = path.join(dir, 'Scenario.json');
  if (!fs.existsSync(scenarioFile)) {
    const err = new Error(`模板不存在：${id}`);
    err.code = 'ENOENT_FILE';
    throw err;
  }
  let meta = {};
  const metaFile = path.join(dir, 'meta.json');
  if (fs.existsSync(metaFile)) {
    try {
      meta = JSON.parse(fs.readFileSync(metaFile, 'utf8'));
    } catch {
      /* 忽略损坏的元信息 */
    }
  }
  return { id, dir, scenarioFile, meta };
}

/**
 * 读取模板的剧本文本（模板是纯 JSON，没有注释，可直接返回原文）。
 *
 * @param {string} id 模板 id
 * @returns {{content:string, name:string}} 内容与建议文件名
 */
function readTemplateContent(id) {
  const { scenarioFile, meta } = readTemplate(id);
  return { content: fs.readFileSync(scenarioFile, 'utf8'), name: meta.name || id };
}

/**
 * 统计剧本的集合规模，存进 meta 供列表快速展示。
 *
 * @param {object} scenario 剧本对象
 * @returns {object} 计数
 */
function countEntities(scenario) {
  const out = {};
  for (const key of ['forceSet', 'corpsSet', 'citySet', 'personSet']) {
    out[key] = scenario && scenario[key] && typeof scenario[key] === 'object' ? Object.keys(scenario[key]).length : 0;
  }
  return out;
}

/**
 * 新增模板。
 *
 * @param {object} input { name, description, content, author }
 * @returns {object} 新模板的列表项
 */
function createTemplate(input = {}) {
  const name = String(input.name || '').trim().slice(0, NAME_MAX);
  if (!name) {
    const err = new Error('模板名称不能为空');
    err.code = 'EVALIDATION';
    throw err;
  }
  const { scenario, text } = parseScenarioContent(input.content);

  const id = makeId();
  const dir = dirOf(id);
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'Scenario.json'), text, 'utf8');

  const meta = {
    name,
    description: String(input.description || '').trim().slice(0, DESC_MAX),
    author: String(input.author || '').trim(),
    createdAt: new Date().toISOString(),
    revision: fingerprint(text),
    scenarioName: String(scenario.Name || ''),
    counts: countEntities(scenario),
  };
  fs.writeFileSync(path.join(dir, 'meta.json'), JSON.stringify(meta, null, 2), 'utf8');

  return listTemplates().find((t) => t.id === id);
}

/**
 * 删除模板。
 *
 * @param {string} id 模板 id
 * @returns {{deleted:string}}
 */
function deleteTemplate(id) {
  const dir = dirOf(id);
  if (!fs.existsSync(dir)) {
    const err = new Error(`模板不存在：${id}`);
    err.code = 'ENOENT_FILE';
    throw err;
  }
  fs.rmSync(dir, { recursive: true, force: true });
  return { deleted: id };
}

/**
 * 把一个 JSON 文本写入目标路径，并在覆盖前把原文件另存为 .bak。
 *
 * 用来实现「从模板开始」与「上传剧本替换当前剧本」，
 * 两者本质上都是「用一段新内容初始化当前工作区」。
 *
 * @param {string} targetFile 目标剧本文件
 * @param {string} content 新内容（原始 JSON 文本）
 * @param {{force?:boolean, keepBackup?:boolean}} [options] 选项
 * @returns {{written:boolean, existed:boolean, backup:string|null, revision:string}}
 */
function writeTargetScenario(targetFile, content, options = {}) {
  const existed = fs.existsSync(targetFile);
  if (existed && !options.force) {
    return { written: false, existed: true, backup: null, revision: '' };
  }

  let backup = null;
  if (existed && options.keepBackup !== false) {
    backup = `${targetFile}.${Date.now()}.bak`;
    fs.copyFileSync(targetFile, backup);
  }

  fs.mkdirSync(path.dirname(targetFile), { recursive: true });
  fs.writeFileSync(targetFile, content, 'utf8');
  return { written: true, existed, backup, revision: fingerprint(content) };
}

module.exports = {
  listTemplates,
  readTemplate,
  readTemplateContent,
  createTemplate,
  deleteTemplate,
  parseScenarioContent,
  writeTargetScenario,
  countEntities,
};
