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
            get => _actionOver;
            set
            {
                if (_actionOver != value)
                {
                    _actionOver = value;
                    GameEvent.OnTroopActionOver?.Invoke(this);
                }
            }
        }
        private bool _actionOver;
        public System.Action overAction;
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
        public int BuildPower { get; private set; }

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

        public override void Init(Scenario scenario)
        {
            _troopName = $"{Leader?.Name}队";
            actionList = new List<ActionBase>();
            ForEachPerson(x =>
            {
                x.mTroop = this;
                if (x.mFeatureList != null)
                {
                    for (int i = 0; i < x.mFeatureList.Count; i++)
                    {
                        x.mFeatureList[i].InitActions(actionList, this, x);
                    }
                }
            });

            StrategySkills.Clear();
            scenario.CommonData.Skills.ForEach(x =>
            {
                if (x.IsStrategy())
                    StrategySkills.Add(SkillInstance.Create(this, x));
            });

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
            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                    actionList[i].Clear();

                actionList.Clear();
            }

            ForEachPerson(x =>
            {
                x.mTroop = this;
                if (x.mFeatureList != null)
                {
                    for (int i = 0; i < x.mFeatureList.Count; i++)
                    {
                        x.mFeatureList[i].InitActions(actionList, this, x);
                    }
                }
            });

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
            isMissionPrepared = false;
            skillRenderEvent = null;
            actionRenderEvent = null;
            moveRenderEvent = null;
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

            if (WaterTroopType == null)
                WaterTroopType = scenario.GetObject<TroopType>(8);

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
                        SkillInstance ins = null;
                        if (skills != null)
                            ins = skills.Find(x => x.skill == skill);
                        if (ins == null)
                            ins = SkillInstance.Create(this, skill);
                        ins.master = this;
                        skillInstances.Add(ins);
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
                        SkillInstance ins = null;
                        if (skills != null)
                            ins = skills.Find(x => x.skill == skill);
                        if (ins == null)
                            ins = SkillInstance.Create(this, skill);

                        skillInstances.Add(ins);
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
        /// <returns>是否成功发起单挑</returns>
        public bool StartDuel(Troop targetTroop)
        {
            if (targetTroop == null || !IsEnemy(targetTroop))
            {
                return false;
            }

            return DuelManager.Instance.StartDuel(this, targetTroop);
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
                    bool isLast = i == tempCellList.Count - 1;
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

        public bool ChangeTroops(int num, SangoObject atk, SkillInstance skill, int atkBack)
        {
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(num);
            GameEvent.OnTroopChangeTroops?.Invoke(this, atk, skill, atkBack, overrideData);
            num = overrideData.ValueAndRecycle;

            if (num == 0)
                return IsAlive;

            if (Render != null)
            {
                bool isCrit = false;
                if (skill != null)
                {
                    isCrit = skill.IsCritical();
                }
                Render.ShowInfo(num, (int)InfoType.Troop, isCrit);
            }
            troops = troops + num;
            if (num < 0)
            {
                if (Render != null && Render.IsVisible())
                {
                    GameMedia.Instance.PlayPersonSay(Leader, GameRandom.Chance(50) ? 3216 : 3230);
                }

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
            }

            if (troops > MaxTroops)
                troops = MaxTroops;

            IsAlive = troops > 0;
            if (!IsAlive)
            {
#if SANGO_DEBUG
                Sango.Log.Info($"{mBelongForce.Name}的[{Name} 部队 溃灭!!");
#endif

                if (Render != null && Render.IsVisible())
                {
                    GameMedia.Instance.PlaySfx(83);
                }

                OnDestroy(atk, skill, atkBack);

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

        public void OnDestroy(SangoObject atk, SkillInstance skill, int atkBack)
        {
            // 添加俘虏流程
            if (atk.ObjectType == SangoObjectType.Troops)
            {
                Troop atkTroop = (Troop)atk;

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
        }

        public void ReleaseCaptive()
        {
            for (int i = 0; i < captiveList.Count; i++)
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

        public bool SpellSkill(SkillInstance skill, Cell spellCell)
        {

            if (skillRenderEvent != null)
            {
                if (skillRenderEvent.IsDone)
                {
                    skillRenderEvent = null;
                    return true;
                }
                else
                    return false;
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
                        if (cell.IsEmpty())
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
            }
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
                city.captiveList.Add(p);
                p.ChangeCurrentCity(city);
                p.mTroop = null;
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

            mBelongCity.allTroops.Remove(this);
            Scenario.Cur.Remove(this);
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
                if (missionType == 0 && mBelongCity != null)
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

            if (!TroopMissionBehaviour.DoAI(this, scenario))
                return false;

            GameEvent.OnTroopAIEnd?.Invoke(this, scenario);
            AIFinished = true;
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
    }
}
