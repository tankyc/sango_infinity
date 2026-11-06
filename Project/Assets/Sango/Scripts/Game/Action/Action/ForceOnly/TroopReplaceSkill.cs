using Sango.Core.Tools;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种战法替换成新战法	p1:兵种kind (0全兵种全地形 -1陆地 -2水上)  p2:原技能id p3:目标技能id
    /// value: 技能id kinds: 兵种类型 srcSkillId: 被替换技能id
    /// 接入事件：OnTroopCalculateAttribute
    /// </summary>
    public class TroopReplaceSkill : ForceTroopActionBase
    {
        int srcSkillId;

        /// <summary>已报告过的非法目标战法 Id（同一配置只提示一次，避免每支部队重算都刷一条日志）。</summary>
        static readonly HashSet<int> reportedInvalidSkill = new HashSet<int>();
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
            if (Force != troop.BelongForce) return;
            int skillId = srcSkillId;
            int replaceSkillId = value;
            Skill skill = scenario.GetObject<Skill>(replaceSkillId);

            // 【必须判空】配置的 value（目标战法 Id）在本局技能表里不存在时 GetObject 返回 null
            // （Scenario.GetObject → SangoObjectSet.Get）。继续往下会
            // SkillInstance.Create(troop, null) → Init 里读 skill.atk 抛 NullReferenceException
            // （实测栈：本方法第 44 行 → SkillInstance.Init:236，AI 出阵时崩）。
            // 必须在 RemoveAll **之前**返回：否则旧战法已被删、新战法又加不上，部队会白丢一个战法。
            // 与 TroopAddSkill 的同名处理保持一致（一次性报告，避免每支部队刷一条）。
            if (skill == null)
            {
                if (reportedInvalidSkill.Add(replaceSkillId))
                    Sango.Log.Error($"TroopReplaceSkill 配置的目标战法 Id={replaceSkillId} 在本局技能表中不存在，已跳过替换");
                return;
            }

            if (kinds == null)
            {
                if (troop.landSkills.RemoveAll(x => x.skill.Id == skillId) > 0)
                    troop.landSkills.Add(SkillInstance.Create(troop, skill));
                if (troop.waterSkills.RemoveAll(x => x.skill.Id == skillId) > 0)
                    troop.waterSkills.Add(SkillInstance.Create(troop, skill));
            }
            else
            {
                // LandTroopType / WaterTroopType 是按 Id 懒解析的可空引用
                //（Troop.cs:812-820，兵种 Id 缺失时就是 null），取 .kind 前必须判空
                if (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind))
                {
                    if (troop.landSkills.RemoveAll(x => x.skill.Id == skillId) > 0)
                        troop.landSkills.Add(SkillInstance.Create(troop, skill));
                }
                if (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind))
                {
                    if (troop.waterSkills.RemoveAll(x => x.skill.Id == skillId) > 0)
                        troop.waterSkills.Add(SkillInstance.Create(troop, skill));
                }
            }
        }
    }
}
