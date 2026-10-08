using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Sango.Mod
{
    /// <summary>
    /// 模组封面加载器（mod.info 的 poster 字段）
    ///
    /// poster 有两种合法写法，必须都支持：
    ///   1. 包内文件名（如 poster.jpg）—— 从模组目录读本地文件；老包与手工打包的包都是这种
    ///   2. http(s) 地址 —— 在创意工坊发布时上传封面后，服务端会把封面的访问地址写进 mod.info，
    ///      需要联网取图（上传的封面不随 zip 下发，所以只能按地址去站点取）
    ///
    /// 远程封面按 URL 哈希缓存到 persistentDataPath/ModPosters：同一张图只下载一次；
    /// 断网或站点暂时不可用时，有缓存就直接用缓存，没有则回调 null（调用方回退 empty.png），
    /// 不会让界面一直等在图下载上。
    /// </summary>
    public static class ModPosterLoader
    {
        /// <summary>URL → 已解码的贴图。用强引用持有：模组封面总量很小，且不能被 GC 回收</summary>
        private static readonly Dictionary<string, Texture> memoryCache = new Dictionary<string, Texture>();

        /// <summary>URL → 等待同一次下载结果的回调，用来合并对同一张封面的并发请求</summary>
        private static readonly Dictionary<string, List<Action<Texture>>> pendingCallbacks =
            new Dictionary<string, List<Action<Texture>>>();

        /// <summary>远程封面磁盘缓存目录</summary>
        private static string CacheDir
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, "ModPosters"); }
        }

        /// <summary>poster 是否是远程地址（而不是包内文件名）</summary>
        public static bool IsRemote(string poster)
        {
            return !string.IsNullOrEmpty(poster)
                && (poster.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || poster.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 加载包内封面文件（同步）。
        /// 文件不存在时返回占位图，保持模组管理器原有行为。
        /// </summary>
        public static Texture LoadLocal(string posterFullPath)
        {
            if (string.IsNullOrEmpty(posterFullPath) || !System.IO.File.Exists(posterFullPath))
                return EmptyTexture();
            return Sango.Loader.ObjectLoader.LoadObject<Texture>(posterFullPath, false, false);
        }

        /// <summary>
        /// 加载远程封面（异步）。texture 参数可能为 null，调用方需自行回退占位图。
        /// 已有缓存或正在下载时不会重复发起请求。
        /// </summary>
        /// <param name="host">用于启动协程的宿主（通常是发起加载的窗口）</param>
        /// <param name="url">封面的 http(s) 地址</param>
        /// <param name="onLoaded">加载完成回调</param>
        public static void LoadRemote(MonoBehaviour host, string url, Action<Texture> onLoaded)
        {
            if (string.IsNullOrEmpty(url))
            {
                if (onLoaded != null) onLoaded.Invoke(null);
                return;
            }

            Texture cached;
            if (memoryCache.TryGetValue(url, out cached) && cached != null)
            {
                if (onLoaded != null) onLoaded.Invoke(cached);
                return;
            }

            List<Action<Texture>> waiting;
            if (pendingCallbacks.TryGetValue(url, out waiting))
            {
                waiting.Add(onLoaded);
                return;
            }

            pendingCallbacks[url] = new List<Action<Texture>> { onLoaded };

            if (host == null || !host.isActiveAndEnabled)
            {
                // 宿主不可用（窗口已关闭等）时不再发起下载，直接让调用方显示占位图
                Finish(url, null);
                return;
            }

            host.StartCoroutine(DownloadRoutine(url));
        }

        /// <summary>占位空图（所有调用方共用同一张，避免重复加载）</summary>
        public static Texture EmptyTexture()
        {
            return Sango.Loader.ObjectLoader.LoadObject<Texture>("Assets/empty.png", false, false);
        }

        /// <summary>下载（或读磁盘缓存）一张远程封面，然后唤醒所有等待者</summary>
        private static IEnumerator DownloadRoutine(string url)
        {
            Texture2D texture = null;
            string cachePath = GetCachePath(url);

            // 1. 先试磁盘缓存：断网、或站点暂时不可用时也能显示上次下载过的封面
            if (System.IO.File.Exists(cachePath))
            {
                try
                {
                    texture = Decode(System.IO.File.ReadAllBytes(cachePath));
                }
                catch (Exception e)
                {
                    Sango.Log.Warning("封面缓存读取失败：" + e.Message);
                }

                // 缓存文件损坏（下载中断、磁盘错误）→ 删掉重下，避免永远显示占位图
                if (texture == null)
                {
                    try { System.IO.File.Delete(cachePath); }
                    catch (Exception) { }
                }
            }

            // 2. 没有可用缓存才联网
            if (texture == null)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = 15;
                    yield return request.SendWebRequest();

                    if (string.IsNullOrEmpty(request.error) && request.responseCode == 200)
                    {
                        byte[] bytes = request.downloadHandler != null ? request.downloadHandler.data : null;
                        texture = Decode(bytes);
                        if (texture != null && bytes != null && bytes.Length > 0)
                        {
                            try
                            {
                                System.IO.Directory.CreateDirectory(CacheDir);
                                System.IO.File.WriteAllBytes(cachePath, bytes);
                            }
                            catch (Exception e)
                            {
                                Sango.Log.Warning("封面缓存写入失败：" + e.Message);
                            }
                        }
                    }
                    else
                    {
                        Sango.Log.Warning("封面下载失败 " + url + "：" + request.error);
                    }
                }
            }

            if (texture != null)
                memoryCache[url] = texture;

            Finish(url, texture);
        }

        /// <summary>唤醒并清空某个 URL 上排队的所有回调</summary>
        private static void Finish(string url, Texture texture)
        {
            List<Action<Texture>> callbacks;
            if (!pendingCallbacks.TryGetValue(url, out callbacks))
                return;

            pendingCallbacks.Remove(url);
            for (int i = 0; i < callbacks.Count; i++)
            {
                if (callbacks[i] != null)
                    callbacks[i].Invoke(texture);
            }
        }

        /// <summary>字节流 → 贴图；解码失败返回 null（不会留下半成品对象）</summary>
        private static Texture2D Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(bytes))
                return texture;

            UnityEngine.Object.Destroy(texture);
            return null;
        }

        /// <summary>
        /// 缓存文件路径：取 URL 的 64 位 FNV-1a 哈希做文件名。
        /// URL 里可能带查询参数或中文，直接当文件名既不合法、也区分不了不同地址；
        /// 不用 MD5 是为了避免移动端 IL2CPP 对加密库的额外裁剪配置。
        /// </summary>
        private static string GetCachePath(string url)
        {
            return System.IO.Path.Combine(CacheDir, HashUrl(url) + GetExtension(url));
        }

        /// <summary>64 位 FNV-1a 哈希（纯托管实现，无 AOT 依赖）</summary>
        private static string HashUrl(string url)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offsetBasis;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(url);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }
            return hash.ToString("x16");
        }

        /// <summary>从 URL 里取图片扩展名（只影响缓存文件的可读性，实际解码由内容决定）</summary>
        private static string GetExtension(string url)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(new Uri(url).AbsolutePath);
                if (!string.IsNullOrEmpty(ext) && ext.Length <= 5)
                    return ext.ToLowerInvariant();
            }
            catch (Exception)
            {
                // URL 不合法时退回 .img，缓存本身仍然可用
            }
            return ".img";
        }
    }
}
