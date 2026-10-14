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
        /// 建筑规划偏好：建筑小类（<c>BuildingKindType</c> 的整数值）→ 份额权重乘数
        /// （**百分比，100 = 不修正**）。
        ///
        /// 例：幽州多马 → <c>"buildBias": { "36": 150 }</c>，即该州的厩舍权重 ×1.5。
        /// 未列出的建筑小类按 100（不修正）计。
        ///
        /// 【默认没有】字段默认 null = 该州没有特性，规划器读到 null 就整层跳过修正，
        /// 因此 Provinces.json 与所有既有剧本数据都**不需要任何改动**。
        ///
        /// 【优先级】本州配置**优先于**所属地域（<see cref="Region"/>.buildBias），两者不相乘。
        /// </summary>
        [JsonProperty] public Dictionary<int, int> buildBias;

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
