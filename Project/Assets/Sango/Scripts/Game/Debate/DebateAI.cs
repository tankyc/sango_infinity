/*
 * 文件名：DebateAI.cs
 * 描述：舌战(Debate)AI 决策模块，由 s11_sys_debate_ai.cpp 翻译而来
 *
 * 翻译说明：
 *   1. C++ 中 AI::Row 通过成员函数指针 (&Debate::xxx) 组织决策表；C# 无成员函数指针，
 *      改为记录决策函数 id（DebateAIRow 枚举），由 AiInvokeRow 统一分发，语义完全等价。
 *   2. 决策表按【对手出的牌】分三种情形：①对手出的是当前话题卡；②对手出了其它牌；
 *      ③己方先手（对手未出牌）；另有一个"低智力"兜底表。
 *   3. std::numeric_limits<int>::max()/min() 对应 int.MaxValue / int.MinValue。
 */

using System;

namespace Sango.Core.Debate
{
    public partial class Debate
    {
        #region 静态决策数据（对应匿名命名空间中的常量表）

        // 8b2568, 8b2574
        private static readonly int[] Topic3Card = new int[]
        {
            (int)DebateCard.DebateCard_Story3, (int)DebateCard.DebateCard_Logic3, (int)DebateCard.DebateCard_Trend3,
        };

        // 8B25a4：由大到小（高阶优先）
        private static readonly int[][] TopicCardDesc = new int[][]
        {
            new int[] { (int)DebateCard.DebateCard_Story3, (int)DebateCard.DebateCard_Story2, (int)DebateCard.DebateCard_Story1 },
            new int[] { (int)DebateCard.DebateCard_Logic3, (int)DebateCard.DebateCard_Logic2, (int)DebateCard.DebateCard_Logic1 },
            new int[] { (int)DebateCard.DebateCard_Trend3, (int)DebateCard.DebateCard_Trend2, (int)DebateCard.DebateCard_Trend1 },
        };

        // 8b2580, 8b25c8, 8b25ec：由小到大（低阶优先）
        private static readonly int[][] TopicCardAsc = new int[][]
        {
            new int[] { (int)DebateCard.DebateCard_Story1, (int)DebateCard.DebateCard_Story2, (int)DebateCard.DebateCard_Story3 },
            new int[] { (int)DebateCard.DebateCard_Logic1, (int)DebateCard.DebateCard_Logic2, (int)DebateCard.DebateCard_Logic3 },
            new int[] { (int)DebateCard.DebateCard_Trend1, (int)DebateCard.DebateCard_Trend2, (int)DebateCard.DebateCard_Trend3 },
        };

        /// <summary>把话题索引限制在合法范围内，避免 C++ 中的越界下标</summary>
        private static int TopicIndex(int topic)
        {
            return Utils.Clamp(topic, 0, (int)Topic.Topic_Max - 1);
        }

        #endregion

        #region 决策表分发

