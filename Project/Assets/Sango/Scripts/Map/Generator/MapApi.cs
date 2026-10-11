using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sango.Core;
using UnityEngine;

namespace Sango.MapGen
{
    /// <summary>
    /// 地图操作 API 层(面向 AI / 脚本)。
    ///
    /// 设计原则：
    ///   1. 所有方法"字符串 JSON 入参、字符串 JSON 出参"，便于通过 Unity MCP 的 execute_code
    ///      或任何外部脚本直接调用，无需了解 C# 类型；
    ///   2. 统一返回格式：{ "success": bool, "message": "中文提示", "data": {...} }；
    ///   3. 提供 Run(command) 单一入口，用 { "op": "...", "args": {...} } 分发到具体方法；
    ///   4. Describe() 返回完整 API 清单，AI 可先调用它自我发现能力。
    ///
    /// 调用示例(通过 Unity MCP execute_code)：
    ///   return Sango.MapGen.MapApi.Run("{\"op\":\"generateWorld\",\"args\":{\"landRatio\":0.6,\"river\":{\"flowThreshold\":12}}}");
    /// </summary>
    public static class MapApi
    {
        // ------------------------------------------------------------------
        // 统一入口
        // ------------------------------------------------------------------

        /// <summary>
        /// 单一入口：根据 { "op": "...", "args": {...} } 分发到具体操作。
        /// </summary>
        /// <param name="json">指令 JSON</param>
        /// <returns>结果 JSON</returns>
        public static string Run(string json)
        {
            JObject root = ParseObject(json);
            if (root == null)
                return Response(false, "指令解析失败：无法解析为 JSON 对象", null);

            string op = root.Value<string>("op");
            if (string.IsNullOrEmpty(op))
                return Response(false, "指令缺少 op 字段", null);

            JToken args = root["args"];
            string argsJson = args != null ? args.ToString(Formatting.None) : "{}";

            switch (op)
            {
                case "describe": return Describe();
                case "mapInfo": return GetMapInfo();
                case "createNewMap": return CreateNewMap(argsJson);
                case "loadMap": return LoadMap(argsJson);
                case "saveMap": return SaveMap(argsJson);
                case "generateTerrain": return GenerateTerrain(argsJson);
                case "generateRiver": return GenerateRiver(argsJson);
                case "generateCities": return GenerateCities(argsJson);
                case "generateRoads": return GenerateRoads(argsJson);
                case "generateBaseMap": return GenerateBaseMap(argsJson);
                case "setupSeasonLayers": return SetupSeasonLayers(argsJson);
                case "generateWorld": return GenerateWorld(argsJson);
                case "listCities": return ListCities();
                case "syncScenarioCities": return SyncScenarioCities();
                case "createScenarioCities": return CreateScenarioCities(argsJson);
                case "applyChinaScenario": return ApplyChinaScenario(argsJson);
                case "paintTerrain": return PaintTerrain(argsJson);
                case "queryCell": return QueryCell(argsJson);
                case "terrainStats": return QueryTerrainStats();
                case "terrainTypes": return ListTerrainTypes();
                default:
                    return Response(false, "未知的 op：" + op, null);
            }
        }

        /// <summary>
        /// 返回 API 清单，供 AI 自我发现能力。
        /// </summary>
        /// <returns>API 清单 JSON</returns>
        public static string Describe()
        {
            var apis = new object[]
            {
                new { op = "mapInfo", args = "", desc = "获取当前地图信息(尺寸、格数、顶点数、标识)" },
                new { op = "createNewMap", args = "{ width:int, height:int, key:string }", desc = "按像素尺寸新建地图(格数 = 尺寸/4)" },
                new { op = "loadMap", args = "{ path:string }", desc = "从 .bin 文件加载地图；path 可为相对 Content 目录的路径" },
                new { op = "saveMap", args = "{ path?:string }", desc = "保存地图为 .bin；path 省略时保存到 Map/{标识}.bin" },
                new { op = "generateTerrain", args = "MapGenConfig 字段(见 configSchema)", desc = "程序化生成地形(高度场+地格分类+顶点数据)" },
                new { op = "generateRiver", args = "RiverGenConfig 字段(见 riverSchema)", desc = "在已生成地形之上生成河流" },
                new { op = "generateCities", args = "{ ...CityGenConfig, territory?:TerritoryGenConfig }", desc = "在已生成地形上选址城市并划分领地" },
                new { op = "generateRoads", args = "RoadGenConfig 字段(见 roadSchema)", desc = "在已生成城市之间铺设道路(主径/栈道/渡所)" },
                new { op = "generateBaseMap", args = "MapGenConfig 字段(baseMapTint/baseMapShade/baseMapSeasonal 生效)", desc = "按地形与高度生成四季底图(逐顶点底色图，写出 4 个季节 png)" },
                new { op = "setupSeasonLayers", args = "{ terrainLayerCount?:int, loadTextures?:bool }", desc = "初始化四季地表贴图层(层数+四季贴图名，缺图自动回退 Default 目录)" },
                new { op = "generateWorld", args = "{ ...MapGenConfig, river?:RiverGenConfig, city?:CityGenConfig, territory?:TerritoryGenConfig, road?:RoadGenConfig }", desc = "一次性生成地形、河流、城市、领地与道路" },
                new { op = "listCities", args = "", desc = "列出当前上下文中的城市据点(坐标/名称/相邻城市)" },
                new { op = "syncScenarioCities", args = "", desc = "把生成的城市坐标同步到当前剧本(Scenario.Cur)的城市并导出" },
                new { op = "createScenarioCities", args = "ScenarioCityGenConfig 字段(见 scenarioCitySchema)", desc = "把生成的城市写入剧本：不存在则按模板新建，存在则更新坐标，可自动导出" },
                new { op = "applyChinaScenario", args = "{ scenarioPath?:string, exportPath?:string, mapType?:string, applyCityNames?:bool }", desc = "把真实三国城池(名称/坐标/相邻关系)写入剧本，缩放关所与港口，并把剧本的地图类型指向当前地图，导出为新剧本文件(不覆盖原文件)" },
                new { op = "paintTerrain", args = "{x,y,terrain} 或 {x,y,w,h,terrain} 或 {points:[{x,y,terrain}]}", desc = "手动改写指定地格的地形类型" },
                new { op = "queryCell", args = "{ x:int, y:int }", desc = "查询单个地格的地形、状态与通行性" },
                new { op = "terrainStats", args = "", desc = "统计当前地图各类地形的数量" },
                new { op = "terrainTypes", args = "", desc = "列出所有地形类型(ID/名称/是否水域/是否可通行)" },
            };

            return Response(true, "可用操作列表", new
            {
                entry = "Run({\"op\":\"...\",\"args\":{...}})",
                apis = apis,
                configSchema = ConfigSchema(),
                riverSchema = RiverSchema(),
                citySchema = CitySchema(),
                territorySchema = TerritorySchema(),
                roadSchema = RoadSchema(),
                scenarioCitySchema = ScenarioCitySchema()
            });
        }

