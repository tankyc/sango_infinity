using System;

namespace Sango.Core
{
    /// <summary>
    /// 外交判定规则（逐条对齐 Open311 的 `s11_sys_person_order_diplomacy.cpp`）。
    ///
    /// 【定位】只放**纯函数**：输入双方势力 / 使者 / 金额 / 期限，输出判定结果与数值。
    /// 不碰场景状态、不发事件、不扣费 —— 那些属于 Action 层（`DiplomacyAction*`）。
    /// 这样每条公式都能单独验证，也能被 AI 直接调用探底（原版 AI 就是调 try_* 系列）。
    ///
    /// 【友好度口径】本项目用 <see cref="Force.GetLike"/>（0~100，50 中立）读取，
    /// 底层 `Scenario.RelationMap` 已与 Open311 的 `Force::like_` 同尺度（0~100、双向对称），
    /// 不存在换算层 —— 见 `Force.GetLike` 注释。
    ///
    /// 【难度】原版 `multiply_difficulty`：Easy = 原值，Normal ×0.8，Hard ×0.7。
    /// 本项目 difficulty 0/1/2/3 = 简单/普通/困难/超级，超级沿用 Hard。
    /// </summary>
    public static class DiplomacyRules
    {
        /// <summary>外交判定结果（对齐原版 PersonOrderDiplomacyResult 的四种出口）</summary>
        public enum Result
        {
            /// <summary>失败</summary>
            Failure = 0,

            /// <summary>勉强成功：原版 perfect = false，演出/文案用"勉强"版本</summary>
            SuccessNarrow = 1,

            /// <summary>成功</summary>
            Success = 2,

            /// <summary>进入舌战</summary>
            Debate = 3,
        }

        /// <summary>
        /// 把三态判定映射成 0~100 的成功率。
        ///
        /// 【为什么需要】原版外交是**确定性判定**（try_* 直接返回成功 / 舌战 / 失败），
        /// 而本项目 Action 层沿用了"成功率 + GameRandom.Chance"的旧接口。
        /// 过渡期用本方法兼容：数值只表示"把握有多大"，真正的判定出口仍由 DiplomacyRules 给出。
        /// 后续把 Action 层改成直接消费 <see cref="Result"/> 后，本方法即可删除。
        /// </summary>
        public static int ToSuccessRate(Result result)
        {
            switch (result)
            {
                case Result.Success: return 100;
                case Result.SuccessNarrow: return 75;
                case Result.Debate: return 50;
                default: return 10;
            }
        }

        /// <summary>友好度上限（原版 max_force_like_ = 100，s11_db.cpp:973）</summary>
        public const int MaxLike = 100;

        /// <summary>友好度初始值（原版新势力登场 force_set_like(50)，s11_sys_scen_force.cpp:358）</summary>
        public const int DefaultLike = 50;

        /// <summary>AI 主动结盟的友好度门槛（s11_game_ai_force_cmd_dip.cpp:329）</summary>
        public const int AiAllianceLikeThreshold = 70;

        /// <summary>玩家提案被拒后的禁谈回合数（原版 force_set_diplomacy_ban_timer(9)）</summary>
        public const int DiplomacyBanTurns = 9;

        /// <summary>同盟成功时友好度 +10（s11_sys_person_order_diplomacy.cpp:615）</summary>
        public const int AllianceLikeGain = 10;

        /// <summary>破弃同盟友好度 -50（:862）</summary>
        public const int DiscardLikePenalty = -50;

        /// <summary>劝降失败友好度 -30（:1485）</summary>
        public const int AnnexFailLikePenalty = -30;

        /// <summary>
        /// 外交命令「劝降」成功：出使者所在势力技巧点 +200
        /// （s11_sys_person_order_diplomacy.cpp:1474 `force_add_tech_point(actor_force, 200, self->actor)`）。
        /// 入口减半后实际 +100。
        ///
        /// 【注意不要和下面这条混淆】原版另有 <c>Game::annexation()</c>（s11_game_week.cpp:1016-1152），
        /// 那是**周事件**：满 12 个月后每季度首日，让一个 AI 势力吞并另一个相邻 AI 势力，
        /// 成功时给**吞并方** <c>force_add_tech_point(dest, 1000, nullptr)</c>（:1150，实际 +500）。
        /// 它与「劝降命令」是两条互不相干的路径（触发源不同、受益方不同、也没有使者），
        /// 本项目尚未实现该周事件，见 <see cref="AnnexTechGain"/> 的说明。
        /// </summary>
        public const int AnnexTechGain = 200;

