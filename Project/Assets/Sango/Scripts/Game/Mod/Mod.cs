//using HybridCLR;

using Sango.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Sango.Mod
{
    public class Mod
    {
        /// <summary>
        /// 唯一标识符,只能用英文
        /// </summary>
        public string Id { internal set; get; }
        /// <summary>
        /// Mod名字,可以中文
        /// </summary>
        public string Name { internal set; get; }
        /// <summary>
        /// Mod标签
        /// </summary>
        public string Tag { internal set; get; }
        /// <summary>
        /// 海报图片相对路径
        /// </summary>
        public string Version { internal set; get; }
        /// <summary>
        /// 说明文字
        /// </summary>
        public string Description { internal set; get; }
        /// <summary>
        /// 依赖模组,以分号隔开 mod1;mod2
        /// </summary>
        public string Depends { internal set; get; }
        /// <summary>
        /// 海报图片相对路径
        /// </summary>
        public string Poster { internal set; get; }
        /// <summary>
        /// 作者收款/赞赏二维码图片地址(绝对 URL),为空表示作者没有放二维码。
        /// 取值优先级:包内 mod.info 的 donate > 创意工坊市场清单下发的 donate;
        /// 注意 mod.info 按 '=' 切分取值,地址里不能带 '=' 参数。
        /// </summary>
        public string Donate { internal set; get; }
        /// <summary>
        /// Mod路径
        /// </summary>
        public string ModDir { internal set; get; }
        /// <summary>
        /// 作者
        /// </summary>
        public string Author { internal set; get; }
        /// <summary>
        /// Assembly名字
        /// </summary>
        public string EntryAssembly { internal set; get; }
        /// <summary>
        /// 源url(主下载地址,等于 UrlList 的第一个)
        /// </summary>
        public string Url { internal set; get; }

        /// <summary>
        /// 候选下载地址列表(按优先级排列)。
        /// 第一个地址下载失败时自动换下一个,全部失败才提示玩家;
        /// 为空时只用 <see cref="Url"/> 单地址。
        /// </summary>
        public List<string> UrlList = new List<string>();

        public string MarketName;

        public long Size;

        /// <summary>
        /// 源version
        /// </summary>
        public string UrlVersion { internal set; get; }
        public float loadProgress = 0;
        public bool isDownloading = false;

        /// <summary>
        /// 最近一次下载失败的原因(已翻译成中文,供界面提示玩家);开始新的下载时清空
        /// </summary>
        public string LastDownloadError { internal set; get; }

        /// <summary>
        /// 本次下载开始的时间(Time.realtimeSinceStartup)
        /// 用于 isDownloading 的兜底复位:协程若因未捕获异常被中断,这个标记会永远停在 true,
        /// 表现就是玩家再点"下载"毫无反应(OnClickDownload 首行会直接 return)
        /// </summary>
        float downloadStartTime = 0;

        /// <summary>
        /// 玩家是否请求取消本次下载
        /// 下载协程会在每帧检查该标记,取消后已下载的部分保留在 .part 文件里,下次可断点续传
        /// </summary>
        public bool isCancelRequested = false;

        public string ModDirName { internal set; get; }

        public Assembly Assembly { internal set; get; }

        public void LoadData()
        {
            string path = GetFullPath("Data");
            Directory.EnumFiles(path, "*.json", SearchOption.AllDirectories, (file) =>
            {
                Sango.Log.Info($"LoadData: {file}");
            });
            //Directory.EnumFiles(path, "*.txt", SearchOption.AllDirectories, (file) =>
            //{
            //    Sango.Log.Info($"LoadData: {file}");
            //    Game.GameData.Load(file);
            //});

            //Game.GameData.LoadBin(path);

            // 加载技能配置
            Sango.Core.SkillConfigManager.Instance.LoadModSkillConfigs(this);
        }

        public void LoadAssembly()
        {
//            string path = GetFullPath("Metadata");
//            HomologousImageMode mode = HomologousImageMode.SuperSet;
//            Directory.EnumFiles(path, "*.dll", SearchOption.AllDirectories, (file) =>
//            {
//#if SANGO_DEBUG
//                Sango.Log.Info($"LoadMetadataAssembly: {file}");
//#endif
//                byte[] dllBytes = File.ReadAllBytes(file);
//                // 加载assembly对应的dll，会自动为它hook。一旦aot泛型函数的native函数不存在，用解释器版本代码
//                LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, mode);
//#if SANGO_DEBUG
//                Sango.Log.Info($"LoadMetadataForAOTAssembly:{file}. mode:{mode} ret:{err}");
//#endif
//            });

//            path = GetFullPath($"{EntryAssembly}.dll");
//            if (File.Exists(path))
//            {
//#if !UNITY_EDITOR
//                Assembly = Assembly.Load(File.ReadAllBytes(path));
//#else
//                Assembly = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == EntryAssembly);
//#endif
//                // 执行入口函数
//                Type entryType = Assembly.GetType("Entry");
//                entryType.GetMethod("Start").Invoke(null, null);
//            }
        }

        public void LoadUI()
        {
            string path = GetFullPath("UI");
            Directory.EnumFiles(path, "*.bytes", SearchOption.TopDirectoryOnly, (file) =>
            {
                Sango.Log.Info($"LoadUI: {file}");
                string packageName = System.IO.Path.GetFileNameWithoutExtension(file).Split('_')[0];
                Window.Instance.AddPackage(file, packageName);
            });
        }
        /// <summary>
        /// 登记本模组 Package 目录下的资源包。
        /// 注意：这里只做登记，不读取文件；真正的 AssetBundle 加载推迟到
        /// 第一次使用该包内资源时（详见 PackageManager 的懒加载说明）。
        /// </summary>
        public void LoadPackage()
        {
            string path = GetFullPath($"Package/{PlatformUtility.GetPlatformName()}");
            Directory.EnumFiles(path, "*.pkg", SearchOption.TopDirectoryOnly, (file) =>
            {
                Sango.Log.Info($"登记资源包: {file}");
                string packageName = System.IO.Path.GetFileNameWithoutExtension(file).Split('_')[0];
                PackageManager.Instance.AddPackage(packageName, file, true);
            });
        }

        public void LoadLanguage()
        {
            string path = GetFullPath("Language");
            Directory.EnumFiles(path, "*.json", SearchOption.TopDirectoryOnly, (file) =>
            {
                Sango.Log.Info($"Find Language File: {file}");
                GameLanguage.AddFile(file);
            });
        }
        public void LoadScenario()
        {
            string path = GetFullPath("Scenario");
            int count = ShortScenario.all_scenario_info_list.Count;
            Directory.EnumFiles(path, "*.json", SearchOption.TopDirectoryOnly, (file) =>
            {
                Sango.Log.Info($"Find Scenario: {file}");
                ShortScenario shortScenario = ShortScenario.Add(file);
                shortScenario.Info.id = count + shortScenario.Info.id;
                shortScenario.ModName = Name;
               
                Scenario scenario = Scenario.Add(file);
                scenario.Info.id = count + scenario.Info.id;
            });
        }
        public string GetFullPath(string path)
        {
            return $"{ModDir}/{path}";
        }

        /// <summary>
        /// 模组私有资源目录名
        /// 该目录内的结构与模组根目录完全一致(Data/Language/Scenario/Assets...),
        /// 但它<B>不参与</B>全局搜索,也不会被自动加载或参与模组间覆盖:
        /// 只有显式引用时才会被访问,例如 <c>mod://私有命名空间/Assets/ItemIcon/1.png</c>
        /// 或短写 <c>私有命名空间:Assets/ItemIcon/1</c>(详见 <see cref="ModAssetPath"/>)。
        /// 适合存放本模组必须固定生效、不希望被其他模组改写的配置与资源。
        /// </summary>
        public const string PRIVATE_DIR_NAME = "Private";

        /// <summary>
        /// 私有资源命名空间声明文件(放在 Private 目录下)。
        ///
        /// 【为什么需要它】上传创意工坊时,服务端会把包内 mod.info 的 id 换成自己分配的
        /// 8 位短 id(见 ModHub 的 zipRepacker)。若私有资源引用写死 <c>mod://模组Id/...</c>,
        /// 作者本地调试正常、玩家装到机器上必然解析失败 —— 因为 id 变了。
        ///
        /// 约定:作者在 <c>Private/mod.namespace</c> 里写一行自定义标识(例如 <c>hantu</c>),
        /// 引用统一写 <c>mod://hantu/...</c>。该文件随包上传、内容永不被服务端改写,
        /// 因此本地与线上完全一致;没有该文件时按 mod.info 的 privateid、再按真实 id 兜底,
        /// 保证改造前发布的老模组仍能正常访问自己的私有目录。
        /// </summary>
        public const string PRIVATE_NAMESPACE_FILE = "mod.namespace";

        /// <summary>
        /// mod.info 里的 privateid(创意工坊在重写 mod.info 时写入的"作者本地 id")。
        /// 老包没有该字段时为空;它只是命名空间的第二来源,不参与其他任何判断。
        /// </summary>
        public string PrivateId { internal set; get; }

        /// <summary>命名空间读取缓存(null = 尚未读取,"" = 确认没有)</summary>
        string privateNamespaceCache;
        bool privateNamespaceLoaded;

        /// <summary>
        /// 私有资源命名空间:私有资源引用的稳定前缀。
        /// 取值顺序:<c>Private/mod.namespace</c> -> mod.info 的 privateid -> 空串。
        /// 只允许字母/数字/下划线/短横线(见 <see cref="ModAssetPath.IsValidPrivateKey"/>),
        /// 非法内容打印中文告警并视为未设置,避免脏标识污染解析表。
        /// </summary>
        public string PrivateNamespace
        {
            get
            {
                if (privateNamespaceLoaded)
                    return privateNamespaceCache;

                privateNamespaceLoaded = true;
                privateNamespaceCache = "";

                // 1.优先读 Private/mod.namespace(作者显式声明,最稳定)
                string file = GetPrivateFullPath(PRIVATE_NAMESPACE_FILE);
                if (file != null && System.IO.File.Exists(file))
                {
                    try
                    {
                        string content = System.IO.File.ReadAllText(file).Trim();
                        // 只取第一行:作者常在后面追加说明文字,不能让它把整个命名空间判成非法
                        int lineEnd = content.IndexOfAny(new char[] { '\r', '\n' });
                        if (lineEnd >= 0)
                            content = content.Substring(0, lineEnd).Trim();

                        if (ModAssetPath.IsValidPrivateKey(content))
                            privateNamespaceCache = content;
                        else if (content.Length > 0)
                            Sango.Log.Warning($"模组私有命名空间不合法(只允许字母/数字/下划线/短横线),已忽略: {Id} -> {content}");
                    }
                    catch (System.Exception e)
                    {
                        Sango.Log.Warning($"读取模组私有命名空间失败: {Id} {e.Message}");
                    }
                }

                // 2.没有声明文件时回落到 mod.info 的 privateid(创意工坊重写 mod.info 时写入的作者本地 id)
                if (privateNamespaceCache.Length == 0 && ModAssetPath.IsValidPrivateKey(PrivateId))
                    privateNamespaceCache = PrivateId;

                return privateNamespaceCache;
            }
        }

        /// <summary>
        /// 清空私有命名空间的读取缓存。
        /// 模组目录被替换/重新下载/重新读取 mod.info 后必须调用,
        /// 否则会继续用旧目录下读到的命名空间,表现为"更新了模组但私有资源还是旧的/找不到"。
        /// </summary>
        internal void ResetPrivateCache()
        {
            privateNamespaceLoaded = false;
            privateNamespaceCache = null;
        }

        /// <summary>
        /// 本模组私有资源的全部有效标识(去重、忽略空值)。
        /// 顺序:命名空间 -> privateid -> 真实 id,三者指向同一份 Private 目录。
        ///
        /// 登记与注销都必须遍历这个列表:
        /// 只认命名空间会让"没写声明文件的老包"失效,
        /// 只认真实 id 会让"上传后被重新分配 id"的包失效。
        /// </summary>
        /// <returns>私有资源标识列表(至少包含真实 id)</returns>
        public List<string> GetPrivateKeys()
        {
            List<string> keys = new List<string>();
            AddPrivateKey(keys, PrivateNamespace);
            AddPrivateKey(keys, PrivateId);
            AddPrivateKey(keys, Id);
            return keys;
        }

        /// <summary>
        /// 追加一个私有资源标识(去重,忽略空值)
        /// </summary>
        /// <param name="keys">标识列表</param>
        /// <param name="key">待追加的标识</param>
        static void AddPrivateKey(List<string> keys, string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            for (int i = 0; i < keys.Count; i++)
            {
                if (string.Equals(keys[i], key, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            keys.Add(key);
        }

        /// <summary>
        /// 私有资源目录的完整路径: {ModDir}/Private
        /// </summary>
        public string PrivateDir
        {
            get { return string.IsNullOrEmpty(ModDir) ? null : $"{ModDir}/{PRIVATE_DIR_NAME}"; }
        }

        /// <summary>
        /// 是否拥有私有资源目录(没有该目录的模组不会登记私有资源根目录)
        /// </summary>
        public bool HasPrivateDir
        {
            get { return !string.IsNullOrEmpty(ModDir) && Sango.Directory.Exists(PrivateDir); }
        }

        /// <summary>
        /// 获取私有资源目录下某个相对路径的完整路径(不判断文件是否存在)
        /// </summary>
        /// <param name="path">相对路径,例如 Data/Common/AIConfig.json</param>
        /// <returns>私有目录下的完整路径</returns>
        public string GetPrivateFullPath(string path)
        {
            string privateDir = PrivateDir;
            if (string.IsNullOrEmpty(privateDir))
                return null;
            return $"{privateDir}/{path}";
        }

        /// <summary>
        /// 按私有资源协议解析本模组私有目录下的文件(文件不存在时返回 null 并打印中文日志)
        /// 等价于 <c>mod://{私有命名空间}/{path}</c>,供模组自己的代码显式加载私有配置/资源。
        /// 未声明命名空间时回落到真实 id,因此改造前的老模组行为完全不变。
        /// </summary>
        /// <param name="path">相对私有目录的路径,例如 Data/Common/AIConfig.json</param>
        /// <returns>私有目录下的完整文件路径;不存在返回 null</returns>
        public string ResolvePrivateFile(string path)
        {
            if (string.IsNullOrEmpty(Id) || string.IsNullOrEmpty(path))
                return null;

            // 命名空间不会随创意工坊重新分配 id 而变化,优先使用;没有声明时回落真实 id
            string key = PrivateNamespace;
            if (string.IsNullOrEmpty(key))
                key = Id;
            return ModAssetPath.Resolve($"{ModAssetPath.Scheme}{key}/{path}");
        }

        public bool HasNewVersion()
        {
            return !string.IsNullOrEmpty(UrlVersion) && !string.IsNullOrEmpty(Version) && UrlVersion != Version;
        }

        public bool CanUpgrage()
        {
            return HasNewVersion() || !IsValidMod();
        }

        /// <summary>
        /// 是否正在下载
        /// 附带超时兜底:下载协程一旦被未捕获的异常打断,isDownloading 会永久为 true,
        /// 而界面的下载按钮首行就是"正在下载则直接 return",玩家会认为"点了没反应"。
        /// 这里用开始时间做保护,超过 10 分钟视为失效,允许玩家重新点击。
        /// </summary>
        public bool IsDownloading()
        {
            if (isDownloading && downloadStartTime > 0
                && UnityEngine.Time.realtimeSinceStartup - downloadStartTime > 600f)
            {
                Sango.Log.Error($"模组下载状态超时未复位,已强制重置: {Id}");
                isDownloading = false;
                loadProgress = 0;
                downloadStartTime = 0;
            }
            return isDownloading;
        }

        public bool IsValidMod()
        {
            return !string.IsNullOrEmpty(ModDirName);
        }

        /// <summary>
        /// 取消本模组的下载
        /// 下载协程会在下一帧中断请求,已下载的部分保留在 .part 文件里,
        /// 玩家下次点下载会从断点继续,不用从头再下一遍
        /// </summary>
        public void CancelDownload()
        {
            if (!isDownloading)
                return;

            isCancelRequested = true;
            Sango.Log.Info($"玩家取消了模组下载: {Id}");
        }

        /// <summary>
        /// 下载本模组
        /// 若本模组存在前置依赖(可能还有多层的间接依赖),会先把"未安装"的依赖依次下载完成,
        /// 再下载本模组,保证下载完成后依赖链是完整的
        /// </summary>
        public void Download()
        {
            if (IsDownloading())
                return;

            // 收集"底层依赖在最前,自身在最后"的下载队列(递归依赖,已去重)
            List<Mod> downloadList = ModManager.Instance.CollectDepends(this);
            App.Instance.StartCoroutine(DownloadCoroutine(downloadList));
        }

        /// <summary>
        /// 依次下载队列中的模组(协程)
        /// 队列顺序已由 CollectDepends 保证为"依赖在前,本体在后",因此顺序下载即可满足依赖关系
        /// </summary>
        /// <param name="downloadList">待下载的模组列表(依赖在前,本体在后)</param>
        private System.Collections.IEnumerator DownloadCoroutine(List<Mod> downloadList)
        {
            for (int i = 0; i < downloadList.Count; i++)
            {
                Mod target = downloadList[i];

                // 除本体之外的依赖:已安装的直接跳过,只补下"缺失"的依赖,
                // 避免下载一个模组时顺手把依赖升级了(那应该由玩家自己决定)
                if (target != this && target.IsValidMod())
                {
                    Sango.Log.Info($"前置依赖已安装,跳过下载: {target.Id}");
                    continue;
                }

                // 已有别的下载任务在跑时跳过,避免同一个模组被重复下载
                if (target.IsDownloading())
                {
                    Sango.Log.Info($"模组正在下载中,跳过: {target.Id}");
                    continue;
                }

                if (string.IsNullOrEmpty(target.Url))
                {
                    // 没有下载地址时必须中断并提示玩家:
                    // 旧实现是 continue 静默跳过,表现为"点了下载什么都没发生"(依赖残缺时还会装出坏包)
                    string noUrlMsg = "该模组没有下载地址(作者未上传或市场数据异常)";
                    Sango.Log.Error($"模组没有下载地址,无法下载: {target.Id}");
                    target.LastDownloadError = noUrlMsg;
                    GameEvent.OnModDownloadFailed?.Invoke(target, noUrlMsg);
                    yield break;
                }

                Sango.Log.Info($"开始下载模组: {target.Id}");
                bool success = false;
                yield return DownloadOne(target, (ok) => success = ok);

                if (!success)
                {
                    // 玩家主动取消:不再继续后面对依赖,也不算失败(不弹失败提示)
                    if (target.isCancelRequested)
                    {
                        Sango.Log.Info($"下载已被玩家取消,中断后续下载: {target.Id}");
                        yield break;
                    }

                    // 依赖下载失败时不再继续,避免下载出一个依赖残缺的模组
                    Sango.Log.Error($"模组下载失败,已中断后续下载: {target.Id}");
                    yield break;
                }
            }
        }

        /// <summary>
        /// 下载单个模组并等待其完成(协程)
        ///
        /// 按 <see cref="UrlList"/> 顺序尝试:第一个地址(通常是 GitCode 镜像)失败后
        /// 自动换下一个,全部失败才向玩家抛提示;玩家主动取消则不再尝试后续地址。
        /// 因此市场清单里配置两个下载地址时,任何一个源不可用都不会让玩家卡住。
        /// </summary>
        /// <param name="target">要下载的模组</param>
        /// <param name="onDone">完成回调,参数表示是否成功</param>
        private System.Collections.IEnumerator DownloadOne(Mod target, System.Action<bool> onDone)
        {
            target.isDownloading = true;
            target.isCancelRequested = false;
            target.loadProgress = 0;
            target.LastDownloadError = null;
            target.downloadStartTime = UnityEngine.Time.realtimeSinceStartup;

            // 兜底:老数据没有 UrlList 时只用主地址
            List<string> urls = target.UrlList;
            if (urls == null || urls.Count == 0)
                urls = new List<string> { target.Url };

            bool success = false;
            bool cancelled = false;

            for (int i = 0; i < urls.Count; i++)
            {
                string url = urls[i];
                if (string.IsNullOrEmpty(url))
                    continue;

                // 换源前清掉上一个源留下的 .zip / .part:
                // 两个源的字节不保证完全一致,续传会把两份数据拼成坏包
                if (i > 0)
                    ClearDownloadCache(target.Id);

                GitDownloader.GitDownloadResult result = null;
                target.loadProgress = 0;
                // 用"整体替换"的方式安装:先解压到临时目录,校验通过后才替换旧版本,
                // 避免直接覆盖解压导致旧版本删掉的文件残留。
                // target.Size 用于磁盘预检与断点续传;isCancelRequested 供玩家随时取消
                yield return GitDownloader.DownloadAndInstall(url, ModManager.MOD_ROOT_DIR, target.Id,
                    (progress) =>
                    {
                        target.loadProgress = progress;
                    },
                    (content) =>
                    {
                        result = content;
                    },
                    target.Size,
                    () => target.isCancelRequested
                );

                success = result != null && result.IsSuccess;
                cancelled = result != null && result.Error == GitDownloader.GitDownloadError.Cancelled;

                if (success)
                {
                    // 下载完成后重新读取 mod.info,刷新名字/版本/依赖等信息
                    ModManager.Instance.UpdateMod($"{result.ExtractTargetPath}/{target.Id}", target);
                    break;
                }

                if (cancelled)
                {
                    // 玩家主动取消:不是故障,不弹失败提示,进度归零等待下次续传
                    target.loadProgress = 0;
                    Sango.Log.Info($"模组下载已取消: {target.Id}(已下载部分已保留,下次继续)");
                    break;
                }

                // 失败但还有备用地址:静默换源重试,不打扰玩家
                if (i < urls.Count - 1)
                {
                    Sango.Log.Warning($"模组下载失败,尝试备用地址: {target.Id} {url}");
                    continue;
                }

                // 所有地址都失败:用最后一个错误提示玩家
                target.LastDownloadError = result != null ? result.ErrorMessage : "无返回结果";
                Sango.Log.Error($"模组下载失败(已尝试 {urls.Count} 个地址): {target.Id} {target.LastDownloadError}");
                GameEvent.OnModDownloadFailed?.Invoke(target, target.LastDownloadError);
            }

            // 所有地址都是空串等极端情况:同样要给玩家一个明确原因,不能静默结束
            if (!success && !cancelled && string.IsNullOrEmpty(target.LastDownloadError))
            {
                target.LastDownloadError = "该模组没有可用的下载地址(市场数据异常)";
                Sango.Log.Error($"模组没有可用下载地址: {target.Id}");
                GameEvent.OnModDownloadFailed?.Invoke(target, target.LastDownloadError);
            }

            if (success)
                target.loadProgress = 1;
            target.isDownloading = false;
            target.downloadStartTime = 0;

            onDone?.Invoke(success);
        }

        /// <summary>
        /// 清掉某个模组的下载缓存(正式 zip 与断点包)。
        /// 换下载源重试前必须调用,否则会拿另一个源的断点继续续传,拼出损坏的压缩包。
        /// </summary>
        /// <param name="modId">模组Id</param>
        private static void ClearDownloadCache(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return;
            try
            {
                string cacheDir = System.IO.Path.Combine(ModManager.MOD_ROOT_DIR, ".download");
                string zipPath = System.IO.Path.Combine(cacheDir, modId + ".zip");
                if (System.IO.File.Exists(zipPath))
                    System.IO.File.Delete(zipPath);
                if (System.IO.File.Exists(zipPath + ".part"))
                    System.IO.File.Delete(zipPath + ".part");
            }
            catch (System.Exception e)
            {
                // 删不掉不影响重试:GitDownloader 会自行判断残包大小是否可用于续传
                Sango.Log.Error($"清理下载缓存失败: {modId} {e.Message}");
            }
        }
    }
}
