#include "pch.h"

s11_begin_namespace

/// <summary>4fb490</summary>
void Duel::ai_update_chara(AI* self)
{
    if (not self->parent)
        return;
    if (not utils::in_range(self->team, 0, DuelTeam_Max - 1))
        return;
    if (not utils::in_range(self->opponent_team, 0, DuelTeam_Max - 1))
        return;
    self->chara = self->parent->get_current_chara(self->team);
    self->opponent_chara = self->parent->get_current_chara(self->opponent_team);
}

/// <summary>4fb4d0</summary>
const Duel::AI::Row* Duel::ai_get_table(const AI* self, duel_ai_type_t type, duel_ai_table_t table_id) const
{
    // 8b0a98
    static constexpr AI::Row ryofu_sp_try[] = {
        { 100, DuelAIRow_SpecialTry_Spirit_GTE, 300, 0 },
        {   5, DuelAIRow_SpecialTry_Kenshu, 0, 0 },
        {   5, DuelAIRow_SpecialTry_Kiai, 0, 0 },
        {  10, DuelAIRow_SpecialTry_Stop, 0, 0 },
        {  80, DuelAIRow_SpecialTry_AnkiOrMusou, 0, 0 },
        {  60, DuelAIRow_SpecialTry_OpponentHP_LTE, 50, 0 },
        { 100, DuelAIRow_SpecialTry_HP_LTE, 25, 0 },
        {  30, DuelAIRow_SpecialTry_Always, 0, 0 },
        { 100, DuelAIRow_SpecialTry_Stop, 0, 0 },
        AI::Row(),
    };

    // 8b0b38
    static constexpr AI::Row shoushin_sp_try[] = {
        { 100, DuelAIRow_SpecialTry_Spirit_GTE, 300, 0 },
        {  80, DuelAIRow_SpecialTry_Kenshu, 0, 0 },
        {  40, DuelAIRow_SpecialTry_Stop, 0, 0 },
        {  20, DuelAIRow_SpecialTry_HP_LTE, 50, 0 },
        {  10, DuelAIRow_SpecialTry_Always, 0, 0 },
        { 100, DuelAIRow_SpecialTry_Stop, 0, 0 },
        AI::Row(),
    };

    // 8b0ba8
    static constexpr AI::Row reisei_sp_try[] = {
        { 100, DuelAIRow_SpecialTry_Spirit_GTE, 300, 0 },
        {  80, DuelAIRow_SpecialTry_AnkiOrMusou, 0, 0 },
        {  50, DuelAIRow_SpecialTry_Kenshu, 0, 0 },
        {  10, DuelAIRow_SpecialTry_Kiai, 0, 0 },
        {  90, DuelAIRow_SpecialTry_Kyuusho, 0, 0 },
        {  40, DuelAIRow_SpecialTry_Stop, 0, 0 },
        {  10, DuelAIRow_SpecialTry_HP_LTE, 50, 0 },
        { 100, DuelAIRow_SpecialTry_Stop, 0, 0 },
        AI::Row(),
    };

    // 8b0c38
    static constexpr AI::Row goutan_sp_try[] = {
        { 100, DuelAIRow_SpecialTry_Spirit_GTE, 300, 0 },
        {   5, DuelAIRow_SpecialTry_Kenshu, 0, 0 },
        {   5, DuelAIRow_SpecialTry_Kiai, 0, 0 },
        {  80, DuelAIRow_SpecialTry_AnkiOrMusou, 0, 0 },
        {  10, DuelAIRow_SpecialTry_Stop, 0, 0 },
        {  60, DuelAIRow_SpecialTry_Kyuusho, 0, 0 },
        {  35, DuelAIRow_SpecialTry_HP_LTE, 50, 0 },
        {  80, DuelAIRow_SpecialTry_OpponentHP_LTE, 25, 0 },
        {  35, DuelAIRow_SpecialTry_Always, 0, 0 },
        { 100, DuelAIRow_SpecialTry_Stop, 0, 0 },
        AI::Row(),
    };

    // 8b0ce8
    static constexpr AI::Row chototsu_sp_try[] = {
        { 100, DuelAIRow_SpecialTry_Spirit_GTE, 300, 0 },
        {  20, DuelAIRow_SpecialTry_Kiai, 0, 0 },
        {  40, DuelAIRow_SpecialTry_Kyuusho, 0, 0 },
        {  60, DuelAIRow_SpecialTry_HP_LTE, 33, 0 },
        {  60, DuelAIRow_SpecialTry_OpponentHP_LTE, 33, 0 },
        {  50, DuelAIRow_SpecialTry_Always, 0, 0 },
        { 100, DuelAIRow_SpecialTry_Stop, 0, 0 },
        AI::Row(),
    };

    // 8b0d68
    static constexpr AI::Row ryofu_sp[] = {
        {  95, DuelAIRow_Special_Taikyaku, 12, 0 },
        {  20, DuelAIRow_Special_Nisetaikyaku, 0, 0 },
        {  30, DuelAIRow_Special_Anki, 0, 0 },
        {  30, DuelAIRow_Special_AnkiOrMusou, 0, 0 },
        {  40, DuelAIRow_Special_Anki, 0, 0 },
        {  80, DuelAIRow_Special_Nisetaikyaku, 0, 0 },
        { 100, DuelAIRow_Special_Random, 0, 0 },
        AI::Row(),
    };

    // 8b0de8
    static constexpr AI::Row shoushin_sp[] = {
        {  90, DuelAIRow_Special_Taikyaku, 16, 0 },
        { 100, DuelAIRow_Special_Nisetaikyaku, 0, 0 },
        {  90, DuelAIRow_Special_AnkiOrMusou, 0, 0 },
        {  70, DuelAIRow_Special_Anki, 0, 0 },
        {  80, DuelAIRow_Special_Kenshu, 0, 0 },
        {  80, DuelAIRow_Special_Kyuusho, 0, 0 },
        { 100, DuelAIRow_Special_Random, 0, 0 },
        AI::Row(),
    };

    // 8b0e68
    static constexpr AI::Row reisei_sp[] = {
        {  90, DuelAIRow_Special_Taikyaku, 14, 0 },
        { 100, DuelAIRow_Special_Nisetaikyaku, 0, 0 },
        {  90, DuelAIRow_Special_AnkiOrMusou, 0, 0 },
        {  30, DuelAIRow_Special_Kiai, 0, 0 },
        {  30, DuelAIRow_Special_Kenshu, 0, 0 },
        {  50, DuelAIRow_Special_Kyuusho, 0, 0 },
        {  60, DuelAIRow_Special_Anki, 0, 0 },
        { 100, DuelAIRow_Special_Random, 0, 0 },
        AI::Row(),
    };

    // 8b0ef8
    static constexpr AI::Row goutan_sp[] = {
        {  70, DuelAIRow_Special_Taikyaku, 12, 0 },
        {  40, DuelAIRow_Special_Nisetaikyaku, 0, 0 },
        {  10, DuelAIRow_Special_Kiai, 0, 0 },
        {  30, DuelAIRow_Special_Anki, 0, 0 },
        {  40, DuelAIRow_Special_AnkiOrMusou, 0, 0 },
        {  10, DuelAIRow_Special_Kenshu, 0, 0 },
        {  70, DuelAIRow_Special_Kyuusho, 0, 0 },
        {  70, DuelAIRow_Special_Nisetaikyaku, 0, 0 },
        {  90, DuelAIRow_Special_Anki, 0, 0 },
        { 100, DuelAIRow_Special_Random, 0, 0 },
        AI::Row(),
    };

    // 8b0fa8
    static constexpr AI::Row chototsu_sp[] = {
        {  40, DuelAIRow_Special_Taikyaku, 12, 0 },
        {  20, DuelAIRow_Special_Nisetaikyaku, 0, 0 },
        {  15, DuelAIRow_Special_Kiai, 0, 0 },
        {  50, DuelAIRow_Special_Kyuusho, 0, 0 },
        {  30, DuelAIRow_Special_Anki, 0, 0 },
        {   5, DuelAIRow_Special_Kenshu, 0, 0 },
        {  70, DuelAIRow_Special_Nisetaikyaku, 0, 0 },
        {  60, DuelAIRow_Special_AnkiOrMusou, 0, 0 },
        {  90, DuelAIRow_Special_Anki, 0, 0 },
        { 100, DuelAIRow_Special_Random, 0, 0 },
        AI::Row(),
    };

    // 8b1058
    static constexpr AI::Row ryofu_st[] = {
        { 100, DuelAIRow_Stance_A_Invulnerable, 0, 0 },
        {  95, DuelAIRow_Stance_Stop_StanceTimer_GTE, 1, 0 }, // 1턴 95% 확률로 유지
        {  50, DuelAIRow_Stance_S_HP_GTE, 80, 0 },
        {  10, DuelAIRow_Stance_S_HasNotAttackBuff, 0, 0 },
        {   5, DuelAIRow_Stance_S_HasNotDefenseBuff, 0, 0 },
        { 100, DuelAIRow_Stance_A_Always, 0, 0 },
        AI::Row(),
    };

    // 8b10c8
    static constexpr AI::Row shoushin_st[] = {
        { 100, DuelAIRow_Stance_A_Invulnerable, 0, 0 },
        {  80, DuelAIRow_Stance_D_BlowCounter_GTE, 40, 0 },
        {  90, DuelAIRow_Stance_Stop_StanceTimer_GTE, 3, 0 }, // 3턴 90% 확률로 유지
        {  80, DuelAIRow_Stance_S_HasNotDefenseBuff, 0, 0 },
        {  50, DuelAIRow_Stance_A_OpponentHP_LTE, 25, 0 },
        { 100, DuelAIRow_Stance_A_Always, 0, 0 },
        AI::Row(),
    };

    // 8b1138
    static constexpr AI::Row reisei_st[] = {
        { 100, DuelAIRow_Stance_A_Invulnerable, 0, 0 },
        {  90, DuelAIRow_Stance_Stop_StanceTimer_GTE, 3, 0 }, // 3턴 90% 확률로 유지
        {  70, DuelAIRow_Stance_D_BlowCounter_GTE, 40, 0 },
        {  60, DuelAIRow_Stance_S_HasNotDefenseBuff, 0, 0 },
        {  30, DuelAIRow_Stance_S_HasNotAttackBuff, 0, 0 },
        {  50, DuelAIRow_Stance_A_OpponentHP_LTE, 33, 0 },
        { 100, DuelAIRow_Stance_A_Always, 0, 0 },
        AI::Row(),
    };

    // 8b11b8
    static constexpr AI::Row goutan_st[] = {
        { 100, DuelAIRow_Stance_A_Invulnerable, 0, 0 },
        {  90, DuelAIRow_Stance_Stop_StanceTimer_GTE, 2, 0 }, // 2턴 90% 확률로 유지
        {  40, DuelAIRow_Stance_S_HP_GTE, 75, 0 },
        {  60, DuelAIRow_Stance_A_OpponentHP_LTE, 40, 0 },
        {  10, DuelAIRow_Stance_S_HasNotDefenseBuff, 0, 0 },
        {  10, DuelAIRow_Stance_S_HasNotAttackBuff, 0, 0 },
        { 100, DuelAIRow_Stance_A_Always, 0, 0 },
        AI::Row(),
    };

    // 8b1238
    static constexpr AI::Row chototsu_st[] = {
        { 100, DuelAIRow_Stance_A_Invulnerable, 0, 0 },
        {  90, DuelAIRow_Stance_Stop_StanceTimer_GTE, 1, 0 }, // 1턴 90% 확률로 유지
        {  90, DuelAIRow_Stance_A_OpponentHP_LTE, 50, 0 },
        {  60, DuelAIRow_Stance_S_HP_GTE, 80, 0 },
        {   5, DuelAIRow_Stance_F_Always, 0, 0 },
        {  10, DuelAIRow_Stance_S_HasNotAttackBuff, 0, 0 },
        { 100, DuelAIRow_Stance_A_Always, 0, 0 },
        AI::Row(),
    };

    // 8b12b8
    static constexpr AI::Row ryofu_sw[] = {
        { 100, DuelAIRow_Switch_Stop_Invulnerable, 0, 0 },
        {  50, DuelAIRow_Switch_38, 0, 0 },
        {  80, DuelAIRow_Switch_Kunshu, 0, 0 },
        {  40, DuelAIRow_Switch_HP_LTE, 33, 0 },
        { 100, DuelAIRow_Switch_38, 0, 0 },
        AI::Row(),
    };

    // 8b1318
    static constexpr AI::Row shoushin_sw[] = {
        { 100, DuelAIRow_Switch_Stop_Invulnerable, 0, 0 },
        { 100, DuelAIRow_Switch_Kunshu, 0, 0 },
        {  30, DuelAIRow_Switch_NotBestChara, 0, 0 },
        {  70, DuelAIRow_Switch_HP_LTE, 75, 0 },
        {  70, DuelAIRow_Switch_StrengthDiff_LTE, 7, 0 },
        { 100, DuelAIRow_Switch_38, 0, 0 },
        AI::Row(),
    };

    // 8b1388
    static constexpr AI::Row reisei_sw[] = {
        { 100, DuelAIRow_Switch_Stop_Invulnerable, 0, 0 },
        {  80, DuelAIRow_Switch_Kunshu, 0, 0 },
        {  30, DuelAIRow_Switch_NotBestChara, 0, 0 },
        {  60, DuelAIRow_Switch_StrengthDiff_LTE, 7, 0 },
        {  70, DuelAIRow_Switch_HP_LTE, 50, 0 },
        { 100, DuelAIRow_Switch_38, 0, 0 },
        AI::Row(),
    };

    // 8b13f8
    static constexpr AI::Row goutan_sw[] = {
        { 100, DuelAIRow_Switch_Stop_Invulnerable, 0, 0 },
        {  60, DuelAIRow_Switch_Kunshu, 0, 0 },
        {  80, DuelAIRow_Switch_HP_LTE, 33, 0 },
        {  60, DuelAIRow_Switch_StrengthDiff_LTE, 8, 0 },
        { 100, DuelAIRow_Switch_38, 0, 0 },
        AI::Row(),
    };

    // 8b1458
    static constexpr AI::Row chototsu_sw[] = {
        { 100, DuelAIRow_Switch_Stop_Invulnerable, 0, 0 },
        {  60, DuelAIRow_Switch_Kunshu, 0, 0 },
        {  70, DuelAIRow_Switch_HP_LTE, 25, 0 },
        {  60, DuelAIRow_Switch_StrengthDiff_LTE, 12, 0 },
        { 100, DuelAIRow_Switch_38, 0, 0 },
        AI::Row(),
    };

    // 8b0a40
    static constexpr std::array<std::array<const AI::Row*, DuelAIType_Max>, DuelAIType_Max> table = { {
        { ryofu_sp_try, ryofu_sp, ryofu_st, ryofu_sw },
        { shoushin_sp_try, shoushin_sp, shoushin_st, shoushin_sw },
        { reisei_sp_try, reisei_sp, reisei_st, reisei_sw },
        { goutan_sp_try, goutan_sp, goutan_st, goutan_sw },
        { chototsu_sp_try, chototsu_sp, chototsu_st, chototsu_sw },
    } };

    return table[type][table_id];
}