        /// <summary>同盟成功技术点 +50（:611-632）</summary>
        public const int AllianceTechGain = 50;

        /// <summary>破弃同盟技术点 -50（:861-898）</summary>
        public const int DiscardTechCost = 50;

        /// <summary>停战成功技术点 +30（:1132-1151）</summary>
        public const int CeasefireTechGain = 30;

        /// <summary>
        /// 友好度分档阈值（原版 System::get_yookou，s11_sys_scen_force.cpp:916-927）：
        /// &lt;20 险恶 / 20~39 不和 / 40~60 中立 / 61~80 好意 / 81~100 亲密。
        /// </summary>
        public static int GetYookouLevel(int like)
        {
            if (like < 20) return 0;        // Low
            if (like < 40) return 1;        // MiddleLow
            if (like <= 60) return 2;       // Middle
            if (like <= 80) return 3;       // MiddleHigh
            return 4;                       // High
        }

        /// <summary>
        /// 战斗类敌对惩罚：把双方友好度压到 **15 地板**。
        ///
        /// 对齐原版四处同构实现（逻辑完全一致，只是调用点不同）：
        /// <list type="bullet">
        /// <item>城池陷落 —— `s11_sys_event_kanraku.cpp:221-229`</item>
        /// <item>据点 / 设施被破坏 —— `s11_sys_scen_building.cpp:372-382`</item>
        /// <item>部队被击破 —— `s11_sys_scen_unit.cpp:428-443`</item>
        /// <item>单挑致败者战死 —— `s11_sys_duel.cpp:223-230`</item>
        /// </list>
        /// 规则是：`if (like > 15) set_like(15); else add_like(-1);`
        /// —— 一场战争最多把关系压到 15（"打完还能再谈"），但反复冲突仍可缓慢跌破。
        /// </summary>
        /// <param name="attacker">进攻方势力</param>
        /// <param name="defender">防守方势力</param>
        public static void ApplyHostilityPenalty(Force attacker, Force defender)
        {
            if (attacker == null || defender == null || attacker == defender)
                return;
            if (!attacker.IsAlive || !defender.IsAlive)
                return;

            int like = attacker.GetLike(defender);
            if (like > HostilityLikeFloor)
                attacker.SetLike(defender, HostilityLikeFloor);
            else
                attacker.AddLike(defender, -1);
        }

        /// <summary>战斗敌对惩罚的友好度地板（原版 15）</summary>
        public const int HostilityLikeFloor = 15;

        /// <summary>
        /// 两个势力是否相邻（读 `Force.NeighborForceList`）。
        ///
        /// 【对应原版】Open311 `System::is_force_neighbored`（s11_sys_scen_force.cpp:863-879）：
        /// 遍历 a 的全部据点，逐个数其邻接据点是否属于 b。
        ///
        /// 【本项目落点】直接用 `Force.NeighborForceList`（`Force.cs:674-677`）：
        /// 由 `Force.UpdateTurnInfo` 在**该势力回合开始**时重建
        /// （`Force.cs:2154` 起，遍历自己每座都市的 `City.NeighborList`，把非本势力的归属方收进去）。
        ///
        /// 【与原版的已知差异】
        /// <list type="number">
        /// <item>缓存滞后：列表是回合初快照，回合中据点易主后最多差一回合；
        ///       原版是调用即实时遍历。</item>
        /// <item>收集源只取本势力的**都市**（`c.IsCity()` 才有邻接遍历），
        ///       所以"A 只有关隘/港口与 B 接壤"这种边角情况不会进表；对方的关港则会被正常收集。</item>
        /// </list>
        /// 两处差异只影响"相邻 / 远方"的措辞，不影响关系值本身。
        ///
        /// 【不要混用】`Force.hostileForceIds / friendlyForceIds` 是 AI 决策优先级表
        /// （`s11_game_ai_force.cpp:432-522`），与地理相邻无关。
        /// </summary>
        /// <param name="a">势力 A</param>
        /// <param name="b">势力 B</param>
        /// <returns>a 的相邻势力表里含 b 时返回 true</returns>
        public static bool IsNeighbored(Force a, Force b)
        {
            if (a == null || b == null || a == b)
                return false;
            if (a.NeighborForceList == null)
                return false;

            return a.NeighborForceList.Contains(b);
        }

