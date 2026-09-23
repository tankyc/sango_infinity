/*
 * 文件名：DebateExternal.cs
 * 描述：舌战(Debate)系统所依赖的外部工具类型
 *
 * 【重要说明】本文件只保留"工程里没有等价物"的通用工具，业务相关类型一律直连工程真实实现：
 *   · Person  → 直接使用 Sango.Core.Person，额外访问器见 DebatePersonAdapter.cs
 *   · Force   → 直接使用 Sango.Core.Force（本命名空间不再定义同名存根，否则会遮蔽真实类型）
 *   · Troop   → 直接使用 Sango.Core.Troop（对应 C++ MilitaryUnitObject）
 *   · Corps   → 直接使用 Sango.Core.Corps（对应 C++ District）
 *   · Item    → 暂不接入，书籍判定预留为 DebateGameSystem.HasAllRhetoric
 *   · 业务层   → DebateGameSystem（见 DebateGameSystem.cs）
 *   · 表现层   → IDebateView / DebateViewBase（见 DebateView.cs）
 *
 * 保留在这里的：
 *   Utils      —— C++ utils:: 命名空间的等价工具（is_alive / in_range / clamp / swap）
 *   IAlive     —— 供 Utils.IsAlive 使用的存活标记接口
 *   DebateRandom —— 随机数发生器，默认转发到工程的 GameRandom
 *   Message / Logger —— 消息对象与日志接口（日志统一走 Sango.Log，Logger 仅留给测试注入）
 */

using System;
using System.Collections.Generic;

namespace Sango.Core.Debate
{
    #region 基础工具

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
            // 部队/军团等真实类型：直接读它们的 IsAlive 属性
            if (obj is Troop t) return t.IsAlive;
            if (obj is Corps c) return c.IsAlive;
            if (obj is Force f) return f.IsAlive;
            return true;
        }

        /// <summary>同 IsAlive，语义上用于"数据有效"判断</summary>
        public static bool IsActive(object obj) { return IsAlive(obj); }

        /// <summary>判断 value 是否落在 [low, high] 闭区间内。对应 utils::in_range</summary>
        public static bool InRange(int value, int low, int high)
        {
            return value >= low && value <= high;
        }

        /// <summary>对应 std::clamp</summary>
        public static int Clamp(int value, int low, int high)
        {
            if (value < low) return low;
            if (value > high) return high;
            return value;
        }

        /// <summary>交换两个值。对应 std::swap</summary>
        public static void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }
    }

    /// <summary>存活标记接口，供 Utils.IsAlive 使用</summary>
    public interface IAlive
    {
        bool IsAlive { get; }
    }

    /// <summary>随机数发生器。默认转发到项目的 GameRandom。</summary>
    public static class DebateRandom
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

    #region 消息文本

    /// <summary>
    /// 消息对象。对应 C++ Message。
    /// 文本模板见 DebateMessageText（DebateGameSystem.cs），渲染由业务层 GetMessage 完成。
    /// </summary>
    public class Message
    {
        public DebateMessageId Id;
        public object[] Objs = new object[8];
        public string Str0;
        public int Num0;
        public int Num1;

        public void SetObj0(DebateMessageId id, object obj0) { Id = id; Objs[0] = obj0; }
        public void SetObj0Obj1(DebateMessageId id, object obj0, object obj1) { Id = id; Objs[0] = obj0; Objs[1] = obj1; }
    }

    /// <summary>日志接口。对应 C++ Logger</summary>
    public interface Logger
    {
        void Debug(string text);
    }

    // 舌战不再自带日志实现类：过程日志由 Debate.LogDebate 直接走项目的 Sango.Log
    //（Sango.Log 已由编译期开关 SANGO_DEBUG 统一控制，未定义时连实参求值都会被删掉）。
    // Logger 接口只保留给测试用例注入捕获器（见 DebateTestLogger / SetLogger）。

    #endregion
}
