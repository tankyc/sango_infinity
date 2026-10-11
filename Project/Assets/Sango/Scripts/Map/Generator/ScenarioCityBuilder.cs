using Sango.Core;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Sango.MapGen
{
    /// <summary>
    /// 剧本城池写入参数。
    /// </summary>
    public class ScenarioCityGenConfig
    {
        /// <summary>模板城市ID；0 表示自动取剧本中第一座城市作为模板(用于拷贝模型/等级/数值等默认值)。</summary>
        public int templateCityId = 0;
        /// <summary>新城市的建筑类型ID(1 通常为都市)。</summary>
        public int buildingTypeId = 1;
        /// <summary>新城市的所属势力ID；0 表示无主。</summary>
        public int defaultForceId = 0;
        /// <summary>是否写入相邻城市关系。</summary>
        public bool linkNeighbors = true;
        /// <summary>已存在的城市是否更新坐标与相邻关系。</summary>
        public bool overwriteExisting = true;
        /// <summary>
        /// 是否把已存在城市的名称也更新为据点的名称。
        /// 使用真实三国城池表时应开启，让剧本里的城名与地图一致。
        /// </summary>
        public bool applyCityNames = false;
        /// <summary>是否删除超出生成范围的旧城市。</summary>
        public bool removeExtraCities = false;
        /// <summary>是否自动导出剧本文件。</summary>
        public bool autoExport = true;
        /// <summary>导出路径；为空时使用剧本自身的 FilePath。</summary>
        public string exportPath = null;
    }

    /// <summary>
    /// 剧本城池写入结果。
    /// </summary>
    public class ScenarioCityResult
    {
        /// <summary>是否成功</summary>
        public bool success;
        /// <summary>提示信息(中文)</summary>
        public string message = "";
        /// <summary>新建的城市数量</summary>
        public int created;
        /// <summary>更新的城市数量</summary>
        public int updated;
        /// <summary>删除的城市数量</summary>
        public int removed;
        /// <summary>使用的模板城市ID；0 表示未使用模板</summary>
        public int templateCityId;
        /// <summary>写入后剧本中的城市总数</summary>
        public int totalCities;
        /// <summary>是否已导出剧本</summary>
        public bool exported;
        /// <summary>实际导出路径</summary>
        public string exportPath;
    }

    /// <summary>
    /// 剧本城池写入器(M5 扩展)。
    ///
    /// 作用：把程序化生成的城市据点真正写进剧本(Scenario.citySet)，
    /// 使其成为可被游戏读取的城池对象。
    ///
    /// 做法：
    ///   1. 从剧本中取一座城市作为"模板"，用 JSON 往返克隆其全部序列化字段
    ///      (模型、建筑类型、城市等级、各类上限与数值)，再覆盖 Id/名称/坐标/相邻关系；
    ///   2. Id 直接采用生成据点的编号，从而与地图地格的 areaId 一一对应；
    ///   3. 同步写入 NeighborList_list(int[]) 与运行期 NeighborList 引用；
    ///   4. 可选地删除超出范围的旧城市，并导出剧本文件。
    /// </summary>
    public static class ScenarioCityBuilder
    {
        /// <summary>
        /// 依据生成上下文写入剧本城市。
        /// </summary>
        /// <param name="scenario">目标剧本</param>
        /// <param name="ctx">生成上下文</param>
        /// <param name="config">写入参数</param>
        /// <returns>写入结果</returns>
        public static ScenarioCityResult Build(Scenario scenario, MapGenContext ctx, ScenarioCityGenConfig config)
        {
            if (ctx == null || ctx.cities == null || ctx.cities.Count == 0)
                return new ScenarioCityResult { message = "写入失败：没有已生成的城市据点，请先执行城市生成" };

            return Build(scenario, ctx.cities, config);
        }

        /// <summary>
        /// 依据城市据点列表写入剧本城市。
        /// </summary>
        /// <param name="scenario">目标剧本</param>
        /// <param name="sites">城市据点列表</param>
        /// <param name="config">写入参数</param>
        /// <returns>写入结果</returns>
        public static ScenarioCityResult Build(Scenario scenario, List<CitySite> sites, ScenarioCityGenConfig config)
        {
            ScenarioCityResult result = new ScenarioCityResult();

            if (scenario == null)
            {
                result.message = "写入失败：当前没有剧本(Scenario 为空)，请在剧本编辑器或游戏内调用";
                Sango.Log.Error(result.message);
                return result;
            }
            if (scenario.citySet == null)
            {
                result.message = "写入失败：剧本没有城市数据(citySet 为空)";
                Sango.Log.Error(result.message);
                return result;
            }
            if (sites == null || sites.Count == 0)
            {
                result.message = "写入失败：没有可写入的城市据点";
                Sango.Log.Error(result.message);
                return result;
            }

            ScenarioCityGenConfig cfg = config ?? new ScenarioCityGenConfig();

            // 1. 取模板城市
            City template = null;
            if (cfg.templateCityId > 0)
                template = scenario.citySet.Get(cfg.templateCityId);
            if (template == null)
            {
                scenario.citySet.ForEach(c =>
                {
                    if (template == null)
                        template = c;
                });
            }

            string templateJson = template != null ? JsonConvert.SerializeObject(template) : null;
            result.templateCityId = template != null ? template.Id : 0;

            // 2. 逐个写入
            HashSet<int> writtenIds = new HashSet<int>();
            for (int i = 0; i < sites.Count; i++)
            {
                CitySite site = sites[i];
                if (site.id <= 0)
                    continue;

                writtenIds.Add(site.id);

                City city = scenario.citySet.Get(site.id);
                if (city != null)
                {
                    if (!cfg.overwriteExisting)
                        continue;

                    city.x = site.x;
                    city.y = site.y;
                    if (cfg.applyCityNames && !string.IsNullOrEmpty(site.name))
                        city.Name = site.name;
                    ApplyNeighbors(city, site, scenario.citySet, cfg);
                    result.updated++;
                    continue;
                }

                // 新建：从模板克隆，保证模型/等级/数值等字段完整
                City created;
                if (!string.IsNullOrEmpty(templateJson))
                {
                    created = new City();
                    JsonConvert.PopulateObject(templateJson, created);
                }
                else
                {
                    created = new City();
                }

                created.Id = site.id;
                created.Name = site.name;
                created.x = site.x;
                created.y = site.y;
                created.rot = 0f;
                created.BelongForceId = cfg.defaultForceId;
                created.BelongCorpsId = 0;

                if (cfg.buildingTypeId > 0)
                    created.BuildingTypeId = cfg.buildingTypeId;

                ApplyNeighbors(created, site, scenario.citySet, cfg);

                // Set 会把对象放到 Id 对应的下标，并自动扩容
                scenario.citySet.Set(created);
                result.created++;
            }

            // 3. 收尾刷新相邻关系
            // 新建过程中，被引用的相邻城市可能尚未创建，因此这里在所有城市就位后再统一刷新一次
            if (cfg.linkNeighbors)
            {
                for (int i = 0; i < sites.Count; i++)
                {
                    CitySite site = sites[i];
                    if (site.id <= 0)
                        continue;
                    City city = scenario.citySet.Get(site.id);
                    if (city != null)
                        ApplyNeighbors(city, site, scenario.citySet, cfg);
                }
            }

            // 4. 删除超出范围的旧城市
            if (cfg.removeExtraCities)
            {
                List<int> removeIds = new List<int>();
                for (int id = 1; id < 100000; id++)
                {
                    City city = scenario.citySet.Get(id);
                    if (city == null)
                        continue;
                    if (!writtenIds.Contains(id))
                        removeIds.Add(id);
                }

                for (int i = 0; i < removeIds.Count; i++)
                {
                    scenario.citySet.Remove(removeIds[i]);
                    result.removed++;
                }
            }

            // 5. 统计
            int total = 0;
            scenario.citySet.ForEach(c => total++);
            result.totalCities = total;

            // 6. 导出
            if (cfg.autoExport)
            {
                string path = !string.IsNullOrEmpty(cfg.exportPath) ? cfg.exportPath : scenario.FilePath;
                if (!string.IsNullOrEmpty(path))
                {
                    scenario.Export(path);
                    result.exported = true;
                    result.exportPath = path;
                }
            }

            result.success = true;
            result.message = string.Format("剧本城池写入完成：新建 {0} 座、更新 {1} 座、删除 {2} 座，当前共 {3} 座{4}",
                result.created, result.updated, result.removed, result.totalCities,
                result.exported ? "，已导出剧本" : "");
            Sango.Log.Info(result.message);
            return result;
        }

        /// <summary>
        /// 写入相邻城市关系(同时写入序列化用的 int[] 与运行期对象列表)。
        /// </summary>
        private static void ApplyNeighbors(City city, CitySite site, SangoObjectSet<City> citySet, ScenarioCityGenConfig cfg)
        {
            if (!cfg.linkNeighbors || site.neighbors == null)
                return;

            int count = site.neighbors.Count;
            int[] ids = new int[count];
            for (int i = 0; i < count; i++)
                ids[i] = site.neighbors[i];

            city.NeighborList_list = ids;

            // 运行期列表直接按 Id 解析，避免依赖全局剧本上下文
            if (city.NeighborList == null)
                city.NeighborList = new SangoObjectList<City>();

            city.NeighborList.Clear();
            for (int i = 0; i < count; i++)
            {
                City neighbor = citySet.Get(ids[i]);
                if (neighbor != null && neighbor != city)
                    city.NeighborList.Add(neighbor);
            }
        }
    }
}
