#include "pch.h"

s11_begin_namespace

namespace {

// 8b2568, 8b2574
constexpr std::array<debate_card_t, Wadai_Max> Wadai3Card = {
    DebateCard_Fact3, DebateCard_Logic3, DebateCard_Time3,
};

// 8B25a4
constexpr std::array<std::array<debate_card_t, 3>, Wadai_Max> WadaiCardDesc = { {
    { DebateCard_Fact3, DebateCard_Fact2, DebateCard_Fact1, },
    { DebateCard_Logic3, DebateCard_Logic2, DebateCard_Logic1, },
    { DebateCard_Time3, DebateCard_Time2, DebateCard_Time1, },
} };

// 8b2580, 8b25c8, 8b25ec
constexpr std::array<std::array<debate_card_t, 3>, Wadai_Max> WadaiCardAsc = { {
    { DebateCard_Fact1, DebateCard_Fact2, DebateCard_Fact3, },
    { DebateCard_Logic1, DebateCard_Logic2, DebateCard_Logic3, },
    { DebateCard_Time1, DebateCard_Time2, DebateCard_Time3, },
} };

} // namespace

/// <summary>516fc0</summary>
void Debate::ai_init(AI* self, Debate* parent, debate_team_t team)
{
    self->parent = parent;
    self->team = team;
    self->opponent_team = get_opponent_team(team);
}

/// <summary>516fe0</summary>
debate_critical_t Debate::ai_calc_critical(AI* self)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    Person* person = chara->person;
    Person* opponent_person = opponent_chara->person;
    if (not utils::is_alive(person))
        return system_->rand_int(DebateCritical_Max);
    if (person->is_hate(opponent_person->get_id()))
        return DebateCritical_PushOn;
    if (person->is_like(opponent_person->get_id()))
        return DebateCritical_HaveMercy;
    return system_->rand_int(DebateCritical_Max);
}

/// <summary>517060</summary>
int Debate::ai_get_card_count(AI* self, debate_card_t card)
{
    if (not is_card_available(self->team, card))
        return 0;
    Character* chara = get_chara(self->team);
    int n = 0;
    for (int i = 0; i < chara->max_card_count; i++)
    {
        if (chara_get_card(chara, i) == card)
            n++;
    }
    return n;
}

/// <summary>5170c0</summary>
int Debate::ai_cond_card_anger_goutan(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);

    if (chara->anger_timer <= 0)
        return -1;
    if (chara_get_seikaku(chara) != Seikaku_Goutan)
        return -1;

    int best = -1;
    int best_level = std::numeric_limits<int>::max();
    for (int i = 0; i < chara->max_card_count; i++)
    {
        debate_card_t card = chara_get_card(chara, i);
        if (not utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast))
            continue;
        int level = get_card_level(card);
        if (level < best_level or (best_level == level and system_->rand_bool(50)))
        {
            best_level = level;
            best = i;
        }
    }
    return best;
}

/// <summary>517190</summary>
int Debate::ai_cond_card_anger_reisei_rethink(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;

    if (chara->anger_timer <= 0)
        return -1;
    if (chara_get_seikaku(chara) != Seikaku_Reisei)
        return -1;

    int n = 0;
    n += ai_get_card_count(self, Wadai3Card[self->parent->wadai_]);
    n += ai_get_card_count(self, DebateCard_Mushi);
    n += ai_get_card_count(self, DebateCard_Kiben);
    n += ai_get_card_count(self, DebateCard_Daikatsu);
    if (not n)
    {
        card_index = chara_get_card_index(chara, DebateCard_Rethink);
        if (card_index >= 0 and is_card_available(self->team, DebateCard_Rethink))
            return card_index;
    }
    if (system_->rand_bool(30))
    {
        card_index = chara_get_card_index(chara, DebateCard_Rethink);
        if (card_index >= 0 and is_card_available(self->team, DebateCard_Rethink))
            return card_index;
    }
    return -1;
}

/// <summary>517270</summary>
int Debate::ai_cond_card_vs_anger_goutan(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;
    int chinsei_card_count;
    int gyakujou_card_count;

    if (opponent_chara->anger_timer <= 0)
        return -1;
    if (chara_get_seikaku(opponent_chara) != Seikaku_Goutan)
        return -1;

    card_index = chara_get_card_index(chara, DebateCard_Daikatsu);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Daikatsu))
        return card_index;

    card_index = chara_get_card_index(chara, DebateCard_Mushi);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Mushi))
        return card_index;

    chinsei_card_count = ai_get_card_count(self, DebateCard_Chinsei);
    gyakujou_card_count = ai_get_card_count(self, DebateCard_Gyakujou);