        // ------------------------------------------------------------------
        // 地图基础操作
        // ------------------------------------------------------------------

        /// <summary>获取当前地图信息</summary>
        public static string GetMapInfo()
        {
            Render.MapRender map = Render.MapRender.Instance;
            if (map == null)
                return Response(false, "地图系统未初始化", null);

            bool hasMap = map.mapData != null && map.mapGrid != null;

            return Response(true, "地图信息", new
            {
                hasMap = hasMap,
                workContent = map.WorkContent,
                fileName = map.FileName,
                mapWidth = map.mapWidth,
                mapHeight = map.mapHeight,
                cellsX = hasMap ? map.mapGrid.bounds.x : 0,
                cellsY = hasMap ? map.mapGrid.bounds.y : 0,
                vertexXMax = hasMap ? map.mapData.vertex_x_max : 0,
                vertexYMax = hasMap ? map.mapData.vertex_y_max : 0,
                gridSize = hasMap ? map.mapGrid.gridSize : 0
            });
        }

        /// <summary>新建地图</summary>
        /// <param name="json">{ width, height, key }</param>
        public static string CreateNewMap(string json)
        {
            JObject o = ParseObject(json);
            if (o == null)
                return Response(false, "参数解析失败", null);

            Render.MapRender map = Render.MapRender.Instance;
            if (map == null)
                return Response(false, "地图系统未初始化", null);

            int width = o.Value<int?>("width") ?? 1024;
            int height = o.Value<int?>("height") ?? 1024;
            string key = o.Value<string>("key") ?? "GeneratedMap";

            if (width <= 0 || height <= 0 || width % 4 != 0 || height % 4 != 0)
                return Response(false, "宽高必须为正且能被 4 整除(格数 = 尺寸/4)", null);

            // 注意：NewMap 内部会调用 Init() 把 WorkContent 重置为 "Default"，
            // 因此必须在 NewMap 之后再写回地图标识。
            map.NewMap(width, height);
            map.WorkContent = key;

            return Response(true, string.Format("已新建地图：{0}x{1} 像素，{2}x{3} 格", width, height, width / 4, height / 4),
                new { cellsX = map.mapGrid.bounds.x, cellsY = map.mapGrid.bounds.y });
        }

        /// <summary>加载地图</summary>
        /// <param name="json">{ path }</param>
        public static string LoadMap(string json)
        {
            JObject o = ParseObject(json);
            string path = o?.Value<string>("path");
            if (string.IsNullOrEmpty(path))
                return Response(false, "缺少 path 参数", null);

            Render.MapRender map = Render.MapRender.Instance;
            if (map == null)
                return Response(false, "地图系统未初始化", null);

            string fullPath = ResolvePath(path, null);
            if (!System.IO.File.Exists(fullPath))
                return Response(false, "文件不存在：" + fullPath, null);

            map.LoadMap(fullPath);
            return Response(true, "地图已加载：" + fullPath, new
            {
                cellsX = map.mapGrid.bounds.x,
                cellsY = map.mapGrid.bounds.y
            });
        }

        /// <summary>保存地图</summary>
        /// <param name="json">{ path? }</param>
        public static string SaveMap(string json)
        {
            JObject o = ParseObject(json);
            string path = o?.Value<string>("path");

            Render.MapRender map = Render.MapRender.Instance;
            if (map == null)
                return Response(false, "地图系统未初始化", null);
            if (map.mapData == null || map.mapGrid == null)
                return Response(false, "当前没有可保存的地图", null);

            string defaultName = string.IsNullOrEmpty(map.WorkContent) ? "GeneratedMap" : map.WorkContent;
            string fullPath = ResolvePath(path, "Map/" + defaultName + ".bin");

            string dir = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            map.SaveMap(fullPath);
            return Response(true, "地图已保存：" + fullPath, new { path = fullPath });
        }

        // ------------------------------------------------------------------
        // 生成操作
        // ------------------------------------------------------------------

        /// <summary>程序化生成地形</summary>
        /// <param name="json">MapGenConfig 字段</param>
        public static string GenerateTerrain(string json)
        {
            MapGenResult result = RunGenerate(json, false, false);
            return ResultResponse(result);
        }

        /// <summary>在已生成地形之上生成河流</summary>
        /// <param name="json">RiverGenConfig 字段</param>
        public static string GenerateRiver(string json)
        {
            Render.MapRender map = Render.MapRender.Instance;
            if (map == null)
                return Response(false, "地图系统未初始化", null);

            RiverGenConfig riverCfg = Deserialize<RiverGenConfig>(json) ?? new RiverGenConfig();
            MapGenResult result = MapGenerator.GenerateRiversOnly(map, riverCfg);
            return ResultResponse(result);
        }

