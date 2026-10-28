using System.Collections.Generic;

namespace Sango.Core
{
    /*
     * 战斗公式逻辑库（可插拔）
     * ────────────────────────────────────────────────────────────────
     * 本文件把"战斗伤害怎么算"从 Troop 里抽出来，做成和 SkillSuccessMethod /
     * SkillCriticalMethod 同一套范式：
     *
     *   抽象基类（Calculate） → 静态注册表（Register/Create） → JSON 里配一个名字
     *
     * 与战法成功率那套的**唯一区别**：那边每个 SkillInstance 持有一个实例（带 master 状态），
     * 这边是**无状态单例**——因为 AI 每次评估都要预演伤害（TroopAIUtility），
     * 每帧可能调用几十上百次，不能每次都 new。
     *
     * 注册表带实例缓存：同一个名字只会 new 一次。
     *
     * 配置位置（从粗到细）：
     *   1. ScenarioVariables.troopDamageMethod / garrisonDamageMethod / durabilityDamageMethod（全局）
     *   2. 兵种级覆盖见 TroopType（后续按需接入）
     *   3. 名字为空或找不到 → 自动回落到 Open311 默认实现，**绝不返回 null**
     */

    /// <summary>
    /// 战斗公式基类。TContext 是一次结算用到的全部输入，避免公式去读全局可变状态。
    /// </summary>
    public abstract class CombatFormula<TContext>
    {
        /// <summary>计算伤害（返回最终整型伤害值）</summary>
        public abstract int Calculate(in TContext ctx);
    }

    /// <summary>
    /// 公式注册表（按公式基类分别开一份静态表，互不干扰）。
    /// </summary>
    public static class CombatFormulaRegistry<T> where T : class
    {
        public delegate T Creator();

        /// <summary>名字 → 工厂</summary>
        static readonly Dictionary<string, Creator> Map = new Dictionary<string, Creator>();

        /// <summary>名字 → 单例（无状态，可安全复用）</summary>
        static readonly Dictionary<string, T> Cache = new Dictionary<string, T>();

        /// <summary>未配置时使用的公式名</summary>
        public static string DefaultName;

        /// <summary>注册一个公式实现。重复注册会覆盖，并清掉该名字的缓存实例。</summary>
        public static void Register(string name, Creator creator)
        {
            if (string.IsNullOrEmpty(name) || creator == null) return;
            Map[name] = creator;
            Cache.Remove(name);
        }

        /// <summary>设置默认公式名（配置为空或找不到时用它）</summary>
        public static void SetDefault(string name)
        {
            DefaultName = name;
        }

        /// <summary>按名字取公式实例；找不到返回 null，由上层基类做兜底。</summary>
        public static T Create(string name)
        {
            if (string.IsNullOrEmpty(name)) name = DefaultName;
            if (string.IsNullOrEmpty(name)) return null;

            if (Cache.TryGetValue(name, out T cached)) return cached;

            if (!Map.TryGetValue(name, out Creator creator)) return null;

            T instance = creator();
            if (instance == null) return null;
            Cache[name] = instance;
            return instance;
        }

        /// <summary>清空实例缓存（热重载 / 换配置后调用）</summary>
        public static void ClearCache()
        {
            Cache.Clear();
        }
    }

    #region 上下文

    /// <summary>部队 → 部队 的兵力伤害输入</summary>
    public readonly struct TroopDamageContext
    {
        public readonly Troop Attacker;
        public readonly Troop Target;
        public readonly SkillInstance Skill;
        public readonly ScenarioVariables Vars;
        /// <summary>难度系数（仅对玩家生效，1 = 无修正）</summary>
        public readonly float DifficultyFactor;

        public TroopDamageContext(Troop attacker, Troop target, SkillInstance skill, ScenarioVariables vars, float difficultyFactor)
        {
            Attacker = attacker;
            Target = target;
            Skill = skill;
            Vars = vars;
            DifficultyFactor = difficultyFactor;
        }
    }

    /// <summary>部队 → 据点守军 的兵力伤害输入</summary>
    public readonly struct GarrisonDamageContext
    {
        public readonly Troop Attacker;
        public readonly City Target;
        public readonly SkillInstance Skill;
        public readonly ScenarioVariables Vars;
        public readonly float DifficultyFactor;

        public GarrisonDamageContext(Troop attacker, City target, SkillInstance skill, ScenarioVariables vars, float difficultyFactor)
        {
            Attacker = attacker;
            Target = target;
            Skill = skill;
            Vars = vars;
            DifficultyFactor = difficultyFactor;
        }
    }

