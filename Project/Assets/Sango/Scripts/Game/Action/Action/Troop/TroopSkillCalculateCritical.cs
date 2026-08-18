using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种类型战法的暴击率增加
    /// value： 增加值(百分比) 
    /// kinds： 兵种类型  
    /// checkLand： 0:只检查kinds 1:只对landType检查kinds 2只对waterType检查kinds 
    /// isNormal  0都可以 1是 2不是
    /// condition： 额外条件 支持参数(troop,troop,skill)
    /// </summary>
    public class TroopSkillCalculateCritical : TroopTroopActionBase
    {
        /// <summary>
        /// 自己是否是目标
        /// </summary>
        bool selfIsTarget;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            selfIsTarget = p.Value<bool>("selfIsTarget");
            if (value >= 100 || value <= -100)
                GameEvent.OnTroopBeforeCalculateSkillCritical += OnTroopCalculateSkillCritical;
            else
                GameEvent.OnTroopAfterCalculateSkillCritical += OnTroopCalculateSkillCritical;

        }

        public override void Clear()
        {
            if (value >= 100 || value <= -100)
                GameEvent.OnTroopBeforeCalculateSkillCritical -= OnTroopCalculateSkillCritical;
            else
                GameEvent.OnTroopAfterCalculateSkillCritical -= OnTroopCalculateSkillCritical;
        }

        void OnTroopCalculateSkillCritical(Troop troop, SkillInstance skill, Cell spellCell, OverrideData<int> overrideData)
        {
            if (Force != null && troop.mBelongForce != Force) return;

            if(!selfIsTarget)
            {
                if (Troop != null && Troop != troop) return;
            }
            else
            {
                if (Troop != null && Troop != spellCell.troop) return;
            }

            if (!CheckIsNormalSkill(skill, isNormal))
                return;

            if (checkLand == 1 && troop.IsInWater)
                return;
            if (checkLand == 2 && !troop.IsInWater)
                return;

            if (checkLand == 0 && kinds != null && !kinds.Contains(troop.LandTroopType.kind) && !kinds.Contains(troop.WaterTroopType.kind))
                return;

            if (checkLand == 1 && kinds != null && !kinds.Contains(troop.LandTroopType.kind))
                return;

            if (checkLand == 2 && kinds != null && !kinds.Contains(troop.WaterTroopType.kind))
                return;

            if (condition != null && !condition.Check(new TroopActionConditionDatabase(skill, spellCell)))
                return;

            overrideData.Value += value;
        }
    }
}
