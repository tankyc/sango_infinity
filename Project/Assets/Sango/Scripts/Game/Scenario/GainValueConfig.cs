using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 成长资源的**获取地点**。作为 Key-Value 配置的键：
    /// 每种资源各一张表（功绩 / 技巧点 / 武将经验 / 兵种适性经验 / 能力经验），键就是这里的地点。
    ///
    /// 值的语义有三种，由 <see cref="GainValueConfig.KindOf"/> 给出：
    ///   · <see cref="GainValueKind.Fixed"/> 固定值：直接就是加多少（例如单挑胜利功绩 200）
    ///   · <see cref="GainValueKind.Rate"/>  百分比：对某个基数打折
    ///     （例如战斗伤害折算 10 表示 damage 的 10%；副将功绩比例 60 表示 60%）
    ///   · <see cref="GainValueKind.Range"/> 区间：在 [最小值, 最大值] 内随机
    ///     （例如内政工作的能力经验，默认 5~8）
    ///
    /// 编号显式写死、只允许追加：调顺序会把老存档/老剧本里已存的键对错。
    /// 每个地点只归属一张表（见各表的 Places 列表），这样 <see cref="GainValueConfig.DefaultOf"/> 可以只维护一份。
    /// </summary>
    public enum GainPlace
    {
        None = 0,

        #region 战斗（功绩表）
        /// <summary>战斗伤害折算（百分比：damage * 值 / 100）</summary>
        CombatTroopDamage = 1,
        /// <summary>击破敌方部队（固定加成）</summary>
        CombatDestroyTroop = 2,
        /// <summary>攻略城池（固定加成）</summary>
        CombatDestroyCity = 3,
        /// <summary>攻破关（固定加成）</summary>
        CombatDestroyGate = 4,
        /// <summary>攻破港（固定加成）</summary>
        CombatDestroyPort = 5,
        /// <summary>破坏建筑（固定加成）</summary>
        CombatDestroyBuilding = 6,

        /// <summary>主将功绩比例（百分比）</summary>
        MeritLeaderFactor = 10,
        /// <summary>副将功绩比例（百分比）</summary>
        MeritMemberFactor = 11,
        /// <summary>技巧点按功绩的比例（百分比）</summary>
        TechniquePointFromMerit = 12,
        /// <summary>武将等级经验按功绩的比例（百分比）</summary>
        ExpFromMerit = 13,
        #endregion

        #region 单挑
        /// <summary>单挑平局：双方功绩</summary>
        DuelDraw = 20,
        /// <summary>单挑胜利：胜方功绩</summary>
        DuelWin = 21,
        /// <summary>单挑胜利（俘获对方）：胜方功绩</summary>
        DuelCapture = 22,
        /// <summary>单挑失败：败方功绩</summary>
        DuelLose = 23,
        /// <summary>单挑胜利：胜方势力的技巧点</summary>
        DuelWinTechniquePoint = 24,
        /// <summary>单挑平局：双方武将经验</summary>
        DuelDrawExp = 25,
        /// <summary>单挑胜利：胜方武将经验</summary>
        DuelWinExp = 26,
        /// <summary>单挑失败：败方武将经验</summary>
        DuelLoseExp = 27,
        #endregion

        #region 舌战
        /// <summary>舌战胜利：功绩</summary>
        DebateWin = 30,
        /// <summary>舌战胜利（仁德 / 强硬）：功绩</summary>
        DebateWinKind = 31,
        /// <summary>舌战失败：功绩</summary>
        DebateLose = 32,
        /// <summary>舌战胜利：势力技巧点</summary>
        DebateWinTechniquePoint = 33,
        /// <summary>舌战胜利：能力经验（区间）</summary>
        DebateAttributeExp = 34,
        /// <summary>舌战胜利（仁德 / 强硬）：能力经验（区间）</summary>
        DebateAttributeExpKind = 35,
        /// <summary>舌战失败：能力经验</summary>
        DebateAttributeExpLose = 36,
        #endregion

        #region 内政 / 工作（能力经验）
        /// <summary>
        /// 执行内政工作的能力经验（区间随机）。
        /// 具体加哪一项属性由"工作类型 → 属性"的映射决定：
        /// 训练→武力、征兵/组建部队/生产马→统率、搜索/生产兵器/造舰/研究→智力、
        /// 农业/商业/交易/建造/派遣→政治、巡查/登用→魅力。
        /// </summary>
        AttributeExpFromJob = 40,
        #endregion

        #region 兵种适性经验
        /// <summary>
        /// 部队每次攻击/战法结算的适性经验。
        /// 加到哪一项适性由 TroopType.influenceAbility 决定（枪/戟/弩/骑/水/器）。
        /// </summary>
        AbilityExpAttack = 50,
        /// <summary>击破敌方部队时额外获得的适性经验</summary>
        AbilityExpDestroyTroop = 51,
        /// <summary>攻破城池 / 关 / 港时额外获得的适性经验</summary>
        AbilityExpDestroyCity = 52,
        #endregion
    }

    /// <summary>配置值的语义</summary>
    public enum GainValueKind
    {
        /// <summary>固定值：直接加这个数</summary>
        Fixed,
        /// <summary>百分比：基数 * 值 / 100</summary>
        Rate,
        /// <summary>区间：在 [value, valueMax] 内随机</summary>
        Range,
    }

    [Serializable]
    public class GainValueEntry
    {
        public GainPlace place;
        /// <summary>固定值 / 百分比 / 区间下限</summary>
        public int value;
        /// <summary>区间上限（仅 Range 语义使用；<= 0 表示未设置，回落到默认上限）</summary>
        public int valueMax;
    }

    /// <summary>
    /// 一张"获取地点 → 数值"的表。
    ///
    /// 只保存**被改过**的项；查不到的键回落到 <see cref="DefaultOf"/>（= 引入本表之前的硬编码值），
    /// 所以老存档 / 老剧本里没有这些字段时，行为与改动前完全一致。
    /// </summary>
    [Serializable]
    public class GainValueConfig
    {
        /// <summary>百分比口径的分母</summary>
        public const int RateBase = 100;

        public List<GainValueEntry> entries = new List<GainValueEntry>();

        public int Get(GainPlace place)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    GainValueEntry e = entries[i];
                    if (e != null && e.place == place)
                        return e.value;
                }
            }
            return DefaultOf(place);
        }

        public void Set(GainPlace place, int value)
        {
            GainValueEntry e = FindOrAdd(place);
            e.value = value;
        }

        /// <summary>设置区间（Range 语义的地点用）</summary>
        public void SetRange(GainPlace place, int min, int max)
        {
            GainValueEntry e = FindOrAdd(place);
            e.value = min;
            e.valueMax = max;
        }

        public void GetRange(GainPlace place, out int min, out int max)
        {
            min = Get(place);
            max = MaxOfValue(place);
            if (max < min) max = min;
        }

        /// <summary>在 [min, max] 内随机取一个值（闭区间）。外面一般用静态的 GainValueConfig.Roll</summary>
        public int RollValue(GainPlace place)
        {
            GetRange(place, out int min, out int max);
            if (max <= min) return min;
            return UnityEngine.Random.Range(min, max + 1);
        }

        GainValueEntry FindOrAdd(GainPlace place)
        {
            if (entries == null) entries = new List<GainValueEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                GainValueEntry e = entries[i];
                if (e != null && e.place == place) return e;
            }
            GainValueEntry added = new GainValueEntry { place = place };
            entries.Add(added);
            return added;
        }

        int MaxOfValue(GainPlace place)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    GainValueEntry e = entries[i];
                    if (e != null && e.place == place)
                        return e.valueMax > 0 ? e.valueMax : DefaultMaxOf(place);
                }
            }
            return DefaultMaxOf(place);
        }

        /// <summary>按百分比取值：baseValue * value / 100</summary>
        public static int Rate(int baseValue, int value)
        {
            return (int)((long)baseValue * value / RateBase);
        }

        #region 全局读取（Scenario.Cur 不存在时安全回落到默认值，便于纯逻辑推演 / 编辑器内调用）

        public static int Merit(GainPlace place) { return ValueOf(TableOf(place), place); }
        public static int TechniquePoint(GainPlace place) { return ValueOf(TableOf(place), place); }
        public static int Exp(GainPlace place) { return ValueOf(TableOf(place), place); }
        public static int AbilityExp(GainPlace place) { return ValueOf(TableOf(place), place); }
        public static int AttributeExp(GainPlace place) { return ValueOf(TableOf(place), place); }

        /// <summary>在 [min, max] 内随机取值（Range 语义的地点用；无剧本时用默认区间）</summary>
        public static int Roll(GainPlace place)
        {
            GainValueConfig cfg = TableOf(place);
            if (cfg != null) return cfg.RollValue(place);

            int min = DefaultOf(place);
            int max = DefaultMaxOf(place);
            if (max <= min) return min;
            return UnityEngine.Random.Range(min, max + 1);
        }

        static int ValueOf(GainValueConfig table, GainPlace place)
        {
            return table != null ? table.Get(place) : DefaultOf(place);
        }

        /// <summary>这个地点属于哪张表（未归属 / 无剧本时返回 null）</summary>
        static GainValueConfig TableOf(GainPlace place)
        {
            ScenarioVariables v = Scenario.Cur != null ? Scenario.Cur.Variables : null;
            if (v == null) return null;

            if (Array.IndexOf(AttributeExpPlaces, place) >= 0) return v.attributeExpGain;
            if (Array.IndexOf(MeritPlaces, place) >= 0) return v.meritGain;
            if (Array.IndexOf(TechniquePointPlaces, place) >= 0) return v.techniquePointGain;
            if (Array.IndexOf(ExpPlaces, place) >= 0) return v.expGain;
            if (Array.IndexOf(AbilityExpPlaces, place) >= 0) return v.abilityExpGain;
            return null;
        }

        #endregion

        #region 各表的键列表（供编辑器渲染；也是哪些键属于哪张表的唯一说明）

        public static readonly GainPlace[] MeritPlaces =
        {
            GainPlace.CombatTroopDamage, GainPlace.CombatDestroyTroop, GainPlace.CombatDestroyCity,
            GainPlace.CombatDestroyGate, GainPlace.CombatDestroyPort, GainPlace.CombatDestroyBuilding,
            GainPlace.MeritLeaderFactor, GainPlace.MeritMemberFactor,
            GainPlace.DuelDraw, GainPlace.DuelWin, GainPlace.DuelCapture, GainPlace.DuelLose,
            GainPlace.DebateWin, GainPlace.DebateWinKind, GainPlace.DebateLose,
        };

        public static readonly GainPlace[] TechniquePointPlaces =
        {
            GainPlace.TechniquePointFromMerit,
            GainPlace.DuelWinTechniquePoint,
            GainPlace.DebateWinTechniquePoint,
        };

        public static readonly GainPlace[] ExpPlaces =
        {
            GainPlace.ExpFromMerit,
            GainPlace.DuelDrawExp, GainPlace.DuelWinExp, GainPlace.DuelLoseExp,
        };

        public static readonly GainPlace[] AbilityExpPlaces =
        {
            GainPlace.AbilityExpAttack,
            GainPlace.AbilityExpDestroyTroop,
            GainPlace.AbilityExpDestroyCity,
        };

        public static readonly GainPlace[] AttributeExpPlaces =
        {
            GainPlace.AttributeExpFromJob,
            GainPlace.DebateAttributeExp, GainPlace.DebateAttributeExpKind, GainPlace.DebateAttributeExpLose,
        };

        #endregion

        /// <summary>内置默认值（固定值类的数值 / 百分比类的百分比 / 区间类的下限）</summary>
        public static int DefaultOf(GainPlace place)
        {
            switch (place)
            {
                // 战斗（功绩）
                case GainPlace.CombatTroopDamage: return 10;      // damage / 10
                case GainPlace.CombatDestroyTroop: return 200;
                case GainPlace.CombatDestroyCity: return 1200;
                case GainPlace.CombatDestroyGate: return 600;
                case GainPlace.CombatDestroyPort: return 300;
                case GainPlace.CombatDestroyBuilding: return 200;
                // 战斗后的分配比例
                case GainPlace.MeritLeaderFactor: return 100;
                case GainPlace.MeritMemberFactor: return 60;
                case GainPlace.TechniquePointFromMerit: return 20;  // gp / 5
                case GainPlace.ExpFromMerit: return 20;             // gp / 5
                // 单挑
                case GainPlace.DuelDraw: return 50;
                case GainPlace.DuelWin: return 100;
                case GainPlace.DuelCapture: return 200;
                case GainPlace.DuelLose: return 10;
                case GainPlace.DuelWinTechniquePoint: return 50;
                case GainPlace.DuelDrawExp: return 3;
                case GainPlace.DuelWinExp: return 10;
                case GainPlace.DuelLoseExp: return 1;
                // 舌战（能力经验已按"低频成长"收紧到 1~3，原来是 10 / 30）
                case GainPlace.DebateWin: return 100;
                case GainPlace.DebateWinKind: return 200;
                case GainPlace.DebateLose: return 10;
                case GainPlace.DebateWinTechniquePoint: return 50;
                case GainPlace.DebateAttributeExp: return 1;
                case GainPlace.DebateAttributeExpKind: return 1;
                case GainPlace.DebateAttributeExpLose: return 1;
                // 内政工作的能力经验（区间下限）。
                // 节奏（回合制：每人每回合最多一个行为、一年 36 回合）：
                //   按一人一年约 20 次工作算，平均 12.5 点/次 ≈ 250 点/年 = 1 点属性/年
                //   （与 AttributeExpLevelNeed = 250 正好对齐；30 点上限约 30 年，符合一场战役的长度）
                case GainPlace.AttributeExpFromJob: return 10;
                // 兵种适性经验（与 AbilityExpLevelNeed = 100 对齐）。
                // 节奏：一个部队一年约 15 次战斗结算（含反击），3 点/次 ≈ 45 点/年
                //   → 一级 100 点约 2.2 年，10 级满级约 22 年（击破 / 攻城会明显加速）
                case GainPlace.AbilityExpAttack: return 3;
                case GainPlace.AbilityExpDestroyTroop: return 10;
                case GainPlace.AbilityExpDestroyCity: return 30;
                default: return 0;
            }
        }

        /// <summary>区间类地点的默认上限（非区间地点返回与 <see cref="DefaultOf"/> 相同）</summary>
        public static int DefaultMaxOf(GainPlace place)
        {
            switch (place)
            {
                case GainPlace.AttributeExpFromJob: return 15;
                case GainPlace.DebateAttributeExp: return 3;
                case GainPlace.DebateAttributeExpKind: return 3;
                default: return DefaultOf(place);
            }
        }

        public static GainValueKind KindOf(GainPlace place)
        {
            switch (place)
            {
                case GainPlace.CombatTroopDamage:
                case GainPlace.MeritLeaderFactor:
                case GainPlace.MeritMemberFactor:
                case GainPlace.TechniquePointFromMerit:
                case GainPlace.ExpFromMerit:
                    return GainValueKind.Rate;
                case GainPlace.AttributeExpFromJob:
                case GainPlace.DebateAttributeExp:
                case GainPlace.DebateAttributeExpKind:
                    return GainValueKind.Range;
                default:
                    return GainValueKind.Fixed;
            }
        }

        /// <summary>编辑器里的可调上限（百分比 / 区间类放宽到 1000，固定值类给足空间）</summary>
        public static int MaxOf(GainPlace place)
        {
            return KindOf(place) == GainValueKind.Fixed ? 100000 : 1000;
        }

        /// <summary>获取地点的中文名（配置界面标题）</summary>
        public static string NameOf(GainPlace place)
        {
            switch (place)
            {
                case GainPlace.CombatTroopDamage: return "战斗伤害折算(百分比)";
                case GainPlace.CombatDestroyTroop: return "击破敌方部队";
                case GainPlace.CombatDestroyCity: return "攻略城池";
                case GainPlace.CombatDestroyGate: return "攻破关";
                case GainPlace.CombatDestroyPort: return "攻破港";
                case GainPlace.CombatDestroyBuilding: return "破坏建筑";
                case GainPlace.MeritLeaderFactor: return "主将功绩比例(百分比)";
                case GainPlace.MeritMemberFactor: return "副将功绩比例(百分比)";
                case GainPlace.TechniquePointFromMerit: return "技巧点按功绩比例(百分比)";
                case GainPlace.ExpFromMerit: return "武将经验按功绩比例(百分比)";
                case GainPlace.DuelDraw: return "单挑平局功绩";
                case GainPlace.DuelWin: return "单挑胜利功绩";
                case GainPlace.DuelCapture: return "单挑俘获功绩";
                case GainPlace.DuelLose: return "单挑失败功绩";
                case GainPlace.DuelWinTechniquePoint: return "单挑胜利技巧点";
                case GainPlace.DuelDrawExp: return "单挑平局武将经验";
                case GainPlace.DuelWinExp: return "单挑胜利武将经验";
                case GainPlace.DuelLoseExp: return "单挑失败武将经验";
                case GainPlace.DebateWin: return "舌战胜利功绩";
                case GainPlace.DebateWinKind: return "舌战胜利功绩(仁德/强硬)";
                case GainPlace.DebateLose: return "舌战失败功绩";
                case GainPlace.DebateWinTechniquePoint: return "舌战胜利技巧点";
                case GainPlace.DebateAttributeExp: return "舌战胜利能力经验";
                case GainPlace.DebateAttributeExpKind: return "舌战胜利能力经验(仁德/强硬)";
                case GainPlace.DebateAttributeExpLose: return "舌战失败能力经验";
                case GainPlace.AttributeExpFromJob: return "内政工作能力经验";
                case GainPlace.AbilityExpAttack: return "兵种适性 攻击结算";
                case GainPlace.AbilityExpDestroyTroop: return "兵种适性 击破敌部队";
                case GainPlace.AbilityExpDestroyCity: return "兵种适性 攻破城关港";
                default: return place.ToString();
            }
        }
    }
}
