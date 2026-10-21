namespace Sango.Core
{
    /// <summary>
    /// 建筑触发器基类：把宿主建筑 / 势力暴露给条件与效果，并提供归属校验。
    ///
    /// 与部队触发器（TroopTrigger）的区别：宿主是 Building（城市除外，城市用 CityTrigger），
    /// 因此 ActionTroop / ActionPerson 之类的属性恒为空，条件里请使用 ActionCity / ActionForce。
    /// </summary>
    public abstract class BuildingTrigger : Trigger
    {
        /// <summary>宿主建筑（建筑上装配时有值）</summary>
        public Building Building;
        /// <summary>宿主势力（技法装配时有值）</summary>
        public Force Force;

        /// <summary>宿主建筑所属城市</summary>
        public override City ActionCity => Building != null ? Building.BelongCity : null;
        /// <summary>宿主建筑所属军团</summary>
        public override Corps ActionCorps => Building != null ? Building.BelongCorps : null;
        /// <summary>宿主建筑所属势力</summary>
        public override Force ActionForce => Building != null ? Building.BelongForce : Force;
        /// <summary>宿主建筑中心格</summary>
        public override Cell ActionCell => Building != null ? Building.CenterCell : null;
        /// <summary>宿主对象</summary>
        public override object ActionObject => Building;

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

            Building = sangoObjects[0] as Building;
            if (Building == null)
                Force = sangoObjects[0] as Force;
        }

        /// <summary>
        /// 归属校验：只响应宿主建筑（或宿主势力）对应的事件
        /// </summary>
        /// <param name="building">事件里的建筑</param>
        /// <returns>是否属于本触发器</returns>
        protected bool CheckBuilding(Building building)
        {
            if (building == null)
                return false;
            if (Building != null && Building != building)
                return false;
            if (Force != null && Force != building.BelongForce)
                return false;
            return true;
        }
    }
}
