//using FairyGUI;
//using LuaInterface;
//using Sango.Game;
//using Sango.Loader;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace Sango
//{
//    public class WindowManager : Singletion<WindowManager>
//    {
//        // Dependency: ListWrapper<T> replicates the Lua list:new() functionality
//        public class ListWrapper<T>
//        {
//            private List<T> _list = new List<T>();

//            // Returns the number of elements in the list.
//            public int length
//            {
//                get { return _list.Count; }
//            }

//            // Appends an item to the end of the list.
//            public void push(T item)
//            {
//                _list.Add(item);
//            }

//            // Returns the first element of the list.
//            public T head()
//            {
//                if (_list.Count > 0)
//                    return _list[0];
//                return default(T);
//            }

//            // Returns the last element of the list.
//            public T tail()
//            {
//                if (_list.Count > 0)
//                    return _list[_list.Count - 1];
//                return default(T);
//            }

//            // Erases (removes) an item from the list.
//            public void erase(T item)
//            {
//                _list.Remove(item);
//            }

//            // Provides enumerator for iterating.
//            public IEnumerable<T> Items
//            {
//                get { return _list; }
//            }
//        }

//        // Definition for Layer structure
//        public class Layer
//        {
//            // unity渲染层
//            public int layer;
//            // layer名字
//            public string name;
//            // 当前层的历史记录
//            public ListWrapper<UGUIWindow> openedList;

//            public Layer(int k, string layerName)
//            {
//                this.layer = k;
//                this.name = layerName;
//                this.openedList = new ListWrapper<UGUIWindow>();
//            }
//        }

//        // Interface for PoolNode functionality
//        public interface IUI_PoolNode
//        {
//            GameObject gameObject { get; set; }
//            void SetVisible(bool b);
//            void SetUIScale(dynamic scale);
//            void SetCenterLayout(int a, int b);
//        }

//        // Dummy implementation for PoolNode
//        public class PoolNode : IUI_PoolNode
//        {
//            public GameObject gameObject { get; set; }

//            public PoolNode(GameObject go)
//            {
//                gameObject = go;
//            }

//            public void SetVisible(bool b)
//            {
//                // Implementation that sets visibility
//                if (gameObject != null)
//                {
//                    gameObject.activeSelf = b;
//                }
//            }

//            public void SetUIScale(dynamic scale)
//            {
//                // Implementation for UI scale adjustment
//            }

//            public void SetCenterLayout(int a, int b)
//            {
//                // Implementation for setting center layout with given parameters
//            }
//        }

//        // Dummy GameObject class
//        public class GameObject
//        {
//            public bool activeSelf { get; set; }
//            public string name { get; set; }
//            public GameObject(string name)
//            {
//                this.name = name;
//                activeSelf = false;
//            }
//            public void SetActive(bool b)
//            {
//                activeSelf = b;
//            }
//        }

//        // Dummy XResManager
//        public static class XResManager
//        {
//            // 模拟资源加载器, 返回一个PoolNode
//            public static IUI_PoolNode Get(string resName, GameObject uiRoot)
//            {
//                // For simulation, always return a PoolNode with a new GameObject
//                return new PoolNode(new GameObject(resName));
//            }
//        }

//        // Dummy XLog for logging
//        public static class XLog
//        {
//            public static void LogError(string message, LogType type)
//            {
//                Console.WriteLine("Error: " + message);
//            }
//            public static void LogWarning(string message, LogType type)
//            {
//                Console.WriteLine("Warning: " + message);
//            }
//            public static void Log(string message, LogType type)
//            {
//                Console.WriteLine("Log: " + message);
//            }
//        }

//        public enum LogType
//        {
//            UI
//        }

//        // Dummy UpdateBeat for update handling
//        public static class UpdateBeat
//        {
//            private static Dictionary<object, Serialization.Action<object>> updateActions = new Dictionary<object, Serialization.Action<object>>();

//            public static void Add(Serialization.Action<object> update, object owner)
//            {
//                if (!updateActions.ContainsKey(owner))
//                    updateActions.Add(owner, update);
//            }

//            public static void Remove(Serialization.Action<object> update, object owner)
//            {
//                if (updateActions.ContainsKey(owner))
//                    updateActions.Remove(owner);
//            }
//        }