        /// <summary>在已生成地形上选址城市并划分领地</summary>
        /// <param name="json">{ ...CityGenConfig, territory?:TerritoryGenConfig }</param>
        public static string GenerateCities(string json)
        {
            Render.MapRender map = Render.MapRender.Instance;
            if (map == null)
                return Response(false, "地图系统未初始化", null);

            JObject root = ParseObject(json);
            CityGenConfig cityCfg = ExtractCityConfig(root, json) ?? new CityGenConfig();
            TerritoryGenConfig terrCfg = ExtractTerritoryConfig(root, cityCfg);

            MapGenResult result = MapGenerator.GenerateCitiesOnly(map, cityCfg, terrCfg);
            return ResultResponse(result);
        }

        /// <summary>在已生成城市之间铺设道路</summary>
        /// <param name="json">RoadGenConfig 字段</param>
        public static string GenerateRoads(string json)
        {
            Render.MapRender map = Render.MapRender.Instance;
            if (map == null)
                return Response(false, "地图系统未初始化", null);

            JObject root = ParseObject(json);
            RoadGenConfig roadCfg = ExtractRoadConfig(root);

            MapGenResult result = MapGenerator.GenerateRoadsOnly(map, roadCfg);
            return ResultResponse(result);
        }

        /// <summary>
        /// 生成底图(逐顶点底色图)，写出 4 个季节的 png 并立即生效。
        /// </summary>
        /// <param name="json">MapGenConfig 字段(baseMapTint / baseMapShade 生效)</param>
        public static string GenerateBaseMap(string json)
        {
            Render.MapRender map = Render.MapRender.Instance;
            if (map == null)
                return Response(false, "地图系统未初始化", null);

            MapGenContext ctx = MapGenContext.Current;
            if (ctx == null || ctx.map != map)
            {
                MapGenConfig cfg = Deserialize<MapGenConfig>(json) ?? new MapGenConfig();
                ctx = MapGenContext.Build(map, cfg);
                if (ctx == null)
                    return Response(false, "生成失败：地图尺寸无效或地形类型数据未初始化", null);
            }

            int written = BaseMapGenerator.Generate(ctx);
            return Response(written > 0,
                written > 0
                    ? string.Format("底图生成完成：写出 {0} 个季节文件", written)
                    : "底图生成失败：未找到底图模块或输出目录不可写",
                new { files = written });
        }

        /// <summary>
        /// 初始化四季地表贴图层：把图层数补齐并把四季贴图名设为 layer_N / water_0，
        /// 缺图时自动回退到 Assets/Map/Default/Terrain/{Season} 目录下的原版贴图。
        /// </summary>
        /// <param name="json">{ terrainLayerCount?:int(默认36), loadTextures?:bool(默认true) }</param>
        public static string SetupSeasonLayers(string json)
        {
            Render.MapRender map = Render.MapRender.Instance;
            if (map == null || map.mapLayer == null)
                return Response(false, "地图系统未初始化或没有图层数据", null);

            JObject o = ParseObject(json);
            int terrainLayerCount = o?.Value<int?>("terrainLayerCount") ?? SeasonLayerSetup.DefaultTerrainLayerCount;
            bool loadTextures = o?.Value<bool?>("loadTextures") ?? true;

            int layers = SeasonLayerSetup.Setup(map, terrainLayerCount, loadTextures);
            if (layers <= 0)
                return Response(false, "四季贴图层初始化失败", null);

            // 顺便让地形绘制使用与原版一致的图层映射
            int maxLayer = Mathf.Max(0, map.mapLayer.layerDatas.Length - 2);

            return Response(true, string.Format("四季贴图层初始化完成：共 {0} 层(地表 {1} + 水面 1)",
                    layers, layers - 1),
                new
                {
                    layers = layers,
                    terrainLayers = layers - 1,
                    maxUsableLayer = maxLayer,
                    seasons = SeasonLayerSetup.SeasonNames,
                    currentSeason = map.mapLayer.curSeason
                });
        }

        /// <summary>列出当前生成上下文中的城市</summary>
        public static string ListCities()
        {
            MapGenContext ctx = MapGenContext.Current;
            if (ctx == null || ctx.cities == null)
                return Response(false, "尚未生成城市", null);

            return Response(true, string.Format("共 {0} 座城市", ctx.cities.Count),
                new { count = ctx.cities.Count, items = ToCityList(ctx.cities) });
        }

        /// <summary>
        /// 把生成的城市坐标同步到当前剧本(Scenario.Cur)的城市对象并导出。
        /// </summary>
        public static string SyncScenarioCities()
        {
            MapGenContext ctx = MapGenContext.Current;
            if (ctx == null || ctx.cities == null || ctx.cities.Count == 0)
                return Response(false, "尚无生成的城市数据，请先执行城市生成", null);

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return Response(false, "当前没有正在运行的剧本(Scenario.Cur 为空)，请在剧本编辑器或游戏内调用", null);
            if (scenario.citySet == null)
                return Response(false, "当前剧本没有城市数据", null);

            int updated = 0;
            int missing = 0;
            for (int i = 0; i < ctx.cities.Count; i++)
            {
                CitySite site = ctx.cities[i];
                City city = scenario.citySet.Get(site.id);
                if (city == null)
                {
                    missing++;
                    continue;
                }

                city.x = site.x;
                city.y = site.y;
                updated++;
            }

            bool exported = false;
            if (!string.IsNullOrEmpty(scenario.FilePath))
            {
                scenario.Export(scenario.FilePath);
                exported = true;
            }

            return Response(true, string.Format("已同步 {0} 座城市位置{1}",
                    updated, missing > 0 ? "(" + missing + " 座在剧本中不存在)" : ""),
                new { updated = updated, missing = missing, exported = exported });
        }

