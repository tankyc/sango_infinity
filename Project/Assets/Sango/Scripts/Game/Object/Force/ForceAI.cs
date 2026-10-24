using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace Sango.Core
{
    /// <summary>
    /// 势力 AI 命令集合。
    /// <para>partial 拆分：ForceAIOutpost.cs 承载"据点态势采集 + 守将指派"（对齐原版 load_force_data）。</para>
    /// </summary>
    public partial class ForceAI
    {


        /// <summary>
        /// AI个性类型
        /// </summary>
        public enum AIPersonalityType
        {
            Aggressive,    // 侵略型
            Defensive,     // 防御型
            Diplomatic,    // 外交型
            Economic,      // 经济型
            Balanced       // 平衡型
        }

        /// <summary>
        /// 获取势力的AI个性
        /// </summary>
        public static AIPersonalityType GetAIPersonality(Person person)
        {
            // 基于势力领袖的性格决定AI个性
            if (person != null && person.mPersonality != null)
            {
                // 基于性格特征计算AI个性
                int warScore = person.mPersonality.warTendencyAdd;
                int defenseScore = person.mPersonality.defenseTendencyAdd;
                int diplomacyScore = person.mPersonality.diplomacyTendencyAdd;
                int economicScore = person.mPersonality.economicTendencyAdd;

                // 找出最高得分的个性类型
                int maxScore = System.Math.Max(System.Math.Max(warScore, defenseScore), System.Math.Max(diplomacyScore, economicScore));

                // 【修复】无任何倾向(全为 0)时判定为平衡型。
                // 原逻辑在最高分并列时按 war→defense→diplomacy→economic 顺序短路,
                // 导致"无明显倾向"的君主一律被判为侵略型。
                if (maxScore <= 0)
                    return AIPersonalityType.Balanced;

                // 最高分并列时同样判定为平衡型
                int maxCount = 0;
                if (warScore == maxScore) maxCount++;
                if (defenseScore == maxScore) maxCount++;
                if (diplomacyScore == maxScore) maxCount++;
                if (economicScore == maxScore) maxCount++;
                if (maxCount > 1)
                    return AIPersonalityType.Balanced;

                if (maxScore == warScore)
                    return AIPersonalityType.Aggressive;
                else if (maxScore == defenseScore)
                    return AIPersonalityType.Defensive;
                else if (maxScore == diplomacyScore)
                    return AIPersonalityType.Diplomatic;
                else if (maxScore == economicScore)
                    return AIPersonalityType.Economic;
                else
                    return AIPersonalityType.Balanced;
            }
            return AIPersonalityType.Balanced;
        }

        /// <summary>
        /// AI 外交调度 —— 对齐 Open311 的 `GameAI::force_diplomacy`
        /// （`s11_game_ai_force_cmd_dip.cpp:5-74`）。
        ///
        /// 【原版的固定顺序】annex → discard → alliance → donate → ceasefire → exchange → reinforce，
        /// 逐项 try，某项成功即止。第一版实现 **alliance / donate / ceasefire** 三项：
        ///   · annex 需要实力评分与"相邻敌对势力数"统计，放下一版；
        ///   · discard 需要"无战事周数"统计，放下一版；
        ///   · exchange 已并入 <see cref="AICaptives"/>（赎买己方俘虏走 Exchange 的只索要模式）；
        ///   · reinforce 需要部队部署系统配合（约定出兵后得真的派兵）。
        ///
        /// 【开门条件】前 9 回合不外交（原版 `get_turn() < 9`）、和平期不外交、
        /// 禁谈期内不外交（`diplomacyBanTimer`）、首都必须有空闲武将可当使者。
        /// 之后每回合整体只掷一次 30%（原版 `rand_bool(30)`），不是每个势力每回合都外交。
        /// </summary>
        public static bool AIDiplomacy(Force force, Scenario scenario)
        {
            if (force == null || scenario == null) return true;
            if (!force.IsAlive) return true;
            if (force.IsPlayer) return true;
            if (scenario.IsPeacePeriod) return true;
            if (force.mGovernor == null || force.CapitalCity == null) return true;
            if (force.CapitalCity.freePersons == null || force.CapitalCity.freePersons.Count == 0) return true;

            int turn = scenario.TurnCount;
            if (turn < 9) return true;                  // 原版：前 9 回合完全不外交
            if (force.diplomacyBanTimer > 0) return true;

            // 【原版结构】没有全局概率，七项各自判频率，成功即止（:7-74）。
            // 顺序：annex → discard → alliance → donate → ceasefire → exchange → reinforce。
            if (TryAIAnnex(force, scenario, turn)) return true;
            if (TryAIDiscard(force, scenario, turn)) return true;
            if (TryAIAlliance(force, scenario, turn)) return true;
            if (TryAIDonate(force, scenario, turn)) return true;
            if (TryAICeasefire(force, scenario, turn)) return true;
            if (TryAIExchange(force, scenario, turn)) return true;
            if (TryAIReinforce(force, scenario, turn)) return true;

            return true;
        }

        /// <summary>
        /// 本方是否有"交战中的据点"（原版 `据点状态计数[Battle] != 0`）。
        ///
        /// 【近似口径】敌方部队正处在我方城池上 = 领地被入侵。
        /// 原版是 AI 表里逐据点维护的实时状态，这里每次现算（O(部队数)），
        /// 对外交这种低频决策完全够用。
        /// </summary>
        static bool HasBattleOutpost(Force force, Scenario scenario)
        {
            if (scenario == null || scenario.troopsSet == null) return false;

            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop == null || !troop.IsAlive) continue;

                Force tf = troop.BelongForce;
                if (tf == null || tf == force) continue;
                if (force.IsAlliance(tf)) continue;

                City c = troop.CurrentCity;
                if (c != null && c.BelongForce == force)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// AI 主动结盟 —— 原版 `can_attempt_alliance`（`s11_game_ai_force_cmd_dip.cpp:317-331`）：
        /// 友好度 ≥ 70、尚未同盟、且**自己正被威胁**（原版 `has_hostile`）才考虑。
        /// 本项目用"存在友好度 &lt; 20 的相邻势力"近似"被威胁"，避免依赖未统一的敌对字段。
        /// </summary>
        static bool TryAIAlliance(Force force, Scenario scenario, int turn)
        {
            // 原版：rand_bool(30)（:234），且**本方没有交战据点**（:236）才谈结盟
            if (HasBattleOutpost(force, scenario)) return false;
            if (!GameRandom.Chance(30)) return false;

            if (force.NeighborForceList == null || force.NeighborForceList.Count == 0)
                return false;

            bool threatened = false;
            for (int i = 0; i < force.NeighborForceList.Count; i++)
            {
                Force n = force.NeighborForceList[i];
                if (n != null && n.IsAlive && force.GetLike(n) < 20) { threatened = true; break; }
            }
            if (!threatened) return false;

            // 原版 `ai_table_alliance_target_comp` 的择优方向由 `get_aishou_first_chance` 决定：
            //   aishou_first = true  → 挑**君主相性最近**的（重情义）
            //   aishou_first = false → 挑**友好度最高**的（重利益）
            // 旧实现固定按友好度排序，等于把这条分支写死了。
            bool aishouFirst = GameRandom.Chance(GetAishouFirstChance(force));

            Force best = null;
            int bestAishou = int.MaxValue;
            int bestLike = -1;
            for (int i = 0; i < force.NeighborForceList.Count; i++)
            {
                Force n = force.NeighborForceList[i];
                if (n == null || !n.IsAlive || n == force) continue;
                if (force.IsAlliance(n)) continue;

                int like = force.GetLike(n);
                if (like < DiplomacyRules.AiAllianceLikeThreshold) continue;

                int aishou = force.mGovernor != null && n.mGovernor != null
                    ? force.mGovernor.CompatibilityDistance(n.mGovernor)
                    : 999;

                bool better = aishouFirst
                    ? (aishou < bestAishou || (aishou == bestAishou && like > bestLike))
                    : (like > bestLike || (like == bestLike && aishou < bestAishou));

                if (better)
                {
                    bestAishou = aishou;
                    bestLike = like;
                    best = n;
                }
            }

            if (best == null) return false;

            Person diplomat = FindSuitableDiplomat(force);
            if (diplomat == null) return false;

            DiplomacyManager dm = GameSystem.GetSystem<DiplomacyManager>();
            DiplomacyActionBase action = dm.CreateDiplomacyAction(
                DiplomacyActionType.Alliance, force, best, diplomat, GetDiplomaticGoldUse(force, best));
            if (action == null || !action.CanPerform()) return false;

            dm.DispatchDiplomat(action);
            Sango.Log.Info($"@外交AI@{force.Name} 派遣 {diplomat.Name} 向 {best.Name} 提议结盟（友好度 {bestLike}）");
            return true;
        }

        /// <summary>
        /// AI 主动送礼 —— 原版 `force_donate`（`s11_game_ai_force_cmd_dip.cpp:334-427`）：
        /// 送礼是**为结盟铺路**的手段 —— 目标友好度达到 70（同盟门槛）后就不再送。
        /// 原版还要求 `turn % 5 == force_id % 5` 错峰，这里保留。
        /// </summary>
        static bool TryAIDonate(Force force, Scenario scenario, int turn)
        {
            // 原版：rand_bool(30)（:340）+ 无交战据点（:342）+ turn % 5 错峰（:346）
            if (HasBattleOutpost(force, scenario)) return false;
            if (!GameRandom.Chance(30)) return false;
            if (turn % 5 != force.Id % 5) return false;

            if (force.NeighborForceList == null || force.NeighborForceList.Count == 0)
                return false;

            // 原版 `force_donate`：弱势力（power_rank < 50）金门槛 2500，其它 5000
            int rank = GetPowerRank(force, scenario);
            bool weak = rank < 50;
            int minGold = weak ? 2500 : 5000;

            City capital = force.CapitalCity;
            if (capital == null || capital.gold < minGold) return false;

            // 目标：友好度最高、且还没到"停止赠送"门槛的相邻势力。
            // 原版停止门槛：已同盟 60，未同盟 70（即结盟线）—— 送礼只为铺路，够线就停。
            Force best = null;
            int bestLike = -1;
            for (int i = 0; i < force.NeighborForceList.Count; i++)
            {
                Force n = force.NeighborForceList[i];
                if (n == null || !n.IsAlive || n == force) continue;

                int like = force.GetLike(n);
                int stopLike = force.IsAlliance(n) ? 60 : DiplomacyRules.AiAllianceLikeThreshold;
                if (like >= stopLike) continue;
                if (like < 20) continue;                                        // 险恶关系送礼也无用
                if (like > bestLike) { bestLike = like; best = n; }
            }

            if (best == null) return false;

            Person diplomat = FindSuitableDiplomat(force);
            if (diplomat == null) return false;

            DiplomacyManager dm = GameSystem.GetSystem<DiplomacyManager>();
            DiplomacyActionBase action = dm.CreateDiplomacyAction(
                DiplomacyActionType.SendGift, force, best, diplomat, 1000);
            if (action == null || !action.CanPerform()) return false;

            dm.DispatchDiplomat(action);
            Sango.Log.Info($"@外交AI@{force.Name} 派遣 {diplomat.Name} 向 {best.Name} 送礼（友好度 {bestLike}）");
            return true;
        }

        /// <summary>
        /// AI 主动停战 —— 原版 `force_ceasefire` / `force_preventive_ceasefire`
        /// （`s11_game_ai_force_cmd_dip.cpp:442-682`）：
        /// 与关系恶劣的相邻势力求和，期限按实力排名折算（`power_rank * 72 / 100`，夹在 6~72 周）。
        /// 本项目用"相邻势力里友好度最低者"近似"打不过的那一家"。
        /// </summary>
        static bool TryAICeasefire(Force force, Scenario scenario, int turn)
        {
            // 原版两条路径（:530 战时 / :632 预防）：
            //   · 有交战据点 → turn % 21 错峰，且只对"打进来的那家"求和；
            //   · 无交战据点 → turn % 6 错峰（势力数 ≥3 时才错峰），属于"预防性停战"。
            bool inBattle = HasBattleOutpost(force, scenario);
            if (inBattle)
            {
                if (turn % 21 != force.Id % 21) return false;
            }
            else
            {
                if (CountAliveForces(scenario) >= 3 && turn % 6 != force.Id % 6) return false;
            }

            if (force.NeighborForceList == null || force.NeighborForceList.Count == 0)
                return false;

            // 战时：原版直接锁定"打进来的那家"（`:521-545` 扫描进入我领地的敌军），
            // 而不是"关系最差的邻居" —— 求和对象必须是真的在打你的势力。
            Force invader = null;
            if (inBattle && scenario.troopsSet != null)
            {
                for (int i = 0; i < scenario.troopsSet.Count; i++)
                {
                    Troop troop = scenario.troopsSet[i];
                    if (troop == null || !troop.IsAlive) continue;

                    Force tf = troop.BelongForce;
                    if (tf == null || tf == force || force.IsAlliance(tf)) continue;

                    City c = troop.CurrentCity;
                    if (c != null && c.BelongForce == force) { invader = tf; break; }
                }
            }

            Force worst = null;
            int worstLike = 101;
            for (int i = 0; i < force.NeighborForceList.Count; i++)
            {
                Force n = force.NeighborForceList[i];
                if (n == null || !n.IsAlive || n == force) continue;
                if (force.IsAlliance(n) || force.IsTruce(n)) continue;

                int like = force.GetLike(n);
                if (like < worstLike) { worstLike = like; worst = n; }
            }

            // 有入侵者时以它为准（哪怕关系不算太差，也在打仗）
            if (invader != null)
            {
                worst = invader;
                worstLike = force.GetLike(invader);
            }

            if (worst == null || (!inBattle && worstLike >= 40)) return false;

            Person diplomat = FindSuitableDiplomat(force);
            if (diplomat == null) return false;

            // 期限：原版 `time = power_rank * 72 / 100`，clamp 6..72（`:600`）
            int rank = GetPowerRank(force, scenario);
            int weeks = rank * 72 / 100;
            if (weeks < 6) weeks = 6;
            if (weeks > 72) weeks = 72;

            DiplomacyManager dm = GameSystem.GetSystem<DiplomacyManager>();
            DiplomacyActionTruce truce = dm.CreateDiplomacyAction(
                DiplomacyActionType.Truce, force, worst, diplomat, GetDiplomaticGoldUse(force, worst)) as DiplomacyActionTruce;
            if (truce == null) return false;

            truce.TruceDuration = weeks;
            if (!truce.CanPerform()) return false;

            dm.DispatchDiplomat(truce);
            Sango.Log.Info($"@外交AI@{force.Name} 派遣 {diplomat.Name} 向 {worst.Name} 提议停战" +
                           $"（友好度 {worstLike}，期限 {weeks} 回合，实力排名 {rank}）");
            return true;
        }

        /// <summary>
        /// "相性优先"的概率（对应原版 `get_aishou_first_chance`，`s11_sys_scen_ai.cpp:984-998`）：
        ///
        /// <code>
        /// n = 4 + 义理 - 野心        // 0..8
        /// n &lt; 4 → 5 + n*10          // 5..35：野心大，更看重利益（挑相性近的）
        /// 否则   → 50 + n*5          // 70..90：义理高，更看重情义（挑友好的）
        /// </code>
        ///
        /// 义理取 `mArgumentation.kind - 1`（项目 kind 是 1~5，要换算成原版的 0~4），
        /// 野心直接用 `Person.ambition`（剧本数据里本就是 0~4）。
        /// </summary>
        static int GetAishouFirstChance(Force force)
        {
            if (force == null || force.mGovernor == null) return 0;

            int giri = force.mGovernor.mArgumentation != null
                ? force.mGovernor.mArgumentation.kind - 1
                : 1;
            int n = 4 + giri - force.mGovernor.ambition;

            if (n < 0) n = 0;
            if (n > 8) n = 8;

            return n < 4 ? 5 + n * 10 : 50 + n * 5;
        }

        /// <summary>
        /// 外交金预算（对应原版 `ai_table_get_diplomatic_gold_use`，`s11_sys_scen_ai.cpp:861-868`）：
        /// AI→AI 固定 1000；AI→玩家 `1000 + 1500 * rand(100-like) / 100`，clamp 1000~2500。
        /// 关系越差，越愿意多花钱。
        /// </summary>
        static int GetDiplomaticGoldUse(Force src, Force dest)
        {
            const int baseGold = 1000;
            if (dest == null || !dest.IsPlayer) return baseGold;

            int like = src != null ? src.GetLike(dest) : 0;
            if (like < 0) like = 0;
            if (like > 100) like = 100;

            int gold = baseGold + 1500 * GameRandom.Range(0, 101 - like) / 100;
            if (gold < 1000) gold = 1000;
            if (gold > 2500) gold = 2500;
            return gold;
        }

        /// <summary>
        /// 势力实力排名（0~100，越高越强）。对应原版 `context_->force[id].power_rank`。
        ///
        /// 【近似口径】原版由 AI 表定期维护，这里按"城数×10 + 兵力/1000"现算并取百分位。
        /// 用于劝降概率、送礼 min_gold（弱势力 2500 / 常规 5000）、停战期限折算。
        /// </summary>
        internal static int GetPowerRank(Force force, Scenario scenario)
        {
            if (force == null || scenario == null || scenario.forceSet == null)
                return 0;

            int selfScore = force.CityCount * 10 + force.FightPower / 1000;
            int lower = 0;
            int total = 0;

            for (int i = 0; i < scenario.forceSet.Count; i++)
            {
                Force f = scenario.forceSet[i];
                if (f == null || !f.IsAlive) continue;

                total++;
                int score = f.CityCount * 10 + f.FightPower / 1000;
                if (score < selfScore) lower++;
            }

            return total > 0 ? lower * 100 / total : 0;
        }

        /// <summary>
        /// 势力的"玩家接壤排名"（0~100，越高越倾向对玩家强硬）。
        ///
        /// 对齐出处：Open311 `s11_game_ai.cpp:181-349` 的
        /// `context_->force[id].player_neighbored_rank` 计算。
        /// <code>
        /// 若 场上势力数 &lt; 4 → 全部 100（小局不让 AI 欺负玩家）
        /// 否则排序：与玩家接壤的玩家势力数 降序 → 国力 降序 → Id
        ///           rank = 100，step = 100 / n，逐个赋值并递减（clamp 0~100）
        /// </code>
        /// 消费者：
        ///   · IsNeedHostile —— `like &lt; n + rank/10`（越高越容易把玩家列为敌人）
        ///   · 外交对象选择 —— `rand_bool(rank)` 命中则跳过对玩家的示好
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>0~100；不是 AI 势力或无玩家时返回 0</returns>
        internal static int GetPlayerNeighboredRank(Force force, Scenario scenario)
        {
            if (force == null || scenario == null || scenario.forceSet == null)
                return 0;

            List<Force> aiList = new List<Force>();
            int playerCount = 0;
            for (int i = 0; i < scenario.forceSet.Count; i++)
            {
                Force f = scenario.forceSet[i];
                if (f == null || !f.IsAlive)
                    continue;
                if (f.IsPlayer)
                    playerCount++;
                else
                    aiList.Add(f);
            }

            if (playerCount == 0)
                return 0;
            if (aiList.Count == 0)
                return 100;
            // 场上势力总数 < 4 → 全部 100 —— :181-189
            if (aiList.Count + playerCount < 4)
                return 100;

            aiList.Sort((a, b) =>
            {
                int an = CountNeighborPlayerForces(a);
                int bn = CountNeighborPlayerForces(b);
                if (an != bn)
                    return bn - an;
                int ap = GetPowerRank(a, scenario);
                int bp = GetPowerRank(b, scenario);
                if (ap != bp)
                    return bp - ap;
                return a.Id - b.Id;
            });

            int n = aiList.Count;
            int step = 100 / n;
            int rank = 100;
            for (int i = 0; i < n; i++)
            {
                if (aiList[i] == force)
                    return rank;
                rank = System.Math.Clamp(rank - step, 0, 100);
            }
            return 0;
        }

        /// <summary>
        /// 统计某势力接壤的玩家势力数量。
        /// 对齐出处：s11_game_ai.cpp:325-335 的 `a_neighbor_player_count`。
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>接壤的玩家势力数</returns>
        static int CountNeighborPlayerForces(Force force)
        {
            if (force == null || force.NeighborForceList == null)
                return 0;
            int count = 0;
            for (int i = 0; i < force.NeighborForceList.Count; i++)
            {
                Force f = force.NeighborForceList[i];
                if (f != null && f.IsAlive && f.IsPlayer)
                    count++;
            }
            return count;
        }

        /// <summary>场上存活势力数（对应原版 `force_list.size()`）</summary>
        static int CountAliveForces(Scenario scenario)
        {
            if (scenario == null || scenario.forceSet == null) return 0;

            int n = 0;
            for (int i = 0; i < scenario.forceSet.Count; i++)
            {
                Force f = scenario.forceSet[i];
                if (f != null && f.IsAlive) n++;
            }
            return n;
        }

        /// <summary>
        /// AI 劝降 —— 原版 `force_annex`（`:77-126`）：
        /// `turn ≥ 36`、近 18 回合势力数无变化、`rand_bool(power_rank)`。
        /// 本项目没有 `power_rank`，用"本势力城数占全场城数的百分比"近似（越强势越可能劝降）。
        /// </summary>
        static bool TryAIAnnex(Force force, Scenario scenario, int turn)
        {
            if (turn < 36) return false;
            if (CountAliveForces(scenario) <= 2) return false;

            // 原版 :86：最近 18 回合内势力数有变化就跳过 —— 局势还在洗牌时不去劝降。
            // 对应 Scenario.GetForceCountHighDiff（惰性记录，首次调用前历史为空返回 0）。
            if (scenario.GetForceCountHighDiff(18) != 0) return false;

            // 原版：`rand_bool(power_rank)` —— 强国更可能劝降。
            // 旧实现用"城数占全场百分比"，本轮的 GetPowerRank 把兵力也计入，更接近原版实力排名。
            int rank = GetPowerRank(force, scenario);
            if (!GameRandom.Chance(Math.Min(rank, 100))) return false;

            if (force.NeighborForceList == null) return false;

            for (int i = 0; i < force.NeighborForceList.Count; i++)
            {
                Force target = force.NeighborForceList[i];
                if (target == null || !target.IsAlive || target == force) continue;
                if (target.IsPlayer) continue;
                if (force.IsAlliance(target)) continue;

                Person diplomat = FindSuitableDiplomat(force);
                if (diplomat == null) return false;

                DiplomacyManager dm = GameSystem.GetSystem<DiplomacyManager>();
                DiplomacyActionBase action = dm.CreateDiplomacyAction(
                    DiplomacyActionType.Annex, force, target, diplomat, 0);
                if (action == null || !action.CanPerform()) continue;

                dm.DispatchDiplomat(action);
                Sango.Log.Info($"@外交AI@{force.Name} 派遣 {diplomat.Name} 劝降 {target.Name}（实力占比 {rank}%）");
                return true;
            }

            return false;
        }

        /// <summary>
        /// AI 破弃同盟 —— 原版 `force_discard`（`:128-226`）。
        /// 原版两条判据：友好度 ≤ `58 - 义理*3`，或"无战事周数 ≥ `72 + 义理*12`"。
        /// 本项目没有"无战事周数"统计，先只保留友好度这一条（阈值取中位 58）。
        /// </summary>
        static bool TryAIDiscard(Force force, Scenario scenario, int turn)
        {
            if (force.AllianceList == null || force.AllianceList.Count == 0) return false;

            for (int i = 0; i < force.AllianceList.Count; i++)
            {
                Alliance a = force.AllianceList[i];
                if (a == null || !a.IsAlive || a.allianceType != AllianceType.Alliance) continue;

                for (int j = 0; j < a.ForceList.Count; j++)
                {
                    Force other = a.ForceList[j];
                    if (other == null || other == force) continue;

                    // 原版 `:161-226`：阈值 = `58 - 义理*3`（义理越高越念旧，越不容易背盟）。
                    // 【改为配置驱动】每档的具体阈值写在 `Argumentations.json` 的
                    // `discardLikeThreshold`（1→55、2→52、3→49、4→46、5→43），
                    // 策划可以直接调，不必再改代码里的系数；
                    // 未配置（0）时回退到 `58 - kind*3`，保持旧行为。
                    Argumentation arg = force.mGovernor != null ? force.mGovernor.mArgumentation : null;
                    int discardThreshold;
                    if (arg != null && arg.discardLikeThreshold > 0)
                        discardThreshold = arg.discardLikeThreshold;
                    else
                    {
                        int kind = arg != null ? arg.kind : 3;
                        discardThreshold = 58 - kind * 3;
                    }

                    if (force.GetLike(other) > discardThreshold) continue;
                    if (!GameRandom.Chance(30)) continue;

                    a.IsAlive = false;
                    a.leftCount = 0;
                    force.AllianceList.Remove(a);
                    other.AllianceList.Remove(a);

                    // 原版破弃：友好度 -50、技术点 -50（:861-898）
                    force.AddLike(other, DiplomacyRules.DiscardLikePenalty);
                    force.GainTechniquePoint(-DiplomacyRules.DiscardTechCost);

                    // 原版同时 force_break_ceasefire：破弃同盟 = 关系回到敌对，停战一并作废
                    BreakTruce(force, other);

                    Sango.Log.Info($"@外交AI@{force.Name} 破弃了与 {other.Name} 的同盟");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 作废两势力之间的停战协议（对应 Open311 的 `force_break_ceasefire`）。
        /// 破弃同盟时调用：关系回到敌对，停战不能再留着。
        /// </summary>
        static void BreakTruce(Force a, Force b)
        {
            if (a == null || b == null || a.AllianceList == null || b.AllianceList == null)
                return;

            for (int i = a.AllianceList.Count - 1; i >= 0; i--)
            {
                Alliance t = a.AllianceList[i];
                if (t == null || t.allianceType != AllianceType.Truce || !t.Contains(b))
                    continue;

                t.IsAlive = false;
                t.leftCount = 0;
                a.AllianceList.Remove(t);
                b.AllianceList.Remove(t);
            }
        }

        /// <summary>
        /// AI 俘虏交换 —— 原版 `force_exchange`（`:684-775`）。
        /// 本项目的"赎回自家俘虏"已经由 <see cref="AICaptives"/> 走 Exchange 的只索要模式，
        /// 这里不再重复实现"双方换俘"，直接让位，避免同一回合两处派使者。
        /// </summary>
        static bool TryAIExchange(Force force, Scenario scenario, int turn)
        {
            return false;
        }

        /// <summary>
        /// AI 请求援军 —— 原版 `force_reinforce`（`:777-906`）：
        /// `rand_bool(30)`、无交战据点、**只对玩家同盟**提出。
        /// </summary>
        static bool TryAIReinforce(Force force, Scenario scenario, int turn)
        {
            if (force.requestForceId >= 0 || force.requestTimer > 0) return false;
            if (HasBattleOutpost(force, scenario)) return false;
            if (!GameRandom.Chance(30)) return false;

            if (force.AllianceList == null) return false;

            for (int i = 0; i < force.AllianceList.Count; i++)
            {
                Alliance a = force.AllianceList[i];
                if (a == null || !a.IsAlive || a.allianceType != AllianceType.Alliance) continue;

                for (int j = 0; j < a.ForceList.Count; j++)
                {
                    Force ally = a.ForceList[j];
                    if (ally == null || ally == force || !ally.IsPlayer) continue;

                    Person diplomat = FindSuitableDiplomat(force);
                    if (diplomat == null) return false;

                    DiplomacyManager dm = GameSystem.GetSystem<DiplomacyManager>();
                    DiplomacyActionBase action = dm.CreateDiplomacyAction(
                        DiplomacyActionType.Reinforce, force, ally, diplomat, 1000);
                    if (action == null || !action.CanPerform()) continue;

                    dm.DispatchDiplomat(action);
                    Sango.Log.Info($"@外交AI@{force.Name} 派遣 {diplomat.Name} 向盟友 {ally.Name} 请求援军");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 查找合适的外交使者
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>合适的武将</returns>
        private static Person FindSuitableDiplomat(Force force)
        {
            if (force.mGovernor?.BelongCity == null) return null;

            // 使用ForceAI中的外交推荐方法选择合适的武将
            Person[] recommendedDiplomats = CounsellorRecommendDiplomacy(force.CapitalCity.freePersons);
            if (recommendedDiplomats != null && recommendedDiplomats.Length > 0)
            {
                return recommendedDiplomats[0];
            }

            return null;
        }

        /// <summary>
        /// 检查是否处于外交免疫期
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="targetForceId">目标势力ID</param>
        /// <returns>是否处于免疫期</returns>
        public static bool IsInDiplomacyImmunity(Force force, int targetForceId)
        {
            if (force.DiplomacyImmunityTime.TryGetValue(targetForceId, out int immunityEndTime))
            {
                return immunityEndTime > (Scenario.Cur?.TurnCount ?? 0);
            }
            return false;
        }

        /// <summary>
        /// 清理过期的外交免疫时间
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景</param>
        private static void CleanupDiplomacyImmunity(Force force, Scenario scenario)
        {
            if (scenario == null || force.DiplomacyImmunityTime.Count == 0) return;

            List<int> expiredForces = new List<int>();
            foreach (var kvp in force.DiplomacyImmunityTime)
            {
                if (kvp.Value <= scenario.TurnCount)
                {
                    expiredForces.Add(kvp.Key);
                }
            }

            foreach (int forceId in expiredForces)
            {
                force.DiplomacyImmunityTime.Remove(forceId);
                force.DiplomacyFailCount.Remove(forceId);
            }
        }

        /// <summary>
        /// 记录外交失败
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="targetForceId">目标势力ID</param>
        private static void RecordDiplomacyFailure(Force force, int targetForceId)
        {
            // 增加失败次数
            if (force.DiplomacyFailCount.TryGetValue(targetForceId, out int failCount))
            {
                failCount++;
                force.DiplomacyFailCount[targetForceId] = failCount;
            }
            else
            {
                force.DiplomacyFailCount[targetForceId] = 1;
            }

            // 如果失败次数超过3次，设置外交免疫时间
            if (force.DiplomacyFailCount[targetForceId] >= 3)
            {
                // 设置30天的免疫时间
                force.DiplomacyImmunityTime[targetForceId] = (Scenario.Cur?.TurnCount ?? 0) + 30;
                force.DiplomacyFailCount.Remove(targetForceId);

#if SANGO_DEBUG
                Force targetForce = Scenario.Cur?.forceSet.Get(targetForceId);
                if (targetForce != null)
                {
                    Sango.Log.Info($"@外交@{force.Name} 对 {targetForce.Name} 的外交连续失败3次，进入30天外交免疫期！");
                }
#endif
            }
        }

        /// <summary>
        /// 检查是否需要撕毁条约
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景</param>
        /// <param name="PersonalityId">AI个性</param>
        /// <returns>是否执行了撕毁条约</returns>
        private static bool CheckBreakAlliance(Force force, Scenario scenario, AIPersonalityType PersonalityId)
        {
            // 检查所有当前的同盟和停战协议
            List<Alliance> alliancesToBreak = new List<Alliance>();
            List<Force> targetsToBreak = new List<Force>();

            foreach (Alliance alliance in force.AllianceList)
            {
                if (!alliance.IsAlive) continue;

                // 找到同盟中的另一个势力
                Force otherForce = null;
                foreach (Force f in alliance.ForceList)
                {
                    if (f != force)
                    {
                        otherForce = f;
                        break;
                    }
                }

                if (otherForce == null) continue;

                // 检查是否需要撕毁条约
                if (ShouldBreakAlliance(force, otherForce, alliance, scenario, PersonalityId))
                {
                    alliancesToBreak.Add(alliance);
                    targetsToBreak.Add(otherForce);
                }
            }

            // 执行撕毁条约
            for (int i = 0; i < alliancesToBreak.Count; i++)
            {
                // 获取同盟关系并撕毁
                Alliance alliance = force.CheckAlliance(targetsToBreak[i]);
                if (alliance != null)
                {
                    alliance.IsAlive = false;
                    // 从双方的同盟列表中移除
                    force.AllianceList.Remove(alliance);
                    targetsToBreak[i].AllianceList.Remove(alliance);
                }
                return true; // 一次只撕毁一个条约
            }

            return false;
        }

        /// <summary>
        /// 判断是否应该撕毁条约
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="otherForce">另一方势力</param>
        /// <param name="alliance">条约</param>
        /// <param name="scenario">场景</param>
        /// <param name="PersonalityId">AI个性</param>
        /// <returns>是否应该撕毁</returns>
        private static bool ShouldBreakAlliance(Force force, Force otherForce, Alliance alliance, Scenario scenario, AIPersonalityType PersonalityId)
        {
            int relation = scenario.GetRelation(force, otherForce);

            // 关系恶化到一定程度
            if (relation < -500)
            {
                // 侵略型AI更倾向于撕毁条约
                int chance = PersonalityId == AIPersonalityType.Aggressive ? 40 : 20;
                if (GameRandom.Chance(chance))
                {
                    return true;
                }
            }

            // 对方与自己的敌人结盟
            foreach (Force enemy in force.NeighborForceList)
            {
                if (force.IsEnemy(enemy) && otherForce.IsAlliance(enemy))
                {
                    // 侵略型和防御型AI更倾向于撕毁条约
                    int chance = (PersonalityId == AIPersonalityType.Aggressive || PersonalityId == AIPersonalityType.Defensive) ? 50 : 30;
                    if (GameRandom.Chance(chance))
                    {
                        return true;
                    }
                }
            }

            // 停战协议下，关系恢复到一定程度
            if (alliance.allianceType == AllianceType.Truce && relation > 500)
            {
                // 侵略型AI更倾向于撕毁停战协议
                int chance = PersonalityId == AIPersonalityType.Aggressive ? 30 : 15;
                if (GameRandom.Chance(chance))
                {
                    return true;
                }
            }

            // 经济型AI在对方经济实力过强时可能撕毁条约
            if (PersonalityId == AIPersonalityType.Economic)
            {
                // 【统一态势】使用统一模型计算敌我实力比(自身不足对方 2/3 时倾向于撕约)
                float powerRatio = BattleSituation.GetPowerRatio(force, otherForce);
                if (powerRatio < 1f / 1.5f)
                {
                    if (GameRandom.Chance(25))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 计算势力实力
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>势力实力值</returns>
        private static int CalculateForcePower(Force force)
        {
            // 【统一态势】改用统一的战场态势模型计算势力实力,避免各层重复实现
            return BattleSituation.EvaluateForceSnapshot(force).fightPower;
        }

        /// <summary>
        /// 计算外交资源价值
        /// </summary>
        /// <param name="force">发送方势力</param>
        /// <param name="targetForce">目标势力</param>
        /// <param name="relation">当前关系值</param>
        /// <param name="actionType">外交行动类型</param>
        /// <param name="PersonalityId">AI个性</param>
        /// <returns>资源价值</returns>
        private static int CalculateDiplomacyResourceValue(Force force, Force targetForce, int relation, DiplomacyActionType actionType, AIPersonalityType PersonalityId)
        {
            City centerCity = force.CapitalCity;
            if (centerCity == null) return 0;

            int baseValue = 0;
            int maxValue = centerCity.gold / 2; // 最多使用一半的资金

            switch (actionType)
            {
                case DiplomacyActionType.SendGift:
                    // 使用配置的送礼金额
                    baseValue = Scenario.Cur.Variables.diplomacySendGiftAmount;
                    break;
                case DiplomacyActionType.Truce:
                    // 停战需要较高投入
                    baseValue = 2000 - relation;
                    baseValue = System.Math.Max(1500, System.Math.Min(baseValue, 5000));
                    break;
                case DiplomacyActionType.Alliance:
                    // 结盟需要高投入
                    baseValue = 3000 + (relation / 5);
                    baseValue = System.Math.Max(2000, System.Math.Min(baseValue, 8000));
                    break;
                // Marriage / Trade / AllianceRequest 三个分支已删除：
                // 原版没有这些命令，对应枚举项已移除。
                default:
                    baseValue = 1000;
                    break;
            }

            // 根据AI个性调整投入
            switch (PersonalityId)
            {
                case AIPersonalityType.Diplomatic:
                    // 外交型AI更愿意投入
                    baseValue = (int)(baseValue * 1.2f);
                    break;
                case AIPersonalityType.Economic:
                    // 经济型AI投入较少
                    baseValue = (int)(baseValue * 0.8f);
                    break;
                case AIPersonalityType.Aggressive:
                    // 侵略型AI在结盟时投入较多
                    if (actionType == DiplomacyActionType.Alliance)
                    {
                        baseValue = (int)(baseValue * 1.1f);
                    }
                    break;
                case AIPersonalityType.Defensive:
                    // 防御型AI在停战时投入较多
                    if (actionType == DiplomacyActionType.Truce)
                    {
                        baseValue = (int)(baseValue * 1.1f);
                    }
                    break;
            }

            // 确保投入不超过最大可用资金
            return System.Math.Min(baseValue, maxValue);
        }

        /// <summary>
        /// AI-俘虏
        /// </summary>
        public static bool AICaptives(Force force, Scenario scenario)
        {
            // 处理本势力的俘虏
            ProcessCaptives(force, scenario);

            // 处理其他势力的俘虏赎回请求
            ProcessRansomRequests(force, scenario);

            return true;
        }

        private static bool ProcessCaptives(Force force, Person captive, Scenario scenario)
        {
            // 检查是否可以招降
            if (CanRecruitCaptive(force, captive, scenario))
            {
                // 尝试招降
                if (TryRecruitCaptive(force, captive, scenario))
                {
                    return true;
                }
            }

            // 检查是否应该释放
            if (ShouldReleaseCaptive(force, captive, scenario))
            {
                ReleaseCaptive(force, captive, scenario);
                return true;
            }

            return false;
        }

        private static void ProcessCaptives(Force force, Scenario scenario)
        {
            // 【注意】循环内统一采用"移除后 i--"的写法：因为删除当前元素后，
            // 后续元素会前移填补该下标，i-- 再配合循环的 i++ 即可回到同一位置继续检查。
            // 该写法成立的前提是**列表确实缩短了** —— 因此凡是要 i-- 的分支，
            // 都必须先把元素从列表中真正移除，否则会陷入死循环。

            force.ForEachCity(city =>
            {
                for (int i = 0; i < city.captiveList.Count; i++)
                {
                    Person captive = city.captiveList[i];

                    // 空槽：SangoObjectList 不支持按下标删除，这里直接跳过
                    //（i++ 会继续前进，不会死循环；列表正常不应含 null）
                    if (captive == null)
                        continue;

                    // 【修复】状态与列表不一致（例如已被别处招降 / 释放但引用残留）：
                    // 主动摘除残留引用，而不是继续对它做"招降 / 释放"处理，
                    // 否则释放分支会对非俘虏调用 Escape，打出"不是囚犯,无法逃跑!"错误。
                    if (!captive.IsPrisoner)
                    {
                        captive.BelongForce?.BeCaptiveList.Remove(captive);
                        city.captiveList.Remove(captive);
                        i--;
                        continue;
                    }

                    // 检查是否可以招降
                    if (ProcessCaptives(force, captive, scenario))
                    {
                        i--;
                        continue;
                    }
                }
            });

            force.ForEachTroop(troop =>
            {
                for (int i = 0; i < troop.captiveList.Count; i++)
                {
                    Person captive = troop.captiveList[i];

                    // 空槽：同上，直接跳过
                    if (captive == null)
                        continue;

                    // 【修复】同上：部队俘虏列表同样需要状态守卫与残留清理。
                    // 这是本次报错的直接触发点 —— 俘虏在城市轮被招降后仍留在
                    // troop.captiveList 中，部队轮再次处理时会对非俘虏调用 Escape。
                    if (!captive.IsPrisoner)
                    {
                        captive.BelongForce?.BeCaptiveList.Remove(captive);
                        if (captive.mBelongTroop == troop)
                            captive.mBelongTroop = null;
                        troop.captiveList.Remove(captive);
                        i--;
                        continue;
                    }

                    // 检查是否可以招降
                    if (ProcessCaptives(force, captive, scenario))
                    {
                        i--;
                        continue;
                    }
                }
            });
        }

        private static bool CanRecruitCaptive(Force force, Person captive, Scenario scenario)
        {
            // 检查忠诚度
            if (captive.loyalty > 70)
                return false;

            // 检查势力关系
            int relation = scenario.GetRelation(force, captive.BelongForce);
            if (relation < -5000)
                return false;

            // 检查是否有足够的资金
            City capital = force.mGovernor?.BelongCity;
            if (capital == null || capital.gold < 2000)
                return false;

            return true;
        }

        private static bool TryRecruitCaptive(Force force, Person captive, Scenario scenario)
        {
            // 【修复】原实现把 force.Id（势力 ID）当作 type 传入，语义完全错乱：
            // type 应为 PersonRecruitType（0 普通登庸 / 1 有势力俘虏 / 2 无势力俘虏），
            // 传入势力 ID 会让招降概率落入错误的公式分支。这里按俘虏原势力是否尚存选取正确类型。
            int recruitType = captive.BelongForce != null
                ? (int)PersonRecruitType.OnCityFall
                : (int)PersonRecruitType.OnForceFall;
            int probability = GameFormula.Instance.RecruitPersonProbability(force.mGovernor, captive, recruitType);

            // 根据势力领袖的性格调整招降概率
            if (force.mGovernor != null && force.mGovernor.mPersonality != null)
            {
                probability += force.mGovernor.mPersonality.recruitCaptiveTendencyAdd * 100;
            }

            if (GameRandom.Chance(probability, 10000))
            {
                // 先记录原势力名:招降后 captive 的归属会被改写,日志会失真
                string lastForceName = captive.BelongForce?.Name;

                // 【修复】俘虏可能挂在"部队"或"城池"的列表上，必须按实际归属清理：
                // 原实现无条件调用 CurrentCity.RemoveCaptive，若俘虏来自部队俘获
                // （挂在 troop.captiveList，CurrentCity 列表里并没有它），
                // 则会出现两个连锁问题：
                //   1) 城市列表移除失败，打印"不能移除不存在的!!!"；
                //   2) troop.captiveList 残留该武将，ProcessCaptives 的部队循环会再次处理它，
                //      此时 state 已复位为一般武将，释放分支调用 Escape 会报"不是囚犯,无法逃跑!"。
                if (captive.mBelongTroop != null)
                    captive.mBelongTroop.RemoveCaptive(captive);
                else
                    captive.CurrentCity?.RemoveCaptive(captive);

                captive.BelongForce?.BeCaptiveList.Remove(captive);

                // 招降成功必须把状态从俘虏复位为一般武将,
                // 否则会出现"已脱离俘虏名单、已加入正常城市,却仍是俘虏状态"的不一致,
                // 进而导致该武将永远无法被派活(IsFree 恒为 false)
                captive.state = (int)PersonStateType.Normal;
                captive.ChangeBelongCity(force.CapitalCity);
                captive.SetMission(MissionType.PersonReturn, force.CapitalCity);
                Sango.Log.Info($"{force.Name}成功招降了{lastForceName}的{captive.Name}！");
                return true;

            }
            return false;
        }

        private static bool ShouldReleaseCaptive(Force force, Person captive, Scenario scenario)
        {
            // 检查忠诚度
            //if (captive.loyalty > 100)
            //    return true;

            // 检查势力关系
            int relation = scenario.GetRelation(force, captive.BelongForce);
            if (relation > 5000)
                return true;

            // 检查是否有足够的粮食
            City capital = captive.CurrentCity;
            if (capital != null && capital.totalGainGold < capital.GoldCost(scenario))
                return true;

            // 根据势力领袖的性格调整释放概率
            int releaseChance = 5;
            if (force.mGovernor != null && force.mGovernor.mPersonality != null)
            {
                releaseChance += force.mGovernor.mPersonality.releaseCaptiveTendencyAdd;
            }

            // 随机释放
            if (GameRandom.Chance(releaseChance))
                return true;

            return false;
        }

        private static void ReleaseCaptive(Force force, Person captive, Scenario scenario)
        {
            // 【修复】仅对真正的俘虏执行释放：上游可能已将其招降或释放（state 复位为一般武将），
            // 此时再调用 Escape 会触发"不是囚犯,无法逃跑!"的错误日志。
            if (captive == null || !captive.IsPrisoner)
                return;

            Sango.Log.Info($"{force.Name}在{captive.CurrentCity?.Name}释放了{captive.BelongForce?.Name}的{captive.Name}！");
            // 直接调用Person.Escape方法释放俘虏
            captive.Escape(EscapeType.Released, force);
        }

        private static void ProcessRansomRequests(Force force, Scenario scenario)
        {
            // 检查是否有其他势力的俘虏在本势力
            if (force.BeCaptiveList == null || force.BeCaptiveList.Count == 0)
                return;
            force.BeCaptiveList.Sort(PersonSortFunction.SortByMilitaryAbility.Sort);

            // 根据势力领袖的性格调整赎回概率
            int ransomChance = 50;
            if (force.mGovernor != null && force.mGovernor.mPersonality != null)
            {
                ransomChance += force.mGovernor.mPersonality.ransomCaptiveTendencyAdd;
            }
            // 原势力有足够的资金，考虑接受赎回
            if (GameRandom.Chance(ransomChance))
            {
                Person captive = force.BeCaptiveList[GameRandom.Range(force.BeCaptiveList.Count)];
                int ransom = CalculateRansom(captive);
                bool hasSend = false;
                force.ForEachCity(city =>
                {
                    if (!hasSend && city.gold >= ransom + 500)
                    {
                        Person[] people = ForceAI.CounsellorRecommendDiplomacy(city.freePersons);
                        if (people != null && people.Length > 0)
                        {
                            Person person = people[0];
                            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                            // 创建外交行为实例并计算成功率
                            // 原版没有独立的"赎回"命令，赎买只是俘虏交换的退化形态
                            // （try_exchange 的 release == nullptr 分支 = 只索要、不带回）。
                            DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.Exchange, force, captive.CurrentCity.BelongForce, person, ransom);
                            int rate = diplomacyManager.CalculateDiplomacySuccessRate(action);
                            if (rate > 50)
                            {
                                city.gold -= ransom;
                                person.SetMission(MissionType.PersonDiplomacy, captive.CurrentCity, (int)DiplomacyActionType.Exchange, ransom, person.Id);
                                city.freePersons.Remove(person);
                                hasSend = true;
                                Sango.Log.Info($"{captive.BelongForce?.Name}派遣{person.Name}前往{captive.CurrentCity.BelongForce.Name}赎回我方俘虏{captive.Name}！");
                                return;
                            }
                        }
                    }
                });
            }
        }

        private static int CalculateRansom(Person captive)
        {
            // 根据俘虏的能力计算赎金
            int baseRansom = 5000;
            int abilitySum = captive.Command + captive.Strength + captive.Intelligence + captive.Politics + captive.Glamour;
            int abilityBonus = abilitySum * 10;
            return baseRansom + abilityBonus;
        }
        /// <summary>
        /// AI-科技研发
        /// </summary>

        public static bool AITechniques(Force force, Scenario scenario)
        {
            // 检查是否有城市可以进行科技研发
            City capital = force.mGovernor?.BelongCity;
            if (capital == null)
                return true;

            // 检查是否有空闲武将进行研发
            if (capital.freePersons.Count == 0)
                return true;

            // 检查是否有正在研发的科技
            if (force.ResearchTechnique > 0)
                return true;

            // 选择要研发的科技
            Technique targetTechnique = SelectTechnique(force, scenario);
            if (targetTechnique == null)
                return true;

            // 选择研发人员
            Person[] researchers = ForceAI.CounsellorRecommendResearch(capital.freePersons, targetTechnique);
            if (researchers == null || researchers.Length == 0)
                return true;

            // 计算研发成本
            int[] cost = targetTechnique.GetCost(researchers, capital);
            if (cost == null)
                return true;

            // 检查是否有足够的资金和技巧点
            if (capital.gold < cost[0] || force.TechniquePoint < cost[1])
                return true;

            // 开始研发
            StartResearch(force, capital, targetTechnique, researchers, scenario);

            return true;
        }

        private static Technique SelectTechnique(Force force, Scenario scenario)
        {
            List<Technique> availableTechniques = new List<Technique>();
            Dictionary<Technique, float> techniqueWeights = new Dictionary<Technique, float>();

            // 收集所有可研发的科技
            foreach (Technique technique in force.InitTechniques)
            {
                if (IsTechniqueAvailable(force, technique, scenario))
                {
                    availableTechniques.Add(technique);
                    float weight = CalculateTechniqueWeight(force, technique, scenario);
                    techniqueWeights[technique] = weight;
                }
            }

            if (availableTechniques.Count == 0)
                return null;

            // 根据权重选择科技
            return WeightedRandom(techniqueWeights);
        }

        private static bool IsTechniqueAvailable(Force force, Technique technique, Scenario scenario)
        {
            // 检查是否已经研发过
            if (force.HasTechnique(technique.Id))
                return false;

            // 检查前置科技
            if (technique.needTech > 0 && !force.HasTechnique(technique.needTech))
                return false;

            return true;
        }

        private static float CalculateTechniqueWeight(Force force, Technique technique, Scenario scenario)
        {
            float weight = 1.0f;

            // 基础权重
            weight += technique.level * 0.1f;

            // 根据当前局势调整权重
            if (IsAtWar(force, scenario))
            {
                // 战争时期优先军事相关科技
                weight *= 1.3f;
            }
            else
            {
                // 和平时期优先经济相关科技
                weight *= 1.2f;
            }

            // 根据城市数量调整权重
            int cityCount = force.CityCount;
            if (cityCount > 5)
            {
                // 城市多优先管理相关科技
                weight *= 1.1f;
            }

            // 根据势力领袖的性格调整科技研发倾向
            if (force.mGovernor != null && force.mGovernor.mPersonality != null)
            {
                weight *= (1.0f + force.mGovernor.mPersonality.technologyTendencyAdd * 0.01f);
            }

            return weight;
        }

        private static bool IsAtWar(Force force, Scenario scenario)
        {
            foreach (Force otherForce in scenario.forceSet)
            {
                if (otherForce != null && otherForce != force)
                {
                    int relation = scenario.GetRelation(force, otherForce);
                    if (relation < -3000)
                        return true;
                }
            }
            return false;
        }

        private static Technique WeightedRandom(Dictionary<Technique, float> weights)
        {
            float totalWeight = 0;
            foreach (float weight in weights.Values)
            {
                totalWeight += weight;
            }

            float random = GameRandom.Range(0, totalWeight);
            float current = 0;

            foreach (KeyValuePair<Technique, float> pair in weights)
            {
                current += pair.Value;
                if (random <= current)
                {
                    return pair.Key;
                }
            }

            return weights.Keys.FirstOrDefault();
        }

        private static void StartResearch(Force force, City city, Technique technique, Person[] researchers, Scenario scenario)
        {
            // 计算研发所需时间和资源
            int[] cost = technique.GetCost(researchers, city);
            if (cost == null)
                return;

            int researchDays = cost[2];
            int researchCost = cost[0];
            int techPointCost = cost[1];

            // 消耗资金和技巧点
            city.gold -= researchCost;
            force.GainTechniquePoint(-techPointCost);

            // 分配研发任务
            foreach (Person researcher in researchers)
            {
                if (researcher != null)
                {
                    city.freePersons.Remove(researcher);
                    researcher.SetMission(MissionType.PersonResearch, city, researchDays, technique.Id);
                }
            }

            // 记录研发中的科技
            force.ResearchTechnique = technique.Id;
            force.ResearchLeftCounter = researchDays;

            Sango.Log.Info($"{force.Name}开始研发科技{technique.Name}，预计需要{researchDays}天！");
        }

        /// <summary>
        /// 3人推荐代理
        /// </summary>
        /// <param name="checkValue"></param>
        /// <param name="check1"></param>
        /// <param name="check2"></param>
        /// <param name="check3"></param>
        /// <returns></returns>
        public delegate bool Recommend3PersonValue(ref int[] checkValue, Person check1, Person check2, Person check3);
        public delegate bool Recommend2PersonValue(ref int[] checkValue, Person check1, Person check2);
        public delegate bool Recommend1PersonValue(ref int[] checkValue, Person check1);

        /// <summary>
        /// 1-3人遍历最优推荐基础函数(效率慢,最优解,尽量不用在人数>20的城市)
        /// </summary>
        /// <param name="personList"></param>
        /// <param name="recommend3PersonValueFunc"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        static Person[] CounsellorRecommend3Person(List<Person> personList, Recommend3PersonValue recommend3PersonValueFunc)
        {
            return CounsellorRecommend3Person(personList, null, null, recommend3PersonValueFunc);
        }

        static Person[] CounsellorRecommend3Person(List<Person> personList, Person p1, Person p2, Recommend3PersonValue recommend3PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (count >= 20)
                return CounsellorFastRecommend3Person(personList, recommend3PersonValueFunc);

            Person[] checkPersons = new Person[3];
            int[] maxValue = new int[] { -99999, 99999 };
            bool hasValue = false;
            for (int i = 0; i < count; i++)
            {
                Person person1 = personList[i];
                if (p1 != null && person1 != p1) continue;
                if (recommend3PersonValueFunc(ref maxValue, person1, null, null))
                {
                    checkPersons[0] = person1;
                    checkPersons[1] = null;
                    checkPersons[2] = null;
                    hasValue = true;
                }
                for (int j = i + 1; j < count; j++)
                {
                    Person person2 = personList[j];
                    if (p2 != null && person2 != p2) continue;
                    if (recommend3PersonValueFunc(ref maxValue, person1, person2, null))
                    {
                        checkPersons[0] = person1;
                        checkPersons[1] = person2;
                        checkPersons[2] = null;
                        hasValue = true;
                    }
                    for (int k = j + 1; k < count; k++)
                    {
                        Person person3 = personList[k];
                        if (recommend3PersonValueFunc(ref maxValue, person1, person2, person3))
                        {
                            checkPersons[0] = person1;
                            checkPersons[1] = person2;
                            checkPersons[2] = person3;
                            hasValue = true;
                        }
                    }
                }
            }

            if (!hasValue)
                return null;

            return checkPersons;
        }

        static Person[] CounsellorRecommend2Person(List<Person> personList, Recommend2PersonValue recommend2PersonValueFunc)
        {
            return CounsellorRecommend2Person(personList, null, recommend2PersonValueFunc);
        }
        static Person[] CounsellorRecommend2Person(List<Person> personList, Person p1, Recommend2PersonValue recommend2PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (count >= 20)
                return CounsellorFastRecommend2Person(personList, recommend2PersonValueFunc);

            Person[] checkPersons = new Person[3];
            int[] maxValue = new int[] { -99999, 99999 };
            bool hasValue = false;
            for (int i = 0; i < count; i++)
            {
                Person person1 = personList[i];
                if (p1 != null && p1 != person1) continue;
                if (recommend2PersonValueFunc(ref maxValue, person1, null))
                {
                    checkPersons[0] = person1;
                    checkPersons[1] = null;
                    hasValue = true;
                }
                for (int j = i + 1; j < count; j++)
                {
                    Person person2 = personList[j];
                    if (recommend2PersonValueFunc(ref maxValue, person1, person2))
                    {
                        checkPersons[0] = person1;
                        checkPersons[1] = person2;
                        hasValue = true;
                    }
                }
            }

            if (!hasValue)
                return null;

            return checkPersons;
        }

        static Person[] CounsellorRecommend1Person(List<Person> personList, Recommend1PersonValue recommend1PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (count >= 20)
                return CounsellorFastRecommend1Person(personList, recommend1PersonValueFunc);

            Person[] checkPersons = new Person[3];
            int[] maxValue = new int[] { -99999, 99999 };
            bool hasValue = false;
            for (int i = 0; i < count; i++)
            {
                Person person1 = personList[i];
                if (recommend1PersonValueFunc(ref maxValue, person1))
                {
                    checkPersons[0] = person1;
                    hasValue = true;
                }
            }

            if (!hasValue)
                return null;

            return checkPersons;
        }

        /// <summary>
        /// 3人快速推荐, 不考虑溢出, 先找到第一个最高武将, 第N个武将第N高或者能改变执行值
        /// </summary>
        /// <param name="personList"></param>
        /// <param name="recommend3PersonValueFunc"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        static Person[] CounsellorFastRecommend3Person(List<Person> personList, Recommend3PersonValue fastRecommend3PersonValueFunc)
        {
            return CounsellorFastRecommend3Person(personList, null, null, fastRecommend3PersonValueFunc);
        }

        static Person[] CounsellorFastRecommend3Person(List<Person> personList, Person p1, Person p2, Recommend3PersonValue fastRecommend3PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (personList.Count <= 3)
                return personList.ToArray();

            Person[] checkPersons = new Person[3];
            int[] maxValues = new int[] { -99999, 99999 };
            Person person1 = null, person2 = null;

            // 第一位找目标属性最大
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (p1 != null && p1 != person) continue;
                if (fastRecommend3PersonValueFunc(ref maxValues, person, null, null))
                {
                    person1 = person;
                    checkPersons[0] = person;
                }
            }


            maxValues[0] = -99999;
            maxValues[1] = 99999;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (p2 != null && p2 != person) continue;
                if (person != person1 && fastRecommend3PersonValueFunc(ref maxValues, person1, person, null))
                {
                    person2 = person;
                    checkPersons[1] = person;
                }
            }

            maxValues[0] = -99999;
            maxValues[1] = 99999;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (person != person1 && person != person2 && fastRecommend3PersonValueFunc(ref maxValues, person1, person2, person))
                {
                    checkPersons[2] = person;
                }
            }

            return checkPersons;
        }

        static Person[] CounsellorFastRecommend2Person(List<Person> personList, Recommend2PersonValue fastRecommend2PersonValueFunc)
        {
            return CounsellorFastRecommend2Person(personList, null, fastRecommend2PersonValueFunc);
        }
        static Person[] CounsellorFastRecommend2Person(List<Person> personList, Person p1, Recommend2PersonValue fastRecommend2PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (personList.Count <= 2)
                return personList.ToArray();

            Person[] checkPersons = new Person[3];
            int[] maxValues = new int[] { -99999, 99999 };
            Person person1 = null;
            // 第一位找目标属性最大
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (p1 != null && person != p1) continue;
                if (fastRecommend2PersonValueFunc(ref maxValues, person, null))
                {
                    person1 = person;
                    checkPersons[0] = person;
                }
            }

            maxValues[0] = -99999;
            maxValues[1] = 99999;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (person != person1 && fastRecommend2PersonValueFunc(ref maxValues, person1, person))
                {
                    checkPersons[1] = person;
                }
            }
            return checkPersons;
        }

        static Person[] CounsellorFastRecommend1Person(List<Person> personList, Recommend1PersonValue fastRecommend1PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (personList.Count <= 1)
                return personList.ToArray();

            Person[] checkPersons = new Person[3];
            int[] maxValues = new int[] { -99999, 99999 };
            // 第一位找目标属性最大
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (fastRecommend1PersonValueFunc(ref maxValues, person))
                {
                    checkPersons[0] = person;
                }
            }
            return checkPersons;
        }


        /// <summary>
        /// 军师建造推荐
        /// </summary>
        public static Person[] CounsellorRecommendBuild(List<Person> personList, BuildingType buildingType)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxBuildTurn, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = GameUtility.Method_PersonBuildAbility(check1, check2, check3);
                int turnCount = buildingType.durabilityLimit % buildAbility == 0 ? 0 : 1;
                int buildCount = System.Math.Min(Scenario.Cur.Variables.BuildMaxTurn, buildingType.durabilityLimit / buildAbility + turnCount);

                if (buildCount < maxBuildTurn[1])
                {
                    maxBuildTurn[1] = buildCount;
                    return true;
                }

                return false;

            });
        }

        /// <summary>
        /// 军师巡查推荐
        /// </summary>
        public static Person[] CounsellorRecommendInspection(List<Person> personList)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseSecurityAbility;
                if (check2 != null) buildAbility += check2.BaseSecurityAbility;
                if (check3 != null) buildAbility += check3.BaseSecurityAbility;

                // 与执行侧同式（原版 `Σ统率/28 + 2`），推荐结果才与实际产出一致
                buildAbility = GameUtility.Method_SecurityAbility(buildAbility);

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }

                return false;
            });
        }

        /// <summary>
        /// 军师商业推荐
        /// </summary>
        public static Person[] CounsellorRecommendDevelop(List<Person> personList)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseCommerceAbility;
                if (check2 != null) buildAbility += check2.BaseCommerceAbility;
                if (check3 != null) buildAbility += check3.BaseCommerceAbility;

                buildAbility = GameUtility.Method_DevelopAbility(buildAbility);

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;

            });
        }


        /// <summary>
        /// 军师农业推荐
        /// </summary>
        public static Person[] CounsellorRecommendFarming(List<Person> personList)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseAgricultureAbility;
                if (check2 != null) buildAbility += check2.BaseAgricultureAbility;
                if (check3 != null) buildAbility += check3.BaseAgricultureAbility;

                buildAbility = GameUtility.Method_FarmingAbility(buildAbility);

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;

            });
        }

        /// <summary>
        /// 军师训练推荐
        /// </summary>
        public static Person[] CounsellorRecommendTrainTroops(List<Person> personList)
        {
            // 训练公式依赖"本城兵力"与"有无训练所"，从候选武将的所在城取
            City city = null;
            if (personList != null)
            {
                for (int i = 0; i < personList.Count; i++)
                {
                    if (personList[i] == null)
                        continue;
                    city = personList[i].BelongCity ?? personList[i].CurrentCity;
                    break;
                }
            }
            int troops = city != null ? city.troops : 0;
            bool hasTrainingGround = city != null
                && CityFacilityRequirement.HasGovFacility(city, InteriorFacilityKind.TrainingSchool);

            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                // 与执行侧同式：Σ武力 + max武力
                int sumAbility = 0;
                int maxAbility = 0;
                if (check1 != null)
                {
                    sumAbility += check1.BaseTrainTroopAbility;
                    if (check1.BaseTrainTroopAbility > maxAbility) maxAbility = check1.BaseTrainTroopAbility;
                }
                if (check2 != null)
                {
                    sumAbility += check2.BaseTrainTroopAbility;
                    if (check2.BaseTrainTroopAbility > maxAbility) maxAbility = check2.BaseTrainTroopAbility;
                }
                if (check3 != null)
                {
                    sumAbility += check3.BaseTrainTroopAbility;
                    if (check3.BaseTrainTroopAbility > maxAbility) maxAbility = check3.BaseTrainTroopAbility;
                }

                int buildAbility = GameUtility.Method_TrainTroops(sumAbility, maxAbility, troops, hasTrainingGround);

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;

            });
        }

        /// <summary>
        /// 军师招募士兵推荐
        /// </summary>
        public static Person[] CounsellorRecommendRecruitTroop(List<Person> personList)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseRecruitmentAbility;
                if (check2 != null) buildAbility += check2.BaseRecruitmentAbility;
                if (check3 != null) buildAbility += check3.BaseRecruitmentAbility;

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;

            });
        }

        /// <summary>
        /// 判断某人是否持有推荐特技列表中的任意一个。
        /// </summary>
        /// <param name="features">推荐特技 Id 列表</param>
        /// <param name="person">候选武将</param>
        /// <returns>持有任意一个返回 true</returns>
        static bool HasAnyRecommendFeature(int[] features, Person person)
        {
            if (person == null || features == null)
                return false;
            for (int i = 0; i < features.Length; i++)
            {
                if (person.HasFeatrue(features[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 军师招募兵装生产推荐。
        ///
        /// 推荐特技来自 <c>JobTypes.json</c> 的 <c>recommandFeatures</c>（生产兵装 → 能吏/发明）。
        /// 带特技的人**优先入队**：用一个远大于能力量级的加成表达"绝对优先"，
        /// 与运输推荐的 `transport_actor_comp` 同一套思路。
        /// </summary>
        /// <summary>
        /// 从候选里挑一名"带推荐特技且能力最高"的武将（没有持有者时返回 null）。
        /// </summary>
        /// <param name="personList">候选武将</param>
        /// <param name="features">推荐特技 Id 列表</param>
        /// <param name="score">能力打分函数</param>
        /// <returns>最优的特技持有者；没有则 null</returns>
        static Person PickRecommendFeaturePerson(List<Person> personList, int[] features, Func<Person, int> score)
        {
            if (personList == null || features == null || features.Length == 0 || score == null)
                return null;

            Person best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < personList.Count; i++)
            {
                Person p = personList[i];
                if (!HasAnyRecommendFeature(features, p))
                    continue;
                int s = score(p);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = p;
                }
            }
            return best;
        }

        public static Person[] CounsellorRecommendCreateItems(List<Person> personList, int[] commandFeatures = null)
        {
            if (personList == null || personList.Count == 0)
                return null;

            // 调用方未显式指定时，从 job 数据取
            if (commandFeatures == null || commandFeatures.Length == 0)
                commandFeatures = JobType.GetJobRecommendFeatures((int)CityJobType.CreateItems);

            // 【快速路径特判】候选 ≥20 人时 `CounsellorRecommend3Person` 会切到
            // `CounsellorFastRecommend3Person` —— 那是**逐名额**挑人（评估时另外两人传 null），
            // "整队只奖励一次特技"的语义在那边表达不出来，会把 3 个名额全塞满特技持有者。
            // 这里改为：**先单独挑一名**特技持有者（能力最高的那个）锁定 1 个名额，
            // 剩余名额从其余人里按纯能力排序挑满 —— 既保证"队里恰有 1 名持技者"，
            // 又不让特技把整队的能力水平拉低。
            if (personList.Count >= 20)
            {
                Person featurePerson = PickRecommendFeaturePerson(
                    personList, commandFeatures, p => p.BaseCreativeAbility);

                if (featurePerson != null)
                {
                    List<Person> rest = new List<Person>(personList.Count);
                    for (int i = 0; i < personList.Count; i++)
                    {
                        if (personList[i] != featurePerson)
                            rest.Add(personList[i]);
                    }

                    // 剩余名额：纯能力排序（特技优先已由 featurePerson 占掉）
                    Person[] others = CounsellorRecommend3Person(rest, null, null,
                        (ref int[] mv, Person c1, Person c2, Person c3) =>
                        {
                            int v = 0;
                            if (c1 != null) v += c1.BaseCreativeAbility;
                            if (c2 != null) v += c2.BaseCreativeAbility;
                            if (c3 != null) v += c3.BaseCreativeAbility;
                            if (v > mv[0])
                            {
                                mv[0] = v;
                                return true;
                            }
                            return false;
                        });

                    Person[] merged = new Person[3];
                    merged[0] = featurePerson;
                    int n = 1;
                    if (others != null)
                    {
                        for (int i = 0; i < others.Length && n < 3; i++)
                        {
                            if (others[i] != null)
                                merged[n++] = others[i];
                        }
                    }
                    return merged;
                }
                // 没有任何特技持有者 → 落到下面的纯能力路径
            }

            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseCreativeAbility;
                if (check2 != null) buildAbility += check2.BaseCreativeAbility;
                if (check3 != null) buildAbility += check3.BaseCreativeAbility;

                // 推荐特技优先 —— **只奖励一次**。
                // 原版 `transport_actor_comp` 是单选场景（挑一个人），不存在"队伍里带特技的人越多越好"的语义；
                // 若按人头逐个加分，"3 个都带特技"的组合会比"1 个带特技 + 2 个高能力"得分高得多，
                // 把两支完全不同的队伍拉成同一档，反而挤掉了更合适的组合。
                // 这里改为：队里**有任一人**满足推荐特技就加一次分，其余名额交回能力排序。
                if (HasAnyRecommendFeature(commandFeatures, check1)
                    || HasAnyRecommendFeature(commandFeatures, check2)
                    || HasAnyRecommendFeature(commandFeatures, check3))
                {
                    buildAbility += 100000;
                }

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;

            });
        }

        /// <summary>
        /// 军师部队推荐指定部队
        /// </summary>
        /// <param name="personList">候选武将（通常是本城空闲武将）</param>
        /// <param name="troopType">目标兵种</param>
        /// <param name="maxPersonLimit">最多几人（与部队的三个槽位对应）</param>
        /// <param name="city">目标城；**传了才会做推荐队伍的"最低兵力"门槛判定**（拿不出兵就跳过该队伍）</param>
        public static Person[] CounsellorRecommendMakeTroop(List<Person> personList, TroopType troopType,
            int maxPersonLimit = 3, City city = null)
        {
            if (personList.Count <= 0)
                return null;

            if (personList.Count <= 1)
                return personList.ToArray();

            Person[] checkPersons = new Person[3];
            Person person1 = null;

            // 【推荐队伍优先】本城若有"身份成员凑得齐"的推荐队伍 / 我的队伍（且队伍兵种与本处目标不冲突），
            // 就直接采用它的成员；没有则回落到下面的常规挑选。
            // 判定细节见 TroopTeamService.TryPickMembersForTroop（身份成员必须齐全，补位可选）。
            {
                List<Person> teamPick = TroopTeamService.TryPickMembersForTroop(personList, troopType, maxPersonLimit, city);
                if (teamPick != null && teamPick.Count > 0)
                    return teamPick.ToArray();
            }
            ///
            int checkValue = 0;
            int v_int = 0;
            int v_stength = 0;
            int v_command = 0;
            int level = 0;

            List<Person> list = new List<Person>(personList);
            list.Sort((a, b) =>
            {
                return b.MilitaryAbility.CompareTo(a.MilitaryAbility);
            });

            // 确认主将
            person1 = list[0];
            checkPersons[0] = person1;
            list.RemoveAt(0);
            v_int = person1.Intelligence;
            v_stength = person1.Strength;
            v_command = person1.Command;
            level = Troop.CheckTroopTypeLevel(troopType, person1);

            int destSlot = 1;
            if (destSlot >= maxPersonLimit)
                return checkPersons;

            // 必须要兵符携带者为主将
            if (troopType.validItemId > 0)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Person person = list[i];
                    if (person.HasItem(troopType.validItemId))
                    {
                        checkPersons[destSlot] = person;
                        list.RemoveAt(i);
                        person1 = person;
                        v_int = System.Math.Max(v_int, person1.Intelligence);
                        v_stength = System.Math.Max(v_stength, person1.Strength);
                        v_command = System.Math.Max(v_command, person1.Command);
                        level = System.Math.Max(level, Troop.CheckTroopTypeLevel(troopType, person1));
                        destSlot++;
                        break;
                    }
                }
            }

            // 补充适配的特性的武将, 一般适配特性的武将都带有高适应力
            if (troopType.matchFeatures != null)
            {
                bool alreadtHasFeature = false;
                for (int i = 0; i < destSlot; i++)
                {
                    Person exsistP = checkPersons[i];
                    if (exsistP.FeatureList != null)
                    {
                        for (int j = 0; j < troopType.matchFeatures.Length; j++)
                        {
                            if (exsistP.FeatureList.Contains(troopType.matchFeatures[j]))
                            {
                                alreadtHasFeature = true;
                                break;
                            }
                        }
                    }
                    if (alreadtHasFeature)
                        break;
                }

                if (!alreadtHasFeature)
                {
                    person1 = null;
                    // 优先战斗力适配的特性主将
                    for (int i = 0; i < list.Count; i++)
                    {
                        Person person = list[i];
                        if (person.FeatureList != null)
                        {
                            for (int j = 0; j < troopType.matchFeatures.Length; j++)
                            {
                                if (person.FeatureList.Contains(troopType.matchFeatures[j]))
                                {
                                    checkPersons[destSlot] = person;
                                    person1 = person;
                                    v_int = System.Math.Max(v_int, person1.Intelligence);
                                    v_stength = System.Math.Max(v_stength, person1.Strength);
                                    v_command = System.Math.Max(v_command, person1.Command);
                                    level = System.Math.Max(level, Troop.CheckTroopTypeLevel(troopType, person1));
                                    break;
                                }
                            }
                        }

                        if (person1 != null)
                            break;
                    }

                    if (person1 != null)
                    {
                        list.Remove(person1);
                        destSlot++;
                    }
                }
            }

            if (destSlot >= maxPersonLimit)
                return checkPersons;

            // 优先补充适应
            list.Sort((a, b) =>
            {
                int lvl_a = Troop.CheckTroopTypeLevel(troopType, a);
                int lvl_b = Troop.CheckTroopTypeLevel(troopType, b);
                if (lvl_a == lvl_b)
                {
                    return a.MilitaryAbility.CompareTo(b.MilitaryAbility);
                }
                else
                    return lvl_b.CompareTo(lvl_a);
            });

            if (list.Count == 0)
                return checkPersons;

            person1 = list[0];
            int templevel = Troop.CheckTroopTypeLevel(troopType, person1);
            if (level < templevel)
            {
                list.RemoveAt(0);
                v_int = System.Math.Max(v_int, person1.Intelligence);
                v_stength = System.Math.Max(v_stength, person1.Strength);
                v_command = System.Math.Max(v_command, person1.Command);
                checkPersons[destSlot] = person1;
                destSlot++;
            }

            if (destSlot >= maxPersonLimit)
                return checkPersons;

            for (int k = 0; k < 2; k++)
            {
                // 不需要补充适应,尝试补充战斗力
                if (v_stength < 60)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Person person = list[i];
                        int checkLvl = Troop.CheckTroopTypeLevel(troopType, person);
                        if (person.Strength >= 70 && (checkLvl < level && level >= 3 || checkLvl <= level && level < 3))
                        {
                            checkPersons[destSlot] = person;
                            destSlot++;
                            v_int = System.Math.Max(v_int, person.Intelligence);
                            v_stength = System.Math.Max(v_stength, person.Strength);
                            v_command = System.Math.Max(v_command, person.Command);
                            list.RemoveAt(i);
                            break;
                        }
                    }
                }
                else if (v_command < 60)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Person person = list[i];
                        int checkLvl = Troop.CheckTroopTypeLevel(troopType, person);
                        if (person.Command >= 70 && (checkLvl < level && level >= 3 || checkLvl <= level && level < 3))
                        {
                            checkPersons[destSlot] = person;
                            destSlot++;
                            v_int = System.Math.Max(v_int, person.Intelligence);
                            v_stength = System.Math.Max(v_stength, person.Strength);
                            v_command = System.Math.Max(v_command, person.Command);
                            list.RemoveAt(i);
                            break;
                        }
                    }
                }

                if (destSlot >= maxPersonLimit)
                    return checkPersons;

                if (person1.Intelligence < 70)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Person person = list[i];
                        int checkLvl = Troop.CheckTroopTypeLevel(troopType, person);
                        if (person.Intelligence >= 80 && (checkLvl < level && level >= 3 || checkLvl <= level && level < 3))
                        {
                            checkPersons[destSlot] = person;
                            v_int = System.Math.Max(v_int, person.Intelligence);
                            v_stength = System.Math.Max(v_stength, person.Strength);
                            v_command = System.Math.Max(v_command, person.Command);
                            destSlot++;
                            list.RemoveAt(i);
                            break;
                        }
                    }
                }

                if (destSlot >= maxPersonLimit)
                    return checkPersons;
            }

            return checkPersons;
        }

        /// <summary>
        /// 军师运输队伍推荐
        /// </summary>
        /// <summary>
        /// 运输队主将推荐。
        ///
        /// 对齐出处：Open311 `transport_actor_comp`（s11_game_ai_kyoten_cmd_mil_transport.cpp:433-451）：
        /// <list type="number">
        /// <item>优先带「运搬」特技（<c>SkillId_Unpan</c>）的人；</item>
        /// <item>其次按**五维之和**升序 —— 越弱的人越适合去运粮，把强将留在前线。</item>
        /// </list>
        /// <para>
        /// 【两点差异】
        /// · 本项目 `Features.json` 里没有「运搬」这条特技，第一优先级无法实现，
        ///   因此只做第二优先级（能力最弱）。
        /// · 原实现用 <c>MilitaryAbility</c>（统×2 + max(武,智)×3）作为打分，
        ///   与原版的五维之和不是同一个量；现改为五维之和，与 `ai_table_get_stat_sum` 对齐。
        /// </para>
        /// </summary>
        /// <param name="personList">候选武将</param>
        /// <returns>推荐的主将（长度 1）</returns>
        public static Person[] CounsellorRecommendTransportTroop(List<Person> personList)
        {
            // 推荐特技来自 JobTypes.json 的 `recommandFeatures`（运输 → 搬运 = Feature 8）。
            // 对应原版运输命令传入的 `SkillId_Unpan`（本项目译作「搬运」，不是「运搬」）。
            int[] recommendFeatures = JobType.GetJobRecommendFeatures((int)CityJobType.MakeTansport);

            return CounsellorRecommend1Person(personList, (ref int[] maxValue, Person check1) =>
            {
                // 五维之和 —— 对齐 ai_table_get_stat_sum
                int buildAbility = check1.Command + check1.Strength + check1.Intelligence
                                 + check1.Politics + check1.Glamour;

                // 第一优先级：带推荐特技的人 —— 对齐 `transport_actor_comp` 的
                // `if (a_has_skill != b_has_skill) return a_has_skill;`（mil_transport.cpp:433-441）。
                // 用一个远大于五维和（最大 500）的偏移量表达"绝对优先"，
                // 这样仍然沿用单值比较器，不必为两段优先级另写挑选逻辑。
                if (recommendFeatures != null)
                {
                    for (int i = 0; i < recommendFeatures.Length; i++)
                    {
                        if (check1.HasFeatrue(recommendFeatures[i]))
                        {
                            buildAbility -= 100000;
                            break;
                        }
                    }
                }

                if (buildAbility < maxValue[1])
                {
                    maxValue[1] = buildAbility;
                    return true;
                }
                return false;
            });
        }

        /// <summary>
        /// 军师交易推荐。
        ///
        /// 推荐特技来自 <c>JobTypes.json</c> 的 <c>recommandFeatures</c>（交易粮食 → 未配置）。
        /// 未配置时不影响排序，仅按政治力挑人。
        /// </summary>
        public static Person[] CounsellorRecommendTrade(List<Person> personList, int[] commandFeatures = null)
        {
            if (commandFeatures == null || commandFeatures.Length == 0)
                commandFeatures = JobType.GetJobRecommendFeatures((int)CityJobType.TradeFood);

            return CounsellorRecommend1Person(personList, (ref int[] maxValue, Person check1) =>
            {
                int buildAbility = check1.Politics;
                // 推荐特技优先
                if (HasAnyRecommendFeature(commandFeatures, check1))
                    buildAbility += 100000;

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;
            });
        }

        /// <summary>
        /// 军师外交推荐
        /// </summary>
        public static Person[] CounsellorRecommendDiplomacy(List<Person> personList)
        {
            return CounsellorRecommend1Person(personList, (ref int[] maxValue, Person check1) =>
            {
                // 计算外交能力：政治 + 魅力/2
                int diplomacyAbility = check1.Politics + check1.Glamour / 2;
                if (diplomacyAbility > maxValue[0])
                {
                    maxValue[0] = diplomacyAbility;
                    return true;
                }
                return false;
            });
        }

        /// <summary>
        /// 研究队伍人数上限（与 JobType #19 研究的人数限制、技巧研究界面上的三个武将槽位一致）
        /// </summary>
        const int ResearchPersonLimit = 3;

        /// <summary>
        /// 研究推荐特技兜底：特技 #85 指导，研究技巧时费用减半（与 CityAI 里 86 眼力的写法保持同一约定）。
        ///
        /// 为什么需要兜底：<see cref="Technique.recommandFeatures"/>（以及 JobType 上的同名字段）
        /// 在数据侧（Technique_技术表 / JobTypes.json）从未导出过，取到的永远是 null，
        /// 于是"优先把影响研究的特技持有者选进研究队伍"这条链路整体失效：
        /// 默认推荐的人选永远不含指导持有者，玩家按推荐直接开始研究，费用自然不减半，
        /// 表现就是"指导特技不生效"。这里按原版行为兜底：城里有指导持有者就优先入队。
        /// </summary>
        static readonly int[] defaultResearchRecommandFeatures = new int[] { 85 };

        /// <summary>
        /// 军师研究推荐
        ///
        /// 关键规则：推荐特技（指导）的持有者必须进队伍。
        /// 因为研究费用/耗旬的加成动作是由"参与者身上的特技"装配的
        /// （Technique.GetCost → GameUtility.InitJobFeature → CityImproveResearchCost），
        /// 持有者不参与研究，该特技就等同于没有实现。
        /// 其余名额按研究所需属性从高到低补足：研究耗旬取三人属性之和，属性越高耗旬越短。
        /// </summary>
        /// <param name="personList">候选武将（一般为目标城市的未行动武将）</param>
        /// <param name="technique">目标技巧</param>
        public static Person[] CounsellorRecommendResearch(List<Person> personList, Technique technique)
        {
            if (personList == null || personList.Count == 0 || technique == null)
                return null;

            int[] recommandFeatures = technique.recommandFeatures;
            if (recommandFeatures == null || recommandFeatures.Length == 0)
                recommandFeatures = defaultResearchRecommandFeatures;

            List<Person> featurePersonList = personList.FindAll(x => x.FeatureList != null && x.FeatureList.Contains(recommandFeatures));
            // 持有者足够填满队伍时直接成队（与原逻辑一致）
            if (featurePersonList.Count >= ResearchPersonLimit)
                return featurePersonList.GetRange(0, ResearchPersonLimit).ToArray();

            Person[] recommendList = RecommendResearchByAttribute(personList, technique);
            if (featurePersonList.Count == 0)
                return recommendList;

            // 把持有者补进推荐队伍：
            // 候选人数 >= 20 时 RecommendResearchByAttribute 走的是"快速推荐"分支，
            // 以及原三人约束分支只保证结果"含持有者"、受候选顺序限制可能凑不满三人，
            // 所以这里统一兜底，保证持有者一定在队伍里。
            List<Person> result = new List<Person>(ResearchPersonLimit);
            if (recommendList != null)
            {
                for (int i = 0; i < recommendList.Length && result.Count < ResearchPersonLimit; i++)
                {
                    if (recommendList[i] != null)
                        result.Add(recommendList[i]);
                }
            }

            for (int i = 0; i < featurePersonList.Count && i < ResearchPersonLimit; i++)
            {
                Person holder = featurePersonList[i];
                if (holder == null || result.Contains(holder))
                    continue;

                if (result.Count < ResearchPersonLimit)
                {
                    result.Add(holder);
                }
                else
                {
                    // 满员时换掉研究属性最低的一位，尽量少牺牲研究速度
                    int minIndex = 0;
                    for (int j = 1; j < result.Count; j++)
                    {
                        if (GetResearchAttribute(result[j], technique) < GetResearchAttribute(result[minIndex], technique))
                            minIndex = j;
                    }
                    result[minIndex] = holder;
                }
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        /// <summary>
        /// 取武将用于研究的能力值（与研究耗旬使用同一个取属性入口 Person.GetAttribute）
        /// </summary>
        /// <param name="person">待取值的武将</param>
        /// <param name="technique">目标技巧（其 needAttr 指明研究所需属性）</param>
        static int GetResearchAttribute(Person person, Technique technique)
        {
            int needAttr = technique.needAttr;
            // 越界值沿用原实现 default 分支的约定：按智力计算
            if (needAttr < (int)AttributeType.Command || needAttr > (int)AttributeType.Glamour)
                needAttr = (int)AttributeType.Intelligence;
            return person.GetAttribute(needAttr);
        }

        /// <summary>
        /// 按研究所需属性挑选最优三人（不含"推荐特技持有者必须进队"的约束）
        /// </summary>
        /// <param name="personList">候选武将</param>
        /// <param name="technique">目标技巧</param>
        static Person[] RecommendResearchByAttribute(List<Person> personList, Technique technique)
        {
            switch (technique.needAttr)
            {
                case (int)AttributeType.Command:
                    return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Command;
                        if (check2 != null) buildAbility += check2.Command;
                        if (check3 != null) buildAbility += check3.Command;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
                case (int)AttributeType.Strength:
                    return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Strength;
                        if (check2 != null) buildAbility += check2.Strength;
                        if (check3 != null) buildAbility += check3.Strength;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
                case (int)AttributeType.Intelligence:
                    return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Intelligence;
                        if (check2 != null) buildAbility += check2.Intelligence;
                        if (check3 != null) buildAbility += check3.Intelligence;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
                case (int)AttributeType.Politics:
                    return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Politics;
                        if (check2 != null) buildAbility += check2.Politics;
                        if (check3 != null) buildAbility += check3.Politics;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
                case (int)AttributeType.Glamour:
                    return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Glamour;
                        if (check2 != null) buildAbility += check2.Glamour;
                        if (check3 != null) buildAbility += check3.Glamour;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
                default:
                    // 默认情况：使用智力作为研发能力
                    return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Intelligence;
                        if (check2 != null) buildAbility += check2.Intelligence;
                        if (check3 != null) buildAbility += check3.Intelligence;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
            }
        }

        /// <summary>
        /// 军师推荐搜索的人选
        /// </summary>
        /// <param name="personList"></param>
        /// <param name="target"></param>
        /// <param name="commandFeatures"></param>
        /// <returns></returns>
        public static Person[] CounsellorRecommendSearching(List<Person> personList, City target, int[] commandFeatures = null)
        {
            if (target.invisiblePersons.Count == 0)
                return null;

            // 调用方未显式指定时，从 job 数据取（搜索 → 眼力）
            if (commandFeatures == null || commandFeatures.Length == 0)
                commandFeatures = JobType.GetJobRecommendFeatures((int)CityJobType.Searching);

            List<Person> result = new List<Person>();
            for (int i = 0; i < personList.Count; i++)
            {
                Person person = personList[i];
                if (commandFeatures != null && person.HasFeatrue(commandFeatures))
                    result.Add(person);
                else if (person.Politics >= 90)
                    result.Add(person);
            }

            if (result.Count == 0)
                return null;

            return result.ToArray();
        }

        /// <summary>
        /// 军师推荐招募人才的人选
        /// </summary>
        /// <param name="personList"></param>
        /// <param name="target"></param>
        /// <param name="commandFeatures"></param>
        /// <returns></returns>
        public static Person CounsellorRecommendRecruitPerson(List<Person> personList, Person target, int[] commandFeatures = null)
        {
            // 调用方未显式指定时，从 job 数据取（登用 → 眼力）。
            // 原实现虽然声明了这个参数却从未使用，等于"推荐特技"在这条链路上完全没生效。
            if (commandFeatures == null || commandFeatures.Length == 0)
                commandFeatures = JobType.GetJobRecommendFeatures((int)CityJobType.RecruitPerson);

            // 推荐特技持有者里取成功率最高的**一个**（不是"遇到就返回第一个"，
            // 也不是"多人都算"）。原版 `transport_actor_comp` 同样是"从候选里挑一个人"。
            Person bestFeaturePerson = null;
            int bestFeatureProbability = -1;

            Person maxP = null;
            int max = 0;
            for (int i = 0; i < personList.Count; i++)
            {
                Person person = personList[i];
                int probability = GameFormula.Instance.RecruitPersonProbability(person, target, 0);

                if (HasAnyRecommendFeature(commandFeatures, person))
                {
                    // 只记录、不立刻返回，保证拿到的是"持有者中最好的那个"
                    if (probability > bestFeatureProbability)
                    {
                        bestFeatureProbability = probability;
                        bestFeaturePerson = person;
                    }
                    continue;
                }

                if (probability >= 100)
                    return person;
                else if (probability >= 30)
                {
                    if (probability > max)
                    {
                        max = probability;
                        maxP = person;
                    }
                }
            }

            // 有特技持有者就优先用他 —— 一次只取一个，但**成功率不能太差**。
            // 加 `>= 30` 这道门槛，与下面"非特技者也要 ≥30 才纳入"的口径对齐：
            // 否则一个 20% 的眼力持有者会挤掉 95% 的高成功率高官，推荐反而更差。
            if (bestFeaturePerson != null && bestFeatureProbability >= 30)
                return bestFeaturePerson;

            return maxP != null ? maxP : bestFeaturePerson;
        }

        public static bool AISetOfficial(Force force, Scenario scenario)
        {
            List<Official> match_officials = new List<Official>();
            force.ForEachPerson(person =>
            {
                if (person.CanUpgradeOfficial)
                {
                    Official[] officials = person.Official.NextOfficials;
                    match_officials.Clear();
                    foreach (Official official in officials)
                    {
                        if (official.CheckPerson(person))
                        {
                            match_officials.Add(official);
                        }
                    }

                    // 【优化】选择可授予中等级最高的官职,避免随机任命浪费高能力武将
                    if (match_officials.Count > 0)
                    {
                        Official dst = match_officials[0];
                        for (int i = 1; i < match_officials.Count; i++)
                        {
                            if (match_officials[i].level > dst.level)
                                dst = match_officials[i];
                        }
                        person.UpgradeOfficial(dst);
                    }
                }
            });

            return true;
        }

    }

}
