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
 * 说明：本文件只保留"真实 Person 上没有、或语义需要翻译"的访问器：
 *   · 话术    → Person.wordTac（舌战话术索引数组，对应 C++ Person::has_wajutsu）
 *   · 地区 ID → 工程的军团 Corps（Person.mBelongCorps）
 *   · 位置 ID → 武将所在部队 Troop（Person.mTroop）
 *   · 性格    → DebatePersonality（真实 personality 1~4 → 舌战 胆小/冷静/刚胆/莽撞）
 * 其余（姓名 / 能力 / 势力 / 伤病 / 是否玩家操控）直接读真实 Person 的成员。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core.Debate
{
    #region 全局设置 / 钩子

    /// <summary>舌战系统全局设置</summary>
    public static class DebateSettings
    {
        /// <summary>是否输出舌战过程日志（默认开启，输出到 Sango.Log）</summary>
        public static bool EnableLog = true;
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

        /// <summary>
        /// 所在军团（Corps）ID。对应 C++ 的 district_id ——
        /// 工程里"地区"由军团承担（Corps.IsPlayerControl 等价于 C++ district.is_player() &amp;&amp; get_number()==1）。
        /// </summary>
        public static int GetDistrictId(this Person self)
        {
            if (self == null) return -1;
            return self.mBelongCorps != null ? self.mBelongCorps.Id : -1;
        }

        /// <summary>所在位置（部队 Troop）ID。对应 C++ 的 location_id，用于战报定位</summary>
        public static int GetLocationId(this Person self)
        {
            if (self == null) return -1;
            return self.mTroop != null ? self.mTroop.Id : -1;
        }

        /// <summary>伤病程度（0=健康，越大越重）</summary>
        public static int GetInjury(this Person self)
        {
            return self == null ? -1 : self.injury;
        }

        /// <summary>
        /// 是否拥有指定话术（index 与 Rhetoric 枚举一致：0=大喝 / 1=诡辩 / 2=无视 / 3=镇静 / 4=激昂）。
        /// 对应 C++ Person::has_wajutsu，数据源为 Person.wordTac（舌战话术索引数组）。
        /// </summary>
        public static bool HasRhetoric(this Person self, int index)
        {
            if (self == null || self.wordTac == null) return false;
            for (int i = 0; i < self.wordTac.Length; i++)
            {
                if (self.wordTac[i] == index)
                    return true;
            }
            return false;
        }

        /// <summary>是否玩家控制</summary>
        public static bool IsPlayerControlled(this Person self)
        {
            return self != null && (self.IsPlayer || self.IsPlayerControl);
        }
    }

    #endregion
}
