#pragma once

s11_begin_namespace

class System;

/// <summary></summary>
class Duel
{
public:
    static constexpr int MaxTeamCharaCount = Unit::MaxMemberCount;
    static constexpr int MaxTeamCount = DuelTeam_Max;
    static constexpr int MaxHP = 100;
    static constexpr int MaxSpirit = 300;
    static constexpr int MaxAnimQueueSize = 11;

    struct Param
    {
        std::array<std::array<Person*, MaxTeamCharaCount>, MaxTeamCount> person = {}; // 0
        std::array<Unit*, MaxTeamCount> unit = {}; // 18
        std::array<int, MaxTeamCount> start_chara = { -1, -1 }; // 20 시작 무장
        std::array<player_t, MaxTeamCount> player_id = { -1, -1 }; // 28
        scene_t ret_scene = Scene_Max; // 30
        duel_type_t type = -1; // 34
        std::array<duel_control_t, MaxTeamCount> control = { -1, -1 }; // 38
        int max_blow_counter = 50; // 40
        duel_stage_t stage = DuelStage_Grassland; // 44
        bool tutorial = false; // 48
        duel_ftk_type_t ftk_type = -1; // 4c
        duel_team_t ftk_team = -1; // 50
        duel_team_t winner_team = -1; // 54
        duel_team_t loser_team = -1; // 58
        int winner_chara = -1; // 5c
        int loser_chara = -1; // 60
        std::array<std::array<duel_chara_result_t, MaxTeamCharaCount>, MaxTeamCount> result; // 64 결과
        std::array<std::array<int, MaxTeamCharaCount>, MaxTeamCount> hp; // 7c 체력
        std::array<std::array<int, MaxTeamCharaCount>, MaxTeamCount> spirit; // 94 투지
        std::array<std::array<shoubyou_t, MaxTeamCharaCount>, MaxTeamCount> shoubyou; // ac 상병
        int end_blow_counter = 0; // c4
        duel_status_t flags = 0; // c8
        // int state; // cc

        Param()
        {
            result[0].fill(0);
            result[1].fill(0);
            hp[0].fill(MaxHP);
            hp[1].fill(MaxHP);
            spirit[0].fill(0);
            spirit[1].fill(0);
            shoubyou[0].fill(-1);
            shoubyou[1].fill(-1);
        }
    };

    struct Character
    {
        Person* person = nullptr; // 0 무장
        int hp = 0; // 4 체력
        int spirit = 0; // 8 투지
        shoubyou_t shoubyou = -1; // c 상병
        duel_stance_t stance = -1; // 10 행동방침
        duel_chara_state_t state = -1; // 14 상태
        int number = -1; // 18 교대 번호
        bitset4<DuelItemType_Max> item = {}; // 1c 보물
        std::array<int, DuelSpecial_Max> special_remaining_count = {}; // 20 남은 사용 가능 횟수
    };

    struct Team
    {
        std::array<Character, MaxTeamCharaCount> chara; // 0 무장
        int chara_count = 0; // c0
        int current_chara = -1; // c4 현재 무장
        player_t player_id = -1; // c8
        int appearing_timer = 0; // cc 등장 금지 타이버 -
        int switching_timer = 0; // d0 교대 금지 타이머 -
        int invulnerable_timer = 0; // d4 방어중시 크리티컬 타이머 -
        duel_control_t control = -1; // d8
        std::array<int, DuelBuffType_Max> buff_timer = {}; // dc 버프 상태 카운터 -
        int stance_timer = {}; // e8 행동 방침 카운터 +
    };

    struct AI
    {
        struct Row
        {
            int chance = 0; // 0
            duel_ai_row_t id = DuelAIRow_Max; // 4
            int param1 = 0; // 8
            int param2 = 0; // c
        };

        bool initialized = false; // 0
        Duel* parent = nullptr; // 4
        duel_team_t team = -1; // 8
        duel_team_t opponent_team = -1; // c
        int chara = -1; // 10
        int opponent_chara = -1; // 14
    };

    struct BlowAnim
    {
        int value = -1;
    };