        /// <summary>
        /// 把生成的城市写入剧本(不存在则按模板新建，存在则更新坐标)，可自动导出。
        /// </summary>
        /// <param name="json">ScenarioCityGenConfig 字段</param>
        public static string CreateScenarioCities(string json)
        {
            MapGenContext ctx = MapGenContext.Current;
            if (ctx == null || ctx.cities == null || ctx.cities.Count == 0)
                return Response(false, "尚未生成城市，请先执行城市生成", null);

            Scenario scenario = ResolveScenario();
            if (scenario == null)
                return Response(false, "未找到可用剧本(Scenario 为空)，请在剧本编辑器或游戏内调用", null);

            ScenarioCityGenConfig cfg = Deserialize<ScenarioCityGenConfig>(json) ?? new ScenarioCityGenConfig();
            ScenarioCityResult result = ScenarioCityBuilder.Build(scenario, ctx.cities, cfg);

            return Response(result.success, result.message, new
            {
                created = result.created,
                updated = result.updated,
                removed = result.removed,
                templateCityId = result.templateCityId,
                totalCities = result.totalCities,
                exported = result.exported,
                exportPath = result.exportPath
            });
        }

        /// <summary>
        /// 把"真实三国城池"写入剧本，并让剧本指向当前地图。
        ///
        /// 做的事：
        ///   1. 42 座城市：坐标取当前生成上下文(已经过可建格吸附，与地图上的"都市"地格一致)，
        ///      名称与相邻关系来自真实三国数据(ChinaCityTable)；
        ///   2. 其余据点(关所 / 港，Id &gt; 42)：按同一"原版中国图 → 本图"的换算缩放坐标，
        ///      否则它们会停留在旧地图的位置上；
        ///   3. 剧本的 Info.mapType 指向当前地图标识——否则游戏会去加载另一个 .bin，
        ///      城市坐标就会与地图对不上；
        ///   4. 导出为新的剧本文件(默认 Scenario/Scenario_china.json)，不覆盖原文件。
        /// </summary>
        /// <param name="json">{ scenarioPath?, exportPath?, mapType?, applyCityNames? }</param>
        /// <returns>结果 JSON</returns>
        public static string ApplyChinaScenario(string json)
        {
            JObject o = ParseObject(json);

            MapGenContext ctx = MapGenContext.Current;
            if (ctx == null || ctx.cities == null || ctx.cities.Count == 0)
                return Response(false, "尚未生成城市，请先执行城市生成(generateCities / generateWorld)", null);

            string scenarioPath = o?.Value<string>("scenarioPath");
            if (string.IsNullOrEmpty(scenarioPath))
                scenarioPath = "Scenario/Scenario.json";
            string fullPath = ResolvePath(scenarioPath, null);
            if (!System.IO.File.Exists(fullPath))
                return Response(false, "剧本文件不存在：" + fullPath, null);

            // 直接在 JSON 层面改写：
            //   1. 避免引擎反序列化(Scenario 需要先准备好 CommonData/Map 等)带来的脆弱依赖；
            //   2. 能完整保留剧本里未被本模块认知的字段；
            //   3. 关所/港口(Id > 42)也能一并处理——它们不在地图的生成上下文里。
            JObject root;
            JObject citySet;
            try
            {
                root = JObject.Parse(System.IO.File.ReadAllText(fullPath));
                citySet = root["citySet"] as JObject;
            }
            catch (System.Exception e)
            {
                return Response(false, "剧本解析失败：" + e.Message, null);
            }

            if (citySet == null)
                return Response(false, "剧本里没有 citySet 节点：" + fullPath, null);

            bool applyNames = o == null || o.Value<bool?>("applyCityNames") != false;

            // 1) 42 座真实城池：坐标 / 名称 / 相邻关系(坐标来自生成上下文，与地图上的"都市"地格一致)
            int updated = 0;
            int missing = 0;
            for (int i = 0; i < ctx.cities.Count; i++)
            {
                CitySite site = ctx.cities[i];
                JObject node = citySet[site.id.ToString()] as JObject;
                if (node == null)
                {
                    missing++;
                    continue;
                }

                node["x"] = site.x;
                node["y"] = site.y;
                if (applyNames && !string.IsNullOrEmpty(site.name))
                    node["Name"] = site.name;

                JArray neighborArray = new JArray();
                if (site.neighbors != null)
                {
                    for (int k = 0; k < site.neighbors.Count; k++)
                        neighborArray.Add(site.neighbors[k]);
                }
                node["NeighborList"] = neighborArray;
                updated++;
            }

            // 2) 其余据点(关所 / 港)：按同一"原版中国图 → 本图"的换算缩放坐标，
            //    否则它们会停留在旧地图的位置上
            int scaledSites = 0;
            foreach (System.Collections.Generic.KeyValuePair<string, JToken> kv in citySet)
            {
                int id;
                if (!int.TryParse(kv.Key, out id) || id <= ChinaCityTable.All.Length)
                    continue;

                JObject node = kv.Value as JObject;
                if (node == null)
                    continue;

                int sx = node.Value<int?>("x") ?? 0;
                int sy = node.Value<int?>("y") ?? 0;
                ctx.CellFromSource(sx, sy, out int nx, out int ny);
                node["x"] = nx;
                node["y"] = ny;
                scaledSites++;
            }

            // 3) 剧本指向当前地图：否则游戏会去加载另一个 .bin，城市坐标就会与地图对不上
            Render.MapRender map = Render.MapRender.Instance;
            string wantMapType = o?.Value<string>("mapType");
            if (string.IsNullOrEmpty(wantMapType) && map != null)
                wantMapType = map.WorkContent;

            bool mapTypeChanged = false;
            JObject info = root["Info"] as JObject;
            if (info != null && !string.IsNullOrEmpty(wantMapType)
                && info.Value<string>("mapType") != wantMapType)
            {
                info["mapType"] = wantMapType;
                mapTypeChanged = true;
            }

            // 4) 导出到新文件，不覆盖原剧本
            string exportPath = o?.Value<string>("exportPath");
            if (string.IsNullOrEmpty(exportPath))
                exportPath = "Scenario/Scenario_china.json";
            string exportFull = ResolvePath(exportPath, null);

            string dir = System.IO.Path.GetDirectoryName(exportFull);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllText(exportFull, root.ToString(Formatting.None));

            return Response(true, string.Format(
                "真实城池写入完成：更新 {0} 座(剧本中缺失 {1} 座)；关所/港缩放 {2} 座；地图类型 → {3}；已导出 {4}",
                updated, missing, scaledSites, wantMapType, exportFull),
                new
                {
                    exportPath = exportFull,
                    updated = updated,
                    missing = missing,
                    scaledSites = scaledSites,
                    mapType = wantMapType,
                    mapTypeChanged = mapTypeChanged
                });
        }

