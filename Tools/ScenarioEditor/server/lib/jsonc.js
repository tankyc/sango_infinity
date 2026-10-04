/**
 * JSONC 解析辅助模块
 *
 * 游戏工程内的公共数据表（Build/Content/Data/Common/*.json）允许书写
 * `//` 行注释、`/* *\/` 块注释以及尾随逗号，标准 JSON.parse 无法解析，
 * 此模块负责在解析前把这些内容安全地剥离。
 */

/**
 * 剥离 JSON 文本中的注释与尾随逗号。
 *
 * 实现要点：
 * - 逐字符扫描，遇到字符串字面量时按转义规则整体跳过，避免误伤字符串里的 `//`；
 * - 剥离 `//` 行注释与 `/* *\/` 块注释；
 * - 剥离对象/数组最后一个元素后的多余逗号。
 *
 * @param {string} text 原始 JSONC 文本
 * @returns {string} 可被 JSON.parse 接受的 JSON 文本
 */
function stripJsonComments(text) {
  let out = '';
  let inString = false;
  let inLineComment = false;
  let inBlockComment = false;

  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    const next = text[i + 1];

    if (inLineComment) {
      if (ch === '\n') {
        inLineComment = false;
        out += ch;
      }
      continue;
    }

    if (inBlockComment) {
      if (ch === '*' && next === '/') {
        inBlockComment = false;
        i++;
      }
      continue;
    }

    if (inString) {
      out += ch;
      if (ch === '\\') {
        // 转义字符：原样吞掉下一个字符
        if (next !== undefined) {
          out += next;
          i++;
        }
        continue;
      }
      if (ch === '"') inString = false;
      continue;
    }

    if (ch === '"') {
      inString = true;
      out += ch;
      continue;
    }

    if (ch === '/' && next === '/') {
      inLineComment = true;
      i++;
      continue;
    }

    if (ch === '/' && next === '*') {
      inBlockComment = true;
      i++;
      continue;
    }

    out += ch;
  }

  // 去掉尾随逗号：形如 `,\s*}` 或 `,\s*]`
  out = out.replace(/,(\s*[}\]])/g, '$1');
  // 去掉 UTF-8 BOM
  if (out.charCodeAt(0) === 0xfeff) out = out.slice(1);
  return out;
}

/**
 * 解析 JSONC 文本。
 *
 * @param {string} text JSONC 文本
 * @returns {any} 解析结果
 */
function parseJsonc(text) {
  return JSON.parse(stripJsonComments(text));
}

module.exports = { stripJsonComments, parseJsonc };
