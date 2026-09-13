/*
 * 文件名：DuelTestInstance.cs
 * 描述：单挑系统的测试实例（纯逻辑推演，无需表现层）
 *
 * 用途：
 *   在不依赖任何 UI / 动画 / 场景的前提下驱动一次完整单挑，用于验证单挑的逻辑流程。
 *   默认 view = false，因此：
 *     - 不需要 Engine（GetEngine 返回 null）
 *     - IsIdle() 恒为 true，阶段状态机不会被"动画播放中"阻塞
 *     - 体力 / 斗志 / 合数在 PlayBlowAnim / PlayHpAnim / PlaySpiritAnim 中立即结算
 *
 * 使用方式（传入游戏真实武将）：
 *   var test = new DuelTestInstance(luBu, zhaoYun, seed: 12345);
 *   test.OnLog = Debug.Log;      // 可选：接管日志输出（默认输出到 Console）
 *   test.Run();                  // 一次性跑完整场
 *   // 或者逐步执行：while (!test.Step()) { }
 *
 * 也可以不传武将，仅按姓名+武力快速造两个测试武将：
 *   var test = new DuelTestInstance("吕布", 95, "赵云", 88, seed: 12345);
 *
 * 常用配置项：
 *   Verbose          - 是否输出每一步状态快照（默认 true）
 *   LogInternalDebug - 是否转发 Duel 内部调试日志（默认 false）
 *   View             - 是否启用表现层（默认 false；置 true 需要提供 Engine）
 *
 * 注意：
 *   Param.shoubyou 必须填 0（健康），否则武力计算结果会退化为 0
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core.Duel
{
    #region 测试用存根实现

    /// <summary>日志实现，把单挑内部日志转发到外部回调</summary>
    public class DuelTestLogger : Logger
    {
        public Action<string> OnLog;

        public void Debug(string text)
        {
            OnLog?.Invoke(text);
        }
    }

    /// <summary>测试用部队</summary>
    public class DuelTestUnit : Unit
    {
        public override bool IsPlayerControlled() { return false; }
        public override int GetTroops() { return 10000; }
        public override int AddEnergy(int value) { return value; }
        public override int GetColor() { return 0; }
        public override bool HasMember(PersonId id) { return false; }
    }

    /// <summary>
    /// 测试用系统：随机源走 DuelRandom（可设种子复现），其余为无副作用实现。
    /// 接入真实游戏时，请把这些方法替换为对项目实际接口的调用。
    /// </summary>
    public class DuelTestSystem : GameSystem
    {
        private readonly DuelTestLogger m_logger = new DuelTestLogger();

        /// <summary>日志回调</summary>
        public Action<string> OnLog { get { return m_logger.OnLog; } set { m_logger.OnLog = value; } }

        public override Engine GetEngine() { return null; }          // view = false，不需要表现层
        public override Logger GetLogger() { return m_logger; }
        public override int RandInt(int max) { return DuelRandom.Range(max); }
        public override bool RandBool(int percent) { return DuelRandom.Chance(percent); }
        public override Difficulty GetDifficulty() { return DuelSettings.Difficulty; }
        public override LifeMode GetLifeMode() { return DuelSettings.LifeMode; }
        public override BattleDeathMode GetBattleDeathMode() { return DuelSettings.BattleDeathMode; }
        public override bool IsFeatDisabled(Feature feature) { return DuelSettings.IsFeatDisabled(feature); }
        public override Force GetForce(int forceId) { return new Force(); }
        public override District GetDistrict(int districtId) { return null; }
        public override List<Item> GetPersonItemList(Person person) { return DuelSettings.GetPersonItemList(person); }
        public override int GetDuelItemPower(Person person) { return DuelSettings.GetDuelItemPower(person); }
        public override string GetMessage(Message msg) { return msg.Id.ToString(); }
        public override SystemEvents GetEvents() { return new SystemEvents(); }
        public override void Message(string text, object target, object[] args, bool pause) { }
        public override void HistoryLog(Message msg, Unit unit, bool show, int color) { }
        public override void Ping(object pos, int type, int color) { }
        public override string GetShoubyouName(int shoubyou) { return "伤病" + shoubyou; }
        public override void PersonSetShoubyou(Person person, int shoubyou) { person.injury = shoubyou; }
        public override void PersonAddHp(Person person, int value) { person.stamina += value; }
        public override void PersonAddExp(Person person, PersonStatType type, int subType, int value) { }
        public override void PersonAddKouseki(Person person, int value) { }
        public override void PersonDie(Person person, Person killer, Unit unit, Unit killerUnit, DeathType type, bool flag) { person.Dead(); }
        public override void HoryoShoguu(List<Person> all, List<Person> captured, Unit loserUnit, Unit winnerUnit) { }
        public override void PersonDetach(Person person, Person toPerson, Unit toUnit, Unit fromUnit) { }
        public override void DistrictAppointTotoku(District district, Force force) { }
        public override void ForceSetLike(int forceId, int targetForceId, int value) { }
        public override void ForceAddLike(int forceId, int targetForceId, int value) { }
        public override void ForceAddTechPoint(Force force, int value, Unit unit) { }
        public override int UnitAddTroops(Unit unit, int value) { return value; }
        public override void FloatingDamage(int value, FloatingCounterType type, Unit unit) { }
    }

    #endregion

    /// <summary>
    /// 单挑测试实例：构造一次单挑并逐步/一次性驱动它，用于验证单挑的逻辑流程。
    /// </summary>
    public class DuelTestInstance
    {
        /// <summary>单挑本体</summary>
        public Duel Duel { get; private set; }

        /// <summary>启动参数 / 结算结果</summary>
        public Duel.Param Param { get; private set; }

        /// <summary>
        /// 是否启用表现层。默认 false（纯逻辑推演）。
        /// 置为 true 时必须提供 Engine，否则阶段状态机会一直等待动画/输入。
        /// </summary>
        public bool View { get { return Duel != null && Duel.View; } set { if (Duel != null) Duel.View = value; } }

        /// <summary>日志回调（不设置则只输出到 Console）</summary>
        public Action<string> OnLog;

        /// <summary>是否输出每一步的状态快照（关闭后只在阶段切换与结束时输出）</summary>
        public bool Verbose { get; set; } = true;

        /// <summary>是否转发 Duel 内部调试日志（set_stance / action_anim 等）</summary>
        public bool LogInternalDebug { get; set; } = false;

        private DuelTestSystem m_system;
        private int m_frame;
        private bool m_finished;

        /// <summary>
        /// 用游戏真实武将构造一次单挑测试
        /// </summary>
        /// <param name="challenger">挑战方武将</param>
        /// <param name="challenged">应战方武将</param>
        /// <param name="seed">随机种子（相同种子可复现；传 null 则沿用项目 GameRandom）</param>
        /// <param name="maxBlowCounter">最大合数，超过判平局</param>
        /// <param name="charaCount">每队参战人数（1~3，仅用于测试替补登场/交替）</param>
        public DuelTestInstance(Person challenger, Person challenged,
            int? seed = 1, int maxBlowCounter = 50, int charaCount = 1)
        {
            if (seed.HasValue)
                DuelRandom.SetSeed(seed.Value);
            else
                DuelRandom.UseProjectRandom();

            Setup(challenger, challenged, maxBlowCounter, charaCount);
        }

        /// <summary>
        /// 便捷构造：按姓名+武力临时造两个测试武将（不需要游戏存档数据）
        /// </summary>
        public DuelTestInstance(string nameA, int strengthA, string nameB, int strengthB,
            int? seed = 1, int maxBlowCounter = 50)
            : this(CreatePerson(nameA, strengthA, 1), CreatePerson(nameB, strengthB, 2),
                   seed, maxBlowCounter, 1)
        {
        }

        private void Setup(Person challenger, Person challenged, int maxBlowCounter, int charaCount)
        {
            m_system = new DuelTestSystem();
            m_system.OnLog = text => { if (LogInternalDebug) Log("[内部] " + text); };

            Param = new Duel.Param
            {
                type = (int)DuelType.DuelType_2,
                maxBlowCounter = maxBlowCounter,
                stage = (int)DuelStage.DuelStage_Grassland,
                tutorial = false,
            };

            int count = Math.Max(1, Math.Min(charaCount, Duel.MaxTeamCharaCount));

            for (int i = 0; i < Duel.MaxTeamCount; i++)
            {
                bool isChallenger = i == (int)DuelTeam.DuelTeam_Challenger;
                Param.startChara[i] = 0;
                Param.control[i] = (int)DuelControl.DuelControl_Auto;   // 默认 AI 托管
                Param.playerId[i] = -1;
                Param.unit[i] = new DuelTestUnit();

                for (int j = 0; j < count; j++)
                {
                    Param.person[i][j] = isChallenger ? challenger : challenged;
                    Param.hp[i][j] = Duel.MaxHP;
                    Param.spirit[i][j] = 0;
                    // 伤病必须初始化为健康（0），否则武力计算结果会退化为 0
                    Param.shoubyou[i][j] = 0;
                }
            }

            Duel = new Duel(m_system, Param);
            Duel.View = false; // 默认无表现层
        }

        /// <summary>
        /// 造一个仅含单挑所需字段的测试武将（真实 Sango.Core.Person 实例）
        /// </summary>
        /// <param name="personality">性格（1胆小 / 2冷静 / 3刚胆 / 4莽撞），直接对应单挑 AI 性格</param>
        public static Person CreatePerson(string name, int strength, int id,
            int stamina = Duel.MaxHP, int loyalty = 100, int personality = 2)
        {
            Person person = new Person();
            person.Id = id;
            person.Name = name;
            person.state = (int)PersonStateType.Normal;
            person.strength = new PersonAttributeValue();
            person.strength.baseValue = strength;
            person.strength._value = strength;   // Value => _value + extra_value
            person.stamina = stamina;
            person.injury = 0;
            person.loyalty = loyalty;
            person.personality = personality;
            return person;
        }

        /// <summary>初始化单挑（构造后可调用；需要重置时可再次调用）</summary>
        public void Init()
        {
            m_frame = 0;
            m_finished = false;
            Duel.Init();
            Log($"== 单挑初始化 == 种子={DuelRandom.GetSeed()} 最大合数={Param.maxBlowCounter}");
            Log(DumpState());
        }

        /// <summary>
        /// 推进一帧（即阶段状态机的一个 step）
        /// </summary>
        /// <returns>true 表示单挑已结束</returns>
        public bool Step()
        {
            if (m_finished)
                return true;

            int prevPhase = Duel.Phase;
            m_finished = Duel.OnPhase(0);
            m_frame++;

            // 非详细模式下仅在阶段切换时输出
            if (Verbose || Duel.Phase != prevPhase)
                Log(DumpState());

            if (m_finished)
                Log(ReportResult());

            return m_finished;
        }

        /// <summary>一次性跑完整场单挑</summary>
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
            sb.Append($"[{m_frame:D4}] {GetPhaseName(Duel.Phase)} step={Duel.Step} ");
            sb.Append($"合数={Duel.BlowCounter} 状态={Duel.State}");

            for (int i = 0; i < Duel.MaxTeamCount; i++)
            {
                string side = i == (int)DuelTeam.DuelTeam_Challenger ? "挑战方" : "应战方";
                int chara = Duel.GetCurrentChara(i);
                Person person = Duel.GetCurrentPerson(i);
                sb.Append($" | {side}:{person.GetName()} hp={Duel.GetCurrentHp(i)} 斗志={Duel.GetCurrentSpirit(i, chara)}");
                sb.Append($" 方针={GetStanceName(Duel.GetStance(i, chara))}");
            }
            return sb.ToString();
        }

        /// <summary>输出最终结果</summary>
        public string ReportResult()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("== 单挑结束 ==");
            sb.Append($"结果={(DuelResult)Duel.Result} 胜队={Duel.WinnerTeam} 败队={Duel.LoserTeam} ");
            sb.AppendLine($"合数={Duel.BlowCounter} 状态标记=0x{Duel.Flags:x}");

            // 结算：把结果写回 Param（胜负奖惩、伤病、俘虏等）
            Duel.ResultHandler();
            sb.AppendLine($"结算后：param.winnerTeam={Param.winnerTeam} param.winnerChara={Param.winnerChara} " +
                          $"param.loserTeam={Param.loserTeam} param.loserChara={Param.loserChara}");

            for (int i = 0; i < Duel.MaxTeamCount; i++)
            {
                for (int j = 0; j < Duel.MaxTeamCharaCount; j++)
                {
                    if (!Utils.IsAlive(Param.person[i][j]))
                        continue;
                    sb.AppendLine($"  队伍{i} {Param.person[i][j].GetName()} 结局={(DuelCharaResult)Param.result[i][j]} " +
                                  $"hp={Param.hp[i][j]} 伤病={Param.shoubyou[i][j]}");
                }
            }
            return sb.ToString();
        }

        private void Log(string text)
        {
            if (OnLog != null)
                OnLog(text);
            else
                Console.WriteLine(text);
        }

        /// <summary>阶段名</summary>
        public static string GetPhaseName(int phase)
        {
            switch (phase)
            {
                case (int)DuelPhase.DuelPhase_Init: return "初始化";
                case (int)DuelPhase.DuelPhase_FTK: return "一击必杀";
                case (int)DuelPhase.DuelPhase_Opening: return "寒暄";
                case (int)DuelPhase.DuelPhase_TurnStart: return "回合开始";
                case (int)DuelPhase.DuelPhase_Join: return "登场";
                case (int)DuelPhase.DuelPhase_Command: return "指令输入";
                case (int)DuelPhase.DuelPhase_ActionStart: return "行动开始";
                case (int)DuelPhase.DuelPhase_SpecialCommand: return "必杀输入";
                case (int)DuelPhase.DuelPhase_Special: return "必杀";
                case (int)DuelPhase.DuelPhase_ActionEnd: return "行动结束";
                case (int)DuelPhase.DuelPhase_Retreat: return "退却";
                case (int)DuelPhase.DuelPhase_TurnEnd: return "回合结束";
                case (int)DuelPhase.DuelPhase_Closing: return "结束";
                default: return "无";
            }
        }

        /// <summary>行动方针名</summary>
        public static string GetStanceName(int stance)
        {
            switch (stance)
            {
                case (int)DuelStance.DuelStance_Attack: return "攻击重视";
                case (int)DuelStance.DuelStance_Defense: return "防御重视";
                case (int)DuelStance.DuelStance_Spirit: return "斗志重视";
                case (int)DuelStance.DuelStance_Fury: return "一发重视";
                default: return "无";
            }
        }
    }
}
