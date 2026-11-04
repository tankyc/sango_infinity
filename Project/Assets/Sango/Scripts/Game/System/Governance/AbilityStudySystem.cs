/*
 * 文件名：AbilityStudySystem.cs
 * 描述：势力级"能力研究"与"能力培育"，消费 <see cref="Ability"/> 数据表。
 *
 * 对齐出处：Open311（D:\open311\s11）
 *   · 能力研究     —— s11_game_ai_force_cmd.cpp:754-817 force_research_ability
 *   · 培育总入口   —— s11_game_ai_force_cmd_learn.cpp:5-35 force_learn
 *   · 基础能力培育 —— 同上 :37-93  force_learn_base_stat
 *   · 兵种适性培育 —— 同上 :95-135
 *   · 特技培育     —— 同上 :137-200 force_learn_skill
 *
 * 【两层结构（与原版一致）】
 *   ① 研究：势力花金 + 行动力，把一条 Ability 从"未掌握"变为"已掌握"
 *           （原版 get_researching_ability_id 记录进行中，完成进 learned_abilities_）
 *   ② 培育：从"已掌握"的能力里，找符合条件的武将逐个学会
 *           （原版 get_learnable_ability_list 过滤已掌握 + 学习次数未满）
 *
 * 【基础能力的三档区间】由 <see cref="Ability.maxStat"/> 反查，
 * 沿用原版 StatTable 的固定对应：70→56~65 / 80→66~75 / 95→76~90。
 * 注意：档位区间与"非前线只培育政治"都只属**势力 AI 挑人**口径
 * （s11_game_ai_force_cmd_learn.cpp:64-77、:68-73）；玩家侧走原版 System::can_learn
 * （s11_sys_scen_ability.cpp:37-67），二者都不判。
 *
 * 【已知架构差异：单 actor vs 任务制】
 * 原版三条培育指令（LearnBaseStat / LearnHeishuTekisei / LearnSkill）各自是"势力级指令槽"，
 * can_learn_*_command 用 find_actor 判"本势力已有 1 人在执行该指令"，非空即整条指令不可用
 * （s11_sys_person_order_kunshu.cpp:1643-1653 / :1787-1798 / :1929-1940）。
 * 本项目改成 per-person 的 3 旬 Mission（见 StudyTurns），同一条指令可并发多人，
 * 培养速度约为原版的三倍。此项**有意保留**，未做单 actor 门禁。
 */

