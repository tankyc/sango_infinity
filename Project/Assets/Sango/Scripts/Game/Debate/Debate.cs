/*
 * 文件名：Debate.cs
 * 描述：舌战(Debate)系统主体，由 s11_sys_debate.h + s11_sys_debate.cpp 翻译而来
 *
 * 翻译约定：
 *   1. C++ 的 typedef int xxx_t（debate_team_t / debate_card_t 等）统一使用 int，
 *      使 -1 表示无效值的语义得以保留
 *   2. 成员方法由 snake_case 改为 PascalCase
 *   3. C++ 的 std::array 改为 C# 数组；嵌套 struct 改为 class（引用类型，避免值拷贝语义差异）
 *   4. 原 C++ 的 Message / point16 / MilitaryUnitObject 等外部类型改为接口存根（见 DebateExternal.cs）
 *   5. 原 C++ 的话题卡结算方法 wadai() 与本命名空间的 Topic 枚举同名，重命名为 TopicCard()
 *   6. 该类为 partial：AI 决策在 DebateAI.cs，阶段状态机在 DebatePhase.cs
 *
 * 命名对照（C++ 罗马音 -> 英文）：
 *   wadai/topic_t   -> Topic       话题（故事 / 道理 / 时势）
 *   wajutsu         -> Rhetoric    话术（大喝 / 诡辩 / 无视 / 镇静 / 激昂）
 *   seikaku         -> Personality 性格（胆小 / 冷静 / 刚胆 / 莽撞）
 *   shoubyou        -> Injury      伤病（健康 / 轻伤 / 中伤 / 重伤）
 *   daikatsu        -> Shout       大喝
 *   reflected           -> Sophistry   诡辩
 *   mushi           -> Ignore      无视
 *   chinsei         -> Compose     镇静
 *   gyakujou        -> Agitate     激昂
 *   kouseki         -> Merit       功绩
 *   kakunin         -> Confirm     确认
 */

using System;
using System.Collections.Generic;

namespace Sango.Core.Debate
{
    /// <summary>
    /// 舌战系统。对应 C++ Debate 类。
    /// </summary>
    public partial class Debate
    {
        #region 常量

        /// <summary>参战队伍数</summary>
        public const int MaxTeamCount = 2;

        /// <summary>体力下限</summary>
        public const int MinHP = -100;

        /// <summary>体力上限</summary>
        public const int MaxHP = 1000;

        /// <summary>愤怒（压力）上限</summary>
        public const int MaxStress = 100;

        /// <summary>卡组牌堆长度</summary>
        public const int DeckTableSize = 18;

        /// <summary>手牌上限</summary>
        public const int MaxCardCount = 7;

        #endregion

        #region 嵌套类型

        /// <summary>舌战启动参数 / 结算结果。对应 C++ Debate::Param</summary>
        public class Param
        {
            /// <summary>启动参数中的武将信息。对应 C++ Debate::Param::Character</summary>
            public class Character
            {
                public Person person = null; // 0
                public int hp = MaxHP;       // 4
                public bool control = false; // 8
            }

            /// <summary>参战武将（下标为队伍）</summary>
            public Character[] characters = NewParamCharacterArray(); // 0
            /// <summary>是否反转（玩家在应战方）</summary>
            public bool reverse = false; // 18
            /// <summary>获胜队伍</summary>
            public int winner = -1; // 1c
            /// <summary>胜利方式</summary>
            public int winType = (int)DebateWinType.DebateWinType_Normal; // 20
            /// <summary>是否教程</summary>
            public bool tutorial = false; // 24
            /// <summary>是否已结束</summary>
            public bool finished = false; // 28
            /// <summary>返回场景</summary>
            public int retScene = (int)Scene.Scene_Max; // 2c

            internal static Character[] NewParamCharacterArray()
            {
                Character[] array = new Character[MaxTeamCount];
                for (int i = 0; i < array.Length; i++)
                    array[i] = new Character();
                return array;
            }
        }

        /// <summary>卡组。对应 C++ Debate::Deck</summary>
        public class Deck
        {
            /// <summary>牌堆（初始全为 -1）</summary>
            public int[] table = NewFilledArray(DeckTableSize, -1); // 0
            /// <summary>牌堆读取下标</summary>
            public int index = 0; // 48
            /// <summary>可使用的话术卡</summary>
            public int[] rhetoricCard = NewFilledArray((int)Rhetoric.Rhetoric_Max, -1); // 4c
            /// <summary>可使用的话术卡数量</summary>
            public int rhetoricCardCount = 0; // 60

            /// <summary>构造：牌堆各字段已由字段初始化器填好，此处无需额外处理</summary>
            public Deck()
            {
            }
        }

        /// <summary>舌战中的武将运行时数据。对应 C++ Debate::Character</summary>
        public class Character
        {
            /// <summary>武将</summary>
            public Person person = null; // 0
            /// <summary>体力</summary>
            public int hp = 0; // 4
            /// <summary>愤怒（压力）</summary>
            public int stress = 0; // 8
            /// <summary>兴奋计时器</summary>
            public int angerTimer = 0; // c
            /// <summary>攻击力</summary>
            public int attack = 0; // 10
            /// <summary>手牌</summary>
            public int[] card = NewFilledArray(MaxCardCount, -1); // 14
            /// <summary>手牌上限</summary>
            public int maxCardCount = 0; // 30
            /// <summary>卡组</summary>
            public Deck deck = new Deck(); // 34
            /// <summary>是否玩家操作</summary>
            public bool control = false; // 98
            /// <summary>性格</summary>
            public int personality = -1; // 9c

            /// <summary>构造：各字段已由字段初始化器填好，此处无需额外处理</summary>
            public Character()
            {
            }
        }

        /// <summary>AI 上下文。对应 C++ Debate::AI</summary>
        public class AI
        {
            /// <summary>AI 决策表行。对应 C++ Debate::AI::Row</summary>
            public class Row
            {
                /// <summary>决策函数 id（对应 DebateAIRow）</summary>
                public int id = (int)DebateAIRow.DebateAIRow_Max; // 0
                public int param1 = 0; // 4
                public int param2 = 0; // 8

                public Row() { }
                public Row(int id, int param1, int param2)
                {
                    this.id = id;
                    this.param1 = param1;
                    this.param2 = param2;
                }
            }

            public Debate parent = null; // 0
            public int team = -1;        // 4
            public int opponentTeam = -1; // 8
        }

        #endregion

        #region 字段