//        // Dummy worldManager for camera controls
//        public static class worldManager
//        {
//            public static void setWorldCameraEnable(bool enable)
//            {
//                // Simulate camera enabling/disabling
//            }
//            public static void setWorldCameraBlur(bool blur)
//            {
//                // Simulate camera blur setting
//            }
//        }

//        // Dummy x_eventManager for event posting
//        public static class x_eventManager
//        {
//            public static void postEvent(string eventType, params object[] args)
//            {
//                // Simulate posting an event
//                Console.WriteLine("Event Posted: " + eventType);
//            }
//        }

//        // Dummy gameTools for logging errors
//        public static class gameTools
//        {
//            public static void errorLog(object message)
//            {
//                Console.WriteLine("ErrorLog: " + message.ToString());
//            }

//            public static void logFormat(string format, params object[] args)
//            {
//                Console.WriteLine(string.Format(format, args));
//            }
//        }

//        // Dummy x for various global settings and functions
//        public static class x
//        {
//            public static GameObject _uiRoot = new GameObject("UIRoot");
//            public static bool borderShow = false;
//            public static bool _isDebug = false;
//            public static void reimportShader(GameObject go)
//            {
//                // Simulate reimporting a shader.
//            }
//            public static void link(GameObject go, UGUIWindow ui, bool flag)
//            {
//                // Simulate linking UI and GameObject
//            }
//        }

//        // Dummy _EventType constants
//        public static class _EventType
//        {
//            public const string Event_OnUIFocus = "Event_OnUIFocus";
//            public const string Event_OnUIVisible = "Event_OnUIVisible";
//            public const string Event_OnOpenUI = "Event_OnOpenUI";
//        }

//        // Dummy script loader dictionary and require function simulation
//        public static class ScriptLoader
//        {
//            public static Dictionary<string, Dictionary<string, object>> _loadedScripts = new Dictionary<string, Dictionary<string, object>>();

//            public static Dictionary<string, object> require(string script)
//            {
//                // Simulate loading a script and returning a table (dictionary)
//                // For simulation, return a new dictionary with __index pointing to itself.
//                var tab = new Dictionary<string, object>();
//                tab["__index"] = tab;
//                return tab;
//            }
//        }

//        // UI class representing the UI object with various fields and methods.
//        public class UGUIWindow
//        {
//            // 以下字段直接对应Lua的UI对象各个属性
//            public IUI_PoolNode _uiPoolNode;
//            public GameObject _uiObject;
//            public string _script;
//            public dynamic _scale;
//            public Serialization.Action<UGUIWindow, IUI_PoolNode> _onCreateUIByPoolNode;
//            public bool _focus = false;
//            public bool _visible = false;
//            public List<UGUIWindow> _subUIs; // Assuming subUIs is a list of UI objects
//            public UGUIWindow _prevUI;
//            public UGUIWindow _nextUI;
//            public int _siblingOrder;
//            public bool _isOpened = false;
//            public bool _isSubUI = false;
//            public bool _activeSelf = false;
//            public int _uiType;
//            public string _name;
//            public dynamic _key;
//            public bool _isAwoke = false;
//            public Serialization.Action<object[]> awake;
//            public Serialization.Action<object[]> onRefresh;
//            public Action onFocus;
//            public Action onLoseFocus;
//            public Serialization.Action<bool> onVisible;
//            public Serialization.Action<object, Dictionary<string, object>> _anim; // Assuming _anim has method openAnim
//            public Serialization.Action<object> update;
//            public Serialization.Action<bool> _setInputEnable;
//            public Serialization.Action<string, int> _setLayerAndOrder;
//            public Action _modelOverlay;
//            public Layer _layer;

//            // For simulation, PlaySE method
//            public void playSE(object args, bool flag)
//            {
//                // Simulate playing sound effect
//            }
//        }

//        // The main UI Manager class replicating x_uiManager functionality.
//        public static class x_uiManager
//        {
//            // 每一个UI预留可用层级数(增长数)
//            private static int _Order_Increase_Value_ = 50;

