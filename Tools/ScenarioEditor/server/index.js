/**
 * 剧本编辑器后端服务
 *
 * 提供三类能力：
 * 1. 剧本文件的读取 / 保存 / 冲突检测 / 备份回滚；
 * 2. 游戏公共数据表（枚举、特性、官职、州、都市等级等）的只读下发；
 * 3. 武将库数据代理（优先从「武将库网站」拉取，失败自动降级到本地文件）。
 *
 * 生产模式下同时托管前端构建产物 dist/。
 */
const express = require('express');
const fs = require('fs');
const path = require('path');
const { TOOL_ROOT, LIBRARY_API } = require('./lib/paths');
const scenarioFile = require('./lib/scenarioFile');
const commonFiles = require('./lib/commonFiles');
const templates = require('./lib/templates');
const commonPatch = require('./lib/commonPatch');
const auth = require('./lib/auth');
const workspace = require('./lib/workspace');
const { getOptions } = require('./lib/options');
const { getLibrary } = require('./lib/library');

const app = express();
const PORT = Number(process.env.PORT) || 3009;
const DIST_DIR = path.join(TOOL_ROOT, 'dist');

app.use(express.json({ limit: '64mb' }));

/**
 * 登录解析：把 req.user 挂上（未登录为 null，即游客）。
 *
 * 放在所有业务路由之前。校验失败不在这里拒绝，
 * 因为「浏览剧本」对游客开放，只有写操作才需要登录。
 */
app.use(auth.attachUser);

/**
 * 取当前请求的数据工作区。
 *
 * @param {import('express').Request} req 请求对象
 * @returns {object} 工作区
 */
function wsOf(req) {
  return workspace.resolveWorkspace(req);
}

/**
 * 写操作守卫：工作区不可写时统一拒绝。
 *
 * 单机模式（WORKSPACE_MODE=local）下人人可写，行为与以前完全一致；
 * 多用户模式下未登录的游客只能看，避免匿名请求写坏工程目录。
 *
 * @param {import('express').Request} req 请求对象
 * @param {import('express').Response} res 响应对象
 * @returns {object|null} 可写时返回工作区，否则已响应并返回 null
 */
function requireWrite(req, res) {
  const ws = wsOf(req);
  if (ws.writable) return ws;
  res.status(401).json({
    ok: false,
    code: 'EAUTH',
    message: '请先登录后再修改数据',
  });
  return null;
}

/**
 * 管理员守卫：上传模板 / 基准版需要 admin 及以上角色。
 *
 * @param {import('express').Request} req 请求对象
 * @param {import('express').Response} res 响应对象
 * @returns {boolean} 是否放行
 */
function requireAdmin(req, res) {
  if (!auth.isAuthAvailable()) {
    // 认证不可用（找不到密钥文件）时退化为单机模式，视为本地管理员
    return true;
  }
  if (req.user && req.user.isAdmin) return true;
  res.status(403).json({
    ok: false,
    code: 'EFORBIDDEN',
    message: req.user ? '该操作需要管理员权限' : '请先用管理员账号登录',
  });
  return false;
}

/**
 * 统一异常响应包装。
 *
 * @param {import('express').Response} res 响应对象
 * @param {Error} err 错误对象
 * @param {number} [status] HTTP 状态码
 */
function fail(res, err, status = 500) {
  const code = err.code || 'EUNKNOWN';
  const map = {
    ENOENT_SCENARIO: 404,
    ENOENT_FILE: 404,
    ENOENT_BACKUP: 404,
    ENOENT_COMMON_DIR: 404,
    EBADNAME: 400,
    EVALIDATION: 400,
    EBAD_SCENARIO: 500,
    EBAD_BACKUP: 400,
    ECONFLICT: 409,
  };
  res.status(map[code] || status).json({
    ok: false,
    code,
    message: err.message,
    details: err.details || undefined,
    currentRevision: err.currentRevision || undefined,
  });
}

