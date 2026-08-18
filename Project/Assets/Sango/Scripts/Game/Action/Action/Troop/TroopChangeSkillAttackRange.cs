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
    public class TroopChangeSkillAttackRange : TroopTroopActionBase
    {
        int atkType;
        int atkRange;
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            atkType = p.Value<int>("atkType");
            atkRange = p.Value<int>("atkRange");
            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        void SetRange(SkillInstance skillInstance)
        {
            skillInstance.atkOffsetPoint = new int[] { atkType, atkRange };
        }

        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != null && troop.mBelongForce != Force) return;
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
