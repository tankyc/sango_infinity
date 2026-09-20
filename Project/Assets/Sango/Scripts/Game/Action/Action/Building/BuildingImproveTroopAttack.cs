using TKNewtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 提升范围内部队的攻击力
    /// value:提升值 bound: 生效范围 targetType: 作用目标范围, 0己方 1敌人 2所有
    /// </summary>
    public class BuildingImproveTroopAttack : BuildingImproveBase
    {
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            // 【修复】原先误用了防御类的 key（"BuildingImproveTroopDefence"），
            // 导致同一支部队同时受"太鼓台(加攻)"与"阵/砦/城塞(加防)"影响时，
            // 二者会写入 troop.buildingImproveMap 的同一个键而互相覆盖：
            // 后进入范围的建筑会顶掉先前的效果，离开范围时又会把对方的值一并扣回，
            // 表现为"攻击加成时有时无、甚至变成负值"。
            improveKey = "BuildingImproveTroopAttack";
            base.Init(p, sangoObjects);
        }

        protected override void OnEnter(Troop troop)
        {
            troop.extraAttack += value;
        }

        protected override void OnLeave(Troop troop)
        {
            troop.extraAttack -= value;
        }
    }
}
