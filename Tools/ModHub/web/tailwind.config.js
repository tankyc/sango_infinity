/**
 * Tailwind 主题：由 CSS 变量驱动，支持多套配色实时切换
 *
 * 为什么这么做：配色是最容易被反复调整的东西。把色值抽到 CSS 变量后，
 * 「换配色」= 在 index.css 里加一段变量覆盖，组件与类名一行都不用改，
 * 也就能让使用者在几套方案之间直接切换对比，而不是靠描述去猜。
 *
 * 变量语义（详见 index.css 顶部注释）：
 *   ink   —— 背景层级（950 页面底 → 500 悬浮层）
 *   paper —— 前景文字（50 最强 → 600 最弱）
 *   gold  —— 强调色（300 最醒目 → 900 最淡）
 *   onaccent —— 强调色上的文字（金底上的字，浅色主题下需要变白）
 *
 * 不使用 emoji 图标，图标统一来自 lucide-react。
 *
 * @type {import('tailwindcss').Config}
 */
const v = (name) => `rgb(var(--c-${name}) / <alpha-value>)`;

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        /** 墨：背景与分隔层 */
        ink: {
          950: v('ink-950'),
          900: v('ink-900'),
          850: v('ink-850'),
          800: v('ink-800'),
          700: v('ink-700'),
          600: v('ink-600'),
          500: v('ink-500'),
        },
        /** 金：强调色（描边、主按钮、高亮数字） */
        gold: {
          200: v('gold-200'),
          300: v('gold-300'),
          400: v('gold-400'),
          500: v('gold-500'),
          600: v('gold-600'),
          700: v('gold-700'),
          900: v('gold-900'),
        },
        /** 朱砂：警示、好评、危险操作 */
        cinnabar: {
          400: v('cinnabar-400'),
          500: v('cinnabar-500'),
          600: v('cinnabar-600'),
        },
        /** 宣纸：前景文字与浅色卡片 */
        paper: {
          50: v('paper-50'),
          100: v('paper-100'),
          200: v('paper-200'),
          300: v('paper-300'),
          400: v('paper-400'),
          500: v('paper-500'),
          600: v('paper-600'),
        },
        /** 竹青：正向状态（已订阅、已上架、正常账号） */
        bamboo: {
          400: v('bamboo-400'),
          500: v('bamboo-500'),
        },
        /** 强调色之上的文字：金底按钮、金底徽章 */
        onaccent: v('on-accent'),
      },
      fontFamily: {
        // 中文衬线用于标题（宋体气质），无衬线用于正文；均不依赖外网字体，避免国内加载失败
        serif: ['"Noto Serif SC"', '"Source Han Serif SC"', '"Songti SC"', 'SimSun', 'STSong', 'serif'],
        sans: ['"Noto Sans SC"', '"PingFang SC"', '"Microsoft YaHei"', 'system-ui', 'sans-serif'],
        // 数字与版本号用等宽，列表对齐更整齐
        mono: ['"JetBrains Mono"', 'Consolas', '"Courier New"', 'monospace'],
      },
      boxShadow: {
        // 强调色描边光晕：卡片 hover 与主按钮使用
        gold: '0 0 0 1px rgb(var(--c-gold-500) / 0.35), 0 6px 24px -8px rgb(var(--c-gold-500) / 0.35)',
        'gold-sm': '0 0 0 1px rgb(var(--c-gold-500) / 0.25)',
        panel: '0 10px 30px -12px rgb(var(--c-ink-950) / 0.55)',
      },
      backgroundImage: {
        // 顶部横幅与精选卡的金色斜向渐变
        'gold-veil':
          'linear-gradient(135deg, rgb(var(--c-gold-500) / 0.16) 0%, rgb(var(--c-gold-500) / 0) 55%)',
        // 精选卡底部渐变压字
        'ink-fade':
          'linear-gradient(180deg, rgb(var(--c-ink-900) / 0) 0%, rgb(var(--c-ink-900) / 0.92) 78%)',
      },
      keyframes: {
        'fade-up': {
          '0%': { opacity: '0', transform: 'translateY(6px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        shimmer: {
          '0%': { backgroundPosition: '-320px 0' },
          '100%': { backgroundPosition: '320px 0' },
        },
      },
      animation: {
        'fade-up': 'fade-up 240ms ease-out both',
        shimmer: 'shimmer 1.4s linear infinite',
      },
    },
  },
  plugins: [],
};
