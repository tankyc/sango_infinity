/**
 * 路径解析模块
 *
 * 统一收敛「剧本文件」「公共数据表」「武将库」三类资源在磁盘上的位置，
 * 允许通过环境变量覆盖，便于把本工具指向其它工程副本。
 */
const path = require('path');

/** 本工具根目录（Tools/ScenarioEditor） */
const TOOL_ROOT = path.resolve(__dirname, '..', '..');

/** 游戏工程根目录（sango_infinity） */
const PROJECT_ROOT = path.resolve(TOOL_ROOT, '..', '..');

/** 剧本 JSON 文件路径 */
const SCENARIO_FILE = process.env.SCENARIO_FILE
  ? path.resolve(process.env.SCENARIO_FILE)
  : path.join(PROJECT_ROOT, 'Build', 'Content', 'Scenario', 'Scenario.json');

/** 公共数据表目录（Common/*.json） */
const COMMON_DIR = process.env.COMMON_DIR
  ? path.resolve(process.env.COMMON_DIR)
  : path.join(PROJECT_ROOT, 'Build', 'Content', 'Data', 'Common');

/** 武将库网站的服务地址（优先从该网站取武将源） */
const LIBRARY_API = process.env.LIBRARY_API || 'http://localhost:3001';

/** 武将库网站本地数据文件（网站未启动时的降级来源） */
const LIBRARY_WEB_FILE = path.join(PROJECT_ROOT, 'Tools', 'PersonLibraryWeb', 'server', 'data', 'PersonLibrary.json');

/** 武将库网站的「自建武将库」数据文件（网站未启动时的降级来源） */
const LIBRARY_CUSTOM_WEB_FILE = path.join(
  PROJECT_ROOT,
  'Tools',
  'PersonLibraryWeb',
  'server',
  'data',
  'CustomPerson.json'
);

/** 游戏工程内置武将库文件（最终降级来源） */
const LIBRARY_GAME_FILE = path.join(PROJECT_ROOT, 'Build', 'Content', 'Data', 'PersonLibrary.json');

/** 备份目录 */
const BACKUP_DIR = path.join(TOOL_ROOT, 'server', 'backups');

/** 公共数据表备份目录 */
const COMMON_BACKUP_DIR = path.join(BACKUP_DIR, 'common');

/** 多用户工作区根目录（WORKSPACE_MODE=multi 时按用户名分目录） */
const WORKSPACE_DIR = process.env.WORKSPACE_DIR
  ? path.resolve(process.env.WORKSPACE_DIR)
  : path.join(TOOL_ROOT, 'server', 'workspaces');

/** 剧本模板库目录（管理员上传，全体共享） */
const TEMPLATE_DIR = process.env.TEMPLATE_DIR
  ? path.resolve(process.env.TEMPLATE_DIR)
  : path.join(TOOL_ROOT, 'server', 'templates');

/** 公共数据基准版目录（管理员上传，用于算差量补丁） */
const BASELINE_DIR = process.env.BASELINE_DIR
  ? path.resolve(process.env.BASELINE_DIR)
  : path.join(TOOL_ROOT, 'server', 'baseline');

/** 武将库网站的令牌密钥文件（复用其账号体系做登录校验） */
const LIBRARY_SECRET_FILE = path.join(
  PROJECT_ROOT,
  'Tools',
  'PersonLibraryWeb',
  'server',
  'data',
  '.token-secret'
);

/** 武将库网站的账号文件（含角色，用于判定管理员） */
const LIBRARY_ACCOUNTS_FILE = path.join(
  PROJECT_ROOT,
  'Tools',
  'PersonLibraryWeb',
  'server',
  'data',
  'accounts.json'
);

module.exports = {
  TOOL_ROOT,
  PROJECT_ROOT,
  SCENARIO_FILE,
  COMMON_DIR,
  LIBRARY_API,
  LIBRARY_WEB_FILE,
  LIBRARY_CUSTOM_WEB_FILE,
  LIBRARY_GAME_FILE,
  BACKUP_DIR,
  COMMON_BACKUP_DIR,
  WORKSPACE_DIR,
  TEMPLATE_DIR,
  BASELINE_DIR,
  LIBRARY_SECRET_FILE,
  LIBRARY_ACCOUNTS_FILE,
};
