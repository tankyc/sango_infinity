/*
 * 文件名：DebateTestInstance.cs
 * 描述：舌战(Debate)系统的运行封装，用于在游戏内创建并驱动一次舌战
 *
 * 用法（游戏内）：
 *   var debate = new DebateTestInstance(zhugeLiang, zhouYu);
 *   debate.OnLog = Debug.Log;
 *   debate.Run();                       // 一次性跑完
 *   // 或逐帧驱动（配合 UI 动画）：while (!debate.Step()) { yield return null; }
 *   // 结果：debate.Param.winner / Param.winType / Param.finished
 *
 * 用法（纯逻辑，无 UI）：
 *   var debate = new DebateTestInstance(zhugeLiang, zhouYu) { View = false };
 *   debate.Run();
 *
 * 【重要】Update(0) 返回 true 表示"仍在继续"，false 表示"已结束"，
 *         与单挑 Duel.OnPhase 的语义相反，封装时已统一为 Step()/Run() 返回"是否结束"。
 *
 * 【重要】Param.characters[i].control = true 表示该方由玩家操作，此时必须提供表现层
 *        （IDebateView；Run 会调用 engine.YesNo 询问"是否进入舌战"）；AI 对战请保持 false。
 */

using System;
using System.Text;

namespace Sango.Core.Debate
{
    #region 测试用系统实现

    /// <summary>日志实现，把舌战内部日志转发到外部回调</summary>
    public class DebateTestLogger : Logger
    {
        public Action<string> OnLog;

        public void Debug(string text)
        {
            OnLog?.Invoke(text);
        }
    }

    /// <summary>
    /// 测试用业务层：继承真实业务桥 <see cref="DebateGameSystem"/>，
    /// 只把"会碰 UI / 音效 / 玩家数据"的副作用改成空实现，其余（随机、世界查询、消息文本）沿用真实实现。
    /// </summary>
    public class DebateTestSystem : DebateGameSystem
    {
        private readonly DebateTestLogger m_logger = new DebateTestLogger();

        /// <summary>日志回调</summary>
        public Action<string> OnLog { get { return m_logger.OnLog; } set { m_logger.OnLog = value; } }

        public override IDebateView GetEngine() { return null; }         // 纯逻辑模式无需表现层
        public override Logger GetLogger() { return m_logger; }
        public override int GetSeed() { return DebateRandom.GetSeed(); }
        public override int RandInt(int max) { return DebateRandom.Range(max); }
        public override bool RandBool(int percent) { return DebateRandom.Chance(percent); }
        public override bool IsFeatDisabled(Feature feature) { return DebateRules.IsFeatDisabled(feature); }

        public override void Message(Message msg, object target, object[] args, bool pause) { }
        public override void HistoryLog(string text, Person person, bool show) { }
        public override void PlaySe(int seId) { }
        public override void PersonSetInjury(Person person, int injury) { if (person != null) person.injury = injury; }
        public override void PersonAddStatExp(Person person, PersonStatType type, int value, bool show) { }
        public override void PersonAddMerit(Person person, int value) { }
        public override void ForceAddTechPoint(Force force, int value, object unit) { }
    }

    #endregion

    /// <summary>
    /// 舌战运行实例：构造一次舌战并逐步或一次性驱动它。
    /// </summary>
    public class DebateTestInstance
    {
        /// <summary>舌战本体</summary>
        public Debate Debate { get; private set; }

        /// <summary>启动参数 / 结算结果</summary>
        public Debate.Param Param { get; private set; }

        /// <summary>
        /// 是否启用表现层。默认 false（纯逻辑推演）。
        /// 置 true 时必须提供 IDebateView，否则出牌阶段会一直等待玩家输入。
        /// </summary>
        public bool View { get { return Debate != null && Debate.View; } set { if (Debate != null) Debate.View = value; } }

        /// <summary>日志回调（不设置则直接走 Sango.Log，其输出由编译期符号 SANGO_DEBUG 决定）</summary>
        public Action<string> OnLog;

        /// <summary>是否输出每一步状态快照</summary>
        public bool Verbose { get; set; } = true;

        /// <summary>是否转发舌战内部调试日志（含出牌、伤害等战斗过程，默认开启）</summary>
        public bool LogInternalDebug { get; set; } = true;

        private DebateTestSystem m_system;
        private int m_frame;
        private bool m_finished;

        /// <summary>
        /// 创建一次舌战
        /// </summary>
        /// <param name="challenger">挑战方武将</param>
        /// <param name="challenged">应战方武将</param>
        /// <param name="seed">随机种子（相同种子可复现；传 null 则沿用项目 GameRandom）</param>
        /// <param name="challengerControl">挑战方是否由玩家操作（true 需配合 Engine）</param>
        /// <param name="challengedControl">应战方是否由玩家操作</param>
        public DebateTestInstance(Person challenger, Person challenged,
            int? seed = 1, bool challengerControl = false, bool challengedControl = false)
        {
            if (seed.HasValue)
                DebateRandom.SetSeed(seed.Value);
            else
                DebateRandom.UseProjectRandom();

            m_system = new DebateTestSystem();
            m_system.OnLog = text => { if (LogInternalDebug) Log("[内部] " + text); };

            Param = new Debate.Param();
            Param.characters[0].person = challenger;
            Param.characters[0].hp = Debate.MaxHP;
            Param.characters[0].control = challengerControl;
            Param.characters[1].person = challenged;
            Param.characters[1].hp = Debate.MaxHP;
            Param.characters[1].control = challengedControl;

            Debate = new Debate(m_system, Param);
            Debate.View = false;
        }