    struct HPAnim
    {
        int damage = 0; // 0
        duel_team_t atk_team = -1; // 4
        duel_team_t def_team = -1; // 8
        int atk_chara = -1; // c
        int def_chara = -1; // 10
        int shoubyou_damage = 0; // 14
    };

    struct SpiritAnim
    {
        int atk_value = 0; // 0
        duel_team_t atk_team = -1; // 4
        int atk_chara = -1; // 8
        int def_value = 0; // c
        duel_team_t def_team = -1; // 10
        int def_chara = -1; // 14
    };

    struct Action
    {
        duel_team_t team = -1; // 0
        int chara = -1; // 4
        duel_action_t type = -1; // 8
        duel_action_result_t result = -1; // c
    };

    struct SpecialAction
    {
        duel_team_t team = -1; // 0
        int chara = -1; // 4
        duel_sp_t type = -1; // 8
        duel_sp_result_t result = -1; // c
    };

    Duel(System* system, Param* param);

    /// <summary>475810</summary>
    void none();

    /// <summary>4d3940</summary>
    void result_injury_report(Unit* unit, Person* person, bool last);
    /// <summary>4d3a80</summary>
    void result_injury();
    /// <summary>4d3bd0</summary>
    void result_draw();
    /// <summary>4d3cb0</summary>
    void result_normal();
    /// <summary>4d43c0</summary>
    void result_handler();

    /// <summary>4fb490</summary>
    void ai_update_chara(AI* self);
    /// <summary>4fb4d0</summary>
    const AI::Row* ai_get_table(const AI* self, duel_ai_type_t type, duel_ai_table_t table_id) const;
    /// <summary>4fb500</summary>
    int ai_get_hp(const AI* self, duel_team_t team, int chara) const;
    /// <summary>4fb540</summary>
    int ai_get_hp(const AI* self, bool opponent = false) const;
    /// <summary>4fb590</summary>
    int ai_get_spirit(const AI* self, bool opponent = false) const;
    /// <summary>4fb5e0</summary>
    const AI::Row* ai_get_table(const AI* self, duel_ai_table_t table_id) const;
    /// <summary>4fb6f0</summary>
    bool ai_comp(const AI* self, duel_ai_comp_op_t op, int a, int b) const;
    /// <summary>4fb750</summary>
    bool ai_comp_hp(const AI* self, const AI::Row* row, duel_ai_comp_op_t op, bool opponent = false) const;
    /// <summary>4fb780</summary>
    bool ai_comp_spirit(const AI* self, const AI::Row* row, duel_ai_comp_op_t op, bool opponent = false) const;
    /// <summary>4fb7b0</summary>
    bool ai_is_special_enabled(const AI* self, duel_sp_t special, bool opponent = false) const;
    /// <summary>4fb830</summary>
    bool ai_has_buff(const AI* self, duel_buff_type_t buff, bool opponent = false) const;
    /// <summary>4fb890</summary>
    bool ai_calc_special_try(const AI* self) const;
    /// <summary>4fbb00</summary>
    bool ai_init(AI* self, duel_team_t team);
    /// <summary>4fbb70</summary>
    int ai_get_power(const AI* self, duel_team_t team, int chara) const;
    /// <summary>4fbc60</summary>
    int ai_get_best_chara(const AI* self, duel_team_t team) const;
    /// <summary>4fbcd0</summary>
    duel_sp_t ai_random_special(const AI* self) const;
    /// <summary>4fbe00</summary>
    int ai_calc_switch(const AI* self) const;
    /// <summary>4fc1a0</summary>
    bool ai_comp_power(const AI* self, duel_ai_comp_op_t op, duel_team_t a_team, int a_chara, duel_team_t b_team, int b_chara) const;
    /// <summary>4fc1e0</summary>
    duel_sp_t ai_calc_special(const AI* self) const;
    /// <summary>4fc5a0</summary>
    duel_stance_t ai_calc_stance(const AI* self) const;

    /// <summary>4fc940</summary>
    bool is_valid(const Action* action) const;
    /// <summary>4fc980</summary>
    bool is_valid(const SpecialAction* special) const;

