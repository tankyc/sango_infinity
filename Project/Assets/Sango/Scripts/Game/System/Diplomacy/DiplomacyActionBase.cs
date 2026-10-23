using System;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 外交行为基类
    /// </summary>
    public abstract class DiplomacyActionBase
    {
        /// <summary>
        /// 外交行动类型
        /// </summary>
        public DiplomacyActionType ActionType { get; protected set; }

        /// <summary>
        /// 发送方势力
        /// </summary>
        public Force Sender { get; protected set; }

        /// <summary>
        /// 接收方势力
        /// </summary>
        public Force Receiver { get; protected set; }

        /// <summary>
        /// 执行外交的武将
        /// </summary>
        public Person Diplomat { get; set; }

        /// <summary>
        /// 外交参数（如金钱、兵力等）
        /// </summary>
        public int ResourceValue { get; protected set; }

        /// <summary>
        /// 外交成功率
        /// </summary>
        public int SuccessRate { get; protected set; }

        /// <summary>
        /// 最近一次判定结果（各 Action 的 CalculateSuccessRate 填写）。
        ///
        /// 【用途】原版 `try_*` 返回三态，其中 `Debate` 表示"该进舌战，而不是直接判失败"。
        /// 本项目 Action 对外仍暴露 0~100 成功率（兼容旧 UI / AI），中间结果靠本字段带出来，
        /// 由 <see cref="Perform"/> 决定是否把这次交涉转交舌战。
        /// </summary>
        public DiplomacyRules.Result LastResult { get; protected set; }

        /// <summary>
        /// 初始化外交行为
        /// </summary>
        /// <param name="actionType">外交行动类型</param>
        /// <param name="sender">发送方势力</param>
        /// <param name="receiver">接收方势力</param>
        /// <param name="diplomat">执行外交的武将</param>
        /// <param name="resourceValue">资源价值</param>
        public DiplomacyActionBase(DiplomacyActionType actionType, Force sender, Force receiver, Person diplomat = null, int resourceValue = 0)
        {
            ActionType = actionType;
            Sender = sender;
            Receiver = receiver;
            Diplomat = diplomat;
            ResourceValue = resourceValue;
        }

        /// <summary>
        /// 检查是否可以执行此外交行为
        /// </summary>
        /// <returns>是否可以执行</returns>
        public abstract bool CanPerform();

        /// <summary>
        /// 计算外交成功率
        /// </summary>
        /// <returns>成功率（0-100）</returns>
        public abstract int CalculateSuccessRate();

        /// <summary>
        /// 执行外交行为（会先检查条件）
        /// </summary>
        /// <returns>是否成功</returns>
        public abstract bool Perform();

        /// <summary>
        /// 不检查条件直接执行外交行为
        /// </summary>
        /// <param name="success">执行结果（true表示成功，false表示失败）</param>
        public abstract void PerformWithoutCheck(bool success);

        /// <summary>
        /// 派遣时触发的回调方法
        /// 由 DiplomacyManager.DispatchDiplomat 调用，用于对不同外交事件做不同处理
        /// </summary>
        public virtual void OnDispatch()
        {
            // 默认实现：可以被子类重写以实现不同外交事件的特殊处理
            if (Sender != null)
        {
                Sango.Log.Info($"@外交@{Sender.Name} 派遣 {Diplomat?.Name ?? "使者"} 执行{GetActionName()}");
        }

            // 触发派遣事件
            OnDiplomacyDispatch?.Invoke(this);
        }

        /// <summary>
        /// 派遣事件
        /// </summary>
        public static event Action<DiplomacyActionBase> OnDiplomacyDispatch;

        /// <summary>
        /// 外交行为失败时的惩罚机制
        /// </summary>
        public virtual void OnFailed()
        {
            // 如果没有发送方或接收方，直接返回
            if (Sender == null || Receiver == null)
                return;

            // 获取当前关系值
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            int currentRelation = diplomacyManager.GetRelation(Sender, Receiver);

            // 根据外交行动类型和当前关系计算惩罚值
            int penalty = CalculateFailedPenalty(currentRelation);

            // 减少关系值
            diplomacyManager.ReduceRelation(Sender, Receiver, penalty);

            Sango.Log.Info($"@外交@{Sender.Name} 对 {Receiver.Name} 的{GetActionName()}行动失败，关系减少了 {penalty} 点");

            // 触发失败事件
            OnDiplomacyFailed?.Invoke(this, penalty);
        }

        /// <summary>
        /// 判定这次交涉是否应当转交舌战（原版 `try_*` 返回 `Debate` 的语义）。
        ///
        /// 各 Action 的 <see cref="Perform"/> 在算出判定结果后调用本方法；
        /// 返回 true 表示舌战已接管，调用方**直接 return**，不要落地失败结算。
        /// </summary>
        protected bool TryHandOverToDebate()
        {
            if (LastResult != DiplomacyRules.Result.Debate)
                return false;

            return Sango.Core.Debate.DebateTrigger.ForceDiplomacyDebate(this);
        }

        /// <summary>
        /// 只广播失败事件，**不扣旧的关系值**。
        ///
        /// 【为什么单独开一个入口】基类 <see cref="OnFailed"/> 会按 0~10000 的关系尺度扣值，
        /// 那是外交重构前的口径；重构后各 Action 已按原版公式自带惩罚（劝降 -30 like、
        /// 援军 -25 技术点等），再走 OnFailed 会重复扣。
        /// 但失败事件本身必须发：<see cref="Sango.Core.Debate.DebateTrigger"/> 订阅
        /// <see cref="OnDiplomacyFailed"/> 来实现"外交失败 → 按概率进舌战"，
        /// 不发这个事件，舌战永远接不上。
        /// </summary>
        /// <param name="penalty">本次失败造成的损失（仅用于事件监听方展示，本方法不据此扣值）</param>
        protected void RaiseFailedEvent(int penalty)
        {
            OnDiplomacyFailed?.Invoke(this, penalty);
        }

        /// <summary>
        /// 计算失败惩罚值
        /// </summary>
        /// <param name="currentRelation">当前关系值</param>
        /// <returns>惩罚值</returns>
        protected virtual int CalculateFailedPenalty(int currentRelation)
        {
            // 基础惩罚值
            int basePenalty = Scenario.Cur.Variables.diplomacyFailedBasePenalty;

            // 关系越差，惩罚越小（已经敌对了，再差也差不到哪去）
            if (currentRelation < 0)
            {
                float factor = 1.0f - ((float)Math.Abs(currentRelation) / 2000.0f);
                basePenalty = (int)(basePenalty * factor);
            }
            // 关系越好，惩罚越大（背叛感更强）
            else if (currentRelation > 500)
            {
                float factor = 1.0f + ((float)currentRelation / 2000.0f);
                basePenalty = (int)(basePenalty * factor);
            }

            // 确保惩罚值在合理范围内
            return Math.Max(basePenalty, 1);
        }

        /// <summary>
        /// 外交失败事件
        /// </summary>
        public static event Action<DiplomacyActionBase, int> OnDiplomacyFailed;

        /// <summary>
        /// 获取外交行为名称
        /// </summary>
        /// <returns>行为名称</returns>
        public virtual string GetActionName()
        {
            switch (ActionType)
            {
                case DiplomacyActionType.Alliance:
                    return "结盟";
                case DiplomacyActionType.Truce:
                    return "停战";
                case DiplomacyActionType.DeclareWar:
                    return "宣战";
                case DiplomacyActionType.SendGift:
                    return "送礼";
                case DiplomacyActionType.Annex:
                    return "劝降";
                case DiplomacyActionType.Exchange:
                    return "俘虏交换";
                case DiplomacyActionType.Reinforce:
                    return "请求援军";
                default:
                    return "未知行动";
            }
        }
    }

    /// <summary>
    /// 外交行动类型枚举
    /// </summary>
    public enum DiplomacyActionType
    {
        /// <summary>
        /// 结盟
        /// </summary>
        Alliance = 0,
        
        /// <summary>
        /// 停战
        /// </summary>
        Truce = 1,
        
        /// <summary>
        /// 宣战
        /// </summary>
        DeclareWar = 2,
        
        /// <summary>
        /// 送礼
        /// </summary>
        SendGift = 3,

        // 4~10 号原本是 请求技术 / 请求兵力 / 通商 / 和亲 / 请求结盟 / 请求停战 / 赎回俘虏。
        // 原版（s11_sys_person_order_diplomacy.cpp）只有 7 个使者命令，这些自作命令已删除；
        // 数值刻意保留空缺，避免老存档里已存在的 actionType 数字被重新解释成别的命令。

        /// <summary>
        /// 劝降（原版 Annex / 0x13）
        /// </summary>
        Annex = 11,

        /// <summary>
        /// 俘虏交换（原版 Exchange / 0x14）
        /// </summary>
        Exchange = 12,

        /// <summary>
        /// 请求援军（原版 Reinforce / 0x15）
        /// </summary>
        Reinforce = 13,
    }
}