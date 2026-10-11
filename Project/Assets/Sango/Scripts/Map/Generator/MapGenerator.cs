using System.Collections.Generic;
using UnityEngine;

namespace Sango.MapGen
{
    /// <summary>
    /// 程序化地图生成结果，用于界面展示、日志输出与 API 返回。
    /// </summary>
    public class MapGenResult
    {
        /// <summary>是否成功</summary>
        public bool success;
        /// <summary>提示信息(中文)</summary>
        public string message = "";
        /// <summary>地格宽度(格数)</summary>
        public int cellsX;
        /// <summary>地格高度(格数)</summary>
        public int cellsY;
        /// <summary>耗时(毫秒)</summary>
        public long elapsedMs;
        /// <summary>各地形类型数量，下标为地形类型ID</summary>
        public int[] terrainCounts = new int[TerrainIds.Count];
        /// <summary>是否包含河流生成</summary>
        public bool hasRivers;
        /// <summary>生成的城市数量</summary>
        public int cityCount;
        /// <summary>生成的城市据点列表</summary>
        public List<CitySite> cities;
        /// <summary>铺设的道路地格数量</summary>
        public int roadCount;
        /// <summary>是否包含道路生成</summary>
        public bool hasRoads;
        /// <summary>是否已生成底图(BaseTex)</summary>
        public bool hasBaseMap;
    }

    /// <summary>
    /// 程序化地图生成器(统一入口与流程编排)。
    ///
    /// 阶段顺序：地形(高度场+分类) → 河流 → 城市选址 → 领地划分 → 道路 → 写回引擎。
    /// 各阶段共享同一个 MapGenContext，保证数据自洽。
    ///
    /// 依赖关系：
    ///   MapGenContext   : 数据(高度场/阈值/地格场/材质/分类/归属)
    ///   RiverGenerator  : M2 河流
    ///   CityPlacer      : M3 城市选址
    ///   TerritoryBuilder: M3 领地划分
    ///   RoadGenerator   : M4 道路
    ///   MapApi          : 对外 JSON 接口
    /// </summary>
    public static class MapGenerator
    {
        /// <summary>
        /// 只生成地形(高度场 + 地格分类 + 顶点数据)。
        /// </summary>
        public static MapGenResult Generate(Render.MapRender map, MapGenConfig config)
        {
            return GenerateAll(map, config, null, null, null, null);
        }

        /// <summary>
        /// 生成地形，并可选地生成河流。
        /// </summary>
        public static MapGenResult GenerateAll(Render.MapRender map, MapGenConfig config, RiverGenConfig riverConfig)
        {
            return GenerateAll(map, config, riverConfig, null, null, null);
        }

        /// <summary>
        /// 生成地形、河流、城市与领地(不含道路)。
        /// </summary>
        public static MapGenResult GenerateAll(Render.MapRender map, MapGenConfig config,
            RiverGenConfig riverConfig, CityGenConfig cityConfig, TerritoryGenConfig territoryConfig)
        {
            return GenerateAll(map, config, riverConfig, cityConfig, territoryConfig, null);
        }

        /// <summary>
        /// 完整生成流程：地形 → 河流 → 城市 → 领地 → 道路。
        /// </summary>
        /// <param name="map">目标地图</param>
        /// <param name="config">地形参数</param>
        /// <param name="riverConfig">河流参数；为 null 时跳过</param>
        /// <param name="cityConfig">城市参数；为 null 时跳过(同时也会跳过领地与道路)</param>
        /// <param name="territoryConfig">领地参数；为 null 时使用默认值</param>
        /// <param name="roadConfig">道路参数；为 null 时跳过</param>
        /// <returns>生成结果</returns>
        public static MapGenResult GenerateAll(Render.MapRender map, MapGenConfig config,
            RiverGenConfig riverConfig, CityGenConfig cityConfig, TerritoryGenConfig territoryConfig,
            RoadGenConfig roadConfig)
        {
            MapGenResult result = new MapGenResult();

            if (map == null || map.mapData == null || map.mapGrid == null)
            {
                result.message = "生成失败：地图数据为空，请先加载或新建一张地图";
                Sango.Log.Error(result.message);
                return result;
            }

            long startTicks = System.DateTime.Now.Ticks;

            MapGenContext ctx = MapGenContext.Build(map, config);
            if (ctx == null)
            {
                result.message = "生成失败：地图尺寸无效或地形类型数据(TerrainTypes)未初始化";
                Sango.Log.Error(result.message);
                return result;
            }

            result.cellsX = ctx.cellsX;
            result.cellsY = ctx.cellsY;

            // 河流
            if (riverConfig != null)
            {
                RiverGenerator.Generate(ctx, riverConfig);
                result.hasRivers = true;
            }

            // 城市与领地
            if (cityConfig != null)
            {
                List<CitySite> sites = CityPlacer.Generate(ctx, cityConfig);
                TerritoryBuilder.Build(ctx, sites, territoryConfig ?? new TerritoryGenConfig
                {
                    interiorRadius = cityConfig.interiorRadius
                });
                result.cityCount = sites.Count;
                result.cities = sites;
            }

            // 道路
            if (roadConfig != null && ctx.cities != null && ctx.cities.Count >= 2)
            {
                result.roadCount = RoadGenerator.Generate(ctx, roadConfig);
                result.hasRoads = true;
            }

            return Finish(map, ctx, result, startTicks);
        }

