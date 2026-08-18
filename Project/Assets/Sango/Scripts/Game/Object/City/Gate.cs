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
        public override int BaseGainGold => mBelongCity.BaseGainGold / 5;
        public override int BaseGainFood => mBelongCity.BaseGainFood / 5;

        public override void OnPrepareRender()
        {
            Render = new GateRender(this);
        }

        public override void AIPrepare(Scenario scenario)
        {
            // 准备敌人信息
            PrepareEnemiesInfo(scenario);

            UpdateActiveTroopTypes();
            UpdateFightPower();

            AICommandList.Add(CityAI.AIAttack);
            // 物资输送
            AICommandList.Add(CityAI.AITransfromToBelongCity);
            GameEvent.OnCityAIPrepare?.Invoke(this, scenario);
        }

    }
}
