/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using LuaInterface;
using System;
using UnityEngine;
namespace Sango
{

    /// <summary>
    /// 游戏容器衔接类, 负责衔接功能容器与脚本
    /// </summary>
    public class Behaviour : MonoBehaviour, IModule
    {
        /// <summary>
        /// 脚本数据
        /// </summary>
        protected LuaTable mScriptTable;
        protected LuaFunction mAwakeFunction;
        protected LuaFunction mStartFunction;
        protected LuaFunction mDestroyFunction;
        protected LuaFunction mEnableFunction;
        protected LuaFunction mDisableFunction;
        protected LuaFunction mTriggerEnterFunction;
        protected LuaFunction mTriggerExitFunction;

        /// <summary>
        /// 给一个GameObject挂接一个脚本对象
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="table"></param>
        public static Behaviour Link(GameObject obj, LuaTable table, bool callawake = true)
        {
            Behaviour b = obj.GetComponent<Behaviour>();
            if (b == null)
                b = obj.AddComponent<Behaviour>();
            b.AttachScript(table, callawake);
            return b;
        }

        /// <summary>
        /// 给一个GameObject挂接一个脚本对象
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="table"></param>
        public static Behaviour Link(GameObject obj, LuaTable table, Type t, bool callawake = true)
        {
            Behaviour b = obj.GetComponent(t) as Behaviour;
            if (b == null) {
                if (!t.IsSubclassOf(typeof(Behaviour)))
                    return null;
                b = obj.AddComponent(t) as Behaviour;
            }
            b.AttachScript(table, callawake);
            return b;
        }

        #region Module

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
            mAwakeFunction = GetFunction(Config.AWAKE_REF);
            mStartFunction = GetFunction(Config.START_REF);
            mDestroyFunction = GetFunction(Config.DESTROY_REF);
            mEnableFunction = GetFunction(Config.ENABLE_REF);
            mDisableFunction = GetFunction(Config.DISABLE_REF);
            mTriggerEnterFunction = GetFunction(Config.TRIGGERENTER_REF);
            mTriggerExitFunction = GetFunction(Config.TRIGGEREXIT_REF);
        }

        /// <summary>
        /// 挂接脚本
        /// </summary>
        /// <param name="table">lua table</param>
        /// <param name="callawake">是否调用awake函数</param>
        public virtual void AttachScript(LuaTable table, bool callawake = true)
        {
            bool isReAttach = (mScriptTable != null && mScriptTable == table);
            if (isReAttach) {
                if (callawake)
                    CallMethod(mAwakeFunction);
                return;
            }

            // 清除以前的脚本
            if (mScriptTable != null) {
                CallMethod(mDestroyFunction);
                mScriptTable[Config.SCRIPT_BEHAVIOUR_NAME] = null;
            }

            if (table == null) return;

            mScriptTable = table;
            mScriptTable[Config.SCRIPT_BEHAVIOUR_NAME] = this;

            OnInitFunctions();

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

            mScriptTable[Config.SCRIPT_BEHAVIOUR_NAME] = null;

            SangoLuaClient.SafeRelease(ref mDestroyFunction);
            SangoLuaClient.SafeRelease(ref mAwakeFunction);
            SangoLuaClient.SafeRelease(ref mStartFunction);
            SangoLuaClient.SafeRelease(ref mDestroyFunction);
            SangoLuaClient.SafeRelease(ref mEnableFunction);
            SangoLuaClient.SafeRelease(ref mDisableFunction);
            SangoLuaClient.SafeRelease(ref mTriggerEnterFunction);
            SangoLuaClient.SafeRelease(ref mTriggerExitFunction);
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

        #region Unity回调函数

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

        protected virtual void Start()
        {
            CallMethod(mStartFunction);
        }

        protected virtual void OnDestroy()
        {
            CallMethod(mDestroyFunction);
            if (mScriptTable != null)
                mScriptTable[Config.SCRIPT_BEHAVIOUR_NAME] = null;

            SangoLuaClient.SafeRelease(ref mAwakeFunction);
            SangoLuaClient.SafeRelease(ref mStartFunction);
            SangoLuaClient.SafeRelease(ref mDestroyFunction);
            SangoLuaClient.SafeRelease(ref mEnableFunction);
            SangoLuaClient.SafeRelease(ref mDisableFunction);
            SangoLuaClient.SafeRelease(ref mTriggerEnterFunction);
            SangoLuaClient.SafeRelease(ref mTriggerExitFunction);
            SangoLuaClient.SafeRelease(ref mScriptTable);
        }

        protected virtual void OnEnable()
        {
            // 编辑器 更新数据信息
            CallMethod(mEnableFunction);
        }

        protected virtual void OnDisable()
        {
            CallMethod(mDisableFunction);
        }

        // U3D碰撞相关
        protected virtual void OnTriggerEnter(Collider other)
        {
            CallMethod(mTriggerEnterFunction, other.gameObject);
        }

        // U3D碰撞相关
        protected virtual void OnTriggerExit(Collider other)
        {
            CallMethod(mTriggerExitFunction, other.gameObject);
        }

        #endregion // Unity回调函数

    }

}