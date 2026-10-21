using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 建筑光环：以建筑中心格为中心，给半径内的部队提供常驻加成。
    ///
    /// 与 BuildingImproveTroopAttack / BuildingImproveTroopDefence 的区别：
    ///   · 那两个是"按属性写死"的历史实现（太鼓台加攻、阵加防），走 buildingImproveMap；
    ///   · 本类是通用实现，属性、数值、半径、敌我、叠加规则全部由 JSON 配置，
    ///     新增建筑不用再写代码。二者可以共存（都作用在 troop.extraAttack / extraDefence 上）。
    ///
    /// 参数：
    /// value      光环数值（可为负，负数即减益光环）
    /// bound      光环半径（格），必须 > 0
    /// auraType   作用属性：0 攻击力 / 1 防御力
    /// targetType 作用对象：0 己方 / 1 敌方 / 2 所有
    /// stackMode  叠加规则：0 多个光环取最大值 / 1 累加
    ///
    /// 接入事件：OnTroopEnterCell（部队进入 / 离开范围）。
    /// </summary>
    public class BuildingAura : AuraActionBase
    {
        /// <summary>光环源建筑</summary>
        BuildingBase building;

        /// <summary>
        /// 初始化建筑光环
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (sangoObjects == null || sangoObjects.Length == 0)
                return;

            building = sangoObjects[0] as BuildingBase;
            if (building == null)
                Force = sangoObjects[0] as Force;

            base.Init(p, sangoObjects);
        }

        /// <summary>光环中心：建筑中心格（建筑被摧毁时视为无中心）</summary>
        protected override Cell CenterCell => (building != null && building.IsAlive) ? building.CenterCell : null;

        /// <summary>光环归属势力：建筑所属势力</summary>
        protected override Force OwnerForce => building != null ? building.BelongForce : Force;
    }
}
