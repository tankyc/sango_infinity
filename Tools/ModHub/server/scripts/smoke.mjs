// =============================================================
// M1 冒烟测试：注册登录 → 发布 → 市场清单 → 直链下载 → 发新版
// 用法：
//   1) 启动服务：npm run dev   或   npm run build && npm start
//   2) 执行：   npm run smoke
// =============================================================

import { createRequire } from 'node:module';
import crypto from 'node:crypto';
import zlib from 'node:zlib';

const require = createRequire(import.meta.url);
const { ZipFile } = require('yazl');
const yauzl = require('yauzl');

const BASE = process.env.SMOKE_BASE_URL ?? 'http://127.0.0.1:8081';

let passed = 0;
let failed = 0;

function section(title) {
  console.log(`\n── ${title} ${'─'.repeat(Math.max(0, 52 - title.length))}`);
}

function check(name, ok, extra) {
  if (ok) {
    passed++;
    console.log(`  ✅ ${name}`);
  } else {
    failed++;
    console.log(`  ❌ ${name}${extra ? '  → ' + JSON.stringify(extra) : ''}`);
  }
}

function sha256(buf) {
  return crypto.createHash('sha256').update(buf).digest('hex');
}

/** 构造一个 zip 包 */
function buildZip(entries) {
  return new Promise((resolve, reject) => {
    const zip = new ZipFile();
    const chunks = [];
    zip.outputStream.on('data', (c) => chunks.push(c));
    zip.outputStream.on('end', () => resolve(Buffer.concat(chunks)));
    zip.outputStream.on('error', reject);
    for (const e of entries) {
      const content = typeof e.content === 'string' ? Buffer.from(e.content, 'utf8') : e.content;
      zip.addBuffer(content, e.name);
    }
    zip.end();
  });
}

/**
 * 手写 zip（Stored 无压缩）
 * 用途：构造 yazl 拒绝生成的恶意文件名，例如 "../escape/mod.info"，验证服务端的 zip slip 拦截。
 */
function buildRawZip(entries) {
  const local = [];
  const central = [];
  let offset = 0;
  const dosDate = ((2020 - 1980) << 9) | (1 << 5) | 1;
  const dosTime = 0;

  for (const e of entries) {
    const nameBuf = Buffer.from(e.name, 'utf8');
    const data = typeof e.content === 'string' ? Buffer.from(e.content, 'utf8') : e.content;
    const crc = zlib.crc32(data);

    const lfh = Buffer.alloc(30);
    lfh.writeUInt32LE(0x04034b50, 0);
    lfh.writeUInt16LE(20, 4);
    lfh.writeUInt16LE(0, 6);
    lfh.writeUInt16LE(0, 8);
    lfh.writeUInt16LE(dosTime, 10);
    lfh.writeUInt16LE(dosDate, 12);
    lfh.writeUInt32LE(crc, 14);
    lfh.writeUInt32LE(data.length, 18);
    lfh.writeUInt32LE(data.length, 22);
    lfh.writeUInt16LE(nameBuf.length, 26);
    lfh.writeUInt16LE(0, 28);
    local.push(lfh, nameBuf, data);

    const cdh = Buffer.alloc(46);
    cdh.writeUInt32LE(0x02014b50, 0);
    cdh.writeUInt16LE(20, 4);
    cdh.writeUInt16LE(20, 6);
    cdh.writeUInt16LE(0, 8);
    cdh.writeUInt16LE(0, 10);
    cdh.writeUInt16LE(dosTime, 12);
    cdh.writeUInt16LE(dosDate, 14);
    cdh.writeUInt32LE(crc, 16);
    cdh.writeUInt32LE(data.length, 20);
    cdh.writeUInt32LE(data.length, 24);
    cdh.writeUInt16LE(nameBuf.length, 28);
    cdh.writeUInt16LE(0, 30);
    cdh.writeUInt16LE(0, 32);
    cdh.writeUInt16LE(0, 34);
    cdh.writeUInt16LE(0, 36);
    cdh.writeUInt32LE(0, 38);
    cdh.writeUInt32LE(offset, 42);
    central.push(cdh, nameBuf);

    offset += lfh.length + nameBuf.length + data.length;
  }

  const centralBuf = Buffer.concat(central);
  const eocd = Buffer.alloc(22);
  eocd.writeUInt32LE(0x06054b50, 0);
  eocd.writeUInt16LE(0, 4);
  eocd.writeUInt16LE(0, 6);
  eocd.writeUInt16LE(entries.length, 8);
  eocd.writeUInt16LE(entries.length, 10);
  eocd.writeUInt32LE(centralBuf.length, 12);
  eocd.writeUInt32LE(offset, 16);
  eocd.writeUInt16LE(0, 20);

  return Buffer.concat([...local, centralBuf, eocd]);
}

