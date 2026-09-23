using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 外交事件管理器
    /// </summary>
    [GameSystem(order = 101, nickName = "DiplomacyEventManager")]
    public class DiplomacyEventManager : GameSystem
    {
        /// <summary>
        /// 外交事件列表
        /// </summary>
        private List<DiplomacyEvent> _diplomacyEvents;

        /// <summary>
        /// 当前回合的事件触发次数
        /// </summary>
        private int _eventTriggerCount;



        /// <summary>
        /// 当前回合标识符
        /// </summary>
        private int _currentTurn;

        /// <summary>
        /// 初始化外交事件管理器
        /// </summary>
        public override void Init()
        {
            //_diplomacyEvents = new List<DiplomacyEvent>();
            //// 初始化事件计数器
            //_eventTriggerCount = 0;
            //// 初始化默认外交事件
            //InitDefaultEvents();
            //// 注册回合开始事件监听（仅在回合开始时触发）
            //GameEvent.OnTurnStart += OnTurnStart;
        }

        /// <summary>
        /// 初始化默认外交事件
        /// </summary>
        private void InitDefaultEvents()
        {
            // 添加默认外交事件
            _diplomacyEvents.Add(new DiplomacyEvent
            {
                Id = 1,
                Name = "使者来访",
                Description = "对方派遣使者来访，带来了友好的问候。",
                MinRelation = 0,
                MaxRelation = 500,
                Probability = 5,
                Effect = (sender, receiver) =>
                {
                    // 增加关系
                    DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                    diplomacyManager.AddRelation(sender, receiver, 50);
                    // 添加日志记录
                    string message = $"{receiver.ColorName}派遣使者访问{sender.ColorName}，带来了友好的问候，关系增加了50点！";
                    PlayerMessage.AddTextMessage(message, sender, sender.CapitalCity.x, sender.CapitalCity.y);
                    Sango.Log.Info($"@外交事件@{receiver.Name} 的使者访问了 {sender.Name}，关系增加了50点！");
                }
            });

            _diplomacyEvents.Add(new DiplomacyEvent
            {
                Id = 2,
                Name = "边境冲突",
                Description = "双方在边境发生了小规模冲突。",
                MinRelation = -500,
                MaxRelation = 0,
                Probability = 8,
                Effect = (sender, receiver) =>
                {
                    // 减少关系
                    DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                    diplomacyManager.ReduceRelation(sender, receiver, 100);
                    // 添加日志记录
                    string message = $"{sender.ColorName}与{receiver.ColorName}在边境发生了冲突，关系减少了100点！";
                    PlayerMessage.AddTextMessage(message, sender, sender.CapitalCity.x, sender.CapitalCity.y);

                    Sango.Log.Info($"@外交事件@{sender.Name} 与 {receiver.Name} 在边境发生了冲突，关系减少了100点！");
                }
            });

            _diplomacyEvents.Add(new DiplomacyEvent
            {
                Id = 3,
                Name = "贸易提议",
                Description = "对方提出了贸易合作的提议。",
                MinRelation = 300,
                MaxRelation = 800,
                Probability = 10,
                Effect = (sender, receiver) =>
                {
                    // 尝试通商
                    DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                    bool success = diplomacyManager.PerformDiplomacyAction(DiplomacyActionType.Trade, sender, receiver);
                    // 添加日志记录
                    if (success)
                    {
                        string message = $"{receiver.ColorName}向{sender.ColorName}提出了贸易合作的提议，双方达成了通商协议！";
                        PlayerMessage.AddTextMessage(message, sender, sender.CapitalCity.x, sender.CapitalCity.y);

                    }
                }
            });

            _diplomacyEvents.Add(new DiplomacyEvent
            {
                Id = 4,
                Name = "结盟邀请",
                Description = "对方邀请你结成同盟。",
                MinRelation = 800,
                MaxRelation = 1500,
                Probability = 5,
                Effect = (sender, receiver) =>
                {
                    // 尝试结盟
                    DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                    bool success = diplomacyManager.PerformDiplomacyAction(DiplomacyActionType.AllianceRequest, receiver, sender);
                    // 添加日志记录
                    string message = $"{receiver.ColorName}邀请{sender.ColorName}结成同盟！";
                    PlayerMessage.AddTextMessage(message, sender, sender.CapitalCity.x, sender.CapitalCity.y);

                }
            });

            _diplomacyEvents.Add(new DiplomacyEvent
            {
                Id = 5,
                Name = "停战请求",
                Description = "对方请求停战。",
                MinRelation = -800,
                MaxRelation = -300,
                Probability = 7,
                Effect = (sender, receiver) =>
                {
                    // 尝试停战
                    DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                    bool success = diplomacyManager.PerformDiplomacyAction(DiplomacyActionType.TruceRequest, receiver, sender);
                    // 添加日志记录
                    string message = $"{receiver.ColorName}请求与{sender.ColorName}停战！";
                    PlayerMessage.AddTextMessage(message, sender, sender.CapitalCity.x, sender.CapitalCity.y);

                }
            });
        }

        /// <summary>
        /// 回合开始事件处理
        /// </summary>
        /// <param name="scenario">游戏场景</param>
        private void OnTurnStart(Scenario scenario)
        {
            // 回合开始时重置事件计数器
            _eventTriggerCount = 0;

            // 为所有势力检查外交事件
            CheckEventsForAllForces(scenario);
        }

        /// <summary>
        /// 检查并触发外交事件
        /// </summary>
        /// <param name="forceA">势力A</param>
        /// <param name="forceB">势力B</param>
        public void CheckAndTriggerEvents(Force forceA, Force forceB)
        {
            if (forceA == null || forceB == null || forceA == forceB)
                return;

            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>(); ;
            int relation = diplomacyManager.GetRelation(forceA, forceB);

            foreach (DiplomacyEvent e in _diplomacyEvents)
            {
                // 检查关系是否符合条件
                if (relation >= e.MinRelation && relation<= e.MaxRelation)
                {
                    // 检查概率
                    if (GameRandom.Chance(e.Probability))
                    {
                        // 触发事件
                        e.Effect(forceA, forceB);
                        // 增加事件计数器
                        _eventTriggerCount++;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 为所有势力检查外交事件
        /// </summary>
        /// <param name="scenario">游戏场景</param>
        public void CheckEventsForAllForces(Scenario scenario)
        {
            // 检查是否已经达到本回合事件触发上限
            if (_eventTriggerCount >= 3)
                return;
            
            // 遍历所有势力对
            for (int i = 0; i< scenario.forceSet.Count; i++)
            {
                Force forceA = scenario.forceSet[i];
                // 检查forceA是否为null
                if (forceA == null) continue;
                if (!forceA.IsAlive) continue;

                for (int j = i + 1; j < scenario.forceSet.Count; j++)
                {
                    Force forceB = scenario.forceSet[j];
                    // 检查forceB是否为null
                    if (forceB == null) continue;
                    if (!forceB.IsAlive) continue;

                    // 检查并触发事件
                    CheckAndTriggerEvents(forceA, forceB);
                    
                    // 检查是否已经达到本回合事件触发上限
                    if (_eventTriggerCount >= 3)
                        return;
                }
            }
        }
    }

    /// <summary>
    /// 外交事件
    /// </summary>
    public class DiplomacyEvent
    {
        /// <summary>
        /// 事件ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 事件名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 事件描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 最小关系要求
        /// </summary>
        public int MinRelation { get; set; }

        /// <summary>
        /// 最大关系要求
        /// </summary>
        public int MaxRelation { get; set; }

        /// <summary>
        /// 触发概率
        /// </summary>
        public int Probability { get; set; }

        /// <summary>
        /// 事件效果
        /// </summary>
        public System.Action<Force, Force> Effect { get; set; }
    }
}