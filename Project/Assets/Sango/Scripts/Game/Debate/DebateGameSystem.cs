/*
 * 文件名：DebateGameSystem.cs
 * 描述：舌战业务层（对应 C++ System），把舌战逻辑的外部依赖**直接落到工程真实代码**
 *
 * 设计说明（与单挑的 Game/Duel/DuelGameSystem.cs 同构）：
 *   · 舌战逻辑（Debate / DebatePhase / DebateAI）只认这一层的 virtual 方法，不直接碰工程类型；
 *   · 本类的方法体一律调用工程既有 API（Person / Troop / Corps / Force / GameDialog / PlayerMessage /
 *     GameMedia / ScenarioVariables），不再有虚构的存根类型；
 *   · 纯逻辑推演（无表现层、无剧本）时同样安全：Scenario.Cur 为 null 时所有世界查询返回 null。
 *
 * 与原 C++ 的对应关系：
 *   System::rand_int / rand_bool        → DebateRandom（转发 GameRandom）
 *   System::get_force                   → Scenario.forceSet.Get
 *   System::get_district                → Scenario.corpsSet.Get（C++ 的 district 在工程里是"军团 Corps"）
 *   System::get_location_object         → Scenario.troopsSet.Get（对应工程的 Troop）
 *   System::get_person_item_list        → 暂未接入（Item 预留，见 HasAllRhetoric）
 *   System::message / get_msg           → GameDialog + DebateMessageText
 *   System::history_log                 → PlayerMessage.AddTextMessage
 *   System::play_se                     → GameMedia.PlaySfx
 *   System::person_set_shoubyou          → Person.injury
 *   System::person_add_stat_exp         → PersonAttributeValue.SetExp（属性经验，非武将等级经验）
 *   System::person_add_kouseki          → Person.GainMerit
 *   System::force_add_tech_point        → Force.GainTechniquePoint
 *   System::is_feat_disabled            → DebateRules（读剧本参数）
 */

using System;
using System.Collections.Generic;

namespace Sango.Core.Debate
{
    #region 舌战消息文本

    /// <summary>
    /// 舌战消息文本表。对应 s11 的 MessageId。
    /// 项目暂未在语言表里登记舌战文本，先在此内置中文模板；
    /// 后续如需多语言，把 GetTemplate 换成 GameLanguage.GetString(id) 即可（与单挑 DuelMessageText 同做法）。
    /// </summary>
    public static class DebateMessageText
    {
        private static readonly Dictionary<int, string> s_texts = new Dictionary<int, string>
        {
            { (int)DebateMessageId.O_DEB_FATALBLOW_INJURED,  "{0}在舌战中负伤了。" },
            { (int)DebateMessageId.LD_DEB_FATALBLOW_INJURED, "{0}在舌战中负伤了。" },
            { (int)DebateMessageId.N_DEB_DEBATE_CONFIRM,     "{0}向{1}发起舌战，是否应战？" },
        };

        /// <summary>获取模板，未登记时返回空串</summary>
        public static string GetTemplate(int id)
        {
            return s_texts.TryGetValue(id, out string text) ? text : string.Empty;
        }
    }

    #endregion

    /// <summary>
    /// 舌战业务层。对应 C++ System（类名与工程的 Sango.Core.Player.GameSystem 冲突，故加 Debate 前缀）。
    /// </summary>
    public class DebateGameSystem
    {
        /// <summary>话题名称表，下标与 Topic 枚举一致</summary>
        private static readonly string[] s_topicNames = { "故事", "道理", "时势" };

        /// <summary>表现层（见 DebateView.cs）</summary>
        protected IDebateView m_View;

        /// <summary>日志</summary>
        protected Logger m_Logger = new SangoLogger();

        /// <summary>消息框是否正在显示（供 Debate.IsIdle 判定是否阻塞逻辑推进）</summary>
        public bool MessageBoxVisible { get; protected set; }

        public DebateGameSystem() { }
        public DebateGameSystem(IDebateView view) { m_View = view; }

