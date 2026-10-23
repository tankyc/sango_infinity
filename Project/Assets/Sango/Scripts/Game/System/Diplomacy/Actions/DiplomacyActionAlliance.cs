using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 结盟外交行为
    /// </summary>
    public class DiplomacyActionAlliance : DiplomacyActionBase
    {


        /// <summary>
        /// 初始化结盟外交行为
        /// </summary>
        /// <param name="sender">发送方势力</param>
        /// <param name="receiver">接收方势力</param>
        /// <param name="diplomat">执行外交的武将</param>
        /// <param name="resourceValue">资源价值</param>
        public DiplomacyActionAlliance(Force sender, Force receiver, Person diplomat = null, int resourceValue = 0)
            : base(DiplomacyActionType.Alliance, sender, receiver, diplomat, resourceValue)
        {
            // 如果没有指定资源价值，使用默认值
            if (resourceValue == 0)
            {
                ResourceValue = 3000; // 默认结盟费用
            }
        }

        /// <summary>
        /// 检查是否可以执行结盟行为
        /// </summary>
        /// <returns>是否可以执行</returns>
        public override bool CanPerform()
        {
            if (Sender == null || Receiver == null || Sender == Receiver)
                return false;

            // 检查资金：优先检查执行外交的武将所属城市的资金
            int availableGold = 0;
            if (Diplomat != null && Diplomat.BelongCity != null)
            {
                // 如果有执行外交的武将，检查武将所属城市的资金
                availableGold = Diplomat.BelongCity.gold;
            }
            else if (Sender.CapitalCity != null)
            {
                // 否则检查发送方首都的资金
                availableGold = Sender.CapitalCity.gold;
            }

            if (availableGold < ResourceValue)
                return false;

            // 【原版对齐】原版 can_alliance_command（s11_sys_person_order_diplomacy.cpp:438-473）
            // 的前置只有三条：行动力、金 ≥ 1000、存在未同盟势力。
            // **没有"友好度必须达到某阈值"这道门槛** —— 友好度只进判定公式（(110 - like) * 2），
            // 关系太差时自然算不过线。
            // 旧的 relation >= diplomacyAllianceRelationThreshold 与公式门槛重复，
            // 会让低友好度区间的成功/失败边界变得不可预测，故删除。

            // 检查是否已经有同盟关系
            if (Sender.HasActiveAgreement(Receiver))
                return false;

            // 【原版对齐】`get_high_value_horyo`（`:150`、`:183`）：对方手里握着我方俘虏时，
            // 同盟谈判直接失败 —— 自己人都没要回来，对方不可能先跟你结盟。
            // 原版按"功绩最高"筛选，这里简化为"只要关押着我方俘虏就否决"（偏严格）。
            if (HasOwnCaptiveHeldBy(Receiver, Sender))
                return false;

            return true;
        }

        /// <summary>
        /// 对方是否关押着发起方的俘虏（对应原版 `get_high_value_horyo`）。
        /// </summary>
        static bool HasOwnCaptiveHeldBy(Force holder, Force owner)
        {
            if (holder == null || owner == null || holder.BeCaptiveList == null)
                return false;

            bool found = false;
            holder.BeCaptiveList.ForEach(p =>
            {
                if (!found && p != null && p.IsAlive && p.BelongForce == owner)
                    found = true;
            });

            return found;
        }

        /// <summary>
        /// 计算结盟成功率
        /// </summary>
        /// <returns>成功率（0-100）</returns>
        public override int CalculateSuccessRate()
        {
            // 【原版对齐】try_alliance（s11_sys_person_order_diplomacy.cpp:709-738）：
            //   n = 政治 - 相性/5 + 金/150 + rel_coef(1,-1,1)*10，再乘难度系数
            //   n > (110 - like) * 2 → 成功；(100 - like) * 2 → 舌战
            // 旧实现用的是 0~10000 关系尺度的自造公式，已整体替换。
            // 前置否决（对方手握我方高价值俘虏 / 目标君主厌恶我方君主且使者无"论客"）
            // 由调用方在 CanPerform 里判，这里只算分。
            DiplomacyRules.Result result = DiplomacyRules.TryAlliance(
                Diplomat,
                Sender != null ? Sender.mGovernor : null,
                Receiver != null ? Receiver.mGovernor : null,
                Sender,
                Receiver,
                ResourceValue,
                Scenario.Cur.Variables.difficulty);

            LastResult = result;
            SuccessRate = DiplomacyRules.ToSuccessRate(result);
            return SuccessRate;
        }

        /// <summary>
        /// 派遣时触发的回调方法
        /// 处理结盟的资源消耗
        /// </summary>
        public override void OnDispatch()
        {
            // 调用基类方法
            base.OnDispatch();

            // 如果不需要资源，直接返回
            if (ResourceValue <= 0)
                return;

            // 确定扣除资金的城市：优先武将所属城市，其次首都
            City paymentCity = null;
            if (Diplomat != null && Diplomat.BelongCity != null)
            {
                paymentCity = Diplomat.BelongCity;
            }
            else if (Sender != null && Sender.CapitalCity != null)
            {
                paymentCity = Sender.CapitalCity;
            }

            // 扣除资金
            if (paymentCity != null && paymentCity.gold >= ResourceValue)
            {
                paymentCity.gold -= ResourceValue;

                Sango.Log.Info($"@外交@{Sender.Name} 结盟花费 {ResourceValue} 金");
            }
        }

        /// <summary>
        /// 执行结盟行为
        /// </summary>
        /// <returns>是否成功</returns>
        public override bool Perform()
        {
            if (!CanPerform())
            {
                // 条件不满足也算失败，触发惩罚
                OnFailed();
                return false;
            }

            // 计算成功率
            CalculateSuccessRate();

            // 原版 try_alliance 返回 Debate → 确定性进入舌战（不掷骰、不落地失败）
            if (TryHandOverToDebate()) return false;

            // 判断是否成功
            if (!GameRandom.Chance(SuccessRate))
            {
                // 失败走统一出口：广播事件（舌战可接管）。（原版同盟失败不扣友好度）
                PerformWithoutCheck(false);
                return false;
            }

            // 创建联盟
            Alliance alliance = new Alliance()
            {
                ForceList = new SangoObjectList<Force>(),
                leftCount = Scenario.Cur.Variables.diplomacyAllianceDuration, // 12个月
                allianceType = AllianceType.Alliance,
                IsAlive = true
            };

            alliance.ForceList.Add(Sender);
            alliance.ForceList.Add(Receiver);

            // 添加到场景
            Scenario.Cur.Add(alliance);

            // 添加到势力的联盟列表
            Sender.AllianceList.Add(alliance);
            Receiver.AllianceList.Add(alliance);

            // 【原版成功奖励】`s11_sys_person_order_diplomacy.cpp:611-632`：
            //   force_add_like(actor, target, +10) + 技术点 +50
            // 旧实现走的是 0~10000 关系尺度的 diplomacyAllianceRelationIncrease，
            // 与 like 口径（0~100）不是一回事，已换掉。
            Sender.AddLike(Receiver, DiplomacyRules.AllianceLikeGain);
            Sender.GainTechniquePoint(DiplomacyRules.AllianceTechGain);

            Sango.Log.Info($"@外交@{Sender.Name} 与 {Receiver.Name} 达成了{Scenario.Cur.Variables.diplomacyAllianceDuration / 3}个月的结盟 Id={alliance.Id}!!");

            // 触发事件
            GameEvent.OnDiplomacyAlliance?.Invoke(Sender, Receiver, true);

            return true;
        }

        /// <summary>
        /// 不检查条件直接执行结盟行为
        /// </summary>
        /// <param name="success">执行结果（true表示成功，false表示失败）</param>
        public override void PerformWithoutCheck(bool success)
        {
            if (!success)
            {
                // 【原版失败处理】`:633-646`：**不扣友好度**，只做两件事：
                //   ① 广播失败事件（DebateTrigger 订阅它 → 按概率转舌战）；
                //   ② 目标是玩家时，给发起方设 9 周禁谈期。
                // 旧实现走 OnFailed()，它按 0~10000 关系尺度扣值，与重构后的 like 口径冲突，已替换。
                RaiseFailedEvent(0);

                if (Receiver != null && Receiver.IsPlayer)
                    Sender.diplomacyBanTimer = DiplomacyRules.DiplomacyBanTurns;

                GameEvent.OnDiplomacyAlliance?.Invoke(Sender, Receiver, false);
                return;
            }

            // 创建联盟
            Alliance alliance = new Alliance()
            {
                ForceList = new SangoObjectList<Force>(),
                leftCount = Scenario.Cur.Variables.diplomacyAllianceDuration, // 12个月
                allianceType = AllianceType.Alliance,
                IsAlive = true
            };

            alliance.ForceList.Add(Sender);
            alliance.ForceList.Add(Receiver);

            // 添加到场景
            Scenario.Cur.Add(alliance);

            // 添加到势力的联盟列表
            Sender.AllianceList.Add(alliance);
            Receiver.AllianceList.Add(alliance);

            // 【原版成功奖励】`s11_sys_person_order_diplomacy.cpp:611-632`：
            //   force_add_like(actor, target, +10) + 技术点 +50
            // 旧实现走的是 0~10000 关系尺度的 diplomacyAllianceRelationIncrease，
            // 与 like 口径（0~100）不是一回事，已换掉。
            Sender.AddLike(Receiver, DiplomacyRules.AllianceLikeGain);
            Sender.GainTechniquePoint(DiplomacyRules.AllianceTechGain);

            Sango.Log.Info($"@外交@{Sender.Name} 与 {Receiver.Name} 达成了{Scenario.Cur.Variables.diplomacyAllianceDuration / 3}个月的结盟 Id={alliance.Id}!!");

            // 触发事件
            GameEvent.OnDiplomacyAlliance?.Invoke(Sender, Receiver, true);
        }
    }
}