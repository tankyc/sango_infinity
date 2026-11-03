using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 劝降（Annex）—— 对齐 Open311 的 `try_annex`（`s11_sys_person_order_diplomacy.cpp:1335-1494`）。
    ///
    /// 【流程】
    ///   1. <see cref="CanPerform"/> 跑原版的硬条件链（势力数 / 开局年数 / 城数比 / 兵力比 /
    ///      目标城数 / 相邻 / 君主厌恶 / 宁死不降名单）；任一不满足直接不可点，**不消耗资源**；
    ///   2. <see cref="CalculateSuccessRate"/> 跑评分段（`n` vs `性格*7 - like/4 + 100`）；
    ///   3. 成功：**直接吞并**目标势力（`Force.Ruin`），原版就是"劝降成功 = 对方势力灭亡并入我方"；
    ///   4. 失败：友好度 -30，且目标若是玩家则进入禁谈期（本项目目标不可能是玩家，见 CanPerform）。
    ///
    /// 【与原版的差异】
    ///   · 原版前置会剔除玩家势力（`force->get_player_id() != -1`），这里同样拒绝；
    ///   · 原版的性格枚举本项目没有，改由性格数据的 `annexResistance` 给出 0~3 档位
    ///     （见 DiplomacyRules.TryAnnex 注释）。
    /// </summary>
    public class DiplomacyActionAnnex : DiplomacyActionBase
    {
        /// <summary>最近一次不可执行的原因（给 UI 提示用）</summary>
        public string FailReason { get; private set; }

        public DiplomacyActionAnnex(Force sender, Force receiver, Person diplomat = null, int resourceValue = 0)
            : base(DiplomacyActionType.Annex, sender, receiver, diplomat, resourceValue)
        {
            // 劝降也要备一份"礼物"，原版金在公式里只是弱项，这里保留默认值便于 UI 显示
            if (resourceValue == 0)
                ResourceValue = 1000;
        }

        /// <summary>
        /// 检查是否可以执行劝降。
        /// 条件全部来自原版 `try_annex` 的硬判定链，任一不满足即不可点。
        /// </summary>
        public override bool CanPerform()
        {
            FailReason = null;

            if (Sender == null || Receiver == null || Sender == Receiver)
                return false;

            // 原版：玩家势力不能作为劝降目标（can_annex_command 的 O_MCD_DIPLOMACY_ANNEX_ALLPLAYER）
            if (Receiver.IsPlayer)
            {
                FailReason = "不能劝降玩家势力";
                return false;
            }

            if (Receiver.IsAlliance(Sender))
            {
                FailReason = "对方已与我方同盟，无需劝降";
                return false;
            }

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return false;

            // 场上存活势力数（原版 get_normal_force_list().size()）
            int aliveForceCount = 0;
            if (scenario.forceSet != null)
            {
                for (int i = 0; i < scenario.forceSet.Count; i++)
                {
                    Force f = scenario.forceSet[i];
                    if (f != null && f.IsAlive)
                        aliveForceCount++;
                }
            }

            // 已进行的年数（原版 get_elapsed_years()）
            int elapsedYears = 0;
            if (scenario.Info != null)
            {
                int startYear = scenario.Info.startYear > 0 ? scenario.Info.startYear : scenario.Info.year;
                elapsedYears = scenario.Info.year - startYear;
            }

            bool neighbored = Sender.NeighborForceList != null && Sender.NeighborForceList.Contains(Receiver);
            bool hates = Receiver.mGovernor != null && Sender.mGovernor != null
                         && Receiver.mGovernor.IsHate(Sender.mGovernor);

            string reason;
            bool ok = DiplomacyRules.CanAnnex(
                aliveForceCount,
                elapsedYears,
                Sender.CityCount,
                Receiver.CityCount,
                Sender.FightPower,
                Receiver.FightPower,
                neighbored,
                hates,
                Receiver.mGovernor != null ? Receiver.mGovernor.Name : null,
                out reason);

            if (!ok)
            {
                FailReason = reason;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 劝降判定（原版 `try_annex` 评分段）。
        ///
        /// 【三项个人项必须用使者】原版 `:1600-1606`：
        /// <code>
        /// n -= tgt_kunshu->get_aishou_distance(self->actor->get_id()) / 7;
        /// n += get_relation_coef_int(self, 10, 0, 20);
        /// n += self->actor->get_stat(PersonStatType_Charisma);
        /// </code>
        /// 旧实现传的是 <c>Sender.mGovernor</c>，等于"派谁去结果都一样"，
        /// 玩家自然觉得"随便派个人成功率也很高"。
        /// </summary>
        public override int CalculateSuccessRate()
        {
            ScenarioVariables variables = Scenario.Cur.Variables;

            // 原版 try_annex 的"目标性格"是 0~3 的连续档位，不是性格表主键。
            // 本项目读性格上的 annexResistance 配置值（胆小 0 / 冷静 1 / 刚胆 2 / 莽撞 3），
            // 不能传 PersonalityId（那是 1 起的表主键，会把 g 整体抬高 7）。
            int personality = Receiver != null && Receiver.mGovernor != null
                            && Receiver.mGovernor.mPersonality != null
                ? Receiver.mGovernor.mPersonality.annexResistance
                : 0;

            // 没有使者就没有"人"可派：势力层面可谈（CanPerform）不代表能出发。
            Person actor = Diplomat;
            if (actor == null)
            {
                LastResult = DiplomacyRules.Result.Failure;
                SuccessRate = 0;
                return 0;
            }

            DiplomacyRules.Result result = DiplomacyRules.TryAnnex(
                actor,
                Receiver != null ? Receiver.mGovernor : null,
                Sender,
                Receiver,
                Sender != null ? Sender.CityCount : 0,
                Receiver != null ? Receiver.CityCount : 0,
                personality,
                variables.difficulty,
                IsProtectingEmperor(Sender));

            LastResult = result;

            // 【显示口径必须等于落地口径】Perform 不再二次掷骰，所以：
            //   Success / SuccessNarrow → 必成（100）
            //   Debate                  → 进舌战（50，胜负另算）
            //   Failure                 → 必败（0）
            // 旧实现走 ToSuccessRate（10 / 50 / 75 / 100），而 Perform 又按这个百分比掷一次骰，
            // 结果是"判失败也有 10% 直接吞并、判舌战有 50% 直接吞并"，成功率被系统性地抬高。
            SuccessRate = (result == DiplomacyRules.Result.Success
                        || result == DiplomacyRules.Result.SuccessNarrow) ? 100
                        : result == DiplomacyRules.Result.Debate ? 50 : 0;
            return SuccessRate;
        }

        /// <summary>
        /// 执行劝降（判定只做一次，结果直接落地）。
        /// </summary>
        public override bool Perform()
        {
            if (!CanPerform())
            {
                GameEvent.OnDiplomacyAnnex?.Invoke(Sender, Receiver, false);
                return false;
            }

            // 没有使者就不出发：CanPerform 只校验"势力层面能否谈"，不代表有人可派。
            // 若继续往下走，CalculateSuccessRate 会判 Failure 并走失败结算（友好度 -30），
            // 等于玩家什么都没做却被扣关系。
            if (Diplomat == null)
            {
                FailReason = "没有可派遣的使者";
                GameEvent.OnDiplomacyAnnex?.Invoke(Sender, Receiver, false);
                return false;
            }

            // 【只判定一次】TryAnnex 内部已含随机段（rand 0~n-1），把三态映射成百分比
            // 再掷一次骰属于双重随机：Failure 会白拿 10% 成功率，Debate 会白拿 50%。
            CalculateSuccessRate();
            DiplomacyRules.Result result = LastResult;

            // 原版 try_annex 返回 Debate → 确定性进入舌战（不掷骰、不落地失败）
            if (result == DiplomacyRules.Result.Debate && TryHandOverToDebate())
                return false;

            bool success = result == DiplomacyRules.Result.Success
                        || result == DiplomacyRules.Result.SuccessNarrow;

            PerformWithoutCheck(success);
            return success;
        }

        /// <summary>
        /// 势力是否保护皇帝。原版 `Force::is_protecting_the_emperor` 的派生口径：
        /// 皇帝武将所在城的归属势力（本项目沿用 <see cref="Scenario.emperorPersonId"/>，
        /// ≤ 0 表示皇帝已退位/不存在）。对应 `try_annex` 的 `n += 10`。
        /// </summary>
        static bool IsProtectingEmperor(Force force)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || force == null || scenario.emperorPersonId <= 0 || scenario.personSet == null)
                return false;

            Person emperor = scenario.personSet.Get(scenario.emperorPersonId);
            if (emperor == null || !emperor.IsAlive || emperor.IsDead)
                return false;

            return emperor.BelongCity != null && emperor.BelongCity.BelongForce == force;
        }

        /// <summary>
        /// 不检查条件直接结算（使者抵达后由 DiplomacyManager 回调）。
        /// </summary>
        /// <param name="success">判定结果</param>
        public override void PerformWithoutCheck(bool success)
        {
            if (!success)
            {
                // 原版失败：force_add_like(actor, target, -30)（:1485）
                Sender.AddLike(Receiver, DiplomacyRules.AnnexFailLikePenalty);
                Sango.Log.Info($"@外交@{Sender.Name} 劝降 {Receiver.Name} 失败，友好度 " +
                               $"{DiplomacyRules.AnnexFailLikePenalty}（当前 {Sender.GetLike(Receiver)}）");

                // 广播失败事件：DebateTrigger 订阅它，按概率把这次失败转成舌战
                RaiseFailedEvent(-DiplomacyRules.AnnexFailLikePenalty);
                GameEvent.OnDiplomacyAnnex?.Invoke(Sender, Receiver, false);
                return;
            }

            // 【原版语义】成功 = `force_ruin(target, true, actor)`（:1469-1482），
            // 第二个参数 `true` 是**投降分支**：城池与武将整体并入胜方，
            // 而不是"彻底灭亡"（那个分支会把人全部下野、城池留在原地变成孤儿城）。
            // 本项目对应三步：SurrenderAllBuildings → PersonStateSystem.Recruit → Ruin 收尾。
            //
            // 功绩暂不结算：本项目的功绩入口与使者绑定的口径待 P3 统一后再补。
            //
            // 【技巧点口径】这里对应的是原版「外交命令·劝降」成功：
            //   s11_sys_person_order_diplomacy.cpp:1474 `force_add_tech_point(actor_force, 200, self->actor)`
            //   入口减半 ⇒ 实际 +100。
            // 原版另有一条 s11_game_week.cpp:1150 的 +1000，属于周事件 Game::annexation()
            //（AI 势力互相吞并，受益方是吞并方 AI，与使者无关），不是本命令的第二段奖励。
            // 两条路径不能叠加到同一次劝降里，否则玩家一次劝降会拿到 +300（原版只有 +100）。
            Sender.GainTechniquePoint(DiplomacyRules.AnnexTechGain);

            // 【原版对齐】出使武将本人功绩：
            //   s11_sys_person_order_diplomacy.cpp:1476 `person_add_kouseki(self->actor, 4000)`
            // 给的是**使者**，不是全势力；数值走 GainPlace.AnnexSuccessMerit（剧本设置可调）。
            if (Diplomat != null)
            {
                int merit = GainValueConfig.Merit(GainPlace.AnnexSuccessMerit);
                Diplomat.GainMerit(merit);
                Sango.Log.Info($"@外交@劝降成功，使者 {Diplomat.Name} 功绩 +{merit}");
            }

            City fallCity = Receiver.CapitalCity;

            // ① 城池整体并入我方第一军团
            int movedCities = Receiver.SurrenderAllBuildings(Sender);

            // ② 武将转籍。必须先快照：Recruit 会改 Receiver 的名册，边遍历边改会漏人。
            List<Person> members = new List<Person>();
            Receiver.ForEachPerson(p =>
            {
                if (p != null && p.IsAlive)
                    members.Add(p);
            });

            int movedPersons = 0;
            for (int i = 0; i < members.Count; i++)
            {
                Person p = members[i];

                // 落点：优先他原本的所属城（已随城池移交），否则退到对方旧都，再退到我方首都
                City dest = p.BelongCity != null && p.BelongCity.BelongForce == Sender
                    ? p.BelongCity
                    : fallCity;

                if (dest == null || dest.BelongForce != Sender)
                    dest = Sender.CapitalCity;

                if (dest == null || dest.BelongForce != Sender)
                    continue;

                // 【投降君主先降级】对齐原版 force_ruin 的投降分支（s11_sys_scen_force.cpp:976-982）：
                //     if (person->is_kunshu()) person->set_mibun(Mibun_Totoku);
                //     person_change_force(person, by, new_kunshu, PersonForceChangeReason_Surrendered);
                // Recruit 内部有"在职君主不可被收编"的护栏（防止君主被登庸流程误收编），
                // 所以这里必须先显式降级；reason 用 ForceChanged 表示这是合法的君主身份变更
                // （与继任 Force.Succeed 同一口径），SetState 的君主保护会放行。
                if (p.IsGovernor)
                    PersonStateSystem.SetState(p, PersonStateType.Normal, PersonChangeReason.ForceChanged);

                PersonStateSystem.Recruit(p, dest, PersonChangeReason.Employed);
                movedPersons++;
            }

            Sango.Log.Info($"@外交@{Sender.Name} 劝降 {Receiver.Name} 成功：" +
                           $"接收 {movedCities} 座据点、{movedPersons} 名武将");

            // ③ 投降君主交出玉玺 / 铜雀（原版 force_ruin 末尾的
            // `person_lose_special_items(kunshu, dest_kunshu)`，s11_sys_scen_force.cpp:997-1000；
            // 投降分支 dest_kunshu = 受降方君主，所以是"移交"而不是"销毁"）。
            // 必须在 Ruin 之前做：Ruin 里同一句调用传的是空接收者 = 销毁。
            ItemGrantRules.TransferSpecialItems(Receiver.mGovernor, Sender.mGovernor);

            // ④ 收尾：此时城池与武将都已转走，剩下的只是"标记灭亡 + 广播事件"
            Receiver.Ruin(Sender, fallCity, Sender.mGovernor);

            GameEvent.OnDiplomacyAnnex?.Invoke(Sender, Receiver, true);
        }
    }
}