        /// <summary>获取表现层。返回 null 表示纯逻辑推演</summary>
        public virtual IDebateView GetEngine() { return m_View; }

        /// <summary>设置表现层</summary>
        public virtual void SetEngine(IDebateView view) { m_View = view; }

        /// <summary>获取日志对象；关闭日志开关时返回 null</summary>
        public virtual Logger GetLogger() { return DebateSettings.EnableLog ? m_Logger : null; }

        /// <summary>设置日志对象</summary>
        public virtual void SetLogger(Logger logger) { m_Logger = logger; }

        #region 随机

        /// <summary>获取当前随机种子</summary>
        public virtual int GetSeed() { return DebateRandom.GetSeed(); }

        /// <summary>返回 [0, max) 的随机整数</summary>
        public virtual int RandInt(int max) { return DebateRandom.Range(max); }

        /// <summary>以 percent% 的概率返回 true</summary>
        public virtual bool RandBool(int percent) { return DebateRandom.Chance(percent); }

        #endregion

        #region 全局设置

        /// <summary>功能是否被禁用（读剧本参数，目前只有"禁止会心"）</summary>
        public virtual bool IsFeatDisabled(Feature feature) { return DebateRules.IsFeatDisabled(feature); }

        #endregion

        #region 世界查询

        /// <summary>获取势力</summary>
        public virtual Force GetForce(int forceId)
        {
            if (Scenario.Cur == null) return null;
            return Scenario.Cur.forceSet.Get(forceId);
        }

        /// <summary>
        /// 获取军团。对应 C++ System::get_district：
        /// 工程里没有 District 类，C++ 的 district 语义（is_player + get_number()==1）由军团 Corps 承担
        /// —— Corps.IsPlayerControl 就是 IsPlayer &amp;&amp; IsCaptainCorps（number == 1）。
        /// </summary>
        public virtual Corps GetDistrict(int districtId)
        {
            if (Scenario.Cur == null) return null;
            return Scenario.Cur.corpsSet.Get(districtId);
        }

        /// <summary>获取部队对象。对应 C++ System::get_location_object（工程里是 Troop）</summary>
        public virtual Troop GetLocationObject(int locationId)
        {
            if (Scenario.Cur == null) return null;
            return Scenario.Cur.troopsSet.Get(locationId);
        }

        /// <summary>
        /// 是否持有"书籍"从而解锁全部话术。对应 C++ get_person_item_list + ItemType_Book 判定。
        ///
        /// 宝物（Item）暂不接入：C++ 里的书籍判定先做成钩子，置空则恒为 false（只靠 wordTac 解锁话术）。
        /// 接入方式：给 OnHasAllRhetoric 赋值，或直接改写本方法用 Person.itemStore / HasItem 判断。
        /// </summary>
        public virtual bool HasAllRhetoric(Person person)
        {
            return OnHasAllRhetoric != null && OnHasAllRhetoric(person);
        }

        /// <summary>自定义"持有书籍"判定（宝物接入前留空）</summary>
        public static Func<Person, bool> OnHasAllRhetoric;

        #endregion

        #region 消息 / 日志 / 提示

        /// <summary>把舌战消息对象渲染为文本</summary>
        public virtual string GetMessage(Message msg)
        {
            if (msg == null) return string.Empty;
            string template = DebateMessageText.GetTemplate((int)msg.Id);
            if (string.IsNullOrEmpty(template)) return string.Empty;

            object[] args = new object[8];
            for (int i = 0; i < msg.Objs.Length && i < args.Length; i++)
            {
                object o = msg.Objs[i];
                args[i] = o is Person p ? p.Name : o;
            }
            if (!string.IsNullOrEmpty(msg.Str0) && args[1] == null) args[1] = msg.Str0;

            List<object> used = new List<object>(4);
            for (int i = 0; i < 4; i++)
                used.Add(args[i] == null ? string.Empty : args[i]);

            try
            {
                return string.Format(template, used.ToArray()).Trim();
            }
            catch (FormatException)
            {
                return template;
            }
        }

