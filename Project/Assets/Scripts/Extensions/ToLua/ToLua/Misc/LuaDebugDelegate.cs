using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

public class LuaDebugDelegate
{
    public delegate void handler(IntPtr luastate);
    static ArrayList mHandlers = new ArrayList();
    public static void addHander(handler h)
    {
        mHandlers.Add(h);
    }
    public static void OnLuaStateInited(IntPtr luastate)
    {
        foreach (handler handle in mHandlers)
        {
            handle(luastate);
        }
    }

}