    /// <summary>部队 → 建筑 的耐久伤害输入</summary>
    public readonly struct DurabilityDamageContext
    {
        public readonly Troop Attacker;
        public readonly BuildingBase Target;
        public readonly SkillInstance Skill;
        public readonly ScenarioVariables Vars;
        public readonly float DifficultyFactor;

        public DurabilityDamageContext(Troop attacker, BuildingBase target, SkillInstance skill, ScenarioVariables vars, float difficultyFactor)
        {
            Attacker = attacker;
            Target = target;
            Skill = skill;
            Vars = vars;
            DifficultyFactor = difficultyFactor;
        }
    }

    #endregion

    #region 公式入口（各公式族一份注册表）

    /// <summary>
    /// 部队打部队的兵力伤害公式。
    /// 当前默认实现 Open311TroopDamageMethod 逐行对齐原版 calc_troops_damage
    /// （D:\open311\s11\s11_sys_damage.cpp:1404-1440）。
    /// </summary>
    public abstract class TroopDamageMethod : CombatFormula<TroopDamageContext>
    {
        static TroopDamageMethod fallback;

        public static void Init()
        {
            CombatFormulaRegistry<TroopDamageMethod>.Register("Open311", () => new Open311TroopDamageMethod());
            CombatFormulaRegistry<TroopDamageMethod>.SetDefault("Open311");
        }

        /// <summary>取公式实例，永不返回 null</summary>
        public static TroopDamageMethod Create(string name)
        {
            TroopDamageMethod method = CombatFormulaRegistry<TroopDamageMethod>.Create(name);
            if (method != null) return method;
            // 配置写错 / 旧存档没有该字段时兜底，保证战斗主路径不断
            if (fallback == null) fallback = new Open311TroopDamageMethod();
            return fallback;
        }
    }

    /// <summary>
    /// 部队打据点（城/关/港）守军的兵力伤害公式。
    /// 当前默认实现对齐原版 calc_attack_damage 的建筑分支
    /// （D:\open311\s11\s11_sys_damage.cpp:438-467）。
    /// </summary>
    public abstract class GarrisonDamageMethod : CombatFormula<GarrisonDamageContext>
    {
        static GarrisonDamageMethod fallback;

        public static void Init()
        {
            CombatFormulaRegistry<GarrisonDamageMethod>.Register("Open311", () => new Open311GarrisonDamageMethod());
            CombatFormulaRegistry<GarrisonDamageMethod>.SetDefault("Open311");
        }

        public static GarrisonDamageMethod Create(string name)
        {
            GarrisonDamageMethod method = CombatFormulaRegistry<GarrisonDamageMethod>.Create(name);
            if (method != null) return method;
            if (fallback == null) fallback = new Open311GarrisonDamageMethod();
            return fallback;
        }
    }

    /// <summary>
    /// 部队打建筑的耐久伤害公式。
    /// 当前默认实现对齐原版 calc_hp_damage / calc_shousha_hp_damage
    /// （D:\open311\s11\s11_sys_damage.cpp:1531-1553）。
    /// </summary>
    public abstract class DurabilityDamageMethod : CombatFormula<DurabilityDamageContext>
    {
        static DurabilityDamageMethod fallback;

        public static void Init()
        {
            CombatFormulaRegistry<DurabilityDamageMethod>.Register("Open311", () => new Open311DurabilityDamageMethod());
            CombatFormulaRegistry<DurabilityDamageMethod>.SetDefault("Open311");
        }

        public static DurabilityDamageMethod Create(string name)
        {
            DurabilityDamageMethod method = CombatFormulaRegistry<DurabilityDamageMethod>.Create(name);
            if (method != null) return method;
            if (fallback == null) fallback = new Open311DurabilityDamageMethod();
            return fallback;
        }
    }

    #endregion

    #region 公共数学部分

