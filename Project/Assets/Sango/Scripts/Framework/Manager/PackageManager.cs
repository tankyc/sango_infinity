
using System;
using System.Collections.Generic;

namespace Sango
{
    /// <summary>
    /// 资源包管理器：模组 Package 目录的统一入口
    ///
    /// 懒加载约定（本类改造的核心约束）：
    /// 1. <see cref="AddPackage"/> 只做登记，不做任何文件读取，启动阶段零 IO；
    /// 2. 第一次 <see cref="LoadAssets"/> 命中某个包时才真正 LoadFromFile，
    ///    命中过的包常驻内存，与改造前"加载后一直持有"的引用语义保持一致；
    /// 3. 未命中的包在本次查找里同样会被加载（否则无法判断包内是否有该资源），
    ///    因此同一次查找结束后用"资源名 -> 包索引"缓存把结果固化，
    ///    后续同名查询不再碰文件；确认查不到的资源名进入负缓存，避免反复遍历。
    ///
    /// 这样做的收益：模组很多时，启动不再把每个 .pkg 全量加载进内存，
    /// 实际用不到的包（例如只玩某个剧本时其余模组的资源包）永远不会被打开。
    /// </summary>
    public class PackageManager : Singleton<PackageManager>
    {
        /// <summary>
        /// 单个资源包名下的登记信息（同一个 packageName 可以由多个模组提供，
        /// 前面的包优先级更高，与改造前 Insert(0) 的覆盖语义一致）
        /// </summary>
        class PackageInfo
        {
            /// <summary>资源包名（= .pkg 文件名去掉下划线后缀）</summary>
            public string name { get; set; }

            /// <summary>最近一次登记的原始路径（仅用于排查，不参与加载）</summary>
            public string path { get; set; }

            /// <summary>包文件真实路径，索引越小优先级越高；懒加载按此顺序查找</summary>
            public List<string> paths = new List<string>();

            /// <summary>包路径 -> AssetBundle 索引（已加载并持有引用，保证每个包只加载一次）</summary>
            public Dictionary<string, int> loadedPathIndex = new Dictionary<string, int>();

            /// <summary>加载失败的包路径：避免每次查找都重复尝试一个坏包</summary>
            public HashSet<string> brokenPaths = new HashSet<string>();

            /// <summary>资源名 -> 命中的包索引（正向缓存，命中后不再遍历）</summary>
            public Dictionary<string, int> assetIndex = new Dictionary<string, int>();

            /// <summary>已确认所有包都不含该资源名（负缓存，避免反复遍历）</summary>
            public HashSet<string> missingAssets = new HashSet<string>();

            /// <summary>
            /// 包优先级变化后清空查找缓存：
            /// 已加载的包继续持有引用（AB 不失效），只是让后续查找重新走一遍顺序判定
            /// </summary>
            public void InvalidateLookup()
            {
                assetIndex.Clear();
                missingAssets.Clear();
            }
        }

        /// <summary>所有已登记的资源包（key = 包名）</summary>
        Dictionary<string, PackageInfo> packages = new Dictionary<string, PackageInfo>();

        /// <summary>
        /// 登记一个资源包（只登记路径，不加载）
        /// </summary>
        /// <param name="packageName">包名（= .pkg 文件名去掉下划线后缀）</param>
        /// <param name="packagePath">包文件路径</param>
        /// <returns>是否登记成功</returns>
        public bool AddPackage(string packageName, string packagePath)
        {
            return AddPackage(packageName, packagePath, true);
        }

        /// <summary>
        /// 登记一个资源包（只登记路径，不加载）
        /// </summary>
        /// <param name="packageName">包名</param>
        /// <param name="packagePath">包文件路径</param>
        /// <param name="front">为 true 时插到队列最前（优先级最高），与旧实现一致</param>
        /// <returns>是否登记成功</returns>
        public bool AddPackage(string packageName, string packagePath, bool front)
        {
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packagePath))
                return false;

            PackageInfo packageInfo;
            if (!packages.TryGetValue(packageName, out packageInfo))
            {
                packageInfo = new PackageInfo()
                {
                    name = packageName,
                };
                packages.Add(packageName, packageInfo);
            }

            packageInfo.path = packagePath;

            string finalePath = Path.FindFile(packagePath);
            if (finalePath == null)
                return false;

#if UNITY_EDITOR
            // 编辑器下资源由 AssetDatabase 直接读取（见 LoadAssets 的编辑器分支），
            // 这里不登记运行时包路径，避免编辑器里误加载到旧的 .pkg
            return true;
#else
            // 同一个包被重复登记（模组重复加载）时只保留一份
            if (packageInfo.paths.Contains(finalePath))
                return true;

            if (front)
                packageInfo.paths.Insert(0, finalePath);
            else
                packageInfo.paths.Add(finalePath);

