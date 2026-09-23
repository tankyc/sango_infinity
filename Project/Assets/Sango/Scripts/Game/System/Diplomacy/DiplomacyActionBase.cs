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
                case DiplomacyActionType.RequestTechnique:
                    return "请求技术";
                case DiplomacyActionType.RequestTroops:
                    return "请求兵力";
                case DiplomacyActionType.Trade:
                    return "通商";
                case DiplomacyActionType.Marriage:
                    return "和亲";
                case DiplomacyActionType.AllianceRequest:
                    return "请求结盟";
                case DiplomacyActionType.TruceRequest:
                    return "请求停战";
                case DiplomacyActionType.Ransom:
                    return "赎回俘虏";
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
        
        /// <summary>
        /// 请求技术
        /// </summary>
        RequestTechnique = 4,
        
        /// <summary>
        /// 请求兵力
        /// </summary>
        RequestTroops = 5,
        
        /// <summary>
        /// 通商
        /// </summary>
        Trade = 6,
        
        /// <summary>
        /// 和亲
        /// </summary>
        Marriage = 7,
        
        /// <summary>
        /// 请求结盟
        /// </summary>
        AllianceRequest = 8,
        
        /// <summary>
        /// 请求停战
        /// </summary>
        TruceRequest = 9,
        
        /// <summary>
        /// 赎回俘虏
        /// </summary>
        Ransom = 10
    }
}