/// <summary>4fb500</summary>
int Duel::ai_get_hp(const AI* self, duel_team_t team, int chara) const
{
    return self->parent->get_hp(team, chara);
}

/// <summary>4fb540</summary>
int Duel::ai_get_hp(const AI* self, bool opponent) const
{
    if (not opponent)
        return ai_get_hp(self, self->team, self->chara);
    else
        return ai_get_hp(self, self->opponent_team, self->opponent_chara);
}

/// <summary>4fb590</summary>
int Duel::ai_get_spirit(const AI* self, bool opponent) const
{
    if (not opponent)
        return self->parent->get_spirit(self->team, self->chara);
    else
        return self->parent->get_spirit(self->opponent_team, self->opponent_chara);
}

/// <summary>4fb5e0</summary>
const Duel::AI::Row* Duel::ai_get_table(const AI* self, duel_ai_table_t table_id) const
{
    Person* person = self->parent->get_person(self->team, self->chara);
    if (not utils::is_active(person))
        return nullptr;
    if (person->get_id() == PersonId_Ryofu)
        return ai_get_table(self, DuelAIType_Ryofu, table_id);
    switch (person->get_seikaku())
    {
    case Seikaku_Shoushin:
        return ai_get_table(self, DuelAIType_Shoushin, table_id);
    case Seikaku_Reisei:
        return ai_get_table(self, DuelAIType_Reisei, table_id);
    case Seikaku_Goutan:
        return ai_get_table(self, DuelAIType_Goutan, table_id);
    case Seikaku_Chototsu:
        return ai_get_table(self, DuelAIType_Chototsu, table_id);
    }
    return nullptr;
}

