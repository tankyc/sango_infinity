using Sango.Core.Tools;
using System.Collections.Generic;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 改变某兵种类型战法的气力消耗
    /// value： 改变值
    /// kinds： 兵种类型 
    /// condition： 额外条件
    /// </summary>
    public class TroopAddSkillSpellRange : TroopTroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopAfterCalculateAttribute += OnTroopAfterCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopAfterCalculateAttribute -= OnTroopAfterCalculateAttribute;
        }

        void AddRange(SkillInstance skillInstance)
        {
            List<int> rangeL = new List<int>(skillInstance.spellRanges);
            int end_v = skillInstance.spellRanges[skillInstance.spellRanges.Length - 1];
            for (int i = 0; i < value; ++i)
                rangeL.Add(end_v + i + 1);
            skillInstance.spellRanges = rangeL.ToArray();
        }

        void OnTroopAfterCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;


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
                            AddRange(skill);
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
                            AddRange(skill);
                        }
                    });

                    troop.StrategySkills.ForEach(skill =>
                    {
                        TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                        if (condition.Check(troopActionConditionDatabase))
                        {
                            AddRange(skill);
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
                        AddRange(skill);
                    });

                    troop.waterSkills.ForEach(skill =>
                    {
                        if (!CheckIsNormalSkill(skill, isNormal))
                            return;

                        if (!CheckIsRangeSkill(skill, isRange))
                            return;
                        AddRange(skill);
                    });

                    troop.StrategySkills.ForEach(skill =>
                    {
                        AddRange(skill);
                    });
                }
            }
            else
            {
                if (kinds.Contains(troop.LandTroopType.kind))
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
                                AddRange(skill);
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
                            AddRange(skill);
                        });
                    }
                }
                if (kinds.Contains(troop.WaterTroopType.kind))
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
                                AddRange(skill);
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
                            AddRange(skill);
                        });
                    }
                }
            }
        }
    }
}
