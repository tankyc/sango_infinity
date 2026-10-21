using System.Collections.Generic;

namespace Sango
{
    /// <summary>
    /// 模组私有资源引用协议(显式调用,不参与任何全局搜索与覆盖)
    ///
    /// 背景:模组目录下的 <c>Private</c> 子目录只给"本模组自己"用。
    /// 它<B>不登记</B>到 <see cref="Path"/> 的搜索路径里,也不会参与模组间的同名覆盖,
    /// 只有被显式引用时才会被访问,因此不会被其他模组意外覆盖,调试时也不会"命中别人家的副本"。
    ///
    /// 引用格式(两种写法等价,解析结果都固定落在 <c>&lt;模组目录&gt;/Private/&lt;相对路径&gt;</c>):
    ///   ① 推荐:<c>mod://&lt;模组Id&gt;/&lt;相对路径&gt;</c>   例:<c>mod://ajbknx/Icon/1.png</c>
    ///   ② 短写:<c>&lt;模组Id&gt;:&lt;相对路径&gt;</c>        例:<c>ajbknx:Icon/1</c>
    /// 说明:
    ///   · 短写只有在"冒号前的名字是已登记的模组Id"时才会被识别,
    ///     否则仍按原有的 <c>PackageName:AssetPath</c> 包协议解析,不会互相干扰;
    ///   · 解析失败(模组未启用/文件不存在/路径越界)时返回 null 并打印中文日志,<B>绝不回落到其他模组或内容目录</B>,
    ///     以保证"用 Id 固定调用本模组私有资源"的语义。
    /// </summary>
    public static class ModAssetPath
    {
        /// <summary>
        /// 私有资源协议头(推荐写法)
        /// </summary>
        public const string Scheme = "mod://";

        /// <summary>
        /// 模组Id -> 私有资源根目录(绝对路径)的登记表
        /// 只用于解析显式引用,不参与普通路径搜索
        /// </summary>
        static readonly Dictionary<string, string> modPrivateRoots =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 图片扩展名候选表:引用里不写扩展名时按此顺序补全
        /// </summary>
        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };

        /// <summary>
        /// 登记某个模组的私有资源根目录
        /// </summary>
        /// <param name="modId">模组唯一标识(mod.info 的 id)</param>
        /// <param name="privateDir">私有资源根目录,例如 .../Mods/ajbknx/Private</param>
        /// <returns>true=本次新登记;false=参数为空</returns>
        public static bool RegisterModRoot(string modId, string privateDir)
        {
            if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(privateDir))
                return false;