/** 健康检查与运行环境信息 */
app.get('/api/health', (req, res) => {
  const ws = wsOf(req);
  res.json({
    ok: true,
    scenarioFile: ws.scenarioFile,
    scenarioExists: fs.existsSync(ws.scenarioFile),
    libraryApi: LIBRARY_API,
    port: PORT,
    workspace: workspace.workspaceInfo(),
  });
});

/**
 * 当前登录状态与权限。
 *
 * 前端据此决定是否显示「上传模板 / 上传剧本」等管理员入口。
 * 认证不可用时（找不到密钥文件）按单机模式处理，视为本地管理员。
 */
app.get('/api/auth/me', (req, res) => {
  const available = auth.isAuthAvailable();
  const user = req.user;
  res.json({
    ok: true,
    authAvailable: available,
    /** 认证不可用时退化为单机模式，本地即管理员 */
    multiUser: workspace.MULTI_USER,
    user,
    canWrite: wsOf(req).writable,
    canAdmin: !available || Boolean(user && user.isAdmin),
  });
});

/**
 * 登录：代理到武将库网站的登录接口。
 *
 * 之所以由本服务代转而不是让浏览器直连网站：
 * 1. 两个服务不同端口，浏览器直连要处理 CORS；
 * 2. 令牌签发逻辑只有网站清楚，代理转发可以把「取令牌的方式」这一层完全封在里面，
 *    将来网站换认证方式，编辑器不用改前端。
 *
 * 令牌本身仍由编辑器独立校验（见 lib/auth.js），代理只负责「换令牌」这一步。
 */