/** 列出 zip 内所有条目名 */
function listZipEntries(buffer) {
  return new Promise((resolve, reject) => {
    yauzl.fromBuffer(buffer, { lazyEntries: true }, (err, zip) => {
      if (err) return reject(err);
      const names = [];
      zip.on('entry', (entry) => {
        names.push(entry.fileName);
        zip.readEntry();
      });
      zip.on('error', reject);
      zip.on('end', () => resolve(names));
      zip.readEntry();
    });
  });
}

function modInfoContent({ id, name, version, author, description, depends }) {
  return [
    `id=${id}`,
    `name=${name}`,
    `description=${description}`,
    `version=${version}`,
    `author=${author}`,
    `depends=${depends ?? ''}`,
    'poster=poster.jpg',
  ].join('\n');
}

const PERSON_JSON = JSON.stringify({ list: [{ id: 20001, name: '测试武将', command: 88, strength: 77, intelligence: 66, politics: 55, glamour: 44 }] });
const SCENARIO_JSON = JSON.stringify({ id: 1, name: '测试剧本', year: 184, forces: [{ id: 1, name: '测试势力' }] });
const FAKE_PNG = Buffer.from('89504e470d0a1a0a0000000d49484452', 'hex');

async function postForm(path, fields, token) {
  const fd = new FormData();
  for (const [k, v] of Object.entries(fields)) {
    if (v instanceof Blob) {
      fd.append(k, v, v.filename ?? 'file');
    } else if (Buffer.isBuffer(v)) {
      fd.append(k, new Blob([v]), 'blob.bin');
    } else {
      fd.append(k, String(v));
    }
  }
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(BASE + path, { method: 'POST', headers, body: fd });
  return { status: res.status, body: await res.json().catch(() => null) };
}