            string normalized = privateDir.Replace('\\', '/').TrimEnd('/');
            modPrivateRoots[modId] = normalized;
            Log.Info($"登记模组私有资源目录: {modId} -> {normalized}", Log.LogType.Game);
            return true;
        }

        /// <summary>
        /// 注销某个模组的私有资源根目录(模组被删除/停用时调用)
        /// </summary>
        /// <param name="modId">模组唯一标识</param>
        /// <returns>true=注销成功</returns>
        public static bool UnregisterModRoot(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return false;
            return modPrivateRoots.Remove(modId);
        }

        /// <summary>
        /// 清空所有模组的私有资源根目录(重新初始化模组时调用)
        /// </summary>
        public static void ClearModRoots()
        {
            modPrivateRoots.Clear();
        }

        /// <summary>
        /// 判断某个模组的私有资源是否已登记
        /// </summary>
        /// <param name="modId">模组唯一标识</param>
        public static bool HasModRoot(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return false;
            return modPrivateRoots.ContainsKey(modId);
        }

        /// <summary>
        /// 判断一个字符串是否是模组私有资源引用
        /// 命中条件:
        ///   ① 以 <c>mod://</c> 开头(无论模组是否登记,都按私有协议处理,便于给出准确报错);
        ///   ② 或者形如 <c>Id:相对路径</c>,且冒号前的 Id 已登记为模组。
        /// 注意:Windows 盘符路径(<c>d:/xxx</c>)不会被误判。
        /// </summary>
        /// <param name="path">待判断的资源引用</param>
        public static bool IsModUri(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (path.StartsWith(Scheme, System.StringComparison.OrdinalIgnoreCase))
                return true;

            // 短写:必须能拆出"已登记的模组Id",否则让给原有的 PackageName:AssetPath 协议
            string modId;
            string relativePath;
            if (!TrySplitShortUri(path, out modId, out relativePath))
                return false;
            return modPrivateRoots.ContainsKey(modId);
        }

        /// <summary>
        /// 拆分引用,得到模组Id与相对路径
        /// </summary>
        /// <param name="uri">资源引用</param>
        /// <param name="modId">输出:模组Id</param>
        /// <param name="relativePath">输出:相对私有根目录的路径(统一用'/'分隔)</param>
        /// <returns>true=格式正确</returns>
        public static bool TrySplit(string uri, out string modId, out string relativePath)
        {
            modId = null;
            relativePath = null;
            if (string.IsNullOrEmpty(uri))
                return false;

            if (uri.StartsWith(Scheme, System.StringComparison.OrdinalIgnoreCase))
            {
                string rest = uri.Substring(Scheme.Length).Replace('\\', '/');
                int index = rest.IndexOf('/');
                if (index <= 0 || index >= rest.Length - 1)
                    return false;
                modId = rest.Substring(0, index).Trim();
                relativePath = rest.Substring(index + 1).Trim();
                return !string.IsNullOrEmpty(modId) && !string.IsNullOrEmpty(relativePath);
            }

            if (!TrySplitShortUri(uri, out modId, out relativePath))
                return false;
            return !string.IsNullOrEmpty(modId) && !string.IsNullOrEmpty(relativePath);
        }

        /// <summary>
        /// 按"文件"语义解析私有资源引用:命中返回完整路径,否则返回 null 并打印中文 Error
        /// 引用里不写扩展名时,会自动按 .png/.jpg/.jpeg 补全再试
        /// </summary>
        /// <param name="uri">资源引用,例如 mod://ajbknx/Icon/1</param>
        /// <returns>私有目录下的完整文件路径;解析失败返回 null</returns>
        public static string Resolve(string uri)
        {
            string error;
            string path = ResolveInternal(uri, out error);
            if (!string.IsNullOrEmpty(path))
                return path;

            Log.Error(error);
            return null;
        }

        /// <summary>
        /// 静默解析(不打印日志),供"探测型"调用方使用
        /// </summary>
        /// <param name="uri">资源引用</param>
        /// <param name="fullPath">输出:完整文件路径</param>
        /// <returns>true=解析成功且文件存在</returns>
        public static bool TryResolve(string uri, out string fullPath)
        {
            string error;
            fullPath = ResolveInternal(uri, out error);
            return !string.IsNullOrEmpty(fullPath);
        }

        /// <summary>
        /// 按"目录"语义解析私有资源引用:命中返回完整目录路径,否则返回 null 并打印中文 Error
        /// </summary>
        /// <param name="uri">目录引用,例如 mod://ajbknx/Icon</param>
        /// <returns>私有目录下的完整目录路径;解析失败返回 null</returns>
        public static string ResolveDirectory(string uri)
        {
            string error;
            string path = ResolveBase(uri, out error);
            if (string.IsNullOrEmpty(path))
            {
                Log.Error(error);
                return null;
            }

            if (System.IO.Directory.Exists(path))
                return path;

            Log.Error($"模组私有目录不存在: {uri} (期望路径: {path})");
            return null;
        }

        /// <summary>
        /// 解析出完整的绝对路径(不判断是否存在),供需要自行处理读写的调用方使用
        /// </summary>
        /// <param name="uri">资源引用</param>
        /// <returns>完整路径;格式错误/模组未登记/路径越界时返回 null</returns>
        public static string ResolveFullPath(string uri)
        {
            string error;
            return ResolveBase(uri, out error);
        }

        /// <summary>
        /// 文件解析实现:先拆引用拼路径,再按"精确 -> 补图片扩展名"两步查找
        /// </summary>
        /// <param name="uri">资源引用</param>
        /// <param name="error">输出:失败原因(中文)</param>
        /// <returns>完整文件路径;失败返回 null</returns>
        static string ResolveInternal(string uri, out string error)
        {
            string path = ResolveBase(uri, out error);
            if (string.IsNullOrEmpty(path))
                return null;

            if (System.IO.File.Exists(path))
                return path;

            // 没写扩展名:按图片扩展名补全再试(与 Path.FindFile 的图片兜底口径一致)
            if (string.IsNullOrEmpty(System.IO.Path.GetExtension(path)))
            {
                for (int i = 0; i < ImageExtensions.Length; i++)
                {
                    string candidate = path + ImageExtensions[i];
                    if (System.IO.File.Exists(candidate))
                        return candidate;
                }
            }

            error = $"模组私有资源不存在: {uri} (期望路径: {path})";
            return null;
        }

        /// <summary>
        /// 短写拆分:<c>Id:相对路径</c>
        /// 盘符(单字母加冒号)与带目录分隔符的前缀都不算短写
        /// </summary>
        static bool TrySplitShortUri(string path, out string modId, out string relativePath)
        {
            modId = null;
            relativePath = null;

            int index = path.IndexOf(':');
            if (index <= 0 || index >= path.Length - 1)
                return false;

            string prefix = path.Substring(0, index).Trim();
            if (prefix.Length == 0)
                return false;

            // Windows 盘符: "d:/xxx" 不是模组引用
            if (prefix.Length == 1 && char.IsLetter(prefix[0]))
                return false;

            // 前缀里不允许出现目录分隔符,避免把 "a/b:c" 这类路径当模组Id
            if (prefix.IndexOf('/') >= 0 || prefix.IndexOf('\\') >= 0)
                return false;

            modId = prefix;
            relativePath = path.Substring(index + 1).Replace('\\', '/').Trim();
            return !string.IsNullOrEmpty(relativePath);
        }

        /// <summary>
        /// 解析的公共部分:拆分引用、找到私有根目录、拼出绝对路径并做越界校验
        /// </summary>
        /// <param name="uri">资源引用</param>
        /// <param name="error">输出:失败原因(中文)</param>
        static string ResolveBase(string uri, out string error)
        {
            error = null;

            string modId;
            string relativePath;
            if (!TrySplit(uri, out modId, out relativePath))
            {
                error = $"模组私有资源引用格式错误: {uri} (应为 mod://模组Id/相对路径 或 模组Id:相对路径)";
                return null;
            }

            string root;
            if (!modPrivateRoots.TryGetValue(modId, out root) || string.IsNullOrEmpty(root))
            {
                error = $"模组私有资源引用失败,模组未启用或不存在: {uri} (模组Id: {modId})";
                return null;
            }

            // 用相对路径推导绝对路径,再校验是否仍在私有根目录内,防止 ".." 逃逸到其他模组或系统目录
            string rootFull;
            string targetFull;
            try
            {
                rootFull = System.IO.Path.GetFullPath(root).Replace('\\', '/').TrimEnd('/');
                targetFull = System.IO.Path.GetFullPath(rootFull + "/" + relativePath).Replace('\\', '/');
            }
            catch (System.Exception e)
            {
                error = $"模组私有资源路径解析失败: {uri} {e.Message}";
                return null;
            }

            if (!targetFull.StartsWith(rootFull + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                error = $"模组私有资源路径越界(禁止访问私有目录之外的文件): {uri}";
                return null;
            }

            return targetFull;
        }
    }
}