        /// <summary>
        /// 在已有上下文之上只重新生成城市与领地。
        /// </summary>
        public static MapGenResult GenerateCitiesOnly(Render.MapRender map, CityGenConfig cityConfig,
            TerritoryGenConfig territoryConfig)
        {
            MapGenContext ctx = MapGenContext.Current;
            if (ctx == null || ctx.map != map)
            {
                MapGenResult failed = new MapGenResult { message = "生成失败：尚未构建生成上下文，请先执行地形生成" };
                Sango.Log.Error(failed.message);
                return failed;
            }

            long startTicks = System.DateTime.Now.Ticks;
            MapGenResult result = new MapGenResult { cellsX = ctx.cellsX, cellsY = ctx.cellsY };

            CityGenConfig cfg = cityConfig ?? new CityGenConfig();
            List<CitySite> sites = CityPlacer.Generate(ctx, cfg);
            TerritoryBuilder.Build(ctx, sites, territoryConfig ?? new TerritoryGenConfig
            {
                interiorRadius = cfg.interiorRadius
            });
            result.cityCount = sites.Count;
            result.cities = sites;

            return Finish(map, ctx, result, startTicks);
        }

        /// <summary>
        /// 在已有上下文之上只重新生成河流。
        /// </summary>
        public static MapGenResult GenerateRiversOnly(Render.MapRender map, RiverGenConfig riverConfig)
        {
            MapGenContext ctx = MapGenContext.Current;
            if (ctx == null || ctx.map != map)
            {
                MapGenResult failed = new MapGenResult { message = "生成失败：尚未构建生成上下文，请先执行地形生成" };
                Sango.Log.Error(failed.message);
                return failed;
            }

            long startTicks = System.DateTime.Now.Ticks;
            MapGenResult result = new MapGenResult { cellsX = ctx.cellsX, cellsY = ctx.cellsY };

            RiverGenerator.Generate(ctx, riverConfig ?? new RiverGenConfig());
            result.hasRivers = true;

            return Finish(map, ctx, result, startTicks);
        }

        /// <summary>
        /// 在已有上下文之上只重新生成道路。
        /// </summary>
        public static MapGenResult GenerateRoadsOnly(Render.MapRender map, RoadGenConfig roadConfig)
        {
            MapGenContext ctx = MapGenContext.Current;
            if (ctx == null || ctx.map != map)
            {
                MapGenResult failed = new MapGenResult { message = "生成失败：尚未构建生成上下文，请先执行地形生成" };
                Sango.Log.Error(failed.message);
                return failed;
            }

            if (ctx.cities == null || ctx.cities.Count < 2)
            {
                MapGenResult failed = new MapGenResult { message = "生成失败：尚未生成城市，无法铺设道路" };
                Sango.Log.Error(failed.message);
                return failed;
            }

            long startTicks = System.DateTime.Now.Ticks;
            MapGenResult result = new MapGenResult { cellsX = ctx.cellsX, cellsY = ctx.cellsY };

            result.roadCount = RoadGenerator.Generate(ctx, roadConfig ?? new RoadGenConfig());
            result.hasRoads = true;
            result.cityCount = ctx.cities.Count;
            result.cities = ctx.cities;

            return Finish(map, ctx, result, startTicks);
        }

        /// <summary>
        /// 收尾：写回引擎、统计数据并组装结果。
        /// </summary>
        private static MapGenResult Finish(Render.MapRender map, MapGenContext ctx, MapGenResult result, long startTicks)
        {
            ctx.RebuildTerrainCounts();
            ctx.ApplyCellTerrain();
            ctx.ApplyVertexData();

            // 底图(逐顶点底色图)：必须在顶点数据写回之后，因为它依赖地形与高度
            if (ctx.config != null && ctx.config.generateBaseMap)
                result.hasBaseMap = BaseMapGenerator.Generate(ctx) > 0;

            ctx.RefreshGrid();

            result.terrainCounts = ctx.terrainCounts;
            result.success = true;
            result.elapsedMs = (System.DateTime.Now.Ticks - startTicks) / 10000;

            string extra = "";
            if (result.hasRivers) extra += "含河流";
            if (result.cityCount > 0) extra += (extra.Length > 0 ? "、" : "") + "含城市";
            if (result.roadCount > 0) extra += (extra.Length > 0 ? "、" : "") + "含道路";
            result.message = string.Format("地图生成完成：{0}x{1} 格{2}，耗时 {3} 毫秒",
                ctx.cellsX, ctx.cellsY,
                extra.Length > 0 ? "(" + extra + ")" : "",
                result.elapsedMs);

            Sango.Log.Info(result.message);
            return result;
        }
    }
}
