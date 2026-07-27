using System;
using System.Collections.Generic;
using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 技能实例
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class SkillInstance : SangoObject
    {
        public override string Name => skill?.Name ?? string.Empty;
        public override int Id => skill?.Id ?? -1;

        /// <summary>
        /// 技能
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Skill>))]
        [JsonProperty]
        public Skill skill;

        /// <summary>
        /// 当前剩余冷却
        /// </summary>
        [JsonProperty]
        public int CDCount;

        /// <summary>
        /// 拥有者
        /// </summary>
        public Troop master;

        /// <summary>
        /// 基础攻击力
        /// </summary>
        [JsonProperty] public int atk;

        /// <summary>
        /// 基础攻城值
        /// </summary>
        [JsonProperty] public int atkDurability;

        /// <summary>
        /// 释放所需能量
        /// </summary>
        [JsonProperty] public int costEnergy;

        /// <summary>
        /// 是否可以对部队造成伤害
        /// </summary>
        [JsonProperty] public bool canDamageTroop;

        /// <summary>
        /// 是否可以对器械造成伤害
        /// </summary>
        [JsonProperty] public bool canDamageMachine;

        /// <summary>
        /// 是否可以对船只造成伤害
        /// </summary>
        [JsonProperty] public bool canDamageBoat;

        /// <summary>
        /// 是否可以对建筑造成伤害
        /// </summary>
        [JsonProperty] public bool canDamageBuilding;

        /// <summary>
        /// 是否可误伤
        /// </summary>
        [JsonProperty] public bool canDamageTeam;

        /// <summary>
        /// 是否可空放
        /// </summary>
        [JsonProperty] public bool canSpellToCell;

        /// <summary>
        /// 是否只可对友军释放
        /// </summary>
        [JsonProperty] public bool onlySpellToTeam;

        /// <summary>
        /// 施法范围
        /// </summary>
        [JsonProperty] public int[] spellRanges;

        /// <summary>
        /// 基础成功率
        /// </summary>
        [JsonProperty] public int successRate;

        /// <summary>
        /// 成功率加成
        /// </summary>
        [JsonProperty] public int[] successRateAdd;

        /// <summary>
        /// 额外的攻击位置
        /// </summary>
        [JsonProperty] public int[] atkOffsetPoint;

        /// <summary>
        /// 位移配置
        /// </summary>
        [JsonProperty] public int[] offsetAction;

        /// <summary>
        /// 碰撞伤害系数
        /// </summary>
        [JsonProperty] public int blockFactor;

        /// <summary>
        /// 成功率计算类型
        /// </summary>
        [JsonProperty] public string successMethod;

        /// <summary>
        /// 暴击率计算类型
        /// </summary>
        [JsonProperty] public string criticalMethod;

        /// <summary>
        /// 射程筛选逻辑
        /// </summary>
        [JsonProperty] public string rangeFilterMethod;

        /// <summary>
        /// 技能时间轴
        /// </summary>
        [JsonProperty] public SkillTimeline timeline;

        /// <summary>
        /// 技能时间轴实例
        /// 负责处理具体的时间控制逻辑
        /// </summary>
        private SkillTimelineInstance timelineInstance;

        protected List<SkillEffect> effects;
        protected SkillVisualizer skillVisualizer;
        public int tempCriticalFactor;

        public SkillSuccessMethod skillSuccessMethod;
        public SkillCriticalMethod skillCriticalMethod;
        public SkillRangeFilterMethod skillRangeFilterMethod;


        // 时间轴事件处理相关
        private List<Cell> tempTimelineCellList = new List<Cell>();

        protected void InitSkillEffects()
        {
            if (skill.skillEffects == null) return;
            if (skill.skillEffects.Count == 0) return;
            if (effects != null) return;
            effects = new List<SkillEffect>();
            for (int i = 0; i < skill.skillEffects.Count; i++)
            {
                JObject valus = skill.skillEffects[i] as JObject;
                SkillEffect eft = SkillEffect.Create(valus.Value<string>("class"));
                if (eft != null)
                {
                    eft.Init(valus, this);
                    effects.Add(eft);
                }
            }
        }

        /// <summary>
        /// 在势力开始时候调用
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public override bool OnForceTurnStart(Scenario scenario)
        {
            return true;
        }

        /// <summary>
        /// 根据技能创建技能实例
        /// </summary>
        /// <param name="skill"></param>
        /// <returns></returns>
        public static SkillInstance Create(Troop master, Skill skill)
        {
            SkillInstance ins = new SkillInstance();
            ins.Init(master, skill);
            return ins;
        }

        public void Init(Troop master, Skill skill)
        {
            this.skill = skill;
            this.master = master;
            CDCount = 0;
            atk = skill.atk;
            atkDurability = skill.atkDurability;
            costEnergy = skill.costEnergy;
            canDamageTroop = skill.canDamageTroop;
            canDamageMachine = skill.canDamageMachine;
            canDamageBoat = skill.canDamageBoat;
            canDamageBuilding = skill.canDamageBuilding;
            canDamageTeam = skill.canDamageTeam;
            canSpellToCell = skill.canSpellToCell;
            onlySpellToTeam = skill.onlySpellToTeam;
            spellRanges = skill.spellRanges;
            successRate = skill.successRate;
            atkOffsetPoint = skill.atkOffsetPoint;
            offsetAction = skill.offsetAction;
            blockFactor = skill.blockFactor;
            successMethod = skill.successMethod;
            criticalMethod = skill.criticalMethod;
            rangeFilterMethod = skill.rangeFilterMethod;

            InitSkillEffects();
            InitSkillVisualizer();
            InitTimeline();
            InitTimelineInstance();
            skillCriticalMethod = SkillCriticalMethod.Create(criticalMethod);
            skillSuccessMethod = SkillSuccessMethod.Create(successMethod);
            skillRangeFilterMethod = SkillRangeFilterMethod.Create(rangeFilterMethod);

            GameEvent.OnSkillCalculateAttribute?.Invoke(master, this);
        }

        protected void InitTimeline()
        {
            if (skill.timelineData != null)
            {
                timeline = new SkillTimeline();
                timeline.Init(skill.timelineData);
            }
        }

        /// <summary>
        /// 初始化时间轴实例
        /// </summary>
        protected void InitTimelineInstance()
        {
            if (timeline != null)
            {
                timelineInstance = new SkillTimelineInstance();
                timelineInstance.Init(timeline, this);
            }
        }

        protected void InitSkillVisualizer()
        {
            string visualType = skill.visualType ?? (IsRange() ? "Range" : "Default");
            skillVisualizer = SkillVisualizer.Create(visualType);
            if (skillVisualizer != null)
            {
                skillVisualizer.Init(this);
            }
        }

        /// <summary>
        /// 获取施放距离
        /// </summary>
        /// <param name="atker"></param>
        /// <param name="where"></param>
        /// <param name="cells"></param>
        public void GetSpellRange(Troop atker, Cell where, List<Cell> cells)
        {
            List<Cell> ranges = new List<Cell>();
            if (spellRanges == null || spellRanges.Length == 0)
            {
                ranges.Add(where);
            }
            else
            {
                for (int i = 0; i < spellRanges.Length; i++)
                {
                    Scenario.Cur.Map.GetRing(where, spellRanges[i], ranges, true);
                }
            }

            skillRangeFilterMethod?.Calculate(this, atker, where, ranges);
            cells.AddRange(ranges);
        }


        /// <summary>
        /// 获取攻击范围
        /// </summary>
        /// <param name="atker"></param>
        /// <param name="spell"></param>
        /// <param name="cells"></param>
        public void GetAttackCells(Troop atker, Cell spell, List<Cell> cells)
        {
            skill.GetAttackCells(atkOffsetPoint, atker, spell, cells);
        }

        /// <summary>
        /// 是否是远程技能
        /// </summary>
        /// <returns></returns>
        public bool IsRange()
        {
            return skill.IsRange();
        }

        /// <summary>
        /// 是否是计略技能
        /// </summary>
        /// <returns></returns>
        public bool IsStrategy()
        {
            return skill.IsStrategy();
        }

        public bool IsCritical()
        {
            return tempCriticalFactor > 100;
        }

        /// <summary>
        /// 是否是普攻
        /// </summary>
        /// <returns></returns>
        public bool IsNormal()
        {
            return skill.IsNormalSkill;
        }


        /// <summary>
        ///  是否是单体技能
        /// </summary>
        /// <returns></returns>
        public bool IsSingleSkill()
        {
            return skill.IsSingleSkill();
        }

        /// <summary>
        /// 是否有攻击效果
        /// </summary>
        /// <returns></returns>
        public bool HasEffect()
        {
            return skill.HasEffect();
        }


        public bool CanAddToTroop(Troop troop)
        {
            return skill.CanAddToTroop(troop);
        }

        public bool CanBeSpell(Troop troop)
        {
            //TODO: 完善技能释放规则
            if (skill.costEnergy > troop.morale)
                return false;

            return true;
        }

        public bool CanSpellToHere(Troop who, Cell where)
        {
            if (canSpellToCell)
                return true;

            if (canDamageTroop && where.troop != null)
            {
                bool isEnemy = where.troop.IsEnemy(who);
                if (onlySpellToTeam && !isEnemy)
                    return true;
                else if (isEnemy)
                    return true;
            }

            if (canDamageBuilding && where.building != null)
            {
                bool isEnemy = where.building.IsEnemy(who);
                if (onlySpellToTeam && !isEnemy)
                    return true;
                else if (isEnemy)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 成功率判断
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="spellCell"></param>
        /// <returns></returns>
        public bool CheckSuccess(Cell spellCell)
        {
            // 普通攻击,必中
            if (IsNormal()) return true;

            // 除开计略,目标只有有控制状态,必中
            if (spellCell.troop != null && !IsStrategy() && spellCell.troop.HasControlBuff())
                return true;

            Troop troop = master;
            int baseSuccessRate = 0;
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(baseSuccessRate);
            GameEvent.OnTroopBeforeCalculateSkillSuccess?.Invoke(troop, this, spellCell, overrideData);
            baseSuccessRate = overrideData.ValueAndRecycle;

            if (baseSuccessRate >= 100)
            {
#if SANGO_DEBUG
                Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} 部队 准备释放技能: {Name} =>({spellCell.x},{spellCell.y})] 成功率:{baseSuccessRate}");
#endif
                return true;
            }
            else if (baseSuccessRate <= -100)
            {
#if SANGO_DEBUG
                Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} 部队 准备释放技能: {Name} =>({spellCell.x},{spellCell.y})] 成功率:{baseSuccessRate}");
