import { Link } from 'react-router-dom';
import { PERSON_LIB_URL } from '../../lib/siteLinks';

/** 页脚：版权、导航与客户端接入提示（把「市场地址」直接告诉玩家，方便手动填进模组管理器） */
export function Footer() {
  return (
    <footer className="relative mt-16 overflow-hidden border-t border-gold-900/50 bg-ink-850/70">
      {/* 装饰：朱砂印章落在右下角，透明度压得很低，不跟正文抢注意力 */}
      <img
        src="/art/seal.jpg"
        alt=""
        aria-hidden
        className="pointer-events-none absolute -bottom-8 right-2 h-28 w-28 rounded-sm object-cover opacity-[var(--art-faint)] [filter:brightness(var(--art-brightness))_saturate(1.05)] sm:right-8 sm:h-32 sm:w-32"
      />
      <div className="relative mx-auto flex max-w-[1600px] flex-col gap-3 px-4 py-8 text-xs text-paper-500 sm:px-5">
        <div className="gold-rule" />
        <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
          <span className="font-serif text-sm text-paper-300">Sango Infinity 创意工坊</span>
          <Link to="/browse" className="transition-colors duration-200 hover:text-gold-300">
            浏览模组
          </Link>
          <Link to="/upload" className="transition-colors duration-200 hover:text-gold-300">
            发布模组
          </Link>
          <a href="/market/mod_list.txt" className="transition-colors duration-200 hover:text-gold-300">
            市场清单
          </a>
          <a
            href={PERSON_LIB_URL}
            target="_blank"
            rel="noreferrer noopener"
            className="transition-colors duration-200 hover:text-gold-300"
          >
            武将库
          </a>
        </div>
        <p>
          游戏内接入：在「模组管理器 → 在线市场」填入本页市场地址即可订阅下载；
          模组仅包含数据与资源，不含可执行代码，安装前会经过安全扫描。
        </p>
        <p className="text-paper-600">本站为同人作品，与《三国志》系列版权方无关。</p>
      </div>
    </footer>
  );
}
