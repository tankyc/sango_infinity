#pragma once

s11_begin_namespace

/// <summary></summary>
typedef int duel_type_t;
enum DuelTypeEnum
{
    DuelType_0 = 0, // ?
    DuelType_1 = 1, // ?
    DuelType_2 = 2, // 전투
    DuelType_Event = 3, // 이벤트
    DuelType_Max = 4,
};

/// <summary></summary>
typedef int duel_stage_t;
enum DuelStageEnum
{
    DuelStage_Rampart = 0,
    DuelStage_Grassland = 1,
    DuelStage_Forest = 2,
    DuelStage_Max = 3,
};

/// <summary></summary>
typedef int duel_team_t;
enum DuelTeamEnum
{
    DuelTeam_Challenger = 0,
    DuelTeam_Challenged = 1,
    DuelTeam_Max = 2,
};

/// <summary></summary>
typedef int duel_control_t;
enum DuelControlEnum
{
    DuelControl_Manual = 0,
    DuelControl_Auto = 1,
};

/// <summary></summary>
typedef int duel_chara_state_t;
enum DuelCharaStateEnum
{
    DuelCharaState_Active = 0, // 전투 중
    DuelCharaState_Waiting = 1, // 대기
    DuelCharaState_NotJoined = 2, // 아직 참전하지 않음
    DuelCharaState_Max = 3,
};

/// <summary></summary>
typedef int duel_item_type_t;
enum DuelItemTypeEnum
{
    DuelItemType_EliteHorse = 0, // 1 명마
    DuelItemType_Sword = 1, // 2 검
    DuelItemType_LongSpear = 2, // 4 긴무기
    DuelItemType_CrescentHalberd = 3, // 8 방천화극
    DuelItemType_BlueDragon = 4, // 0x10 청룡언월도
    DuelItemType_SerpentBlade = 5, // 0x20 사모
    DuelItemType_ThrowingKnife = 6, // 0x40 암기
    DuelItemType_Bow = 7, // 0x80 활
    DuelItemType_Max = 8,
};

/// <summary></summary>
typedef int duel_ftk_type_t;
enum DuelFtkTypeEnum
{
    DuelFtkType_Normal = 0,
    DuelFtkType_BowA = 1,
    DuelFtkType_BowB = 2, // 상대방의 대사가 끝나기 전에 활을 쏨
    DuelFtkType_Max = 3,
};

/// <summary></summary>
typedef int duel_stance_t;
enum DuelStanceEnum
{
    DuelStance_Attack = 0, // 공격중시
    DuelStance_Defense = 1, // 방어중시
    DuelStance_Spirit = 2, // 투지중시
    DuelStance_Fury = 3, // 일발중시
    DuelStance_Max = 4,

#if 0
    DuelStance_Kougeki = 0,
    DuelStance_Bougyo = 1,
    DuelStance_Kiryoku = 2,
    DuelStance_Ippatsu = 3,
#endif
};

/// <summary></summary>
typedef int duel_buff_type_t;
enum DuelBuffTypeEnum
{
    DuelBuffType_Attack = 0, // 기합
    DuelBuffType_Defense = 1, // 견수
    DuelBuffType_CriticalChance = 2, // 일발
    DuelBuffType_Max = 3,
};

/// <summary></summary>
typedef int duel_action_t;
enum DuelActionEnum
{
    DuelAction_AttackA = 0,
    DuelAction_AttackB = 1,
    DuelAction_AttackC = 2,
    DuelAction_AttackD = 3,
    DuelAction_AttackE = 4,
    DuelAction_AttackCritical = 5,
    DuelAction_DefenseCritical = 6,
    DuelAction_SpiritCritical = 7,
    DuelAction_Max = 8,

    DuelAction_AttackMax = 5,
};

/// <summary></summary>
typedef int duel_action_result_t;
enum DuelActionResultEnum
{
    DuelActionResult_Hit = 0, // 맞음
    DuelActionResult_Blocked = 1, // 막음
    DuelActionResult_Dodged = 2, // 회피
    DuelActionResult_Max = 3,
};

