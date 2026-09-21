#pragma once

s11_begin_namespace

class System;

/// <summary></summary>
class Debate
{
public:
    static constexpr int MaxTeamCount = 2;
    static constexpr int MinHP = -100;
    static constexpr int MaxHP = 1000;
    static constexpr int MaxStress = 100;
    static constexpr int DeckTableSize = 18;
    static constexpr int MaxCardCount = 7;

    struct Param
    {
        struct Character
        {
            Person* person = nullptr; // 0
            int hp = MaxHP; // 4
            bool control = false; // 8
        };
        std::array<Character, MaxTeamCount> chara; // 0
        bool reverse = false; // 18
        debate_team_t winner = -1; // 1c 이긴팀
        debate_win_type_t win_type = DebateWinType_Normal; // 20
        bool tutorial = false; // 24
        bool finished = false; // 28
        scene_t ret_scene = Scene_Max; // 2c
    };

    struct Deck
    {
        std::array<debate_card_t, DeckTableSize> table; // 0
        int index; // 48
        std::array<debate_card_t, Wajutsu_Max> wajutsu_card; // 4c 사용 가능한 화술 카드
        int wajutsu_card_count; // 60

        /// <summary>51db00</summary>
        Deck()
        {
            table.fill(-1);
            index = 0;
            wajutsu_card.fill(-1);
            wajutsu_card_count = 0;
        }
    };

    struct Character
    {
        Person* person; // 0 무장
        int hp; // 4 체력
        int stress; // 8 분노
        int anger_timer; // c 흥분 타이머 -
        int attack; // 10 공격력
        std::array<debate_card_t, MaxCardCount> card; // 14 패
        int max_card_count; // 30 최대 패 수
        Deck deck; // 34
        bool control; // 98 플레이어
        seikaku_t seikaku; // 9c 성격

        /// <summary>51dd00</summary>
        Character()
        {
            person = nullptr;
            hp = 0;
            stress = 0;
            anger_timer = 0;
            attack = 0;
            max_card_count = 0;
            control = false;
            seikaku = -1;
            card.fill(-1);
        }
    };

    struct AI
    {
        struct Row
        {
            int (Debate::* func)(AI* self, int param1, int param2) = nullptr; // 0
            int param1 = 0; // 4
            int param2 = 0; // 8
        };

        Debate* parent = nullptr; // 0
        debate_team_t team = -1; // 4
        debate_team_t opponent_team = -1; // 8
    };

    Debate(System* system, Param* param);

    /// <summary>475810</summary>
    void none();

    /// <summary>516fc0</summary>
    void ai_init(AI* self, Debate* parent, debate_team_t team);
    /// <summary>516fe0</summary>
    debate_critical_t ai_calc_critical(AI* self);
    /// <summary>517060</summary>
    int ai_get_card_count(AI* self, debate_card_t card);
    /// <summary>5170c0. 대담(흥분)</summary>
    int ai_cond_card_anger_goutan(AI* self, int, int);
    /// <summary>517190. 냉정(흥분) 재고</summary>
    int ai_cond_card_anger_reisei_rethink(AI* self, int, int);
    /// <summary>517270. 대담(흥분) 상대</summary>
    int ai_cond_card_vs_anger_goutan(AI* self, int, int);
    /// <summary>5173f0. 냉정(흥분) 상대</summary>
    int ai_cond_card_vs_anger_reisei(AI* self, int, int);
    /// <summary>517450. 냉정(흥분)</summary>
    int ai_cond_card_anger_reisei(AI* self, int, int);
    /// <summary>517520</summary>
    int ai_cond_card_daikatsu_kiben_mushi(AI* self, int type, int chance);
    /// <summary>517650</summary>
    int ai_cond_card_low_wadai(AI* self, int, int);
    /// <summary>517720</summary>
    int ai_find_card_high_wadai(AI* self, int, int);
    /// <summary>5177b0</summary>
    int ai_find_card_low_wadai(AI* self, int, int);
    /// <summary>517840</summary>
    int ai_cond_card_gyakujou(AI* self, int, int);
    /// <summary>5178c0</summary>
    int ai_cond_card_chinsei(AI* self, int, int);
    /// <summary>517950</summary>
    int ai_cond_card_rethink(AI* self, int ignore_opponent, int);
    /// <summary>517b70</summary>
    int ai_find_card_rethink(AI* self, int chance, int);
    /// <summary>517bd0</summary>
    int ai_cond_card_mushi(AI* self, int, int);
    /// <summary>517c30</summary>
    int ai_find_card_high_any_wadai(AI* self, int, int);
    /// <summary>517ce0</summary>
    int ai_rand_card_daikatsu_kiben_mushi_wadai(AI* self, int, int);
    /// <summary>517e50</summary>
    int ai_rand_card(AI* self, int, int);
    /// <summary>517ef0</summary>
    int ai_calc_card(AI* self);

