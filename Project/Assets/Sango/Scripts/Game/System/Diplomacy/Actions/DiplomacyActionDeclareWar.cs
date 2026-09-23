namespace Sango.Core
{
    /// <summary>
    /// 宣战外交行为
    /// </summary>
    public class DiplomacyActionDeclareWar : DiplomacyActionBase
    {


        /// <summary>
        /// 初始化宣战外交行为
        /// </summary>
        /// <param name="sender">发送方势力</param>
        /// <param name="receiver">接收方势力</param>
        /// <param name="diplomat">执行外交的武将</param>
        public DiplomacyActionDeclareWar(Force sender, Force receiver, Person diplomat = null)
            : base(DiplomacyActionType.DeclareWar, sender, receiver, diplomat, 0)
        {
        }

        /// <summary>
        /// 检查是否可以执行宣战行为
        /// </summary>
        /// <returns>是否可以执行</returns>
        public override bool CanPerform()
        {
            if (Sender == null || Receiver == null || Sender == Receiver)
                return false;

            // 检查是否已经有协议
            if (Sender.HasActiveAgreement(Receiver))
                return false;

            return true;
        }

        /// <summary>
        /// 计算宣战成功率（宣战总是成功）
        /// </summary>
        /// <returns>成功率（0-100）</returns>
        public override int CalculateSuccessRate()
        {
            SuccessRate = 100; // 宣战总是成功
            return SuccessRate;
        }

        /// <summary>
        /// 执行宣战行为
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

            // 解除联盟
            Alliance alliance = Sender.CheckAlliance(Receiver);
            if (alliance != null)
            {
                alliance.IsAlive = false;
                Sender.AllianceList.Remove(alliance);
                Receiver.AllianceList.Remove(alliance);
            }

            // 减少关系
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            diplomacyManager.ReduceRelation(Sender, Receiver, Scenario.Cur.Variables.diplomacyDeclareWarRelationDecrease);

            Sango.Log.Info($"@外交@{Sender.Name} 向 {Receiver.Name} 宣战!!");

            // 触发事件
            GameEvent.OnDiplomacyDeclareWar?.Invoke(Sender, Receiver, true);

            return true;
        }

        /// <summary>
        /// 不检查条件直接执行宣战行为
        /// </summary>
        /// <param name="success">执行结果（true表示成功，false表示失败）</param>
        public override void PerformWithoutCheck(bool success)
        {
            if (!success)
            {
                // 失败时触发惩罚机制
                OnFailed();
                // 触发失败事件
                GameEvent.OnDiplomacyDeclareWar?.Invoke(Sender, Receiver, false);
                return;
            }

            // 解除联盟
            Alliance alliance = Sender.CheckAlliance(Receiver);
            if (alliance != null)
            {
                alliance.IsAlive = false;
                Sender.AllianceList.Remove(alliance);
                Receiver.AllianceList.Remove(alliance);
            }

            // 减少关系
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            diplomacyManager.ReduceRelation(Sender, Receiver, Scenario.Cur.Variables.diplomacyDeclareWarRelationDecrease);

            Sango.Log.Info($"@外交@{Sender.Name} 向 {Receiver.Name} 宣战!!");

            // 触发事件
            GameEvent.OnDiplomacyDeclareWar?.Invoke(Sender, Receiver, true);
        }
    }
}