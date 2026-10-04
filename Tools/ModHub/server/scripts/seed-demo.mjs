/**
 * 本地演示数据脚本（仅开发环境使用）
 *
 * 作用：往本地 ModHub 灌入一批「封面 / 分类 / 标签 / 前置依赖 / Markdown 简介」各不相同的演示模组，
 *       便于开发前端时观察真实排版效果（尤其是封面渐变、无封面占位、长简介换行、标签筛选）。
 *
 * 用法：
 *   1. 先启动服务：npm run dev
 *   2. 再灌数据：  npm run seed          （默认 http://localhost:8081）
 *                   npm run seed -- http://localhost:8081
 *
 * 说明：会真实写入数据库与 data/files 目录，请只在开发库上执行。
 *       重复执行时同名 id 会返回 409，脚本会自动跳过并提示。
 */
import zlib from 'node:zlib';
import yazl from 'yazl';

const BASE = (process.argv[2] ?? 'http://localhost:8081').replace(/\/+$/, '');
const USERNAME = 'demo_author';
const PASSWORD = 'demo123456';

/* ----------------------------- 最小 PNG 编码器 ----------------------------- */

const CRC_TABLE = (() => {
  const table = new Int32Array(256);
  for (let n = 0; n < 256; n += 1) {
    let c = n;
    for (let k = 0; k < 8; k += 1) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    table[n] = c;
  }
  return table;
})();

function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i += 1) c = CRC_TABLE[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function pngChunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length, 0);
  const typeBuf = Buffer.from(type, 'ascii');
  const crcBuf = Buffer.alloc(4);
  crcBuf.writeUInt32BE(crc32(Buffer.concat([typeBuf, data])), 0);
  return Buffer.concat([len, typeBuf, data, crcBuf]);
}

/**
 * 生成一张水墨鎏金风格的渐变封面（无文字，避免依赖任何字体资源）
 * 深墨底 + 斜向金色渐变 + 一道金线，用于验证前端卡片的封面呈现。
 */
function gradientPng(width, height, tint) {
  const raw = Buffer.alloc((width * 3 + 1) * height);
  const [tr, tg, tb] = tint;
  let o = 0;

  for (let y = 0; y < height; y += 1) {
    raw[o] = 0; // filter type: none
    o += 1;
    for (let x = 0; x < width; x += 1) {
      const t = (x / width) * 0.65 + (y / height) * 0.35;
      // 底色：深墨 (#0D0B08) → 主题色
      raw[o] = Math.round(13 + (tr - 13) * t * 0.55);
      raw[o + 1] = Math.round(11 + (tg - 11) * t * 0.55);
      raw[o + 2] = Math.round(8 + (tb - 8) * t * 0.55);
      // 金线：横向一道，位置随 tint 变化，制造"每张封面各不相同"的观感
      const lineY = Math.round(height * (0.68 + (tr % 7) / 100));
      if (Math.abs(y - lineY) <= 1) {
        raw[o] = 201;
        raw[o + 1] = 162;
        raw[o + 2] = 39;
      }
      o += 3;
    }
  }

  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 2; // color type: truecolor

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    pngChunk('IHDR', ihdr),
    pngChunk('IDAT', zlib.deflateSync(raw, { level: 6 })),
    pngChunk('IEND', Buffer.alloc(0)),
  ]);
}

/* ----------------------------- zip 打包 ----------------------------- */

function buildZip(modInfoText, id) {
  const zipfile = new yazl.ZipFile();
  zipfile.addBuffer(Buffer.from(modInfoText, 'utf8'), `${id}/mod.info`);
  zipfile.addBuffer(
    Buffer.from(`{"说明":"演示数据，${id}"}`, 'utf8'),
    `${id}/Data/Common/Demo.json`,
  );
  zipfile.end();

  return new Promise((resolve, reject) => {
    const chunks = [];
    zipfile.outputStream.on('data', (c) => chunks.push(c));
    zipfile.outputStream.on('end', () => resolve(Buffer.concat(chunks)));
    zipfile.outputStream.on('error', reject);
  });
}

/* ----------------------------- 演示数据定义 ----------------------------- */

