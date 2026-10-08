/**
 * JSON 结构化差异与合并
 *
 * 被两处复用，因此单独抽出来，避免同一套算法写两遍：
 * - `commonPatch.js` —— 导出「只含改动字段」的补丁给用户交付（嵌套树，可读性好）
 * - `backupStore.js` —— 增量备份（存差异而不是整份 1.67MB 的剧本）
 *
 * 差异规则刻意保持简单可预测：
 * - **对象递归**：逐键比较，只输出真正变化的键；新增的键按「变化」处理。
 * - **数组与标量整体替换**：数组只要有一个元素不同就整段输出。
 *   逐元素 diff 在插入/删除时索引会错位，得不偿失；而剧本与公共数据表的
 *   主流形态是「以 Id 为键的对象」，对象递归已覆盖绝大多数实际改动。
 *
 * 内部用**扁平路径**（`[["Features","1","Id"], 2]`）而不是嵌套树表达差异：
 * 嵌套树在「标量变成对象」这类类型变化时会产生歧义
 * （`{a:{y:2}}` 到底是整体替换 a、还是设置 a.y？），扁平路径没有这个问题，
 * 因此更适合作为需要**被应用回去**的备份格式。
 */
'use strict';

/**
 * 是否为纯对象（排除数组与 null）。
 *
 * @param {unknown} v 值
 * @returns {boolean} 是否纯对象
 */
function isPlainObject(v) {
  return v !== null && typeof v === 'object' && !Array.isArray(v);
}

/**
 * 深度相等。只用于判断「是否需要输出」，实现可以朴素些。
 *
 * @param {unknown} a 值 A
 * @param {unknown} b 值 B
 * @returns {boolean} 是否相等
 */
function deepEqual(a, b) {
  if (a === b) return true;
  if (Array.isArray(a) && Array.isArray(b)) {
    return a.length === b.length && a.every((v, i) => deepEqual(v, b[i]));
  }
  if (isPlainObject(a) && isPlainObject(b)) {
    const ka = Object.keys(a);
    const kb = Object.keys(b);
    return ka.length === kb.length && ka.every((k) => k in b && deepEqual(a[k], b[k]));
  }
  return false;
}

/**
 * 递归收集差异。
 *
 * @param {unknown} base 基准值
 * @param {unknown} cur 当前值
 * @param {string[]} trail 当前路径
 * @param {{changed: Array<[string[], unknown]>, removed: string[][]}} out 输出累加器
 */
function collect(base, cur, trail, out) {
  // 数组与标量整体替换；类型不同（含「标量 <-> 对象」）也整体替换
  if (!isPlainObject(base) || !isPlainObject(cur)) {
    if (!deepEqual(base, cur)) out.changed.push([trail, cur]);
    return;
  }

  for (const key of Object.keys(cur)) {
    const next = [...trail, key];
    if (!Object.prototype.hasOwnProperty.call(base, key)) {
      // 基准里没有这个键 -> 视为新增
      out.changed.push([next, cur[key]]);
    } else {
      collect(base[key], cur[key], next, out);
    }
  }
  for (const key of Object.keys(base)) {
    if (!Object.prototype.hasOwnProperty.call(cur, key)) out.removed.push([...trail, key]);
  }
}

/**
 * 计算两份数据的差异。
 *
 * @param {unknown} base 基准数据
 * @param {unknown} cur 当前数据
 * @returns {{changed: Array<[string[], unknown]>, removed: string[][]}} 扁平差异
 */
function diffValues(base, cur) {
  const out = { changed: [], removed: [] };
  collect(base, cur, [], out);
  return out;
}

/**
 * 差异是否为空。
 *
 * @param {{changed: Array, removed: Array}} delta 差异
 * @returns {boolean} 是否无改动
 */
function isDeltaEmpty(delta) {
  return (!delta.changed || delta.changed.length === 0) && (!delta.removed || delta.removed.length === 0);
}

/**
 * 沿路径写入值（不可变更新：只克隆路径上的节点，其余结构共享）。
 *
 * @param {unknown} node 当前节点
 * @param {string[]} path 路径
 * @param {unknown} value 新值
 * @returns {unknown} 新节点
 */
function setAtPath(node, path, value) {
  if (path.length === 0) return value;
  const [head, ...rest] = path;
  const isArray = Array.isArray(node);
  const isObject = isPlainObject(node);

  if (isArray) {
    const copy = node.slice();
    const index = Number(head);
    copy[index] = setAtPath(copy[index], rest, value);
    return copy;
  }
  if (isObject) {
    const copy = { ...node };
    copy[head] = setAtPath(copy[head], rest, value);
    return copy;
  }
  // 路径中途类型对不上（应用差异时基准比预期短），按数字下标决定容器类型
  const fresh = Number.isInteger(Number(head)) ? [] : {};
  return setAtPath(fresh, path, value);
}

/**
 * 沿路径删除键（不可变更新）。
 *
 * @param {unknown} node 当前节点
 * @param {string[]} path 路径
 * @returns {unknown} 新节点
 */
function deleteAtPath(node, path) {
  if (path.length === 0) return node;
  const [head, ...rest] = path;

  if (Array.isArray(node)) {
    const copy = node.slice();
    const index = Number(head);
    if (rest.length === 0) copy.splice(index, 1);
    else copy[index] = deleteAtPath(copy[index], rest);
    return copy;
  }
  if (isPlainObject(node)) {
    const copy = { ...node };
    if (rest.length === 0) delete copy[head];
    else copy[head] = deleteAtPath(copy[head], rest);
    return copy;
  }
  return node;
}

/**
 * 把差异应用到基准数据上，得到还原后的数据。
 *
 * @param {unknown} base 基准数据
 * @param {{changed: Array<[string[], unknown]>, removed: string[][]}} delta 差异
 * @returns {unknown} 还原后的数据
 */
function applyDelta(base, delta) {
  let out = base;
  for (const [path, value] of delta.changed || []) {
    out = setAtPath(out, path, value);
  }
  // 删除放在写入之后：同一条路径既被写入又被删除时，以删除为准
  for (const path of delta.removed || []) {
    out = deleteAtPath(out, path);
  }
  return out;
}

/**
 * 把「路径 -> 值」还原成保留原始层级的嵌套对象（给用户看的补丁用）。
 *
 * @param {Array<[string[], unknown]>} entries 变更项
 * @returns {object} 差异树
 */
function buildTree(entries) {
  const root = {};
  for (const [parts, value] of entries) {
    if (parts.length === 0) continue;
    let node = root;
    for (let i = 0; i < parts.length - 1; i += 1) {
      const key = parts[i];
      // 中途撞上「整体替换的值」时无法再往下钻，直接覆盖
      if (!isPlainObject(node[key])) node[key] = {};
      node = node[key];
    }
    node[parts[parts.length - 1]] = value;
  }
  return root;
}

module.exports = {
  isPlainObject,
  deepEqual,
  diffValues,
  isDeltaEmpty,
  applyDelta,
  buildTree,
};
