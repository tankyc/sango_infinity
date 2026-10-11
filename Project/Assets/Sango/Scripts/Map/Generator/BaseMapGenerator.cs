using UnityEngine;

namespace Sango.MapGen
{
    /// <summary>
    /// 底图(BaseTex)生成器 —— 支持四季差异。
    ///
    /// 原版机制实测结论：
    ///   * 底图存放在 Assets/Map/{WorkContent}/BaseTex/BaseMap0..3.png，
    ///     四张**内容各不相同**，分别对应
    ///     MapRender.SeasonNames = { "Autumn", "Spring", "Summer", "Winter" }，
    ///     即 **索引 0=秋、1=春、2=夏、3=冬**（不是时序！文件名后缀就是季节索引）。
    ///   * 尺寸为 vertex_width × vertex_height（原版 1024×1024），
    ///     即"每个顶点一个像素"的底色图，本质就是顶点色。
    ///   * 着色器按世界坐标取 UV：u = worldZ / _MapWidth，v = 1 - worldX / _MapHeight，
    ///     其中 _MapWidth = vertex_width * quadSize、_MapHeight = vertex_height * quadSize。
    ///   * 片元里以 _BaseColorIntensity(默认 0.85) 与地形贴图相乘：
    ///     lerp(diffuse, baseColor * diffuse, 0.85)。底图负责整体色调，逐图层贴图负责细节。
    ///   * 季节切换由 MapProperty 订阅 map.onSeasonChange 自动完成，这里只需按季节写好 4 张图。
    ///
    /// 因此本生成器按季节生成 4 张逐顶点底色图：
    ///   春(嫩绿) / 夏(浓绿) / 秋(黄褐) / 冬(去饱和 + 雪线变白)。
    /// 底色以"接近白色 + 轻微地形色调"为主，避免被 0.85 的强混合压暗。
    /// </summary>
    public static class BaseMapGenerator
    {
        /// <summary>季节数量（0=秋,1=春,2=夏,3=冬）</summary>
        private const int SeasonCount = SeasonLayerSetup.SeasonCount;

        /// <summary>基底白度：越接近 1 越不会压暗地形贴图</summary>
        private const float BaseWhite = 0.95f;

        /// <summary>季节索引常量（与 MapRender.SeasonNames 顺序一致）</summary>
        private const int SeasonAutumn = 0;
        private const int SeasonSpring = 1;
        private const int SeasonSummer = 2;
        private const int SeasonWinter = 3;

        /// <summary>季节色调参数</summary>
        private struct SeasonStyle
        {
            /// <summary>整体色调乘子</summary>
            public Color tint;
            /// <summary>去饱和强度(0~1)，冬季较大</summary>
            public float desaturate;
            /// <summary>雪线起始相对海拔(0~1)；&lt;=0 表示不积雪</summary>
            public float snowStart;
        }

        /// <summary>四季风格(索引与 MapRender.SeasonNames 一致)</summary>
        private static readonly SeasonStyle[] Styles = new SeasonStyle[SeasonCount]
        {
            // 0 秋：偏黄褐
            new SeasonStyle { tint = new Color(1.00f, 0.93f, 0.72f), desaturate = 0.12f, snowStart = 0f },
            // 1 春：偏嫩绿、偏亮
            new SeasonStyle { tint = new Color(0.94f, 1.00f, 0.90f), desaturate = 0.00f, snowStart = 0f },
            // 2 夏：浓绿、略暗
            new SeasonStyle { tint = new Color(0.86f, 0.97f, 0.84f), desaturate = 0.00f, snowStart = 0f },
            // 3 冬：去饱和 + 高海拔积雪
            new SeasonStyle { tint = new Color(0.94f, 0.96f, 1.00f), desaturate = 0.55f, snowStart = 0.55f },
        };

        /// <summary>各地形在地图底图上的代表色</summary>
        private static readonly Color32[] TerrainColor = BuildTerrainColor();

