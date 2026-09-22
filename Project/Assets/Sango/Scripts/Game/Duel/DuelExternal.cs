/*
 * 文件名：DuelExternal.cs
 * 描述：单挑(Duel)系统所依赖的外部类型与工具
 *
 * 【重要说明】
 *   · Person 已改为直接使用游戏的 Sango.Core.Person，本文件不再定义同名类。
 *     单挑所需的额外访问器由 DuelPersonAdapter.cs 的扩展方法补齐。
 *   · 其余类型（Unit / Force / District / Item / Engine / GameSystem / Message 等）均为"接口存根"，
 *     仅保留单挑系统调用到的接口签名。接入真实游戏时把它们桥接到项目业务类即可，
 *     无需改动 Duel.cs / DuelAI.cs / DuelPhase.cs 中的任何单挑逻辑。
 *   · 难度 / 寿命 / 功能开关 / 宝物 / 武将ID解析 等全局配置集中在 DuelSettings（DuelPersonAdapter.cs）。
 *
 *   注意：这些存根定义在 Sango.Core.Duel 命名空间下，会优先于外层 Sango.Core 同名类型被解析。
 *         接入完成后建议直接删除对应存根，改用项目真实类型。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core.Duel
{
    #region 基础工具

    /// <summary>32 位位集合，对应 C++ 的 bitset4&lt;N&gt;（以 4 字节存储的位图）</summary>
    public struct Bitset32
    {
        public uint Value;

        public Bitset32(uint value) { Value = value; }

        /// <summary>以若干位序号构造位集合</summary>
        public Bitset32(params int[] bits)
        {
            Value = 0;
            if (bits == null) return;
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i] >= 0) Value |= 1u << bits[i];
            }
        }

        /// <summary>获取/设置指定位</summary>
        public bool this[int bit]
        {
            get
            {
                if (bit < 0 || bit >= 32) return false;
                return (Value & (1u << bit)) != 0;
            }
            set
            {
                if (bit < 0 || bit >= 32) return;
                if (value) Value |= 1u << bit;
                else Value &= ~(1u << bit);
            }
        }

        /// <summary>是否为空</summary>
        public bool IsEmpty { get { return Value == 0; } }

        /// <summary>是否与 other 有交集</summary>
        public bool Intersects(Bitset32 other) { return (Value & other.Value) != 0; }

        public static Bitset32 Empty { get { return new Bitset32(0u); } }
    }

    /// <summary>通用工具函数，对应 C++ 的 utils 命名空间与部分 std 算法</summary>
    public static class Utils
    {
        /// <summary>判断对象是否有效（非 null 且存活）。对应 utils::is_alive / utils::is_active</summary>
        public static bool IsAlive(object obj)
        {
            if (obj == null) return false;
            if (obj is IAlive a) return a.IsAlive;
            // 游戏真实武将：已死亡即视为无效
            if (obj is Person p) return p.state != (int)PersonStateType.Dead;
            return true;
        }

        /// <summary>同 IsAlive，语义上用于"数据有效"判断</summary>
        public static bool IsActive(object obj) { return IsAlive(obj); }

        /// <summary>判断 value 是否落在 [low, high] 闭区间内。对应 utils::in_range</summary>
        public static bool InRange(int value, int low, int high)
        {
            return value >= low && value <= high;
        }

        /// <summary>将 flags 中第 bit 位置 1。对应 utils::set_bits</summary>
        public static void SetBits(ref int flags, int bit)
        {
            if (bit < 0 || bit >= 32) return;
            flags |= 1 << bit;
        }

        /// <summary>清除 flags 中第 bit 位</summary>
        public static void ClearBits(ref int flags, int bit)
        {
            if (bit < 0 || bit >= 32) return;
            flags &= ~(1 << bit);
        }

        /// <summary>判断 flags 中第 bit 位是否为 1</summary>
        public static bool HasBits(int flags, int bit)
        {
            if (bit < 0 || bit >= 32) return false;
            return (flags & (1 << bit)) != 0;
        }

        /// <summary>对应 std::clamp</summary>
        public static int Clamp(int value, int low, int high)
        {
            if (value < low) return low;
            if (value > high) return high;
            return value;
        }

        /// <summary>对应 std::ranges::contains（用于定长数组）</summary>
        public static bool Contains<T>(T[] array, T value)
        {
            if (array == null) return false;
            return Array.IndexOf(array, value) >= 0;
        }
    }

    /// <summary>存活标记接口，供 Utils.IsAlive 使用</summary>
    public interface IAlive
    {
        bool IsAlive { get; }
    }

    // 随机数发生器 DuelRandom 已迁移至 DuelPersonAdapter.cs（默认转发到项目的 GameRandom）

    #endregion

    #region 武将 / 部队 / 势力 等相关枚举（外部依赖）

    // 武将身份统一用**真实 Id**（Person.Id，与 PersonLibrary.json 里的 Id 一致），
    // 原来那套 PersonId 枚举（只有 10 个名将 + 靠姓名解析）已删除：它既是 PersonLibrary 的重复副本，
    // 又让"没登记的武将"拿不到正确身份。相关解析器 DuelPersonId 一并删除。

    /// <summary>宝物大类（Item::get_type）</summary>
    public enum ItemType
    {
        None = 0,
        EliteHorse = 1,   // 名马
        Sword = 2,        // 剑
        LongSpear = 3,    // 长武器
        ThrowingKnife = 4,// 暗器
        Bow = 5,          // 弓
    }

    /// <summary>宝物 ID（Item::get_id）</summary>
    public enum ItemId
    {
        Invalid = -1,
        SerpentBlade = 0,    // 蛇矛
        BlueDragon = 1,      // 青龙偃月刀
        CrescentHalberd = 2, // 方天画戟
    }

    /// <summary>特技 ID</summary>
    public enum SkillId
    {
        None = 0,
        Lucky = 1, // 强运
    }

    /// <summary>性格</summary>
    public enum DuelPersonality
    {
        Timid = 0, // 小心
        Calm = 1,   // 冷静
        Bold = 2,   // 大胆
        Reckless = 3, // 猪突
    }

    /// <summary>伤病程度</summary>
    public enum InjuryLevel
    {
        Healthy = 0, // 健康
        NearDeath = 3, // 濒死（原代码中用于上限判断）
        Max = 4,
    }

    /// <summary>功能开关</summary>
    public enum Feature
    {
        DuelAIRetreat = 0,     // AI 退却
        DuelFirstTurnKill = 1, // 一击必杀（初回合）
        Capture = 2,            // 捕缚
    }

    /// <summary>难度</summary>
    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    /// <summary>寿命模式</summary>
    public enum LifeMode
    {
        Normal = 0,
        Virtual = 1, // 假想模式
    }

    /// <summary>战死频率</summary>
    public enum BattleDeathMode
    {
        None = 0,
        Normal = 1,
        High = 2,
    }

    /// <summary>武将能力类型</summary>
    public enum PersonStatType
    {
        Strength = 0, // 武力
        Intelligence = 1,
        Command = 2,
        Politics = 3,
    }

    /// <summary>浮动数值类型</summary>
    public enum FloatingCounterType
    {
        Energy = 0,  // 士气
        Troops = 1,  // 兵力
    }

    /// <summary>提示类型</summary>
    public enum PingType
    {
        Normal = 0,
        Repeat = 1,
    }

    /// <summary>死亡类型</summary>
    public enum DeathType
    {
        Natural = 0,
    }

    /// <summary>场景</summary>
    public enum Scene
    {
        Scene_Max = -1,
    }

    #endregion

    #region 消息文本 ID

    /// <summary>单挑相关消息文本 ID</summary>
    public enum DuelMessageId
    {
        LD_WAR_IKKI_INJURY,     // 负伤
        N_WAR_IKKI_INJURY,
        F_WAR_IKKI_HIKIWAKE_A,  // 平局
        F_WAR_IKKI_ATO_A,       // 逃走（胜方视角）
        F_WAR_IKKI_ATO_B,       // 逃走（败方视角）
        F_WAR_IKKI_HORYO_A,     // 俘虏（胜方视角）
        F_WAR_IKKI_HORYO_B,     // 俘虏（败方视角）
        F_WAR_IKKI_SHIBOU,      // 死亡
        LB_WAR_IKKI_WIN,        // 胜利
        LD_WAR_IKKI_LOST,       // 败北
        N_WAR_IKKI_KAKUNIN,     // 是否进入单挑的确认框
    }

    #endregion

    #region 外部对象存根

    /// <summary>
    /// 消息对象。对应 C++ Message。
    /// 接入时可桥接到项目自身的文本格式化系统。
    /// </summary>
    public class Message
    {
        public DuelMessageId Id;
        public object[] Objs = new object[8];
        public string Str0;
        public int Num0;
        public int Num1;

        public void SetObj0(DuelMessageId id, object obj0) { Id = id; Objs[0] = obj0; }
        public void SetObj0Str0(DuelMessageId id, object obj0, string str0) { Id = id; Objs[0] = obj0; Str0 = str0; }
        public void SetObj0Obj1(DuelMessageId id, object obj0, object obj1) { Id = id; Objs[0] = obj0; Objs[1] = obj1; }

        public void SetObj0Obj1Obj2Obj3Obj4Obj5Num0Num1(
            DuelMessageId id, object obj0, object obj1, object obj2, object obj3, object obj4, object obj5, int num0, int num1)
        {
            Id = id;
            Objs[0] = obj0; Objs[1] = obj1; Objs[2] = obj2;
            Objs[3] = obj3; Objs[4] = obj4; Objs[5] = obj5;
            Num0 = num0; Num1 = num1;
        }
    }

    /// <summary>玩家数量上限。对应 C++ Player_Max</summary>
    public static class DuelPlayer
    {
        public const int Max = 8;
    }

    /// <summary>日志接口。对应 C++ Logger</summary>
    public interface Logger
    {
        void Debug(string text);
    }

    // 注意：单挑系统直接使用的 Person 为游戏真实类型 Sango.Core.Person（本命名空间未再定义同名类，
    //       因此此处的 Person 会解析到外层 Sango.Core.Person）。
    //       单挑所需的额外访问器由 DuelPersonAdapter.cs 以扩展方法形式补齐。
    //
    //       原先的 Game / Scenario 存根已移除，相关全局设置改为 DuelSettings（见 DuelPersonAdapter.cs）。

    #endregion

}
