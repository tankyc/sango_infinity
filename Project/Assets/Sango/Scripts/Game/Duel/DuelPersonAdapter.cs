/*
 * 文件名：DuelPersonAdapter.cs
 * 描述：单挑系统与游戏真实 Person（Sango.Core.Person）之间的适配层
 *
 * 设计思路：
 *   单挑系统内部一律使用 Sango.Core.Person。真实 Person 没有提供单挑所需的全部方法，
 *   因此这里用【扩展方法】补齐，单挑本体代码（Duel.cs / DuelAI.cs）无需大改即可直接使用真实武将。
 *
 *   注意：真实 Person 已有的实例方法（IsLike / IsHate / IsBrother / IsParentchild 等）
 *        优先级高于扩展方法，因此单挑里调用它们时会自动走游戏自带实现。
 *
 * 接入时需要调整的项（都在本文件，集中在 DuelSettings / DuelPersonId / DuelFeatureId）：
 *   1. DuelPersonId.IdMap - 特殊武将（吕布/关羽/张飞...）的 ID 映射，不填则按姓名匹配
 *   2. DuelFeatureId      - 强运 / 捕缚 等特技在贵项目中的 ID
 *   3. DuelSettings       - 难度、寿命模式、功能开关、宝物列表等钩子
 *
 * 性格（personality）无需配置：项目内 1胆小/2冷静/3刚胆/4莽撞 与单挑 AI 性格一一对应，
 * 由 DuelSeikaku 直接转换；如需改数据可覆盖 DuelSeikaku.PersonalityMap 或 Resolver。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core.Duel
{
    #region 随机源

    /// <summary>
    /// 单挑随机源。默认转发到项目的 GameRandom；
    /// 调用 SetSeed 后切换为独立的可复现随机（用于测试 / 录像回放）。
    /// </summary>
    public static class DuelRandom
    {
        private static Random s_random = null;
        private static int s_seed = 0;

        /// <summary>设置种子，切换为可复现随机</summary>
        public static void SetSeed(int seed)
        {
            s_seed = seed;
            s_random = new Random(seed);
        }

        /// <summary>恢复使用项目的 GameRandom</summary>
        public static void UseProjectRandom()
        {
            s_random = null;
            s_seed = 0;
        }

        /// <summary>当前种子（未设置时为 0）</summary>
        public static int GetSeed() { return s_seed; }

        /// <summary>返回 [0, max) 的随机整数</summary>
        public static int Range(int max)
        {
            if (max <= 0) return 0;
            return s_random != null ? s_random.Next(max) : GameRandom.Range(max);
        }

        /// <summary>以 percent% 的概率返回 true</summary>
        public static bool Chance(int percent)
        {
            if (percent <= 0) return false;
            if (percent >= 100) return true;
            return s_random != null ? s_random.Next(100) < percent : GameRandom.Chance(percent);
        }
    }

    #endregion

    #region 全局设置 / 钩子

    /// <summary>
    /// 单挑系统所需的全局设置与外部依赖钩子。
    /// 接入真实游戏时，按需给这些字段/委托赋值即可，无需改动单挑本体。
    /// </summary>
    public static class DuelSettings
    {
        private static readonly List<Item> s_emptyItems = new List<Item>();

        /// <summary>难度</summary>
        public static Difficulty Difficulty = Difficulty.Normal;

        /// <summary>寿命模式（影响黄忠等"老将"加成）</summary>
        public static LifeMode LifeMode = LifeMode.Normal;

        /// <summary>战死频率</summary>
        public static BattleDeathMode BattleDeathMode = BattleDeathMode.Normal;

        /// <summary>功能是否被禁用（一击必杀 / 捕缚 / AI 退却 等）</summary>
        public static Func<Feature, bool> IsFeatDisabled = feature => false;

        /// <summary>获取武将持有的宝物列表（影响单挑中的名马/剑/长武器/暗器/弓）</summary>
        public static Func<Person, List<Item>> GetPersonItemList = person => s_emptyItems;

        /// <summary>获取武将宝物提供的单挑战力加成</summary>
        public static Func<Person, int> GetDuelItemPower = person => 0;

        /// <summary>获取势力颜色（用于历史日志着色），默认 0</summary>
        public static Func<Person, int> GetForceColor = person => 0;

        /// <summary>百分概率判定，默认走 DuelRandom</summary>
        public static Func<int, bool> RandBool = DuelRandom.Chance;

        /// <summary>[0, max) 随机整数，默认走 DuelRandom</summary>
        public static Func<int, int> RandInt = DuelRandom.Range;

        /// <summary>根据伤病计算有效武力。默认：伤病每级衰减 20%</summary>
        public static Func<Person, int, int> CalcStrength = (person, shoubyou) =>
        {
            int strength = person.Strength;
            int level = Math.Max(0, Math.Min(shoubyou, (int)Shoubyou.Hinshi));
            return strength * (10 - level * 2) / 10;
        };

        /// <summary>恢复为默认实现</summary>
        public static void Reset()
        {
            Difficulty = Difficulty.Normal;
            LifeMode = LifeMode.Normal;
            BattleDeathMode = BattleDeathMode.Normal;
            IsFeatDisabled = feature => false;
            GetPersonItemList = person => s_emptyItems;
            GetDuelItemPower = person => 0;
            GetForceColor = person => 0;
            RandBool = DuelRandom.Chance;
            RandInt = DuelRandom.Range;
            CalcStrength = (person, shoubyou) =>
            {
                int strength = person.Strength;
                int level = Math.Max(0, Math.Min(shoubyou, (int)Shoubyou.Hinshi));
                return strength * (10 - level * 2) / 10;
            };
        }
    }

    #endregion

    #region 武将 ID 解析

    /// <summary>
    /// 把真实武将解析为单挑内部的 PersonId（吕布 / 关羽 / 张飞 ...）。
    /// 优先查 IdMap（按 Id 精确匹配，性能最好）；未命中则按姓名匹配。
    /// 结果按 Id 缓存，避免每次伤害计算都做字符串匹配。
    /// </summary>
    public static class DuelPersonId
    {
        private static readonly Dictionary<PersonId, string[]> s_names = new Dictionary<PersonId, string[]>
        {
            { PersonId.Ryofu,            new[] { "吕布", "呂布" } },
            { PersonId.Chouhi,           new[] { "张飞", "張飛" } },
            { PersonId.Kanu,             new[] { "关羽", "關羽" } },
            { PersonId.Kyocho,           new[] { "许褚", "許褚" } },
            { PersonId.Chouun,           new[] { "赵云", "趙雲" } },
            { PersonId.Bachou,           new[] { "马超", "馬超" } },
            { PersonId.Kouchuu_Kanshou,  new[] { "黄忠", "黃忠" } },
            { PersonId.Kakouen,          new[] { "夏侯渊", "夏侯淵" } },
            { PersonId.Ousou,            new[] { "黄盖", "黃蓋" } },
            { PersonId.Shukuyuu,         new[] { "周瑜" } },
        };

        /// <summary>按武将 Id 精确映射（优先级最高）。例：IdMap[851] = PersonId.Ryofu;</summary>
        public static readonly Dictionary<int, PersonId> IdMap = new Dictionary<int, PersonId>();

        private static readonly Dictionary<int, PersonId> s_cache = new Dictionary<int, PersonId>();

        /// <summary>清除姓名匹配缓存（IdMap 或武将数据变动后调用）</summary>
        public static void ClearCache() { s_cache.Clear(); }

        /// <summary>解析武将，未识别时返回 PersonId.Invalid</summary>
        public static PersonId Resolve(Person person)
        {
            if (person == null) return PersonId.Invalid;

            if (IdMap.TryGetValue(person.Id, out PersonId byId))
                return byId;

            if (s_cache.TryGetValue(person.Id, out PersonId cached))
                return cached;

            PersonId result = PersonId.Invalid;
            string name = person.Name;
            if (!string.IsNullOrEmpty(name))
            {
                foreach (KeyValuePair<PersonId, string[]> kv in s_names)
                {
                    for (int i = 0; i < kv.Value.Length; i++)
                    {
                        if (name.IndexOf(kv.Value[i], StringComparison.Ordinal) >= 0)
                        {
                            result = kv.Key;
                            break;
                        }
                    }
                    if (result != PersonId.Invalid) break;
                }
            }

            s_cache[person.Id] = result;
            return result;
        }
    }

    #endregion

    #region 性格 / 特技

    /// <summary>
    /// 把真实武将的 personality(性格) 直接转换为单挑 AI 性格。
    ///
    /// 项目内 Personalities 数据（Build/Content/Data/Common/Personalities.json）与单挑 AI 性格一一对应：
    ///   Id/kind = 1 胆小 → Seikaku.Shoushin（小心）
    ///   Id/kind = 2 冷静 → Seikaku.Reisei （冷静）
    ///   Id/kind = 3 刚胆 → Seikaku.Goutan  （大胆）
    ///   Id/kind = 4 莽撞 → Seikaku.Chototsu（猪突）
    /// 因此 kind / personality 减 1 即为 Seikaku 枚举值，无需额外配置。
    ///
    /// 若贵项目改动过性格数据，可用 PersonalityMap 覆盖，或替换 Resolver 委托。
    /// </summary>
    public static class DuelSeikaku
    {
        /// <summary>性格数量（对应 Personalities 的 1~4）</summary>
        public const int Count = 4;

        /// <summary>
        /// 自定义覆盖：personality 值 → 单挑 AI 性格。
        /// 优先级最高，仅在贵项目性格数据与默认值不一致时才需要填写。
        /// </summary>
        public static readonly Dictionary<int, Seikaku> PersonalityMap = new Dictionary<int, Seikaku>();

        /// <summary>兜底性格（数据缺失时使用）</summary>
        public static Seikaku Fallback = Seikaku.Reisei;

        /// <summary>自定义解析委托，置空则使用内置转换</summary>
        public static Func<Person, Seikaku> Resolver = null;

        public static Seikaku Resolve(Person person)
        {
            if (person == null) return Fallback;

            if (Resolver != null)
                return Resolver(person);

            // 优先用性格对象上的 kind（数据里 kind 与 Id 一致，语义更准确）
            int kind = person.mPersonality != null ? person.mPersonality.kind : 0;
            if (kind <= 0)
                kind = person.personality;

            if (PersonalityMap.TryGetValue(kind, out Seikaku mapped))
                return mapped;

            if (kind >= 1 && kind <= Count)
                return (Seikaku)(kind - 1);

            return Fallback;
        }
    }

    /// <summary>
    /// 单挑用到的特技在贵项目中的 ID。默认 -1 表示未配置（视为不拥有该特技）。
    /// </summary>
    public static class DuelFeatureId
    {
        /// <summary>强运：不会战死、不会被俘、必定退却成功</summary>
        public static int Kyouun = -1;

        /// <summary>捕缚：单挑获胜时更容易俘虏</summary>
        public static int Hobaku = -1;
    }

    #endregion

    #region Person 扩展方法

    /// <summary>
    /// 为 Sango.Core.Person 补齐单挑所需的访问器。
    /// 单挑本体通过这些扩展方法读写武将，从而与游戏真实武将无缝对接。
    /// </summary>
    public static class PersonDuelExtensions
    {
        /// <summary>武将标识（解析为单挑内部的 PersonId）</summary>
        public static PersonId GetId(this Person self)
        {
            return DuelPersonId.Resolve(self);
        }

        /// <summary>姓名</summary>
        public static string GetName(this Person self)
        {
            return self == null ? string.Empty : self.Name;
        }

        /// <summary>所属势力 ID</summary>
        public static int GetForceId(this Person self)
        {
            if (self == null) return -1;
            return self.mBelongForce != null ? self.mBelongForce.Id : self.BelongForce;
        }

        /// <summary>所在地区（城市）ID</summary>
        public static int GetDistrictId(this Person self)
        {
            if (self == null) return -1;
            City city = self.mCurrentCity ?? self.mBelongCity;
            return city != null ? city.Id : self.BelongCity;
        }

        /// <summary>伤病程度（0=健康，越大越重）</summary>
        public static int GetShoubyou(this Person self)
        {
            return self == null ? -1 : self.injury;
        }

        /// <summary>体力</summary>
        public static int GetHp(this Person self)
        {
            return self == null ? 0 : self.stamina;
        }

        /// <summary>年龄</summary>
        public static int GetAge(this Person self)
        {
            return self == null ? 0 : self.Age;
        }

        /// <summary>单挑 AI 性格</summary>
        public static Seikaku GetSeikaku(this Person self)
        {
            return DuelSeikaku.Resolve(self);
        }

        /// <summary>忠诚度</summary>
        public static int GetLoyalty(this Person self)
        {
            return self == null ? 0 : self.loyalty;
        }

        /// <summary>出生地 ID</summary>
        public static int GetBirthplaceId(this Person self)
        {
            return self == null ? -1 : self.birthplace;
        }

        /// <summary>势力颜色</summary>
        public static int GetColor(this Person self)
        {
            return self == null ? 0 : DuelSettings.GetForceColor(self);
        }

        /// <summary>获取指定能力值</summary>
        public static int GetStat(this Person self, PersonStatType type)
        {
            if (self == null) return 0;
            switch (type)
            {
                case PersonStatType.Strength: return self.Strength;
                case PersonStatType.Command: return self.Command;
                case PersonStatType.Intelligence: return self.Intelligence;
                case PersonStatType.Politics: return self.Politics;
            }
            return 0;
        }

        /// <summary>按伤病计算后的能力值</summary>
        public static int CalcStat(this Person self, PersonStatType type, int shoubyou)
        {
            if (self == null) return 0;
            if (type == PersonStatType.Strength && DuelSettings.CalcStrength != null)
                return DuelSettings.CalcStrength(self, shoubyou);
            return self.GetStat(type);
        }

        /// <summary>是否拥有指定特技</summary>
        public static bool HasSkill(this Person self, SkillId skill)
        {
            if (self == null) return false;
            switch (skill)
            {
                case SkillId.Kyouun:
                    return DuelFeatureId.Kyouun >= 0 && self.HasFeatrue(DuelFeatureId.Kyouun);
            }
            return false;
        }

        /// <summary>是否玩家武将</summary>
        public static bool IsPlayer(this Person self)
        {
            return self != null && (self.IsPlayer || self.IsPlayerControl);
        }

        /// <summary>是否君主</summary>
        public static bool IsKunshu(this Person self)
        {
            return self != null && self.state == (int)PersonStateType.Governor;
        }

        /// <summary>是否为血亲 / 配偶 / 义兄弟（用于一击必杀与俘虏判定的豁免）</summary>
        public static bool IsFamily(this Person self, Person other)
        {
            if (self == null || other == null || self == other) return false;
            if (self.IsParentchild(other)) return true;
            if (self.IsSpouse(other)) return true;
            if (self.IsGikyoudai(other)) return true;
            return false;
        }

        /// <summary>对方是否为配偶</summary>
        public static bool IsSpouse(this Person self, Person other)
        {
            if (self == null || other == null) return false;
            if (self.mSpouseList != null && self.mSpouseList.objects != null && self.mSpouseList.objects.Contains(other))
                return true;
            if (other.mSpouseList != null && other.mSpouseList.objects != null && other.mSpouseList.objects.Contains(self))
                return true;
            return false;
        }

        /// <summary>对方是否为义兄弟</summary>
        public static bool IsGikyoudai(this Person self, Person other)
        {
            if (self == null || other == null) return false;
            return self.IsBrother(other);
        }

        /// <summary>对方是否为血亲</summary>
        public static bool IsKetsuen(this Person self, Person other)
        {
            if (self == null || other == null) return false;
            if (self.consanguinity > 0 && self.consanguinity == other.consanguinity) return true;
            return self.IsParentchild(other);
        }

        /// <summary>与对方的相性距离（0~150，越小越亲近）</summary>
        public static int GetAishouDistance(this Person self, Person other)
        {
            if (self == null || other == null) return 75;
            return self.CompatibilityDistance(other);
        }
    }

    #endregion
}