const DEMOS = [
  {
    id: 'demo_scenario_changban',
    name: '长坂坡·单骑救主',
    category: 'scenario',
    tags: '剧本,三国,单骑',
    gameVersion: '1.2',
    version: '1.1',
    summary: '以赵云七进七出为主线的独立剧本，内含 12 段事件与专属势力配置',
    description: [
      '# 剧本说明',
      '',
      '时间点设定在建安十三年秋，曹操南下、刘备携民渡江之际。',
      '',
      '## 包含内容',
      '',
      '- 全新势力：**刘备军**（初始仅两城，兵力吃紧）',
      '- 12 段事件链：从当阳桥断后到张飞喝退曹军',
      '- 专属战法：`单骑救主`（士气与机动提升）',
      '',
      '> 建议从「史实模式」开始，难度较高。',
      '',
      '反馈请到工坊评论区留言。',
    ].join('\n'),
    changelog: '1.1\n- 修正了当阳桥事件的触发条件\n- 赵云初始兵力调整为 3000\n\n1.0\n- 首发',
    tint: [201, 162, 39],
  },
  {
    id: 'demo_person_mingjiang',
    name: '三国名人堂·武将扩展包',
    category: 'person',
    tags: '武将,历史,数据',
    gameVersion: '1.2',
    version: '2.0',
    summary: '320 名史实武将，五维按正史与演义折中设计，含 60 个新特技',
    description: [
      '# 内容一览',
      '',
      '1. 新增 320 名武将，覆盖黄巾之乱至西晋统一',
      '2. 五维数值参考《三国志》与《后汉书》',
      '3. 新增 60 个特技，全部给出触发条件说明',
      '',
      '**注意**：本包会改写部分原版武将的初始所属，建议搭配原版剧本使用。',
    ].join('\n'),
    changelog: '2.0\n- 补充 80 名后期武将\n- 重做特技数值\n\n1.0\n- 首发 240 名武将',
    tint: [140, 109, 31],
  },
  {
    id: 'demo_face_ink',
    name: '水墨风武将头像包',
    category: 'face',
    tags: '头像,水墨,美术',
    gameVersion: '*',
    version: '1.0',
    summary: '900 张统一画风的水墨立绘，含老年 / 中年 / 青年三态',
    description: [
      '# 头像包说明',
      '',
      '- 共 900 张，覆盖原版全部武将',
      '- 每名武将提供青年 / 中年 / 老年三种形态',
      '- 统一为水墨写意风格，描边与鎏金底色配套',
      '',
      '安装后无需额外设置，进入游戏即生效。',
    ].join('\n'),
    changelog: '1.0\n- 首发',
    tint: [70, 88, 96],
  },
  {
    id: 'demo_sound_battle',
    name: '战场语音补全',
    category: 'sound',
    tags: '语音,音效',
    gameVersion: '1.2',
    version: '1.2',
    summary: '补齐 200 条单挑与战法语音，含方言版可选替换',
    description: [
      '# 语音包',
      '',
      '原版单挑与战法发动时缺少对应语音，本模组补齐 200 条。',
      '',
      '- 单挑开场 / 交锋 / 胜负各阶段均有语音',
      '- 45 个战法补齐发动音',
      '- 附带一套方言版，可在 `Data/MediaData.json` 中切换',
    ].join('\n'),
    changelog: '1.2\n- 新增 40 条语音\n\n1.0\n- 首发 160 条',
    tint: [178, 58, 46],
  },
  {
    id: 'demo_skill_rebalance',
    name: '战法平衡重制',
    category: 'skill',
    tags: '技能,平衡,数值',
    gameVersion: '1.2',
    version: '3.1',
    summary: '重做 45 个战法的伤害系数与发动概率，削弱无解连携',
    description: [
      '# 平衡思路',
      '',
      '目标是让**发动概率**与**收益**成正比，砍掉几个一眼强的连携。',
      '',
      '- 神算、鬼谋类战法发动概率下调 15%',
      '- 火攻对建筑伤害上调 20%',
      '- 枪衾、戟衾的连携加成改为递减',
      '',
      '详细数值表见包内 `Data/Skill/Skills.json`。',
    ].join('\n'),
    changelog: '3.1\n- 修正火攻对建筑伤害计算错误\n\n3.0\n- 重做 45 个战法',
    tint: [79, 112, 72],
  },
  {
    id: 'demo_ui_gilded',
    name: '界面美化·鎏金版',
    category: 'ui',
    tags: '界面,皮肤,鎏金',
    gameVersion: '1.2',
    version: '0.9',
    summary: '重绘主界面、部队面板与内政列表，统一为鎏金描边风格',
    depends: 'demo_face_ink',
    description: [
      '# 鎏金界面皮肤',
      '',
      '把原版偏灰的界面统一换成水墨底 + 鎏金描边。',
      '',
      '- 主界面、势力面板、部队面板全部重绘',
      '- 字体颜色对比度按可读性调整',
      '',
      '> 前置：需要先安装 **水墨风武将头像包**，否则立绘区会出现空白。',
    ].join('\n'),
    changelog: '0.9\n- 首版，覆盖主界面与部队面板',
    tint: [201, 162, 39],
  },
];

