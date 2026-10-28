using Sango.Core.Tools;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 改变部队的建设能力
    /// value： 改变值
    /// kinds： 兵种类型 
    /// condition： 额外条件
    /// 接入事件：OnTroopCalculateAttribute
    /// </summary>
    public class TroopImproveBuildPower : TroopTroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;
            // 读改写走 BuildPowerRaw：BuildPower 的 getter 会乘 Buff 系数（如混乱 0.8），
            // 直接读改写会把该系数固化进基础值，导致反复触发时持续缩水。
            troop.BuildPowerRaw = troop.BuildPowerRaw * value / 100;
        }
    }
}
