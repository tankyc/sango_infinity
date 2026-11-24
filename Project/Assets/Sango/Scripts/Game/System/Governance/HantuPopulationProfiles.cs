using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Sango.Core
{
    internal sealed class HantuPopulationProfiles
    {
        const string DataFile = "Data/HantuPopulationProfiles.json";

        // 名称和坐标与据点编号一起校验，防止编号复用时把人口导入改版地图的其他据点。
        internal sealed class Settlement
        {
            public string name;
            public int? x;
            public int? y;
            public int? buildingType;
            public int? urban;
            public int? rural;
            public int? commerce;
            public int? agriculture;
        }

        public string mapType;
        public int[] scenarioIds;
        public Dictionary<int, Settlement> cities;

        internal static HantuPopulationProfiles LoadFor(ScenarioInfo info)
        {
            if (info == null || info.isSave || string.IsNullOrEmpty(info.mapType)) return null;
            string path = Sango.Path.FindFile(DataFile);
            if (path == null) return null;

            try
            {
                var profiles = JsonConvert.DeserializeObject<HantuPopulationProfiles>(File.ReadAllText(path));
                if (profiles?.mapType != info.mapType || profiles.cities == null
                    || profiles.scenarioIds == null || Array.IndexOf(profiles.scenarioIds, info.id) < 0)
                    return null;
                return profiles;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
            {
                Sango.Log.Warning($"[人口] 人口底表无法读取，使用剧本自带数据或据点默认值：{error.Message}");
                return null;
            }
        }

        internal Settlement FindSettlement(City city)
        {
            if (cities.TryGetValue(city.Id, out Settlement row) && row != null
                && row.name == city.Name && row.x == city.x && row.y == city.y
                && row.buildingType == city.BuildingTypeId
                && row.urban.HasValue && row.urban.Value >= 0
                && row.rural.HasValue && row.rural.Value >= 0)
                return row;
            return null;
        }
    }
}
