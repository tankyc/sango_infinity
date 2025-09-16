/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using LuaInterface;
namespace Sango
{
    /// <summary>
    /// 游戏容器接口,CSharp端,可作为游戏资源与游戏脚本的中间载体
    /// 如果想直接链接脚本与资源请参照XBehaviour的link方法
    /// </summary>
    public class Module : IModule
    {
        /// <summary>
        /// 脚本数据
        /// </summary>
        protected LuaInterface.LuaTable mScriptTable;
        protected LuaFunction mAwakeFunction;
        protected LuaFunction mDestroyFunction;
        #region 脚本接口

        /// <summary>
        /// 是否包含脚本
        /// </summary>
        public bool HasScript()
        {
            return mScriptTable != null;
        }

        protected virtual void OnInitFunctions()
        {
            mAwakeFunction = GetFunction(Config.AWAKE_REF);
            mDestroyFunction = GetFunction(Config.DESTROY_REF);
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
        /// 获取所绑定LuaTable
        /// </summary>
        /// <returns>LuaTable</returns>
        public virtual LuaTable GetTable()
        {
            return mScriptTable;
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

            mScriptTable[Config.SCRIPT_CONTAINER_NAME] = null;

            SangoLuaClient.SafeRelease(ref mAwakeFunction);
            SangoLuaClient.SafeRelease(ref mDestroyFunction);
            SangoLuaClient.SafeRelease(ref mScriptTable);
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

        #endregion

    }

}