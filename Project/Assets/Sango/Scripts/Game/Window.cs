//using FairyGUI;

using Sango.Core;
using Sango.Loader;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango
{

    public class Window : Singleton<Window>
    {

        public class WindowInterface
        {
            //public FairyGUI.Window fgui_instance;
            public UGUIWindow ugui_instance;

            public bool HasValid()
            {
                //return fgui_instance != null || ugui_instance != null;
                return ugui_instance != null;
            }

            public void SetVisible(bool b)
            {
                ugui_instance?.gameObject.SetActive(b);
            }

            public bool IsVisible()
            {
                if (ugui_instance == null) return false;
                return ugui_instance.gameObject.activeSelf;
            }

            public void Open()
            {
                //fgui_instance?.Show();
                ugui_instance?.Open();

            }

            public void Open(params object[] objects)
            {
                //fgui_instance?.Show();
                ugui_instance?.Open(objects);

            }

            public void Close()
            {
                //fgui_instance?.Hide();
                ugui_instance?.Close();
            }
            public void Refresh()
            {
                //fgui_instance?.Refresh();
                ugui_instance?.Refresh();
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

        public WindowInterface CreateNewWindow(string windowName)
        {
            UnityEngine.GameObject uguiWinObj = PoolManager.Create($"Assets/UI/Prefab/{windowName}.prefab");
            if (uguiWinObj != null)
            {
                uguiWinObj.name = windowName;
                //Canvas canvas = uguiWinObj.GetComponent<Canvas>();
                //if (canvas != null)
                //{
                //    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                //    canvas.worldCamera = Sango.Core.Game.Instance.UICamera;
                //}
                uguiWinObj.transform.SetParent(Sango.Core.Game.Instance.UIRoot, false);

                GameSetting.Instance.UpdateTargetGameObject(uguiWinObj);

                UGUIWindow uGUIWindow = uguiWinObj.GetComponent<UGUIWindow>();
                if (uGUIWindow == null)
                    uGUIWindow = uguiWinObj.AddComponent<UGUIWindow>();
                uGUIWindow.OnCloseAction = () =>
                {
                    PoolManager.Recycle(uguiWinObj);
                };
                uguiWinObj.transform.SetAsLastSibling();
                WindowInfo info = new WindowInfo()
                {
                    name = windowName,
                    packageName = null,
                    resName = null,
                    scriptName = null,
                    instance = new WindowInterface() { ugui_instance = uGUIWindow }
                };
                return info.instance;
            }
            return null;
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
            //        Sango.Core.Event.OnWindowCreate?.Invoke(windowName, info.instance);
            //    }
            //}
            //else
            {
                UnityEngine.Object winObj = ObjectLoader.LoadObject<UnityEngine.GameObject>($"Assets/UI/Prefab/{windowName}.prefab");
                if (winObj != null)
                {
                    GameObject uguiWinObj = GameObject.Instantiate(winObj) as GameObject;
                    if (uguiWinObj != null)
                    {
                        uguiWinObj.name = windowName;
                        //Canvas canvas = uguiWinObj.GetComponent<Canvas>();
                        //if (canvas != null)
                        //{
                        //    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        //    canvas.worldCamera = Sango.Core.Game.Instance.UICamera;
                        //}
                        uguiWinObj.transform.SetParent(Sango.Core.Game.Instance.UIRoot, false);

                        GameSetting.Instance.UpdateTargetGameObject(uguiWinObj);

                        UGUIWindow uGUIWindow = uguiWinObj.GetComponent<UGUIWindow>();
                        if (uGUIWindow == null)
                            uGUIWindow = uguiWinObj.AddComponent<UGUIWindow>();

                        //uguiWinObj.transform.SetParent(Game.Game.Instance.rootGameObject.transform, false);
                        WindowInfo info1 = new WindowInfo()
                        {
                            name = windowName,
                            packageName = null,
                            resName = null,
                            scriptName = null,
                            instance = new WindowInterface() { ugui_instance = uGUIWindow }
                        };
                        windowMap.Add(windowName, info1);
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

        public WindowInterface Open(string windowName)
        {
#if SANGO_DEBUG
            UnityEngine.Debug.Log($"显示窗口:{windowName}");
#endif
            WindowInterface win = CreateWindow(windowName);
            if (win != null)
            {
                win.Open();
                Sango.Core.GameEvent.OnWindowCreate?.Invoke(windowName, win);
            }
            return win;
        }

        public WindowInterface Open(string windowName, params object[] objects)
        {
#if SANGO_DEBUG
            UnityEngine.Debug.Log($"显示窗口:{windowName}");
#endif
            WindowInterface win = CreateWindow(windowName);
            if (win != null)
            {
                win.Open(objects);
                Sango.Core.GameEvent.OnWindowCreate?.Invoke(windowName, win);
            }
            return win;
        }

        public T Open<T>(string windowName) where T : UGUIWindow
        {
            WindowInterface win = Open(windowName);
            return win.ugui_instance as T;
        }

        public T Open<T>(string windowName, params object[] objects) where T : UGUIWindow
        {
            WindowInterface win = Open(windowName, objects);
            return win.ugui_instance as T;
        }


        public WindowInterface OpenNew(string windowName)
        {
#if SANGO_DEBUG
            UnityEngine.Debug.Log($"显示窗口:{windowName}");
#endif
            WindowInterface win = CreateNewWindow(windowName);
            if (win != null)
            {
                win.Open();
                Sango.Core.GameEvent.OnWindowCreate?.Invoke(windowName, win);
            }
            return win;
        }

        public WindowInterface OpenNew(string windowName, params object[] objects)
        {
#if SANGO_DEBUG
            UnityEngine.Debug.Log($"显示窗口:{windowName}");
#endif
            WindowInterface win = CreateNewWindow(windowName);
            if (win != null)
            {
                win.Open(objects);
                Sango.Core.GameEvent.OnWindowCreate?.Invoke(windowName, win);
            }
            return win;
        }

        public T OpenNew<T>(string windowName) where T : UGUIWindow
        {
            WindowInterface win = OpenNew(windowName);
            return win.ugui_instance as T;
        }

        public T OpenNew<T>(string windowName, params object[] objects) where T : UGUIWindow
        {
            WindowInterface win = OpenNew(windowName, objects);
            return win.ugui_instance as T;
        }


        public WindowInterface GetWindow(string windowName)
        {
            WindowInfo info;
            if (windowMap.TryGetValue(windowName, out info))
                return info.instance;
            return null;
        }

        public void Close(string windowName)
        {
#if SANGO_DEBUG
            UnityEngine.Debug.Log($"隐藏窗口:{windowName}");
#endif
            WindowInfo info;
            if (windowMap.TryGetValue(windowName, out info))
            {
                if (info.instance != null)
                {
                    if (info.instance.ugui_instance != null)
                        info.instance.ugui_instance.Close();
                    //if (info.instance.fgui_instance != null)
                    //    info.instance.fgui_instance.Hide();
                }
            }
        }

        public void CloseAll()
        {
            foreach (WindowInfo info in windowMap.Values)
            {
                if (info.instance != null)
                {
                    if (info.instance.ugui_instance != null)
                        info.instance.ugui_instance.Close();
                }
            }
        }

        public void DestroyAll()
        {
            foreach (WindowInfo info in windowMap.Values)
            {
                if (info.instance != null)
                {
                    if (info.instance.ugui_instance != null)

                    {
                        GameObject.Destroy(info.instance.ugui_instance.gameObject);
                    }
                }
            }
            windowMap.Clear();
        }


        public bool IsOpen(string windowName)
        {
            WindowInfo info;
            if (windowMap.TryGetValue(windowName, out info))
            {
                if (info.instance != null)
                {
                    if (info.instance.ugui_instance != null)
                        return info.instance.ugui_instance.IsOpen;
                }
            }
            return false;
        }

        public void SetVisible(string windowName, bool b)
        {
            WindowInfo info;
            if (!windowMap.TryGetValue(windowName, out info))
                return;

            info.instance.SetVisible(b);

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
            //    Sango.Core.Event.OnWindowCreate?.Invoke(windowName, windowInterface);
            //    return windowInterface;
            //}
            return null;
        }
    }
}
