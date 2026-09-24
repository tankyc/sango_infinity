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
        static readonly HashSet<string> portKindCommandIds = new HashSet<string>
        {
            "AIAttack", "AITransfromToBelongCity",
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
                // 物资输送
                AICommandList.Add(CityAI.AITransfromToBelongCity);
            }

            GameEvent.OnCityAIPrepare?.Invoke(this, scenario);
        }
    }
}