        /// <summary>
        /// 决策表函数分发。对应 C++ 的 (this->*func)(self, param1, param2)。
        /// </summary>
        private int AiInvokeRow(AI self, int id, int param1, int param2)
        {
            switch (id)
            {
                case (int)DebateAIRow.DebateAIRow_CondCardAngerBold: return AiCondCardAngerBold(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_CondCardAngerCalmRethink: return AiCondCardAngerCalmRethink(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_CondCardVsAngerBold: return AiCondCardVsAngerBold(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_CondCardVsAngerCalm: return AiCondCardVsAngerCalm(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_CondCardAngerCalm: return AiCondCardAngerCalm(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_CondCardShoutSophistryIgnore: return AiCondCardShoutSophistryIgnore(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_CondCardLowTopic: return AiCondCardLowTopic(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_FindCardHighTopic: return AiFindCardHighTopic(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_FindCardLowTopic: return AiFindCardLowTopic(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_CondCardAgitate: return AiCondCardAgitate(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_CondCardCompose: return AiCondCardCompose(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_CondCardRethink: return AiCondCardRethink(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_FindCardRethink: return AiFindCardRethink(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_CondCardIgnore: return AiCondCardIgnore(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_FindCardHighAnyTopic: return AiFindCardHighAnyTopic(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_RandCardShoutSophistryIgnoreTopic: return AiRandCardShoutSophistryIgnoreTopic(self, param1, param2);
                case (int)DebateAIRow.DebateAIRow_RandCard: return AiRandCard(self, param1, param2);
            }
            return -1;
        }

        #endregion

        #region AI 初始化 / 会心

        /// <summary>516fc0</summary>
        public void AiInit(AI self, Debate parent, int team)
        {
            self.parent = parent;
            self.team = team;
            self.opponentTeam = GetOpponentTeam(team);
        }

        /// <summary>516fe0</summary>
        public int AiCalcCritical(AI self)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            Person person = character.person;
            Person opponentPerson = opponentCharacter.person;
            if (!Utils.IsAlive(person))
                return system.RandInt((int)DebateCritical.DebateCritical_Max);
            if (person.IsHate(opponentPerson))
                return (int)DebateCritical.DebateCritical_PushOn;
            if (person.IsLike(opponentPerson))
                return (int)DebateCritical.DebateCritical_HaveMercy;
            return system.RandInt((int)DebateCritical.DebateCritical_Max);
        }

        /// <summary>517060</summary>
        public int AiGetCardCount(AI self, int card)
        {
            if (!IsCardAvailable(self.team, card))
                return 0;
            Character character = GetCharacter(self.team);
            int n = 0;
            for (int i = 0; i < character.maxCardCount; i++)
            {
                if (CharacterGetCard(character, i) == card)
                    n++;
            }
            return n;
        }

        #endregion

        #region 条件 / 查找函数

        /// <summary>5170c0。大胆（兴奋）时优先出最低阶话题卡</summary>
        public int AiCondCardAngerBold(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);

            if (character.angerTimer <= 0)
                return -1;
            if (CharacterGetPersonality(character) != (int)Personality.Personality_Bold)
                return -1;

            int best = -1;
            int bestLevel = int.MaxValue;
            for (int i = 0; i < character.maxCardCount; i++)
            {
                int card = CharacterGetCard(character, i);
                if (!Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
                    continue;
                int level = GetCardLevel(card);
                if (level < bestLevel || (bestLevel == level && system.RandBool(50)))
                {
                    bestLevel = level;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>517190。冷静（兴奋）时再考</summary>
        public int AiCondCardAngerCalmRethink(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;

            if (character.angerTimer <= 0)
                return -1;
            if (CharacterGetPersonality(character) != (int)Personality.Personality_Calm)
                return -1;

            int n = 0;
            n += AiGetCardCount(self, Topic3Card[TopicIndex(self.parent.topic)]);
            n += AiGetCardCount(self, (int)DebateCard.DebateCard_Ignore);
            n += AiGetCardCount(self, (int)DebateCard.DebateCard_Sophistry);
            n += AiGetCardCount(self, (int)DebateCard.DebateCard_Shout);
            if (n == 0)
            {
                cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Rethink);
                if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Rethink))
                    return cardIndex;
            }
            if (system.RandBool(30))
            {
                cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Rethink);
                if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Rethink))
                    return cardIndex;
            }
            return -1;
        }

        /// <summary>517270。对手大胆（兴奋）</summary>
        public int AiCondCardVsAngerBold(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;
            int composeCardCount;
            int agitateCardCount;

            if (opponentCharacter.angerTimer <= 0)
                return -1;
            if (CharacterGetPersonality(opponentCharacter) != (int)Personality.Personality_Bold)
                return -1;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Shout);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Shout))
                return cardIndex;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Ignore);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Ignore))
                return cardIndex;

            composeCardCount = AiGetCardCount(self, (int)DebateCard.DebateCard_Compose);
            agitateCardCount = AiGetCardCount(self, (int)DebateCard.DebateCard_Agitate);
            // 注：原反编译此处为 > 比较（原注释标注 s11_suspicious，疑似应为 >=），此处保持原样
            if (composeCardCount > 1 && agitateCardCount > 1)
            {
                cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Agitate);
                if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Agitate))
                    return cardIndex;
            }
            else if (composeCardCount > 2 && agitateCardCount == 0)
            {
                cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Compose);
                if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Compose))
                    return cardIndex;
            }

            int best = -1;
            int bestLevel = int.MaxValue;
            for (int i = 0; i < character.maxCardCount; i++)
            {
                int card = CharacterGetCard(character, i);
                if (!Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
                    continue;
                if (!IsCardAvailable(self.team, card))
                    continue;
                int level = GetCardLevel(card);
                if (level < bestLevel || (bestLevel == level && system.RandBool(50)))
                {
                    bestLevel = level;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>5173f0。对手冷静（兴奋）</summary>
        public int AiCondCardVsAngerCalm(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;

            if (opponentCharacter.angerTimer <= 0)
                return -1;
            if (CharacterGetPersonality(opponentCharacter) != (int)Personality.Personality_Calm)
                return -1;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Ignore);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Ignore))
                return cardIndex;
            return -1;
        }

