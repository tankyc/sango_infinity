/*
 * 文件名：DuelEnum.cs
 * 描述：单挑(Duel)系统枚举定义，由 s11_enum_duel.h 翻译而来
 * 说明：
 *   1. C++ 中的 typedef int xxx_t 在 C# 中统一使用 int，保持 1:1 翻译语义（允许 -1 表示无效值）
 *   2. 枚举成员名保留原始 C++ 命名，便于与反编译源码对照
 */

namespace Sango.Core.Duel
{
    /// <summary>单挑类型（对话相关）</summary>
    public enum DuelType
    {
        DuelType_0 = 0,     // ?
        DuelType_1 = 1,     // ?
        DuelType_2 = 2,     // 战斗
        DuelType_Event = 3, // 事件
        DuelType_Max = 4,
    }

    /// <summary>单挑场地</summary>
    public enum DuelStage
    {
        DuelStage_Rampart = 0,   // 城墙
        DuelStage_Grassland = 1, // 草地
        DuelStage_Forest = 2,    // 森林
        DuelStage_Max = 3,
    }

    /// <summary>单挑阵营：挑战方 / 应战方</summary>
    public enum DuelTeam
    {
        DuelTeam_Challenger = 0, // 挑战方（挑起单挑的一方）
        DuelTeam_Challenged = 1, // 应战方（接受单挑的一方）
        DuelTeam_Max = 2,
    }

    /// <summary>操作方式</summary>
    public enum DuelControl
    {
        DuelControl_Manual = 0, // 手动
        DuelControl_Auto = 1,   // 自动（AI）
    }

    /// <summary>参战武将状态</summary>
    public enum DuelCharaState
    {
        DuelCharaState_Active = 0,    // 战斗中
        DuelCharaState_Waiting = 1,   // 待机（已参战，场下）
        DuelCharaState_NotJoined = 2, // 尚未参战
        DuelCharaState_Max = 3,
    }

    /// <summary>单挑中生效的宝物类型（位序号）</summary>
    public enum DuelItemType
    {
        DuelItemType_EliteHorse = 0,      // 名马
        DuelItemType_Sword = 1,           // 剑
        DuelItemType_LongSpear = 2,       // 长武器
        DuelItemType_CrescentHalberd = 3, // 方天画戟
        DuelItemType_BlueDragon = 4,      // 青龙偃月刀
        DuelItemType_SerpentBlade = 5,    // 蛇矛
        DuelItemType_ThrowingKnife = 6,   // 暗器
        DuelItemType_Bow = 7,             // 弓
        DuelItemType_Max = 8,
    }

    /// <summary>一击必杀（一合）类型</summary>
    public enum DuelFtkType
    {
        DuelFtkType_Normal = 0,
        DuelFtkType_BowA = 1,
        DuelFtkType_BowB = 2, // 对方台词结束前放箭
        DuelFtkType_Max = 3,
    }

    /// <summary>行动方针（架势）</summary>
    public enum DuelStance
    {
        DuelStance_Attack = 0,  // 攻击重视
        DuelStance_Defense = 1, // 防御重视
        DuelStance_Spirit = 2,  // 斗志重视
        DuelStance_Fury = 3,    // 一发重视
        DuelStance_Max = 4,
    }

    /// <summary>增益状态（Buff）类型</summary>
    public enum DuelBuffType
    {
        DuelBuffType_Attack = 0,          // 气合（攻击力提升）
        DuelBuffType_Defense = 1,         // 坚守（防御力提升）
        DuelBuffType_CriticalChance = 2,  // 一发（暴击率提升）
        DuelBuffType_Max = 3,
    }

    /// <summary>普通行动类型</summary>
    public enum DuelAction
    {
        DuelAction_AttackA = 0,
        DuelAction_AttackB = 1,
        DuelAction_AttackC = 2,
        DuelAction_AttackD = 3,
        DuelAction_AttackE = 4,
        DuelAction_AttackCritical = 5,  // 攻击重视·暴击
        DuelAction_DefenseCritical = 6, // 防御重视·暴击（无敌）
        DuelAction_SpiritCritical = 7,  // 斗志重视·暴击（斗志全满）
        DuelAction_Max = 8,

        DuelAction_AttackMax = 5, // 随机普通攻击时的上界（不含）
    }

    /// <summary>普通行动判定结果</summary>
    public enum DuelActionResult
    {
        DuelActionResult_Hit = 0,     // 命中
        DuelActionResult_Blocked = 1, // 被格挡
        DuelActionResult_Dodged = 2,  // 被闪避
        DuelActionResult_Max = 3,
    }