        /// <summary>当前阶段</summary>
        protected int phase = -1; // 4
        /// <summary>下一个阶段</summary>
        protected int nextPhase = -1; // 8
        /// <summary>当前阶段内步骤</summary>
        protected int step = 0; // c
        /// <summary>参战武将运行时数据</summary>
        protected Character[] characters = NewCharacterArray(); // 10
        /// <summary>AI 上下文（下标为队伍）</summary>
        protected AI[] aiContext = NewAIArray(); // 150
        /// <summary>当前话题</summary>
        protected int topic = -1; // 168
        /// <summary>先攻方</summary>
        protected int first = -1; // 16c
        /// <summary>已出的卡牌</summary>
        protected int[] playedCard = new int[] { -1, -1 }; // 170
        /// <summary>出牌更强的一方</summary>
        protected int attacker = -1; // 178
        /// <summary>即将愤怒的一方</summary>
        protected int angering = -1; // 17c
        /// <summary>会心选择</summary>
        protected int critical = -1; // 180
        /// <summary>是否可再考</summary>
        protected bool[] canRethink = new bool[MaxTeamCount]; // 184
        /// <summary>崩坏程度 (1000 - hp) / 250</summary>
        protected int[] crumbledLevel = new int[MaxTeamCount]; // 18c
        /// <summary>是否崩坏</summary>
        protected bool[] crumbled = new bool[MaxTeamCount]; // 194
        /// <summary>获胜队伍</summary>
        protected int winner = -1; // 19c
        /// <summary>结果</summary>
        protected int winType = (int)DebateWinType.DebateWinType_Normal; // 1a0
        /// <summary>连续攻击方</summary>
        protected int comboAttacker = -1; // 1a4
        /// <summary>连续攻击计数</summary>
        protected int comboCounter = 0; // 1a8
        /// <summary>是否连续攻击（小心性格）</summary>
        protected bool combo = false; // 1ac
        /// <summary>系统（业务层，见 DebateGameSystem.cs）</summary>
        protected DebateGameSystem system = null;
        /// <summary>表现层（见 DebateView.cs 的 IDebateView）</summary>
        protected IDebateView engine = null;
        /// <summary>启动参数</summary>
        protected Param param = null;
        /// <summary>是否启用表现层</summary>
        protected bool view = false;

        #endregion

        #region 构造 / 外部访问器

        /// <summary>构造舌战。对应 C++ Debate::Debate(System*, Param*)</summary>
        public Debate(DebateGameSystem system, Param param)
        {
            this.system = system;
            this.engine = system != null ? system.GetEngine() : null;
            this.param = param;
        }

        /// <summary>是否启用表现层（false 时为纯逻辑推演，数值立即结算）</summary>
        public bool View { get { return view; } set { view = value; } }

        /// <summary>
        /// 逻辑层是否空闲。表现层正在播表现（动画、结算画面）或消息框开着时返回 false，
        /// 阶段驱动会停在当前步骤等待。对应单挑的 Duel.IsIdle。
        ///
        /// 无表现层（view = false，纯逻辑推演 / 单元测试）或没有表现层实现时恒为 true，数值立即结算。
        /// </summary>
        public virtual bool IsIdle()
        {
            if (!view)
                return true;
            if (engine == null)
                return true;
            if (engine.DebateIsAnimating(this))
                return false;
            if (system != null && system.MessageBoxVisible && engine.DebateIsMessageBoxVisible(this))
                return false;
            return true;
        }

        /// <summary>启动参数 / 结算结果</summary>
        public Param DebateParam { get { return param; } }

        /// <summary>当前阶段</summary>
        public int Phase { get { return phase; } }

        /// <summary>当前步骤</summary>
        public int Step { get { return step; } }

        /// <summary>获取指定队伍的武将</summary>
        public Character GetCharacter(int team) { return characters[team]; }

        /// <summary>设置当前话题</summary>
        public void SetTopic(int topic) { this.topic = topic; }

        /// <summary>当前话题（对应 C++ wadai_）。表现层用它高亮"与本回合话题一致"的手牌</summary>
        public int CurrentTopic { get { return topic; } }

        /// <summary>
        /// 当前胜者（对应 C++ winner_）。
        /// 注意与 Param.winner 的区别：后者要等 ClosingPhase 末尾的 ParamSetWinner 才写入，
        /// 而表现层是在 DebateClosing 回调里就要显示胜负的，所以必须读这个。
        /// </summary>
        public int CurrentWinner { get { return winner; } }

        /// <summary>当前胜利方式（对应 C++ win_type_），同样先于 Param.winType 就绪</summary>
        public int CurrentWinType { get { return winType; } }

        /// <summary>
        /// 该方这一合还能不能再考（对应 C++ can_rethink_）。
        /// C++ 里这个标记只被 AI 决策行读（ai_cond_card_rethink），玩家侧漏了判断，
        /// 表现层据此把「再考」牌置灰，避免玩家连点同一张再考卡导致出牌→重抽→再出牌的死循环。
        /// </summary>
        public bool CanRethink(int team)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return false;
            return canRethink[team];
        }

        #endregion

        #region 卡组 / 手牌

        /// <summary>生成卡组</summary>
        public void DeckGenerate(Deck self)
        {
            // 4, 1, 7, 5, 2, 8, 6, 3, 9
            int[] table = {
                (int)DebateCard.DebateCard_Logic1, (int)DebateCard.DebateCard_Story1, (int)DebateCard.DebateCard_Trend1,
                (int)DebateCard.DebateCard_Logic2, (int)DebateCard.DebateCard_Story2, (int)DebateCard.DebateCard_Trend2,
                (int)DebateCard.DebateCard_Logic3, (int)DebateCard.DebateCard_Story3, (int)DebateCard.DebateCard_Trend3,
            };
            // 小卡
            for (int i = 0; i < 8; i++)
                self.table[i] = table[system.RandInt(3)];
            // 中卡
            for (int i = 0; i < 4; i++)
                self.table[8 + i] = table[3 + system.RandInt(3)];
            // 大卡
            for (int i = 0; i < 2; i++)
                self.table[12 + i] = table[6 + system.RandInt(3)];
            // 打乱话术卡
            if (self.rhetoricCardCount != 0)
            {
                for (int i = 0; i < 2000; i++)
                    Utils.Swap(ref self.rhetoricCard[i % self.rhetoricCardCount], ref self.rhetoricCard[system.RandInt(self.rhetoricCardCount)]);
            }
            // 话术卡（没有则用随机攻击卡）
            for (int i = 0; i < 4; i++)
            {
                if (i < self.rhetoricCardCount)
                    self.table[14 + i] = self.rhetoricCard[i];
                else
                    self.table[14 + i] = table[system.RandInt(9)];
            }
            // 整体打乱
            for (int i = 0; i < 2000; i++)
                Utils.Swap(ref self.table[i % 18], ref self.table[system.RandInt(18)]);
        }

        /// <summary>根据智力计算手牌上限</summary>
        public static int GetMaxCardCount(Person person)
        {
            if (!Utils.IsAlive(person))
                return 0;
            int intelligence = person.GetStat(PersonStatType.PersonStatType_Intelligence);
            if (intelligence < 70)
                return 4;
            else if (intelligence < 80)
                return 5;
            else if (intelligence < 90)
                return 6;
            return 7;
        }

        /// <summary>获取智力</summary>
        public int CharacterGetIntelligence(Character self)
        {
            return self.person.GetStat(PersonStatType.PersonStatType_Intelligence);
        }

        /// <summary>获取武力</summary>
        public int CharacterGetStrength(Character self)
        {
            return self.person.GetStat(PersonStatType.PersonStatType_Strength);
        }

        /// <summary>获取性格</summary>
        public int CharacterGetPersonality(Character self)
        {
            if (Utils.InRange(self.personality, 0, (int)Personality.Personality_Max - 1))
                return self.personality;
            return self.person.GetPersonality();
        }

        /// <summary>设置兴奋计时器</summary>
        public void CharacterSetAngerTimer(Character self)
        {
            switch (CharacterGetPersonality(self))
            {
                case (int)Personality.Personality_Timid:
                case (int)Personality.Personality_Reckless:
                    self.angerTimer = 1;
                    break;
                case (int)Personality.Personality_Calm:
                case (int)Personality.Personality_Bold:
                    self.angerTimer = 4;
                    break;
            }
        }

        /// <summary>递减兴奋计时器</summary>
        public void CharacterDecAngerTimer(Character self)
        {
            if (self.angerTimer > 0)
                self.angerTimer--;
        }

        /// <summary>重置兴奋计时器</summary>
        public void CharacterResetAngerTimer(Character self)
        {
            self.angerTimer = 0;
        }

