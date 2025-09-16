/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
namespace Sango
{
    /// <summary>
    /// 游戏容器接口,作为游戏脚本的中间载体
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// 是否挂载有脚本
        /// </summary>
        /// <returns></returns>
        bool HasScript();

        /// <summary>
        /// 初始化脚本
        /// </summary>
        /// <param name="table"></param>
        void AttachScript(LuaInterface.LuaTable table, bool callawake);

        /// <summary>
        /// 获取方法
        /// </summary>
        /// <param name="methodName"></param>
        /// <returns></returns>
        LuaInterface.LuaFunction GetFunction(string methodName);

        /// <summary>
        /// 获取表
        /// </summary>
        /// <returns></returns>
        LuaInterface.LuaTable GetTable();

        /// <summary>
        /// 解除连接
        /// </summary>
        /// <param name="table"></param>
        /// <param name="callawake"></param>
        void DetachScript(bool callDestroy = true);

    }
}