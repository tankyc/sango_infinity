/**
 * 配色方案（主题）清单
 *
 * 不局限于中式审美：既有水墨纸本这类东方器物配色，也有 Steam 平台风、赛博霓虹、
 * 终端极客、复古木质、极简黑白、暖阳橙、樱花粉、海盐蓝等常见产品风格，
 * 方便按自己的喜好或使用场景（白天/夜间、投屏/手机）挑一套。
 *
 * 真正的色值定义在 index.css 的 CSS 变量里，这里只维护「有哪些主题、叫什么、明暗、预览色」。
 * 新增一套配色 = index.css 里加一段 [data-theme='xxx'] 变量 + 这里加一条元数据。
 */

export type ThemeKey =
  | 'ink'
  | 'indigo'
  | 'pine'
  | 'cinder'
  | 'plum'
  | 'steam'
  | 'cyber'
  | 'terminal'
  | 'retro'
  | 'paper'
  | 'ivory'
  | 'snow'
  | 'celadon'
  | 'frost'
  | 'clay'
  | 'mono'
  | 'sunset'
  | 'sakura'
  | 'sea';

export interface ThemeMeta {
  key: ThemeKey;
  label: string;
  hint: string;
  mode: 'dark' | 'light';
  /** 选择器里的预览色块：[底色, 强调色] */
  swatch: [string, string];
}

export const THEMES: ThemeMeta[] = [
  /* ---------------- 深色：东方器物 ---------------- */
  { key: 'ink', label: '水墨鎏金', hint: '深墨底 + 鎏金', mode: 'dark', swatch: ['#0D0B08', '#C9A227'] },
  { key: 'indigo', label: '靛青月白', hint: '深靛蓝 + 亮金，层次更亮', mode: 'dark', swatch: ['#0A0E16', '#D6A83C'] },
  { key: 'pine', label: '松烟竹青', hint: '深墨绿 + 秋香赭金', mode: 'dark', swatch: ['#080C09', '#AE8E3A'] },
  { key: 'cinder', label: '玄夜朱砂', hint: '深灰底 + 朱砂红', mode: 'dark', swatch: ['#101014', '#BE4234'] },
  { key: 'plum', label: '紫檀鎏金', hint: '深紫檀底 + 暖金', mode: 'dark', swatch: ['#120C12', '#C9A227'] },

  /* ---------------- 深色：现代与风格化 ---------------- */
  { key: 'steam', label: '蒸汽工坊', hint: 'Steam 平台风：深蓝黑 + 亮蓝', mode: 'dark', swatch: ['#172232', '#66C0F4'] },
  { key: 'cyber', label: '赛博霓虹', hint: '深紫黑 + 霓虹青，强调色带品红', mode: 'dark', swatch: ['#0E0A1A', '#22C8E0'] },
  { key: 'terminal', label: '终端荧光', hint: '近黑底 + 荧光绿，极客风', mode: 'dark', swatch: ['#080B09', '#4AD668'] },
  { key: 'retro', label: '复古木质', hint: '深棕底 + 琥珀橙', mode: 'dark', swatch: ['#16100A', '#D08C32'] },

  /* ---------------- 浅色：东方纸本 ---------------- */
  { key: 'paper', label: '宣纸墨本', hint: '宣纸米白 + 墨字朱砂', mode: 'light', swatch: ['#F6F2E9', '#9E781A'] },
  { key: 'ivory', label: '牙白鎏金', hint: '象牙白 + 金', mode: 'light', swatch: ['#FAF7F0', '#A27A16'] },
  { key: 'snow', label: '雪纸朱印', hint: '纯白 + 朱红，白纸红印', mode: 'light', swatch: ['#FFFFFF', '#B22A20'] },
  { key: 'celadon', label: '月白青瓷', hint: '冷白 + 青瓷绿', mode: 'light', swatch: ['#F3F8F6', '#26826E'] },
  { key: 'clay', label: '米白赭石', hint: '米白 + 赭石棕，土陶暖调', mode: 'light', swatch: ['#F8F4EC', '#9C6026'] },

  /* ---------------- 浅色：通用产品风 ---------------- */
  { key: 'frost', label: '霜银石墨', hint: '中性灰白 + 石墨蓝，干净利落', mode: 'light', swatch: ['#F6F7F9', '#344862'] },
  { key: 'mono', label: '极简黑白', hint: '纯白灰阶 + 黑，唯一彩色是警示红', mode: 'light', swatch: ['#FAFAFA', '#121214'] },
  { key: 'sunset', label: '暖阳橙', hint: '暖白底 + 珊瑚橙', mode: 'light', swatch: ['#FDF8F4', '#CE4E1C'] },
  { key: 'sakura', label: '樱花粉', hint: '浅粉底 + 玫红', mode: 'light', swatch: ['#FDF6F8', '#C03868'] },
  { key: 'sea', label: '海盐蓝', hint: '浅冷蓝底 + 深海蓝', mode: 'light', swatch: ['#F1F7FB', '#1A689C'] },
];

export const DARK_THEMES = THEMES.filter((t) => t.mode === 'dark');
export const LIGHT_THEMES = THEMES.filter((t) => t.mode === 'light');

export const THEME_STORAGE_KEY = 'sango_modhub_theme';

/**
 * 默认配色：玄夜朱砂（深灰底 + 朱砂红）
 *
 * 改这个值时，另外两处必须一起改，否则会出现"首屏一套、加载后又一套"的闪烁：
 *   1. index.html 的 <html data-theme="...">（内联脚本执行前就要有正确的属性）
 *   2. ThemeSwitcher 的 useState 初始值（它决定切换器里高亮哪一项）
 */
export const DEFAULT_THEME_KEY: ThemeKey = 'cinder';

export function isThemeKey(value: unknown): value is ThemeKey {
  return typeof value === 'string' && THEMES.some((t) => t.key === value);
}

/** 读取已保存的主题（没存过或 localStorage 不可用时退回默认） */
export function readStoredTheme(): ThemeKey {
  try {
    const raw = window.localStorage.getItem(THEME_STORAGE_KEY);
    return isThemeKey(raw) ? raw : DEFAULT_THEME_KEY;
  } catch {
    return DEFAULT_THEME_KEY;
  }
}

/** 应用主题：改 <html data-theme> 并持久化 */
export function applyTheme(key: ThemeKey): void {
  document.documentElement.dataset.theme = key;
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, key);
  } catch {
    /* 隐私模式下忽略 */
  }
}
