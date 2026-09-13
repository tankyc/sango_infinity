/*
 * 文件名：DebatePersonAdapter.cs
 * 描述：舌战系统与游戏真实 Person（Sango.Core.Person）之间的适配层
 *
 * 设计思路：
 *   舌战系统内部一律使用 Sango.Core.Person。真实 Person 没有提供舌战所需的全部方法，
 *   因此这里用【扩展方法】补齐，舌战本体代码（Debate.cs / DebateAI.cs / DebatePhase.cs）无需大改
 *   即可直接使用真实武将。
 *
 *   注意：真实 Person 已有的实例方法（IsLike / IsHate / IsBrother / IsParentchild 等）
 *        优先级高于扩展方法，因此舌战里调用它们时会自动走游戏自带实现。
 *
 * 接入时需要调整的项（都在本文件，集中在 DebateSettings / DebatePersonality）：
 *   1. DebateSettings.HasRhetoric  - 武将是否拥有第 i 个话术（对应 C++ Person::has_rhetoric）
 *   2. DebateSettings.GetLocationId - 武将所在位置（部队）ID，用于历史记录定位
 *   3. DebatePersonality             - 性格转换（默认由 personality 值 1~4 直接映射）
 */

using System;
using System.Collections.Generic;

namespace Sango.Core.Debate
{
    #region 全局设置 / 钩子

    /// <summary>
    /// 舌战系统所需的全局设置与外部依赖钩子。
    /// 接入真实游戏时，按需给这些字段/委托赋值即可，无需改动舌战本体。
    /// </summary>
    public static class DebateSettings
    {
        /// <summary>是否输出舌战过程日志（默认开启，输出到 Sango.Log）</summary>
        public static bool EnableLog = true;

        /// <summary>武将是否拥有指定话术（i 与 Rhetoric 枚举一致）。对应 C++ Person::has_rhetoric</summary>
        public static Func<Person, int, bool> HasRhetoric = (person, index) => false;

        /// <summary>获取武将所在位置（部队）ID，用于历史记录定位。默认 -1（无位置）</summary>
        public static Func<Person, int> GetLocationId = person => -1;

        /// <summary>获取势力颜色（用于历史日志着色），默认 0</summary>
        public static Func<Person, int> GetForceColor = person => 0;

        /// <summary>百分概率判定，默认走 DebateRandom</summary>
        public static Func<int, bool> RandBool = DebateRandom.Chance;

        /// <summary>[0, max) 随机整数，默认走 DebateRandom</summary>
        public static Func<int, int> RandInt = DebateRandom.Range;

        /// <summary>恢复为默认实现</summary>
        public static void Reset()
        {
            HasRhetoric = (person, index) => false;
            GetLocationId = person => -1;
            GetForceColor = person => 0;
            RandBool = DebateRandom.Chance;
            RandInt = DebateRandom.Range;
        }
    }

    #endregion

    #region 性格解析

    /// <summary>
    /// 把真实武将的 personality(性格) 转换为舌战性格。
    ///
    /// 项目内 Personalities 数据（Build/Content/Data/Common/Personalities.json）与舌战性格一一对应：
    ///   kind = 1 胆小 → Personality_Timid（小心）
    ///   kind = 2 冷静 → Personality_Calm （冷静）
    ///   kind = 3 刚胆 → Personality_Bold  （大胆）
    ///   kind = 4 莽撞 → Personality_Reckless（猪突）
    /// 因此 kind / personality 减 1 即为 Personality 枚举值，无需额外配置。
    ///
    /// 若贵项目改动过性格数据，可用 PersonalityMap 覆盖，或替换 Resolver 委托。
    /// </summary>
    public static class DebatePersonality
    {
        /// <summary>性格数量（对应 Personalities 的 1~4）</summary>
        public const int Count = 4;

        /// <summary>
        /// 自定义覆盖：personality 值 → 舌战性格。
        /// 优先级最高，仅在贵项目性格数据与默认值不一致时才需要填写。
        /// </summary>
        public static readonly Dictionary<int, int> PersonalityMap = new Dictionary<int, int>();

        /// <summary>兜底性格（数据缺失时使用）</summary>
        public static int Fallback = (int)Personality.Personality_Calm;

        /// <summary>自定义解析委托，置空则使用内置转换</summary>
        public static Func<Person, int> Resolver = null;

        /// <summary>解析武将性格</summary>
        public static int Resolve(Person person)
        {
            if (person == null) return Fallback;

            if (Resolver != null)
                return Resolver(person);

            // 优先用性格对象上的 kind（数据里 kind 与 Id 一致，语义更准确）
            int kind = person.mPersonality != null ? person.mPersonality.kind : 0;
            if (kind <= 0)
                kind = person.personality;

            if (PersonalityMap.TryGetValue(kind, out int mapped))
                return mapped;

            if (kind >= 1 && kind <= Count)
                return kind - 1;

            return Fallback;
        }
    }

    #endregion

    #region Person 扩展方法

    /// <summary>
    /// 为 Sango.Core.Person 补齐舌战所需的访问器。
    /// 舌战本体通过这些扩展方法读写武将，从而与游戏真实武将无缝对接。
    /// </summary>
    public static class PersonDebateExtensions
    {
        /// <summary>姓名</summary>
        public static string GetName(this Person self)
        {
            return self == null ? string.Empty : self.Name;
        }

        /// <summary>获取指定能力值</summary>
        public static int GetStat(this Person self, PersonStatType type)
        {
            if (self == null) return 0;
            switch (type)
            {
                case PersonStatType.PersonStatType_Strength: return self.Strength;
                case PersonStatType.PersonStatType_Intelligence: return self.Intelligence;
                case PersonStatType.PersonStatType_Command: return self.Command;
                case PersonStatType.PersonStatType_Politics: return self.Politics;
            }
            return 0;
        }

        /// <summary>舌战性格</summary>
        public static int GetPersonality(this Person self)
        {
            return DebatePersonality.Resolve(self);
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

        /// <summary>所在位置（部队）ID</summary>
        public static int GetLocationId(this Person self)
        {
            if (self == null) return -1;
            return DebateSettings.GetLocationId(self);
        }

        /// <summary>势力颜色</summary>
        public static int GetColor(this Person self)
        {
            return self == null ? 0 : DebateSettings.GetForceColor(self);
        }

        /// <summary>伤病程度（0=健康，越大越重）</summary>
        public static int GetInjury(this Person self)
        {
            return self == null ? -1 : self.injury;
        }

        /// <summary>是否拥有指定话术</summary>
        public static bool HasRhetoric(this Person self, int index)
        {
            if (self == null) return false;
            return DebateSettings.HasRhetoric(self, index);
        }

        /// <summary>是否玩家控制</summary>
        public static bool IsPlayerControlled(this Person self)
        {
            return self != null && (self.IsPlayer || self.IsPlayerControl);
        }
    }

    #endregion
}