/// <summary>4fb6f0</summary>
bool Duel::ai_comp(const AI* self, duel_ai_comp_op_t op, int a, int b) const
{
    switch (op)
    {
    case DuelAICompOp_GreaterThanOrEqual:
        return a >= b;
    case DuelAICompOp_LessThanOrEqual:
        return a <= b;
    case DuelAICompOp_LessThan:
        return a < b;
    case DuelAICompOp_GreaterThan:
        return a > b;
    }
    return false;
}

/// <summary>4fb750</summary>
bool Duel::ai_comp_hp(const AI* self, const AI::Row* row, duel_ai_comp_op_t op, bool opponent) const
{
    return ai_comp(self, op, ai_get_hp(self, opponent), row->param1);
}

/// <summary>4fb780</summary>
bool Duel::ai_comp_spirit(const AI* self, const AI::Row* row, duel_ai_comp_op_t op, bool opponent) const
{
    return ai_comp(self, op, ai_get_spirit(self, opponent), row->param1);
}

/// <summary>4fb7b0</summary>
bool Duel::ai_is_special_enabled(const AI* self, duel_sp_t special, bool opponent) const
{
    if (not opponent)
        return self->parent->is_special_enabled(self->team, self->chara, special);
    else
        return self->parent->is_special_enabled(self->opponent_team, self->opponent_chara, special);
}

