using UnityEngine;

namespace Sango.MapGen
{
    /// <summary>
    /// 四季地表贴图层的初始化工具。
    ///
    /// 原版四季机制实测结论：
    ///   * 每个图层（<c>MapLayer.LayerData</c>）都持有 4 套贴图：diffuse / normal / mask 各 4 个，
    ///     分别对应 <c>MapRender.SeasonNames = { "Autumn", "Spring", "Summer", "Winter" }</c>，
    ///     即 **索引 0=秋、1=春、2=夏、3=冬**（不是时序，务必按此顺序）。
    ///   * 同一图层在四季中用**同一个文件名**，但**贴图内容不同**：
    ///     DefaultMap 实测 37 张地表贴图中 35 张四季不同，只有 water_0 与 layer_35 四季相同。
    ///   * 四季贴图尺寸必须一致（平铺系数 textureScale 由贴图尺寸算出）。
    ///   * 贴图路径：<c>Assets/Map/{WorkContent}/Terrain/{SeasonName}/{layer_N}.png</c>，
    ///     找不到时 <c>CreateTexture</c> 会回退到 <c>Assets/Map/Default/...</c>。
    ///   * 季节切换由 <c>MapProperty.Init()</c> 订阅 <c>map.onSeasonChange</c> 自动完成：
    ///     设置 <c>map.curSeason</c> 后，各图层的材质会自动切到该季节的 diffuse/normal/mask，
    ///     底图（MapBaseColor）也会切到该季节的 _BaseTex。
    ///   * **水面层恒为最后一层**（layerDatas.Length - 1），贴图名固定为 water_0。
    ///
    /// 本工具解决的实际问题：新建地图时 <c>MapLayer</c> 只有 2 层且贴图名为空，
    /// 地表会渲染成白板。这里把层数与四季贴图名初始化好（自动回退到 Default 目录的
    /// 原版四季贴图），使程序化生成的新地图一落地就有四季地表纹理。
    /// </summary>
    public static class SeasonLayerSetup
    {
        /// <summary>默认地表图层数(与原版 Terrain 目录的 layer_0..layer_35 一致)</summary>
        public const int DefaultTerrainLayerCount = 36;

        /// <summary>四季名称，顺序必须与 MapRender.SeasonNames 一致(0=秋,1=春,2=夏,3=冬)</summary>
        public static readonly string[] SeasonNames = { "Autumn", "Spring", "Summer", "Winter" };

        /// <summary>季节数量</summary>
        public const int SeasonCount = 4;

        /// <summary>水面层使用的贴图名</summary>
        private const string WaterTextureName = "water_0";

        /// <summary>
        /// 初始化地图的四季地表贴图层。
        /// </summary>
        /// <param name="map">目标地图</param>
        /// <param name="terrainLayerCount">地表图层数(不含水面层)，默认 36</param>
        /// <param name="loadTextures">是否立即加载四季贴图(会回退到 Default 目录)</param>
        /// <returns>实际处理的图层总数(含水面层)；失败返回 0</returns>
        public static int Setup(Render.MapRender map, int terrainLayerCount, bool loadTextures)
        {
            if (map == null || map.mapLayer == null || map.mapLayer.layerDatas == null)
            {
                Sango.Log.Error("四季贴图层初始化失败：地图图层数据不存在");
                return 0;
            }

            Render.MapLayer mapLayer = map.mapLayer;
            int target = Mathf.Clamp(terrainLayerCount, 1, 120);
            int want = target + 1; // 加上最后一层水面层

            // AddLayer 会把新层插在倒数第二位，水面层始终保持最后
            int guard = 0;
            while (mapLayer.layerDatas.Length < want && guard++ < 256)
                mapLayer.AddLayer();

            Render.MapLayer.LayerData[] layers = mapLayer.layerDatas;
            int processed = 0;
            Shader waterShader = Shader.Find("Sango/water_urp");

            for (int i = 0; i < layers.Length; i++)
            {
                Render.MapLayer.LayerData layerData = layers[i];
                if (layerData == null)
                    continue;

                bool isWater = (i == layers.Length - 1);
                string textureName = isWater ? WaterTextureName : ("layer_" + i);

                // 四季使用同一个文件名（内容由各赛季的 png 提供）
                for (int s = 0; s < SeasonCount; s++)
                    layerData.diffuseTexName[s] = textureName;

                // 水面层换上水面着色器
                if (isWater && waterShader != null && layerData.material != null)
                    layerData.material.shader = waterShader;

                // 立即加载四季贴图（CreateTexture 会自动回退到 Assets/Map/Default）
                if (loadTextures)
                    layerData.AutoLoadDiffuse();

                processed++;
            }

            // 重新排定渲染队列并刷新当前季节材质
            mapLayer.UpdateLayerRenderQueue();
            mapLayer.UpdateRender();

            Sango.Log.Info(string.Format("四季贴图层初始化完成：共 {0} 层(含水面层)，当前季节 {1}",
                processed, SeasonNames[Mathf.Abs(map.curSeason) % SeasonCount]));
            return processed;
        }

        /// <summary>
        /// 按地图当前可用图层数初始化（不改变层数），仅补齐四季贴图名并加载。
        /// </summary>
        /// <param name="map">目标地图</param>
        /// <returns>实际处理的图层总数；失败返回 0</returns>
        public static int SetupExisting(Render.MapRender map)
        {
            if (map == null || map.mapLayer == null || map.mapLayer.layerDatas == null)
                return 0;

            int terrainLayers = Mathf.Max(1, map.mapLayer.layerDatas.Length - 1);
            return Setup(map, terrainLayers, true);
        }
    }
}
