using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 往部队的战法列表里追加一个战法。
    ///
    /// 接入事件：OnTroopBeforeCalculateAttribute（属性重算前），重复追加同一战法会被跳过。
    /// 参数：value 为战法 Id。
    /// </summary>
    public class TroopAddSkill : TroopTroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopBeforeCalculateAttribute += OnTroopBeforeCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopBeforeCalculateAttribute -= OnTroopBeforeCalculateAttribute;
        }

        void OnTroopBeforeCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

            Skill skill = scenario.GetObject<Skill>(value);
            if (skill.IsStrategy())
            {
                if (!troop.StrategySkills.Exists(x => x.Id == skill.Id))
                    troop.StrategySkills.Add(SkillInstance.Create(troop, skill));
            }
            else
            {
                if (kinds == null)
                {

                    if (!troop.landSkills.Exists(x => x.Id == skill.Id))
                        troop.landSkills.Add(SkillInstance.Create(troop, skill));
                    if (!troop.waterSkills.Exists(x => x.Id == skill.Id))
                        troop.waterSkills.Add(SkillInstance.Create(troop, skill));
                }
                else
                {
                    if (kinds.Contains(troop.LandTroopType.kind))
                    {
                        if (!troop.landSkills.Exists(x => x.Id == value))
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
}