    /// <summary>4560a0, v+14. 버튼 활성화 여부(튜토리얼용)</summary>
    virtual bool is_button_enabled(duel_button_t button) const;
    /// <summary>506760, v+4</summary>
    virtual bool on_phase(int delta);
    /// <summary>5067b0, v+8</summary>
    virtual void exit();
    /// <summary>5067c0</summary>
    bool is_player(duel_team_t team) const;
    /// <summary>5067f0</summary>
    bool is_manual(duel_team_t team) const;
    /// <summary>506820</summary>
    int get_current_chara(duel_team_t team) const;
    /// <summary>506850</summary>
    duel_team_t get_opponent_team(duel_team_t team) const;
    /// <summary>506870</summary>
    Person* get_person(duel_team_t team, int chara) const;
    /// <summary>5068b0</summary>
    Person* get_current_person(duel_team_t team) const;
    /// <summary>5068f0</summary>
    duel_stance_t get_stance(duel_team_t team, int chara) const;
    /// <summary>506930</summary>
    int get_hp(duel_team_t team, int chara) const;
    /// <summary>506970</summary>
    void set_hp(duel_team_t team, int chara, int value);
    /// <summary>5069b0</summary>
    int add_hp(duel_team_t team, int chara, int value);
    /// <summary>506a20</summary>
    int get_current_hp(duel_team_t team) const;
    /// <summary>506a60</summary>
    int get_strength(duel_team_t team, int chara, bool revised) const;
    /// <summary>506aa0</summary>
    int get_current_strength(duel_team_t team, bool revised) const;
    /// <summary>506ae0</summary>
    int get_spirit(duel_team_t team, int chara) const;
    /// <summary>506b20</summary>
    void set_spirit(duel_team_t team, int chara, int value);
    /// <summary>506b60</summary>
    int add_spirit(duel_team_t team, int chara, int value);
    /// <summary>506ba0</summary>
    int get_current_spirit(duel_team_t team, int chara) const;
    /// <summary>506be0</summary>
    int get_switching_timer(duel_team_t team) const;
    /// <summary>506c10</summary>
    bool is_injured(duel_team_t team, int chara) const;
    /// <summary>506c50</summary>
    bool has_buff(duel_team_t team, duel_buff_type_t type) const;
    /// <summary>506c90</summary>
    bool has_item(duel_team_t team, int chara, bitset4<DuelItemType_Max> flags) const;
    /// <summary>506cd0</summary>
    bool is_invulnerable(duel_team_t team, int chara) const;
    /// <summary>506d10</summary>
    int get_stance_timer(duel_team_t team) const;
    /// <summary>506d40</summary>
    void set_ui(void*);
    /// <summary>506d90</summary>
    bool check_state(duel_team_t team, int chara, duel_chara_state_t state) const;
    /// <summary>506e00</summary>
    bool is_joined(duel_team_t team, int chara) const;
    /// <summary>506e70</summary>
    bool is_manual() const;
    /// <summary>506ec0. 사용 가능한 필살 있음</summary>
    bool can_special(duel_team_t team, int chara) const;
    /// <summary>506f20. 필살 남은 횟수 있음</summary>
    bool is_special_available(duel_team_t team, int chara, duel_sp_t special) const;
    /// <summary>506f70. 필살 투지 비용</summary>
    int get_special_spirit_cost(duel_sp_t special) const;
    /// <summary>506ff0</summary>
    void set_next_phase(duel_phase_t phase);
    /// <summary>507020, v+18</summary>
    virtual void on_phase_end();
    /// <summary>507050, v+1c</summary>
    virtual void on_phase_begin();
    /// <summary>507080, v+20</summary>
    virtual void on_phase_change();
    /// <summary>5070b0, v+28</summary>
    virtual bool is_idle() const;
    /// <summary>5070f0</summary>
    void set_stance(duel_team_t team, int chara, duel_stance_t stance);
    /// <summary>507170</summary>
    int create_cooldown_timer(duel_team_t team, int chara) const;
    /// <summary>5071b0</summary>
    int get_special_remaining_count(duel_team_t team, int chara, duel_sp_t special) const;
    /// <summary>507200</summary>
    void set_special_remaining_count(duel_team_t team, int chara, duel_sp_t special, int value);
    /// <summary>507250</summary>
    int get_action_ratio(duel_team_t a_team, int a_chara, duel_team_t b_team, int b_chara) const;
    /// <summary>5072d0. 필살 타격 수</summary>
    int get_special_hit_count(const SpecialAction* special) const;
    /// <summary>507320. 퇴각 성공 확률</summary>
    bool calc_retreat_chance(duel_team_t team, int chara) const;
    /// <summary>5074b0. 크리티컬 발생 확률</summary>
    bool calc_critical_chance(duel_team_t team, int chara) const;
    /// <summary>507660</summary>
    int create_invulnerable_timer(duel_team_t team, int chara) const;
    /// <summary>5076a0</summary>
    void reset_buff_timer(duel_team_t team, int chara, duel_buff_type_t buff);
    /// <summary>507700. 대상과 가족이거나 의형제</summary>
    bool is_family(duel_team_t a_team, int a_chara, duel_team_t b_team, int b_chara) const;
    /// <summary>5077f0</summary>
    bool init_phase(int delta);
    /// <summary>507850</summary>
    bool opening_phase(int delta);
    /// <summary>5078e0</summary>
    bool blow_anim(int count);
    /// <summary>5079f0</summary>
    bool hp_anim(int count);
    /// <summary>507b10</summary>
    bool spirit_anim(int count);
    /// <summary>507c30</summary>
    void update_param_result();
    /// <summary>507e10</summary>
    void update_stance(bool player = false);
    /// <summary>507f10</summary>
    bool update_special_action();
    /// <summary>507fd0</summary>
    void update_switching();
    /// <summary>508070. 퇴각 승패 결정</summary>
    bool update_retreat_result();
    /// <summary>5080f0</summary>
    void update_timer();
    /// <summary>508200</summary>
    void reset_action();
    /// <summary>5082e0. 최소 체력</summary>
    static int get_duel_min_hp(Person* self);
    /// <summary>508330</summary>
    int get_chara_count(duel_team_t team, bool joined) const;
    /// <summary>5083a0. 합 증감</summary>
    int add_blow_counter(int value);
    /// <summary>5083e0</summary>
    person_id_t get_person_id(duel_team_t team, int chara) const;
    /// <summary>508440</summary>
    int get_chara(duel_team_t team, int number) const;
    /// <summary>5084b0. 상병 변경</summary>
    void change_shoubyou(duel_team_t team, int chara, shoubyou_t shoubyou);
    /// <summary>508560. 상병 증감</summary>
    shoubyou_t add_shoubyou(duel_team_t team, int chara, int value);
    /// <summary>5085f0, v+c. 필살 사용 가능</summary>
    virtual bool is_special_enabled(duel_team_t team, int chara, duel_sp_t special) const;
    /// <summary>5086c0. 필살 결과</summary>
    duel_sp_result_t calc_special_result(const SpecialAction* special) const;
    /// <summary>508720. 일합에 승리한 팀</summary>
    static duel_team_t calc_duel_ftk_team(Person* a, Person* b, bool a_bow, bool b_bow, shoubyou_t a_shoubyou, shoubyou_t b_shoubyou, int* chance);
    /// <summary>508b90</summary>
    /// <param name="predict">hp_anim_array 값을 참고하여 미리 계산할지 여부(비동기로 진행되는 방식일 경우에 사용됨)</param>
    duel_result_t calc_result(bool predict, const HPAnim* hp_anim_array);
    /// <summary>508cc0. 대미지</summary>
    int calc_attack_damage(duel_team_t team, int chara) const;
    /// <summary>508da0. 필살 대미지</summary>
    int calc_special_damage(const SpecialAction* special) const;
    /// <summary>509120. 참전 가능한 상태인지</summary>
    bool can_join(duel_team_t team, int chara) const;
    /// <summary>509190. 함전 할지</summary>
    bool calc_join(duel_team_t team, int chara) const;
    /// <summary>509450. 이번 턴 공격 차례</summary>
    duel_team_t calc_actor_team() const;
    /// <summary>509690</summary>
    duel_action_t calc_action(duel_team_t team, int chara);
    /// <summary>509800</summary>
    duel_team_t calc_ftk_team() const;
    /// <summary>509930</summary>
    duel_ftk_type_t calc_ftk_type() const;
    /// <summary>509a30. 사망 확률</summary>
    /// <param name="team">패배팀</param>
    bool calc_kill_chance(duel_team_t team);
    /// <summary>509b40. 포박 확률</summary>
    /// <param name="team">패배팀</param>
    bool calc_capture_chance(duel_team_t team);
    /// <summary>509c10</summary>
    bool ftk_anim();
    /// <summary>509d20</summary>
    bool change_current_chara(duel_team_t team, int chara);
    /// <summary>509e30</summary>
    int calc_action_ratio(duel_team_t a_team, int a_chara, duel_team_t b_team, int b_chara) const;
    /// <summary>50a120. 투지 획득 계산</summary>
    /// <param name="attack">true일 경우 공격 시, false일 경우 피격 시</param>
    int calc_spirit_gain(duel_team_t team, int chara, bool attack, int value) const;
    /// <summary>50a2a0</summary>
    duel_team_t calc_special_try() const;
    /// <summary>50a390</summary>
    void update_special_action_result();
    /// <summary>50a3b0. 회피 확률</summary>
    bool calc_dodge_chance(duel_team_t team, int chara) const;
    /// <summary>50a4b0. 필살 부상 확률</summary>
    bool calc_wound_chance(const SpecialAction* special) const;
    /// <summary>50a6b0</summary>
    bool ftk_phase(int delta);
    /// <summary>50a8a0</summary>
    bool command_phase(int delta);
    /// <summary>50aa80</summary>
    bool special_command_phase(int delta);
    /// <summary>50abd0</summary>
    bool retreat_phase(int delta);
    /// <summary>50aca0</summary>
    bool turn_end_phase(int delta);
    /// <summary>50ad70</summary>
    bool closing_phase(int delta);
    /// <summary>50afd0</summary>
    bool join_anim();
    /// <summary>50b0f0</summary>
    bool switch_anim();
    /// <summary>50b1a0</summary>
    bool special_action_anim();
    /// <summary>50b5a0, v+24</summary>
    virtual bool is_valid_phase(duel_phase_t phase) const;
    /// <summary>50b5c0</summary>
    void calc_action_ratio();
    /// <summary>50b600</summary>
    bool init_team(duel_team_t team);
    /// <summary>50b7e0. 대미지</summary>
    int calc_damage(int* atk_spirit, int* def_spirit, duel_team_t team, int chara, duel_action_t action, duel_action_result_t result);
    /// <summary>50bb70</summary>
    int calc_appearing_chara(duel_team_t team) const;
    /// <summary>50bcb0</summary>
    duel_action_result_t calc_action_result(duel_team_t team, int chara, duel_action_t action) const;
    /// <summary>50bf30</summary>
    bool join_phase(int delta);
    /// <summary>50bfc0</summary>
    bool special_phase(int delta);
    /// <summary>50c170</summary>
    void calc_appearing();
    /// <summary>50c1a0</summary>
    void update_action();
    /// <summary>50c280</summary>
    bool action_anim();
    /// <summary>50c490. 일합에 승리한 팀</summary>
    static duel_team_t calc_duel_ftk_team(Person* a, Person* b, int* chance);
    /// <summary>50c6b0</summary>
    bool turn_start_phase(int delta);
    /// <summary>50c760</summary>
    bool action_start_phase(int delta);
    /// <summary>50c7f0</summary>
    bool action_end_phase(int delta);
    /// <summary>50c930, v+0</summary>
    virtual void init();

