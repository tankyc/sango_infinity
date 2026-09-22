/*
 * 文件名：DuelGameSystem.cs
 * 描述：单挑业务桥接层。对应 C++ System。
 *
 * 职责：
 *   · 把单挑逻辑层（Duel / DuelPhase / DuelAI）对"游戏世界"的所有请求，
 *     翻译成对 Sango Infinity 真实 API 的调用（Troop / Person / Force / City / PlayerMessage / GameDialog ...）。
 *   · 单挑逻辑层只认这些方法名，因此换表现方式、换数据结构都不需要动逻辑层。
 *
 * 约定：
 *   · 无法确定对应实现的少量接口（地图闪烁提示、任命地区都督、浮动数值），
 *     一律做成可赋值的委托钩子 + 默认空实现，并标注 TODO，避免凭空臆造游戏规则。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core.Duel
{
    #region 日志

    /// <summary>单挑日志适配：转发到项目的 Sango.Log</summary>
    public class DuelLogger : Logger
    {
        /// <summary>是否输出调试日志（AI 决策过程等，默认关闭以免刷屏）</summary>
        public static bool Enabled = false;

        public void Debug(string text)
        {
            if (Enabled)
                Sango.Log.Info(text);
        }
    }

    #endregion

    #region 单挑事件

    /// <summary>
    /// 单挑系统事件。对应 C++ SystemEvents。
    /// 单挑结算到"结果已确定、开始应用结果"时回调一次。
    /// </summary>
    public class DuelGameEvents
    {
        /// <summary>当前正在结算的单挑，由 DuelManager 注入</summary>
        public Duel CurrentDuel;

        /// <summary>单挑结果确定（结算前）</summary>
        public virtual void OnDuelFinished()
        {
            GameEvent.OnDuelFinished?.Invoke(CurrentDuel);
        }
    }

    #endregion

    #region 单挑消息文本

    /// <summary>
    /// 单挑消息文本表。对应 s11 的 MessageId。
    /// 项目暂未在 Language 表中登记单挑文本，先在此内置中文模板，
    /// 后续如需多语言，把 GetMessage 换成 GameLanguage.GetString(id) 即可。
    /// </summary>
    public static class DuelMessageText
    {
        private static readonly Dictionary<int, string> s_texts = new Dictionary<int, string>
        {
            { (int)DuelMessageId.LD_WAR_DUEL_INJURY,    "{0}在单挑中负伤（{1}）" },
            { (int)DuelMessageId.N_WAR_DUEL_INJURY,     "{0}在单挑中负伤了。" },
            { (int)DuelMessageId.F_WAR_DUEL_DRAW_A, "{0}与{1}的单挑不分胜负。" },
            { (int)DuelMessageId.F_WAR_DUEL_ESCAPE_A,      "{0}击退了{1}。" },
            { (int)DuelMessageId.F_WAR_DUEL_ESCAPE_B,      "{0}被{1}击退了。" },
            { (int)DuelMessageId.F_WAR_DUEL_CAPTURE_A,    "{0}生擒了{1}。" },
            { (int)DuelMessageId.F_WAR_DUEL_CAPTURE_B,    "{0}被{1}生擒了。" },
            { (int)DuelMessageId.F_WAR_DUEL_DEATH,     "{0}斩杀了{1}。" },
            { (int)DuelMessageId.LB_WAR_DUEL_WIN,       "{0}在单挑中战胜了{1}。" },
            { (int)DuelMessageId.LD_WAR_DUEL_LOST,      "{0}在单挑中败给了{1}。" },
            { (int)DuelMessageId.N_WAR_DUEL_CONFIRM,    "是否接受{0}的单挑？" },
        };

        /// <summary>获取模板，未登记时返回空串</summary>
        public static string GetTemplate(int id)
        {
            return s_texts.TryGetValue(id, out string text) ? text : string.Empty;
        }
    }

    #endregion

    /// <summary>
    /// 单挑业务层。对应 C++ System（与 .NET 的 System 命名空间冲突，故重命名）。
    /// </summary>
    public class DuelGameSystem
    {
        /// <summary>伤病名称（健康 / 轻伤 / 中伤 / 重伤）</summary>
        private static readonly string[] s_injuryLevelNames = { "健康", "轻伤", "中伤", "重伤" };

        /// <summary>表现层</summary>
        protected IDuelView m_View;

        /// <summary>日志</summary>
        protected Logger m_Logger = new DuelLogger();

        /// <summary>事件</summary>
        protected DuelGameEvents m_Events = new DuelGameEvents();

        /// <summary>当前单挑（结算播报时需要）</summary>
        public Duel CurrentDuel
        {
            get { return m_Events.CurrentDuel; }
            set { m_Events.CurrentDuel = value; }
        }

        /// <summary>消息框是否可见（逻辑层据此暂停推进）</summary>
        public bool MessageBoxVisible { get; protected set; }

        /// <summary>表现层是否可用（无表现层时为 false，逻辑层瞬时结算）</summary>
        public bool HasView { get { return m_View != null; } }

        public DuelGameSystem() { }

        public DuelGameSystem(IDuelView view) { m_View = view; }

        /// <summary>获取表现层</summary>
        public virtual IDuelView GetEngine() { return m_View; }

        /// <summary>设置表现层</summary>
        public virtual void SetEngine(IDuelView view) { m_View = view; }

        /// <summary>获取日志对象</summary>
        public virtual Logger GetLogger() { return m_Logger; }

        #region 随机

        /// <summary>获取当前随机种子</summary>
        public virtual int GetSeed() { return DuelRandom.GetSeed(); }

        /// <summary>返回 [0, max) 的随机整数</summary>
        public virtual int RandInt(int max) { return DuelRandom.Range(max); }

        /// <summary>以 percent% 的概率返回 true</summary>
        public virtual bool RandBool(int percent) { return DuelRandom.Chance(percent); }

        #endregion

        #region 全局设置

        /// <summary>获取难度（读剧本参数 difficulty 并映射到单挑 3 档）</summary>
        public virtual Difficulty GetDifficulty() { return DuelRules.GetDifficulty(); }

        /// <summary>获取寿命模式（读剧本参数 duelLifeMode）</summary>
        public virtual LifeMode GetLifeMode() { return DuelRules.GetLifeMode(); }

        /// <summary>获取战死频率（读剧本参数 duelDeathMode）</summary>
        public virtual BattleDeathMode GetBattleDeathMode() { return DuelRules.GetBattleDeathMode(); }

        /// <summary>获取单挑获胜的基础抓捕率（读剧本参数 captureChangceWhenDuelWin）</summary>
        public virtual int GetDuelWinCaptureChance() { return DuelRules.GetDuelWinCaptureChance(); }

        /// <summary>功能是否被禁用（读剧本参数里的三个开关）</summary>
        public virtual bool IsFeatDisabled(Feature feature) { return DuelRules.IsFeatDisabled(feature); }

        #endregion

        #region 世界查询

        /// <summary>获取势力</summary>
        public virtual Force GetForce(int forceId)
        {
            if (Scenario.Cur == null) return null;
            return Scenario.Cur.forceSet.Get(forceId);
        }

        /// <summary>
        /// 获取武将所在地区（城市）。对应 C++ System::get_district。
        /// 项目里"地区"由 City 承担，故直接返回 City。
        /// </summary>
        public virtual City GetDistrict(int districtId)
        {
            if (Scenario.Cur == null) return null;
            return Scenario.Cur.citySet.Get(districtId);
        }

        /// <summary>获取武将持有的宝物列表</summary>
        public virtual List<Equipment> GetPersonItemList(Person person)
        {
            return DuelSettings.GetPersonItemList != null
                ? DuelSettings.GetPersonItemList(person)
                : new List<Equipment>();
        }

        /// <summary>获取武将宝物提供的单挑战力加成</summary>
        public virtual int GetDuelItemPower(Person person)
        {
            return DuelSettings.GetDuelItemPower != null ? DuelSettings.GetDuelItemPower(person) : 0;
        }

        #endregion

        #region 消息 / 日志 / 提示

        /// <summary>把单挑消息对象渲染为文本</summary>
        public virtual string GetMessage(Message msg)
        {
            if (msg == null) return string.Empty;
            string template = DuelMessageText.GetTemplate((int)msg.Id);
            if (string.IsNullOrEmpty(template)) return string.Empty;

            object[] args = new object[8];
            for (int i = 0; i < msg.Objs.Length && i < args.Length; i++)
            {
                object o = msg.Objs[i];
                args[i] = o is Person p ? p.Name : o;
            }
            if (!string.IsNullOrEmpty(msg.Str0) && args[1] == null) args[1] = msg.Str0;

            // 模板里只用到前若干个占位符，多余的参数会被 string.Format 忽略
            List<object> used = new List<object>(4);
            for (int i = 0; i < 4; i++)
                used.Add(args[i] == null ? string.Empty : args[i]);

            try
            {
                return string.Format(template, used.ToArray()).Replace("{", "{").Trim();
            }
            catch (FormatException)
            {
                return template;
            }
        }

        /// <summary>弹出单挑消息（阻塞式，玩家点击后继续）</summary>
        public virtual void Message(string text, object target, object[] args, bool pause)
        {
            if (string.IsNullOrEmpty(text)) return;

            MessageBoxVisible = true;
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickSay, text, () =>
            {
                MessageBoxVisible = false;
            }, null, target as Person);

            if (!pause)
                MessageBoxVisible = false;
        }

        /// <summary>
        /// 写入战报（历史消息）。对应 C++ System::history_log。
        /// 使用项目的玩家消息系统，颜色通过势力归属体现。
        /// </summary>
        public virtual void HistoryLog(Message msg, Troop troop, bool show, int color)
        {
            string text = GetMessage(msg);
            if (string.IsNullOrEmpty(text)) return;

            if (OnHistoryLog != null)
            {
                OnHistoryLog(text, troop);
                return;
            }

            if (!show || troop == null) return;
            Sango.Core.Player.PlayerMessage.AddTextMessage(text, troop.mBelongForce, troop.x, troop.y);
        }

        /// <summary>自定义战报输出（置空则使用 PlayerMessage）</summary>
        public static System.Action<string, Troop> OnHistoryLog;

        /// <summary>
        /// 地图闪烁提示。对应 C++ System::ping。
        /// 项目当前没有统一的"地图点名闪烁"接口，先做成钩子。
        /// TODO: 接入地图闪烁表现（例如 MapRender 上的高亮对象）后在此实现。
        /// </summary>
        public virtual void Ping(object pos, int type, int color)
        {
            OnPing?.Invoke(pos, type, color);
        }

        /// <summary>自定义地图提示实现</summary>
        public static System.Action<object, int, int> OnPing;

        /// <summary>
        /// 浮动数值显示（士气 / 兵力变化）。
        /// TODO: 接入项目现有的浮动文本表现后在此实现。
        /// </summary>
        public virtual void FloatingDamage(int value, FloatingCounterType type, Troop troop)
        {
            OnFloatingDamage?.Invoke(value, type, troop);
        }

        /// <summary>自定义浮动数值实现</summary>
        public static System.Action<int, FloatingCounterType, Troop> OnFloatingDamage;

        #endregion

        #region 武将状态

        /// <summary>获取伤病名称</summary>
        public virtual string GetInjuryLevelName(int injuryLevel)
        {
            if (injuryLevel < 0 || injuryLevel >= s_injuryLevelNames.Length) return s_injuryLevelNames[0];
            return s_injuryLevelNames[injuryLevel];
        }

        /// <summary>设置武将伤病（0=健康 ~ 3=重伤）</summary>
        public virtual void PersonSetInjuryLevel(Person person, int injuryLevel)
        {
            if (person == null) return;
            person.injury = Math.Max(0, Math.Min(injuryLevel, (int)InjuryLevel.NearDeath));
        }

        /// <summary>增减武将体力（0 ~ 100）</summary>
        public virtual void PersonAddHp(Person person, int value)
        {
            if (person == null) return;
            int hp = person.stamina + value;
            person.stamina = Math.Max(0, Math.Min(100, hp));
        }

        /// <summary>增减武将经验。项目使用统一的武将经验值</summary>
        public virtual void PersonAddExp(Person person, PersonStatType type, int subType, int value)
        {
            if (person == null || value == 0) return;
            person.GainExp(value);
        }

        /// <summary>增减武将功绩</summary>
        public virtual void PersonAddKouseki(Person person, int value)
        {
            if (person == null || value == 0) return;
            person.GainMerit(value);
        }

        /// <summary>武将死亡</summary>
        public virtual void PersonDie(Person person, Person killer, Troop troop, Troop killerTroop, DeathType type, bool flag)
        {
            if (person == null) return;
            person.Dead();
        }

        #endregion

        #region 部队 / 俘虏

        /// <summary>增减部队兵力，返回实际变化量</summary>
        public virtual int TroopAddTroops(Troop troop, int value)
        {
            if (troop == null || value == 0) return 0;
            int before = troop.troops;
            troop.ChangeTroops(value, null, 0);
            return troop.troops - before;
        }

        /// <summary>俘虏处理：把被俘武将登记到胜方部队。对应 C++ System::horyo_shoguu</summary>
        public virtual void TakeCaptives(List<Person> all, List<Person> captured, Troop loserTroop, Troop winnerTroop)
        {
            if (captured == null || winnerTroop == null) return;
            for (int i = 0; i < captured.Count; i++)
            {
                Person person = captured[i];
                if (person == null) continue;
                // 统一走部队的俘虏流程：设置俘虏状态、脱离原城市、登记俘虏名单
                winnerTroop.AddCaptive(person);
            }
        }

        /// <summary>
        /// 把武将从原部队摘除。
        ///
        /// 判据是"该武将是否还登记在这支部队里"(Leader / Member1 / Member2)，**不能用 person.mTroop**：
        /// 俘虏流程 Troop.AddCaptive 会先把 person.mTroop 改成捕获方部队，按 mTroop 判断就永远不成立，
        /// 被俘武将便会一直留在原部队的主将/成员字段上（而它的 mBelongCity 已被 AddCaptive 清空）；
        /// 该部队日后被歼灭时 Troop.Clear 取 mBelongCity 就会空引用崩溃。
        /// </summary>
        public virtual void PersonDetach(Person person, Person toPerson, Troop toTroop, Troop fromTroop)
        {
            if (person == null || fromTroop == null) return;
            if (fromTroop.HasMember(person.Id))
                fromTroop.RemovePerson(person);
        }

        #endregion

        #region 势力

        /// <summary>
        /// 任命地区都督。对应 C++ System::district_appoint_totoku。
        /// TODO: 项目里"都督"由军团（Corps）承担，此处的语义是"攻下该地后重设都督"，
        ///       待军团任命规则确定后在此接入。
        /// </summary>
        public virtual void AppointDistrictCommander(City city, Force force)
        {
            OnAppointDistrictCommander?.Invoke(city, force);
        }

        /// <summary>自定义地区都督任命实现</summary>
        public static System.Action<City, Force> OnAppointDistrictCommander;

        /// <summary>设置势力友好度</summary>
        public virtual void ForceSetLike(int forceId, int targetForceId, int value)
        {
            if (Scenario.Cur == null) return;
            Force a = Scenario.Cur.forceSet.Get(forceId);
            Force b = Scenario.Cur.forceSet.Get(targetForceId);
            if (a == null || b == null) return;
            Scenario.Cur.AddRelation(a, b, value - Scenario.Cur.GetRelation(a, b));
        }

        /// <summary>增减势力友好度</summary>
        public virtual void ForceAddLike(int forceId, int targetForceId, int value)
        {
            if (Scenario.Cur == null) return;
            Force a = Scenario.Cur.forceSet.Get(forceId);
            Force b = Scenario.Cur.forceSet.Get(targetForceId);
            if (a == null || b == null) return;
            Scenario.Cur.AddRelation(a, b, value);
        }

        /// <summary>增减势力技术点</summary>
        public virtual void ForceAddTechPoint(Force force, int value, Troop troop)
        {
            if (force == null || value == 0) return;
            force.GainTechniquePoint(value);
        }

        #endregion

        /// <summary>获取单挑系统事件</summary>
        public virtual DuelGameEvents GetEvents() { return m_Events; }
    }
}
