#pragma once

s11_begin_namespace

/// <summary></summary>
typedef int debate_team_t;
enum DebateTeamEnum
{
    DebateTeam_Challenger = 0,
    DebateTeam_Challenged = 1,
    DebateTeam_Max = 2,
};

/// <summary></summary>
typedef int debate_card_t;
enum DebateCardEnum
{
    DebateCard_Rethink = 0, // 재고
    DebateCard_Fact1 = 1, // 고사 소
    DebateCard_Fact2 = 2, // 고사 중
    DebateCard_Fact3 = 3, // 고사 대
    DebateCard_Logic1 = 4, // 도리 소
    DebateCard_Logic2 = 5, // 도리 중
    DebateCard_Logic3 = 6, // 도리 대
    DebateCard_Time1 = 7, // 시절 소
    DebateCard_Time2 = 8, // 시절 중
    DebateCard_Time3 = 9, // 시절 대
    DebateCard_Daikatsu = 0xa, // 대갈
    DebateCard_Kiben = 0xb, // 궤변
    DebateCard_Mushi = 0xc, // 무시
    DebateCard_Chinsei = 0xd, // 진정
    DebateCard_Gyakujou = 0xe, // 흥분
    DebateCard_Max = 0xf,

    DebateCard_WadaiFirst = DebateCard_Fact1,
    DebateCard_WadaiLast = DebateCard_Time3,
    DebateCard_WajutsuFirst = DebateCard_Daikatsu,
    DebateCard_WajutsuLast = DebateCard_Gyakujou,

#if 0
    DebateCard_Saikou = 0,

    DebateCard_Thunder = 0xa,
    DebateCard_Sophistry = 0xb,
    DebateCard_Ignore = 0xc,
    DebateCard_Appease = 0xd,
    DebateCard_Rage = 0xe,

    DebateCard_Renew = 0,
    DebateCard_Bellow = 0xa,
    DebateCard_Settle = 0xd,
    DebateCard_Frenzy = 0xe,
#endif
};

/// <summary></summary>
typedef int debate_phase_t;
enum DebatePhaseEnum
{
    DebatePhase_Opening = 0,
    DebatePhase_FTK = 1,
    DebatePhase_Unknown2 = 2, // ?
    DebatePhase_TurnStart = 3,
    DebatePhase_Play = 4,
    DebatePhase_Damage = 5,
    DebatePhase_Anger = 6,
    DebatePhase_TurnEnd = 7,
    DebatePhase_Critical = 8,
    DebatePhase_Closing = 9,
    DebatePhase_Max = 0xa,
};

/// <summary></summary>
typedef int debate_win_type_t;
enum DebateWinTypeEnum
{
    DebateWinType_Normal = 0,
    DebateWinType_PushOn = 1,
    DebateWinType_HaveMercy = 2,
    DebateWinType_Max = 3,
};

/// <summary></summary>
typedef int debate_critical_t;
enum DebateCriticalEnum
{
    DebateCritical_PushOn = 0, // 더 몰아 붙인다
    DebateCritical_HaveMercy = 1, // 인정을 베푼다
    DebateCritical_Max = 2,
};

s11_end_namespace