        private static Color32[] BuildTerrainColor()
        {
            Color32[] c = new Color32[TerrainIds.Count];
            for (int i = 0; i < c.Length; i++)
                c[i] = new Color32(150, 150, 150, 255);

            c[TerrainIds.None] = new Color32(120, 120, 120, 255);
            c[TerrainIds.Grass] = new Color32(110, 170, 90, 255);
            c[TerrainIds.Soil] = new Color32(150, 120, 80, 255);
            c[TerrainIds.Sand] = new Color32(200, 185, 140, 255);
            c[TerrainIds.Marsh] = new Color32(110, 140, 110, 255);
            c[TerrainIds.Poison] = new Color32(140, 90, 160, 255);
            c[TerrainIds.Forest] = new Color32(70, 120, 70, 255);
            c[TerrainIds.Stream] = new Color32(90, 140, 185, 255);
            c[TerrainIds.River] = new Color32(90, 140, 185, 255);
            c[TerrainIds.Sea] = new Color32(70, 115, 160, 255);
            c[TerrainIds.Waste] = new Color32(150, 140, 110, 255);
            c[TerrainIds.RoadMain] = new Color32(190, 175, 140, 255);
            c[TerrainIds.Plank] = new Color32(150, 130, 100, 255);
            c[TerrainIds.Ford] = new Color32(140, 130, 110, 255);
            c[TerrainIds.Shallow] = new Color32(120, 165, 175, 255);
            c[TerrainIds.Shore] = new Color32(170, 155, 120, 255);
            c[TerrainIds.Cliff] = new Color32(135, 130, 125, 255);
            c[TerrainIds.City] = new Color32(185, 160, 110, 255);
            c[TerrainIds.Port] = new Color32(150, 140, 120, 255);
            c[TerrainIds.Gate] = new Color32(160, 150, 110, 255);
            c[TerrainIds.PathSmall] = new Color32(175, 160, 130, 255);
            // 远海/深海：与近海(9)同色相、逐级加深，形成水深梯度
            c[TerrainIds.FarSea] = new Color32(48, 86, 130, 255);
            c[TerrainIds.DeepSea] = new Color32(28, 56, 96, 255);
            return c;
        }

        /// <summary>
        /// 依据生成上下文生成四季底图并落盘，随后立即生效。
        /// </summary>
        /// <param name="ctx">生成上下文</param>
        /// <returns>成功写出的文件数量（0 表示未生成）</returns>
        public static int Generate(MapGenContext ctx)
        {
            if (ctx == null || ctx.map == null || ctx.map.mapData == null)
                return 0;
            if (ctx.map.mapBaseColor == null)
            {
                Sango.Log.Warning("底图生成跳过：地图底图模块(MapBaseColor)不存在");
                return 0;
            }

            int width = ctx.map.mapData.vertex_width;
            int height = ctx.map.mapData.vertex_height;
            if (width <= 0 || height <= 0)
                return 0;

            bool seasonal = ctx.config == null || ctx.config.baseMapSeasonal;

            Texture2D[] textures = new Texture2D[SeasonCount];
            if (seasonal)
            {
                for (int s = 0; s < SeasonCount; s++)
                    textures[s] = BuildTexture(ctx, width, height, s);
            }
            else
            {
                // 不区分季节：四张用同一张图
                Texture2D single = BuildTexture(ctx, width, height, SeasonSpring);
                for (int s = 0; s < SeasonCount; s++)
                    textures[s] = single;
            }

            int written = WriteFiles(ctx, textures);

            // 立即生效：四季底图交给 MapBaseColor，由季节切换自动选用
            for (int s = 0; s < SeasonCount; s++)
                ctx.map.mapBaseColor.texture[s] = textures[s];
            ctx.map.mapBaseColor.UpdateRender();

            Sango.Log.Info(string.Format("底图生成完成：{0}x{1}，{2}，写出 {3} 个季节文件",
                width, height, seasonal ? "四季差异" : "四季相同", written));
            return written;
        }