        /// <summary>517450。己方冷静（兴奋）</summary>
        public int AiCondCardAngerCalm(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;

            if (character.angerTimer <= 0)
                return -1;
            if (CharacterGetPersonality(character) != (int)Personality.Personality_Calm)
                return -1;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Shout);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Shout))
                return cardIndex;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Sophistry);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Sophistry))
                return cardIndex;

            int topic3 = Topic3Card[TopicIndex(self.parent.topic)];
            cardIndex = CharacterGetCardIndex(character, topic3);
            if (cardIndex >= 0 && IsCardAvailable(self.team, topic3))
                return cardIndex;
            return -1;
        }

        /// <summary>517520。大喝 / 诡辩 / 无视</summary>
        public int AiCondCardShoutSophistryIgnore(AI self, int type, int chance)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;
            int opponentPlayedCard;

            switch (type)
            {
                case 0:
                    // 概率
                    if (!system.RandBool(chance))
                        return -1;
                    break;
                case 1:
                    // 仅性格为小心时
                    if (CharacterGetPersonality(character) != (int)Personality.Personality_Timid)
                        return -1;
                    break;
                case 2:
                    // 先手且对手出话术卡时选择无视
                    opponentPlayedCard = GetPlayedCard(self.opponentTeam);
                    if (!Utils.InRange(opponentPlayedCard, 0, (int)DebateCard.DebateCard_Max - 1))
                        return -1;
                    if (Utils.InRange(opponentPlayedCard, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
                        return -1;
                    cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Ignore);
                    if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Ignore))
                        return cardIndex;
                    break;
            }

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Shout);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Shout))
                return cardIndex;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Sophistry);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Sophistry))
                return cardIndex;

            if (type == 2)
                return -1;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Ignore);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Ignore))
                return cardIndex;
            return -1;
        }

        /// <summary>517650。低阶话题卡</summary>
        public int AiCondCardLowTopic(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;
            int opponentTopicCardCount = 0;

            // 对手持有符合当前话题的卡
            for (int i = 0; i < opponentCharacter.maxCardCount; i++)
            {
                int card = CharacterGetCard(opponentCharacter, i);
                if (Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast) && GetCardTopic(card) == self.parent.topic)
                    opponentTopicCardCount++;
            }
            if (opponentTopicCardCount != 0)
                return -1;

            int topic = TopicIndex(self.parent.topic);
            for (int i = 0; i < 3; i++)
            {
                cardIndex = CharacterGetCardIndex(character, TopicCardAsc[topic][i]);
                if (cardIndex >= 0 && IsCardAvailable(self.team, TopicCardAsc[topic][i]))
                    return cardIndex;
            }
            return -1;
        }

        /// <summary>517720。寻找高阶话题卡</summary>
        public int AiFindCardHighTopic(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;

            int topic = TopicIndex(self.parent.topic);
            for (int i = 0; i < 3; i++)
            {
                cardIndex = CharacterGetCardIndex(character, TopicCardDesc[topic][i]);
                if (cardIndex >= 0 && IsCardAvailable(self.team, TopicCardDesc[topic][i]))
                    return cardIndex;
            }
            return -1;
        }

        /// <summary>5177b0。寻找低阶话题卡</summary>
        public int AiFindCardLowTopic(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;

            int topic = TopicIndex(self.parent.topic);
            for (int i = 0; i < 3; i++)
            {
                cardIndex = CharacterGetCardIndex(character, TopicCardAsc[topic][i]);
                if (cardIndex >= 0 && IsCardAvailable(self.team, TopicCardAsc[topic][i]))
                    return cardIndex;
            }
            return -1;
        }

        /// <summary>517840。激昂</summary>
        public int AiCondCardAgitate(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;
            int chance;

            // 根据愤怒值决定概率
            if (character.stress >= 70)
                chance = 100;
            else if (character.stress >= 60)
                chance = 40;
            else if (character.stress >= 50)
                chance = 10;
            else
                chance = 0;
            if (!system.RandBool(chance))
                return -1;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Agitate);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Agitate))
                return cardIndex;
            return -1;
        }

        /// <summary>5178c0。镇静</summary>
        public int AiCondCardCompose(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;
            int composeCardCount;
            int agitateCardCount;

            composeCardCount = AiGetCardCount(self, (int)DebateCard.DebateCard_Compose);
            agitateCardCount = AiGetCardCount(self, (int)DebateCard.DebateCard_Agitate);
            // 无激昂卡且镇静卡不足两张
            if (agitateCardCount == 0 && composeCardCount <= 1)
                return -1;
            // 体力不足 200
            if (character.hp < 200)
                return -1;
            // 50%
            if (!system.RandBool(50))
                return -1;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Compose);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Compose))
                return cardIndex;
            return -1;
        }

        /// <summary>517950。再考</summary>
        public int AiCondCardRethink(AI self, int ignoreOpponent, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;
            int topicCardCount = 0;
            int opponentTopicCardCount = 0;
            int shoutCardCount;
            int sophistryCardCount;
            int ignoreCardCount;
            int chance;

            for (int i = 0; i < character.maxCardCount; i++)
            {
                int card = CharacterGetCard(character, i);
                if (Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast) && GetCardTopic(card) == self.parent.topic)
                    topicCardCount++;
            }
            for (int i = 0; i < opponentCharacter.maxCardCount; i++)
            {
                int card = CharacterGetCard(opponentCharacter, i);
                if (Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast) && GetCardTopic(card) == self.parent.topic)
                    opponentTopicCardCount++;
            }

            // 自己已有话题卡
            if (topicCardCount > 0)
                return -1;
            // 对手无话题卡
            if (ignoreOpponent == 0 && opponentTopicCardCount == 0)
                return -1;

            shoutCardCount = AiGetCardCount(self, (int)DebateCard.DebateCard_Shout);
            sophistryCardCount = AiGetCardCount(self, (int)DebateCard.DebateCard_Sophistry);
            ignoreCardCount = AiGetCardCount(self, (int)DebateCard.DebateCard_Ignore);
            chance = ignoreOpponent != 0 ? 100 : 70;
            // 持有话术卡则概率减半
            if (shoutCardCount + sophistryCardCount + ignoreCardCount != 0)
                chance /= 2;
            if (!system.RandBool(chance))
                return -1;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Rethink);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Rethink))
                return cardIndex;
            return -1;
        }

        /// <summary>517b70。寻找再考</summary>
        public int AiFindCardRethink(AI self, int chance, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;

            if (!system.RandBool(chance))
                return -1;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Rethink);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Rethink))
                return cardIndex;
            return -1;
        }

        /// <summary>517bd0。无视</summary>
        public int AiCondCardIgnore(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;

            // 对手愤怒不足 70
            if (opponentCharacter.stress < 70)
                return -1;
            // 自己有激昂卡
            if (AiGetCardCount(self, (int)DebateCard.DebateCard_Agitate) == 0)
                return -1;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Ignore);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Ignore))
                return cardIndex;
            return -1;
        }

        /// <summary>517c30。寻找任意高阶话题卡</summary>
        public int AiFindCardHighAnyTopic(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);

            int best = -1;
            int bestLevel = int.MinValue;
            for (int i = 0; i < character.maxCardCount; i++)
            {
                int card = CharacterGetCard(character, i);
                if (!Utils.InRange(card, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast))
                    continue;
                if (!IsCardAvailable(self.team, card))
                    continue;
                int level = GetCardLevel(card);
                if (level > bestLevel || (bestLevel == level && system.RandBool(50)))
                {
                    bestLevel = level;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>517ce0。随机大喝 / 诡辩 / 无视 / 话题卡</summary>
        public int AiRandCardShoutSophistryIgnoreTopic(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int cardIndex;
            int[] table = new int[MaxCardCount];
            int count = 0;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Shout);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Shout))
                table[count++] = cardIndex;

            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Sophistry);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Sophistry))
                table[count++] = cardIndex;

            // 原 C++ 此处被 s11_suspicious(1) 标记（疑似应为 Sophistry），默认分支使用 Ignore
            cardIndex = CharacterGetCardIndex(character, (int)DebateCard.DebateCard_Ignore);
            if (cardIndex >= 0 && IsCardAvailable(self.team, (int)DebateCard.DebateCard_Ignore))
                table[count++] = cardIndex;

            int topic = TopicIndex(self.parent.topic);
            for (int i = 0; i < 3; i++)
            {
                cardIndex = CharacterGetCardIndex(character, TopicCardAsc[topic][i]);
                if (cardIndex >= 0 && IsCardAvailable(self.team, TopicCardAsc[topic][i]))
                    table[count++] = cardIndex;
            }

            if (count == 0)
                return -1;
            if (count > 2)
            {
                for (int i = 0; i < 1000; i++)
                {
                    int a = system.RandInt(count);
                    int b = system.RandInt(count);
                    if (a != b)
                        Utils.Swap(ref table[a], ref table[b]);
                }
            }
            return table[0];
        }

        /// <summary>517e50。随机出牌</summary>
        public int AiRandCard(AI self, int param1, int param2)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int[] table = new int[MaxCardCount];
            int count = 0;

            for (int i = 0; i < character.maxCardCount; i++)
            {
                int card = CharacterGetCard(character, i);
                if (!Utils.InRange(card, 0, (int)DebateCard.DebateCard_Max - 1))
                    continue;
                if (!IsCardAvailable(self.team, card))
                    continue;
                table[count++] = i;
            }

            if (count > 2)
            {
                for (int i = 0; i < 1000; i++)
                {
                    int a = system.RandInt(count);
                    int b = system.RandInt(count);
                    if (a != b)
                        Utils.Swap(ref table[a], ref table[b]);
                }
            }
            return table[0];
        }

        #endregion

        #region 主决策入口

        /// <summary>517ef0。计算要出的卡牌</summary>
        public int AiCalcCard(AI self)
        {
            Character character = GetCharacter(self.team);
            Character opponentCharacter = GetCharacter(self.opponentTeam);
            int opponentPlayedCard;
            int opponentCardTopic = -1;
            int cardIndex;
            AI.Row[] table;
            int count;

            opponentPlayedCard = GetPlayedCard(self.opponentTeam);
            if (Utils.InRange(opponentPlayedCard, 0, (int)DebateCard.DebateCard_Max - 1))
                opponentCardTopic = GetCardTopic(opponentPlayedCard);

            // 对手出的是当前话题卡
            if (Utils.InRange(opponentPlayedCard, (int)DebateCard.DebateCard_TopicFirst, (int)DebateCard.DebateCard_TopicLast)
                && opponentCardTopic == self.parent.topic)
            {
                // 8b2368
                table = new AI.Row[] {
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardAngerBold, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardAngerCalmRethink, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardVsAngerBold, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardVsAngerCalm, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardShoutSophistryIgnore, 0, 60),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardRethink, 1, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_FindCardHighTopic, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_FindCardRethink, 30, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardShoutSophistryIgnore, 0, 100),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardCompose, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_FindCardHighAnyTopic, 0, 0),
                };
                count = table.Length;
            }
            // 对手出话术卡或其它话题卡
            else if (Utils.InRange(opponentPlayedCard, 0, (int)DebateCard.DebateCard_Max - 1) && opponentCardTopic != self.parent.topic)
            {
                // 8b23f0
                table = new AI.Row[] {
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardAngerBold, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardAngerCalmRethink, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardVsAngerBold, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardVsAngerCalm, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardShoutSophistryIgnore, 2, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardShoutSophistryIgnore, 0, 30),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardRethink, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_FindCardLowTopic, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_FindCardRethink, 30, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardShoutSophistryIgnore, 0, 100),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardCompose, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_FindCardHighAnyTopic, 0, 0),
                };
                count = table.Length;
            }
            // 先手
            else
            {
                // 8b2480
                table = new AI.Row[] {
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardAngerBold, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardAngerCalmRethink, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardVsAngerBold, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardVsAngerCalm, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardLowTopic, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardAngerCalm, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardRethink, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardIgnore, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardShoutSophistryIgnore, 1, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardShoutSophistryIgnore, 0, 40),
                    new AI.Row((int)DebateAIRow.DebateAIRow_FindCardHighTopic, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardShoutSophistryIgnore, 0, 100),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardAgitate, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_FindCardRethink, 30, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_CondCardCompose, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_FindCardHighAnyTopic, 0, 0),
                };
                count = table.Length;
            }

            // 智力不足 60 时使用简化表
            int myInt = CharacterGetIntelligence(character);
            int chance = System.Math.Max(60 - myInt, 0) / 2;
            if (system.RandBool(chance))
            {
                // 8b2540
                table = new AI.Row[] {
                    new AI.Row((int)DebateAIRow.DebateAIRow_RandCardShoutSophistryIgnoreTopic, 0, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_FindCardRethink, 50, 0),
                    new AI.Row((int)DebateAIRow.DebateAIRow_RandCard, 0, 0),
                };
                count = table.Length;
            }

            if (system.RandBool(chance))
                return AiRandCard(self, 0, 0);
            for (int i = 0; i < count; i++)
            {
                cardIndex = AiInvokeRow(self, table[i].id, table[i].param1, table[i].param2);
                if (cardIndex >= 0)
                {
                    LogDebate($"【AI决策】{GetTeamName(self.team)} 命中决策表第{i + 1}/{count}行 → 选择「{GetCardName(CharacterGetCard(character, cardIndex))}」");
                    return cardIndex;
                }
            }
            return AiRandCard(self, 0, 0);
        }

        #endregion
    }
}
