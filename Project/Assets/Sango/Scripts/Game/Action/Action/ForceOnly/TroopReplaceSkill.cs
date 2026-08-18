using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种战法替换成新战法	p1:兵种kind (0全兵种全地形 -1陆地 -2水上)  p2:原技能id p3:目标技能id
    /// value: 技能id kinds: 兵种类型 srcSkillId: 被替换技能id
    /// </summary>
    public class TroopReplaceSkill : ForceTroopActionBase
    {
        int srcSkillId;
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            srcSkillId = p.Value<int>("srcSkillId");
            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != troop.mBelongForce) return;
            int skillId = srcSkillId;
            int replaceSkillId = value;
            Skill skill = scenario.GetObject<Skill>(replaceSkillId);
            if (kinds == null)
            {
                troop.landSkills.RemoveAll(x => x.skill.Id == skillId);
                troop.landSkills.Add(SkillInstance.Create(troop, skill));
                troop.waterSkills.RemoveAll(x => x.skill.Id == skillId);
                troop.waterSkills.Add(SkillInstance.Create(troop, skill));
            }
            else
            {
                if (kinds.Contains(troop.LandTroopType.kind))
                {
                    troop.landSkills.RemoveAll(x => x.skill.Id == skillId);
                    troop.landSkills.Add(SkillInstance.Create(troop, skill));
                }
                if (kinds.Contains(troop.WaterTroopType.kind))
                {
                    troop.waterSkills.RemoveAll(x => x.skill.Id == skillId);
                    troop.waterSkills.Add(SkillInstance.Create(troop, skill));
                }
            }
        }
    }
}
