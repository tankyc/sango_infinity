/**
 * 版本号比较工具
 *
 * 模组版本形如 1.0 / 1.2 / 1.2.3（由 mod.info 的 version 校验保证为点分数字）。
 * 这里给出数值比较；遇到非数字段时退化为字典序，保证任何输入都能得到稳定排序。
 */

export function compareVersion(a: string, b: string): number {
  const pa = String(a ?? '').split('.');
  const pb = String(b ?? '').split('.');
  const len = Math.max(pa.length, pb.length);

  for (let i = 0; i < len; i++) {
    const sa = (pa[i] ?? '').trim();
    const sb = (pb[i] ?? '').trim();
    const na = Number(sa);
    const nb = Number(sb);

    // 任一侧不是纯数字（含空串/含字母）时退化为字典序，保证排序稳定
    if (sa === '' || sb === '' || Number.isNaN(na) || Number.isNaN(nb)) {
      if (sa !== sb) return sa < sb ? -1 : 1;
      continue;
    }
    if (na !== nb) return na < nb ? -1 : 1;
  }
  return 0;
}

/** 取列表中的最大版本，空列表返回 undefined */
export function maxVersion(versions: string[]): string | undefined {
  if (versions.length === 0) return undefined;
  return versions.reduce((acc, cur) => (compareVersion(cur, acc) > 0 ? cur : acc));
}
