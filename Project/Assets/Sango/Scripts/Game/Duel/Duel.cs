/*
 * 文件名：Duel.cs
 * 描述：单挑(Duel)系统主体，由 s11_sys_duel.h + s11_sys_duel.cpp 翻译而来
 *
 * 翻译约定：
 *   1. C++ 的 typedef int xxx_t 统一使用 int，使 -1 表示无效值的语义得以保留
 *   2. 成员方法由 snake_case 改为 PascalCase；枚举/常量保留原始 C++ 命名以便对照
 *   3. C++ 的 std::array 改为 C# 数组；嵌套 struct 改为 class（引用类型，避免值拷贝语义差异）
 *   4. 原 blow_anim / hp_anim / spirit_anim 三个方法与同名嵌套类型冲突，重命名为 PlayBlowAnim 等
 *   5. 原 C++ 的 *this = Duel(...) 整体重置改为 Reset()
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sango.Core.Duel
{
    /// <summary>
    /// 单挑系统。对应 C++ Duel 类。
    /// 该类为 partial：AI 决策在 DuelAI.cs，阶段状态机在 DuelPhase.cs。
    /// </summary>
    public partial class Duel
    {
        #region 常量

        /// <summary>每队最多武将数</summary>
        public const int MaxTeamCharaCount = Unit.MaxMemberCount;

        /// <summary>队伍数</summary>
        public const int MaxTeamCount = (int)DuelTeam.DuelTeam_Max;

        /// <summary>体力上限</summary>
        public const int MaxHP = 100;

        /// <summary>斗志上限</summary>
        public const int MaxSpirit = 300;

        /// <summary>动画队列长度</summary>
        public const int MaxAnimQueueSize = 11;

        #endregion

        #region 嵌套类型

        /// <summary>单挑启动参数 / 结算结果。对应 Duel::Param</summary>
        public class Param
        {
            /// <summary>参战武将 [队伍][序号]</summary>
            public Person[][] person;
            /// <summary>参战部队</summary>
            public Unit[] unit;
            /// <summary>起始武将</summary>
            public int[] startChara;
            /// <summary>玩家 ID</summary>
            public int[] playerId;
            /// <summary>返回场景</summary>
            public Scene retScene = Scene.Scene_Max;
            /// <summary>单挑类型（台词相关）</summary>
            public int type = -1;
            /// <summary>操作方式</summary>
            public int[] control;
            /// <summary>最大合数</summary>
            public int maxBlowCounter = 50;
            /// <summary>场地</summary>
            public int stage = (int)DuelStage.DuelStage_Grassland;
            /// <summary>是否教程</summary>
            public bool tutorial = false;
            /// <summary>一击必杀类型</summary>
            public int ftkType = -1;
            /// <summary>一击必杀队伍</summary>
            public int ftkTeam = -1;
            /// <summary>胜利队伍</summary>
            public int winnerTeam = -1;
            /// <summary>失败队伍</summary>
            public int loserTeam = -1;
            /// <summary>胜利武将</summary>
            public int winnerChara = -1;
            /// <summary>失败武将</summary>
            public int loserChara = -1;
            /// <summary>武将结局 [队伍][序号]</summary>
            public int[][] result;
            /// <summary>体力 [队伍][序号]</summary>
            public int[][] hp;
            /// <summary>斗志 [队伍][序号]</summary>
            public int[][] spirit;
            /// <summary>伤病 [队伍][序号]</summary>
            public int[][] shoubyou;
            /// <summary>结束时合数</summary>
            public int endBlowCounter = 0;
            /// <summary>状态标记</summary>
            public int flags = 0;

            public Param()
            {
                person = NewArray2D<Person>(MaxTeamCount, MaxTeamCharaCount);
                unit = new Unit[MaxTeamCount];
                startChara = new int[] { -1, -1 };
                playerId = new int[] { -1, -1 };
                control = new int[] { -1, -1 };
                result = NewArray2D<int>(MaxTeamCount, MaxTeamCharaCount);
                hp = NewArray2D<int>(MaxTeamCount, MaxTeamCharaCount);
                spirit = NewArray2D<int>(MaxTeamCount, MaxTeamCharaCount);
                shoubyou = NewArray2D<int>(MaxTeamCount, MaxTeamCharaCount);

                Fill(result, 0);
                Fill(hp, MaxHP);
                Fill(spirit, 0);
                Fill(shoubyou, -1);
            }
        }

        /// <summary>单挑中的武将数据。对应 Duel::Character</summary>
        public class Character
        {
            public Person person = null;
            public int hp = 0;
            public int spirit = 0;
            public int shoubyou = -1;
            public int stance = -1;
            public int state = -1;
            public int number = -1;
            public Bitset32 item = Bitset32.Empty;
            public int[] specialRemainingCount = new int[(int)DuelSpecial.DuelSpecial_Max];
        }

        /// <summary>单挑中的队伍数据。对应 Duel::Team</summary>
        public class Team
        {
            public Character[] chara = NewCharacterArray();
            public int charaCount = 0;
            public int currentChara = -1;
            public int playerId = -1;
            /// <summary>登场禁止计时器</summary>
            public int appearingTimer = 0;
            /// <summary>交替禁止计时器</summary>
            public int switchingTimer = 0;
            /// <summary>无敌计时器</summary>
            public int invulnerableTimer = 0;
            public int control = -1;
            /// <summary>增益状态计时器</summary>
            public int[] buffTimer = new int[(int)DuelBuffType.DuelBuffType_Max];
            /// <summary>行动方针持续回合</summary>
            public int stanceTimer = 0;

            internal static Character[] NewCharacterArray()
            {
                Character[] array = new Character[MaxTeamCharaCount];
                for (int i = 0; i < array.Length; i++)
                    array[i] = new Character();
                return array;
            }
        }

        /// <summary>AI 上下文。对应 Duel::AI</summary>
        public class AI
        {
            /// <summary>AI 决策表行。对应 Duel::AI::Row</summary>
            public class Row
            {
                public int chance = 0;
                public int id = (int)DuelAIRow.DuelAIRow_Max;
                public int param1 = 0;
                public int param2 = 0;

                public Row() { }
                public Row(int chance, int id, int param1, int param2)
                {
                    this.chance = chance;
                    this.id = id;
                    this.param1 = param1;
                    this.param2 = param2;
                }
            }

            public bool initialized = false;
            public Duel parent = null;
            public int team = -1;
            public int opponentTeam = -1;
            public int chara = -1;
            public int opponentChara = -1;
        }

        /// <summary>合数动画数据。对应 Duel::BlowAnim</summary>
        public class BlowAnim
        {
            public int value = -1;
        }

        /// <summary>体力动画数据。对应 Duel::HPAnim</summary>
        public class HPAnim
        {
            public int damage = 0;
            public int atkTeam = -1;
            public int defTeam = -1;
            public int atkChara = -1;
            public int defChara = -1;
            public int shoubyouDamage = 0;
        }

        /// <summary>斗志动画数据。对应 Duel::SpiritAnim</summary>
        public class SpiritAnim
        {
            public int atkValue = 0;
            public int atkTeam = -1;
            public int atkChara = -1;
            public int defValue = 0;
            public int defTeam = -1;
            public int defChara = -1;
        }

        /// <summary>普通行动。对应 Duel::Action</summary>
        public class Action
        {
            public int team = -1;
            public int chara = -1;
            public int type = -1;
            public int result = -1;
        }

        /// <summary>必杀行动。对应 Duel::SpecialAction</summary>
        public class SpecialAction
        {
            public int team = -1;
            public int chara = -1;
            public int type = -1;
            public int result = -1;
        }

        #endregion

        #region 静态表

        /// <summary>行动方针系数。对应 Stance 结构体（8b1750 StanceCoef）</summary>
        private class Stance
        {
            public int speed;      // 速度
            public int hit;        // 命中
            public int attack;     // 攻击力
            public int block;      // 防御
            public int _10;
            public int _14;
            public int attackSub;  // 攻击力（副）
            public int spiritGain; // 被击时斗志回复

            public Stance(int speed, int hit, int attack, int block, int _10, int _14, int attackSub, int spiritGain)
            {
                this.speed = speed;
                this.hit = hit;
                this.attack = attack;
                this.block = block;
                this._10 = _10;
                this._14 = _14;
                this.attackSub = attackSub;
                this.spiritGain = spiritGain;
            }
        }

        /// <summary>必杀斗志消耗（837398 SpecialSpiritCost）</summary>
        private static readonly int[] SpecialSpiritCost = new int[(int)DuelSpecial.DuelSpecial_Max]
        {
            100, 100, 100, 100, 200, 300, 0, 0,
        };

        /// <summary>行动方针系数表（8b1750）</summary>
        private static readonly Stance[] StanceCoef = new Stance[(int)DuelStance.DuelStance_Max]
        {
            new Stance(42,  71, 16, 12,  5, 25, 3,  4), // 攻击重视
            new Stance(10, 200, 14, 90, 32,  5, 2,  8), // 防御重视
            new Stance(15, 125, 14, 55, 12, 12, 3, 10), // 斗志重视
            new Stance( 5,  71, 16, 22, 12, 12, 3,  7), // 一发重视
        };

        /// <summary>一击必杀的合数（8373b8）</summary>
        private static readonly int[] FtkBlowCount = new int[(int)DuelFtkType.DuelFtkType_Max] { 1, 1, 1 };

        /// <summary>必定被格挡的组合（8373f0）</summary>
        private static readonly PersonId[] BlockTable = new PersonId[3]
        {
            PersonId.Ryofu, PersonId.Chouhi, PersonId.Kanu,
        };

        #endregion

        #region 字段

        protected int phase = -1;
        protected int nextPhase = -1;
        protected int step = -1;
        /// <summary>合数</summary>
        protected int blowCounter = 0;
        protected DuelState state = DuelState.DuelState_Play;
        /// <summary>最大合数</summary>
        protected int maxBlowCounter = 0;
        /// <summary>是否反转队伍（使用时 Param 的队伍也需反转）</summary>
        protected bool reverse = false;
        /// <summary>单挑类型（台词相关）</summary>
        protected int type = -1;
        protected Team[] team = NewTeamArray();
        /// <summary>攻击比例 [挑战方武将][应战方武将]</summary>
        protected int[][] actionRatio = NewArray2D<int>(MaxTeamCharaCount, MaxTeamCharaCount);
        /// <summary>消息框弹出时中断</summary>
        protected bool messageboxBlocking = false;
        protected object scene = null;
        protected object ui = null;
        protected object uiStance = null;
        protected object uiSpecial = null;
        protected object uiResult = null;
        protected AI[] ai = NewAIArray();
        protected int ftkTeam = -1;
        protected int ftkType = -1;
        /// <summary>即将登场的武将</summary>
        protected int[] appearingChara = new int[] { -1, -1 };
        /// <summary>即将交替的武将</summary>
        protected int[] switchingChara = new int[] { -1, -1 };
        /// <summary>即将退却的武将？（原代码未命名）</summary>
        protected int[] retreatChara = new int[] { -1, -1 };
        protected BlowAnim[] blowAnimQueue = NewBlowAnimArray();
        protected HPAnim[] hpAnimQueue = NewHPAnimArray();
        protected SpiritAnim[] spiritAnimQueue = NewSpiritAnimArray();
        protected int actionCount = 0;
        protected Action[] actionQueue = NewActionArray();
        protected SpecialAction specialAction = new SpecialAction();
        protected int winnerTeam = -1;
        protected int loserTeam = -1;
        protected int result = -1;
        protected int flags = 0;
        /// <summary>下回合将进入必杀阶段的队伍</summary>
        protected int specialTryTeam = -1;

        protected GameSystem system = null;
        protected Engine engine = null;
        protected Param param = null;
        protected bool view = false;

        #endregion

        #region 对外只读访问器

        /// <summary>当前阶段</summary>
        public int Phase { get { return phase; } }
        /// <summary>当前步骤</summary>
        public int Step { get { return step; } }
        /// <summary>当前运行状态</summary>
        public DuelState State { get { return state; } }
        /// <summary>当前合数</summary>
        public int BlowCounter { get { return blowCounter; } }
        /// <summary>最大合数</summary>
        public int MaxBlowCounter { get { return maxBlowCounter; } }
        /// <summary>单挑结果（结算后有效）</summary>
        public int Result { get { return result; } }
        /// <summary>胜利队伍</summary>
        public int WinnerTeam { get { return winnerTeam; } }
        /// <summary>失败队伍</summary>
        public int LoserTeam { get { return loserTeam; } }
        /// <summary>状态标记</summary>
        public int Flags { get { return flags; } }
        /// <summary>是否启用表现层（false 时为纯逻辑推演，数值立即结算）</summary>
        public bool View { get { return view; } set { view = value; } }
        /// <summary>启动参数 / 结算结果</summary>
        public Param DuelParam { get { return param; } }
        /// <summary>是否已结束</summary>
        public bool IsFinished { get { return result >= 0 && phase == (int)DuelPhase.DuelPhase_Closing; } }

        #endregion

        #region 构造 / 初始化

        public Duel(GameSystem system, Param param)
        {
            this.system = system;
            this.engine = system != null ? system.GetEngine() : null;
            this.param = param;
        }

        /// <summary>重置到初始状态。对应 C++ 中 init() 里的 *this = Duel(system_, param_)</summary>
        protected virtual void Reset()
        {
            phase = -1;
            nextPhase = -1;
            step = -1;
            blowCounter = 0;
            state = DuelState.DuelState_Play;
            maxBlowCounter = 0;
            reverse = false;
            type = -1;
            team = NewTeamArray();
            actionRatio = NewArray2D<int>(MaxTeamCharaCount, MaxTeamCharaCount);
            messageboxBlocking = false;
            scene = null;
            ui = null;
            uiStance = null;
            uiSpecial = null;
            uiResult = null;
            ai = NewAIArray();
            ftkTeam = -1;
            ftkType = -1;
            appearingChara = new int[] { -1, -1 };
            switchingChara = new int[] { -1, -1 };
            retreatChara = new int[] { -1, -1 };
            blowAnimQueue = NewBlowAnimArray();
            hpAnimQueue = NewHPAnimArray();
            spiritAnimQueue = NewSpiritAnimArray();
            actionCount = 0;
            actionQueue = NewActionArray();
            specialAction = new SpecialAction();
            winnerTeam = -1;
            loserTeam = -1;
            result = -1;
            flags = 0;
            specialTryTeam = -1;
        }

        internal static T[][] NewArray2D<T>(int a, int b)
        {
            T[][] array = new T[a][];
            for (int i = 0; i < a; i++)
                array[i] = new T[b];
            return array;
        }

        internal static void Fill(int[][] array, int value)
        {
            for (int i = 0; i < array.Length; i++)
            {
                for (int j = 0; j < array[i].Length; j++)
                    array[i][j] = value;
            }
        }

        private static Team[] NewTeamArray()
        {
            Team[] array = new Team[MaxTeamCount];
            for (int i = 0; i < array.Length; i++)
                array[i] = new Team();
            return array;
        }

        private static AI[] NewAIArray()
        {
            AI[] array = new AI[MaxTeamCount];
            for (int i = 0; i < array.Length; i++)
                array[i] = new AI();
            return array;
        }

        private static BlowAnim[] NewBlowAnimArray()
        {
            BlowAnim[] array = new BlowAnim[MaxAnimQueueSize];
            for (int i = 0; i < array.Length; i++)
                array[i] = new BlowAnim();
            return array;
        }

        private static HPAnim[] NewHPAnimArray()
        {
            HPAnim[] array = new HPAnim[MaxAnimQueueSize];
            for (int i = 0; i < array.Length; i++)
                array[i] = new HPAnim();
            return array;
        }

        private static SpiritAnim[] NewSpiritAnimArray()
        {
            SpiritAnim[] array = new SpiritAnim[MaxAnimQueueSize];
            for (int i = 0; i < array.Length; i++)
                array[i] = new SpiritAnim();
            return array;
        }

        private static Action[] NewActionArray()
        {
            Action[] array = new Action[MaxAnimQueueSize];
            for (int i = 0; i < array.Length; i++)
                array[i] = new Action();
            return array;
        }

        private static Bitset32 ItemFlags(DuelItemType type)
        {
            return new Bitset32((int)type);
        }

        private void LogDebug(string text)
        {
            Logger logger = system != null ? system.GetLogger() : null;
            if (logger != null)
                logger.Debug(text);
        }

        #endregion

        #region 结算（4d3940 - 4d43c0）

        /// <summary>4d3940。负伤报告</summary>
        public void ResultInjuryReport(Unit unit, Person person, bool last)
        {
            if (!Utils.IsAlive(unit))
                return;
            if (!Utils.IsAlive(person))
                return;
            if (last)
                system.Ping(unit, (int)PingType.Repeat, unchecked((int)0x80808080));
            Message msg = new Message();
            msg.SetObj0Str0(DuelMessageId.LD_WAR_IKKI_INJURY, person, system.GetShoubyouName(person.GetShoubyou()));
            system.HistoryLog(msg, unit, true, person.GetColor());
            if (person.IsPlayer())
            {
                msg.SetObj0(DuelMessageId.N_WAR_IKKI_INJURY, person);
                system.Message(system.GetMessage(msg), null, null, true);
            }
        }

        /// <summary>4d3a80。应用伤病与体力</summary>
        public void ResultInjury()
        {
            int[] injured = new int[MaxTeamCount];
            for (int i = 0; i < MaxTeamCount; i++)
            {
                for (int j = 0; j < MaxTeamCharaCount; j++)
                {
                    Person person = ParamGetPerson(param, i, j);
                    if (!Utils.IsAlive(person))
                        continue;
                    int shoubyou = ParamGetShoubyou(param, i, j);
                    if (person.GetShoubyou() < shoubyou)
                        injured[i]++;
                    system.PersonSetShoubyou(person, shoubyou);
                    int hp = ParamGetHp(param, i, j);
                    hp = Math.Max(hp, 1);
                    system.PersonAddHp(person, hp - person.GetHp());
                }
            }
            for (int i = 0; i < MaxTeamCount; i++)
            {
                for (int j = 0; j < MaxTeamCharaCount; j++)
                {
                    if (injured[i] == 0)
                        continue;
                    injured[i]--;
                    Unit unit = ParamGetUnit(param, i, j);
                    Person person = ParamGetPerson(param, i, j);
                    if (!Utils.IsAlive(unit))
                        continue;
                    if (!Utils.IsAlive(person))
                        continue;
                    ResultInjuryReport(unit, person, injured[i] == 0);
                }
            }
        }

        /// <summary>4d3bd0。平局结算</summary>
        public void ResultDraw()
        {
            Person challenger = ParamGetChallenger(param);
            Person challenged = ParamGetChallenged(param);
            if (!Utils.IsAlive(challenger))
                return;
            if (!Utils.IsAlive(challenged))
                return;
            if (ParamIsManual(param))
            {
                Message msg = new Message();
                msg.SetObj0Obj1(DuelMessageId.F_WAR_IKKI_HIKIWAKE_A, challenger, challenged);
                system.Message(system.GetMessage(msg), challenger, null, true);
            }
            ResultInjury();
            system.PersonAddExp(challenger, PersonStatType.Strength, -1, 3);
            system.PersonAddExp(challenged, PersonStatType.Strength, -1, 3);
            system.PersonAddKouseki(challenger, 50);
            system.PersonAddKouseki(challenged, 50);
        }

        /// <summary>4d3cb0。胜负结算</summary>
        public void ResultNormal()
        {
            Person challenger = ParamGetChallenger(param);
            Person challenged = ParamGetChallenged(param);
            if (!Utils.IsAlive(challenger))
                return;
            if (!Utils.IsAlive(challenged))
                return;
            if (!Utils.InRange(param.winnerTeam, 0, MaxTeamCount - 1))
                return;
            if (!Utils.InRange(param.winnerChara, 0, MaxTeamCharaCount - 1))
                return;
            if (!Utils.InRange(param.loserTeam, 0, MaxTeamCount - 1))
                return;
            if (!Utils.InRange(param.loserChara, 0, MaxTeamCharaCount - 1))
                return;
            Person winnerPerson = ParamGetWinnerPerson(param);
            Person loserPerson = ParamGetLoserPerson(param);
            Unit winnerUnit = ParamGetWinnerUnit(param);
            Unit loserUnit = ParamGetLoserUnit(param);
            Unit challengerUnit = param.unit[(int)DuelTeam.DuelTeam_Challenger];
            Unit challengedUnit = param.unit[(int)DuelTeam.DuelTeam_Challenged];
            if (!Utils.IsAlive(winnerPerson))
                return;
            if (!Utils.IsAlive(loserPerson))
                return;
            if (!Utils.IsAlive(winnerUnit))
                return;
            if (!Utils.IsAlive(loserUnit))
                return;
            if (!Utils.IsAlive(challengerUnit))
                return;
            if (!Utils.IsAlive(challengedUnit))
                return;
            bool playerControlled = winnerUnit.IsPlayerControlled() || loserUnit.IsPlayerControlled();
            Force winnerForce = system.GetForce(winnerPerson.GetForceId());
            Force loserForce = system.GetForce(loserPerson.GetForceId());
            Force challengerForce = system.GetForce(challenger.GetForceId());
            if (!Utils.IsAlive(winnerForce))
                return;
            if (!Utils.IsAlive(loserForce))
                return;
            if (!Utils.IsAlive(challengerForce))
                return;
            system.GetEvents().OnDuelFinished();
            int loserResult = ParamGetCharaResult(param, param.loserTeam, param.loserChara);
            // 一方不是普通势力时不发生俘虏或死亡
            if (!winnerForce.IsNormal() || !loserForce.IsNormal())
                loserResult = (int)DuelCharaResult.DuelCharaResult_Escaped;
            Message msg = new Message();
            bool captured = false;
            bool dead = false;
            switch (loserResult)
            {
                case (int)DuelCharaResult.DuelCharaResult_Escaped:
                    if (playerControlled)
                    {
                        msg.SetObj0Obj1(DuelMessageId.F_WAR_IKKI_ATO_A, winnerPerson, loserPerson);
                        system.Message(system.GetMessage(msg), winnerPerson, null, true);
                        msg.SetObj0Obj1(DuelMessageId.F_WAR_IKKI_ATO_B, loserPerson, winnerPerson);
                        system.Message(system.GetMessage(msg), loserPerson, null, true);
                    }
                    break;
                case (int)DuelCharaResult.DuelCharaResult_Captured:
                    if (playerControlled)
                    {
                        msg.SetObj0Obj1(DuelMessageId.F_WAR_IKKI_HORYO_A, winnerPerson, loserPerson);
                        system.Message(system.GetMessage(msg), winnerPerson, null, true);
                        msg.SetObj0Obj1(DuelMessageId.F_WAR_IKKI_HORYO_B, loserPerson, winnerPerson);
                        system.Message(system.GetMessage(msg), loserPerson, null, true);
                    }
                    captured = true;
                    break;
                case (int)DuelCharaResult.DuelCharaResult_Dead:
                    if (playerControlled)
                    {
                        msg.SetObj0Obj1(DuelMessageId.F_WAR_IKKI_SHIBOU, winnerPerson, loserPerson);
                        system.Message(system.GetMessage(msg), winnerPerson, null, true);
                    }
                    dead = true;
                    break;
            }
            if (winnerForce.IsPlayer())
            {
                msg.SetObj0Obj1(DuelMessageId.LB_WAR_IKKI_WIN, winnerPerson, loserPerson);
                system.HistoryLog(msg, winnerUnit, true, winnerUnit.GetColor());
            }
            else if (loserForce.IsPlayer())
            {
                msg.SetObj0Obj1(DuelMessageId.LD_WAR_IKKI_LOST, loserPerson, winnerPerson);
                system.HistoryLog(msg, loserUnit, true, loserUnit.GetColor());
            }
            system.Ping(challengerUnit.GetPos(), 0, unchecked((int)0x80808080));
            ResultInjury();
            if (captured)
            {
                District district = system.GetDistrict(loserPerson.GetDistrictId());
                List<Person> all = new List<Person>();
                List<Person> capturedList = new List<Person>();
                all.Add(loserPerson);
                capturedList.Add(loserPerson);
                system.HoryoShoguu(all, capturedList, loserUnit, winnerUnit);
                if (loserUnit.HasMember(loserPerson.GetId()))
                    system.PersonDetach(loserPerson, winnerPerson, winnerUnit, loserUnit);
                if (Utils.IsAlive(district))
                    system.DistrictAppointTotoku(district, winnerForce);
                if (!Utils.IsAlive(loserUnit) && Utils.IsAlive(loserForce) && winnerForce.IsNormal())
                {
                    if (loserForce.GetLike(winnerForce.GetId()) > 15)
                        system.ForceSetLike(loserForce.GetId(), winnerForce.GetId(), 15);
                    else
                        system.ForceAddLike(loserForce.GetId(), winnerForce.GetId(), -1);
                }
            }
            else if (dead)
            {
                system.PersonDie(loserPerson, winnerPerson, null, null, DeathType.Natural, false);
            }
            int troopsDamage = 0;
            if (Utils.IsAlive(loserUnit))
                troopsDamage = Math.Min(loserUnit.GetTroops() * 30 / 100, 2000 + system.RandInt(50));
            int energyChange = winnerUnit.AddEnergy(15);
            system.FloatingDamage(energyChange, FloatingCounterType.Energy, winnerUnit);
            if (Utils.IsAlive(loserUnit))
            {
                energyChange = loserUnit.AddEnergy(-15);
                system.FloatingDamage(energyChange, FloatingCounterType.Energy, loserUnit);
                int troopsChange = system.UnitAddTroops(loserUnit, -troopsDamage);
                loserUnit.SyncEquipmentQuantity();
                system.FloatingDamage(troopsChange, FloatingCounterType.Troops, loserUnit);
            }
            system.PersonAddExp(winnerPerson, PersonStatType.Strength, -1, 10);
            if (Utils.IsAlive(loserPerson))
                system.PersonAddExp(loserPerson, PersonStatType.Strength, -1, 1);
            system.PersonAddKouseki(winnerPerson, loserResult == (int)DuelCharaResult.DuelCharaResult_Captured ? 200 : 100);
            if (Utils.IsAlive(loserPerson))
                system.PersonAddKouseki(loserPerson, 10);
            if (Utils.IsAlive(winnerForce))
                system.ForceAddTechPoint(winnerForce, 50, null);
        }

        /// <summary>4d43c0。结算入口</summary>
        public void ResultHandler()
        {
            if (Utils.InRange(param.winnerTeam, 0, MaxTeamCount - 1) && Utils.InRange(param.winnerChara, 0, MaxTeamCharaCount - 1))
                ResultNormal();
            else
                ResultDraw();
        }

        #endregion

        #region 有效性 / 基础查询（4fc940 - 506ec0）

        /// <summary>4fc940</summary>
        public bool IsValid(Action action)
        {
            if (!Utils.InRange(action.team, 0, MaxTeamCount - 1))
                return false;
            if (!Utils.InRange(action.chara, 0, MaxTeamCharaCount - 1))
                return false;
            if (!Utils.InRange(action.type, 0, (int)DuelAction.DuelAction_Max - 1))
                return false;
            if (!Utils.InRange(action.result, 0, (int)DuelActionResult.DuelActionResult_Max - 1))
                return false;
            return true;
        }

        /// <summary>4fc980</summary>
        public bool IsValid(SpecialAction special)
        {
            if (!Utils.InRange(special.team, 0, MaxTeamCount - 1))
                return false;
            if (!Utils.InRange(special.chara, 0, MaxTeamCharaCount - 1))
                return false;
            if (!Utils.InRange(special.type, 0, (int)DuelSpecial.DuelSpecial_Max - 1))
                return false;
            if (!Utils.InRange(special.result, 0, (int)DuelSpecialResult.DuelSpecialResult_Max - 1))
                return false;
            return true;
        }

        /// <summary>4560a0, v+14。按钮是否可用（教程用）</summary>
        public virtual bool IsButtonEnabled(int button)
        {
            return true;
        }

        /// <summary>5067b0, v+8</summary>
        public virtual void Exit()
        {
        }

        /// <summary>5067c0</summary>
        public bool IsPlayer(int team)
        {
            return Utils.InRange(this.team[team].playerId, 0, Player.Max - 1);
        }

        /// <summary>5067f0</summary>
        public bool IsManual(int team)
        {
            return this.team[team].control == (int)DuelControl.DuelControl_Manual;
        }

        /// <summary>506820</summary>
        public int GetCurrentChara(int team)
        {
            return this.team[team].currentChara;
        }

        /// <summary>506850</summary>
        public int GetOpponentTeam(int team)
        {
            if (Utils.InRange(team, 0, MaxTeamCount - 1))
                return team == (int)DuelTeam.DuelTeam_Challenger ? (int)DuelTeam.DuelTeam_Challenged : (int)DuelTeam.DuelTeam_Challenger;
            return -1;
        }

        /// <summary>506870</summary>
        public Person GetPerson(int team, int chara)
        {
            return TeamGetPerson(this.team[team], chara);
        }

        /// <summary>5068b0</summary>
        public Person GetCurrentPerson(int team)
        {
            if (!Utils.InRange(this.team[team].currentChara, 0, MaxTeamCharaCount - 1))
                return null;
            return TeamGetPerson(this.team[team], this.team[team].currentChara);
        }

        /// <summary>5068f0</summary>
        public int GetStance(int team, int chara)
        {
            return TeamGetStance(this.team[team], chara);
        }

        /// <summary>506930</summary>
        public int GetHp(int team, int chara)
        {
            return TeamGetHp(this.team[team], chara);
        }

        /// <summary>506970</summary>
        public void SetHp(int team, int chara, int value)
        {
            TeamSetHp(this.team[team], chara, value);
        }

        /// <summary>5069b0</summary>
        public int AddHp(int team, int chara, int value)
        {
            if (value < 0)
            {
                if (team == (reverse ? (int)DuelTeam.DuelTeam_Challenged : (int)DuelTeam.DuelTeam_Challenger))
                    Utils.SetBits(ref flags, (int)DuelStatus.DuelStatus_ChallengerHPDamaged);
                else
                    Utils.SetBits(ref flags, (int)DuelStatus.DuelStatus_ChallengedHPDamaged);
            }
            return TeamAddHp(this.team[team], chara, value);
        }

        /// <summary>506a20</summary>
        public int GetCurrentHp(int team)
        {
            if (!Utils.InRange(this.team[team].currentChara, 0, MaxTeamCharaCount - 1))
                return 0;
            return TeamGetHp(this.team[team], this.team[team].currentChara);
        }

        /// <summary>506a60</summary>
        public int GetStrength(int team, int chara, bool revised)
        {
            return TeamGetStrength(this.team[team], chara, revised);
        }

        /// <summary>506aa0</summary>
        public int GetCurrentStrength(int team, bool revised)
        {
            if (!Utils.InRange(this.team[team].currentChara, 0, MaxTeamCharaCount - 1))
                return Person.MinStat;
            return TeamGetStrength(this.team[team], this.team[team].currentChara, revised);
        }

        /// <summary>506ae0</summary>
        public int GetSpirit(int team, int chara)
        {
            return TeamGetSpirit(this.team[team], chara);
        }

        /// <summary>506b20</summary>
        public void SetSpirit(int team, int chara, int value)
        {
            TeamSetSpirit(this.team[team], chara, value);
        }

        /// <summary>506b60</summary>
        public int AddSpirit(int team, int chara, int value)
        {
            return TeamAddSpirit(this.team[team], chara, value);
        }

        /// <summary>506ba0</summary>
        public int GetCurrentSpirit(int team, int chara)
        {
            if (!Utils.InRange(this.team[team].currentChara, 0, MaxTeamCharaCount - 1))
                return 0;
            return TeamGetSpirit(this.team[team], this.team[team].currentChara);
        }

        /// <summary>506be0</summary>
        public int GetSwitchingTimer(int team)
        {
            return this.team[team].switchingTimer;
        }

        /// <summary>506c10</summary>
        public bool IsInjured(int team, int chara)
        {
            return TeamGetShoubyou(this.team[team], chara) != (int)Shoubyou.Kenkou;
        }

        /// <summary>506c50</summary>
        public bool HasBuff(int team, int type)
        {
            return TeamHasBuff(this.team[team], type);
        }

        /// <summary>506c90</summary>
        public bool HasItem(int team, int chara, Bitset32 flags)
        {
            return TeamHasItem(this.team[team], chara, flags);
        }

        /// <summary>506cd0</summary>
        public bool IsInvulnerable(int team, int chara)
        {
            return this.team[team].invulnerableTimer > 0;
        }

        /// <summary>506d10</summary>
        public int GetStanceTimer(int team)
        {
            return this.team[team].stanceTimer;
        }

        /// <summary>506d40</summary>
        public void SetUi(object ui)
        {
        }

        /// <summary>506d90</summary>
        public bool CheckState(int team, int chara, int state)
        {
            bool active = TeamIsActive(this.team[team], chara);
            if (active && Utils.InRange(state, 0, (int)DuelCharaState.DuelCharaState_Max - 1))
                return TeamGetState(this.team[team], chara) == state;
            return active;
        }

        /// <summary>506e00</summary>
        public bool IsJoined(int team, int chara)
        {
            for (int i = 0; i < (int)DuelCharaState.DuelCharaState_Max; i++)
            {
                if (i == (int)DuelCharaState.DuelCharaState_NotJoined)
                    continue;
                if (CheckState(team, chara, i))
                    return true;
            }
            return false;
        }

        /// <summary>506e70</summary>
        public bool IsManual()
        {
            for (int i = 0; i < MaxTeamCount; i++)
            {
                if (team[i].control == (int)DuelControl.DuelControl_Manual)
                    return true;
            }
            return false;
        }

        #endregion

        #region 必杀相关（506ec0 - 5074b0）

        /// <summary>506ec0。是否有可用的必杀</summary>
        public bool CanSpecial(int team, int chara)
        {
            for (int i = 0; i < (int)DuelSpecial.DuelSpecial_Max; i++)
            {
                if (IsSpecialEnabled(team, chara, i))
                    return true;
            }
            return false;
        }

        /// <summary>506f20。必杀剩余次数是否还有</summary>
        public bool IsSpecialAvailable(int team, int chara, int special)
        {
            if (!Utils.InRange(special, 0, (int)DuelSpecial.DuelSpecial_Max - 1))
                return false;
            return TeamGetSpecialRemainingCount(this.team[team], chara, special) != 0;
        }

        /// <summary>506f70。必杀斗志消耗</summary>
        public int GetSpecialSpiritCost(int special)
        {
            return SpecialSpiritCost[special];
        }

        /// <summary>5070f0</summary>
        public void SetStance(int team, int chara, int stance)
        {
            if (TeamIsActive(this.team[team], chara) && GetStance(team, chara) != stance)
                this.team[team].stanceTimer = 0;
            TeamSetStance(this.team[team], chara, stance);
            LogDebug($"Duel::set_stance {team}-{chara} {stance} 0x{system.GetSeed():x}");
        }

        /// <summary>507170</summary>
        public int CreateCooldownTimer(int team, int chara)
        {
            return 4 + system.RandInt(3);
        }

        /// <summary>5071b0</summary>
        public int GetSpecialRemainingCount(int team, int chara, int special)
        {
            return TeamGetSpecialRemainingCount(this.team[team], chara, special);
        }

        /// <summary>507200</summary>
        public void SetSpecialRemainingCount(int team, int chara, int special, int value)
        {
            TeamSetSpecialRemainingCount(this.team[team], chara, special, value);
        }

        /// <summary>507250。查表获取攻击比例</summary>
        public int GetActionRatio(int aTeam, int aChara, int bTeam, int bChara)
        {
            if (aTeam == bTeam)
                return 0;
            if (aTeam == (int)DuelTeam.DuelTeam_Challenger)
                return actionRatio[aChara][bChara];
            else
                return 100 - actionRatio[bChara][aChara];
        }

        /// <summary>5072d0。必杀命中次数</summary>
        public int GetSpecialHitCount(SpecialAction special)
        {
            switch (special.type)
            {
                case (int)DuelSpecial.DuelSpecial_Hissatsuwaza:
                // case DuelSpecial_Kiai:
                // case DuelSpecial_Kenshu:
                // case DuelSpecial_Taikyaku:
                case (int)DuelSpecial.DuelSpecial_Kyuusho:
                case (int)DuelSpecial.DuelSpecial_Musou:
                case (int)DuelSpecial.DuelSpecial_Anki:
                case (int)DuelSpecial.DuelSpecial_Nisetaikyaku:
                    return 1;
            }
            return 0;
        }

        /// <summary>507320。退却成功概率</summary>
        public bool CalcRetreatChance(int team, int chara)
        {
            Person person = GetPerson(team, chara);
            if (Utils.IsActive(person) && person.HasSkill(SkillId.Kyouun))
                return true;
            int opponentTeam = GetOpponentTeam(team);
            int opponentChara = this.team[opponentTeam].currentChara;
            if (Utils.InRange(opponentChara, 0, MaxTeamCharaCount - 1))
            {
                bool horse = TeamHasItem(this.team[team], chara, ItemFlags(DuelItemType.DuelItemType_EliteHorse));
                bool opponentHorse = TeamHasItem(this.team[opponentTeam], opponentChara, ItemFlags(DuelItemType.DuelItemType_EliteHorse));
                if (horse)
                {
                    if (!opponentHorse)
                        return true;
                }
                else
                {
                    if (opponentHorse)
                        return false;
                }
            }
            if (blowCounter > 10)
            {
                int n = blowCounter;                    // 10 ..
                n += GetHp(team, chara) / 2;            // 0 .. 50
                n += GetStrength(team, chara, true);    // 1 .. 110
                return n > system.RandInt(100);
            }
            else
            {
                int n = 0;
                n += GetHp(team, chara) / 2;            // 0 .. 50
                n += GetStrength(team, chara, true);    // 1 .. 110
                return n > system.RandInt(250);
            }
        }

        /// <summary>5074b0。暴击发生概率</summary>
        public bool CalcCriticalChance(int team, int chara)
        {
            int chance = 0;
            int stance = GetStance(team, chara);
            int t = GetStanceTimer(team);
            switch (stance)
            {
                case (int)DuelStance.DuelStance_Attack:
                    chance = 2 * t + (system.RandBool(5) ? 1 : 0);
                    break;
                case (int)DuelStance.DuelStance_Defense:
                    chance = 5 + 4 * t + (system.RandBool(5) ? 1 : 0);
                    break;
                case (int)DuelStance.DuelStance_Spirit:
                    chance = 5 + 3 * t + (system.RandBool(5) ? 1 : 0);
                    break;
                case (int)DuelStance.DuelStance_Fury:
                    chance = 100;
                    break;
                default:
                    return false;
            }
            Person person = GetPerson(team, chara);
            if (Utils.IsActive(person))
            {
                int age;
                switch (person.GetId())
                {
                    case PersonId.Chouhi:
                        chance += 12;
                        break;
                    case PersonId.Ryofu:
                    case PersonId.Kanu:
                        chance += 3;
                        break;
                    case PersonId.Kyocho:
                    case PersonId.Chouun:
                    case PersonId.Bachou:
                        chance += 2;
                        break;
                    case PersonId.Kouchuu_Kanshou:
                        if (system.GetLifeMode() == LifeMode.Virtual)
                        {
                            chance += 3;
                            break;
                        }
                        age = person.GetAge();
                        if (age < 60)
                            chance += 2;
                        else if (age < 65)
                            chance += 3;
                        else if (age < 70)
                            chance += 4;
                        else
                            chance += 5;
                        break;
                }
            }
            if (HasItem(team, chara, ItemFlags(DuelItemType.DuelItemType_SerpentBlade)))
                chance += 3;
            if (HasBuff(team, (int)DuelBuffType.DuelBuffType_CriticalChance))
                chance += 5;
            if (t <= 5 && stance != (int)DuelStance.DuelStance_Fury)
                chance = 0;
            return system.RandBool(chance);
        }

        #endregion

        #region 计时器 / 状态更新（507660 - 5083e0）

        /// <summary>507660</summary>
        public int CreateInvulnerableTimer(int team, int chara)
        {
            return 5 + system.RandInt(5);
        }

        /// <summary>5076a0</summary>
        public void ResetBuffTimer(int team, int chara, int buff)
        {
            if (view && engine != null)
                engine.DuelResetBuff(this, team, buff);
            TeamSetBuffTimer(this.team[team], buff, 0);
        }

        /// <summary>507700。对方是否为血亲或义兄弟</summary>
        public bool IsFamily(int aTeam, int aChara, int bTeam, int bChara)
        {
            if (!Utils.InRange(aTeam, 0, MaxTeamCount - 1))
                return false;
            if (!Utils.InRange(bTeam, 0, MaxTeamCount - 1))
                return false;
            if (!Utils.InRange(aChara, 0, MaxTeamCharaCount - 1))
                return false;
            if (!Utils.InRange(bChara, 0, MaxTeamCharaCount - 1))
                return false;
            Person src = GetPerson(aTeam, aChara);
            Person target = GetPerson(bTeam, bChara);
            if (!Utils.IsActive(src))
                return false;
            if (!Utils.IsActive(target))
                return false;
            return src.IsFamily(target.GetId()) || src.SpouseIs(target.GetId()) || src.IsGikyoudai(target.GetId());
        }

        /// <summary>5078e0。播放合数动画（无表现层时立即结算）</summary>
        public bool PlayBlowAnim(int count)
        {
            if (!Utils.InRange(count, 0, MaxAnimQueueSize - 1))
                return false;
            if (view && engine != null)
            {
                engine.DuelBlowAnim(this, blowAnimQueue, count);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    BlowAnim anim = blowAnimQueue[i];
                    if (anim.value < 0)
                        continue;
                    AddBlowCounter(anim.value);
                    // calc_result 函数中可能会用到，因此初始化
                    blowAnimQueue[i] = new BlowAnim();
                }
            }
            return true;
        }

        /// <summary>5079f0。播放体力动画（无表现层时立即结算）</summary>
        public bool PlayHpAnim(int count)
        {
            if (!Utils.InRange(count, 0, MaxAnimQueueSize - 1))
                return false;
            if (view && engine != null)
            {
                engine.DuelHpAnim(this, hpAnimQueue, count);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    HPAnim anim = hpAnimQueue[i];
                    if (!Utils.InRange(anim.defTeam, 0, MaxTeamCount - 1))
                        continue;
                    AddHp(anim.defTeam, anim.defChara, -anim.damage);
                    AddShoubyou(anim.defTeam, anim.defChara, anim.shoubyouDamage);
                    if (anim.shoubyouDamage > 0)
                        CalcActionRatio();
                    // calc_result 函数中可能会用到，因此初始化
                    hpAnimQueue[i] = new HPAnim();
                }
            }
            return true;
        }

        /// <summary>507b10。播放斗志动画（无表现层时立即结算）</summary>
        public bool PlaySpiritAnim(int count)
        {
            if (!Utils.InRange(count, 0, MaxAnimQueueSize - 1))
                return false;
            if (view && engine != null)
            {
                engine.DuelSpiritAnim(this, spiritAnimQueue, count);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    SpiritAnim anim = spiritAnimQueue[i];
                    if (!Utils.InRange(anim.atkTeam, 0, MaxTeamCount - 1))
                        continue;
                    AddSpirit(anim.atkTeam, anim.atkChara, anim.atkValue);
                    AddSpirit(anim.defTeam, anim.defChara, anim.defValue);
                    // calc_result 函数中可能会用到，因此初始化
                    spiritAnimQueue[i] = new SpiritAnim();
                }
            }
            return true;
        }

        /// <summary>507c30。把结果写回 Param</summary>
        public void UpdateParamResult()
        {
            if (Utils.InRange(winnerTeam, 0, MaxTeamCount - 1) && Utils.InRange(loserTeam, 0, MaxTeamCount - 1))
            {
                param.winnerTeam = reverse ? GetOpponentTeam(winnerTeam) : winnerTeam;
                param.loserTeam = reverse ? GetOpponentTeam(loserTeam) : loserTeam;
                param.winnerChara = GetCurrentChara(param.winnerTeam);
                param.loserChara = GetCurrentChara(param.loserTeam);
            }
            param.flags = flags;
            for (int i = 0; i < MaxTeamCount; i++)
            {
                int t = reverse ? GetOpponentTeam(i) : i;
                for (int j = 0; j < MaxTeamCharaCount; j++)
                {
                    param.hp[t][j] = TeamGetHp(this.team[t], j);
                    param.shoubyou[t][j] = TeamGetShoubyou(this.team[t], j);
                }
            }
            param.endBlowCounter = blowCounter;
            Logger logger = system != null ? system.GetLogger() : null;
            if (logger != null)
            {
                logger.Debug($"Duel::result {ftkType} {ftkTeam} {param.winnerTeam}-{param.winnerChara} {param.endBlowCounter} 0x{param.flags:x} 0x{system.GetSeed():x}");
                for (int i = 0; i < (int)DuelTeam.DuelTeam_Max; i++)
                {
                    for (int j = 0; j < team[i].charaCount; j++)
                    {
                        logger.Debug($"{i}-{j} {param.result[i][j]} {param.hp[i][j]} {param.spirit[i][j]} {param.shoubyou[i][j]}");
                    }
                }
            }
        }

        /// <summary>507e10。更新行动方针</summary>
        public void UpdateStance(bool player = false)
        {
            if (player)
            {
                if (!view)
                    return;
                for (int i = 0; i < MaxTeamCount; i++)
                {
                    if (team[i].control != (int)DuelControl.DuelControl_Manual)
                        continue;
                    if (engine == null)
                        return;
                    int stance = engine.DuelGetStance(this, i);
                    if (!Utils.InRange(stance, 0, (int)DuelStance.DuelStance_Max - 1))
                        continue;
                    SetStance(i, team[i].currentChara, stance);
                }
            }
            else
            {
                for (int i = 0; i < MaxTeamCount; i++)
                {
                    if (team[i].control == (int)DuelControl.DuelControl_Manual
                        && Utils.InRange(team[i].currentChara, 0, MaxTeamCharaCount - 1)
                        && Utils.InRange(TeamGetStance(team[i], team[i].currentChara), 0, (int)DuelStance.DuelStance_Max - 1))
                        continue;
                    int stance = AiCalcStance(ai[i]);
                    if (!Utils.InRange(stance, 0, (int)DuelStance.DuelStance_Max - 1))
                        continue;
                    SetStance(i, team[i].currentChara, stance);
                }
            }
        }

        /// <summary>507f10。确定要使用的必杀</summary>
        public bool UpdateSpecialAction()
        {
            if (!Utils.InRange(specialTryTeam, 0, MaxTeamCount - 1))
                return false;
            specialAction.team = specialTryTeam;
            specialAction.chara = GetCurrentChara(specialTryTeam);
            if (IsManual(specialTryTeam))
            {
                if (!view || engine == null)
                    return false;
                int sp = engine.DuelGetSpecial(this, specialTryTeam);
                if (!Utils.InRange(sp, 0, (int)DuelSpecial.DuelSpecial_Max - 1))
                    return false;
                specialAction.type = sp;
            }
            else
            {
                specialAction.type = AiCalcSpecial(ai[specialTryTeam]);
            }
            return true;
        }

        /// <summary>507fd0。更新交替武将</summary>
        public void UpdateSwitching()
        {
            for (int i = 0; i < MaxTeamCount; i++)
            {
                if (team[i].control == (int)DuelControl.DuelControl_Manual)
                {
                    if (view && engine != null)
                        switchingChara[i] = engine.DuelGetSwitchingChara(this, i);
                }
                else
                {
                    switchingChara[i] = AiCalcSwitch(ai[i]);
                }
            }
        }

        /// <summary>508070。退却胜负判定</summary>
        public bool UpdateRetreatResult()
        {
            int opponentTeam = GetOpponentTeam(specialAction.team);
            if (!IsValid(specialAction))
                return false;
            winnerTeam = opponentTeam;
            loserTeam = specialAction.team;
            return true;
        }

        /// <summary>5080f0。推进各类计时器</summary>
        public void UpdateTimer()
        {
            for (int i = 0; i < MaxTeamCount; i++)
            {
                team[i].stanceTimer++;
                if (team[i].appearingTimer > 0)
                    team[i].appearingTimer--;
                if (team[i].switchingTimer > 0)
                    team[i].switchingTimer--;
                if (team[i].invulnerableTimer > 0)
                {
                    team[i].invulnerableTimer--;
                    if (team[i].invulnerableTimer == 0 && view && engine != null)
                        engine.DuelResetInvulnerable(this, i);
                    team[i].stanceTimer = 0;
                }
                for (int j = 0; j < (int)DuelBuffType.DuelBuffType_Max; j++)
                {
                    int timer = TeamGetBuffTimer(team[i], j);
                    if (timer > 0)
                    {
                        timer--;
                        TeamSetBuffTimer(team[i], j, timer);
                    }
                    if (timer == 0 && view && engine != null)
                        engine.DuelResetBuff(this, i, j);
                }
            }
        }

        /// <summary>508200。清空行动队列</summary>
        public void ResetAction()
        {
            for (int i = 0; i < MaxAnimQueueSize; i++)
            {
                blowAnimQueue[i] = new BlowAnim();
                hpAnimQueue[i] = new HPAnim();
                spiritAnimQueue[i] = new SpiritAnim();
                actionQueue[i] = new Action();
            }
            specialAction = new SpecialAction();
            specialTryTeam = -1;
            if (view && engine != null)
                engine.DuelResetAnim(this);
        }

        /// <summary>5082e0。最低体力（低于此值不再参战）</summary>
        public static int GetDuelMinHp(Person self)
        {
            if (!Utils.IsActive(self))
                return 50;
            switch (self.GetSeikaku())
            {
                case Seikaku.Shoushin:
                    return 80;
                case Seikaku.Reisei:
                    return 70;
                case Seikaku.Goutan:
                    return 60;
            }
            return 50;
        }

        /// <summary>508330</summary>
        public int GetCharaCount(int team, bool joined)
        {
            int n = 0;
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                if (joined)
                {
                    if (IsJoined(team, i))
                        n++;
                }
                else
                {
                    if (TeamIsActive(this.team[team], i))
                        n++;
                }
            }
            return n;
        }

        /// <summary>5083a0。合数增减</summary>
        public int AddBlowCounter(int value)
        {
            int n = blowCounter;
            blowCounter += value;
            if (blowCounter < 0)
                blowCounter = 0;
            if (blowCounter != n)
            {
                if (view && engine != null)
                    engine.DuelUpdateBlowCounter(this);
            }
            return blowCounter - n;
        }

        /// <summary>5083e0</summary>
        public PersonId GetPersonId(int team, int chara)
        {
            Person person = TeamGetPerson(this.team[team], chara);
            if (Utils.IsActive(person))
                return person.GetId();
            return PersonId.Invalid;
        }

        #endregion

        #region 伤病 / 必杀判定（508440 - 5088b0）

        /// <summary>508440。按交替序号查找武将</summary>
        public int GetChara(int team, int number)
        {
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                if (TeamGetNumber(this.team[team], i) == number)
                    return i;
            }
            return -1;
        }

        /// <summary>5084b0。改变伤病</summary>
        public void ChangeShoubyou(int team, int chara, int shoubyou)
        {
            int oldShoubyou = TeamGetShoubyou(this.team[team], chara);
            if (!Utils.InRange(oldShoubyou, 0, (int)Shoubyou.Max - 2))
                return;
            shoubyou = Utils.Clamp(shoubyou, 0, (int)Shoubyou.Max - 2);
            TeamSetShoubyou(this.team[team], chara, shoubyou);
            if (oldShoubyou < shoubyou)
            {
                if (team == (reverse ? (int)DuelTeam.DuelTeam_Challenged : (int)DuelTeam.DuelTeam_Challenger))
                    Utils.SetBits(ref flags, (int)DuelStatus.DuelStatus_ChallengerShoubyouDamaged);
                else
                    Utils.SetBits(ref flags, (int)DuelStatus.DuelStatus_ChallengedShoubyouDamaged);
            }
        }

        /// <summary>508560。伤病增减</summary>
        public int AddShoubyou(int team, int chara, int value)
        {
            int shoubyou = TeamGetShoubyou(this.team[team], chara);
            if (!Utils.InRange(shoubyou, 0, (int)Shoubyou.Max - 2))
                return -1;
            shoubyou = Utils.Clamp(shoubyou + value, 0, (int)Shoubyou.Max - 2);
            ChangeShoubyou(team, chara, shoubyou);
            return shoubyou;
        }

        /// <summary>5085f0, v+c。必杀是否可用</summary>
        public virtual bool IsSpecialEnabled(int team, int chara, int special)
        {
            if (system.IsFeatDisabled(Feature.DuelAIRetreat) && special == (int)DuelSpecial.DuelSpecial_Taikyaku && !IsPlayer(team))
                return false;
            if (!IsSpecialAvailable(team, chara, special))
                return false;
            if (GetSpirit(team, chara) < GetSpecialSpiritCost(special))
                return false;
            if (special == (int)DuelSpecial.DuelSpecial_Nisetaikyaku)
                return blowCounter >= 15;
            return true;
        }

        /// <summary>5086c0。必杀结果</summary>
        public int CalcSpecialResult(SpecialAction special)
        {
            if (!Utils.InRange(special.type, 0, (int)DuelSpecial.DuelSpecial_Max - 1))
                return -1;
            switch (special.type)
            {
                case (int)DuelSpecial.DuelSpecial_Taikyaku:
                    if (CalcRetreatChance(special.team, special.chara))
                        return (int)DuelSpecialResult.DuelSpecialResult_Hit;
                    return (int)DuelSpecialResult.DuelSpecialResult_Miss;
            }
            return (int)DuelSpecialResult.DuelSpecialResult_Hit;
        }

        /// <summary>508720。一合取胜的队伍</summary>
        public static int CalcDuelFtkTeam(Person a, Person b, bool aBow, bool bBow, int aShoubyou, int bShoubyou, out int chance)
        {
            chance = 0;
            Scenario scenario = a.GetScenario();
            if (scenario.GetGame().IsFeatDisabled(Feature.DuelFirstTurnKill))
                return -1;
            if (!Utils.IsAlive(a))
                return -1;
            if (!Utils.IsAlive(b))
                return -1;
            int aStr = GetDuelStrength(a, aShoubyou, true);
            int bStr = GetDuelStrength(b, bShoubyou, true);
            Person atk;
            Person def;
            int atkStr;
            int defStr;
            int atkTeam;
            if (aStr > bStr || (aStr == bStr && scenario.RandBool(70)))
            {
                atk = a;
                def = b;
                atkStr = aStr;
                defStr = bStr;
                atkTeam = (int)DuelTeam.DuelTeam_Challenger;
            }
            else
            {
                atk = b;
                def = a;
                atkStr = bStr;
                defStr = aStr;
                atkTeam = (int)DuelTeam.DuelTeam_Challenged;
            }
            int defStrRev = Math.Max(defStr, atkStr / 2);
            // 防御：双方武力均为 0（伤病值非法）时原代码会除零，此处直接判定为不发生一击必杀
            if (defStrRev <= 0)
                return -1;
            int n = (atkStr * atkStr) / (defStrRev * defStrRev); // 1 .. 4
            n *= 37 - (74 * defStrRev * defStrRev) / (defStrRev * defStrRev + atkStr * atkStr); // 0 .. 23
            n = Utils.Clamp(n, 0, 100); // 0 .. 92
            if (aBow)
                n += 5;
            if (!atk.IsPlayer() && def.IsPlayer())
            {
                if (Utils.IsAlive(scenario))
                {
                    switch (scenario.GetDifficulty())
                    {
                        case Difficulty.Normal:
                            n = n * 4 / 3; // 1.333...
                            break;
                        case Difficulty.Hard:
                            n = n * 3 / 2; // 1.5
                            break;
                    }
                }
            }
            else if (atk.IsPlayer() && !def.IsPlayer())
            {
                if (Utils.IsAlive(scenario))
                {
                    switch (scenario.GetDifficulty())
                    {
                        case Difficulty.Normal:
                            n = n * 4 / 5; // 0.8
                            break;
                        case Difficulty.Hard:
                            n = n / 2; // 0.5
                            break;
                    }
                }
            }
            int age;
            switch (atk.GetId())
            {
                case PersonId.Ryofu:
                    n += 10;
                    break;
                case PersonId.Chouhi:
                case PersonId.Kanu:
                    n += 5;
                    break;
                case PersonId.Kyocho:
                case PersonId.Chouun:
                case PersonId.Bachou:
                    n += 3;
                    break;
                case PersonId.Kouchuu_Kanshou:
                    if (Utils.IsAlive(scenario) && scenario.GetLifeMode() == LifeMode.Virtual)
                    {
                        n += 5;
                        break;
                    }
                    age = atk.GetAge();
                    if (age < 60)
                        n += 1;
                    else if (age < 65)
                        n += 2;
                    else if (age < 70)
                        n += 3;
                    else if (age < 80)
                        n += 4;
                    else if (age < 85)
                        n += 5;
                    else if (age < 90)
                        n += 10;
                    else
                        n += 15;
                    break;
            }
            // 武力 70 以上才会发生
            if (atk.GetStat(PersonStatType.Strength) < 70)
                n = 0;
            // 武力差不足 5 时，必须有弓才会发生
            if (!aBow && atkStr - defStrRev < 5)
                n = 0;
            // 对手为吕布、关羽、张飞、许褚、赵云、马超时不会发生
            switch (def.GetId())
            {
                case PersonId.Ryofu:
                case PersonId.Kanu:
                case PersonId.Chouhi:
                case PersonId.Kyocho:
                case PersonId.Chouun:
                case PersonId.Bachou:
                    n = 0;
                    break;
            }
            chance = n;
            if (scenario.RandBool(n))
                return atkTeam;
            return -1;
        }

        /// <summary>508b90。计算胜负</summary>
        /// <param name="predict">是否参照 hpAnimArray 预先计算（异步表现层时使用）</param>
        public int CalcResult(bool predict, HPAnim[] hpAnimArray)
        {
            int[] hp = new int[MaxTeamCount];
            // 异步进行时体力由 scene 更新
            if (predict && hpAnimArray != null)
            {
                for (int i = 0; i < actionCount; i++)
                {
                    if (!Utils.InRange(hpAnimArray[i].defTeam, 0, MaxTeamCount - 1))
                        continue;
                    hp[hpAnimArray[i].defTeam] += hpAnimArray[i].damage;
                }
            }
            for (int i = 0; i < MaxTeamCount; i++)
                hp[i] = GetCurrentHp(i) - hp[i];
            if (hp[(int)DuelTeam.DuelTeam_Challenger] > 0)
            {
                if (hp[(int)DuelTeam.DuelTeam_Challenged] > 0)
                {
                    return -1;
                }
                else
                {
                    winnerTeam = (int)DuelTeam.DuelTeam_Challenger;
                    loserTeam = (int)DuelTeam.DuelTeam_Challenged;
                    return reverse ? (int)DuelResult.DuelResult_ChallengedWin : (int)DuelResult.DuelResult_ChallengerWin;
                }
            }
            else
            {
                if (hp[(int)DuelTeam.DuelTeam_Challenged] > 0)
                {
                    winnerTeam = (int)DuelTeam.DuelTeam_Challenged;
                    loserTeam = (int)DuelTeam.DuelTeam_Challenger;
                    return reverse ? (int)DuelResult.DuelResult_ChallengerWin : (int)DuelResult.DuelResult_ChallengedWin;
                }
                else
                {
                    return (int)DuelResult.DuelResult_Draw;
                }
            }
        }

        #endregion

        #region 伤害 / 登场 / 行动（508cc0 - 50c490）

        /// <summary>508cc0。普通攻击伤害</summary>
        public int CalcAttackDamage(int team, int chara)
        {
            int opponentTeam = GetOpponentTeam(team);
            int opponentChara = GetCurrentChara(opponentTeam);
            int n = TeamGetStanceAttack(this.team[team], chara);     // 14 .. 16
            n = n * GetActionRatio(team, chara, opponentTeam, opponentChara) / 50; // 0 .. 2
            n = n * TeamGetStanceAttackSub(this.team[team], chara) * 7 / 27;       // 0.26 * attack_sub
            // = 0 .. 24
            if (n < 3)
                n = 3;
            return n;
        }

        /// <summary>508da0。必杀伤害</summary>
        public int CalcSpecialDamage(SpecialAction special)
        {
            int team = special.team;
            int chara = special.chara;
            int opponentTeam = GetOpponentTeam(special.team);
            int opponentChara = GetCurrentChara(opponentTeam);
            if (!IsValid(special))
                return 0;
            if (!Utils.InRange(opponentTeam, 0, MaxTeamCount - 1))
                return 0;
            if (!Utils.InRange(opponentChara, 0, MaxTeamCharaCount - 1))
                return 0;

            int n = GetActionRatio(team, chara, opponentTeam, opponentChara); // 1 .. 99
            n = n * 20 / 55; // 0.36

            if (HasBuff(team, (int)DuelBuffType.DuelBuffType_Attack))
                n = n * 5 / 4; // 1.25
            if (HasBuff(opponentTeam, (int)DuelBuffType.DuelBuffType_Defense))
                n = n * 3 / 4; // 0.75

            if (HasItem(team, chara, ItemFlags(DuelItemType.DuelItemType_CrescentHalberd)))
                n = n * 9 / 8; // 1.125
            else if (HasItem(team, chara, ItemFlags(DuelItemType.DuelItemType_LongSpear)))
                n = n * 10 / 9; // 1.1...

            switch (GetPersonId(team, chara))
            {
                case PersonId.Kouchuu_Kanshou:
                case PersonId.Kakouen:
                    if (special.type == (int)DuelSpecial.DuelSpecial_Nisetaikyaku)
                        n = n * 11 / 10; // 1.1
                    break;
                case PersonId.Ousou:
                case PersonId.Shukuyuu:
                    if (special.type == (int)DuelSpecial.DuelSpecial_Anki)
                        n = n * 11 / 10; // 1.1
                    break;
                case PersonId.Ryofu:
                    n = n * 13 / 11; // 1.18...
                    break;
            }

            switch (special.type)
            {
                case (int)DuelSpecial.DuelSpecial_Hissatsuwaza:
                    n = n * 6 / 5; // 1.2
                    break;
                case (int)DuelSpecial.DuelSpecial_Musou:
                    n = n * 3;
                    break;
                case (int)DuelSpecial.DuelSpecial_Anki:
                    n = n * 6 / 5; // 1.2
                    break;
                case (int)DuelSpecial.DuelSpecial_Nisetaikyaku:
                    n = n * 3 / 2; // 1.5
                    break;
                case (int)DuelSpecial.DuelSpecial_Kiai:
                case (int)DuelSpecial.DuelSpecial_Kenshu:
                    n = 0;
                    break;
            }

            switch (system.GetDifficulty())
            {
                case Difficulty.Easy:
                    if (IsPlayer(team))
                    {
                        if (!IsPlayer(opponentTeam))
                            n = n * 11 / 10; // 1.1
                    }
                    else
                    {
                        if (IsPlayer(opponentTeam))
                            n = n * 4 / 5; // 0.8
                    }
                    break;
            }

            if (GetStance(opponentTeam, opponentChara) == (int)DuelStance.DuelStance_Defense)
                n = n * 3 / 4; // 0.75

            n = Math.Min(n, 80);
            return n;
        }

        /// <summary>509120。是否可以参战</summary>
        public bool CanJoin(int team, int chara)
        {
            int hp = GetHp(team, chara);
            Person person = GetPerson(team, chara);
            if (!Utils.IsActive(person))
                return false;
            if (hp < GetDuelMinHp(person))
                return false;
            return true;
        }

        /// <summary>509190。是否加入战斗</summary>
        public bool CalcJoin(int team, int chara)
        {
            Person person = GetPerson(team, chara);
            Person curPerson = GetCurrentPerson(team);
            Person opponentCurPerson = GetCurrentPerson(GetOpponentTeam(team));
            if (!Utils.IsActive(person))
                return false;
            if (!Utils.IsActive(curPerson))
                return false;
            if (!Utils.IsActive(opponentCurPerson))
                return false;
            if ((int)person.GetId() < 0)
                return false;
            if ((int)curPerson.GetId() < 0)
                return false;
            if ((int)opponentCurPerson.GetId() < 0)
                return false;

            if (person.IsGikyoudai(curPerson.GetId()) || person.IsFuufu(curPerson.GetId()))
            {
                if (blowCounter < 3)
                    return false;
                return system.RandBool(80);
            }

            if (person.IsHate(opponentCurPerson.GetId()))
            {
                if (blowCounter < 4)
                    return false;
                return system.RandBool(60);
            }

            if (person.IsHate(curPerson.GetId()))
            {
                if (!curPerson.IsKunshu())
                    return false;
                int curForceId = curPerson.GetForceId();
                if (curForceId >= 0 && person.GetForceId() != curForceId)
                    return false;
                if (blowCounter < 4)
                    return false;
                return system.RandBool(person.GetLoyalty() / 4);
            }

            if (person.IsLike(curPerson.GetId()) || person.IsKetsuen(curPerson.GetId()))
            {
                if (blowCounter < 6)
                    return false;
                return system.RandBool(50);
            }

            if (person.GetBirthplaceId() == curPerson.GetBirthplaceId())
            {
                if (blowCounter < 6)
                    return false;
                int n = 10 + (75 - person.GetAishouDistance(curPerson.GetId())) / 2; // 10 .. 47
                return system.RandBool(n);
            }

            if (blowCounter < 6)
                return false;
            int n2 = (75 - person.GetAishouDistance(curPerson.GetId())) / 2; // 0 .. 37
            if (n2 < 1)
                n2 = 1;
            return system.RandBool(n2);
        }

        /// <summary>509450。本回合先攻的队伍</summary>
        public int CalcActorTeam()
        {
            int aTeam = reverse ? (int)DuelTeam.DuelTeam_Challenged : (int)DuelTeam.DuelTeam_Challenger;
            int bTeam = reverse ? (int)DuelTeam.DuelTeam_Challenger : (int)DuelTeam.DuelTeam_Challenged;
            int aSpeed = TeamGetStanceSpeed(team[aTeam], team[aTeam].currentChara);
            int bSpeed = TeamGetStanceSpeed(team[bTeam], team[bTeam].currentChara);
            int sum = Math.Max(bSpeed + aSpeed, 1);
            if (aSpeed < bSpeed)
            {
                int tmpTeam = aTeam; aTeam = bTeam; bTeam = tmpTeam;
                int tmpSpeed = aSpeed; aSpeed = bSpeed; bSpeed = tmpSpeed;
            }
            int n = aSpeed * 100 / sum;
            n += (GetActionRatio(aTeam, team[aTeam].currentChara, bTeam, team[bTeam].currentChara) - 50) / 2;
            if (HasItem(aTeam, team[aTeam].currentChara, ItemFlags(DuelItemType.DuelItemType_BlueDragon)))
                n += 2;
            if (HasItem(bTeam, team[bTeam].currentChara, ItemFlags(DuelItemType.DuelItemType_BlueDragon)))
                n -= 2;
            if (GetPersonId(aTeam, team[aTeam].currentChara) == PersonId.Kanu)
                n += 2;
            if (GetPersonId(bTeam, team[bTeam].currentChara) == PersonId.Kanu)
                n -= 2;
            n = Utils.Clamp(n, 1, 99);
            if (system.RandBool(n))
                return aTeam;
            return bTeam;
        }

        /// <summary>509690。决定行动</summary>
        public int CalcAction(int team, int chara)
        {
            int action = -1;
            if (CalcCriticalChance(team, chara))
            {
                switch (GetStance(team, chara))
                {
                    case (int)DuelStance.DuelStance_Attack:
                        action = (int)DuelAction.DuelAction_AttackCritical;
                        break;
                    case (int)DuelStance.DuelStance_Defense:
                        if (!IsInvulnerable(team, chara))
                            action = (int)DuelAction.DuelAction_DefenseCritical;
                        break;
                    case (int)DuelStance.DuelStance_Spirit:
                        action = (int)DuelAction.DuelAction_SpiritCritical;
                        break;
                    case (int)DuelStance.DuelStance_Fury:
                        if (!IsInvulnerable(team, chara) && system.RandBool(10))
                        {
                            action = (int)DuelAction.DuelAction_DefenseCritical;
                            break;
                        }
                        action = system.RandBool(20) ? (int)DuelAction.DuelAction_SpiritCritical : (int)DuelAction.DuelAction_AttackCritical;
                        break;
                }
                // 这里需要初始化吗？
                this.team[team].stanceTimer = 0;
                if (action == (int)DuelAction.DuelAction_DefenseCritical)
                    this.team[team].invulnerableTimer = CreateInvulnerableTimer(team, chara);
            }
            if (action < 0)
                return system.RandInt((int)DuelAction.DuelAction_AttackMax);
            return action;
        }

        /// <summary>509800</summary>
        public int CalcFtkTeam()
        {
            Person a = GetCurrentPerson((int)DuelTeam.DuelTeam_Challenger);
            Person b = GetCurrentPerson((int)DuelTeam.DuelTeam_Challenged);
            if (!Utils.IsActive(a))
                return -1;
            if (!Utils.IsActive(b))
                return -1;
            bool aBow = HasItem((int)DuelTeam.DuelTeam_Challenger, team[(int)DuelTeam.DuelTeam_Challenger].currentChara, ItemFlags(DuelItemType.DuelItemType_Bow));
            bool bBow = HasItem((int)DuelTeam.DuelTeam_Challenged, team[(int)DuelTeam.DuelTeam_Challenged].currentChara, ItemFlags(DuelItemType.DuelItemType_Bow));
            int aShoubyou = TeamGetShoubyou(team[(int)DuelTeam.DuelTeam_Challenger], team[(int)DuelTeam.DuelTeam_Challenger].currentChara);
            int bShoubyou = TeamGetShoubyou(team[(int)DuelTeam.DuelTeam_Challenged], team[(int)DuelTeam.DuelTeam_Challenged].currentChara);
            int chance;
            return CalcDuelFtkTeam(a, b, aBow, bBow, aShoubyou, bShoubyou, out chance);
        }

        /// <summary>509930</summary>
        public int CalcFtkType()
        {
            int aTeam = ftkTeam;
            if (!Utils.InRange(ftkTeam, 0, MaxTeamCount - 1))
                return -1;
            int bTeam = GetOpponentTeam(aTeam);
            int aChara = team[aTeam].currentChara;
            int bChara = team[bTeam].currentChara;
            if (IsFamily(aTeam, aChara, bTeam, bChara))
                return (int)DuelFtkType.DuelFtkType_Normal;
            if (type == (int)DuelType.DuelType_Event)
                return (int)DuelFtkType.DuelFtkType_Normal;
            if (!HasItem(aTeam, aChara, ItemFlags(DuelItemType.DuelItemType_Bow)))
                return (int)DuelFtkType.DuelFtkType_Normal;
            if (system.RandBool(80))
            {
                if (system.RandBool(50))
                    return (int)DuelFtkType.DuelFtkType_BowA;
                return (int)DuelFtkType.DuelFtkType_BowB;
            }
            return (int)DuelFtkType.DuelFtkType_Normal;
        }

        /// <summary>509a30。死亡概率</summary>
        /// <param name="team">失败方队伍</param>
        public bool CalcKillChance(int team)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return false;
            int chara = GetCurrentChara(team);
            Debug.Assert(Utils.InRange(chara, 0, MaxTeamCharaCount - 1));
            Person person = GetPerson(team, chara);
            if (Utils.IsActive(person) && person.HasSkill(SkillId.Kyouun))
                return false;
            if (specialAction.type == (int)DuelSpecial.DuelSpecial_Taikyaku)
                return false;
            int opponentTeam = GetOpponentTeam(team);
            int opponentChara = GetCurrentChara(opponentTeam);
            if (IsFamily(opponentTeam, opponentChara, team, chara))
                return false;
            int chance = 0;
            switch (system.GetBattleDeathMode())
            {
                case BattleDeathMode.Normal:
                    chance = 2;
                    break;
                case BattleDeathMode.High:
                    chance = 5;
                    break;
            }
            return system.RandBool(chance);
        }

        /// <summary>509b40。俘虏概率</summary>
        /// <param name="team">失败方队伍</param>
        public bool CalcCaptureChance(int team)
        {
            if (system.IsFeatDisabled(Feature.Hobaku))
                return false;
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return false;
            int chara = GetCurrentChara(team);
            Debug.Assert(Utils.InRange(chara, 0, MaxTeamCharaCount - 1));
            Person person = GetPerson(team, chara);
            if (Utils.IsActive(person) && person.HasSkill(SkillId.Kyouun))
                return false;
            if (specialAction.type == (int)DuelSpecial.DuelSpecial_Taikyaku)
                return false;
            int opponentTeam = GetOpponentTeam(team);
            int opponentChara = GetCurrentChara(opponentTeam);
            if (IsFamily(opponentTeam, opponentChara, team, chara))
                return false;
            int chance = 80;
            return system.RandBool(chance);
        }

        /// <summary>509c10</summary>
        public bool FtkAnim()
        {
            int opponentTeam = GetOpponentTeam(ftkTeam);
            if (opponentTeam >= 0)
            {
                blowAnimQueue[0].value = Utils.InRange(ftkType, 0, (int)DuelFtkType.DuelFtkType_Max - 1) ? FtkBlowCount[ftkType] : 0;
                if (PlayBlowAnim(1))
                {
                    HPAnim anim = new HPAnim();
                    anim.damage = MaxHP;
                    anim.atkTeam = ftkTeam;
                    anim.atkChara = team[ftkTeam].currentChara;
                    anim.defTeam = opponentTeam;
                    anim.defChara = team[opponentTeam].currentChara;
                    hpAnimQueue[0] = anim;
                    if (PlayHpAnim(1))
                    {
                        Utils.SetBits(ref flags, (int)DuelStatus.DuelStatus_Ftk);
                        return true;
                    }
                }
            }
            winnerTeam = -1;
            loserTeam = -1;
            ftkTeam = -1;
            ftkType = -1;
            return false;
        }

        /// <summary>509d20</summary>
        public bool ChangeCurrentChara(int team, int chara)
        {
            if (!TeamChangeCurrentChara(this.team[team], chara))
                return false;
            this.team[team].stanceTimer = 0;
            this.team[team].appearingTimer = CreateCooldownTimer(team, chara);
            this.team[team].switchingTimer = CreateCooldownTimer(team, chara);
            this.team[team].invulnerableTimer = 0;
            if (view && engine != null)
                engine.DuelChangeCurrentChara(this, team);
            for (int i = 0; i < (int)DuelBuffType.DuelBuffType_Max; i++)
            {
                TeamSetBuffTimer(this.team[team], i, 0);
                ResetBuffTimer(team, chara, i);
            }
            if (ai[team].initialized)
                AiUpdateChara(ai[team]);
            return true;
        }

        /// <summary>509e30。计算攻击比例</summary>
        public int CalcActionRatio(int aTeam, int aChara, int bTeam, int bChara)
        {
            if (!CheckState(aTeam, aChara, -1) || !CheckState(bTeam, bChara, -1))
                return 0;

            int aStr = GetStrength(aTeam, aChara, true); // 1 .. 110
            int bStr = GetStrength(bTeam, bChara, true);

            int maxStr = Math.Max(aStr, bStr);
            int minStr = Math.Min(aStr, bStr);
            int a, b, c, d;
            int x, y, z;

            a = Math.Max(maxStr - 5, 0); // 0 .. 105
            a = a * a / 1500;            // 0 .. 7

            x = maxStr / 10;             // 0 .. 11
            y = minStr / 10;
            b = Math.Max(x - y, 1);      // 1 .. 11

            x = aStr - minStr;           // 0 .. 109
            y = x + b - a - 1;           // 0 .. 112
            c = Math.Max(y, 0) * b;      // 0 .. 1232

            y = Math.Min(x, a);          // 0 .. 7
            z = y + b - a;               // 1 .. 11
            d = 0;
            d += y * (a - y);            // 0
            d += y * (y + 1) / 2;        // 0 .. 28
            d += Math.Max(z, 0) * Math.Max(z - 1, 0) / 2; // 0 .. 55
            int aScore = 180 + c + d;    // 180 .. 1495

            x = bStr - minStr;
            y = x + b - a - 1;
            c = Math.Max(y, 0) * b;

            y = Math.Min(x, a);
            z = y + b - a;
            d = 0;
            d += y * (a - y);
            d += y * (y + 1) / 2;
            d += Math.Max(z, 0) * Math.Max(z - 1, 0) / 2;
            int bScore = 180 + c + d;

            aScore *= aScore; // 32400 .. 2235025
            bScore *= bScore;

            int sum = aScore + bScore;

            if (aScore >= bScore)
                return Math.Min(aScore * 100 / sum, 99); // 1 .. 99
            else
                return 100 - Math.Min(bScore * 100 / sum, 99);
        }

        /// <summary>50a120。斗志获取量</summary>
        /// <param name="attack">true 为攻击时，false 为被击时</param>
        public int CalcSpiritGain(int team, int chara, bool attack, int value)
        {
            if (value <= 0)
                return 0;

            int n = value;
            int min = 3;
            // 被击时若处于防御重视/斗志重视则最少 5
            if (!attack)
            {
                switch (GetStance(team, chara))
                {
                    case (int)DuelStance.DuelStance_Defense:
                    case (int)DuelStance.DuelStance_Spirit:
                        min = 5;
                        break;
                }
            }
            if (n < min)
                n = min;

            n = n * TeamGetStanceSpiritGain(this.team[team], chara) / 7;

            if (!attack)
                n = n * 5 / 4;

            int hp = GetHp(team, chara);
            if (hp < 30)
                n = n * 2;             // 2
            else if (hp < 50)
                n = n * 3 / 2;         // 1.5

            switch (system.GetDifficulty())
            {
                case Difficulty.Easy:
                    if (IsPlayer(team))
                        n = n * 6 / 5; // 1.2
                    break;
                case Difficulty.Hard:
                    if (IsPlayer(team))
                        n = n * 4 / 5; // 0.8
                    else
                        n = n * 6 / 5; // 1.2
                    break;
            }

            if (HasItem(team, chara, ItemFlags(DuelItemType.DuelItemType_Sword)))
                n = n * 3 / 2;         // 1.5

            if (n < 1)
                n = 1;
            return n;
        }

        /// <summary>50a2a0</summary>
        public int CalcSpecialTry()
        {
            for (int i = 0; i < MaxTeamCount; i++)
            {
                if (team[i].control == (int)DuelControl.DuelControl_Manual)
                {
                    if (view && engine != null && engine.DuelIsSpecialButtonPushed(this, i))
                        return i;
                }
            }
            bool[] special = new bool[MaxTeamCount];
            for (int i = 0; i < MaxTeamCount; i++)
            {
                if (team[i].control != (int)DuelControl.DuelControl_Manual)
                    special[i] = AiCalcSpecialTry(ai[i]);
            }
            if (special[0] && special[1])
                special[system.RandBool(50) ? 0 : 1] = false;
            for (int i = 0; i < MaxTeamCount; i++)
            {
                if (special[i])
                    return i;
            }
            return -1;
        }

        /// <summary>50a390</summary>
        public void UpdateSpecialActionResult()
        {
            specialAction.result = CalcSpecialResult(specialAction);
        }

        /// <summary>50a3b0。闪避概率</summary>
        public bool CalcDodgeChance(int team, int chara)
        {
            int opponentTeam = GetOpponentTeam(team);
            int opponentChara = GetCurrentChara(opponentTeam);
            int strength = GetStrength(team, chara, true);                 // 1 .. 110
            int opponentStrength = GetStrength(opponentTeam, opponentChara, true);
            int n = 10 + (strength - opponentStrength) / 3;
            n = Utils.Clamp(n, 5, 30);
            if (GetStance(team, chara) == (int)DuelStance.DuelStance_Defense)
                n += 25;
            return system.RandBool(n);
        }

        /// <summary>50a4b0。必杀致伤概率</summary>
        public bool CalcWoundChance(SpecialAction special)
        {
            int team = special.team;
            int chara = special.chara;
            int opponentTeam = GetOpponentTeam(team);
            int opponentChara = GetCurrentChara(opponentTeam);

            int n = GetActionRatio(team, chara, opponentTeam, opponentChara); // 1 .. 99
            n = n * n / 50; // 0 .. 196
            n = Utils.Clamp(n, 1, 90);

            if (IsManual(opponentTeam))
            {
                switch (system.GetDifficulty())
                {
                    case Difficulty.Normal:
                        n = n * 6 / 5; // 1.2
                        break;
                    case Difficulty.Hard:
                        n = n * 3 / 2; // 1.5
                        break;
                }
            }

            Person person = GetPerson(team, chara);
            Person opponentPerson = GetPerson(opponentTeam, opponentChara);

            switch (person.GetId())
            {
                case PersonId.Kakouen:
                case PersonId.Kouchuu_Kanshou:
                    if (specialAction.type == (int)DuelSpecial.DuelSpecial_Nisetaikyaku)
                    {
                        switch (opponentPerson.GetId())
                        {
                            case PersonId.Kanu:
                            case PersonId.Kyocho:
                            case PersonId.Chouhi:
                            case PersonId.Chouun:
                            case PersonId.Bachou:
                            case PersonId.Ryofu:
                                n = n * 11 / 10;
                                break;
                            default:
                                n = 100;
                                break;
                        }
                    }
                    break;
            }

            if (Utils.IsActive(opponentPerson) && opponentPerson.HasSkill(SkillId.Kyouun))
                n = 0;

            return system.RandBool(n);
        }

        /// <summary>50afd0。登场</summary>
        public bool JoinAnim()
        {
            for (int i = 0; i < MaxTeamCount; i++)
            {
                if (!Utils.InRange(appearingChara[i], 0, MaxTeamCharaCount - 1))
                    continue;
                int oldChara = GetCurrentChara(i);
                if (appearingChara[i] == oldChara)
                    continue;
                if (!ChangeCurrentChara(i, appearingChara[i]))
                    continue;
                int number = -1;
                for (int j = 0; j < MaxTeamCharaCount; j++)
                    number = Math.Max(number, TeamGetNumber(team[i], j));
                TeamSetNumber(team[i], appearingChara[i], number + 1);
                if (view && engine != null)
                    engine.DuelJoin(this, i, oldChara);
                return false;
            }
            return true;
        }

        /// <summary>50b0f0。交替</summary>
        public bool SwitchAnim()
        {
            for (int i = 0; i < MaxTeamCount; i++)
            {
                if (!Utils.InRange(switchingChara[i], 0, MaxTeamCharaCount - 1))
                    continue;
                int oldChara = GetCurrentChara(i);
                if (switchingChara[i] == oldChara)
                    continue;
                if (!ChangeCurrentChara(i, switchingChara[i]))
                    continue;
                if (view && engine != null)
                    engine.DuelSwitch(this, i, oldChara);
                return false;
            }
            return true;
        }

        /// <summary>50b1a0。必杀执行</summary>
        public bool SpecialActionAnim()
        {
            int team = specialAction.team;
            int chara = specialAction.chara;
            int opponentTeam = GetOpponentTeam(team);
            int opponentChara = GetCurrentChara(opponentTeam);

            actionCount = GetSpecialHitCount(specialAction);

            for (int i = 0; i < actionCount; i++)
            {
                int hpDamage = CalcSpecialDamage(specialAction);
                blowAnimQueue[i].value = actionQueue[i].result != (int)DuelActionResult.DuelActionResult_Dodged ? 1 : 0;

                hpAnimQueue[i].damage = hpDamage;
                hpAnimQueue[i].atkTeam = team;
                hpAnimQueue[i].atkChara = chara;
                hpAnimQueue[i].defTeam = opponentTeam;
                hpAnimQueue[i].defChara = opponentChara;

                spiritAnimQueue[i].atkValue = 0;
                spiritAnimQueue[i].atkTeam = team;
                spiritAnimQueue[i].atkChara = chara;
                spiritAnimQueue[i].defValue = CalcSpiritGain(opponentTeam, opponentChara, false, hpDamage);
                spiritAnimQueue[i].defTeam = opponentTeam;
                spiritAnimQueue[i].defChara = opponentChara;

                LogDebug($"Duel::special_action_anim {specialAction.type} {team}-{chara} {hpDamage} {spiritAnimQueue[i].atkValue} {spiritAnimQueue[i].defValue}");
            }

            AddSpirit(team, chara, -GetSpecialSpiritCost(specialAction.type));

            int remainingCount = GetSpecialRemainingCount(team, chara, specialAction.type);
            if (remainingCount > 0)
                SetSpecialRemainingCount(team, chara, specialAction.type, remainingCount - 1);

            bool wound = false;
            switch (specialAction.type)
            {
                case (int)DuelSpecial.DuelSpecial_Kiai:
                    TeamSetBuffTimer(this.team[team], (int)DuelBuffType.DuelBuffType_Attack, -1);
                    break;
                case (int)DuelSpecial.DuelSpecial_Kenshu:
                    TeamSetBuffTimer(this.team[team], (int)DuelBuffType.DuelBuffType_Defense, -1);
                    break;
                case (int)DuelSpecial.DuelSpecial_Kyuusho:
                case (int)DuelSpecial.DuelSpecial_Nisetaikyaku:
                    wound = CalcWoundChance(specialAction);
                    break;
                case (int)DuelSpecial.DuelSpecial_Musou:
                    wound = CalcWoundChance(specialAction);
                    goto case (int)DuelSpecial.DuelSpecial_Anki;
                case (int)DuelSpecial.DuelSpecial_Anki:
                    for (int i = 0; i < (int)DuelBuffType.DuelBuffType_Max; i++)
                        TeamSetBuffTimer(this.team[opponentTeam], i, 0);
                    break;
            }
            if (wound)
                hpAnimQueue[actionCount - 1].shoubyouDamage = 1;

            PlayBlowAnim(actionCount);
            PlayHpAnim(actionCount);
            PlaySpiritAnim(actionCount);
            return true;
        }

        /// <summary>50b5c0。重算攻击比例表</summary>
        public void CalcActionRatio()
        {
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                for (int j = 0; j < MaxTeamCharaCount; j++)
                    actionRatio[i][j] = CalcActionRatio((int)DuelTeam.DuelTeam_Challenger, i, (int)DuelTeam.DuelTeam_Challenged, j);
            }
            Logger logger = system != null ? system.GetLogger() : null;
            if (logger != null)
            {
                for (int i = 0; i < MaxTeamCharaCount; i++)
                {
                    for (int j = 0; j < MaxTeamCharaCount; j++)
                        logger.Debug($"calc_action_ratio {i}-{j} {actionRatio[i][j]}");
                }
            }
        }

        /// <summary>50b600</summary>
        public bool InitTeam(int team)
        {
            if (reverse)
                team = GetOpponentTeam(team);
            int count = 0;
            Person[] personArray = new Person[MaxTeamCharaCount];
            int[] hpArray = new int[MaxTeamCharaCount];
            int[] spiritArray = new int[MaxTeamCharaCount];
            int[] shoubyouArray = new int[MaxTeamCharaCount];
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                Person person = param.person[team][i];
                if (!Utils.IsActive(person))
                    continue;
                personArray[i] = person;
                hpArray[i] = param.hp[team][i];
                spiritArray[i] = param.spirit[team][i];
                shoubyouArray[i] = param.shoubyou[team][i];
                count++;
            }
            if (count == 0)
                return false;
            int startChara = param.startChara[team];
            if (!Utils.InRange(startChara, 0, MaxTeamCharaCount - 1))
                return false;
            TeamInit(this.team[team], personArray, hpArray, spiritArray, shoubyouArray, count, startChara, param.control[team], param.playerId[team]);
            ChangeCurrentChara(team, startChara);
            TeamSetNumber(this.team[team], startChara, 0);
            for (int i = 0; i < MaxTeamCharaCount; i++)
                SetStance(team, i, (int)DuelStance.DuelStance_Spirit);

            Logger logger = system != null ? system.GetLogger() : null;
            if (logger != null)
            {
                for (int i = 0; i < this.team[team].charaCount; i++)
                {
                    logger.Debug($"{team}-{i} {this.team[team].chara[i].person.GetId()}{this.team[team].chara[i].person.GetName()} {this.team[team].chara[i].hp} {this.team[team].chara[i].spirit} {this.team[team].chara[i].state} 0x{this.team[team].chara[i].item.Value:x}");
                }
            }
            return true;
        }

        /// <summary>50b7e0。计算伤害</summary>
        public int CalcDamage(out int atkSpirit, out int defSpirit, int team, int chara, int action, int result)
        {
            atkSpirit = 0;
            defSpirit = 0;

            Debug.Assert(Utils.InRange(team, 0, MaxTeamCount - 1));
            Debug.Assert(Utils.InRange(chara, 0, MaxTeamCharaCount - 1));
            Debug.Assert(Utils.InRange(action, 0, (int)DuelAction.DuelAction_Max - 1));
            Debug.Assert(Utils.InRange(result, 0, (int)DuelActionResult.DuelActionResult_Max - 1));

            int opponentTeam = GetOpponentTeam(team);
            int opponentChara = GetCurrentChara(opponentTeam);

            int n = CalcAttackDamage(team, chara);

            switch (system.GetDifficulty())
            {
                case Difficulty.Easy:
                    if (IsPlayer(team))
                    {
                        if (!IsPlayer(opponentTeam))
                            n = n * 11 / 10; // 1.1
                    }
                    else
                    {
                        if (IsPlayer(opponentTeam))
                            n = n * 4 / 5;  // 0.8
                    }
                    break;
            }

            if (HasItem(team, chara, ItemFlags(DuelItemType.DuelItemType_CrescentHalberd)))
                n = n * 9 / 8; // 1.125
            else if (HasItem(team, chara, ItemFlags(DuelItemType.DuelItemType_LongSpear)))
                n = n * 10 / 9; // 1.1...

            if (GetPersonId(team, chara) == PersonId.Ryofu)
                n = n * 13 / 11; // 1.18...

            if (HasBuff(team, (int)DuelBuffType.DuelBuffType_Attack))
                n = n * 5 / 4; // 1.25
            if (HasBuff(opponentTeam, (int)DuelBuffType.DuelBuffType_Defense))
                n = n * 3 / 4; // 0.75

            switch (action)
            {
                case (int)DuelAction.DuelAction_AttackCritical:
                    n = n * 3 / 4; // 0.75
                    break;
                case (int)DuelAction.DuelAction_DefenseCritical:
                case (int)DuelAction.DuelAction_SpiritCritical:
                    atkSpirit = 0;
                    defSpirit = 0;
                    return 0;
            }

            atkSpirit = n;
            defSpirit = n;

            bool incDefSpirit = false;
            int opponentStance = GetStance(opponentTeam, opponentChara);

            // 被击时若处于防御重视/斗志重视则斗志上升增加
            switch (opponentStance)
            {
                case (int)DuelStance.DuelStance_Defense:
                case (int)DuelStance.DuelStance_Spirit:
                    incDefSpirit = true;
                    break;
            }

            if (IsInvulnerable(opponentTeam, opponentChara))
            {
                atkSpirit = 0;
                if (incDefSpirit)
                    defSpirit = Math.Max(n / 2, 1);
                else
                    defSpirit = 0;
                return 0;
            }

            switch (result)
            {
                case (int)DuelActionResult.DuelActionResult_Blocked:
                    if (opponentStance == (int)DuelStance.DuelStance_Defense)
                        n = Math.Max(n * 3 / 10, 1); // 0.3
                    else
                        n = n / 2;                   // 0.5
                    atkSpirit = atkSpirit * 3 / 4;   // 0.75
                    defSpirit = Math.Max(defSpirit / 2, 1); // 0.5
                    return n;
                case (int)DuelActionResult.DuelActionResult_Dodged:
                    atkSpirit = 0;
                    if (opponentStance == (int)DuelStance.DuelStance_Defense)
                        return 0;
                    if (incDefSpirit)
                        defSpirit = Math.Max(defSpirit / 2, 1); // 0.5
                    else
                        defSpirit = 0;
                    return 0;
            }

            return n;
        }

        /// <summary>50bb70</summary>
        public int CalcAppearingChara(int team)
        {
            if (!TeamHasSubChara(this.team[team], (int)DuelCharaState.DuelCharaState_NotJoined))
                return -1;
            if (this.team[team].appearingTimer > 0)
                return -1;
            if (TeamGetHp(this.team[team], this.team[team].currentChara) >= 33)
            {
                for (int i = 0; i < (int)DuelBuffType.DuelBuffType_Max; i++)
                {
                    if (TeamHasBuff(this.team[team], i))
                        return -1;
                }
            }
            if (this.team[team].invulnerableTimer > 0)
                return -1;
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                if (!TeamIsActive(this.team[team], i))
                    continue;
                if (TeamGetState(this.team[team], i) != (int)DuelCharaState.DuelCharaState_NotJoined)
                    continue;
                if (!CanJoin(team, i))
                    continue;
                if (!CalcJoin(team, i))
                    continue;
                return i;
            }
            return -1;
        }

        /// <summary>50bcb0。行动判定</summary>
        public int CalcActionResult(int team, int chara, int action)
        {
            int opponentTeam = GetOpponentTeam(team);
            Debug.Assert(Utils.InRange(opponentTeam, 0, MaxTeamCount - 1));
            int opponentChara = GetCurrentChara(opponentTeam);
            Debug.Assert(Utils.InRange(opponentChara, 0, MaxTeamCharaCount - 1));

            if (IsInvulnerable(opponentTeam, opponentChara))
                return (int)DuelActionResult.DuelActionResult_Blocked;

            Person person = GetPerson(team, chara);
            Debug.Assert(Utils.IsActive(person));
            Person opponentPerson = GetPerson(opponentTeam, opponentChara);
            Debug.Assert(Utils.IsActive(opponentPerson));

            if (Utils.Contains(BlockTable, person.GetId()) && Utils.Contains(BlockTable, opponentPerson.GetId()) && system.RandBool(50))
                return (int)DuelActionResult.DuelActionResult_Blocked;

            int ratio = GetActionRatio(team, chara, opponentTeam, opponentChara);
            int hit = TeamGetStanceHit(this.team[team], chara);
            int block = TeamGetStanceBlock(this.team[opponentTeam], opponentChara);
            int n = hit;
            n = n * (100 - block) / 80; // 1 .. 1.25
            n = n * ratio / 50;          // 0 .. 1
            n = n + system.RandInt(10);  // 实际范围 1 .. 435
            n = Utils.Clamp(n, 10, 99);
            if (system.RandBool(n))
                return (int)DuelActionResult.DuelActionResult_Hit;

            if (CalcDodgeChance(opponentTeam, opponentChara))
                return (int)DuelActionResult.DuelActionResult_Dodged;
            return (int)DuelActionResult.DuelActionResult_Blocked;
        }

        /// <summary>50c170</summary>
        public void CalcAppearing()
        {
            for (int i = 0; i < MaxTeamCount; i++)
                appearingChara[i] = CalcAppearingChara(i);
        }

        /// <summary>50c1a0</summary>
        public void UpdateAction()
        {
            for (int i = 0; i < MaxAnimQueueSize; i++)
                actionQueue[i] = new Action();
            int team = CalcActorTeam();
            Debug.Assert(Utils.InRange(team, 0, MaxTeamCount - 1));
            int chara = GetCurrentChara(team);
            Debug.Assert(Utils.InRange(chara, 0, MaxTeamCharaCount - 1));
            int action = CalcAction(team, chara);
            Debug.Assert(Utils.InRange(action, 0, (int)DuelAction.DuelAction_Max - 1));
            actionCount = 1;
            if (action == (int)DuelAction.DuelAction_AttackCritical)
                actionCount = 3 + system.RandInt(2);
            for (int i = 0; i < actionCount; i++)
            {
                actionQueue[i].team = team;
                actionQueue[i].chara = chara;
                actionQueue[i].type = action;
                actionQueue[i].result = CalcActionResult(team, chara, action);
            }
        }

        /// <summary>50c280。普通攻击执行</summary>
        public bool ActionAnim()
        {
            if (actionCount <= 0)
                return false;
            for (int i = 0; i < actionCount; i++)
            {
                int team = actionQueue[i].team;
                int chara = actionQueue[i].chara;
                int opponentTeam = GetOpponentTeam(team);
                int opponentChara = GetCurrentChara(opponentTeam);
                int atkSpirit = 0;
                int defSpirit = 0;
                int hpDamage = CalcDamage(out atkSpirit, out defSpirit, team, chara, actionQueue[i].type, actionQueue[i].result);

                hpAnimQueue[i].damage = hpDamage;
                hpAnimQueue[i].atkTeam = team;
                hpAnimQueue[i].atkChara = chara;
                hpAnimQueue[i].defTeam = opponentTeam;
                hpAnimQueue[i].defChara = opponentChara;

                spiritAnimQueue[i].atkValue = CalcSpiritGain(team, chara, true, atkSpirit);
                spiritAnimQueue[i].atkTeam = team;
                spiritAnimQueue[i].atkChara = chara;
                spiritAnimQueue[i].defValue = CalcSpiritGain(opponentTeam, opponentChara, false, defSpirit);
                spiritAnimQueue[i].defTeam = opponentTeam;
                spiritAnimQueue[i].defChara = opponentChara;

                switch (actionQueue[i].type)
                {
                    case (int)DuelAction.DuelAction_DefenseCritical:
                        blowAnimQueue[i].value = 0;
                        break;
                    case (int)DuelAction.DuelAction_SpiritCritical:
                        spiritAnimQueue[i].atkValue = 100;
                        blowAnimQueue[i].value = 0;
                        break;
                    default:
                        blowAnimQueue[i].value = actionQueue[i].result != (int)DuelActionResult.DuelActionResult_Dodged ? 1 : 0;
                        break;
                }

                LogDebug($"Duel::action_anim {team}-{chara} {actionQueue[i].type} {actionQueue[i].result} {hpDamage} {spiritAnimQueue[i].atkValue} {spiritAnimQueue[i].defValue}");
            }

            PlayBlowAnim(actionCount);
            PlayHpAnim(actionCount);
            PlaySpiritAnim(actionCount);
            return true;
        }

        /// <summary>50c490。一合取胜的队伍（自动判定是否持弓）</summary>
        public static int CalcDuelFtkTeam(Person a, Person b, out int chance)
        {
            chance = 0;
            Scenario scenario = a.GetScenario();
            if (scenario.GetGame().IsFeatDisabled(Feature.DuelFirstTurnKill))
                return -1;
            if (!Utils.IsAlive(a))
                return -1;
            if (!Utils.IsAlive(b))
                return -1;
            int aShoubyou = a.GetShoubyou();
            int bShoubyou = b.GetShoubyou();
            bool aBow = false;
            bool bBow = false;
            foreach (Item item in scenario.GetGame().GetPersonItemList(a))
            {
                if (Utils.IsAlive(item) && item.GetTypeValue() == ItemType.Bow)
                {
                    aBow = true;
                    break;
                }
            }
            foreach (Item item in scenario.GetGame().GetPersonItemList(b))
            {
                if (Utils.IsAlive(item) && item.GetTypeValue() == ItemType.Bow)
                {
                    bBow = true;
                    break;
                }
            }
            return CalcDuelFtkTeam(a, b, aBow, bBow, aShoubyou, bShoubyou, out chance);
        }

        #endregion

        #region 初始化（50c930 - 50d780）

        /// <summary>50c930, v+0</summary>
        public virtual void Init()
        {
            LogDebug($"Duel::init 0x{system.GetSeed():x}");

            Reset();

            maxBlowCounter = param.maxBlowCounter;
            ftkType = param.ftkType;
            ftkTeam = param.ftkTeam;

            type = param.type;
            if (!Utils.InRange(type, 0, (int)DuelType.DuelType_Max - 1))
                type = (int)DuelType.DuelType_2;
            for (int i = 0; i < MaxTeamCount; i++)
                InitTeam(i);
            for (int i = 0; i < MaxTeamCount; i++)
                AiInit(ai[i], i);
            CalcActionRatio();

            SetNextPhase((int)DuelPhase.DuelPhase_Init);
        }

        /// <summary>50c980</summary>
        public void CharaInitSpecial(Character self, Person person)
        {
            if (!Utils.IsActive(person))
                return;
            for (int i = 0; i < self.specialRemainingCount.Length; i++)
                self.specialRemainingCount[i] = -1;
            self.specialRemainingCount[(int)DuelSpecial.DuelSpecial_Anki] = self.item[(int)DuelItemType.DuelItemType_ThrowingKnife] ? 1 : 0;
            self.specialRemainingCount[(int)DuelSpecial.DuelSpecial_Nisetaikyaku] = self.item[(int)DuelItemType.DuelItemType_Bow] ? 1 : 0;
        }

        /// <summary>50c9d0</summary>
        public Person TeamGetPerson(Team self, int chara)
        {
            Person person = self.chara[chara].person;
            if (Utils.IsActive(person))
                return person;
            return null;
        }

        /// <summary>50ca20</summary>
        public bool TeamIsActive(Team self, int chara)
        {
            Person person = self.chara[chara].person;
            return Utils.IsActive(person);
        }

        /// <summary>50ca70</summary>
        public void TeamSetBuffTimer(Team self, int type, int timer)
        {
            self.buffTimer[type] = timer;
        }

        /// <summary>50ca90</summary>
        public int TeamGetBuffTimer(Team self, int type)
        {
            return self.buffTimer[type];
        }

        /// <summary>50cab0</summary>
        public int TeamGetStance(Team self, int chara)
        {
            if (TeamIsActive(self, chara))
                return self.chara[chara].stance;
            return -1;
        }

        /// <summary>50cb00</summary>
        public void TeamSetStance(Team self, int chara, int stance)
        {
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                if (TeamIsActive(self, i))
                    self.chara[i].stance = stance;
            }
        }

        /// <summary>50cb80</summary>
        public int TeamGetState(Team self, int chara)
        {
            if (TeamIsActive(self, chara))
                return self.chara[chara].state;
            return -1;
        }

        /// <summary>50cbd0</summary>
        public int TeamGetHp(Team self, int chara)
        {
            if (TeamIsActive(self, chara))
                return self.chara[chara].hp;
            return MaxHP;
        }

        /// <summary>50cc20</summary>
        public void TeamSetHp(Team self, int chara, int value)
        {
            if (TeamIsActive(self, chara))
                self.chara[chara].hp = value;
        }

        /// <summary>50cc70</summary>
        public int TeamGetSpirit(Team self, int chara)
        {
            if (TeamIsActive(self, chara))
                return self.chara[chara].spirit;
            return MaxSpirit;
        }

        /// <summary>50ccc0</summary>
        public void TeamSetSpirit(Team self, int chara, int value)
        {
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                if (TeamIsActive(self, i))
                    self.chara[i].spirit = value;
            }
        }

        /// <summary>50cd40</summary>
        public int TeamGetShoubyou(Team self, int chara)
        {
            if (TeamIsActive(self, chara))
                return self.chara[chara].shoubyou;
            return -1;
        }

        /// <summary>50cd90</summary>
        public void TeamSetShoubyou(Team self, int chara, int shoubyou)
        {
            if (TeamIsActive(self, chara))
                self.chara[chara].shoubyou = shoubyou;
        }

        /// <summary>50cde0</summary>
        public int TeamGetNumber(Team self, int chara)
        {
            if (TeamIsActive(self, chara))
                return self.chara[chara].number;
            return -1;
        }

        /// <summary>50ce30</summary>
        public void TeamSetNumber(Team self, int chara, int value)
        {
            if (TeamIsActive(self, chara))
                self.chara[chara].number = value;
        }

        /// <summary>50ce80</summary>
        public int TeamGetSpecialRemainingCount(Team self, int chara, int special)
        {
            if (TeamIsActive(self, chara))
                return self.chara[chara].specialRemainingCount[special];
            return 0;
        }

        /// <summary>50cee0</summary>
        public void TeamSetSpecialRemainingCount(Team self, int chara, int special, int value)
        {
            if (TeamIsActive(self, chara))
                self.chara[chara].specialRemainingCount[special] = value;
        }

        /// <summary>50cf70</summary>
        public bool TeamHasItem(Team self, int chara, Bitset32 flags)
        {
            if (TeamIsActive(self, chara))
                return self.chara[chara].item.Intersects(flags);
            return false;
        }

        /// <summary>50cf90。武力</summary>
        public static int GetDuelStrength(Person self, int shoubyou, bool revised)
        {
            Scenario scenario = self.GetScenario();
            if (!Utils.IsActive(self))
                return 0;
            if (!Utils.InRange(shoubyou, 0, (int)Shoubyou.Hinshi))
                return 0;
            int n = self.CalcStat(PersonStatType.Strength, shoubyou);
            if (!revised)
                return n;
            int age;
            switch (self.GetId())
            {
                case PersonId.Ryofu:
                    return n + 10;
                case PersonId.Chouhi:
                case PersonId.Kanu:
                    return n + 5;
                case PersonId.Kyocho:
                case PersonId.Chouun:
                case PersonId.Bachou:
                    return n + 3;
                case PersonId.Kouchuu_Kanshou:
                    if (Utils.IsAlive(scenario) && scenario.GetLifeMode() == LifeMode.Virtual)
                        return n + 5;
                    age = self.GetAge();
                    if (age < 60)
                        return n + 1;
                    else if (age < 65)
                        return n + 2;
                    else if (age < 70)
                        return n + 3;
                    else if (age < 80)
                        return n + 4;
                    else if (age < 90)
                        return n + 5;
                    return n + 10;
            }
            return n;
        }

        /// <summary>50d0c0</summary>
        public int TeamGetStanceSpeed(Team self, int chara)
        {
            int stance = TeamGetStance(self, chara);
            Debug.Assert(Utils.InRange(stance, 0, (int)DuelStance.DuelStance_Max - 1));
            return StanceCoef[stance].speed;
        }

        /// <summary>50d0f0</summary>
        public int TeamGetStanceHit(Team self, int chara)
        {
            int stance = TeamGetStance(self, chara);
            Debug.Assert(Utils.InRange(stance, 0, (int)DuelStance.DuelStance_Max - 1));
            return StanceCoef[stance].hit;
        }

        /// <summary>50d120</summary>
        public int TeamGetStanceAttack(Team self, int chara)
        {
            int stance = TeamGetStance(self, chara);
            Debug.Assert(Utils.InRange(stance, 0, (int)DuelStance.DuelStance_Max - 1));
            return StanceCoef[stance].attack;
        }

        /// <summary>50d150</summary>
        public int TeamGetStanceBlock(Team self, int chara)
        {
            int stance = TeamGetStance(self, chara);
            Debug.Assert(Utils.InRange(stance, 0, (int)DuelStance.DuelStance_Max - 1));
            return StanceCoef[stance].block;
        }

        /// <summary>50d180</summary>
        public int TeamGetStanceAttackSub(Team self, int chara)
        {
            int stance = TeamGetStance(self, chara);
            Debug.Assert(Utils.InRange(stance, 0, (int)DuelStance.DuelStance_Max - 1));
            return StanceCoef[stance].attackSub;
        }

        /// <summary>50d1b0</summary>
        public int TeamGetStanceSpiritGain(Team self, int chara)
        {
            int stance = TeamGetStance(self, chara);
            Debug.Assert(Utils.InRange(stance, 0, (int)DuelStance.DuelStance_Max - 1));
            return StanceCoef[stance].spiritGain;
        }

        /// <summary>50d270</summary>
        public bool TeamHasSubChara(Team self, int state)
        {
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                if (self.currentChara == i)
                    continue;
                if (!TeamIsActive(self, i))
                    continue;
                if (TeamGetState(self, i) == state)
                    return true;
            }
            return false;
        }

        /// <summary>50d2f0</summary>
        public bool TeamHasBuff(Team self, int type)
        {
            int timer = self.buffTimer[type];
            return timer == -1 || timer > 0;
        }

        /// <summary>50d320</summary>
        public bool TeamChangeCurrentChara(Team self, int chara)
        {
            int oldChara = self.currentChara;
            self.currentChara = chara;
            if (TeamIsActive(self, oldChara))
                self.chara[oldChara].state = (int)DuelCharaState.DuelCharaState_Waiting;
            if (TeamIsActive(self, chara))
                self.chara[chara].state = (int)DuelCharaState.DuelCharaState_Active;
            for (int i = 0; i < (int)DuelBuffType.DuelBuffType_Max; i++)
                self.buffTimer[i] = 0;
            return true;
        }

        /// <summary>50d3e0</summary>
        public int TeamGetStrength(Team self, int chara, bool revised)
        {
            if (TeamIsActive(self, chara))
                return GetDuelStrength(TeamGetPerson(self, chara), TeamGetShoubyou(self, chara), revised);
            return Person.MinStat;
        }

        /// <summary>50d420</summary>
        public int TeamAddHp(Team self, int chara, int value)
        {
            int diff = MaxHP;
            if (TeamIsActive(self, chara))
            {
                int n = self.chara[chara].hp;
                int max = MaxHP;
                diff = Utils.Clamp(n + value, 0, max) - n;
                self.chara[chara].hp += diff;
            }
            return diff;
        }

        /// <summary>50d4a0</summary>
        public int TeamAddSpirit(Team self, int chara, int value)
        {
            int diff = MaxSpirit;
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                if (TeamIsActive(self, i))
                {
                    int n = self.chara[i].spirit;
                    int max = MaxSpirit;
                    diff = Utils.Clamp(n + value, 0, max) - n;
                    self.chara[i].spirit += diff;
                }
            }
            return diff;
        }

        /// <summary>50d5b0</summary>
        public void CharaInitItem(Character self, Person person)
        {
            self.item = Bitset32.Empty;
            if (!Utils.IsActive(person))
                return;
            foreach (Item item in system.GetPersonItemList(person))
            {
                if (!Utils.IsAlive(item))
                    continue;
                switch (item.GetTypeValue())
                {
                    case ItemType.EliteHorse:
                        self.item[(int)DuelItemType.DuelItemType_EliteHorse] = true;
                        break;
                    case ItemType.Sword:
                        self.item[(int)DuelItemType.DuelItemType_Sword] = true;
                        break;
                    case ItemType.LongSpear:
                        self.item[(int)DuelItemType.DuelItemType_LongSpear] = true;
                        switch (item.GetItemId())
                        {
                            case ItemId.SerpentBlade:
                                self.item[(int)DuelItemType.DuelItemType_SerpentBlade] = true;
                                break;
                            case ItemId.BlueDragon:
                                self.item[(int)DuelItemType.DuelItemType_BlueDragon] = true;
                                break;
                            case ItemId.CrescentHalberd:
                                self.item[(int)DuelItemType.DuelItemType_CrescentHalberd] = true;
                                break;
                        }
                        break;
                    case ItemType.ThrowingKnife:
                        self.item[(int)DuelItemType.DuelItemType_ThrowingKnife] = true;
                        break;
                    case ItemType.Bow:
                        self.item[(int)DuelItemType.DuelItemType_Bow] = true;
                        break;
                }
            }
        }

        /// <summary>50d700</summary>
        public void CharaInit(Character self, Person person, int hp, int spirit, int shoubyou)
        {
            Character fresh = new Character();
            self.person = fresh.person;
            self.hp = fresh.hp;
            self.spirit = fresh.spirit;
            self.shoubyou = fresh.shoubyou;
            self.stance = fresh.stance;
            self.state = fresh.state;
            self.number = fresh.number;
            self.item = fresh.item;
            self.specialRemainingCount = fresh.specialRemainingCount;

            if (!Utils.IsActive(person))
                return;
            self.person = person;
            self.hp = hp;
            self.spirit = spirit;
            self.shoubyou = shoubyou;
            CharaInitItem(self, person);
            CharaInitSpecial(self, person);
        }

        /// <summary>50d780</summary>
        public void TeamInit(Team self, Person[] personArray, int[] hpArray, int[] spiritArray, int[] shoubyouArray, int count, int currentChara, int control, int playerId)
        {
            Debug.Assert(personArray != null);
            Debug.Assert(hpArray != null);
            Debug.Assert(spiritArray != null);
            Debug.Assert(shoubyouArray != null);
            Debug.Assert(Utils.InRange(count, 1, MaxTeamCharaCount));

            self.chara = Team.NewCharacterArray();
            self.charaCount = 0;
            self.appearingTimer = 0;
            self.switchingTimer = 0;
            self.invulnerableTimer = 0;
            self.stanceTimer = 0;
            self.buffTimer = new int[(int)DuelBuffType.DuelBuffType_Max];

            self.currentChara = currentChara;
            if (!Utils.InRange(self.currentChara, 0, MaxTeamCharaCount - 1))
                self.currentChara = 0;
            self.charaCount = count;
            self.control = control;
            self.playerId = playerId;
            for (int i = 0; i < count; i++)
            {
                Person person = personArray[i];
                if (!Utils.IsActive(person))
                    continue;
                CharaInit(self.chara[i], person, hpArray[i], spiritArray[i], shoubyouArray[i]);
                self.chara[i].state = self.currentChara == i
                    ? (int)DuelCharaState.DuelCharaState_Active
                    : (int)DuelCharaState.DuelCharaState_NotJoined;
            }
        }

        #endregion

        #region Param 访问（50dac0 - 50e2b0）

        /// <summary>50dac0</summary>
        public Unit ParamGetWinnerUnit(Param self)
        {
            if (!Utils.InRange(self.winnerTeam, 0, MaxTeamCount - 1))
                return null;
            return self.unit[self.winnerTeam];
        }

        /// <summary>50dae0</summary>
        public Unit ParamGetLoserUnit(Param self)
        {
            if (!Utils.InRange(self.loserTeam, 0, MaxTeamCount - 1))
                return null;
            return self.unit[self.loserTeam];
        }

        /// <summary>50db00</summary>
        public int ParamGetCharaResult(Param self, int team, int chara)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return -1;
            if (!Utils.InRange(chara, 0, MaxTeamCharaCount - 1))
                return -1;
            return self.result[team][chara];
        }

        /// <summary>50db40</summary>
        public Unit ParamGetUnit(Param self, int team, int chara)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return null;
            if (!Utils.InRange(chara, 0, MaxTeamCharaCount - 1))
                return null;
            return self.unit[team];
        }

        /// <summary>50db80</summary>
        public int ParamGetHp(Param self, int team, int chara)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return 0;
            if (!Utils.InRange(chara, 0, MaxTeamCharaCount - 1))
                return 0;
            return self.hp[team][chara];
        }

        /// <summary>50dbc0</summary>
        public int ParamGetSpirit(Param self, int team, int chara)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return 0;
            if (!Utils.InRange(chara, 0, MaxTeamCharaCount - 1))
                return 0;
            return self.spirit[team][chara];
        }

        /// <summary>50dc10</summary>
        public int ParamGetShoubyou(Param self, int team, int chara)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return -1;
            if (!Utils.InRange(chara, 0, MaxTeamCharaCount - 1))
                return -1;
            return self.shoubyou[team][chara];
        }

        /// <summary>50dc60</summary>
        public Person ParamGetPerson(Param self, int team, int chara)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return null;
            if (!Utils.InRange(chara, 0, MaxTeamCharaCount - 1))
                return null;
            return self.person[team][chara];
        }

        /// <summary>50dca0</summary>
        public Person ParamGetWinnerPerson(Param self)
        {
            return ParamGetPerson(self, self.winnerTeam, self.winnerChara);
        }

        /// <summary>50dcd0</summary>
        public Person ParamGetLoserPerson(Param self)
        {
            return ParamGetPerson(self, self.loserTeam, self.loserChara);
        }

        /// <summary>50dd00</summary>
        public int ParamGetStartChara(Param self, int team)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return -1;
            return self.startChara[team];
        }

        /// <summary>50dd30</summary>
        public int ParamGetPlayerId(Param self, int team)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return -1;
            return self.playerId[team];
        }

        /// <summary>50dd60</summary>
        public int ParamGetControl(Param self, int team)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return -1;
            return self.control[team];
        }

        /// <summary>50dd90</summary>
        public bool ParamIsManual(Param self)
        {
            return self.control[0] == (int)DuelControl.DuelControl_Manual || self.control[1] == (int)DuelControl.DuelControl_Manual;
        }

        /// <summary>50e290</summary>
        public Person ParamGetChallenger(Param self)
        {
            int chara = self.startChara[(int)DuelTeam.DuelTeam_Challenger];
            if (Utils.InRange(chara, 0, MaxTeamCharaCount - 1))
                return self.person[(int)DuelTeam.DuelTeam_Challenger][chara];
            return null;
        }

        /// <summary>50e2b0</summary>
        public Person ParamGetChallenged(Param self)
        {
            int chara = self.startChara[(int)DuelTeam.DuelTeam_Challenged];
            if (Utils.InRange(chara, 0, MaxTeamCharaCount - 1))
                return self.person[(int)DuelTeam.DuelTeam_Challenged][chara];
            return null;
        }

        #endregion

        #region 运行（50ed00）

        /// <summary>50ed00。运行单挑直到结束</summary>
        /// <returns>是否走了表现层（即玩家确认进入单挑）</returns>
        public bool Run()
        {
            bool useView = false;
            if (!param.tutorial)
            {
                if (param.control[0] == (int)DuelControl.DuelControl_Manual || param.control[1] == (int)DuelControl.DuelControl_Manual)
                {
                    int challengerCount = 0;
                    int challengedCount = 0;
                    for (int i = 0; i < MaxTeamCharaCount; i++)
                    {
                        if (Utils.IsAlive(param.person[0][i]))
                            challengerCount++;
                        if (Utils.IsAlive(param.person[1][i]))
                            challengedCount++;
                    }
                    Message msg = new Message();
                    msg.SetObj0Obj1Obj2Obj3Obj4Obj5Num0Num1(
                        DuelMessageId.N_WAR_IKKI_KAKUNIN,
                        param.person[0][0], param.person[0][1], param.person[0][2],
                        param.person[1][0], param.person[1][1], param.person[1][2],
                        challengerCount, challengedCount);
                    useView = engine != null && engine.YesNo(system.GetMessage(msg));
                    if (!useView)
                        engine = null;
                }
            }

            Init();

            // 原代码为阻塞式死循环；此处加入上限保护，避免阶段卡死导致 Unity 主线程冻结
            int guard = 0;
            while (!OnPhase(0))
            {
                if (++guard > 1000000)
                    break;
            }
            Exit();
            return useView;
        }

        /// <summary>682780, v+10</summary>
        public virtual bool IsTutorial()
        {
            return false;
        }

        #endregion
    }
}