/* ----------------------------- 请求封装 ----------------------------- */

async function getToken() {
  const body = new URLSearchParams({ username: USERNAME, password: PASSWORD });

  let res = await fetch(`${BASE}/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  });
  if (!res.ok) {
    res = await fetch(`${BASE}/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body,
    });
  }
  if (!res.ok) throw new Error(`登录/注册失败：HTTP ${res.status} ${await res.text()}`);

  const data = await res.json();
  return data.token;
}

function modInfoText(demo) {
  // 值里不能出现 '='（游戏按 '=' 截断），这里的文案都已规避
  return [
    `id=${demo.id}`,
    `name=${demo.name}`,
    `description=${demo.summary}`,
    `version=${demo.version}`,
    `author=${USERNAME}`,
    `depends=${demo.depends ?? ''}`,
    'poster=poster.png',
    '',
  ].join('\n');
}

async function publish(token, demo) {
  const zipBuffer = await buildZip(modInfoText(demo), demo.id);
  const posterBuffer = gradientPng(960, 360, demo.tint);

  const fd = new FormData();
  fd.append('file', new Blob([zipBuffer], { type: 'application/zip' }), `${demo.id}.zip`);
  fd.append('poster', new Blob([posterBuffer], { type: 'image/png' }), `${demo.id}.png`);
  fd.append('name', demo.name);
  fd.append('summary', demo.summary);
  fd.append('description', demo.description);
  fd.append('category', demo.category);
  fd.append('tags', demo.tags);
  if (demo.depends) fd.append('depends', demo.depends);
  fd.append('gameVersion', demo.gameVersion);
  fd.append('version', demo.version);
  fd.append('changelog', demo.changelog);

  const res = await fetch(`${BASE}/api/mods`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body: fd,
  });

  const text = await res.text();
  if (res.status === 409) return { id: demo.id, skipped: true };
  if (!res.ok) throw new Error(`${demo.id} 发布失败：HTTP ${res.status} ${text}`);
  return { id: demo.id, skipped: false, warnings: JSON.parse(text).warnings ?? [] };
}

/* ----------------------------- 主流程 ----------------------------- */

async function main() {
  console.log(`目标服务：${BASE}`);
  const token = await getToken();
  console.log(`已登录：${USERNAME}\n`);

  for (const demo of DEMOS) {
    try {
      const r = await publish(token, demo);
      if (r.skipped) console.log(`  跳过（id 已存在）：${demo.id}`);
      else {
        console.log(`  已发布：${demo.id}  ${demo.name}`);
        for (const w of r.warnings) console.log(`     提示：${w}`);
      }
    } catch (e) {
      console.error(`  失败：${demo.id} —— ${e.message}`);
    }
  }

  const summary = await fetch(`${BASE}/api/browse?limit=1`).then((r) => r.json());
  console.log(`\n当前上架模组总数：${summary.total}`);
}

main().catch((e) => {
  console.error('灌数据失败：', e.message);
  process.exitCode = 1;
});