/// <summary>4fb830</summary>
bool Duel::ai_has_buff(const AI* self, duel_buff_type_t buff, bool opponent) const
{
    if (not opponent)
        return self->parent->has_buff(self->team, buff);
    else
        return self->parent->has_buff(self->opponent_team, buff);
}

/// <summary>4fb890</summary>
bool Duel::ai_calc_special_try(const AI* self) const
{
    // 사용 가능한 필살 없음
    if (not self->parent->can_special(self->team, self->chara))
        return false;
    for (const AI::Row* row = ai_get_table(self, DuelAITable_SpecialTry); utils::in_range(row->id, 0, DuelAIRow_Max - 1); row++)
    {
        if (not system_->rand_bool(row->chance))
            continue;
        bool buff = false;
        switch (row->id)
        {
        case DuelAIRow_SpecialTry_HP_LTE:
            if (not ai_comp_hp(self, row, DuelAICompOp_LessThanOrEqual))
                continue;
            break;
        case DuelAIRow_SpecialTry_Spirit_GTE:
            if (not ai_comp_spirit(self, row, DuelAICompOp_GreaterThanOrEqual))
                continue;
            break;
        case DuelAIRow_SpecialTry_OpponentHP_LTE:
            if (not ai_comp_hp(self, row, DuelAICompOp_LessThanOrEqual, true))
                continue;
            break;
        case DuelAIRow_SpecialTry_AnkiOrMusou:
            if (not ai_is_special_enabled(self, DuelSpecial_Anki) and not ai_is_special_enabled(self, DuelSpecial_Musou))
                continue;
            for (int i = 0; i < DuelBuffType_Max; i++)
            {
                if (ai_has_buff(self, i, true))
                    return true;
            }
            continue;
        case DuelAIRow_SpecialTry_Kyuusho:
            if (not ai_is_special_enabled(self, DuelSpecial_Kyuusho))
                continue;
            break;
        case DuelAIRow_SpecialTry_Kiai:
            if (not ai_is_special_enabled(self, DuelSpecial_Kiai))
                continue;
            // 이미 공격 버프 상태
            if (ai_has_buff(self, DuelBuffType_Attack))
                continue;
            break;
        case DuelAIRow_SpecialTry_Kenshu:
            if (not ai_is_special_enabled(self, DuelSpecial_Kenshu))
                continue;
            // 이미 방어 버프 상태
            if (ai_has_buff(self, DuelBuffType_Defense))
                continue;
            break;
        case DuelAIRow_SpecialTry_Always:
            break;
        case DuelAIRow_SpecialTry_Stop:
            return false;
        }
        return true;
    }
    return false;
}

