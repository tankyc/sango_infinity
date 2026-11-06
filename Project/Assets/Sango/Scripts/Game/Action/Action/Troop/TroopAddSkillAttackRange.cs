using Sango.Core.Tools;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 在原有命中波及范围的基础上追加 value 格（增量）。
    ///
    /// 接入事件：OnTroopAfterCalculateAttribute（属性重算后）。
    /// 与 TroopChangeSkillAttackRange（覆盖原范围）区分。
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
                // 【防护】原本没有命中偏移的技能（如计略）被追加范围时，[type, value] 只有
                // "半径型"分支才有意义；若 atkType 是 Customize，生成的 [0, range] 会让
                // Skill.GetAttackCells 的 Customize 分支在 atkOffsetPoint[i+1] 越界崩溃。
                if (atkType == (int)SkillAttackOffsetType.Customize)
                {
                    Sango.Log.Error("TroopAddSkillAttackRange 配置非法：atkType=Customize 无法用 " +
                                    "[type,value] 表达，已忽略该次追加");
                    return;
                }
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
                        // 原数组的类型越界（不在 0~10）时整体换成配置的 atkType；
                        // 同样不能是 Customize，否则生成 [0, value] 会导致后续越界
                        if (atkType == (int)SkillAttackOffsetType.Customize)
                        {
                            Sango.Log.Error("TroopAddSkillAttackRange 配置非法：atkType=Customize 无法用 " +
                                            "[type,value] 表达，已忽略该次追加");
                            break;
                        }
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
