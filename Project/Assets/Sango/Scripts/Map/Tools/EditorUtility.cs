using HSVPicker;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;




namespace Sango.Tools
{
    public static class EditorUtility
    {
        public class EditorFieldData
        {

        }

        public class FloatFieldData : EditorFieldData
        {
            public bool hasDot = false;
        }
        static bool changed = false;
        static public Vector3 Vector3Field(Vector3 v)
        {
            GUILayout.BeginHorizontal();
            changed = false;
            float x = FloatField(v.x);
            float y = FloatField(v.y);
            float z = FloatField(v.z);
            GUILayout.EndHorizontal();
            if (changed)
            {
                return new Vector3(x, y, z);
            }
            return v;
        }

        static public Vector3 Vector3Field(Vector3 v, string name)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name);
            changed = false;
            float x = FloatField(v.x);
            float y = FloatField(v.y);
            float z = FloatField(v.z);
            GUILayout.EndHorizontal();
            if (changed)
            {
                return new Vector3(x, y, z);
            }
            return v;
        }

        static public Vector2 Vector2Field(Vector2 v, string name)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name);
            changed = false;
            float x = FloatField(v.x);
            float y = FloatField(v.y);
            GUILayout.EndHorizontal();
            if (changed)
            {
                return new Vector2(x, y);
            }
            return v;
        }

        static public Vector2 Vector2Field(UnityEngine.Rect position, Vector2 v, string name, int width)
        {
            UnityEngine.Rect r = position;
            if (!string.IsNullOrEmpty(name))
            {
                GUI.Label(r, name);
                r.x += name.Length * 16;
            }
            changed = false;
            r.width = width;
            r.height = 20;
            float x = FloatField(r, v.x);
            r.x += width;
            r.width = width;
            r.height = 20;
            float y = FloatField(r, v.x);
            if (changed)
            {
                float x_t, y_t;
                return new Vector2(x, y);
            }
            return v;
        }

        static public Vector2 Vector2Field(Vector2 v)
        {
            GUILayout.BeginHorizontal();
            changed = false;
            float x = FloatField(v.x);
            float y = FloatField(v.y);
            GUILayout.EndHorizontal();
            if (changed)
            {
                return new Vector2(x, y);
            }
            return v;
        }

        static public Vector2 Vector2Field(UnityEngine.Rect position, Vector2 v, int width)
        {

            GUILayout.BeginHorizontal();
            changed = false;
            float x = FloatField(position, v.x);
            position.x += width;
            float y = FloatField(position, v.y);
            GUILayout.EndHorizontal();
            if (changed)
            {
                return new Vector2(x, y);
            }
            return v;
        }

        static string s_float_cache_string;
        static string s_int_cache_string;
        static int s_last_editor_gui_id;

        static public string TextField(string v, string name, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(name))
            {
                GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            }
            string x = GUILayout.TextField(v);
            GUILayout.EndHorizontal();
            return x;
        }

        static public string TextField(string v, string name, int width)
        {
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(name))
            {
                GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            }
            string x = GUILayout.TextField(v, GUILayout.MinWidth(width));
            GUILayout.EndHorizontal();
            return x;
        }
        static public string TextField(string v, string name, int width, int height)
        {
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(name))
            {
                GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            }
            string x = GUILayout.TextField(v, GUILayout.MinWidth(width), GUILayout.MinHeight(height));
            GUILayout.EndHorizontal();
            return x;
        }

        static public float FloatField(float v, string name)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            float x = FloatField(v);
            GUILayout.EndHorizontal();
            return x;
        }

        static public float FloatField(float v)
        {
            GUI.changed = false;
            string x;
            int keyboardId = GUIUtility.GetControlID(FocusType.Keyboard);
            if (GUIUtility.keyboardControl - 1 == keyboardId)
            {
                if (s_last_editor_gui_id != GUIUtility.keyboardControl)
                {
                    s_float_cache_string = v.ToString();
                    s_last_editor_gui_id = GUIUtility.keyboardControl;
                }
                s_float_cache_string = GUILayout.TextField(s_float_cache_string);
                x = s_float_cache_string;
            }
            else
            {
                x = GUILayout.TextField(v.ToString());
            }

            if (GUI.changed)
            {
                changed = true;
                float x_t;
                float.TryParse(x, out x_t);
                return x_t;
            }
            return v;
        }


        static public float FloatField(float v, params GUILayoutOption[] options)
        {
            GUI.changed = false;
            string x;
            int keyboardId = GUIUtility.GetControlID(FocusType.Keyboard);
            if (GUIUtility.keyboardControl - 1 == keyboardId)
            {
                if (s_last_editor_gui_id != GUIUtility.keyboardControl)
                {
                    s_float_cache_string = v.ToString();
                    s_last_editor_gui_id = GUIUtility.keyboardControl;
                }
                s_float_cache_string = GUILayout.TextField(s_float_cache_string, options);
                x = s_float_cache_string;
            }
            else
            {
                x = GUILayout.TextField(v.ToString(), options);
            }

            if (GUI.changed)
            {
                changed = true;
                float x_t;
                float.TryParse(x, out x_t);
                return x_t;
            }
            return v;
        }
        static public float FloatField(UnityEngine.Rect position, float v)
        {
            GUI.changed = false;
            string x;
            int keyboardId = GUIUtility.GetControlID(FocusType.Keyboard);
            if (GUIUtility.keyboardControl - 1 == keyboardId)
            {
                if (s_last_editor_gui_id != GUIUtility.keyboardControl)
                {
                    s_float_cache_string = v.ToString();
                    s_last_editor_gui_id = GUIUtility.keyboardControl;
                }
                s_float_cache_string = GUI.TextField(position, s_float_cache_string);
                x = s_float_cache_string;
            }
            else
            {
                x = GUI.TextField(position, v.ToString());
            }

            if (GUI.changed)
            {
                changed = true;
                float x_t;
                float.TryParse(x, out x_t);
                return x_t;
            }
            return v;
        }

        static public int IntField(int v, params GUILayoutOption[] options)
        {
            GUI.changed = false;
            string x;
            int keyboardId = GUIUtility.GetControlID(FocusType.Keyboard);
            if (GUIUtility.keyboardControl - 1 == keyboardId)
            {
                if (s_last_editor_gui_id != GUIUtility.keyboardControl)
                {
                    s_int_cache_string = v.ToString();
                    s_last_editor_gui_id = GUIUtility.keyboardControl;
                }
                s_int_cache_string = GUILayout.TextField(s_int_cache_string, options);
                x = s_int_cache_string;
            }
            else
            {
                x = GUILayout.TextField(v.ToString(), options);
            }

            if (GUI.changed)
            {
                changed = true;
                int x_t;
                int.TryParse(x, out x_t);
                return x_t;
            }
            return v;
        }

        static public int IntField(int v, string name)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            int x = IntField(v);
            GUILayout.EndHorizontal();
            return x;
        }

        static public int IntField(int v, string name, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            int x = IntField(v, options);
            GUILayout.EndHorizontal();
            return x;
        }

        static public int IntField(int v, string name, int width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            int x = IntField(v, GUILayout.MinWidth(width));
            GUILayout.EndHorizontal();
            return x;
        }

        static public ulong ULongField(ulong v, params GUILayoutOption[] options)
        {
            GUI.changed = false;
            string x;
            int keyboardId = GUIUtility.GetControlID(FocusType.Keyboard);
            if (GUIUtility.keyboardControl - 1 == keyboardId)
            {
                if (s_last_editor_gui_id != GUIUtility.keyboardControl)
                {
                    s_int_cache_string = v.ToString();
                    s_last_editor_gui_id = GUIUtility.keyboardControl;
                }
                s_int_cache_string = GUILayout.TextField(s_int_cache_string, options);
                x = s_int_cache_string;
            }
            else
            {
                x = GUILayout.TextField(v.ToString(), options);
            }

            if (GUI.changed)
            {
                changed = true;
                ulong x_t;
                ulong.TryParse(x, out x_t);
                return x_t;
            }
            return v;
        }

        static public ulong ULongField(ulong v, string name)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            ulong x = ULongField(v);
            GUILayout.EndHorizontal();
            return x;
        }

        static public ulong ULongField(ulong v, string name, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            ulong x = ULongField(v, options);
            GUILayout.EndHorizontal();
            return x;
        }

        static public ulong ULongField(ulong v, string name, int width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            ulong x = ULongField(v, GUILayout.MinWidth(width));
            GUILayout.EndHorizontal();
            return x;
        }

        static public Texture2D CreateTex(Color color, int size)
        {
            Texture2D tex = new Texture2D(size, size);
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    tex.SetPixel(i, j, color);
                }
            }
            tex.Apply();
            return tex;
        }

        static Texture2D colorTex;
        static GUIContent colorContent;

        //创建 图片+文本 的GUIContent
        public static GUIContent CreateImgTextContent(string text, Color color, ref Texture2D saveTex, int size = 16)
        {
            if (saveTex == null)
            {

            }

            GUIContent guiContent = new GUIContent(text, saveTex);
            return guiContent;
        }
        static private ColorPicker picker;
        static public void ColorField(Color v, string name, UnityAction<Color> changeCall)
        {
            if (colorTex == null)
                colorTex = new Texture2D(16, 16);

            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    colorTex.SetPixel(i, j, v);
                }
            }
            colorTex.Apply();

            if (colorContent == null)
            {
                colorContent = new GUIContent();
            }
            colorContent.text = name;
            colorContent.image = colorTex;

            if (GUILayout.Button(colorContent))
            {
                if (picker == null)
                {
                    GameObject obj = GameObject.Instantiate(Resources.Load("Picker")) as GameObject;
                    if (obj != null)
                    {
                        picker = obj.GetComponentInChildren<ColorPicker>(true);
                        picker.gameObject.SetActive(false);
                    }
                }

                if (picker != null)
                {
                    if (picker.gameObject.activeInHierarchy)
                    {
                        picker.gameObject.SetActive(false);
                    }
                    else
                    {
                        picker.gameObject.SetActive(true);
                        picker.onValueChanged.RemoveAllListeners();
                        picker.onValueChanged.AddListener(changeCall);
                    }

                }

            }
        }
        public static string lastOpenFilePath;
        public static void OpenTexture(string filter, object customData, Action<string, UnityEngine.Object, object> call)
        {
            if (string.IsNullOrEmpty(lastOpenFilePath))
                lastOpenFilePath = Path.ContentRootPath;

            string[] path = WindowDialog.OpenFileDialog("贴图", System.IO.Path.GetDirectoryName(lastOpenFilePath), filter);
            if (path != null)
            {
                string fName = path[0];
                lastOpenFilePath = fName;
                Loader.TextureLoader.LoadFromFile(fName, customData, (UnityEngine.Object obj, object customData) =>
                {
                    call.Invoke(fName, obj, customData);
                });
            }
        }


        /// <summary>
        /// 打开popup的选择界面
        /// </summary>
        public class CustomPopup : EditorWindow
        {
            public int select;
            public string[] displayedOptions;
            public bool hasopen;
            public string filter;
            public CustomPopupInfo info;

            public Vector2 scrollPosition;

            public void ShowGUI()
            {

                if (Input.GetMouseButtonDown(0))
                {
                    Vector3 mousePosition = Input.mousePosition;
                    mousePosition.y = Screen.height - mousePosition.y;
                    bool b = windowRect.Contains(mousePosition);
                    if (!b)
                    {
                        EditorWindow.RemoveWindow(this);
                        return;
                    }
                }
                GUILayout.Label("搜索：");
                filter = GUILayout.TextField(filter);
                GUILayout.Space(20);

                scrollPosition = GUILayout.BeginScrollView(scrollPosition);
                for (int i = 0; i < displayedOptions.Length; i++)
                {
                    string info = displayedOptions[i];

                    if (this.filter != null && this.filter.Length != 0)
                    {
                        if (!info.Contains(this.filter))
                        {
                            continue;
                        }
                    }

                    if (select == i)
                    {
                        info = "--->" + info;
                    }

                    if (GUILayout.Button(info))
                    {
                        select = i;
                        this.info.Set(i);
                        EditorWindow.RemoveWindow(this);
                    }
                }
                GUILayout.EndScrollView();
            }
        }


        /// <summary>
        /// 自定义Popup的Style缓存可以有多个参数，不止是Rect，也可以自定义其他的
        /// </summary>
        public class CustomPopupTempStyle
        {

            public UnityEngine.Rect rect;

            static Dictionary<int, CustomPopupTempStyle> temp = new();

            public static CustomPopupTempStyle Get(int contrelId)
            {
                if (!temp.ContainsKey(contrelId))
                {
                    return null;
                }
                CustomPopupTempStyle t;
                temp.Remove(contrelId, out t);
                return t;
            }

            public static void Set(int contrelId, CustomPopupTempStyle style)
            {
                temp[contrelId] = style;
            }
        }

        /// <summary>
        /// 存储popup的信息如选择等
        /// </summary>
        public class CustomPopupInfo
        {
            public int SelectIndex { get; private set; }
            public int contrelId;
            public bool used;
            public static CustomPopupInfo instance;

            public CustomPopupInfo(int contrelId, int selectIndex)
            {
                this.contrelId = contrelId;
                this.SelectIndex = selectIndex;
            }

            public static int Get(int controlID, int selected)
            {
                if (instance == null)
                {
                    return selected;
                }

                if (instance.contrelId == controlID && instance.used)
                {
                    GUI.changed = selected != instance.SelectIndex;
                    selected = instance.SelectIndex;
                    instance = null;
                }

                return selected;
            }

            public void Set(int selected)
            {
                SelectIndex = selected;
                used = true;
            }
        }

        static public int Popup(int selectIndex, string[] displayedOptions, params GUILayoutOption[] options)
        {
            if (displayedOptions == null || displayedOptions.Length == 0)
                return 0;

            int contrelId = GUIUtility.GetControlID(FocusType.Passive);

            string display = "（空）";

            if (selectIndex >= 0 && selectIndex < displayedOptions.Length)
                display = displayedOptions[selectIndex];

            if (GUILayout.Button(display, options))
            {
                Vector3 mousePosition = Input.mousePosition;
                mousePosition.y = Screen.height - mousePosition.y;

                UnityEngine.Rect rect = CustomPopupTempStyle.Get(contrelId).rect;
                if (mousePosition.x + 100 > Screen.width)
                    rect.x = Screen.width - 200;
                else
                    rect.x = mousePosition.x - 100;
                if (mousePosition.y + 400 > Screen.height)
                    rect.y = Screen.height - 400;
                else
                    rect.y = mousePosition.y;
                rect.width = 200;
                rect.height = 400;
                CustomPopup popup = EditorWindow.AddWindow<CustomPopup>(9999999, rect, DrawPopupWindow, "", Resources.Load<GUISkin>("GUISkin/PopupPanel")) as CustomPopup;
                if (popup != null)
                {
                    popup.canClose = false;
                    popup.dragable = false;
                    popup.minmaxable = false;
                    popup.select = selectIndex;
                    popup.displayedOptions = displayedOptions;
                    popup.info = new CustomPopupInfo(contrelId, selectIndex);
                    CustomPopupInfo.instance = popup.info;
                }
            }

            if (UnityEngine.Event.current.type == EventType.Repaint)
            {
                CustomPopupTempStyle style = new CustomPopupTempStyle();
                style.rect = GUILayoutUtility.GetLastRect();
                CustomPopupTempStyle.Set(contrelId, style);
            }

            return CustomPopupInfo.Get(contrelId, selectIndex);
        }

        static void DrawPopupWindow(int windowID, EditorWindow window)
        {
            CustomPopup popup = window as CustomPopup;
            if (popup != null)
            {
                popup.ShowGUI();
            }
        }

        static public int Popup(string name, int selectIndex, string[] displayedOptions, int width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.MinWidth(name.Length * GUI.skin.font.fontSize + 10));
            int x = Popup(selectIndex, displayedOptions, GUILayout.MinWidth(width));
            GUILayout.EndHorizontal();
            return x;
        }



    }
}
