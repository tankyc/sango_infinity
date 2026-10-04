/**
 * 武将库模块
 *
 * 武将源优先从「武将库网站」（Tools/PersonLibraryWeb，默认 http://localhost:3001）拉取；
 * 网站未启动时依次降级为：网站本地数据文件 -> 游戏工程内置武将库文件。
 *
 * 由于武将库与剧本 personSet 的多维字段编码不同，此模块同时提供归一化输出与
 * 「转成剧本武将对象」的转换能力，便于前端做导入与字段回填。
 */
const fs = require('fs');
const { parseJsonc } = require('./jsonc');
const { LIBRARY_API, LIBRARY_WEB_FILE, LIBRARY_CUSTOM_WEB_FILE, LIBRARY_GAME_FILE } = require('./paths');

/** 五维字段名与中文标签 */
const ATTRIBUTE_FIELDS = ['command', 'strength', 'intelligence', 'politics', 'glamour'];

/** 兵种适性字段名 */
const ABILITY_FIELDS = ['spearLv', 'halberdLv', 'crossbowLv', 'rideLv', 'waterLv', 'machineLv'];

/** 简单缓存，避免每次打开面板都去拉取 */
const cache = new Map();

/**
 * 带超时的 fetch 封装。
 *
 * @param {string} url 请求地址
 * @param {number} timeoutMs 超时毫秒
 * @returns {Promise<any>} 解析后的 JSON
 */
async function fetchJson(url, timeoutMs = 2500) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const res = await fetch(url, { signal: controller.signal });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return await res.json();
  } finally {
    clearTimeout(timer);
  }
}

/**
 * 从容器对象中取出武将集合。
 *
 * 兼容三种结构：
 * - 武将库网站接口：{ persons: [...] }
 * - 数据文件：{ PersonLibrary: { "1": {...} } }
 *
 * @param {any} data 原始数据
 * @returns {Array<object>} 武将数组
 */
function extractPersons(data) {
  if (!data) return [];
  if (Array.isArray(data.persons)) return data.persons;
  if (data.PersonLibrary) {
    return Object.values(data.PersonLibrary).filter((x) => x && typeof x === 'object');
  }
  if (Array.isArray(data)) return data;
  return [];
}

/**
 * 读取本地 JSON 文件中的武将集合。
 *
 * @param {string} file 文件路径
 * @returns {Array<object>}
 */
function readLocalPersons(file) {
  if (!fs.existsSync(file)) return [];
  return extractPersons(parseJsonc(fs.readFileSync(file, 'utf8')));
}

/**
 * 归一化单个武将记录。
 *
 * 五维与适性在武将库中分别是 5 元 / 3 元数组，此处统一取「基础值 / 等级」的标量形式，
 * 同时保留原始数组供高级用途使用。
 *
 * @param {object} p 原始武将对象
 * @param {string} lib 来源库标识
 * @returns {object|null} 归一化结果
 */
function normalizePerson(p, lib) {
  if (!p || typeof p !== 'object') return null;
  const id = Number(p.Id ?? p.id);
  if (!Number.isFinite(id) || id <= 0) return null;

  const pickBase = (arr) => {
    if (Array.isArray(arr)) return Number(arr[0]) || 0;
    return Number(arr) || 0;
  };

  const result = {
    Id: id,
    lib,
    Name: p.Name ?? '',
    familyName: p.familyName ?? '',
    giveName: p.giveName ?? '',
    nickName: p.nickName ?? '',
    description: p.description ?? '',
    sex: p.sex,
    appearance: p.appearance,
    yearBorn: p.yearBorn,
    yearDead: p.yearDead,
    compatibility: p.compatibility,
    state: p.state,
    personality: p.personality,
    argumentation: p.argumentation,
    type: p.type,
    headIconID: p.headIconID,
    imageID: p.imageID,
    image: p.image,
    loyalty: p.loyalty,
    Official: p.Official,
    Level: p.Level,
    FeatureList: p.FeatureList,
    LikePersonList: p.LikePersonList,
    HatePersonList: p.HatePersonList,
    SpouseList: p.SpouseList,
    Father: p.Father,
    Mother: p.Mother,
    Brother: p.Brother,
  };

  for (const f of ATTRIBUTE_FIELDS) result[f] = pickBase(p[f]);
  for (const f of ABILITY_FIELDS) result[f] = pickBase(p[f]);

  return result;
}

/**
 * 把归一化记录转换为剧本 personSet 的字段编码。
 *
 * 剧本中：
 * - 五维为 [基础值, 成长类型Id]
 * - 兵种适性为 [等级]
 *
 * @param {object} rec 归一化记录
 * @param {{id?:number}} [override] 覆盖项（例如指定新 Id）
 * @returns {object} 剧本武将对象
 */
