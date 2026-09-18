/*
 * 文件名：Troop.cs
 * 描述：部队类，管理游戏中的部队对象
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Sango.Core.Action;
using Sango.Render;
using Sango.Tools;
using System;
using System.Collections.Generic;
using TKNewtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 部队类，管理游戏中的部队对象
    /// 部队是游戏中的作战单位，包含主将、副将、兵力、士气等属性
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Troop : SangoObjectExtensionData
    {
        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override SangoObjectType ObjectType => SangoObjectType.Troops;

        /// <summary>
        /// 带颜色的部队名称
        /// </summary>
        public string ColorName => $"<color=#85B964>{Name}</color>";

        /// <summary>
        /// AI是否完成行动
        /// </summary>
        public virtual bool AIFinished { get; set; }

        /// <summary>
        /// AI是否准备完成
        /// </summary>
        public virtual bool AIPrepared { get; set; }

        /// <summary>
        /// 是否已经委任
        /// </summary>
        public bool IsAppoint => missionType > 0;

        /// <summary>
        /// 所属势力
        /// </summary>
        public Force mBelongForce => Leader?.mBelongForce;

        /// <summary>
        /// 所属军团
        /// </summary>
        public Corps mBelongCorps => Leader?.mBelongCorps;

        /// <summary>
        /// 所属城池
        /// </summary>
        public City mBelongCity => Leader?.mBelongCity;

        /// <summary>
        /// 所在城池
        /// </summary>
        public City mCurrentCity => cell.BelongCity.mBelongCity == null ? cell.BelongCity : cell.BelongCity.mBelongCity;

        /// <summary>
        /// 统领（主将）
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Leader { get; set; }

        /// <summary>
        /// 副将1
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Member1 { get; set; }

        /// <summary>
        /// 副将2
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Member2 { get; set; }


        /// <summary>
        /// 俘虏列表
        /// </summary>
        //[JsonConverter(typeof(SangoObjectListIDConverter<Person>))]
        //[JsonProperty]
        public SangoObjectList<Person> captiveList = new SangoObjectList<Person>();


        /// <summary>
        /// 部队名称缓存
        /// </summary>
        string _troopName;

        /// <summary>
        /// 部队名称
        /// </summary>
        public override string Name => _troopName;

        /// <summary>
        /// 所在格子
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(XY2CellConverter))]
        public Cell cell;

        /// <summary>
        /// 坐标x
        /// </summary>
        public int x => cell?.x ?? 0;

        /// <summary>
        /// 坐标y
        /// </summary>
        public int y => cell?.y ?? 0;

        /// <summary>
        /// 当前兵力
        /// </summary>
        [JsonProperty] public int troops;

        /// <summary>
        /// 出征回合
        /// </summary>
        [JsonProperty] public int liveDays;

        /// <summary>
        /// 最大兵力
        /// </summary>
        public int MaxTroops { get; set; }

        /// <summary>
        /// 是否满兵
        /// </summary>
        public bool IsFull => troops >= MaxTroops;

        /// <summary>
        /// 当前伤兵
        /// </summary>
        [JsonProperty] public int woundedTroops;

        /// <summary>
        /// 战意
        /// </summary>
        [JsonProperty]
        public int energy;

        /// <summary>
        /// 士气
        /// </summary>
        [JsonProperty]
        public int morale;

        /// <summary>
        /// 最大士气
        /// </summary>
        public int MaxMorale => mBelongCity?.MaxMorale ?? 100;

        /// <summary>
        /// 上次因"兄弟同心"推满气力的回合数;
        /// 用于限制同一对兄弟部队每回合只结算一次。
        /// 初始值必须为-1: 剧本的 turnCount 从0开始, 用默认的0会让第1回合永远无法触发
        /// </summary>
        [JsonProperty]
        public int swornCheerTurn = -1;

        /// <summary>
        /// "兄弟同心"对白是否正在进行、气力尚未真正加满的临时标记(不存档)。
        /// 加气走的是异步对白, 若不在攻击前把它跑完, 部队会一边播对白一边出手,
        /// 导致本次攻击吃不到刚推满的气力。SpellSkill 会在此标记为真时挂起, 等加气结束再出手。
        /// </summary>
        internal bool pendingSwornCheer = false;

        /// <summary>
        /// 移动能力
        /// </summary>
        public int MoveAbility => IsInWater ? waterMoveAbility : landMoveAbility;

        /// <summary>
        /// 水上移动能力
        /// </summary>
        public int waterMoveAbility;

        /// <summary>
        /// 陆地移动能力
        /// </summary>
        public int landMoveAbility;

        /// <summary>
        /// 携带粮食
        /// </summary>
        [JsonProperty] public int food;

        /// <summary>
        /// 携带金钱
        /// </summary>
        [JsonProperty] public int gold;

        /// <summary>
        /// 携带人口
        /// </summary>
        [JsonProperty] public int population;

        /// <summary>
        /// 携带道具
        /// </summary>
        [JsonProperty]
        public ItemStore itemStore = new ItemStore();

        /// <summary>
        /// 被影响的建筑,用来判断建筑影响唯一化
        /// </summary>
        public Dictionary<string, BuildingImproveBase> buildingImproveMap = new Dictionary<string, BuildingImproveBase>();


        public int captiveChangce = 0;

        /// <summary>
        /// 是否行动完毕
        /// </summary>
        [JsonProperty]
        public override bool ActionOver
        {
            get => actionOver;
            set
            {
                if (actionOver != value)
                {
                    if (value)
                    {
                        if (ActionOverCount > 0)
                        {
                            ActionOverCount--;
                            isExtraAction = true;
                            return;
                        }
                    }
                    isExtraAction = false;
                    actionOver = value;
                    ActionOverCount = 0;
                    GameEvent.OnTroopActionOver?.Invoke(this);
                }
            }
        }
        private bool actionOver;
        // 额外行动的标志
        public bool isExtraAction = false;
        // 额外行动的次数
        public int ActionOverCount = 0;

        public bool IsPlayer => mBelongForce?.IsPlayer ?? false;
        /// <summary>
        /// 是否为玩家控制的
        /// </summary>
        public virtual bool IsPlayerControl => mBelongCorps?.IsPlayerControl ?? false;
        public bool IsCurPlayer => mBelongForce?.IsCurPlayer ?? false;

        /// <summary>
        /// 当前任务类型
        /// </summary>
        [JsonProperty] public int missionType;

        /// <summary>
        /// 任务目标
        /// </summary>
        [JsonProperty] public int missionTarget;

        /// <summary>
        /// 任务参数1
        /// </summary>
        [JsonProperty] public int missionParams1;

        /// <summary>
        /// 任务参数1
        /// </summary>
        [JsonProperty] public int missionParams2;

        /// <summary>
        /// 当前状态管理器
        /// </summary>
        [JsonProperty]
        public BuffManager buffManager = new BuffManager();

        public void AddBuff(int id, int turnCount, Troop srcTroop) { buffManager.AddBuff(id, turnCount, srcTroop); }
        public void RemoveBuff(int id) { buffManager.RemoveBuff(id); }
        public void RemoveBuffByKind(int kind) { buffManager.RemoveBuffByKind(kind); }

        public bool HasControlBuff()
        {
            return buffManager.HasControlBuff();
        }

        /// <summary>
        /// 任务地点
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(XY2CellConverter))]
        public Cell missionTargetCell;

        public TroopType TroopType
        {
            get { return IsInWater ? WaterTroopType : LandTroopType; }
            set { LandTroopType = value; }
        }

        /// <summary>
        /// 兵种适应力
        /// </summary>
        public int TroopTypeLv => IsInWater ? WaterTroopTypeLv : LandTroopTypeLv;

        /// <summary>
        /// 部队类型
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<TroopType>))]
        [JsonProperty]
        public TroopType WaterTroopType { get; set; }

        /// <summary>
        /// 部队类型
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<TroopType>))]
        [JsonProperty]
        public TroopType LandTroopType { get; set; }

        public int WaterTroopTypeLv { get; private set; }
        public int LandTroopTypeLv { get; private set; }

        /// <summary>
        /// 是否在水中
        /// </summary>
        /// <returns></returns>
        public bool IsInWater => cell?.TerrainType.isWater ?? false;


        public List<SkillInstance> skills => IsInWater ? waterSkills : landSkills;

        /// <summary>
        /// 当前技能
        /// </summary>
        //[JsonProperty]
        public List<SkillInstance> waterSkills;

        /// <summary>
        /// 当前技能
        /// </summary>
        //[JsonProperty]
        public List<SkillInstance> landSkills;

        /// <summary>
        /// 计略
        /// </summary>
        public List<SkillInstance> StrategySkills = new List<SkillInstance>();

        // 近战普攻
        public SkillInstance NormalSkill => IsInWater ? waterNormalSkill : landNormalSkill;
        SkillInstance waterNormalSkill;
        SkillInstance landNormalSkill;

        // 远程普攻
        public SkillInstance NormalRangeSkill => IsInWater ? waterNormalRangeSkill : landNormalRangeSkill;
        SkillInstance waterNormalRangeSkill;
        SkillInstance landNormalRangeSkill;

        /// <summary>
        /// 对部队的额外伤害增减
        /// </summary>
        public float DamageTroopExtraFactor => IsInWater ? waterDamageTroopExtraFactor : landDamageTroopExtraFactor;
        public float waterDamageTroopExtraFactor;
        public float landDamageTroopExtraFactor;
        /// <summary>
        /// 对建筑的额外伤害增益
        /// </summary>
        public float DamageBuildingExtraFactor => IsInWater ? waterDamageBuildingExtraFactor : landDamageBuildingExtraFactor;
        public float waterDamageBuildingExtraFactor;
        public float landDamageBuildingExtraFactor;


        public int defeatTroopCanGainFoodFactor;
        public int defeatTroopCanGainGoldFactor;

        public int SpearLv { get; private set; }
        public int HalberdLv { get; private set; }
        public int CrossbowLv { get; private set; }
        public int RideLv { get; private set; }
        public int WaterLv { get; private set; }
        public int MachineLv { get; private set; }

        /// <summary>
        /// 攻击力
        /// </summary>
        public int Attack => extraAttack + (IsInWater ? waterAttack : landAttack) + trrainBonusAtk;

        public int waterAttack;
        public int landAttack;
        public int extraAttack;
        public int trrainBonusAtk;
        /// <summary>
        /// 防御力
        /// </summary>
        public int Defence => extraDefence + (IsInWater ? waterDefence : landDefence) + trrainBonusDef;

        public int waterDefence;
        public int landDefence;
        public int extraDefence;
        public int trrainBonusDef;

        /// <summary>
        /// 建设力
        /// </summary>
        public int BuildPower { get; set; }

        /// <summary>
        /// 统率
        /// </summary>
        public int Command { get; private set; }

        /// <summary>
        /// 武力
        /// </summary>
        public int Strength { get; private set; }

        /// <summary>
        /// 智力
        /// </summary>
        public int Intelligence { get; private set; }

        /// <summary>
        /// 政治
        /// </summary>
        public int Politics { get; private set; }

        /// <summary>
        /// 魅力
        /// </summary>
        public int Glamour { get; private set; }

        /// <summary>
        /// 是否忽略陆地ZOC
        /// </summary>
        public bool ignoreLandZOC = false;

        /// <summary>
        /// 是否忽略水上ZOC
        /// </summary>
        public bool ignoreWaterZOC = false;

        /// <summary>
        /// 是否免疫火焰
        /// </summary>
        public bool ignoreFire = false;

        public bool IsJustCreated => x == mBelongCity.x && y == mBelongCity.y;

        public override ObjectRender GetRender() { return Render; }
        public TroopRender Render { get; private set; }
        bool isMissionPrepared = false;
        public int foodCost = 0;
        public float foodCostFactor = 1;

        public List<ActionBase> actionList;
        public List<Cell> MoveRange = new List<Cell>(256);

        static List<Feature> temp_FeatureList = new List<Feature>();
        public void InitActionList()
        {
            temp_FeatureList.Clear();
            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                    actionList[i].Clear();

                actionList.Clear();
            }
            else
                actionList = new List<ActionBase>();

            ForEachPerson(x =>
            {
                if (x.mFeatureList != null)
                {
                    for (int i = 0; i < x.mFeatureList.Count; i++)
                    {
                        Feature feature = x.mFeatureList[i];
                        if (feature != null && feature.kind <= (int)FeatureKindType.TroopSupport)
                        {
                            if (!feature.only)
                            {
                                temp_FeatureList.Add(feature);
                                feature.InitActions(actionList, this, x);
                            }
                            else
                            {
                                if (!temp_FeatureList.Contains(feature))
                                {
                                    temp_FeatureList.Add(feature);
                                    feature.InitActions(actionList, this, x);
                                }
                            }
                        }
                    }
                }
            });
        }


        public override void Init(Scenario scenario)
        {
            _troopName = $"{Leader?.Name}队";
            ForEachPerson(x => x.mTroop = this);
            InitActionList();

            CalculateAttribute(scenario);
            if (LandTroopType.isFight && LandTroopType.Id != 1)
                mBelongCity.allAttackTroops.Add(this);
            mBelongCity.allTroops.Add(this);
            cell.troop = this;
            Render = new TroopRender(this);
            foodCost = (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (troops + woundedTroops) * TroopType.foodCostFactor);
            foodCost = (int)Math.Ceiling(foodCost * foodCostFactor);

            if (captiveList.Count > 0)
            {
                captiveList.RemoveAll(x => x.mTroop != this);
                captiveList.ForEach(person =>
                {
                    if (person.mBelongForce != null)
                        person.mBelongForce.BeCaptiveList.Add(person);
                });
            }

            buffManager.Init(this);
            GameEvent.OnTroopEnterCell?.Invoke(this, cell, null);
            UpdateTerrainBonus(cell);
        }
        public void ResetActionAndStatus()
        {
            InitActionList();
            CalculateAttribute(Scenario.Cur);
        }

        public virtual bool Run(Corps corps, Force force, Scenario scenario)
        {
            return false;
            //yield return Event.OnTroopsStart?.Invoke(this, corps, force, scenario);
            //yield return Event.OnTroopsAI?.Invoke(this, corps, force, scenario);
            //yield return Event.OnTroopsEnd?.Invoke(this, corps, force, scenario);
        }
        public override void OnScenarioPrepare(Scenario scenario)
        {

            //foreach (SkillInstance s in landSkills)
            //{
            //    s.Init(this, s.skill);
            //}
            //foreach (SkillInstance s in waterSkills)
            //{
            //    s.Init(this, s.skill);
            //}
            PrepeareFoodCost();

            //MemberList?.InitCache();// = new SangoObjectList<Person>().FromString(_memberListStr, scenario.personSet);
        }

        public int PrepeareFoodCost()
        {
            Scenario scenario = Scenario.Cur;
            foodCost = (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (troops + woundedTroops) * TroopType.foodCostFactor);
            foodCost = (int)Math.Ceiling(foodCost * foodCostFactor);
            return foodCost;
        }
        public int PrepeareFoodCost(int target)
        {
            Scenario scenario = Scenario.Cur;
            foodCost = (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (target + woundedTroops) * TroopType.foodCostFactor);
            foodCost = (int)Math.Ceiling(foodCost * foodCostFactor);
            return foodCost;
        }

        public bool IsNewTroop => liveDays == 0;

        public override bool OnForceTurnStart(Scenario scenario)
        {
            liveDays++;
            MoveRange.Clear();
            ActionOver = false;
            AIFinished = false;
            AIPrepared = false;
            isMoving = false;
            isMissionPrepared = false;
            skillRenderEvent = null;
            skillRenderEventIsAssist = false;
            actionRenderEvent = null;
            moveRenderEvent = null;
            troopMissionBehaviour = null;
            if (food <= 0)
            {
                // 伤兵直接抛弃
                woundedTroops = 0;
                // 减少士气
                morale = (int)System.Math.Ceiling(morale * 0.3f);
                if (morale < 0)
                    morale = 0;

                if (troops < 500)
                {
                    Clear();
                    return true;
                }
                else
                {
                    // 每回合死亡当前兵力的30%;
                    int damage = (int)System.Math.Ceiling(troops * 0.3f);
                    troops -= damage;
                }
            }
            else
            {
                PrepeareFoodCost();
                ChangeFood(-foodCost, false);
            }

            buffManager.OnForceTurnStart(scenario);

            foreach (SkillInstance skillInstance in waterSkills)
                skillInstance.OnForceTurnStart(scenario);
            foreach (SkillInstance skillInstance in landSkills)
                skillInstance.OnForceTurnStart(scenario);

            GameEvent.OnTroopTurnStart?.Invoke(this, scenario);

            if (Render != null)
            {
                Render.UpdateRender();
            }

            // 俘虏羁押天数累积
            for (int i = captiveList.Count - 1; i >= 0; i--)
            {
                Person person = captiveList[i];
                person.missionCounter++;
            }

            return true;
        }
        public override bool OnForceTurnEnd(Scenario scenario)
        {
            // 计算俘虏越狱
            for (int i = captiveList.Count - 1; i >= 0; i--)
            {
                Person person = captiveList[i];
                if (person == null) continue;

                if (GameRandom.Chance(GameFormula.Instance.PersonEscapeProbablility_InTroop(person, this, scenario), 10000))
                {
                    person.Escape(EscapeType.Escape);
                    if (IsPlayer)
                    {
                        PersonEscapeEvent personEscapeEvent = new PersonEscapeEvent()
                        {
                            person = person
                        };
                        RenderEvent.Instance.Add(personEscapeEvent);
                    }
#if SANGO_DEBUG
                    Sango.Log.Info($"{person.Name}逃跑!");
#endif
                }
            }
            GameEvent.OnTroopTurnEnd?.Invoke(this, scenario);
            return true;
        }

        public int IsWithOutFood()
        {
            if (food == 0) return 0;
            if (food < foodCost * 4) return 1;
            return 2;
        }

        public void ForEachMember(Action<Person> action)
        {
            if (Member1 != null) action(Member1);
            if (Member2 != null) action(Member2);
        }

        public void ForEachPerson(Action<Person> action)
        {
            if (Leader != null) action(Leader);
            if (Member1 != null) action(Member1);
            if (Member2 != null) action(Member2);
        }

        /// <summary>
        /// 计算属性
        /// </summary>
        public void CalculateAttribute(Scenario scenario)
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;
            captiveChangce = Variables.captureChangceWhenTroopFall;

            StrategySkills.Clear();
            scenario.CommonData.Skills.ForEach(x =>
            {
                if (x.IsStrategy())
                    StrategySkills.Add(SkillInstance.Create(this, x));
            });

            if (WaterTroopType == null)
                WaterTroopType = scenario.GetObject<TroopType>(8);

            defeatTroopCanGainFoodFactor = Variables.defeatTroopCanGainFoodFactor;
            defeatTroopCanGainGoldFactor = Variables.defeatTroopCanGainGoldFactor;

            LandTroopTypeLv = -1;
            WaterTroopTypeLv = -1;
            Command = -1;
            Strength = -1;
            Intelligence = -1;
            Politics = -1;
            Glamour = -1;
            // 计算能力,能力取最大
            ForEachPerson((p) =>
            {
                Command = System.Math.Max(Command, p.Command);
                Strength = System.Math.Max(Strength, p.Strength);
                Intelligence = System.Math.Max(Intelligence, p.Intelligence);
                Politics = System.Math.Max(Politics, p.Politics);
                Glamour = System.Math.Max(Glamour, p.Glamour);
                LandTroopTypeLv = System.Math.Max(LandTroopTypeLv, CheckTroopTypeLevel(LandTroopType, p));
                WaterTroopTypeLv = System.Math.Max(WaterTroopTypeLv, p.WaterLv);
                p.escapeFactorWhenTroopDestroy = 0;
            });

            List<SkillInstance> skillInstances = new List<SkillInstance>();
            landNormalSkill = null;
            waterNormalSkill = null;
            landNormalRangeSkill = null;
            waterNormalRangeSkill = null;

            if (LandTroopType.skills != null)
            { // 准备技能
                for (int i = 0; i < LandTroopType.skills.Length; i++)
                {
                    Skill skill = Scenario.Cur.GetObject<Skill>(LandTroopType.skills[i]);
                    if (skill != null && skill.CanAddToTroop(this, false))
                    {
                        skillInstances.Add(SkillInstance.Create(this, skill));
                    }
                }
            }
            landSkills = skillInstances;

            skillInstances = new List<SkillInstance>();
            if (WaterTroopType.skills != null)
            {
                for (int i = 0; i < WaterTroopType.skills.Length; i++)
                {
                    Skill skill = Scenario.Cur.GetObject<Skill>(WaterTroopType.skills[i]);
                    if (skill != null && skill.CanAddToTroop(this, true))
                    {
                        skillInstances.Add(SkillInstance.Create(this, skill));
                    }
                }
            }
            waterSkills = skillInstances;

            // 防御力 = (70%统率+30%智力) * 兵种防御力 / 100 * 适应力加成(A为1)
            landDefence = TroopsLevelBoost((
                Command * Variables.fight_troop_defence_command_factor
                + Strength * Variables.fight_troop_defence_strength_factor
                + Intelligence * Variables.fight_troop_defence_intelligence_factor
                + Politics * Variables.fight_troop_defence_politics_factor
                + Glamour * Variables.fight_troop_defence_glamour_factor
                ) / 10000 * LandTroopType.def, LandTroopTypeLv) / 100;

            // 攻击力 = (70%武力+30%统率) * 兵种攻击力 / 100 * 适应力加成(A为1)
            landAttack = TroopsLevelBoost((
                 Command * Variables.fight_troop_attack_command_factor
                + Strength * Variables.fight_troop_attack_strength_factor
                + Intelligence * Variables.fight_troop_attack_intelligence_factor
                + Politics * Variables.fight_troop_attack_politics_factor
                + Glamour * Variables.fight_troop_attack_glamour_factor
                ) / 10000 * LandTroopType.atk, LandTroopTypeLv) / 100;


            // 防御力 = (70%统率+30%智力) * 兵种防御力 / 100 * 适应力加成(A为1)
            waterDefence = TroopsLevelBoost((
                Command * Variables.fight_troop_defence_command_factor
                + Strength * Variables.fight_troop_defence_strength_factor
                + Intelligence * Variables.fight_troop_defence_intelligence_factor
                + Politics * Variables.fight_troop_defence_politics_factor
                + Glamour * Variables.fight_troop_defence_glamour_factor
                ) / 10000 * WaterTroopType.def, WaterTroopTypeLv) / 100;

            // 攻击力 = (70%武力+30%统率) * 兵种攻击力 / 100 * 适应力加成(A为1)
            waterAttack = TroopsLevelBoost((
                 Command * Variables.fight_troop_attack_command_factor
                + Strength * Variables.fight_troop_attack_strength_factor
                + Intelligence * Variables.fight_troop_attack_intelligence_factor
                + Politics * Variables.fight_troop_attack_politics_factor
                + Glamour * Variables.fight_troop_attack_glamour_factor
                ) / 10000 * WaterTroopType.atk, WaterTroopTypeLv) / 100;


            // 建设能力 = 政治 * 67% + 50;
            BuildPower = Politics * 2 / 3 + 50;

            waterMoveAbility = WaterTroopType.move;
            landMoveAbility = LandTroopType.move;

            CalculateMaxTroops();

            // 事件可二次修改属性
            GameEvent.OnTroopCalculateAttribute?.Invoke(this, scenario);
            GameEvent.OnTroopAfterCalculateAttribute?.Invoke(this, scenario);

            for (int i = 0; i < landSkills.Count; i++)
            {
                SkillInstance ins = landSkills[i];
                if (ins.IsNormal())
                {
                    if (ins.IsRange())
                        landNormalRangeSkill = ins;
                    else
                        landNormalSkill = ins;
                }
            }

            for (int i = 0; i < waterSkills.Count; i++)
            {
                SkillInstance ins = waterSkills[i];
                if (ins.IsNormal())
                {
                    if (ins.IsRange())
                        waterNormalRangeSkill = ins;
                    else
                        waterNormalSkill = ins;
                }
            }
        }

        public void CalculateMaxTroops()
        {
            int max = Leader.TroopsLimit;
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(max);
            GameEvent.OnTroopCalculateMaxTroops?.Invoke(Leader.mBelongCity, this, overrideData);
            MaxTroops = overrideData.ValueAndRecycle;
        }

        public int MoveCost(Cell cell)
        {
            if (cell.IsWater)
                return WaterTroopType.MoveCost(cell);
            else
                return LandTroopType.MoveCost(cell);
        }

        public int GetTargetMoveAbility(Cell cell)
        {
            if (cell.IsWater)
                return waterMoveAbility;
            else
                return landMoveAbility;
        }

        public bool IsAlliance(BuildingBase other)
        {
            return IsAlliance(mBelongForce, other.mBelongForce);
        }

        public bool IsEnemy(BuildingBase other)
        {
            return IsEnemy(mBelongForce, other.mBelongForce);
        }

        public bool IsSameForce(BuildingBase other)
        {
            return IsSameForce(mBelongForce, other.mBelongForce);
        }
        public bool IsSameForce(Person other)
        {
            return IsSameForce(mBelongForce, other.mBelongForce);
        }

        public bool IsAlliance(Troop other)
        {
            return IsAlliance(mBelongForce, other.mBelongForce);
        }

        public bool IsEnemy(Troop other)
        {
            return IsEnemy(mBelongForce, other.mBelongForce);
        }

        public bool IsSameForce(Troop other)
        {
            return IsSameForce(mBelongForce, other.mBelongForce);
        }

        public bool IsTransport => LandTroopType.IsTransport();
        public bool IsMachine => LandTroopType.IsMachine();
        public bool IsHelepolis => LandTroopType.IsHelepolis();
        public bool IsFight => LandTroopType.isFight;
        public bool IsRange => LandTroopType.isRange;

        /// <summary>
        /// 发起单挑
        /// </summary>
        /// <param name="targetTroop">目标部队</param>
        /// <param name="withView">是否带表现层（false 时瞬时结算）</param>
        /// <returns>是否成功发起单挑</returns>
        public bool StartDuel(Troop targetTroop, bool withView = true)
        {
            if (targetTroop == null || !IsEnemy(targetTroop))
            {
                return false;
            }

            return Duel.DuelManager.Instance.StartDuel(this, targetTroop, withView);
        }

        public int GetAttackBackFactor(SkillInstance skill, int distance)
        {
            if (IsMachine)
                return 0;
            if (skill.IsRange() && distance > 1)
                return 0;
            else if (!skill.IsRange() && distance == 1)
                return 90;
            return 0;
        }

        public void AttackBuilding(Cell buildingInCell)
        {

        }

        public void AttackBuilding(BuildingBase buildingBase)
        {

        }


        //@param attacker Troops
        //@param defender Troops
        //@param skill Skill
        public static int CalculateSkillDamage(Troop attacker, Troop target, SkillInstance skill)
        {
            var attack_troops_type = attacker.TroopType;
            var defender_troops_type = target.TroopType;

            ScenarioVariables Variables = Scenario.Cur.Variables;

            float difficultyDamageFactor = 1;
            if (attacker.mBelongForce != null && attacker.mBelongForce.IsPlayer)
                difficultyDamageFactor = Variables.DifficultyDamageFactor;

            int atkBounds = skill != null ? skill.atk : 10;
            /*
             *公式来源参考:
             *https://game.ali213.net/thread-5983352-1-1.html  freedomv20的[数据研究] <三国志11 战斗伤害计算公式>
             *https://www.bilibili.com/opus/828102349572538433 ryan_knight_12吧 楚狂的 <三国志11伤害到底是怎样算的?>
             *https://tieba.baidu.com/p/6061024246?pn=1 不懂秃驴爱的 <三国志11：部队的兵力与攻击力数据实测，究竟带多少兵才是最优解>
             */

            int damage = (int)(
                (

                (System.Math.Pow(atkBounds * Variables.fight_base_damage, 0.5) + System.Math.Max(0, (int)((System.Math.Pow(attacker.Attack, 2) - System.Math.Pow(System.Math.Max(40, target.Defence), 2)) / 300)) +
                System.Math.Max(0, (attacker.troops - target.troops) / Variables.fight_base_troops_need) + 50)

                * 10 * ((int)(

                (((int)(attacker.troops * 0.01) + 300) * System.Math.Pow((attacker.Attack + 50), 2)) /
                (((int)(attacker.troops * 0.01) + 300) * System.Math.Pow((attacker.Attack + 50), 2) * 0.01 +
                ((int)(target.troops * 0.01) + 300) * System.Math.Pow((target.Defence + 50), 2) * 0.01)

                - 50)

                + 50)
                // 原有基础上优化System.Math.Max(1, attacker.troops / 4),1兵打出15伤害同于实际测试
                * System.Math.Min(System.Math.Pow(System.Math.Max(1, attacker.troops / 4), 0.5), 40)

                * Variables.fight_damage_magic_number /* * 太鼓台系数*/

                + attacker.troops / Variables.fight_base_troop_count

                )
                //兵种相克系数
                * CalculateRestrainBoost(attacker, target)

                // 额外增益 (科技系数等)
                * System.Math.Max(0, (1 + attacker.DamageTroopExtraFactor))

                // 难度系数,仅对玩家生效
                * difficultyDamageFactor
                );

            ////士气矫正后的伤害
            //damage = damage * (UnityEngine.Mathf.Max(attacker.morale - Variables.fight_morale_decay_below, 0) / (100 - Variables.fight_morale_decay_below) *
            //Variables.fight_morale_add + (1 - Variables.fight_morale_decay_percent) + UnityEngine.Mathf.Min(UnityEngine.Mathf.Max(attacker.morale, 0), Variables.fight_morale_decay_below) / Variables.fight_morale_decay_below * Variables.fight_morale_decay_percent);

            return damage;
        }

        public static int CalculateSkillDamage(Troop attacker, BuildingBase target, SkillInstance skill)
        {
            var attack_troops_type = attacker.TroopType;
            var buildingType = target.BuildingType;
            ScenarioVariables Variables = Scenario.Cur.Variables;

            float difficultyDamageFactor = 1;
            if (attacker.mBelongForce != null && attacker.mBelongForce.IsPlayer)
                difficultyDamageFactor = Variables.DifficultyDamageFactor;

            if (attacker.IsHelepolis)
            {
                int damage = (int)(attacker.troops / 25 + System.Math.Pow(attacker.troops, 0.5f) + System.Math.Min(System.Math.Pow(attacker.troops, 0.5f), 40) * attacker.Attack * System.Math.Pow((1f / 1500f), 0.5f) * (1 + (float)skill.atkDurability / 25f) * buildingType.damageBounds
               // 额外增益 (科技系数等)
               * System.Math.Max(0, (1 + attacker.DamageBuildingExtraFactor))
               * attack_troops_type.durabilityDmg / 100
               // 难度系数,仅对玩家生效
               * difficultyDamageFactor
               );
                return damage;
            }
            else
            {
                int damage = (int)(System.Math.Pow(attacker.troops, 0.5f) * attacker.Attack * System.Math.Pow((1f / 1500f), 0.5f) * (1 + (float)skill.atkDurability / 25f) * buildingType.damageBounds
                // 额外增益 (科技系数等)
                * System.Math.Max(0, (1 + attacker.DamageBuildingExtraFactor))
                * attack_troops_type.durabilityDmg / 100
                // 难度系数,仅对玩家生效
                * difficultyDamageFactor
                );

                return damage;
            }
        }

        public static int CalculateSkillDamageTroopOnCity(Troop attacker, City target, SkillInstance skill)
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;

            float difficultyDamageFactor = 1;
            if (attacker.mBelongForce != null && attacker.mBelongForce.IsPlayer)
                difficultyDamageFactor = Variables.DifficultyDamageFactor;

            int atkBounds = skill != null ? skill.atk : 10;
            /*
             *公式来源参考:
             *https://game.ali213.net/thread-5983352-1-1.html  freedomv20的[数据研究] <三国志11 战斗伤害计算公式>
             *https://www.bilibili.com/opus/828102349572538433 ryan_knight_12吧 楚狂的 <三国志11伤害到底是怎样算的?>
             *https://tieba.baidu.com/p/6061024246?pn=1 不懂秃驴爱的 <三国志11：部队的兵力与攻击力数据实测，究竟带多少兵才是最优解>
             */

            int damage = (int)(
                (

                (System.Math.Pow(atkBounds * Variables.fight_base_damage, 0.5) + System.Math.Max(0, (int)((System.Math.Pow(attacker.Attack, 2) - System.Math.Pow(System.Math.Max(40, target.GetDefence()), 2)) / 300)) +
                System.Math.Max(0, (attacker.troops - target.troops) / Variables.fight_base_troops_need) + 50)

                * 10 * ((int)(

                (((int)(attacker.troops * 0.01) + 300) * System.Math.Pow((attacker.Attack + 50), 2)) /
                (((int)(attacker.troops * 0.01) + 300) * System.Math.Pow((attacker.Attack + 50), 2) * 0.01 +
                ((int)(target.troops * 0.01) + 300) * System.Math.Pow((target.GetDefence() + 50), 2) * 0.01)

                - 50)

                + 50)
                // 原有基础上优化System.Math.Max(1, attacker.troops / 4),1兵打出15伤害同于实际测试
                * System.Math.Min(System.Math.Pow(System.Math.Max(1, attacker.troops / 4), 0.5), 40)

                * Variables.fight_damage_magic_number /* * 太鼓台系数*/

                * target.BuildingType.damageBounds

                + attacker.troops / Variables.fight_base_troop_count

                )

                // 额外增益 (科技系数等)
                * System.Math.Max(0, (1 + attacker.DamageTroopExtraFactor))

                // 难度系数,仅对玩家生效
                * difficultyDamageFactor
                );

            ////士气矫正后的伤害
            //damage = damage * (UnityEngine.Mathf.Max(attacker.morale - Variables.fight_morale_decay_below, 0) / (100 - Variables.fight_morale_decay_below) *
            //Variables.fight_morale_add + (1 - Variables.fight_morale_decay_percent) + UnityEngine.Mathf.Min(UnityEngine.Mathf.Max(attacker.morale, 0), Variables.fight_morale_decay_below) / Variables.fight_morale_decay_below * Variables.fight_morale_decay_percent);

            return damage;
        }


        public static int CalculateSkillDamage(BuildingBase attacker, Troop target, SkillInstance skill)
        {

            ScenarioVariables Variables = Scenario.Cur.Variables;

            //基础伤害
            float base_atk = attacker.GetAttack();
            int base_troops = attacker.GetSkillMethodAvaliabledTroops();

            float difficultyDamageFactor = 1;
            if (attacker.mBelongForce != null && attacker.mBelongForce.IsPlayer)
                difficultyDamageFactor = Variables.DifficultyDamageFactor;

            int damage = (int)(
                 (

                 (System.Math.Max(0, (int)((System.Math.Pow(base_atk, 2) - System.Math.Pow(System.Math.Max(40, target.Defence), 2)) / 300)) +
                 System.Math.Max(0, (base_troops - target.troops) / Variables.fight_base_troops_need) + 50)

                 * 10 * ((int)(

                 (((int)(base_troops * 0.01) + 300) * System.Math.Pow((base_atk + 50), 2)) /
                 (((int)(base_troops * 0.01) + 300) * System.Math.Pow((base_atk + 50), 2) * 0.01 +
                 ((int)(target.troops * 0.01) + 300) * System.Math.Pow((target.Defence + 50), 2) * 0.01)

                 - 50)

                 + 50)

                 * System.Math.Min(System.Math.Pow(System.Math.Max(1, base_troops / 4), 0.5), 40)

                 * Variables.fight_damage_magic_number /* * 太鼓台系数*/

                 + base_troops / Variables.fight_base_troop_count

                 )

                // 难度系数,仅对玩家生效
                * difficultyDamageFactor

                 // 额外增益 (科技系数等)
                 //* System.Math.Max(0, (1 + attacker.DamageTroopExtraFactor))
                 /* * 难度系数*/);

            return damage;
        }

        public static int CalculateSkillDamage(BuildingBase attacker, Troop target, int atk)
        {

            ScenarioVariables Variables = Scenario.Cur.Variables;

            //基础伤害
            float base_atk = atk;
            int base_troops = attacker.GetSkillMethodAvaliabledTroops();

            float difficultyDamageFactor = 1;
            if (attacker.mBelongForce != null && attacker.mBelongForce.IsPlayer)
                difficultyDamageFactor = Variables.DifficultyDamageFactor;

            int damage = (int)(
                 (

                 (System.Math.Max(0, (int)((System.Math.Pow(base_atk, 2) - System.Math.Pow(System.Math.Max(40, target.Defence), 2)) / 300)) +
                 System.Math.Max(0, (base_troops - target.troops) / Variables.fight_base_troops_need) + 50)

                 * 10 * ((int)(

                 (((int)(base_troops * 0.01) + 300) * System.Math.Pow((base_atk + 50), 2)) /
                 (((int)(base_troops * 0.01) + 300) * System.Math.Pow((base_atk + 50), 2) * 0.01 +
                 ((int)(target.troops * 0.01) + 300) * System.Math.Pow((target.Defence + 50), 2) * 0.01)

                 - 50)

                 + 50)

                 * System.Math.Min(System.Math.Pow(System.Math.Max(1, base_troops / 4), 0.5), 40)

                 * Variables.fight_damage_magic_number /* * 太鼓台系数*/

                 + base_troops / Variables.fight_base_troop_count

                 )

                // 难度系数,仅对玩家生效
                * difficultyDamageFactor

                 // 额外增益 (科技系数等)
                 //* System.Math.Max(0, (1 + attacker.DamageTroopExtraFactor))
                 /* * 难度系数*/);

            return damage;
        }

        // 暴击判断
        public static bool CalculateSkillCriticalBoost(Troop attacker, Troop defender, Skill skill, out float p)
        {
            //TODO 完善暴击逻辑
            p = 1;
            return false;
        }

        // 暴击判断
        public static bool CalculateSkillCriticalBoost(Troop attacker, BuildingBase defender, Skill skill, out float p)
        {
            //TODO 完善暴击逻辑
            p = 1;
            return false;
        }

        // 克制系数
        public static float CalculateRestrainBoost(Troop attacker, Troop target)
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;
            var attack_troops_type = attacker.TroopType;
            float[] t_map = Variables.troops_type_restraint[attack_troops_type.kind];
            var defender_troops_type = target.TroopType;
            return t_map[defender_troops_type.kind];
        }

        public static int CheckTroopTypeLevel(TroopType troopType, Person person)
        {
            int influenceAbility = troopType.influenceAbility - 1;
            if (influenceAbility < 0) return 0;
            switch (troopType.influenceAbility)
            {
                case (int)AbilityType.Spear:
                    return person.SpearLv;
                case (int)AbilityType.Halberd:
                    return person.HalberdLv;
                case (int)AbilityType.Water:
                    return person.WaterLv;
                case (int)AbilityType.Crossbow:
                    return person.CrossbowLv;
                case (int)AbilityType.Ride:
                    return person.RideLv;
                case (int)AbilityType.Machine:
                    return person.MachineLv;
            }
            return 0;
        }


        // 适应力加成
        //@param attacker Troops
        public int TroopsLevelBoost(int value, int troopTypeLv)
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;
            if (troopTypeLv < 0)
                troopTypeLv = 0;
            if (troopTypeLv >= Variables.troops_adaptation_level_boost.Length)
                troopTypeLv = Variables.troops_adaptation_level_boost.Length - 1;

            return value * Variables.troops_adaptation_level_boost[troopTypeLv] / 100;
        }

        internal static List<Cell> tempCellList = new List<Cell>(256);
        internal static List<TroopMoveEvent> tempMoveEventList = new List<TroopMoveEvent>(32);
        internal static List<Cell> spellRangeCells = new List<Cell>(256);
        internal bool isMoving = false;
        internal IRenderEventBase moveRenderEvent = null;
        internal IRenderEventBase actionRenderEvent = null;
        internal IRenderEventBase skillRenderEvent = null;

        /// <summary>
        /// skillRenderEvent 是否由"援助补刀"排入。
        /// 援助不属于这支部队自己的行动, 它播完后不能拿来当部队下一次施法的"已完成"信号,
        /// 否则那次普通攻击会被 SpellSkill 开头的早退分支整个吞掉(没出手却直接结束行动)
        /// </summary>
        internal bool skillRenderEventIsAssist;

        /// <summary>
        /// 该方法必须确定destCell在移动范围内
        /// </summary>
        /// <param name="destCell"></param>
        /// <returns></returns>
        public bool MoveTo(Cell destCell)
        {
            if (destCell == cell)
            {
                moveRenderEvent = null;
                isMoving = false;
                return true;
            }

            if (moveRenderEvent != null && moveRenderEvent.IsDone)
            {
                moveRenderEvent = null;
                isMoving = false;
                return true;
            }
            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append($"{isMoving},");
            if (!isMoving)
            {
                tempCellList.Clear();
                tempMoveEventList.Clear();
                //TODO: 移动
                Scenario.Cur.Map.GetMovePath(this, destCell, tempCellList);

                isMoving = true;
                Cell start = cell;
                for (int i = 1; i < tempCellList.Count; i++)
                {
                    bool isLast = (i == (tempCellList.Count - 1));
                    Cell dest = tempCellList[i];
                    TroopMoveEvent @event = RenderEvent.Instance.Create<TroopMoveEvent>();
                    @event.Init(this, start, dest, isLast, null);

                    if (isLast)
                        moveRenderEvent = @event;

                    RenderEvent.Instance.Add(@event);
                    start = dest;
                }

                if (moveRenderEvent == null)
                {
                    isMoving = false;
                    return true;
                }
            }


            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append($"11{moveRenderEvent},");

            if (moveRenderEvent == null)
            {
                isMoving = false;
                return true;
            }

            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append("11,");

            return false;
        }

        public bool TryMoveToSpell(Cell destCell, SkillInstance skill)
        {
            if (!isMoving)
            {
                tempCellList.Clear();
                tempMoveEventList.Clear();
                spellRangeCells.Clear();
                //TODO: 移动
                Scenario.Cur.Map.GetMovePath(this, destCell, tempCellList);

                isMoving = true;
                Cell start = cell;
                for (int i = 1; i < tempCellList.Count; i++)
                {
                    Cell dest = tempCellList[i];

                    bool findSpell = false;
                    skill.GetSpellRange(this, cell, spellRangeCells);
                    for (int k = 0; k < spellRangeCells.Count; k++)
                    {
                        Cell spellCell = spellRangeCells[k];
                        if (spellCell == destCell)
                        {
                            findSpell = true;
                            break;
                        }
                    }

                    TroopMoveEvent @event = RenderEvent.Instance.Create<TroopMoveEvent>();
                    @event.Init(this, start, dest, i == tempCellList.Count - 1, null);
                    RenderEvent.Instance.Add(@event);
                    start = dest;

                    if (findSpell)
                    {
                        break;
                    }
                }
            }

            return false;
        }


        public bool ChangeGold(int num, bool showInfo = true)
        {
            if (num != 0)
            {
                Render?.ShowInfo(num, (int)InfoType.Gold);
            }

            gold += num;
            if (gold < 0)
            {
                gold = 0;
                return true;
            }
            return false;
        }

        public bool ChangeFood(int num, bool showInfo = true)
        {
            if (showInfo && num != 0)
            {
                Render?.ShowInfo(num, (int)InfoType.Food);
            }

            food += num;
            if (food < 0)
            {
                food = 0;
                return true;
            }
            return false;
        }

        public bool ChangeTroops(int num, SangoObject atk, int atkBack)
        {
            if (!IsAlive)
                return false;

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(num);
            GameEvent.OnTroopChangeTroops?.Invoke(this, atk, atkBack, overrideData);
            num = overrideData.ValueAndRecycle;

            if (num == 0)
                return IsAlive;

            if (Render != null)
            {
                bool isCrit = false;
                if (atk.ObjectType == SangoObjectType.SkillInstance)
                {
                    SkillInstance skill = (SkillInstance)atk;
                    if (skill != null)
                    {
                        isCrit = skill.IsCritical();
                    }
                    Render.ShowInfo(num, (int)InfoType.Troop, isCrit);
                }
            }
            troops = troops + num;
            if (num < 0)
            {

                int absNum = System.Math.Abs(num);
                woundedTroops += (int)System.Math.Ceiling(absNum * 0.14f);
                int _foodCost = (int)System.Math.Ceiling(Scenario.Cur.Variables.baseFoodCostInTroop * absNum * TroopType.foodCostFactor) / 2;
                int divFood = 0;
                // 有概率保留部分
                if (GameRandom.Chance(80))
                    divFood += _foodCost;
                if (GameRandom.Chance(50))
                    divFood += _foodCost;
                ChangeFood(-divFood, false);

                IsAlive = troops > 0;

                if (!IsAlive && Render != null && Render.IsVisible())
                {
                    GameMedia.Instance.PlayPersonSay(Leader, GameRandom.Chance(50) ? 3216 : 3230);
                    GameParticales.Instance.PlayEfect("Assets/Effect/Prefab/ef_troop_destroy.prefab", Render.MapObject.position, 3);
                }
            }
            else
            {
                if (troops > MaxTroops)
                    troops = MaxTroops;
            }

            if (!IsAlive)
            {
#if SANGO_DEBUG
                Sango.Log.Info($"{mBelongForce.Name}的[{Name} 部队 溃灭!!");
#endif

                if (Render != null && Render.IsVisible())
                {
                    GameMedia.Instance.PlaySfx(83);
                }

                OnDestroy(atk, atkBack);

                // 移除
                Clear();
            }

            if (Render != null)
            {
                Render.UpdateRender();
            }

            return IsAlive;
        }

        public void ChangeMorale(int num, bool showInfo = true)
        {
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(num);
            GameEvent.OnTroopChangeMorale?.Invoke(this, morale, overrideData);
            num = overrideData.ValueAndRecycle;

            if (num == 0)
                return;

            morale += num;

            if (morale < 0)
                morale = 0;
            else if (morale > MaxMorale)
                morale = MaxMorale;

            if (showInfo)
                Render?.ShowInfo(num, (int)InfoType.Morale);
            Render?.UpdateRender();
        }

        public int GetCaptureChangce()
        {
            return captiveChangce;
        }

        public void OnDestroy(SangoObject atk, int atkBack)
        {
            // 添加俘虏流程
            if (atk.ObjectType == SangoObjectType.SkillInstance)
            {
                SkillInstance skill = (SkillInstance)atk;

                Troop atkTroop = skill.master;

                List<Person> captives = new List<Person>();
                if (Leader.state != (int)PersonStateType.Governor)
                {
                    int p = Math.Max(0, atkTroop.GetCaptureChangce() - Leader.escapeFactorWhenTroopDestroy);
                    if (GameRandom.Chance(p))
                        captives.Add(Leader);
                }
                if (Member1 != null && Member1.state != (int)PersonStateType.Governor)
                {
                    int p = Math.Max(0, atkTroop.GetCaptureChangce() - Member1.escapeFactorWhenTroopDestroy);
                    if (GameRandom.Chance(p))
                        captives.Add(Member1);
                }

                if (Member2 != null && Member2.state != (int)PersonStateType.Governor)
                {
                    int p = Math.Max(0, atkTroop.GetCaptureChangce() - Member2.escapeFactorWhenTroopDestroy);
                    if (GameRandom.Chance(p))
                        captives.Add(Member2);
                }

                if (captives.Count > 0)
                {
                    CityRecruitPersonWhenTroopFallEvent te = RenderEvent.Instance.Create<CityRecruitPersonWhenTroopFallEvent>();
                    te.Init(captives, atkTroop);
                    RenderEvent.Instance.Add(te);
                }
            }

            GameEvent.OnTroopDestroyed?.Invoke(this, atk, atkBack, Scenario.Cur);

        }

        public void ReleaseCaptive()
        {
            // 必须倒序,因为Escape会修改captiveList
            for (int i = captiveList.Count - 1; i >= 0; i--)
            {
                Person person = captiveList[i];
                person.Escape(EscapeType.TroopDestroyed);
            }
            captiveList.Clear();
        }

        public int GetTroopsNum()
        {
            return troops;
        }

        public bool SpellSkill(SkillInstance skill, Cell spellCell, bool waitForCheer = true)
        {
            // "兄弟同心"加气走异步对白: 本次攻击想吃的这口气力还没真正加满时先挂起,
            // 等对白播完(回调里清标记)再真正出手, 从而保证"先提气、再攻击"。
            // 援助补刀是别人正在结算的攻击的连带演出, 不能被挂起, 故 waitForCheer 传 false。
            if (waitForCheer && pendingSwornCheer)
                return false;

            if (skillRenderEvent != null)
            {
                if (skillRenderEvent.IsDone)
                {
                    skillRenderEvent = null;
                    // 援助补刀留下的"已播完"事件不是本次施法的完成信号, 这里return true会让
                    // 这支部队明明一枪没出, 却被调用方当成"攻击已完成"而结束整个行动
                    if (!skillRenderEventIsAssist)
                        return true;
                    skillRenderEventIsAssist = false;
                }
                else
                    return false;
            }
            // 确认要排一次新的施法事件了, 抹掉技能实例上可能残留的援助标记,
            // 否则这次正常攻击会被当成援助折算伤害, 也不会去呼叫别人援助
            if (skill != null)
            {
                skill.assistAttackFlag = false;
                skillRenderEventIsAssist = false;
            }
            skill.tempCriticalFactor = 100;
            if (skill.CheckSuccess(spellCell))
            {

                int criticalFactor = skill.CheckCritical(spellCell);
                if (criticalFactor > 100 && !skill.IsNormal())
                {
#if SANGO_DEBUG
                    Sango.Log.Info($"{mBelongForce.Name}的[{Name} 部队 技能: {skill.Name} =>({spellCell.x},{spellCell.y})]  暴击判定成功!  暴击伤害倍率{criticalFactor}!!");
#endif
                    TroopSpellSkillCriticalEvent @event = RenderEvent.Instance.Create<TroopSpellSkillCriticalEvent>();
                    @event.Init(skill, spellCell, criticalFactor);
                    skillRenderEvent = @event;
                    RenderEvent.Instance.Add(@event);
                }
                else
                {
                    TroopSpellSkillEvent @event = RenderEvent.Instance.Create<TroopSpellSkillEvent>();
                    @event.Init(skill, spellCell);
                    skillRenderEvent = @event;
                    RenderEvent.Instance.Add(@event);
                }
            }
            else
            {
#if SANGO_DEBUG
                Sango.Log.Info($"{mBelongForce.Name}的[{Name} 部队 技能: {skill.Name} =>({spellCell.x},{spellCell.y})]  判定失败! 释放不成功!!");
#endif
                TroopSpellSkillFailEvent @event = RenderEvent.Instance.Create<TroopSpellSkillFailEvent>();
                @event.Init(this, skill, spellCell);
                skillRenderEvent = @event;
                RenderEvent.Instance.Add(@event);
            }
            return false;
        }

        public void SupplyTroop(Troop target)
        {

        }

        public void SupplyTroop(Troop target, ItemStore itemStore, int gold, int food, int troops)
        {
            // 中和士气
            int m = (morale * troops + target.morale * target.troops) / (target.troops + troops);

            this.troops -= troops;
            this.gold -= gold;
            this.food -= food;
            this.itemStore.Remove(itemStore);
            target.itemStore.Add(itemStore);
            target.troops += troops;
            target.gold += gold;
            target.food += food;

            int dis = m - target.morale;
            target.morale = m;

            if (troops > 0)
                Render?.ShowInfo(-troops, (int)InfoType.Troop);
            if (gold > 0)
                Render?.ShowInfo(-gold, (int)InfoType.Gold);
            if (food > 0)
                Render?.ShowInfo(-food, (int)InfoType.Food);

            if (troops > 0)
                target.Render?.ShowInfo(troops, (int)InfoType.Troop);
            if (gold > 0)
                target.Render?.ShowInfo(gold, (int)InfoType.Gold);
            if (food > 0)
                target.Render?.ShowInfo(food, (int)InfoType.Food);
            if (dis != 0)
                target.Render?.ShowInfo(dis, (int)InfoType.Morale);

            ActionOver = true;
            Render?.UpdateRender();
        }

        public bool BuildBuilding(Cell dest, BuildingType buildingType)
        {
            if (actionRenderEvent != null)
            {
                if (actionRenderEvent.IsDone)
                {
                    actionRenderEvent = null;
                    return true;
                }
                else
                    return false;
            }

            TroopBuildBuildingEvent @event = RenderEvent.Instance.Create<TroopBuildBuildingEvent>();
            @event.Init(this, buildingType, dest);
            actionRenderEvent = @event;
            RenderEvent.Instance.Add(@event);

            return false;
        }

        Cell tryToDest;
        public bool TryCloseTo(Cell destCell)
        {
            return TryMoveToCell(destCell);
        }
        public bool TryMoveToCity(City city)
        {
            return TryMoveToCell(city.CenterCell, (x) =>
            {
                return x.building == city;
            });
        }
        public bool TryMoveToCell(Cell targetCell)
        {
            return TryMoveToCell(targetCell, null);
        }

        public bool TryMoveToCell(Cell targetCell, Func<Cell, bool> check)
        {
            if (targetCell == cell)
            {
                moveRenderEvent = null;
                isMoving = false;
                return true;
            }

            if (!isMoving)
            {      //TODO: 尝试移动
                tempCellList.Clear();
                tryToDest = null;

                // 先检查移动范围内是否可达目标
                Map map = Scenario.Cur.Map;
                if (MoveRange.Count == 0)
                    map.GetMoveRange(this, MoveRange);
                for (int i = 1; i < MoveRange.Count; ++i)
                {
                    Cell dst = MoveRange[i];
                    if (dst == targetCell || (check != null && check(dst)))
                    {
                        tryToDest = dst;
                        break;
                    }
                }

                if (tryToDest == null)
                {
                    //TODO: 移动
                    map.GetDirectMovePath(this, targetCell, tempCellList);
                    Cell thisCell = this.cell;
                    for (int i = 1; i < tempCellList.Count; i++)
                    {
                        Cell dest = tempCellList[i];
                        if (!MoveRange.Contains(dest) || (check != null && check(dest)))
                        {
                            tryToDest = thisCell;
                            break;
                        }
                        else
                        {
                            thisCell = dest;
                        }
                    }
                }

                if (tryToDest != null && (check == null || !check(tryToDest)) && !tryToDest.CanStay(this))
                {
                    PriorityQueue<Cell> nearnestCellInMoveRange = new PriorityQueue<Cell>();
                    for (int i = 0; i < MoveRange.Count; i++)
                    {
                        Cell cell = MoveRange[i];
                        if (cell.IsEmpty() && cell.CanStay(this))
                        {
                            nearnestCellInMoveRange.Push(cell, map.Distance(cell, tryToDest));
                        }
                    }
                    tryToDest = nearnestCellInMoveRange.Lower();
                }
            }

            if (tryToDest == null)
            {
                return true;
            }

            return MoveTo(tryToDest);
        }

        void UpdateTerrainBonus(Cell destCell)
        {
            // 地形影响
            int terrainId = destCell.TerrainType.Id;
            if (TroopType.terrainIncreaseAtkBonus != null && terrainId < TroopType.terrainIncreaseAtkBonus.Length)
                trrainBonusAtk = TroopType.terrainIncreaseAtkBonus[terrainId];
            else
                trrainBonusAtk = 0;

            if (TroopType.terrainDecreaseDefenceBonus != null && terrainId < TroopType.terrainDecreaseDefenceBonus.Length)
                trrainBonusDef = TroopType.terrainDecreaseDefenceBonus[terrainId];
            else
                trrainBonusDef = 0;
        }

        public void UpdateCell(Cell destCell, Cell lastCell, bool isEndMove)
        {
            //TODO: 地格更新,需要处理一些事件
            if (lastCell != null)
                GameEvent.OnTroopLeaveCell?.Invoke(this, lastCell, destCell);

            GameEvent.OnTroopEnterCell?.Invoke(this, destCell, lastCell);


#if SANGO_DEBUG
            Sango.Log.Info($"{mBelongForce.Name}的[{Name} 部队 移动=> ({destCell.x},{destCell.y})]");
#endif

            if (destCell.fire != null)
                destCell.fire.BurnTroop(this);

            Render.UpdateModelByCell(destCell);
            UpdateTerrainBonus(destCell);

            if (isEndMove)
            {
                destCell.troop = this;
                cell.troop = null;
                cell = destCell;
                if (Render.MapObject != null)
                {
                    Render.MapObject.position = cell.Position;
                }
                //else
                //{
                //    Sango.Log.Error($"why {Name}->Render.MapObject is null");
                //}

                // 只有整段移动结束才结算, 途中经过的格子不触发
                TryCheerSwornBrother();
            }
        }

        /// <summary>
        /// 兄弟同心: 部队移动完成后搜索相邻格子的部队,
        /// 把所有"主将与自己同属一个兄弟组"的友军部队全部找出来(不止一队),
        /// 做一次概率检定, 成功则自己与这些兄弟部队的气力一起推满;
        /// 同一组兄弟部队每回合只结算一次
        /// </summary>
        public const int SwornCheerChance = 30;

        /// <summary>
        /// 执行一次"兄弟同心"检查
        /// </summary>
        /// <returns>是否成功推满气力</returns>
        public bool TryCheerSwornBrother()
        {
            if (!IsAlive) return false;
            // 主将不属于任何兄弟组(剧本原设兄弟/仲介结义)时直接跳过
            if (Leader == null || !Leader.HasSwornBrother) return false;

            int turn = Scenario.Cur.TurnCount;
            // 本回合已经结算过
            if (swornCheerTurn == turn) return false;

            // 先收齐相邻格里全部同组兄弟部队, 不能只找到第一个就算完
            List<Troop> brothers = new List<Troop>();
            Cell[] neighbors = cell.Neighbors;
            for (int i = 0, count = neighbors.Length; i < count; ++i)
            {
                Troop other = neighbors[i] == null ? null : neighbors[i].troop;
                if (CanCheerWith(other, turn))
                    brothers.Add(other);
            }

            if (brothers.Count == 0) return false;

            // 在场各队气力本来就都是满的, 不必再走一场对白
            if (IsAllCheerMoraleFull(brothers)) return false;

            // 整组只做一次判定, 过了就全体推满
            if (!GameRandom.Chance(SwornCheerChance)) return false;

            // 先记下回合, 免得对白期间又有兄弟部队靠过来重复排队
            swornCheerTurn = turn;
            for (int i = 0; i < brothers.Count; i++)
                brothers[i].swornCheerTurn = turn;

            List<GameDialog.TalkData> talks = BuildCheerTalks(brothers);
            if (talks.Count == 0)
            {
                CheerFullMorale(brothers);
                return true;
            }

            // 对白是异步的: 先把自己和所有参与兄弟都标记为"提气进行中",
            // 让接下来想吃这口气力的攻击挂起等对白播完, 保证"先提气、再攻击"
            MarkCheerPending(brothers, true);

            // 每个武将各说一句, 全部说完才加气
            GameDialog.StartTalk(talks, () =>
            {
                MarkCheerPending(brothers, false);
                CheerFullMorale(brothers);
            });
            return true;
        }

        /// <summary>
        /// 批量设置/清除自己和相邻兄弟部队的"提气进行中"标记
        /// </summary>
        void MarkCheerPending(List<Troop> brothers, bool pending)
        {
            pendingSwornCheer = pending;
            for (int i = 0; i < brothers.Count; i++)
            {
                Troop brother = brothers[i];
                if (brother != null)
                    brother.pendingSwornCheer = pending;
            }
        }

        /// <summary>
        /// 自己与这些兄弟部队的气力是不是都已经满了
        /// </summary>
        bool IsAllCheerMoraleFull(List<Troop> brothers)
        {
            if (morale < MaxMorale) return false;
            for (int i = 0; i < brothers.Count; i++)
            {
                Troop brother = brothers[i];
                if (brother.morale < brother.MaxMorale)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 组织"兄弟同心"的台词: 参与的每支部队里在场武将每人一句, 兄弟组的组头(兄长)先开口
        /// </summary>
        List<GameDialog.TalkData> BuildCheerTalks(List<Troop> brothers)
        {
            List<GameDialog.TalkData> elderTalks = new List<GameDialog.TalkData>();
            List<GameDialog.TalkData> youngerTalks = new List<GameDialog.TalkData>();

            // 自己排最前, 其余兄弟部队按相邻格顺序
            CollectCheerTalks(this, elderTalks, youngerTalks);
            for (int i = 0; i < brothers.Count; i++)
                CollectCheerTalks(brothers[i], elderTalks, youngerTalks);

            // 不用Sort(不稳定排序), 直接分两组拼接, 保证兄长在前、同队内主将先于副将
            List<GameDialog.TalkData> talks = new List<GameDialog.TalkData>(elderTalks.Count + youngerTalks.Count);
            talks.AddRange(elderTalks);
            talks.AddRange(youngerTalks);
            return talks;
        }

        /// <summary>
        /// 按主将、副将的顺序收集一支部队里每个人的台词
        /// </summary>
        static void CollectCheerTalks(Troop troop, List<GameDialog.TalkData> elderTalks, List<GameDialog.TalkData> youngerTalks)
        {
            // 兄弟提气只有主将说话，副将不参与
            Person person = troop.Leader;
            if (person == null) return;
            GameDialog.TalkData talk = new GameDialog.TalkData();
            talk.person = person;
            talk.text = CheerTalk(person);
            if (IsFamilyHead(person))
                elderTalks.Add(talk);
            else
                youngerTalks.Add(talk);
        }

        /// <summary>
        /// 是否兄弟组的组头: 仲介结义与剧本原设都是组头的 Brother 指向自己
        /// </summary>
        static bool IsFamilyHead(Person person)
        {
            return person != null && person.Brother > 0 && person.Brother == person.Id;
        }

        /// <summary>
        /// 兄长与幼弟各自的台词, 各备两句随机, 免得反复触发时千篇一律
        /// </summary>
        static string CheerTalk(Person person)
        {
            return GameRandom.Chance(50)
                ? $"你我兄弟并肩，定能奋力杀敌！"
                : "兄弟同心，定能战胜敌军！";
        }

        /// <summary>
        /// 相邻格子的这支部队算不算"可以一起受激励的兄弟部队"
        /// </summary>
        bool CanCheerWith(Troop other, int turn)
        {
            if (other == null || other == this) return false;
            if (!other.IsAlive || other.Leader == null) return false;
            if (other.swornCheerTurn == turn) return false;

            // 友军: 同势力或同盟
            if (!IsSameForce(other) && !IsAlliance(other)) return false;

            // 主将与自己同属一个兄弟组;
            // 必须用对称的 IsBrotherGroupmate: Person.IsBrother 读的是 BrotherList,
            // 而幼弟那份列表里不一定含组头, 会让幼弟移动时漏掉兄长那支部队。
            // 同一个组里的人彼此都是兄弟, 所以相邻的多支部队无需再两两判定
            if (!Leader.IsBrotherGroupmate(other.Leader)) return false;

            return true;
        }

        /// <summary>
        /// 对白全部说完后, 把自己和所有相邻兄弟部队的气力一起推到上限
        /// </summary>
        void CheerFullMorale(List<Troop> brothers)
        {
            CheerSelfFullMorale();
            for (int i = 0; i < brothers.Count; i++)
            {
                Troop brother = brothers[i];
                if (brother != null)
                    brother.CheerSelfFullMorale();
            }

            Sango.Log.Info($"{mBelongForce.Name}的[{Name}]与兄弟部队[{JoinTroopNames(brothers)}]同心同德，{brothers.Count + 1}队气力推满！");
        }

        /// <summary>
        /// 用顿号拼接兄弟部队名, 仅供日志使用
        /// </summary>
        static string JoinTroopNames(List<Troop> troops)
        {
            string names = "";
            for (int i = 0; i < troops.Count; i++)
            {
                if (troops[i] == null) continue;
                names += names.Length == 0 ? troops[i].Name : "、" + troops[i].Name;
            }
            return names;
        }

        /// <summary>
        /// 把本部队气力推到上限
        /// </summary>
        void CheerSelfFullMorale()
        {
            // 对白期间部队可能已经溃灭或回城, 这里要重新检查
            if (!IsAlive) return;

            int num = MaxMorale - morale;
            if (num > 0)
                ChangeMorale(num);
        }

        /// <summary>
        /// 援助攻击的伤害折算比例(百分比)
        /// </summary>
        public const int AssistAttackDamagePercent = 50;

        /// <summary>
        /// "辅佐"特性的Id(Features.json: 即使没建立人际关系(厌恶除外)也可获得支援攻击)
        /// </summary>
        public const int AssistFeatureId = 27;

        /// <summary>
        /// 援助搜索半径(格)。以敌军为圆心向外螺旋收候选:
        /// 一格内只要不是器械就能近战补刀, 两格及以上必须是射程够得着的弓箭军
        /// </summary>
        public const int AssistAttackRange = 4;

        // 螺旋候选格与远程射程格: 用静态缓冲复用, 免得每次呼叫援助都分配 List
        internal static List<Cell> assistSearchCells = new List<Cell>(128);
        internal static List<Cell> assistRangeCells = new List<Cell>(256);

        /// <summary>
        /// 各关系的援助概率档位(百分比)
        /// </summary>
        public const int AssistChanceSpouse = 50;
        public const int AssistChanceSwornBrother = 50;
        public const int AssistChanceAssistFeature = 30;
        public const int AssistChanceLike = 30;
        public const int AssistChanceBloodRelative = 20;

        /// <summary>
        /// 临时诊断开关: 打开后把援助攻击每一层的拦截原因写进日志, 定位完请置回false
        /// </summary>
        public static bool AssistDiagnosis = true;

        /// <summary>
        /// 援助攻击: 本部队攻击敌军却没把它打灭时, 敌军周围 AssistAttackRange 格内的友军部队
        /// 按其与本部队主将的关系分档判定概率, 命中则各补一记打折的普攻。
        /// 出手资格按距离分层: 一格内不是器械就能直接近战补刀,
        /// 两格及以上只能是弓箭军(带远程普攻), 而且敌军要落在它的射程里
        /// </summary>
        public void TryAssistAttack(Troop enemy)
        {
            if (!IsAlive || enemy == null || !enemy.IsAlive)
            {
                return;
            }
            if (Leader == null)
            {
                return;
            }
            Cell enemyCell = enemy.cell;
            Map map = Scenario.Cur == null ? null : Scenario.Cur.Map;
            if (enemyCell == null || map == null)
            {
                return;
            }

            // 用 GetSpiral 而不是 SpiralAction: 后者走的 RingAction 在地图边缘会先解引用再判空,
            // 半径大于1时可能空引用; GetSpiral 内部逐格 GetCell 已判过空
            // 螺旋由近及远, 天然让身边的部队优先补刀
            assistSearchCells.Clear();
            map.GetSpiral(enemyCell, AssistAttackRange, assistSearchCells);

            int candidates = 0;
            int joined = 0;
            for (int i = 0, count = assistSearchCells.Count; i < count; ++i)
            {
                Cell cell = assistSearchCells[i];
                // 圆心这一格就是敌军自己站着的格, 不是候选援助位
                if (cell == enemyCell) continue;
                Troop helper = cell.troop;
                if (helper == null) continue;
                candidates++;

                int distance = map.Distance(enemyCell, cell);

                string block = AssistBlockReason(helper, enemy);
                if (block != null)
                {
                    continue;
                }

                // 兵种与射程按距离分层, 顺带挑出这一发要用哪个普攻技能
                string reject = null;
                SkillInstance skill = SelectAssistSkill(helper, cell, enemyCell, distance, out reject);
                if (skill == null)
                {
                    continue;
                }

                int chance = CalcAssistChance(this, helper);
                if (chance <= 0)
                {
                    continue;
                }

                bool hit = GameRandom.Chance(chance);
                if (!hit) continue;

                if (DoAssistAttack(helper, enemy, skill))
                    joined++;
            }
        }

        /// <summary>
        /// 援助攻击的临时诊断输出
        /// </summary>
        public static void LogAssist(string message)
        {
            if (!AssistDiagnosis) return;
        }

        /// <summary>
        /// 按关系分档计算 helper 前来支援 self 本次攻击的概率(百分比)。
        /// 只取命中的第一档: 夫妇50 / 义兄弟50 / 辅佐特技30 / 亲爱30 / 直系血亲20 / 其他0
        /// </summary>
        public static int CalcAssistChance(Troop self, Troop helper)
        {
            if (self == null || helper == null)
            {
                return 0;
            }

            Person leader = self.Leader;
            Person helperLeader = helper.Leader;
            if (leader == null || helperLeader == null)
            {
                return 0;
            }

            // 诊断需要, 六项判据先全部算完再按优先级取值;
            // 短路只体现在return顺序上, 结果与逐条if完全一致, 不会出现诊断与实际不符
            bool isSpouse = leader.IsSpouse(helperLeader);
            bool isSwornBrother = leader.IsBrotherGroupmate(helperLeader);
            bool hasAssistFeature = leader.HasFeatrue(AssistFeatureId);
            bool isLike = leader.IsLike(helperLeader);
            bool isBloodRelative = leader.IsBloodRelative(helperLeader);
            bool hated = Person.IsHatedByEither(leader, helperLeader);



            // 夫妇
            if (isSpouse)
                return AssistChanceSpouse;

            // 义兄弟(剧本原设兄弟与本功能同源, 一并归入本档)
            if (isSwornBrother)
                return AssistChanceSwornBrother;

            // 持有"辅佐"特性, 且双方没有厌恶
            if (hasAssistFeature && !hated)
                return AssistChanceAssistFeature;

            // 亲爱
            if (isLike)
                return AssistChanceLike;

            // 直系血亲, 且双方没有厌恶
            if (isBloodRelative && !hated)
                return AssistChanceBloodRelative;

            if (hasAssistFeature || isBloodRelative)
                LogAssist("→ 0 (虽有辅佐特性或血亲, 但双方存在厌恶关系, 被拦下)");
            else
                LogAssist("→ 0 (五档全不沾, 改概率常量也不会触发)");

            return 0;
        }

        /// <summary>
        /// 这支部队是否具备前来援助的战场条件(不含兵种与射程, 那部分由 SelectAssistSkill 判)
        /// </summary>
        bool CanAssistAttack(Troop helper, Troop enemy)
        {
            return AssistBlockReason(helper, enemy) == null;
        }

        /// <summary>
        /// 战场条件逐条检查; 返回null表示通过, 否则返回被哪一条拦下。
        /// 兵种、射程这类与距离相关的资格由 SelectAssistSkill 判
        /// </summary>
        string AssistBlockReason(Troop helper, Troop enemy)
        {
            if (helper == null) return "该格没有部队";
            // 螺旋把圆心格也收了进来, 那格站的就是敌军自己
            if (helper == enemy) return "就是敌军本队";
            // 攻击者自己再打一次不算援助
            if (helper == this) return "就是攻击方自己";
            if (!helper.IsAlive) return "无法攻击";
            // 路上的部队不参与援助: 它的施法槽位会被移动事件清掉, 半路插手会把表现搞乱
            if (helper.isMoving) return "正在移动";
            // 必须是自己的友军: 同势力或同盟
            if (!IsSameForce(helper) && !IsAlliance(helper)) return "既不同势力也不同盟";
            // 援助方也得真能与敌军交战
            if (!helper.IsEnemy(enemy)) return "与该敌军不构成敌对";
            // 前面的援助已经把敌军打灭, 后面的不必再补刀
            if (!enemy.IsAlive) return "敌军已被打灭";
            if (enemy.cell == null) return "敌军已不在地图上";

            return null;
        }

        /// <summary>
        /// 按援助方与敌军的距离挑出本次补刀要用的普攻, 同时校验兵种与射程是否允许出手。
        /// 一格: 不是器械就行, 用近战普攻;
        /// 两格及以上: 只能用带远程普攻的弓箭/弩兵, 而且敌军必须在它的射程之内。
        /// 返回null表示出不了手, reject 给出原因
        /// </summary>
        SkillInstance SelectAssistSkill(Troop helper, Cell helperCell, Cell enemyCell, int distance, out string reject)
        {
            reject = null;

            // 兵器队一律不援助: 近战不会使, 远程也不给(两格外只留给弓箭军)
            if (helper.IsMachine)
            {
                reject = "是兵器部队";
                return null;
            }

            if (distance <= 1)
            {
                SkillInstance melee = helper.NormalSkill;
                if (melee == null)
                {
                    reject = "没有近战普攻";
                    return null;
                }
                return melee;
            }

            // "是不是弓箭军"以有没有远程普攻为准: 水战时 NormalRangeSkill 会切到水军那一份,
            // 而 LandTroopType.isRange 只反映陆战兵种, 拿它做判据会把水上的弓兵误杀
            SkillInstance ranged = helper.NormalRangeSkill;
            if (ranged == null)
            {
                reject = $"与敌军隔{distance}格, 不是弓箭军(没有远程普攻)";
                return null;
            }

            // 射程校验: GetSpellRange 给出的是这支部队站在原地能施放的格子集合, 敌军得在里面
            assistRangeCells.Clear();
            ranged.GetSpellRange(helper, helperCell, assistRangeCells);
            bool reachable = false;
            for (int i = 0, count = assistRangeCells.Count; i < count; ++i)
            {
                if (assistRangeCells[i] == enemyCell)
                {
                    reachable = true;
                    break;
                }
            }
            if (!reachable)
            {
                reject = $"与敌军隔{distance}格, 超出<{ranged.Name}>的射程";
                return null;
            }

            // 与攻击菜单判断"有没有目标可打"用的是同一套条件, 免得这里放行、真正施法却被拒
            if (!ranged.CanSpellToHere(helper, enemyCell))
            {
                reject = $"<{ranged.Name}>对敌军所在格不满足施放条件";
                return null;
            }

            return ranged;
        }

        /// <summary>
        /// 让支援部队以完全等同于普通攻击的方式出手:
        /// 走 SpellSkill 进入渲染事件队列, 动画/暴击/伤害/敌军还击/EP/气力消耗全部自动生效,
        /// 多支部队依次演出而不是瞬间一起扣血。返回是否真的排上了队列
        /// </summary>
        bool DoAssistAttack(Troop helper, Troop enemy, SkillInstance skill)
        {
            // 与普通攻击同样的释放前置检查(气力不足就不能出手)
            if (!skill.CanBeSpell(helper))
            {
                LogAssist($"[{helper.Name}] 气力不足(需{skill.costEnergy}), 放弃援助");
                return false;
            }

            // SpellSkill 开头遇到"已播完但没清空"的旧事件时, 会只清掉旧事件就 return true,
            // 一个援助事件也不会排, 所以这里必须先把它们清干净
            if (helper.skillRenderEvent != null)
            {
                if (helper.skillRenderEvent.IsDone)
                    helper.skillRenderEvent = null;
                else
                {
                    LogAssist($"[{helper.Name}] 正在施法, 本次援助放弃");
                    return false;
                }
            }

            helper.SpellSkill(skill, enemy.cell, false);

            // SpellSkill 只是把事件排进队列, 伤害要等轮到该事件才结算,
            // 所以援助标记必须排在队之后再挂: 它挂在技能实例上, 结算一次即消耗,
            // 不会像挂在部队上那样残留到该部队自己的下一次攻击
            if (helper.skillRenderEvent == null)
            {
                LogAssist($"[{helper.Name}] 援助未能排入渲染队列, 本次放弃");
                return false;
            }
            skill.assistAttackFlag = true;
            // 这场援助演完就该把这支部队的施法状态让出来, 它自己的行动还没用
            helper.skillRenderEventIsAssist = true;
            // 把事件标成援助: 敌军可能在排队的这几发之间就被打灭了,
            // 那时这场补刀要整场取消(见 TroopSpellSkillEvent.IsAssistCancelled)。
            // 普攻必中也不会进暴击分支, 所以援助排出的只会是 TroopSpellSkillEvent
            if (helper.skillRenderEvent is TroopSpellSkillEvent spellEvent)
                spellEvent.isAssistAttack = true;

            LogAssist($"[{helper.Name}] 已排入援助攻击队列, 用<{skill.Name}>打[{enemy.Name}]");
            return true;
        }

        public void EnterCity(City city)
        {
            City lastBelongCity = mBelongCity;
            city.AddGold(gold);
            city.AddFood(food);
            city.AddTroops(troops);

            // 处理俘虏
            captiveList.ForEach(p =>
            {
                p.mTroop = null;
                city.captiveList.Add(p);
                p.ChangeCurrentCity(city);
            });
            captiveList.Clear();

            // 返还兵装
            city.itemStore.Gain(LandTroopType.costItems, troops + woundedTroops);
            city.itemStore.Gain(WaterTroopType.costItems, troops + woundedTroops);
            city.woundedTroops += woundedTroops;
            city.itemStore.Add(itemStore);
            // 中和士气
            city.morale = (city.morale * city.troops + morale * troops) / (city.troops + troops);
            ForEachPerson((person) =>
            {
                person.ActionOver = true;
            });

            if (LandTroopType.isFight && LandTroopType.Id != 1)
                mBelongCity.allAttackTroops.Remove(this);

            mBelongCity.allTroops.Remove(this);
            city.Render.UpdateRender();

            if (city == mBelongCity)
            {
                Clear();
#if SANGO_DEBUG
                Sango.Log.Info($"{mBelongForce.Name}的[{Name}]部队回到{city.mBelongForce?.Name}的城池:<{city.Name}>");
#endif
                return;
            }

            //missionParams1 == 1 是AI运输的
            if (!TroopType.isFight && missionParams1 <= 0)
            {
                if (mBelongCorps.IsPlayerControl)
                {
                    List<Person> pList = new List<Person>();
                    ForEachPerson((person) =>
                    {
                        pList.Add(person);

                    });

                    PlayerChoice.ChoiceData[] choiceDatas = new PlayerChoice.ChoiceData[]
                    {
                        new PlayerChoice.ChoiceData()
                        {
                           lab = $"返回{mBelongCity.ColorName}",
                           call = () =>
                           {
                               foreach(Person person in pList)
                               {
                                   person.ChangeCurrentCity(city);
                                   person.SetMission(MissionType.PersonReturn, person.mBelongCity);
                               }
                               pList.Clear();
                           }
                        },
                        new PlayerChoice.ChoiceData()
                        {
                           lab = $"将其留在{city.ColorName}",
                           call = () =>
                           {
                               foreach(Person person in pList)
                               {
                                   person.OnWillChangeToCity(city);
                                   person.ChangeBelongCity(city);
                                   person.ChangeCurrentCity(city);
                               }
                               pList.Clear();
                           }
                        }
                    };
                    //GameSystem.GetSystem<PlayerChoice>().Start(choiceDatas);

                    TroopTransformChoiceEvent troopTransformChoiceEvent = new TroopTransformChoiceEvent()
                    {
                        choiceDatas = choiceDatas
                    };
                    RenderEvent.Instance.Add(troopTransformChoiceEvent);

                }
                else
                {
                    // 运输武将返回所属城市
                    ForEachPerson((person) =>
                    {
                        person.ChangeCurrentCity(city);
                        person.SetMission(MissionType.PersonReturn, person.mBelongCity);
                    });
                }
            }
            else
            {
                ForEachPerson((person) =>
                {
                    person.OnWillChangeToCity(city);
                    person.ChangeBelongCity(city);
                    person.ChangeCurrentCity(city);
                });
            }

            Clear();

#if SANGO_DEBUG
            Sango.Log.Info($"{mBelongForce.Name}的[{Name}]部队进入{city.mBelongForce?.Name}的城池:<{city.Name}>");
#endif
        }

        public override void Clear()
        {
            mBelongCity.allTroops.Remove(this);
            Scenario.Cur.Remove(this);

            ReleaseCaptive();
            buildingImproveMap.Clear();
            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                    actionList[i].Clear();

                actionList.Clear();
                actionList = null;
            }

            if (LandTroopType.isFight && LandTroopType.Id != 1)
                mBelongCity.allAttackTroops.Remove(this);


            ForEachPerson((person) =>
            {
                person.mTroop = null;
            });
            base.Clear();
            IsAlive = false;
            missionTarget = 0;
            missionTargetCell = null;
            missionType = 0;
            ActionOver = true;
            Render.Clear();
            if (cell != null && cell.troop == this)
                cell.troop = null;

            for (int i = 0; i < StrategySkills.Count; i++)
                StrategySkills[i].Clear();

            if (landSkills != null)
                for (int i = 0; i < landSkills.Count; i++)
                    landSkills[i].Clear();

            if (waterSkills != null)
                for (int i = 0; i < waterSkills.Count; i++)
                    waterSkills[i].Clear();

            StrategySkills.Clear();
            landSkills?.Clear();
            waterSkills?.Clear();

            GameEvent.OnTroopClear?.Invoke(this, Scenario.Cur);

        }

        public void RemovePerson(Person person, bool justRemove = false)
        {
            if (person == null) return;

            if (Member1 == person)
            {
                Member1.mTroop = null;
                Member1 = null;

                Member1 = Member2;
                Member2 = null;
            }
            else if (Member2 == person)
            {
                Member2.mTroop = null;
                Member2 = null;
            }
            else if (Leader == person)
            {
                Leader.mTroop = null;
                Leader = null;

                if (Member1 != null)
                {
                    Leader = Member1;
                    Member1 = Member2;
                    Member2 = null;
                }
            }

            if (!justRemove)
                CalculateAttribute(Scenario.Cur);
        }

        /// <summary>
        /// 加入某个势力,需要指定一个城市
        /// </summary>
        /// <param name="city"></param>
        public bool JoinToForce(City city)
        {
            if (Leader.IsSameForce(city)) return false;
            ForEachMember(mem =>
            {
                RemovePerson(mem, true);
                mem.SetMission(MissionType.PersonReturn, mem.mBelongCity);
                mem.ActionOver = true;
            });
            Leader.JoinToForce(city);
            ResetActionAndStatus();
            return true;
        }

        public void OnPersonChangeCity(Person person, City old_city, City new_city)
        {

        }


        public void SetMission(MissionType missionType, int missionTarget)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"{mBelongForce.Name}的[{Name} 部队 任务变更:{missionType} -> {missionTarget}!!");
