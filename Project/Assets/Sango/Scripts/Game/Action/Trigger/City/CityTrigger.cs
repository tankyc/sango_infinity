namespace Sango.Core
{
    /// <summary>
    /// 城市触发器基类：把宿主城市 / 势力暴露给条件与效果，并提供归属校验。
    ///
    /// 条件里一般用 ActionCity / ActionForce / ActionCell；ActionTroop 恒为空。
    /// </summary>
    public abstract class CityTrigger : Trigger
    {
        /// <summary>宿主城市（城市上装配时有值）</summary>
        public City City;
        /// <summary>宿主势力（技法装配时有值）</summary>
        public Force Force;
        /// <summary>目标势力（如城市沦陷时的新归属）</summary>
        protected Force targetForce;
        /// <summary>目标部队（如城市沦陷时的攻方部队）</summary>
        protected Troop targetTroop;

        /// <summary>宿主城市</summary>
        public override City ActionCity => City;
        /// <summary>宿主城市所属军团</summary>
        public override Corps ActionCorps => City != null ? City.BelongCorps : null;
        /// <summary>宿主城市所属势力</summary>
        public override Force ActionForce => City != null ? City.BelongForce : Force;
        /// <summary>宿主城市中心格</summary>
        public override Cell ActionCell => City != null ? City.CenterCell : null;

        /// <summary>目标城市（城市沦陷时即宿主城市本身）</summary>
        public override City TargetCity => City;
        /// <summary>目标势力</summary>
        public override Force TargetForce => targetForce;
        /// <summary>目标部队</summary>
        public override Troop TargetTroop => targetTroop;
        /// <summary>目标格子</summary>
        public override Cell TargetCell => targetTroop != null ? targetTroop.cell : (City != null ? City.CenterCell : null);

        /// <summary>宿主对象</summary>
        public override object ActionObject => City;

        /// <summary>
        /// 初始化：解析宿主对象
        /// </summary>
        /// <param name="call">触发器回调</param>
        /// <param name="sangoObjects">宿主对象</param>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);

            if (sangoObjects == null || sangoObjects.Length == 0)
                return;

            City = sangoObjects[0] as City;
            if (City == null)
                Force = sangoObjects[0] as Force;
        }

        /// <summary>
        /// 归属校验：只响应宿主城市（或宿主势力）对应的事件
        /// </summary>
        /// <param name="city">事件里的城市</param>
        /// <returns>是否属于本触发器</returns>
        protected bool CheckCity(City city)
        {
            if (city == null)
                return false;
            if (City != null && City != city)
                return false;
            if (Force != null && Force != city.BelongForce)
                return false;
            return true;
        }

        /// <summary>
        /// 每次触发前清空一次目标上下文，避免上一次的目标残留
        /// </summary>
        protected void ResetTargets()
        {
            targetForce = null;
            targetTroop = null;
        }
    }
}
