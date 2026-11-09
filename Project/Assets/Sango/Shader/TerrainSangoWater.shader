// TerrainSango 水面着色器。
//
// 【为什么水面要单独一个 shader，而不是在地表着色里叠一层颜色】
//   旧管线（MapTerrain.VertexPosition）里水面是一张独立网格，顶点高度取
//   `water` 字节 × 0.5，而不是地表高度 `height` × 0.5：
//       // MapTerrain.cs（原实现，已注释）
//       if (d.water > 0) return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
//   也就是说"水面高度"由 water 数据决定，因此它必须是一张真的、位于海平面高度的面。
//
// 【顶点取数】
//   与地表共用同一张 _LayerIndexTex（RGBA32，1 texel = 1 顶点）：
//       R = 地表图层索引
//       G = 海面高度字节（已做 8 邻居回退，岸线不会斜插到 0）
//       B = 水面标记（自身 water > 0 才为 255，用作 alpha）
//   alpha 由顶点传到片元后由光栅化器线性插值 → 岸线是一格之内的渐变。
//
// 【水面贴图是动画图集】
//   water_0.png 是 8×8 的逐帧水流动画（见 sango_shaderLib.hlsl:212-227 的 SANGO_WATER 分支）：
//       c_add  = floor((z + x) / 120 % 225)          // 让相邻区域的帧错开
//       time   = floor(_Speed * _Time.y + c_add)     // 每秒推进 _Speed 帧
//       row    = time / _HorizontalAmount
//       column = time - row * _HorizontalAmount
//       uv     = (uv + (column, -row)) / (8, 8)
//   直接把它当普通贴图采样，会同时采到 64 个小格 —— 画面会糊成一片灰白，必须按帧取。
//
// 【配色与透明度】
//   diffuse = 光照 * tex.rgb * _WaterColor(0.45,0.5,0.44)
//   alpha   = _WaterAlpha(0.9) * tex.a * 顶点水面权重
//   与旧 water.shader 的 `_Alpha * _BaseMap_var.a * vertColor.a` 同口径。