    /// <summary>50c980</summary>
    void chara_init_special(Character* self, Person* person);
    /// <summary>50c9d0</summary>
    Person* team_get_person(const Team* self, int chara) const;
    /// <summary>50ca20</summary>
    bool team_is_active(const Team* self, int chara) const;
    /// <summary>50ca70</summary>
    void team_set_buff_timer(Team* self, duel_buff_type_t type, int timer);
    /// <summary>50ca90</summary>
    int team_get_buff_timer(const Team* self, duel_buff_type_t type) const;
    /// <summary>50cab0</summary>
    duel_stance_t team_get_stance(const Team* self, int chara) const;
    /// <summary>50cb00</summary>
    void team_set_stance(Team* self, int chara, duel_stance_t stance);
    /// <summary>50cb80</summary>
    duel_chara_state_t team_get_state(const Team* self, int chara) const;
    /// <summary>50cbd0</summary>
    int team_get_hp(const Team* self, int chara) const;
    /// <summary>50cc20</summary>
    void team_set_hp(Team* self, int chara, int value);
    /// <summary>50cc70</summary>
    int team_get_spirit(const Team* self, int chara) const;
    /// <summary>50ccc0</summary>
    void team_set_spirit(Team* self, int chara, int value);
    /// <summary>50cd40</summary>
    shoubyou_t team_get_shoubyou(const Team* self, int chara) const;
    /// <summary>50cd90</summary>
    void team_set_shoubyou(Team* self, int chara, shoubyou_t shoubyou);
    /// <summary>50cde0</summary>
    int team_get_number(const Team* self, int chara) const;
    /// <summary>50ce30</summary>
    void team_set_number(Team* self, int chara, int value);
    /// <summary>50ce80</summary>
    int team_get_special_remaining_count(const Team* self, int chara, duel_sp_t special) const;
    /// <summary>50cee0</summary>
    void team_set_special_remaining_count(Team* self, int chara, duel_sp_t special, int value);
    /// <summary>50cf70</summary>
    bool team_has_item(const Team* self, int chara, bitset4<DuelItemType_Max> flags) const;
    /// <summary>50cf90. 무력</summary>
    static int get_duel_strength(Person* self, shoubyou_t shoubyou, bool revised);
    /// <summary>50d0c0</summary>
    int team_get_stance_speed(const Team* self, int chara) const;
    /// <summary>50d0f0</summary>
    int team_get_stance_hit(const Team* self, int chara) const;
    /// <summary>50d120</summary>
    int team_get_stance_attack(const Team* self, int chara) const;
    /// <summary>50d150</summary>
    int team_get_stance_block(const Team* self, int chara) const;
    /// <summary>50d180</summary>
    int team_get_stance_attack_sub(const Team* self, int chara) const;
    /// <summary>50d1b0</summary>
    int team_get_stance_spirit_gain(const Team* self, int chara) const;
    /// <summary>50d270</summary>
    bool team_has_sub_chara(const Team* self, duel_chara_state_t state) const;
    /// <summary>50d2f0</summary>
    bool team_has_buff(const Team* self, duel_buff_type_t type) const;
    /// <summary>50d320</summary>
    bool team_change_current_chara(Team* self, int chara);
    /// <summary>50d3e0</summary>
    int team_get_strength(const Team* self, int chara, bool revised) const;
    /// <summary>50d420</summary>
    int team_add_hp(Team* self, int chara, int value);
    /// <summary>50d4a0</summary>
    int team_add_spirit(Team* self, int chara, int value);
    /// <summary>50d5b0</summary>
    void chara_init_item(Character* self, Person* person);
    /// <summary>50d700</summary>
    void chara_init(Character* self, Person* person, int hp, int spirit, shoubyou_t shoubyou);
    /// <summary>50d780</summary>
    void team_init(Team* self, Person* person_array[], int hp_array[], int spirit_array[], shoubyou_t shoubyou_array[], int count, int current_chara, duel_control_t control, player_t player_id);

