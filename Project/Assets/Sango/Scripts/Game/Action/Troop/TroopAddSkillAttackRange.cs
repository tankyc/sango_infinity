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
    public class TroopAddSkillAttackRange : TroopTroopActionBase
    {
        int atkType;
        int addRange;
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            atkType = p.Value<int>("atkType");
            addRange = p.Value<int>("addRange");
            GameEvent.OnTroopAfterCalculateAttribute += OnTroopAfterCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopAfterCalculateAttribute -= OnTroopAfterCalculateAttribute;
        }

        void AddAttackRange(SkillInstance skillInstance)
        {
            if (skillInstance.atkOffsetPoint == null || skillInstance.atkOffsetPoint.Length == 0)
            {
                skillInstance.atkOffsetPoint = new int[] { atkType, addRange };
            }
            else
            {
                SkillAttackOffsetType aopType = (SkillAttackOffsetType)skillInstance.atkOffsetPoint[0];
                switch (aopType)
                {
                    // 0
                    case SkillAttackOffsetType.Customize:
                        break;
                    // 1
                    case SkillAttackOffsetType.Ring:
                        {
                            if (skillInstance.atkOffsetPoint.Length > 1)
                            {
                                int radius = skillInstance.atkOffsetPoint[1];
                                radius = radius + addRange;
                                skillInstance.atkOffsetPoint[1] = radius;
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 2
                    case SkillAttackOffsetType.DirectionLine:
                        {
                            if (skillInstance.atkOffsetPoint.Length > 1)
                            {
                                int length = skillInstance.atkOffsetPoint[1];
                                length = length + addRange;
                                skillInstance.atkOffsetPoint[1] = length;
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    //3
                    case SkillAttackOffsetType.SelfRing:
                        {
                            if (skillInstance.atkOffsetPoint.Length > 1)
                            {
                                int radius = skillInstance.atkOffsetPoint[1];
                                radius = radius + addRange;
                                skillInstance.atkOffsetPoint[1] = radius;
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    //4
                    case SkillAttackOffsetType.SpellNeighbors:
                        break;
                    // 5
                    case SkillAttackOffsetType.Spiral:
                        {
                            if (skillInstance.atkOffsetPoint.Length > 1)
                            {
                                int radius = skillInstance.atkOffsetPoint[1];
                                radius = radius + addRange;
                                skillInstance.atkOffsetPoint[1] = radius;
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 6
                    case SkillAttackOffsetType.Fan:
                        {
                            if (skillInstance.atkOffsetPoint.Length > 2)
                            {
                                int radius = skillInstance.atkOffsetPoint[1];
                                radius = radius + addRange;
                                skillInstance.atkOffsetPoint[1] = radius;
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 7
                    case SkillAttackOffsetType.Rectangle:
                        {
                            if (skillInstance.atkOffsetPoint.Length > 2)
                            {
                                int width = skillInstance.atkOffsetPoint[1];
                                width = width + addRange;
                                skillInstance.atkOffsetPoint[1] = width;

                                int length = skillInstance.atkOffsetPoint[2];
                                length = length + addRange;
                                skillInstance.atkOffsetPoint[2] = length;
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 8
                    case SkillAttackOffsetType.Cross:
                        {
                            if (skillInstance.atkOffsetPoint.Length > 1)
                            {
                                int radius = skillInstance.atkOffsetPoint[1];
                                radius = radius + addRange;
                                skillInstance.atkOffsetPoint[1] = radius;
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 9
                    case SkillAttackOffsetType.Square:
                        {
                            if (skillInstance.atkOffsetPoint.Length > 1)
                            {
                                int radius = skillInstance.atkOffsetPoint[1];
                                radius = radius + addRange;
                                skillInstance.atkOffsetPoint[1] = radius;
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 10
                    case SkillAttackOffsetType.Diamond:
                        {
                            if (skillInstance.atkOffsetPoint.Length > 1)
                            {
                                int radius = skillInstance.atkOffsetPoint[1];
                                radius = radius + addRange;
                                skillInstance.atkOffsetPoint[1] = radius;
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    default:
                        skillInstance.atkOffsetPoint = new int[] { atkType, addRange };
                        break;
                }
            }
        }

        void OnTroopAfterCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;


            if (kinds == null)
            {
                troop.landSkills.ForEach(skill =>
                {
                    if (!CheckIsNormalSkill(skill, isNormal))
                        return;

                    if (!CheckIsRangeSkill(skill, isRange))
                        return;

                    TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                    if (condition != null && !condition.Check(troopActionConditionDatabase))
                        return;

                    AddAttackRange(skill);
                });

                troop.waterSkills.ForEach(skill =>
                {
                    if (!CheckIsNormalSkill(skill, isNormal))
                        return;

                    if (!CheckIsRangeSkill(skill, isRange))
                        return;

                    TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                    if (condition != null && !condition.Check(troopActionConditionDatabase))
                        return;
                    AddAttackRange(skill);
                });

                troop.StrategySkills.ForEach(skill =>
                {
                    TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                    if (condition != null && !condition.Check(troopActionConditionDatabase))
                        return;

                    AddAttackRange(skill);
                });
            }
            else
            {
                if (kinds.Contains(troop.LandTroopType.kind))
                {
                    troop.landSkills.ForEach(skill =>
                    {
                        if (!CheckIsNormalSkill(skill, isNormal))
                            return;

                        if (!CheckIsRangeSkill(skill, isRange))
                            return;

                        TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                        if (condition != null && !condition.Check(troopActionConditionDatabase))
                            return;

                        AddAttackRange(skill);
                        
                    });
                }

                if (kinds.Contains(troop.WaterTroopType.kind))
                {
                    troop.waterSkills.ForEach(skill =>
                    {
                        if (!CheckIsNormalSkill(skill, isNormal))
                            return;

                        if (!CheckIsRangeSkill(skill, isRange))
                            return;

                        TroopSkillConditionDatabase troopActionConditionDatabase = new TroopSkillConditionDatabase(skill);
                        if (condition != null && !condition.Check(troopActionConditionDatabase))
                            return;

                        AddAttackRange(skill);
                    });
                }
            }
        }
    }
}