app.post('/api/auth/login', async (req, res) => {
  try {
    const { username, password } = req.body || {};
    if (!username || !password) {
      const err = new Error('请填写账号与密码');
      err.code = 'EVALIDATION';
      throw err;
    }

    let upstream;
    try {
      upstream = await fetch(`${LIBRARY_API}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
        signal: AbortSignal.timeout(8000),
      });
    } catch (e) {
      const err = new Error(`无法连接武将库网站（${LIBRARY_API}）：${e.message}`);
      err.code = 'ENOENT_SCENARIO';
      throw err;
    }

    const data = await upstream.json().catch(() => null);
    if (!upstream.ok || !data || !data.token) {
      res.status(upstream.status === 200 ? 401 : upstream.status).json({
        ok: false,
        code: 'EAUTH',
        message: (data && (data.message || data.error)) || '账号或密码不正确',
      });
      return;
    }

    // 用刚拿到的令牌反查一次，把角色 / 显示名一并返回给前端
    const user = auth.resolveUser({ headers: { authorization: `Bearer ${data.token}` }, query: {} });
    res.json({ ok: true, token: data.token, user });
  } catch (e) {
    fail(res, e, 502);
  }
});

/** 读取剧本 */
app.get('/api/scenario', (req, res) => {
  try {
    const ws = wsOf(req);
    // 多用户模式下用户还没初始化剧本时，给一个明确的空状态而不是 500
    if (!fs.existsSync(ws.scenarioFile)) {
      res.json({
        ok: true,
        scenario: null,
        meta: null,
        needInit: true,
        workspace: { owner: ws.owner, label: ws.label, isLocal: ws.isLocal },
      });
      return;
    }
    const { scenario, meta } = scenarioFile.readScenario(ws);
    res.json({ ok: true, scenario, meta, workspace: { owner: ws.owner, label: ws.label, isLocal: ws.isLocal } });
  } catch (e) {
    fail(res, e);
  }
});

/** 仅读取元信息（用于轮询检测外部修改） */
app.get('/api/scenario/meta', (req, res) => {
  try {
    const { meta } = scenarioFile.readScenario(wsOf(req));
    res.json({ ok: true, meta });
  } catch (e) {
    fail(res, e);
  }
});

/**
 * 下载剧本文件。
 *
 * 直接下发磁盘上的原件，保留原有的 2 空格缩进与 CRLF，
 * 保证拿到的文件与游戏侧实际读取的完全一致（未保存的改动不在其中）。
 *
 * 浏览器直链无法自定义请求头，因此令牌也接受 `?token=` 形式（由 auth 模块统一提取）。
 */
app.get('/api/scenario/download', (req, res) => {
  try {
    const { content, meta } = scenarioFile.readScenario(wsOf(req));
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    res.setHeader('Content-Disposition', 'attachment; filename="Scenario.json"; filename*=UTF-8\'\'Scenario.json');
    res.setHeader('Content-Length', Buffer.byteLength(content, 'utf8'));
    // 便于下载方核对拿到的是哪个版本
    res.setHeader('X-Scenario-Revision', meta.revision);
    res.send(content);
  } catch (e) {
    fail(res, e);
  }
});

/**
 * 上传剧本覆盖当前工作区。
 *
 * 请求体是**原始 JSON 文本**（Content-Type: text/plain），
 * 而不是再包一层 JSON 字符串——1.7MB 的剧本二次转义既浪费带宽又容易出错。
 * 覆盖前会把原文件另存为 `.bak`，且默认要求显式 force 才覆盖已有剧本。
 */
app.post('/api/scenario/upload', express.text({ type: 'text/plain', limit: '64mb' }), (req, res) => {
  try {
    const ws = requireWrite(req, res);
    if (!ws) return;

    const { scenario, text } = templates.parseScenarioContent(req.body);
    const force = req.query.force === '1';
    const result = templates.writeTargetScenario(ws.scenarioFile, text, { force });
    if (!result.written) {
      res.status(409).json({
        ok: false,
        code: 'EEXISTS',
        message: '当前工作区已有剧本，覆盖会丢弃现有内容',
      });
      return;
    }
    res.json({
      ok: true,
      name: String(scenario.Name || ''),
      revision: result.revision,
      backup: result.backup,
      replaced: result.existed,
    });
  } catch (e) {
    fail(res, e);
  }
});

/** 保存剧本 */
app.put('/api/scenario', (req, res) => {
  try {
    const ws = requireWrite(req, res);
    if (!ws) return;

    const { scenario, baseRevision } = req.body || {};
    if (!scenario) {
      const err = new Error('请求体缺少 scenario 字段');
      err.code = 'EVALIDATION';
      throw err;
    }
    const result = scenarioFile.writeScenario(ws, scenario, { baseRevision });
    res.json({ ok: true, meta: result.meta, backup: result.backup });
  } catch (e) {
    fail(res, e);
  }
});

/** 备份列表 */
app.get('/api/backups', (req, res) => {
  try {
    res.json({ ok: true, backups: scenarioFile.listBackups(wsOf(req)) });
  } catch (e) {
    fail(res, e);
  }
});

/** 从备份恢复 */
app.post('/api/backups/:name/restore', (req, res) => {
  try {
    const ws = requireWrite(req, res);
    if (!ws) return;

    const result = scenarioFile.restoreBackup(ws, req.params.name);
    res.json({ ok: true, meta: result.meta, restored: result.restored, backup: result.backup });
  } catch (e) {
    fail(res, e);
  }
});

/* ------------------------------------------------------------------ */
/* 剧本模板库                                                          */
/* ------------------------------------------------------------------ */

/** 模板列表（游客可浏览，便于挑一个作为起点） */
app.get('/api/templates', (req, res) => {
  try {
    res.json({ ok: true, templates: templates.listTemplates() });
  } catch (e) {
    fail(res, e);
  }
});

/** 下载某个模板（浏览器直链，令牌可用 ?token=） */
app.get('/api/templates/:id/download', (req, res) => {
  try {
    const { content, name } = templates.readTemplateContent(req.params.id);
    const safe = name.replace(/[\\/:*?"<>|]/g, '_') || 'Scenario';
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    res.setHeader(
      'Content-Disposition',
      `attachment; filename="Scenario.json"; filename*=UTF-8''${encodeURIComponent(`${safe}.json`)}`
    );
    res.setHeader('Content-Length', Buffer.byteLength(content, 'utf8'));
    res.send(content);
  } catch (e) {
    fail(res, e);
  }
});

/**
 * 新增模板（仅管理员）。
 *
 * 请求体是原始 JSON 文本（text/plain）时按「直接上传剧本文件」处理；
 * 是 application/json 时按 { name, description, content } 处理，
 * 其中 content 也可以省略，表示「把当前工作区的剧本存成模板」。
 */
app.post(
  '/api/templates',
  express.text({ type: 'text/plain', limit: '64mb' }),
  (req, res) => {
    try {
      if (!requireAdmin(req, res)) return;

      let payload;
      if (typeof req.body === 'string') {
        payload = { name: String(req.query.name || '').trim(), description: '', content: req.body };
      } else {
        const body = req.body || {};
        payload = {
          name: body.name,
          description: body.description,
          content: body.content || scenarioFile.readScenario(wsOf(req)).content,
          author: req.user ? req.user.displayName : '本地管理员',
        };
      }
      if (!payload.name) {
        const err = new Error('请填写模板名称');
        err.code = 'EVALIDATION';
        throw err;
      }
      res.json({ ok: true, template: templates.createTemplate(payload) });
    } catch (e) {
      fail(res, e);
    }
  }
);

/** 删除模板（仅管理员） */
app.delete('/api/templates/:id', (req, res) => {
  try {
    if (!requireAdmin(req, res)) return;
    res.json({ ok: true, ...templates.deleteTemplate(req.params.id) });
  } catch (e) {
    fail(res, e);
  }
});

/**
 * 从模板开始：把模板内容写进当前工作区。
 *
 * 已有剧本时必须显式 `?force=1`，否则返回 409 让前端二次确认，
 * 避免点了模板就把正在编辑的内容冲掉。
 */
app.post('/api/templates/:id/apply', (req, res) => {
  try {
    const ws = requireWrite(req, res);
    if (!ws) return;

    const { content } = templates.readTemplateContent(req.params.id);
    const force = req.query.force === '1';
    const result = templates.writeTargetScenario(ws.scenarioFile, content, { force });
    if (!result.written) {
      res.status(409).json({
        ok: false,
        code: 'EEXISTS',
        message: '当前工作区已有剧本，套用模板会覆盖现有内容',
      });
      return;
    }
    res.json({ ok: true, revision: result.revision, backup: result.backup, replaced: result.existed });
  } catch (e) {
    fail(res, e);
  }
});

/** 公共数据表 / 枚举选项 */
app.get('/api/options', (req, res) => {
  try {
    res.json({ ok: true, options: getOptions() });
  } catch (e) {
    fail(res, e);
  }
});

/** 公共数据表目录清单 */
app.get('/api/common', (req, res) => {
  try {
    const ws = wsOf(req);
    const refresh = req.query.refresh === '1';
    res.json({ ok: true, dir: ws.commonDir, files: commonFiles.listFiles(ws, refresh) });
  } catch (e) {
    fail(res, e);
  }
});

/** 读取某个公共数据表 */
app.get('/api/common/file', (req, res) => {
  try {
    res.json({ ok: true, ...commonFiles.readCommonFile(wsOf(req), req.query.name) });
  } catch (e) {
    fail(res, e);
  }
});

/** 保存某个公共数据表 */
app.put('/api/common/file', (req, res) => {
  try {
    const ws = requireWrite(req, res);
    if (!ws) return;

    const result = commonFiles.writeCommonFile(ws, req.body);
    res.json({ ok: true, name: result.name, backup: result.backup, meta: result.meta });
  } catch (e) {
    fail(res, e);
  }
});

/** 公共数据表备份列表 */
app.get('/api/common/backups', (req, res) => {
  try {
    res.json({ ok: true, backups: commonFiles.listBackups(wsOf(req), req.query.name) });
  } catch (e) {
    fail(res, e);
  }
});

/** 从备份恢复某个公共数据表 */
app.post('/api/common/backups/:dir/restore', (req, res) => {
  try {
    const ws = requireWrite(req, res);
    if (!ws) return;

    const result = commonFiles.restoreBackup(ws, req.params.dir);
    res.json({ ok: true, name: result.name, restored: result.restored, meta: result.meta });
  } catch (e) {
    fail(res, e);
  }
});

/* ------------------------------------------------------------------ */
/* 公共数据：单文件下载 / 基准版 / 差量补丁                            */
/* ------------------------------------------------------------------ */

/**
 * 下载单个公共数据表的完整内容。
 *
 * 直接下发磁盘原文（含注释与原始缩进），交付给游戏侧时不必二次加工。
 */
app.get('/api/common/file/download', (req, res) => {
  try {
    const doc = commonFiles.readCommonFile(wsOf(req), req.query.name);
    const name = String(doc.name);
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    res.setHeader(
      'Content-Disposition',
      `attachment; filename="${name}"; filename*=UTF-8''${encodeURIComponent(name)}`
    );
    res.setHeader('Content-Length', Buffer.byteLength(doc.raw, 'utf8'));
    res.send(doc.raw);
  } catch (e) {
    fail(res, e);
  }
});

/**
 * 下载单个公共数据表相对基准版的**差量补丁**。
 *
 * 仅包含改动过的字段，未改动的字段完全不出现。没有基准时返回 409，
 * 因为「相对什么算差异」无从确定。
 */
app.get('/api/common/file/patch', (req, res) => {
  try {
    const ws = wsOf(req);
    const name = String(req.query.name);
    const baseRaw = commonPatch.readBaseline(name);
    if (baseRaw === null) {
      res.status(409).json({
        ok: false,
        code: 'ENOBASELINE',
        message: `「${name}」还没有基准版，无法计算差量。请先让管理员上传基准。`,
      });
      return;
    }
    const currentRaw = commonFiles.readCommonFile(ws, name).raw;
    const list = commonPatch.listBaseline().find((b) => b.name === name);
    const patch = commonPatch.diffFile(name, baseRaw, currentRaw, list || {});

    if (commonPatch.isEmptyPatch(patch)) {
      res.status(204).end();
      return;
    }

    const fileName = name.replace(/\.json$/i, '.patch.json');
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    res.setHeader(
      'Content-Disposition',
      `attachment; filename="${fileName}"; filename*=UTF-8''${encodeURIComponent(fileName)}`
    );
    res.setHeader('X-Patch-Changed', String(patch._meta.changed));
    res.send(JSON.stringify(patch, null, 2));
  } catch (e) {
    fail(res, e);
  }
});

/**
 * 下载整个 Common 目录相对基准版的差量补丁（多文件合并成一个 JSON）。
 *
 * 不做 zip，是为了不给项目引入压缩库依赖；合并 JSON 对新旧消费方都更简单。
 * 只有「有基准且有改动」的文件会出现在结果里。
 */
app.get('/api/common/patch', (req, res) => {
  try {
    const ws = wsOf(req);
    const baselines = commonPatch.listBaseline();
    if (baselines.length === 0) {
      res.status(409).json({
        ok: false,
        code: 'ENOBASELINE',
        message: '服务端还没有任何基准版，无法计算差量。请先让管理员上传基准。',
      });
      return;
    }

    const files = {};
    let changed = 0;
    let removed = 0;
    for (const item of baselines) {
      let currentRaw;
      try {
        currentRaw = commonFiles.readCommonFile(ws, item.name).raw;
      } catch {
        // 工作区里没有这个文件：跳过而不是报错，用户可能只下载了关心的那几个
        continue;
      }
      const baseRaw = commonPatch.readBaseline(item.name);
      if (baseRaw === null) continue;

      const patch = commonPatch.diffFile(item.name, baseRaw, currentRaw, item);
      if (commonPatch.isEmptyPatch(patch)) continue;

      files[item.name] = { _meta: patch._meta, data: patch.data, removed: patch.removed };
      changed += patch._meta.changed;
      removed += patch._meta.removed;
    }

    if (Object.keys(files).length === 0) {
      res.status(204).end();
      return;
    }

    const payload = {
      _meta: {
        kind: 'common-patch',
        generatedAt: new Date().toISOString(),
        baselineCount: baselines.length,
        fileCount: Object.keys(files).length,
        changed,
        removed,
        workspace: ws.owner,
      },
      files,
    };

    const fileName = `common.patch.json`;
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    res.setHeader(
      'Content-Disposition',
      `attachment; filename="${fileName}"; filename*=UTF-8''${encodeURIComponent(fileName)}`
    );
    res.send(JSON.stringify(payload, null, 2));
  } catch (e) {
    fail(res, e);
  }
});

/**
 * 基准版清单。
 *
 * 顺带比对每个文件的当前指纹，标出「已与基准一致 / 有改动」，
 * 清单界面不用自己去读文件。
 */
app.get('/api/common/baseline', (req, res) => {
  try {
    const ws = wsOf(req);
    const items = commonPatch.listBaseline().map((b) => {
      let currentRevision = '';
      let upToDate = false;
      try {
        currentRevision = commonFiles.readCommonFile(ws, b.name).revision;
        upToDate = currentRevision === b.revision;
      } catch {
        // 工作区缺这个文件，视为「不一致」
      }
      return { ...b, currentRevision, upToDate };
    });
    res.json({ ok: true, baselines: items });
  } catch (e) {
    fail(res, e);
  }
});

/**
 * 上传基准版（仅管理员）。
 *
 * 两种用法：
 * - `{ name, content }`：把一段内容登记为某个文件的基准；
 * - `{ files?: string[] }`：把**当前工作区**的公共数据快照为基准（省略 files 即全部）。
 *
 * 第二种是管理员最常用的：本地确认过是「官方版」之后一次性登记整份基准。
 */
app.post('/api/common/baseline', (req, res) => {
  try {
    if (!requireAdmin(req, res)) return;
    const ws = wsOf(req);
    const body = req.body || {};
    const author = req.user ? req.user.displayName : '本地管理员';

    // 用法一：直接提交单个文件内容
    if (body.name && typeof body.content === 'string') {
      res.json({ ok: true, baseline: commonPatch.writeBaseline(String(body.name), body.content, author) });
      return;
    }

    // 用法二：从当前工作区快照
    const available = commonFiles.listFiles(ws).map((f) => f.name);
    const wanted = Array.isArray(body.files) && body.files.length > 0 ? body.files : available;
    const written = [];
    for (const name of wanted) {
      if (!available.includes(name)) continue;
      const raw = commonFiles.readCommonFile(ws, name).raw;
      written.push(commonPatch.writeBaseline(name, raw, author));
    }
    res.json({ ok: true, count: written.length, baselines: written });
  } catch (e) {
    fail(res, e);
  }
});

/** 删除基准版（仅管理员） */
app.delete('/api/common/baseline', (req, res) => {
  try {
    if (!requireAdmin(req, res)) return;
    res.json({ ok: true, ...commonPatch.deleteBaseline(String(req.query.name)) });
  } catch (e) {
    fail(res, e);
  }
});

/** 武将库（武将源） */
app.get('/api/library', async (req, res) => {
  try {
    const force = req.query.refresh === '1';
    const data = await getLibrary({ force });
    res.json({ ok: true, ...data });
  } catch (e) {
    fail(res, e);
  }
});

/** 静态资源：生产模式下托管前端构建产物 */
app.use(express.static(DIST_DIR));

/** SPA 回退：非 /api 前缀的请求统一返回 index.html */
app.get(/^\/(?!api\/).*/, (req, res, next) => {
  const indexFile = path.join(DIST_DIR, 'index.html');
  if (fs.existsSync(indexFile)) {
    res.sendFile(indexFile);
  } else {
    next();
  }
});

app.listen(PORT, () => {
  const info = workspace.workspaceInfo();
  const authReady = auth.isAuthAvailable();
  console.log('==============================================');
  console.log('  Sango Infinity 剧本编辑器 · 后端服务已启动');
  console.log(`  服务地址： http://localhost:${PORT}`);
  console.log(`  数据目录： ${info.root}`);
  console.log(`  工作区模式： ${info.mode === 'multi' ? 'multi（多用户隔离）' : 'local（读写工程目录）'}`);
  console.log(`  账号体系： ${authReady ? '已接入武将库网站账号（独立校验令牌）' : '未找到密钥文件，按单机模式运行'}`);
  console.log(`  剧本模板： ${path.join(TOOL_ROOT, 'server', 'templates')}`);
  console.log(`  武将库源： ${LIBRARY_API}（失败时自动降级到本地文件）`);
  console.log('==============================================');
});
