/*
 * 文件名：GitDownloader.cs
 * 描述：从 Git 仓库地址下载并解压到目标文件夹的工具类
 * 支持 https://github.com/owner/repo、https://github.com/owner/repo/tree/branch 等形式
 * 创建日期：2026-07-01
 * 最后修改：2026-07-01
 */
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace Sango
{
    /// <summary>
    /// Git 资源下载器
    /// 提供从 Git 链接下载仓库 zip 包并解压到目标文件夹的 API
    /// </summary>
    public static class GitDownloader
    {
        /// <summary>
        /// Git 下载错误码
        /// </summary>
        public enum GitDownloadError
        {
            /// <summary>
            /// 无错误
            /// </summary>
            None = 0,

            /// <summary>
            /// URL 格式非法
            /// </summary>
            InvalidUrl = 1,

            /// <summary>
            /// 网络请求失败
            /// </summary>
            NetworkError = 2,

            /// <summary>
            /// 解压失败
            /// </summary>
            ExtractError = 3,
        }

        /// <summary>
        /// Git 下载结果
        /// </summary>
        public class GitDownloadResult
        {
            /// <summary>
            /// 是否成功
            /// </summary>
            public bool IsSuccess;

            /// <summary>
            /// 错误码
            /// </summary>
            public GitDownloadError Error = GitDownloadError.None;

            /// <summary>
            /// 错误信息
            /// </summary>
            public string ErrorMessage;

            /// <summary>
            /// 下载到本地的 zip 路径
            /// </summary>
            public string ZipSavePath;

            /// <summary>
            /// 解压目标文件夹
            /// </summary>
            public string ExtractTargetPath;
        }

        /// <summary>
        /// 从 Git 链接下载并解压到目标文件夹(协程版)
        /// 支持的 URL 形式:
        ///   - https://github.com/owner/repo
        ///   - https://github.com/owner/repo/tree/branch
        ///   - https://github.com/owner/repo/tree/branch/sub/path
        ///   - https://github.com/owner/repo/archive/refs/heads/branch.zip
        ///   - https://github.com/owner/repo/archive/refs/tags/tag.zip
        ///   - https://github.com/owner/repo/commit/sha
        /// </summary>
        /// <param name="gitUrl">Git 仓库地址</param>
        /// <param name="targetFolder">解压目标文件夹(绝对路径)</param>
        /// <param name="onProgress">进度回调,范围 0~1</param>
        /// <param name="onComplete">完成回调,返回下载结果</param>
        public static IEnumerator DownloadAndExtract(string gitUrl, string targetFolder, System.Action<float> onProgress, System.Action<GitDownloadResult> onComplete)
        {
            GitDownloadResult result = new GitDownloadResult();
            result.ExtractTargetPath = targetFolder;

            // 1. 解析 Git URL 得到 zip 下载地址
            string zipUrl;
            string refType; // branch / tag / commit
            string refName;
            string subFolder;
            if (!TryParseGitUrl(gitUrl, out zipUrl, out refType, out refName, out subFolder, out string errMsg))
            {
                Log.Warning("Git 链接解析失败: " + errMsg + " 原始URL: " + gitUrl, Log.LogType.Download);
                result.Error = GitDownloadError.InvalidUrl;
                result.ErrorMessage = errMsg;
                onComplete?.Invoke(result);
                yield break;
            }

            Log.Info("开始下载 Git 资源: " + gitUrl + " -> " + zipUrl, Log.LogType.Download);

            // 2. 准备临时 zip 保存路径
            string tempZipPath = System.IO.Path.Combine(targetFolder, "GitDownload_" + System.Math.Abs(gitUrl.GetHashCode()) + ".zip");
            result.ZipSavePath = tempZipPath;
            if (File.Exists(tempZipPath))
                File.Delete(tempZipPath);

            Sango.Directory.Create(tempZipPath, false);

            // 3. 使用 UnityWebRequest 下载 zip
            UnityWebRequest request = UnityWebRequest.Get(zipUrl);
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            while (!op.isDone)
            {
                onProgress?.Invoke(op.progress * 0.7f);
                yield return null;
            }
            onProgress?.Invoke(0.7f);

            if (request.result != UnityWebRequest.Result.Success)
            {
                string netErr = "网络请求失败: " + request.error + " URL: " + zipUrl;
                Log.Warning(netErr, Log.LogType.Download);
                result.Error = GitDownloadError.NetworkError;
                result.ErrorMessage = netErr;
                request.Dispose();
                onComplete?.Invoke(result);
                yield break;
            }

            // 4. 写入本地 zip
            try
            {
                File.WriteAllBytes(tempZipPath, request.downloadHandler.data);
            }
            catch (System.Exception e)
            {
                string writeErr = "写入 zip 文件失败: " + e.Message + " 路径: " + tempZipPath;
                Log.Warning(writeErr, Log.LogType.Download);
                result.Error = GitDownloadError.NetworkError;
                result.ErrorMessage = writeErr;
                request.Dispose();
                onComplete?.Invoke(result);
                yield break;
            }
            Log.Info("Git 资源 zip 下载完成: " + tempZipPath + " 大小: " + request.downloadHandler.data.Length, Log.LogType.Download);
            request.Dispose();

            int yieldCount = 0;
            // 5. 解压 zip
            //try
            //{
            Directory.Create(targetFolder);
            using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
            {
                int count = archive.Entries.Count;
                int cur = 0;
                if (count <= 0)
                {
                    // 空仓库,直接成功
                    result.IsSuccess = true;
                    onProgress?.Invoke(1f);
                    try { File.Delete(tempZipPath); } catch { }
                    onComplete?.Invoke(result);
                    yield break;
                }

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // GitHub 下载的 zip 顶层会带一个 "repo-branch/" 目录
                    // 如果指定了子目录则裁剪掉;否则保留整个仓库
                    string entryName = entry.FullName.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(subFolder))
                    {
                        if (!entryName.StartsWith(subFolder + "/", System.StringComparison.OrdinalIgnoreCase)
                            && !entryName.Equals(subFolder, System.StringComparison.OrdinalIgnoreCase))
                        {
                            cur++;
                            onProgress?.Invoke(0.7f + 0.3f * (cur / (float)count));
                            continue;
                        }
                        entryName = entryName.Substring(subFolder.Length);
                        if (entryName.StartsWith("/"))
                            entryName = entryName.Substring(1);
                    }

                    if (string.IsNullOrEmpty(entryName))
                    {
                        cur++;
                        onProgress?.Invoke(0.7f + 0.3f * (cur / (float)count));
                        continue;
                    }

                    string destPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(targetFolder, entryName));
                    // 防止 zip slip 漏洞
                    string fullTarget = System.IO.Path.GetFullPath(targetFolder);
                    if (!fullTarget.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                        fullTarget += System.IO.Path.DirectorySeparatorChar;
                    if (!destPath.StartsWith(fullTarget, System.StringComparison.OrdinalIgnoreCase))
                    {
                        string slipErr = "检测到非法路径(可能的 zip slip 攻击): " + entryName;
                        Log.Warning(slipErr, Log.LogType.Download);
                        result.Error = GitDownloadError.ExtractError;
                        result.ErrorMessage = slipErr;
                        onComplete?.Invoke(result);
                        yield break;
                    }

                    if (string.IsNullOrEmpty(System.IO.Path.GetFileName(destPath)))
                    {
                        // 目录条目
                        Directory.Create(destPath);
                    }
                    else
                    {
                        Directory.Create(System.IO.Path.GetDirectoryName(destPath));
                        // overwrite:Git 仓库内文件可能存在重复条目
                        if (File.Exists(destPath))
                            File.Delete(destPath);
                        entry.ExtractToFile(destPath);
                    }

                    cur++;
                    onProgress?.Invoke(0.7f + 0.3f * (cur / (float)count));

                    yieldCount++;
                    if (yieldCount > 10)
                    {
                        yieldCount = 0;
                        yield return null;
                    }
                }
            }
            //}
            //catch (System.Exception e)
            //{
            //    string extractErr = "解压失败: " + e.Message + " zip: " + tempZipPath + " 目标: " + targetFolder;
            //    Log.Error(extractErr, Log.LogType.Download);
            //    result.Error = GitDownloadError.ExtractError;
            //    result.ErrorMessage = extractErr;
            //    onComplete?.Invoke(result);
            //    yield break;
            //}

            // 6. 清理临时 zip
            try { File.Delete(tempZipPath); } catch { }

            result.IsSuccess = true;
            onProgress?.Invoke(1f);
            Log.Info("Git 资源下载并解压完成: " + gitUrl + " -> " + targetFolder, Log.LogType.Download);
            onComplete?.Invoke(result);
        }

        /// <summary>
        /// 下载并以"整体替换"的方式安装模组(协程版)
        ///
        /// 与直接覆盖解压的区别:
        ///   1. 先解压到模组根目录下的临时文件夹,安装过程中完全不碰已有模组;
        ///   2. 在临时文件夹里定位真正的模组目录(含 mod.info 的那一层),并校验 mod.info 中的 id;
        ///   3. 把旧模组目录改名备份,再把新目录整体移动为 &lt;rootFolder&gt;/&lt;expectModId&gt;;
        ///   4. 成功后删除备份与临时目录,任何一步失败都回滚,旧版本保持可用。
        ///
        /// 这样可以避免"新版本已经删掉的文件仍残留在旧目录里被继续加载"的问题。
        /// </summary>
        /// <param name="gitUrl">下载地址</param>
        /// <param name="rootFolder">模组根目录(Mods)</param>
        /// <param name="expectModId">期望的模组Id,同时也是安装后的目录名</param>
        /// <param name="onProgress">进度回调,范围 0~1</param>
        /// <param name="onComplete">完成回调,返回安装结果</param>
        public static IEnumerator DownloadAndInstall(string gitUrl, string rootFolder, string expectModId,
            System.Action<float> onProgress, System.Action<GitDownloadResult> onComplete)
        {
            GitDownloadResult result = new GitDownloadResult();
            result.ExtractTargetPath = rootFolder;

            if (string.IsNullOrEmpty(expectModId))
            {
                result.Error = GitDownloadError.InvalidUrl;
                result.ErrorMessage = "模组Id为空,无法安装";
                Log.Warning(result.ErrorMessage, Log.LogType.Download);
                onComplete?.Invoke(result);
                yield break;
            }

            if (!Sango.Directory.Exists(rootFolder))
                Sango.Directory.Create(rootFolder);

            // 临时解压目录:与正式目录同盘,后面才能用 Move 完成"整体替换"
            string stamp = System.DateTime.Now.Ticks.ToString();
            string tempFolder = System.IO.Path.Combine(rootFolder, $".tmp_{expectModId}_{stamp}");

            // 1.解压到临时目录
            GitDownloadResult extractResult = null;
            yield return DownloadAndExtract(gitUrl, tempFolder, onProgress, (r) => extractResult = r);

            if (extractResult == null || !extractResult.IsSuccess)
            {
                // 解压失败:丢掉临时目录,旧版本原样保留
                SafeDeleteDirectory(tempFolder);
                result.Error = extractResult != null ? extractResult.Error : GitDownloadError.ExtractError;
                result.ErrorMessage = extractResult != null ? extractResult.ErrorMessage : "解压失败";
                onComplete?.Invoke(result);
                yield break;
            }

            // 2.定位模组目录并校验 mod.info
            string modRoot = FindModRoot(tempFolder, expectModId);
            if (string.IsNullOrEmpty(modRoot))
            {
                SafeDeleteDirectory(tempFolder);
                result.Error = GitDownloadError.ExtractError;
                result.ErrorMessage = "下载包中没有找到 mod.info,已取消安装";
                Log.Warning(result.ErrorMessage + " URL: " + gitUrl, Log.LogType.Download);
                onComplete?.Invoke(result);
                yield break;
            }

            string realModId = ReadModInfoValue(modRoot, "id");
            if (!string.IsNullOrEmpty(realModId) && realModId != expectModId)
            {
                SafeDeleteDirectory(tempFolder);
                result.Error = GitDownloadError.ExtractError;
                result.ErrorMessage = $"下载包内的模组Id({realModId})与期望的Id({expectModId})不一致,已取消安装";
                Log.Warning(result.ErrorMessage + " URL: " + gitUrl, Log.LogType.Download);
                onComplete?.Invoke(result);
                yield break;
            }

            // 3.整体替换:旧目录先改名备份,新目录再顶上,失败可以回滚
            string finalFolder = System.IO.Path.Combine(rootFolder, expectModId);
            string backupFolder = null;
            if (System.IO.Directory.Exists(finalFolder))
            {
                backupFolder = System.IO.Path.Combine(rootFolder, $".old_{expectModId}_{stamp}");
                try
                {
                    System.IO.Directory.Move(finalFolder, backupFolder);
                }
                catch (System.Exception e)
                {
                    SafeDeleteDirectory(tempFolder);
                    result.Error = GitDownloadError.ExtractError;
                    result.ErrorMessage = "旧模组目录无法移动(可能文件被占用,请重启游戏后再更新): " + e.Message;
                    Log.Warning(result.ErrorMessage, Log.LogType.Download);
                    onComplete?.Invoke(result);
                    yield break;
                }
            }

            try
            {
                System.IO.Directory.Move(modRoot, finalFolder);
            }
            catch (System.Exception e)
            {
                // 回滚:把备份改回原名,保证旧版本仍然可用
                if (!string.IsNullOrEmpty(backupFolder) && System.IO.Directory.Exists(backupFolder))
                {
                    try { System.IO.Directory.Move(backupFolder, finalFolder); } catch { }
                }
                SafeDeleteDirectory(tempFolder);
                result.Error = GitDownloadError.ExtractError;
                result.ErrorMessage = "安装新版本失败,已回滚到旧版本: " + e.Message;
                Log.Warning(result.ErrorMessage, Log.LogType.Download);
                onComplete?.Invoke(result);
                yield break;
            }

            // 4.清理备份与临时目录
            SafeDeleteDirectory(tempFolder);
            if (!string.IsNullOrEmpty(backupFolder))
                SafeDeleteDirectory(backupFolder);

            result.IsSuccess = true;
            result.ExtractTargetPath = rootFolder;
            onProgress?.Invoke(1f);
            Log.Info("模组已整体替换安装完成: " + finalFolder, Log.LogType.Download);
            onComplete?.Invoke(result);
        }

        /// <summary>
        /// 判断目录名是否是安装过程中的临时目录(临时解压目录 / 旧版本备份目录)
        /// 这类目录不能当作正常模组加载
        /// </summary>
        /// <param name="dir">目录路径</param>
        /// <returns>是临时目录返回true</returns>
        public static bool IsTempModDirectory(string dir)
        {
            if (string.IsNullOrEmpty(dir))
                return false;
            string name = System.IO.Path.GetFileName(dir.TrimEnd('/', '\\'));
            return name.StartsWith(".tmp_") || name.StartsWith(".old_");
        }

        /// <summary>
        /// 安全删除目录(目录不存在或删除失败都不会抛异常)
        /// </summary>
        static void SafeDeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                if (System.IO.Directory.Exists(path))
                    System.IO.Directory.Delete(path, true);
            }
            catch (System.Exception e)
            {
                Log.Warning("删除目录失败: " + path + " " + e.Message, Log.LogType.Download);
            }
        }

        /// <summary>
        /// 在解压出来的临时目录里定位真正的模组目录(含 mod.info 的那一层)
        /// 优先返回 mod.info 中 id 与期望Id一致的那个,都不匹配时返回第一个找到的目录
        /// </summary>
        /// <param name="tempFolder">临时解压目录</param>
        /// <param name="expectModId">期望的模组Id</param>
        /// <returns>模组目录路径,没找到返回null</returns>
        static string FindModRoot(string tempFolder, string expectModId)
        {
            List<string> candidates = new List<string>();
            CollectModRoots(tempFolder, 0, 3, candidates);

            if (candidates.Count == 0)
                return null;
            if (candidates.Count == 1)
                return candidates[0];

            for (int i = 0; i < candidates.Count; i++)
            {
                if (ReadModInfoValue(candidates[i], "id") == expectModId)
                    return candidates[i];
            }
            return candidates[0];
        }

        /// <summary>
        /// 递归收集含 mod.info 的目录(限制最大深度,避免遍历整棵解压树)
        /// </summary>
        /// <param name="dir">当前目录</param>
        /// <param name="depth">当前深度</param>
        /// <param name="maxDepth">最大深度</param>
        /// <param name="result">收集结果</param>
        static void CollectModRoots(string dir, int depth, int maxDepth, List<string> result)
        {
            if (depth > maxDepth || !System.IO.Directory.Exists(dir))
                return;

            if (System.IO.File.Exists(System.IO.Path.Combine(dir, "mod.info")))
            {
                result.Add(dir);
                return;
            }

            string[] subDirs = System.IO.Directory.GetDirectories(dir);
            for (int i = 0; i < subDirs.Length; i++)
                CollectModRoots(subDirs[i], depth + 1, maxDepth, result);
        }

        /// <summary>
        /// 读取 mod.info 中指定键的值(逐行 key=value,键名不区分大小写)
        /// </summary>
        /// <param name="modDir">模组目录</param>
        /// <param name="key">键名(小写),例如 id</param>
        /// <returns>键对应的值,读取失败返回null</returns>
        static string ReadModInfoValue(string modDir, string key)
        {
            string infoFile = System.IO.Path.Combine(modDir, "mod.info");
            if (!System.IO.File.Exists(infoFile))
                return null;

            try
            {
                string[] lines = System.IO.File.ReadAllLines(infoFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line))
                        continue;

                    int index = line.IndexOf('=');
                    if (index <= 0)
                        continue;

                    if (line.Substring(0, index).Trim().ToLower() == key)
                        return line.Substring(index + 1).Trim();
                }
            }
            catch (System.Exception e)
            {
                Log.Warning("读取 mod.info 失败: " + infoFile + " " + e.Message, Log.LogType.Download);
            }
            return null;
        }

        /// <summary>
        /// 从 Git 链接下载并解压到目标文件夹(协程版)
        /// 支持的 URL 形式:
        ///   - https://github.com/owner/repo
        ///   - https://github.com/owner/repo/tree/branch
        ///   - https://github.com/owner/repo/tree/branch/sub/path
        ///   - https://github.com/owner/repo/archive/refs/heads/branch.zip
        ///   - https://github.com/owner/repo/archive/refs/tags/tag.zip
        ///   - https://github.com/owner/repo/commit/sha
        /// </summary>
        /// <param name="gitUrl">Git 仓库地址</param>
        /// <param name="targetFolder">解压目标文件夹(绝对路径)</param>
        /// <param name="onProgress">进度回调,范围 0~1</param>
        /// <param name="onComplete">完成回调,返回下载结果</param>
        public static IEnumerator Get(string gitUrl, System.Action<float> onProgress, System.Action<string> onComplete)
        {

            Log.Info("开始下载 Git 资源: " + gitUrl, Log.LogType.Download);

            // 3. 使用 UnityWebRequest 下载 zip
            UnityWebRequest request = UnityWebRequest.Get(gitUrl);
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            onProgress?.Invoke(0f);
            while (!op.isDone)
            {
                onProgress?.Invoke(op.progress);
                yield return null;
            }
            onProgress?.Invoke(1f);

            if (request.result != UnityWebRequest.Result.Success)
            {
                string netErr = "网络请求失败: " + request.error + " URL: " + gitUrl;
                Log.Warning(netErr, Log.LogType.Download);
                request.Dispose();
                onComplete?.Invoke("");
                yield break;
            }
            onComplete?.Invoke(request.downloadHandler.text);
            Log.Info("Git 资源 下载完成: " + request.downloadHandler.text + " 大小: " + request.downloadHandler.data.Length, Log.LogType.Download);
            request.Dispose();
        }


        /// <summary>
        /// 同步阻塞版(仅用于编辑器或非主线程用途)
        /// </summary>
        /// <param name="gitUrl">Git 仓库地址</param>
        /// <param name="targetFolder">解压目标文件夹(绝对路径)</param>
        /// <param name="onProgress">进度回调,范围 0~1</param>
        /// <returns>下载结果</returns>
        public static GitDownloadResult DownloadAndExtractSync(string gitUrl, string targetFolder, System.Action<float> onProgress = null)
        {
            GitDownloadResult result = new GitDownloadResult();
            result.ExtractTargetPath = targetFolder;

            string zipUrl;
            string refType;
            string refName;
            string subFolder;
            if (!TryParseGitUrl(gitUrl, out zipUrl, out refType, out refName, out subFolder, out string errMsg))
            {
                Log.Warning("Git 链接解析失败: " + errMsg + " 原始URL: " + gitUrl, Log.LogType.Download);
                result.Error = GitDownloadError.InvalidUrl;
                result.ErrorMessage = errMsg;
                return result;
            }

            string tempZipPath = System.IO.Path.Combine(Application.temporaryCachePath, "GitDownload_" + System.Math.Abs(gitUrl.GetHashCode()) + ".zip");
            result.ZipSavePath = tempZipPath;
            if (File.Exists(tempZipPath))
                File.Delete(tempZipPath);

            UnityWebRequest request = UnityWebRequest.Get(zipUrl);
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            while (!op.isDone)
            {
                onProgress?.Invoke(op.progress * 0.7f);
                Thread.Sleep(10);
            }
            onProgress?.Invoke(0.7f);

            if (request.result != UnityWebRequest.Result.Success)
            {
                string netErr = "网络请求失败: " + request.error + " URL: " + zipUrl;
                Log.Warning(netErr, Log.LogType.Download);
                result.Error = GitDownloadError.NetworkError;
                result.ErrorMessage = netErr;
                request.Dispose();
                return result;
            }

            try
            {
                File.WriteAllBytes(tempZipPath, request.downloadHandler.data);
            }
            catch (System.Exception e)
            {
                string writeErr = "写入 zip 文件失败: " + e.Message + " 路径: " + tempZipPath;
                Log.Warning(writeErr, Log.LogType.Download);
                result.Error = GitDownloadError.NetworkError;
                result.ErrorMessage = writeErr;
                request.Dispose();
                return result;
            }
            request.Dispose();

            try
            {
                Directory.Create(targetFolder);
                using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
                {
                    int count = archive.Entries.Count;
                    int cur = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string entryName = entry.FullName.Replace('\\', '/');
                        if (!string.IsNullOrEmpty(subFolder))
                        {
                            if (!entryName.StartsWith(subFolder + "/", System.StringComparison.OrdinalIgnoreCase)
                                && !entryName.Equals(subFolder, System.StringComparison.OrdinalIgnoreCase))
                            {
                                cur++;
                                onProgress?.Invoke(0.7f + 0.3f * (cur / (float)count));
                                continue;
                            }
                            entryName = entryName.Substring(subFolder.Length);
                            if (entryName.StartsWith("/"))
                                entryName = entryName.Substring(1);
                        }

                        if (string.IsNullOrEmpty(entryName))
                        {
                            cur++;
                            onProgress?.Invoke(0.7f + 0.3f * (cur / (float)count));
                            continue;
                        }

                        string destPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(targetFolder, entryName));
                        string fullTarget = System.IO.Path.GetFullPath(targetFolder);
                        if (!fullTarget.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                            fullTarget += System.IO.Path.DirectorySeparatorChar;
                        if (!destPath.StartsWith(fullTarget, System.StringComparison.OrdinalIgnoreCase))
                        {
                            string slipErr = "检测到非法路径(可能的 zip slip 攻击): " + entryName;
                            Log.Warning(slipErr, Log.LogType.Download);
                            result.Error = GitDownloadError.ExtractError;
                            result.ErrorMessage = slipErr;
                            return result;
                        }

                        if (string.IsNullOrEmpty(System.IO.Path.GetFileName(destPath)))
                        {
                            Directory.Create(destPath);
                        }
                        else
                        {
                            Directory.Create(System.IO.Path.GetDirectoryName(destPath));
                            if (File.Exists(destPath))
                                File.Delete(destPath);
                            entry.ExtractToFile(destPath);
                        }

                        cur++;
                        onProgress?.Invoke(0.7f + 0.3f * (cur / (float)count));
                    }
                }
            }
            catch (System.Exception e)
            {
                string extractErr = "解压失败: " + e.Message + " zip: " + tempZipPath + " 目标: " + targetFolder;
                Log.Warning(extractErr, Log.LogType.Download);
                result.Error = GitDownloadError.ExtractError;
                result.ErrorMessage = extractErr;
                return result;
            }

            try { File.Delete(tempZipPath); } catch { }
            result.IsSuccess = true;
            onProgress?.Invoke(1f);
            Log.Info("Git 资源下载并解压完成: " + gitUrl + " -> " + targetFolder, Log.LogType.Download);
            return result;
        }

        /// <summary>
        /// 解析 Git URL
        /// </summary>
        /// <param name="gitUrl">原始 URL</param>
        /// <param name="zipUrl">下载用的 zip URL</param>
        /// <param name="refType">引用类型(branch/tag/commit)</param>
        /// <param name="refName">引用名</param>
        /// <param name="subFolder">子目录(无则为空)</param>
        /// <param name="errMsg">错误信息</param>
        /// <returns>是否解析成功</returns>
        public static bool TryParseGitUrl(string gitUrl, out string zipUrl, out string refType, out string refName, out string subFolder, out string errMsg)
        {
            zipUrl = null;
            refType = null;
            refName = null;
            subFolder = null;
            errMsg = null;

            if (string.IsNullOrEmpty(gitUrl))
            {
                errMsg = "Git URL 为空";
                return false;
            }

            // 已经是 .zip 链接直接返回
            if (gitUrl.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase))
            {
                zipUrl = gitUrl;
                return true;
            }

            // 只支持 https:// 形式
            if (!gitUrl.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase)
                && !gitUrl.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase))
            {
                errMsg = "仅支持 http(s) 形式的 Git 链接";
                return false;
            }

            // 兼容 .git 后缀
            string cleanUrl = gitUrl;
            if (cleanUrl.EndsWith(".git", System.StringComparison.OrdinalIgnoreCase))
                cleanUrl = cleanUrl.Substring(0, cleanUrl.Length - 4);

            // 解析 URL 段
            // 形式1: https://host/owner/repo
            // 形式2: https://host/owner/repo/tree/branch
            // 形式3: https://host/owner/repo/tree/branch/sub/folder
            // 形式4: https://host/owner/repo/blob/branch/path
            // 形式5: https://host/owner/repo/commit/sha
            // 形式6: https://host/owner/repo/releases/tag/tag
            try
            {
                System.Uri uri = new System.Uri(cleanUrl);
                string[] segs = uri.AbsolutePath.Split(new char[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (segs.Length < 2)
                {
                    errMsg = "URL 段过少,无法识别 owner/repo";
                    return false;
                }

                string owner = segs[0];
                string repo = segs[1];
                string baseUrl = uri.Scheme + "://" + uri.Host;

                // 默认分支
                if (segs.Length == 2)
                {
                    // 直接 https://github.com/owner/repo 时无法得知默认分支,
                    // 此处通过 GitHub API 不便,使用 HEAD 分支占位:
                    // 实际使用建议传入 https://github.com/owner/repo/tree/main
                    errMsg = "未指定分支,请使用 https://" + uri.Host + "/" + owner + "/" + repo + "/tree/<branch> 形式";
                    return false;
                }

                string op = segs[2].ToLowerInvariant();
                if (op == "tree" || op == "blob")
                {
                    if (segs.Length < 4)
                    {
                        errMsg = "缺少分支/标签名";
                        return false;
                    }
                    refType = "branch";
                    refName = segs[3];
                    // 子目录
                    if (segs.Length > 4)
                    {
                        subFolder = string.Join("/", segs, 4, segs.Length - 4);
                    }
                    zipUrl = baseUrl + "/" + owner + "/" + repo + "/archive/refs/heads/" + refName + ".zip";
                    return true;
                }
                else if (op == "commit")
                {
                    if (segs.Length < 4)
                    {
                        errMsg = "缺少 commit SHA";
                        return false;
                    }
                    refType = "commit";
                    refName = segs[3];
                    zipUrl = baseUrl + "/" + owner + "/" + repo + "/archive/" + refName + ".zip";
                    return true;
                }
                else if (op == "releases")
                {
                    // /releases/tag/tagname 或 /releases/download/tagname/file
                    if (segs.Length >= 5 && segs[3].ToLowerInvariant() == "tag")
                    {
                        refType = "tag";
                        refName = segs[4];
                        zipUrl = baseUrl + "/" + owner + "/" + repo + "/archive/refs/tags/" + refName + ".zip";
                        return true;
                    }
                    errMsg = "不支持的 releases 形式,目前仅支持 /releases/tag/<tagname>";
                    return false;
                }
                else if (op == "tags")
                {
                    if (segs.Length < 4)
                    {
                        errMsg = "缺少 tag 名";
                        return false;
                    }
                    refType = "tag";
                    refName = segs[3];
                    zipUrl = baseUrl + "/" + owner + "/" + repo + "/archive/refs/tags/" + refName + ".zip";
                    return true;
                }
                else
                {
                    // 把 owner/repo/<op>... 当作分支
                    refType = "branch";
                    refName = op;
                    if (segs.Length > 3)
                        subFolder = string.Join("/", segs, 3, segs.Length - 3);
                    zipUrl = baseUrl + "/" + owner + "/" + repo + "/archive/refs/heads/" + refName + ".zip";
                    return true;
                }
            }
            catch (System.Exception e)
            {
                errMsg = "URL 解析异常: " + e.Message;
                return false;
            }
        }
    }
}