/// <summary>4fbb00</summary>
bool Duel::ai_init(AI* self, duel_team_t team)
{
    self->parent = this;
    self->team = team;
    self->opponent_team = get_opponent_team(team);
    ai_update_chara(self);
    self->initialized = true;
    return true;
}

/// <summary>4fbb70</summary>
int Duel::ai_get_power(const AI* self, duel_team_t team, int chara) const
{
    int hp_coef = (ai_get_hp(self, team, chara) + 9) / 10; // 0 .. 10
    int strength_coef = (self->parent->get_strength(team, chara, true) + 4) / 5; // 1 .. 22
    Person* person = self->parent->get_person(team, chara);
    if (not utils::is_alive(person))
        return 0;
    int n = (hp_coef + 20) * strength_coef * strength_coef * 96 / 10; // 192 .. 139392
    n += system_->get_duel_item_power(person); // 0 .. 30
    if (n < 1)
        return 1;
    return n;
}

/// <summary>4fbc60</summary>
int Duel::ai_get_best_chara(const AI* self, duel_team_t team) const
{
    int best_power = 0;
    int best = -1;
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        if (not self->parent->check_state(team, i, -1))
            continue;
        int power = ai_get_power(self, team, i);
        if (best_power < power)
        {
            best_power = power;
            best = i;
        }
    }
    return best;
}

