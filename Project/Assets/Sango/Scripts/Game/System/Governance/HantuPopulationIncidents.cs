using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public static class HantuPopulationIncidents
    {
        const int HantuOpenFieldCharge = 52163;
        const int CavalryKind = 5;
        const int Weiya = 96;

        public static void OnFacilityDestroyed(Building facility, Troop attacker)
        {
            if (!HantuPopulation.Enabled || facility == null || !facility.isComplate || !facility.IsIntorBuilding()
                || attacker?.BelongForce == null || !attacker.IsAlive)
                return;

            City city = facility.BelongCity;
            Force defender = city?.BelongForce;
            if (defender == null || !city.IsAlive || facility.BelongForce != defender
                || !attacker.BelongForce.IsEnemy(defender) || IsProtected(city))
                return;

            int rural = Math.Max(0, city.ruralPopulation.GetValueOrDefault());
            int percent = HantuPopulationRules.QuarterRoll(Scenario.Cur.TurnCount, facility.Id, 5, 2);
            int lost = (int)((long)rural * percent / 100);
            if (lost == 0) return;
            city.ruralPopulation = rural - lost;
            HantuPopulation.RecordChange(Scenario.Cur, city, HantuPopulationChangeSource.FacilityDestroyed, 0, -lost);
            Sango.Log.Info($"[人口] {city.Name} 的内政设施遭敌军摧毁，农村人口减少 {lost}");
        }

        public static void OnTroopTurnStart(Scenario scenario, Troop troop)
        {
            if (scenario.Variables?.hantuPopulationEnabled != true || !troop.IsAlive || troop.troops <= 0
                || troop.hantuPopulationRaidTurn == scenario.TurnCount || troop.LandTroopType?.isFight != true)
                return;

            Cell cell = troop.cell;
            // 草地、土、主径。建筑格及其他地形不触发。
            int terrain = cell?.TerrainType?.Id ?? -1;
            if ((terrain != 1 && terrain != 2 && terrain != 11) || cell.building != null)
                return;

            City target = cell.BelongCity;
            City home = troop.BelongCity;
            Force raider = troop.BelongForce;
            if (raider == null || target?.BelongForce == null || home == null || !home.IsAlive || !target.IsAlive
                || home.BelongForce != raider || !raider.IsEnemy(target.BelongForce) || !CanRaid(scenario, troop, raider)
                || IsProtected(target))
                return;

            int source = Math.Max(0, target.ruralPopulation.GetValueOrDefault());
            int destination = Math.Max(0, home.ruralPopulation.GetValueOrDefault());
            // 汉土按两个参数取较小值，将人口转移到主将所属据点。双方人数按实际迁入量增减。
            int requested = Math.Min((int)((long)source * 2 / 100), troop.troops / 5);
            int moved = HantuPopulationRules.Transfer(source, destination, HantuPopulation.RuralCapacity(home), requested);
            if (moved == 0) return;
            target.ruralPopulation = source - moved;
            home.ruralPopulation = destination + moved;
            troop.hantuPopulationRaidTurn = scenario.TurnCount;
            HantuPopulation.RecordChange(scenario, target, HantuPopulationChangeSource.Raid, 0, -moved);
            HantuPopulation.RecordChange(scenario, home, HantuPopulationChangeSource.Raid, 0, moved);
            Sango.Log.Info($"[人口] {troop.Name} 从{target.Name}劫掠 {moved} 农村人口至{home.Name}");
        }

        static bool CanRaid(Scenario scenario, Troop troop, Force force)
        {
            if (IsHantuRaidingTechnique(scenario, HantuOpenFieldCharge) && troop.LandTroopType.kind == CavalryKind
                && force.HasTechnique(HantuOpenFieldCharge))
                return true;

            // 独立剧本可以给自身科技设定 hantuPopulationRaid，不依赖汉土科技的编号。
            if (force.Techniques == null) return false;
            for (int i = 0; i < force.Techniques.Count; ++i)
                if (force.Techniques[i]?.hantuPopulationRaid == true)
                    return true;
            return false;
        }

        static bool IsHantuRaidingTechnique(Scenario scenario, int techniqueId)
        {
            return techniqueId == HantuOpenFieldCharge && IsHantuMap(scenario);
        }

        static bool IsHantuMap(Scenario scenario) => scenario?.Info?.mapType == "san11frontier";

        public static string RaidDescription(Scenario scenario, Technique technique)
        {
            if (scenario?.Variables?.hantuPopulationEnabled != true)
                return null;
            if (technique.hantuPopulationRaid)
                return "人口开启时，战斗陆军每回合可在敌方草地、土或主径劫掠农村人口。";
            if (IsHantuRaidingTechnique(scenario, technique.Id))
                return "人口开启时，骑兵每回合可在敌方草地、土或主径劫掠农村人口。";
            return null;
        }

        public static void SettleRefugees(Scenario scenario, City city)
        {
            if (scenario.Variables?.hantuPopulationEnabled != true || city == null || !city.IsAlive
                || city.BelongForce == null || !city.IsCity())
                return;

            ScenarioVariables variables = scenario.Variables;
            bool tribal = city.barbarianTribe > 0;
            int featureId = tribal ? BarbarianConfig.GetGuardFeatureId(city.barbarianTribe) : Weiya;
            if (featureId <= 0 || !HasRefugeeFeature(city, featureId, IsHantuMap(scenario),
                    variables.hantuRefugeeFeatureOffset, variables.hantuWeiyaTreasureFeatureId))
                return;

            // 固定 / 浮动系数走剧本参数，默认值与旧硬编码一致
            int fixedCount = tribal ? variables.hantuRefugeeTribalFixed : variables.hantuRefugeeNormalFixed;
            int variation = tribal ? variables.hantuRefugeeTribalVariation : variables.hantuRefugeeNormalVariation;
            int divisor = variables.hantuRefugeeRollDivisor > 0 ? variables.hantuRefugeeRollDivisor : 6;
            // 原资料确认固定、浮动系数。汉土以百人为单位，按据点、季度定出浮动，读档不重抽。
            int roll = HantuPopulationRules.QuarterRoll(scenario.SeasonStamp, city.Id, divisor, variation + 1) - 1;
            int requested = (fixedCount + roll) * 100;
            int rural = Math.Max(0, city.ruralPopulation.GetValueOrDefault());
            int joined = Math.Max(0, Math.Min(requested, HantuPopulation.RuralCapacity(city) - rural));
            if (joined == 0) return;
            city.ruralPopulation = rural + joined;
            HantuPopulation.RecordChange(scenario, city, HantuPopulationChangeSource.Refugees, 0, joined);
            Sango.Log.Info($"[人口] {city.Name} {(tribal ? "异族内附" : "流民归附")}，农村人口增加 {joined}");
        }

        static bool HasRefugeeFeature(City city, int featureId, bool hantuMap, int featureOffset, int weiyaTreasureId)
        {
            for (int i = 0; i < city.allPersons.Count; ++i)
            {
                Person person = city.allPersons[i];
                if (person?.IsDead != false || !CitySecurity.IsPresentOfficer(person, city)) continue;
                // 汉土普通特技 47092–47096 保留防异族/盗贼语义。宝物版威压为 32025。
                // 两个偏移都走剧本参数，0 = 不启用该来源；同名军功特技仍按配置原有语义处理。
                if (person.HasFeature(featureId)
                    || (hantuMap && featureOffset > 0 && person.HasFeature(featureOffset + featureId))
                    || (hantuMap && featureId == Weiya && weiyaTreasureId > 0 && person.HasFeature(weiyaTreasureId)))
                    return true;
            }
            return false;
        }

        static bool IsProtected(City city)
        {
            List<Feature> activeFeatures = null;
            for (int i = 0; i < city.allPersons.Count; ++i)
            {
                Person person = city.allPersons[i];
                if (person?.IsDead != false || !CitySecurity.IsPresentOfficer(person, city)) continue;
                if (activeFeatures == null) activeFeatures = new List<Feature>(8);
                person.CollectEffectiveFeatures(-1, activeFeatures);
                for (int j = 0; j < activeFeatures.Count; ++j)
                    if (activeFeatures[j].hantuPopulationProtection)
                        return true;
                activeFeatures.Clear();
            }
            return false;
        }
    }
}
