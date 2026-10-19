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
        /// 源url
        /// </summary>
        public string Url { internal set; get; }

        public string MarketName;

        public long Size;

        /// <summary>
        /// 源version
        /// </summary>
        public string UrlVersion { internal set; get; }
        public float loadProgress = 0;
        public bool isDownloading = false;

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
        public void LoadPackage()
        {
            string path = GetFullPath($"Package/{PlatformUtility.GetPlatformName()}");
            Directory.EnumFiles(path, "*.pkg", SearchOption.TopDirectoryOnly, (file) =>
            {
                Sango.Log.Info($"LoadPackage: {file}");
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

        public bool HasNewVersion()
        {
            return !string.IsNullOrEmpty(UrlVersion) && !string.IsNullOrEmpty(Version) && UrlVersion != Version;
        }

        public bool CanUpgrage()
        {
            return HasNewVersion() || !IsValidMod();
        }

        public bool IsDownloading()
        {
            return isDownloading;
        }

        public bool IsValidMod()
        {
            return !string.IsNullOrEmpty(ModDirName);
        }

        /// <summary>
        /// 下载本模组
        /// 若本模组存在前置依赖(可能还有多层的间接依赖),会先把"未安装"的依赖依次下载完成,
        /// 再下载本模组,保证下载完成后依赖链是完整的
        /// </summary>
        public void Download()
        {
            if (isDownloading)
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
                if (target.isDownloading)
                {
                    Sango.Log.Info($"模组正在下载中,跳过: {target.Id}");
                    continue;
                }

                if (string.IsNullOrEmpty(target.Url))
                {
                    Sango.Log.Warning($"模组没有下载地址,无法下载: {target.Id}");
                    continue;
                }

                Sango.Log.Info($"开始下载模组: {target.Id}");
                bool success = false;
                yield return DownloadOne(target, (ok) => success = ok);

                if (!success)
                {
                    // 依赖下载失败时不再继续,避免下载出一个依赖残缺的模组
                    Sango.Log.Error($"模组下载失败,已中断后续下载: {target.Id}");
                    yield break;
                }
            }
        }

        /// <summary>
        /// 下载单个模组并等待其完成(协程)
        /// </summary>
        /// <param name="target">要下载的模组</param>
        /// <param name="onDone">完成回调,参数表示是否成功</param>
        private System.Collections.IEnumerator DownloadOne(Mod target, System.Action<bool> onDone)
        {
            target.isDownloading = true;
            target.loadProgress = 0;

            GitDownloader.GitDownloadResult result = null;
            // 用"整体替换"的方式安装:先解压到临时目录,校验通过后才替换旧版本,
            // 避免直接覆盖解压导致旧版本删掉的文件残留
            yield return GitDownloader.DownloadAndInstall(target.Url, ModManager.MOD_ROOT_DIR, target.Id,
                (progress) =>
                {
                    target.loadProgress = progress;
                },
                (content) =>
                {
                    result = content;
                }
            );

            target.loadProgress = 1;
            target.isDownloading = false;

            bool success = result != null && result.IsSuccess;
            if (success)
            {
                // 下载完成后重新读取 mod.info,刷新名字/版本/依赖等信息
                ModManager.Instance.UpdateMod($"{result.ExtractTargetPath}/{target.Id}", target);
            }
            else
            {
                Sango.Log.Error($"模组下载失败: {target.Id} {(result != null ? result.ErrorMessage : "无返回结果")}");
            }

            onDone?.Invoke(success);
        }
    }
}