/// <summary>4fbcd0</summary>
duel_sp_t Duel::ai_random_special(const AI* self) const
{
    std::array<int, DuelSpecial_Max> weight = {};
    int weight_sum = 0;
    for (int i = 0; i < DuelSpecial_Max; i++)
    {
        if (not ai_is_special_enabled(self, i))
            continue;
        switch (i)
        {
        case DuelSpecial_Hissatsuwaza:
            weight_sum += 20;
            break;
        case DuelSpecial_Kiai:
            if (ai_has_buff(self, DuelBuffType_Attack))
                continue;
            weight_sum += 3;
            break;
        case DuelSpecial_Kenshu:
            if (ai_has_buff(self, DuelBuffType_Defense))
                continue;
            weight_sum += 3;
            break;
        case DuelSpecial_Taikyaku:
            continue;
        case DuelSpecial_Kyuusho:
            weight_sum += 20;
            break;
        case DuelSpecial_Musou:
            weight_sum += 60;
            break;
        case DuelSpecial_Anki:
            weight_sum += 20;
            break;
        case DuelSpecial_Nisetaikyaku:
            weight_sum += 20;
            break;
        }
        weight[i] = weight_sum;
    }
    int n = system_->rand_int(weight_sum);
    for (int i = 0; i < DuelSpecial_Max; i++)
    {
        if (weight[i] > n)
            return i;
    }
    return -1;
}

