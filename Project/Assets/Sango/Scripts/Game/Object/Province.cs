using System;
using System.Collections.Generic;
using TKNewtonsoft.Json;
namespace Sango.Core
{
    /// <summary>
    /// 州
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Province : SangoObject
    {

        [JsonProperty]
        public string desc;

        /// <summary>
        /// 所属州的 id（存档数值），读取 Region 时按需解析
        /// </summary>
        [JsonProperty("Region")]
        public int RegionId;

        Region mRegion;
        public Region Region
        {
            get
            {
                if (mRegion == null && RegionId > 0) mRegion = IdRef.Resolve<Region>(RegionId);
                return mRegion;
            }
            set { mRegion = value; RegionId = value != null ? value.Id : 0; }
        }

        [JsonConverter(typeof(SangoObjectListIDConverter<Province>))]
        [JsonProperty]
        public SangoObjectList<Province> neighbors = new SangoObjectList<Province>();

        public string ColorName => $"<color=#93C86D>{Name}</color>";

        public City RandomBelongCity(Scenario scenario)
        {
            List<City> cities = new List<City>();
            scenario.citySet.ForEach((city) =>
            {
                if (city.province == this)
                    cities.Add(city);
            });

            if (cities.Count == 0)
                return null;

            return cities[GameRandom.Range(0, cities.Count)];
        }
    }
}
