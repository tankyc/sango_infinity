using Newtonsoft.Json;
using Sango.Render;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Port : Gate
    {
        public override void OnPrepareRender()
        {
            Render = new PortRender(this);
        }

        public override bool OnForceTurnStart(Scenario scenario)
        {
            return base.OnForceTurnStart(scenario);
        }

        /// <summary>港关允许的 AI 命令集合(不做都市内政)</summary>
        /// <remarks>
        /// 原先的 "AITransfromToBelongCity"（港关 → 归属都市）已随城池运输一起移除：
        /// 港关与归属都市现在是同一个资源单元，由 ResourceDispatcher 统一调度
        /// （港关富余优先上交归属都市、都市缺货优先从自家港关调，成本打优惠）。
        /// </remarks>
        static readonly HashSet<string> portKindCommandIds = new HashSet<string>
        {
            "AIAttack", "AITrainTroop","AIRewardPerson", "AISearching","AIRecruitPerson"
        };

        public override void AIPrepare(Scenario scenario)
        {
            // 准备敌人信息
            PrepareEnemiesInfo(scenario);

            UpdateActiveTroopTypes();
            UpdateFightPower();

            if (AIConfig.Instance.useDynamicCityOrder)
            {
                // 【动态排序】港关按类型过滤,只保留军事 + 向所属城运输
                AICommandList.AddRange(CityAIOrderPlanner.Plan(this, scenario, portKindCommandIds));
            }
            else
            {
                AICommandList.Add(CityAI.AIAttack);
                // 向归属都市运输已移交资源调度（ResourceDispatcher，势力级）
                AICommandList.Add(CityAI.AISearching);
                AICommandList.Add(CityAI.AITrainTroop);
                AICommandList.Add(CityAI.AIRewardPerson);
                AICommandList.Add(CityAI.AIRecruitPerson);
            }

            GameEvent.OnCityAIPrepare?.Invoke(this, scenario);
        }
    }
}
