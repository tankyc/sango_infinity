using Sango.Core.Tools;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 把战法的命中波及范围设为 value（覆盖）。
    ///
    /// 接入事件：OnTroopCalculateAttribute（部队属性重算时）。
    /// 与 TroopAddSkillAttackRange（在原有范围上追加）区分。
    /// </summary>
    public class TroopChangeSkillAttackRange : TroopTroopActionBase
    {
        int atkType;
        int atkRange;

        /// <summary>
        /// 可选的完整命中偏移配置，格式 <c>[type, p1, p2, ...]</c>。
        ///
        /// 【为什么需要】<c>[atkType, atkRange]</c> 两元素形式只能表达"类型 + 一个参数"，
        /// 而 <see cref="SkillAttackOffsetType.DecaySpread"/> 需要「半径 + 各环概率」多个参数。
        /// 配了这个字段就整份覆盖，未配则回落到 <c>[atkType, atkRange]</c>，兼容既有数据。
        /// </summary>
        int[] atkOffsetPointConfig;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            atkType = p.Value<int>("atkType");
            atkRange = p.Value<int>("atkRange");

            // 用索引取而不是 p.Value<JArray>()：后者依赖 Newtonsoft 的泛型转换路径，
            // 拿到的是否为原始 JArray 实例取决于版本；直接 as 转换语义明确、不会抛异常。
            JArray offsetArray = p["atkOffsetPoint"] as JArray;
            if (offsetArray != null && offsetArray.Count > 0)
            {
                atkOffsetPointConfig = new int[offsetArray.Count];
                for (int i = 0; i < offsetArray.Count; i++)
                {
                    JToken item = offsetArray[i];
                    atkOffsetPointConfig[i] = item != null ? item.ToObject<int>() : 0;
                }
            }

            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        void SetRange(SkillInstance skillInstance)
        {
            if (atkOffsetPointConfig != null)
            {
                // 【防护】Customize 的语义是"成对偏移"(x1,y1,x2,y2...)，必须由数据自己配对，
                // 这里只拦下"长度为偶数"的非法写法，避免 Skill.GetAttackCells 越界。
                if (atkOffsetPointConfig[0] == (int)SkillAttackOffsetType.Customize
                    && atkOffsetPointConfig.Length % 2 == 0)
                {
                    Sango.Log.Error("TroopChangeSkillAttackRange 配置非法：Customize 需要 " +
                                    "[type,x1,y1,...] 的奇数长度，已忽略该次覆盖");
                    return;
                }

                // 【防护】DecaySpread 至少要 5 个元素（类型 / 圈数 / 概率 / 递减类型 / 递减数值），
                // 少配了会在 Skill.GetAttackCells 里退化成"只命中目标格"，波及完全失效。
                // 与其静默劣化，不如在装配阶段就报出来。
                if (atkOffsetPointConfig[0] == (int)SkillAttackOffsetType.DecaySpread
                    && atkOffsetPointConfig.Length < 5)
                {
                    Sango.Log.Error("TroopChangeSkillAttackRange 配置非法：DecaySpread 需要 " +
                                    "[11,atkRange,概率初始值,概率递减类型,概率递减数值] 五个元素，" +
                                    $"当前只有 {atkOffsetPointConfig.Length} 个，已忽略该次覆盖");
                    return;
                }

                // 每个技能实例各拿一份副本：TroopAddSkillAttackRange 会就地改
                // atkOffsetPoint[1]，共享同一个数组会串到别的技能实例上。
                skillInstance.atkOffsetPoint = (int[])atkOffsetPointConfig.Clone();
                return;
            }

            // 【防护】Customize 的语义是"成对偏移"(x1,y1,x2,y2...)，无法用 [type, value]
            // 两个元素表达。若配置把 atkType 写成 0（Customize），这里生成的 [0, range]
            // 会让 Skill.GetAttackCells 的 Customize 分支在 atkOffsetPoint[i+1] 越界崩溃。
            if (atkType == (int)SkillAttackOffsetType.Customize)
            {
                Sango.Log.Error("TroopChangeSkillAttackRange 配置非法：atkType=Customize 需要成对偏移，" +
                                "不能用 [type,value] 覆盖，已忽略该次覆盖");
                return;
            }
            skillInstance.atkOffsetPoint = new int[] { atkType, atkRange };
        }

        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
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
