// Sango 地形着色器（阶段 1：几何 / LOD）。
//
// 【核心结构】
//   所有 patch 共用同一张 65×65 的平面网格（顶点在 [0,1] 区间，Y 恒为 0）。
//   一个 patch 只提交一个实例矩阵：平移 = 世界原点，缩放 = 世界边长。
//   顶点着色器做三件事：
//     1) 由实例矩阵反推 patch 的世界原点与边长；
//     2) 按世界 XZ 采样 R8 高度纹理，得到顶点高度；
//     3) 按到观察点 _EyePos 的距离把顶点位置插值到"父层网格"上（CDLOD 的 morph）。
//        _EyePos 由 C# 侧传入，必须与 TerrainSangoLod 的分裂判定用同一个观察点。
//
// 【为什么 morph 能消掉 LOD 接缝】
//   分裂阈值 T = _SplitFactor × patchSize。一个 patch 在 d ∈ [T, 2T] 之间时，
//   顶点逐渐被吸到父层的 32 段网格上（64 格合并成 32 段）；d 退到 T 时它恰好被 4 个
//   子节点替换，子节点此刻的网格正好等于父层被吸后的那一层网格 → 切换不跳变。
//   唯一例外是叶层：它没有子节点，morph 对它只剩"丢精度"的副作用，因此叶层强制
//   morph = 0（见 vert 里的 step(1.5, levelRatio)）；叶层替换父层时会补进细节，
//   表现为细节渐入，而不再是严格的几何一致。
//
// 【为什么高度用 R8 就够】
//   源数据 VertexData.height 本身是 1 字节，R8_UNorm 与之逐位等价；
//   叶层顶点与 texel 1:1 对齐，采样无损。详见 TerrainSangoSource 的注释。
Shader "Sango/TerrainSango"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.46, 0.52, 0.34, 1)
        _HeightTex ("Height (R8, 1 texel = 1 vertex)", 2D) = "black" {}
        _HeightTexSize ("Height Texture Size", Vector) = (1024, 1024, 0, 0)
        _QuadSize ("Vertex Spacing", Float) = 5
        _HeightRange ("Height Range (world units)", Float) = 127.5
        _SplitFactor ("Split Factor", Float) = 1
        _LeafSize ("Leaf Patch World Size", Float) = 320
        _SkirtDepth ("Skirt Depth Ratio", Range(0, 0.05)) = 0
        _DebugView ("Debug View (0=none 1=lod 2=morph)", Float) = 0
        _EyePos ("Observer Position (world, must equal LOD eye)", Vector) = (0, 0, 0, 0)
        _UseLayers ("Use Terrain Layers", Float) = 0
        _LayerCount ("Layer Count", Float) = 1
        _WorldSize ("Map World Size (X=Z extent, Y=X extent)", Vector) = (1, 1, 0, 0)
        _UseBaseMap ("Use Base Map Overlay", Float) = 0
        _BaseColorIntensity ("Base Map Intensity", Range(0, 1)) = 0.85
        _WaterLayerIndex ("Water Layer Index (-1 = none)", Float) = -1
        _UseGrid ("Show Grid", Float) = 0
        _GridSize ("Grid Size (world units)", Float) = 20
        _GridFlag ("Grid Flag", Float) = 1
        _DarkFlag ("Dark Flag", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "TerrainSangoForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 移动端兼容：TEXTURE2D_ARRAY 的最低要求是 shader model 3.5（GLES3.1 / Vulkan / Metal / DX11），
            // 写成 4.5 会让部分移动 GPU 的变体直接编译失败 → Shader.Find 拿到无可用 SubShader 的 shader
            // → 地形整片不渲染。这里按"纹理数组的真实下限"取 3.5。
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_HeightTex);
            SAMPLER(sampler_HeightTex);

            // 地表图层：索引纹理（点采样，R8）+ 贴图数组 + 每层参数（RGBAFloat，宽 = 层数，高 = 1）
            TEXTURE2D(_LayerIndexTex);
            SAMPLER(sampler_LayerIndexTex);
            TEXTURE2D_ARRAY(_LayerAlbedo);
            SAMPLER(sampler_LayerAlbedo);
            TEXTURE2D(_LayerParams);
            SAMPLER(sampler_LayerParams);

            // 底图与地格：对应旧管线的 _BaseTex / _GridTex / _GridMask / _RangeMask / _DarkMask
            TEXTURE2D(_BaseTex);
            SAMPLER(sampler_BaseTex);
            TEXTURE2D(_GridTex);
            SAMPLER(sampler_GridTex);
            TEXTURE2D(_GridMask);
            SAMPLER(sampler_GridMask);
            TEXTURE2D(_RangeMask);
            SAMPLER(sampler_RangeMask);
            TEXTURE2D(_DarkMask);
            SAMPLER(sampler_DarkMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _HeightTexSize;
                float _QuadSize;
                float _HeightRange;
                float _SplitFactor;
                float _LeafSize;
                float _SkirtDepth;
                float _DebugView;
                float4 _EyePos;
                // CDLOD 边界缝合：x=东 y=西 z=北 w=南，1 = 该边贴着更粗层（或地图外）。
                // 命中的边把最外圈顶点的 morph 强制收敛到 1，与粗邻居的格点密度/高度完全重合，消除 T 型接缝。
                float4 _EdgeCoarse;
                // 相机到地形中值平面的高度（× HeightLodWeight）：与 C# 侧共享同一视距口径
                float _ViewHeight;
                float _UseLayers;
                float _LayerCount;
                float4 _WorldSize;
                float _UseBaseMap;
                float _BaseColorIntensity;
                float _WaterLayerIndex;
                float _UseGrid;
                float _GridSize;
                float _GridFlag;
                float _DarkFlag;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 debugInfo : TEXCOORD2;   // x = LOD 层级, y = morph 系数
            };

            // 按世界 XZ 采样高度。
            // 轴序：世界 Z → uv.x（列），世界 X → uv.y（行），与 MapData 的 (vx, vy) 对应。
            float SampleHeight(float2 worldXZ)
            {
                float2 texel = float2(worldXZ.y, worldXZ.x) / max(_QuadSize, 1e-4);
                // 【吸附到最近的顶点格】相邻 patch 在共享边界上的 worldXZ 理论相等、
                // 实际只差实例矩阵乘出的浮点尾数；不吸附时两边的 texel 可能各取到相邻一格，
                // 高度差可达几十米，边界上就是一条撕裂缝。顶点着色器只在格点上运行，吸附是恒等变换。
                texel = floor(texel + 0.5);
                // texel 索引 → texel 中心 UV，保证叶层顶点精确落在 texel 中心
                float2 uv = (texel + 0.5) / max(_HeightTexSize.xy, 1.0);
                return SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, uv, 0).r * _HeightRange;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                // 实例矩阵只编码平移与缩放：原点 = M * (0,0,0)，边长 = |M * (1,0,0) - 原点|
                float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 axisX = TransformObjectToWorld(float3(1.0, 0.0, 0.0)) - originWS;
                float patchSize = max(length(axisX), 1e-3);

                float2 local = input.positionOS.xz;   // [0,1]
                float2 worldXZ = originWS.xz + local * patchSize;

                // ---- CDLOD morph：距离越远越向父层网格收敛 ----
                // 距离原点必须用 _EyePos（= C# 侧 TerrainSangoLod.Eye），不能用 _WorldSpaceCameraPos：
                // LOD 的分裂判定用的是"视线与地形中值平面的交点"，Scene 视图把相机推远时两者相差一整个图幅，
                // 混用会让"何时分裂"与"顶点何时收敛"脱钩（表现为红色阈值带跑到相机脚下）。
                float threshold = max(_SplitFactor, 1e-3) * patchSize;
                // 视距必须与 C# 侧 TerrainSangoLod.DistanceToRect 完全一致：
                //   sqrt(平面距离² + 相机高度²)
                // 若这里只用平面距离，相机拉高后 LOD 已合并到粗层、顶点却仍按"很近"保持不收敛，
                // 切换瞬间会看到几何跳变（morph 与分级脱钩）。
                float2 planar = _EyePos.xz - worldXZ;
                float distance = sqrt(dot(planar, planar) + _ViewHeight * _ViewHeight);
                float morph = saturate(2.0 - distance / threshold);

                // 叶层禁 morph。
                // 叶 patch 没有子节点、永远不会再分裂，对它做 morph 没有任何"可隐藏的跳变"收益，
                // 只会把离相机最近（d < T = _LeafSize）的那片地面压到父层的 32 格网格上，
                // 精度直接减半；而且 morph 随相机距离逐帧变化，顶点会滑动，表现为"脚下地形蠕动"。
                // 层级由 patchSize / _LeafSize 反推（叶层恰为 1，往上依次 2、4、8），
                // 用 1.5 做阈值避免浮点误差把叶层误判成 level 1。
                float levelRatio = patchSize / max(_LeafSize, 1e-3);
                morph *= step(1.5, levelRatio);

                // ---- 边界缝合（T 型接缝根治）----
                // 细层 patch 贴着更粗层时，两侧边界顶点密度差 2 倍，山脊处粗侧直线边与
                // 细侧折线之间会撕出穿透性破洞（露背景）。把细层该边的最外圈顶点 morph
                // 强制收敛到 1 后，顶点吸附到父层格点：两侧密度一致、worldXZ 相同、
                // 经 SampleHeight 的 texel 吸附后高度也相同 → 边界完全重合，缝消失。
                // 只锁最外一圈：patch 内部顶点全部共享、拓扑连续，密度差不会产生洞，
                // 只是靠边一圈三角形被拉伸，视觉代价远小于破洞。
                // C# 侧（TerrainSangoLod.MarkEdgeCoarse）按"同层邻居是否存在"算出四个标记。
                float edgeX = min(local.x, 1.0 - local.x);
                float edgeZ = min(local.y, 1.0 - local.y);
                float coarseMask = 0.0;
                if (edgeX < 1.0 / 64.0)
                    coarseMask = max(coarseMask, local.x < 0.5 ? _EdgeCoarse.y : _EdgeCoarse.x);
                if (edgeZ < 1.0 / 64.0)
                    coarseMask = max(coarseMask, local.y < 0.5 ? _EdgeCoarse.w : _EdgeCoarse.z);
                morph = max(morph, coarseMask);

                // 64 格合并成 32 段：把顶点吸到偶数格点上
                float2 snapped = floor(local * 32.0 + 0.5) / 32.0;
                float2 morphedLocal = lerp(local, snapped, morph);
                worldXZ = originWS.xz + morphedLocal * patchSize;

                float height = SampleHeight(worldXZ);

                // 裙边：把 patch 边界一圈压下去，兜住极端情况下的 T 型接缝（默认关闭）
                if (_SkirtDepth > 0.0)
                {
                    float edge = min(min(local.x, 1.0 - local.x), min(local.y, 1.0 - local.y));
                    height -= (1.0 - saturate(edge * 64.0)) * _SkirtDepth * patchSize;
                }

                // ---- 法线：四抽样中央差分 ----
                float delta = max(patchSize / 64.0, 1e-3);
                float hR = SampleHeight(worldXZ + float2(delta, 0.0));
                float hL = SampleHeight(worldXZ - float2(delta, 0.0));
                float hD = SampleHeight(worldXZ + float2(0.0, delta));
                float hU = SampleHeight(worldXZ - float2(0.0, delta));
                output.normalWS = normalize(float3(hL - hR, 2.0 * delta, hU - hD));

                output.positionWS = float3(worldXZ.x, height, worldXZ.y);
                output.positionCS = TransformWorldToHClip(output.positionWS);

                // 调试信息：层级由 patch 世界尺寸反推，不需要额外的实例数据
                float level = log2(max(patchSize / max(_LeafSize, 1e-3), 1.0));
                output.debugInfo = float2(level, morph);
                return output;
            }

            /// 取某个顶点 texel 对应的地表反照率。
            /// texelCoord 用"顶点格坐标"（1 格 = 1 个顶点），uv0 是整幅地图的 0~1 坐标。
            half3 SampleLayerAlbedo(float2 texelCoord, float2 uv0)
            {
                float2 uvIndex = (texelCoord + 0.5) / max(_HeightTexSize.xy, 1.0);
                // R = 图层索引（G/B 是水面高度与水面标记，只给水面着色器用）
                float raw = SAMPLE_TEXTURE2D_LOD(_LayerIndexTex, sampler_LayerIndexTex, uvIndex, 0).r;
                // 索引越界（缺层/文件损坏）时夹到最后一层，避免采样到未定义的切片
                float layer = min(floor(raw * 255.0 + 0.5), max(_LayerCount - 1.0, 0.0));

                float2 paramUV = float2((layer + 0.5) / max(_LayerCount, 1.0), 0.5);
                float4 layerParam = SAMPLE_TEXTURE2D_LOD(_LayerParams, sampler_LayerParams, paramUV, 0);

                // 与旧管线同一口径：uv = (vx/vertex_width, vy/vertex_height) * textureScale，
                // 而 vx/vertex_width 就是 世界Z / 地图世界宽度
                return SAMPLE_TEXTURE2D_ARRAY(_LayerAlbedo, sampler_LayerAlbedo, uv0 * layerParam.xy, layer).rgb;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_DebugView > 0.5)
                {
                    if (_DebugView < 1.5)
                    {
                        // LOD 层级着色：同一层级同色，用于确认"近处细、远处粗"
                        float level = input.debugInfo.x;
                        return half4(frac(level * 0.618), frac(level * 0.382 + 0.33), frac(level * 0.271 + 0.66), 1.0);
                    }
                    // Morph 热力图：绿 = 未收敛（全精度），红 = 已收敛到父层
                    float morph = input.debugInfo.y;
                    return half4(morph, 1.0 - morph, 0.0, 1.0);
                }

                // ---- 地表反照率：48 层贴图按"顶点 alpha 混合"取 4 个角做双线性插值 ----
                //
                // 【为什么要取 4 个角，而不是点采样一个索引】
                //   旧管线给每个 layer 单独建网格，同一格的 4 个角各自带 alpha=1/0（见 MapTerrain：
                //   textureIndex == layer ? color_white : color_black，color_black 的 alpha 为 0），
                //   再用 Blend SrcAlpha OneMinusSrcAlpha 逐层叠加。光栅化会在这 4 个角之间线性插值，
                //   于是两种地表的交界是"在一格之内渐变"，不是硬边。
                //   新管线没有每层网格，改用：取该像素所在格的 4 个角各自的图层，按格内小数坐标
                //   做双线性混合 —— 与旧管的 alpha 插值同轴、同一格、同一权重，效果等价。
                half3 albedo = _BaseColor.rgb;
                if (_UseLayers > 0.5)
                {
                    float2 worldXZ = float2(input.positionWS.z, input.positionWS.x);
                    float2 uv0 = worldXZ / max(_WorldSize.xy, 1.0);
                    float2 texel = worldXZ / max(_QuadSize, 1e-4);
                    float2 cell = floor(texel);
                    float2 f = texel - cell;

                    half3 c00 = SampleLayerAlbedo(cell, uv0);
                    half3 c10 = SampleLayerAlbedo(cell + float2(1.0, 0.0), uv0);
                    half3 c01 = SampleLayerAlbedo(cell + float2(0.0, 1.0), uv0);
                    half3 c11 = SampleLayerAlbedo(cell + float2(1.0, 1.0), uv0);
                    albedo = lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
                    // 水面不在这里叠色：它是独立的一张面，高度来自顶点的 water 数据，
                    // 由 Sango/TerrainSangoWater 单独绘制（见 TerrainSangoWater.shader 顶部注释）。
                }

                Light mainLight = GetMainLight();
                float3 normalWS = normalize(input.normalWS);
                float ndotl = saturate(dot(normalWS, mainLight.direction));

                // ---- 光照：与旧管线 sango_shaderLib.hlsl:344-357 同构 ----
                // 旧版：
                //   ambient       = SampleSH(normalWS).rgb;                      // :335 球谐环境光
                //   directDiffuse = lerp(shadowColor, mainLight.color, NdotL * shadow)
                //                 + ambient * saturate(shadow + 0.5);            // :355
                // 新管线不采样阴影（没有阴影坐标，shadow 恒为 1），等价于：
                //   lighting = lerp(shadowColor, mainLight.color, NdotL) + ambient
                //
                // 【必须真的采 SH】之前这里写的是固定常量 0.28 —— 那等于把环境光钉死，
                // 场景改环境光/换天空盒/改 Light Probe 自发光，地形一律不响应。
                // SampleSH 取的就是环境光探针（Ambient Probe / Light Probe），
                // 与旧地形的取光通道完全一致。
                half3 shadowColor = half3(0.20h, 0.22h, 0.26h);   // 与旧版兜底下限一致，避免背光纯黑
                half3 ambient = SampleSH(normalWS).rgb;

                half3 lighting = lerp(shadowColor, mainLight.color, ndotl) + ambient;

                float3 color = albedo * lighting;

                // ---- 底图（旧管线 SANGO_BASE_COLOR）----
                // lerp(diffuse, baseColor * diffuse, intensity)：把美术手绘的大色块乘上去，
                // 口径与旧管线完全一致，包括 baseUV 的 V 轴翻转。
                float2 baseUV = float2(input.positionWS.z / max(_WorldSize.x, 1.0),
                                       1.0 - input.positionWS.x / max(_WorldSize.y, 1.0));
                if (_UseBaseMap > 0.5)
                {
                    half4 baseColor = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, baseUV);
                    color = lerp(color, baseColor.rgb * color, _BaseColorIntensity);
                }

                // ---- 地格显示（旧管线 SANGO_GRID_COLOR）----
                // 上下两行交错的六边形格贴图：奇数行 UV 下移半格，所以先按 worldZ/gridSize 求奇偶。
                if (_UseGrid > 0.5)
                {
                    float gridSize = max(_GridSize, 1.0);
                    float row = floor(fmod(input.positionWS.z / gridSize, 2.0));
                    float2 gridUV = float2(input.positionWS.z / gridSize,
                                           1.0 - input.positionWS.x / gridSize + row * 0.5);
                    half4 gridColor = SAMPLE_TEXTURE2D(_GridTex, sampler_GridTex, gridUV);

                    float2 maskUV = float2(baseUV.x, baseUV.y + row * 0.5 / max(_WorldSize.y / gridSize, 1.0));
                    half4 gridMask = SAMPLE_TEXTURE2D(_GridMask, sampler_GridMask, maskUV);
                    half4 rangeMask = SAMPLE_TEXTURE2D(_RangeMask, sampler_RangeMask, maskUV);
                    half4 darkMask = SAMPLE_TEXTURE2D(_DarkMask, sampler_DarkMask, maskUV);

                    float gridFlag = max(_GridFlag, max(gridMask.g, max(gridMask.b, gridMask.r)));
                    float flag = saturate(gridColor.a * 1.8 * gridFlag);
                    color = lerp(color, gridColor.rgb, gridColor.a * gridFlag * gridMask.a);
                    color = lerp(color, half3(0.0, 1.0, 0.0), flag * gridMask.g);
                    color = lerp(color, half3(0.0, 0.0, 1.0), flag * gridMask.b);
                    color = lerp(color, half3(1.0, 0.0, 0.0), flag * gridMask.r);
                    color = lerp(color, color * 0.5, saturate(_DarkFlag * (1.0 - darkMask.a)));
                    color = lerp(color, color * rangeMask.rgb, rangeMask.a);
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
