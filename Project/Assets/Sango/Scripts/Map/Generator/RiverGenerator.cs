using UnityEngine;
using System;

namespace Sango.MapGen
{
    /// <summary>
    /// 河流生成参数。
    /// </summary>
    public class RiverGenConfig
    {
        /// <summary>形成河流所需的最小汇流量(单位：格)。越大河流越少越粗。</summary>
        public int flowThreshold = 14;
        /// <summary>加宽大江的汇流倍数：汇流量 ≥ flowThreshold * 该值 的河流加宽一格。</summary>
        public int widenFlowFactor = 4;
        /// <summary>
        /// 大江判定倍数：汇流量 ≥ flowThreshold * 该值 的河段使用「河」(大江)地形，其余使用「川」(小河)。
        /// 标定自原版地图：原版同时存在「川」(小河)与「河」(大江)两种水道地形。
        /// 设为 0 或 1 时全部使用「川」。
        /// </summary>
        public int majorRiverFactor = 8;
        /// <summary>是否允许在内流盆地生成湖泊。</summary>
        public bool allowLake = true;
        /// <summary>形成湖泊所需的最小汇流量(单位：格)。</summary>
        public int lakeThreshold = 45;
    }

    /// <summary>
    /// 河流生成器(M2)。
    ///
    /// 算法：基于地格高度场的"汇流累积"模型：
    ///   1. 每个非水域地格初始汇流量为 1(降雨)；
    ///   2. 按高度从高到低处理，把当前地格的汇流量累加到"最低邻居"；
    ///   3. 汇流量达到阈值的地格判定为河道：汇流量大的为大江(河)，其余为小河(川)；
    ///      大江再按汇流倍数加宽；
    ///   4. 内流盆地(没有更低邻居且有较大汇流量)判定为湖泊。
    ///
    /// 邻居方向由 HexNeighbors 提供，与引擎逻辑一致。
    /// </summary>
    public static class RiverGenerator
    {
        /// <summary>
        /// 在给定上下文上生成河流，直接修改 ctx.cellTerrain。
        /// </summary>
        /// <param name="ctx">生成上下文</param>
        /// <param name="config">河流参数</param>
        /// <returns>生成的河流地格数量</returns>
        public static int Generate(MapGenContext ctx, RiverGenConfig config)
        {
            if (ctx == null || ctx.cellTerrain == null)
                return 0;

            RiverGenConfig cfg = config ?? new RiverGenConfig();
            int cellsX = ctx.cellsX;
            int cellsY = ctx.cellsY;
            int total = cellsX * cellsY;
            if (total <= 0)
                return 0;

            float[] height = ctx.cellHeight;
            byte[,] terrain = ctx.cellTerrain;

            // 记录生成前的水域状态(汇流的"终点")
            bool[] isWater = new bool[total];
            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    int index = cx * cellsY + cy;
                    isWater[index] = MapGenContext.IsWaterTerrain(terrain[cx, cy]);
                }
            }

            // 1. 按高度降序排序
            int[] order = new int[total];
            for (int i = 0; i < total; i++)
                order[i] = i;
            Array.Sort(order, (a, b) => height[b].CompareTo(height[a]));

            // 2. 汇流累积
            int[] flow = new int[total];
            int[] downstream = new int[total];
            for (int i = 0; i < total; i++)
            {
                flow[i] = 1;
                downstream[i] = -1;
            }

            for (int k = 0; k < total; k++)
            {
                int index = order[k];
                if (isWater[index])
                    continue;

                int cx = index / cellsY;
                int cy = index % cellsY;

                float lowest = height[index];
                int lowestIndex = -1;

                for (int d = 0; d < HexNeighbors.DirectionCount; d++)
                {
                    int neighborIndex = HexNeighbors.GetNeighbor(cx, cy, d, cellsX, cellsY, out int nx, out int ny);
                    if (neighborIndex < 0)
                        continue;

                    if (height[neighborIndex] < lowest)
                    {
                        lowest = height[neighborIndex];
                        lowestIndex = neighborIndex;
                    }
                }

                if (lowestIndex >= 0)
                {
                    flow[lowestIndex] += flow[index];
                    downstream[index] = lowestIndex;
                }
            }

