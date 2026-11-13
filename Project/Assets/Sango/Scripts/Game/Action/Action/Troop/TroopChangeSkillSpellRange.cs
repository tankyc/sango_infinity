using Sango.Core.Tools;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 在战法模板施放距离的基础上再追加 value 层（每层 +1 格）。
    ///
    /// 接入事件：OnTroopCalculateAttribute（部队属性重算时）。
    /// 与 TroopAddSkillSpellRange 的效果基本一致（后者基于实例当前距离），配置时二选一即可。
    /// </summary>
    public class TroopChangeSkillSpellRange : TroopTroopActionBase
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

        void SetRange(SkillInstance skillInstance)
        {
            // 【越界修复】模板 spellRanges 为空时，原实现会访问 [-1] 抛 IndexOutOfRangeException
            if (skillInstance == null || skillInstance.skill == null
                || skillInstance.skill.spellRanges == null || skillInstance.skill.spellRanges.Length == 0)
                return;

            List<int> rangeL = new List<int>(skillInstance.skill.spellRanges);
            int end_v = skillInstance.skill.spellRanges[skillInstance.skill.spellRanges.Length - 1];
            for (int i = 0; i < value; ++i)
                rangeL.Add(end_v + i + 1);
            skillInstance.spellRanges = rangeL.ToArray();
        }

        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

            // 【kinds 语义】这里一律按"兵种种类 kind"匹配（1剑 2枪 3戟 4弩 5骑 6水军 7运输 8冲车木兽 9井阑投石），
            // 不是 TroopType.Id。历史上射程（13/14）、工神（11~14）、水将/水神（8~10）都误填成兵种 Id，
            // 与 kind 永远对不上，导致整段遍历被跳过、特技静默失效。


            if (kinds == null)
            {
                if (condition != null)
                {
                    troop.landSkills.ForEach(skill =>
                    {
                        if (!CheckIsNormalSkill(skill, isNormal))
                            return;

                        if (!CheckIsRangeSkill(skill, isRange))
                            return;

                        TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                        if (condition.Check(troopActionConditionDatabase))
                        {
                            SetRange(skill);
                        }
                    });

                    troop.waterSkills.ForEach(skill =>
                    {
                        if (!CheckIsNormalSkill(skill, isNormal))
                            return;

                        if (!CheckIsRangeSkill(skill, isRange))
                            return;

                        TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                        if (condition.Check(troopActionConditionDatabase))
                        {
                            SetRange(skill);
                        }
                    });

                    troop.StrategySkills.ForEach(skill =>
                    {
                        TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                        if (condition.Check(troopActionConditionDatabase))
                        {
                            SetRange(skill);
                        }
                    });
                }
                else
                {
                    troop.landSkills.ForEach(skill =>
                    {
                        if (!CheckIsNormalSkill(skill, isNormal))
                            return;

                        if (!CheckIsRangeSkill(skill, isRange))
                            return;
                        SetRange(skill);
                    });

                    troop.waterSkills.ForEach(skill =>
                    {
                        if (!CheckIsNormalSkill(skill, isNormal))
                            return;

                        if (!CheckIsRangeSkill(skill, isRange))
                            return;
                        SetRange(skill);
                    });

                    troop.StrategySkills.ForEach(skill =>
                    {
                        SetRange(skill);
                    });
                }
            }
            else
            {
                if (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind))
                {
                    if (condition != null)
                    {
                        troop.landSkills.ForEach(skill =>
                        {
                            if (!CheckIsNormalSkill(skill, isNormal))
                                return;

                            if (!CheckIsRangeSkill(skill, isRange))
                                return;

                            TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                            if (condition.Check(troopActionConditionDatabase))
                            {
                                SetRange(skill);
                            }
                        });
                    }
                    else
                    {
                        troop.landSkills.ForEach(skill =>
                        {
                            if (!CheckIsNormalSkill(skill, isNormal))
                                return;

                            if (!CheckIsRangeSkill(skill, isRange))
                                return;
                            SetRange(skill);
                        });
                    }
                }
                if (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind))
                {
                    if (condition != null)
                    {
                        troop.waterSkills.ForEach(skill =>
                        {
                            if (!CheckIsNormalSkill(skill, isNormal))
                                return;

                            if (!CheckIsRangeSkill(skill, isRange))
                                return;

                            TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                            if (condition.Check(troopActionConditionDatabase))
                            {
                                SetRange(skill);
                            }
                        });
                    }
                    else
                    {
                        troop.waterSkills.ForEach(skill =>
                        {
                            if (!CheckIsNormalSkill(skill, isNormal))
                                return;

                            if (!CheckIsRangeSkill(skill, isRange))
                                return;
                            SetRange(skill);
                        });
                    }
                }
            }
        }
    }
}
