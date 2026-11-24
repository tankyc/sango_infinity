using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sango.Core
{
    public enum HantuPopulationChangeSource
    {
        Unknown = 0,
        NaturalGrowth = 1,
        UrbanRuralConversion = 2,
        GateMigration = 3,
        Recruitment = 4,
        Disaster = 5,
        Siege = 6,
        CityCaptured = 7,
        FacilityDestroyed = 8,
        Refugees = 9,
        PopulationEdit = 10,
        Initialization = 11,
        Raid = 12,
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class HantuPopulationChange
    {
        [JsonProperty] public int turn;
        [JsonProperty] public int season;
        [JsonProperty] public string date;
        [JsonProperty] public HantuPopulationChangeSource source;
        [JsonProperty] public long urbanDelta;
        [JsonProperty] public long ruralDelta;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class HantuPopulationCityChanges
    {
        [JsonProperty] public int cityId;
        [JsonProperty] public List<HantuPopulationChange> changes = new List<HantuPopulationChange>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class HantuPopulationPoint
    {
        [JsonProperty] public int cityId;
        [JsonProperty] public int urban;
        [JsonProperty] public int rural;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class HantuPopulationQuarter
    {
        [JsonProperty] public int season;
        [JsonProperty] public string date;
        [JsonProperty] public List<HantuPopulationPoint> points = new List<HantuPopulationPoint>();
    }

    /// <summary>仅保存实际发生的人口变化与季度快照，不回推或补造旧档历史。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class HantuPopulationHistory
    {
        public const int QuarterLimit = 24;
        public const int ChangesPerCityLimit = 24;
        static readonly HantuPopulationChange[] EmptyChanges = new HantuPopulationChange[0];

        [JsonProperty] public List<HantuPopulationQuarter> quarters = new List<HantuPopulationQuarter>();
        [JsonProperty] public List<HantuPopulationCityChanges> cities = new List<HantuPopulationCityChanges>();
        internal int Revision { get; private set; }

        public void Record(Scenario scenario, City city, HantuPopulationChangeSource source, int urbanDelta, int ruralDelta)
        {
            if (scenario == null || city == null || city.Id <= 0 || (urbanDelta == 0 && ruralDelta == 0))
                return;
            Revision++;
            if (cities == null) cities = new List<HantuPopulationCityChanges>();
            HantuPopulationCityChanges bucket = null;
            for (int i = 0; i < cities.Count; i++)
                if (cities[i] != null && cities[i].cityId == city.Id) { bucket = cities[i]; break; }
            if (bucket == null)
            {
                bucket = new HantuPopulationCityChanges { cityId = city.Id };
                cities.Add(bucket);
            }
            if (bucket.changes == null) bucket.changes = new List<HantuPopulationChange>();
            // 同回合、同来源合并：连续征兵/多次劫掠不会按调用次数撑大存档。
            for (int i = bucket.changes.Count - 1; i >= 0; i--)
            {
                HantuPopulationChange existing = bucket.changes[i];
                if (existing != null && existing.turn == scenario.TurnCount
                    && existing.season == scenario.SeasonStamp && existing.source == source)
                {
                    existing.urbanDelta += urbanDelta;
                    existing.ruralDelta += ruralDelta;
                    return;
                }
            }
            bucket.changes.Add(new HantuPopulationChange
            {
                turn = scenario.TurnCount,
                season = scenario.SeasonStamp,
                date = scenario.GetDateStr(),
                source = source,
                urbanDelta = urbanDelta,
                ruralDelta = ruralDelta,
            });
            if (bucket.changes.Count > ChangesPerCityLimit)
                bucket.changes.RemoveRange(0, bucket.changes.Count - ChangesPerCityLimit);
        }

        public void CaptureQuarter(Scenario scenario)
        {
            if (scenario == null || scenario.citySet == null) return;
            Revision++;
            var quarter = new HantuPopulationQuarter { season = scenario.SeasonStamp, date = scenario.GetDateStr() };
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || city.Id <= 0 || !city.IsAlive) continue;
                quarter.points.Add(new HantuPopulationPoint
                {
                    cityId = city.Id,
                    urban = Math.Max(0, city.population),
                    rural = Math.Max(0, city.ruralPopulation.GetValueOrDefault()),
                });
            }
            if (quarters == null) quarters = new List<HantuPopulationQuarter>();
            for (int i = quarters.Count - 1; i >= 0; i--)
                if (quarters[i] == null || quarters[i].season == quarter.season) quarters.RemoveAt(i);
            quarters.Add(quarter);
            quarters.Sort((a, b) => a.season.CompareTo(b.season));
            if (quarters.Count > QuarterLimit) quarters.RemoveRange(0, quarters.Count - QuarterLimit);
        }

        public IReadOnlyList<HantuPopulationChange> GetRecentChanges(int cityId)
        {
            if (cities != null)
                for (int i = 0; i < cities.Count; i++)
                    if (cities[i] != null && cities[i].cityId == cityId && cities[i].changes != null)
                        return cities[i].changes;
            return EmptyChanges;
        }

        /// <param name="cityId">0 表示全图。</param>
        public static bool TryGetPopulation(HantuPopulationQuarter quarter, int cityId, out long urban, out long rural)
        {
            urban = rural = 0;
            if (quarter == null || quarter.points == null) return false;
            bool found = false;
            for (int i = 0; i < quarter.points.Count; i++)
            {
                HantuPopulationPoint point = quarter.points[i];
                if (point == null || (cityId != 0 && point.cityId != cityId)) continue;
                urban += point.urban;
                rural += point.rural;
                found = true;
            }
            return found;
        }

        public static string SourceName(HantuPopulationChangeSource source)
        {
            switch (source)
            {
                case HantuPopulationChangeSource.NaturalGrowth: return "自然增长";
                case HantuPopulationChangeSource.UrbanRuralConversion: return "城乡转化";
                case HantuPopulationChangeSource.GateMigration: return "港关迁徙";
                case HantuPopulationChangeSource.Recruitment: return "征兵";
                case HantuPopulationChangeSource.Disaster: return "灾害";
                case HantuPopulationChangeSource.Siege: return "围城损失";
                case HantuPopulationChangeSource.CityCaptured: return "据点陷落";
                case HantuPopulationChangeSource.FacilityDestroyed: return "设施被毁";
                case HantuPopulationChangeSource.Refugees: return "流民归附";
                case HantuPopulationChangeSource.PopulationEdit: return "人口调整";
                case HantuPopulationChangeSource.Initialization: return "初始人口";
                case HantuPopulationChangeSource.Raid: return "人口劫掠";
                default: return "其他变化";
            }
        }
    }
}
