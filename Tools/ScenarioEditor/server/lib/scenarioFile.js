/**
 * 剧本文件读写模块
 *
 * 职责：
 * 1. 以「保留字段顺序 + CRLF + 2 空格缩进」的原始风格读写 Scenario.json；
 * 2. 写入前做结构性校验，避免把损坏的数据落盘；
 * 3. 每次写入前自动生成一份备份，支持回滚。
 *
 * 所有函数都要求传入 `ws`（工作区），路径全部从工作区取而不是模块级常量——
 * 公网部署时每个登录用户有自己的工作区目录，模块级常量只能指向其中一份。
 * 调用方用 `workspace.resolveWorkspace(req)` 拿工作区，单机模式下它指向工程目录。
 */
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { LOCAL_WORKSPACE } = require('./workspace');
const backupStore = require('./backupStore');

/**
 * 备份保留份数。
 *
 * 原先 60 份 × 1.67 MB ≈ 100 MB/用户，上云后是持续增长的存储成本；
 * 配合增量存储（见 backupStore.js），10 份已足够覆盖「改错了往回退」的日常需求。
 */
const BACKUP_KEEP = 10;

/** 受管理的四大集合（键名 -> 中文名），用于结构校验与统计 */
const COLLECTIONS = [
  { key: 'forceSet', label: '势力' },
  { key: 'corpsSet', label: '军团' },
  { key: 'citySet', label: '都市' },
  { key: 'personSet', label: '武将' },
];

/**
 * 计算内容指纹，用于前端判断文件是否被外部修改。
 *
 * @param {string} content 文件原始内容
 * @returns {string} 短指纹
 */
function fingerprint(content) {
  return crypto.createHash('sha1').update(content).digest('hex').slice(0, 12);
}

/**
 * 取工作区（缺省退回工程目录，便于单测直接调用）。
 *
 * @param {object} [ws] 工作区
 * @returns {object} 工作区
 */
function wsOf(ws) {
  return ws || LOCAL_WORKSPACE;
}

/**
 * 读取剧本文件与元信息。
 *
 * @param {object} [ws] 工作区
 * @returns {{content:string, scenario:any, meta:object}}
 */
function readScenario(ws) {
  const w = wsOf(ws);
  if (!fs.existsSync(w.scenarioFile)) {
    const err = new Error(`剧本文件不存在：${w.scenarioFile}`);
    err.code = 'ENOENT_SCENARIO';
    throw err;
  }
  const content = fs.readFileSync(w.scenarioFile, 'utf8');
  const stat = fs.statSync(w.scenarioFile);
  let scenario;
  try {
    scenario = JSON.parse(content);
  } catch (e) {
    const err = new Error(`剧本文件不是合法 JSON：${e.message}`);
    err.code = 'EBAD_SCENARIO';
    throw err;
  }
  return {
    content,
    scenario,
    meta: {
      file: w.scenarioFile,
      size: stat.size,
      mtime: stat.mtime.toISOString(),
      revision: fingerprint(content),
    },
  };
}

/**
 * 按原始风格把剧本对象序列化为文本（2 空格缩进 + CRLF，末尾无换行）。
 *
 * @param {any} scenario 剧本对象
 * @returns {string} 序列化文本
 */
function serializeScenario(scenario) {
  return JSON.stringify(scenario, null, 2).replace(/\r?\n/g, '\r\n');
}

/**
 * 对即将落盘的剧本做防御性结构校验。
 *
 * 只拦截「一定会让游戏无法运行」的问题，业务层面的字段取值校验由前端完成。
 *
 * @param {any} scenario 剧本对象
 * @returns {string[]} 错误列表，为空表示通过
 */
function validateStructure(scenario) {
  const errors = [];
  if (!scenario || typeof scenario !== 'object' || Array.isArray(scenario)) {
    return ['剧本根节点必须是对象'];
  }
  if (typeof scenario.Id !== 'number') errors.push('缺少顶层 Id 字段或类型不是数字');
  if (typeof scenario.Name !== 'string') errors.push('缺少顶层 Name 字段或类型不是字符串');
  if (!scenario.Info || typeof scenario.Info !== 'object') errors.push('缺少 Info 节点');

  for (const { key, label } of COLLECTIONS) {
    const set = scenario[key];
    if (!set || typeof set !== 'object' || Array.isArray(set)) {
      errors.push(`${label}集合（${key}）缺失或类型错误`);
      continue;
    }
    for (const [id, item] of Object.entries(set)) {
      if (!item || typeof item !== 'object' || Array.isArray(item)) {
        errors.push(`${label}集合（${key}）中 id=${id} 的元素不是对象`);
        continue;
      }
      if (Number(item.Id) !== Number(id)) {
        errors.push(`${label}集合（${key}）中键 ${id} 与元素 Id=${item.Id} 不一致`);
      }
    }
  }
  return errors;
}

