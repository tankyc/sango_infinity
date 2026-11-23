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

        internal static void Refresh(Scenario scenario)
        {
            Prepare(scenario);
            for (int i = 1; i < scenario.citySet.Count; ++i)
            {
                City city = scenario.citySet[i];
                if (city != null && city.IsAlive && city.BelongCorps != null)
                    city.CalculateHarvest();
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
            if (scenario.Variables?.hantuPopulationEnabled != true) return;
            int season = scenario.SeasonStamp;
            if (scenario.hantuPopulationSeason == season) return;
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
                GrowAndConvert(city, season);
            foreach (City city in cities)
                MigrateToParent(city, season);
            foreach (City city in cities)
                HantuPopulationIncidents.SettleRefugees(scenario, city);
            scenario.hantuPopulationSeason = season;
            foreach (City city in cities)
                if (city.BelongCorps != null)
                    city.CalculateHarvest();
        }

        static void GrowAndConvert(City city, int season)
        {
            City parent = city is Gate ? city.BelongCity : city;
            int safety = EffectiveSecurity(city, parent);
            int politics = Math.Max(0, Math.Min(100, city.Leader?.Politics ?? 0));
            int urbanCap = UrbanCapacity(city), ruralCap = RuralCapacity(city);
            int ruralGrowthCap = city is Gate || IsSmallCity(city) ? ruralCap : 7500000;
            int urban = Math.Max(0, city.population), rural = Math.Max(0, city.ruralPopulation.GetValueOrDefault());
            int commerce = Math.Max(0, Math.Min(100, parent?.commerce ?? 0));
            int agriculture = Math.Max(0, Math.Min(100, parent?.agriculture ?? 0));
            int developmentPercent = city is Port ? 20 : city is Gate ? 0 : 100;
            bool bigCity = city.IsCity() && city.specialty == 6;
            bool harvest = city.HasDisaster(City.DisasterType.Housaku);
            urban += HantuPopulationRules.Growth(urban, urbanCap, urbanCap, politics,
                HantuPopulationRules.Percent(commerce, developmentPercent), safety, 80, harvest ? 5 : 0, bigCity);
            rural += HantuPopulationRules.Growth(rural, ruralCap, ruralGrowthCap, politics,
                HantuPopulationRules.Percent(agriculture, developmentPercent), safety, 60, harvest ? 25 : 0, bigCity);

            int rollCount = HantuPopulationRules.ConversionRollCount(safety);
            if (HantuPopulationRules.CanConvertUrban(safety))
            {
                int roll = HantuPopulationRules.QuarterRoll(season, city.Id, 1, rollCount);
                int moved = HantuPopulationRules.Transfer(urban, rural, ruralCap, HantuPopulationRules.Percent(urban, roll));
                urban -= moved;
                rural += moved;
            }
            // 治安等于门限时两条分支均成立，按先出城再入城的模板顺序执行。
            if (HantuPopulationRules.CanConvertRural(safety))
            {
                int roll = HantuPopulationRules.QuarterRoll(season, city.Id, 2, rollCount);
                int requested = HantuPopulationRules.RuralConversion(rural, roll, parent?.rate ?? city.rate);
                int moved = HantuPopulationRules.Transfer(rural, urban, urbanCap, requested);
                rural -= moved;
                urban += moved;
            }
            city.population = urban;
            city.ruralPopulation = rural;
        }

        static int EffectiveSecurity(City city, City parent)
        {
            // 原作的练兵设施按满治安参与人口结算。港关查询其所属城。
            if (parent != null)
            {
                foreach (Cell cell in parent.interiorCellList)
                {
                    if (cell?.building is Building facility && facility.IsAlive && facility.isComplate
                        && facility.BuildingType?.kind == (int)InteriorFacilityKind.TrainingSchool)
                        return 100;
                }
            }
            int localSecurity = (parent?.security ?? city.security) - (city is Gate ? 10 : 0);
            return Math.Max(0, Math.Min(100, localSecurity));
        }

        static void MigrateToParent(City city, int season)
        {
            City parent = city.BelongCity;
            if (!(city is Gate) || parent == null || parent is Gate || !parent.IsAlive
                || city.BelongForce == null || city.BelongForce != parent.BelongForce)
                return;

            int urban = city.population;
            int rural = city.ruralPopulation.GetValueOrDefault();
            int parentRural = parent.ruralPopulation.GetValueOrDefault();
            int urbanRoll = HantuPopulationRules.QuarterRoll(season, city.Id, 3, HantuPopulationRules.MigrationRollCount);
            int ruralRoll = HantuPopulationRules.QuarterRoll(season, city.Id, 4, HantuPopulationRules.MigrationRollCount);
            int urbanMove = HantuPopulationRules.Migration(urban, parent.population, UrbanCapacity(parent), urbanRoll);
            int ruralMove = HantuPopulationRules.Migration(rural, parentRural, RuralCapacity(parent), ruralRoll);
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
            if (amount == 0) return;
            int rural = Math.Max(0, city.ruralPopulation.GetValueOrDefault());
            int urban = Math.Max(0, city.population);
            int ruralCharge = Math.Min(rural, HantuPopulationRules.RuralRecruitCost(amount, city.rate));
            int urbanCharge = Math.Min(urban, amount - ruralCharge);
            // 城市不足时由剩余农村人口补差，保证成功征兵按实际兵数足额扣除。
            city.population = urban - urbanCharge;
            city.ruralPopulation = rural - Math.Min(rural, amount - urbanCharge);
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
