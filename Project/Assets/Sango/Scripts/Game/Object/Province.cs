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

        [JsonConverter(typeof(Id2ObjConverter<Region>))]
        [JsonProperty]
        public Region Region { get; set; }

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
