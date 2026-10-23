using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 停战外交行为
    /// </summary>
    public class DiplomacyActionTruce : DiplomacyActionBase
    {


        /// <summary>
        /// 初始化停战外交行为
        /// </summary>
        /// <param name="sender">发送方势力</param>
        /// <param name="receiver">接收方势力</param>
        /// <param name="diplomat">执行外交的武将</param>
        /// <param name="resourceValue">资源价值</param>
        public DiplomacyActionTruce(Force sender, Force receiver, Person diplomat = null, int resourceValue = 0)
            : base(DiplomacyActionType.Truce, sender, receiver, diplomat, resourceValue)
        {
            // 如果没有指定资源价值，使用默认值
            if (resourceValue == 0)
            {
                ResourceValue = 2000; // 默认停战费用
            }
        }

        /// <summary>
        /// 检查是否可以执行停战行为
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

            // 【原版对齐】原版 can_ceasefire_command（s11_sys_person_order_diplomacy.cpp:987-1020）
            // 只要求"存在未同盟的势力"，**没有友好度死门槛** —— 友好度在公式里体现为
            // `time*4/3 - like/4 + 80` 这条线：关系越差，越难谈成，但永远不是绝对不可能。
            // 旧实现额外加了 relation >= diplomacyTruceRelationThreshold，等于把
            // "关系极差时也可能求和成功"这条原版设计直接掐掉了，故删除。

            // 检查是否已经有停战协议
            if (Sender.HasActiveAgreement(Receiver))
                return false;

            return true;
        }

        /// <summary>
        /// 计算停战成功率
        /// </summary>
        /// <returns>成功率（0-100）</returns>
        public override int CalculateSuccessRate()
        {
            // 【原版对齐】try_ceasefire（s11_sys_person_order_diplomacy.cpp:1229-1310）：
            //   n = √政治*9 - 相性²/90 + rel_coef(10,-15,20) + 金/110，再乘难度系数
            //   n > time*4/3 - like/4 + 80 → 成功；+70 → 舌战
            // 停战期取剧本参数 diplomacyTruceDuration（本项目一回合 = 一旬，单位与原版的"周"不同，
            // 但公式里的 time 只作为难度锚点，语义一致）。
            DiplomacyRules.Result result = DiplomacyRules.TryCeasefire(
                Diplomat,
                Sender != null ? Sender.mGovernor : null,
                Receiver != null ? Receiver.mGovernor : null,
                Sender,
                Receiver,
                Scenario.Cur.Variables.diplomacyTruceDuration,
                ResourceValue,
                Scenario.Cur.Variables.difficulty);

            LastResult = result;
            SuccessRate = DiplomacyRules.ToSuccessRate(result);
            return SuccessRate;
        }

        /// <summary>
        /// 派遣时触发的回调方法
        /// 处理停战的资源消耗
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

                Sango.Log.Info($"@外交@{Sender.Name} 停战花费 {ResourceValue} 金");
            }
        }

        /// <summary>
        /// 执行停战行为
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

            // 原版 try_ceasefire 返回 Debate → 确定性进入舌战（不掷骰、不落地失败）
            if (TryHandOverToDebate()) return false;

            // 判断是否成功
            if (!GameRandom.Chance(SuccessRate))
            {
                // 失败走统一出口：广播事件（舌战可接管）。（原版停战失败不扣友好度）
                PerformWithoutCheck(false);
                return false;
            }

            // 创建停战协议
            Alliance truce = new Alliance()
            {
                ForceList = new SangoObjectList<Force>(),
                leftCount = Scenario.Cur.Variables.diplomacyTruceDuration, // 6个月
                allianceType = AllianceType.Truce, // 停战类型
                IsAlive = true
            };

            truce.ForceList.Add(Sender);
            truce.ForceList.Add(Receiver);

            // 添加到场景
            Scenario.Cur.Add(truce);

            // 添加到势力的联盟列表
            Sender.AllianceList.Add(truce);
            Receiver.AllianceList.Add(truce);

            // 增加关系
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            diplomacyManager.AddRelation(Sender, Receiver, Scenario.Cur.Variables.diplomacyTruceRelationIncrease);

            Sango.Log.Info($"@外交@{Sender.Name} 与 {Receiver.Name} 达成了{Scenario.Cur.Variables.diplomacyTruceDuration / 3}个月的停战协议!!");

            // 触发事件
            GameEvent.OnDiplomacyTruce?.Invoke(Sender, Receiver, true);

            return true;
        }

        /// <summary>
        /// 不检查条件直接执行停战行为
        /// </summary>
        /// <param name="success">执行结果（true表示成功，false表示失败）</param>
        public override void PerformWithoutCheck(bool success)
        {
            if (!success)
            {
                // 【原版失败处理】`:1152-1165`：**不扣友好度**，只做两件事：
                //   ① 广播失败事件（DebateTrigger 订阅它 → 按概率转舌战）；
                //   ② 目标是玩家时，给发起方设 9 周禁谈期。
                // 旧实现走 OnFailed()，它按 0~10000 关系尺度扣值，与重构后的 like 口径冲突，已替换。
                RaiseFailedEvent(0);

                if (Receiver != null && Receiver.IsPlayer)
                    Sender.diplomacyBanTimer = DiplomacyRules.DiplomacyBanTurns;

                GameEvent.OnDiplomacyTruce?.Invoke(Sender, Receiver, false);
                return;
            }

            // 创建停战协议
            Alliance truce = new Alliance()
            {
                ForceList = new SangoObjectList<Force>(),
                leftCount = Scenario.Cur.Variables.diplomacyTruceDuration, // 6个月
                allianceType = AllianceType.Truce, // 停战类型
                IsAlive = true
            };

            truce.ForceList.Add(Sender);
            truce.ForceList.Add(Receiver);

            // 添加到场景
            Scenario.Cur.Add(truce);

            // 添加到势力的联盟列表
            Sender.AllianceList.Add(truce);
            Receiver.AllianceList.Add(truce);

            // 增加关系
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            diplomacyManager.AddRelation(Sender, Receiver, Scenario.Cur.Variables.diplomacyTruceRelationIncrease);

            Sango.Log.Info($"@外交@{Sender.Name} 与 {Receiver.Name} 达成了{Scenario.Cur.Variables.diplomacyTruceDuration / 3}个月的停战协议!!");

            // 触发事件
            GameEvent.OnDiplomacyTruce?.Invoke(Sender, Receiver, true);
        }
    }
}