    /// <summary>50dac0</summary>
    Unit* param_get_winner_unit(Param* self);
    /// <summary>50dae0</summary>
    Unit* param_get_loser_unit(Param* self);
    /// <summary>50db00</summary>
    duel_chara_result_t param_get_chara_result(Param* self, duel_team_t team, int chara);
    /// <summary>50db40</summary>
    Unit* param_get_unit(Param* self, duel_team_t team, int chara);
    /// <summary>50db80</summary>
    int param_get_hp(Param* self, duel_team_t team, int chara);
    /// <summary>50dbc0</summary>
    int param_get_spirit(Param* self, duel_team_t team, int chara);
    /// <summary>50dc10</summary>
    shoubyou_t param_get_shoubyou(Param* self, duel_team_t team, int chara);
    /// <summary>50dc60</summary>
    Person* param_get_person(Param* self, duel_team_t team, int chara);
    /// <summary>50dca0</summary>
    Person* param_get_winner_person(Param* self);
    /// <summary>50dcd0</summary>
    Person* param_get_loser_person(Param* self);
    /// <summary>50dd00</summary>
    int param_get_start_chara(Param* self, duel_team_t team);
    /// <summary>50dd30</summary>
    player_t param_get_player_id(Param* self, duel_team_t team);
    /// <summary>50dd60</summary>
    duel_control_t param_get_control(Param* self, duel_team_t team);
    /// <summary>50dd90</summary>
    bool param_is_manual(Param* self);