    /// <summary>51db90</summary>
    void deck_generate(Deck* self);
    /// <summary>51dd50</summary>
    static int get_max_card_count(Person* person);
    /// <summary>51dd90</summary>
    int chara_get_intelligence(const Character* self) const;
    /// <summary>51ddc0</summary>
    int chara_get_strength(const Character* self) const;
    /// <summary>51ddf0</summary>
    seikaku_t chara_get_seikaku(const Character* self) const;
    /// <summary>51de20</summary>
    void chara_set_anger_timer(Character* self);
    /// <summary>51de80</summary>
    void chara_dec_anger_timer(Character* self);
    /// <summary>51de90</summary>
    void chara_reset_anger_timer(Character* self);
    /// <summary>51dea0</summary>
    void chara_sort_cards(Character* self);
    /// <summary>51def0</summary>
    void chara_fill_cards(Character* self);
    /// <summary>51dfb0</summary>
    void chara_set_card(Character* self, int index, debate_card_t card);
    /// <summary>51dfc0</summary>
    debate_card_t chara_get_card(const Character* self, int index) const;
    /// <summary>51dfe0</summary>
    void chara_remove_card(Character* self, int index);
    /// <summary>51e070</summary>
    int chara_get_card_index(const Character* self, debate_card_t card) const;
    /// <summary>51e0b0</summary>
    int chara_get_empty_card_index(const Character* self) const;
    /// <summary>51e0e0</summary>
    void chara_rethink(Character* self, wadai_t wadai);
    /// <summary>51e170</summary>
    void chara_set_hp(Character* self, int value);
    /// <summary>51e1a0</summary>
    void chara_add_hp(Character* self, int value);
    /// <summary>51e1d0</summary>
    void chara_set_stress(Character* self, int value);
    /// <summary>51e1f0</summary>
    void chara_add_stress(Character* self, int value);
    /// <summary>51e220</summary>
    bool deck_init(Deck* self, Person* person);
    /// <summary>51e350</summary>
    bool chara_init(Character* self, Person* person, Person* opponent_person, int hp, bool control);
    /// <summary>51e420</summary>
    void chara_init_cards(Character* self, int max_card_count = MaxCardCount);
    /// <summary>51e460</summary>
    void param_set_control(Param* self);
    /// <summary>51e510</summary>
    bool param_is_challenger_win(const Param* self) const;
    /// <summary>51e520</summary>
    Person* param_get_winner_person(const Param* self) const;
    /// <summary>51e550</summary>
    Person* param_get_loser_person(const Param* self) const;
    /// <summary>51e580</summary>
    Person* param_get_challenger_person(const Param* self) const;
    /// <summary>51e590</summary>
    Person* param_get_person(const Param* self, debate_team_t team) const;
    /// <summary>51e5b0</summary>
    int param_get_hp(const Param* self, debate_team_t team) const;
    /// <summary>51e5d0</summary>
    bool param_get_control(const Param* self, debate_team_t team) const;
    /// <summary>51e5f0</summary>
    void param_set_winner(Param* self, debate_team_t team, debate_win_type_t type);
    /// <summary>51e8c0</summary>
    void param_set_finished(Param* self);
    /// <summary>51e8d0</summary>
    bool param_init(Param* self, Person* a, int a_hp, bool a_control, Person* b, int b_hp, bool b_control, bool tutorial);
    /// <summary>51e9f0</summary>
    bool param_set_is_full(const Param* self);
    /// <summary>51ea30</summary>
    bool param_init(Param* self, Person* a, int a_hp, Person* b, int b_hp, bool tutorial);
    /// <summary>51eb00</summary>
    bool param_init(Param* self, Person* a, Person* b, bool tutorial);
    /// <summary>65cc50</summary>
    debate_team_t param_get_challenger(const Param* self) const;

