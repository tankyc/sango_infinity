using System;
using System.Collections.Generic;
using Newtonsoft.Json;
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

        public SangoObjectList<Province> neighbors = new SangoObjectList<Province>();

        [JsonProperty("neighbors")]
        public int [] neighbors_list;


        public string ColorName => $"<color=#93C86D>{Name}</color>";

        public override void OnScenarioPrepare(Scenario scenario)
        {
            base.OnScenarioPrepare(scenario);
            if(neighbors_list != null && neighbors_list.Length > 0 && neighbors.Count == 0) {
                neighbors.FromArray(neighbors_list);
            }
        }

        /// <summary>
        /// 存档前把运行期列表回写成 int[]。
        /// 缺少这一步的后果：读档后把 id 解析成了对象，运行中还会增删，
        /// 但 int[] 仍是**读档时的旧值** → 存档会把旧数据写回去（丢改动）。
        /// </summary>
        public override void OnScenarioSave(Scenario scenario)
        {
            base.OnScenarioSave(scenario);
            neighbors_list = neighbors != null ? neighbors.ToArray() : null;
        }

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
