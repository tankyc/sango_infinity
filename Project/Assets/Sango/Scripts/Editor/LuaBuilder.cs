/*
'*******************************************************************
Tank 
'*******************************************************************
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using Sango;

/// <summary>
/// lua打包器
/// 将lua打包至项目文件的Assets/Scripts文件夹里
/// </summary>
public static class LuaBuilder
{
    public delegate void BuildLua();
    public static BuildLua overrideBuildFunc;
    //[MenuItem("Sango/脚本/导出脚本包")]
    public static void Build()
    {
        if (overrideBuildFunc != null)
        {
            overrideBuildFunc();
            return;
        }
        BuildScriptsBundle();
    }
    [MenuItem("Sango/脚本/清除所有Wrap文件")]
    public static void CleanAllLuaWrapFiles()
    {
        ToLuaMenu.ClearLuaWraps();
    }
    [MenuItem("Sango/脚本/重新生成Wrap文件(bindLua.xml)")]
    public static void GetAllLuaWrapfx()
    {
        string luaBindListPath = Application.dataPath + "/LuaBind.xml";
        string luaFilterListPath = Application.dataPath + "/LuaFilter.xml";
        string luaDynamicListPath = Application.dataPath + "/LuaDynamic.xml";
        string luaDelegateListPath = Application.dataPath + "/LuaDelegateBind.xml";


        List<ToLuaMenu.BindType> bindList = new List<ToLuaMenu.BindType>(CustomSettings.customTypeList);
        List<string> filterList = new List<string>(ToLuaExport.memberFilter);
        List<DelegateType> delegateList = new List<DelegateType>(CustomSettings.customDelegateList);

        // 添加自定义过滤列表
        if (Sango.File.Exists(luaFilterListPath))
        {
            XDocument xDoc = XDocument.Load(luaFilterListPath);
            if (xDoc != null)
            {
                XElement rootNode = xDoc.Root;
                foreach (XElement node in rootNode.Elements("filter"))
                {
                    string strType = node.Value;
                    filterList.Add(strType);

                }
            }
        }

        ToLuaExport.memberFilter = filterList;


        // 导出类
        Assembly[] ass = System.AppDomain.CurrentDomain.GetAssemblies();

        // 添加动态列表
        if (Sango.File.Exists(luaDynamicListPath))
        {
            XDocument xDoc = XDocument.Load(luaDynamicListPath);
            if (xDoc != null)
            {
                XElement rootNode = xDoc.Root;
                foreach (XElement node in rootNode.Elements("word"))
                {
                    string strType = node.Value;
                    Type t = FindType(strType, ass);
                    if (t != null)
                    {
                        bool find = false;
                        for (int i = 0; i < CustomSettings.dynamicList.Count; ++i)
                        {
                            if (CustomSettings.dynamicList[i] == t)
                            {
                                find = true;
                                break;
                            }
                        }
                        if (!find)
                            CustomSettings.dynamicList.Add(t);
                    }
                    else
                    {
                        UnityEngine.Debug.LogError("无法找到相关Type: " + strType);
                    }
                }
            }
        }

        // 读取xml
        if (Sango.File.Exists(luaBindListPath))
        {
            // 读取需要打包的资源
            XDocument xDoc = XDocument.Load(luaBindListPath);
            if (xDoc != null)
            {
                XElement rootNode = xDoc.Root;
                foreach (XElement node in rootNode.Elements("word"))
                {
                    XAttribute baseType = node.Attribute("baseType");
                    XAttribute extendType = node.Attribute("extendType");

                    string strType = node.Value;
                    Type t = FindType(strType, ass);

                    if (t != null)
                    {
                        bindList.RemoveAll(x => x.type == t);

                        ToLuaMenu.BindType b = new ToLuaMenu.BindType(t);
                        if (baseType != null)
                        {
                            Type bastT = FindType(baseType.Value, ass);
                            if (bastT != null)
                            {
                                b = b.SetBaseType(bastT);
                            }
                        }
                        if (extendType != null)
                        {
                            Type extendT = FindType(extendType.Value, ass);
                            if (extendT != null)
                            {
                                b = b.AddExtendType(extendT);
                            }
                        }
                        bindList.Add(b);
                    }
                    else
                    {
                        UnityEngine.Debug.LogError("无法找到相关Type: " + strType);
                    }
                }
            }
        }

        // 添加动态列表
        if (Sango.File.Exists(luaDelegateListPath))
        {
            XDocument xDoc = XDocument.Load(luaDelegateListPath);
            if (xDoc != null)
            {
                XElement rootNode = xDoc.Root;
                foreach (XElement node in rootNode.Elements("word"))
                {
                    string strType = node.Value;
                    Type t = FindType(strType, ass);
                    if (t != null)
                    {
                        delegateList.RemoveAll(x => x.type == t);
                        delegateList.Add(new DelegateType(t));
                    }
                    else
                    {
                        UnityEngine.Debug.LogError("无法找到相关Type: " + strType);
                    }
                }
            }
        }
        CustomSettings.customDelegateList = delegateList.ToArray();

        CustomSettings.customTypeList = bindList.ToArray();

        // 导出类型绑定表
        ToLuaMenu.GenLuaAll();
    }
    public static void CopyLuaFiles(string sourceDir, string destDir, bool appendext = true, string searchPattern = "*.lua", SearchOption option = SearchOption.AllDirectories, bool isMoveFile = false, bool renameForPath = false)
    {
        if (!System.IO.Directory.Exists(sourceDir))
        {
            return;
        }

        string[] files = System.IO.Directory.GetFiles(sourceDir, searchPattern, option);
        int len = sourceDir.Length;
        if (sourceDir[len - 1] == '/' || sourceDir[len - 1] == '\\')
        {
            --len;
        }

        for (int i = 0; i < files.Length; i++)
        {
            string str = files[i].Remove(0, len);
            string dest = destDir + "/" + str;
            if (renameForPath)
            {
                string fileName = str.Replace('\\', '_');
                fileName = fileName.Replace('/', '_');
                // _aa_bb
                if (fileName.StartsWith("_"))
                    fileName = fileName.Remove(0, 1);

                dest = destDir + "/" + System.IO.Path.GetDirectoryName(str) + "/" + fileName;
            }

            if (appendext) dest += ".bytes";
            string dir = System.IO.Path.GetDirectoryName(dest);

            // 不拷贝doc文件夹,这是用来做编辑器索引的目录
            int ind = dir.LastIndexOf('\\');
            if (ind > 0)
            {
                string sr = dir.Substring(ind + 1);
                if (sr == "doc" || sr == "Luajit" || sr == "Luajit64" || sr == "jit")
                {
                    //File.Delete(files[i]);
                    continue;
                }
            }
            System.IO.Directory.CreateDirectory(dir);
            if (isMoveFile)
            {
                if (Sango.File.Exists(dest))
                    Sango.File.Delete(dest);
                Sango.File.Move(files[i], dest);
            }
            else
            {
                System.IO.File.Copy(files[i], dest, true);
            }
        }
    }
    public static void SetAssetBundleNameToOneFile()
    {
        string[] files = System.IO.Directory.GetFiles(Application.dataPath + "/Lua", "*.bytes", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            AssetImporter importer = AssetImporter.GetAtPath(files[i]);
            if (importer && !importer.assetBundleName.Equals(ScriptsLoaderBase.LUA_BUNDLE_NAME))
            {
                importer.SetAssetBundleNameAndVariant(ScriptsLoaderBase.LUA_BUNDLE_NAME, null);
                importer.SaveAndReimport();
            }
        }
        AssetDatabase.Refresh();
    }
    public static void BuildScriptsBundle()
    {
        string destPath = Application.dataPath + "/Lua";
        string luaSrcPath = SangoSetting.projectDataDir + "/Scripts";
        CopyLuaFiles(luaSrcPath, destPath, true, "*.lua", SearchOption.AllDirectories, false, true);
        AssetDatabase.Refresh();
        SetAssetBundleNameToOneFile();
        AssetDatabase.Refresh();
        string output = SangoSetting.projectDataDir + "/Assets/" + SangoSetting.GetBuildTargetName() + "/Scripts";
        if (!System.IO.Directory.Exists(output))
            System.IO.Directory.CreateDirectory(output);
        BuildLuaBundle(output);
    }
    private static void BuildLuaBundle(string output)
    {
        string destPath = SangoSetting.projectDataDir + "/Assets/" + SangoSetting.GetBuildTargetName() + "/Scripts";
        BuildAssetBundleOptions buildOptions = BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.DisableWriteTypeTree;
        if (!Sango.Directory.Exists(destPath))
            Sango.Directory.Create(destPath, true);
        BuildPipeline.BuildAssetBundles(destPath, buildOptions, EditorUserBuildSettings.activeBuildTarget);
    }
    private static Type FindType(string strType, Assembly[] ass)
    {
        Type t = null;
        for (int i = 0; i < ass.Length; ++i)
        {
            t = ass[i].GetType(strType);
            if (t != null)
                break;
        }
        return t;
    }


}
