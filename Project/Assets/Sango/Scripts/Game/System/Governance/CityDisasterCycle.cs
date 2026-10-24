/*
 * 文件名：CityDisasterCycle.cs
 * 描述：城池灾害的月度循环 —— 传播 / 转正 / 自愈 / 丰收。
 *
 * 对齐出处：Open311（D:\open311\s11）
 *   · 传播与阶段判定 —— s11_game_disaster.cpp:68-149  Disaster::denpa（三个子阶段）
 *   · 自愈           —— 同文件 :56-66  Disaster::chinsei
 *   · 疫病参数       —— 同文件 :164-231 Ekibyou（感染率 5/10/15、发生期治安 -10）
 *   · 蝗虫参数       —— 同文件 :233-298 Inago（感染率 10/20、发生期治安 -10）
 *   · 月度调用顺序   —— s11_game_month.cpp:337-375（含丰收的 4 月抽样、7 月转正）
 *
 * 【三个阶段】
 *   ① 传播：邻城有同种灾害 + 未受灾 + 掷骰命中 → 立即感染
 *   ② 转正：季度首月把 `nextDisaster` 升为 `disaster`（即"上月预报的灾害现在发生"）
 *   ③ 感染：开局满 1 年后，季度首月按概率给若干城置 `nextDisaster`（下季度爆发）
 *
 * 【未搬】原版的演出与日志（`hassei_event` / `denpa_event` / `ping`），
 * 本项目只写日志，不做图片 / 音效。
 */