        /// <summary>便捷构造：按姓名+智力临时造两个测试武将</summary>
        public DebateTestInstance(string nameA, int intelligenceA, string nameB, int intelligenceB,
            int? seed = 1)
            : this(CreatePerson(nameA, intelligenceA, 1), CreatePerson(nameB, intelligenceB, 2), seed)
        {
        }

        /// <summary>
        /// 造一个仅含舌战所需字段的测试武将（真实 Sango.Core.Person 实例）
        /// </summary>
        public static Person CreatePerson(string name, int intelligence, int id, int personality = 2)
        {
            Person person = new Person();
            person.Id = id;
            person.Name = name;
            person.state = (int)PersonStateType.Normal;
            person.intelligence = new PersonAttributeValue();
            person.intelligence.baseValue = intelligence;
            person.intelligence._value = intelligence;
            person.strength = new PersonAttributeValue();
            person.strength.baseValue = 60;
            person.strength._value = 60;
            person.injury = 0;
            person.personality = personality;
            return person;
        }

        /// <summary>初始化舌战</summary>
        public void Init()
        {
            m_frame = 0;
            m_finished = false;
            if (!Debate.Init())
            {
                Log("!! 舌战初始化失败：武将无效");
                m_finished = true;
                return;
            }
            Log($"== 舌战初始化 == 种子={DebateRandom.GetSeed()} 话题={(Topic)0}");
            Log(DumpState());
        }

        /// <summary>
        /// 推进一帧
        /// </summary>
        /// <returns>true 表示舌战已结束</returns>
        public bool Step()
        {
            if (m_finished)
                return true;

            // 注意：Debate.Update 返回 true 表示"仍在继续"，这里统一取反
            bool running = Debate.Update(0);
            m_frame++;
            m_finished = !running;

            if (Verbose)
                Log(DumpState());

            if (m_finished)
                Log(ReportResult());

            return m_finished;
        }

        /// <summary>一次性跑完整场舌战</summary>
        public void Run(int maxFrames = 100000)
        {
            Init();
            int guard = 0;
            while (!Step())
            {
                if (++guard > maxFrames)
                {
                    Log("!! 超过最大帧数上限，疑似阶段卡死");
                    break;
                }
            }
        }

        /// <summary>输出当前状态快照</summary>
        public string DumpState()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"[{m_frame:D4}] {GetPhaseName(Debate.Phase)} step={Debate.Step}");

            for (int i = 0; i < Debate.MaxTeamCount; i++)
            {
                Debate.Character c = Debate.GetCharacter(i);
                if (c == null) continue;
                string side = i == (int)DebateTeam.DebateTeam_Challenger ? "挑战方" : "应战方";
                sb.Append($" | {side}:{c.person.GetName()} hp={c.hp} 压力={c.stress} 出牌={Debate.GetCardName(Debate.GetPlayedCard(i))} 手牌=[{GetHandText(c)}]");
            }
            return sb.ToString();
        }

        /// <summary>格式化某方当前手牌（用于状态快照）</summary>
        private static string GetHandText(Debate.Character c)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < c.maxCardCount; i++)
            {
                if (c.card[i] < 0) continue;
                if (sb.Length > 0) sb.Append('、');
                sb.Append(Debate.GetCardName(c.card[i]));
            }
            return sb.Length > 0 ? sb.ToString() : "空";
        }

        /// <summary>输出最终结果</summary>
        public string ReportResult()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("== 舌战结束 ==");
            sb.AppendLine($"胜队={Param.winner} 胜利方式={(DebateWinType)Param.winType} finished={Param.finished}");

            for (int i = 0; i < Debate.MaxTeamCount; i++)
            {
                Debate.Character c = Debate.GetCharacter(i);
                if (c == null) continue;
                sb.AppendLine($"  队伍{i} {c.person.GetName()} hp={c.hp} 压力={c.stress} 伤病={c.person.injury}");
            }
            return sb.ToString();
        }

        private void Log(string text)
        {
            if (OnLog != null)
            {
                // 调用方显式挂了回调（测试 / 调试）：按回调走，不经过总开关
                OnLog(text);
                return;
            }

            // 没有回调时直接走项目日志（Sango.Log 的开关是编译期的 SANGO_DEBUG）
            Sango.Log.Info(text, Sango.Log.LogType.Game);
        }

        /// <summary>阶段名</summary>
        public static string GetPhaseName(int phase)
        {
            switch (phase)
            {
                case (int)DebatePhase.DebatePhase_Opening: return "开场";
                case (int)DebatePhase.DebatePhase_FTK: return "一击必杀";
                case (int)DebatePhase.DebatePhase_Unknown2: return "未知";
                case (int)DebatePhase.DebatePhase_TurnStart: return "回合开始";
                case (int)DebatePhase.DebatePhase_Play: return "出牌";
                case (int)DebatePhase.DebatePhase_Damage: return "伤害结算";
                case (int)DebatePhase.DebatePhase_Anger: return "愤怒";
                case (int)DebatePhase.DebatePhase_TurnEnd: return "回合结束";
                case (int)DebatePhase.DebatePhase_Critical: return "会心";
                case (int)DebatePhase.DebatePhase_Closing: return "结束";
                default: return "无";
            }
        }
    }
}
