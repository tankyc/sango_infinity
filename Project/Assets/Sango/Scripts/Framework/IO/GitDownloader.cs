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
        /// 网络失败后的最大尝试次数。
        /// SSL 握手失败/连接被重置多数是偶发的(代理抖动、TLS 被临时拦截),重试一两次往往就能成功。
        /// </summary>
        const int MaxDownloadRetry = 3;

        /// <summary>
        /// 下载过程中"完全没有进度变化"的容忍时间(秒)。
        /// 超过该时间没有收到任何数据就主动中断请求:
        /// 旧实现没有超时,网络被拦截时 while(!op.isDone) 会永远转下去,
        /// 界面表现就是"点了下载没有任何响应",而且再也点不动(见 IsDownloading 的说明)。
        /// </summary>
        const float NoProgressTimeout = 20f;

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

            /// <summary>
            /// 玩家主动取消(保留已下载的部分,下次可断点续传)
            /// </summary>
            Cancelled = 4,
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
        /// <param name="expectedBytes">预计的 zip 大小(用于断点续传判定与磁盘预检,0 表示未知)</param>
        /// <param name="isCancelled">取消检测回调,返回 true 时中断下载(已下载部分会保留)</param>
        public static IEnumerator DownloadAndExtract(string gitUrl, string targetFolder,
            System.Action<float> onProgress, System.Action<GitDownloadResult> onComplete,
            long expectedBytes = 0, System.Func<bool> isCancelled = null)
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
            //    下载中的内容写在 xxx.zip.part,中断后保留,重试时从断点继续
            string tempZipPath = System.IO.Path.Combine(targetFolder, "GitDownload_" + System.Math.Abs(gitUrl.GetHashCode()) + ".zip");
            result.ZipSavePath = tempZipPath;
            Sango.Directory.Create(tempZipPath, false);

            // 3. 下载 zip(带重试 / 断点续传 / 无进度超时 / 直接落盘)
            bool downloadOk = false;
            bool cancelled = false;
            string downloadError = null;
            yield return DownloadWithRetry(zipUrl, tempZipPath, expectedBytes,
                (p) => onProgress?.Invoke(p * 0.7f),
                (ok, err, isCancel) => { downloadOk = ok; downloadError = err; cancelled = isCancel; },
                isCancelled);

            if (cancelled)
            {
                result.Error = GitDownloadError.Cancelled;
                result.ErrorMessage = "下载已取消";
                Log.Info("下载已取消(已下载部分已保留,下次继续): " + zipUrl, Log.LogType.Download);
                onComplete?.Invoke(result);
                yield break;
            }

            if (!downloadOk)
            {
                string netErr = "网络请求失败: " + downloadError + " URL: " + zipUrl;
                // 这里必须用 Error:Warning/Info 带 [Conditional("SANGO_DEBUG")],正式包里整句会被编译剔除,
                // 线上玩家下载失败时既没有提示也没有任何日志,无法排查
                Log.Error(netErr, Log.LogType.Download);
                result.Error = GitDownloadError.NetworkError;
                result.ErrorMessage = GetFriendlyNetworkError(downloadError);
                SafeDeleteFile(tempZipPath);
                onComplete?.Invoke(result);
                yield break;
            }

            // 4. 校验下载内容确实是 zip
            //    常见故障:被运营商/代理拦截后返回一段 HTML 错误页,虽然 HTTP 200 但不是压缩包,
            //    直接丢给 ZipFile.OpenRead 会抛异常,旧代码没有兜住,会把整条协程链打断
            if (!IsZipFile(tempZipPath))
            {
                string badErr = "下载到的内容不是有效的压缩包(可能被网络拦截或服务器返回了错误页面) URL: " + zipUrl;
                Log.Error(badErr, Log.LogType.Download);
                result.Error = GitDownloadError.NetworkError;
                result.ErrorMessage = "下载到的内容不是有效的压缩包(可能被网络拦截),请稍后重试";
                SafeDeleteFile(tempZipPath);
                SafeDeleteFile(tempZipPath + ".part");
                onComplete?.Invoke(result);
                yield break;
            }

            Log.Info("Git 资源 zip 下载完成: " + tempZipPath + " 大小: " + new FileInfo(tempZipPath).Length, Log.LogType.Download);

            // 5. 解压(后台线程,不阻塞主线程)
            bool extractOk = false;
            string extractError = null;
            yield return ExtractAsync(tempZipPath, targetFolder, subFolder, 0.7f, 0.3f,
                (ok, err) => { extractOk = ok; extractError = err; }, onProgress);

            // 6. 清理临时 zip
            SafeDeleteFile(tempZipPath);

            if (!extractOk)
            {
                result.Error = GitDownloadError.ExtractError;
                result.ErrorMessage = extractError;
                onComplete?.Invoke(result);
                yield break;
            }

            result.IsSuccess = true;
            onProgress?.Invoke(1f);
            Log.Info("Git 资源下载并解压完成: " + gitUrl + " -> " + targetFolder, Log.LogType.Download);
            onComplete?.Invoke(result);
        }

        /// <summary>
        /// 带重试的下载(协程)
        /// 失败重试时优先走断点续传;SSL 握手失败则自动放宽证书校验再试
        /// </summary>
        /// <param name="url">下载地址</param>
        /// <param name="savePath">本地保存路径</param>
        /// <param name="totalSize">预计文件大小(0 表示未知)</param>
        /// <param name="onProgress">进度回调,范围 0~1</param>
        /// <param name="onDone">完成回调(是否成功, 失败原因, 是否被取消)</param>
        /// <param name="isCancelled">取消检测回调</param>
        static IEnumerator DownloadWithRetry(string url, string savePath, long totalSize,
            System.Action<float> onProgress, System.Action<bool, string, bool> onDone,
            System.Func<bool> isCancelled)
        {
            int attempt = 0;
            bool ok = false;
            bool cancelled = false;
            string error = null;
            // SSL 握手失败时是否放宽证书校验(只在确认是 SSL 错误后才打开)
            bool allowInsecure = false;

            while (attempt < MaxDownloadRetry && !ok)
            {
                if (IsCancelled(isCancelled))
                {
                    cancelled = true;
                    break;
                }

                attempt++;
                if (attempt > 1)
                    Log.Info("下载失败,准备第 " + attempt + " 次重试(断点续传): " + url, Log.LogType.Download);

                yield return DownloadToFile(url, savePath, totalSize, onProgress,
                    (o, e, c) => { ok = o; error = e; cancelled = c; },
                    allowInsecure, isCancelled);

                if (ok || cancelled)
                    break;

                if (IsSslError(error))
                {
                    // 服务器证书链过期/玩家网络中有代理或杀软拦截 TLS 时,
                    // Unity 内置的 CA 库会握手失败;下一次重试放宽证书校验,给玩家一条可走的路。
                    // 模组包本身没有签名机制,放宽校验不额外降低安全性;
                    // 但解压前仍会校验 zip 文件头,避免把错误页面当成模组安装。
                    if (!allowInsecure)
                        Log.Error("SSL 握手失败,下一次重试将放宽证书校验: " + error, Log.LogType.Download);
                    allowInsecure = true;
                }
                else
                {
                    // 非 SSL 错误(超时/断开):稍等一下再重试,避免瞬间连续打服务器
                    yield return new WaitForSecondsRealtime(1f);
                }
            }

            onDone?.Invoke(ok, error, cancelled);
        }

        /// <summary>
        /// 下载单个文件到本地(协程)
        /// 特性:断点续传(写在 .part,中断后保留)、无进度超时、可取消、直接落盘
        /// </summary>
        /// <param name="url">下载地址</param>
        /// <param name="savePath">本地保存路径(最终文件名)</param>
        /// <param name="totalSize">预计文件大小(0 表示未知,未知时不做续传)</param>
        /// <param name="onProgress">进度回调,范围 0~1(含已续传的部分)</param>
        /// <param name="onDone">完成回调(是否成功, 失败原因, 是否被取消)</param>
        /// <param name="allowInvalidCertificate">是否放宽 HTTPS 证书校验(仅 SSL 握手失败重试时使用)</param>
        /// <param name="isCancelled">取消检测回调</param>
        static IEnumerator DownloadToFile(string url, string savePath, long totalSize,
            System.Action<float> onProgress, System.Action<bool, string, bool> onDone,
            bool allowInvalidCertificate = false, System.Func<bool> isCancelled = null)
        {
            Sango.Directory.Create(savePath, false);

            // 下载中的数据写在 <savePath>.part:中途取消/断网/超时都保留,下次从断点继续
            string partPath = savePath + ".part";

            long existing = 0;
            try
            {
                if (File.Exists(partPath))
                    existing = new FileInfo(partPath).Length;
            }
            catch (System.Exception e)
            {
                Log.Error("读取断点文件失败: " + partPath + " " + e.Message, Log.LogType.Download);
                existing = 0;
            }

            // 残包大小已经等于目标大小:上次其实已经下完,直接收尾
            if (totalSize > 0 && existing == totalSize)
            {
                if (CommitPartFile(partPath, savePath, out string commitErr))
                {
                    onProgress?.Invoke(1f);
                    onDone?.Invoke(true, null, false);
                    yield break;
                }
                Log.Error(commitErr, Log.LogType.Download);
                existing = 0;
            }

            // 残包比目标还大 / 不知道总大小 → 无法安全续传,丢弃重下
            if (existing > 0 && (totalSize <= 0 || existing > totalSize))
            {
                Log.Info("断点文件大小异常,放弃续传重新下载: " + partPath, Log.LogType.Download);
                existing = 0;
                SafeDeleteFile(partPath);
            }

            if (existing > 0)
                Log.Info("检测到未完成的下载,从断点继续: " + partPath + " 已下载 " + existing + " 字节", Log.LogType.Download);

            string error = null;
            bool cancelled = false;
            bool restartFromZero = false;
            bool serverRejected = false;
            long responseCode = 0;

            // 直接构造请求(而不是 UnityWebRequest.Get),这样只创建下载到文件的 handler,
            // 不会额外产生一个用不上的内存 buffer
            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
            {
                // append=true:续传时把新数据追加到 .part 末尾
                request.downloadHandler = new DownloadHandlerFile(partPath, existing > 0);

                // 断点续传:告诉服务器只要剩余部分(R2 / nginx 都返回 206)
                if (existing > 0)
                    request.SetRequestHeader("Range", "bytes=" + existing + "-");

                // SSL 握手失败的重试:放宽证书链校验
                // 说明:该 handler 只作用于本次请求,不影响游戏内其它 HTTPS 请求
                if (allowInvalidCertificate)
                    request.certificateHandler = new RelaxedCertificateHandler();

                // 不使用 request.timeout:它计算的是整个请求的总时长,大文件正常下载也会被误杀。
                // 这里改用"多久没有任何进度变化"来判断是否卡死
                request.timeout = 0;

                UnityWebRequestAsyncOperation op = request.SendWebRequest();

                float lastProgress = -1f;
                float stallTime = 0f;
                while (!op.isDone)
                {
                    // 取消:立即中断,已下载部分保留在 .part 里
                    if (IsCancelled(isCancelled))
                    {
                        request.Abort();
                        cancelled = true;
                        break;
                    }

                    float p = CalcDownloadProgress(request, existing, totalSize);
                    if (p > lastProgress + 0.0001f)
                    {
                        // 进度有推进,重置卡死计时
                        lastProgress = p;
                        stallTime = 0f;
                    }
                    else
                    {
                        stallTime += Time.unscaledDeltaTime;
                    }

                    if (stallTime >= NoProgressTimeout)
                    {
                        // 服务器/代理迟迟不返回数据,主动中断,避免无限等待
                        request.Abort();
                        error = "下载长时间无响应(超过 " + (int)NoProgressTimeout + " 秒没有收到数据)";
                        break;
                    }

                    onProgress?.Invoke(UnityEngine.Mathf.Clamp01(p));
                    yield return null;
                }

                responseCode = request.responseCode;

                if (!cancelled && string.IsNullOrEmpty(error))
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        error = string.IsNullOrEmpty(request.error) ? request.result.ToString() : request.error;
                    }
                    else if (existing > 0 && responseCode == 200)
                    {
                        // 我们带了 Range,服务器却回 200(忽略续传)→ 这次返回的是整包,
                        // 已经被追加到残包后面,内容不可用,必须丢弃重下
                        restartFromZero = true;
                    }
                    else if (responseCode != 200 && responseCode != 206)
                    {
                        error = "服务器返回异常状态码: " + responseCode;
                        serverRejected = true;
                    }
                }
            }

            if (cancelled)
            {
                // 保留 .part:下次点下载可以直接续传
                onDone?.Invoke(false, "下载已取消", true);
                yield break;
            }

            if (restartFromZero)
            {
                SafeDeleteFile(partPath);
                onDone?.Invoke(false, "服务器不支持断点续传,已重新开始下载", false);
                yield break;
            }

            if (string.IsNullOrEmpty(error))
            {
                long finalLen = 0;
                try
                {
                    finalLen = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
                }
                catch (System.Exception e)
                {
                    error = "读取下载文件失败: " + e.Message;
                }

                if (string.IsNullOrEmpty(error))
                {
                    // 只有"续传模式"才严格校验总长度:续传必须知道终点在哪。
                    // 首次整包下载时以服务器实际返回的大小为准 —— 市场清单里的 size 可能
                    // 与真实包体不一致(作者重传同版本等),若严格相等会导致永远下载失败
                    if (existing > 0 && totalSize > 0 && finalLen != totalSize)
                        error = "下载不完整(已下载 " + finalLen + " / " + totalSize + " 字节)";
                    else if (finalLen <= 0)
                        error = "服务器返回了空文件";
                    else if (totalSize > 0 && finalLen != totalSize)
                        Log.Info("下载大小与市场清单不一致(实际 " + finalLen + " / 清单 " + totalSize +
                                 "),以实际文件为准", Log.LogType.Download);

                    if (string.IsNullOrEmpty(error) && !CommitPartFile(partPath, savePath, out string commitErr))
                        error = commitErr;
                }
            }

            if (!string.IsNullOrEmpty(error))
            {
                if (serverRejected)
                {
                    // 4xx/5xx 说明 URL 或权限有问题,残包没有续传价值
                    SafeDeleteFile(partPath);
                }
                else
                {
                    // 超时/断开/不完整:保留 .part,下次接着下
                    Log.Info("本次下载未完成,已保留断点文件以便续传: " + partPath, Log.LogType.Download);
                }
            }

            onDone?.Invoke(string.IsNullOrEmpty(error), error, false);
        }

        /// <summary>
        /// 计算含"已续传部分"的总进度
        /// </summary>
        /// <param name="request">当前请求</param>
        /// <param name="alreadyBytes">续传前已存在的字节数</param>
        /// <param name="totalSize">文件总大小(0 表示未知)</param>
        /// <returns>进度 0~1</returns>
        static float CalcDownloadProgress(UnityWebRequest request, long alreadyBytes, long totalSize)
        {
            if (totalSize <= 0)
                return request.downloadProgress;

            long done = alreadyBytes + (long)request.downloadedBytes;
            if (done >= totalSize)
                return 1f;
            return done / (float)totalSize;
        }

        /// <summary>
        /// 把下载完成的 .part 提交为正式文件
        /// </summary>
        /// <param name="partPath">断点文件路径</param>
        /// <param name="savePath">最终文件路径</param>
        /// <param name="error">失败原因</param>
        /// <returns>成功返回 true</returns>
        static bool CommitPartFile(string partPath, string savePath, out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(partPath))
                {
                    error = "下载文件不存在: " + partPath;
                    return false;
                }

                if (File.Exists(savePath))
                    File.Delete(savePath);

                File.Move(partPath, savePath);
                return true;
            }
            catch (System.Exception e)
            {
                error = "保存下载文件失败: " + e.Message;
                return false;
            }
        }

        /// <summary>
        /// 检查是否已被取消(回调本身抛异常时按未取消处理,不能因为监听方出错就中断下载)
        /// </summary>
        /// <param name="isCancelled">取消检测回调</param>
        /// <returns>已取消返回 true</returns>
        static bool IsCancelled(System.Func<bool> isCancelled)
        {
            if (isCancelled == null)
                return false;
            try
            {
                return isCancelled();
            }
            catch (System.Exception e)
            {
                Log.Error("取消状态检查失败: " + e.Message, Log.LogType.Download);
                return false;
            }
        }

        /// <summary>
        /// 后台线程解压(协程),解压期间不阻塞主线程
        /// </summary>
        /// <param name="zipPath">zip 文件路径</param>
        /// <param name="targetFolder">解压目标目录</param>
        /// <param name="subFolder">只解压该子目录(为空则解压全部)</param>
        /// <param name="progressFrom">进度起点(如 0.7)</param>
        /// <param name="progressAmount">本阶段占用的进度区间(如 0.3)</param>
        /// <param name="onDone">完成回调(是否成功, 失败原因)</param>
        /// <param name="onProgress">总进度回调</param>
        static IEnumerator ExtractAsync(string zipPath, string targetFolder, string subFolder,
            float progressFrom, float progressAmount,
            System.Action<bool, string> onDone, System.Action<float> onProgress)
        {
            Directory.Create(targetFolder);

            float percent = 0f;
            bool done = false;
            System.Exception exception = null;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    ExtractZipToFolder(zipPath, targetFolder, subFolder, (p) => percent = p);
                }
                catch (System.Exception e)
                {
                    exception = e;
                }
                finally
                {
                    done = true;
                }
            });

            while (!done)
            {
                onProgress?.Invoke(progressFrom + progressAmount * UnityEngine.Mathf.Clamp01(percent));
                yield return null;
            }

            // 兜底:解压阶段的任何异常都必须转成"失败结果"返回,
            // 否则异常会打断整条协程链,onComplete 永远不触发,
            // 上层 isDownloading 会永远停在 true,表现为"再点下载没有任何响应"
            if (exception != null)
            {
                string extractErr = "解压失败: " + exception.Message + " zip: " + zipPath + " 目标: " + targetFolder;
                Log.Error(extractErr, Log.LogType.Download);
                onDone?.Invoke(false, "解压失败(压缩包可能损坏或磁盘空间不足): " + exception.Message);
                yield break;
            }

            onProgress?.Invoke(progressFrom + progressAmount);
            onDone?.Invoke(true, null);
        }

        /// <summary>
        /// 下载/解压前的磁盘空间预检
        /// 估算方式:zip 本身 + 解压产物(约 1.5 倍),再留 100MB 余量,
        /// 避免"下载到一半/解压到一半才发现空间不足"
        /// </summary>
        /// <param name="folder">安装目录</param>
        /// <param name="expectedBytes">预计的 zip 大小(0 表示未知,跳过检查)</param>
        /// <param name="error">空间不足时的中文提示</param>
        /// <returns>空间足够返回 true</returns>
        public static bool EnsureDiskSpace(string folder, long expectedBytes, out string error)
        {
            error = null;
            if (expectedBytes <= 0)
                return true;

            try
            {
                string full = System.IO.Path.GetFullPath(folder);
                string rootPath = System.IO.Path.GetPathRoot(full);
                if (string.IsNullOrEmpty(rootPath))
                    return true;

                DriveInfo drive = new DriveInfo(rootPath);
                if (!drive.IsReady)
                    return true;

                long need = (long)(expectedBytes * 2.5d) + 100L * 1024 * 1024;
                if (drive.AvailableFreeSpace < need)
                {
                    error = "磁盘空间不足: 安装该模组约需 " + FormatSize(need) +
                            " 可用空间,当前仅剩 " + FormatSize(drive.AvailableFreeSpace) + ",请清理磁盘后重试";
                    return false;
                }
            }
            catch (System.Exception e)
            {
                // 预检失败不应该阻断下载,记录日志后放行
                Log.Error("磁盘空间预检失败: " + e.Message, Log.LogType.Download);
            }
            return true;
        }

        /// <summary>
        /// 把字节数格式化成易读的字符串(仅用于提示文案)
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <returns>如 "128.5 MB"</returns>
        static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
                return (bytes / (1024d * 1024 * 1024)).ToString("0.0") + " GB";
            if (bytes >= 1024L * 1024)
                return (bytes / (1024d * 1024)).ToString("0.0") + " MB";
            if (bytes >= 1024)
                return (bytes / 1024d).ToString("0.0") + " KB";
            return bytes + " B";
        }

        /// <summary>
        /// 判断 UnityWebRequest 的错误是否是 SSL/TLS 握手失败
        /// </summary>
        /// <param name="error">UnityWebRequest.error 的原始内容</param>
        /// <returns>是 SSL 相关错误返回true</returns>
        static bool IsSslError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return false;
            string lower = error.ToLowerInvariant();
            return lower.Contains("ssl") || lower.Contains("tls") || lower.Contains("certificate");
        }

        /// <summary>
        /// 把 UnityWebRequest 的错误翻译成玩家能看懂的中文提示
        /// </summary>
        /// <param name="unityError">UnityWebRequest.error 的原始内容</param>
        /// <returns>中文提示</returns>
        public static string GetFriendlyNetworkError(string unityError)
        {
            if (string.IsNullOrEmpty(unityError))
                return "网络请求失败,请检查网络后重试";

            string lower = unityError.ToLowerInvariant();

            // "Unable to complete SSL connection" 属于 curl 层的握手失败
            if (IsSslError(unityError))
                return "SSL/TLS 安全连接失败(常见原因:网络被代理/加速器拦截、系统时间不正确、服务器证书过期),请检查网络或关闭代理后重试";

            if (lower.Contains("cannot resolve") || lower.Contains("could not resolve"))
                return "域名解析失败,无法连接服务器,请检查网络或 DNS 设置";

            if (lower.Contains("timed out") || lower.Contains("timeout"))
                return "连接服务器超时,请检查网络后重试";

            if (lower.Contains("403") || lower.Contains("404"))
                return "服务器上找不到该文件(可能已被作者删除或地址失效)";

            return "网络请求失败: " + unityError;
        }

        /// <summary>
        /// 判断文件是否是 zip 压缩包(检查文件头 PK\x03\x04 / PK\x05\x06 / PK\x07\x08)
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>是 zip 返回true</returns>
        static bool IsZipFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < 4)
                        return false;

                    int b0 = fs.ReadByte();
                    int b1 = fs.ReadByte();
                    int b2 = fs.ReadByte();
                    int b3 = fs.ReadByte();
                    if (b0 != 'P' || b1 != 'K')
                        return false;

                    // 普通 zip / 空 zip / 分卷标记
                    return (b2 == 3 && b3 == 4) || (b2 == 5 && b3 == 6) || (b2 == 7 && b3 == 8);
                }
            }
            catch (System.Exception e)
            {
                Log.Error("校验压缩包失败: " + path + " " + e.Message, Log.LogType.Download);
                return false;
            }
        }

        /// <summary>
        /// 解压 zip 到目标目录(同步方法,设计为由后台线程调用)
        /// </summary>
        /// <param name="zipPath">zip 文件路径</param>
        /// <param name="targetFolder">解压目标目录</param>
        /// <param name="subFolder">只解压该子目录(为空则解压全部)</param>
        /// <param name="onProgress">进度回调,范围 0~1</param>
        static void ExtractZipToFolder(string zipPath, string targetFolder, string subFolder, System.Action<float> onProgress)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                int count = archive.Entries.Count;
                if (count <= 0)
                {
                    // 空仓库,直接成功
                    onProgress?.Invoke(1f);
                    return;
                }

                int cur = 0;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    cur++;
                    // GitHub 下载的 zip 顶层会带一个 "repo-branch/" 目录
                    // 如果指定了子目录则裁剪掉;否则保留整个仓库
                    string entryName = entry.FullName.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(subFolder))
                    {
                        if (!entryName.StartsWith(subFolder + "/", System.StringComparison.OrdinalIgnoreCase)
                            && !entryName.Equals(subFolder, System.StringComparison.OrdinalIgnoreCase))
                        {
                            onProgress?.Invoke(cur / (float)count);
                            continue;
                        }
                        entryName = entryName.Substring(subFolder.Length);
                        if (entryName.StartsWith("/"))
                            entryName = entryName.Substring(1);
                    }

                    if (string.IsNullOrEmpty(entryName))
                    {
                        onProgress?.Invoke(cur / (float)count);
                        continue;
                    }

                    string destPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(targetFolder, entryName));
                    // 防止 zip slip 漏洞
                    string fullTarget = System.IO.Path.GetFullPath(targetFolder);
                    if (!fullTarget.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                        fullTarget += System.IO.Path.DirectorySeparatorChar;
                    if (!destPath.StartsWith(fullTarget, System.StringComparison.OrdinalIgnoreCase))
                        throw new System.Exception("检测到非法路径(可能的 zip slip 攻击): " + entryName);

                    if (string.IsNullOrEmpty(System.IO.Path.GetFileName(destPath)))
                    {
                        // 目录条目
                        System.IO.Directory.CreateDirectory(destPath);
                    }
                    else
                    {
                        string parentDir = System.IO.Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(parentDir) && !System.IO.Directory.Exists(parentDir))
                            System.IO.Directory.CreateDirectory(parentDir);

                        // overwrite:Git 仓库内文件可能存在重复条目
                        entry.ExtractToFile(destPath, true);
                    }

                    onProgress?.Invoke(cur / (float)count);
                }
            }
        }

        /// <summary>
        /// 安全删除文件(文件不存在或删除失败都不会抛异常)
        /// </summary>
        /// <param name="path">文件路径</param>
        static void SafeDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (System.Exception e)
            {
                Log.Error("删除临时文件失败: " + path + " " + e.Message, Log.LogType.Download);
            }
        }

        /// <summary>
        /// 宽松的 HTTPS 证书校验器:始终校验通过。
        /// 只在 SSL 握手失败后的重试中使用(见 DownloadToFile 的 allowInvalidCertificate 参数)。
        /// 典型成因:玩家网络中的代理/杀毒软件/加速器对 TLS 做了中间拦截,
        /// 或系统证书库里缺少服务器使用的根证书,导致 Unity 内置 CA 库报
        /// "Unable to complete SSL connection"。此时若不放宽校验,玩家永远下不了模组。
        /// 该 handler 是请求级的,不会影响游戏内其它 HTTPS 请求。
        /// </summary>
        class RelaxedCertificateHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                return true;
            }
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
        /// <param name="expectedBytes">预计的 zip 大小(0 表示未知),用于磁盘预检与断点续传判定</param>
        /// <param name="isCancelled">取消检测回调,返回 true 时中断下载(已下载部分保留)</param>
        public static IEnumerator DownloadAndInstall(string gitUrl, string rootFolder, string expectModId,
            System.Action<float> onProgress, System.Action<GitDownloadResult> onComplete,
            long expectedBytes = 0, System.Func<bool> isCancelled = null)
        {
            GitDownloadResult result = new GitDownloadResult();
            result.ExtractTargetPath = rootFolder;

            if (string.IsNullOrEmpty(expectModId))
            {
                result.Error = GitDownloadError.InvalidUrl;
                result.ErrorMessage = "模组Id为空,无法安装";
                Log.Error(result.ErrorMessage, Log.LogType.Download);
                onComplete?.Invoke(result);
                yield break;
            }

            // 0.解析下载地址(后面下载与解压都要用,提前失败可以少建一堆目录)
            string zipUrl;
            string refType;
            string refName;
            string subFolder;
            if (!TryParseGitUrl(gitUrl, out zipUrl, out refType, out refName, out subFolder, out string parseErr))
            {
                result.Error = GitDownloadError.InvalidUrl;
                result.ErrorMessage = parseErr;
                Log.Error("模组下载地址解析失败: " + parseErr + " URL: " + gitUrl, Log.LogType.Download);
                onComplete?.Invoke(result);
                yield break;
            }

            // 模组根目录创建失败(无写权限/磁盘只读)必须直接返回失败:
            // 若异常冒出协程,onComplete 不会触发,下载状态会永久卡在"下载中"
            try
            {
                if (!Sango.Directory.Exists(rootFolder))
                    Sango.Directory.Create(rootFolder);
            }
            catch (System.Exception e)
            {
                result.Error = GitDownloadError.ExtractError;
                result.ErrorMessage = "无法创建模组目录(请检查游戏目录的写入权限): " + e.Message;
                Log.Error(result.ErrorMessage, Log.LogType.Download);
                onComplete?.Invoke(result);
                yield break;
            }

            // 磁盘空间预检:先算账再动手,避免下到一半/解压到一半才失败
            if (!EnsureDiskSpace(rootFolder, expectedBytes, out string spaceErr))
            {
                result.Error = GitDownloadError.ExtractError;
                result.ErrorMessage = spaceErr;
                Log.Error(spaceErr, Log.LogType.Download);
                onComplete?.Invoke(result);
                yield break;
            }

            // 1.下载 zip 到固定缓存目录
            //    关键:不再放在随机命名的临时目录里,否则每次重试/重新点击都会换目录,
            //    上次的 .part 找不到,断点续传就形同虚设
            string cacheDir = System.IO.Path.Combine(rootFolder, ".download");
            string zipPath = System.IO.Path.Combine(cacheDir, expectModId + ".zip");
            try
            {
                if (!System.IO.Directory.Exists(cacheDir))
                    System.IO.Directory.CreateDirectory(cacheDir);
            }
            catch (System.Exception e)
            {
                // 缓存目录建不了就退回到模组根目录,不影响主流程
                Log.Error("创建下载缓存目录失败,改用模组根目录: " + e.Message, Log.LogType.Download);
                zipPath = System.IO.Path.Combine(rootFolder, expectModId + ".zip");
            }

            bool downloadOk = false;
            bool cancelled = false;
            string downloadError = null;
            yield return DownloadWithRetry(zipUrl, zipPath, expectedBytes,
                (p) => onProgress?.Invoke(p * 0.7f),
                (ok, err, isCancel) => { downloadOk = ok; downloadError = err; cancelled = isCancel; },
                isCancelled);

            if (cancelled)
            {
                // 已下载部分保留在 .part,旧版本完全不受影响
                result.Error = GitDownloadError.Cancelled;
                result.ErrorMessage = "下载已取消";
                Log.Info("模组下载已取消: " + expectModId, Log.LogType.Download);
                onComplete?.Invoke(result);
                yield break;
            }

            if (!downloadOk)
            {
                Log.Error("模组下载失败: " + expectModId + " " + downloadError + " URL: " + zipUrl, Log.LogType.Download);
                result.Error = GitDownloadError.NetworkError;
                result.ErrorMessage = GetFriendlyNetworkError(downloadError);
                SafeDeleteFile(zipPath);
                onComplete?.Invoke(result);
                yield break;
            }

            // 校验下载内容确实是 zip(被拦截时可能拿到 HTML 错误页)
            if (!IsZipFile(zipPath))
            {
                Log.Error("下载到的内容不是有效的压缩包: " + zipPath + " URL: " + zipUrl, Log.LogType.Download);
                result.Error = GitDownloadError.NetworkError;
                result.ErrorMessage = "下载到的内容不是有效的压缩包(可能被网络拦截),请稍后重试";
                SafeDeleteFile(zipPath);
                SafeDeleteFile(zipPath + ".part");
                onComplete?.Invoke(result);
                yield break;
            }

            Log.Info("模组包下载完成: " + zipPath + " 大小: " + new FileInfo(zipPath).Length, Log.LogType.Download);

            // 2.解压到临时目录
            //    临时解压目录:与正式目录同盘,后面才能用 Move 完成"整体替换"
            string stamp = System.DateTime.Now.Ticks.ToString();
            string tempFolder = System.IO.Path.Combine(rootFolder, $".tmp_{expectModId}_{stamp}");

            bool extractOk = false;
            string extractError = null;
            yield return ExtractAsync(zipPath, tempFolder, subFolder, 0.7f, 0.3f,
                (ok, err) => { extractOk = ok; extractError = err; }, onProgress);

            // 解压完就删掉 zip:成功失败都删,避免长期占用空间
            SafeDeleteFile(zipPath);

            if (!extractOk)
            {
                // 解压失败:丢掉临时目录,旧版本原样保留
                SafeDeleteDirectory(tempFolder);
                result.Error = GitDownloadError.ExtractError;
                result.ErrorMessage = extractError;
                onComplete?.Invoke(result);
                yield break;
            }

            // 3.定位模组目录并校验 mod.info
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

            // 枚举子目录可能因为权限/路径过长抛异常,
            // 这里必须兜住:一旦异常冒出协程,后面的 onComplete 就永远不触发,下载状态会永久卡住
            string[] subDirs;
            try
            {
                subDirs = System.IO.Directory.GetDirectories(dir);
            }
            catch (System.Exception e)
            {
                Log.Error("枚举模组目录失败: " + dir + " " + e.Message, Log.LogType.Download);
                return;
            }

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

            // 3. 使用 UnityWebRequest 请求文本(市场列表之类的小文件)
            UnityWebRequest request = UnityWebRequest.Get(gitUrl);
            // 必须给超时:否则打开模组界面时如果网络不通,会一直停在"加载中 0%",
            // 玩家看到的就是"没有任何响应"
            request.timeout = 20;
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
                // 用 Error 保证正式包也能留下痕迹(Warning 会被 [Conditional] 编译剔除)
                Log.Error(netErr, Log.LogType.Download);
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
