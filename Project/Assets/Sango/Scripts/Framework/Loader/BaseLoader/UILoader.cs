//using System.Collections.Generic;
//using UnityEngine;
//using Sango.Loader;
//using LuaInterface;
//using System.IO;
//using FairyGUI;
//using Sango;

//namespace Sango.Loader
//{
//    public class UILoader : ObjectLoader
//    {
//        /// <summary>
//        /// 添加UI资源包
//        /// </summary>
//        /// <param name="fileName">资源包路径</param>
//        /// <param name="packageName">资源包名字</param>
//        /// <returns></returns>
//        public static UIPackage AddPackage(string fileName, string packageName)
//        {
//            fileName = Path.FindFile(fileName);
//            if (fileName == null)
//                return null;

//            byte[] desc = System.IO.File.ReadAllBytes(fileName);
//            UIPackage pkg = UIPackage.AddPackage(desc, packageName.Split("_")[0], (string name, string extension, System.Type type, PackageItem item) =>
//            {
//                if (type == typeof(Texture))
//                {
//                    string path = System.IO.Path.GetDirectoryName(fileName);
//                    string texturePath = string.Format("{0}/{1}{2}", path, name, extension);
//                    Sango.Loader.TextureLoader.LoadFromFile(texturePath, item, (UnityEngine.Object obj, object customData) =>
//                    {
//                        item.owner.SetItemAsset(item, obj, DestroyMethod.None);
//                    }, true);
//                }
//                else if (type == typeof(AudioClip))
//                {

//                }
//            });
//            pkg.customId = packageName;
//            return pkg;
//        }

//        public static GObject CreateObject(string pkgName, string resName)
//        {
//            UIPackage pkg = UIPackage.GetById(pkgName);
//            if (pkg != null)
//                return pkg.CreateObject(resName);
//            else
//                return null;
//        }

//        public static bool CheckItem(string pkgName, string resName)
//        {
//            UIPackage pkg = UIPackage.GetById(pkgName);
//            if (pkg != null)
//                return pkg.GetItemByName(resName) != null;
//            else
//                return false;
//        }

//        public static UIPanel CreatePanel(string pkgName, string resName)
//        {
//            GameObject go = new GameObject();
//            UIPanel panel = go.AddComponent<UIPanel>();
//            panel.packageId = pkgName;
//            panel.componentName = resName;
//            panel.container.renderMode = RenderMode.WorldSpace;
//            return panel;
//        }
//    }
//}
