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
    public class TroopSetSkillCost : TroopTroopActionBase
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

            if (kinds == null)
            {
                if (condition != null)
                {
                    troop.landSkills.ForEach(skill =>
                    {
                        TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                        if (condition.Check(troopActionConditionDatabase))
                        {
                            skill.costEnergy = value;
                        }
                    });

                    troop.waterSkills.ForEach(skill =>
                    {
                        TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                        if (condition.Check(troopActionConditionDatabase))
                        {
                            skill.costEnergy = value;
                        }
                    });

                    troop.StrategySkills.ForEach(skill =>
                    {
                        TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                        if (condition.Check(troopActionConditionDatabase))
                        {
                            skill.costEnergy = value;
                        }
                    });
                }
                else
                {
                    troop.landSkills.ForEach(skill =>
                    {
                        skill.costEnergy = value;
                    });

                    troop.waterSkills.ForEach(skill =>
                    {
                        skill.costEnergy = value;
                    });

                    troop.StrategySkills.ForEach(skill =>
                    {
                        skill.costEnergy = value;
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
                            TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                            if (condition.Check(troopActionConditionDatabase))
                            {
                                skill.costEnergy = value;
                            }
                        });
                    }
                    else
                    {
                        troop.landSkills.ForEach(skill =>
                        {
                            skill.costEnergy = value;
                        });
                    }
                }
                if (kinds.Contains(troop.WaterTroopType.kind))
                {
                    if (condition != null)
                    {
                        troop.waterSkills.ForEach(skill =>
                        {
                            TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                            if (condition.Check(troopActionConditionDatabase))
                            {
                                skill.costEnergy = value;
                            }
                        });
                    }
                    else
                    {
                        troop.waterSkills.ForEach(skill =>
                        {
                            skill.costEnergy = value;
                        });
                    }
                }
            }
        }
    }
}