/// <summary>4fbe00</summary>
int Duel::ai_calc_switch(const AI* self) const
{
    bool can_switch = false;
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        if (is_joined(self->team, i) and i != self->chara)
        {
            can_switch = true;
            break;
        }
    }
    if (not can_switch)
        return -1;

    int hp = ai_get_hp(self);
    int timer = get_switching_timer(self->team);
    // 체력이 20 이하일 경우 타이머 완화
    if (not timer or (hp <= 20 and timer < 3))
        ;
    else
        return -1;

    int best_chara = ai_get_best_chara(self, self->team);
    if (not utils::in_range(best_chara, 0, MaxTeamCharaCount - 1))
        return -1;

    for (const AI::Row* row = ai_get_table(self, DuelAITable_Switch); utils::in_range(row->id, 0, DuelAIRow_Max - 1); row++)
    {
        // 체력이 낮을 수록 확률 2배까지 증가
        int chance = row->chance;
        if (hp <= 30)
            chance = chance * std::min(45 - hp, 30) / 15;
        if (not system_->rand_bool(chance))
            continue;

        // 체력이 1/3 이상이고 버프 상태라면 교대 안함
        if (hp >= 33)
        {
            for (int i = 0; i < DuelBuffType_Max; i++)
            {
                if (has_buff(self->team, i))
                    return -1;
            }
        }

        switch (row->id)
        {
        case DuelAIRow_Switch_HP_LTE:
            if (not ai_comp_hp(self, row, DuelAICompOp_LessThanOrEqual))
                continue;
            return best_chara;
        case DuelAIRow_Switch_Kunshu:
            if (not get_person(self->team, self->chara)->is_kunshu())
                continue;
            return best_chara;
        case DuelAIRow_Switch_StrengthDiff_LTE:
            if (get_strength(self->opponent_team, self->opponent_chara, true) - get_strength(self->team, self->chara, true) <= row->param1)
                continue;
            return best_chara;
        case DuelAIRow_Switch_NotBestChara:
            if (self->chara == best_chara)
                continue;
            return best_chara;
        case DuelAIRow_Switch_Stop_Invulnerable:
            if (not is_invulnerable(self->team, self->chara))
                continue;
            return -1;
        }
    }
    return -1;
}

/// <summary>4fc1a0</summary>
bool Duel::ai_comp_power(const AI* self, duel_ai_comp_op_t op, duel_team_t a_team, int a_chara, duel_team_t b_team, int b_chara) const
{
    int a = ai_get_power(self, a_team, a_chara);
    int b = ai_get_power(self, b_team, b_chara);
    return ai_comp(self, op, a, b);
}

/// <summary>4fc1e0</summary>
duel_sp_t Duel::ai_calc_special(const AI* self) const
{
    // 사용 가능한 필살 없음
    if (not self->parent->can_special(self->team, self->chara))
        return -1;
    for (const AI::Row* row = ai_get_table(self, DuelAITable_Special); utils::in_range(row->id, 0, DuelAIRow_Max - 1); row++)
    {
        if (not system_->rand_bool(row->chance))
            continue;
        duel_sp_t sp = -1;
        bool buff = false;
        switch (row->id)
        {
        case DuelAIRow_Special_Nisetaikyaku:
            sp = DuelSpecial_Nisetaikyaku;
            break;
        case DuelAIRow_Special_Anki:
            sp = DuelSpecial_Anki;
            break;
        case DuelAIRow_Special_Kyuusho:
            sp = DuelSpecial_Kyuusho;
            break;
        case DuelAIRow_Special_AnkiOrMusou:
            if (ai_is_special_enabled(self, DuelSpecial_Anki))
                sp = DuelSpecial_Anki;
            else if (ai_is_special_enabled(self, DuelSpecial_Musou))
                sp = DuelSpecial_Musou;
#if s11_suspicious(0)
            // 원래 의도는 버프를 가지고 있으면 continue 하려고 한것 같음
            if (true)
                continue;
#endif
            for (int i = 0; i < DuelBuffType_Max; i++)
            {
                if (ai_has_buff(self, i))
                {
                    buff = true;
                    break;
                }
            }
            if (buff)
                continue;
            break;
        case DuelAIRow_Special_Kiai:
            if (ai_has_buff(self, DuelBuffType_Attack))
                continue;
            sp = DuelSpecial_Kiai;
            break;
        case DuelAIRow_Special_Kenshu:
            if (ai_has_buff(self, DuelBuffType_Defense))
                continue;
            sp = DuelSpecial_Kenshu;
            break;
        case DuelAIRow_Special_Taikyaku:
            if (ai_comp_hp(self, row, DuelAICompOp_LessThanOrEqual))
            {
                AI::Row row2 = { row->chance, row->id, row->param1 + 10, row->param2 };
                if (not ai_comp_hp(self, &row2, DuelAICompOp_LessThanOrEqual, true))
                {
                    int chara = ai_calc_switch(self);
                    // 교체 가능한 무장 있음
                    if (utils::in_range(chara, 0, MaxTeamCharaCount - 1) and chara != self->chara)
                        continue;
                    sp = DuelSpecial_Taikyaku;
                    break;
                }
            }
            continue;
        case DuelAIRow_Special_Random:
            sp = ai_random_special(self);
            break;
        }
        if (not ai_is_special_enabled(self, sp))
            continue;
        return sp;
    }
    return -1;
}