//            // UI分级三层,分别拥有不同的管理
//            public static int _Layer_Base_ = 1;
//            public static int _Layer_Normal_ = 2;
//            public static int _Layer_Middle_ = 3;
//            public static int _Layer_Pop_ = 4;
//            public static int _Layer_Top_ = 5;
//            public static int _Layer_LabelMessage_ = 6;
//            public static int _Layer_Extra_ = 7;
//            public static int _Layer_God_ = 8;

//            // 当前打开的UI列表
//            private static ListWrapper<object> _openedUIList = new ListWrapper<object>();

//            // 存储所有UI对象, keyed by their key.
//            private static Dictionary<object, UGUIWindow> uiRegistry = new Dictionary<object, UGUIWindow>();

//            // 当前聚焦的UI
//            private static UGUIWindow _curFocusUI = null;

//            // 创建layer数据
//            // createLayer对应于Lua中的createLayer函数
//            private static Layer createLayer(int k, string layerName)
//            {
//                return new Layer(k, layerName);
//            }

//            // 每一层间隔的layer数
//            // 需要在Unity添加好每个层(名字)
//            public static Dictionary<int, Layer> _Layers = new Dictionary<int, Layer>()
//        {
//            // 基础层
//            { _Layer_Base_, createLayer(_Layer_Base_, "Defualt") },
//            // 普通层
//            { _Layer_Normal_, createLayer(_Layer_Normal_, "Normal") },
//            // 中间层
//            { _Layer_Middle_, createLayer(_Layer_Middle_, "Middle") },
//            // 弹出菜单层
//            { _Layer_Pop_, createLayer(_Layer_Pop_, "Pop") },
//            // 顶层
//            { _Layer_Top_, createLayer(_Layer_Top_, "Top") },
//            // 消息层,走马灯
//            { _Layer_LabelMessage_, createLayer(_Layer_LabelMessage_, "LabelMessage") },
//            // 特殊额外层
//            { _Layer_Extra_, createLayer(_Layer_Extra_, "Extra") },
//            // 特殊上帝层
//            { _Layer_God_, createLayer(_Layer_God_, "God") }
//        };

//            // 用于注册UI对象到管理器中
//            public static void registerUI(UGUIWindow ui)
//            {
//                if (ui != null && ui._key != null)
//                {
//                    uiRegistry[ui._key] = ui;
//                }
//            }

//            // 查找UI
//            public static UGUIWindow findUI(object key)
//            {
//                if (uiRegistry.ContainsKey(key))
//                {
//                    return uiRegistry[key];
//                }
//                return null;
//            }

//            // 创建layer数据结束

//            // 创建UIByPoolNode
//            private static void _createUIByPoolNode(UGUIWindow ui, IUI_PoolNode poolNode)
//            {
//                ui._uiPoolNode = poolNode;

//                // local go = poolNode.gameObject;
//                GameObject go = poolNode.gameObject;
//                // --XUITools.SetUIParent(go, x._uiRoot)
//                if (!go.activeSelf)
//                {
//                    go.SetActive(true);
//                }
//                x.reimportShader(go);
//                ui._uiObject = go;

//                if (x.borderShow)
//                {
//                    ui._uiPoolNode.SetCenterLayout(720, 1280);
//                }

//                // 挂载脚本
//                string script = ui._script;
//                if (script != null && script != "")
//                {
//                    // 在C#中没有metatable, 使用辅助字段存储脚ipt table if needed.
//                    // 模拟getmetatable(ui)返回null
//                    object metatable = null;
//                    if (metatable == null)
//                    {
//                        // 尝试初始化脚本并关联
//                        Dictionary<string, object> tab = null;
//                        if (ScriptLoader._loadedScripts.ContainsKey(script))
//                        {
//                            tab = ScriptLoader._loadedScripts[script];
//                        }
//                        else
//                        {
//                            tab = ScriptLoader.require(script);
//                            if (tab == null)
//                            {
//                                XLog.LogError("没有找到脚本: " + script, LogType.UI);
//                            }
//                            else
//                            {
//                                // tab将作为ui的metatable, 所以ui必须可以使用tab的方法
//                                tab["__index"] = tab;
//                                // 在C#中，无法直接设置metatable，所以此处不做额外处理.
//                                ScriptLoader._loadedScripts[script] = tab;
//                            }
//                        }
//                    }
//                }
//                // GO 和 脚本链接
//                x.link(go, ui, false);
//                if (ui._scale != null)
//                {
//                    poolNode.SetUIScale(ui._scale);
//                }