#if s11_suspicious(2)
    // >= 비교를 해야하는게 아닌지?
#endif
    if (chinsei_card_count > 1 and gyakujou_card_count > 1)
    {
        card_index = chara_get_card_index(chara, DebateCard_Gyakujou);
        if (card_index >= 0 and is_card_available(self->team, DebateCard_Gyakujou))
            return card_index;
    }
    else if (chinsei_card_count > 2 and not gyakujou_card_count)
    {
        card_index = chara_get_card_index(chara, DebateCard_Chinsei);
        if (card_index >= 0 and is_card_available(self->team, DebateCard_Chinsei))
            return card_index;
    }

    int best = -1;
    int best_level = std::numeric_limits<int>::max();
    for (int i = 0; i < chara->max_card_count; i++)
    {
        debate_card_t card = chara_get_card(chara, i);
        if (not utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast))
            continue;
        if (not is_card_available(self->team, card))
            continue;
        int level = get_card_level(card);
        if (level < best_level or (best_level == level and system_->rand_bool(50)))
        {
            best_level = level;
            best = i;
        }
    }
    return best;
}

/// <summary>5173f0</summary>
int Debate::ai_cond_card_vs_anger_reisei(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;

    if (opponent_chara->anger_timer <= 0)
        return -1;
    if (chara_get_seikaku(opponent_chara) != Seikaku_Reisei)
        return -1;

    card_index = chara_get_card_index(chara, DebateCard_Mushi);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Mushi))
        return card_index;
    return -1;
}

/// <summary>517450</summary>
int Debate::ai_cond_card_anger_reisei(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;

    if (chara->anger_timer <= 0)
        return -1;
    if (chara_get_seikaku(chara) != Seikaku_Reisei)
        return -1;

    card_index = chara_get_card_index(chara, DebateCard_Daikatsu);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Daikatsu))
        return card_index;

    card_index = chara_get_card_index(chara, DebateCard_Kiben);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Kiben))
        return card_index;

    card_index = chara_get_card_index(chara, Wadai3Card[self->parent->wadai_]);
    if (card_index >= 0 and is_card_available(self->team, Wadai3Card[self->parent->wadai_]))
        return card_index;
    return -1;
}

/// <summary>517520</summary>
int Debate::ai_cond_card_daikatsu_kiben_mushi(AI* self, int type, int chance)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;
    debate_card_t opponent_played_card;

    switch (type)
    {
    case 0:
        // 확률
        if (not system_->rand_bool(chance))
            return -1;
        break;
    case 1:
        // 성격이 소심일 경우에만
        if (chara_get_seikaku(chara) != Seikaku_Shoushin)
            return -1;
        break;
    case 2:
        // 선공이고 화술 카드 대상일 경우 무시
        opponent_played_card = get_played_card(self->opponent_team);
        if (not utils::in_range(opponent_played_card, 0, DebateCard_Max - 1))
            return -1;
        if (utils::in_range(opponent_played_card, DebateCard_WadaiFirst, DebateCard_WadaiLast))
            return -1;
        card_index = chara_get_card_index(chara, DebateCard_Mushi);
        if (card_index >= 0 and is_card_available(self->team, DebateCard_Mushi))
            return card_index;
        break;
    }

    card_index = chara_get_card_index(chara, DebateCard_Daikatsu);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Daikatsu))
        return card_index;

    card_index = chara_get_card_index(chara, DebateCard_Kiben);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Kiben))
        return card_index;

    if (type == 2)
        return -1;

    card_index = chara_get_card_index(chara, DebateCard_Mushi);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Mushi))
        return card_index;
    return -1;
}

/// <summary>517650</summary>
int Debate::ai_cond_card_low_wadai(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;
    int opponent_wadai_card_count = 0;

    // 상대가 화제에 맞는 카드 가지고 있음
    for (int i = 0; i < opponent_chara->max_card_count; i++)
    {
        debate_card_t card = chara_get_card(opponent_chara, i);
        if (utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast) and get_card_wadai(card) == self->parent->wadai_)
            opponent_wadai_card_count++;
    }
    if (opponent_wadai_card_count)
        return -1;

    for (int i = 0; i < 3; i++)
    {
        card_index = chara_get_card_index(chara, WadaiCardAsc[self->parent->wadai_][i]);
        if (card_index >= 0 and is_card_available(self->team, WadaiCardAsc[self->parent->wadai_][i]))
            return card_index;
    }
    return -1;
}