        /// <summary>
        /// 友好度档位名称（原版五档文本，s11_sys_string.cpp:188-197）。
        /// 险恶 / 不和 / 中立 / 好意 / 亲密。
        /// </summary>
        /// <param name="like">友好度 0~100</param>
        /// <returns>档位中文名</returns>
        public static string GetYookouName(int like)
        {
            switch (GetYookouLevel(like))
            {
                case 0: return "险恶";
                case 1: return "不和";
                case 2: return "中立";
                case 3: return "好意";
                default: return "亲密";
            }
        }

        /// <summary>
        /// 友好度的标准显示文本：`50（中立）`。
        ///
        /// 原版 UI 既有数字也有档位（使者接待对话显示 0~100 数字、
        /// 关系变化播报与外交结果用五档文字），这里合并成"数字（档位）"一处产出，
        /// 避免各界面各自拼格式导致口径分叉。
        /// </summary>
        /// <param name="like">友好度 0~100</param>
        /// <returns>形如「50（中立）」</returns>
        public static string FormatLike(int like)
        {
            return $"{like}（{GetYookouName(like)}）";
        }

        /// <summary>
        /// 难度系数（原版 multiply_difficulty，`s11_sys_person_order_diplomacy.cpp:175-185`）。
        /// </summary>
        /// <param name="value">原始判定值</param>
        /// <param name="difficulty">0 简单 / 1 普通 / 2 困难 / 3 超级</param>
        public static int MultiplyDifficulty(int value, int difficulty)
        {
            switch (difficulty)
            {
                case 1: return value * 8 / 10;
                case 2:
                case 3: return value * 7 / 10;
                default: return value;
            }
        }

        /// <summary>
        /// 关系系数（原版 get_relation_coef_int，`:145-157`）：
        /// 目标君主与使者是义兄弟/夫妻 → family；亲爱 → like；厌恶 → hate；其它 0。
        ///
        /// 注意原版这里比的是**目标君主 vs 使者本人**（不是使者君主），
        /// 这也是"派个和目标君主关系好的武将当使者更容易成"的机制来源。
        /// </summary>
        public static int GetRelationCoef(Person targetGovernor, Person actor, int like, int hate, int family)
        {
            if (targetGovernor == null || actor == null) return 0;

            if (targetGovernor.Brother == actor || targetGovernor.IsSpouse(actor)) return family;
            if (targetGovernor.IsLike(actor)) return like;
            if (targetGovernor.IsHate(actor)) return hate;
            return 0;
        }

        /// <summary>
        /// 赠送（Donate）的友好度增量 —— 原版 `calc_donate_like_change`（`:385-406`）。
        ///
        /// <code>
        /// aishou = min(相性差, 50)；目标君主厌恶使者君主时强制 50
        /// f = rel_coef(1, 0, 2) * 12 + 政治 - aishou * 7 / 4
        /// n = max(f, 0) / 6 + 2
        /// n = 目标君主厌恶使者本人 ? 2 : n + 2
        /// </code>
        /// 结果区间 2~20（赠送不参与难度系数，原版 calc_donate_like_change 未调用 multiply_difficulty）。
        /// </summary>
        /// <param name="actor">使者（决定政治与关系系数）</param>
        /// <param name="actorGovernor">使者方君主（决定相性差）</param>
        /// <param name="targetGovernor">目标势力君主</param>
        public static int CalcDonateLikeGain(Person actor, Person actorGovernor, Person targetGovernor)
        {
            if (actor == null || targetGovernor == null) return 0;

            int aishou = 50;
            if (actorGovernor != null)
            {
                aishou = Math.Min(actorGovernor.CompatibilityDistance(targetGovernor), 50);
                if (targetGovernor.IsHate(actorGovernor)) aishou = 50;
            }

            float f = GetRelationCoef(targetGovernor, actor, 1, 0, 2) * 12;
            f += actor.Politics - aishou * 7 / 4f;
            f /= 6f;

            int n = (int)Math.Max(f, 0f) + 2;
            return targetGovernor.IsHate(actor) ? 2 : n + 2;
        }

