namespace Sango.Core
{
    /// <summary>
    /// 势力触发器基类：宿主是势力（技法装配），用于"势力回合开始 / 结束"这类全局时机。
    ///
    /// 条件里一般用 ActionForce；ActionCity / ActionTroop 恒为空，
    /// 需要遍历部下时请在效果里自行调用 Force.ForEachXxx。
    /// </summary>
    public abstract class ForceTrigger : Trigger
    {
        /// <summary>宿主势力</summary>
        public Force Force;

        /// <summary>宿主势力</summary>
        public override Force ActionForce => Force;
        /// <summary>宿主对象</summary>
        public override object ActionObject => Force;

        /// <summary>
        /// 初始化：解析宿主势力
        /// </summary>
        /// <param name="call">触发器回调</param>
        /// <param name="sangoObjects">宿主对象</param>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);

            if (sangoObjects == null || sangoObjects.Length == 0)
                return;

            Force = sangoObjects[0] as Force;
        }

        /// <summary>
        /// 归属校验：只响应宿主势力的事件
        /// </summary>
        /// <param name="force">事件里的势力</param>
        /// <returns>是否属于本触发器</returns>
        protected bool CheckForce(Force force)
        {
            if (force == null)
                return false;
            if (Force != null && Force != force)
                return false;
            return true;
        }
    }
}