            // 3. 生成河流(不覆盖山地，保留山脉屏障)
            int riverCount = 0;
            for (int k = 0; k < total; k++)
            {
                int index = order[k];
                if (isWater[index])
                    continue;

                int cx = index / cellsY;
                int cy = index % cellsY;
                if (terrain[cx, cy] == TerrainIds.Cliff)
                    continue;

                if (flow[index] >= cfg.flowThreshold)
                {
                    terrain[cx, cy] = (byte)RiverTerrainFor(flow[index], cfg);
                    riverCount++;
                }
            }

            // 4. 加宽大江：汇流量足够大的河段，把其最低的陆上邻居也变成河道
            if (cfg.widenFlowFactor > 0)
            {
                int widenThreshold = cfg.flowThreshold * cfg.widenFlowFactor;
                for (int k = 0; k < total; k++)
                {
                    int index = order[k];
                    int cx = index / cellsY;
                    int cy = index % cellsY;
                    if (terrain[cx, cy] != TerrainIds.Stream && terrain[cx, cy] != TerrainIds.River)
                        continue;
                    if (flow[index] < widenThreshold)
                        continue;

                    float lowest = float.MaxValue;
                    int lowestIndex = -1;
                    for (int d = 0; d < HexNeighbors.DirectionCount; d++)
                    {
                        int neighborIndex = HexNeighbors.GetNeighbor(cx, cy, d, cellsX, cellsY, out int nx, out int ny);
                        if (neighborIndex < 0)
                            continue;
                        if (terrain[nx, ny] == TerrainIds.Cliff || MapGenContext.IsWaterTerrain(terrain[nx, ny]))
                            continue;

                        if (height[neighborIndex] < lowest)
                        {
                            lowest = height[neighborIndex];
                            lowestIndex = neighborIndex;
                        }
                    }

                    if (lowestIndex >= 0)
                    {
                        int nx = lowestIndex / cellsY;
                        int ny = lowestIndex % cellsY;
                        // 加宽段与来源河段同级(大江加宽后仍为大江)
                        terrain[nx, ny] = terrain[cx, cy];
                        riverCount++;
                    }
                }
            }

            // 5. 内流盆地生成湖泊
            if (cfg.allowLake)
            {
                for (int index = 0; index < total; index++)
                {
                    if (isWater[index] || downstream[index] >= 0)
                        continue;
                    if (flow[index] < cfg.lakeThreshold)
                        continue;

                    int cx = index / cellsY;
                    int cy = index % cellsY;
                    if (terrain[cx, cy] == TerrainIds.Cliff)
                        continue;

                    terrain[cx, cy] = (byte)TerrainIds.Stream;
                    riverCount++;
                }
            }

            Sango.Log.Info(string.Format("河流生成：共 {0} 个地格变为河道", riverCount));
            return riverCount;
        }

        /// <summary>
        /// 按汇流量选择水道地形：汇流量大的河段判定为大江(河)，其余为小河(川)。
        /// 两者在顶点高度上的差别仅由地形ID区分，水位标定共用同一套规则。
        /// </summary>
        /// <param name="flow">该地格的汇流量</param>
        /// <param name="cfg">河流参数</param>
        /// <returns>地形类型ID(川或河)</returns>
        private static int RiverTerrainFor(int flow, RiverGenConfig cfg)
        {
            int factor = cfg.majorRiverFactor;
            if (factor <= 1)
                return TerrainIds.Stream;

            int majorFlow = cfg.flowThreshold * factor;
            return flow >= majorFlow ? TerrainIds.River : TerrainIds.Stream;
        }
    }
}