//                if (ui._onCreateUIByPoolNode != null)
//                {
//                    ui._onCreateUIByPoolNode(ui, poolNode);
//                }
//            }

//            // 创建UI
//            private static void _createUI(UGUIWindow ui)
//            {
//                string resName = ui._uiObject == null ? ui._name : ui._name;
//                IUI_PoolNode poolNode = XResManager.Get(resName, x._uiRoot); //--不需要修改
//                if (poolNode == null)
//                {
//                    XLog.LogError(" 资源创建失败!! " + "  resName : " + resName, LogType.UI);
//                    return;
//                }
//                _createUIByPoolNode(ui, poolNode);
//            }

//            // 查找前后有效UI
//            private static Tuple<UGUIWindow, UGUIWindow> _findPrevAndNextValidUI(Layer layer)
//            {
//                if (layer == null)
//                {
//                    return new Tuple<UGUIWindow, UGUIWindow>(null, null);
//                }
//                UGUIWindow _prev = null, _next = null;
//                if (layer.openedList.length > 0)
//                {
//                    _prev = layer.openedList.tail();
//                }
//                else
//                {
//                    for (int i = layer.layer - 1; i >= 1; i--)
//                    {
//                        Layer _layer;
//                        if (_Layers.TryGetValue(i, out _layer))
//                        {
//                            if (_layer.openedList.length > 0)
//                            {
//                                _prev = _layer.openedList.tail();
//                                break;
//                            }
//                        }
//                    }
//                }

//                for (int i = layer.layer + 1; i <= _Layer_Extra_; i++)
//                {
//                    Layer _layer;
//                    if (_Layers.TryGetValue(i, out _layer))
//                    {
//                        if (_layer.openedList.length > 0)
//                        {
//                            _next = _layer.openedList.head();
//                            break;
//                        }
//                    }
//                }
//                return new Tuple<UGUIWindow, UGUIWindow>(_prev, _next);
//            }

//            // 聚焦UI
//            private static void _focusUI(UGUIWindow ui)
//            {
//                if (ui == null)
//                {
//                    return;
//                };

//                ui._focus = true;

//                if (ui.onFocus != null)
//                {
//                    x_eventManager.postEvent(_EventType.Event_OnUIFocus, ui._key);
//                    ui.onFocus();
//                }

//                if (ui._visible && ui._setInputEnable != null)
//                {
//                    ui._setInputEnable(true);
//                }

//                List<UGUIWindow> subUIs = ui._subUIs;
//                if (subUIs != null)
//                {
//                    foreach (UGUIWindow val in subUIs)
//                    {
//                        val._focus = true;

//                        if (val.onFocus != null)
//                        {
//                            x_eventManager.postEvent(_EventType.Event_OnUIFocus, val._key);
//                            val.onFocus();
//                        }

//                        if (ui._visible && val._setInputEnable != null)
//                        {
//                            val._setInputEnable(true);
//                        }
//                    }
//                }
//            }

//            // 检查并启用输入
//            private static void _checkEnabled(UGUIWindow ui)
//            {
//                if (ui._visible && ui._setInputEnable != null)
//                {
//                    ui._setInputEnable(true);
//                }

//                List<UGUIWindow> subUIs = ui._subUIs;
//                if (subUIs != null)
//                {
//                    foreach (UGUIWindow val in subUIs)
//                    {
//                        if (val._visible && val._setInputEnable != null)
//                        {
//                            val._setInputEnable(true);
//                        }
//                    }
//                }
//            }

//            // 失去焦点UI
//            private static void _loseFocusUI(UGUIWindow ui)
//            {
//                if (ui == null)
//                {
//                    return;
//                };

//                ui._focus = false;

//                if (ui.onLoseFocus != null)
//                {
//                    ui.onLoseFocus();
//                }

//                if (ui._setInputEnable != null)
//                {
//                    ui._setInputEnable(false);
//                }
//                List<UGUIWindow> subUIs = ui._subUIs;
//                if (subUIs != null)
//                {
//                    foreach (UGUIWindow val in subUIs)
//                    {
//                        val._focus = false;

