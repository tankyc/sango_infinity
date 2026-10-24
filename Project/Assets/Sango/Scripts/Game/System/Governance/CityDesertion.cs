using Sango.Core;

namespace Sango.Core
{
    /// <summary>
    /// 兵临城下时的逃兵结算。
    ///
    /// 对齐出处：Open311 `Game::desertion`（s11_game_week.cpp:905-925）：
    /// <code>
    /// for (每个据点) {
    ///     if (not alive or force_id &lt; 0) continue;
    ///     int troops = building->get_troops();
    ///     if (troops &lt;= 0 or not building_has_enemy(building)) continue;   // 只有被围才逃
    ///     int n = (int)(sqrt(max(troops, 100) / 100) * 30.f);
    ///     n += rand_int(50);
    ///     building_add_troops(building, -n);
    /// }
    /// </code>
    /// <para>
    /// 【为什么放在回合开始】原版在 `game_week` 里按周结算，本项目一回合 = 一旬（= 原版一周），
    /// 因此挂在 <c>Scenario.OnTurnStart</c>。
    /// </para>
    /// <para>
    /// 【规模感】逃兵量与兵力平方根成正比，而不是固定值：
    /// 1 万兵约 300~349/回合，4 万兵约 600~649/回合，10 万兵约 948~997/回合。
    /// 被围久了城防会自然崩，不需要额外规则。
    /// </para>
    /// </summary>
    public static class CityDesertion
    {
        /// <summary>
        /// 每个回合开始时，对所有"被敌军兵临"的据点结算逃兵。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public static void OnTurnStart(Scenario scenario)
        {
            if (scenario == null || scenario.citySet == null)
                return;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive)
                    continue;
                if (city.BelongForce == null)
                    continue;

                int troops = city.troops;
                if (troops <= 0)
                    continue;
                // 只有兵临城下才会逃 —— 对应 building_has_enemy
                if (!city.HasEnemyNearby())
                    continue;

                // n = sqrt(max(troops, 100) / 100) * 30 + rand(0..49)
                int n = (int)(System.Math.Sqrt(System.Math.Max(troops, 100) / 100.0) * 30);
                n += GameRandom.Range(0, 50);
                if (n <= 0)
                    continue;

                city.AddTroops(-n);

                Sango.Log.Info($"*[逃兵]{city.BelongForce.Name} 的 {city.Name} 因敌军兵临，逃兵 {n}（余 {city.troops}）");
            }
        }
    }
}
