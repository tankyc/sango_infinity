/**
 * 复制文本到剪贴板
 *
 * 注意：navigator.clipboard 只在「安全上下文」（HTTPS 或 localhost）下可用，
 * 而站点目前是明文 HTTP（http://IP 或 http://域名），直接用它会出现
 * 「点了复制、按钮没反应」这种静默失败。
 * 所以这里保留 execCommand 兜底路径 —— 该 API 虽已标记废弃，
 * 但在非安全上下文里仍然是唯一可用的方案。
 */
export async function copyText(text: string): Promise<boolean> {
  if (window.isSecureContext && navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // 授权被拒或 API 异常，继续走下面的兜底方案
    }
  }
  return legacyCopy(text);
}

function legacyCopy(text: string): boolean {
  try {
    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    // 放到视口外，避免页面上出现跳动
    area.style.position = 'fixed';
    area.style.top = '-1000px';
    area.style.opacity = '0';
    document.body.appendChild(area);
    area.select();
    const ok = document.execCommand('copy');
    document.body.removeChild(area);
    return ok;
  } catch {
    return false;
  }
}