/// <summary>517720</summary>
int Debate::ai_find_card_high_wadai(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;

    for (int i = 0; i < 3; i++)
    {
        card_index = chara_get_card_index(chara, WadaiCardDesc[self->parent->wadai_][i]);
        if (card_index >= 0 and is_card_available(self->team, WadaiCardDesc[self->parent->wadai_][i]))
            return card_index;
    }
    return -1;
}

/// <summary>5177b0</summary>
int Debate::ai_find_card_low_wadai(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;

    for (int i = 0; i < 3; i++)
    {
        card_index = chara_get_card_index(chara, WadaiCardAsc[self->parent->wadai_][i]);
        if (card_index >= 0 and is_card_available(self->team, WadaiCardAsc[self->parent->wadai_][i]))
            return card_index;
    }
    return -1;
}

/// <summary>517840</summary>
int Debate::ai_cond_card_gyakujou(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;
    int chance;

    // 분노 수치에 따른 확률
    if (chara->stress >= 70)
        chance = 100;
    else if (chara->stress >= 60)
        chance = 40;
    else if (chara->stress >= 50)
        chance = 10;
    else
        chance = 0;
    if (not system_->rand_bool(chance))
        return -1;

    card_index = chara_get_card_index(chara, DebateCard_Gyakujou);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Gyakujou))
        return card_index;
    return -1;
}

/// <summary>5178c0</summary>
int Debate::ai_cond_card_chinsei(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;
    int chinsei_card_count;
    int gyakujou_card_count;

    chinsei_card_count = ai_get_card_count(self, DebateCard_Chinsei);
    gyakujou_card_count = ai_get_card_count(self, DebateCard_Gyakujou);
    // 흥분 카드가 없고, 진정 카드가 한장 이하일 경우
    if (not gyakujou_card_count and chinsei_card_count <= 1)
        return -1;
    // 체력 200 미만
    if (chara->hp < 200)
        return -1;
    // 50%
    if (not system_->rand_bool(50))
        return -1;

    card_index = chara_get_card_index(chara, DebateCard_Chinsei);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Chinsei))
        return card_index;
    return -1;
}

/// <summary>517950</summary>
int Debate::ai_cond_card_rethink(AI* self, int ignore_opponent, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;
    int wadai_card_count = 0;
    int opponent_wadai_card_count = 0;
    int daikatsu_card_count;
    int kiben_card_count;
    int mushi_card_count;
    int chance;

    for (int i = 0; i < chara->max_card_count; i++)
    {
        debate_card_t card = chara_get_card(chara, i);
        if (utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast) and get_card_wadai(card) == self->parent->wadai_)
            wadai_card_count++;
    }
    for (int i = 0; i < opponent_chara->max_card_count; i++)
    {
        debate_card_t card = chara_get_card(opponent_chara, i);
        if (utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast) and get_card_wadai(card) == self->parent->wadai_)
            opponent_wadai_card_count++;
    }

    // 화제 카드 있음
    if (wadai_card_count > 0)
        return -1;
    // 상대 화제 카드 없음
    if (not ignore_opponent and not opponent_wadai_card_count)
        return -1;

    daikatsu_card_count = ai_get_card_count(self, DebateCard_Daikatsu);
    kiben_card_count = ai_get_card_count(self, DebateCard_Kiben);
    mushi_card_count = ai_get_card_count(self, DebateCard_Mushi);
    chance = ignore_opponent ? 100 : 70;
    // 화술 카드가 있다면 확률 반감
    if (daikatsu_card_count + kiben_card_count + mushi_card_count)
        chance /= 2;
    if (not system_->rand_bool(chance))
        return - 1;

    card_index = chara_get_card_index(chara, DebateCard_Rethink);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Rethink))
        return card_index;
    return -1;
}

/// <summary>517b70</summary>
int Debate::ai_find_card_rethink(AI* self, int chance, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;

    if (not system_->rand_bool(chance))
        return -1;

    card_index = chara_get_card_index(chara, DebateCard_Rethink);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Rethink))
        return card_index;
    return -1;
}

