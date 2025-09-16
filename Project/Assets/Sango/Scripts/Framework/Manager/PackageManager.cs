
using LuaInterface;
using System;
using System.Collections.Generic;
using UnityEngine;
using Sango;
using Sango.Mod;

namespace Sango
{
    public class PackageManager : Singletion<PackageManager>
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
            string assetsPath = $"Assets/Packages/{packageName}.pkg+{ModManager.EditModName ?? "Content"}/{assetName}";
            UnityEngine.Object editorObj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetsPath, assetType);
            if (editorObj != null) return editorObj;
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