#endif
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget;
            NeedPrepareMission();
        }



        public void ClearMission()
        {
            this.missionType = 0;
            this.missionTarget = 0;
            this.missionTargetCell = null;
            this.missionParams1 = 0;
            this.missionParams2 = 0;
        }

        TroopMissionBehaviour troopMissionBehaviour;
        public TroopMissionBehaviour TroopMissionBehaviour
        {
            get
            {
                if (missionType == 0 && mBelongCity != null && !IsPlayerControl)
                {
                    SetMission(MissionType.TroopReturnCity, mBelongCity.Id);
                    NeedPrepareMission();
                }

                if (missionType == 0)
                {
                    return null;
                }

                if (this.troopMissionBehaviour == null || (int)troopMissionBehaviour.MissionType != missionType)
                {
                    troopMissionBehaviour = TroopMissionBehaviour.Create(missionType);
                    isMissionPrepared = false;
                    troopMissionBehaviour.Prepare(this, Scenario.Cur);
                    isMissionPrepared = true;
                }
                return troopMissionBehaviour;
            }
        }

        public void NeedPrepareMission()
        {
            isMissionPrepared = false;
        }

        public override bool DoAI(Scenario scenario)
        {
            if (AIFinished)
                return true;

            if (!AIPrepared)
            {
                AIPrepare(scenario);
                AIPrepared = true;
                GameEvent.OnTroopAIStart?.Invoke(this, scenario);
            }

            TroopMissionBehaviour temp = TroopMissionBehaviour;
            if (temp == null)
            {
                GameEvent.OnTroopAIEnd?.Invoke(this, scenario);
                AIFinished = true;
                ActionOver = true;
                return true;
            }

            if (!isMissionPrepared)
            {
                temp.Prepare(this, scenario);
                isMissionPrepared = true;
            }
#if SANGO_DEBUG_AI
            if (GameAIDebug.Instance.WaitForShowAIPrepare())
                return false;
#endif
            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append("1,");
            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append($"12:{temp.GetType()},");
            if (!temp.DoAI(this, scenario))
                return false;
            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append("13,");
            GameEvent.OnTroopAIEnd?.Invoke(this, scenario);
            AIFinished = true;
            troopMissionBehaviour = null;
            ActionOver = true;
            return true;
        }

        public void AIPrepare(Scenario scenario)
        {
            // 永不退缩
            //if ((morale <= 5 && GameRandom.Changce(60)) ||
            //    (troops < 500 && GameRandom.Changce(80)) ||
            //    food < (int)System.System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (troops + woundedTroops) * TroopType.foodCostFactor) * 3 && GameRandom.Changce(80)
            //    )
            //{
            //    missionType = (int)MissionType.ReturnCity;
            //    missionTarget = BelongCity.Id;
            //}
        }

        public void Burn(Cell dest)
        {

        }

        public int GetItemNumber(int itemKind)
        {
            if (IsTransport)
                return itemStore.GetNumber(itemKind);
            else
            {
                int number = troops + woundedTroops;
                if (LandTroopType.costItems != null && LandTroopType.costItems.Length > 0)
                {
                    for (int i = 0; i < LandTroopType.costItems.Length; i += 2)
                    {
                        int itemTypeId = LandTroopType.costItems[i];
                        if (itemKind == itemTypeId)
                            return LandTroopType.costItems[i + 1] * number / 1000;
                    }
                }

                if (WaterTroopType.costItems != null && WaterTroopType.costItems.Length > 0)
                {
                    for (int i = 0; i < WaterTroopType.costItems.Length; i += 2)
                    {
                        int itemTypeId = WaterTroopType.costItems[i];
                        if (itemKind == itemTypeId)
                            return WaterTroopType.costItems[i + 1] * number / 1000;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// 添加囚犯
        /// </summary>
        /// <param name="person">要添加的武将</param>
        /// <returns>添加的武将</returns>
        public Person AddCaptive(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> captiveList 添加 {person.Name} ");
#endif
            person.OnWillBeCaptive();
            person.ClearMission();
            person.state = (int)PersonStateType.Prisoner;
            captiveList.Add(person);
            person.mBelongForce?.BeCaptiveList.Remove(person);
            person.mBelongForce?.BeCaptiveList.Add(person);
            person.mTroop = this;
            person.ChangeCurrentCity(this.mCurrentCity);
            if (person.mBelongCity != null)
            {
                person.mBelongCity.allPersons.Remove(person);
                person.mBelongCity.wildPersons.Remove(person);
                person.mBelongCity.freePersons.Remove(person);
                person.mBelongCity = null;
            }

#if SANGO_DEBUG
            Sango.Log.Info($"@人才@[{person.Name}]被<{mBelongForce.Name}>俘虏至{Name}");
#endif
            return person;
        }

        /// <summary>
        /// 添加囚犯
        /// </summary>
        /// <param name="person">要添加的武将</param>
        /// <returns>添加的武将</returns>
        public Person RemoveCaptive(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> captiveList 删除 {person.Name} ");
#endif
            captiveList.Remove(person);
            person.mBelongForce?.BeCaptiveList.Remove(person);
            person.mTroop = null;
            return person;
        }

        /// <summary>
        /// 结算部队本次战斗获得的功绩与技巧点。
        /// 计算本次战斗获得的技巧点，并开放给部队特技 Action 改写。
        /// 功绩与经验保持原有口径，不受技巧点特技影响。
        /// </summary>
        /// <param name="gp">本次战斗获得的原始功绩。</param>
        /// <param name="isDestroyEnemyTroop">是否由本部队击破敌方部队触发。</param>
        public void GainEP(int gp, bool isDestroyEnemyTroop = false)
        {
            Tools.OverrideData<int> techniquePoint = Tools.OverrideData<int>.Create(gp / 5);
            // 由已装配的部队特技决定是否改写本次技巧点，避免按特技 ID 硬编码。
            GameEvent.OnTroopCalculateTechniquePoint?.Invoke(this, isDestroyEnemyTroop, techniquePoint);
            mBelongForce.GainTechniquePoint(techniquePoint.ValueAndRecycle);

            // 主将获得100%功绩,
            if (Leader != null)
            {
                Leader?.GainMerit(gp);
                Leader?.GainExp(gp / 5);
            }
            int memberGp = gp * 6 / 10;
            ForEachMember(x =>
            {
                x.GainMerit(memberGp);
                x.GainExp(memberGp / 5);
            });
        }

        public void GainTargetResource(Troop target)
        {
            // 获取对方部分钱粮
            int getFood = target.food * Math.Max(0, Math.Min(100, defeatTroopCanGainFoodFactor)) / 100;
            int getGold = target.gold * Math.Max(0, Math.Min(100, defeatTroopCanGainGoldFactor)) / 100;
            if (getFood > 0)
            {
                ChangeFood(getFood);
            }
            if (getGold > 0)
            {
                ChangeGold(getGold);
            }
        }
    }
}