/**
 * 把剧本写入磁盘，写入前自动备份。
 *
 * @param {object} ws 工作区
 * @param {any} scenario 剧本对象
 * @param {{baseRevision?:string, skipConflictCheck?:boolean, reason?:string}} [options] 写入选项
 * @returns {{meta:object, backup:string|null}} 写入结果
 */
function writeScenario(ws, scenario, options = {}) {
  const w = wsOf(ws);
  const errors = validateStructure(scenario);
  if (errors.length > 0) {
    const err = new Error(`剧本结构校验未通过：\n- ${errors.slice(0, 20).join('\n- ')}`);
    err.code = 'EVALIDATION';
    err.details = errors;
    throw err;
  }

  // 磁盘内容只读一次：冲突检测与备份都要用，重复读 1.67 MB 没必要
  const hasExisting = fs.existsSync(w.scenarioFile);
  const currentText = hasExisting ? fs.readFileSync(w.scenarioFile, 'utf8') : '';

  // 冲突检测：磁盘内容若已被外部改动，则拒绝覆盖，避免丢失他人修改
  if (!options.skipConflictCheck && hasExisting) {
    const currentRevision = fingerprint(currentText);
    if (options.baseRevision && options.baseRevision !== currentRevision) {
      const err = new Error('磁盘上的剧本文件已被外部修改，请先重新加载再保存');
      err.code = 'ECONFLICT';
      err.currentRevision = currentRevision;
      throw err;
    }
  }

  // 备份现有文件（增量：自动决定存全量还是存差异）
  let backupName = null;
  if (hasExisting) {
    const created = backupStore.createBackup({
      dir: w.backupDir,
      content: currentText,
      reason: options.reason || '自动备份（保存前）',
      file: w.scenarioFile,
      revision: fingerprint(currentText),
    });
    backupName = created.name;
    backupStore.pruneBackups({ dir: w.backupDir, keep: BACKUP_KEEP });
  }

  const text = serializeScenario(scenario);
  fs.mkdirSync(path.dirname(w.scenarioFile), { recursive: true });
  fs.writeFileSync(w.scenarioFile, text, 'utf8');
  const stat = fs.statSync(w.scenarioFile);

  return {
    backup: backupName,
    meta: {
      file: w.scenarioFile,
      size: stat.size,
      mtime: stat.mtime.toISOString(),
      revision: fingerprint(text),
    },
  };
}

/**
 * 列出全部备份（按时间倒序）。
 *
 * @param {object} [ws] 工作区
 * @returns {Array<{name:string, at:string, size:number, reason:string}>}
 */
function listBackups(ws) {
  const w = wsOf(ws);
  return backupStore.listBackups(w.backupDir);
}

/**
 * 从指定备份恢复剧本（恢复前会先把当前文件另存为一份备份）。
 *
 * 备份可能是差异格式，具体还原由 backupStore 负责。
 *
 * @param {object} ws 工作区
 * @param {string} name 备份目录名
 * @returns {{meta:object, restored:string}} 恢复结果
 */
function restoreBackup(ws, name) {
  const w = wsOf(ws);

  // 防御路径穿越：备份名只允许时间戳 + 序号这一种形状
  if (typeof name !== 'string' || !/^[A-Za-z0-9_]+$/.test(name)) {
    const err = new Error(`备份不存在：${name}`);
    err.code = 'ENOENT_BACKUP';
    throw err;
  }
  if (!fs.existsSync(path.join(w.backupDir, name))) {
    const err = new Error(`备份不存在：${name}`);
    err.code = 'ENOENT_BACKUP';
    throw err;
  }

  let scenario;
  try {
    const content = backupStore.loadBackup(w.backupDir, name);
    scenario = JSON.parse(content);
  } catch (e) {
    const err = new Error(`备份内容无法还原：${e.message}`);
    err.code = 'EBAD_BACKUP';
    throw err;
  }

  const result = writeScenario(w, scenario, { skipConflictCheck: true, reason: `回滚到备份 ${name}` });
  return { ...result, restored: name };
}

/**
 * 只保留最近的若干份备份。
 *
 * 具体清理（含「保留集里的差异备份依赖被删锚点」的重建）由 backupStore 负责。
 *
 * @param {number} keep 保留数量
 * @param {object} [ws] 工作区
 * @returns {{deleted:string[], reanchored:string[]}}
 */
function pruneBackups(keep, ws) {
  const w = wsOf(ws);
  return backupStore.pruneBackups({ dir: w.backupDir, keep });
}

module.exports = {
  COLLECTIONS,
  readScenario,
  writeScenario,
  serializeScenario,
  validateStructure,
  listBackups,
  restoreBackup,
  pruneBackups,
  fingerprint,
};