    /// <summary>50e290</summary>
    Person* param_get_challenger(Param* self);
    /// <summary>50e2b0</summary>
    Person* param_get_challenged(Param* self);

    /// <summary>50ed00</summary>
    bool run();

    /// <summary>682780, v+10</summary>
    virtual bool is_tutorial() const;

protected:
    s11_vmt; // 8373c4
    duel_phase_t phase_ = -1; // 4
    duel_phase_t next_phase_ = -1; // 8
    int step_ = -1; // c
    int blow_counter_ = 0; // 10 합
    duel_state_t state_ = DuelState_Play; // 14 상태
    int max_blow_counter_ = 0; // 18 최대 합
    bool reverse_ = false; // 1c 팀을 반대로?(사용하려면 param도 팀을 반대로 설정해야함)
    duel_type_t type_ = -1; // 20 대사 관련
    std::array<Team, MaxTeamCount> team_; // 24 팀
    std::array<std::array<int, MaxTeamCharaCount>, MaxTeamCharaCount> action_ratio_ = {}; // 1fc 공격 비율
    bool messagebox_blocking_ = false; // 220 메시지 박스가 뜬 상태라면 중단
    void* scene_ = nullptr; // 224
    void* ui_ = nullptr; // 228
    void* ui_stance_ = nullptr; // 22c
    void* ui_special_ = nullptr; // 230
    void* ui_result_ = nullptr; // 234
    std::array<AI, MaxTeamCount> ai_; // 238
    duel_team_t ftk_team_ = -1; // 268
    duel_ftk_type_t ftk_type_ = -1; // 26c
    std::array<int, MaxTeamCount> appearing_chara_ = { -1, -1 }; // 270 등장할 무장
    std::array<int, MaxTeamCount> switching_chara_ = { -1, -1 }; // 278 교대할 무장
    std::array<int, MaxTeamCount> _280 = { -1, -1 }; // 280 퇴각할 무장?
    std::array<BlowAnim, MaxAnimQueueSize> blow_anim_queue_; // 288 합
    std::array<HPAnim, MaxAnimQueueSize> hp_anim_queue_; // 2b4 체력
    std::array<SpiritAnim, MaxAnimQueueSize> spirit_anim_queue_; // 3bc 투지
    int action_count_ = 0; // 4c4 50c44d
    std::array<Action, MaxAnimQueueSize> action_queue_; // 4c8
    SpecialAction special_action_; // 578 사용할 필살
    duel_team_t winner_team_ = -1; // 588 승리팀
    duel_team_t loser_team_ = -1; // 58c 패배팀
    duel_result_t result_ = -1; // 590 결과
    duel_status_t flags_ = 0; // 594 상태
    duel_team_t special_try_team_ = -1; // 598 다음 턴 필살 페이즈에 진입할 팀

    System* system_ = nullptr;
    Engine* engine_ = nullptr;
    Param* param_ = nullptr;
    bool view_ = false;
};

s11_end_namespace