            // 新包插到队首会改变优先级，之前的查找结果不再可信
            packageInfo.InvalidateLookup();
            return true;
#endif
        }

        /// <summary>
        /// 加载资源包内的资源（懒加载：首次访问到某个包时才真正加载它）
        /// </summary>
        /// <param name="packageName">包名</param>
        /// <param name="assetName">资源名（打包时的 addressableName）</param>
        /// <param name="assetType">资源类型，可为 null</param>
        /// <returns>资源对象，找不到返回 null</returns>
        public UnityEngine.Object LoadAssets(string packageName, string assetName, Type assetType)
        {
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(assetName))
                return null;

#if UNITY_EDITOR
            string assetsPath = $"Assets/Mods/{packageName}/{assetName}";
            UnityEngine.Object editorObj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetsPath, assetType);
            if (editorObj != null) return editorObj;

            if (assetType == typeof(UnityEngine.Texture) && !System.IO.Path.HasExtension(assetName))
            {
                assetsPath = $"Assets/Mods/{packageName}/{assetName}.png";
                editorObj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetsPath, assetType);
                if (editorObj != null) return editorObj;

                // 道具图标（Assets/ItemIcon）已统一为 .jpg，这里必须一并探测：
                // 少了这一行，编辑器下 jpg 资源只能靠运行时 TextureLoader 兜底，打包路径会取不到图
                assetsPath = $"Assets/Mods/{packageName}/{assetName}.jpg";
                editorObj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetsPath, assetType);
                if (editorObj != null) return editorObj;

                assetsPath = $"Assets/Mods/{packageName}/{assetName}.jpeg";
                editorObj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetsPath, assetType);
                if (editorObj != null) return editorObj;

                assetsPath = $"Assets/Mods/{packageName}/{assetName}.tga";
                editorObj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetsPath, assetType);
                if (editorObj != null) return editorObj;

                assetsPath = $"Assets/Mods/{packageName}/{assetName}.bmp";
                editorObj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetsPath, assetType);
                if (editorObj != null) return editorObj;
            }
#endif
            PackageInfo packageInfo;
            if (!packages.TryGetValue(packageName, out packageInfo))
            {
                return null;
            }

            // 1. 正向缓存：该资源此前已经定位到具体包，直接取
            int cachedIndex;
            if (packageInfo.assetIndex.TryGetValue(assetName, out cachedIndex))
            {
                if (AssetBundleManager.ContainsAsset(cachedIndex, assetName))
                    return AssetBundleManager.LoadAsset(cachedIndex, assetName, assetType);

                // 包被释放或资源被移除：缓存失效，清掉后重新查找
                packageInfo.assetIndex.Remove(assetName);
            }

            // 2. 负缓存：已经确认所有包都不含该资源，直接返回，避免反复遍历与 IO
            if (packageInfo.missingAssets.Contains(assetName))
                return null;

            // 3. 按优先级逐个加载包并查找：命中即常驻，未命中的包不重复加载
            for (int i = 0, count = packageInfo.paths.Count; i < count; i++)
            {
                int idx = GetOrLoad(packageInfo, packageInfo.paths[i]);
                if (idx < 0)
                    continue;

                if (AssetBundleManager.ContainsAsset(idx, assetName))
                {
                    packageInfo.assetIndex[assetName] = idx;
                    return AssetBundleManager.LoadAsset(idx, assetName, assetType);
                }
            }

            // 全部包都不含该资源：记入负缓存，下次同查询直接返回 null
            packageInfo.missingAssets.Add(assetName);
            return null;
        }

        /// <summary>
        /// 加载资源包内的资源（不限定类型）
        /// </summary>
        /// <param name="packageName">包名</param>
        /// <param name="assetName">资源名</param>
        /// <returns>资源对象，找不到返回 null</returns>
        public UnityEngine.Object LoadAssets(string packageName, string assetName)
        {
            return LoadAssets(packageName, assetName, null);
        }

        /// <summary>
        /// 取得某个包的 AB 索引：已加载则复用，未加载则此刻加载（懒加载的落点）
        /// </summary>
        /// <param name="packageInfo">包登记信息</param>
        /// <param name="path">包文件真实路径</param>
        /// <returns>AB 索引，加载失败返回 <see cref="AssetBundleManager.InvalidId"/></returns>
        static int GetOrLoad(PackageInfo packageInfo, string path)
        {
            int idx;
            if (packageInfo.loadedPathIndex.TryGetValue(path, out idx))
                return idx;

            // 坏包只尝试一次：反复 LoadFromFile 失败会刷屏错误日志并拖慢每次查找
            if (packageInfo.brokenPaths.Contains(path))
                return AssetBundleManager.InvalidId;

            idx = AssetBundleManager.Load(path);
            if (idx < 0)
            {
                packageInfo.brokenPaths.Add(path);
                Sango.Log.Error($"资源包加载失败，已跳过：{path}");
                return AssetBundleManager.InvalidId;
            }

            packageInfo.loadedPathIndex[path] = idx;
            return idx;
        }
    }
}
