import { Fragment, type ReactNode } from 'react';

/**
 * 极简 Markdown 渲染器
 *
 * 为什么自己写：模组简介只需要很小一部分 Markdown 语法，为此引入 marked + DOMPurify
 * 反而增加体积与 XSS 面。这里只支持简介里真正会用到的子集：
 *   标题(#~####)、无序列表(- *)、有序列表(1.)、引用(>)、分隔线(---)、
 *   行内 **粗体**、`代码`、[链接](url)
 *
 * 安全：全部通过 React 元素构建，**不走 dangerouslySetInnerHTML**，因此不存在注入风险。
 */

const INLINE_PATTERN = /(\*\*[^*]+\*\*|`[^`]+`|\[[^\]]+\]\([^)\s]+\))/g;

function renderInline(text: string, keyPrefix: string): ReactNode[] {
  const parts = text.split(INLINE_PATTERN).filter((p) => p !== '');
  return parts.map((part, index) => {
    const key = `${keyPrefix}-${index}`;

    if (part.startsWith('**') && part.endsWith('**') && part.length > 4) {
      return (
        <strong key={key} className="font-semibold text-paper-100">
          {part.slice(2, -2)}
        </strong>
      );
    }
    if (part.startsWith('`') && part.endsWith('`') && part.length > 2) {
      return (
        <code key={key} className="rounded bg-ink-700/80 px-1 py-0.5 font-mono text-[12px] text-gold-300">
          {part.slice(1, -1)}
        </code>
      );
    }
    const link = /^\[([^\]]+)\]\(([^)\s]+)\)$/.exec(part);
    if (link) {
      return (
        <a
          key={key}
          href={link[2]}
          target="_blank"
          rel="noreferrer noopener"
          className="text-gold-400 underline decoration-gold-700 underline-offset-2 transition-colors duration-200 hover:text-gold-300"
        >
          {link[1]}
        </a>
      );
    }
    return <Fragment key={key}>{part}</Fragment>;
  });
}

/**
 * 把可能含 Markdown 的文本压成纯文本
 *
 * 用途：列表卡片、精选大卡这类「摘要位」只能放纯文本（我们只给两行），
 * 若作者把带 `#`、`**` 的 Markdown 填进了「一句话简介」，原样显示会露出记号、很难看。
 * 正文（description / changelog）不走这里，而是交给 MiniMarkdown 正常渲染。
 */