function toScenarioPerson(rec, override = {}) {
  const id = override.id ?? rec.Id;
  const person = {
    Id: id,
    Name: rec.Name || '',
    familyName: rec.familyName || rec.Name || '',
    giveName: rec.giveName || '',
    nickName: rec.nickName || '',
  };
  const num = (v, def) => (Number.isFinite(Number(v)) ? Number(v) : def);
  person.BelongForce = 0;
  person.BelongCorps = 0;
  person.BelongCity = 0;
  person.CurrentCity = 0;
  person.Official = num(rec.Official, 0);
  person.Level = num(rec.Level, 0);
  person.state = num(rec.state, 8);
  person.sex = num(rec.sex, 0);
  person.compatibility = num(rec.compatibility, 0);
  person.personality = num(rec.personality, 3);
  person.argumentation = num(rec.argumentation, 3);
  person.type = num(rec.type, 0);
  person.birthplace = 1;
  person.appearance = num(rec.appearance, 200);
  person.yearBorn = num(rec.yearBorn, 160);
  person.yearDead = num(rec.yearDead, 220);
  person.oldAge = 255;
  person.old_age = 255;
  person.loyalty = num(rec.loyalty, 0);
  person.merit = 0;
  person.stamina = 100;
  person.injury = 0;
  person.headIconID = num(rec.headIconID, 0);
  person.imageID = num(rec.imageID, 0);
  person.horse = -1;
  person.left_weapon = -1;
  person.right_weapon = -1;
  person.ambition = 2;
  person.tone = 0;
  person.voice = 1;
  person.wadai = 0;
  person.wajutsu = [];
  person.wordTac = [0, 0];
  person.generation = 1;
  person.promotion = 1;
  person.local_affiliation = 0;
  person.hanLoyalty = 1;
  person.skeleton = 0;
  person.death_type = 0;
  person.strategic_tendency = 2;
  person.ketsuen = id;
  person.body = [0, 0, 0, 0, 0, 0, -1, -1];

  for (const f of ATTRIBUTE_FIELDS) person[f] = [num(rec[f], 50), 5];
  for (const f of ABILITY_FIELDS) person[f] = [num(rec[f], 0)];

  if (Array.isArray(rec.FeatureList) && rec.FeatureList.length) person.FeatureList = [...rec.FeatureList];
  if (Array.isArray(rec.LikePersonList) && rec.LikePersonList.length) {
    person.LikePersonList = [...rec.LikePersonList];
  }
  if (Array.isArray(rec.HatePersonList) && rec.HatePersonList.length) {
    person.HatePersonList = [...rec.HatePersonList];
  }
  if (Array.isArray(rec.SpouseList) && rec.SpouseList.length) person.SpouseList = [...rec.SpouseList];
  if (Number.isFinite(Number(rec.Father))) person.Father = Number(rec.Father);
  if (Number.isFinite(Number(rec.Mother))) person.Mother = Number(rec.Mother);
  if (Number.isFinite(Number(rec.Brother))) person.Brother = Number(rec.Brother);

  return person;
}

/**
 * 武将库来源定义。
 *
 * 武将库网站（Tools/PersonLibraryWeb）维护的是**两个**库，两个都要拉：
 * - `base`   基础武将库（官方武将）
 * - `custom` 自建武将库（玩家自建，Id 从 1000 起）
 *
 * 早先只拉了 `base`，导致自建武将在编辑器里完全看不到，
 * 会被误认为「编辑器不支持自建武将」。
 *
 * 每个库都有独立的降级链，base 最后可退到游戏内置库，
 * custom 只有网站接口与网站的 CustomPerson.json 两个来源。
 */
const LIB_SOURCES = [
  {
    key: 'base',
    label: '基础武将库',
    fallbacks: [
      { file: LIBRARY_WEB_FILE, label: '网站数据文件' },
      { file: LIBRARY_GAME_FILE, label: '游戏内置库' },
    ],
  },
  {
    key: 'custom',
    label: '自建武将库',
    fallbacks: [{ file: LIBRARY_CUSTOM_WEB_FILE, label: '网站自建库文件' }],
  },
];

/**
 * 读取单个库的原始数据。
 *
 * 注意「接口通了但库是空的」是合法状态（新建的自建库就是这样），
 * 因此不能把空数组当成失败；只有请求本身出错才走降级链。
 *
 * @param {{key:string,label:string,fallbacks:Array<{file:string,label:string}>}} src 库来源定义
 * @returns {Promise<{raw:Array<object>, origin:string, error:string}>}
 */
async function loadOneLib(src) {
  const url = `${LIBRARY_API}/api/persons?lib=${src.key}`;
  try {
    const data = await fetchJson(url);
    return { raw: extractPersons(data), origin: `${src.label}（武将库网站）`, error: '' };
  } catch (e) {
    const apiError = `${src.label}网站接口不可用（${e.message}）`;
    for (const fb of src.fallbacks) {
      const local = readLocalPersons(fb.file);
      if (local.length > 0) {
        return { raw: local, origin: `${src.label}（${fb.label}）`, error: '' };
      }
    }
    return { raw: [], origin: '', error: apiError };
  }
}

/**
 * 获取武将库数据。
 *
 * @param {{force?:boolean}} [options] force=true 时跳缓存
 * @returns {Promise<{source:string, api:string, count:number, persons:Array<object>, libs:Array<object>, error:string}>}
 */
async function getLibrary(options = {}) {
  const cacheKey = 'all';
  if (!options.force && cache.has(cacheKey)) return cache.get(cacheKey);

  const loaded = await Promise.all(LIB_SOURCES.map((src) => loadOneLib(src)));

  const persons = [];
  const libs = [];
  const errors = [];

  LIB_SOURCES.forEach((src, i) => {
    const res = loaded[i];
    let count = 0;
    for (const p of res.raw) {
      const n = normalizePerson(p, src.key);
      if (n) {
        persons.push(n);
        count += 1;
      }
    }
    libs.push({ key: src.key, label: src.label, count, origin: res.origin, error: res.error });
    if (res.error) errors.push(res.error);
  });

  persons.sort((a, b) => a.Id - b.Id);

  const result = {
    source: libs.filter((l) => l.origin).map((l) => l.origin).join(' + ') || '（无可用数据源）',
    api: LIBRARY_API,
    count: persons.length,
    persons,
    libs,
    error: errors.join('；'),
  };
  cache.set(cacheKey, result);
  return result;
}

module.exports = { getLibrary, toScenarioPerson, LIB_SOURCES, ATTRIBUTE_FIELDS, ABILITY_FIELDS };
