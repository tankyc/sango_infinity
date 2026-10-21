using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队光环：以该部队所在格子为中心，给半径内的部队提供常驻加成。
    ///
    /// 光环跟着部队移动：部队每走一步都会整体重算覆盖范围（旧位置失去、新位置获得）。
    ///
    /// 参数：
    /// value      光环数值（可为负，负数即减益光环）
    /// bound      光环半径（格），必须 > 0
    /// auraType   作用属性：0 攻击力 / 1 防御力
    /// targetType 作用对象：0 己方（含自己） / 1 敌方 / 2 所有
    /// stackMode  叠加规则：0 多个光环取最大值 / 1 累加
    ///
    /// 接入事件：OnTroopEnterCell（其他部队进入 / 离开，以及源部队自身移动）。
    /// 说明：装配在势力技法上时没有中心格，光环不生效（势力级请用属性类效果）。
    /// </summary>
    public class TroopAura : AuraActionBase
    {
        /// <summary>光环源部队</summary>
        Troop troop;

        /// <summary>
        /// 初始化部队光环
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (sangoObjects == null || sangoObjects.Length == 0)
                return;

            troop = sangoObjects[0] as Troop;
            if (troop == null)
                Force = sangoObjects[0] as Force;

            base.Init(p, sangoObjects);
        }

        /// <summary>光环中心：源部队所在格子（部队已溃灭时视为无中心）</summary>
        protected override Cell CenterCell => (troop != null && troop.IsAlive) ? troop.cell : null;

        /// <summary>光环归属势力：源部队所属势力</summary>
        protected override Force OwnerForce => troop != null ? troop.BelongForce : Force;

        /// <summary>
        /// 部队是否是光环源（源部队移动时整体重算）
        /// </summary>
        /// <param name="target">移动的部队</param>
        /// <returns>是否是光环源</returns>
        protected override bool IsAuraOwner(Troop target)
        {
            return troop != null && target == troop;
        }
    }
}