    /// <summary>
    /// 战斗公式的公共数学部分：无状态、不碰 Troop/City，方便单测与逐位对拍。
    /// </summary>
    public static class CombatFormulaMath
    {
        /// <summary>
        /// 原版 calc_troops_damage 的逐行复刻（D:\open311\s11\s11_sys_damage.cpp:1404-1440）。
        ///
        /// 【形参说明】原版的 troops / target_troops 实际传入的是 get_command()
        /// （= min(官职带兵上限, 兵力)，见 1375-1382），满编与残兵时都等于实际兵力；
        /// 本项目没有单独的 command 概念，直接用兵力。
        ///
        /// 【顺序不能动】原版全程整型运算、除法向零截断，先算谁后算谁都会影响尾数：
        ///     n  = sqrt(troops_atk * 64)
        ///     n += max((兵力 - 敌兵力) / 2000, 0)
        ///     n += max((max(atk,40)² - max(def,40)²) / 300, 0)
        ///     n += 50
        ///     n *= (buffed + 10)              // buffed 恒 false（太鼓台未接入）→ ×10
        ///     A  = (atk+50)² × (兵力/100+300) / 100
        ///     B  = (def+50)² × (敌兵力/100+300) / 100
        ///     C  = A>=B ? A*100/(A+B) : max(100 - B*100/(A+B), 1)
        ///     n *= C
        ///     n *= min(sqrt(max(兵力/4, 1)), 40)
        ///     n *= 1/2100
        ///     n += 兵力/200
        /// </summary>
        public static double CalcTroopsDamageBody(ScenarioVariables vars, int atkStat, int defStat, int attackerTroops, int targetTroops, int troopsAtk)
        {
            // 配置项兜底，避免被调成 0 后除零
            int troopsNeed = (int)vars.fight_base_troops_need;
            if (troopsNeed <= 0) troopsNeed = 2000;
            int troopCount = (int)vars.fight_base_troop_count;
            if (troopCount <= 0) troopCount = 200;

            // a = max(atk,40)²，b = max(def,40)²
            int a = System.Math.Max(atkStat, 40);
            a = a * a;
            int b = System.Math.Max(defStat, 40);
            b = b * b;

            double n = System.Math.Sqrt(troopsAtk * vars.fight_base_damage);
            n = n + System.Math.Max((attackerTroops - targetTroops) / troopsNeed, 0);
            n = n + System.Math.Max((a - b) / 300, 0);
            n = n + 50.0;
            // 原版 (buffed + 10)：buffed 是"周围 3 格内有本方太鼓台"，本次不接入，恒按 10
            n = n * 10.0;

            // 兵力比系数：原版 a/b 先各自 /100，再折算成百分比
            int aa = (atkStat + 50) * (atkStat + 50) * (attackerTroops / 100 + 300) / 100;
            int bb = (defStat + 50) * (defStat + 50) * (targetTroops / 100 + 300) / 100;
            int sum = aa + bb;
            if (sum <= 0) sum = 1;
            int ratio;
            if (aa >= bb)
                ratio = aa * 100 / sum;
            else
                ratio = System.Math.Max(100 - (bb * 100 / sum), 1);
            n = n * ratio;

            // 兵力开方系数，封顶 40
            int quarter = System.Math.Max(attackerTroops / 4, 1);
            n = n * System.Math.Min(System.Math.Sqrt((double)quarter), 40.0);

            n = n * vars.fight_damage_magic_number;
            n = n + attackerTroops / troopCount;

            return n;
        }

        /// <summary>
        /// **建筑打部队**的兵力伤害 —— 逐行复刻原版 calc_troops_damage(Building*, Unit*)
        /// （D:\open311\s11\s11_sys_damage.cpp:1442-1482）。
        ///
        ///     max_hp = max(建筑最大耐久, 1)
        ///     cur_hp = max(当前耐久, max_hp / 2)             ← 半血保护：残血设施不掉伤害
        ///     n  = cur_hp / max_hp
        ///     n *= 152 - 目标防御
        ///     n *= 设施攻击（BuildingType.troopAtk）
        ///     n /= 132
        ///     n += rand(0, troopAtkBonus)
        ///
        /// 【与原版一致】只有阵 400 / 砦 600 / 城塞 800 / 箭楼 300 / 连弩楼 500 / 投石台 900
        /// 在攻击表里；**城 / 关 / 港 不主动攻击部队**（troopAtk = 0 → 本方法返回 0）。
        /// </summary>
        public static int CalcBuildingTroopsDamage(BuildingBase self, Troop target)
        {
            if (self == null || target == null) return 0;

            BuildingType type = self.BuildingType;
            if (type == null || type.troopAtk <= 0) return 0;

            int maxHp = System.Math.Max(self.DurabilityLimit, 1);
            int curHp = System.Math.Max(self.durability, maxHp / 2);

            double n = (double)curHp / maxHp;
            n *= 152 - target.Defence;
            n *= type.troopAtk;
            n /= 132.0;

            int damage = (int)n;
            if (type.troopAtkBonus > 0)
                damage += GameRandom.Range(0, type.troopAtkBonus);

            return System.Math.Max(damage, 0);
        }

        /// <summary>难度系数：原版只对玩家生效（s11_sys_damage.cpp:470-471）</summary>
        public static float GetDifficultyFactor(Force force, ScenarioVariables vars)
        {
            if (force != null && force.IsPlayer) return vars.DifficultyDamageFactor;
            return 1f;
        }
    }

    #endregion
}