using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 城池灾害的月度循环。
    /// </summary>
    public static class CityDisasterCycle
    {
        /// <summary>一年多少回合（季度首月判定用 36 回合/年 → 3 回合为一个月？不，按月判定）</summary>
        const int MonthsPerYear = 12;

        /// <summary>
        /// 月度灾害结算入口。
        ///
        /// 对齐出处：s11_game_month.cpp:337-375 —— 原版顺序是
        /// <c>ekibyou.denpa()</c> → <c>inago.denpa()</c> → 4 月丰收抽样 → 7 月丰收转正。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public static void OnMonthStart(Scenario scenario)
        {
            if (scenario == null || scenario.citySet == null)
                return;

            int month = scenario.Info != null ? scenario.Info.month : 1;
            bool firstOfQuarter = IsFirstMonthOfQuarter(month);
            int elapsedYears = ElapsedYears(scenario);

            // 自愈：季度首月必然复原，否则按治安掷骰 —— :56-66
            Chinsei(scenario, City.DisasterType.Ekibyou, firstOfQuarter);
            Chinsei(scenario, City.DisasterType.Inago, firstOfQuarter);

            // 传播 / 转正 / 感染 —— :68-149
            Denpa(scenario, City.DisasterType.Ekibyou, month, firstOfQuarter, elapsedYears, 5, 10, 10, 15);
            Denpa(scenario, City.DisasterType.Inago, month, firstOfQuarter, elapsedYears, 10, 20, 10, 10);

            // 丰收：4 月抽样、7 月转正 —— s11_game_month.cpp:339-398
            if (month == 4)
                PlanHousaku(scenario);
            if (month == 7)
                ApplyHousaku(scenario);
        }

        /// <summary>
        /// 是否为季度首月（1 / 4 / 7 / 10）。
        /// 对齐 Open311 `is_first_month_of_quarter()`。
        /// </summary>
        /// <param name="month">月份</param>
        /// <returns>是季度首月返回 true</returns>
        static bool IsFirstMonthOfQuarter(int month)
        {
            return month >= 1 && month <= MonthsPerYear && (month - 1) % 3 == 0;
        }

        /// <summary>
        /// 已过整年数。对齐 Open311 `get_elapsed_years()`（36 回合 = 1 年）。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>年数</returns>
        static int ElapsedYears(Scenario scenario)
        {
            return scenario.TurnCount / 36;
        }

        /// <summary>
        /// 自愈：把已复原的灾害清掉。
        ///
        /// 对齐出处：s11_game_disaster.cpp:56-66 的 <c>chinsei()</c> 与 :169-179 / :238-248
        /// （季度首月必愈，否则按 `rand_bool(max((治安-75)*3, 20))` 掷骰）。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <param name="type">灾害类型</param>
        /// <param name="firstOfQuarter">是否季度首月</param>
        static void Chinsei(Scenario scenario, City.DisasterType type, bool firstOfQuarter)
        {
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || !city.HasDisaster(type))
                    continue;

                // 治安高出 75 的部分每点 3%，最高 20% 下限；季度首月必愈
                int chance = System.Math.Max((city.security - 75) * 3, 20);
                if (!firstOfQuarter && !GameRandom.Chance(chance))
                    continue;

                city.SetDisaster(type, false);
                Sango.Log.Info($"*[灾害]{city.Name} 的{GetDisasterName(type)}已平息");
            }
        }

        /// <summary>
        /// 传播 / 转正 / 感染三段。
        ///
        /// 对齐出处：s11_game_disaster.cpp:68-149 的 <c>Disaster::denpa()</c>。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <param name="type">灾害类型</param>
        /// <param name="month">当前月份</param>
        /// <param name="firstOfQuarter">是否季度首月</param>
        /// <param name="elapsedYears">已过整年数</param>
        /// <param name="m1">1 月感染率</param>
        /// <param name="m4">4 月感染率</param>
        /// <param name="m10">10 月感染率</param>
        /// <param name="m7">7 月感染率</param>
        static void Denpa(Scenario scenario, City.DisasterType type, int month,
            bool firstOfQuarter, int elapsedYears, int m1, int m4, int m10, int m7)
        {
            // ① 传播：邻城有同种灾害 → 本城有概率被传染 —— :74-105
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive)
                    continue;
                if (city.HasDisaster(type))
                    continue;
                if (!IsDisasterNeighbored(city, type, scenario))
                    continue;
                // 政治 / 治安越高越不易被传染 —— :19-22
                int chance = System.Math.Min((105 - city.security) / 2, 20);
                if (!GameRandom.Chance(chance))
                    continue;

                city.SetDisaster(type, true);
                Sango.Log.Info($"*[灾害]{city.Name} 从邻城传染到{GetDisasterName(type)}");
            }

            // ② 转正：季度首月把 next 升为当前 —— :106-125
            if (firstOfQuarter)
            {
                for (int i = 0; i < scenario.citySet.Count; i++)
                {
                    City city = scenario.citySet[i];
                    if (city == null || !city.IsAlive)
                        continue;
                    if (!city.HasDisaster(type, true))
                        continue;

                    city.SetDisaster(type, false, true);
                    if (city.HasDisaster(type))
                        continue;
                    city.SetDisaster(type, true);
                    ApplyDisasterEffect(city, type);
                }
            }

            // ③ 感染：开局满 1 年的季度首月，按概率给若干城置"下季度爆发" —— :126-148
            if (elapsedYears < 1 || !firstOfQuarter)
                return;

            int infectionChance;
            switch (month)
            {
                case 1: infectionChance = m1; break;
                case 4: infectionChance = m4; break;
                case 7: infectionChance = m7; break;
                case 10: infectionChance = m10; break;
                default: infectionChance = 0; break;
            }
            if (infectionChance <= 0 || !GameRandom.Chance(infectionChance))
                return;

            List<City> candidates = new List<City>();
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive)
                    continue;
                if (city.HasDisaster(type) || city.HasDisaster(type, true))
                    continue;
                candidates.Add(city);
            }
            if (candidates.Count < 3)
                return;

            int maxCount = System.Math.Min(candidates.Count, 1 + GameRandom.Range(0, 3));
            for (int i = 0; i < maxCount; i++)
            {
                if (candidates.Count == 0)
                    break;
                int index = GameRandom.Range(0, candidates.Count);
                City city = candidates[index];
                candidates.RemoveAt(index);
                city.SetDisaster(type, true, true);
            }
        }

        /// <summary>
        /// 邻城是否已有同种灾害。对齐 s11_game_disaster.cpp:151-162。
        /// </summary>
        /// <param name="city">本城</param>
        /// <param name="type">灾害类型</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>邻城有该灾害返回 true</returns>
        static bool IsDisasterNeighbored(City city, City.DisasterType type, Scenario scenario)
        {
            if (city.NeighborList == null)
                return false;
            for (int i = 0; i < city.NeighborList.Count; i++)
            {
                City neighbor = city.NeighborList[i] as City;
                if (neighbor == null)
                    continue;
                if (neighbor.HasDisaster(type))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 灾害爆发的即时效果。对齐 Ekibyou/Inago 的 <c>hassei_event</c>（治安 -10）。
        /// </summary>
        /// <param name="city">城池</param>
        /// <param name="type">灾害类型</param>
        static void ApplyDisasterEffect(City city, City.DisasterType type)
        {
            if (city == null)
                return;
            city.AddSecurity(-10);
            Sango.Log.Info($"*[灾害]{city.Name} 爆发{GetDisasterName(type)}（治安 -10）");
        }

        /// <summary>
        /// 4 月：丰收抽样（给若干城置"7 月丰收"）。
        /// 对齐 s11_game_month.cpp:339-375。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        static void PlanHousaku(Scenario scenario)
        {
            if (!GameRandom.Chance(30))
                return;

            List<City> kiganList = new List<City>();   // 有"祈愿"特技的城优先
            List<City> emptyList = new List<City>();
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive)
                    continue;
                if (city.HasDisaster(City.DisasterType.Housaku)
                    || city.HasDisaster(City.DisasterType.Housaku, true))
                    break;
                if (city.HasDisaster(City.DisasterType.Ekibyou)
                    || city.HasDisaster(City.DisasterType.Inago, true))
                    continue;
                emptyList.Add(city);
            }
            if (kiganList.Count + emptyList.Count < 4)
                return;

            int max = 2 + GameRandom.Range(0, 3);
            int n = System.Math.Min(System.Math.Min(3, kiganList.Count), max);
            for (int i = 0; i < n; i++)
            {
                int index = GameRandom.Range(0, kiganList.Count);
                City city = kiganList[index];
                kiganList.RemoveAt(index);
                city.SetDisaster(City.DisasterType.Housaku, true, true);
            }
        }

        /// <summary>
        /// 7 月：丰收转正。
        /// 对齐 s11_game_month.cpp:376-398。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        static void ApplyHousaku(Scenario scenario)
        {
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive)
                    continue;
                if (!city.HasDisaster(City.DisasterType.Housaku, true))
                    continue;
                city.SetDisaster(City.DisasterType.Housaku, false, true);
                city.SetDisaster(City.DisasterType.Housaku, true);
                Sango.Log.Info($"*[灾害]{city.Name} 迎来丰收");
            }
        }

        /// <summary>灾害名（日志用）</summary>
        /// <param name="type">灾害类型</param>
        /// <returns>中文名</returns>
        static string GetDisasterName(City.DisasterType type)
        {
            switch (type)
            {
                case City.DisasterType.Ekibyou: return "疫病";
                case City.DisasterType.Inago: return "蝗灾";
                case City.DisasterType.Housaku: return "丰收";
                default: return "灾害";
            }
        }
    }
}