//                        if (val.onLoseFocus != null)
//                        {
//                            val.onLoseFocus();
//                        }

//                        if (val._setInputEnable != null)
//                        {
//                            val._setInputEnable(false);
//                        }
//                    }
//                }
//            }

//            // 设置UI可见性
//            private static void _setUIVisible(UGUIWindow ui, bool b)
//            {
//                if (ui._isOpened)
//                {
//                    if (ui._isSubUI)
//                    {
//                        ui._activeSelf = b;
//                    }
//                    ui._visible = b;
//                    ui._uiPoolNode.SetVisible(b);
//                    x_eventManager.postEvent(_EventType.Event_OnUIVisible, b, ui._key, ui);

//                    if (ui.onVisible != null)
//                    {
//                        ui.onVisible(b);
//                    }

//                    if (b)
//                    {
//                        _checkScreenAdaptation(ui, true);
//                    }

//                    if (ui._subUIs != null)
//                    {
//                        foreach (UGUIWindow v in ui._subUIs)
//                        {
//                            if (b)
//                            {
//                                if (v._activeSelf == true)
//                                {
//                                    v._visible = true;
//                                    if (v._uiPoolNode != null)
//                                    {
//                                        v._uiPoolNode.SetVisible(true);
//                                        _checkScreenAdaptation(v, true);
//                                        x_eventManager.postEvent(_EventType.Event_OnUIVisible, true, v._key, v);
//                                        if (v.onVisible != null)
//                                        {
//                                            v.onVisible(true);
//                                        }
//                                    }
//                                    else
//                                    {
//                                        XLog.LogError(Environment.StackTrace + "v._uiPoolNode is nil", LogType.UI);
//                                    }

//                                }
//                            }
//                            else
//                            {
//                                v._visible = false;
//                                if (v._uiPoolNode != null)
//                                {
//                                    v._uiPoolNode.SetVisible(false);
//                                    x_eventManager.postEvent(_EventType.Event_OnUIVisible, false, v._key, v);
//                                    if (v.onVisible != null)
//                                    {
//                                        v.onVisible(false);
//                                    }
//                                }
//                                else
//                                {
//                                    XLog.LogError(Environment.StackTrace + "v._uiPoolNode is nil", LogType.UI);
//                                }

//                            }
//                        }
//                    }
//                }
//            }

//            // Dummy function for screen adaptation check.
//            private static void _checkScreenAdaptation(UGUIWindow ui, bool flag)
//            {
//                // Simulate screen adaptation adjustments.
//            }

//            // 打开一个UI,并置于layerId层中.
//            // layer层默认为_Layer_Normal_
//            // @param key any
//            // @param layerId number
//            public static UGUIWindow open(object key, int? layerId, params object[] args)
//            {
//                return openByFunc(key, layerId, _createUI, args);
//            }

//            public static UGUIWindow openByPoolNode(object key, int? layerId, IUI_PoolNode poolNode, params object[] args)
//            {
//                Serialization.Action<UGUIWindow> func = (UGUIWindow ui) =>
//                {
//                    _createUIByPoolNode(ui, poolNode);
//                };
//                return openByFunc(key, layerId, func, args);
//            }

//            // 打开一个UI,并置于layerId层中.
//            // layer层默认为_Layer_Normal_
//            public static UGUIWindow openByFunc(object key, int? layerId, Serialization.Action<UGUIWindow> createReplaceFunc, params object[] args)
//            {
//                UGUIWindow ui = findUI(key);
//                if (ui == null)
//                {
//                    XLog.LogError("open ui数据未找到 key : " + key, LogType.UI);
//                    return null;
//                }
//                XLog.LogWarning("打开UI : " + ui._name, LogType.UI);
//                // 需要一个目标层级
//                if (layerId == null)
//                {
//                    layerId = _Layer_Normal_;
//                }

//                Layer destLayer = null;
//                if (!_Layers.TryGetValue((int)layerId, out destLayer))
//                {
//                    XLog.LogError("layer数据未找到 layerId : " + layerId, LogType.UI);
//                    return null;
//                }

