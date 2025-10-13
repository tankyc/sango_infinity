using Newtonsoft.Json;
using Sango.Game.Render;
using Sango.Tools;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using static UnityEngine.GraphicsBuffer;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Troop : SangoObject
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Troops; } }
        public virtual bool AIFinished { get; set; }
        public virtual bool AIPrepared { get; set; }
        /// <summary>
        /// 所属势力
        /// </summary>
        public Force BelongForce => Leader.BelongForce;

        /// <summary>
        /// 所属势力
        /// </summary>
        public Corps BelongCorps => Leader.BelongCorps;

        /// <summary>
        /// 所属城池
        /// </summary>
        public City BelongCity => Leader.BelongCity;

        /// <summary>
        /// 统领
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
        /// 部队类型
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<TroopType>))]
        [JsonProperty]
        public TroopType TroopType { get; set; }

        /// <summary>
        /// 兵种适应力
        /// </summary>
        public int TroopTypeLv { get; private set; }

        /// <summary>
        /// 俘虏
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Person>))]
        [JsonProperty]
        public SangoObjectList<Person> captiveList = new SangoObjectList<Person>();

        /// <summary>
        /// 部队名
        /// </summary>
        public override string Name => Leader.Name;

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

        public int MaxTroops => Leader.TroopsLimit;
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
        /// 移动能力
        /// </summary>
        public int MoveAbility => TroopType.move;

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
        [JsonProperty] public ItemStore itemStore = new ItemStore();

        /// <summary>
        /// 是否行动完毕
        /// </summary>
        [JsonProperty] public override bool ActionOver { get; set; }

        /// <summary>
        /// 当前任务类型
        /// </summary>
        [JsonProperty] public int missionType;

        /// <summary>
        /// 任务目标
        /// </summary>
        [JsonProperty] public int missionTarget;

        /// <summary>
        /// 当前技能
        /// </summary>
        [JsonProperty]
        public List<SkillInstance> skills;

        /// <summary>
        /// 伤害额外增减
        /// </summary>
        public float DamageTroopExtraFactor { get; private set; }
        public float DamageBuildingExtraFactor { get; private set; }

        public int SpearLv { get; private set; }
        public int HalberdLv { get; private set; }
        public int CrossbowLv { get; private set; }
        public int HorseLv { get; private set; }
        public int WaterLv { get; private set; }
        public int MachineLv { get; private set; }

        /// <summary>
        /// 攻击力
        /// </summary>
        public int Attack { get;  set; }

        /// <summary>
        /// 防御力
        /// </summary>
        public int Defence { get;  set; }

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


        public TroopRender Render { get; private set; }
        bool isMissionPrepared = false;
        public int foodCost = 0;

        public override void Init(Scenario scenario)
        {
            CalculateAttribute(scenario);
            Render = new TroopRender(this);
            foodCost = (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (troops + woundedTroops) * TroopType.foodCostFactor);
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
            foreach (Person person in captiveList)
            {
                if (person.BelongForce != null)
                    person.BelongForce.CaptiveList.Add(person);
            }
            foodCost = (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (troops + woundedTroops) * TroopType.foodCostFactor);
            //MemberList?.InitCache();// = new SangoObjectList<Person>().FromString(_memberListStr, scenario.personSet);
        }
        public override bool OnTurnStart(Scenario scenario)
        {
            ActionOver = false;
            AIFinished = false;
            AIPrepared = false;
            isMissionPrepared = false;
            if (food <= 0)
            {
                // 伤兵直接抛弃
                woundedTroops = 0;
                // 减少士气
                morale = (int)Math.Ceiling(morale * 0.5f);
                if (morale < 0)
                    morale = 0;
                if (troops < 500)
                {
                    Clear();
                }
                else
                {
                    // 每回合死亡当前兵力的30%;
                    int damage = (int)Math.Ceiling(troops * 0.3f);
                    troops -= damage;
                }
            }
            else
            {
                foodCost = (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (troops + woundedTroops) * TroopType.foodCostFactor);
                ChangeFood(-foodCost);
            }

            if (Render != null)
            {
                Render.UpdateRender();
            }

            return true;
        }

        public int IsWithOutFood()
        {
            if (food == 0) return 0;
            if (food < foodCost * 3) return 1;
            return 2;
        }

        public void ForEachMember(Action<Person> action)
        {
            if (Member1 != null) action(Member1);
            if (Member2 != null) action(Member2);
        }

        public void ForEachPerson(Action<Person> action)
        {
            action(Leader);
            if (Member1 != null) action(Member1);
            if (Member2 != null) action(Member2);
        }

        /// <summary>
        /// 计算属性
        /// </summary>
        public void CalculateAttribute(Scenario scenario)
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;

            // 计算能力,能力取最大
            ForEachPerson((p) =>
            {
                Command = Math.Max(Command, p.Command);
                Strength = Math.Max(Strength, p.Strength);
                Intelligence = Math.Max(Intelligence, p.Intelligence);
                Politics = Math.Max(Politics, p.Politics);
                Glamour = Math.Max(Glamour, p.Glamour);
                TroopTypeLv = Math.Max(TroopTypeLv, CheckTroopTypeLevel(TroopType, p));
            });

            List<SkillInstance> skillInstances = new List<SkillInstance>();
            // 准备技能
            for (int i = 0; i < TroopType.skills.Count; i++)
            {
                Skill skill = Scenario.Cur.GetObject<Skill>(TroopType.skills[i]);
                if (skill != null && skill.CanAddToTroop(this))
                {

                    SkillInstance ins = null;
                    if(skills != null)
                        ins = skills.Find(x => x.Skill == skill);
                    if (ins != null)
                        skillInstances.Add(ins);
                    else
                        skillInstances.Add(new SkillInstance() { Skill = skill, CDCount = 0 });
                }
            }
            skills = skillInstances;

            // 防御力 = (70%统率+30%智力) * 兵种防御力 / 100 * 适应力加成(A为1)
            Defence = TroopsLevelBoost((
                Command * Variables.fight_troop_defence_command_factor
                + Strength * Variables.fight_troop_defence_strength_factor
                + Intelligence * Variables.fight_troop_defence_intelligence_factor 
                + Politics * Variables.fight_troop_defence_intelligence_factor 
                + Glamour * Variables.fight_troop_defence_intelligence_factor
                ) / 10000 * TroopType.def) / 100;

            // 攻击力 = (70%武力+30%统率) * 兵种攻击力 / 100 * 适应力加成(A为1)
            Attack = TroopsLevelBoost((
                 Command * Variables.fight_troop_attack_command_factor
                + Strength * Variables.fight_troop_attack_strength_factor
                + Intelligence * Variables.fight_troop_attack_intelligence_factor
                + Politics * Variables.fight_troop_attack_politics_factor
                + Glamour * Variables.fight_troop_attack_glamour_factor
                ) / 10000 * TroopType.atk) / 100;

            // 建设能力 = 政治 * 67% + 50;
            BuildPower = Politics * 6700 / 10000 + 50;

            // 事件可二次修改属性
            scenario.Event.OnTroopCalculateAttribute?.Invoke(this, scenario);

        }

        public int MoveCost(Cell cell)
        {
            return TroopType.MoveCost(cell);
        }
        public bool IsAlliance(BuildingBase other)
        {
            return IsAlliance(BelongForce, other.BelongForce);
        }

        public bool IsEnemy(BuildingBase other)
        {
            return IsEnemy(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(BuildingBase other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        public bool IsAlliance(Troop other)
        {
            return IsAlliance(BelongForce, other.BelongForce);
        }

        public bool IsEnemy(Troop other)
        {
            return IsEnemy(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(Troop other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        public float GetAttackBackFactor(Skill skill, int distance)
        {
            if (skill.IsRange() && distance > 1)
                return 0;
            else if (!skill.IsRange() && distance == 1)
                return 0.9f;
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
        public static int CalculateSkillDamage(Troop attacker, Troop target, Skill skill)
        {
            var attack_troops_type = attacker.TroopType;
            var defender_troops_type = target.TroopType;

            ScenarioVariables Variables = Scenario.Cur.Variables;

            float difficultyDamageFactor = 1;
            if (attacker.BelongForce != null && attacker.BelongForce.IsPlayer)
                difficultyDamageFactor = Variables.DifficultyDamageFactor;

            float crit_P = 1;
            if (CalculateSkillCriticalBoost(attacker, target, skill, out crit_P))
            {

            }
            int atkBounds = skill != null ? skill.atk : 10;
            /*
             *公式来源参考:
             *https://game.ali213.net/thread-5983352-1-1.html  freedomv20的[数据研究] <三国志11 战斗伤害计算公式>
             *https://www.bilibili.com/opus/828102349572538433 ryan_knight_12吧 楚狂的 <三国志11伤害到底是怎样算的?>
             *https://tieba.baidu.com/p/6061024246?pn=1 不懂秃驴爱的 <三国志11：部队的兵力与攻击力数据实测，究竟带多少兵才是最优解>
             */

            int damage = (int)(
                (
                
                (Math.Pow(atkBounds * Variables.fight_base_damage, 0.5) +  Math.Max(0, (int)((Math.Pow(attacker.Attack, 2) -  Math.Pow(Math.Max(40, target.Defence), 2)) / 300)) +
                Math.Max(0, (attacker.troops - target.troops) / Variables.fight_base_troops_need) + 50)
                
                * 10 * ((int)(
                
                (((int)(attacker.troops * 0.01) + 300) * Math.Pow((attacker.Attack + 50), 2)) / 
                (((int)(attacker.troops * 0.01) + 300) * Math.Pow((attacker.Attack + 50), 2) * 0.01 +
                ((int)(target.troops * 0.01) + 300) * Math.Pow((target.Defence + 50), 2) * 0.01)
                
                - 50)
                
                + 50)
                // 原有基础上优化Math.Max(1, attacker.troops / 4),1兵打出15伤害同于实际测试
                * Math.Min(Math.Pow(Math.Max(1, attacker.troops / 4), 0.5), 40) 
                
                * Variables.fight_damage_magic_number /* * 太鼓台系数*/

                + attacker.troops / Variables.fight_base_troop_count

                )
                //兵种相克系数
                * CalculateRestrainBoost(attacker, target)
                //会心系数
                * crit_P
                // 额外增益 (科技系数等)
                * Math.Max(0, (1 + attacker.DamageTroopExtraFactor))

                // 难度系数,仅对玩家生效
                * difficultyDamageFactor
                );


            ////基础伤害
            //float base_dmg = Variables.fight_base_damage * (attack_troops_type.atk - defender_troops_type.def);

            ////兵力加成系数
            //var troops_add = (attacker.troops - Variables.fight_base_troops_need) / Variables.fight_base_troop_count * Variables.fight_base_troop_factor_per_count;

            ////基础减伤
            //var base_reduce = Variables.fight_base_reduce_percent * target.Command / 100f;

            //var damage = base_dmg * (1 + troops_add) * (1 - base_reduce);

            ////士气矫正后的伤害
            //damage = damage * (UnityEngine.Mathf.Max(attacker.morale - Variables.fight_morale_decay_below, 0) / (100 - Variables.fight_morale_decay_below) *
            //Variables.fight_morale_add + (1 - Variables.fight_morale_decay_percent) + UnityEngine.Mathf.Min(UnityEngine.Mathf.Max(attacker.morale, 0), Variables.fight_morale_decay_below) / Variables.fight_morale_decay_below * Variables.fight_morale_decay_percent);

            return damage;
        }

        public static int CalculateSkillDamage(Troop attacker, BuildingBase target, Skill skill)
        {
            var attack_troops_type = attacker.TroopType;
            var buildingType = target.BuildingType;
            ScenarioVariables Variables = Scenario.Cur.Variables;

            float difficultyDamageFactor = 1;
            if (attacker.BelongForce != null && attacker.BelongForce.IsPlayer)
                difficultyDamageFactor = Variables.DifficultyDamageFactor;

            float crit_P = 1;
            if (CalculateSkillCriticalBoost(attacker, target, skill, out crit_P))
            {

            }

            int damage = (int)(Math.Pow(attacker.troops, 0.5f) * attacker.Attack * Math.Pow((1f / 1500f), 0.5f) * (1 + (float)skill.atkDurability / 25f) * buildingType.damageBounds
                // 会心 
                * crit_P
                // 额外增益 (科技系数等)
                * Math.Max(0, (1 + attacker.DamageBuildingExtraFactor))
                * attack_troops_type.durabilityDmg / 100
                // 难度系数,仅对玩家生效
                * difficultyDamageFactor
                );

            return damage;
        }

        public static int CalculateSkillDamage(BuildingBase attacker, Troop target, Skill skill)
        {

            ScenarioVariables Variables = Scenario.Cur.Variables;

            //基础伤害
            float base_atk = attacker.GetAttack();
            int base_troops = attacker.GetSkillMethodAvaliabledTroops();

            float difficultyDamageFactor = 1;
            if (attacker.BelongForce != null && attacker.BelongForce.IsPlayer)
                difficultyDamageFactor = Variables.DifficultyDamageFactor;

            int damage = (int)(
                 (

                 (Math.Max(0, (int)((Math.Pow(base_atk, 2) - Math.Pow(Math.Max(40, target.Defence), 2)) / 300)) +
                 Math.Max(0, (base_troops - target.troops) / Variables.fight_base_troops_need) + 50)

                 * 10 * ((int)(

                 (((int)(base_troops * 0.01) + 300) * Math.Pow((base_atk + 50), 2)) /
                 (((int)(base_troops * 0.01) + 300) * Math.Pow((base_atk + 50), 2) * 0.01 +
                 ((int)(target.troops * 0.01) + 300) * Math.Pow((target.Defence + 50), 2) * 0.01)

                 - 50)

                 + 50)

                 * Math.Min(Math.Pow(Math.Max(1, base_troops / 4), 0.5), 40) 
                 
                 * Variables.fight_damage_magic_number /* * 太鼓台系数*/

                 + base_troops / Variables.fight_base_troop_count

                 )

                // 难度系数,仅对玩家生效
                * difficultyDamageFactor

                 // 额外增益 (科技系数等)
                 //* Math.Max(0, (1 + attacker.DamageTroopExtraFactor))
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

            if (attack_troops_type.Id < 0 || attack_troops_type.Id > Variables.troops_type_restraint.Length)
                return 1;
            float[] t_map = Variables.troops_type_restraint[attack_troops_type.Id];

            var defender_troops_type = target.TroopType;
            if (defender_troops_type.Id < 0 || defender_troops_type.Id > t_map.Length)
                return 1;
            return t_map[defender_troops_type.Id];
        }

        public static int CheckTroopTypeLevel(TroopType troopType, Person person)
        {
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
                case (int)AbilityType.Horse:
                    return person.HorseLv;
                case (int)AbilityType.Machine:
                    return person.MachineLv;
            }
            return 0;
        }


        // 克制系数
        //@param attacker Troops
        public int TroopsLevelBoost(int value)
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;
            if (TroopTypeLv < 0 || TroopTypeLv > Variables.troops_adaptation_level_boost.Length)
                return value;

            return value * Variables.troops_adaptation_level_boost[TroopTypeLv] / 100;
        }

        internal static List<Cell> tempCellList = new List<Cell>(256);
        internal static List<Cell> tempMoveRange = new List<Cell>(256);
        internal static List<TroopMoveEvent> tempMoveEventList = new List<TroopMoveEvent>(32);
        internal static List<Cell> spellRangeCells = new List<Cell>(256);
        internal bool isMoving = false;
        IRenderEventBase moveRenderEvent = null;
        IRenderEventBase skillRenderEvent = null;

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
                    TroopMoveEvent @event = new TroopMoveEvent()
                    {
                        troop = this,
                        dest = dest,
                        start = start,
                        isLastMove = isLast
                    };

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

            //if (tempMoveEventList.Count == 0)
            //{
            //    Sango.Log.Print($"{BelongForce.Name}的[{Name} 部队 移动到=> ({x},{y})]");
            //    isMoving = false;
            //    return true;
            //}

            //if (!Render.IsVisible())
            //{
            //    while (tempMoveEventList.Count > 0)
            //    {
            //        TroopMoveEvent @event = tempMoveEventList[0];
            //        UpdateCell(@event.dest, @event.start, tempMoveEventList.Count == 1);
            //        tempMoveEventList.RemoveAt(0);
            //    }
            //    isMoving = false;
            //    return true;
            //}
            //else
            //{
            //    TroopMoveEvent @event = tempMoveEventList[0];
            //    if (@event.Update(UnityEngine.Time.deltaTime))
            //    {
            //        UpdateCell(@event.dest, @event.start, tempMoveEventList.Count == 1);
            //        tempMoveEventList.RemoveAt(0);
            //    }
            //    return false;
            //}
        }

        public bool TryMoveToSpell(Cell destCell, Skill skill)
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

                    RenderEvent.Instance.Add(new TroopMoveEvent()
                    {
                        troop = this,
                        dest = dest,
                        start = start,
                        isLastMove = i == tempCellList.Count - 1
                    });
                    start = dest;

                    if (findSpell)
                    {
                        break;
                    }
                }
            }

            //if (tempMoveEventList.Count == 0)
            //{
            //    Sango.Log.Print($"{BelongForce.Name}的[{Name} 部队 移动到=> ({x},{y})]");
            //    isMoving = false;
            //    return true;
            //}

            //if (!Render.IsVisible())
            //{
            //    while (tempMoveEventList.Count > 0)
            //    {
            //        TroopMoveEvent @event = tempMoveEventList[0];
            //        UpdateCell(@event.dest, @event.start, tempMoveEventList.Count == 1);
            //        tempMoveEventList.RemoveAt(0);
            //    }
            //    return false;
            //}
            //else
            //{
            //    TroopMoveEvent @event = tempMoveEventList[0];
            //    if (@event.Update(UnityEngine.Time.deltaTime))
            //    {
            //        UpdateCell(@event.dest, @event.start, tempMoveEventList.Count == 1);
            //        tempMoveEventList.RemoveAt(0);
            //    }
            //    return false;
            //}

            return false;

        }

        //public bool MoveToClosest(Cell destCell)
        //{
        //    if (!isMoving)
        //    {
        //        tempCellList.Clear();
        //        tempMoveEventList.Clear();
        //        //TODO: 移动
        //        Scenario.Cur.Map.GetClosestMovePath(this, destCell, tempCellList);
        //        isMoving = true;
        //        Cell start = cell;
        //        for (int i = 1; i < tempCellList.Count; i++)
        //        {
        //            Cell dest = tempCellList[i];
        //            tempMoveEventList.Add(new TroopMoveEvent()
        //            {
        //                troop = this,
        //                dest = dest,
        //                start = start,
        //                isLastMove = i == tempCellList.Count - 1
        //            });
        //            start = dest;
        //        }
        //    }

        //    if (tempMoveEventList.Count == 0)
        //    {
        //        Sango.Log.Print($"{BelongForce.Name}的[{Name} 部队 移动到=> ({x},{y})]");
        //        isMoving = false;
        //        return true;
        //    }
        //    TroopMoveEvent @event = tempMoveEventList[0];
        //    if (!@event.IsVisible() || @event.Update(UnityEngine.Time.deltaTime))
        //    {
        //        UpdateCell(@event.dest, @event.start, tempMoveEventList.Count == 1);
        //        tempMoveEventList.RemoveAt(0);
        //    }

        //    return false;
        //}


        public bool ChangeFood(int num)
        {
            food += num;
            if (food < 0)
            {
                food = 0;
                return true;
            }
            return false;
        }

        public bool ChangeTroops(int num, SangoObject atk)
        {
            if (Render != null)
                Render.ShowDamage(num, 2);

            troops = troops + num;
            if (num < 0)
            {
                int absNum = Math.Abs(num);
                woundedTroops += (int)Math.Ceiling(absNum * 0.14f);
                int _foodCost = (int)System.Math.Ceiling(Scenario.Cur.Variables.baseFoodCostInTroop * absNum * TroopType.foodCostFactor) / 2;
                int divFood = 0;
                // 有概率保留部分
                if (GameRandom.Changce(80))
                    divFood += _foodCost;
                if (GameRandom.Changce(50))
                    divFood += _foodCost;
                ChangeFood(-divFood);
            }

            IsAlive = troops > 0;
            if (!IsAlive)
            {
                // 移除
                Clear();
                Sango.Log.Print($"{BelongForce.Name}的[{Name} 部队 溃灭!!");
            }

            if (Render != null)
            {
                Render.UpdateRender();
            }

            return IsAlive;
        }

        public int GetTroopsNum()
        {
            return troops;
        }

        public bool SpellSkill(Skill skill, Cell spellCell)
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

            TroopSpellSkillEvent @event = new TroopSpellSkillEvent()
            {
                troop = this,
                skill = skill,
                spellCell = spellCell,
            };
            skillRenderEvent = @event;
            RenderEvent.Instance.Add(@event);

            return false;
        }
        Cell tryToDest;
        public bool TryMoveTo(Cell destCell)
        {
            if (!isMoving)
            {      //TODO: 尝试移动
                tempCellList.Clear();
                tryToDest = null;
                //TODO: 移动
                Scenario.Cur.Map.GetDirectMovePath(this, destCell, tempCellList);
                int totaleMoveAbility = MoveAbility;
                int checkIndex = 0;
                for (int i = 1; i < tempCellList.Count; i++)
                {
                    Cell dest = tempCellList[i];
                    int destCost = MoveCost(dest);
                    if (totaleMoveAbility > destCost && !Scenario.Cur.Map.IsZOC(this, dest))
                    {
                        totaleMoveAbility -= destCost;
                    }
                    else
                    {
                        checkIndex = i;
                        break;
                    }
                }

                for (int i = checkIndex - 1; i >= 1; i--)
                {
                    Cell dest = tempCellList[i];
                    if (dest.IsEmpty())
                    {
                        tryToDest = dest;
                        break;
                    }
                }
            }

            if (tryToDest == null)
                return true;

            return MoveTo(tryToDest);
        }
        public bool TryCloseTo(Cell destCell)
        {
            if (!isMoving)
            {      //TODO: 尝试移动
                tempCellList.Clear();
                tryToDest = null;

                Map map = Scenario.Cur.Map;
                //TODO: 移动
                map.GetDirectMovePath(this, destCell, tempCellList);
#if SANGO_DEBUG_AI
                GameAIDebug.Instance.ShowTargetDirectPath(tempCellList, this);
#endif

                int totaleMoveAbility = MoveAbility;
                for (int i = 1; i < tempCellList.Count; i++)
                {
                    Cell dest = tempCellList[i];
                    int destCost = MoveCost(dest);
                    if (totaleMoveAbility > destCost)
                    {
                        if (map.IsZOC(this, dest))
                        {
                            totaleMoveAbility = 0;
                        }
                        else
                        {
                            totaleMoveAbility -= destCost;
                        }
                        tryToDest = dest;
                    }
                    else
                    {
                        break;
                    }
                }

                if (tryToDest != null)
                {
                    List<Cell> temp = new List<Cell>();
                    map.GetMoveRange(this, temp);
                    PriorityQueue<Cell> nearnestCellInMoveRange = new PriorityQueue<Cell>();
                    for (int i = 0; i < temp.Count; i++)
                    {
                        Cell cell = temp[i];
                        if (cell.IsEmpty())
                        {
                            nearnestCellInMoveRange.Push(cell, map.Distance(cell, tryToDest));
                        }
                    }
                    tryToDest = nearnestCellInMoveRange.Lower();
                }
            }

            if (tryToDest == null)
                return true;

#if SANGO_DEBUG_AI

            if (GameAIDebug.Instance.WaitForTargetDirectPath())
                return false;
#endif


            return MoveTo(tryToDest);
        }
        public bool TryMoveToCity(City city)
        {
            if (!isMoving)
            {      //TODO: 尝试移动
                tempCellList.Clear();
                tryToDest = null;

                tempMoveRange.Clear();

                // 先检查移动范围内是否可达目标
                Map map = Scenario.Cur.Map;
                map.GetMoveRange(this, tempMoveRange);
                for (int i = 1; i < tempMoveRange.Count; ++i)
                {
                    Cell cell = tempMoveRange[i];
                    if (cell.building == city)
                    {
                        tryToDest = cell;
                        break;
                    }
                }

                if (tryToDest == null)
                {
                    //TODO: 移动
                    map.GetDirectMovePath(this, city.CenterCell, tempCellList);

                    int totaleMoveAbility = MoveAbility;
                    for (int i = 1; i < tempCellList.Count; i++)
                    {
                        Cell dest = tempCellList[i];
                        int destCost = MoveCost(dest);
                        if (totaleMoveAbility > destCost)
                        {
                            if (map.IsZOC(this, dest))
                            {
                                totaleMoveAbility = 0;
                            }
                            else
                            {
                                totaleMoveAbility -= destCost;
                            }
                            tryToDest = dest;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (tryToDest != null)
                    {
                        //List<Cell> temp = new List<Cell>();
                        //map.GetMoveRange(this, temp);
                        PriorityQueue<Cell> nearnestCellInMoveRange = new PriorityQueue<Cell>();
                        for (int i = 0; i < tempMoveRange.Count; i++)
                        {
                            Cell cell = tempMoveRange[i];
                            if (cell.IsEmpty())
                            {
                                nearnestCellInMoveRange.Push(cell, map.Distance(cell, tryToDest));
                            }
                        }
                        tryToDest = nearnestCellInMoveRange.Lower();
                    }
                }
            }

            if (tryToDest == null)
            {
                return true;
            }

            return MoveTo(tryToDest);
        }

        public void UpdateCell(Cell destCell, Cell lastCell, bool isEndMove)
        {
            //TODO: 地格更新,需要处理一些事件
            if (lastCell != null)
                ScenarioEvent.Event.OnTroopLeaveCell?.Invoke(lastCell, destCell);

            ScenarioEvent.Event.OnTroopEnterCell?.Invoke(cell, lastCell);
#if SANGO_DEBUG
            Sango.Log.Print($"{BelongForce.Name}的[{Name} 部队 移动=> ({destCell.x},{destCell.y})]");
#endif
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
            cell.troop = null;
            city.gold += gold;
            city.food += food;
            city.troops += troops;
            if (city.troops > city.CityLevelType.maxTroops)
                city.troops = city.CityLevelType.maxTroops;

            city.woundedTroops += woundedTroops;
            // 设置了,
            IsAlive = false;

            Scenario.Cur.Remove(this);
            ForEachPerson((person) =>
            {
                person.BelongTroop = null;
                person.ActionOver = true;
            });

            Render.Clear();
            ActionOver = true;
            if (city == BelongCity)
            {
                Sango.Log.Print($"{BelongForce.Name}的[{Name}]部队回到{city.BelongForce?.Name}的城池:<{city.Name}>");
                return;
            }
            ForEachPerson((person) =>
            {
                person.ChangeCity(city);
            });

            Sango.Log.Print($"{BelongForce.Name}的[{Name}]部队进入{city.BelongForce?.Name}的城池:<{city.Name}>");
        }

        public override void Clear()
        {
            Scenario.Cur.Remove(this);
            ForEachPerson((person) =>
            {
                person.BelongTroop = null;
            });
            base.Clear();
            IsAlive = false;
            ActionOver = true;
            Render.Clear();
            if (cell != null && cell.troop == this)
                cell.troop = null;
        }

        public void RemovePerson(Person person)
        {
            if (person == null) return;

            if (Member1 == person)
            {
                Member1.BelongTroop = null;
                Member1 = null;
            }
            else if (Member2 == person)
            {
                Member2.BelongTroop = null;
                Member2 = null;
            }

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
                RemovePerson(mem);
                mem.SetMission(MissionType.PersonReturn, mem.BelongCity, 1);
                mem.ActionOver = true;
            });
            CalculateAttribute(Scenario.Cur);
            return true;
        }

        public void OnPersonChangeCity(Person person, City old_city, City new_city)
        {

        }


        public List<Cell> MoveRange = new List<Cell>();
        public void SetMission(MissionType missionType, int missionTarget)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget;
        }

        TroopMissionBehaviour troopMissionBehaviour;
        public TroopMissionBehaviour TroopMissionBehaviour
        {
            get
            {
                if (missionType == 0)
                {
                    missionType = (int)MissionType.ReturnCity;
                    missionTarget = BelongCity.Id;
                    NeedPrepareMission();
                }

                if (this.troopMissionBehaviour == null || (int)troopMissionBehaviour.MissionType != missionType)
                {
                    switch (missionType)
                    {
                        case (int)MissionType.DestroyTroop:
                            troopMissionBehaviour = new TroopDestroyTroop();
                            break;
                        case (int)MissionType.DestroyBuilding:
                            troopMissionBehaviour = new TroopDestroyBuilding();
                            break;
                        case (int)MissionType.OccupyCity:
                            troopMissionBehaviour = new TroopOccupyCity();
                            break;
                        case (int)MissionType.BanishTroop:
                            troopMissionBehaviour = new TroopBanishTroop();
                            break;
                        case (int)MissionType.ProtectBuilding:
                            troopMissionBehaviour = new TroopProtectBuilding();
                            break;
                        case (int)MissionType.ProtectTroop:
                            troopMissionBehaviour = new TroopProtectTroop();
                            break;
                        case (int)MissionType.ProtectCity:
                            troopMissionBehaviour = new TroopProtectCity();
                            break;
                        case (int)MissionType.MovetoCity:
                            troopMissionBehaviour = new TrooprMovetoCity();
                            break;
                        case (int)MissionType.ReturnCity:
                            troopMissionBehaviour = new TroopReturnCity();
                            break;
                        default:
                            troopMissionBehaviour = new TroopReturnCity();
                            break;
                    }
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
                scenario.Event.OnTroopAIStart?.Invoke(this, scenario);
            }

            TroopMissionBehaviour temp = TroopMissionBehaviour;
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

            scenario.Event.OnTroopAIEnd?.Invoke(this, scenario);
            AIFinished = true;
            ActionOver = true;
            return true;
        }

        public void AIPrepare(Scenario scenario)
        {
            // 永不退缩
            //if ((morale <= 5 && GameRandom.Changce(60)) ||
            //    (troops < 500 && GameRandom.Changce(80)) ||
            //    food < (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (troops + woundedTroops) * TroopType.foodCostFactor) * 3 && GameRandom.Changce(80)
            //    )
            //{
            //    missionType = (int)MissionType.ReturnCity;
            //    missionTarget = BelongCity.Id;
            //}
        }

    }
}
