//using System.Collections.Generic;
//using UnityEngine;
//using SangoCS.Loader;
//using LuaInterface;
//using System.IO;
//using FairyGUI;

//namespace SangoCS.UI
//{

//    public class UILoader : XFramework.XSingletion<UILoader>
//    {

//        public static bool AddPackage(string fileName, string packageName)
//        {
//            if (!File.Exists(fileName))
//            {
//                Debug.LogError(fileName + "文件不存在!");
//                return false;
//            }

//            byte[] desc = File.ReadAllBytes(fileName);
//            UIPackage.AddPackage(desc, "Bag", (string name, string extension, System.Type type, PackageItem item) =>
//            {
//                if(type == typeof(Texture))
//                {
//                    SangoCS.Loader.TextureLoader.LoadFromFile("D:/" + name + extension, item, (UnityEngine.Object obj, object customData) =>
//                    {
//                        item.owner.SetItemAsset(item, obj, DestroyMethod.None);
//                    }, true);
//                }
//                else if(type == typeof(AudioClip))
//                {

//                }
//            });


//            return true;
//        }
//    }
//}