        /// <summary>
        /// 解析当前可用剧本：优先正在运行的剧本，其次选中的剧本，最后退回地图编辑器持有的剧本。
        /// </summary>
        private static Scenario ResolveScenario()
        {
            if (Scenario.Cur != null)
                return Scenario.Cur;
            if (Scenario.CurSelected != null)
                return Scenario.CurSelected;

            try
            {
                Sango.Tools.MapEditor editor = UnityEngine.Object.FindObjectOfType<Sango.Tools.MapEditor>();
                if (editor != null)
                    return editor.scenario;
            }
            catch
            {
                // 编辑器对象不存在时忽略
            }

            return null;
        }

        /// <summary>一次性生成地形、河流、城市、领地与道路</summary>
        /// <param name="json">{ ...MapGenConfig, river?, city?, territory?, road? }</param>
        public static string GenerateWorld(string json)
        {
            MapGenResult result = RunGenerate(json, true, true, true);
            return ResultResponse(result);
        }

        /// <summary>
        /// 内部：解析配置并执行生成。
        /// </summary>
        /// <param name="json">参数 JSON</param>
        /// <param name="withRiver">是否同时生成河流</param>
        /// <param name="withCity">是否同时生成城市与领地</param>
        /// <param name="withRoad">是否同时生成道路</param>
        private static MapGenResult RunGenerate(string json, bool withRiver, bool withCity, bool withRoad = false)
        {
            Render.MapRender map = Render.MapRender.Instance;
            if (map == null)
            {
                return new MapGenResult { message = "地图系统未初始化" };
            }

            JObject root = ParseObject(json);
            MapGenConfig terrainCfg;

            // 支持两种写法：地形参数平铺，或 { terrain:{...}, river:{...}, city:{...} }
            if (root != null && root["terrain"] != null)
                terrainCfg = Deserialize<MapGenConfig>(root["terrain"].ToString(Formatting.None)) ?? new MapGenConfig();
            else
                terrainCfg = Deserialize<MapGenConfig>(json) ?? new MapGenConfig();

            RiverGenConfig riverCfg = null;
            if (withRiver)
            {
                riverCfg = (root != null && root["river"] != null)
                    ? (Deserialize<RiverGenConfig>(root["river"].ToString(Formatting.None)) ?? new RiverGenConfig())
                    : new RiverGenConfig(); // 默认生成河流
            }

            CityGenConfig cityCfg = null;
            TerritoryGenConfig terrCfg = null;
            if (withCity)
            {
                cityCfg = ExtractCityConfig(root, json) ?? new CityGenConfig();
                terrCfg = ExtractTerritoryConfig(root, cityCfg);
            }

            // 道路需要城市，因此在未生成城市时不会生效
            RoadGenConfig roadCfg = (withRoad && cityCfg != null) ? ExtractRoadConfig(root) : null;

            return MapGenerator.GenerateAll(map, terrainCfg, riverCfg, cityCfg, terrCfg, roadCfg);
        }

        /// <summary>从参数中提取道路配置</summary>
        private static RoadGenConfig ExtractRoadConfig(JObject root)
        {
            if (root != null && root["road"] != null)
                return Deserialize<RoadGenConfig>(root["road"].ToString(Formatting.None)) ?? new RoadGenConfig();
            if (root != null && root["cliffCost"] != null)
                return Deserialize<RoadGenConfig>(root.ToString(Formatting.None)) ?? new RoadGenConfig();
            return new RoadGenConfig();
        }

        /// <summary>从参数中提取城市配置</summary>
        private static CityGenConfig ExtractCityConfig(JObject root, string rawJson)
        {
            if (root != null && root["city"] != null)
                return Deserialize<CityGenConfig>(root["city"].ToString(Formatting.None));
            if (root != null && root["cityCount"] != null)
                return Deserialize<CityGenConfig>(root.ToString(Formatting.None));
            return null;
        }

        /// <summary>从参数中提取领地配置</summary>
        private static TerritoryGenConfig ExtractTerritoryConfig(JObject root, CityGenConfig cityCfg)
        {
            TerritoryGenConfig cfg = null;
            if (root != null && root["territory"] != null)
                cfg = Deserialize<TerritoryGenConfig>(root["territory"].ToString(Formatting.None));

            if (cfg == null)
                cfg = new TerritoryGenConfig();

            // 未显式指定内政半径时，沿用地形城市配置中的半径
            if (root == null || root["territory"] == null || root["territory"]["interiorRadius"] == null)
            {
                if (cityCfg != null)
                    cfg.interiorRadius = cityCfg.interiorRadius;
            }

            return cfg;
        }

