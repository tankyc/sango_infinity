using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 改变某兵种类型战法的气力消耗
    /// value： 改变值
    /// kinds： 兵种类型 
    /// condition： 额外条件
    /// </summary>
    public class TroopAddSkill : TroopTroopActionBase
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
            if (Force != null && troop.mBelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

            Skill skill = scenario.GetObject<Skill>(value);
            if (kinds == null)
            {
                if(!troop.landSkills.Exists(x=>x.Id == skill.Id))
                    troop.landSkills.Add(SkillInstance.Create(troop, skill));
                if(!troop.waterSkills.Exists(x=>x.Id == skill.Id))
                    troop.waterSkills.Add(SkillInstance.Create(troop, skill));
            }
            else
            {
                if (kinds.Contains(troop.LandTroopType.kind))
                {
                    if(!troop.landSkills.Exists(x=>x.Id == value))
                        troop.landSkills.Add(SkillInstance.Create(troop, skill));
                }
                if (kinds.Contains(troop.WaterTroopType.kind))
                {
                    if (!troop.waterSkills.Exists(x => x.Id == value))
                        troop.waterSkills.Add(SkillInstance.Create(troop, skill));
                }
            }
        }
    }
}
