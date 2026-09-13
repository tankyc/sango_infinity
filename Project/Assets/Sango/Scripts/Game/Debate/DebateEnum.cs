/*
 * 文件名：DebateEnum.cs
 * 描述：舌战(Debate)系统枚举定义，由 s11_enum_debate.h 翻译而来
 *
 * 命名说明：
 *   原 C++ 中的日/韩罗马音标识符已统一改为英文命名，便于阅读与维护：
 *     wadai    -> Topic      （话题：故事 / 道理 / 时势）
 *     wajutsu  -> Rhetoric   （话术：大喝 / 诡辩 / 无视 / 镇静 / 激昂）
 *     seikaku  -> Personality（性格：胆小 / 冷静 / 刚胆 / 莽撞）
 *     shoubyou -> Injury     （伤病：健康 / 轻伤 / 中伤 / 重伤）
 *     daikatsu -> Shout      （大喝）
 *     kiben    -> Sophistry  （诡辩）
 *     mushi    -> Ignore     （无视）
 *     chinsei  -> Compose    （镇静）
 *     gyakujou -> Agitate    （激昂）
 *     kakunin  -> Confirm    （确认）
 *   其余说明：
 *     1. C++ 的 typedef int xxx_t 在 C# 中统一使用 int，保留 -1 表示无效值的语义
 *     2. 枚举成员名保留"类型_取值"的形式，便于与反编译源码对照
 *     3. 部分 C++ 中未在 s11_enum_debate.h 出现的枚举由舌战实现文件归纳补齐
 */

namespace Sango.Core.Debate
{
    /// <summary>舌战阵营：挑战方 / 应战方。对应 C++ debate_team_t</summary>
    public enum DebateTeam
    {
        DebateTeam_Challenger = 0, // 挑战方（挑起舌战的一方）
        DebateTeam_Challenged = 1, // 应战方（接受舌战的一方）
        DebateTeam_Max = 2,
    }

    /// <summary>舌战卡牌。对应 C++ debate_card_t</summary>
    public enum DebateCard
    {
        DebateCard_Rethink = 0, // 再考（重抽手牌）
        DebateCard_Story1 = 1,  // 故事·小
        DebateCard_Story2 = 2,  // 故事·中
        DebateCard_Story3 = 3,  // 故事·大
        DebateCard_Logic1 = 4,  // 道理·小
        DebateCard_Logic2 = 5,  // 道理·中
        DebateCard_Logic3 = 6,  // 道理·大
        DebateCard_Trend1 = 7,  // 时势·小
        DebateCard_Trend2 = 8,  // 时势·中
        DebateCard_Trend3 = 9,  // 时势·大
        DebateCard_Shout = 0xa,     // 大喝
        DebateCard_Sophistry = 0xb, // 诡辩
        DebateCard_Ignore = 0xc,    // 无视
        DebateCard_Compose = 0xd,   // 镇静
        DebateCard_Agitate = 0xe,   // 激昂
        DebateCard_Max = 0xf,

        /// <summary>话题卡（故事 / 道理 / 时势）起始</summary>
        DebateCard_TopicFirst = DebateCard_Story1,
        /// <summary>话题卡结束</summary>
        DebateCard_TopicLast = DebateCard_Trend3,
        /// <summary>话术卡起始</summary>
        DebateCard_RhetoricFirst = DebateCard_Shout,
        /// <summary>话术卡结束</summary>
        DebateCard_RhetoricLast = DebateCard_Agitate,
    }

    /// <summary>舌战流程阶段。对应 C++ debate_phase_t</summary>
    public enum DebatePhase
    {
        DebatePhase_Opening = 0,   // 开场
        DebatePhase_FTK = 1,       // 一击必杀（First Turn Kill）判定
        DebatePhase_Unknown2 = 2,  // 未知阶段（原 C++ 未命名过渡阶段）
        DebatePhase_TurnStart = 3, // 回合开始
        DebatePhase_Play = 4,      // 出牌
        DebatePhase_Damage = 5,    // 伤害结算
        DebatePhase_Anger = 6,     // 愤怒（激昂）触发
        DebatePhase_TurnEnd = 7,   // 回合结束
        DebatePhase_Critical = 8,  // 会心（追击 / 留情）
        DebatePhase_Closing = 9,   // 结束
        DebatePhase_Max = 0xa,
    }

    /// <summary>舌战胜利方式。对应 C++ debate_win_type_t</summary>
    public enum DebateWinType
    {
        DebateWinType_Normal = 0,    // 普通胜利
        DebateWinType_PushOn = 1,    // 追击（痛打落水狗）
        DebateWinType_HaveMercy = 2, // 留情（手下留情）
        DebateWinType_Max = 3,
    }

    /// <summary>会心选择。对应 C++ debate_critical_t</summary>
    public enum DebateCritical
    {
        DebateCritical_PushOn = 0,    // 追击
        DebateCritical_HaveMercy = 1, // 留情
        DebateCritical_Max = 2,
    }