using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 势力级能力研究与能力培育。
    /// </summary>
    public static class AbilityStudySystem
    {
        // 研究与培育的金钱 / 行动力消耗、单次提升点数、触发概率、培育上限
        // 统一走 GainValueConfig 的 AbilityStudy* 段（存放在 Scenario.Cur.Variables.abilityStudyGain），
        // 可在剧本设置界面"能力研究与培育"分区调整 —— 项目规范禁止硬编码成长数值。
        // 各键默认值均已对照原版 PersonOrderManager 成员默认值（s11_sys_person_order.h:1364-1372）。

        /// <summary>研究一条能力的金钱消耗（原版 research_ability_gold_cost_ = 300）</summary>
        public static int ResearchGoldCost { get { return GainValueConfig.AbilityStudy(GainPlace.AbilityStudyResearchGoldCost); } }

        /// <summary>研究一条能力的行动力消耗（原版 research_ability_action_point_cost_ = 20；AI 预检门槛为该值 ×2，s11_game_ai_force_cmd.cpp:769）</summary>
        public static int ResearchActionPointCost { get { return GainValueConfig.AbilityStudy(GainPlace.AbilityStudyResearchActionPointCost); } }

        /// <summary>培育一次的金钱消耗（原版 learn_gold_cost_ = 0，培育不花钱）</summary>
        public static int LearnGoldCost { get { return GainValueConfig.AbilityStudy(GainPlace.AbilityStudyLearnGoldCost); } }

        /// <summary>培育一次的行动力消耗（原版 learn_action_point_cost_ = 20，三段培育统一扣减，s11_sys_person_order_kunshu.cpp:1717/1861/2003）</summary>
        public static int LearnActionPointCost { get { return GainValueConfig.AbilityStudy(GainPlace.AbilityStudyLearnActionPointCost); } }

        /// <summary>培育一次最多提升的能力点数（原版 n = min(maxStat - pure, 5) 的字面上限，s11_sys_person_order_kunshu.cpp:1749）</summary>
        public static int StatGain { get { return GainValueConfig.AbilityStudy(GainPlace.AbilityStudyStatGain); } }

        /// <summary>培育触发概率百分比（原版 rand_bool(50)）</summary>
        public static int LearnChance { get { return GainValueConfig.AbilityStudy(GainPlace.AbilityStudyLearnChance); } }

        /// <summary>培育累计经验上限（原版 max_person_learning_stat_exp_ = 2000，s11_db.h:183）。
        /// 【口径】点数 × 每人所需经验：默认 20 点 × 250 = 5000。
        /// 原版 exp_per_stat = 100 时 2000 恰为 20 点；本项目 AttributeExpLevelNeed 默认 250，
        /// 配置值不能再照抄 2000（否则只剩 8 点，老将几年后就被彻底堵死）。
        /// 达到后该属性不可再培育（s11_sys_scen_ability.cpp:51 的 can_learn 判定），非培育途径仍可涨到总上限。</summary>
        public static int LearnExpCap { get { return GainValueConfig.AbilityStudy(GainPlace.AbilityStudyLearnExpCap); } }

        /// <summary>
        /// 培育通道还能给某条属性加多少点。
        ///
        /// 口径：<c>(培育经验上限 - 当前属性经验) / 每人所需经验</c>，向下取整。
        /// 对齐出处：s11_sys_scen_ability.cpp:51 的 can_learn ——
        /// <c>stat_exp &gt;= max_person_learning_stat_exp_</c> 即不可再培育。
        /// 抽成方法是为了三处（AI 基础能力 / 玩家列表 / 玩家下单）用同一口径，
        /// 避免"列表里能选、点了却失败"这类分叉。
        /// </summary>
        /// <param name="stat">属性对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>剩余可培育点数；0 表示不可再培育</returns>
        public static int GetRemainingLearnPoints(PersonAttributeValue stat, Scenario scenario)
        {
            if (stat == null || scenario == null || scenario.Variables == null)
                return 0;
            int expPerStat = scenario.Variables.AttributeExpLevelNeed;
            if (expPerStat <= 0)
                return 0;
            int remainExp = LearnExpCap - stat.valueExp;
            return remainExp > 0 ? remainExp / expPerStat : 0;
        }

        // ==================== 研究（第一层） ====================

        /// <summary>
        /// 推进当前研究：每势力每回合调一次。
        /// 对齐出处：force_research_ability 完成时把能力并入 learned_abilities_。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>本回合是否有研究进度</returns>
        public static bool AdvanceResearch(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return false;
            if (force.researchingAbilityId < 0)
                return false;

            if (force.researchingTurnsLeft > 1)
            {
                force.researchingTurnsLeft--;
                return true;
            }

            int abilityId = force.researchingAbilityId;
            Ability ability = GetAbility(scenario, abilityId);

            // ① 标记研究完成 —— :1424-1426
            force.researchingAbilityId = -1;
            force.researchingTurnsLeft = 0;
            if (force.learnedAbilityIds == null)
                force.learnedAbilityIds = new HashSet<int>();
            force.learnedAbilityIds.Add(abilityId);
            // 该能力的学习计数清零 —— :1425（set_learning_counter(0)）；
            // 不清会导致重研究同名能力后因旧计数耗满而永远无法培育
            if (force.abilityLearnCounters != null)
                force.abilityLearnCounters.Remove(abilityId);

            Sango.Log.Info($"*[能力研究]{force.Name} 完成研究「{ability?.Name ?? abilityId.ToString()}」");

            // ② 隐藏能力激活：刚研究出来的前置可能让某条隐藏能力达标 —— :1442-1455
            //    与原版"开局随机填槽"不同，这里是达成制：激活只写槽位 + 打"待播动画"标记，
            //    玩家下一次打开研究界面才看到"从格子里出现"的演出。
            TryActivateHiddenAbilities(force, scenario);

            // ③ 玩家势力强提示 —— 原版研究完成发生在君主命令计时结束时
            //    （s11_sys_person_order_kunshu.cpp:1417-1455），这里补一次演出 + 消息栏播报
            if (force.IsPlayer)
                NotifyResearchComplete(force, ability, abilityId);

            return true;
        }

        /// <summary>
        /// 研究完成的玩家提示：RenderEvent 动画窗口（强提示）+ 消息栏播报。
        ///
        /// 强提示走 <see cref="Sango.Render.WindowEvent"/>，与技法研究完成
        /// （<c>TechniqueResearch</c> 里的 <c>window_technique_complete</c>）完全同一套机制：
        /// 窗口播完动画自动关闭，RenderEvent 队列据此继续往下走。
        /// </summary>
        /// <param name="force">完成研究的势力</param>
        /// <param name="ability">完成的能力（数据缺失时为 null）</param>
        /// <param name="abilityId">能力 Id（数据缺失时用于文案兜底）</param>
        static void NotifyResearchComplete(Force force, Ability ability, int abilityId)
        {
            if (force == null)
                return;

            string abilityName = ability != null ? ability.Name : abilityId.ToString();

            // 强提示窗口：能力数据缺失时不播（窗口没有可显示的内容）
            if (ability != null && Sango.Render.RenderEvent.Instance != null)
            {
                Sango.Render.WindowEvent windowEvent =
                    Sango.Render.RenderEvent.Instance.Create<Sango.Render.WindowEvent>();
                windowEvent.Init("window_ability_complete", new object[] { ability });
                Sango.Render.RenderEvent.Instance.Add(windowEvent);
            }

            // 消息栏：坐标取君主所在城，玩家点这条消息可以定位过去
            City city = force.mGovernor != null
                ? (force.mGovernor.BelongCity ?? force.mGovernor.CurrentCity)
                : null;
            Sango.Core.Player.PlayerMessage.AddTextMessage(
                $"【能力研究】{force.Name} 完成研究「{abilityName}」，武将们可以开始培育了。",
                force, city != null ? city.x : 0, city != null ? city.y : 0);
        }

        /// <summary>
        /// 某条能力当前是否可被研究。
        ///
        /// 对齐出处：s11_sys_scen_ability.cpp:5-30 is_ability_researchable：
        ///   · 能力有效；
        ///   · 势力尚未研究出它；
        ///   · 隐藏能力（`hiddenSlot >= 0`）必须已进入势力的隐藏槽位；
        ///   · 前置能力全部已研究。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="ability">能力</param>
        /// <returns>可研究返回 true</returns>
        public static bool IsAbilityResearchable(Force force, Ability ability)
        {
            if (force == null || ability == null)
                return false;
            if (!ability.IsAlive)
                return false;
            if (force.HasAbility(ability.Id))
                return false;
            if (ability.hiddenSlot >= 0 && !force.HasHiddenAbility(ability.Id))
                return false;
            return ability.IsPrerequisiteMet(force.learnedAbilityIds);
        }

        /// <summary>
        /// 挑一条能力开始研究。
        ///
        /// 对齐出处：force_research_ability（force_cmd.cpp:754-817）：
        ///   · 已在研究中 → 不重复开始
        ///   · 君主所在城金 ≥ 研究消耗、军团行动力 ≥ 消耗 × 2
        ///   · 候选 = 未掌握 + 前置满足
        ///   · 打分 <c>rand(20) + (value * 2 + statRank) * 5</c>，取最高
        ///     （statRank 是该能力所属属性在势力四维里的强弱排位，越弱排位越高分）
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否开始了研究</returns>
        public static bool StartResearch(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return false;
            if (force.researchingAbilityId >= 0)
                return false;

            Person governor = force.mGovernor;
            if (governor == null || !governor.IsAlive)
                return false;
            City city = governor.BelongCity ?? governor.CurrentCity;
            if (city == null)
                return false;

            // 资金口径（已核对原版 s11_game_ai_force_cmd.cpp:764-766）：
            //   原版 gold = min(building.gold, building.gold + gold_income) —— 收入非负时恒等于城内金，
            //   且执行处（s11_sys_person_order_kunshu.cpp:1358）同样只判 building.gold。
            //   即原版口径本就是"只看城内金"，本项目与此一致。
            if (city.gold < ResearchGoldCost)
                return false;

            // 行动力预检门槛 = 研究行动力消耗 ×2 —— :769（原版 ap_cost = 20，本项目独立配置）
            if (city.BelongCorps != null && city.BelongCorps.ActionPoint < ResearchActionPointCost * 2)
                return false;

            // 属性强弱排位：index = AbilityStatKind，值越小越强 → 转成"越弱分越高"
            int[] statRank = BuildStatRank(force, scenario);

            List<Ability> candidates = GetResearchableAbilities(force, scenario);
            if (candidates.Count == 0)
                return false;

            Ability best = null;
            int bestScore = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                Ability ability = candidates[i];
                int rank = 0;
                if (ability.statKind >= 0 && ability.statKind < statRank.Length)
                    rank = statRank[ability.statKind];
                int score = GameRandom.Range(0, 20) + (ability.value * 2 + rank) * 5;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = ability;
                }
            }
            if (best == null)
                return false;

            return StartResearchAt(force, scenario, city, best);
        }

        /// <summary>
        /// 指定"在哪个城、研究哪条能力"开始研究（玩家界面用）。
        ///
        /// 与 <see cref="StartResearch"/> 共用同一套前置判定，
        /// 区别只是候选由玩家指定而不是自动挑。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="city">出资城（须为本势力、金够）</param>
        /// <param name="ability">要研究的能力</param>
        /// <returns>是否成功开始</returns>
        public static bool StartResearchAt(Force force, Scenario scenario, City city, Ability ability)
        {
            if (force == null || scenario == null || city == null || ability == null)
                return false;
            if (force.researchingAbilityId >= 0)
                return false;
            if (force.HasAbility(ability.Id))
                return false;
            if (!ability.IsPrerequisiteMet(force.learnedAbilityIds))
                return false;
            if (city.gold < ResearchGoldCost)
                return false;

            city.AddGold(-ResearchGoldCost);
            // 扣研究行动力（一倍，预检才是两倍门槛）—— :1404
            city.BelongCorps?.ReduceActionPoint(ResearchActionPointCost);

            force.researchingAbilityId = ability.Id;
            // 研究耗时：timeCost 单位是"月"，原版按 turn(旬) 计时要 ×3 —— :1402
            force.researchingTurnsLeft = System.Math.Max(ability.timeCost * 3, 1);

            Sango.Log.Info($"*[能力研究]{force.Name} 在 {city.Name} 开始研究「{ability.Name}」，预计 {force.researchingTurnsLeft} 回合");
            return true;
        }

        /// <summary>
        /// 取"可研究"的能力：未掌握 + 前置满足。
        /// 对齐出处：Open311 `get_researchable_ability_list(force)`。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>候选能力列表</returns>
        public static List<Ability> GetResearchableAbilities(Force force, Scenario scenario)
        {
            List<Ability> result = new List<Ability>();
            SangoObjectSet<Ability> abilities = scenario?.CommonData?.Abilities;
            if (abilities == null || force == null)
                return result;

            // 【性能】原 ForEach(lambda) 捕获 force / result，改为按下标遍历。
            Ability[] researchableArray = abilities.objects;
            for (int i = 1; i < researchableArray.Length; i++)
            {
                Ability ability = researchableArray[i];
                if (ability == null)
                    continue;
                if (force.HasAbility(ability.Id))
                    continue;
                // 隐藏能力：未发现则不可研究 —— 对齐 is_ability_researchable（s11_sys_scen_ability.cpp:12-13）
                if (ability.hiddenSlot >= 0 && !force.HasHiddenAbility(ability.Id))
                    continue;
                if (!ability.IsPrerequisiteMet(force.learnedAbilityIds))
                    continue;
                result.Add(ability);
            }
            return result;
        }

        /// <summary>动态生成的 Skill 类能力起始 Id（避开手工数据的 1~999）</summary>
        const int DynamicSkillAbilityIdBase = 1001;

        /// <summary>
        /// 确保 Ability 表里有 Skill 类条目（隐藏能力槽的填充源）。
        ///
        /// 对齐说明：原版这些条目是剧本数据（Ability 表的 <c>hidden_slot</c> + <c>skill_id</c>），
        /// 本项目 `Abilities.json` 只手工填了基础能力与兵种适性，
        /// 因此这里按"**每种偏向（Feature.kind）随机取最高等级的特技**"动态生成 10 条，
        /// 每个隐藏槽一条 —— 与 `force_init_hidden_ability` 的"每槽填一条"结构一致。
        /// <para>
        /// 幂等：只要表里已经存在任意 Skill 类条目就直接返回，
        /// 因此读档 / 二次载入不会重复生成。
        /// </para>
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public static void EnsureSkillAbilities(Scenario scenario)
        {
            if (scenario == null || scenario.CommonData == null)
                return;
            SangoObjectSet<Ability> abilities = scenario.CommonData.Abilities;
            SangoObjectSet<Feature> features = scenario.CommonData.Features;
            if (abilities == null || features == null)
                return;

            bool hasSkill = false;
            // 【性能】原 ForEach(lambda) 捕获 hasSkill，改为按下标遍历；命中即 break。
            Ability[] skillCheckArray = abilities.objects;
            for (int i = 1; i < skillCheckArray.Length; i++)
            {
                Ability ability = skillCheckArray[i];
                if (ability != null && ability.IsSkill)
                {
                    hasSkill = true;
                    break;
                }
            }
            if (hasSkill)
                return;

            // FeatureKindType 共 9 种（1 部队移动 … 9 人际关系），10 个槽位循环取用
            const int kindCount = 9;
            for (int slot = 0; slot < Force.MaxHiddenAbilityCount; slot++)
            {
                int kind = slot % kindCount + 1;

                // 该偏向里等级最高的特技；同等级取 Id 最小的那个。
                // 【为什么不用随机】动态生成的 Ability 不写进 Abilities.json，
                // 读档时会被重新生成一次；若用随机，Id 1001~1010 对应的特技会变，
                // 而存档里的 `Force.hiddenAbilityIds` 记的是 Id —— 就会出现"槽里显示的特技变了"。
                // 因此这里用确定性规则，保证跨会话同一 Id 永远指向同一特技。
                int bestLevel = -1;
                Feature pick = null;
                features.ForEach(feature =>
                {
                    if (feature == null || feature.kind != kind)
                        return;
                    if (feature.level > bestLevel || (feature.level == bestLevel && pick != null && feature.Id < pick.Id))
                    {
                        bestLevel = feature.level;
                        pick = feature;
                    }
                });
                if (pick == null)
                    continue;

                Ability ability = new Ability();
                ability.Id = DynamicSkillAbilityIdBase + slot;
                ability.Name = pick.Name;
                ability.desc = pick.desc;
                ability.category = (int)AbilityCategory.Skill;
                ability.skillId = pick.Id;
                ability.hiddenSlot = slot;
                ability.learningLimit = (int)AbilityLearningLimit.Once;
                ability.timeCost = 1;
                ability.value = pick.level * 10;
                ability.statKind = (int)AbilityStatKind.Strength;
                ability.requiredAbilityIds = new int[Ability.MaxRequiredAbilityCount];
                ability.personStatType = -1;
                ability.troopKind = -1;
                ability.targetAptitude = -1;

                abilities.Add(ability);
            }
        }

        /// <summary>
        /// 10 个隐藏槽位各自的种类（<see cref="AbilityStatKind"/>），下标 = 槽位号。
        ///
        /// <para>
        /// 对齐出处：<c>force_init_hidden_ability</c>（s11_sys_scen_force.cpp:739-765）按势力最弱维挑选可用槽位 ——
        /// 统率 → {1, 6}、武力 → {0, 4, 5}、智力 → {3, 9}、政治 → {2, 7, 8, 3, 9}。
        /// 槽位 3 / 9 同时出现在政治与智力两组里，但落到具体能力上，每个槽的候选维度仍唯一。
        /// 本项目 <c>Abilities.json</c> 里每个槽的 4~5 条候选 statKind 完全一致，与上表逐项吻合：
        /// 0 武(0) / 1 统(1) / 2 政(2) / 3 智(3) / 4 武(0) / 5 武(0) / 6 统(1) / 7 政(2) / 8 政(2) / 9 智(3)。
        /// </para>
        /// <para>
        /// 用途：隐藏能力**只能落在同种类的位置上**（界面上即同一种底色的格子），
        /// 激活时用它做一次校验，防止 Json 里有人把异类能力塞进某个槽。
        /// 槽位坐标与原版研究界面 10 个隐藏位一一对应（水神/血路/指导/反计/疾驰/神将/飞将/微收/仁政/神算）。
        /// </para>
        /// </summary>
        static readonly int[] HiddenSlotStatKinds = { 0, 1, 2, 3, 0, 0, 1, 2, 2, 3 };

        /// <summary>
        /// 取隐藏槽位的种类（<see cref="AbilityStatKind"/>）。
        /// 界面按它选种类底图，激活按它判"同种类才能进槽"。
        /// </summary>
        /// <param name="slot">槽位下标 0~9</param>
        /// <returns>种类（0 武力 / 1 统率 / 2 政治 / 3 智力）；越界返回 -1</returns>
        public static int GetHiddenSlotStatKind(int slot)
        {
            if (slot < 0 || slot >= HiddenSlotStatKinds.Length)
                return -1;
            return HiddenSlotStatKinds[slot];
        }

        /// <summary>
        /// 清理"不符合达成制"的隐藏槽内容 —— 只用于旧存档迁移。
        ///
        /// <para>
        /// 【为什么需要】激活制改造之前，槽位沿用原版 <c>force_init_hidden_ability</c>
        /// （s11_sys_scen_force.cpp:701-802）的"开局随机预填 5 个槽"，槽里可能躺着一条
        /// **前置根本没研究**的能力。旧存档读进来后槽内容原样保留，
        /// 界面（<see cref="Sango.UI.UIAbilityStudyArea"/> → <c>FilterHiddenSlots</c>）
        /// 按"槽内容"把它画出来 —— 玩家就会看到一条自己既没研究前置、也点不动的隐藏能力。
        /// </para>
        /// <para>
        /// 【判据】与 <see cref="TryActivateHiddenAbilities"/> 完全对齐：
        /// 能力存在、且（已研究 或（前置全部满足 且 与槽位种类匹配））。不满足的一律清空，
        /// 随后由 <see cref="TryActivateHiddenAbilities"/> 按达成制重新填 —— 干净、无残留。
        /// </para>
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>被清空的槽位数量</returns>
        public static int PruneHiddenAbilitySlots(Force force, Scenario scenario)
        {
            if (force == null || scenario == null || force.hiddenAbilityIds == null)
                return 0;

            SangoObjectSet<Ability> abilities = scenario.CommonData != null ? scenario.CommonData.Abilities : null;
            if (abilities == null)
                return 0;

            int pruned = 0;
            for (int slot = 0; slot < force.hiddenAbilityIds.Length; slot++)
            {
                int abilityId = force.hiddenAbilityIds[slot];
                if (abilityId < 0)
                    continue;

                Ability ability = GetAbility(scenario, abilityId);
                int slotKind = GetHiddenSlotStatKind(slot);
                bool keep = ability != null
                    && (force.HasAbility(abilityId)
                        || (ability.IsPrerequisiteMet(force.learnedAbilityIds)
                            && slotKind >= 0 && ability.statKind == slotKind));

                if (keep)
                    continue;

                force.SetHiddenAbilityId(slot, -1);
                force.ClearHiddenAbilityNew(slot);
                pruned++;
            }

            // 旧档一次性会清掉几十上百个槽，逐条打日志会刷屏 —— 只汇总一条
            if (pruned > 0)
                Sango.Log.Info($"*[能力研究]{force.Name} 清理 {pruned} 个不符合达成制的隐藏槽（旧存档随机预填迁移）");
            return pruned;
        }

        /// <summary>
        /// 尝试激活隐藏能力：把"前置已全部研究完成"的隐藏能力写进它所属的空槽位。
        ///
        /// <para>
        /// 【与原版的差异】原版在开局用 <c>force_init_hidden_ability</c>
        /// （s11_sys_scen_force.cpp:701-802）按"最弱维偏好"随机预填 5 个槽，玩家一开局就能看到这些
        /// 隐藏能力（只是不可研究）。本项目改为**达成制**：槽位初始全空，只有当某条隐藏能力的
        /// 前置被全部研究出来时才激活；激活只写槽位 + 打"待播动画"标记，
        /// 真正的"出现"演出等玩家下一次打开研究界面才播（见 UIAbilityStudyArea）。
        /// </para>
        /// <para>
        /// 一个槽位只装一条：同槽的多条候选里，先满足前置的那条占位，其余保持未激活。
        /// </para>
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>本次新激活的槽位数量</returns>
        public static int TryActivateHiddenAbilities(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return 0;

            SangoObjectSet<Ability> abilities = scenario.CommonData != null ? scenario.CommonData.Abilities : null;
            if (abilities == null)
                return 0;

            int activated = 0;
            abilities.ForEach(ability =>
            {
                if (ability == null || !ability.IsAlive)
                    return;

                int slot = ability.hiddenSlot;
                if (slot < 0 || slot >= Force.MaxHiddenAbilityCount)
                    return;
                // 该槽已激活别的能力 —— 一槽一条
                if (force.GetHiddenAbilityId(slot) >= 0)
                    return;
                // 已经研究出来的不再激活
                if (force.HasAbility(ability.Id))
                    return;
                // 前置没研究完 —— 不激活
                if (!ability.IsPrerequisiteMet(force.learnedAbilityIds))
                    return;
                // 种类不符 —— 隐藏能力只能落在同种类（同色）的位置上
                int slotKind = HiddenSlotStatKinds[slot];
                if (ability.statKind != slotKind)
                {
                    Sango.Log.Warning($"*[能力研究] 能力「{ability.Name}」种类 {ability.statKind} 与隐藏槽 {slot} 的种类 {slotKind} 不符，已跳过");
                    return;
                }

                force.SetHiddenAbilityId(slot, ability.Id);
                force.MarkHiddenAbilityNew(slot);
                activated++;
                Sango.Log.Info($"*[能力研究]{force.Name} 激活隐藏能力「{ability.Name}」（槽位 {slot}）");
            });
            return activated;
        }

        // ==================== 培育（第二层） ====================

        /// <summary>
        /// 基础能力培育：给"属性基础值落在某档区间"的武将培育提升。
        ///
        /// 对齐出处：force_learn_base_stat（force_cmd_learn.cpp:37-93）+ 培育执行段
        /// （s11_sys_person_order_kunshu.cpp:1690-1753）：
        ///   · 候选能力 = 已掌握 + BaseStat 类 + 学习次数未满
        ///   · 区间由该能力的档位上限反查（70→56~65 / 80→66~75 / 95→76~90）
        ///   · 非前线据点只培育政治（原版 :68-73）
        ///   · can_learn 门槛：培育累计经验 ≥ 上限 或 基础值 ≥ maxStat → 不可培育
        ///     （s11_sys_scen_ability.cpp:51，对应 max_person_learning_stat_exp_ = 2000）
        ///   · 命中后 50% 概率执行（:77 rand_bool(50)）
        ///   · 提升走"能力经验"通道：n = min(maxStat - 基础值, 单次上限)（:1749），
        ///     加 n × AttributeExpLevelNeed 经验（原版 exp_per_stat × n），不直接改基础值
        ///   · 金钱消耗原版为 0（learn_gold_cost_ = 0），行动力消耗 20（:1717）
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否培育了一人</returns>
        public static bool LearnBaseStat(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return false;

            List<Ability> abilities = GetLearnedAbilities(force, scenario, AbilityCategory.BaseStat);
            if (abilities.Count == 0)
                return false;

            // 每点能力所需经验（原版 exp_per_stat = 100；本项目统一由规则 30 的节奏键承担）
            int expPerStat = scenario.Variables.AttributeExpLevelNeed;
            if (expPerStat <= 0)
                return false;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.BelongForce != force)
                    continue;
                // can_learn_order（s11_sys_person_order_kunshu.cpp:1602-1637）在本项目的口径
                // = IsFree（无部队/无任务/非俘虏/存活）+ IsHealthy（无伤病），见 Person.IsFree 注释。
                if (!person.IsFree || !person.IsHealthy)
                    continue;

                City city = person.BelongCity ?? person.CurrentCity;
                if (city == null || city.gold < LearnGoldCost)
                    continue;

                OutpostInfo info = OutpostRuntime.Find(city.Id);
                // 交战据点整人跳过 —— 原版 get_learnable_person_list 公共段
                // （s11_game_ai_force_cmd_learn.cpp:408-409）：
                // `kyoten_[...].state == KyotenState_Battle` → continue（三段共用）。
                if (info != null && info.state == OutpostState.Battle)
                    continue;
                bool isFront = info != null && info.state == OutpostState.Front;

                for (int a = 0; a < abilities.Count; a++)
                {
                    Ability ability = abilities[a];
                    // 原版 System::can_learn（s11_sys_scen_ability.cpp:48-53）对基础能力**没有**
                    // "该武将已学过这条 ability"的判定，只判 exp 上限与 pure < maxStat，
                    // 因此同一条能力可以对同一人反复培育，直到触及上限。
                    // 非前线只培育政治 —— :68-73
                    if (!isFront && ability.personStatType != 3)
                        continue;

                    PersonAttributeValue stat = GetAttribute(person, ability.personStatType);
                    if (stat == null)
                        continue;

                    // can_learn：基础值达能力上限 → 不可再培育 —— s11_sys_scen_ability.cpp:51
                    int pure = stat.baseValue;
                    if (pure >= ability.maxStat)
                        continue;
                    // 培育通道的剩余点数（经验达上限即 0）
                    int remainPoints = GetRemainingLearnPoints(stat, scenario);
                    if (remainPoints <= 0)
                        continue;

                    GetTierRange(ability.maxStat, out int rangeMin, out int rangeMax);
                    if (pure < rangeMin || pure > rangeMax)
                        continue;
                    if (!GameRandom.Chance(LearnChance))
                        continue;

                    // 单次提升点数：受"能力上限剩余空间""单次上限""培育经验剩余额度"三重约束 —— :1749
                    int gain = System.Math.Min(ability.maxStat - pure, StatGain);
                    gain = System.Math.Min(gain, remainPoints);
                    if (gain <= 0)
                        continue;

                    // 【任务制】原版培育是武将任务（person_set_order_timer(actor, 3) + action_done），
                    // 这里只发起任务并扣费；属性在 3 旬后由 FinishStudyMission 结算。
                    // gain 预先算好放进 missionParams2，避免完成时条件变化导致重算偏差。
                    city.AddGold(-LearnGoldCost);
                    city.BelongCorps?.ReduceActionPoint(LearnActionPointCost);
                    person.SetMission(MissionType.PersonStudyAbility, city, StudyTurns, ability.Id, gain);

                    Sango.Log.Info($"*[能力培育]{force.Name} 的 {person.Name} 开始研习「{ability.Name}」（{StudyTurns} 旬，预计 +{gain}）");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 兵种适性培育：把某兵种的适性提到表里的目标等级。
        ///
        /// 对齐出处：force_cmd_learn.cpp:95-135（兵种适性培育段）；
        /// 挑人前置来自 get_learnable_person_list（s11_game_ai_force_cmd_learn.cpp:392-427）：
        /// 据点在前线 + 当前武力 ≥ 70，否则该武将跳过。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否培育了一人</returns>
        public static bool LearnTroopAptitude(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return false;

            List<Ability> abilities = GetLearnedAbilities(force, scenario, AbilityCategory.TroopAptitude);
            if (abilities.Count == 0)
                return false;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.BelongForce != force)
                    continue;
                // can_learn_order 口径 = IsFree + IsHealthy（见 Person.IsFree 注释）
                if (!person.IsFree || !person.IsHealthy)
                    continue;

                City city = person.BelongCity ?? person.CurrentCity;
                if (city == null || city.gold < LearnGoldCost)
                    continue;

                // 原版 get_learnable_person_list 的兵种适性段
                // （s11_game_ai_force_cmd_learn.cpp:415-418）：
                // `kyoten_[...].state != KyotenState_Front or person->get_stat(Strength) < 70` → 跳过。
                // 注：`get_stat` 是含经验加成的当前值，对应本项目 Person.Strength（非 baseValue）。
                OutpostInfo info = OutpostRuntime.Find(city.Id);
                if (info == null || info.state != OutpostState.Front)
                    continue;
                if (person.Strength < 70)
                    continue;

                for (int a = 0; a < abilities.Count; a++)
                {
                    Ability ability = abilities[a];
                    // 原版 System::can_learn 的适性分支（s11_sys_scen_ability.cpp:54-58）
                    // 只判"当前适性是否仍差目标一档"，无 person 级"已学"锁 ——
                    // 达到目标档后由下面的 baseValue 判定自然拦住。

                    PersonAbilityValue aptitude = GetAptitude(person, ability.troopKind);
                    if (aptitude == null)
                        continue;
                    // 已达标或超过目标档 → 不需要这条
                    if (aptitude.baseValue >= ability.targetAptitude)
                        continue;
                    if (!GameRandom.Chance(LearnChance))
                        continue;

                    // 【任务制】同基础能力：发起 3 旬任务，适性在完成时由 FinishStudyMission 提升
                    city.AddGold(-LearnGoldCost);
                    city.BelongCorps?.ReduceActionPoint(LearnActionPointCost);
                    person.SetMission(MissionType.PersonStudyAbility, city, StudyTurns, ability.Id);

                    Sango.Log.Info($"*[能力培育]{force.Name} 的 {person.Name} 开始研习「{ability.Name}」（{StudyTurns} 旬）");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 特技培育（完整移植 force_learn_skill）。
        ///
        /// 对齐出处：s11_game_ai_force_cmd_learn.cpp:137-396。
        /// <para>
        /// 两个阶段照搬原版：
        /// ① 先从"已研究且仍可学"的特技里挑**一条** target ——
        ///    三级比较键 势力内没人有 &gt; 剩余学习名额多 &gt; 50% 掷骰
        ///    （<see cref="FeatureLearnRule.IsPreferredAbility"/>）；
        /// ② 再对这条特技给全势力武将打分取最高（同分 50% 掷骰）。
        /// </para>
        /// <para>
        /// 【打分差异已消除】原版按特技 Id 分支的 30 多套公式，本项目已搬到 Features.json：
        /// 分数口径 / 兵种适性 / 是否要求前线 / 驻在都市或关港 / 都市设施 / 兵力下限
        /// 全部来自 <see cref="Feature"/> 的 learnXxx 字段，代码里**不含任何特技 Id 判断**
        /// （见 <see cref="FeatureLearnRule.TryCalcScore"/>）。
        /// </para>
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否有一人开始研习</returns>
        public static bool LearnSkill(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return false;

            // 候选 = 已研究 + 学习名额未满 + 特技数据有效
            List<Ability> abilities = GetLearnedAbilities(force, scenario, AbilityCategory.Skill);
            if (abilities.Count == 0)
                return false;

            // 阶段一：选一条待培育特技 —— :151-186
            HashSet<int> ownedSkillIds = FeatureLearnRule.CollectOwnedSkillIds(force, scenario);
            Ability target = null;
            for (int a = 0; a < abilities.Count; a++)
            {
                Ability candidate = abilities[a];
                if (candidate.skillId <= 0)
                    continue;
                if (FeatureLearnRule.IsPreferredAbility(candidate, target, force, ownedSkillIds))
                    target = candidate;
            }
            if (target == null)
                return false;

            Feature feature = scenario.GetObject<Feature>(target.skillId);
            if (feature == null)
                return false;

            // 阶段二：给 target 打分选人 —— :189-386
            Person best = null;
            City bestCity = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.BelongForce != force)
                    continue;
                // can_learn_order（本项目口径 = IsFree）+ 健康 + 已拥有该特技
                // —— s11_sys_person_order_kunshu.cpp:1602-1637
                if (!person.IsFree || !person.IsHealthy)
                    continue;
                if (person.HasFeatrue(target.skillId))
                    continue;
                if (person.FeatureList == null)
                    continue;

                City city = person.BelongCity ?? person.CurrentCity;
                if (city == null || city.gold < LearnGoldCost)
                    continue;

                // 交战据点整人跳过 —— 原版 get_learnable_person_list 公共段
                // （s11_game_ai_force_cmd_learn.cpp:408-409）：三段共用，连内政类特技也不在战斗中培育。
                OutpostInfo info = OutpostRuntime.Find(city.Id);
                if (info != null && info.state == OutpostState.Battle)
                    continue;

                int score;
                if (!FeatureLearnRule.TryCalcScore(person, feature, city, out score))
                    continue;

                // 取最高分；同分 50% 掷骰替换 —— :379-385
                if (best == null || bestScore < score
                    || (bestScore == score && GameRandom.Chance(50)))
                {
                    best = person;
                    bestScore = score;
                    bestCity = city;
                }
            }
            if (best == null)
                return false;

            // 【任务制】发起封闭研习，特技在完成时由 FinishStudyMission 授予
            bestCity.AddGold(-LearnGoldCost);
            bestCity.BelongCorps?.ReduceActionPoint(LearnActionPointCost);
            best.SetMission(MissionType.PersonStudyAbility, bestCity, StudyTurns, target.Id);

            Sango.Log.Info($"*[特技培育]{force.Name} 的 {best.Name} 开始研习「{target.Name}」（{StudyTurns} 旬，评分 {bestScore}）");
            return true;
        }

        // ==================== 玩家侧：单人培育 ====================

        /// <summary>
        /// 取某武将当前可研习的能力列表（玩家界面用）。
        ///
        /// 判定口径严格按原版**玩家侧**链路，不是 AI 挑人那套：
        /// <c>can_learn_base_stat_command</c> / <c>can_learn_skill_command</c>
        /// （s11_sys_person_order_kunshu.cpp:1627-1688、:1913-1974）
        /// → <c>PersonOrderManager::get_learnable_person_list</c>（同文件 :1583-1602）
        /// → <c>PersonOrderManager::can_learn_order</c>（同文件 :1604-1625）
        /// → <c>System::can_learn</c>（s11_sys_scen_ability.cpp:37-67）。
        /// <para>
        /// 【与 AI 口径的差异】原版下列判定**只存在于势力 AI 挑人段**
        /// （s11_game_ai_force_cmd_learn.cpp），玩家侧一条都没有：
        /// <list type="number">
        /// <item>非前线只培育政治（:68-73）</item>
        /// <item>基础能力档位区间 56~65 / 66~75 / 76~90（:64-77）</item>
        /// <item>特技的驻在地 / 据点兵力 / 前线 / 都市设施 / 适性 B 档（:189-386、:392-427）</item>
        /// </list>
        /// 本项目原先把这套 AI 口径直接套到玩家界面，其中"前线"还依赖
        /// <see cref="OutpostRuntime"/> 这张"单势力、AI 准备阶段"的快照表 ——
        /// 玩家手动操作时可能为空或还停在上一个 AI 势力，于是被误判成非前线。
        /// 现在玩家分支（<paramref name="aiPerspective"/> = false）完全不看这些。
        /// </para>
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="aiPerspective">
        /// true = 按**势力 AI** 的挑人口径附加上述全部 AI 专属判定。
        /// 仅供 <see cref="CountLearnablePersons"/> 的排查日志使用，玩家界面必须传 false。
        /// </param>
        /// <returns>可研习的能力；没有返回空列表</returns>
        public static List<Ability> GetLearnableAbilitiesFor(Person person, Scenario scenario,
            bool aiPerspective = false)
        {
            List<Ability> result = new List<Ability>();
            if (person == null || !person.IsAlive || scenario == null)
                return result;
            // can_learn_order 口径（s11_sys_person_order_kunshu.cpp:1604-1625）：
            // 无部队 / 无任务 / 非俘虏（IsFree）。放在方法内部而不是交给调用方，
            // 避免将来新增调用方漏判 → "列表能选、点了必失败"。
            if (!person.IsFree)
                return result;
            // 健康判据 —— 原版 can_learn_order（同文件 :1622-1623）：
            // 带伤的人不该出现在可选列表里，避免"列表能选、点了必失败"。
            if (!person.IsHealthy)
                return result;

            Force force = person.BelongForce;
            if (force == null)
                return result;
            City city = person.BelongCity ?? person.CurrentCity;
            if (city == null)
                return result;

            SangoObjectSet<Ability> abilities = scenario.CommonData != null ? scenario.CommonData.Abilities : null;
            if (abilities == null)
                return result;

            // 玩家侧不判前线（见方法注释）；AI 口径才查态势表
            bool isFront = true;
            if (aiPerspective)
            {
                OutpostInfo info = OutpostRuntime.Find(city.Id);
                isFront = info != null && info.state == OutpostState.Front;
            }

            abilities.ForEach(ability =>
            {
                if (ability == null)
                    return;
                if (!force.HasAbility(ability.Id))
                    return;
                // 原版无 person 级"已学该 ability"锁（见 System::can_learn）：
                // 基础能力靠 exp / pure 上限自然收敛，特技靠下面的 HasFeatrue 拦。
                if (force.GetAbilityLearnCount(ability.Id) >= ability.LearningLimitCount)
                    return;

                if (ability.IsBaseStat)
                {
                    // 玩家口径 —— 原版 System::can_learn（s11_sys_scen_ability.cpp:48-53）：
                    // 培育经验未达上限 且 基础值 < 该能力上限，两条即可学。
                    PersonAttributeValue stat = GetAttribute(person, ability.personStatType);
                    if (stat == null || stat.baseValue >= ability.maxStat)
                        return;
                    // 培育通道剩余点数为 0 → 已到培育上限（原版 can_learn 不通过）
                    if (GetRemainingLearnPoints(stat, scenario) <= 0)
                        return;
                    if (aiPerspective)
                    {
                        // ↓ 两条均为势力 AI 挑人专属，原版 System::can_learn 没有：
                        //   ① 非前线只培育政治 —— s11_game_ai_force_cmd_learn.cpp:68-73
                        //   ② 档位区间 56~65 / 66~75 / 76~90 —— 同文件 :64-77
                        if (!isFront && ability.personStatType != 3)
                            return;
                        GetTierRange(ability.maxStat, out int rangeMin, out int rangeMax);
                        if (stat.baseValue < rangeMin || stat.baseValue > rangeMax)
                            return;
                    }
                    result.Add(ability);
                }
                else if (ability.IsTroopAptitude)
                {
                    PersonAbilityValue aptitude = GetAptitude(person, ability.troopKind);
                    if (aptitude == null || aptitude.baseValue >= ability.targetAptitude)
                        return;
                    if (aiPerspective)
                    {
                        // AI 挑人专属（s11_game_ai_force_cmd_learn.cpp:415-418）：
                        // 据点必须处在前线，且当前武力 ≥ 70 —— 与 LearnTroopAptitude 同口径
                        if (!isFront || person.Strength < 70)
                            return;
                    }
                    result.Add(ability);
                }
                else if (ability.IsSkill)
                {
                    if (ability.skillId <= 0 || person.HasFeatrue(ability.skillId))
                        return;
                    if (person.FeatureList == null)
                        return;
                    Feature skillFeature = scenario.GetObject<Feature>(ability.skillId);
                    if (skillFeature == null)
                        return;
                    // 玩家口径 —— 原版 System::can_learn（s11_sys_scen_ability.cpp:60-64）
                    // 的 case AbilityType_Skill 只判"未拥有该特技"，上面已判完即可学。
                    // 驻在地 / 据点兵力 / 前线 / 都市设施 / 适性 B 档全是 AI 挑人专属
                    // （s11_game_ai_force_cmd_learn.cpp:189-386、:392-427），玩家侧一律不判。
                    if (aiPerspective)
                    {
                        int skillScore;
                        if (!FeatureLearnRule.TryCalcScore(person, skillFeature, city, out skillScore))
                            return;
                    }
                    result.Add(ability);
                }
            });
            return result;
        }

        /// <summary>
        /// 只做"这个武将能不能现在学这条能力"的判定，**不扣费、不派任务**。
        ///
        /// 与 <see cref="BeginStudy"/> 共用同一套条件（二者必须永远一致）：
        /// <see cref="BeginStudy"/> 内部就是先调本方法，通过后才执行。
        /// 界面用它来决定"节点是否可点 / 确定键是否高亮"，因此这里必须能给出**具体原因**。
        ///
        /// 判定口径与 <see cref="GetLearnableAbilitiesFor"/> 的玩家分支一致
        /// （原版 can_learn_order + System::can_learn），
        /// **不套用 AI 挑人专属的前线 / 档位区间 / 驻在地 / 设施 / 兵力 / 适性 B 档**。
        /// </summary>
        /// <param name="person">要研习的武将</param>
        /// <param name="ability">研习的能力</param>
        /// <param name="city">出资城</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="gain">基础能力类的预计提升点数（适性 / 特技为 0）</param>
        /// <param name="reason">不可执行的原因（可执行时为 null）</param>
        /// <returns>可执行返回 true</returns>
        public static bool CanStudy(Person person, Ability ability, City city, Scenario scenario,
            out int gain, out string reason)
        {
            gain = 0;
            reason = null;

            if (person == null || ability == null || city == null || scenario == null)
            {
                reason = "数据无效";
                return false;
            }
            // can_learn_order 口径（s11_sys_person_order_kunshu.cpp:1604-1625、:1634-1635）
            if (!person.IsAlive)
            {
                reason = "武将已阵亡";
                return false;
            }
            if (!person.IsFree)
            {
                reason = "武将当前不空闲（在部队中或已有任务）";
                return false;
            }
            if (!person.IsHealthy)
            {
                reason = "武将有伤病在身";
                return false;
            }

            // 不判 person 级"已学该 ability"：原版 System::can_learn 没有这条，
            // 同一人可以对同一条基础能力/适性反复培育，直到数值或经验触及上限。
            Force force = person.BelongForce;
            if (force == null)
            {
                reason = "武将无所属势力";
                return false;
            }
            if (!force.HasAbility(ability.Id))
            {
                reason = "该能力尚未研究完成";
                return false;
            }
            if (force.GetAbilityLearnCount(ability.Id) >= ability.LearningLimitCount)
            {
                reason = "该能力的学习次数已用完";
                return false;
            }
            if (city.gold < LearnGoldCost)
            {
                reason = $"资金不足（需 {LearnGoldCost} 金）";
                return false;
            }
            var corps = city.BelongCorps;
            if (corps != null && corps.ActionPoint < LearnActionPointCost)
            {
                reason = $"行动力不足（需 {LearnActionPointCost}）";
                return false;
            }

            if (ability.IsBaseStat)
            {
                // 原版玩家侧 System::can_learn（s11_sys_scen_ability.cpp:48-53）只判两条：
                // 培育经验未达上限、基础值 < 该能力上限。
                // AI 的"档位区间 56~65 / 66~75 / 76~90"（s11_game_ai_force_cmd_learn.cpp:64-77）不适用。
                PersonAttributeValue stat = GetAttribute(person, ability.personStatType);
                if (stat == null)
                {
                    reason = "能力数据与武将属性不匹配";
                    return false;
                }
                if (stat.baseValue >= ability.maxStat)
                {
                    reason = "该属性已达到本条能力的上限";
                    return false;
                }
                // 培育通道剩余点数（经验达上限即 0，等同原版 can_learn 不通过）
                int remainPoints = GetRemainingLearnPoints(stat, scenario);
                if (remainPoints <= 0)
                {
                    reason = "该属性的培育经验已达上限";
                    return false;
                }

                gain = System.Math.Min(ability.maxStat - stat.baseValue, StatGain);
                gain = System.Math.Min(gain, remainPoints);
                if (gain <= 0)
                {
                    reason = "该属性已无可提升空间";
                    return false;
                }
            }
            else if (ability.IsTroopAptitude)
            {
                PersonAbilityValue aptitude = GetAptitude(person, ability.troopKind);
                if (aptitude == null)
                {
                    reason = "能力数据与兵种不匹配";
                    return false;
                }
                if (aptitude.baseValue >= ability.targetAptitude)
                {
                    reason = "该兵种适性已达目标档位";
                    return false;
                }
            }
            else if (ability.IsSkill)
            {
                if (ability.skillId <= 0 || person.FeatureList == null)
                {
                    reason = "特技数据无效";
                    return false;
                }
                if (person.HasFeatrue(ability.skillId))
                {
                    reason = "该武将已拥有此特技";
                    return false;
                }

                // 原版玩家侧 System::can_learn（s11_sys_scen_ability.cpp:60-64）的
                // case AbilityType_Skill 只判"未拥有该特技"，上面已判完。
                // 驻在地 / 据点兵力 / 前线 / 都市设施 / 适性 B 档均为 AI 挑人专属
                // （s11_game_ai_force_cmd_learn.cpp:189-386、:392-427），玩家侧一律不判。
                if (scenario.GetObject<Feature>(ability.skillId) == null)
                {
                    reason = "特技数据无效";
                    return false;
                }
            }
            else
            {
                reason = "该能力类型不支持培育";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 对指定武将发起研习任务（<see cref="StudyTurns"/> 旬后由 <see cref="FinishStudyMission"/> 结算）。
        ///
        /// 玩家主动操作，不掷概率。所有条件都在 <see cref="CanStudy"/> 里，
        /// 本方法只负责"扣费 + 派任务 + 记账"，保证界面上的可执行判定与真正执行永远同源。
        /// </summary>
        /// <param name="person">要研习的武将</param>
        /// <param name="ability">研习的能力</param>
        /// <param name="city">出资城</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功发起</returns>
        public static bool BeginStudy(Person person, Ability ability, City city, Scenario scenario)
        {
            if (!CanStudy(person, ability, city, scenario, out int gain, out string reason))
            {
                Sango.Log.Warning($"[能力培育] 无法开始研习：{reason}");
                return false;
            }

            Force force = person.BelongForce;
            city.AddGold(-LearnGoldCost);
            city.BelongCorps?.ReduceActionPoint(LearnActionPointCost);
            person.SetMission(MissionType.PersonStudyAbility, city, StudyTurns, ability.Id, gain);

            Sango.Log.Info($"*[能力培育]{force.Name} 的 {person.Name} 开始研习「{ability.Name}」（{StudyTurns} 旬，{city.Name}）");
            return true;
        }

        /// <summary>
        /// 剩余可学习次数 = 学习次数上限 - 本势力已学次数。
        /// 对齐出处：Open311 `Ability::get_learning_limit_count()` 与 `Force::get_learning_counter()` 的差值。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="ability">能力</param>
        /// <returns>剩余次数（下限 0）</returns>
        public static int GetRemainLearnCount(Force force, Ability ability)
        {
            if (ability == null)
                return 0;
            int learned = force != null ? force.GetAbilityLearnCount(ability.Id) : 0;
            return System.Math.Max(0, ability.LearningLimitCount - learned);
        }

        /// <summary>
        /// 取"本势力已研究完毕"的指定大类能力。
        ///
        /// 与 <see cref="GetLearnedAbilities"/> 的区别：本方法**不排除学习次数已满的能力** ——
        /// 培育界面要把它们也摆出来（显示"剩 0"并压暗），否则玩家会以为这条能力消失了。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="category">能力大类</param>
        /// <returns>已研究的能力列表（按能力表顺序）</returns>
        public static List<Ability> GetResearchedAbilities(Force force, Scenario scenario, AbilityCategory category)
        {
            List<Ability> result = new List<Ability>();
            SangoObjectSet<Ability> abilities = scenario?.CommonData?.Abilities;
            if (abilities == null || force == null)
                return result;

            // 【性能】原 ForEach(lambda) 捕获 category / force / result，改为按下标遍历。
            Ability[] abilityArray = abilities.objects;
            for (int i = 1; i < abilityArray.Length; i++)
            {
                Ability ability = abilityArray[i];
                if (ability == null || ability.category != (int)category)
                    continue;
                if (!force.HasAbility(ability.Id))
                    continue;
                result.Add(ability);
            }
            return result;
        }

        // ==================== 培育任务（第三层：任务结算） ====================

        /// <summary>
        /// 培育闭关时长（旬）。对齐 Open311 的 <c>person_set_order_timer(actor, 3)</c>（默认 3 旬）。
        /// 数值走 GainValueConfig.AbilityStudyMissionTurns，可在剧本设置界面"能力研究与培育"分区调；
        /// 下限钳到 1 —— 配成 0 会让任务在发起当回合就结算，等于没有闭关。
        /// </summary>
        public static int StudyTurns
        {
            get { return System.Math.Max(1, GainValueConfig.AbilityStudy(GainPlace.AbilityStudyMissionTurns)); }
        }

        /// <summary>
        /// 完成培育任务：由 <see cref="Person.UpdateMission"/> 在
        /// <see cref="MissionType.PersonStudyAbility"/> 的计数归零时调用。
        ///
        /// 按能力大类分派到三个 Apply 方法。任何一步失效（人已换势力 / 能力数据被删 /
        /// 条件不再满足）都只返回 false，不产生半成品状态 —— 扣掉的费用视为研习成本。
        /// </summary>
        /// <param name="person">完成任务的武将</param>
        /// <param name="abilityId">研习的能力 Id（来自 missionParams1）</param>
        /// <param name="gainHint">发起时算好的提升点数（来自 missionParams2，仅基础能力用）</param>
        /// <returns>成功结算返回 true</returns>
        public static bool FinishStudyMission(Person person, int abilityId, int gainHint)
        {
            if (person == null || !person.IsAlive)
                return false;
            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return false;
            Ability ability = GetAbility(scenario, abilityId);
            if (ability == null)
                return false;
            Force force = person.BelongForce;
            if (force == null)
                return false;
            City city = person.BelongCity ?? person.CurrentCity;
            if (city == null)
                return false;

            bool finished;
            if (ability.IsBaseStat)
                finished = ApplyBaseStat(person, ability, city, force, scenario, gainHint);
            else if (ability.IsTroopAptitude)
                finished = ApplyTroopAptitude(person, ability, city, force);
            else if (ability.IsSkill)
                finished = ApplySkill(person, ability, city, force, scenario);
            else
                return false;

            // 结算成功 → 玩家侧反馈（武将对话框 + 消息栏）。失败（人已换势力 / 数据失效）不打扰玩家。
            if (finished)
                NotifyStudyComplete(person, ability, force, city);

            return finished;
        }

        /// <summary>
        /// 培育完成的玩家反馈：武将对话框（走 RenderEvent 队列）+ 消息栏播报。
        ///
        /// <para>
        /// 【为什么对话框要进 RenderEvent】培育任务是在回合结算时到期
        /// （<see cref="Person.UpdateMission"/> → 本方法），那一刻可能还排着部队移动 / 城池结算的演出；
        /// 直接 <c>GameDialog.Open</c> 会跟它们抢窗口，所以排进队列，轮到本事件时再弹。
        /// </para>
        /// <para>AI 势力静默结算，否则回合推进会被一堆对话框拖住。</para>
        /// </summary>
        /// <param name="person">完成研习的武将</param>
        /// <param name="ability">研习的能力</param>
        /// <param name="force">所属势力</param>
        /// <param name="city">所在城（可为空）</param>
        static void NotifyStudyComplete(Person person, Ability ability, Force force, City city)
        {
            if (person == null || ability == null || force == null || !force.IsPlayer)
                return;

            // 消息栏：与"研究完成"同一入口，玩家在消息列表里能看到谁学成了什么
            Sango.Core.Player.PlayerMessage.AddTextMessage(
                $"【能力培育】{person.Name} 研习「{ability.Name}」完成（{city?.Name}）",
                force, city != null ? city.x : 0, city != null ? city.y : 0);

            // 武将对话框：按能力大类换台词，第一人称更能体现"武将反馈"
            if (Sango.Render.RenderEvent.Instance == null)
                return;

            Sango.Render.PersonStudyCompleteEvent studyEvent =
                Sango.Render.RenderEvent.Instance.Create<Sango.Render.PersonStudyCompleteEvent>();
            studyEvent.Init(person, BuildStudyCompleteLine(ability));
            Sango.Render.RenderEvent.Instance.Add(studyEvent);
        }

        /// <summary>按能力大类生成武将在对话框里说的话</summary>
        /// <param name="ability">研习的能力</param>
        /// <returns>台词</returns>
        static string BuildStudyCompleteLine(Ability ability)
        {
            if (ability == null)
                return "主公，属下闭关研习已有所成！";
            if (ability.IsTroopAptitude)
                return $"主公，属下已通晓「{ability.Name}」之要领，率此军征战再无滞碍！";
            if (ability.IsSkill)
                return $"主公，属下已习得特技「{ability.Name}」，必不负主公厚望！";
            return $"主公，属下闭关研习「{ability.Name}」已有所成，日后定当更加尽力！";
        }

        /// <summary>结算基础能力提升（走经验通道，SetExp 内部按上限封顶并重算当前值）</summary>
        static bool ApplyBaseStat(Person person, Ability ability, City city, Force force,
            Scenario scenario, int gain)
        {
            PersonAttributeValue stat = GetAttribute(person, ability.personStatType);
            if (stat == null)
                return false;
            int expPerStat = scenario.Variables.AttributeExpLevelNeed;
            if (expPerStat <= 0 || gain <= 0)
                return false;
            // 闭关 3 旬期间数值可能已被别处推高 → 落地时按当前状态重新钳制。
            // 原版在 exec_learn_base_stat_command（s11_sys_person_order_kunshu.cpp:1749）
            // 里是即时算 n = min(maxStat - pure, 上限)，等价口径。
            gain = System.Math.Min(gain, ability.maxStat - stat.baseValue);
            gain = System.Math.Min(gain, GetRemainingLearnPoints(stat, scenario));
            if (gain <= 0)
                return false;

            stat.SetExp(stat.valueExp + gain * expPerStat, scenario);
            person.MarkAbilityLearned(ability.Id);
            force.MarkAbilityLearned(ability.Id);

            Sango.Log.Info($"*[能力培育]{force.Name} 的 {person.Name} 通过「{ability.Name}」+{gain} 点至 {stat.Value}（{city.Name}）");
            return true;
        }

        /// <summary>结算兵种适性提升（直接设到目标档位）</summary>
        static bool ApplyTroopAptitude(Person person, Ability ability, City city, Force force)
        {
            PersonAbilityValue aptitude = GetAptitude(person, ability.troopKind);
            if (aptitude == null)
                return false;
            if (aptitude.baseValue >= ability.targetAptitude)
                return false;

            aptitude.baseValue = ability.targetAptitude;
            aptitude.Update();
            person.MarkAbilityLearned(ability.Id);
            force.MarkAbilityLearned(ability.Id);

            Sango.Log.Info($"*[能力培育]{force.Name} 的 {person.Name} 通过「{ability.Name}」适性提升至 {aptitude.baseValue}（{city.Name}）");
            return true;
        }

        /// <summary>结算特技授予</summary>
        static bool ApplySkill(Person person, Ability ability, City city, Force force, Scenario scenario)
        {
            // 原版 System::can_learn 的特技分支只判 has_skill，对应的就是下面的 HasFeatrue
            if (ability.skillId <= 0)
                return false;
            if (person.HasFeatrue(ability.skillId))
                return false;
            Feature feature = scenario.GetObject<Feature>(ability.skillId);
            if (feature == null || person.FeatureList == null)
                return false;

            person.FeatureList.Add(feature);
            person.MarkAbilityLearned(ability.Id);
            force.MarkAbilityLearned(ability.Id);

            Sango.Log.Info($"*[特技培育]{force.Name} 的 {person.Name} 通过「{ability.Name}」习得特技「{feature.Name}」（{city.Name}）");
            return true;
        }

        // ==================== 工具 ====================

        /// <summary>
        /// 取"已掌握且该类型、学习次数未满"的能力。
        /// 对齐出处：Open311 `get_learnable_ability_list(force, type)`。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="category">能力大类</param>
        /// <returns>候选能力列表</returns>
        public static List<Ability> GetLearnedAbilities(Force force, Scenario scenario, AbilityCategory category)
        {
            List<Ability> result = new List<Ability>();
            SangoObjectSet<Ability> abilities = scenario?.CommonData?.Abilities;
            if (abilities == null || force == null)
                return result;

            // 【性能】原 ForEach(lambda) 捕获 category / force / result，改为按下标遍历。
            Ability[] studyAbilityArray = abilities.objects;
            for (int i = 1; i < studyAbilityArray.Length; i++)
            {
                Ability ability = studyAbilityArray[i];
                if (ability == null || ability.category != (int)category)
                    continue;
                if (!force.HasAbility(ability.Id))
                    continue;
                // 学习次数已达上限 —— 对齐 get_learning_limit_count 比较
                if (force.GetAbilityLearnCount(ability.Id) >= ability.LearningLimitCount)
                    continue;
                result.Add(ability);
            }
            return result;
        }

        /// <summary>
        /// 本势力当前"能学点什么"的武将数（玩家侧与 AI 侧共用同一套判定）。
        ///
        /// 用途：AI 排查日志 —— 区分"没人符合条件"与"只是 50% 概率没中"。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>可研习列表非空的空闲武将数</returns>
        public static int CountLearnablePersons(Force force, Scenario scenario)
        {
            if (force == null || scenario == null || scenario.personSet == null)
                return 0;

            int count = 0;
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.BelongForce != force)
                    continue;
                if (!person.IsFree)
                    continue;
                // AI 排查日志：必须按 AI 挑人口径（含"需前线"）统计，
                // 否则会把"AI 因非前线而不能学"误报成"有人能学、只是概率没中"
                if (GetLearnableAbilitiesFor(person, scenario, true).Count > 0)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// AI 培育的硬卡点日志：势力**有已研究、仍可培育的能力**，却一个符合条件的武将都没有时输出。
        /// <para>
        /// 这是"研究完了却始终没人去学"的唯一死锁形态。其余情况（有人能学、只是本回合
        /// 50% 概率没命中）不输出，避免每回合刷屏。调用方负责节流。
        /// </para>
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        public static void LogLearnBlockReason(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return;

            int baseStat = GetLearnedAbilities(force, scenario, AbilityCategory.BaseStat).Count;
            int aptitude = GetLearnedAbilities(force, scenario, AbilityCategory.TroopAptitude).Count;
            int skill = GetLearnedAbilities(force, scenario, AbilityCategory.Skill).Count;
            if (baseStat + aptitude + skill <= 0)
                return;                                  // 没得学，属正常
            if (CountLearnablePersons(force, scenario) > 0)
                return;                                  // 有人能学，只是概率没中

            Sango.Log.Info($"*[能力培育]{force.Name} 无人可培育：可培育能力 基础{baseStat}/适性{aptitude}/特技{skill}，"
                + "但无人满足「档位 56~65 / 66~75 / 76~90 + 非前线仅政治 + 适性需前线且武力≥70 + 未达培育经验上限」");
        }

        /// <summary>按 Id 取能力数据</summary>
        /// <param name="scenario">场景对象</param>
        /// <param name="abilityId">能力 Id</param>
        /// <returns>能力；不存在返回 null</returns>
        public static Ability GetAbility(Scenario scenario, int abilityId)
        {
            return scenario?.CommonData?.Abilities?.Get(abilityId);
        }

        /// <summary>
        /// 档位上限反查区间（沿用原版 StatTable 的固定对应）。
        /// 70 → 56~65 / 80 → 66~75 / 95 → 76~90。
        /// </summary>
        /// <param name="maxStat">档位上限</param>
        /// <param name="min">输出：区间下限</param>
        /// <param name="max">输出：区间上限</param>
        public static void GetTierRange(int maxStat, out int min, out int max)
        {
            switch (maxStat)
            {
                case 70: min = 56; max = 65; break;
                case 80: min = 66; max = 75; break;
                case 95: min = 76; max = 90; break;
                default: min = 0; max = -1; break;
            }
        }

        /// <summary>
        /// 是否落在某个可培育档位内（保留给外部判定用）。
        /// </summary>
        /// <param name="baseValue">属性基础值</param>
        /// <returns>落在 56~65 / 66~75 / 76~90 之一返回 true</returns>
        public static bool IsInBaseStatTier(int baseValue)
        {
            return (baseValue >= 56 && baseValue <= 65)
                || (baseValue >= 66 && baseValue <= 75)
                || (baseValue >= 76 && baseValue <= 90);
        }

        /// <summary>
        /// 取属性基础值所在的档位下标（0 / 1 / 2），不在任何档位返回 -1。
        /// </summary>
        /// <param name="baseValue">属性基础值</param>
        /// <returns>档位下标；不在档位内返回 -1</returns>
        public static int GetBaseStatTier(int baseValue)
        {
            if (baseValue >= 56 && baseValue <= 65) return 0;
            if (baseValue >= 66 && baseValue <= 75) return 1;
            if (baseValue >= 76 && baseValue <= 90) return 2;
            return -1;
        }

        /// <summary>
        /// 势力四维总和的排位（0 = 最强维，3 = 最弱维）。
        /// 对齐出处：force_research_ability（force_cmd.cpp:774-799）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="order">输出：四维按强弱降序的索引（0 统率 / 1 武力 / 2 智力 / 3 政治）</param>
        public static void GetBaseStatPriority(Force force, Scenario scenario, List<int> order)
        {
            if (order == null)
                return;
            order.Clear();

            int[] sum = new int[4];
            if (force != null && scenario != null)
            {
                for (int i = 0; i < scenario.personSet.Count; i++)
                {
                    Person person = scenario.personSet[i];
                    if (person == null || !person.IsAlive || person.BelongForce != force)
                        continue;
                    if (person.state != (int)PersonStateType.Governor
                        && person.state != (int)PersonStateType.Commander
                        && person.state != (int)PersonStateType.Leader
                        && person.state != (int)PersonStateType.Normal)
                        continue;
                    if (person.command != null) sum[0] += person.command.baseValue;
                    if (person.strength != null) sum[1] += person.strength.baseValue;
                    if (person.intelligence != null) sum[2] += person.intelligence.baseValue;
                    if (person.politics != null) sum[3] += person.politics.baseValue;
                }
            }

            bool any = sum[0] > 0 || sum[1] > 0 || sum[2] > 0 || sum[3] > 0;
            if (!any)
            {
                order.Add(0);
                order.Add(1);
                order.Add(2);
                order.Add(3);
                return;
            }

            for (int i = 0; i < sum.Length; i++)
            {
                int index = -1;
                for (int j = 0; j < sum.Length; j++)
                {
                    if (sum[j] < 0)
                        continue;
                    if (index < 0 || sum[index] < sum[j])
                        index = j;
                }
                if (index < 0)
                    break;
                order.Add(index);
                sum[index] = -1;
            }
        }

        /// <summary>
        /// 四维强弱 → 能力打分用的 statRank（对齐 AbilityStatKind 的 5 个维度）。
        /// 越弱的维度给越高分（原版 study 里弱维更容易被选中）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>长度 = AbilityStatKind.Max 的排位数组</returns>
        static int[] BuildStatRank(Force force, Scenario scenario)
        {
            int[] rank = new int[(int)AbilityStatKind.Max];
            List<int> order = new List<int>();
            GetBaseStatPriority(force, scenario, order);

            // order 是"强 → 弱"的索引（0 统率 / 1 武力 / 2 智力 / 3 政治）
            // 转成 AbilityStatKind：0 武力 / 1 统率 / 2 政治 / 3 智力 / 4 魅力
            int[] personStatToAbilityStat = { 1, 0, 3, 2, 4 };
            for (int i = 0; i < order.Count; i++)
            {
                int personStat = order[i];
                if (personStat < 0 || personStat >= personStatToAbilityStat.Length)
                    continue;
                int abilityStat = personStatToAbilityStat[personStat];
                if (abilityStat < 0 || abilityStat >= rank.Length)
                    continue;
                // 越弱（i 越大）分越高
                rank[abilityStat] = order.Count - 1 - i;
            }
            return rank;
        }

        /// <summary>
        /// 按"属性索引"取武将属性对象。0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="personStatType">属性索引</param>
        /// <returns>属性对象；无效返回 null</returns>
        static PersonAttributeValue GetAttribute(Person person, int personStatType)
        {
            if (person == null)
                return null;
            switch (personStatType)
            {
                case 0: return person.command;
                case 1: return person.strength;
                case 2: return person.intelligence;
                case 3: return person.politics;
                case 4: return person.glamour;   // 魅力（Ability.personStatType 文档：0~4 含魅力）
                default: return null;
            }
        }

        /// <summary>
        /// 按兵种取适性对象（<see cref="AbilityType"/>：1 枪 / 2 戟 / 3 弩 / 4 骑兵 / 5 器械 / 6 水军）。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="troopKind">兵种</param>
        /// <returns>适性对象；无效返回 null</returns>
        static PersonAbilityValue GetAptitude(Person person, int troopKind)
        {
            if (person == null)
                return null;
            switch (troopKind)
            {
                case (int)AbilityType.Spear: return person.spearLv;
                case (int)AbilityType.Halberd: return person.halberdLv;
                case (int)AbilityType.Crossbow: return person.crossbowLv;
                case (int)AbilityType.Ride: return person.rideLv;
                case (int)AbilityType.Machine: return person.machineLv;
                case (int)AbilityType.Water: return person.waterLv;
                default: return null;
            }
        }
    }
}
