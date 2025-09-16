/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using System.Collections.Generic;
using UnityEngine;
namespace Sango
{
    /// <summary>
    /// 从android或者ios回调监听器 名字:Platform Listener
    /// 在原生代码中调用unity.sendMessage到此对象, 回调onMobileResult函数
    /// </summary>
    public class PlatformListener : Behaviour
    {
        [LuaInterface.NoToLua]
        public const string LISTENER_NAME = "Platform Listener";

        public delegate void PlatformCall(string code);
        private static Dictionary<int, PlatformCall> mDelegates = new Dictionary<int, PlatformCall>();

        [LuaInterface.NoToLua]
        public static void Init()
        {
            GameObject obj = new GameObject(LISTENER_NAME);
            GameObject.DontDestroyOnLoad(obj);
            obj.AddComponent<PlatformListener>();
        }

        void OnPlatformFuncReceive(string funcStr)
        {
            int funcIndex = funcStr.IndexOf(";");
            if (funcIndex != -1)
            {
                string funcCode = funcStr.Substring(0, funcIndex);
                int code = int.Parse(funcCode);
                if( code > 0 )
                {
                    PlatformCall del;
                    if (mDelegates.TryGetValue(code, out del)){
                        string jsonStr = funcStr.Substring(funcIndex + 1);
                        del(jsonStr);
                    }
                }
            }
        }

        public static void AddListener(int funcCode, PlatformCall call)
        {
            if (!mDelegates.ContainsKey(funcCode))
                mDelegates.Add(funcCode, call);
            else
                mDelegates[funcCode] = call;
        }

    }
}