//                // 从原来层中移除
//                Layer srcLayer = ui._layer;
//                if (srcLayer != null)
//                {
//                    srcLayer.openedList.erase(ui);
//                }

//                // 链式结构调整
//                UGUIWindow _prevUI = ui._prevUI;
//                UGUIWindow _nextUI = ui._nextUI;
//                if (_prevUI != null)
//                {
//                    _prevUI._nextUI = _nextUI;
//                }
//                if (_nextUI != null)
//                {
//                    _nextUI._prevUI = _prevUI;
//                }

//                // 加入新层
//                ui._layer = destLayer;

//                // 现在调整到同层了
//                var prevNext = _findPrevAndNextValidUI(destLayer);
//                ui._prevUI = prevNext.Item1;
//                ui._nextUI = prevNext.Item2;
//                if (ui._prevUI != null)
//                {
//                    ui._prevUI._nextUI = ui;
//                }
//                if (ui._nextUI != null)
//                {
//                    ui._nextUI._prevUI = ui;
//                }

//                // 设置层级
//                int siblingOrder = 0;
//                if (destLayer.openedList.length > 0)
//                {
//                    siblingOrder = destLayer.openedList.tail()._siblingOrder + _Order_Increase_Value_;
//                }

//                // 修改成遍历所有子ui  找到最大order作为新ui的起点order
//                if (ui._prevUI != null && ui._prevUI._subUIs != null)
//                {
//                    foreach (UGUIWindow __v in ui._prevUI._subUIs)
//                    {
//                        if (__v._siblingOrder > siblingOrder)
//                        {
//                            siblingOrder = __v._siblingOrder;
//                        }
//                    }
//                }

//                siblingOrder = siblingOrder + _Order_Increase_Value_;

//                int dis = siblingOrder - ui._siblingOrder;
//                destLayer.openedList.push(ui);

//                // 尝试创建资源
//                if (ui._uiObject == null)
//                {
//                    // 缓存资源加载器
//                    createReplaceFunc(ui);
//                }
//                else
//                {
//                    ui._uiPoolNode.SetVisible(true);
//                }

//                ui._visible = true;

//                bool hasSubUI = false;
//                // 焦点判断
//                // 当前聚焦的UI重复打开,只需要刷新即可
//                if (ui._isOpened)
//                {
//                    // 响应onRefresh
//                    if (ui.onRefresh != null)
//                    {
//                        ui.onRefresh(args);
//                    }
//                    if (_curFocusUI == ui)
//                    {
//                        gameTools.errorLog(ui._name);
//                        return ui;
//                    }
//                    hasSubUI = true;
//                }

//                // 放到上return之后，避免值与ui层级不一致情况
//                ui._siblingOrder = siblingOrder;
//                // 大于0的类型为正常类型,会参与焦点互斥
//                if (ui._uiType >= 0)
//                {
//                    if (_curFocusUI == null || _curFocusUI._layer == null || _curFocusUI._layer.layer <= destLayer.layer)
//                    {
//                        UGUIWindow lastFocusUI = _curFocusUI;
//                        _curFocusUI = ui;
//                        // 聚焦
//                        if (lastFocusUI != null && lastFocusUI._layer != null)
//                        {
//                            _loseFocusUI(lastFocusUI);
//                        }
//                        _focusUI(ui);
//                    }
//                    else
//                    {
//                        _loseFocusUI(ui);
//                    }

//                }

//                // 全屏UI打开
//                if (ui._uiType >= 1)
//                {
//                    UGUIWindow prevUI = ui._prevUI;
//                    while (prevUI != null)
//                    {
//                        if (prevUI._uiType >= -1)
//                        {
//                            _setUIVisible(prevUI, false);
//                        }
//                        prevUI = prevUI._prevUI;
//                    }

//                    if (ui._uiType == 1)
//                    {
//                        // worldManager.setWorldCameraEnable(false);
//                    }
//                    else if (ui._uiType == 3)
//                    {
//                        worldManager.setWorldCameraBlur(true);
//                    }
//                }

//                _openedUIList.erase(key);
//                ui._isOpened = true;
//                ui._isSubUI = false;
//                ui._activeSelf = true;

//                // 设置可见
//                _setUIVisible(ui, true);

