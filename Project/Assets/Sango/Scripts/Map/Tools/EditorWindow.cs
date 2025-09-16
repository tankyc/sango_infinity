using System.Collections.Generic;
using UnityEngine;

namespace Sango.Tools
{
    public class EditorWindow : MonoBehaviour
    {
        public delegate void WindowFunction(int winId, EditorWindow window);

        public bool visible;
        public UnityEngine.Rect windowRect;
        public WindowFunction windowFunc;
        public WindowFunction windowMinFunc;
        public string windowName;
        public int windowId;
        public bool canClose = true;
        public bool dragable = true;
        public bool minmaxable = true;
        public GUISkin skin;
        bool isMax = true;

        static UnityEngine.Rect CloseRect = new UnityEngine.Rect(8, 4, 18, 14);
        static UnityEngine.Rect MinRect = new UnityEngine.Rect(24, 4, 18, 14);
        static GUIStyle CloseStyle;
        static GUIStyle MinStyle;
        static GUIStyle MaxStyle;

        internal static void InitGUIStyle()
        {
            if (CloseStyle == null)
            {
                CloseStyle = new GUIStyle(GUI.skin.button);
                CloseStyle.fontSize = 12;
                MinStyle = new GUIStyle(CloseStyle);
                MaxStyle = new GUIStyle(CloseStyle);
            }
        }
        Color lastColor;
        internal void OnDraw(int winId)
        {
            float beginX = windowRect.width - 4;
            if (canClose)
            {
                lastColor = GUI.color;
                GUI.color = Color.red;
                beginX -= 20;
                CloseRect.x = beginX;
                if (GUI.Button(CloseRect, "x", CloseStyle))
                {
                    visible = false;
                }
                GUI.color = lastColor;
            }

            if (minmaxable)
            {
                if (isMax)
                {
                    beginX -= 20;
                    MinRect.x = beginX;
                    if (GUI.Button(MinRect, "-", MinStyle))
                    {
                        isMax = false;
                        windowRect.size = new Vector2(90, 40);
                        return;
                    }

                    if (windowFunc != null)
                        windowFunc(winId, this);
                }
                else
                {

                    beginX -= 20;
                    MinRect.x = beginX;
                    if (GUI.Button(MinRect, "¡õ", MaxStyle))
                    {
                        isMax = true;
                    }

                    if (windowMinFunc != null)
                        windowMinFunc(winId, this);
                    else
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(90);
                        GUILayout.EndHorizontal();
                    }
                }
            }
            else
            {
                if (windowFunc != null)
                    windowFunc(winId, this);
            }

            if (dragable)
                GUI.DragWindow();
        }

        static List<EditorWindow> window_list = new List<EditorWindow>();
        public void Awake()
        {
            window_list.Add(this);
        }

        public void OnDestroy()
        {
            window_list.Remove(this);
        }
        public void OnGUI()
        {
            InitGUIStyle();

            if (visible)
            {
                if(skin != null)
                {
                    GUISkin lastSkin = GUI.skin;
                    GUI.skin = skin;
                    windowRect = GUILayout.Window(windowId, windowRect, OnDraw, windowName);
                    GUI.skin = lastSkin;
                }
                else
                {
                    windowRect = GUILayout.Window(windowId, windowRect, OnDraw, windowName);
                }
            }
        }

        public static bool IsPointOverUI()
        {
            Vector3 mousePosition = Input.mousePosition;
            mousePosition.y = Screen.height - mousePosition.y;
            foreach (EditorWindow win in window_list)
            {
                if (win.visible)
                {
                    if (win.windowRect.Contains(mousePosition))
                        return true;
                }
            }

            return false;
        }
        public static EditorWindow AddWindow(int id, UnityEngine.Rect rect, WindowFunction func, string windowName)
        {
            return AddWindow(id, rect, func, null, windowName);
        }
        public static EditorWindow AddWindow(int id, UnityEngine.Rect rect, WindowFunction func, WindowFunction minfunc, string windowName)
        {
            EditorWindow win = new GameObject(windowName).AddComponent<EditorWindow>();
            win.visible = true;
            win.windowRect = rect;
            win.windowFunc = func;
            win.windowMinFunc = minfunc;
            win.windowName = windowName;
            win.windowId = id;
            win.isMax = true;
            window_list.Add(win);
            return win;
        }

        public static EditorWindow AddWindow<T>(int id, UnityEngine.Rect rect, WindowFunction func, string windowName) where T : EditorWindow
        {
            return AddWindow<T>(id, rect, func, null, windowName);
        }
        public static EditorWindow AddWindow<T>(int id, UnityEngine.Rect rect, WindowFunction func, string windowName, GUISkin skin) where T : EditorWindow
        {
            return AddWindow<T>(id, rect, func, null, windowName, skin);
        }
        public static EditorWindow AddWindow<T>(int id, UnityEngine.Rect rect, WindowFunction func, WindowFunction minfunc, string windowName) where T : EditorWindow
        {
            return AddWindow<T>(id, rect, func, minfunc, windowName, null);
        }
        public static EditorWindow AddWindow<T>(int id, UnityEngine.Rect rect, WindowFunction func, WindowFunction minfunc, string windowName, GUISkin skin) where T : EditorWindow
        {
            EditorWindow win = new GameObject(windowName).AddComponent<T>();
            win.visible = true;
            win.windowRect = rect;
            win.windowFunc = func;
            win.windowMinFunc = minfunc;
            win.windowName = windowName;
            win.windowId = id;
            win.isMax = true;
            win.skin = skin;
            window_list.Add(win);
            return win;
        }

        public static void RemoveWindow(EditorWindow w)
        {
            if (w != null)
                Destroy(w.gameObject);
        }

        

    }


}