        /// <summary>
        /// 同盟缔结判定 —— 原版 `try_alliance`（`:709-738`）。
        ///
        /// <code>
        /// n = 政治 - 相性差/5 + 金/150 + rel_coef(1, -1, 1) * 10
        /// n = multiply_difficulty(n)
        /// like = 目标对发起方的友好度
        /// n > (110 - like) * 2  → 成功
        /// n > (100 - like) * 2  → 舌战
        /// 否则失败
        /// </code>
        ///
        /// 调用方还需自行处理原版的两个**前置否决**（这里做成参数由调用方判断，
        /// 因为需要查对方手里的俘虏与君主厌恶关系）：
        ///   · 对方手里有我方高价值俘虏 → 直接失败（`get_high_value_horyo`）；
        ///   · 目标君主厌恶发起方君主，且使者没有"论客"技能 → 直接失败。
        /// </summary>
        public static Result TryAlliance(Person actor, Person actorGovernor, Person targetGovernor,
                                         Force actorForce, Force targetForce, int gold, int difficulty)
        {
            if (actor == null || actorForce == null || targetForce == null) return Result.Failure;

            int aishou = actorGovernor != null ? actorGovernor.CompatibilityDistance(targetGovernor) : 0;

            int n = actor.Politics - aishou / 5 + gold / 150
                    + GetRelationCoef(targetGovernor, actor, 1, -1, 1) * 10;
            n = MultiplyDifficulty(n, difficulty);

            int like = actorForce.GetLike(targetForce);
            int successLine = (110 - like) * 2;
            int debateLine = (100 - like) * 2;

            if (n > successLine) return Result.Success;
            if (n > debateLine) return Result.Debate;

            // 原版：n*2 > successLine 时 perfect = false（勉强成功），高难度下会出现
            if (n * 2 > successLine) return Result.SuccessNarrow;

            return Result.Failure;
        }

        /// <summary>
        /// 停战判定 —— 原版 `try_ceasefire`（`:1229-1310`）。
        ///
        /// <code>
        /// n = √政治 * 9                      （0~90）
        /// n -= 相性差² / 90                  （相性惩罚）
        /// n += rel_coef(10, -15, 20)         （亲爱/厌恶/血缘）
        /// n += 金 / 110
        /// n = multiply_difficulty(n)
        /// n >  time*4/3 - like/4 + 80 → 成功
        /// n >  time*4/3 - like/4 + 70 → 舌战
        /// </code>
        /// </summary>
        /// <param name="time">请求的停战期（原版以周计；本项目传回合数）</param>
        public static Result TryCeasefire(Person actor, Person actorGovernor, Person targetGovernor,
                                          Force actorForce, Force targetForce, int time, int gold, int difficulty)
        {
            if (actor == null || actorForce == null || targetForce == null || time <= 0) return Result.Failure;

            int aishou = actorGovernor != null ? actorGovernor.CompatibilityDistance(targetGovernor) : 0;

            int n = (int)(Math.Sqrt(actor.Politics) * 9);
            n -= aishou * aishou / 90;
            n += GetRelationCoef(targetGovernor, actor, 10, -15, 20);
            n += gold / 110;
            n = MultiplyDifficulty(n, difficulty);

            int like = actorForce.GetLike(targetForce);
            int successLine = time * 4 / 3 - like / 4 + 80;
            int debateLine = time * 4 / 3 - like / 4 + 70;

            if (n > successLine) return Result.Success;
            if (n > debateLine) return Result.Debate;
            return Result.Failure;
        }

