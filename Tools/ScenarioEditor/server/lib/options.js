/**
 * 选项表模块
 *
 * 从游戏工程的公共数据表（Build/Content/Data/Common/*.json）中提取
 * 「下拉框/校验需要」的 id -> 名称 映射，供前端做枚举选择与合法性校验。
 * 全部数据来源于游戏工程本身，避免在工具里硬编码枚举。
 */
const fs = require('fs');
const path = require('path');
const { parseJsonc } = require('./jsonc');
const { COMMON_DIR } = require('./paths');

/** 缓存：公共数据表在运行期不会变化，读取一次即可 */
let cache = null;

/**
 * 读取并解析某个公共数据表文件。
 *
 * @param {string} file 文件名
 * @returns {any|null} 解析结果，文件不存在或解析失败时返回 null
 */
function loadCommon(file) {
  const full = path.join(COMMON_DIR, file);
  if (!fs.existsSync(full)) return null;
  try {
    return parseJsonc(fs.readFileSync(full, 'utf8'));
  } catch (e) {
    console.warn(`[options] 解析 ${file} 失败：${e.message}`);
    return null;
  }
}

/**
 * 把「以 id 为键的对象集合」转成按 id 升序的 { id, name } 数组。
 *
 * @param {any} root 数据表根节点
 * @param {string} key 集合键名
 * @param {string} [extra] 需要一并带出的附加字段
 * @returns {Array<object>}
 */
function toList(root, key, extra) {
  if (!root || !root[key]) return [];
  const set = root[key];
  const items = Array.isArray(set) ? set : Object.values(set);
  return items
    .filter((x) => x && typeof x === 'object')
    .map((x) => {
      const item = { id: Number(x.Id ?? x.id), name: String(x.Name ?? x.name ?? x.Id ?? '') };
      if (extra) {
        for (const f of extra) {
          if (x[f] !== undefined) item[f] = x[f];
        }
      }
      return item;
    })
    .filter((x) => Number.isFinite(x.id))
    .sort((a, b) => a.id - b.id);
}

/**
 * 兵装/道具的「存放类别」固定名称。
 *
 * 对应 C# 的 ItemStoreKindType 枚举（None=0, Sword=1, Spear=2, Halberd=3,
 * Crossbow=4, Horse=5, Helepolis=6, Catapult=7, Boat=8）。
 * 6/7/8 是多个道具共用的容器类别（如冲车与木兽共用 6），因此用类别名而不是道具名。
 */
const STORE_KIND_LABELS = {
  1: '剑',
  2: '枪',
  3: '戟',
  4: '弓',
  5: '军马',
  6: '器械·冲车',
  7: '器械·井阑',
  8: '船',
};

/**
 * 由 ItemTypes 的 storeKind 聚合出「存放类别」候选表。
 *
 * 剧本与公共数据表里的 itemStore 用扁平数组 [存放类别, 数量] 记录库存，
 * 这里的 key 就是 存放类别（storeKind），**不是 ItemType.Id**：
 * 冲车/木兽共用 6、井阑/投石共用 7、三种船共用 8，而兵符/名马/名剑等各自独立。
 *
 * @param {Array} itemTypes 已归一化的道具列表
 * @param {object} raw 原始 ItemTypes.json 内容
 * @param {string} rootKey 根键名
 * @returns {Array<{id:number,name:string}>} 存放类别候选表
 */
function buildStoreKinds(itemTypes, raw, rootKey) {
  const groups = new Map();
  const body = raw && raw[rootKey];
  if (body && typeof body === 'object') {
    for (const item of Object.values(body)) {
      const kind = Number(item && item.storeKind);
      if (!Number.isFinite(kind)) continue;
      if (!groups.has(kind)) groups.set(kind, []);
      const name = String((item && item.Name) || '').trim();
      if (name) groups.get(kind).push(name);
    }
  }
  if (groups.size === 0) {
    for (const item of itemTypes) {
      const kind = Number(item.storeKind);
      if (!Number.isFinite(kind)) continue;
      if (!groups.has(kind)) groups.set(kind, []);
      if (item.name) groups.get(kind).push(String(item.name));
    }
  }
  return [...groups.entries()]
    .map(([id, names]) => ({
      id,
      name: STORE_KIND_LABELS[id] || names.join('/') || `存放类别 ${id}`,
    }))
    .sort((a, b) => a.id - b.id);
}

/**
 * 组装全部选项数据（带缓存）。
 *
 * @returns {object} 选项集合
 */
function getOptions() {
  if (cache) return cache;

  const provinces = toList(loadCommon('Provinces.json'), 'Provinces', ['Region']);
  const regions = toList(loadCommon('Regions.json'), 'Regions');
  const titles = toList(loadCommon('Titles.json'), 'Titles');
  const officials = toList(loadCommon('Officials.json'), 'Officials');
  const personalities = toList(loadCommon('Personalities.json'), 'Personalities', ['kind']);
  const argumentations = toList(loadCommon('Argumentations.json'), 'Argumentations', ['kind']);
  const personLevels = toList(loadCommon('PersonLevels.json'), 'PersonLevels');
  const abilityLevelTypes = toList(loadCommon('AbilityLevelTypes.json'), 'AbilityLevelTypes');
  const attributeChangeTypes = toList(loadCommon('AttributeChangeTypes.json'), 'AttributeChangeTypes');
  const features = toList(loadCommon('Features.json'), 'Features', ['level', 'kind', 'desc']);
  const cityLevelTypes = toList(loadCommon('CityLevelTypes.json'), 'CityLevelTypes');
  const buildingTypes = toList(loadCommon('BuildingTypes.json'), 'BuildingTypes', ['kind', 'majorType', 'level']);
  const flags = toList(loadCommon('Flags.json'), 'Flags', ['color']).filter((x) => x.name);
  const techniques = toList(loadCommon('Techniques.json'), 'Techniques');
  const itemTypes = toList(loadCommon('ItemTypes.json'), 'ItemTypes', ['kind']);
  const skills = toList(loadCommon('Skills.json'), 'Skills');
  const jobTypes = toList(loadCommon('JobTypes.json'), 'JobTypes');
  const terrainTypes = toList(loadCommon('TerrainTypes.json'), 'TerrainTypes');
  const troopTypes = toList(loadCommon('TroopTypes.json'), 'TroopTypes', ['kind']);
  const buffs = toList(loadCommon('Buffs.json'), 'Buffs', ['kind']);
  const troopAnimations = toList(loadCommon('TroopAnimations.json'), 'TroopAnimations');
  const storeKinds = buildStoreKinds(itemTypes, loadCommon('ItemTypes.json'), 'ItemTypes');

  cache = {
    generatedAt: new Date().toISOString(),
    commonDir: COMMON_DIR,
    provinces,
    regions,
    titles,
    officials,
    personalities,
    argumentations,
    personLevels,
    abilityLevelTypes,
    attributeChangeTypes,
    features,
    cityLevelTypes,
    buildingTypes,
    flags,
    techniques,
    itemTypes,
    skills,
    jobTypes,
    terrainTypes,
    troopTypes,
    buffs,
    troopAnimations,
    storeKinds,
  };
  return cache;
}

module.exports = { getOptions };
