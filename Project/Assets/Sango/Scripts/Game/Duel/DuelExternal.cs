/*
 * 文件名：DuelExternal.cs
 * 描述：单挑(Duel)系统所依赖的外部类型与工具
 *
 * 【重要说明】
 *   本文件中的类型均为"接口存根"，仅保留单挑系统调用到的接口签名，不包含具体实现。
 *   这是翻译 C++ 单挑系统时遇到的、本项目尚未提供的未知类型（或接口不匹配的类型）。
 *   接入真实游戏时，只需把这些接口桥接到项目已有的 Person / Troop / Force 等业务类即可，
 *   无需改动 Duel.cs / DuelAI.cs / DuelPhase.cs 中的任何单挑逻辑。
 *
 *   注意：这些类型定义在 Sango.Core.Duel 命名空间下，会优先于外层 Sango.Core 同名类型被解析，
 *         因此不会影响项目既有的 Person / DuelSystem 等类。
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

    /// <summary>
    /// 单挑随机数发生器。
    /// 反编译代码中所有随机均来自 system 的种子化随机源，为保证单挑可复现，此处独立提供。
    /// </summary>
    public static class DuelRandom
    {
        private static Random s_random = new Random(Environment.TickCount);
        private static int s_seed = 0;

        /// <summary>设置随机种子（对应 system 的种子化随机源）</summary>
        public static void SetSeed(int seed)
        {
            s_seed = seed;
            s_random = new Random(seed);
        }

        /// <summary>获取当前种子。对应 System::get_seed</summary>
        public static int GetSeed() { return s_seed; }

        /// <summary>返回 [0, max) 的随机整数。对应 System::rand_int</summary>
        public static int Range(int max)
        {
            if (max <= 0) return 0;
            lock (s_random) { return s_random.Next(max); }
        }

        /// <summary>以 percent% 的概率返回 true。对应 System::rand_bool</summary>
        public static bool Chance(int percent)
        {
            if (percent <= 0) return false;
            if (percent >= 100) return true;
            return Range(100) < percent;
        }
    }

    #endregion

    #region 武将 / 部队 / 势力 等相关枚举（外部依赖）

    /// <summary>武将 ID</summary>
    public enum PersonId
    {
        Invalid = -1,
        Ryofu = 0,           // 吕布
        Chouhi = 1,          // 张飞
        Kanu = 2,            // 关羽
        Kyocho = 3,          // 许褚
        Chouun = 4,          // 赵云
        Bachou = 5,          // 马超
        Kouchuu_Kanshou = 6, // 黄忠
        Kakouen = 7,         // 夏侯渊
        Ousou = 8,           // 黄盖
        Shukuyuu = 9,        // 周瑜
    }

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
        Kyouun = 1, // 强运
    }

    /// <summary>性格</summary>
    public enum Seikaku
    {
        Shoushin = 0, // 小心
        Reisei = 1,   // 冷静
        Goutan = 2,   // 大胆
        Chototsu = 3, // 猪突
    }

    /// <summary>伤病程度</summary>
    public enum Shoubyou
    {
        Kenkou = 0, // 健康
        Hinshi = 3, // 濒死（原代码中用于上限判断）
        Max = 4,
    }

    /// <summary>功能开关</summary>
    public enum Feature
    {
        DuelAIRetreat = 0,     // AI 退却
        DuelFirstTurnKill = 1, // 一击必杀（初回合）
        Hobaku = 2,            // 捕缚
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
    public static class Player
    {
        public const int Max = 8;
    }

    /// <summary>日志接口。对应 C++ Logger</summary>
    public interface Logger
    {
        void Debug(string text);
    }

    /// <summary>游戏全局对象。对应 C++ Game</summary>
    public class Game
    {
        /// <summary>功能是否被禁用。对应 Game::is_feat_disabled</summary>
        public virtual bool IsFeatDisabled(Feature feature) { return false; }

        /// <summary>获取武将持有的宝物列表。对应 Game::get_person_item_list</summary>
        public virtual List<Item> GetPersonItemList(Person person) { return new List<Item>(); }
    }

    /// <summary>剧本。对应 C++ Scenario</summary>
    public class Scenario
    {
        /// <summary>获取游戏全局对象</summary>
        public virtual Game GetGame() { return null; }

        /// <summary>以 percent% 的概率返回 true</summary>
        public virtual bool RandBool(int percent) { return DuelRandom.Chance(percent); }

        /// <summary>获取难度</summary>
        public virtual Difficulty GetDifficulty() { return Difficulty.Normal; }

        /// <summary>获取寿命模式</summary>
        public virtual LifeMode GetLifeMode() { return LifeMode.Normal; }
    }

    /// <summary>
    /// 武将。对应 C++ Person。
    /// 仅保留单挑系统调用到的接口，接入时桥接到真实的 Sango.Core.Person。
    /// </summary>
    public class Person : IAlive
    {
        /// <summary>能力下限。对应 Person::MinStat</summary>
        public const int MinStat = 1;

        public bool IsAlive { get; set; } = true;

        /// <summary>武将 ID。对应 Person::get_id</summary>
        public virtual PersonId GetId() { return PersonId.Invalid; }

        /// <summary>姓名</summary>
        public virtual string GetName() { return string.Empty; }

        /// <summary>所属剧本</summary>
        public virtual Scenario GetScenario() { return null; }

        /// <summary>所属势力 ID</summary>
        public virtual int GetForceId() { return -1; }

        /// <summary>所在地区 ID</summary>
        public virtual int GetDistrictId() { return -1; }

        /// <summary>伤病程度</summary>
        public virtual int GetShoubyou() { return (int)Shoubyou.Kenkou; }

        /// <summary>体力</summary>
        public virtual int GetHp() { return 0; }

        /// <summary>年龄</summary>
        public virtual int GetAge() { return 0; }

        /// <summary>性格</summary>
        public virtual Seikaku GetSeikaku() { return Seikaku.Reisei; }

        /// <summary>忠诚度</summary>
        public virtual int GetLoyalty() { return 0; }

        /// <summary>出生地 ID</summary>
        public virtual int GetBirthplaceId() { return -1; }

        /// <summary>势力颜色</summary>
        public virtual int GetColor() { return 0; }

        /// <summary>获取能力值</summary>
        public virtual int GetStat(PersonStatType type) { return 0; }

        /// <summary>依据伤病程度计算能力值。对应 Person::calc_stat</summary>
        public virtual int CalcStat(PersonStatType type, int shoubyou) { return GetStat(type); }

        /// <summary>是否拥有指定特技</summary>
        public virtual bool HasSkill(SkillId skill) { return false; }

        /// <summary>是否为玩家武将</summary>
        public virtual bool IsPlayer() { return false; }

        /// <summary>是否为君主</summary>
        public virtual bool IsKunshu() { return false; }

        /// <summary>对方是否为血亲</summary>
        public virtual bool IsFamily(PersonId id) { return false; }

        /// <summary>对方是否为配偶</summary>
        public virtual bool SpouseIs(PersonId id) { return false; }

        /// <summary>对方是否为义兄弟</summary>
        public virtual bool IsGikyoudai(PersonId id) { return false; }

        /// <summary>对方是否为夫妇</summary>
        public virtual bool IsFuufu(PersonId id) { return false; }

        /// <summary>对方是否为义兄弟（别名）</summary>
        public virtual bool IsKetsuen(PersonId id) { return false; }

        /// <summary>是否憎恶对方</summary>
        public virtual bool IsHate(PersonId id) { return false; }

        /// <summary>是否喜好对方</summary>
        public virtual bool IsLike(PersonId id) { return false; }

        /// <summary>与对方的相性距离</summary>
        public virtual int GetAishouDistance(PersonId id) { return 75; }
    }

    /// <summary>宝物。对应 C++ Item</summary>
    public class Item : IAlive
    {
        public bool IsAlive { get; set; } = true;

        /// <summary>宝物大类</summary>
        public virtual ItemType GetTypeValue() { return ItemType.None; }

        /// <summary>宝物 ID</summary>
        public virtual ItemId GetItemId() { return ItemId.Invalid; }
    }

    /// <summary>部队。对应 C++ Unit</summary>
    public class Unit : IAlive
    {
        /// <summary>最大武将数。对应 Unit::MaxMemberCount</summary>
        public const int MaxMemberCount = 3;

        public bool IsAlive { get; set; } = true;

        /// <summary>是否为玩家操作</summary>
        public virtual bool IsPlayerControlled() { return false; }

        /// <summary>兵力</summary>
        public virtual int GetTroops() { return 0; }

        /// <summary>增减士气，返回实际变化量</summary>
        public virtual int AddEnergy(int value) { return 0; }

        /// <summary>同步装备数量</summary>
        public virtual void SyncEquipmentQuantity() { }

        /// <summary>获取坐标</summary>
        public virtual object GetPos() { return null; }

        /// <summary>势力颜色</summary>
        public virtual int GetColor() { return 0; }

        /// <summary>是否包含指定武将</summary>
        public virtual bool HasMember(PersonId id) { return false; }
    }

    /// <summary>势力。对应 C++ Force</summary>
    public class Force : IAlive
    {
        public bool IsAlive { get; set; } = true;

        /// <summary>势力 ID</summary>
        public virtual int GetId() { return -1; }

        /// <summary>是否为一般势力（非异族等）</summary>
        public virtual bool IsNormal() { return true; }

        /// <summary>是否为玩家势力</summary>
        public virtual bool IsPlayer() { return false; }

        /// <summary>获取对某势力的友好度</summary>
        public virtual int GetLike(int forceId) { return 0; }
    }

    /// <summary>地区。对应 C++ District</summary>
    public class District : IAlive
    {
        public bool IsAlive { get; set; } = true;
    }

    #endregion

    #region 引擎（表现层）接口存根

    /// <summary>
    /// 表现层引擎接口。对应 C++ Engine。
    /// 单挑逻辑通过这些方法驱动动画/UI；在无表现层（纯逻辑推演）时，Duel.view 为 false，
    /// 这些方法不会被调用，由 Duel 内部直接结算数值。
    /// </summary>
    public class Engine
    {
        /// <summary>是否正在播放动画</summary>
        public virtual bool DuelIsAnimating(Duel duel) { return false; }

        /// <summary>消息框是否可见</summary>
        public virtual bool DuelIsMessageBoxVisible(Duel duel) { return false; }

        /// <summary>重置动画队列</summary>
        public virtual void DuelResetAnim(Duel duel) { }

        /// <summary>刷新合数显示</summary>
        public virtual void DuelUpdateBlowCounter(Duel duel) { }

        /// <summary>清除无敌状态显示</summary>
        public virtual void DuelResetInvulnerable(Duel duel, int team) { }

        /// <summary>清除增益状态显示</summary>
        public virtual void DuelResetBuff(Duel duel, int team, int buff) { }

        /// <summary>切换当前武将</summary>
        public virtual void DuelChangeCurrentChara(Duel duel, int team) { }

        /// <summary>播放"合数"动画</summary>
        public virtual void DuelBlowAnim(Duel duel, Duel.BlowAnim[] queue, int count) { }

        /// <summary>播放体力动画</summary>
        public virtual void DuelHpAnim(Duel duel, Duel.HPAnim[] queue, int count) { }

        /// <summary>播放斗志动画</summary>
        public virtual void DuelSpiritAnim(Duel duel, Duel.SpiritAnim[] queue, int count) { }

        /// <summary>播放登场动画</summary>
        public virtual void DuelJoin(Duel duel, int team, int oldChara) { }

        /// <summary>播放交替动画</summary>
        public virtual void DuelSwitch(Duel duel, int team, int oldChara) { }

        /// <summary>播放开场</summary>
        public virtual void DuelOpening(Duel duel) { }

        /// <summary>播放一击必杀</summary>
        public virtual void DuelFtk(Duel duel, int team, int chara, int ftkType, int opponentTeam, int opponentChara) { }

        /// <summary>播放退却</summary>
        public virtual void DuelRetreat(Duel duel) { }

        /// <summary>播放平局</summary>
        public virtual void DuelDraw(Duel duel) { }

        /// <summary>播放结束</summary>
        public virtual void DuelClosing(Duel duel) { }

        /// <summary>暂停</summary>
        public virtual void DuelStop(Duel duel) { }

        /// <summary>继续</summary>
        public virtual void DuelPlay(Duel duel) { }

        /// <summary>获取玩家选择的行动方针</summary>
        public virtual int DuelGetStance(Duel duel, int team) { return -1; }

        /// <summary>获取玩家选择的必杀</summary>
        public virtual int DuelGetSpecial(Duel duel, int team) { return -1; }

        /// <summary>获取玩家选择的交替武将</summary>
        public virtual int DuelGetSwitchingChara(Duel duel, int team) { return -1; }

        /// <summary>必杀按钮是否被按下</summary>
        public virtual bool DuelIsSpecialButtonPushed(Duel duel, int team) { return false; }

        /// <summary>停止按钮是否被按下</summary>
        public virtual bool DuelIsStopButtonPushed(Duel duel) { return false; }

        /// <summary>继续按钮是否被按下</summary>
        public virtual bool DuelIsPlayButtonPushed(Duel duel) { return false; }

        /// <summary>必杀取消按钮是否被按下</summary>
        public virtual bool DuelIsSpecialCancelButtonPushed(Duel duel) { return false; }

        /// <summary>弹出是否确认对话框</summary>
        public virtual bool YesNo(string text) { return true; }
    }

    #endregion

    #region 系统（业务层）接口存根

    /// <summary>单挑结果事件</summary>
    public class SystemEvents
    {
        public virtual void OnDuelFinished() { }
    }

    /// <summary>
    /// 系统（业务层）。对应 C++ System。
    /// 提供随机源、消息、日志以及单挑结算所需的各类游戏操作。
    /// 说明：C++ 中该类名为 System，与 .NET 的 System 命名空间冲突，故重命名为 GameSystem。
    /// </summary>
    public class GameSystem
    {
        private Engine m_engine;

        public GameSystem() { }
        public GameSystem(Engine engine) { m_engine = engine; }

        /// <summary>获取表现层引擎</summary>
        public virtual Engine GetEngine() { return m_engine; }

        /// <summary>获取日志对象，无日志时返回 null</summary>
        public virtual Logger GetLogger() { return null; }

        /// <summary>获取当前随机种子</summary>
        public virtual int GetSeed() { return DuelRandom.GetSeed(); }

        /// <summary>返回 [0, max) 的随机整数</summary>
        public virtual int RandInt(int max) { return DuelRandom.Range(max); }

        /// <summary>以 percent% 的概率返回 true</summary>
        public virtual bool RandBool(int percent) { return DuelRandom.Chance(percent); }

        /// <summary>获取难度</summary>
        public virtual Difficulty GetDifficulty() { return Difficulty.Normal; }

        /// <summary>获取寿命模式</summary>
        public virtual LifeMode GetLifeMode() { return LifeMode.Normal; }

        /// <summary>获取战死频率</summary>
        public virtual BattleDeathMode GetBattleDeathMode() { return BattleDeathMode.Normal; }

        /// <summary>功能是否被禁用</summary>
        public virtual bool IsFeatDisabled(Feature feature) { return false; }

        /// <summary>获取势力</summary>
        public virtual Force GetForce(int forceId) { return null; }

        /// <summary>获取地区</summary>
        public virtual District GetDistrict(int districtId) { return null; }

        /// <summary>获取武将持有的宝物列表</summary>
        public virtual List<Item> GetPersonItemList(Person person) { return new List<Item>(); }

        /// <summary>获取武将宝物提供的单挑战力加成</summary>
        public virtual int GetDuelItemPower(Person person) { return 0; }

        /// <summary>获取消息文本</summary>
        public virtual string GetMessage(Message msg) { return string.Empty; }

        /// <summary>显示消息</summary>
        public virtual void Message(string text, object target, object[] args, bool pause) { }

        /// <summary>写入历史日志</summary>
        public virtual void HistoryLog(Message msg, Unit unit, bool show, int color) { }

        /// <summary>地图提示</summary>
        public virtual void Ping(object pos, int type, int color) { }

        /// <summary>获取伤病名称</summary>
        public virtual string GetShoubyouName(int shoubyou) { return string.Empty; }

        /// <summary>设置武将伤病</summary>
        public virtual void PersonSetShoubyou(Person person, int shoubyou) { }

        /// <summary>增减武将体力</summary>
        public virtual void PersonAddHp(Person person, int value) { }

        /// <summary>增减武将经验</summary>
        public virtual void PersonAddExp(Person person, PersonStatType type, int subType, int value) { }

        /// <summary>增减武将功绩</summary>
        public virtual void PersonAddKouseki(Person person, int value) { }

        /// <summary>武将死亡</summary>
        public virtual void PersonDie(Person person, Person killer, Unit unit, Unit killerUnit, DeathType type, bool flag) { }

        /// <summary>俘虏处理</summary>
        public virtual void HoryoShoguu(List<Person> all, List<Person> captured, Unit loserUnit, Unit winnerUnit) { }

        /// <summary>武将脱离原部队</summary>
        public virtual void PersonDetach(Person person, Person toPerson, Unit toUnit, Unit fromUnit) { }

        /// <summary>任命地区都督</summary>
        public virtual void DistrictAppointTotoku(District district, Force force) { }

        /// <summary>设置势力友好度</summary>
        public virtual void ForceSetLike(int forceId, int targetForceId, int value) { }

        /// <summary>增减势力友好度</summary>
        public virtual void ForceAddLike(int forceId, int targetForceId, int value) { }

        /// <summary>增减势力技术点</summary>
        public virtual void ForceAddTechPoint(Force force, int value, Unit unit) { }

        /// <summary>增减部队兵力，返回实际变化量</summary>
        public virtual int UnitAddTroops(Unit unit, int value) { return 0; }

        /// <summary>显示浮动数值</summary>
        public virtual void FloatingDamage(int value, FloatingCounterType type, Unit unit) { }

        /// <summary>获取事件对象</summary>
        public virtual SystemEvents GetEvents() { return null; }
    }

    #endregion
}
