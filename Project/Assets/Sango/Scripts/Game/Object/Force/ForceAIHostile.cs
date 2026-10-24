/*
 * 文件名：ForceAIHostile.cs
 * 描述：势力级 AI —— 敌对 / 友好势力表（ForceAI 的 partial 分支）。
 *
 * 对齐出处：Open311（D:\open311\s11）
 *   · 势力级更新总入口 —— s11_game_ai_force.cpp:412-430  update_force_data
 *   · 敌对势力表       —— s11_game_ai_force.cpp:432-462  set_hostile_force
 *   · 友好势力表       —— s11_game_ai_force.cpp:464-522  set_friendly_force
 *   · 是否需要敌对     —— s11_game_ai_force.cpp:524-590  is_need_hostile
 *   · 是否需要友好     —— s11_game_ai_force.cpp:592-612  is_need_friendly
 *   · 目标城市优先度   —— s11_game_ai_force.cpp:614-698  get_city_priority
 *   · 敌人排序         —— s11_sys_scen_ai.cpp:1599-1627 enemy_priority_comp
 *   · 相性距离         —— s11_sys_scen_ai.cpp:851-859  ai_table_get_aishou_distance
 *   · 邻接敌方势力表   —— s11_sys_scen_ai.cpp:1356-1360 ai_table_get_neighbor_enemy_force_list
 *
 * 语义边界：
 *   这两张表**只影响 AI 的决策优先级**（先打谁、跟谁缓和），
 *   不参与 Force.IsEnemy 的合营/敌对判定 —— 原版同样如此。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public partial class ForceAI
    {
        /// <summary>
        /// 势力级外交态势更新：先定"打谁"，再定"跟谁好"。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:412-430 update_force_data 的前两步
        /// （第三步 set_guard 在 ForceAIOutpost.AIPrepareForceData 里调用）。
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        public static void UpdateHostileAndFriendly(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return;
            if (force.hostileForceIds == null)
                force.hostileForceIds = new HashSet<int>();
            if (force.friendlyForceIds == null)
                force.friendlyForceIds = new HashSet<int>();

            // 每回合重算，避免上一回合的选择残留 —— 对齐原版的
            // ai_table_reset_hostile_force / ai_table_reset_friendly_force（:150-151）
            force.hostileForceIds.Clear();
            force.friendlyForceIds.Clear();

            SetHostileForce(force, scenario);
            SetFriendlyForce(force, scenario);
        }

        /// <summary>
        /// 选定本回合要重点对付的敌人。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:432-462 set_hostile_force。
        /// 规则：
        ///   ① 候选 = 与我方领土接壤的敌方势力
        ///   ② 名额 = min(候选数, 1 + 城数/3)
        ///   ③ 按"玩家优先 → 更弱优先 → 交情更差优先 → 州郡争端优先 → Id"排序
        ///   ④ 至少锁一个（一个都没通过门槛时，无条件锁定排序第一的）
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        public static void SetHostileForce(Force force, Scenario scenario)
        {
            List<Force> forceList = GetNeighborEnemyForceList(force, scenario);
            if (forceList.Count == 0)
                return;

            int powerRank = GetPowerRank(force, scenario);
            // 比较器返回"是否应排在前"，转成 Sort 需要的 -1/1
            forceList.Sort((a, b) =>
            {
                if (EnemyPriorityCompare(a, b, force, powerRank))
                    return -1;
                if (EnemyPriorityCompare(b, a, force, powerRank))
                    return 1;
                return 0;
            });

            int maxEnemyCount = System.Math.Min(forceList.Count, 1 + force.CityCount / 3);
            bool set = false;

            for (int i = 0; i < forceList.Count; i++)
            {
                Force dest = forceList[i];
                if (dest == null || !dest.IsAlive)
                    continue;
                if (!IsNeedHostile(force, dest, scenario))
                    continue;

                force.hostileForceIds.Add(dest.Id);
                set = true;
                maxEnemyCount--;
                if (maxEnemyCount == 0)
                    break;
            }

            // 一个都没选上 → 无条件锁定排序第一的 —— :455-461
            if (!set && forceList.Count > 0)
            {
                Force first = forceList[0];
                if (first != null && first.IsAlive)
                    force.hostileForceIds.Add(first.Id);
            }
        }

        /// <summary>
        /// 选定本回合要拉拢的友好势力。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:464-522 set_friendly_force。
        /// 两轮：
        ///   ① 已是同盟、且关系还过得去的势力 —— 直接标为友好
        ///   ② 敌人的敌人 —— 也标为友好，形成合围
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        public static void SetFriendlyForce(Force force, Scenario scenario)
        {
            List<Force> neighborEnemy = GetNeighborEnemyForceList(force, scenario);

            // 距上次进攻已过 18 回合、且身边没有敌人 → 不需要"友好"这个概念 —— :467-474
            if (GetTurnsSinceLastAttack(force, scenario) >= 18 && neighborEnemy.Count == 0)
                return;

            // 前后方都没有接敌的据点 → 不打仗，也就无所谓友好 —— :475-476
            int frontPlusBattle = OutpostRuntime.stateCount[(int)OutpostState.Front]
                + OutpostRuntime.stateCount[(int)OutpostState.Battle];
            if (frontPlusBattle == 0)
                return;

            // 第一轮：同盟关系 —— :477-492
            for (int i = 0; i < scenario.forceSet.Count; i++)
            {
                Force dest = scenario.forceSet[i];
                if (dest == null || !dest.IsAlive)
                    continue;
                if (dest == force)
                    continue;
                if (!force.IsAlliance(dest))
                    continue;
                if (force.hostileForceIds.Contains(dest.Id))
                    continue;
                if (force.friendlyForceIds.Contains(dest.Id))
                    continue;
                if (!IsNeedFriendly(force, dest, scenario))
                    continue;
                force.friendlyForceIds.Add(dest.Id);
            }

            // 第二轮：敌人的敌人 —— :494-520
            for (int i = 0; i < scenario.forceSet.Count; i++)
            {
                Force dest = scenario.forceSet[i];
                if (dest == null || !dest.IsAlive)
                    continue;
                if (dest == force)
                    continue;
                if (!force.hostileForceIds.Contains(dest.Id))
                    continue;

                List<Force> destEnemyList = GetNeighborEnemyForceList(dest, scenario);
                for (int j = 0; j < destEnemyList.Count; j++)
                {
                    Force destEnemy = destEnemyList[j];
                    if (destEnemy == null || !destEnemy.IsAlive)
                        continue;
                    if (destEnemy == force)
                        continue;
                    if (force.hostileForceIds.Contains(destEnemy.Id))
                        continue;
                    if (force.friendlyForceIds.Contains(destEnemy.Id))
                        continue;
                    if (!IsNeedFriendly(force, destEnemy, scenario))
                        continue;

                    // 目标是玩家时，只有"玩家对当前敌人也没好感"才拉拢 —— :515-516
                    if (destEnemy.IsPlayer && destEnemy.GetLike(dest) < 60)
                        force.friendlyForceIds.Add(destEnemy.Id);
                    // 否则要求"对方也把当前敌人当敌人" —— :517-518
                    else if (destEnemy.hostileForceIds != null && destEnemy.hostileForceIds.Contains(dest.Id))
                        force.friendlyForceIds.Add(destEnemy.Id);
                }
            }
        }

        /// <summary>
        /// 判断某势力是否值得列为主要敌人。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:524-590 is_need_hostile。
        /// 玩家与 AI 走两套判据：玩家看难度阈值 + 交情；AI 看交情 + 是否觊觎对方城池。
        /// </summary>
        /// <param name="src">我方势力</param>
        /// <param name="dest">候选敌方势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否应列为主要敌人</returns>
        public static bool IsNeedHostile(Force src, Force dest, Scenario scenario)
        {
            if (src == null || dest == null)
                return false;

            int like = src.GetLike(dest);
            Person srcGovernor = src.mGovernor;

            if (dest.IsPlayer)
            {
                // 难度基线：简单 50 / 普通 60 / 困难 80 —— :530-544
                int difficulty = scenario != null ? scenario.Variables.difficulty : 1;
                int n;
                switch (difficulty)
                {
                    case 0: n = 50; break;
                    case 2: n = 80; break;
                    default: n = 60; break;
                }
                // 君主之间互相欣赏 → 门槛降到 40 —— :545-546
                if (srcGovernor != null && dest.mGovernor != null
                    && IsLikePerson(srcGovernor, dest.mGovernor))
                    n = 40;

                // 玩家接壤排名加成 —— :547
                //   if (like < n + context_->force[src_id].player_neighbored_rank / 10) return true;
                // rank 越高（越贴着玩家、国力越强）→ 门槛越高 → 越容易把玩家列为敌人。
                int playerRank = ForceAI.GetPlayerNeighboredRank(src, scenario);
                if (like < n + playerRank / 10)
                    return true;
                return HasPriorityTargetCity(src, dest, scenario);
            }

            // AI 对 AI：同盟 40 / 非同盟 60 —— :577-581
            int threshold = src.IsAlliance(dest) ? 40 : 60;
            if (srcGovernor != null && dest.mGovernor != null)
            {
                if (IsLikePerson(srcGovernor, dest.mGovernor))
                    like -= 5;
                if (IsHatePerson(srcGovernor, dest.mGovernor))
                    like += 5;
            }
            if (like < threshold)
                return true;
            return HasPriorityTargetCity(src, dest, scenario);
        }

        /// <summary>
        /// 判断某势力是否值得拉拢为友好。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:592-612 is_need_friendly。
        /// </summary>
        /// <param name="src">我方势力</param>
        /// <param name="dest">候选友好势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否应标为友好</returns>
        public static bool IsNeedFriendly(Force src, Force dest, Scenario scenario)
        {
            if (src == null || dest == null || src == dest)
                return false;

            int difficulty = scenario != null ? scenario.Variables.difficulty : 1;
            int aishou = GetAishouDistance(src, dest);

            if (difficulty != 0)
            {
                // 普通以上：非同盟且相性太远 → 不拉拢 —— :598-602
                if (!src.IsAlliance(dest) && aishou >= 4)
                    return false;
            }
            if (difficulty == 1)
            {
                // 普通难度：目标是玩家且相性太远 → 不拉拢 —— :603-607
                if (dest.IsPlayer && aishou >= 4)
                    return false;
            }

            return src.GetLike(dest) >= 40;
        }

        /// <summary>
        /// 敌人的排序规则。
        ///
        /// 对齐出处：s11_sys_scen_ai.cpp:1599-1627 enemy_priority_comp。
        /// 优先级：玩家 → 比自己弱 → 交情更差 → 州郡争端 → Id 小。
        /// </summary>
        /// <param name="a">候选 A</param>
        /// <param name="b">候选 B</param>
        /// <param name="self">当前势力</param>
        /// <param name="selfPowerRank">当前势力的国力排名</param>
        /// <returns>A 是否应排在 B 前面</returns>
        public static bool EnemyPriorityCompare(Force a, Force b, Force self, int selfPowerRank)
        {
            if (a == null || b == null)
                return a != null;

            // 玩家优先 —— :1601-1605
            if (a.IsPlayer != b.IsPlayer)
                return a.IsPlayer;

            // 相对更弱的优先 —— :1606-1613
            Scenario scenario = Scenario.Cur;
            bool aWeak = GetPowerRank(a, scenario) < selfPowerRank;
            bool bWeak = GetPowerRank(b, scenario) < selfPowerRank;
            if (aWeak != bWeak)
                return aWeak;

            // 交情更差的优先 —— :1614-1619
            int aLike = self.GetLike(a);
            int bLike = self.GetLike(b);
            if (aLike != bLike)
                return aLike < bLike;

            // 州郡争端优先（本项目无州郡归属模型，跳过）—— :1620-1625
            return a.Id < b.Id;
        }

        /// <summary>
        /// 与我方领土接壤的敌方势力列表。
        ///
        /// 对齐出处：s11_sys_scen_ai.cpp:1356-1360 ai_table_get_neighbor_enemy_force_list。
        /// 原版先取"邻接敌方据点"再折算成势力；本项目直接遍历我方据点的邻接据点。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>邻接敌方势力列表</returns>
        public static List<Force> GetNeighborEnemyForceList(Force force, Scenario scenario)
        {
            List<Force> result = new List<Force>();
            if (force == null || scenario == null)
                return result;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force)
                    continue;
                if (city.NeighborList == null)
                    continue;

                for (int n = 0; n < city.NeighborList.Count; n++)
                {
                    City nb = city.NeighborList[n];
                    if (nb == null || !nb.IsAlive || nb.BelongForce == null)
                        continue;
                    Force nbForce = nb.BelongForce;
                    if (nbForce == force || !nbForce.IsAlive)
                        continue;
                    // 只算"敌对"，同盟不算 —— 原版同样是 is_enemy 过滤
                    if (!force.IsEnemy(nbForce))
                        continue;
                    if (!result.Contains(nbForce))
                        result.Add(nbForce);
                }
            }
            return result;
        }

        /// <summary>
        /// 两势力之间的相性距离（0~5，越大越不合）。
        ///
        /// 对齐出处：s11_sys_scen_ai.cpp:851-859 ai_table_get_aishou_distance
        /// （<c>min(君主相性距离 / 25, 5)</c>）。
        /// <para>相性值取 <c>Person.compatibility</c>，距离用 <c>CompatibilityDistance</c> 求差。</para>
        /// </summary>
        /// <param name="src">势力 A</param>
        /// <param name="dest">势力 B</param>
        /// <returns>0~5 的距离</returns>
        public static int GetAishouDistance(Force src, Force dest)
        {
            if (src == null || dest == null)
                return 5;

            Person srcGovernor = src.mGovernor;
            Person destGovernor = dest.mGovernor;
            if (srcGovernor == null || destGovernor == null)
                return 5;

            return System.Math.Min(srcGovernor.CompatibilityDistance(destGovernor) / 25, 5);
        }

        /// <summary>
        /// 判断我方是否"志在必得"敌方某座城。返回该敌势力名下最高的执念优先级。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:549-553 / 584-588 里调用的 get_city_priority，
        /// 表数据见 :614-698。
        /// <para>
        /// 【实现方式】原版是写死的"君主 Id → 城 Id"表；本项目把这份执念落在
        /// <see cref="Personality.obsessionCityIds"/>（性格内嵌属性），于是
        /// "袁绍执念邺城"只需给袁绍那条性格数据填上邺城 Id，无需改代码；
        /// 其他君主（性格里没有执念城）自然返回 0，行为与原版"表里没有就返回 0"一致。
        /// </para>
        /// </summary>
        /// <param name="src">我方势力</param>
        /// <param name="dest">敌方势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>0 表示不构成开战理由；≥2 表示构成（与原版阈值一致）</returns>
        public static int GetCityPriority(Force src, Force dest, Scenario scenario)
        {
            if (src == null || dest == null || scenario == null)
                return 0;

            Person governor = src.mGovernor;
            if (governor == null || governor.mPersonality == null)
                return 0;

            int best = 0;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != dest)
                    continue;
                int priority = governor.mPersonality.GetCityPriority(city.Id);
                if (priority > best)
                    best = priority;
            }
            return best;
        }

        /// <summary>
        /// 是否存在"高优先度"的敌方城池（有则直接开战）。
        /// </summary>
        /// <param name="src">我方势力</param>
        /// <param name="dest">敌方势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否存在</returns>
        static bool HasPriorityTargetCity(Force src, Force dest, Scenario scenario)
        {
            if (GetCityPriority(src, dest, scenario) < 2)
                return false;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive)
                    continue;
                if (city.BelongForce != dest)
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 距上次进攻的回合数。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:468 与 s11_sys_scen_ai.cpp:1039-1044。
        /// 数据来源：<see cref="Force.lastAttackTurn"/>，在各处"派兵出征"成功时由
        /// <see cref="Force.MarkAttackTurn"/> 写入（对应原版 ai_table_update_last_attack_turn）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>回合数</returns>
        static int GetTurnsSinceLastAttack(Force force, Scenario scenario)
        {
            if (force == null)
                return 0;
            return force.GetTurnsSinceLastAttack(scenario);
        }

        /// <summary>
        /// 判断两名武将是否互相欣赏（亲爱关系）。
        /// </summary>
        /// <param name="a">武将 A</param>
        /// <param name="b">武将 B</param>
        /// <returns>是否亲爱</returns>
        static bool IsLikePerson(Person a, Person b)
        {
            if (a == null || b == null)
                return false;
            return a.IsLike(b);
        }

        /// <summary>
        /// 判断两名武将是否互相厌恶。
        /// </summary>
        /// <param name="a">武将 A</param>
        /// <param name="b">武将 B</param>
        /// <returns>是否厌恶</returns>
        static bool IsHatePerson(Person a, Person b)
        {
            if (a == null || b == null)
                return false;
            return a.IsHate(b);
        }
    }
}
