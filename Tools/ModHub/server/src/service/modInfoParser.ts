/**
 * mod.info 解析与生成
 *
 * 游戏侧实现见 Game/Mod/ModManager.cs LoadMod()：INI 风 key=value 纯文本（**不是 JSON**），
 * 解析方式是 `s.Split('=')` 后只取 `c_v[1]` —— 因此：
 *   1. 值里不能出现 '='，否则会被截断
 *   2. 支持的关键字有限：id / name / description / version / depends / poster / assembly / author / size
 *   3. Tag 字段在 Mod 类里存在但 mod.info 没有解析分支 → 标签只能作为网站侧元数据
 *   4. id 由服务端在上传时分配并覆盖（8 位全局唯一短 ID，见 modService.generateModId）：
 *      作者不需要也不应该自填；depends 里填的也是这个 ID
 */

export const MOD_INFO_KEYS = [
  'id',
  'name',
  'description',
  'version',
  'author',
  'depends',
  'poster',
  'assembly',
  'size',
] as const;

export const MOD_ID_PATTERN = /^[A-Za-z0-9_-]+$/;
export const MOD_VERSION_PATTERN = /^\d+(\.\d+){0,3}$/;

export interface ModInfo {
  id: string;
  name: string;
  version: string;
  description?: string;
  author?: string;
  /** ';' 分隔的依赖 id 列表 → Required Items */
  depends: string[];
  poster?: string;
  assembly?: string;
  size?: number;
}

export interface ValidationResult {
  ok: boolean;
  info?: ModInfo;
  errors: string[];
}

/** 解析 key=value 文本，忽略空行与 # 注释行；值允许包含 '=' （后续由校验拦截） */
export function parseModInfo(text: string): Record<string, string> {
  const values: Record<string, string> = {};
  const lines = text.split(/\r?\n/);
  for (const raw of lines) {
    const line = raw.trim();
    if (line === '' || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq <= 0) continue;
    const key = line.slice(0, eq).trim().toLowerCase();
    const value = line.slice(eq + 1).trim();
    if (!(key in values)) values[key] = value;
  }
  return values;
}

/** 生成 mod.info 文本（游戏侧 ModPacker 会用同样格式写出） */
export function serializeModInfo(values: Record<string, string>): string {
  return MOD_INFO_KEYS.filter((k) => values[k] != null && values[k] !== '')
    .map((k) => `${k}=${values[k]}`)
    .join('\n');
}

export interface ValidateOptions {
  /**
   * 是否要求 mod.info 自带 id。
   *
   * 上传路径传 false —— 模组 id 一律由服务端分配（见 modService.generateModId），
   * 包内原 id 会被覆盖，因此它缺失或不合法都不该拦下上传；
   * 手工侧载的模组包仍然需要自己写 id，否则游戏端无法识别。
   */
  requireId?: boolean;
}

/**
 * 校验并结构化 mod.info
 * @param values parseModInfo 的结果
 * @param expectedId 期望的 id（zip 顶层目录名 / 表单传入），不一致时给出提示但不强制
 * @param options 上传路径用 { requireId: false }
 */
export function validateModInfo(
  values: Record<string, string>,
  expectedId?: string,
  options: ValidateOptions = {},
): ValidationResult {
  const requireId = options.requireId !== false;
  const errors: string[] = [];

  // 值内含 '=' 会被 Unity 侧截断，必须在上传时拦截
  for (const key of MOD_INFO_KEYS) {
    const v = values[key];
    if (v != null && v.includes('=')) {
      errors.push(`字段 ${key} 的值不能包含 '='（游戏会按 '=' 截断）: ${v}`);
    }
  }

  const id = values.id ?? '';
  if (requireId) {
    if (id === '') {
      errors.push('mod.info 缺少 id');
    } else if (!MOD_ID_PATTERN.test(id)) {
      errors.push(`id 只能包含字母、数字、下划线与短横线: ${id}`);
    } else if (id.length > 64) {
      errors.push(`id 长度不能超过 64 个字符: ${id}`);
    }
    if (expectedId && id !== expectedId) {
      errors.push(`mod.info 的 id(${id}) 与压缩包顶层目录名(${expectedId}) 不一致`);
    }
  }

  const name = values.name ?? '';
  if (name === '') errors.push('mod.info 缺少 name');

  const version = values.version ?? '';
  if (version === '') {
    errors.push('mod.info 缺少 version');
  } else if (!MOD_VERSION_PATTERN.test(version)) {
    errors.push(`version 需为数字点分形式（如 1.0 / 1.2.3）: ${version}`);
  }

  const poster = values.poster ?? '';
  if (poster !== '' && (poster.includes('/') || poster.includes('\\'))) {
    errors.push(`poster 只能是模组分目录内的文件名: ${poster}`);
  }

  const sizeRaw = values.size ?? '';
  let size: number | undefined;
  if (sizeRaw !== '') {
    size = Number(sizeRaw);
    if (!Number.isFinite(size) || size < 0) {
      errors.push(`size 不是合法数字: ${sizeRaw}`);
      size = undefined;
    }
  }

  const depends = (values.depends ?? '')
    .split(';')
    .map((s) => s.trim())
    .filter((s) => s !== '');
  for (const dep of depends) {
    if (!MOD_ID_PATTERN.test(dep)) {
      errors.push(`depends 中的 id 不合法: ${dep}`);
    }
  }

  if (errors.length > 0) return { ok: false, errors };

  return {
    ok: true,
    errors: [],
    info: {
      id,
      name,
      version,
      description: values.description ?? '',
      author: values.author ?? '',
      depends,
      poster: poster || 'poster.jpg',
      assembly: values.assembly ?? '',
      size,
    },
  };
}