//                // 在全屏UI下面
//                UGUIWindow _next = ui._nextUI;
//                while (_next != null)
//                {
//                    if (_next._uiType >= 1)
//                    {
//                        _setUIVisible(ui, false);
//                        break;
//                    }
//                    _next = _next._nextUI;
//                }

//                // 通用的open动画
//                if (ui._anim != null)
//                {
//                    // 在Lua中调用openAnim(ui._uiObject, { callback = ui.OnOpenAnimComplete });
//                    // 这里我们假定ui._anim是可调用的Action，回调参数以Dictionary传递
//                    ui._anim(ui._uiObject, new Dictionary<string, object>() { { "callback", (Action)(() => { /* OnOpenAnimComplete */ }) } });
//                }

//                // 查看是否添加至update中
//                if (ui.update != null)
//                {
//                    UpdateBeat.Remove(ui.update, ui);
//                    UpdateBeat.Add(ui.update, ui);
//                }

//                // 设置层级接口,可以处理一些层级关系
//                if (ui._setLayerAndOrder != null)
//                {
//                    ui._setLayerAndOrder(destLayer.name, siblingOrder);
//                }

//                // gameTools.logFormat("_setLayerAndOrder:%s,%s,%s", ui._key, siblingOrder, ui._siblingOrder)

//                if (ui._modelOverlay != null)
//                {
//                    ui._modelOverlay();
//                }

//                // 添加进list中,并且记录当前层级
//                _openedUIList.push(key);

//                if (x._isDebug)
//                {
//                    XLog.Log(Environment.StackTrace + "打开UI : " + ui._name, LogType.UI);
//                }
//                // 响应awake
//                // 保证最后执行,不然会出现一些时序问题
//                if (ui.awake != null && !ui._isAwoke)
//                {
//                    ui._isAwoke = true;
//                    ui.awake(args);
//                    ui.playSE(null, true);
//                }

//                if (hasSubUI)
//                {
//                    if (ui._subUIs != null)
//                    {
//                        foreach (UGUIWindow __v in ui._subUIs)
//                        {
//                            __v._siblingOrder = __v._siblingOrder + dis;
//                            if (__v._setLayerAndOrder != null)
//                            {
//                                __v._setLayerAndOrder(destLayer.name, __v._siblingOrder);
//                            }
//                        }
//                    }
//                }
//                x_eventManager.postEvent(_EventType.Event_OnOpenUI, key);
//                return ui;
//            }

//            // Dummy method to simulate setting animation to UI.
//            public static void setAnimToUI(UGUIWindow ui, bool flag)
//            {
//                // Simulation: do nothing.
//            }
//        }

//    ////////////////////////////////////////////////////////////////////
//    //// The following is for testing purposes and demonstrates usage.
//    ////////////////////////////////////////////////////////////////////
//    //namespace UILibraryTest
//    //{
//    //    using System;
//    //    using UILibrary;
//    //    public class Program
//    //    {
//    //        public static void Main(string[] args)
//    //        {
//    //            // Create a dummy UI instance and register it.
//    //            UI myUI = new UI();
//    //            myUI._key = "UI_1";
//    //            myUI._name = "TestUI";
//    //            myUI._uiType = 0;
//    //            // For demonstration, assign empty actions.
//    //            myUI.awake = (object[] parameters) => { Console.WriteLine("UI awake called."); };
//    //            myUI.onFocus = () => { Console.WriteLine("UI onFocus called."); };
//    //            myUI.onLoseFocus = () => { Console.WriteLine("UI onLoseFocus called."); };
//    //            myUI.onVisible = (bool visible) => { Console.WriteLine("UI onVisible called with: " + visible); };
//    //            myUI._setInputEnable = (bool enable) => { Console.WriteLine("UI input set to: " + enable); };
//    //            myUI._setLayerAndOrder = (string layerName, int order) => { Console.WriteLine("UI layer set to " + layerName + " with order " + order); };

//    //            // Register the UI
//    //            x_uiManager.registerUI(myUI);

//    //            // Open the UI on default layer.
//    //            UI openedUI = x_uiManager.open("UI_1", null);
//    //            Console.WriteLine("UI opened: " + (openedUI != null ? openedUI._name : "null"));
//    //        }
//    //    }
//    //}

//}
//}
