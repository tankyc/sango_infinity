//using FairyGUI;
using LuaInterface;
using Sango.Game;
using Sango.Loader;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sango
{

    public class Window : Singletion<Window>
    {

        public class WindowInterface
        {
            //public FairyGUI.Window fgui_instance;
            public UGUIWindow ugui_instance;

            public bool hasValid()
            {
                //return fgui_instance != null || ugui_instance != null;
                return ugui_instance != null;
            }

            public void Show()
            {
                //fgui_instance?.Show();
                ugui_instance?.Show();

            }
            public void Hide()
            {
                //fgui_instance?.Hide();
                ugui_instance?.Hide();
            }
        }

        public struct PackageInfo
        {
            public string name;
            public int count;
        }
        Dictionary<string, PackageInfo> packageMap = new Dictionary<string, PackageInfo>();

        public struct WindowInfo
        {
            public string name;
            public string packageName;
            public string resName;
            public string scriptName;
            public WindowInterface instance;
        }
        Dictionary<string, WindowInfo> windowMap = new Dictionary<string, WindowInfo>();

        public delegate Window CreateFunc(string pkgName, string resName);
        Dictionary<string, CreateFunc> CreateMap = new Dictionary<string, CreateFunc>();

        public bool AddPackage(string fileName, string pkgName)
        {
            //PackageInfo info;
            //int count = 1;
            //if (packageMap.TryGetValue(pkgName, out info))
            //{
            //    info.count++;
            //    count = info.count;
            //    packageMap[pkgName] = info;
            //}
            //else
            //{
            //    packageMap.Add(pkgName, new PackageInfo()
            //    {
            //        name = fileName,
            //        count = count
            //    });
            //}

            //var finalKey = $"{pkgName}_{count}";
            //UIPackage pkg = UILoader.AddPackage(fileName, finalKey);
            //if (pkg != null)
            //{
            //    List<PackageItem> items = pkg.GetItems();
            //    foreach (PackageItem packageItem in items)
            //    {
            //        if (packageItem.type == PackageItemType.Component && packageItem.name.StartsWith("window_"))
            //        {
            //            RegisterWindow(packageItem.name, pkg.customId, packageItem.name, packageItem.name);
            //        }
            //    }
            //}
            //return pkg != null;
            return false;
        }

        public string FindPackage(string pkgName, string resName)
        {
            //PackageInfo info;
            //if (packageMap.TryGetValue(pkgName, out info))
            //{
            //    for (int i = info.count - 1; i >= 0; i--)
            //    {
            //        string finalKey = $"{pkgName}_{i}";
            //        if (UILoader.CheckItem(pkgName, resName))
            //        {
            //            return finalKey;
            //        }
            //    }
            //}
            return null;
        }

        //public GObject CreateObject(string pkgName, string resName)
        //{
        //    PackageInfo info;
        //    if (packageMap.TryGetValue(pkgName, out info))
        //    {
        //        for (int i = info.count - 1; i >= 0; i--)
        //        {
        //            string finalKey = $"{pkgName}_{i}";
        //            GObject obj = UILoader.CreateObject(finalKey, resName);
        //            if (obj != null)
        //            {
        //                return obj;
        //            }
        //        }
        //    }
        //    return null;
        //}
        public void RegisterWindow(string windowName, string pkgName, string resName)
        {
            WindowInfo info = new WindowInfo()
            {
                name = windowName,
                packageName = pkgName,
                resName = resName,
                scriptName = ""
            };

            if (!windowMap.TryAdd(windowName, info))
                windowMap[windowName] = info;
        }
        public void RegisterWindow(string windowName, string pkgName, string resName, string tableName)
        {
            WindowInfo info = new WindowInfo()
            {
                name = windowName,
                packageName = pkgName,
                resName = resName,
                scriptName = tableName
            };

            if (!windowMap.TryAdd(windowName, info))
                windowMap[windowName] = info;
        }

        string[] upperFirstChar(string s, string sep)
        {
            string[] split = s.Split(sep);
            for (int i = 0; i < split.Length; ++i)
            {
                string dst = split[i];
                split[i] = char.ToUpper(dst[0]) + dst.Substring(1);
            }
            return split;
        }

        public LuaTable FindPeerTable(WindowInfo info)
        {
            LuaTable table = null;
            if (!string.IsNullOrEmpty(info.scriptName))
            {
                table = LuaClient.GetTable(info.scriptName);
            }

            if (table == null)
            {
                // ---aa_bb_cc
                table = LuaClient.GetTable(info.name);
            }

            string[] split_s = upperFirstChar(info.name, "_");

            if (table == null)
            {
                // ---Aa_Bb_Cc
                table = LuaClient.GetTable(string.Join("_", split_s));
            }

            if (table == null)
            {
                // ---AaBbCc
                table = LuaClient.GetTable(string.Join("", split_s));
            }

            if (table == null)
            {
                table = LuaClient.GetTable(info.resName);
            }

            return table;
        }

        public WindowInterface CreateWindow(string windowName)
        {
            WindowInfo info;
            if (!windowMap.TryGetValue(windowName, out info))
            //if (windowMap.TryGetValue(windowName, out info))
            //{
            //    if (info.instance == null)
            //    {
            //        FairyGUI.Window win = new FairyGUI.Window();
            //        win.contentPane = UILoader.CreateObject(info.packageName, info.resName) as FairyGUI.GComponent;
            //        LuaTable table = FindPeerTable(info);
            //        if (table != null)
            //            win.SetLuaPeer(table);
            //        info.instance = new WindowInterface() { fgui_instance = win };
            //        windowMap[windowName] = info;
            //        Sango.Game.Event.OnWindowCreate?.Invoke(windowName, info.instance);
            //    }
            //}
            //else
            {

                UnityEngine.Object winObj = ObjectLoader.LoadObject<UnityEngine.GameObject>($"UI:Assets/UI/Prefab/{windowName}.prefab");
                if (winObj != null)
                {
                    GameObject uguiWinObj = GameObject.Instantiate(winObj) as GameObject;
                    if (uguiWinObj != null)
                    {
                        UGUIWindow uGUIWindow = uguiWinObj.GetComponent<UGUIWindow>();
                        if (uGUIWindow == null)
                            uGUIWindow = uguiWinObj.AddComponent<UGUIWindow>();

                        //uguiWinObj.transform.SetParent(Game.Game.Instance.rootGameObject.transform, false);
                        WindowInfo info1 = new WindowInfo()
                        {
                            name = windowName,
                            packageName = null,
                            resName = windowName,
                            scriptName = windowName,
                            instance = new WindowInterface() { ugui_instance = uGUIWindow }
                        };
                        windowMap.Add(windowName, info1);
                        LuaTable table = FindPeerTable(info1);
                        if (table != null)
                            uGUIWindow.AttachScript(table);

                        return info1.instance;
                    }
                }
            }

            return info.instance;
        }

        public void Init(int screenX, int screenY)
        {
            //GRoot.inst.SetContentScaleFactor(screenX, screenY);
        }

        public WindowInterface ShowWindow(string windowName)
        {
            UnityEngine.Debug.Log($"显示窗口:{windowName}");
            WindowInterface win = CreateWindow(windowName);
            if (win != null)
            {
                win.Show();
                Sango.Game.Event.OnWindowCreate?.Invoke(windowName, win);
            }
            return win;
        }

        public void HideWindow(string windowName)
        {
            UnityEngine.Debug.Log($"隐藏窗口:{windowName}");
            WindowInfo info;
            if (windowMap.TryGetValue(windowName, out info))
            {
                if (info.instance != null)
                {
                    if (info.instance.ugui_instance != null)
                        info.instance.ugui_instance.Hide();
                    //if (info.instance.fgui_instance != null)
                    //    info.instance.fgui_instance.Hide();
                }
            }
        }

        //public static FairyGUI.Window CreateWindow(string pkgName, string resName, LuaTable luaTable, bool fullScreen = true)
        //{
        //    FairyGUI.Window win = new FairyGUI.Window();
        //    win.contentPane = UILoader.CreateObject(pkgName, resName) as FairyGUI.GComponent;
        //    win.SetLuaPeer(luaTable);
        //    if (fullScreen)
        //        win.MakeFullScreen();
        //    return win;
        //}

        public WindowInterface NewWindow(string windowName)
        {
            //WindowInfo info;
            //if (windowMap.TryGetValue(windowName, out info))
            //{
            //    FairyGUI.Window win = new FairyGUI.Window();
            //    win.contentPane = UILoader.CreateObject(info.packageName, info.resName) as FairyGUI.GComponent;
            //    LuaTable table = FindPeerTable(info);
            //    if (table != null)
            //        win.SetLuaPeer(table);
            //    WindowInterface windowInterface = new WindowInterface() { fgui_instance = win };
            //    Sango.Game.Event.OnWindowCreate?.Invoke(windowName, windowInterface);
            //    return windowInterface;
            //}
            return null;
        }
    }
}