        /// <summary>手牌排序</summary>
        public void CharacterSortCards(Character self)
        {
            Array.Sort(self.card, 0, self.maxCardCount);
        }

        /// <summary>补满手牌</summary>
        public void CharacterFillCards(Character self)
        {
            self.card[0] = (int)DebateCard.DebateCard_Rethink;
            int last = -1;
            for (int i = 1; i < self.maxCardCount; i++)
            {
                if (self.card[i] != -1)
                    continue;
                if (self.deck.index >= DeckTableSize)
                {
                    DeckGenerate(self.deck);
                    self.deck.index = 0;
                }
                self.card[i] = self.deck.table[self.deck.index++];
                last = i;
            }
            // 至少保证一张攻击卡
            int topicCardCount = 0;
            for (int i = 1; i < self.maxCardCount; i++)
            {
                if (Utils.InRange(self.card[i], (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
                    topicCardCount++;
            }
            if (topicCardCount == 0 && last >= 0)
                self.card[last] = (int)DebateCard.DebateCard_TopicFirst + system.RandInt((int)DebateCard.DebateCard_TopicLast - (int)DebateCard.DebateCard_TopicFirst + 1);
            CharacterSortCards(self);
        }

        /// <summary>设置手牌</summary>
        public void CharacterSetCard(Character self, int index, int card)
        {
            self.card[index] = card;
        }

        /// <summary>获取手牌</summary>
        public int CharacterGetCard(Character self, int index)
        {
            return self.card[index];
        }

        /// <summary>移除手牌并压缩</summary>
        public void CharacterRemoveCard(Character self, int index)
        {
            CharacterSetCard(self, index, -1);
            int[] temp = (int[])self.card.Clone();
            for (int i = 0; i < MaxCardCount; i++)
                self.card[i] = -1;
            int p = 0;
            for (int i = 0; i < self.maxCardCount; i++)
            {
                if (temp[i] != -1)
                    self.card[p++] = temp[i];
            }
        }

        /// <summary>查找指定手牌下标</summary>
        public int CharacterGetCardIndex(Character self, int card)
        {
            for (int i = 0; i < self.maxCardCount; i++)
            {
                if (self.card[i] == card)
                    return i;
            }
            return -1;
        }

        /// <summary>查找空手牌下标</summary>
        public int CharacterGetEmptyCardIndex(Character self)
        {
            for (int i = 0; i < self.maxCardCount; i++)
            {
                if (self.card[i] == -1)
                    return i;
            }
            return -1;
        }

        /// <summary>再考（重抽手牌）</summary>
        public void CharacterRethink(Character self, int topic)
        {
            for (int i = 0; i < self.maxCardCount; i++)
                self.card[i] = -1;
            CharacterFillCards(self);
            if (self.angerTimer > 0)
            {
                int personality = CharacterGetPersonality(self);
                if (personality == (int)Personality.Personality_Calm && system.RandBool(40))
                {
                    // 8b3294
                    int[] card = {
                        (int)DebateCard.DebateCard_Story3, (int)DebateCard.DebateCard_Logic3, (int)DebateCard.DebateCard_Trend3,
                    };
                    if (Utils.InRange(topic, 0, (int)Topic.Topic_Max - 1))
                        self.card[1] = card[topic];
                }
            }
            CharacterSortCards(self);
        }

        /// <summary>设置体力</summary>
        public void CharacterSetHp(Character self, int value)
        {
            self.hp = Utils.Clamp(value, MinHP, MaxHP);
        }

        /// <summary>增减体力</summary>
        public void CharacterAddHp(Character self, int value)
        {
            CharacterSetHp(self, self.hp + value);
        }

        /// <summary>设置愤怒</summary>
        public void CharacterSetStress(Character self, int value)
        {
            self.stress = Utils.Clamp(value, 0, MaxStress);
        }

        /// <summary>增减愤怒</summary>
        public void CharacterAddStress(Character self, int value)
        {
            CharacterSetStress(self, self.stress + value);
        }

        /// <summary>初始化卡组</summary>
        public bool DeckInit(Deck self, Person person)
        {
            // 持有"书籍"可解锁全部话术。宝物(Item)暂未接入，改走业务层预留钩子（见 DebateGameSystem.HasAllRhetoric）
            bool book = system.HasAllRhetoric(person);
            // 武将行为（数据驱动，见 DebatePersonBehaviours）：wordTac / 书籍是基础值，behaviour 可以追加也能覆盖
            DebatePersonBehaviour behaviour = DebatePersonBehaviours.Get(person);
            self.rhetoricCardCount = 0;
            for (int i = 0; i < (int)Rhetoric.Rhetoric_Max; i++)
            {
                if (behaviour.ModifyRhetoric(person, i, book || person.HasRhetoric(i)))
                    self.rhetoricCard[self.rhetoricCardCount++] = (int)DebateCard.DebateCard_RhetoricFirst + i;
            }
            DeckGenerate(self);
            return true;
        }

        /// <summary>初始化武将运行时数据</summary>
        public bool CharacterInit(Character self, Person person, Person opponentPerson, int hp, bool control)
        {
            if (hp <= 0)
                hp = MaxHP;
            self.person = person;
            self.hp = hp;
            int myInt = person.GetStat(PersonStatType.PersonStatType_Intelligence); // 1 .. 100
            int opponentInt = opponentPerson.GetStat(PersonStatType.PersonStatType_Intelligence); // 1 .. 100
            int n = 40 * (myInt - opponentInt) / (131 - myInt); // -30 .. 127
            self.attack = 100 + n; // 70 .. 227
            // 武将行为（数据驱动，见 DebatePersonBehaviours）：公式算完再覆盖
            self.attack = DebatePersonBehaviours.Get(person).ModifyAttack(person, opponentPerson, self.attack);
            CharacterInitCards(self, GetMaxCardCount(person));
            self.control = control;
            self.stress = 0;
            self.angerTimer = 0;
            return true;
        }

        /// <summary>初始化手牌</summary>
        public void CharacterInitCards(Character self, int maxCardCount = MaxCardCount)
        {
            self.maxCardCount = maxCardCount;
            for (int i = 0; i < MaxCardCount; i++)
                self.card[i] = -1;
            DeckInit(self.deck, self.person);
            CharacterFillCards(self);
        }

        #endregion

        #region Param 相关

        /// <summary>根据势力判断操作方</summary>
        public void ParamSetControl(Param self)
        {
            // C++ 的 district 在工程里是军团 Corps：is_player() && get_number()==1 正好等价于 Corps.IsPlayerControl
            Corps corps = system.GetDistrict(self.characters[0].person.GetDistrictId());
            self.characters[0].control = Utils.IsActive(corps) && corps.IsPlayerControl;
            corps = system.GetDistrict(self.characters[1].person.GetDistrictId());
            self.characters[1].control = Utils.IsActive(corps) && corps.IsPlayerControl;
        }

        /// <summary>是否挑战方获胜</summary>
        public bool ParamIsChallengerWin(Param self)
        {
            int team = self.reverse ? (int)DebateTeam.DebateTeam_Challenged : (int)DebateTeam.DebateTeam_Challenger;
            return self.winner == team;
        }

        /// <summary>获取获胜武将</summary>
        public Person ParamGetWinnerPerson(Param self)
        {
            int team = self.reverse ? (int)DebateTeam.DebateTeam_Challenged : (int)DebateTeam.DebateTeam_Challenger;
            if (self.winner == team)
                return self.characters[(int)DebateTeam.DebateTeam_Challenger].person;
            if (self.winner == GetOpponentTeam(team))
                return self.characters[(int)DebateTeam.DebateTeam_Challenged].person;
            return null;
        }

        /// <summary>获取失败武将</summary>
        public Person ParamGetLoserPerson(Param self)
        {
            int team = self.reverse ? (int)DebateTeam.DebateTeam_Challenged : (int)DebateTeam.DebateTeam_Challenger;
            if (self.winner == team)
                return self.characters[(int)DebateTeam.DebateTeam_Challenged].person;
            if (self.winner == GetOpponentTeam(team))
                return self.characters[(int)DebateTeam.DebateTeam_Challenger].person;
            return null;
        }

        /// <summary>获取挑战方武将</summary>
        public Person ParamGetChallengerPerson(Param self)
        {
            return self.characters[(int)DebateTeam.DebateTeam_Challenger].person;
        }

        /// <summary>获取指定队伍武将（考虑反转）</summary>
        public Person ParamGetPerson(Param self, int team)
        {
            if (self.reverse)
                team = GetOpponentTeam(team);
            return self.characters[team].person;
        }

        /// <summary>获取指定队伍体力（考虑反转）</summary>
        public int ParamGetHp(Param self, int team)
        {
            if (self.reverse)
                team = GetOpponentTeam(team);
            return self.characters[team].hp;
        }

        /// <summary>获取指定队伍操作方（考虑反转）</summary>
        public bool ParamGetControl(Param self, int team)
        {
            if (self.reverse)
                team = GetOpponentTeam(team);
            return self.characters[team].control;
        }

        /// <summary>结算胜利方（含经验/功绩/伤病/消息）</summary>
        public void ParamSetWinner(Param self, int team, int type)
        {
            self.winner = team;
            self.winType = type;
            Person person = ParamGetPerson(self, team);
            Person opponentPerson = ParamGetPerson(self, GetOpponentTeam(team));
            int exp = 10;
            int merit = 100;
            bool injured = false;
            switch (type)
            {
                case (int)DebateWinType.DebateWinType_HaveMercy:
                    // 技巧增加
                    if (Utils.IsAlive(person))
                    {
                        Force force = system.GetForce(person.GetForceId());
                        if (Utils.IsAlive(force))
                            system.ForceAddTechPoint(force, 50, null);
                    }
                    // 负伤
                    if (Utils.IsAlive(opponentPerson))
                    {
                        int injury = opponentPerson.GetInjury();
                        if (Utils.InRange(injury, 0, (int)Injury.Injury_Severe))
                        {
                            system.PersonSetInjury(opponentPerson, injury + 1);
                            injured = true;
                        }
                    }
                    merit = 200;
                    break;
                case (int)DebateWinType.DebateWinType_PushOn:
                    // 经验增加
                    exp = 30;
                    // 负伤
                    if (Utils.IsAlive(opponentPerson))
                    {
                        int injury = opponentPerson.GetInjury();
                        if (Utils.InRange(injury, 0, (int)Injury.Injury_Severe))
                        {
                            system.PersonSetInjury(opponentPerson, injury + 1);
                            injured = true;
                        }
                    }
                    merit = 200;
                    break;
            }
            if (Utils.IsAlive(person))
            {
                system.PersonAddStatExp(person, PersonStatType.PersonStatType_Intelligence, exp, true);
                system.PersonAddMerit(person, merit);
            }
            if (Utils.IsAlive(opponentPerson))
            {
                system.PersonAddStatExp(opponentPerson, PersonStatType.PersonStatType_Intelligence, 1, true);
                system.PersonAddMerit(opponentPerson, 10);
            }
            Message msg = new Message();
            if (Utils.IsAlive(opponentPerson) && injured)
            {
                if (view)
                {
                    if (person.IsPlayerControlled() && !opponentPerson.IsPlayerControlled())
                        system.PlaySe(10);
                    else
                        system.PlaySe(11);
                    msg.SetObj0(DebateMessageId.O_DEB_FATALBLOW_INJURED, opponentPerson);
                    system.Message(msg, null, new object[0], false);
                }
                if (opponentPerson.IsPlayerControlled())
                {
                    msg.SetObj0(DebateMessageId.LD_DEB_FATALBLOW_INJURED, opponentPerson);
                    system.HistoryLog(system.GetMessage(msg), opponentPerson, false);
                }
            }
            LogDebate($"【舌战结束】胜者：{GetTeamName(team)}，胜利方式：{GetWinTypeName(type)}");
            self.finished = true;
        }

        /// <summary>标记结束</summary>
        public void ParamSetFinished(Param self)
        {
            self.finished = true;
        }

        /// <summary>初始化参数（完整）</summary>
        public bool ParamInit(Param self, Person a, int aHp, bool aControl, Person b, int bHp, bool bControl, bool tutorial)
        {
            aHp = Utils.Clamp(aHp, 1, MaxHP);
            bHp = Utils.Clamp(bHp, 1, MaxHP);
            self.characters[0].person = a;
            self.characters[0].hp = aHp;
            self.characters[0].control = aControl;
            self.characters[1].person = b;
            self.characters[1].hp = bHp;
            self.characters[1].control = bControl;
            self.reverse = !aControl && bControl;
            self.winner = (int)DebateTeam.DebateTeam_Max;
            self.winType = (int)DebateWinType.DebateWinType_Normal;
            self.tutorial = tutorial;
            self.finished = false;
            return true;
        }

        /// <summary>参数是否已满</summary>
        public bool ParamSetIsFull(Param self)
        {
            for (int i = 0; i < MaxTeamCount; i++)
            {
                if (!Utils.IsActive(ParamGetPerson(self, i)))
                    return false;
            }
            return true;
        }

        /// <summary>初始化参数（按势力判断操作方）</summary>
        public bool ParamInit(Param self, Person a, int aHp, Person b, int bHp, bool tutorial)
        {
            Force aForce = system.GetForce(a.GetForceId());
            Force bForce = system.GetForce(b.GetForceId());
            bool aControl = Utils.IsActive(aForce) && aForce.IsPlayer;
            bool bControl = Utils.IsActive(bForce) && bForce.IsPlayer;
            return ParamInit(self, a, aHp, aControl, b, bHp, bControl, tutorial);
        }

        /// <summary>初始化参数（满体力）</summary>
        public bool ParamInit(Param self, Person a, Person b, bool tutorial)
        {
            return ParamInit(self, a, MaxHP, b, MaxHP, tutorial);
        }

        /// <summary>获取挑战方队伍（考虑反转）</summary>
        public int ParamGetChallenger(Param self)
        {
            return self.reverse ? (int)DebateTeam.DebateTeam_Challenged : (int)DebateTeam.DebateTeam_Challenger;
        }

        #endregion

        #region 卡牌 / 队伍工具

        /// <summary>获取对方队伍</summary>
        public static int GetOpponentTeam(int team)
        {
            return team == (int)DebateTeam.DebateTeam_Challenger
                ? (int)DebateTeam.DebateTeam_Challenged
                : (int)DebateTeam.DebateTeam_Challenger;
        }

        /// <summary>获取卡牌对应话题</summary>
        public static int GetCardTopic(int card)
        {
            switch (card)
            {
                case (int)DebateCard.DebateCard_Story1:
                case (int)DebateCard.DebateCard_Story2:
                case (int)DebateCard.DebateCard_Story3:
                    return (int)Topic.Topic_Story;
                case (int)DebateCard.DebateCard_Logic1:
                case (int)DebateCard.DebateCard_Logic2:
                case (int)DebateCard.DebateCard_Logic3:
                    return (int)Topic.Topic_Logic;
                case (int)DebateCard.DebateCard_Trend1:
                case (int)DebateCard.DebateCard_Trend2:
                case (int)DebateCard.DebateCard_Trend3:
                    return (int)Topic.Topic_Trend;
            }
            return -1;
        }

        /// <summary>获取卡牌等级（0~2）</summary>
        public static int GetCardLevel(int card)
        {
            switch (card)
            {
                case (int)DebateCard.DebateCard_Story1:
                case (int)DebateCard.DebateCard_Logic1:
                case (int)DebateCard.DebateCard_Trend1:
                    return 0;
                case (int)DebateCard.DebateCard_Story2:
                case (int)DebateCard.DebateCard_Logic2:
                case (int)DebateCard.DebateCard_Trend2:
                    return 1;
                case (int)DebateCard.DebateCard_Story3:
                case (int)DebateCard.DebateCard_Logic3:
                case (int)DebateCard.DebateCard_Trend3:
                    return 2;
            }
            return -1;
        }

        /// <summary>增减指定队伍体力</summary>
        public void AddHp(int team, int value)
        {
            CharacterAddHp(GetCharacter(team), value);
        }

        /// <summary>增减指定队伍愤怒</summary>
        public void AddStress(int team, int value)
        {
            CharacterAddStress(GetCharacter(team), value);
        }

        /// <summary>获取卡牌威力</summary>
        public static int GetCardPower(int topic, int card)
        {
            switch (card)
            {
                case (int)DebateCard.DebateCard_Ignore:
                    return 120;
                case (int)DebateCard.DebateCard_Shout:
                    return 110;
                case (int)DebateCard.DebateCard_Sophistry:
                    return 20;
            }
            if (Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
            {
                int n = 1 + GetCardLevel(card);
                if (Utils.InRange(topic, 0, (int)Topic.Topic_Max - 1) && GetCardTopic(card) == topic)
                    n += 10;
                return n;
            }
            return 0;
        }

        #endregion

        #region 出牌 / 判定

        /// <summary>卡牌当前是否可用</summary>
        public bool IsCardAvailable(int team, int card)
        {
            int opponentTeam = GetOpponentTeam(team);
            Character character = GetCharacter(team);
            Character opponentCharacter = GetCharacter(opponentTeam);
            int personality = CharacterGetPersonality(character);
            int opponentPersonality = CharacterGetPersonality(opponentCharacter);
            bool angered = false;
            bool opponentAngered = false;
            if (character.angerTimer > 0)
            {
                switch (personality)
                {
                    case (int)Personality.Personality_Calm:
                    case (int)Personality.Personality_Bold:
                        angered = true;
                        break;
                }
            }
            if (opponentCharacter.angerTimer > 0)
            {
                switch (opponentPersonality)
                {
                    case (int)Personality.Personality_Calm:
                    case (int)Personality.Personality_Bold:
                        opponentAngered = true;
                        break;
                }
            }
            if (!Utils.InRange(card, 0, (int)DebateCard.DebateCard_Max - 1))
                return false;
            switch (card)
            {
                case (int)DebateCard.DebateCard_Rethink:
                    if (angered && personality != (int)Personality.Personality_Calm)
                        return false;
                    return canRethink[team];
                case (int)DebateCard.DebateCard_Shout:
                case (int)DebateCard.DebateCard_Sophistry:
                case (int)DebateCard.DebateCard_Ignore:
                    if (angered && personality != (int)Personality.Personality_Calm)
                        return false;
                    if (opponentAngered && opponentPersonality == (int)Personality.Personality_Calm)
                        return false;
                    return true;
                case (int)DebateCard.DebateCard_Compose:
                case (int)DebateCard.DebateCard_Agitate:
                    if (angered)
                        return false;
                    if (opponentAngered && opponentPersonality == (int)Personality.Personality_Calm)
                        return false;
                    return true;
            }
            return true;
        }

        /// <summary>出牌</summary>
        public bool PlayCard(int team, int index)
        {
            Character character = GetCharacter(team);
            int card = CharacterGetCard(character, index);
            playedCard[team] = card;
            if (view && engine != null)
                engine.DebatePlayCard(this, team, index);
            CharacterRemoveCard(character, index);
            LogDebate($"【出牌】{GetTeamName(team)} 打出「{GetCardName(card)}」，剩余手牌：{HandText(team)}");
            return true;
        }

        /// <summary>获取已出的卡</summary>
        public int GetPlayedCard(int team)
        {
            return playedCard[team];
        }

        /// <summary>重置已出的卡</summary>
        public void ResetPlayedCard(int team)
        {
            playedCard[team] = -1;
        }

        /// <summary>一击必杀判定</summary>
        public bool Ftk()
        {
            if (winner == -1)
            {
                for (int i = 0; i < MaxTeamCount; i++)
                {
                    int opponentTeam = GetOpponentTeam(i);
                    int myInt = CharacterGetIntelligence(characters[i]);
                    int opponentInt = CharacterGetIntelligence(characters[opponentTeam]);
                    if (myInt > 80 && myInt > opponentInt + 10 && characters[i].attack + system.RandInt(200) >= 270)
                    {
                        winner = i;
                        break;
                    }
                }
            }
            if (winner != -1)
            {
                int opponentTeam = GetOpponentTeam(winner);
                CharacterSetHp(characters[opponentTeam], MinHP);
                winType = (int)DebateWinType.DebateWinType_Max;
                LogDebate($"【一击必杀】{GetTeamName(winner)} 瞬间击溃 {GetTeamName(opponentTeam)}");
                if (view && engine != null)
                    engine.DebateFtk(this);
                return true;
            }
            return false;
        }

        /// <summary>回合开始</summary>
        public void TurnStart()
        {
            for (int i = 0; i < MaxTeamCount; i++)
            {
                playedCard[i] = -1;

                // 立足点崩坏
                if (crumbled[i])
                    canRethink[i] = true;

                // 愤怒（冷静）
                if (characters[i].angerTimer > 0 && CharacterGetPersonality(characters[i]) == (int)Personality.Personality_Calm)
                    canRethink[i] = true;

                if (!IsTutorial())
                    CharacterFillCards(characters[i]);

                // 愤怒（大胆）
                if (characters[i].angerTimer > 0 && CharacterGetPersonality(characters[i]) == (int)Personality.Personality_Bold)
                {
                    int topicCardCount = 0;
                    for (int j = 0; j < characters[i].maxCardCount; j++)
                    {
                        int card = CharacterGetCard(characters[i], j);
                        if (Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
                            topicCardCount++;
                    }
                    // 若已无话题卡，则结束愤怒状态
                    if (topicCardCount == 0)
                        CharacterResetAngerTimer(characters[i]);
                }

                crumbled[i] = false;
            }
        }

        /// <summary>回合结束</summary>
        public void TurnEnd()
        {
            for (int i = 0; i < MaxTeamCount; i++)
            {
                if (characters[i].angerTimer > 0)
                {
                    CharacterDecAngerTimer(characters[i]);
                    if (characters[i].angerTimer == 0)
                    {
                        LogDebate($"【状态结束】{GetTeamName(i)} 的兴奋状态结束");
                        if (view && engine != null)
                            engine.DebateAngerEnd(this, i);
                    }
                }
            }

            if (Utils.InRange(attacker, 0, MaxTeamCount - 1))
            {
                first = attacker;
                int card = playedCard[attacker];
                if (Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
                {
                    int topic = GetCardTopic(card);
                    SetTopic(topic);
                    LogDebate($"【话题】转为「{system.GetTopicName(topic)}」");
                }

                if (!IsTutorial())
                {
                    bool changeTopic = false;
                    for (int i = 0; i < MaxTeamCount; i++)
                    {
                        if (playedCard[i] == (int)DebateCard.DebateCard_Sophistry && playedCard[GetOpponentTeam(i)] != (int)DebateCard.DebateCard_Ignore)
                            changeTopic = true;
                    }
                    if (changeTopic)
                    {
                        int topic = system.RandInt((int)Topic.Topic_Max);
                        SetTopic(topic);
                        LogDebate($"【话题】因诡辩扰乱，转为「{system.GetTopicName(topic)}」");
                    }
                }
            }
        }

        /// <summary>计算攻击方</summary>
        public int CalcAttacker()
        {
            int aPower = GetCardPower(topic, playedCard[0]);
            int bPower = GetCardPower(topic, playedCard[1]);
            if (characters[0].angerTimer > 0 && CharacterGetPersonality(characters[0]) == (int)Personality.Personality_Bold)
                aPower = 100;
            if (characters[1].angerTimer > 0 && CharacterGetPersonality(characters[1]) == (int)Personality.Personality_Bold)
                bPower = 100;
            attacker = -1;
            if (aPower > bPower)
                attacker = 0;
            else if (bPower > aPower)
                attacker = 1;
            return attacker;
        }

        /// <summary>体力伤害</summary>
        public int CalcHpDamage(int team, int card)
        {
            int topicCoef = 6;
            if (characters[team].angerTimer > 0)
            {
                switch (CharacterGetPersonality(characters[team]))
                {
                    case (int)Personality.Personality_Timid:
                    case (int)Personality.Personality_Bold:
                        topicCoef = 10;
                        break;
                }
            }
            else
            {
                if (card == (int)DebateCard.DebateCard_Shout)
                    topicCoef = 12;
                else if (GetCardTopic(card) == topic)
                    topicCoef = 10;
            }

            int levelCoef;
            if (card == (int)DebateCard.DebateCard_Shout)
            {
                levelCoef = 15;
            }
            else
            {
                int level = GetCardLevel(card);
                int[] LevelCoef = { 10, 15, 20 }; // 8b3380
                if (Utils.InRange(level, 0, 2))
                    levelCoef = LevelCoef[level];
                else
                    levelCoef = 0;
            }

            int angerCoef = 10;
            if (characters[team].angerTimer > 0 && CharacterGetPersonality(characters[team]) == (int)Personality.Personality_Calm)
                angerCoef = 15;

            int n = characters[team].attack + system.RandInt(5); // 70 .. 231
            n *= angerCoef;   // 10 .. 15
            n *= levelCoef;   // 10 .. 20
            n *= topicCoef;   // 6 .. 12
            n /= 1000;        // 42 .. 693（话题3，大胆愤怒）

            // 武将行为（数据驱动，见 DebatePersonBehaviours）：公式算完再覆盖
            Person self = characters[team].person;
            Person opponent = characters[GetOpponentTeam(team)].person;
            return DebatePersonBehaviours.Get(self).ModifyHpDamage(self, opponent, card, n);
        }

        /// <summary>愤怒伤害</summary>
        public int CalcStressDamage(int card)
        {
            if (card == (int)DebateCard.DebateCard_Shout)
                return 15;
            if (Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
            {
                int[] StressDamage = { 10, 15, 20 }; // 8b338c
                return StressDamage[GetCardLevel(card)];
            }
            return 0;
        }

        #endregion

        #region 伤害结算

        /// <summary>平局</summary>
        public void AttackDraw()
        {
            int stressDamage = 15;
            for (int i = 0; i < MaxTeamCount; i++)
                CharacterAddStress(characters[i], stressDamage);
            LogDebate($"【平局】双方势均力敌，{GetTeamName(0)} 与 {GetTeamName(1)} 愤怒各+{stressDamage} → {TeamState(0)} / {TeamState(1)}");
            if (view && engine != null)
                engine.DebateAttackDraw(this, stressDamage);
        }

        /// <summary>大喝</summary>
        public void Shout(int team, int hpDamage, int stressDamage)
        {
            int targetTeam = GetOpponentTeam(team);
            Character target = GetCharacter(targetTeam);
            CharacterAddHp(target, -hpDamage);
            CharacterAddStress(target, stressDamage);
            LogDebate($"【大喝】{GetTeamName(team)} 大喝，对 {GetTeamName(targetTeam)} 造成 体力-{hpDamage}、愤怒+{stressDamage} → {TeamState(targetTeam)}");
            if (view && engine != null)
                engine.DebateShout(this, team, hpDamage, stressDamage);
        }

        /// <summary>话题卡效果（原 C++ topic，因与枚举同名故重命名）</summary>
        public void TopicCard(int team, int card, int hpDamage, int stressDamage, bool reflected)
        {
            int targetTeam = reflected ? team : GetOpponentTeam(team);
            Character target = GetCharacter(targetTeam);
            CharacterAddHp(target, -hpDamage);
            CharacterAddStress(target, stressDamage);
            string desc = reflected
                ? $"{GetTeamName(GetOpponentTeam(team))} 用「诡辩」反弹 {GetTeamName(team)} 的话题「{GetCardName(card)}」，使其 体力-{hpDamage}、愤怒+{stressDamage}"
                : $"{GetTeamName(team)} 用「{GetCardName(card)}」对 {GetTeamName(targetTeam)} 造成 体力-{hpDamage}、愤怒+{stressDamage}";
            LogDebate($"【伤害】{desc} → {TeamState(targetTeam)}");
            if (view && engine != null)
                engine.DebateTopic(this, team, card, hpDamage, stressDamage, reflected);
        }

        /// <summary>再考</summary>
        public void Rethink(int team)
        {
            Character character = GetCharacter(team);
            CharacterRethink(character, topic);
            if (view && engine != null)
                engine.DebateRethink(this, team);
            canRethink[team] = false;
            LogDebate($"【再考】{GetTeamName(team)} 重抽手牌：{HandText(team)}");
        }

        /// <summary>无视</summary>
        public void Ignore(int team, int stressDamage)
        {
            int targetTeam = GetOpponentTeam(team);
            Character target = GetCharacter(targetTeam);
            CharacterAddStress(target, stressDamage);
            LogDebate($"【无视】{GetTeamName(team)} 无视对方，使 {GetTeamName(targetTeam)} 愤怒+{stressDamage} → {TeamState(targetTeam)}");
            if (view && engine != null)
                engine.DebateIgnore(this, team, stressDamage);
        }

        /// <summary>镇静</summary>
        public void Compose(int team, int stressDamage, bool reflected)
        {
            int targetTeam = reflected ? team : GetOpponentTeam(team);
            Character target = GetCharacter(targetTeam);
            CharacterAddStress(target, stressDamage);
            LogDebate($"【镇静】{GetTeamName(team)} 使用镇静，{GetTeamName(targetTeam)} 愤怒{stressDamage} → {TeamState(targetTeam)}");
            if (view && engine != null)
                engine.DebateCompose(this, team, stressDamage, reflected);
        }

        /// <summary>激昂</summary>
        public void Agitate(int team, int stressDamage, bool reflected)
        {
            int targetTeam = reflected ? GetOpponentTeam(team) : team;
            Character target = GetCharacter(targetTeam);
            CharacterAddStress(target, stressDamage);
            LogDebate($"【激昂】{GetTeamName(team)} 使用激昂，{GetTeamName(targetTeam)} 愤怒+{stressDamage} → {TeamState(targetTeam)}");
            if (view && engine != null)
                engine.DebateAgitate(this, team, stressDamage, reflected);
        }

        /// <summary>愤怒触发</summary>
        public void AngerTrigger(int team, int card)
        {
            Character character = GetCharacter(team);
            Character opponentCharacter = GetCharacter(GetOpponentTeam(team));
            switch (card)
            {
                case (int)DebateCard.DebateCard_Agitate:
                    CharacterAddStress(opponentCharacter, -MaxStress);
                    CharacterAddStress(character, -MaxStress / 2);
                    break;
                case (int)DebateCard.DebateCard_Compose:
                    CharacterAddStress(character, -MaxStress / 2);
                    break;
                default:
                    CharacterAddStress(character, -MaxStress);
                    break;
            }
            LogDebate($"【愤怒触发】{GetTeamName(team)} 愤怒值满；（对方反击牌：{(card >= 0 ? GetCardName(card) : "无")}）");
            if (view && engine != null)
                engine.DebateAngerTrigger(this, angering, card);
            if (card >= 0)
            {
                int index = CharacterGetCardIndex(opponentCharacter, card);
                CharacterRemoveCard(opponentCharacter, index);
            }
        }

        /// <summary>愤怒结算</summary>
        public void Anger()
        {
            if (!Utils.InRange(angering, 0, MaxTeamCount - 1))
                return;
            LogDebate($"【愤怒】{GetTeamName(angering)} 进入兴奋状态");
            Character character = GetCharacter(angering);
            Character opponentCharacter = GetCharacter(GetOpponentTeam(angering));
            CharacterSetAngerTimer(character);
            switch (CharacterGetPersonality(character))
            {
                case (int)Personality.Personality_Timid:
                    comboAttacker = angering;
                    comboCounter = 0;
                    combo = true;
                    LogDebate($"【胆怯反击】{GetTeamName(angering)} 因胆怯开始连续攻击");
                    break;
                case (int)Personality.Personality_Reckless:
                    int hpDamage = 200 + CharacterGetStrength(character);
                    int stressDamage = hpDamage / 15;
                    CharacterAddHp(opponentCharacter, -hpDamage);
                    CharacterAddStress(opponentCharacter, stressDamage);
                    LogDebate($"【猪突猛进】{GetTeamName(angering)} 怒而猛攻，对 {GetTeamName(GetOpponentTeam(angering))} 造成 体力-{hpDamage}、愤怒+{stressDamage} → {TeamState(GetOpponentTeam(angering))}");
                    if (view && engine != null)
                        engine.DebateAngerReckless(this, angering, hpDamage, stressDamage);
                    break;
            }
        }

        /// <summary>连续攻击（小心性格）</summary>
        public void ComboAttack()
        {
            int team = comboAttacker;
            int opponentTeam = GetOpponentTeam(team);
            for (int i = 0; i < characters[team].maxCardCount; i++)
            {
                int card = CharacterGetCard(characters[team], i);
                if (!Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
                    continue;
                int hpDamage = CalcHpDamage(team, card);
                int stressDamage = hpDamage / 15;
                CharacterAddHp(characters[opponentTeam], -hpDamage);
                CharacterAddStress(characters[opponentTeam], stressDamage);
                LogDebate($"【连续攻击】{GetTeamName(team)} 第{comboCounter + 1}击「{GetCardName(card)}」对 {GetTeamName(opponentTeam)} 造成 体力-{hpDamage}、愤怒+{stressDamage} → {TeamState(opponentTeam)}");
                if (view && engine != null)
                    engine.DebateAngerTimid(this, team, hpDamage, stressDamage, comboCounter);
                CharacterRemoveCard(characters[team], i);
                comboCounter++;
            }
            combo = false;
        }

        /// <summary>判定胜负</summary>
        public bool CalcWinner()
        {
            if (winner == -1)
            {
                int team = first;
                int opponentTeam = GetOpponentTeam(team);
                if (characters[opponentTeam].hp <= 0)
                    winner = team;
                else if (characters[team].hp <= 0)
                    winner = opponentTeam;
            }
            if (winner != -1)
            {
                return true;
            }
            return false;
        }

        /// <summary>是否可发动会心</summary>
        public bool CanCritical()
        {
            if (winner == -1)
                return false;
            if (system.IsFeatDisabled(Feature.Feature_DebateCritical))
                return false;
            return characters[GetOpponentTeam(winner)].hp <= -100;
        }

        /// <summary>计算胜利方式</summary>
        public void CalcWinType()
        {
            switch (critical)
            {
                case (int)DebateCritical.DebateCritical_HaveMercy:
                    winType = (int)DebateWinType.DebateWinType_HaveMercy;
                    break;
                case (int)DebateCritical.DebateCritical_PushOn:
                    winType = (int)DebateWinType.DebateWinType_PushOn;
                    break;
            }
        }

        /// <summary>攻击结算</summary>
        public void Attack()
        {
            if (!Utils.InRange(attacker, 0, MaxTeamCount - 1))
            {
                AttackDraw();
                return;
            }
            int team = attacker;
            int opponentTeam = GetOpponentTeam(team);
            int card = playedCard[team];
            LogDebate($"【对阵】{GetTeamName(0)} 出「{GetCardName(playedCard[0])}」 VS {GetTeamName(1)} 出「{GetCardName(playedCard[1])}」；话题「{system.GetTopicName(topic)}」");
            switch (card)
            {
                case (int)DebateCard.DebateCard_Ignore:
                    Ignore(team, 30);
                    break;
                case (int)DebateCard.DebateCard_Sophistry:
                    card = playedCard[opponentTeam];
                    if (Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
                    {
                        int hpDamage = CalcHpDamage(team, card);
                        int stressDamage = CalcStressDamage(card);
                        if (hpDamage > 0)
                            TopicCard(opponentTeam, card, hpDamage, stressDamage, true);
                    }
                    break;
                case (int)DebateCard.DebateCard_Shout:
                    Shout(team, CalcHpDamage(team, card), CalcStressDamage(card));
                    break;
                default:
                    TopicCard(team, card, CalcHpDamage(team, card), CalcStressDamage(card), false);
                    break;
            }
        }

        /// <summary>卡牌附加效果</summary>
        public void Effect(int team, int card)
        {
            int opponentTeam = GetOpponentTeam(team);
            int stressDamage;
            bool reflected;
            switch (card)
            {
                case (int)DebateCard.DebateCard_Rethink:
                    Rethink(team);
                    break;
                case (int)DebateCard.DebateCard_Compose:
                    stressDamage = -System.Math.Max(GetCharacter(opponentTeam).stress / 2, 30);
                    reflected = playedCard[opponentTeam] == (int)DebateCard.DebateCard_Sophistry;
                    Compose(team, stressDamage, reflected);
                    break;
                case (int)DebateCard.DebateCard_Agitate:
                    stressDamage = 40;
                    reflected = playedCard[opponentTeam] == (int)DebateCard.DebateCard_Sophistry;
                    Agitate(team, stressDamage, reflected);
                    break;
            }
        }

        /// <summary>计算愤怒方</summary>
        public bool CalcAngering(bool canReflect)
        {
            int team = first;
            int opponentTeam = GetOpponentTeam(team);
            int card = -1;

            angering = -1;
            if (characters[team].angerTimer <= 0 && characters[team].stress >= MaxStress)
                angering = team;
            if (angering == -1)
            {
                if (characters[opponentTeam].angerTimer <= 0 && characters[opponentTeam].stress >= MaxStress)
                    angering = opponentTeam;
            }
            // 无人愤怒
            if (!Utils.InRange(angering, 0, MaxTeamCount - 1))
                return angering != -1;

            team = angering;
            opponentTeam = GetOpponentTeam(team);

            if (characters[opponentTeam].angerTimer <= 0)
            {
                if (canReflect && CharacterGetCardIndex(characters[opponentTeam], (int)DebateCard.DebateCard_Agitate) > 0)
                    card = (int)DebateCard.DebateCard_Agitate;
                else if (CharacterGetCardIndex(characters[opponentTeam], (int)DebateCard.DebateCard_Compose) > 0)
                    card = (int)DebateCard.DebateCard_Compose;
            }
            AngerTrigger(angering, card);
            if (card == (int)DebateCard.DebateCard_Agitate)
                angering = opponentTeam;
            else if (card == (int)DebateCard.DebateCard_Compose)
                angering = -1;
            return angering != -1;
        }

        /// <summary>计算崩坏程度</summary>
        public bool CalcCrumbled()
        {
            for (int i = 0; i < MaxTeamCount; i++)
            {
                int level = (MaxHP - characters[i].hp) / 250;
                level = Utils.Clamp(level, 0, 3);
                if (level == crumbledLevel[i])
                    continue;
                crumbledLevel[i] = level;
                crumbled[i] = true;
            }
            return crumbled[0] || crumbled[1];
        }

        /// <summary>初始化</summary>
        public virtual bool Init()
        {
            // 5201c0
            topic = system.RandInt((int)Topic.Topic_Max);
            first = -1;
            critical = -1;
            winner = -1;
            winType = (int)DebateWinType.DebateWinType_Normal;
            Person aPerson = ParamGetPerson(param, 0);
            bool aControl = ParamGetControl(param, 0);
            int aHp = ParamGetHp(param, 0);
            Person bPerson = ParamGetPerson(param, 1);
            bool bControl = ParamGetControl(param, 1);
            int bHp = ParamGetHp(param, 1);
            if (!CharacterInit(characters[0], aPerson, bPerson, aHp, aControl))
                return false;
            if (!CharacterInit(characters[1], bPerson, aPerson, bHp, bControl))
                return false;
            for (int i = 0; i < MaxTeamCount; i++)
            {
                AiInit(aiContext[i], this, i);
                crumbledLevel[i] = 0;
                crumbled[i] = false;
                canRethink[i] = true;
            }
            first = ParamGetChallenger(param);

            LogDebate($"【舌战开始】{aPerson.GetName()}(体力{aHp}) VS {bPerson.GetName()}(体力{bHp})，当前话题「{system.GetTopicName(topic)}」");
            nextPhase = (int)DebatePhase.DebatePhase_Opening;
            return true;
        }

        /// <summary>卡牌附加效果（按攻击方先结算）</summary>
        public void Effect()
        {
            if (!Utils.InRange(attacker, 0, MaxTeamCount - 1))
                return;
            int team = attacker;
            int card = playedCard[team];
            int opponentTeam = GetOpponentTeam(team);
            int opponentCard = playedCard[opponentTeam];
            Effect(team, card);
            if (card == (int)DebateCard.DebateCard_Ignore)
                return;
            Effect(opponentTeam, opponentCard);
        }

        /// <summary>运行舌战主循环</summary>
        public bool Run()
        {
            bool useView = false;
            if (param.characters[0].control || param.characters[1].control)
            {
                Message msg = new Message();
                msg.SetObj0Obj1(DebateMessageId.N_DEB_DEBATE_CONFIRM, param.characters[0].person, param.characters[1].person);
                useView = engine != null && engine.YesNo(system.GetMessage(msg));
                if (!useView)
                    engine = null;
            }
            Init();
            while (Update(0))
            {
            }
            return useView;
        }

        /// <summary>是否教程</summary>
        public virtual bool IsTutorial()
        {
            return false;
        }

        #endregion

        #region 内部工具

        /// <summary>卡牌中文名称表，下标与 DebateCard 枚举值一致</summary>
        private static readonly string[] s_cardNames = new string[]
        {
            "再考",    // 0  DebateCard_Rethink
            "故事·小", // 1  DebateCard_Story1
            "故事·中", // 2  DebateCard_Story2
            "故事·大", // 3  DebateCard_Story3
            "道理·小", // 4  DebateCard_Logic1
            "道理·中", // 5  DebateCard_Logic2
            "道理·大", // 6  DebateCard_Logic3
            "时势·小", // 7  DebateCard_Trend1
            "时势·中", // 8  DebateCard_Trend2
            "时势·大", // 9  DebateCard_Trend3
            "大喝",    // 10 DebateCard_Shout
            "诡辩",    // 11 DebateCard_Sophistry
            "无视",    // 12 DebateCard_Ignore
            "镇静",    // 13 DebateCard_Compose
            "激昂",    // 14 DebateCard_Agitate
        };

        /// <summary>获取卡牌中文名称（用于日志）</summary>
        public static string GetCardName(int card)
        {
            if (!Utils.InRange(card, 0, s_cardNames.Length - 1))
                return "无";
            return s_cardNames[card];
        }

        /// <summary>获取胜利方式名称（用于日志）</summary>
        public static string GetWinTypeName(int winType)
        {
            switch (winType)
            {
                case (int)DebateWinType.DebateWinType_PushOn: return "追击";
                case (int)DebateWinType.DebateWinType_HaveMercy: return "留情";
                case (int)DebateWinType.DebateWinType_Normal: return "普通";
            }
            return "一击必杀";
        }

        /// <summary>获取队伍名称（武将名，用于日志）</summary>
        public string GetTeamName(int team)
        {
            if (!Utils.InRange(team, 0, MaxTeamCount - 1))
                return "未知方";
            Person person = characters[team].person;
            if (person == null)
                return $"队伍{team}";
            string name = person.GetName();
            return string.IsNullOrEmpty(name) ? $"队伍{team}" : name;
        }

        /// <summary>格式化队伍当前状态（体力/愤怒），用于日志</summary>
        private string TeamState(int team)
        {
            Character c = GetCharacter(team);
            return $"{GetTeamName(team)}(体力{c.hp}/愤怒{c.stress})";
        }

        /// <summary>格式化队伍手牌，用于日志</summary>
        private string HandText(int team)
        {
            Character c = GetCharacter(team);
            List<string> list = new List<string>();
            for (int i = 0; i < c.maxCardCount; i++)
            {
                if (c.card[i] >= 0)
                    list.Add(GetCardName(c.card[i]));
            }
            return list.Count > 0 ? string.Join("、", list) : "（空）";
        }

        /// <summary>输出舌战过程日志（统一走 Sango.Log）</summary>
        private void LogDebate(string text)
        {
            Logger logger = system.GetLogger();
            if (logger != null)
                logger.Debug(text);
        }

        /// <summary>创建参战武将运行时数组</summary>
        internal static Character[] NewCharacterArray()
        {
            Character[] array = new Character[MaxTeamCount];
            for (int i = 0; i < array.Length; i++)
                array[i] = new Character();
            return array;
        }

        /// <summary>创建 AI 上下文数组</summary>
        internal static AI[] NewAIArray()
        {
            AI[] array = new AI[MaxTeamCount];
            for (int i = 0; i < array.Length; i++)
                array[i] = new AI();
            return array;
        }

        /// <summary>创建指定长度、全部填充为 value 的数组</summary>
        internal static int[] NewFilledArray(int length, int value)
        {
            int[] array = new int[length];
            for (int i = 0; i < length; i++)
                array[i] = value;
            return array;
        }

        #endregion
    }
}