    /// <summary>必杀技类型</summary>
    public enum DuelSpecial
    {
        DuelSpecial_Hissatsuwaza = 0,  // 必杀技
        DuelSpecial_Kiai = 1,          // 气合
        DuelSpecial_Kenshu = 2,        // 坚守
        DuelSpecial_Taikyaku = 3,      // 退却
        DuelSpecial_Kyuusho = 4,       // 急所（弱点）
        DuelSpecial_Musou = 5,         // 无双
        DuelSpecial_Anki = 6,          // 暗器
        DuelSpecial_Nisetaikyaku = 7,  // 伪退却
        DuelSpecial_Max = 8,
    }

    /// <summary>必杀技判定结果</summary>
    public enum DuelSpecialResult
    {
        DuelSpecialResult_Hit = 0,  // 命中
        DuelSpecialResult_Miss = 1, // 失败
        DuelSpecialResult_Max = 2,
    }

    /// <summary>单挑流程阶段</summary>
    public enum DuelPhase
    {
        DuelPhase_Init = 0,           // 初始化
        DuelPhase_FTK = 1,            // 一击必杀判定
        DuelPhase_Opening = 2,        // 寒暄
        DuelPhase_TurnStart = 3,      // 必杀/登场计算
        DuelPhase_Join = 4,           // 登场
        DuelPhase_Command = 5,        // 指令输入
        DuelPhase_ActionStart = 6,    // 执行交替、决定行动
        DuelPhase_SpecialCommand = 7, // 必杀指令输入
        DuelPhase_Special = 8,        // 必杀
        DuelPhase_ActionEnd = 9,      // 执行行动
        DuelPhase_Retreat = 0xa,      // 退却
        DuelPhase_TurnEnd = 0xb,      // 回合结束
        DuelPhase_Closing = 0xc,      // 结束
        DuelPhase_Max = 0xd,
    }

    /// <summary>单挑运行状态</summary>
    public enum DuelState
    {
        DuelState_Play = 0,
        DuelState_Command = 1,
        DuelState_SpecialCommand = 2,
    }

    /// <summary>单挑结果</summary>
    public enum DuelResult
    {
        DuelResult_ChallengerWin = 0, // 挑战方胜利
        DuelResult_ChallengedWin = 1, // 应战方胜利
        DuelResult_2 = 2,
        DuelResult_Draw = 3,          // 平局
        DuelResult_4 = 4,
        DuelResult_5 = 5,
        DuelResult_Max = 6,
    }

    /// <summary>败方武将结局</summary>
    public enum DuelCharaResult
    {
        DuelCharaResult_Escaped = 0,  // 逃走
        DuelCharaResult_Captured = 1, // 被俘
        DuelCharaResult_Dead = 2,     // 死亡
        DuelCharaResult_Max = 3,
    }

    /// <summary>
    /// 单挑状态标记（位序号，实际值为 1 &lt;&lt; n）
    /// </summary>
    public enum DuelStatus
    {
        DuelStatus_Ftk = 0,                         // 0x01 一击必杀
        DuelStatus_ChallengerHPDamaged = 1,         // 0x02 挑战方体力减少
        DuelStatus_ChallengedHPDamaged = 2,         // 0x04 应战方体力减少
        DuelStatus_ChallengerShoubyouDamaged = 3,   // 0x08 挑战方伤病恶化
        DuelStatus_ChallengedShoubyouDamaged = 4,   // 0x10 应战方伤病恶化
    }

    /// <summary>DuelStatus 对应的实际位掩码</summary>
    public static class DuelStatusMask
    {
        public const int Ftk = 1 << (int)DuelStatus.DuelStatus_Ftk;
        public const int ChallengerHPDamaged = 1 << (int)DuelStatus.DuelStatus_ChallengerHPDamaged;
        public const int ChallengedHPDamaged = 1 << (int)DuelStatus.DuelStatus_ChallengedHPDamaged;
        public const int ChallengerShoubyouDamaged = 1 << (int)DuelStatus.DuelStatus_ChallengerShoubyouDamaged;
        public const int ChallengedShoubyouDamaged = 1 << (int)DuelStatus.DuelStatus_ChallengedShoubyouDamaged;
    }

    /// <summary>AI 性格类型（决定使用哪套决策表）</summary>
    public enum DuelAIType
    {
        DuelAIType_Ryofu = 0,    // 吕布
        DuelAIType_Shoushin = 1, // 小心
        DuelAIType_Reisei = 2,   // 冷静
        DuelAIType_Goutan = 3,   // 大胆
        DuelAIType_Chototsu = 4, // 猪突
        DuelAIType_Max = 5,
    }