        /// <summary>
        /// 逐顶点计算某个季节的底色并生成 Texture2D。
        /// 像素排布与原版一致：像素 X = 顶点 X，像素 Y = (height-1) - 顶点 Y
        /// （对应着色器 v = 1 - worldX / _MapHeight）。
        /// </summary>
        private static Texture2D BuildTexture(MapGenContext ctx, int width, int height, int season)
        {
            Color32[] pixels = new Color32[width * height];
            int vxMax = ctx.vertexXMax;
            int vyMax = ctx.vertexYMax;

            MapGenConfig cfg = ctx.config;
            float tint = Mathf.Clamp01(cfg != null ? cfg.baseMapTint : 0.35f);
            float shade = Mathf.Clamp01(cfg != null ? cfg.baseMapShade : 0.25f);

            SeasonStyle style = Styles[Mathf.Clamp(season, 0, SeasonCount - 1)];
            Color baseWhite = new Color(BaseWhite, BaseWhite, BaseWhite, 1f);

            for (int vx = 0; vx < vxMax; vx++)
            {
                int px = Mathf.Clamp(vx, 0, width - 1);
                int cx = Mathf.Clamp(vx / 4, 0, ctx.cellsX - 1);

                for (int vy = 0; vy < vyMax; vy++)
                {
                    int py = Mathf.Clamp((height - 1) - vy, 0, height - 1);

                    int cy = Mathf.Clamp((vy - (cx & 1) * 2) / 4, 0, ctx.cellsY - 1);
                    int terrain = ctx.cellTerrain[cx, cy];

                    Color32 tc = (terrain >= 0 && terrain < TerrainColor.Length)
                        ? TerrainColor[terrain]
                        : TerrainColor[0];

                    // 1) 地形色调只占一小部分，其余保持接近白色
                    Color color = Color.Lerp(baseWhite,
                        new Color(tc.r / 255f, tc.g / 255f, tc.b / 255f, 1f), tint);

                    // 2) 季节整体色调
                    color = new Color(color.r * style.tint.r, color.g * style.tint.g, color.b * style.tint.b, 1f);

                    // 3) 季节性去饱和（冬季）
                    if (style.desaturate > 0f)
                    {
                        float luma = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
                        color = Color.Lerp(color, new Color(luma, luma, luma, 1f), style.desaturate);
                    }

                    // 4) 高度明暗：低地略暗、高地略亮
                    float altitude = 0f;
                    if (ctx.thresholds.seaLevel < 1f)
                    {
                        float h = ctx.heightField[ctx.VertexIndex(vx, vy)];
                        altitude = Mathf.InverseLerp(ctx.thresholds.seaLevel, 1f, h);
                    }
                    color *= Mathf.Lerp(1f - shade, 1f, altitude);

                    // 5) 冬季雪线：高海拔渐变为雪白
                    if (style.snowStart > 0f && altitude > style.snowStart)
                    {
                        float t = Mathf.InverseLerp(style.snowStart, 1f, altitude);
                        t = t * t * (3f - 2f * t); // smoothstep
                        color = Color.Lerp(color, Color.white, t);
                    }

                    pixels[py * width + px] = color;
                }
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.name = "GeneratedBaseMap_" + SeasonLayerSetup.SeasonNames[Mathf.Clamp(season, 0, SeasonCount - 1)];
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// 把四季底图写成 png：Assets/Map/{WorkContent}/BaseTex/BaseMap{i}.png
        /// （i 即季节索引：0=秋,1=春,2=夏,3=冬）
        /// </summary>
        private static int WriteFiles(MapGenContext ctx, Texture2D[] textures)
        {
            string workContent = string.IsNullOrEmpty(ctx.map.WorkContent) ? "Default" : ctx.map.WorkContent;
            string root = Sango.Path.ContentRootPath;
            if (string.IsNullOrEmpty(root))
                return 0;

            string dir = root + "/Assets/Map/" + workContent + "/BaseTex";
            try
            {
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                int written = 0;
                for (int i = 0; i < SeasonCount; i++)
                {
                    if (textures[i] == null)
                        continue;
                    byte[] bytes = textures[i].EncodeToPNG();
                    if (bytes == null)
                        continue;
                    System.IO.File.WriteAllBytes(dir + "/BaseMap" + i + ".png", bytes);
                    written++;
                }
                return written;
            }
            catch (System.Exception e)
            {
                Sango.Log.Error("底图写出失败：" + e.Message);
                return 0;
            }
        }
    }
}
