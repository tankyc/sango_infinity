using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 反击伤害修正（value 为百分比，乘法：100 表示不变）。
    ///
    /// 接入事件：OnTroopCalculateAttackBack（反击伤害计算时），作用对象为受击方。
    /// </summary>
    public class TroopSkillCalculateAttackBack : TroopTroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopCalculateAttackBack += OnTroopCalculateAttackBack;
        }

        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttackBack -= OnTroopCalculateAttackBack;
        }

        void OnTroopCalculateAttackBack(Troop attacker, Troop defencer, SkillInstance skill, Scenario scenario, OverrideData<int> overrideData)
        {
            if (!CheckTroop(defencer, skill)) return;
            overrideData.Value = overrideData.Value * value / 100;
        }
    }
}
