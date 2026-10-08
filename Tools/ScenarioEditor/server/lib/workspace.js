/**
 * 工作区解析
 *
 * 公网部署时多人共用一个服务，不能都去读写工程目录里的那一份 Scenario.json。
 * 这里按登录用户把数据落到独立目录：
 *
 *   server/workspaces/<用户名>/Scenario.json
 *   server/workspaces/<用户名>/common/<表名>.json
 *   server/workspaces/<用户名>/backups/…
 *
 * 由环境变量 `WORKSPACE_MODE` 决定行为：
 * - `local`（默认）未登录也直接读写工程目录，单机 / 内网用法与以前完全一致；
 * - `multi`        未登录只能只读；写操作要求登录，数据落在各自工作区。
 *
 * 之所以用显式开关而不是「有密钥就自动多用户」，是因为开发机上密钥文件本来就在，
 * 自动切换会让本地保存悄悄写进工作区、看不到工程目录的变化。
 */
const fs = require('fs');
const path = require('path');
const { WORKSPACE_DIR, SCENARIO_FILE, COMMON_DIR, BACKUP_DIR, COMMON_BACKUP_DIR } = require('./paths');

/** 是否启用多用户工作区 */
const MULTI_USER = String(process.env.WORKSPACE_MODE || 'local').toLowerCase() === 'multi';

/** 工程目录工作区（单机模式与只读游客共用） */
const LOCAL_WORKSPACE = {
  owner: 'local',
  label: '工程目录',
  root: path.dirname(SCENARIO_FILE),
  scenarioFile: SCENARIO_FILE,
  commonDir: COMMON_DIR,
  backupDir: BACKUP_DIR,
  commonBackupDir: COMMON_BACKUP_DIR,
  isLocal: true,
  /** 单机模式下可写；多用户模式下未登录视为只读游客 */
  writable: !MULTI_USER,
};

/**
 * 把用户名规整为安全的目录名。
 *
 * 网站侧已用 `[A-Za-z0-9_\-.@]` 限制用户名字符，这里再兜一层，
 * 确保任何情况下都不会因为用户名而穿越出工作区根目录。
 *
 * @param {string} name 用户名
 * @returns {string} 安全的目录名
 */
function safeUsername(name) {
  const cleaned = String(name || '')
    .replace(/[^A-Za-z0-9_-]/g, '_')
    .replace(/_{2,}/g, '_')
    .slice(0, 64);
  return cleaned || 'anonymous';
}

/**
 * 解析当前请求对应的数据工作区。
 *
 * @param {import('express').Request} req 请求对象（需已挂 req.user）
 * @returns {object} 工作区描述
 */
function resolveWorkspace(req) {
  const user = (req && req.user) || null;
  if (!MULTI_USER || !user) {
    return { ...LOCAL_WORKSPACE, user };
  }

  const owner = safeUsername(user.username);
  const root = path.join(WORKSPACE_DIR, owner);
  return {
    owner,
    label: user.displayName || owner,
    root,
    scenarioFile: path.join(root, 'Scenario.json'),
    commonDir: path.join(root, 'common'),
    backupDir: path.join(root, 'backups'),
    commonBackupDir: path.join(root, 'backups', 'common'),
    isLocal: false,
    writable: true,
    user,
  };
}

/**
 * 确保工作区目录存在。
 *
 * @param {object} ws 工作区描述
 */
function ensureWorkspace(ws) {
  for (const dir of [ws.root, ws.commonDir, ws.backupDir, ws.commonBackupDir]) {
    if (dir && !fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  }
}

/**
 * 工作区是否已有剧本（决定用户进来时是空白还是继续编辑）。
 *
 * @param {object} ws 工作区描述
 * @returns {boolean}
 */
function hasScenario(ws) {
  try {
    return fs.existsSync(ws.scenarioFile);
  } catch {
    return false;
  }
}

/**
 * 从模板或上传内容初始化工作区的剧本文件。
 *
 * 已存在剧本时默认不覆盖（除非 force），避免误毁正在编辑的内容。
 *
 * @param {object} ws 工作区描述
 * @param {string} content 剧本文本
 * @param {{force?:boolean}} [options] 选项
 * @returns {{written:boolean, existed:boolean}}
 */
function initScenario(ws, content, options = {}) {
  const existed = hasScenario(ws);
  if (existed && !options.force) return { written: false, existed: true };
  ensureWorkspace(ws);
  fs.writeFileSync(ws.scenarioFile, content, 'utf8');
  return { written: true, existed };
}

/**
 * 当前工作区模式信息（下发给前端做提示）。
 *
 * @returns {{mode:string, multiUser:boolean, root:string}}
 */
function workspaceInfo() {
  return {
    mode: MULTI_USER ? 'multi' : 'local',
    multiUser: MULTI_USER,
    root: MULTI_USER ? WORKSPACE_DIR : path.dirname(SCENARIO_FILE),
  };
}

module.exports = {
  MULTI_USER,
  LOCAL_WORKSPACE,
  safeUsername,
  resolveWorkspace,
  ensureWorkspace,
  hasScenario,
  initScenario,
  workspaceInfo,
};
