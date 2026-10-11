namespace Sango.MapGen
{
    /// <summary>
    /// 地形类型 → 地表贴图图层(layerDatas 下标)的映射表。
    ///
    /// 数据来源：实测原版 DefaultMap.bin 的"地形 → 顶点 textureIndex"分布，取各地形的主导图层。
    /// 原版实测结论（占比为该图层在该地形中的比例）：
    ///   川/河/海 → L0(93%~100%)，浅滩 → L5(100%)，岸 → L2(89%)，
    ///   砂地 → L3(61%)/L4(37%)，湿地 → L24(82%)，森 → L30(88%)，草地 → L29(70%)/L15(20%)，
    ///   土 → L27(58%)/L13(35%)，荒地 → L21(45%)/L30(30%)，
    ///   主径/栈道 → L34(69%~83%)，渡所 → L34(39%)，
    ///   都市 → L28(39%)/L27(28%)，关所 → L27(60%)，小径 → L20(66%)，
    ///   崖 → 图层混杂(L18/L20/L6/L8…，美术按岩石形态手绘，这里取众数 L18)。
    ///
    /// 注意：该表取值基于原版 49 层的地图。若目标地图的图层数更少，
    /// 使用方必须把结果钳制到 [0, layerDatas.Length - 2]（最后一层恒为水面层）。
    /// </summary>
    public static class TerrainLayerMap
    {
        /// <summary>按地形ID索引的原版主导图层表</summary>
        private static readonly int[] Original = BuildOriginal();

        /// <summary>构造原版主导图层表</summary>
        private static int[] BuildOriginal()
        {
            int[] map = new int[TerrainIds.Count];

            map[TerrainIds.None] = 0;
            map[TerrainIds.Grass] = 29;
            map[TerrainIds.Soil] = 27;
            map[TerrainIds.Sand] = 3;
            map[TerrainIds.Marsh] = 24;
            map[TerrainIds.Poison] = 1;
            map[TerrainIds.Forest] = 30;
            map[TerrainIds.Stream] = 0;
            map[TerrainIds.River] = 0;
            map[TerrainIds.Sea] = 0;
            map[TerrainIds.Waste] = 21;
            map[TerrainIds.RoadMain] = 34;
            map[TerrainIds.Plank] = 34;
            map[TerrainIds.Ford] = 34;
            map[TerrainIds.Shallow] = 5;
            map[TerrainIds.Shore] = 2;
            map[TerrainIds.Cliff] = 18;
            map[TerrainIds.City] = 28;
            map[TerrainIds.Port] = 3;
            map[TerrainIds.Gate] = 27;
            map[TerrainIds.PathSmall] = 20;

            return map;
        }

        /// <summary>
        /// 崖(山地)的候选贴图图层调色板。
        /// 原版实测崖的图层是**混杂**的：L18 16%、L20 13%、L6 13%、L8 12%、L22/L23 若干，
        /// 即美术按岩石形态用多张贴图拼贴。这里给出该调色板，
        /// 由生成器按"坡度分档 + 噪声"在其间选取，避免山体呈现单一贴图的塑料感。
        /// 排列顺序：陡坡岩壁(0,1) / 中坡碎石(2,3) / 缓坡土石(4,5)。
        /// </summary>
        private static readonly int[] CliffPalette = { 18, 8, 20, 6, 22, 23 };

        /// <summary>
        /// 取崖的候选贴图图层调色板（调用方需按目标地图的可用图层数过滤）。
        /// </summary>
        /// <returns>长度为 6 的图层下标数组</returns>
        public static int[] GetCliffPalette()
        {
            return CliffPalette;
        }

        /// <summary>
        /// 取某地形在原版地图中的主导贴图图层。
        /// </summary>
        /// <param name="terrainId">地形类型ID</param>
        /// <returns>图层下标(未定义的地形返回 0)</returns>
        public static int Get(int terrainId)
        {
            if (terrainId >= 0 && terrainId < Original.Length)
                return Original[terrainId];
            return 0;
        }
    }
}