/// <summary>517bd0</summary>
int Debate::ai_cond_card_mushi(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;

    // 상대 흥분 70 미만
    if (opponent_chara->stress < 70)
        return -1;
    // 흥분 카드 있음
    if (not ai_get_card_count(self, DebateCard_Gyakujou))
        return -1;

    card_index = chara_get_card_index(chara, DebateCard_Mushi);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Mushi))
        return card_index;
    return -1;
}

/// <summary>517c30</summary>
int Debate::ai_find_card_high_any_wadai(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);

    int best = -1;
    int best_level = std::numeric_limits<int>::min();
    for (int i = 0; i < chara->max_card_count; i++)
    {
        debate_card_t card = chara_get_card(chara, i);
        if (not utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast))
            continue;
        if (not is_card_available(self->team, card))
            continue;
        int level = get_card_level(card);
        if (level > best_level or (best_level == level and system_->rand_bool(50)))
        {
            best_level = level;
            best = i;
        }
    }
    return best;
}

/// <summary>517ce0</summary>
int Debate::ai_rand_card_daikatsu_kiben_mushi_wadai(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    int card_index;
    std::array<int, MaxCardCount> table;
    int count = 0;

    card_index = chara_get_card_index(chara, DebateCard_Daikatsu);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Daikatsu))
        table[count++] = card_index;

    card_index = chara_get_card_index(chara, DebateCard_Kiben);
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Kiben))
        table[count++] = card_index;

#if s11_suspicious(1)
    card_index = chara_get_card_index(chara, DebateCard_Kiben);
#else
    card_index = chara_get_card_index(chara, DebateCard_Mushi);
#endif
    if (card_index >= 0 and is_card_available(self->team, DebateCard_Mushi))
        table[count++] = card_index;

    for (int i = 0; i < 3; i++)
    {
        card_index = chara_get_card_index(chara, WadaiCardAsc[self->parent->wadai_][i]);
        if (card_index >= 0 and is_card_available(self->team, WadaiCardAsc[self->parent->wadai_][i]))
            table[count++] = card_index;
    }

    if (not count)
        return -1;
#if s11_suspicious(2)
    // >= 비교를 해야하는게 아닌지?
#endif
    if (count > 2)
    {
        for (int i = 0; i < 1000; i++)
        {
            int a = system_->rand_int(count);
            int b = system_->rand_int(count);
            if (a != b)
                std::swap(table[a], table[b]);
        }
    }
    return table[0];
}

/// <summary>517e50</summary>
int Debate::ai_rand_card(AI* self, int, int)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    std::array<int, MaxCardCount> table;
    int count = 0;

    for (int i = 0; i < chara->max_card_count; i++)
    {
        debate_card_t card = chara_get_card(chara, i);
        if (not utils::in_range(card, 0, DebateCard_Max - 1))
            continue;
        if (not is_card_available(self->team, card))
            continue;
        table[count++] = i;
    }

#if s11_suspicious(2)
    // >= 비교를 해야하는게 아닌지?
#endif
    if (count > 2)
    {
        for (int i = 0; i < 1000; i++)
        {
            int a = system_->rand_int(count);
            int b = system_->rand_int(count);
            if (a != b)
                std::swap(table[a], table[b]);
        }
    }
    return table[0];
}

