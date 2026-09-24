using TKNewtonsoft.Json;
using Sango.Render;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Gate : City
    {
        public override int BaseGainGold => BelongCity.BaseGainGold / 5;
        public override int BaseGainFood => BelongCity.BaseGainFood / 5;

        public override void OnPrepareRender()
        {
            Render = new GateRender(this);
        }

        /// <summary>关隘允许的 AI 命令集合(不做都市内政)</summary>
        static readonly HashSet<string> gateKindCommandIds = new HashSet<string>
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
                // 【动态排序】关隘按类型过滤,只保留军事 + 向所属城运输
                AICommandList.AddRange(CityAIOrderPlanner.Plan(this, scenario, gateKindCommandIds));
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
