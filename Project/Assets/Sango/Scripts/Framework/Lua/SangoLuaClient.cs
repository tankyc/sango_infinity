/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using UnityEngine;
using LuaInterface;
using System;

namespace Sango
{
    [NoToLua]
    public class SangoLuaClient : LuaClient
    {
        [NoToLua]
        public delegate void CallBeforMain();
        [NoToLua]
        public static CallBeforMain DoSomethingCallBeforMain;
        [NoToLua]
        public static void CallMethod(LuaFunction func, LuaTable table)
        {
            if (func == null) return;
            func.BeginPCall();
            if (table != null)
                func.Push(table);
            func.PCall();
            func.EndPCall();
        }
        [NoToLua]
        public static void CallMethod(LuaFunction func, LuaTable table, params object[] parms)
        {
            if (func == null) return;
            func.BeginPCall();
            if (table != null)
                func.Push(table);
            func.PushArgs(parms);
            func.PCall();
            func.EndPCall();
        }
        [NoToLua]
        public static void SafeRelease(ref LuaFunction func)
        {
            if (func != null)
            {
                func.Dispose();
                func = null;
            }
        }
        [NoToLua]
        public static void SafeRelease(ref LuaTable table)
        {
            if (table != null)
            {
                table.Dispose();
                table = null;
            }
        }

        LuaFunction messageFunction = null;
        LuaFunction onLowMemoryFunction = null;
        public static ScriptsLoaderBase scriptLoader;

        [NoToLua]
        public static void SetUseNativeInt64(bool b)
        {
            LuaDLL.tolua_setflag(ToLuaFlags.USE_INT64, b);
        }

        protected virtual void OnLowMemory()
        {
            if (onLowMemoryFunction == null)
                onLowMemoryFunction = luaState.GetFunction("OnLowMemory");
            if (onLowMemoryFunction != null)
            {
                onLowMemoryFunction.BeginPCall();
                onLowMemoryFunction.PCall();
                onLowMemoryFunction.EndPCall();
            }
        }

        protected new void Awake()
        {
            Instance = this;
            Init();

#if UNITY_5_6_OR_NEWER
            Application.lowMemory += OnLowMemory;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
#endif
        }

        protected new void OnDestroy()
        {
            if (messageFunction != null)
            {
                messageFunction.Dispose();
                messageFunction = null;
            }
            Destroy();
        }

        protected new void OnApplicationQuit()
        {
            if (messageFunction != null)
            {
                messageFunction.Dispose();
                messageFunction = null;
            }
            Destroy();
        }

        protected override void OnLoadFinished()
        {
            luaState.Start();
            StartLooper();
            if (DoSomethingCallBeforMain != null)
                DoSomethingCallBeforMain();
            StartMain();
        }

        [NoToLua]
        public void Reset()
        {
            StartMain();
        }

        protected override LuaFileUtils InitLoader()
        {
            if (scriptLoader == null)
            {
                scriptLoader = ScriptConfig.Instance;
                scriptLoader.Init();
            }
            return scriptLoader;
        }

        [NoToLua]
        public virtual void PostMessage(params object[] obj)
        {
            if (messageFunction == null && luaState != null)
                messageFunction = luaState.GetFunction("OnCSharpMessage");
            if (messageFunction != null)
            {
                messageFunction.BeginPCall();
                for (int i = 0; i < obj.Length; ++i)
                    messageFunction.Push(obj[i]);
                messageFunction.PCall();
                messageFunction.EndPCall();
            }
        }

        [NoToLua]
        public static IntPtr GetL()
        {
            return GetMainState().GetL();
        }
        public static void ShowStack(string message)
        {
            LuaDLL.luaL_throw(GetMainState().GetL(), message);
        }
        public static string StackMsg(string message)
        {
            if (Instance == null) return message;
            LuaState state = GetMainState();
            if (state == null) return message;
            IntPtr l = state.GetL();
            if (l == null) return message;
            return LuaDLL.stackMessage(l, message);
        }

        [NoToLua]
        public static void DoFile(string fileName)
        {
            GetMainState().DoFile(fileName);
        }

        [NoToLua]
        public static T DoFile<T>(string fileName)
        {
            return GetMainState().DoFile<T>(fileName);
        }

        [NoToLua]
        public static void Require(string fileName)
        {
            GetMainState().Require(fileName);
        }

        [NoToLua]
        public static T Require<T>(string fileName)
        {
            return GetMainState().Require<T>(fileName);
        }

        [NoToLua]
        public static void DoString(string chunk, string chunkName = "LuaState.cs")
        {
            GetMainState().DoString(chunk, chunkName);
        }

        [NoToLua]
        public static T DoString<T>(string chunk, string chunkName = "LuaState.cs")
        {
            return GetMainState().DoString<T>(chunk, chunkName);
        }

        public void Reload()
        {
            if (scriptLoader != null)
                scriptLoader.Reload();
        }

        public void Clear()
        {
            if (scriptLoader != null)
                scriptLoader.Clear();
        }
    }
}