/// <summary></summary>
typedef int duel_sp_t;
enum DuelSpecialEnum
{
    DuelSpecial_Hissatsuwaza = 0, // 필살기
    DuelSpecial_Kiai = 1, // 기합
    DuelSpecial_Kenshu = 2, // 견수
    DuelSpecial_Taikyaku = 3, // 퇴각
    DuelSpecial_Kyuusho = 4, // 급소
    DuelSpecial_Musou = 5, // 무쌍
    DuelSpecial_Anki = 6, // 암기
    DuelSpecial_Nisetaikyaku = 7, // 거짓퇴각
    DuelSpecial_Max = 8,

#if 0
    DuelSpecial_MortalBlow = 0,
    DuelSpecial_Shout = 1,
    DuelSpecial_IronDefense = 2,
    DuelSpecial_Retreat = 3,
    DuelSpecial_StrikeVitals = 4,
    DuelSpecial_ThrowingKnife = 6,
    DuelSpecial_FeignRetreat = 7,
#endif
};

/// <summary></summary>
typedef int duel_sp_result_t;
enum DuelSpecialResultEnum
{
    DuelSpecialResult_Hit = 0, // 맞음
    DuelSpecialResult_Miss = 1, // 실패
    DuelSpecialResult_Max = 2,
};

/// <summary></summary>
typedef int duel_phase_t;
enum DuelPhaseEnum
{
    DuelPhase_Init = 0, // 초기화
    DuelPhase_FTK = 1, // 초살
    DuelPhase_Opening = 2, // 인사
    DuelPhase_TurnStart = 3, // 필살, 등장 계산
    DuelPhase_Join = 4, // 등장
    DuelPhase_Command = 5, // 입력
    DuelPhase_ActionStart = 6, // 교대 실행, 행동 결정
    DuelPhase_SpecialCommand = 7, // 필살 입력
    DuelPhase_Special = 8, // 필살
    DuelPhase_ActionEnd = 9, // 행동 실행
    DuelPhase_Retreat = 0xa, // 퇴각
    DuelPhase_TurnEnd = 0xb, // 턴 종료
    DuelPhase_Closing = 0xc, // 종료
    DuelPhase_Max = 0xd,
};

/// <summary></summary>
typedef int duel_state_t;
enum DuelStateEnum
{
    DuelState_Play = 0,
    DuelState_Command = 1,
    DuelState_SpecialCommand = 2,
};

/// <summary></summary>
typedef int duel_result_t;
enum DuelResultEnum
{
    DuelResult_ChallengerWin = 0, // 거는쪽 승리
    DuelResult_ChallengedWin = 1, // 받는쪽 승리
    DuelResult_2 = 2,
    DuelResult_Draw = 3, // 무승부
    DuelResult_4 = 4,
    DuelResult_5 = 5,
    DuelResult_Max = 6,
};

/// <summary></summary>
typedef int duel_chara_result_t;
enum DuelCharaResultEnum
{
    DuelCharaResult_Escaped = 0, // 도망
    DuelCharaResult_Captured = 1, // 포로
    DuelCharaResult_Dead = 2, // 사망
    DuelCharaResult_Max = 3,
};

/// <summary></summary>
typedef int duel_status_t;
enum DuelStatusEnum
{
    DuelStatus_Ftk = 0, // 1
    DuelStatus_ChallengerHPDamaged = 1, // 2 거는쪽 체력 감소
    DuelStatus_ChallengedHPDamaged = 2, // 4 받는쪽 체력 감소
    DuelStatus_ChallengerShoubyouDamaged = 3, // 8 거는쪽 상병 악화
    DuelStatus_ChallengedShoubyouDamaged = 4, // 0x10 받는쪽 상병 악화
};

/// <summary></summary>
typedef int duel_ai_type_t;
enum DuelAITypeEnum
{
    DuelAIType_Ryofu = 0, // 여포
    DuelAIType_Shoushin = 1, // 소심
    DuelAIType_Reisei = 2, // 냉정
    DuelAIType_Goutan = 3, // 대답
    DuelAIType_Chototsu = 4, // 저돌
    DuelAIType_Max = 5,
};