        /// <summary>
        /// 劝降的硬条件链 —— 原版 `try_annex`（`:1551-1616`）。
        /// 任一不满足直接失败，且**不消耗任何资源**（原版也是这样，先过链再算分）。
        ///
        /// <code>
        /// 场上势力数 ≤ 2        → 不可劝降
        /// 开局未满 1 年         → 不可劝降
        /// 城市数 &lt; 目标 * 4    → 不可劝降
        /// 兵力   &lt; 目标 * 3    → 不可劝降（由调用方传参）
        /// 目标城市数 ≥ 5        → 不可劝降
        /// 双方不相邻            → 不可劝降
        /// 目标君主厌恶发起方君主 → 不可劝降
        /// 目标君主是特定强人设   → 不可劝降（吕布/董卓/张角/刘备/关羽/张飞/孙坚/孙策/曹操）
        /// </code>
        /// </summary>
        /// <param name="aliveForceCount">场上存活势力数</param>
        /// <param name="elapsedYears">已进行的年数</param>
        /// <param name="actorCityCount">发起方城市数</param>
        /// <param name="targetCityCount">目标城市数</param>
        /// <param name="actorTroops">发起方兵力</param>
        /// <param name="targetTroops">目标兵力</param>
        /// <param name="neighbored">双方是否相邻</param>
        /// <param name="targetGovernorHatesActor">目标君主是否厌恶发起方君主</param>
        /// <param name="targetGovernorName">目标君主姓名（用于特定人设排除）</param>
        /// <param name="reason">不满足时的原因（用于 UI 提示）</param>
        public static bool CanAnnex(int aliveForceCount, int elapsedYears,
                                    int actorCityCount, int targetCityCount,
                                    int actorTroops, int targetTroops,
                                    bool neighbored, bool targetGovernorHatesActor,
                                    string targetGovernorName, out string reason)
        {
            reason = null;

            if (aliveForceCount <= 2) { reason = "场上势力不足三家，无法劝降"; return false; }
            if (elapsedYears < 1) { reason = "开局未满一年，无法劝降"; return false; }
            if (actorCityCount < targetCityCount * 4) { reason = "城市数未达对方的四倍"; return false; }
            if (actorTroops < targetTroops * 3) { reason = "兵力未达对方的三倍"; return false; }
            if (targetCityCount >= 5) { reason = "对方城池过多，不会投降"; return false; }
            if (!neighbored) { reason = "双方不相邻，无法劝降"; return false; }
            if (targetGovernorHatesActor) { reason = "对方君主敌视我方君主"; return false; }

            // 原版按目标君主 Id 硬排除的"宁死不降"人设（`:1560` 的 switch）。
            // 【为什么用名字而不是 Id】原版的 PersonId 常量与本项目武将库的 Id 完全不是一套，
            // 照抄数字会错配到无关武将；名字在两边都稳定，等多语言/改名需求出现时再换成
            // 配置化的"人设标签"。
            if (!string.IsNullOrEmpty(targetGovernorName)
                && Array.IndexOf(UnyieldingGovernors, targetGovernorName) >= 0)
            {
                reason = "对方君主宁死不降";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 原版 `try_annex` 里"宁死不降"的君主名单（`:1560`）。
        /// 按姓名比对，原因见 <see cref="CanAnnex"/> 内的说明。
        /// </summary>
        static readonly string[] UnyieldingGovernors =
        {
            "吕布", "董卓", "张角", "刘备", "关羽", "张飞", "孙坚", "孙策", "曹操",
        };

        /// <summary>
        /// 劝降判定 —— 原版 `try_annex` 的评分段（s11_sys_person_order_diplomacy.cpp:1600-1616）。
        ///
        /// <code>
        /// n += (act_city / tgt_city) / 2;
        /// n += (act_city - tgt_city) / 5;
        /// n -= tgt_kunshu->get_aishou_distance(self->actor->get_id()) / 7;   // 使者相性
        /// n += act_force->is_protecting_the_emperor() ? 10 : 0;
        /// n += get_relation_coef_int(self, 10, 0, 20);                       // 目标君主 ↔ 使者
        /// n += self->actor->get_stat(PersonStatType_Charisma);               // 使者魅力
        /// n  = multiply_difficulty(n, 0.8f, 0.7f) / 2;
        /// n += rand_int(n, ...);                                             // 0 ~ n-1
        /// g  = tgt_kunshu->get_seikaku() * 7 - tgt->get_like(act_force) / 4;
        /// perfect = (n * 2 &lt; g + 100);
        /// n >= g + 100 → Success / SuccessNarrow
        /// n >= g + 95  → Debate
        /// else         → Failure
        /// </code>
        ///
        /// 【关键：三项个人项用的是**使者**，不是发起方君主】
        /// 原版这三处全部读 `self->actor`（被派去劝降的那个人）。旧实现传的是
        /// `Sender.mGovernor`，等于"派谁去都一样"，这既是"随便派人成功率也高"的根源，
        /// 也让政治/魅力高的使者毫无价值。
        ///
        /// 【性格来源】原版的"目标性格"是 0~3 的连续档位（SeikakuEnum），本项目不再映射枚举，
        /// 改由性格数据上的 <c>annexResistance</c> 直接给出档位值：
        /// 胆小 0 / 冷静 1 / 刚胆 2 / 莽撞 3，新增性格可自由填任意整数。
        /// 调用方传 PersonalityId（1 起的表主键）会让 g 整体偏高 7，是错误用法。
        /// </summary>
        /// <param name="actor">执行劝降的**使者**（原版 self-&gt;actor）</param>
        /// <param name="targetGovernor">目标势力君主</param>
        /// <param name="actorForce">发起方势力</param>
        /// <param name="targetForce">目标势力</param>
        /// <param name="actorCityCount">发起方城池数</param>
        /// <param name="targetCityCount">目标城池数</param>
        /// <param name="targetPersonality">目标君主性格档位（annexResistance，0~3）</param>
        /// <param name="difficulty">难度（0 简单 / 1 普通 / 2 困难）</param>
        /// <param name="protectingEmperor">发起方是否保护皇帝（原版 is_protecting_the_emperor）</param>
        public static Result TryAnnex(Person actor, Person targetGovernor,
                                      Force actorForce, Force targetForce,
                                      int actorCityCount, int targetCityCount,
                                      int targetPersonality, int difficulty,
                                      bool protectingEmperor = false)
        {
            if (actor == null || targetGovernor == null || actorForce == null || targetForce == null)
                return Result.Failure;

            // 目标君主 ↔ 使者的相性距离（原版 get_aishou_distance(self->actor->get_id())）
            int aishou = targetGovernor.CompatibilityDistance(actor);

            int n = (actorCityCount / Math.Max(targetCityCount, 1)) / 2
                    + (actorCityCount - targetCityCount) / 5
                    - aishou / 7
                    + (protectingEmperor ? 10 : 0)
                    + GetRelationCoef(targetGovernor, actor, 10, 0, 20)
                    + actor.Glamour;

            n = MultiplyDifficulty(n, difficulty) / 2;

            // 原版 :1610 `n += system_->rand_int(n, ...)`：rand_int(max) 取 0 ~ max-1，
            // 即 0 ~ n-1。旧实现写 0 ~ n/2，随机段只有原版一半，判定整体偏低。
            n += GameRandom.Range(0, Math.Max(1, n));

            int g = targetPersonality * 7 - actorForce.GetLike(targetForce) / 4;

            if (n >= g + 100)
            {
                // 原版 :1612-1616：`perfect` 默认 true，`n*2 >= g+100` 时置 false（勉强成功）
                return n * 2 < g + 100 ? Result.Success : Result.SuccessNarrow;
            }
            if (n >= g + 95) return Result.Debate;
            return Result.Failure;
        }

        /// <summary>
        /// 武将"价值"近似值（0~100），用于俘虏交换的评分。
        ///
        /// 【原版对照】原版 `get_horyo_value` 与交换公式里的 `req_max` 都基于五维综合，
        /// 这里取五项均值作为同一量级的替代（原版具体权重未在调研中还原）。
        /// </summary>
        public static int PersonValue(Person person)
        {
            // 【严格对齐】Open311 的 System::get_horyo_value（s11_sys_scen_ai.cpp:935-974），
            // 返回 **0~10**（不是我原先假设的 0~100 均值）：
            //   · 五维里每有一项 ≥ 80 记 1 分；
            //   · 五维总和 ≥ 350 再记 1 分；
            //   · 基础分封顶 3；
            //   · 再加"目标君主与他的关系"：义兄弟/夫妻 +7、亲爱 +5、血缘 +4。
            if (person == null) return 0;

            int sumStat = 0;
            int value = 0;

            int[] stats = { person.Command, person.Strength, person.Intelligence, person.Politics, person.Glamour };
            for (int i = 0; i < stats.Length; i++)
            {
                sumStat += stats[i];
                if (stats[i] >= 80)
                    value++;
            }

            if (sumStat >= 350)
                value++;

            if (value > 3)
                value = 3;

            // 关系加成：以"俘虏的君主"为基准，看君主与他的关系（原版 :964-972 的优先级：
            // 夫妻/义兄弟 +7 → 亲爱 +5 → 血缘 +4）
            Force owner = person.BelongForce;
            Person kunshu = owner != null ? owner.mGovernor : null;
            if (kunshu != null)
            {
                if (kunshu.Brother == person || kunshu.IsSpouse(person))
                    value += 7;
                else if (kunshu.IsLike(person))
                    value += 5;
                else if (kunshu.IsBloodRelative(person))
                    value += 4;
            }

            return value;
        }

        /// <summary>
        /// 五维中的最高单项（0~100）。
        ///
        /// 【用途】俘虏交换的 `g` 用的是这个，不是 `get_horyo_value`：
        /// 原版 `g = (request_max_stat[0]² + request_max_stat[1]²) / 400 - like/2`
        /// （`s11_sys_person_order_diplomacy.cpp:1957`，注释写明区间 -50..50），
        /// 只有 0~100 的量级才能落在这个区间。
        /// </summary>
        public static int GetMaxStat(Person person)
        {
            if (person == null) return 0;

            int max = person.Command;
            if (person.Strength > max) max = person.Strength;
            if (person.Intelligence > max) max = person.Intelligence;
            if (person.Politics > max) max = person.Politics;
            if (person.Glamour > max) max = person.Glamour;
            return max;
        }

        /// <summary>五维总和（0~500）。原版 `release_sum_stat`。</summary>
        public static int GetSumStat(Person person)
        {
            if (person == null) return 0;
            return person.Command + person.Strength + person.Intelligence
                   + person.Politics + person.Glamour;
        }

        /// <summary>
        /// 释放者关系系数（原版 exchange 公式里的 release_relation_coef）：
        /// 血缘 3 / 亲爱 4 / 厌恶 5 / 其它 6。系数越小，同样的人越"值钱"（加的 n 越多）。
        /// </summary>
        public static int GetReleaseCoef(Person targetGovernor, Person released)
        {
            if (targetGovernor == null || released == null) return 6;
            if (targetGovernor.Brother == released || targetGovernor.IsSpouse(released)) return 3;
            if (targetGovernor.IsLike(released)) return 4;
            if (targetGovernor.IsHate(released)) return 5;
            return 6;
        }

        /// <summary>
        /// 俘虏交换判定 —— 原版 `try_exchange`（`s11_sys_person_order_diplomacy.cpp:1909-1969`）。
        ///
        /// <code>
        /// n = 政治
        /// n += rel_coef(15, -15, 20)
        /// n += 我方释放者价值 / release_coef(血缘3/亲爱4/厌恶5/其它6)
        /// n -= min(相性差, 50) / 2
        /// n += 金 / 40
        /// n = multiply_difficulty(n)
        /// g = (索要者价值² + 释放者价值²) / 400 - like / 2
        /// n >= g + 100 → 成功；n >= g + 90 → 舌战
        /// </code>
        /// </summary>
        /// <param name="offer">我方交出的俘虏（可为 null = 只索要）</param>
        /// <param name="request">我方索要的对方俘虏</param>
        public static Result TryExchange(Person actor, Person actorGovernor, Person targetGovernor,
                                         Force actorForce, Force targetForce,
                                         Person offer, Person request, int gold, int difficulty)
        {
            if (actor == null || actorForce == null || targetForce == null) return Result.Failure;

            int n = actor.Politics;
            n += GetRelationCoef(targetGovernor, actor, 15, -15, 20);

            // 原版 :1953 `release_sum_stat / release_relation_coef`
            // —— 用的是**五维总和**，不是 get_horyo_value
            if (offer != null)
                n += GetSumStat(offer) / GetReleaseCoef(targetGovernor, offer);

            int aishou = actorGovernor != null ? actorGovernor.CompatibilityDistance(targetGovernor) : 0;
            n -= Math.Min(aishou, 50) / 2;
            n += gold / 40;
            n = MultiplyDifficulty(n, difficulty);

            // 原版 :1957 `g = (request_max_stat[0]² + request_max_stat[1]²) / 400 - like/2`
            // request_max_stat = 五维最高单项（0~100），注释给出区间 -50..50
            int g = 0;
            if (request != null)
            {
                int v = GetMaxStat(request);
                g += v * v / 400;
            }
            if (offer != null)
            {
                int v = GetMaxStat(offer);
                g += v * v / 400;
            }
            g -= actorForce.GetLike(targetForce) / 2;

            // 原版 :1958-1968：
            //   n*2 >= g+100 → perfect = false（勉强成功）；
            //   n >= g+100   → 成功；n >= g+90 → 舌战；否则失败。
            if (n >= g + 100)
                return n * 2 >= g + 100 ? Result.SuccessNarrow : Result.Success;
            if (n >= g + 90) return Result.Debate;
            return Result.Failure;
        }

        /// <summary>
        /// 援军请求判定 —— 原版 `try_reinforce`（`s11_sys_person_order_diplomacy.cpp:2437-2482`）。
        ///
        /// <code>
        /// n = (rand(0~4) + 12) * 政治 / 24
        /// n -= (2000 - min(金, 1000)) * 相性差 / 2000
        /// n += 金 / 100 + rel_coef(15, -20, 20) + like
        /// n = multiply_difficulty(n)
        /// g = ((10 - min(源城武将数, 8)) * 4 + (10 - min(源城武将数, 8)) + 目标据点 like) * 2
        ///     + (30000 - min(源城兵力, 20000)) / 120
        /// n >= g → 成功；n >= g - 20 → 舌战
        /// </code>
        /// </summary>
        /// <param name="srcOfficerCount">出兵据点的武将数</param>
        /// <param name="destLike">目标据点所属势力对我方的友好度</param>
        /// <param name="srcTroops">出兵据点的兵力</param>
        public static Result TryReinforce(Person actor, Person actorGovernor, Person targetGovernor,
                                          Force actorForce, Force targetForce, int gold,
                                          int srcOfficerCount, int destLike, int srcTroops, int difficulty)
        {
            if (actor == null || actorForce == null || targetForce == null) return Result.Failure;

            int aishou = actorGovernor != null ? actorGovernor.CompatibilityDistance(targetGovernor) : 0;

            int n = (GameRandom.Range(0, 5) + 12) * actor.Politics / 24;
            n -= (2000 - Math.Min(gold, 1000)) * aishou / 2000;
            n += gold / 100
                 + GetRelationCoef(targetGovernor, actor, 15, -20, 20)
                 + actorForce.GetLike(targetForce);
            n = MultiplyDifficulty(n, difficulty);

            int officers = Math.Min(srcOfficerCount, 8);
            int g = ((10 - officers) * 4 + (10 - officers) + destLike) * 2
                    + (30000 - Math.Min(srcTroops, 20000)) / 120;

            // 原版 :2474-2481：
            //   n*2 >= g → perfect = false（勉强成功）；
            //   n >= g   → 成功；
            //   否则**重掷一次**（rand(7) 而不是 rand(5)）再与 g-20 比较，决定是否进舌战。
            if (n >= g)
                return n * 2 >= g ? Result.SuccessNarrow : Result.Success;

            int n2 = (GameRandom.Range(0, 7) + 12) * actor.Politics / 24;
            n2 -= (2000 - Math.Min(gold, 1000)) * aishou / 2000;
            n2 += gold / 100
                  + GetRelationCoef(targetGovernor, actor, 15, -20, 20)
                  + actorForce.GetLike(targetForce);
            n2 = MultiplyDifficulty(n2, difficulty);

            if (n2 >= g - 20) return Result.Debate;
            return Result.Failure;
        }
    }
}
