/*
 * 文件名：DebateExternal.cs
 * 描述：舌战(Debate)系统所依赖的外部类型与工具
 *
 * 【重要说明】
 *   · Person 已改为直接使用游戏的 Sango.Core.Person，本文件不再定义同名类。
 *     舌战所需的额外访问器由 DebatePersonAdapter.cs 的扩展方法补齐。
 *   · 其余类型（Force / District / Item / MilitaryUnitObject / Engine / GameSystem / Message 等）
 *     均为"接口存根"，仅保留舌战系统调用到的接口签名。接入真实游戏时把它们桥接到项目业务类即可，
 *     无需改动 Debate.cs / DebateAI.cs / DebatePhase.cs 中的任何舌战逻辑。
 *   · 全局设置集中在 DebateSettings（见 DebatePersonAdapter.cs）。
 *
 *   注意：这些存根定义在 Sango.Core.Debate 命名空间下，会优先于外层 Sango.Core 同名类型被解析；
 *         接入完成后建议直接删除对应存根，改用项目真实类型。
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

    /// <summary>地图坐标（16 位）。对应 C++ point16</summary>
    public struct Point16
    {
        public short x;
        public short y;

        public Point16(short x, short y) { this.x = x; this.y = y; }

        /// <summary>无效坐标。对应 C++ NullPos</summary>
        public static Point16 NullPos { get { return new Point16(-1, -1); } }
    }

    #endregion

    #region 消息文本

    /// <summary>
    /// 消息对象。对应 C++ Message。
    /// 接入时可桥接到项目自身的文本格式化系统。
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

    /// <summary>
    /// 默认日志实现：转发到项目的 <see cref="Sango.Log"/>（级别 Info）。
    /// 舌战的所有过程日志都会经由这里输出到 Unity 控制台。
    /// </summary>
    public class SangoLogger : Logger
    {
        public void Debug(string text)
        {
            Sango.Log.Info(text, Sango.Log.LogType.Game);
        }
    }

    #endregion

    #region 武将 / 势力 / 地区 / 宝物 等外部对象存根

    // 注意：舌战系统直接使用的 Person 为游戏真实类型 Sango.Core.Person（本命名空间未再定义同名类，
    //       因此此处的 Person 会解析到外层 Sango.Core.Person）。
    //       舌战所需的额外访问器由 DebatePersonAdapter.cs 以扩展方法形式补齐。

    /// <summary>宝物。对应 C++ Item</summary>
    public class Item : IAlive
    {
        public bool IsAlive { get; set; } = true;

        /// <summary>宝物大类</summary>
        public virtual ItemType GetTypeValue() { return ItemType.ItemType_None; }
    }

    /// <summary>势力。对应 C++ Force</summary>
    public class Force : IAlive
    {
        public bool IsAlive { get; set; } = true;

        /// <summary>势力 ID</summary>
        public virtual int GetId() { return -1; }

        /// <summary>是否为玩家势力</summary>
        public virtual bool IsPlayer() { return false; }
    }

    /// <summary>地区（都市）。对应 C++ District</summary>
    public class District : IAlive
    {
        public bool IsAlive { get; set; } = true;

        /// <summary>是否为玩家所有</summary>
        public virtual bool IsPlayer() { return false; }

        /// <summary>地区编号</summary>
        public virtual int GetNumber() { return 0; }
    }

    /// <summary>部队（六边形格上的军事单位）。对应 C++ MilitaryUnitObject</summary>
    public class MilitaryUnitObject : IAlive
    {
        public bool IsAlive { get; set; } = true;

        /// <summary>获取所在坐标</summary>
        public virtual Point16 GetPos() { return Point16.NullPos; }
    }

    #endregion

    #region 引擎（表现层）接口存根

    /// <summary>
    /// 表现层引擎接口。对应 C++ Engine。
    /// 舌战逻辑通过这些方法驱动动画/UI；在无表现层（纯逻辑推演）时，Debate.view 为 false，
    /// 这些方法不会被调用，由 Debate 内部直接结算数值。
    /// </summary>
    public class Engine
    {
        /// <summary>播放开场</summary>
        public virtual void DebateOpening(Debate debate) { }

        /// <summary>播放一击必杀</summary>
        public virtual void DebateFtk(Debate debate) { }

        /// <summary>愤怒计时结束时回调</summary>
        public virtual void DebateAngerEnd(Debate debate, int team) { }

        /// <summary>播放平局</summary>
        public virtual void DebateAttackDraw(Debate debate, int stressDamage) { }

        /// <summary>播放大喝</summary>
        public virtual void DebateShout(Debate debate, int team, int hpDamage, int stressDamage) { }

        /// <summary>播放话题卡效果</summary>
        public virtual void DebateTopic(Debate debate, int team, int card, int hpDamage, int stressDamage, bool reflected) { }

        /// <summary>播放再考</summary>
        public virtual void DebateRethink(Debate debate, int team) { }

        /// <summary>播放无视</summary>
        public virtual void DebateIgnore(Debate debate, int team, int stressDamage) { }

        /// <summary>播放镇静</summary>
        public virtual void DebateCompose(Debate debate, int team, int stressDamage, bool reflected) { }

        /// <summary>播放激昂</summary>
        public virtual void DebateAgitate(Debate debate, int team, int stressDamage, bool reflected) { }

        /// <summary>愤怒触发</summary>
        public virtual void DebateAngerTrigger(Debate debate, int team, int card) { }

        /// <summary>播放猪突愤怒</summary>
        public virtual void DebateAngerReckless(Debate debate, int team, int hpDamage, int stressDamage) { }

        /// <summary>播放小心的连续攻击</summary>
        public virtual void DebateAngerTimid(Debate debate, int team, int hpDamage, int stressDamage, int comboIndex) { }

        /// <summary>播放出牌</summary>
        public virtual void DebatePlayCard(Debate debate, int team, int index) { }

        /// <summary>播放结束</summary>
        public virtual void DebateClosing(Debate debate) { }

        /// <summary>获取玩家选择的卡牌下标</summary>
        public virtual int DebateSelectCard(Debate debate, int team) { return -1; }

        /// <summary>获取玩家选择的会心类型</summary>
        public virtual int DebateSelectCritical(Debate debate, int team) { return -1; }

        /// <summary>弹出是否确认对话框</summary>
        public virtual bool YesNo(string text) { return true; }
    }

    #endregion

    #region 系统（业务层）接口存根

    /// <summary>
    /// 系统（业务层）。对应 C++ System。
    /// 提供随机源、消息、日志以及舌战结算所需的各类游戏操作。
    /// 说明：C++ 中该类名为 System，与 .NET 的 System 命名空间冲突，故重命名为 GameSystem。
    /// </summary>
    public class GameSystem
    {
        private Engine m_engine;

        public GameSystem() { }
        public GameSystem(Engine engine) { m_engine = engine; }

        /// <summary>获取表现层引擎</summary>
        public virtual Engine GetEngine() { return m_engine; }

        /// <summary>默认日志实现（转发到 Sango.Log）</summary>
        private static readonly Logger s_defaultLogger = new SangoLogger();
        private Logger m_logger = s_defaultLogger;

        /// <summary>获取日志对象；关闭日志开关时返回 null</summary>
        public virtual Logger GetLogger() { return DebateSettings.EnableLog ? m_logger : null; }

        /// <summary>设置日志对象</summary>
        public virtual void SetLogger(Logger logger) { m_logger = logger; }

        /// <summary>获取当前随机种子</summary>
        public virtual int GetSeed() { return DebateRandom.GetSeed(); }

        /// <summary>返回 [0, max) 的随机整数</summary>
        public virtual int RandInt(int max) { return DebateRandom.Range(max); }

        /// <summary>以 percent% 的概率返回 true</summary>
        public virtual bool RandBool(int percent) { return DebateRandom.Chance(percent); }

        /// <summary>功能是否被禁用</summary>
        public virtual bool IsFeatDisabled(Feature feature) { return false; }

        /// <summary>获取势力</summary>
        public virtual Force GetForce(int forceId) { return null; }

        /// <summary>获取地区</summary>
        public virtual District GetDistrict(int districtId) { return null; }

        /// <summary>获取武将持有的宝物列表</summary>
        public virtual List<Item> GetPersonItemList(Person person) { return new List<Item>(); }

        /// <summary>获取坐标对象</summary>
        public virtual MilitaryUnitObject GetLocationObject(int locationId) { return null; }

        /// <summary>获取消息文本</summary>
        public virtual string GetMessage(Message msg) { return string.Empty; }

        /// <summary>获取话题名称（用于日志）</summary>
        public virtual string GetTopicName(int topic) { return topic.ToString(); }

        /// <summary>显示消息</summary>
        public virtual void Message(Message msg, object target, object[] args, bool pause) { }

        /// <summary>写入历史日志</summary>
        public virtual void HistoryLog(Point16 pos, int color, string text, bool show) { }

        /// <summary>播放音效</summary>
        public virtual void PlaySe(int seId) { }

        /// <summary>设置武将伤病</summary>
        public virtual void PersonSetInjury(Person person, int injury) { }

        /// <summary>增减武将能力经验</summary>
        public virtual void PersonAddStatExp(Person person, PersonStatType type, int value, bool show) { }

        /// <summary>增减武将功绩</summary>
        public virtual void PersonAddMerit(Person person, int value) { }

        /// <summary>增减势力技术点</summary>
        public virtual void ForceAddTechPoint(Force force, int value, object unit) { }
    }

    #endregion
}