/// <summary>4fc5a0</summary>
duel_stance_t Duel::ai_calc_stance(const AI* self) const
{
    int best_chara = ai_get_best_chara(self, self->team);
    int opponent_best_chara = ai_get_best_chara(self, self->opponent_team);
    for (const AI::Row* row = ai_get_table(self, DuelAITable_Stance); utils::in_range(row->id, 0, DuelAIRow_Max - 1); row++)
    {
        if (not system_->rand_bool(row->chance))
            continue;
        switch (row->id)
        {
        case DuelAIRow_Stance_A_Always:
            return DuelStance_Attack;
        case DuelAIRow_Stance_A_HP_GTE:
            if (not ai_comp_hp(self, row, DuelAICompOp_GreaterThanOrEqual))
                continue;
            return DuelStance_Attack;
        case DuelAIRow_Stance_A_LowHP:
            if (ai_get_hp(self) * 2 > ai_get_hp(self, true))
                continue;
            return DuelStance_Attack;
        case DuelAIRow_Stance_A_OpponentHP_LTE:
            if (not ai_comp_hp(self, row, DuelAICompOp_LessThanOrEqual, true))
                continue;
            return DuelStance_Attack;
        case DuelAIRow_Stance_A_OpponentBestChara:
            if (self->opponent_chara != opponent_best_chara)
                continue;
            return DuelStance_Attack;
        case DuelAIRow_Stance_A_Invulnerable:
            if (not is_invulnerable(self->team, self->chara))
                continue;
            return DuelStance_Attack;
        case DuelAIRow_Stance_D_BestChara:
            if (self->chara != best_chara)
                continue;
            return DuelStance_Defense;
        case DuelAIRow_Stance_D_BlowCounter_GTE:
            if (blow_counter_ < row->param1)
                continue;
            return DuelStance_Defense;
        case DuelAIRow_Stance_S_HP_GTE:
            if (not ai_comp_hp(self, row, DuelAICompOp_GreaterThanOrEqual))
                continue;
            return DuelStance_Spirit;
        case DuelAIRow_Stance_S_Weak:
            if (not ai_comp_power(self, DuelAICompOp_GreaterThanOrEqual, self->opponent_team, self->opponent_chara, self->team, self->chara))
                continue;
            return DuelStance_Spirit;
        case DuelAIRow_Stance_S_NotBestChara:
            if (self->chara == best_chara)
                continue;
            return DuelStance_Spirit;
        case DuelAIRow_Stance_S_OpponentNotBestChara:
            if (self->opponent_chara == opponent_best_chara)
                continue;
            return DuelStance_Spirit;
        case DuelAIRow_Stance_S_HasNotAttackBuff:
            if (ai_has_buff(self, DuelBuffType_Attack))
                continue;
            return DuelStance_Spirit;
        case DuelAIRow_Stance_S_HasNotDefenseBuff:
            if (ai_has_buff(self, DuelBuffType_Defense))
                continue;
            return DuelStance_Spirit;
        case DuelAIRow_Stance_F_Always:
            return DuelStance_Fury;
        case DuelAIRow_Stance_Stop_StanceTimer_GTE:
            if (get_stance_timer(self->team) < row->param1)
                continue;
            return -1;
        }
    }
    return -1;
}

s11_end_namespace