    /// <summary>话题类型。由 get_card_topic / 卡组生成逻辑归纳</summary>
    public enum Topic
    {
        Topic_Story = 0, // 故事
        Topic_Logic = 1, // 道理
        Topic_Trend = 2, // 时势
        Topic_Max = 3,
    }

    /// <summary>话术类型。由 deck_init / DebateCard_RhetoricFirst 归纳</summary>
    public enum Rhetoric
    {
        Rhetoric_Shout = 0,     // 大喝
        Rhetoric_Sophistry = 1, // 诡辩
        Rhetoric_Ignore = 2,    // 无视
        Rhetoric_Compose = 3,   // 镇静
        Rhetoric_Agitate = 4,   // 激昂
        Rhetoric_Max = 5,
    }

    /// <summary>性格。对应 C++ seikaku_t（影响愤怒持续回合与出牌限制）</summary>
    public enum Personality
    {
        Personality_Timid = 0,    // 胆小
        Personality_Calm = 1,     // 冷静
        Personality_Bold = 2,     // 刚胆
        Personality_Reckless = 3, // 莽撞
        Personality_Max = 4,
    }

    /// <summary>伤病程度。对应 C++ shoubyou_t（舌战用于判断可否继续恶化）</summary>
    public enum Injury
    {
        Injury_Healthy = 0, // 健康
        Injury_Light = 1,   // 轻伤
        Injury_Medium = 2,  // 中伤
        Injury_Severe = 3,  // 重伤
        Injury_Max = 4,
    }

    /// <summary>宝物大类。对应 C++ ItemType（舌战仅判断是否持有书籍）</summary>
    public enum ItemType
    {
        ItemType_None = 0, // 无
        ItemType_Book = 1, // 书籍（持有则解锁全部话术）
        ItemType_Max = 2,
    }

    /// <summary>功能开关。对应 C++ Feature（舌战会心是否启用）</summary>
    public enum Feature
    {
        Feature_DebateCritical = 0, // 舌战会心（追击 / 留情）
        Feature_Max = 1,
    }

    /// <summary>武将能力类型。对应 C++ PersonStatType（舌战仅使用智力 / 武力）</summary>
    public enum PersonStatType
    {
        PersonStatType_Strength = 0,     // 武力
        PersonStatType_Intelligence = 1, // 智力
        PersonStatType_Command = 2,      // 统率
        PersonStatType_Politics = 3,     // 政治
        PersonStatType_Max = 4,
    }

    /// <summary>场景。对应 C++ scene_t（舌战用于记录返回场景）</summary>
    public enum Scene
    {
        Scene_Max = -1,
    }

    /// <summary>
    /// AI 决策行类型。对应 C++ Debate::AI::Row::func 成员函数指针。
    /// 舌战 AI 通过决策表逐行调用这些条件 / 查找函数，命中即返回卡牌下标。
    /// </summary>
    public enum DebateAIRow
    {
        DebateAIRow_CondCardAngerBold = 0,              // 5170c0 刚胆（兴奋）时选最低阶话题卡
        DebateAIRow_CondCardAngerCalmRethink = 1,       // 517190 冷静（兴奋）时再考
        DebateAIRow_CondCardVsAngerBold = 2,            // 517270 对手刚胆（兴奋）
        DebateAIRow_CondCardVsAngerCalm = 3,            // 5173f0 对手冷静（兴奋）
        DebateAIRow_CondCardAngerCalm = 4,              // 517450 己方冷静（兴奋）
        DebateAIRow_CondCardShoutSophistryIgnore = 5,   // 517520 大喝 / 诡辩 / 无视
        DebateAIRow_CondCardLowTopic = 6,               // 517650 低阶话题卡
        DebateAIRow_FindCardHighTopic = 7,              // 517720 高阶话题卡
        DebateAIRow_FindCardLowTopic = 8,               // 5177b0 低阶话题卡
        DebateAIRow_CondCardAgitate = 9,                // 517840 激昂
        DebateAIRow_CondCardCompose = 10,               // 5178c0 镇静
        DebateAIRow_CondCardRethink = 11,               // 517950 再考
        DebateAIRow_FindCardRethink = 12,               // 517b70 再考
        DebateAIRow_CondCardIgnore = 13,                // 517bd0 无视
        DebateAIRow_FindCardHighAnyTopic = 14,          // 517c30 任意高阶话题卡
        DebateAIRow_RandCardShoutSophistryIgnoreTopic = 15, // 517ce0 随机大喝 / 诡辩 / 无视 / 话题卡
        DebateAIRow_RandCard = 16,                      // 517e50 随机卡

        DebateAIRow_Max = 17,
    }

    /// <summary>舌战相关消息文本 ID</summary>
    public enum DebateMessageId
    {
        O_DEB_FATALBLOW_INJURED,  // 致命一击导致负伤（操作方提示）
        LD_DEB_FATALBLOW_INJURED, // 致命一击导致负伤（历史记录）
        N_DEB_DEBATE_CONFIRM,     // 是否进入舌战的确认框
    }
}