export function stripMarkdown(text: string | null | undefined): string {
  return (text ?? '')
    .replace(/\r\n/g, '\n')
    .replace(/```[\s\S]*?```/g, ' ') // 代码块
    .replace(/^#{1,6}\s+/gm, '') // 标题标记
    .replace(/^\s*[-*+]\s+/gm, '') // 无序列表标记
    .replace(/^\s*\d+[.)]\s+/gm, '') // 有序列表标记
    .replace(/^>\s?/gm, '') // 引用
    .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ') // 图片
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1') // 链接保留文字
    .replace(/\*\*([^*]+)\*\*/g, '$1') // 粗体
    .replace(/(^|[^*])\*([^*]+)\*/g, '$1$2') // 斜体
    .replace(/`([^`]+)`/g, '$1') // 行内代码
    .replace(/~~([^~]+)~~/g, '$1') // 删除线
    .replace(/\s+/g, ' ') // 换行与多余空白压成单空格
    .trim();
}

const HEADING_CLASS: Record<number, string> = {
  1: 'mt-6 mb-2 font-serif text-xl text-paper-100',
  2: 'mt-5 mb-2 font-serif text-lg text-paper-100',
  3: 'mt-4 mb-1.5 font-serif text-base text-paper-100',
  4: 'mt-3 mb-1 font-serif text-sm text-paper-200',
};

export function MiniMarkdown({ text, className }: { text: string; className?: string }) {
  const source = (text ?? '').replace(/\r\n/g, '\n');
  if (source.trim() === '') {
    return <p className="text-sm text-paper-500">作者还没有填写正文介绍。</p>;
  }

  const lines = source.split('\n');
  const blocks: ReactNode[] = [];
  let i = 0;

  while (i < lines.length) {
    const raw = lines[i];
    const line = raw.trim();

    if (line === '') {
      i += 1;
      continue;
    }

    // 标题
    const heading = /^(#{1,4})\s+(.*)$/.exec(line);
    if (heading) {
      const level = heading[1].length;
      blocks.push(
        <p key={`h-${i}`} className={HEADING_CLASS[level]}>
          {renderInline(heading[2], `h-${i}`)}
        </p>,
      );
      i += 1;
      continue;
    }

    // 分隔线
    if (/^(-{3,}|_{3,}|\*{3,})$/.test(line)) {
      blocks.push(<div key={`hr-${i}`} className="gold-rule my-4" />);
      i += 1;
      continue;
    }

    // 无序列表
    if (/^[-*]\s+/.test(line)) {
      const items: string[] = [];
      const start = i;
      while (i < lines.length && /^[-*]\s+/.test(lines[i].trim())) {
        items.push(lines[i].trim().replace(/^[-*]\s+/, ''));
        i += 1;
      }
      blocks.push(
        <ul key={`ul-${start}`} className="my-2 list-disc space-y-1 pl-5 text-sm leading-relaxed text-paper-300">
          {items.map((item, idx) => (
            <li key={idx}>{renderInline(item, `ul-${start}-${idx}`)}</li>
          ))}
        </ul>,
      );
      continue;
    }

    // 有序列表
    if (/^\d+[.)]\s+/.test(line)) {
      const items: string[] = [];
      const start = i;
      while (i < lines.length && /^\d+[.)]\s+/.test(lines[i].trim())) {
        items.push(lines[i].trim().replace(/^\d+[.)]\s+/, ''));
        i += 1;
      }
      blocks.push(
        <ol key={`ol-${start}`} className="my-2 list-decimal space-y-1 pl-5 text-sm leading-relaxed text-paper-300">
          {items.map((item, idx) => (
            <li key={idx}>{renderInline(item, `ol-${start}-${idx}`)}</li>
          ))}
        </ol>,
      );
      continue;
    }

    // 引用
    if (/^>\s?/.test(line)) {
      const quoted: string[] = [];
      const start = i;
      while (i < lines.length && /^>\s?/.test(lines[i].trim())) {
        quoted.push(lines[i].trim().replace(/^>\s?/, ''));
        i += 1;
      }
      blocks.push(
        <blockquote
          key={`bq-${start}`}
          className="my-3 border-l-2 border-gold-700/70 bg-ink-700/40 py-2 pl-3 text-sm leading-relaxed text-paper-400"
        >
          {quoted.map((q, idx) => (
            <p key={idx}>{renderInline(q, `bq-${start}-${idx}`)}</p>
          ))}
        </blockquote>,
      );
      continue;
    }

    // 普通段落：连续行合并为一段，保留原有换行
    const paragraph: string[] = [];
    const start = i;
    while (i < lines.length) {
      const cur = lines[i].trim();
      if (
        cur === '' ||
        /^(#{1,4})\s+/.test(cur) ||
        /^[-*]\s+/.test(cur) ||
        /^\d+[.)]\s+/.test(cur) ||
        /^>\s?/.test(cur) ||
        /^(-{3,}|_{3,}|\*{3,})$/.test(cur)
      ) {
        break;
      }
      paragraph.push(cur);
      i += 1;
    }
    blocks.push(
      <p key={`p-${start}`} className="my-2 whitespace-pre-wrap text-sm leading-relaxed text-paper-300">
        {renderInline(paragraph.join('\n'), `p-${start}`)}
      </p>,
    );
  }

  return <div className={className}>{blocks}</div>;
}