async function main() {
  const rand = crypto.randomBytes(4).toString('hex');
  const username = `smoke_${rand}`;
  const password = 'smoke123456';
  const modId = `smoke_mod_${rand}`;

  // ---------------------------------------------------------
  section('健康检查');
  const health = await fetch(`${BASE}/health`).then((r) => r.json());
  check('服务已就绪', health.ok === true, health);
  check('Cold start 使用本地存储直出', health.storage === 'local', health);
  check('CDN 未启用时不产生流量跳转', health.cdn === null, health);

  // ---------------------------------------------------------
  section('注册与登录（兼容 CloudSaveClient 契约）');
  const reg = await postForm('/register', { username, password });
  check('POST /register 返回 token', reg.status === 200 && !!reg.body?.token, reg.body);
  check('响应含 user.id / user.username', !!reg.body?.user?.id && reg.body?.user?.username === username, reg.body?.user);
  const token = reg.body?.token;

  const login = await fetch(`${BASE}/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ username, password }).toString(),
  }).then(async (r) => ({ status: r.status, body: await r.json() }));
  check('POST /login (urlencoded) 可用', login.status === 200 && !!login.body?.token, login.body);

  const me = await fetch(`${BASE}/me`, { headers: { Authorization: `Bearer ${token}` } }).then((r) => r.json());
  check('GET /me 返回当前用户', me?.user?.username === username, me);

  const badLogin = await postForm('/login', { username, password: 'wrong-password' });
  check('密码错误返回 401', badLogin.status === 401, badLogin.body);

  // ---------------------------------------------------------
  section('发布新模组（顶层目录规范）');
  const zipV1 = await buildZip([
    { name: `${modId}/mod.info`, content: modInfoContent({ id: modId, name: '冒烟测试模组', version: '1.0', author: username, description: '验证入库流水线', depends: 'src_face' }) },
    { name: `${modId}/Data/CustomPerson.json`, content: PERSON_JSON },
    { name: `${modId}/Scenario/Scenario1.json`, content: SCENARIO_JSON },
    { name: `${modId}/poster.jpg`, content: FAKE_PNG },
  ]);
  const zipV1Hash = sha256(zipV1);

  const pub = await postForm('/api/mods', {
    file: new Blob([zipV1], { type: 'application/zip' }),
    name: '冒烟测试模组',
    summary: '一句话简介',
    description: '# 正文标题\n\n简介内容',
    category: 'scenario',
    tags: '剧本,武将',
    depends: 'src_face',
    gameVersion: '1.2',
    changelog: '首发版本',
  }, token);
  check('POST /api/mods 返回 201', pub.status === 201, pub.body);
  check('返回的模组 id 与 mod.info 一致', pub.body?.mod?.id === modId, pub.body?.mod?.id);
  check('返回 downloads 直链', typeof pub.body?.downloadUrl === 'string' && pub.body.downloadUrl.includes(`${modId}@1.0.zip`), pub.body?.downloadUrl);
  check('返回豆瓣式 Required Items', Array.isArray(pub.body?.version?.depends) && pub.body.version.depends[0] === 'src_face', pub.body?.version?.depends);

  // ---------------------------------------------------------
  section('市场清单兼容端点（客户端直连）');
  const market = await fetch(`${BASE}/market/mod_list.txt`).then((r) => r.json());
  const entry = market.mods?.find((m) => m.id === modId);
  check('模组出现在 /market/mod_list.txt', !!entry, market.mods?.slice(0, 3));
  check('包含 name / url 字段', market.name === 'Sango Workshop' && typeof market.url === 'string', { name: market.name, url: market.url });
  check('作者字段拼作 auther（历史拼写）', entry?.auther === username, entry?.auther);
  check('size 为数字而非字符串', typeof entry?.size === 'number', entry?.size);
  check('version 取最新版本', entry?.version === '1.0', entry?.version);
  check('poster 字段存在', entry?.poster === 'poster.jpg', entry?.poster);

  const alias = await fetch(`${BASE}/mods/mod_list.txt`).then((r) => r.json());
  check('/mods/mod_list.txt 别名可用', Array.isArray(alias.mods), Object.keys(alias));

  // ---------------------------------------------------------
  section('直链下载（客户端 MakeUrl 规则）');
  const dlRes = await fetch(`${BASE}/mods/${modId}@1.0.zip`);
  check('GET /mods/{id}@1.0.zip 返回 200', dlRes.status === 200, dlRes.status);
  const dlBuf = Buffer.from(await dlRes.arrayBuffer());
  check('下载内容与上传的 zip 完全一致（sha256）', sha256(dlBuf) === zipV1Hash, { got: sha256(dlBuf).slice(0, 12), want: zipV1Hash.slice(0, 12) });
  check('响应头带不可变缓存', (dlRes.headers.get('cache-control') ?? '').includes('immutable'), dlRes.headers.get('cache-control'));
  check('支持 Range 请求', dlRes.headers.get('accept-ranges') === 'bytes', dlRes.headers.get('accept-ranges'));

  const names = await listZipEntries(dlBuf);
  check('zip 内所有条目都在 {id}/ 顶层目录下', names.every((n) => n.startsWith(`${modId}/`)), names);
  check('zip 内含 {id}/mod.info', names.includes(`${modId}/mod.info`), names);

  const marketAlias = await fetch(`${BASE}/market/${modId}@1.0.zip`);
  check('/market/{id}@{version}.zip 别名可用', marketAlias.status === 200, marketAlias.status);

  const rangeRes = await fetch(`${BASE}/mods/${modId}@1.0.zip`, { headers: { Range: 'bytes=0-15' } });
  check('Range 请求返回 206', rangeRes.status === 206, rangeRes.status);
  check('Content-Range 正确', (rangeRes.headers.get('content-range') ?? '').startsWith('bytes 0-15/'), rangeRes.headers.get('content-range'));

  // ---------------------------------------------------------
  section('模组详情');
  const detail = await fetch(`${BASE}/api/mods/${modId}`).then((r) => r.json());
  check('详情页返回最新版本', detail.latestVersion === '1.0', detail.latestVersion);
  check('详情页返回 Required Items', Array.isArray(detail.requiredItems) && detail.requiredItems[0] === 'src_face', detail.requiredItems);
  check('详情页返回标签', detail.tags?.includes('剧本'), detail.tags);

  // ---------------------------------------------------------
  section('发新版本（顶层目录错误 → 服务端自动重写）');
  const wrongTopDir = await buildZip([
    { name: 'wrong_dir/mod.info', content: modInfoContent({ id: modId, name: '冒烟测试模组', version: '1.1', author: username, description: '第二次发布', depends: 'src_face' }) },
    { name: 'wrong_dir/Data/CustomPerson.json', content: PERSON_JSON },
  ]);
  const v2 = await postForm(`/api/mods/${modId}/versions`, {
    file: new Blob([wrongTopDir], { type: 'application/zip' }),
    changelog: '新增 10 名武将',
  }, token);
  check('POST /api/mods/:id/versions 返回 200', v2.status === 200, v2.body);
  check('提示顶层目录已被自动重写', JSON.stringify(v2.body?.warnings ?? []).includes('不一致'), v2.body?.warnings);

  const dl2 = await fetch(`${BASE}/mods/${modId}@1.1.zip`);
  check('新版本可下载', dl2.status === 200, dl2.status);
  const dl2Buf = Buffer.from(await dl2.arrayBuffer());
  const names2 = await listZipEntries(dl2Buf);
  check('重写后所有条目都位于 {id}/ 下（否则游戏内看不到模组）', names2.every((n) => n.startsWith(`${modId}/`)), names2);

  const market2 = await fetch(`${BASE}/market/mod_list.txt`).then((r) => r.json());
  const entry2 = market2.mods?.find((m) => m.id === modId);
  check('市场清单已更新到 1.1（发布后缓存即时失效）', entry2?.version === '1.1', entry2?.version);

  const webDl = await fetch(`${BASE}/api/mods/${modId}/download`);
  check('网页备用下载入口可用', webDl.status === 200, webDl.status);

  // ---------------------------------------------------------
  section('安全与校验（负向用例）');
  const noAuth = await postForm('/api/mods', { file: new Blob([zipV1], { type: 'application/zip' }), name: 'x' });
  check('未登录发布返回 401', noAuth.status === 401, noAuth.body);

  const exeZip = await buildZip([
    { name: `${modId}/mod.info`, content: modInfoContent({ id: modId, name: 'x', version: '2.0', author: username, description: 'x' }) },
    { name: `${modId}/evil.dll`, content: Buffer.from('MZ') },
  ]);
  const exeRes = await postForm('/api/mods', { file: new Blob([exeZip], { type: 'application/zip' }), version: '2.0' }, token);
  check('包含 dll 的模组包被拒收', exeRes.status === 400 && String(exeRes.body?.error).includes('安全检查未通过'), exeRes.body);

  const slipZip = buildRawZip([
    { name: '../escape/mod.info', content: modInfoContent({ id: modId, name: 'x', version: '2.0', author: username, description: 'x' }) },
  ]);
  const slipRes = await postForm('/api/mods', { file: new Blob([slipZip], { type: 'application/zip' }), version: '2.0' }, token);
  check('路径穿越(zip slip) 被拒收', slipRes.status === 400, slipRes.body);

  const eqZip = await buildZip([
    { name: `${modId}/mod.info`, content: modInfoContent({ id: modId, name: 'x', version: '2.0', author: username, description: '含有等号 a=b 会被截断' }) },
  ]);
  const eqRes = await postForm('/api/mods', { file: new Blob([eqZip], { type: 'application/zip' }), version: '2.0' }, token);
  check('mod.info 值中含 = 被拦截', eqRes.status === 400 && JSON.stringify(eqRes.body?.details ?? '').includes("'='"), eqRes.body);

  const conflictZip = await buildZip([
    { name: `${modId}/mod.info`, content: modInfoContent({ id: modId, name: 'x', version: '1.1', author: username, description: '另一份内容' }) },
    { name: `${modId}/Data/Other.json`, content: '{}' },
  ]);
  const conflictRes = await postForm(`/api/mods/${modId}/versions`, { file: new Blob([conflictZip], { type: 'application/zip' }) }, token);
  check('重复版本号返回 409', conflictRes.status === 409, conflictRes.body);

  const missingRes = await fetch(`${BASE}/mods/${modId}@9.9.zip`);
  check('不存在的版本返回 404', missingRes.status === 404, missingRes.status);

  const otherId = `smoke_mod_other_${rand}`;
  const dupIdZip = await buildZip([
    { name: `${modId}/mod.info`, content: modInfoContent({ id: modId, name: '同名冲突', version: '3.0', author: username, description: 'x' }) },
  ]);
  const dupRes = await postForm('/api/mods', { file: new Blob([dupIdZip], { type: 'application/zip' }) }, token);
  check('重复模组 id 返回 409', dupRes.status === 409, dupRes.body);
  void otherId;

  // ---------------------------------------------------------
  console.log(`\n${'═'.repeat(56)}`);
  console.log(`  通过 ${passed} 项，失败 ${failed} 项`);
  console.log(`${'═'.repeat(56)}\n`);
  process.exit(failed === 0 ? 0 : 1);
}

main().catch((e) => {
  console.error('\n冒烟测试异常终止：', e);
  process.exit(1);
});