Shader "Sango/TerrainSangoWater"
{
    Properties
    {
        _WaterLayerIndex ("Water Layer Index", Float) = 48
        _LayerCount ("Layer Count", Float) = 49
        _QuadSize ("Vertex Spacing", Float) = 5
        _WaterColor ("Water Color", Color) = (0.45, 0.5, 0.44, 1)
        _WaterAlpha ("Water Alpha", Range(0, 1)) = 0.9
        _HorizontalAmount ("Atlas Columns", Float) = 8
        _VerticalAmount ("Atlas Rows", Float) = 8
        _Speed ("Animation Frames Per Second", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SangoWater"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 移动端兼容：与 TerrainSango.shader 同理，TEXTURE2D_ARRAY 的下限是 3.5，不要写 4.5
            #pragma target 3.5
            // 必须开实例化：否则 TransformObjectToWorld 拿不到每个实例的矩阵，所有 patch 会叠在原点
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HeightTexSize;
                float4 _WorldSize;
                float4 _WaterColor;
                float _QuadSize;
                float _LayerCount;
                float _WaterLayerIndex;
                float _WaterAlpha;
                float _HorizontalAmount;
                float _VerticalAmount;
                float _Speed;
            CBUFFER_END

            TEXTURE2D(_LayerIndexTex);
            SAMPLER(sampler_LayerIndexTex);
            TEXTURE2D_ARRAY(_LayerAlbedo);
            SAMPLER(sampler_LayerAlbedo);
            TEXTURE2D(_LayerParams);
            SAMPLER(sampler_LayerParams);

            struct Attributes
            {
                float4 positionOS : POSITION;
                // 实例化必须的语义：没有它，unity_ObjectToWorld 不会按实例切换，
                // 所有 patch 会叠在同一个位置（表现为"完全看不到水面"）。
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float waterAlpha : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                // 绑定本实例的矩阵，TransformObjectToWorld 才会用对 patch 的 TRS
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // 轴序必须与地表完全一致：世界 Z → uv.x（列），世界 X → uv.y（行），
                // 见 TerrainSango.shader 的 SampleHeight：texel = float2(worldXZ.y, worldXZ.x) / _QuadSize。
                // 若这里写成 (x, z)，水面图层会被转置 90 度，水的流向与地表贴图走向对不上。
                float2 worldZX = float2(positionWS.z, positionWS.x);

                // 与地表共用同一套 texel 口径：1 texel = 1 顶点
                float2 texel = worldZX / max(_QuadSize, 1e-4);
                float2 uvIndex = (texel + 0.5) / max(_HeightTexSize.xy, 1.0);
                // 变量名不用 sample：它是 HLSL 的取样器状态关键字，部分编译器会直接报错
                float4 layerSample = SAMPLE_TEXTURE2D_LOD(_LayerIndexTex, sampler_LayerIndexTex, uvIndex, 0);

                // G = 海面高度字节（0~255 映射到 0~1），世界高度 = 字节 * 0.5（与 MapTerrain 同口径）
                positionWS.y = layerSample.g * 255.0 * 0.5;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                // 贴图 UV 与地表 uv0 同口径：float2(positionWS.z, positionWS.x) / _WorldSize.xy
                output.uv = worldZX / max(_WorldSize.xy, 1.0);
                // B = 水面标记：自身 water > 0 才为 1，光栅化后即为岸线渐变权重
                output.waterAlpha = step(0.5, layerSample.b);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(input.waterAlpha - 0.001);

                float2 paramUV = float2((_WaterLayerIndex + 0.5) / max(_LayerCount, 1.0), 0.5);
                float4 layerParam = SAMPLE_TEXTURE2D_LOD(_LayerParams, sampler_LayerParams, paramUV, 0);

                // ---- 动画图集取帧（与 sango_shaderLib.hlsl 的 SANGO_WATER 分支同公式）----
                // layerParam.xy 就是文件里该层的 textureScale，与地表 uv0 * layerParam.xy 同口径
                float2 waterUV = input.uv * layerParam.xy;
                float columns = max(_HorizontalAmount, 1.0);
                float rows = max(_VerticalAmount, 1.0);
                float cellOffset = floor(fmod((input.positionWS.z + input.positionWS.x) / 120.0, 225.0));
                float frame = floor(_Speed * _Time.y + cellOffset);
                float row = floor(frame / columns);
                float column = floor(frame - row * columns);
                float2 atlasUV = waterUV + float2(column, -row);
                atlasUV.x /= columns;
                atlasUV.y /= rows;

                half4 tex = SAMPLE_TEXTURE2D_ARRAY(_LayerAlbedo, sampler_LayerAlbedo, atlasUV, _WaterLayerIndex);

                // ---- 光照 ----
                // 旧管线（sango_shaderLib.hlsl:344-356 + :487-489）是：
                //   NdotL = saturate(dot(normalWS, mainLight.direction));
                //   directDiffuse = lerp(shadowColor, mainLight.color, NdotL * shadow) + ambient * saturate(shadow + 0.5);
                //   diffuse = directDiffuse * tex.rgb;  →  SANGO_COLOR 分支再乘 _Color.rgb
                // 水面网格法线朝上，故 NdotL 恒等于光源方向的 Y 分量。
                // 这里沿用 TerrainSango（地表）已验证的同一套光照公式，保证水面与陆地受光一致；
                // 差异：未采样主光阴影（旧管线接了 shadowAttenuation），见文件末尾的"已知差异"。
                Light mainLight = GetMainLight();
                float3 normalWS = float3(0.0, 1.0, 0.0);
                float ndotl = saturate(dot(normalWS, mainLight.direction));

                // 与地表同样接环境光探针（SampleSH），否则换天空盒/改环境光时水面不变，
                // 会和地形出现明显的色温断层
                half3 shadowColor = half3(0.20h, 0.22h, 0.26h);
                half3 ambient = SampleSH(normalWS).rgb;
                half3 lighting = lerp(shadowColor, mainLight.color, ndotl) + ambient;

                half3 color = tex.rgb * _WaterColor.rgb * lighting;

                // 与旧管线同口径：_Alpha * tex.a * 顶点水面权重
                float alpha = _WaterAlpha * tex.a * saturate(input.waterAlpha);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

// ============================================================================
// 已知差异（相对旧管线 Sango/water_urp + MapTerrain 的水面网格）
//
// ① 未接主光阴影：旧 shader 编了 _MAIN_LIGHT_SHADOWS 变体并用 shadowAttenuation
//    参与 lerp，水面会被山体阴影压暗；本 shader 只有 GetMainLight()（无阴影坐标），
//    水面永远按全亮处理。与新版地表 TerrainSango 的取光方式一致，属"整条新管线统一
//    不接阴影"的口径，不是水面独有的偏差。
// ② 不做 CDLOD morph：地表顶点在远处会吸到父层 32 段网格，水面不 morph。
//    patch 边界顶点在两种情况下位置相同（local = 0 / 1 对齐后不变），因此岸线不会错位。
// ③ 网格密度与 patch 划分比地表粗（见 TerrainSangoRenderer.EnsureWaterMesh / WaterLodBias）：
//    水面网格是 33×33（地表 65×65），patch 默认再合并一层（4 个地表 patch → 1 个水面 patch）。
//    理由：水面没有顶点动画，顶点只负责"抬到海平面"（G 通道，本图 88.8% 是同一个值）
//    和"把水面标记插值成岸线渐变"（B 通道）。代价是岸线在 20 世界单位（叶层半格地格）
//    的粒度上取样，比地表粗 4 倍 —— 需要更贴合时把 WaterLodBias 设为 0。
// ④ textureScale 的 offset（_BaseMap_ST.zw）未参与：地图图层数据只给了 scale，
//    旧管线也没设 offset，故两者等价。
// ============================================================================
