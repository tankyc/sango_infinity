using TKNewtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 提升范围内部队的防御力
    /// value:提升值 bound: 生效范围 targetType: 作用目标范围, 0己方 1敌人 2所有
    /// </summary>
    public class BuildingImproveTroopDefence : BuildingImproveBase
    {
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            improveKey = "BuildingImproveTroopDefence";
            base.Init(p, sangoObjects);
        }

        protected override void OnEnter(Troop troop)
        {
            troop.extraDefence += value;
        }

        protected override void OnLeave(Troop troop)
        {
            troop.extraDefence -= value;
        }
    }
}
