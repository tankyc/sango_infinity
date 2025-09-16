/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using LuaInterface;
using System.Collections.Generic;
using UnityEngine;

namespace Sango
{
    /// <summary>
    /// XEngine脚本管理器
    /// 
    /// </summary>
    public class Scripts : Singletion<Scripts>
    {
        protected SangoLuaClient mScriptHandle;

        /// <summary>
        /// 解析lua文件
        /// </summary>
        /// <param name="path">文件相对路劲</param>
        /// <returns>解析结果返回值</returns>
        public virtual void DoFile(string path) { SangoLuaClient.DoFile(path); }
        public virtual T DoFile<T>(string path) { return SangoLuaClient.DoFile<T>(path); }

        /// <summary>
        /// 解析luaString
        /// </summary>
        /// <param name="content">字符串内容</param>
        /// <returns>解析结果返回值</returns>
        public virtual void DoString(string content) { SangoLuaClient.DoString(content); }
        public virtual T DoString<T>(string content) { return SangoLuaClient.DoString<T>(content); }

        public virtual void PostMessage(params object[] obj)
        {
            if (mScriptHandle != null) mScriptHandle.PostMessage(obj);
        }

        /// <summary>
        /// 消息处理队列
        /// </summary>
        private Queue<object[]> mMsgQueue = new Queue<object[]>();


        public virtual void PostMessageToMainThread(params object[] obj)
        {
            if (mScriptHandle != null)
            {
                lock (mMsgQueue)
                {
                    mMsgQueue.Enqueue(obj);
                }
            }
        }

        public void OnUpdate(float dtTime, float unscaleDtTime)
        {
            if (mScriptHandle != null)
            {
                lock (mMsgQueue)
                {
                    int count = mMsgQueue.Count;
                    for (int i = 0; i < count; ++i)
                    {
                        object[] obj = mMsgQueue.Dequeue();
                        if (obj != null)
                        {
                            try
                            {
                                mScriptHandle.PostMessage(obj);
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogError(e.Message);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <returns></returns>
        public virtual bool Init()
        {
            mScriptHandle = new GameObject("script").AddComponent<SangoLuaClient>();
            GameObject.DontDestroyOnLoad(mScriptHandle.gameObject);
            return true;
        }

        /// <summary>
        /// 获取表
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        public LuaTable GetTable(string fullPath)
        {
            LuaInterface.LuaState state = LuaClient.GetMainState();
            if (state == null)
                return null;
            LuaInterface.LuaTable table = state.GetTable(fullPath);
            if (table == null)
                return null;
            return table;
        }

        /// <summary>
        /// 获取方法
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        public LuaFunction GetFunction(string fullPath)
        {
            LuaInterface.LuaState state = LuaClient.GetMainState();
            if (state == null)
                return null;
            LuaInterface.LuaFunction func = state.GetFunction(fullPath);
            if (func == null)
                return null;
            return func;
        }

        /// <summary>
        /// 重载
        /// </summary>
        /// <returns></returns>
        public virtual bool Reload()
        {
            mScriptHandle.Reload();
            return true;
        }

        /// <summary>
        /// 清理状态
        /// </summary>
        public virtual void Clear()
        {
            mScriptHandle.Clear();
        }

        [LuaInterface.LuaByteBuffer]
        public static byte[] LoadFile(string fileName)
        {
            return LuaFileUtils.Instance.ReadFile(fileName);
        }

        
    }
}