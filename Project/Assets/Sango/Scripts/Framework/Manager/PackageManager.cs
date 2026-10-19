

using System;
using System.Collections.Generic;

namespace Sango
{
    public class PackageManager : Singleton<PackageManager>
    {
        class PackageInfo
        {
            public string name { get; set; }
            public string path { get; set; }

            public List<int> packageList = new List<int>();
        }

        Dictionary<string, PackageInfo> packages = new Dictionary<string, PackageInfo>();

        public bool AddPackage(string packageName, string packagePath)
        {
            return AddPackage(packageName, packagePath, true);
        }
        public bool AddPackage(string packageName, string packagePath, bool front)
        {
            PackageInfo packageInfo = null;
            if (!packages.TryGetValue(packageName, out packageInfo))
            {
                packageInfo = new PackageInfo()
                {
                    name = packageName,
                    path = packagePath,
                };
                packages.Add(packageName, packageInfo);
            }

            string finalePath = Path.FindFile(packagePath);
            if (finalePath == null)
                return false;

#if UNITY_EDITOR
            return true;
#endif
            int idx = AssetBundleManager.Load(finalePath);
            if (idx < 0)
                return false;

            if (front)
                packageInfo.packageList.Insert(0, idx);
            else
                packageInfo.packageList.Add(idx);

            return true;
        }

        public UnityEngine.Object LoadAssets(string packageName, string assetName, Type assetType)
        {
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
            PackageInfo packageInfo = null;
            if (!packages.TryGetValue(packageName, out packageInfo))
            {
                return null;
            }

            for (int i = 0, count = packageInfo.packageList.Count; i < count; i++)
            {
                if (AssetBundleManager.ContainsAsset(packageInfo.packageList[i], assetName))
                {
                    UnityEngine.Object asset = AssetBundleManager.LoadAsset(packageInfo.packageList[i], assetName, assetType);
                    if (asset != null)
                        return asset;
                }
            }

            return null;
        }
        public UnityEngine.Object LoadAssets(string packageName, string assetName)
        {
            return LoadAssets(packageName, assetName, null);
        }

    }
}
