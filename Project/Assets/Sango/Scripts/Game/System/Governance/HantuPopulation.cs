using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public static class HantuPopulation
    {
        public static bool Enabled => Scenario.Cur?.Variables?.hantuPopulationEnabled == true;

        public static int UrbanCapacity(City city)
        {
            if (city is Port) return 500000;
            if (city is Gate) return 100000;
            return IsSmallCity(city) ? 500000 : 1500000;
        }

        public static int RuralCapacity(City city)
        {
            if (city is Port) return 2000000;
            if (city is Gate) return 400000;
            return IsSmallCity(city) ? 2000000 : 6000000;
        }

        static bool IsSmallCity(City city)
        {
            return city.BuildingType?.kind == (int)BuildingKindType.LittleCity;
        }

        public static void Prepare(Scenario scenario)
        {
            if (scenario.Variables?.hantuPopulationEnabled != true) return;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                City city = scenario.citySet[i];
                if (city != null) Initialize(city);
            }
        }

        static void Initialize(City city)
        {
            bool hasUrban = city.HasRecordedPopulation;
            bool hasRural = city.ruralPopulation.HasValue;
            if (!hasUrban && !hasRural)
            {
                if (city is Port) { city.population = 16000; city.ruralPopulation = 148000; }
                else if (city is Gate) { city.population = 5000; city.ruralPopulation = 25000; }
                else if (IsSmallCity(city)) { city.population = 30000; city.ruralPopulation = 150000; }
                else { city.population = 92000; city.ruralPopulation = 442000; }
            }
            else
            {
                // 保留已有的城市人口总量和显式零。缺少另一项时从零开始。
                if (!hasUrban) city.population = 0;
                if (!hasRural) city.ruralPopulation = 0;
            }
        }

        public static void SettleSeason(Scenario scenario)
        {
            if (scenario.Variables?.hantuPopulationEnabled != true
                || scenario.hantuPopulationSeason == scenario.SeasonStamp)
                return;
            Prepare(scenario);
            var cities = new List<City>(scenario.citySet.Count);
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                City city = scenario.citySet[i];
                if (city != null && city.IsAlive)
                    cities.Add(city);
            }
            cities.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (City city in cities)
                GrowAndConvert(city);
            foreach (City city in cities)
                MigrateToParent(city);
            scenario.hantuPopulationSeason = scenario.SeasonStamp;
            foreach (City city in cities)
                if (city.BelongCorps != null)
                    city.CalculateHarvest();
        }

        static void GrowAndConvert(City city)
        {
            int safety = city is Gate ? Math.Max(0, (city.BelongCity?.security ?? city.security) - 10) : city.security;
            int politics = Math.Max(0, Math.Min(100, city.Leader?.Politics ?? 0));
            int urbanCap = UrbanCapacity(city), ruralCap = RuralCapacity(city);
            int urban = Math.Max(0, city.population), rural = Math.Max(0, city.ruralPopulation.GetValueOrDefault());
            int commerce = city is Gate ? (city is Port ? (city.BelongCity?.commerce ?? 0) / 5 : 0) : city.commerce;
            int agriculture = city is Gate ? (city is Port ? (city.BelongCity?.agriculture ?? 0) / 5 : 0) : city.agriculture;
            bool harvest = city.HasDisaster(City.DisasterType.Housaku);
            urban += HantuPopulationRules.Growth(urban, urbanCap, politics, Math.Max(0, Math.Min(100, commerce)),
                safety, 80, 0.005, harvest ? 1.05 : 1);
            rural += HantuPopulationRules.Growth(rural, ruralCap, politics, Math.Max(0, Math.Min(100, agriculture)),
                safety, 60, 0.005, harvest ? 1.25 : 1);

            // 汉土的转移比例随治安差增大，每季度最多转出来源人口的 1%。
            int difference = Math.Min(20, Math.Abs(safety - 90));
            if (safety < 90)
            {
                int moved = HantuPopulationRules.Transfer(urban, rural, ruralCap, (int)((long)urban * difference / 2000));
                urban -= moved;
                rural += moved;
            }
            else
            {
                int moved = HantuPopulationRules.Transfer(rural, urban, urbanCap, (int)((long)rural * difference / 2000));
                rural -= moved;
                urban += moved;
            }
            city.population = urban;
            city.ruralPopulation = rural;
        }

        static void MigrateToParent(City city)
        {
            City parent = city.BelongCity;
            if (!(city is Gate) || parent == null || !parent.IsAlive
                || city.BelongForce == null || city.BelongForce != parent.BelongForce)
                return;

            int urban = city.population;
            int rural = city.ruralPopulation.GetValueOrDefault();
            int parentRural = parent.ruralPopulation.GetValueOrDefault();
            // 港关人口达到所属城同类人口的一半时，转移本处人口的 1%。
            int urbanMove = (long)urban * 2 >= parent.population
                ? HantuPopulationRules.Transfer(urban, parent.population, UrbanCapacity(parent), urban / 100) : 0;
            int ruralMove = (long)rural * 2 >= parentRural
                ? HantuPopulationRules.Transfer(rural, parentRural, RuralCapacity(parent), rural / 100) : 0;
            city.population -= urbanMove;
            city.ruralPopulation = rural - ruralMove;
            parent.population += urbanMove;
            parent.ruralPopulation = parentRural + ruralMove;
        }

        public static int RecruitLimit(City city)
        {
            return HantuPopulationRules.RecruitLimit(city.population, city.ruralPopulation.GetValueOrDefault(), city.IsPlayer);
        }

        public static void ChargeRecruitment(City city, int troops)
        {
            int amount = HantuPopulationRules.RecruitCost(troops, city.IsPlayer);
            int rural = Math.Max(0, city.ruralPopulation.GetValueOrDefault());
            int ruralCharge = Math.Min(rural, amount);
            city.ruralPopulation = rural - ruralCharge;
            city.population = Math.Max(0, city.population - (amount - ruralCharge));
        }

        public static int GoldTax(City city)
        {
            return Enabled ? (int)(2.5 * Math.Sqrt(Math.Max(0, city.population))) : 0;
        }

        public static int FoodTax(City city)
        {
            return Enabled ? (int)(12 * Math.Sqrt(Math.Max(0, city.ruralPopulation.GetValueOrDefault()))) : 0;
        }

        public static int MarketGold(BuildingBase building, int value)
        {
            return Enabled && building.BuildingType?.kind == (int)BuildingKindType.Market && building.BelongCity != null
                ? (int)((long)value * HantuPopulationRules.FacilityPercent(building.BelongCity.population, 1000) / 100) : value;
        }

        public static int FarmFood(BuildingBase building, int value)
        {
            return Enabled && building.BuildingType?.kind == (int)BuildingKindType.Farm && building.BelongCity != null
                ? (int)((long)value * HantuPopulationRules.FacilityPercent(building.BelongCity.ruralPopulation.GetValueOrDefault(), 4000) / 100) : value;
        }

        public static void LosePopulation(City city, int urbanPercent, int ruralPercent)
        {
            if (!Enabled) return;
            int urban = Math.Max(0, city.population);
            int rural = Math.Max(0, city.ruralPopulation.GetValueOrDefault());
            city.population = urban - (int)((long)urban * urbanPercent / 100);
            city.ruralPopulation = rural - (int)((long)rural * ruralPercent / 100);
        }

        public static void OnDisaster(City city, City.DisasterType type, Scenario scenario)
        {
            if (!Enabled) return;
            // 使用日期与据点的稳定取值，避免额外消耗游戏随机序列。
            uint seed = unchecked((uint)(city.Id * 73856093 ^ scenario.SeasonStamp * 19349663 ^ scenario.Info.month * 83492791));
            if (type == City.DisasterType.Inago)
                LosePopulation(city, (int)(seed % 3), (int)((seed >> 6) % 5));
            else if (type == City.DisasterType.Ekibyou)
                LosePopulation(city, 2 + (int)(seed % 3), 4 + (int)((seed >> 6) % 5));
        }
    }
}