#endif
                return false;
            }

            if (skillSuccessMethod == null)
            {
#if SANGO_DEBUG
                Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} 部队 准备释放技能: {Name} =>({spellCell.x},{spellCell.y})] 成功率:{successMethod}");
#endif
                return false;
            }

            baseSuccessRate = skillSuccessMethod.Calculate(this, troop, spellCell);

            overrideData = Tools.OverrideData<int>.Create(baseSuccessRate);
            GameEvent.OnTroopAfterCalculateSkillSuccess?.Invoke(troop, this, spellCell, overrideData);
            baseSuccessRate = overrideData.ValueAndRecycle;

#if SANGO_DEBUG
            Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} 部队 准备释放技能: {Name} =>({spellCell.x},{spellCell.y})] 成功率:{baseSuccessRate}");
#endif
            return GameRandom.Chance(baseSuccessRate);
        }

        /*
        *【战法爆击率】
           1、如果有必爆特技则爆击率为100％，无必爆则执行战法爆击率判断。
           2、战法爆击率 ＝ A + B + C
           A：部队武力爆击加成：武力60以下＝0％；武力在60～79之间＝1％；武力大于等于80＝2％
           B：部队适性爆击加成：C＝0％，B＝1％，A＝2％，S＝3％，依次推类
           C：主副将关系爆击加成：
            如果副将亲爱主将＋2％；
            如果副将与主将结义或结婚＋4％；
            如果副将厌恶主将－5％；
           注：每名副将单独计算，即2员副将都亲爱主将＋4％，一仲介一厌恶则－1％；

        * 
        **/
        /// <summary>
        /// 暴击率检查
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="spellCell"></param>
        /// <returns></returns>
        public int CheckCritical(Cell spellCell)
        {
            ScenarioVariables scenarioVariables = Scenario.Cur.Variables;
            Troop troop = master;

            // 必爆流程判断
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(0);
            GameEvent.OnTroopBeforeCalculateSkillCritical?.Invoke(troop, this, spellCell, overrideData);
            if (overrideData.Value >= 100)
            {
                overrideData.Recycle();
                overrideData = Tools.OverrideData<int>.Create(scenarioVariables.skillCriticalFactor);
                GameEvent.OnTroopCalculateSkillCriticalFactor?.Invoke(troop, this, spellCell, overrideData);
                tempCriticalFactor = overrideData.Value;
                overrideData.Recycle();
                return tempCriticalFactor;
            }
            else if (overrideData.Value <= -100)
            {
                tempCriticalFactor = 100;
                overrideData.Recycle();
                return tempCriticalFactor;
            }

            if (skillCriticalMethod == null)
            {
                tempCriticalFactor = 100;
                overrideData.Recycle();
                return tempCriticalFactor;
            }

            int basCriticalRate = skillCriticalMethod.Calculate(this, troop, spellCell);
            overrideData = Tools.OverrideData<int>.Create(basCriticalRate);
            GameEvent.OnTroopAfterCalculateSkillSuccess?.Invoke(troop, this, spellCell, overrideData);
            basCriticalRate = overrideData.ValueAndRecycle;

            int criticalFactor = 100;
            if (GameRandom.Chance(basCriticalRate))
            {
                criticalFactor = scenarioVariables.skillCriticalFactor;
                overrideData = Tools.OverrideData<int>.Create(criticalFactor);
                GameEvent.OnTroopCalculateSkillCriticalFactor?.Invoke(troop, this, spellCell, overrideData);
                criticalFactor = overrideData.ValueAndRecycle;
            }
            tempCriticalFactor = criticalFactor;
            return criticalFactor;
        }


        public bool UpdateRender(Cell spellCell, Scenario scenario, float time, System.Action action)
        {
            if (time <= 0f)
            {

                master.Render.FaceTo(spellCell.Position);
                tempTimelineCellList.Clear();
                GetAttackCells(master, spellCell, tempTimelineCellList);

                // 重置时间轴实例
                if (timelineInstance != null)
                {
                    timelineInstance.Reset();
                }

                if (master.Render.IsVisible())
                {
                    GameMedia.Instance.PlaySfx(skill.actionSound);
                }
            }

            // 使用时间轴实例处理技能表现
            if (timelineInstance != null)
            {
                bool isComplete = timelineInstance.ProcessEvents(master, spellCell, time, action);
                if (isComplete)
                {
                    master.Render.SetAniShow(0);
                    return true;
                }
            }
            else
            {
                // 传统处理方式（兼容旧技能）
                if (time <= 0f)
                {
                    // 播放技能视觉效果
                    PlaySkillVisual(master, spellCell, tempTimelineCellList);
                }
                if (time > 1.2f)
                    action();
                if (time > 2.5f)
                {
                    master.Render.SetAniShow(0);
                    return true;
                }
            }
            return false;
        }

        List<Cell> tempCellList = new List<Cell>();

        public void Action(Cell spellCell, int criticalFactor)
        {
            Troop troop = master;
            Scenario scenario = Scenario.Cur;
            ScenarioVariables scenarioVariables = scenario.Variables;
            Troop targetTroop = spellCell.troop;
            BuildingBase targetBuilding = spellCell.building;
            Tools.OverrideData<int> overrideData;

            List<SangoObject> activedTargetList = new List<SangoObject>();
            //TODO: 释放技能
            tempCellList.Clear();
            GetAttackCells(troop, spellCell, tempCellList);
            int targetDamage = 0;
            for (int i = 0; i < tempCellList.Count; i++)
            {
                Cell atkCell = tempCellList[i];
                Troop beAtkTroop = atkCell.troop;

                if (atk > 0 && beAtkTroop != null && canDamageTroop && (troop.IsEnemy(beAtkTroop) || canDamageTeam))
                {
                    int damage = Troop.CalculateSkillDamage(troop, beAtkTroop, this) * criticalFactor / 100;
                    if (damage < 0)
                        damage = 0;

                    Tools.OverrideData<int> damage_overrideData = Tools.OverrideData<int>.Create(damage);
                    GameEvent.OnSkillDamageTroop?.Invoke(this, beAtkTroop, damage_overrideData);
                    damage = damage_overrideData.Value;

                    beAtkTroop.ChangeTroops(-damage, troop, this, 0);

                    int ep = damage / 100;
                    if (!beAtkTroop.IsAlive) ep += 50;
                    troop.ForEachPerson(p =>
                    {
                        p.GainExp(ep);
                        p.merit += ep;
                    });
#if SANGO_DEBUG
                    Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 使用<{this.Name}> 攻击 {beAtkTroop.BelongForce.Name}的[{beAtkTroop.Name} - {beAtkTroop.TroopType.Name}], 造成伤害:{damage}, 目标剩余兵力: {beAtkTroop.GetTroopsNum()}");
#endif
                    // 反击
                    if (beAtkTroop.IsAlive && targetTroop == beAtkTroop && !beAtkTroop.HasControlBuff())
                    {
                        targetDamage = damage;
                        int hitBack = beAtkTroop.GetAttackBackFactor(this, Scenario.Cur.Map.Distance(troop.cell, spellCell));
                        overrideData = Tools.OverrideData<int>.Create(hitBack);
                        GameEvent.OnTroopCalculateAttackBack?.Invoke(troop, beAtkTroop, this, scenario, overrideData);
                        hitBack = overrideData.ValueAndRecycle;

                        if (hitBack > 0)
                        {
                            if (this.IsRange())
                            {
                                int hitBackDmg = hitBack * Troop.CalculateSkillDamage(beAtkTroop, troop, beAtkTroop.NormalRangeSkill) / 100;
                                troop.ChangeTroops(-hitBackDmg, beAtkTroop, beAtkTroop.NormalRangeSkill, hitBack);
#if SANGO_DEBUG
                                Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 受到 {beAtkTroop.BelongForce.Name}的[{beAtkTroop.Name} - {beAtkTroop.TroopType.Name}]反击伤害:{hitBackDmg}, 目标剩余兵力: {troop.GetTroopsNum()}");
#endif
                                ep = hitBackDmg / 100;
                                if (!troop.IsAlive) ep += 50;
                                beAtkTroop.ForEachPerson(p =>
                                {
                                    p.GainExp(ep);
                                    p.merit += ep;
                                });
                            }
                            else
                            {
                                int hitBackDmg = hitBack * Troop.CalculateSkillDamage(beAtkTroop, troop, beAtkTroop.NormalSkill) / 100;
                                troop.ChangeTroops(-hitBackDmg, beAtkTroop, beAtkTroop.NormalSkill, hitBack);
#if SANGO_DEBUG
                                Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 受到 {beAtkTroop.BelongForce.Name}的[{beAtkTroop.Name} - {beAtkTroop.TroopType.Name}]反击伤害:{hitBackDmg}, 目标剩余兵力: {troop.GetTroopsNum()}");
#endif
                                ep = hitBackDmg / 100;
                                if (!troop.IsAlive)
                                {
                                    // 获取对方部分钱粮
                                    int getFood = troop.food * scenarioVariables.defeatTroopCanGainFoodFactor / 100;
                                    int getGold = troop.gold * scenarioVariables.defeatTroopCanGainGoldFactor / 100;
                                    if (getFood > 0)
                                    {
                                        beAtkTroop.ChangeFood(getFood);
                                    }
                                    if (getGold > 0)
                                    {
                                        beAtkTroop.ChangeGold(getGold);
                                    }

                                    ep += 50;
                                }
                                beAtkTroop.ForEachPerson(p =>
                                {
                                    p.GainExp(ep);
                                    p.merit += ep;
                                });

                            }
                        }
                    }

                    GameEvent.OnSkillDamageTroopAfter?.Invoke(this, beAtkTroop, damage_overrideData);
                }

                BuildingBase beAtkBuildingBase = atkCell.building;
                if (atkDurability > 0 && beAtkBuildingBase != null && this.canDamageBuilding && (troop.IsEnemy(beAtkBuildingBase) || this.canDamageTeam))
                {
                    // 一个目标只会收到一次伤害
                    if (activedTargetList.Contains(beAtkBuildingBase))
                        continue;
                    activedTargetList.Add(beAtkBuildingBase);
                    if (beAtkBuildingBase is City)
                    {
                        City city = (City)beAtkBuildingBase;
                        int damage_troops = Troop.CalculateSkillDamageTroopOnCity(troop, city, this) * criticalFactor / 100;
#if SANGO_DEBUG
                        Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 使用<{this.Name}> 攻击 {beAtkBuildingBase.BelongForce?.Name}的 [{beAtkBuildingBase.Name}], 造成兵力伤害:{damage_troops}, 目标剩余兵力: {city.troops}");
#endif
                        overrideData = Tools.OverrideData<int>.Create(damage_troops);
                        GameEvent.OnSkillDamageBuildingTroops?.Invoke(this, beAtkBuildingBase, overrideData);
                        damage_troops = overrideData.ValueAndRecycle;
                        int ep = damage_troops / 100;
                        if (!city.ChangeTroops(-damage_troops, troop, city.BelongForce != null))
                        {
                            ep += 100;
                            troop.ForEachPerson(p =>
                            {
                                p.GainExp(ep);
                                p.merit += ep;
                            });
#if SANGO_DEBUG
                            Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 攻破城池: <{beAtkBuildingBase.Name}>");
#endif
                            city.OnFall(troop);
                            return;
                        }
                    }

                    int damage = Troop.CalculateSkillDamage(troop, beAtkBuildingBase, this) * criticalFactor / 100;
#if SANGO_DEBUG
                    Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 使用<{this.Name}> 攻击 {beAtkBuildingBase.BelongForce?.Name}的 [{beAtkBuildingBase.Name}], 造成耐久伤害:{damage}, 目标剩余耐久: {beAtkBuildingBase.durability}");
#endif
                    overrideData = Tools.OverrideData<int>.Create(damage);
                    GameEvent.OnSkillDamageBuildingDurability?.Invoke(this, beAtkBuildingBase, overrideData);
                    damage = overrideData.ValueAndRecycle;

                    if (beAtkBuildingBase.ChangeDurability(-damage, troop))
                    {
                        int ep = damage / 10 + 100;
                        troop.ForEachPerson(p =>
                        {
                            p.GainExp(ep);
                            p.merit += ep;
                        });
#if SANGO_DEBUG

                        Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 破坏建筑: <{beAtkBuildingBase.Name}>");
#endif
                    }
                    else
                    {
                        int ep = damage / 10;
                        troop.ForEachPerson(p =>
                        {
                            p.GainExp(ep);
                            p.merit += ep;
                        });

                        // 城池反击
                        if (targetBuilding == beAtkBuildingBase)
                        {
                            float hitBack = beAtkBuildingBase.GetAttackBackFactor(this, Scenario.Cur.Map.Distance(troop.cell, atkCell));
                            if (hitBack > 0)
                            {
                                int atkBack = beAtkBuildingBase.GetAttackBack();
                                overrideData = Tools.OverrideData<int>.Create(atkBack);
                                GameEvent.OnBuildCalculateAttackBack?.Invoke(troop, spellCell, beAtkBuildingBase, this, overrideData);
                                atkBack = overrideData.ValueAndRecycle;
                                if (atkBack > 0)
                                {
                                    int hitBackDmg = (int)System.Math.Ceiling(hitBack * Troop.CalculateSkillDamage(beAtkBuildingBase, troop, atkBack));
                                    troop.ChangeTroops(-hitBackDmg, beAtkBuildingBase, null, atkBack);
#if SANGO_DEBUG
                                    Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 受到 {beAtkBuildingBase.BelongForce?.Name}的[{beAtkBuildingBase.Name}]反击伤害:{hitBackDmg}, 目标剩余兵力: {troop.GetTroopsNum()}");
#endif
                                }
                            }
                        }
                    }


                }
            }

            DoOffset(troop, targetTroop, targetDamage);

            DoEffect(troop, spellCell, tempCellList);

            if (troop.IsAlive)
            {
                troop.ChangeMorale(-this.costEnergy, false);
            }

            GameEvent.OnSkillActionOver?.Invoke(this);
        }

        public void DoOffset(Troop troop, Troop targetTroop, int targetDamage)
        {
            if (targetTroop == null || offsetAction == null || offsetAction.Length <= 0) return;
            List<Cell> checkList = new List<Cell>();
            int dir = troop.cell.DirectionTo(targetTroop.cell);
            for (int k = 0; k < this.offsetAction.Length; k += 2)
            {
                int offsetType = this.offsetAction[k];
                int offsetLength = this.offsetAction[k + 1];
                int targetDir = dir;
                int absOffsetLength = Mathf.Abs(offsetLength);
                if (offsetLength < 0)
                    targetDir -= 3;
                checkList.Clear();
                switch (offsetType)
                {
                    case (int)SkillCellOffsetType.Master:
                        {
                            if (troop.IsAlive)
                            {
                                troop.cell.GetDirectionLine(targetDir, absOffsetLength, checkList);
                                for (int i = 0; i < checkList.Count; i++)
                                {
                                    Cell c = checkList[i];
                                    if (c.CanStay(troop))
                                        troop.UpdateCell(c, troop.cell, true);
                                    else
                                        break;
                                }
                            }
                        }
                        break;
                    case (int)SkillCellOffsetType.Target:
                        {
                            if (targetTroop.IsAlive)
                            {
                                targetTroop.cell.GetDirectionLine(targetDir, absOffsetLength, checkList);
                                for (int i = 0; i < checkList.Count; i++)
                                {
                                    Cell c = checkList[i];
                                    if (c.CanStay(targetTroop))
                                        targetTroop.UpdateCell(c, targetTroop.cell, true);
                                    else
                                        break;
                                }
                            }
                        }
                        break;
                    case (int)SkillCellOffsetType.MasterBlock:
                        {
                            if (troop.IsAlive)
                            {
                                troop.cell.GetDirectionLine(targetDir, absOffsetLength, checkList);
                                for (int i = 0; i < checkList.Count; i++)
                                {
                                    Cell c = checkList[i];
                                    if (c.CanStay(troop))
                                        troop.UpdateCell(c, troop.cell, true);
                                    else
                                    {
                                        Troop blockTroop = c.troop;
                                        if (this.blockFactor > 0 && blockTroop != null && blockTroop.IsEnemy(troop))
                                        {
                                            int blockDmg = targetDamage * this.blockFactor / 100;
                                            blockTroop.ChangeTroops(-blockDmg, troop, this, -blockFactor);
                                            int ep = blockDmg / 100;
                                            if (!blockTroop.IsAlive) ep += 50;
                                            troop.ForEachPerson(p =>
                                            {
                                                p.GainExp(ep);
                                                p.merit += ep;
                                            });
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    case (int)SkillCellOffsetType.TargetBlock:
                        {
                            if (targetTroop.IsAlive)
                            {
                                targetTroop.cell.GetDirectionLine(targetDir, absOffsetLength, checkList);
                                for (int i = 0; i < checkList.Count; i++)
                                {
                                    Cell c = checkList[i];
                                    if (c.CanStay(troop))
                                        targetTroop.UpdateCell(c, targetTroop.cell, true);
                                    else
                                    {
                                        Troop blockTroop = c.troop;
                                        if (this.blockFactor > 0 && blockTroop != null && blockTroop.IsEnemy(troop))
                                        {
                                            int blockDmg = targetDamage * this.blockFactor / 100;
                                            blockTroop.ChangeTroops(-blockDmg, troop, this, -blockFactor);
                                            int ep = blockDmg / 100;
                                            if (!blockTroop.IsAlive) ep += 50;
                                            troop.ForEachPerson(p =>
                                            {
                                                p.GainExp(ep);
                                                p.merit += ep;
                                            });
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    case (int)SkillCellOffsetType.MasterJustCheckEnd:
                        {
                            if (troop.IsAlive)
                            {
                                troop.cell.GetDirectionLine(targetDir, absOffsetLength, checkList);
                                if (checkList.Count == 0)
                                {
                                    int cc = 123;
                                    cc++;
                                }
                                for (int i = checkList.Count - 1; i >= 0; i--)
                                {
                                    Cell c = checkList[i];
                                    if (c.CanStay(troop))
                                    {
                                        troop.UpdateCell(c, troop.cell, true);
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    case (int)SkillCellOffsetType.MasterRandom:
                        {
                            if (troop.IsAlive)
                            {
                                // 收集周围可移动的格子
                                List<Cell> availableCells = new List<Cell>();
                                troop.cell.GetNeighbors((cell) =>
                                {
                                    if (cell.CanStay(troop))
                                        availableCells.Add(cell);
                                });
                                // 随机选择一个格子位移
                                if (availableCells.Count > 0)
                                {
                                    int randomIndex = UnityEngine.Random.Range(0, availableCells.Count);
                                    Cell targetCell = availableCells[randomIndex];
                                    troop.UpdateCell(targetCell, troop.cell, true);
                                }
                            }
                        }
                        break;
                    case (int)SkillCellOffsetType.TargetRandom:
                        {
                            if (targetTroop.IsAlive)
                            {
                                // 收集周围可移动的格子
                                List<Cell> availableCells = new List<Cell>();
                                targetTroop.cell.GetNeighbors((cell) =>
                                {
                                    if (cell.CanStay(targetTroop))
                                        availableCells.Add(cell);
                                });
                                // 随机选择一个格子位移
                                if (availableCells.Count > 0)
                                {
                                    int randomIndex = UnityEngine.Random.Range(0, availableCells.Count);
                                    Cell targetCell = availableCells[randomIndex];
                                    targetTroop.UpdateCell(targetCell, targetTroop.cell, true);
                                }
                            }
                        }
                        break;
                    case (int)SkillCellOffsetType.Master指定位置:
                        {
                            if (troop.IsAlive && offsetAction.Length > k + 2)
                            {
                                // 从offsetAction数组中获取指定位置的坐标
                                int targetX = offsetAction[k + 2];
                                int targetY = offsetAction[k + 3];
                                Cell targetCell = Scenario.Cur.Map.GetCell(targetX, targetY);
                                if (targetCell != null && targetCell.CanStay(troop))
                                {
                                    troop.UpdateCell(targetCell, troop.cell, true);
                                }
                            }
                        }
                        break;
                    case (int)SkillCellOffsetType.Target指定位置:
                        {
                            if (targetTroop.IsAlive && offsetAction.Length > k + 2)
                            {
                                // 从offsetAction数组中获取指定位置的坐标
                                int targetX = offsetAction[k + 2];
                                int targetY = offsetAction[k + 3];
                                Cell targetCell = Scenario.Cur.Map.GetCell(targetX, targetY);
                                if (targetCell != null && targetCell.CanStay(targetTroop))
                                {
                                    targetTroop.UpdateCell(targetCell, targetTroop.cell, true);
                                }
                            }
                        }
                        break;
                }
            }
        }

        public void DoEffect(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            InitSkillEffects();
            if (effects == null || effects.Count == 0) return;

            foreach (Cell target in atkCellList)
            {
                Cell atkCell = target;
                Troop beAtkTroop = atkCell.troop;
                if (beAtkTroop != null && canDamageTroop && (troop.IsEnemy(beAtkTroop) || canDamageTeam))
                {
                    effects.ForEach(s => s.Action(target));
                }
                else
                {
                    BuildingBase beAtkBuildingBase = atkCell.building;
                    if (beAtkBuildingBase != null && this.canDamageBuilding && (troop.IsEnemy(beAtkBuildingBase) || this.canDamageTeam))
                    {
                        effects.ForEach(s => s.Action(target));
                    }
                }
            }
        }

        public void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            if (skillVisualizer != null)
            {
                skillVisualizer.PlaySkillVisual(troop, spellCell, atkCellList);
            }
        }

        public override void Clear()
        {
            if (effects != null)
            {
                effects.ForEach(s => s.Clear());
                effects.Clear();
            }
            if (skillVisualizer != null)
            {
                skillVisualizer.StopSkillVisual();
                skillVisualizer = null;
            }
        }

        public int GerPersonSay()
        {
            if (skill.personSound == null || skill.personSound.Length == 0) return 0;
            return skill.personSound[GameRandom.Range(0, skill.personSound.Length)];
        }

    }
}