        // ------------------------------------------------------------------
        // 地格编辑与查询
        // ------------------------------------------------------------------

        /// <summary>
        /// 改写地格地形。支持三种写法：
        ///   { x, y, terrain }                      单个地格
        ///   { x, y, w, h, terrain }                矩形区域
        ///   { points: [ { x, y, terrain }, ... ] } 批量
        /// </summary>
        /// <param name="json">参数 JSON</param>
        public static string PaintTerrain(string json)
        {
            JObject o = ParseObject(json);
            if (o == null)
                return Response(false, "参数解析失败", null);

            Render.MapRender map = Render.MapRender.Instance;
            if (map == null || map.mapGrid == null)
                return Response(false, "当前没有可编辑的地图", null);

            int changed = 0;

            JArray points = o["points"] as JArray;
            if (points != null)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    JObject p = points[i] as JObject;
                    if (p == null)
                        continue;
                    changed += PaintOne(map, p.Value<int>("x"), p.Value<int>("y"), p.Value<int>("terrain"));
                }
            }
            else if (o["w"] != null && o["h"] != null)
            {
                int x0 = o.Value<int>("x");
                int y0 = o.Value<int>("y");
                int w = o.Value<int>("w");
                int h = o.Value<int>("h");
                int terrain = o.Value<int>("terrain");
                for (int x = x0; x < x0 + w; x++)
                    for (int y = y0; y < y0 + h; y++)
                        changed += PaintOne(map, x, y, terrain);
            }
            else
            {
                changed += PaintOne(map, o.Value<int>("x"), o.Value<int>("y"), o.Value<int>("terrain"));
            }

            if (changed > 0)
                RefreshAfterEdit(map);

