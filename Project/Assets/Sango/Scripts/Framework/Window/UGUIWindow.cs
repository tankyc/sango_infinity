using LuaInterface;
using Sango.Loader;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Sango
{

    public class UGUIWindow : MonoBehaviour
    {
        protected UnityEngine.Canvas[] panels;

        protected LuaTable mScriptTable;
        protected LuaFunction mAwakeFunction;
        protected LuaFunction mStartFunction;
        protected LuaFunction mDestroyFunction;
        protected LuaFunction mEnableFunction;
        protected LuaFunction mDisableFunction;
        #region Module


        protected virtual void Awake()
        {
            //string tableName = gameObject.name;
            //if (tableName.Contains("(Clone)"))
            //    tableName = tableName.Substring(0, tableName.Length - 7);
            //Sango.Log.Error(tableName);
            //// aa_bb_cc
            //LuaTable luaTable = LuaClient.GetTable(tableName);
            //if (luaTable == null)
            //{
            //    string[] parts = tableName.Split("_");
            //    StringBuilder stringBuffer = new StringBuilder();
            //    for (int i = 0; i < parts.Length; i++)
            //    {
            //        string dest = parts[i];
            //        stringBuffer.Append(dest.Substring(0, 1).ToUpper());
            //        stringBuffer.Append(dest.Substring(1).ToUpper());
            //        if (i < parts.Length - 1)
            //            stringBuffer.Append("_");
            //    }
            //    string tableName1 = stringBuffer.ToString();
            //    // Aa_Bb_Cc
            //    luaTable = LuaClient.GetTable(tableName1);
            //    if (luaTable == null)
            //    {
            //        stringBuffer.Clear();
            //        for (int i = 0; i < parts.Length; i++)
            //        {
            //            string dest = parts[i];
            //            stringBuffer.Append(dest.Substring(0, 1).ToUpper());
            //            stringBuffer.Append(dest.Substring(1).ToUpper());
            //        }
            //        string tableName2= stringBuffer.ToString();
            //        // AaBbCc
            //        luaTable = LuaClient.GetTable(tableName2);
            //        if (luaTable == null)
            //        {
            //            Sango.Log.Error($"Can not find window scripts in global table named : {tableName} or {tableName1} or {tableName2}");
            //        }
            //    }
            //}

            //if (luaTable == null)
            //{
            //    BindButton();
            //    BindToggle();
            //    BindScrollRect();
            //    AttachScript(luaTable);
            //}

        }
        [NoToLua]
        /// <summary>
        /// 是否包含脚本
        /// </summary>
        public bool HasScript()
        {
            return mScriptTable != null;
        }
        /// <summary>
        /// 可继承的初始化缓存脚本方法
        /// </summary>
        protected virtual void OnInitFunctions()
        {
            mAwakeFunction = GetFunction("Awake");
            mStartFunction = GetFunction("Start");
            mDestroyFunction = GetFunction("OnDestroy");
            mEnableFunction = GetFunction("OnEnable");
            mDisableFunction = GetFunction("OnDisable");
        }

        /// <summary>
        /// 挂接脚本
        /// </summary>
        /// <param name="table">lua table</param>
        /// <param name="callawake">是否调用awake函数</param>
        public virtual void AttachScript(LuaTable table, bool callawake = true)
        {
            bool isReAttach = (mScriptTable != null && mScriptTable == table);
            if (isReAttach)
            {
                if (callawake)
                    CallMethod(mAwakeFunction);
                return;
            }

            // 清除以前的脚本
            if (mScriptTable != null)
            {
                CallMethod(mDestroyFunction);
                mScriptTable["Behaviour"] = null;
            }

            if (table == null) return;

            mScriptTable = table;
            mScriptTable["Behaviour"] = this;

            OnInitFunctions();

            BindButton();
            BindToggle();
            BindScrollRect();

            if (callawake)
                CallMethod(mAwakeFunction);
        }

        /// <summary>
        /// 解除连接
        /// </summary>
        /// <param name="table"></param>
        /// <param name="callawake"></param>
        public virtual void DetachScript(bool callDestroy = true)
        {
            if (mScriptTable == null) return;
            // 清除以前的脚本
            if (callDestroy)
                CallMethod(mDestroyFunction);

            mScriptTable["Behaviour"] = null;

            SangoLuaClient.SafeRelease(ref mDestroyFunction);
            SangoLuaClient.SafeRelease(ref mAwakeFunction);
            SangoLuaClient.SafeRelease(ref mStartFunction);
            SangoLuaClient.SafeRelease(ref mDestroyFunction);
            SangoLuaClient.SafeRelease(ref mEnableFunction);
            SangoLuaClient.SafeRelease(ref mDisableFunction);
            SangoLuaClient.SafeRelease(ref mScriptTable);
        }

        /// <summary>
        /// 获取关联的脚本的方法
        /// </summary>
        /// <param name="methodName">方法名字</param>
        /// <returns>方法引用</returns>
        [NoToLua]
        public LuaFunction GetFunction(string methodName)
        {
            if (mScriptTable == null) return null;
            return mScriptTable.GetLuaFunction(methodName);
        }
        [NoToLua]
        public bool CallFunction(string methodName)
        {
            LuaFunction call = GetFunction(methodName);
            if (call == null) return false;
            CallMethod(call);
            return true;
        }
        [NoToLua]
        public bool CallFunction(string methodName, params object[] ps)
        {
            LuaFunction call = GetFunction(methodName);
            if (call == null) return false;
            CallMethod(call, ps);
            return true;
        }
        [NoToLua]
        /// <summary>
        /// 获取所绑定LuaTable
        /// </summary>
        /// <returns>LuaTable</returns>
        public virtual LuaTable GetTable()
        {
            return mScriptTable;
        }
        #endregion //XModule
        /// <summary>
        /// 获取对象, 传入对象路径
        /// </summary>
        /// <param name="namePath"> e.g: (root/)UI/nameLabel</param>
        /// <returns>找到的Transform</returns>
        public Transform GetTransform(string namePath)
        {
            Transform rs = transform.Find(namePath);
            if (rs == null && Config.isDebug)
                Log.Warning("在 " + gameObject.name + " 中无法找到节点:" + namePath);
            return rs;
        }
        /// <summary>
        /// 获取对象, 传入对象路径
        /// </summary>
        /// <param name="namePath"> e.g: (root/)UI/nameLabel</param>
        /// <returns>找到的GameObject</returns>
        public GameObject GetObject(string namePath)
        {
            Transform trans = GetTransform(namePath);
            if (trans != null)
                return trans.gameObject;
            return null;
        }
        /// <summary>
        /// 获取对象上的T接口
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="namePath">e.g: (root/)UI/nameLabel</param>
        /// <returns>T接口</returns>
        public T GetComponent<T>(string namePath) where T : Component
        {
            Transform trans = GetTransform(namePath);
            if (trans != null)
                return trans.GetComponent<T>();
            return null;
        }
        /// <summary>
        /// 获取对象上的接口,传入接口type
        /// </summary>
        /// <param name="namePath">e.g: (root/)UI/nameLabel</param>
        /// <param name="typeName">类型字符串名字</param>
        /// <returns>接口</returns>
        [NoToLua]
        public Component GetComponent(string namePath, string typeName)
        {
            Transform trans = GetTransform(namePath);
            if (trans != null)
                return trans.GetComponent(typeName);
            return null;
        }
        public Component GetComponent(string namePath, Type t)
        {
            Transform trans = GetTransform(namePath);
            if (trans != null)
                return trans.GetComponent(t);
            return null;
        }
        protected void CallMethod(LuaFunction func)
        {
            if (func != null)
                SangoLuaClient.CallMethod(func, mScriptTable);
        }
        protected void CallMethod(LuaFunction func, params object[] ps)
        {
            if (func != null)
                SangoLuaClient.CallMethod(func, mScriptTable, ps);
        }

        protected virtual void OnDestroy()
        {
            CallMethod(mDestroyFunction);
            if (mScriptTable != null)
                mScriptTable["Behaviour"] = null;

            SangoLuaClient.SafeRelease(ref mAwakeFunction);
            SangoLuaClient.SafeRelease(ref mStartFunction);
            SangoLuaClient.SafeRelease(ref mDestroyFunction);
            SangoLuaClient.SafeRelease(ref mEnableFunction);
            SangoLuaClient.SafeRelease(ref mDisableFunction);
            SangoLuaClient.SafeRelease(ref mScriptTable);
        }
        private void OnButtonCall(Button btn)
        {
            string[] p = btn.name.Split('#');
            if (p.Length > 1)
            {
                int dataIndex = 0;
                int.TryParse(p[1], out dataIndex);
                CallFunction($"OnButton_{p[0]}", dataIndex);
            }
            else
            {
                CallFunction($"OnButton_{p[0]}");
            }
        }
        private void OnToggleValueChanged(Toggle toggle, bool isOn)
        {
            string[] p = toggle.name.Split('#');
            if (p.Length > 1)
            {
                int dataIndex = 0;
                int.TryParse(p[1], out dataIndex);
                CallFunction($"OnToggle_{p[0]}", isOn, dataIndex);
            }
            else
            {
                CallFunction($"OnToggle_{p[0]}", isOn);
            }
        }
        private void OnScrollRectValueChanged(Vector2 pos, ScrollRect scroll)
        {
            string[] p = scroll.name.Split('#');
            if (p.Length > 1)
            {
                int dataIndex = 0;
                int.TryParse(p[1], out dataIndex);
                CallFunction($"OnToggle_{p[0]}", pos, dataIndex);
            }
            else
            {
                CallFunction($"OnToggle_{p[0]}", pos);
            }
        }
        public void BindButton()
        {
            // 按钮函数绑定
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                button.onClick.AddListener(() =>
                {
                    OnButtonCall(button);
                });
            }
        }
        public void BindToggle()
        {
            // Toggle事件设置
            Toggle[] togs = GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < togs.Length; i++)
            {
                Toggle tog = togs[i];
                tog.onValueChanged.AddListener((b) =>
                {
                    OnToggleValueChanged(tog, b);
                });
            }
        }
        public void BindScrollRect()
        {
            // ScrollRect事件设置
            ScrollRect[] rects = GetComponentsInChildren<ScrollRect>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                ScrollRect scroll = rects[i];

                scroll.onValueChanged.AddListener((v2) =>
                {
                    OnScrollRectValueChanged(v2, scroll);
                });
            }
        }

        /// <summary>
        /// 设置canvas的layer和order
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="order"></param>
        public void SetLayerAndOrder(int layer, int order)
        {
            if (panels == null)
                panels = GetComponentsInChildren<UnityEngine.Canvas>(true);

            if (panels == null || panels.Length == 0)
                return;

            int offset = order - panels[0].sortingOrder;
            for (int i = 0, count = panels.Length; i < count; ++i)
            {
                Canvas p = panels[i];
                if (p != null)
                {
                    p.overrideSorting = true;
                    p.sortingLayerID = layer;
                    p.sortingOrder += offset;
                }
            }
        }
        public void SetLayerNameAndOrder(string layer, int order)
        {
            if (panels == null)
                panels = GetComponentsInChildren<UnityEngine.Canvas>(true);

            if (panels == null || panels.Length == 0)
                return;

            int offset = order - panels[0].sortingOrder;
            for (int i = 0, count = panels.Length; i < count; ++i)
            {
                Canvas p = panels[i];
                if (p != null)
                {
                    p.overrideSorting = true;
                    p.sortingLayerName = layer;
                    p.sortingOrder += offset;
                }
            }
        }

        public virtual void Show()
        {
            if (!this.gameObject.activeInHierarchy)
            {
                this.gameObject.SetActive(true);
            }
            CallFunction("OnShow");
        }

        public virtual void Hide()
        {
            if (this.gameObject.activeInHierarchy)
            {
                this.gameObject.SetActive(false);
            }
            CallFunction("OnHide");
        }

        public void SetText(string path, string content)
        {
            Text text = GetComponent<Text>(path);
            if (text != null)
            { text.text = content; }
        }

        public void SetTexture(string path, string texturePath)
        {
            RawImage rawImage = GetComponent<RawImage>(path);
            if (rawImage != null)
            {
                rawImage.texture = Loader.ObjectLoader.LoadObject<Texture>(texturePath, true, false);
            }
        }

        public void SetImage(string path, string spritePath)
        {
            Image Image = GetComponent<Image>(path);
            if (Image != null)
            {
                Image.sprite = Loader.ObjectLoader.LoadObject<UnityEngine.Sprite>(spritePath);
            }
        }
    }
}
