// Sango 远景水面着色器（MapCell 改造版）。
//
// 【用途】远景（TerrainCellBatchRenderer）时替代 CPU 水面网格：
//   全图一张平面模板网格（顶点在 [0,1]²、Y 恒 0，由 TerrainCellBatchRenderer 构建），
//   一次 DrawMesh 提交。顶点着色器由矩阵反推全图世界范围，按 R8 水面高度纹理
//   （MapCellTerrainAsset.WaterHeightTex，1 texel = 1 顶点格）抬升水面。
//
// 【非水区域剔除】水面高度 byte = 0 的 texel 表示陆地，片元里 clip 掉 ——
//   否则低洼陆地（height = 0）上方会出现平面水位（y = 0）被深度测试放过、染上水色。
//
// 【视觉口径】照抄旧管线 water.shader / sango_shaderLib：
//   · 序列帧动画 uv（SangoWaterTransofromUV，sango_shaderLib:219-236）；
//   · _Color 染色（SANGO_COLOR，sango_shaderLib:499-501）；
//   · alpha = _Alpha * _BaseMap.a（远景无顶点色，岸线渐变交给近景 CPU 水网格）；
//   · 雾（sango_shaderLib:527-536）。
//
// 【原版无对应实现】Open311 无独立水面 Instanced 渲染，此为渲染层自主设计。
Shader "Sango/terrain_cell_water"
{
    Properties
    {
        _BaseMap("MainTex", 2D) = "white" {}
        _WaterHeightTex ("Water Height (R8)", 2D) = "black" {}

        _TexSize ("Tex Size (vertexW, vertexH)", Vector) = (1024, 1024, 0, 0)
        _QuadSize ("Quad Size (world/vertex)", Float) = 5
        _HeightRange ("Height Range", Float) = 127.5
        _Alpha("alpha", float) = 0.9
        _HorizontalAmount("Horizontal Amount", Float) = 8
        _VerticalAmount("Vertical Amount", Float) = 8
        _Color("Water Color", Color) = (0.45, 0.5, 0.44, 1)
        _Speed("Speed", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Lighting Off
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }

            // 【与近景 water_urp 对齐】旧管线 water.shader 没有声明 ZWrite，即默认 ZWrite On；
            //   远景这里原来写死 ZWrite Off，导致远近切换时"水下地形能不能透出来"观感不同
            //   （远景看起来更透）。非水区域已由 frag 的 clip 剔除，不会写深度，因此
            //   改回默认 ZWrite On 不会让陆地被水面网格挡住。
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual
            // 水面与地形现在共用同一张模板网格：水域里 water 可能正好等于 height，
            // 两者几何完全共面会 Z-Fighting（水面闪烁/被地形吃掉）。
            // 用深度偏移把水面判为"更近"，而不是抬高顶点 —— 抬高会让水面相对近景整体上浮。
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma skip_variants FOG_EXP FOG_EXP2

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _TexSize;
                float _QuadSize;
                float _HeightRange;
                float _Alpha;
                float _HorizontalAmount;
                float _VerticalAmount;
                float _Speed;
                float4 _Color;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_WaterHeightTex);
            SAMPLER(sampler_WaterHeightTex);
            // 线性 + Clamp（与纹理自身的 Point 过滤解耦），用于顶点高度与岸线 alpha 的平滑采样。
            // URP 内置的 sampler_LinearClamp 在本工程的 include 组合里取不到，这里自己声明一份。
            // 【命名规则】Unity 对 inline sampler 按名字解析过滤/寻址模式，名字里必须同时含
            //   filter（Point/Linear/Trilinear）与 wrap（Clamp/Repeat/Mirror），否则报
            //   "Unrecognized sampler ... does not match any texture"。
            SAMPLER(sampler_HeightLinearClamp);

            // 近景水面颜色受主光/环境光调制（sango_shaderLib:339-368），远景必须同口径，
            // 否则远近视切换时水面会"突然变亮/变灰"。_ShadowColor 由 MapLight 全局写入。
            float4 _ShadowColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 posWorld : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // 与地形同一个模板网格、同一批实例矩阵：UNITY_SETUP_INSTANCE_ID 之后
                // TransformObjectToWorld 才会应用当前实例的矩阵（平移 = 块原点、缩放 = 块世界边长）。
                // 不做这一步的话所有实例都会叠在原点，水面只剩一块。
                UNITY_SETUP_INSTANCE_ID(input);

                // 矩阵只编码平移与缩放：原点 = M*(0,0,0)，边长 = |M*(1,0,0) - 原点|
                float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 axisX = TransformObjectToWorld(float3(1.0, 0.0, 0.0)) - originWS;
                float mapSize = max(length(axisX), 1e-3);

                float2 worldXZ = originWS.xz + input.positionOS.xz * mapSize;

                // 【水面顶点高度 = 水面高度本身，不加任何抬升】
                //   水面网格现在与地形同模板、同实例矩阵（每块 33×33），顶点密度足够表达水陆边界；
                //   水面高度直接取 water * 0.5 就能与近景 CPU 水面网格（MapData.VertexWaterPosition）
                //   完全同高，远近视切换不会出现"水面突然抬升一点"。
                //   共面 Z-Fighting 交给 Pass 里的 Offset 处理，不动几何高度。
                //
                //   采样用**连续 uv + 线性**：最近 texel 采样会让顶点在水陆之间 0/100 突变，
                //   三角形斜插进地形被深度测试剔掉（水体边缘碎裂）。
                //   顶点索引 → texel 中心：uv = (vx + 0.5) / texW，与片元 maskUV 公式一致，
                //   否则大坐标处会整体偏 1 个 texel。
                float2 gridUV = (float2(worldXZ.y, worldXZ.x) / max(_QuadSize, 1e-4) + 0.5)
                    / max(_TexSize.xy, 1.0);
                float waterHeight = SAMPLE_TEXTURE2D_LOD(_WaterHeightTex, sampler_HeightLinearClamp, gridUV, 0).r * _HeightRange;

                output.posWorld = float3(worldXZ.x, waterHeight, worldXZ.y);
                output.positionCS = TransformWorldToHClip(output.posWorld);

                // 【uv 必须与近景 CPU 水面网格同口径】
                //   近景水面网格的 uv = MapData.VertexUV(vx, vy)
                //     = (vx / vertex_width, vy / vertex_height)，
                //   其中 vx 沿世界 Z、vy 沿世界 X（顶点索引与世界坐标的映射见 MapData.VertexPosition）。
                // 旧写法 TRANSFORM_TEX(input.positionOS.xz) 有两个问题：
                //   ① 模板的 x 对应世界 X、z 对应世界 Z，而近景 uv 是 (Z, X) —— 轴序相反，
                //      水面纹理相当于被转置 90°，远近切换时纹理走向会突变；
                //   ② 归一化用 _BaseMap_ST（默认 1,1），等于"整张图铺一次"，与近景的
                //      vx/vertex_width 在非正方形地图上还对不齐。
                // _TexSize = (vertex_width + 1, vertex_height + 1)，故减 1 还原成顶点跨度。
                // 【tiling 必须跟随材质】近景水面网格的 uv = MapData.VertexUV(vx, vy) ∈ [0,1]，
                // 但 MapLayer 会对水材质调用 SetTextureScale("_BaseMap", textureScale)
                // （MapLayer.cs:76/154/181/325），shader 里的 TRANSFORM_TEX 再乘一次 tiling。
                // 也就是说近景最终 uv = VertexUV * textureScale，纹理实际平铺几十次；
                // 这里如果只给 VertexUV，整张图就只铺一次 —— 远景水面会被放大几十倍。
                // 统一走 TRANSFORM_TEX，让材质上的 _BaseMap_ST 与近景共用同一份 tiling。
                float2 mapUV = float2(worldXZ.y, worldXZ.x) / max((_TexSize.xy - 1.0) * _QuadSize, 1e-3);
                output.uv = TRANSFORM_TEX(mapUV, _BaseMap);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            /// 序列帧动画 uv（照抄 sango_shaderLib:219-236 的 SangoWaterTransofromUV）
            float2 WaterUV(float3 posWorld, float2 uv)
            {
                float c_add = floor((posWorld.z + posWorld.x) / (120) % 225);
                float time = floor(_Speed * _Time.y + c_add);
                float row = floor(time / _HorizontalAmount);
                float column = floor(time - row * _HorizontalAmount);

                float2 result = uv + half2(column, -row);
                result.x /= _HorizontalAmount;
                result.y /= _VerticalAmount;
                return result;
            }

            float4 frag(Varyings i) : SV_TARGET
            {
                // 非水区域剔除：water byte = 0 即陆地（数据口径：water == 0 表示没有水）
                // 这里用 Point 采样取"像素所属 texel"，保证水域轮廓是逐格精确的。
                float2 texel = float2(i.posWorld.z, i.posWorld.x) / max(_QuadSize, 1e-4);
                texel = floor(texel + 0.5);
                float2 maskUV = (texel + 0.5) / max(_TexSize.xy, 1.0);
                float waterMask = SAMPLE_TEXTURE2D_LOD(_WaterHeightTex, sampler_WaterHeightTex, maskUV, 0).r;
                clip(waterMask * 255.0 - 0.5);

                // 岸线 alpha 渐变：对齐近景水面网格的顶点色（water > 0 ? white : transparent）。
                //
                // 【只用 WaterHeightTex，不额外烘掩码】water 字段本身就是"有没有水"的真值：
                //   0 = 无水，非 0 = 水面高度。用连续 uv 做双线性采样得到邻域平均值 avg，
                //   再除以本格的 Point 值 self，就得到"邻域里有水的占比"（0~1）——
                //   水域内部 avg == self → 1；岸线上 avg 是自身与陆地(0)的加权 → 0~1 线性过渡，
                //   与近景顶点色在三角形内的插值同宽（都是 1 个顶点格）。
                // 【为什么不能直接用 avg 当 alpha】实测本图水域 water 平均只有 12（98% 在 0~31），
                //   avg 归一到 0~1 后只有 0.05 量级，直接当 alpha 会把整片水画成全透明。
                //   除法消掉了水域自身高度的量级差异，浅水与深水的岸线表现一致。
                float2 edgeUV = (float2(i.posWorld.z, i.posWorld.x) / max(_QuadSize, 1e-4) + 0.5)
                    / max(_TexSize.xy, 1.0);
                float waterAvg = SAMPLE_TEXTURE2D_LOD(_WaterHeightTex, sampler_HeightLinearClamp, edgeUV, 0).r;
                float edgeMask = saturate(waterAvg / max(waterMask, 1.0 / 255.0));

                // 序列帧 + 染色 + alpha（旧管线 water 口径，无顶点色项）
                float2 uv = WaterUV(i.posWorld, i.uv);
                half4 baseMapVar = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

                // 【光照必须补上】近景水面走 sango_shaderLib 的通用光照分支
                // （:339-368 + :499-501）：diffuse = baseMap.rgb * directDiffuse * _Color.rgb。
                // 远景原来只做了 baseMap * _Color，少了光照项，远近视切换时水色会整体跳变
                // （偏亮/偏灰）。水面法线恒朝上，所以这里用固定 (0,1,0) 复刻同一公式。
                // 远景水面不参与实时阴影（shadow 视为 1），差异在缩到远景后不可见。
                Light mainLight = GetMainLight();
                half3 normalWS = half3(0.0, 1.0, 0.0);
                half3 ambient = SampleSH(normalWS).rgb;
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 shadowColor = max(_ShadowColor.rgb, half3(0.2, 0.22, 0.26));
                half3 directDiffuse = lerp(shadowColor, mainLight.color.rgb, NdotL) + ambient;
                half3 diffuse = baseMapVar.rgb * directDiffuse * _Color.rgb;

                // 近景：alpha = _Alpha * _BaseMap.a * saturate(顶点色.a)
                // 远景：顶点色.a 由 edgeMask（水域掩码双线性值）等价替代
                float4 finalRGBA = float4(diffuse, _Alpha * baseMapVar.a * edgeMask);
                finalRGBA.rgb = MixFog(finalRGBA.rgb, i.fogCoord);
                return finalRGBA;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