/// <summary></summary>
typedef int duel_ai_table_t;
enum DuelAITableEnum
{
    DuelAITable_SpecialTry = 0, // 필살 시도
    DuelAITable_Special = 1, // 필살
    DuelAITable_Stance = 2, // 방침
    DuelAITable_Switch = 3, // 교대
    DuelAITable_Max = 4,
};

/// <summary></summary>
typedef int duel_ai_row_t;
enum DuelAIRowEnum
{
    DuelAIRow_SpecialTry_HP_LTE = 0, // 체력 param1 이하
    DuelAIRow_SpecialTry_Spirit_GTE = 1, // 기력 param1 이상
    DuelAIRow_SpecialTry_OpponentHP_LTE = 2, // 적 체력 param1 이하
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
    DuelAIRow_Special_Taikyaku = 15, // 체력 param1 이하, 적 체력 param1 초과
    DuelAIRow_Special_Random = 16,

    // DuelAITable_Stance
    DuelAIRow_Stance_A_Always = 17,
    DuelAIRow_Stance_A_HP_GTE = 18, // 체력 param1 이상
    DuelAIRow_Stance_A_LowHP = 19, // 적 체력이 내 체력보다 2배 이상
    DuelAIRow_Stance_A_OpponentHP_LTE = 20, // 적 체력 param1 이하
    DuelAIRow_Stance_A_OpponentBestChara = 21, // 적 팀에서 가장 전력이 높은 무장
    DuelAIRow_Stance_A_Invulnerable = 22, // 무적 상태
    DuelAIRow_Stance_D_BestChara = 23, // 팀에서 가장 전력이 높은 무장
    DuelAIRow_Stance_D_BlowCounter_GTE = 24, // 합 param1 이상
    DuelAIRow_Stance_S_HP_GTE = 25, // 체력 param1 이상
    DuelAIRow_Stance_S_Weak = 26, // 적 전력이 내 전력 이상
    DuelAIRow_Stance_S_NotBestChara = 27, // 팀에서 가장 전력이 높은 무장이 아님
    DuelAIRow_Stance_S_OpponentNotBestChara = 28, // 적 팀에서 가장 전력이 높은 무장이 아님
    DuelAIRow_Stance_S_HasNotAttackBuff = 29, // 공격력 버프 없음
    DuelAIRow_Stance_S_HasNotDefenseBuff = 30, // 방어력 버프 없음
    DuelAIRow_Stance_F_Always = 31,
    DuelAIRow_Stance_Stop_StanceTimer_GTE = 32, // 방침 턴 param1 이상

    DuelAIRow_Switch_HP_LTE = 33, // 체력 param1 이하
    DuelAIRow_Switch_Kunshu = 34, // 현재 무장이 군주
    DuelAIRow_Switch_StrengthDiff_LTE = 35, // 적 무력 - 내 무력 param1 이하(적 무력이 내 무력보다 param1 초과)
    DuelAIRow_Switch_NotBestChara = 36, // 팀에서 가장 전력이 높은 무장이 아님
    DuelAIRow_Switch_Stop_Invulnerable = 37, // 무적 상태
    DuelAIRow_Switch_38 = 38, // ?

    DuelAIRow_Max = 39,
};

/// <summary></summary>
typedef int duel_ai_comp_op_t;
enum DuelAICompOpEnum
{
    DuelAICompOp_GreaterThanOrEqual = 0, // 1 >=
    DuelAICompOp_LessThanOrEqual = 1, // 2 <=
    DuelAICompOp_LessThan = 2, // 4 <
    DuelAICompOp_GreaterThan = 3, // 8 >
};

/// <summary></summary>
typedef int duel_button_t;
enum DuelButtonEnum
{
    DuelButton_StanceAttack = 2,
    DuelButton_StanceDefense = 3,
    DuelButton_StanceSpirit = 4,
    DuelButton_StanceFury = 5,
    DuelButton_Special = 14,
    DuelButton_Switch = 24, // 교대
    DuelButton_Max = 26,
};

s11_end_namespace