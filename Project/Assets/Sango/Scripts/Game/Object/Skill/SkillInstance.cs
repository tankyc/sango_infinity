using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public override SangoObjectType ObjectType { get { return SangoObjectType.SkillInstance; } }

        /// <summary>
        /// 技能的 id。存档里只有这个数值，读取 skill 时按需解析。
        /// </summary>
        [JsonProperty("skill")]
        public int skillId;

        Skill mSkill;
        /// <summary>
        /// 技能对象；写入时自动同步 skillId
        /// </summary>
        public Skill skill
        {
            get
            {
                if (mSkill == null && skillId > 0) mSkill = IdRef.Resolve<Skill>(skillId);
                return mSkill;
            }
            set { mSkill = value; skillId = value != null ? value.Id : 0; }
        }

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
        /// 【AI 评分】本战法的动作常量，见 <see cref="Skill.aiActionBonus"/>。
        /// 0 表示未配置，评分时回落到 AIConfig.o311TacticActionBonus。
        /// </summary>
        [JsonProperty] public int aiActionBonus;

        /// <summary>计略类型，见 <see cref="Skill.strategyType"/> 与 <see cref="StrategyType"/>。</summary>
        [JsonProperty] public int strategyType;

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
        /// 释放完成后是否允许挑起单挑。
        /// 初值由战法定义 Skill.canTriggerDuel 复制而来（见 Init），
        /// 因此可在运行期被改写：Init 末尾的 GameEvent.OnSkillCalculateAttribute 里挂 Action 即可按需禁掉；
        /// 旧存档里没有该字段时保持 true，行为与加入本开关之前一致。
        /// </summary>
        [JsonProperty] public bool canTriggerDuel = true;

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
        /// 可释放筛选逻辑
        /// </summary>
        [JsonProperty] public string spellConditionMethod;

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
        public int tempSuccessFactor;

        public SkillSuccessMethod skillSuccessMethod;
        public SkillCriticalMethod skillCriticalMethod;
        public SkillRangeFilterMethod skillRangeFilterMethod;
        public SkillSpellConditionMethod skillSpellConditionMethod;

        public bool isAdd = false;

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
            // 【防御】skill 为 null 时不能构造实例：Init 里第一件事就是读 skill.atk，
            // 直接抛 NullReferenceException（实测栈：TroopReplaceSkill.OnTroopCalculateAttribute
            // → SkillInstance.Create → Init:236，AI 出阵时整个回合被打断）。
            // 返回 null，由调用方判空 —— 与本工程既有约定一致（TroopAttackBack.cs:32-33
            // 就是 `if (skillInstance != null)` 这样写的）。
            if (skill == null)
            {
                Sango.Log.Error("SkillInstance.Create 收到 null 战法（技能 Id 在本局技能表中不存在），已返回 null");
                return null;
            }

            SkillInstance ins = new SkillInstance();
            ins.Init(master, skill);
            return ins;
        }

        public void Init(Troop master, Skill skill)
        {
            if (skill == null)
            {
                Sango.Log.Error("SkillInstance.Init 收到 null 战法，实例未初始化");
                return;
            }

            this.skill = skill;
            this.master = master;
            CDCount = 0;
            atk = skill.atk;
            atkDurability = skill.atkDurability;
            aiActionBonus = skill.aiActionBonus;
            strategyType = skill.strategyType;
            costEnergy = skill.costEnergy;
            canDamageTroop = skill.canDamageTroop;
            canDamageMachine = skill.canDamageMachine;
            canDamageBoat = skill.canDamageBoat;
            canDamageBuilding = skill.canDamageBuilding;
            canDamageTeam = skill.canDamageTeam;
            canSpellToCell = skill.canSpellToCell;
            onlySpellToTeam = skill.onlySpellToTeam;
            canTriggerDuel = skill.canTriggerDuel;
            spellRanges = skill.spellRanges;
            successRate = skill.successRate;
            atkOffsetPoint = skill.atkOffsetPoint;
            offsetAction = skill.offsetAction;
            blockFactor = skill.blockFactor;
            successMethod = skill.successMethod;
            criticalMethod = skill.criticalMethod;
            rangeFilterMethod = skill.rangeFilterMethod;
            spellConditionMethod = skill.spellConditionMethod;

            InitSkillEffects();
            InitSkillVisualizer();
            InitTimeline();
            InitTimelineInstance();
            skillCriticalMethod = SkillCriticalMethod.Create(criticalMethod);
            skillSuccessMethod = SkillSuccessMethod.Create(successMethod);
            skillRangeFilterMethod = SkillRangeFilterMethod.Create(rangeFilterMethod);
            skillSpellConditionMethod = SkillSpellConditionMethod.Create(spellConditionMethod);

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
            string visualType = skill.visualType ?? (IsStrategy() ? "Strategy" : (IsRange() ? "Range" : "Default"));
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
            // 【性能】没有范围过滤器的技能占绝大多数（过滤器只有少数技能配）。
            // 这种情况直接把环形结果追加进调用方的列表，省掉每次调用 new List<Cell>() 的一次分配
            // —— AI 做一次全量技能评估会调用几百次，实测单次评估光这里就产生约 260KB GC。
            // 语义与原来完全一致：GetRing 只做 Add，不读列表原有内容。
            if (skillRangeFilterMethod == null)
            {
                if (spellRanges == null || spellRanges.Length == 0)
                {
                    cells.Add(where);
                }
                else
                {
                    for (int i = 0; i < spellRanges.Length; i++)
                    {
                        Scenario.Cur.Map.GetRing(where, spellRanges[i], cells, true);
                    }
                }
                return;
            }

            // 有过滤器时必须让过滤器只看到"本次算出的范围"，
            // 不能看到 cells 里已有的历史内容（部分调用方会跨技能累积同一个列表）。
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
            // 【防御】Customize 的格式必须是 [type, x1, y1, x2, y2, ...]，长度 1 + 2n（奇数）。
            // 本字段带 [JsonProperty]（第 136 行），会被存档读写；若存档里残留了偶数长度的
            // [0, range]（旧版本 TroopChangeSkillAttackRange 的产物），Skill.GetAttackCells
            // 的 Customize 分支会在 atkOffsetPoint[i + 1] 抛 IndexOutOfRangeException，
            // 直接把整回合 AI 打断（实测栈：TroopOccupyCity.Prepare → PriorityAction）。
            // 这里提前拦下：打印技能与内容，并把脏数组清空自愈（退化为仅命中目标格），
            // 避免每回合反复报同一条错误。
            if (atkOffsetPoint != null && atkOffsetPoint.Length > 0
                && atkOffsetPoint[0] == (int)SkillAttackOffsetType.Customize
                && atkOffsetPoint.Length % 2 == 0)
            {
                Sango.Log.Error(
                    $"技能[{(skill != null ? skill.Id : -1)}]{(skill != null ? skill.Name : "?")} 的命中偏移" +
                    $" atkOffsetPoint 非法：长度 {atkOffsetPoint.Length}，内容 [{string.Join(",", atkOffsetPoint)}]；" +
                    " Customize 需要 [type,x1,y1,...] 的奇数长度，已清空并退化为仅命中目标格");
                atkOffsetPoint = null;
                cells.Add(spell);
                return;
            }

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

        /// <summary>
        /// 转发到 <see cref="Skill.GetCombatExpEvent"/>：取本次施法对应的原版 Exp 表事件行。
        /// 失败流程（<c>TroopSpellSkillFailEvent</c>）也要用它拿 miss 行，所以公开出来。
        /// </summary>
        public CombatExpEvent GetCombatExpEvent(bool hit, bool critical)
        {
            return skill.GetCombatExpEvent(hit, critical);
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


        public bool CanAddToTroop(Troop troop, bool isWater)
        {
            return skill.CanAddToTroop(troop, isWater);
        }

        public bool CanBeSpell(Troop troop)
        {
            //TODO: 完善技能释放规则
            if (skill.costEnergy > troop.morale)
                return false;

            return true;
        }

        /// <summary>
        /// 能否对该格施放。
        ///
        /// 【归属闸门】格子上有活体对象时**先判归属**，再决定能否"对地"施放：
        ///   · <see cref="onlySpellToTeam"/>（消火 / 镇静这类对己方计略）→ 敌对目标不可施放；
        ///   · 攻击型且不能误伤己方（火计等）→ 友军目标不可施放，无主建筑放行；
        ///   · <see cref="canDamageTeam"/> 的攻击型（妖术 / 落雷）保留本项目的"误伤"玩法，不拦友军。
        ///
        /// 【原版对应】get_enabled_strategies（s11_sys_unit_target.cpp:364-389）：
        ///   火计看 <c>can_attack</c>（部队 = is_enemy；建筑 = force_id &lt; 0 || is_enemy）；
        ///   消火看 <c>not hostile_target</c>，敌对目标根本不开启该命令。
        ///
        /// 【修复】原先 <see cref="canSpellToCell"/> 在最前面直接 return true，把下面这层归属
        /// 判定整个短路掉 —— 而「灭火」同时配了 canSpellToCell = true 与 onlySpellToTeam = true
        /// （Skills.json:947-948），后者于是成了死配置，AI 与玩家都能对敌方城池/部队放灭火。
        /// </summary>
        public bool CanSpellToHere(Troop who, Cell where)
        {
            if (skillSpellConditionMethod != null)
            {
                if (!skillSpellConditionMethod.Check(this, this.master, where))
                    return false;
            }

            if (canDamageTroop && where.troop != null)
            {
                bool isEnemy = where.troop.IsEnemy(who);
                if (onlySpellToTeam)
                {
                    if (isEnemy) return false;
                }
                else if (!canDamageTeam && !isEnemy)
                {
                    return false;
                }
                return true;
            }

            if (canDamageBuilding && where.building != null)
            {
                // 无主建筑（BelongForce == null）在原版 can_attack 里是"可攻击"、在 hostile_target
                // 里是"非敌对"：两种口径都不算友军，因此这里只对**有主且非敌**的建筑拦截。
                bool hasOwner = where.building.BelongForce != null;
                bool isEnemy = hasOwner && where.building.IsEnemy(who);
                if (onlySpellToTeam)
                {
                    if (isEnemy) return false;
                }
                else if (!canDamageTeam && hasOwner && !isEnemy)
                {
                    return false;
                }
                return true;
            }

            // 格子上没有可作用的活体对象：只有 canSpellToCell 的技能才能"对空格"施放
            // （火计对可燃地形 / 灭火对空着的着火格）。
            return canSpellToCell;
        }

        /// <summary>
        /// 成功率判断
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="spellCell"></param>
        /// <returns></returns>
        public bool CheckSuccess(Cell spellCell)
        {
            tempSuccessFactor = 1;

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
            Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} 部队 准备释放技能: {Name} =>({spellCell.x},{spellCell.y})] 成功率:{baseSuccessRate}");
                return true;
            }
            else if (baseSuccessRate <= -100)
            {
            Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} 部队 准备释放技能: {Name} =>({spellCell.x},{spellCell.y})] 成功率:{baseSuccessRate}");
                tempSuccessFactor = 0;
                    return false;
            }

            if (skillSuccessMethod == null)
            {
                Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} 部队 准备释放技能: {Name} =>({spellCell.x},{spellCell.y})] 成功率:{successMethod}");
                tempSuccessFactor = 0;
                return false;
            }

            baseSuccessRate = skillSuccessMethod.Calculate(this, troop, spellCell);

            overrideData = Tools.OverrideData<int>.Create(baseSuccessRate);
            GameEvent.OnTroopAfterCalculateSkillSuccess?.Invoke(troop, this, spellCell, overrideData);
            baseSuccessRate = overrideData.ValueAndRecycle;

            Sango.Log.Info($"{troop.BelongForce.Name}的[{troop.Name} 部队 准备释放技能: {Name} =>({spellCell.x},{spellCell.y})] 成功率:{baseSuccessRate}");
            bool b_suc = GameRandom.Chance(baseSuccessRate);
            if(!b_suc)
            {
                tempSuccessFactor = 0;
            }
            return b_suc;
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
            GameEvent.OnTroopAfterCalculateSkillCritical?.Invoke(troop, this, spellCell, overrideData);
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


        // 落雷技能Id与结算时序:第二段特效序列 519(1.775~4.575s)→522(2.15~3.45s)→521×3(上2.205/中2.305/下2.405开始,各2.16s)
        // 三处间隔0.1s消失,下处最晚4.565s结束;伤害结算再提前0.5s到3.82s(雷击连击中段即出数字)
        const int ThunderSkillId = 29;
        const float ThunderDamageDelay = 3.82f;
        const float ThunderSkillOverTime = 4.6f;
        const int ThunderDamageMin = 1500;
        const int ThunderDamageMax = 2500;

        // 妖术(Id=30):buff在524特效播放完毕后结算,让负面状态图标与光效视觉联动更紧密
        // 524时序:startDelay(1.475)+前12帧/9fps(1.333)+后20帧/40fps(0.5)=3.308s
        const int DemonSkillId = 30;
        const float DemonEffectDelay = 3.3f;
        const float DemonSkillOverTime = 4.5f;

        public bool UpdateRender(Cell spellCell, Scenario scenario, float time, System.Action action)
        {
            if (time <= 0f)
            {
                master.FaceTo(spellCell);
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
                if (skill.Id == ThunderSkillId)
                {
                    if (time > ThunderDamageDelay)
                        action();
                    if (time > ThunderSkillOverTime)
                    {
                        master.Render.SetAniShow(0);
                        return true;
                    }
                }
                else if (skill.Id == DemonSkillId)
                {
                    // 妖术:buff在525特效结束后结算
                    if (time > DemonEffectDelay)
                        action();
                    if (time > DemonSkillOverTime)
                    {
                        master.Render.SetAniShow(0);
                        return true;
                    }
                }
                else
                {
                    if (time > 1.2f)
                        action();
                    if (time > 2.5f)
                    {
                        master.Render.SetAniShow(0);
                        return true;
                    }
                }
            }
            return false;
        }

        List<Cell> tempCellList = new List<Cell>();

        /// <summary>
        /// 援助攻击标记: 由 Troop.DoAssistAttack 在把施法事件排入队列后挂上, Action 结算时一次性消耗。
        /// 挂在技能实例上而不是部队上: 一是只有本次施法会被算成援助, 不会残留到该部队的下一次普通攻击;
        /// 二是技能时间轴(SkillTimelineEvent_ExecuteDamage)绕过渲染事件的 Action() 直接调本方法时同样读得到
        /// </summary>
        internal bool assistAttackFlag;

        /// <summary>
        /// 强制指定本次结算使用的 Exp 事件行（-1 = 不强制）。
        ///
        /// 【用途】战法命中率判定失败时，原版仍按普攻打一次，但功绩 / 经验取的是该战法的
        /// **未命中行**：<c>get_exp(tactic-&gt;get_exp() + 1 - tactic_hit - critical)</c>，
        /// <c>tactic_hit = 0</c> ⇒ 组首 + 2（s11_sys_anim_unit.cpp:1090）。
        /// 项目失败流程用 <c>replaceSkill</c>（普攻实例）代为出手，若不覆盖就会拿到 Exp_Attack 的 20 功绩，
        /// 而原版只给 Exp_*Miss 的 10。
        ///
        /// 只在 <c>TroopSpellSkillFailEvent.Action()</c> 里成对设置 / 复位，切勿跨帧保留 ——
        /// 普攻实例是部队共享的，残留会让之后的普攻全部套用战法 miss 行。
        /// </summary>
        internal int forcedExpEvent = -1;

        /// <summary>
        /// 本次施法的计略技巧点是否已经结算过。
        ///
        /// 【为什么需要】同一份技能实例可能被两处调用 <see cref="Action"/>：
        /// 渲染事件（TroopSpellSkillEvent.Action）与时间轴（SkillTimelineEvent_ExecuteDamage）。
        /// 原版 <c>add_strategy_exp</c> 只在结算流程里走一次，这里用一次性标记对齐，
        /// 由 <see cref="Troop"/> 在下一次施法前通过 <see cref="ResetStrategyTechniquePointFlag"/> 复位。
        /// </summary>
        bool strategyTechniquePointGiven;

        /// <summary>施法开始前复位计略技巧点标记（由 Troop 在确认要排新的施法事件后调用）。</summary>
        public void ResetStrategyTechniquePointFlag()
        {
            strategyTechniquePointGiven = false;
        }

        public void Action(Cell spellCell, int criticalFactor)
        {
            Troop troop = master;
            // 一次性消耗: 取完立刻抹掉, 同一次施法里后续的结算按正常攻击走
            bool isAssistAttack = assistAttackFlag;
            if (isAssistAttack)
            {
                assistAttackFlag = false;
                Troop.LogAssist($"[{troop.Name}] 援助攻击开始结算 → [{(spellCell.troop == null ? "已无部队" : spellCell.troop.Name)}]");
            }

            Scenario scenario = Scenario.Cur;
            ScenarioVariables scenarioVariables = scenario.Variables;
            Troop targetTroop = spellCell.troop;
            BuildingBase targetBuilding = spellCell.building;
            Tools.OverrideData<int> overrideData;

            List<SangoObject> activedTargetList = new List<SangoObject>();
            // 本次结算对应的原版 Exp 表事件行：功绩 / 能力经验 / 适性经验 / 技巧点全部由它决定。
            // 正常情况下走到这里说明命中判定已通过（Action 由 CheckSuccess 之后的事件触发），hit = true；
            // 战法命中率失败、由 TroopSpellSkillFailEvent 用普攻代为出手时，forcedExpEvent 会指向 miss 行。
            CombatExpEvent expEvent = forcedExpEvent >= 0
                ? (CombatExpEvent)forcedExpEvent
                : skill.GetCombatExpEvent(true, criticalFactor > 100);
            // 援助攻击在原版走独立的一格 Exp_Assist，见 s11_sys_anim_unit.cpp:818-822。
            if (isAssistAttack)
                expEvent = CombatExpEvent.Assist;
            //TODO: 释放技能
            tempCellList.Clear();
            GetAttackCells(troop, spellCell, tempCellList);
            int targetDamage = 0;
            // 本次施法实际造成伤害的目标数（部队 + 建筑）。
            // 计略技巧点要按命中数放大（原版 add_strategy_exp 的 mul），
            // 但像落雷这种纯伤害计略没有 SkillEffect，不能只靠 DoEffect 的返回计数。
            int damagedTargetCount = 0;
            for (int i = 0; i < tempCellList.Count; i++)
            {
                Cell atkCell = tempCellList[i];
                Troop beAtkTroop = atkCell.troop;

                if(beAtkTroop != null && !beAtkTroop.IsAlive )
                {
                    continue;
                }

                // atk>0 常规伤害; 落雷等独立固定伤害计略 atk=0 也能进分支(由内部 skill.Id 特判走独立公式)
                if ((atk > 0 || skill.Id == ThunderSkillId) && beAtkTroop != null && canDamageTroop && (troop.IsEnemy(beAtkTroop) || canDamageTeam))
                {
                    // 落雷:固定伤害1500~2500兵力(每队随机),不受攻防兵力公式影响
                    int damage;
                    if (skill.Id == ThunderSkillId)
                        damage = GameRandom.Range(ThunderDamageMin, ThunderDamageMax + 1) * criticalFactor / 100;
                    else
                        damage = Troop.CalculateSkillDamage(troop, beAtkTroop, this) * criticalFactor / 100;
                    if (damage < 0)
                        damage = 0;

                    Tools.OverrideData<int> damage_overrideData = Tools.OverrideData<int>.Create(damage);
                    GameEvent.OnSkillDamageTroop?.Invoke(this, beAtkTroop, damage_overrideData);
                    damage = damage_overrideData.Value;

                    // 援助攻击只折算伤害, 其余流程(反击/EP/击杀奖励)与普通攻击完全一致
                    if (isAssistAttack)
                        damage = damage * Troop.AssistAttackDamagePercent / 100;

                    Sango.Log.Info($"{troop.BelongForce?.Name}的[{troop.Name} - {troop.TroopType?.Name}] 使用<{this.Name}> 攻击 {beAtkTroop.BelongForce?.Name}的[{beAtkTroop.Name} - {beAtkTroop.TroopType?.Name}], 造成伤害:{damage}, 目标剩余兵力: {beAtkTroop.GetTroopsNum()}");
                    beAtkTroop.ChangeTroops(-damage, this, 0);
                    damagedTargetCount++;

                    // 【原版对齐】受击方经验（功绩 0 / 技巧点 0，只给能力与适性经验）：
                    //   普攻命中 → Exp_Attacked（s11_sys_anim_unit.cpp:1140）
                    //   战法命中 → Exp_TacticAttacked（:1112）
                    //   计略不走这两行（计略的结算在 add_strategy_exp，受方无经验）。
                    // 原版写的是 `if (utils::is_alive(target_unit))`，所以阵亡的部队不给。
                    if (beAtkTroop.IsAlive && !IsStrategy())
                    {
                        beAtkTroop.GainEP(IsNormal() ? CombatExpEvent.Attacked : CombatExpEvent.TacticAttacked);
                    }

                    if (!beAtkTroop.IsAlive)
                    {
                        troop.GainTargetResource(beAtkTroop);
                    }

                    // 传入击破结果，使名声仅在击破敌方部队的本次结算中生效。
                    // 功绩 / 能力经验 / 适性经验 / 技巧点全部由 expEvent 行给出（原版 Exp 表）。
                    GainBattleExp(troop, expEvent, !beAtkTroop.IsAlive);
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
                                Sango.Log.Info($"{troop.BelongForce?.Name}的[{troop.Name} - {troop.TroopType?.Name}] 受到 {beAtkTroop.BelongForce?.Name}的[{beAtkTroop.Name} - {beAtkTroop.TroopType?.Name}]反击伤害:{hitBackDmg}, 目标剩余兵力: {troop.GetTroopsNum()}");
                                troop.ChangeTroops(-hitBackDmg, beAtkTroop.NormalRangeSkill, hitBack);
                                // 反击在原版走普攻的 Exp 行（Exp_Attack），不带本战法的 tech_point。
                                // 反击方仅在实际击破进攻部队时触发名声。
                                beAtkTroop.GainEP(CombatExpEvent.Attack, !troop.IsAlive);
                                // 被反击的进攻方拿 Exp_Attacked（原版受击方经验，:1140）。
                                if (troop.IsAlive)
                                    troop.GainEP(CombatExpEvent.Attacked);
                            }
                            else
                            {
                                int hitBackDmg = hitBack * Troop.CalculateSkillDamage(beAtkTroop, troop, beAtkTroop.NormalSkill) / 100;
                                Sango.Log.Info($"{troop.BelongForce?.Name}的[{troop.Name} - {troop.TroopType?.Name}] 受到 {beAtkTroop.BelongForce?.Name}的[{beAtkTroop.Name} - {beAtkTroop.TroopType?.Name}]反击伤害:{hitBackDmg}, 目标剩余兵力: {troop.GetTroopsNum()}");
                                troop.ChangeTroops(-hitBackDmg, beAtkTroop.NormalSkill, hitBack);
                                if (!troop.IsAlive)
                                {
                                    beAtkTroop.GainTargetResource(troop);
                                }
                                // 反击在原版走普攻的 Exp 行（Exp_Attack），不带本战法的 tech_point。
                                // 反击方仅在实际击破进攻部队时触发名声。
                                beAtkTroop.GainEP(CombatExpEvent.Attack, !troop.IsAlive);
                                // 被反击的进攻方拿 Exp_Attacked（原版受击方经验，:1140）。
                                if (troop.IsAlive)
                                    troop.GainEP(CombatExpEvent.Attacked);
                            }
                        }
                    }
                    if (this.master.IsAlive && beAtkTroop.IsAlive)
                    {
                        GameEvent.OnSkillDamageTroopAfter?.Invoke(this, beAtkTroop, damage_overrideData);

                        // 只对主目标呼叫援助, 免得AOE把每个溅射目标都引来一轮集火;
                        // !isAssistAttack 是防递归的关键: 援助自身的伤害结算也会走到这里
                        if (targetTroop == beAtkTroop && !isAssistAttack)
                            troop.TryAssistAttack(beAtkTroop);
                        else if (isAssistAttack)
                            Troop.LogAssist($"[{troop.Name}] 本次就是援助攻击, 不再连锁呼叫");
                        else
                            Troop.LogAssist($"技能<{Name}>落点不是主目标, 不呼叫援助");
                    }
                    else
                    {
                        Troop.LogAssist($"打[{beAtkTroop.Name}]后 攻击方存活={this.master.IsAlive} 目标存活={beAtkTroop.IsAlive}, 不呼叫援助");
                    }
                }

                BuildingBase beAtkBuildingBase = atkCell.building;
                if (atkDurability > 0 && beAtkBuildingBase != null && this.canDamageBuilding && (troop.IsEnemy(beAtkBuildingBase) || this.canDamageTeam))
                {
                    // 一个目标只会收到一次伤害
                    if (activedTargetList.Contains(beAtkBuildingBase))
                        continue;
                    activedTargetList.Add(beAtkBuildingBase);
                    damagedTargetCount++;
                    if (beAtkBuildingBase is City)
                    {
                        City city = (City)beAtkBuildingBase;
                        int damage_troops = Troop.CalculateSkillDamageTroopOnCity(troop, city, this) * criticalFactor / 100;
                        Sango.Log.Info($"{troop.BelongForce?.Name}的[{troop.Name} - {troop.TroopType?.Name}] 使用<{this.Name}> 攻击 {beAtkBuildingBase.BelongForce?.Name}的 [{beAtkBuildingBase.Name}], 造成兵力伤害:{damage_troops}, 目标剩余兵力: {city.troops}");
                        overrideData = Tools.OverrideData<int>.Create(damage_troops);
                        GameEvent.OnSkillDamageBuildingTroops?.Invoke(this, beAtkBuildingBase, overrideData);
                        damage_troops = overrideData.ValueAndRecycle;
                        if (!city.IsSameForce(troop) && !city.ChangeTroops(-damage_troops, troop, city.BelongForce != null))
                        {
                            // 原版攻破城池不给额外功绩：功绩只来自本次战法 / 普攻的 Exp 行
                            // （s11_sys_anim_unit.cpp:1136 的 attack_exp），城内部队溃散才另算击破。
                            GainBattleExp(troop, expEvent);
                            Sango.Log.Info($"{troop.BelongForce?.Name}的[{troop.Name} - {troop.TroopType?.Name}] 攻破城池: <{beAtkBuildingBase.Name}>");
                            // 城破会提前 return，计略结算必须在这里补一次，否则"计略烧城"这条路径永远拿不到。
                            ApplyStrategyExp(troop, 0, damagedTargetCount, criticalFactor > 100);
                            city.OnFall(this);
                            return;
                        }
                        else
                        {
                            GainBattleExp(troop, expEvent);
                        }
                    }

                    int damage = Troop.CalculateSkillDamage(troop, beAtkBuildingBase, this) * criticalFactor / 100;
                    Sango.Log.Info($"{troop.BelongForce?.Name}的[{troop.Name} - {troop.TroopType?.Name}] 使用<{this.Name}> 攻击 {beAtkBuildingBase.BelongForce?.Name}的 [{beAtkBuildingBase.Name}], 造成耐久伤害:{damage}, 目标剩余耐久: {beAtkBuildingBase.durability}");
                    overrideData = Tools.OverrideData<int>.Create(damage);
                    GameEvent.OnSkillDamageBuildingDurability?.Invoke(this, beAtkBuildingBase, overrideData);
                    damage = overrideData.ValueAndRecycle;

                    // 【陷阱引爆要传部队，不能传技能对象】
                    // BuildingTrapEffect.TrapChain:145 用 `atk as Troop` 取引爆者，再用它的 cell
                    // 当 srcCell 决定火球 / 落石的滚动方向（对齐原版 trap_anim 的 src_pos）。
                    // 传 this（技能对象）时 attacker 恒为 null → 火球不滚、落石退化成半径 1，
                    // 与 AI 打分（CalcOpen311TrapScore 按方向预测）的判定不一致，
                    // 会出现"AI 以为能炸到人、实际一座陷阱白烧"。
                    // 普通建筑仍传 this，保持既有外交关系值结算口径不变。
                    SangoObject atkSource = this;
                    if (troop != null && beAtkBuildingBase is Building atkTrap && BuildingTrapEffect.CanTrigger(atkTrap))
                        atkSource = troop;

                    if (beAtkBuildingBase.ChangeDurability(-damage, atkSource))
                    {
                        GainBattleExp(troop, expEvent);

                        Sango.Log.Info($"{troop.BelongForce?.Name}的[{troop.Name} - {troop.TroopType?.Name}] 破坏建筑: <{beAtkBuildingBase.Name}>");
                    }
                    else
                    {
                        GainBattleExp(troop, expEvent);

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
                                    Sango.Log.Info($"{troop.BelongForce?.Name}的[{troop.Name} - {troop.TroopType?.Name}] 受到 {beAtkBuildingBase.BelongForce?.Name}的[{beAtkBuildingBase.Name}]反击伤害:{hitBackDmg}, 目标剩余兵力: {troop.GetTroopsNum()}");
                                    troop.ChangeTroops(-hitBackDmg, beAtkBuildingBase, atkBack);
                                }
                            }
                        }
                    }
                }
            }

            DoOffset(troop, targetTroop, targetDamage);

            int effectTargetCount = DoEffect(troop, spellCell, tempCellList);

            // 【原版对齐】计略施放成功给施法方势力技巧点：
            //   s11_sys_anim_unit_strategy.cpp:1323-1329 `add_strategy_exp`：
            //     `int tech_point = unit_add_exp_with_kouseki(unit,
            //          get_exp(Exp_KakeiMiss + 3 * strategy_id - hit - critical), -1, mul, off_screen);`
            //     `if (tech_point > 0) force_add_tech_point(force, pos, tech_point, off_screen);`
            //   其中 mul = 命中目标数，tech_point 取 Exp 表该计略格的 tech_point（0 / 20 / 30）。
            //   miss 的格恒为 0，所以失败不给 —— 本项目 Action 只在 CheckSuccess 通过后才执行，天然一致。
            // `strategyTechniquePointGiven` 防重：同一次施法可能同时经渲染事件与时间轴两处调到 Action。
            ApplyStrategyExp(troop, effectTargetCount, damagedTargetCount, criticalFactor > 100);

            if (troop.IsAlive)
            {
                troop.ChangeMorale(-this.costEnergy, false);
            }

            GameEvent.OnSkillActionOver?.Invoke(this);
        }

        /// <summary>
        /// 结算计略施放成功的功绩 / 能力经验 / 势力技巧点。
        ///
        /// 【原版对齐】s11_sys_anim_unit_strategy.cpp:1323-1329 <c>add_strategy_exp</c>：
        /// <code>
        /// int tech_point = unit_add_exp_with_kouseki(unit,
        ///     get_exp(Exp_KakeiMiss + 3 * strategy_id - hit - critical), -1, mul, off_screen);
        /// if (tech_point &gt; 0) force_add_tech_point(force, pos, tech_point, off_screen);
        /// </code>
        /// 即：功绩 / 能力经验按 Exp 行原值给全队每人一份；技巧点乘 <c>mul</c>（命中目标数）。
        /// 本项目 Action 只在 CheckSuccess 通过后才执行，因此这里等价于"命中才给"（miss 行全 0）。
        ///
        /// 用 <c>strategyTechniquePointGiven</c> 防重：同一次施法可能同时经渲染事件与时间轴两处调到 Action；
        /// 攻破城池的分支还会提前 return，所以结算点必须可重入。
        /// </summary>
        /// <param name="troop">施法方部队</param>
        /// <param name="effectTargetCount">SkillEffect 实际命中的目标格数</param>
        /// <param name="damagedTargetCount">实际受到伤害的目标数（部队 + 建筑）</param>
        void ApplyStrategyExp(Troop troop, int effectTargetCount, int damagedTargetCount, bool critical)
        {
            if (!IsStrategy() || strategyTechniquePointGiven)
                return;
            strategyTechniquePointGiven = true;

            if (troop == null)
                return;

            // 命中数：伤害目标与效果目标取较大值 —— 落雷这类纯伤害计略没有 SkillEffect，
            // 而点火 / 上 buff 这类可能伤害为 0，只在 DoEffect 里命中。
            int hitCount = System.Math.Max(effectTargetCount, damagedTargetCount);
            if (hitCount <= 0) hitCount = 1;

            // 计略的功绩差异在暴击行（伪报 / 扰乱 / 伏兵等 200）与命中行（100）；
            // 技巧点两行相同，因此这里只影响功绩。
            CombatExpEvent expEvent = skill.GetCombatExpEvent(true, critical);
            troop.GainEP(expEvent, false, hitCount);
        }

        /// <summary>
        /// 战斗（普攻 / 战法 / 反击）的 Exp 结算入口。
        ///
        /// 计略**不**从这里走：计略的功绩、能力经验、技巧点统一在 Action 末尾由
        /// <see cref="ApplyStrategyExp"/> 按命中目标数结算一次（原版 add_strategy_exp 的 mul）。
        /// 若这里也放行，火计这种"既有伤害又有 buff"的计略会走伤害分支再算一次，变成双倍。
        /// </summary>
        void GainBattleExp(Troop troop, CombatExpEvent expEvent, bool isDestroyEnemyTroop = false)
        {
            if (IsStrategy())
                return;
            troop.GainEP(expEvent, isDestroyEnemyTroop);
        }

        public void DoOffset(Troop troop, Troop targetTroop, int targetDamage)
        {
            if (targetTroop == null || offsetAction == null || offsetAction.Length <= 0) return;

            // 位移撞击的连带伤害按本技能的命中行结算（拿不到 Action 的 criticalFactor，用实例上记录的暴击倍率）。
            CombatExpEvent expEvent = skill.GetCombatExpEvent(true, tempCriticalFactor > 100);

            // 位移结算前先记下目标所在格：结算后要判断"目标有没有真的被推走"，
            // 靠它给「猛者」这类特技（让被位移的敌方武将负伤）发信号。
            Cell targetCellBefore = targetTroop.cell;

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
                                            blockTroop.ChangeTroops(-blockDmg, this, -blockFactor);
                                            if (!blockTroop.IsAlive)
                                            {
                                                troop.GainTargetResource(blockTroop);
                                            }
                                            // 范围战法逐个结算，必须按当前目标是否被击破传递事实。
                                            // 位移撞击的连带伤害拿不到暴击倍率，按本技能的命中行取。
                                            GainBattleExp(troop, expEvent, !blockTroop.IsAlive);
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
                                            blockTroop.ChangeTroops(-blockDmg, this, -blockFactor);
                                            if (!blockTroop.IsAlive)
                                            {
                                                troop.GainTargetResource(blockTroop);
                                            }
                                            // 范围战法逐个结算，必须按当前目标是否被击破传递事实。
                                            // 位移撞击的连带伤害拿不到暴击倍率，按本技能的命中行取。
                                            GainBattleExp(troop, expEvent, !blockTroop.IsAlive);

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

            // 目标真的被推走了才发信号（施法部队, 被位移部队, 位移格数）：
            // 被挡住 / 无处可去时格子没变，不算"让敌方部队移动区格"。
            if (targetTroop.IsAlive && targetCellBefore != null && targetTroop.cell != null && targetTroop.cell != targetCellBefore)
            {
                GameEvent.OnTroopSkillOffset?.Invoke(troop, targetTroop, targetCellBefore.Distance(targetTroop.cell));
            }
        }

        /// <summary>
        /// 施放技能效果（点火 / 上 buff / 治疗等）。
        /// </summary>
        /// <returns>
        /// 实际施放了效果的目标格数量。计略技巧点要按命中数放大
        /// （原版 s11_sys_anim_unit_strategy.cpp:1326 的 <c>mul</c> 参数），所以这里回传计数。
        /// </returns>
        public int DoEffect(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            InitSkillEffects();
            if (effects == null || effects.Count == 0) return 0;

            int castCount = 0;
            foreach (Cell target in atkCellList)
            {
                Cell atkCell = target;
                if (canSpellToCell)
                {
                    // 主目标格必定施放效果(如点火);周围邻格只有存在部队/建筑(含城池)才施放,空地不施放
                    if (atkCell == spellCell || atkCell.troop != null || atkCell.building != null)
                    {
                        for (int ei = 0; ei < effects.Count; ei++) effects[ei].Action(target);
                        castCount++;
                    }
                }
                else
                {
                    Troop beAtkTroop = atkCell.troop;
                    if (beAtkTroop != null && canDamageTroop && (troop.IsEnemy(beAtkTroop) || canDamageTeam))
                    {
                        for (int ei = 0; ei < effects.Count; ei++) effects[ei].Action(target);
                        castCount++;
                    }
                    else
                    {
                        BuildingBase beAtkBuildingBase = atkCell.building;
                        if (beAtkBuildingBase != null && this.canDamageBuilding && (troop.IsEnemy(beAtkBuildingBase) || this.canDamageTeam))
                        {
                            for (int ei = 0; ei < effects.Count; ei++) effects[ei].Action(target);
                            castCount++;
                        }
                    }
                }
            }
            return castCount;
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
            // 实例会被对象池复用，计略技巧点的一次性标记必须随清理复位，
            // 否则复用后的第一次计略会因为标记残留而拿不到技巧点。
            strategyTechniquePointGiven = false;
            assistAttackFlag = false;
            forcedExpEvent = -1;
            if (effects != null)
            {
                for (int ei = 0; ei < effects.Count; ei++) effects[ei].Clear();
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
