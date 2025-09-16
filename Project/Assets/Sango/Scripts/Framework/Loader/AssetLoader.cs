using Sango.Loader;
using System;
using UnityEngine;

namespace Sango.Game
{
    /// <summary>
    /// 规范资源加载流程
    /// 1.资源路径规范: PackageName:Assets/AAA/BBB/CCC.ddd 都是基于Assets文件夹的资源路径, PackageName为包名
    /// 2.带有包名的文件路径会优先查找对应的包,包内如果找到则直接加载并返回
    /// 3.查找Mod文件,通过Path.Find()判断是否有文件,找到后直接创建并返回
    /// 4.最后查找Content文件夹,判断是否有文件,找到后直接创建并返回
    /// </summary>
    public class AssetLoader
    {
        private static Type TextureType = typeof(UnityEngine.Texture);
        private static Type GameObjectType = typeof(UnityEngine.GameObject);
        private static Type MaterialType = typeof(UnityEngine.Material);
        private static Type SpriteType = typeof(UnityEngine.Sprite);

        /// <summary>
        /// assetName = "PackageName:AssetPath" PackageName = "Resources"时在Resources文件夹中读取
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="objType"></param>
        /// <param name="ps"></param>
        /// <returns></returns>
        public static UnityEngine.Object LoadAsset(string assetPath, System.Type objType, params object[] ps)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            string[] split_str = assetPath.Split(new char[] { ':' });
            string packageName = null;
            string objName = assetPath;
            if (split_str.Length > 1)
            {
                packageName = split_str[0];
                objName = split_str[1];
            }

            if (!string.IsNullOrEmpty(packageName))
            {
                string storeName = string.Format("obj_{0}_{1}", packageName, objName);
                UnityEngine.Object obj = AssetStore.Instance.GetAsset(storeName);
                if (obj == null)
                {
                    if (packageName.Equals("Resources"))
                    {
                        obj = Resources.Load(objName, objType);
                    }
                    else
                    {
                        obj = PackageManager.Instance.LoadAssets(packageName, objName, objType);
                    }
                    if (obj != null)
                    {
                        obj = AssetStore.Instance.StoreAsset(storeName, obj);
                        return obj;
                    }
                }
            }

            if (objType == TextureType)
            {
                return TextureLoader.LoadFromFileSync(objName, (bool)ps[0], (bool)ps[1]);
            }
            else if (objType == MaterialType)
            {
                return MaterialLoader.LoadMaterial(objName, (bool)ps[0]);
            }
            else if (objType == GameObjectType)
            {
                return ModelLoader.LoadFromFileSync(objName, (string)ps[0], (bool)ps[1], (string)ps[2], (bool)ps[3]);
            }
            else if (objType == SpriteType)
            {
                return SpriteLoader.LoadSprite(objName);
            }

            return null;
        }

        public static T LoadAsset<T>(string assetName, params object[] ps) where T : UnityEngine.Object
        {
            Type type = typeof(T);
            return LoadAsset(assetName, type, ps) as T;
        }
    }
}