        /// <summary>弹出舌战消息（pause = true 时阻塞逻辑推进，玩家点击后继续）</summary>
        public virtual void Message(Message msg, object target, object[] args, bool pause)
        {
            string text = GetMessage(msg);
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
        /// 使用项目的玩家消息系统，归属与坐标取自武将所在部队（颜色由势力归属体现，与单挑同做法）。
        /// </summary>
        public virtual void HistoryLog(string text, Person person, bool show)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (OnHistoryLog != null)
            {
                OnHistoryLog(text, person);
                return;
            }

            if (!show || person == null) return;

            Troop troop = person.mTroop;
            if (troop != null)
                Sango.Core.Player.PlayerMessage.AddTextMessage(text, troop.mBelongForce, troop.x, troop.y);
            else
                Sango.Core.Player.PlayerMessage.AddTextMessage(text, person.mBelongForce, 0, 0);
        }

        /// <summary>自定义战报输出（置空则使用 PlayerMessage）</summary>
        public static Action<string, Person> OnHistoryLog;

        /// <summary>播放音效。对应 C++ System::play_se</summary>
        public virtual void PlaySe(int seId)
        {
            GameMedia.Instance.PlaySfx(seId);
        }

        /// <summary>获取话题名称（用于日志）</summary>
        public virtual string GetTopicName(int topic)
        {
            return TopicNameOf(topic);
        }

        /// <summary>话题名称表（表现层与日志共用同一份，避免两处各写一套）</summary>
        public static string TopicNameOf(int topic)
        {
            if (topic < 0 || topic >= s_topicNames.Length) return "无";
            return s_topicNames[topic];
        }

        #endregion

        #region 武将结算

        /// <summary>设置武将伤病。对应 C++ System::person_set_shoubyou（0=健康 ~ 3=重伤）</summary>
        public virtual void PersonSetInjury(Person person, int injury)
        {
            if (person == null) return;
            person.injury = Math.Max(0, Math.Min(injury, Person.InjuryMaxLevel));
        }

        /// <summary>获取武将伤病等级（0=健康）</summary>
        public virtual int GetPersonInjury(Person person)
        {
            return person != null ? person.injury : 0;
        }

        /// <summary>
        /// 增减武将**能力经验**。对应 C++ System::person_add_stat_exp。
        /// 注意：单挑桥接里的 PersonAddExp 落到的是 Person.GainExp（武将等级经验），
        /// 舌战这里按 C++ 原意改属性经验（智力等），所以走 PersonAttributeValue.SetExp。
        /// </summary>
        public virtual void PersonAddStatExp(Person person, PersonStatType type, int value, bool show)
        {
            if (person == null || value == 0) return;
            if (Scenario.Cur == null) return;              // SetExp 需要剧本参数，纯逻辑推演时跳过

            PersonAttributeValue attr = GetAttribute(person, type);
            if (attr == null) return;
            attr.SetExp(attr.valueExp + value, Scenario.Cur);
        }

        /// <summary>增减武将功绩。对应 C++ System::person_add_kouseki</summary>
        public virtual void PersonAddMerit(Person person, int value)
        {
            if (person == null || value == 0) return;
            person.GainMerit(value);
        }

        /// <summary>增减势力技术点。对应 C++ System::force_add_tech_point</summary>
        public virtual void ForceAddTechPoint(Force force, int value, object unit)
        {
            if (force == null || value == 0) return;
            force.GainTechniquePoint(value);
        }

        /// <summary>取能力对象（舌战只用到武力与智力两项）</summary>
        private static PersonAttributeValue GetAttribute(Person person, PersonStatType type)
        {
            switch (type)
            {
                case PersonStatType.PersonStatType_Strength: return person.strength;
                case PersonStatType.PersonStatType_Intelligence: return person.intelligence;
            }
            return null;
        }

        #endregion
    }
}