    /// <summary>AI 决策表类型</summary>
    public enum DuelAITable
    {
        DuelAITable_SpecialTry = 0, // 是否尝试必杀
        DuelAITable_Special = 1,    // 必杀选择
        DuelAITable_Stance = 2,     // 行动方针
        DuelAITable_Switch = 3,     // 交替
        DuelAITable_Max = 4,
    }

    /// <summary>AI 决策表行类型</summary>
    public enum DuelAIRow
    {
        DuelAIRow_SpecialTry_HP_LTE = 0,             // 体力 param1 以下
        DuelAIRow_SpecialTry_Spirit_GTE = 1,         // 斗志 param1 以上
        DuelAIRow_SpecialTry_OpponentHP_LTE = 2,     // 敌方体力 param1 以下
        DuelAIRow_SpecialTry_AnkiOrMusou = 3,
        DuelAIRow_SpecialTry_Kyuusho = 4,
        DuelAIRow_SpecialTry_Kiai = 5,
        DuelAIRow_SpecialTry_Kenshu = 6,
        DuelAIRow_SpecialTry_Always = 7,
        DuelAIRow_SpecialTry_Stop = 8,

        // DuelAITable_Special
        DuelAIRow_Special_Nisetaikyaku = 9,
        DuelAIRow_Special_Anki = 10,
        DuelAIRow_Special_Kyuusho = 11,
        DuelAIRow_Special_AnkiOrMusou = 12,
        DuelAIRow_Special_Kiai = 13,
        DuelAIRow_Special_Kenshu = 14,
        DuelAIRow_Special_Taikyaku = 15, // 体力 param1 以下，敌方体力 param1 以上
        DuelAIRow_Special_Random = 16,

        // DuelAITable_Stance
        DuelAIRow_Stance_A_Always = 17,
        DuelAIRow_Stance_A_HP_GTE = 18,                  // 体力 param1 以上
        DuelAIRow_Stance_A_LowHP = 19,                   // 敌方体力为自己体力 2 倍以上
        DuelAIRow_Stance_A_OpponentHP_LTE = 20,          // 敌方体力 param1 以下
        DuelAIRow_Stance_A_OpponentBestChara = 21,       // 敌方队伍中战力最高的武将
        DuelAIRow_Stance_A_Invulnerable = 22,            // 无敌状态
        DuelAIRow_Stance_D_BestChara = 23,               // 己方队伍中战力最高的武将
        DuelAIRow_Stance_D_BlowCounter_GTE = 24,         // 合数 param1 以上
        DuelAIRow_Stance_S_HP_GTE = 25,                  // 体力 param1 以上
        DuelAIRow_Stance_S_Weak = 26,                    // 敌方战力在自己战力以上
        DuelAIRow_Stance_S_NotBestChara = 27,            // 不是己方战力最高的武将
        DuelAIRow_Stance_S_OpponentNotBestChara = 28,    // 不是敌方战力最高的武将
        DuelAIRow_Stance_S_HasNotAttackBuff = 29,        // 没有攻击力增益
        DuelAIRow_Stance_S_HasNotDefenseBuff = 30,       // 没有防御力增益
        DuelAIRow_Stance_F_Always = 31,
        DuelAIRow_Stance_Stop_StanceTimer_GTE = 32,      // 方针持续回合 param1 以上

        DuelAIRow_Switch_HP_LTE = 33,                    // 体力 param1 以下
        DuelAIRow_Switch_Kunshu = 34,                    // 当前武将为君主
        DuelAIRow_Switch_StrengthDiff_LTE = 35,          // 敌方武力 - 己方武力 param1 以下
        DuelAIRow_Switch_NotBestChara = 36,              // 不是己方战力最高的武将
        DuelAIRow_Switch_Stop_Invulnerable = 37,         // 无敌状态
        DuelAIRow_Switch_38 = 38,                        // ?

        DuelAIRow_Max = 39,
    }

    /// <summary>AI 比较运算符</summary>
    public enum DuelAICompOp
    {
        DuelAICompOp_GreaterThanOrEqual = 0, // >=
        DuelAICompOp_LessThanOrEqual = 1,    // &lt;=
        DuelAICompOp_LessThan = 2,           // &lt;
        DuelAICompOp_GreaterThan = 3,        // >
    }

    /// <summary>单挑界面按钮（教程用）</summary>
    public enum DuelButton
    {
        DuelButton_StanceAttack = 2,
        DuelButton_StanceDefense = 3,
        DuelButton_StanceSpirit = 4,
        DuelButton_StanceFury = 5,
        DuelButton_Special = 14,
        DuelButton_Switch = 24, // 交替
        DuelButton_Max = 26,
    }
}