            return Response(true, string.Format("已改写 {0} 个地格", changed), new { changed });
        }

        /// <summary>改写单个地格并同步上下文/顶点材质</summary>
        private static int PaintOne(Render.MapRender map, int x, int y, int terrainId)
        {
            if (x < 0 || y < 0 || x >= map.mapGrid.bounds.x || y >= map.mapGrid.bounds.y)
                return 0;
            if (terrainId < 0 || terrainId >= TerrainIds.Count)
                return 0;

            map.mapGrid.SetTerrainType(x, y, terrainId);

            // 同步生成上下文，保证后续阶段数据一致
            MapGenContext ctx = MapGenContext.Current;
            int materialLayer;
            if (ctx != null && ctx.map == map)
            {
                ctx.cellTerrain[x, y] = (byte)terrainId;
                materialLayer = (terrainId >= 0 && terrainId < ctx.material.Length) ? ctx.material[terrainId] : 0;
            }
            else
            {
                materialLayer = MapGenContext.MaterialForTerrain(terrainId);
            }

            // 更新地格中心顶点的材质(近似着色)
            int vx = x * 4 + 2;
            int vy = y * 4 + (x & 1) * 2 + 2;
            // VertexData 是 struct，GetVertexData 返回副本，必须用写回接口
            map.mapData.SetVertexTextureIndex(vx, vy, (byte)materialLayer);

            return 1;
        }

        /// <summary>刷新通行遮罩与地表网格</summary>
        private static void RefreshAfterEdit(Render.MapRender map)
        {
            int cellsX = map.mapGrid.bounds.x;
            int cellsY = map.mapGrid.bounds.y;
            for (int x = 0; x < cellsX; x++)
                for (int y = 0; y < cellsY; y++)
                    map.mapGrid.BeginUpdateMovable(x, y);
            map.mapGrid.EndUpdateMovable();

            if (map.mapTerrain != null)
                map.mapTerrain.Rebuild();
        }

        /// <summary>查询单个地格</summary>
        /// <param name="json">{ x, y }</param>
        public static string QueryCell(string json)
        {
            JObject o = ParseObject(json);
            if (o == null)
                return Response(false, "参数解析失败", null);

            Render.MapRender map = Render.MapRender.Instance;
            if (map == null || map.mapGrid == null)
                return Response(false, "当前没有地图", null);

            int x = o.Value<int>("x");
            int y = o.Value<int>("y");
            if (x < 0 || y < 0 || x >= map.mapGrid.bounds.x || y >= map.mapGrid.bounds.y)
                return Response(false, "坐标越界", null);

            Render.MapGrid.GridData data = map.mapGrid.GetGridData(x, y);
            TerrainType terrainType = GameData.Instance?.ScenarioCommonData?.TerrainTypes?.Get(data.terrainType);

            return Response(true, "地格信息", new
            {
                x = x,
                y = y,
                terrainId = data.terrainType,
                terrainName = terrainType != null ? terrainType.Name : "",
                isWater = terrainType != null && terrainType.isWater,
                moveable = terrainType != null && terrainType.moveable,
                terrainState = data.terrainState,
                areaId = data.areaId
            });
        }

        /// <summary>统计当前地图各类地形数量</summary>
        public static string QueryTerrainStats()
        {
            Render.MapRender map = Render.MapRender.Instance;
            if (map == null || map.mapGrid == null)
                return Response(false, "当前没有地图", null);

            int cellsX = map.mapGrid.bounds.x;
            int cellsY = map.mapGrid.bounds.y;
            int[] counts = new int[TerrainIds.Count];
            int total = 0;

            for (int x = 0; x < cellsX; x++)
            {
                for (int y = 0; y < cellsY; y++)
                {
                    Render.MapGrid.GridData data = map.mapGrid.GetGridData(x, y);
                    if (data == null)
                        continue;
                    if (data.terrainType >= 0 && data.terrainType < TerrainIds.Count)
                        counts[data.terrainType]++;
                    total++;
                }
            }

            var items = new System.Collections.Generic.List<object>();
            for (int i = 0; i < TerrainIds.Count; i++)
            {
                if (counts[i] <= 0)
                    continue;
                TerrainType t = GameData.Instance?.ScenarioCommonData?.TerrainTypes?.Get(i);
                items.Add(new
                {
                    id = i,
                    name = t != null ? t.Name : "",
                    count = counts[i],
                    ratio = total > 0 ? (float)counts[i] / total : 0f
                });
            }

            return Response(true, string.Format("共 {0} 个地格", total), new { total = total, items = items });
        }

        /// <summary>列出所有地形类型</summary>
        public static string ListTerrainTypes()
        {
            if (GameData.Instance == null || GameData.Instance.ScenarioCommonData == null
                || GameData.Instance.ScenarioCommonData.TerrainTypes == null)
                return Response(false, "地形类型数据未初始化", null);

            var items = new System.Collections.Generic.List<object>();
            for (int i = 0; i < TerrainIds.Count; i++)
            {
                TerrainType t = GameData.Instance.ScenarioCommonData.TerrainTypes.Get(i);
                if (t == null)
                    continue;
                items.Add(new
                {
                    id = i,
                    name = t.Name,
                    isWater = t.isWater,
                    moveable = t.moveable,
                    canBuild = t.canBuild,
                    baseCost = t.baseCost
                });
            }

            return Response(true, "地形类型列表", new { items = items });
        }

        // ------------------------------------------------------------------
        // 辅助
        // ------------------------------------------------------------------

        /// <summary>生成结果 → JSON 响应</summary>
        private static string ResultResponse(MapGenResult result)
        {
            if (result == null)
                return Response(false, "生成失败", null);

            return Response(result.success, result.message, new
            {
                cellsX = result.cellsX,
                cellsY = result.cellsY,
                elapsedMs = result.elapsedMs,
                hasRivers = result.hasRivers,
                hasBaseMap = result.hasBaseMap,
                roadCount = result.roadCount,
                cityCount = result.cityCount,
                cities = result.cities != null ? ToCityList(result.cities) : null,
                terrainCounts = ToCountList(result.terrainCounts)
            });
        }

        /// <summary>城市列表 → 名称化列表</summary>
        private static System.Collections.Generic.List<object> ToCityList(System.Collections.Generic.List<CitySite> cities)
        {
            var list = new System.Collections.Generic.List<object>();
            if (cities == null)
                return list;

            for (int i = 0; i < cities.Count; i++)
            {
                CitySite site = cities[i];
                list.Add(new
                {
                    id = site.id,
                    name = site.name,
                    x = site.x,
                    y = site.y,
                    score = site.score,
                    neighbors = site.neighbors
                });
            }
            return list;
        }

        /// <summary>地形数量数组 → 名称化列表</summary>
        private static System.Collections.Generic.List<object> ToCountList(int[] counts)
        {
            var list = new System.Collections.Generic.List<object>();
            if (counts == null)
                return list;

            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] <= 0)
                    continue;
                TerrainType t = GameData.Instance?.ScenarioCommonData?.TerrainTypes?.Get(i);
                list.Add(new { id = i, name = t != null ? t.Name : "", count = counts[i] });
            }
            return list;
        }

        /// <summary>统一响应结构</summary>
        private static string Response(bool success, string message, object data)
        {
            return JsonConvert.SerializeObject(new { success = success, message = message, data = data });
        }

        /// <summary>安全解析 JSON 对象</summary>
        private static JObject ParseObject(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new JObject();
            try
            {
                return JObject.Parse(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>安全反序列化(字段名不匹配时忽略)</summary>
        private static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 解析路径：绝对路径原样返回；相对路径相对 Content 根目录。
        /// </summary>
        private static string ResolvePath(string path, string fallbackRelative)
        {
            string real = string.IsNullOrEmpty(path) ? fallbackRelative : path;
            if (string.IsNullOrEmpty(real))
                real = "Map/GeneratedMap.bin";

            if (System.IO.Path.IsPathRooted(real))
                return real;

            string root = Sango.Path.ContentRootPath;
            if (string.IsNullOrEmpty(root))
                return real;
            return root + "/" + real;
        }

        /// <summary>地形参数说明</summary>
        private static object ConfigSchema()
        {
            return new
            {
                seed = "int 随机种子",
                landRatio = "float 陆地占比 0.05~0.95",
                mountainRatio = "float 山地占陆地比例 0~0.6",
                shallowRatio = "float 浅滩占水域比例 0~0.6",
                wetThreshold = "float 森林湿度阈值",
                dryThreshold = "float 荒地湿度阈值",
                sandThreshold = "float 砂地湿度阈值",
                swampThreshold = "float 湿地湿度阈值",
                swampMaxAltitude = "float 湿地最大相对海拔",
                soilThreshold = "float 草地/土分割阈值",
                terrainFeatureSize = "float 地形特征尺寸(格)",
                climateFeatureSize = "float 气候特征尺寸(格)",
                continentScale = "float 大陆频率 0.05~2",
                continentWeight = "float 大陆权重 0~1",
                octaves = "int 倍频数 1~8",
                persistence = "float 振幅衰减",
                lacunarity = "float 频率增长",
                smoothPasses = "int 平滑次数 0~6",
                edgeFalloffCells = "float 边缘衰减宽度(格)",
                seaLevelByte = "int 海平面高度字节(默认 10，海底为 0)",
                waterByte = "int 海平面水面高度字节(默认 11)：海与浅滩共用该水平面；引擎需 water > height 才绘制水面",
                riverDrop = "int 河川水面相对河床的落差字节(默认 11，对齐原版)：保证水面严格高于河床、不重叠",
                riverBankDrop = "int 河川水面相对自然地面的下沉字节(默认 2)：水位随海拔抬升，上游高于海平面",
                maxLandByte = "int 陆地最高高度字节(默认 255)",
                landHeightCurve = "float 陆地高度曲线指数(默认 4，越大平原越多、山峰越尖)",
                renderWater = "bool 是否为水域渲染水面",
                recomputeNormals = "bool 是否重算顶点法线",
                generateBaseMap = "bool 是否生成底图(逐顶点底色图)",
                baseMapTint = "float 底图中地形色权重 0~1(默认 0.35)",
                baseMapShade = "float 底图高度明暗强度 0~1(默认 0.25)",
                baseMapSeasonal = "bool 底图是否区分四季(默认 true；冬季含积雪)",
                cliffLayerSpread = "bool 崖是否在多张岩石贴图间分布(默认 true)",
                cliffSteepSlope = "float 陡坡岩壁阈值(相邻顶点高度字节差，默认 16)",
                cliffGentleSlope = "float 缓坡土石阈值(默认 6)",
                useSkeleton = "bool 是否用原版地图骨架(真实中国形貌与高度基准)；开启后海陆轮廓/山系/河道沿用骨架，地表、城市、道路、领地仍由生成器重绘",
                skeletonPath = "string 骨架来源地图(相对 Content 目录)，默认 Mods/PK22/Map/pk22.bin(内含 200×200 中国区域)",
                skeletonOffsetX = "int 骨架有效区域起点 X；0 = 自动探测(原版中国区域为 28)",
                skeletonOffsetY = "int 骨架有效区域起点 Y；0 = 自动探测(原版中国区域为 28)",
                skeletonWeight = "float 高度场中原版高度的权重 0~1(默认 0.8)：1 = 完全沿用原版高度，0 = 纯噪声",
                skeletonUseMountains = "bool 是否沿用骨架中的山系(崖)，默认 true",
                skeletonUseRivers = "bool 是否沿用骨架中的河道(川/河)，默认 true"
            };
        }

        /// <summary>河流参数说明</summary>
        private static object RiverSchema()
        {
            return new
            {
                flowThreshold = "int 成河最小汇流量(格)，默认 14",
                widenFlowFactor = "int 大江加宽倍数，默认 4",
                majorRiverFactor = "int 大江判定倍数(默认 8)：汇流量 ≥ flowThreshold*该值 的河段用「河」(大江)，其余用「川」(小河)；设为 0/1 则全部用「川」",
                allowLake = "bool 是否生成湖泊",
                lakeThreshold = "int 成湖最小汇流量(格)，默认 45"
            };
        }

        /// <summary>城市参数说明</summary>
        private static object CitySchema()
        {
            return new
            {
                cityCount = "int 目标城市数量，默认 24",
                minSpacing = "int 城市间最小六边形距离(格)，默认 6",
                interiorRadius = "int 内政区半径(格)，默认 3",
                seed = "int 随机种子；0 表示沿用地形种子",
                riversideBonus = "float 临河加成",
                coastBonus = "float 临海加成",
                lowlandBonus = "float 低海拔加成",
                badTerrainPenalty = "float 恶劣地形惩罚",
                mainLandMinRatio = "float 可建城的连通块相对最大陆地连通块的最小占比(默认 0.15)，避免城市落在孤岛上",
                useFixedSites = "bool 是否改用真实三国城池表落位(默认 false)：坐标由原版中国图 lo/hi 换算，名称与相邻关系取真实数据；开启后 cityCount/minSpacing 等失效",
                namePrefix = "string 城市名称前缀，默认 城"
            };
        }

        /// <summary>剧本城池写入参数说明</summary>
        private static object ScenarioCitySchema()
        {
            return new
            {
                templateCityId = "int 模板城市ID；0 表示自动取剧本中第一座城市",
                buildingTypeId = "int 新城市建筑类型ID(1 通常为都市)",
                defaultForceId = "int 新城市所属势力ID；0 表示无主",
                linkNeighbors = "bool 是否写入相邻城市关系，默认 true",
                overwriteExisting = "bool 已存在城市是否更新坐标，默认 true",
                removeExtraCities = "bool 是否删除超出生成范围的旧城市，默认 false",
                autoExport = "bool 是否自动导出剧本文件，默认 true",
                exportPath = "string 导出路径；为空时使用剧本自身的 FilePath"
            };
        }

        /// <summary>道路参数说明</summary>
        private static object RoadSchema()
        {
            return new
            {
                connectNeighborsOnly = "bool 是否只连接相邻城市，默认 true",
                cliffCost = "int 穿越山地(崖)的代价，默认 40，跨越后生成栈道",
                riverCost = "int 渡河(川/河)的代价，默认 25，跨越后生成渡所",
                blockSea = "bool 是否禁止穿海，默认 true",
                blockShallow = "bool 是否禁止穿越水域，默认 true；仅对地形数据里 isWater 为真的地形生效(浅滩是可通行滩涂，不受影响)",
                margin = "int 搜索包围盒外扩格数，默认 10",
                maxPairs = "int 最多处理的城市对；0 表示不限制"
            };
        }

        /// <summary>领地参数说明</summary>
        private static object TerritorySchema()
        {
            return new
            {
                useTerrainCost = "bool 是否按地形代价划分领地，默认 true",
                placeCityTerrain = "bool 是否把城址格改为都市地形，默认 true",
                interiorRadius = "int 内政区半径(格)，默认 3",
                impassableCost = "int 穿行不可通行陆地(山地/岸)的每格代价，默认 999；水域一律不可穿行。该值保证同一陆块上的领地必然相接，避免城市邻接图与道路网被山地割裂",
                linkByTerritory = "bool 是否按领地相接自动回填城市相邻关系(默认 true)；使用真实三国城池表落位时应设为 false，以保留原版邻接表"
            };
        }
    }
}
