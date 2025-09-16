/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
namespace Sango
{
    /// <summary>
    /// 游戏模块接口
    /// </summary>
    public interface IObject
    {
        /// <summary>
        /// 获取模块名字
        /// </summary>
        /// <returns></returns>
        string GetName();

        /// <summary>
        /// 获取模块类型
        /// </summary>
        /// <returns></returns>
        int GetXType();

        /// <summary>
        /// 更新回调
        /// </summary>
        /// <param name="dtTime"></param>
        void OnUpdate(float dtTime, float unscaleDtTime);

        /// <summary>
        /// 后更新回调
        /// </summary>
        /// <param name="dtTime"></param>
        void OnLateUpdate(float dtTime, float unscaleDtTime);

        /// <summary>
        /// 向游戏模块容器注册,并赋予模块名字
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        bool OnRegister(string name);

        /// <summary>
        /// 反注册,即移除出游戏模块容器
        /// </summary>
        void OnUnregister();

        /// <summary>
        /// 收到网络消息处理
        /// </summary>
        /// <param name="gMsg"></param>
        /// <returns></returns>
        void OnMessage(params object[] parms);

        /// <summary>
        /// 重置
        /// </summary>
        void OnReset();

        /// <summary>
        /// 删除物件
        /// </summary>
        void DeleteObject();

        /// <summary>
        /// 是否移除
        /// </summary>
        /// <returns></returns>
        bool IsRemoved();

    }
}