/// <summary>517ef0</summary>
int Debate::ai_calc_card(AI* self)
{
    Character* chara = get_chara(self->team);
    Character* opponent_chara = get_chara(self->opponent_team);
    debate_card_t opponent_played_card;
    wadai_t opponent_card_wadai = -1;
    int card_index;
    const AI::Row* table;
    int count;

    opponent_played_card = get_played_card(self->opponent_team);
    if (utils::in_range(opponent_played_card, 0, DebateCard_Max - 1))
        opponent_card_wadai = get_card_wadai(opponent_played_card);
    // 화제 카드 상대
    if (utils::in_range(opponent_played_card, DebateCard_WadaiFirst, DebateCard_WadaiLast) and opponent_card_wadai == self->parent->wadai_)
    {
        // 8b2368
        static constexpr AI::Row Table[] = {
            { &Debate::ai_cond_card_anger_goutan, 0, 0 },
            { &Debate::ai_cond_card_anger_reisei_rethink, 0, 0 },
            { &Debate::ai_cond_card_vs_anger_goutan, 0, 0 },
            { &Debate::ai_cond_card_vs_anger_reisei, 0, 0 },
            { &Debate::ai_cond_card_daikatsu_kiben_mushi, 0, 60 },
            { &Debate::ai_cond_card_rethink, 1, 0 },
            { &Debate::ai_find_card_high_wadai, 0, 0 },
            { &Debate::ai_find_card_rethink, 30, 0 },
            { &Debate::ai_cond_card_daikatsu_kiben_mushi, 0, 100 },
            { &Debate::ai_cond_card_chinsei, 0, 0 },
            { &Debate::ai_find_card_high_any_wadai, 0, 0 },
        };
        table = Table;
        count = std::extent<decltype(Table)>::value;
    }
    // 화술 카드, 다른 화제 카드 상대
    else if (utils::in_range(opponent_played_card, 0, DebateCard_Max - 1) and opponent_card_wadai != self->parent->wadai_)
    {
        // 8b23f0
        static constexpr AI::Row Table[] = {
            { &Debate::ai_cond_card_anger_goutan, 0, 0 },
            { &Debate::ai_cond_card_anger_reisei_rethink, 0, 0 },
            { &Debate::ai_cond_card_vs_anger_goutan, 0, 0 },
            { &Debate::ai_cond_card_vs_anger_reisei, 0, 0 },
            { &Debate::ai_cond_card_daikatsu_kiben_mushi, 2, 0 },
            { &Debate::ai_cond_card_daikatsu_kiben_mushi, 0, 30 },
            { &Debate::ai_cond_card_rethink, 0, 0 },
            { &Debate::ai_find_card_low_wadai, 0, 0 },
            { &Debate::ai_find_card_rethink, 30, 0 },
            { &Debate::ai_cond_card_daikatsu_kiben_mushi, 0, 100 },
            { &Debate::ai_cond_card_chinsei, 0, 0 },
            { &Debate::ai_find_card_high_any_wadai, 0, 0 },
        };
        table = Table;
        count = std::extent<decltype(Table)>::value;
    }
    // 선공
    else
    {
        // 8b2480
        static constexpr AI::Row Table[] = {
            { &Debate::ai_cond_card_anger_goutan, 0, 0 },
            { &Debate::ai_cond_card_anger_reisei_rethink, 0, 0 },
            { &Debate::ai_cond_card_vs_anger_goutan, 0, 0 },
            { &Debate::ai_cond_card_vs_anger_reisei, 0, 0 },
            { &Debate::ai_cond_card_low_wadai, 0, 0 },
            { &Debate::ai_cond_card_anger_reisei, 0, 0 },
            { &Debate::ai_cond_card_rethink, 0, 0 },
            { &Debate::ai_cond_card_mushi, 0, 0 },
            { &Debate::ai_cond_card_daikatsu_kiben_mushi, 1, 0 },
            { &Debate::ai_cond_card_daikatsu_kiben_mushi, 0, 40 },
            { &Debate::ai_find_card_high_wadai, 0, 0 },
            { &Debate::ai_cond_card_daikatsu_kiben_mushi, 0, 100 },
            { &Debate::ai_cond_card_gyakujou, 0, 0 },
            { &Debate::ai_find_card_rethink, 30, 0 },
            { &Debate::ai_cond_card_chinsei, 0, 0 },
            { &Debate::ai_find_card_high_any_wadai, 0, 0 },
        };
        table = Table;
        count = std::extent<decltype(Table)>::value;
    }

    // 지력60 미만
    int my_int = chara_get_intelligence(chara);
    int chance = std::max(60 - my_int, 0) / 2;
    if (system_->rand_bool(chance))
    {
        // 8b2540
        static constexpr AI::Row Table[] = {
            { &Debate::ai_rand_card_daikatsu_kiben_mushi_wadai, 0, 0 },
            { &Debate::ai_find_card_rethink, 50, 0 },
            { &Debate::ai_rand_card, 0, 0 },
        };
        table = Table;
        count = std::extent<decltype(Table)>::value;
    }

    if (system_->rand_bool(chance))
        return ai_rand_card(self, 0, 0);
    for (int i = 0; i < count; i++)
    {
#if s11_log
        if (Logger* logger = system_->get_logger())
        {
            logger->debug(std::format("ai_calc_card {} {} 0x{:x}",
                i,
                count,
                system_->get_seed()
            ));
        }
#endif
        card_index = (this->*table[i].func)(self, table[i].param1, table[i].param2);
        if (card_index >= 0)
            return card_index;
    }
    return ai_rand_card(self, 0, 0);
}

s11_end_namespace