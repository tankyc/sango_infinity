using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种类型战法的反击率增加
    /// value： 增加值(百分比) 
    /// kinds： 兵种类型 
    /// checkLand： 0:只检查kinds 1:只对landType检查kinds 2只对waterType检查kinds 
    /// isDefender 0攻击方 1受击方 
    /// isNormal  0都可以 1是 2不是
    /// isRange 0都可以 1是 2不是
    /// ondition： 额外条件 支持参数(troop,troop,skill)
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
            if (!CheckTroop(defencer, attacker, skill)) return;
            overrideData.Value = overrideData.Value * value / 100;
        }
    }
}