    /// <summary>51eb30</summary>
    static debate_team_t get_opponent_team(debate_team_t team);
    /// <summary>51eb40</summary>
    static wadai_t get_card_wadai(debate_card_t card);
    /// <summary>51eb90</summary>
    static int get_card_level(debate_card_t card);
    /// <summary>51ebe0</summary>
    bool update(int delta);
    /// <summary>51ec20</summary>
    void set_next_phase(debate_phase_t phase);
    /// <summary>51ec30</summary>
    void inc_step();
    /// <summary>51ec40</summary>
    Character* get_chara(debate_team_t team);
    /// <summary>51ec60</summary>
    void set_wadai(wadai_t wadai);
    /// <summary>51ec70</summary>
    void add_hp(debate_team_t team, int value);
    /// <summary>51ec90</summary>
    void add_stress(debate_team_t team, int value);
    /// <summary>51ecb0</summary>
    bool is_card_available(debate_team_t team, debate_card_t card);
    /// <summary>51ee00</summary>
    bool play_card(debate_team_t team, int index);
    /// <summary>51ee90</summary>
    debate_card_t get_played_card(debate_team_t team) const;
    /// <summary>51eeb0</summary>
    void reset_played_card(debate_team_t team);
    /// <summary>51eed0</summary>
    bool ftk();
    /// <summary>51efd0</summary>
    void turn_start();
    /// <summary>51f0c0</summary>
    void turn_end();
    /// <summary>51f1f0</summary>
    static int get_card_power(wadai_t wadai, debate_card_t card);
    /// <summary>51f250</summary>
    int calc_attacker();
    /// <summary>51f300. 체력 대미지</summary>
    int calc_hp_damage(duel_team_t team, debate_card_t card);
    /// <summary>51f3e0. 분노 대미지</summary>
    int calc_stress_damage(debate_card_t card);
    /// <summary>51f420. 비김</summary>
    void attack_draw();
    /// <summary>51f460. 대갈</summary>
    void daikatsu(debate_team_t team, int hp_damage, int stress_damage);
    /// <summary>51f4c0. 화제 카드</summary>
    void wadai(debate_team_t team, debate_card_t card, int hp_damage, int stress_damage, bool kiben);
    /// <summary>51f540. 재고</summary>
    void rethink(debate_team_t team);
    /// <summary>51f580. 무시</summary>
    void mushi(debate_team_t team, int stress_damage);
    /// <summary>51f5c0. 진정</summary>
    void chinsei(debate_team_t team, int stress_damage, bool kiben);
    /// <summary>51f620. 흥분</summary>
    void gyakujou(debate_team_t team, int stress_damage, bool kiben);
    /// <summary>51f680. 흥분 트리거</summary>
    void anger_trigger(debate_team_t team, debate_card_t card);
    /// <summary>51f740. 흥분</summary>
    void anger();
    /// <summary>51f820</summary>
    void combo_attack();
    /// <summary>51f950</summary>
    bool calc_winner();
    /// <summary>51f9d0</summary>
    bool can_critical() const;
    /// <summary>51fa30</summary>
    void calc_win_type();
    /// <summary>51fa70, v+c</summary>
    virtual bool is_valid_phase(debate_phase_t phase) s11_pure;
    /// <summary>51fa90, v+10</summary>
    virtual void on_phase_begin() s11_pure;
    /// <summary>51fab0, v+14</summary>
    virtual bool on_phase(int delta) s11_pure;
    /// <summary>51fae0, v+18</summary>
    virtual void on_phase_end() s11_pure;
    /// <summary>51fb00</summary>
    bool opening_phase(int delta);
    /// <summary>51fb90</summary>
    bool ftk_phase(int delta);
    /// <summary>51fbd0</summary>
    bool unknown2_phase(int delta);
    /// <summary>51fc00</summary>
    bool turn_start_phase(int delta);
    /// <summary>51fc40</summary>
    bool play_phase(int delta);
    /// <summary>51ff40</summary>
    void damage_phase_begin();
    /// <summary>51ff50</summary>
    bool turn_end_phase(int delta);
    /// <summary>51ff90</summary>
    bool critical_phase(int delta);
    /// <summary>520060</summary>
    bool closing_phase(int delta);
    /// <summary>5202d0</summary>
    void attack();
    /// <summary>5203a0</summary>
    void effect(debate_team_t team, debate_card_t card);
    /// <summary>520450</summary>
    bool calc_angering(bool can_reflect);
    /// <summary>520560</summary>
    bool calc_crumbled();
    /// <summary>520600, v+4</summary>
    virtual bool init();
    /// <summary>520620</summary>
    bool anger_phase(int delta);
#if s11_removed
    /// <summary>520770, v+0</summary>
    virtual void destroy(bool delete_this);
#endif
    /// <summary>5207c0</summary>
    void effect();
    /// <summary>520820</summary>
    bool damage_phase(int delta);

    /// <summary>520f70</summary>
    bool run();

    /// <summary>682780, v+8</summary>
    virtual bool is_tutorial() const;

protected:
    s11_vmt; // 8393cc
    debate_phase_t phase_ = -1; // 4
    debate_phase_t next_phase_ = -1; // 8
    int step_ = 0; // c
    std::array<Character, MaxTeamCount> chara_; // 10
    std::array<AI, MaxTeamCount> ai_; // 150 턴
    wadai_t wadai_ = -1; // 168 화제
    debate_team_t first_ = -1; // 16c 선공
    std::array<debate_card_t, MaxTeamCount> played_card_ = { -1, -1 }; // 170 낸 카드
    debate_team_t attacker_ = -1; // 178 더 높은 카드를 낸 팀
    debate_team_t angering_ = -1; // 17c 흥분할 팀
    debate_critical_t critical_ = -1; // 180
    std::array<bool, MaxTeamCount> can_rethink_ = {}; // 184 재고 가능
    std::array<int, MaxTeamCount> crumbled_level_ = {}; // 18c 무너진 정도 (1000 - hp) / 250
    std::array<bool, MaxTeamCount> crumbled_ = {}; // 194 무너짐
    debate_team_t winner_ = -1; // 19c 승리팀
    debate_win_type_t win_type_ = DebateWinType_Normal; // 1a0 결과
    debate_team_t combo_attacker_ = -1; // 1a4
    int combo_counter_ = 0; // 1a8
    bool combo_ = false; // 1ac 연속 공격(소심)

    System* system_ = nullptr;
    Engine* engine_ = nullptr;
    Param* param_ = nullptr;
    bool view_ = false;
};

s11_end_namespace