// Sango 地形着色器（MapCell 改造版：控制贴图 + Texture2DArray + 共享材质合批）。
//
// 【一个着色器、两种顶点路径】
//   · 近景（MeshRenderer，enableInstancing=false）：顶点高度由 CPU 构建网格时直接烘进
//     position.y，走"TransformObjectToWorld 直出"路径，同时携带精确顶点法线；
//   · 远景（TerrainCellBatchRenderer 的 DrawMeshInstanced，enableInstancing=true）：
//     全体块共用一张平面模板网格（顶点在 [0,1]²、Y 恒 0），实例矩阵只编码平移与边长，
//     顶点着色器反推世界 XZ、按 R8 高度纹理做位移 + 差分法线 —— 远景不需要 CPU 网格。
//   两条路径用 UNITY_INSTANCING_ENABLED 宏区分（见 MapCellTerrainAsset.TerrainMaterial 注释）。
//
// 【地表混合】控制贴图（R8，1 texel = 1 顶点格，R = textureIndex）+ Texture2DArray：
//   取像素所在格 4 个角各自的图层，按格内小数坐标双线性混合 —— 与旧管线
//   "每层独立网格 + 顶点色 alpha 光栅化插值"同轴、同一格、同一权重，过渡效果等价。
//   该口径与 TerrainSango.shader 的 SampleLayerAlbedo 完全一致。
//
// 【其余视觉段完整复刻 sango_shaderLib.hlsl 的 SANGO_TERRAIN 路径】
//   光照(:339-368) / 底图(:372-379) / 编辑器地格(:405-428) / 地格(:430-451) /
//   势力图(:453-465) / 笔刷(:467-494) / 雾(:527-536)，全局参数
//   （_MapWidth/_MapHeight/_GridSize/_FactionMap/_Brush...）仍由 C# 侧 Shader.SetGlobalXXX 设置。
Shader "Sango/terrain_cell"
{
    Properties
    {
        _ControlTex ("Control (R8, R=layerIndex)", 2D) = "black" {}
        _LayerTexArray ("Layer TexArray", 2DArray) = "" {}
        _LayerParams ("Layer Params (RGBAFloat)", 2D) = "black" {}
        _LayerCount ("Layer Count", Float) = 1
        _HeightTex ("Height (R8,远景位移)", 2D) = "black" {}
        _TexSize ("Tex Size (vertexW, vertexH)", Vector) = (1024, 1024, 0, 0)
        _QuadSize ("Quad Size (world/vertex)", Float) = 5
        _HeightRange ("Height Range", Float) = 127.5
        _BaseColor ("Fallback Base Color", Color) = (0.46, 0.52, 0.34, 1)
        _BaseColorIntensity ("Base Color Intensity", Range(0, 1)) = 0.85
        _Alpha ("Alpha", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #define SANGO_BASE_COLOR 1
            #define SANGO_GRID_COLOR 1
            #define SANGO_FOG 1
            #define SANGO_BRUSH 1
            #define SANGO_TERRAIN_TYPE 1
            #define SANGO_FACTION_MAP 1

            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SANGO_EDITOR
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma skip_variants FOG_EXP FOG_EXP2

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ---- 材质属性（UnityPerMaterial：SRP Batcher 合批的必要条件）----
            CBUFFER_START(UnityPerMaterial)
                float4 _TexSize;
                float _QuadSize;
                float _HeightRange;
                float _LayerCount;
                float _BaseColorIntensity;
                float _Alpha;
                float4 _BaseColor;
            CBUFFER_END

            // ---- 纹理 ----
            TEXTURE2D(_ControlTex);
            SAMPLER(sampler_ControlTex);
            TEXTURE2D_ARRAY(_LayerTexArray);
            SAMPLER(sampler_LayerTexArray);
            TEXTURE2D(_LayerParams);
            SAMPLER(sampler_LayerParams);
            TEXTURE2D(_HeightTex);
            SAMPLER(sampler_HeightTex);

            // ---- 全局参数（与旧管线 sango_shaderLib 同名同源，C# 侧 Shader.SetGlobalXXX 维护）----
            float4 _ShadowColor;
            float _MapWidth;
            float _MapHeight;
            float _GridSize;
            TEXTURE2D(_BaseTex);
            TEXTURE2D(_GridTex);
            TEXTURE2D(_GridMask);
            TEXTURE2D(_RangeMask);
            TEXTURE2D(_DarkMask);
            TEXTURE2D(_FactionMap);
            float _FactionMapAlpha;
            float _GridFlag;
            float _DarkFlag;

            // 内联采样器（照抄 sango_shaderLib:180-184 的写法：底图线性重复、地格遮罩点采样）
            #define sampler_BaseMap SamplerState_Linear_Repeat
            #define smpPoint SamplerState_Point_Repeat
            SAMPLER(sampler_BaseMap);
            SAMPLER(smpPoint);

            #if defined(SANGO_EDITOR)
            // 笔刷（全局，TerrainBrush 设置）
            float _BrushType;
            float4 _Brush;
            float4 _BrushUV;
            float _BrushSize;
            float4 _BrushColor;
            TEXTURE2D(_BrushTex);
            SamplerState SamplerState_Point_Clamp;
            // 地格信息显示（GridBrush 设置）
            float _terrainTypeMaskCol;
            float _terrainTypeMaskRow;
            float _terrainTypeAlpha;
            float _TerrainTypeShowFlag;
            TEXTURE2D(_TerrainTypeTex);
            TEXTURE2D(_TerrainTypeMaskTex);
            TEXTURE2D(_TerrainOverlayTex);
            float _TerrainOverlayShowFlag;
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;          // 全图 UV（层贴图 tiling 采样口径与旧管线一致）
                float3 posWorld : TEXCOORD3;
                float fogCoord : TEXCOORD4;
                float4 shadowCoord : TEXCOORD6;
                float4 vertColor : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ---- 远景位移：按世界 XZ 采样 R8 高度纹理 ----
            // 轴序与旧管线一致：世界 Z → vx（texel.x），世界 X → vy（texel.y）。
            // 相邻实例共享边界上的 worldXZ 理论相等、实际只差浮点尾数，
            // 吸附到最近格点后两侧必得同一 texel（TerrainSango 同款保证，防接缝）。
            float SampleHeightWS(float2 worldXZ)
            {
                float2 texel = float2(worldXZ.y, worldXZ.x) / max(_QuadSize, 1e-4);
                texel = floor(texel + 0.5);
                float2 uv = (texel + 0.5) / max(_TexSize.xy, 1.0);
                return SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, uv, 0).r * _HeightRange;
            }

            // ---- 由"已经算好的世界坐标"构造 VertexPositionInputs ----
            //
            // 【不能写成 GetShadowCoord(GetVertexPositionInputs(posWS))】
            // URP 的 GetVertexPositionInputs(float3 positionOS) 入参是 **object space**，
            // 内部第一行就是 positionWS = TransformObjectToWorld(positionOS)。
            // 把世界坐标传进去等于再乘一次 unity_ObjectToWorld：
            //   · 近景：多乘一次块节点的平移（每块偏 320 单位量级）；
            //   · 远景：多乘一次实例矩阵（平移 + 缩放）。
            // 结果是阴影坐标整体错位 —— 地形上出现大片与几何遮挡无关的黑块，
            // 而受光面 / 阴影面的边界完全对不上山脊走向。
            //
            // 正确用法见旧管线 sango_shaderLib.hlsl:254-266：
            //   VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);  // object space
            //   o.shadowCoord = GetShadowCoord(vertexInput);
            // 本 shader 的世界坐标是自己算出来的（远景还要做高度位移），没有现成的 object space
            // 入参可用，因此按 URP 内部同样的公式手工拼一份（与 Core.hlsl 的
            // GetVertexPositionInputs 逐行对应，只有第一行被替换成直接赋值）。
            VertexPositionInputs BuildVertexPositionInputs(float3 positionWS, float4 positionCS)
            {
                VertexPositionInputs input;
                input.positionWS = positionWS;
                input.positionVS = TransformWorldToView(positionWS);
                input.positionCS = positionCS;

                float4 ndc = positionCS * 0.5f;
                input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
                input.positionNDC.zw = positionCS.zw;
                return input;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS;
                float3 normalWS;

                #if defined(UNITY_INSTANCING_ENABLED)
                // ---- 远景 Instanced：平面模板 + 高度纹理位移 ----
                float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 axisX = TransformObjectToWorld(float3(1.0, 0.0, 0.0)) - originWS;
                float patchSize = max(length(axisX), 1e-3);

                float2 worldXZ = originWS.xz + input.positionOS.xz * patchSize;
                float height = SampleHeightWS(worldXZ);
                posWS = float3(worldXZ.x, height, worldXZ.y);

                // 法线：四采样中央差分（模板网格不带法线流）
                float delta = max(patchSize / 32.0, 1e-3);
                float hR = SampleHeightWS(worldXZ + float2(delta, 0.0));
                float hL = SampleHeightWS(worldXZ - float2(delta, 0.0));
                float hD = SampleHeightWS(worldXZ + float2(0.0, delta));
                float hU = SampleHeightWS(worldXZ - float2(0.0, delta));
                normalWS = normalize(float3(hL - hR, 2.0 * delta, hU - hD));

                output.uv = float2(posWS.z / max(_MapWidth, 1e-4), posWS.x / max(_MapHeight, 1e-4));
                #else
                // ---- 近景：CPU 顶点直出（高度已烘进 position.y）----
                posWS = TransformObjectToWorld(input.positionOS.xyz);
                normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                #endif

                output.vertColor = input.color;
                output.posWorld = posWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                // 阴影坐标必须用世界坐标构造（见 BuildVertexPositionInputs 注释）
                output.shadowCoord = GetShadowCoord(BuildVertexPositionInputs(posWS, output.positionCS));
                return output;
            }

            /// 取某个顶点 texel 对应的地表反照率（与 TerrainSango.shader 同款口径）。
            half3 SampleLayerAlbedo(float2 texelCoord, float2 uv0)
            {
                float2 uvIndex = (texelCoord + 0.5) / max(_TexSize.xy, 1.0);
                // R = 图层索引（控制贴图烘焙自 MapData.VertexData.textureIndex）
                float raw = SAMPLE_TEXTURE2D_LOD(_ControlTex, sampler_ControlTex, uvIndex, 0).r;
                // 索引越界（缺层/数据异常）时夹到最后一层，避免采样到未定义切片
                float layer = min(floor(raw * 255.0 + 0.5), max(_LayerCount - 1.0, 0.0));

                float2 paramUV = float2((layer + 0.5) / max(_LayerCount, 1.0), 0.5);
                float4 layerParam = SAMPLE_TEXTURE2D_LOD(_LayerParams, sampler_LayerParams, paramUV, 0);

                // 与旧管线同一口径：层贴图 uv = 全图 uv * textureScale（scale.y 为负 → V 轴翻转）
                return SAMPLE_TEXTURE2D_ARRAY(_LayerTexArray, sampler_LayerTexArray, uv0 * layerParam.xy, layer).rgb;
            }

            float4 frag(Varyings i) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // ---- 光照（照抄 sango_shaderLib.hlsl:339-368，含 _ShadowColor 兜底下限）----
                Light mainLight = GetMainLight(i.shadowCoord);
                float3 normalWS = normalize(i.normalWS);
                float3 lightDirection = mainLight.direction;
                half3 ambient = SampleSH(normalWS).rgb;
                half shadow = mainLight.shadowAttenuation;
                half NdotL = saturate(dot(normalWS, lightDirection));
                half3 shadowColor = max(_ShadowColor.rgb, half3(0.20h, 0.22h, 0.26h));
                half3 directDiffuse = lerp(shadowColor, mainLight.color.rgb, NdotL * shadow) + ambient * saturate(shadow + 0.5);

                // ---- 地表反照率：控制贴图 4 角双线性图层混合 ----
                half3 diffuse;
                if (_LayerCount > 0.5)
                {
                    float2 texel = float2(i.posWorld.z, i.posWorld.x) / max(_QuadSize, 1e-4);
                    float2 cell = floor(texel);
                    float2 f = texel - cell;
                    float2 uv0 = float2(i.posWorld.z / max(_MapWidth, 1e-4),
                                        i.posWorld.x / max(_MapHeight, 1e-4));

                    half3 c00 = SampleLayerAlbedo(cell, uv0);
                    half3 c10 = SampleLayerAlbedo(cell + float2(1.0, 0.0), uv0);
                    half3 c01 = SampleLayerAlbedo(cell + float2(0.0, 1.0), uv0);
                    half3 c11 = SampleLayerAlbedo(cell + float2(1.0, 1.0), uv0);
                    half3 albedo = lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
                    diffuse = directDiffuse * albedo;
                }
                else
                {
                    // 无图层资产（烘焙失败回退）：平色 + 光照
                    diffuse = directDiffuse * _BaseColor.rgb;
                }

                // ---- 底图（sango_shaderLib:372-379）----
                float2 baseUV = float2(i.posWorld.z / max(_MapWidth, 1e-4), 1.0 - i.posWorld.x / max(_MapHeight, 1e-4));
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseMap, baseUV);
                diffuse = lerp(diffuse, baseColor.rgb * diffuse, _BaseColorIntensity);

                // ---- 编辑器：地格类型显示 + 笔刷预览（sango_shaderLib:405-494）----
                #if defined(SANGO_EDITOR)
                float terrainTyperow = floor(i.posWorld.z / (_GridSize) % 2);
                float2 terrainTypegridUV = float2(i.posWorld.z / (_GridSize), 1 - i.posWorld.x / (_GridSize) + terrainTyperow * 0.5);
                float2 terrainTypebaseUV = baseUV;
                float2 terrainTypegridMaskUV = float2(terrainTypebaseUV.x, terrainTypebaseUV.y + terrainTyperow * 0.5 / (_MapHeight / _GridSize));
                float4 terrainTypeShowUV = SAMPLE_TEXTURE2D(_TerrainTypeMaskTex, smpPoint, terrainTypegridMaskUV);
                float3 gammaToLinear8 = terrainTypeShowUV.rgb;

                float2 tyUV = float2(terrainTypegridUV.x - floor(terrainTypegridUV.x), terrainTypegridUV.y - floor(terrainTypegridUV.y));
                float2 ttUV = gammaToLinear8.xy + float2(tyUV.x / _terrainTypeMaskCol, tyUV.y / _terrainTypeMaskRow);

                half4 tarrainTypeColor = SAMPLE_TEXTURE2D(_TerrainTypeTex, smpPoint, ttUV);
                diffuse = lerp(diffuse, diffuse * 0.5 + tarrainTypeColor.rbg, _TerrainTypeShowFlag * _terrainTypeAlpha);

                half4 overColor = SAMPLE_TEXTURE2D(_TerrainOverlayTex, sampler_BaseMap, baseUV);
                diffuse = lerp(diffuse, overColor.rgb, _TerrainOverlayShowFlag);

                if (_BrushType > 0)
                {
                    float size = _BrushSize;
                    float2 uv = baseUV + (0.5f / size);
                    uv = uv - _BrushUV.xy;
                    uv *= size;
                    half4 col = SAMPLE_TEXTURE2D(_BrushTex, SamplerState_Point_Clamp, uv);
                    col.rgb = 1;
                    col *= _BrushColor;

                    diffuse = lerp(diffuse, baseColor.rgb, _BrushUV.z);
                    diffuse = lerp(diffuse, col.rgb, col.a);
                }
                else
                {
                    float3 brushCenter = _Brush.xyz;
                    float3 pixsPos = i.posWorld;
                    brushCenter.y = 0;
                    pixsPos.y = 0;
                    float d = saturate(distance(brushCenter, pixsPos) / _Brush.w + 0.34);
                    diffuse = lerp(half3(0, 0.9, 0), diffuse, d);
                }
                #endif

                // ---- 地格显示（sango_shaderLib:430-451）----
                float row = floor(i.posWorld.z / (_GridSize) % 2);
                float2 gridUV = float2(i.posWorld.z / (_GridSize), 1 - i.posWorld.x / (_GridSize) + row * 0.5);
                half4 gridColor = SAMPLE_TEXTURE2D(_GridTex, sampler_BaseMap, gridUV);
                float2 gridMaskUV = float2(baseUV.x, baseUV.y + row * 0.5 / (_MapHeight / _GridSize));
                half4 gridMaskColor = SAMPLE_TEXTURE2D(_GridMask, smpPoint, gridMaskUV);
                half4 rangeMaskColor = SAMPLE_TEXTURE2D(_RangeMask, smpPoint, gridMaskUV);

                half f_GridFlag = max(_GridFlag, gridMaskColor.g);
                f_GridFlag = max(f_GridFlag, gridMaskColor.b);
                f_GridFlag = max(f_GridFlag, gridMaskColor.r);

                half flag = saturate(gridColor.a * 1.8 * f_GridFlag);
                half4 darkMaskColor = SAMPLE_TEXTURE2D(_DarkMask, smpPoint, gridMaskUV);

                diffuse = lerp(diffuse, gridColor.rgb, gridColor.a * f_GridFlag * gridMaskColor.a);
                diffuse = lerp(diffuse, half3(0, 1, 0), flag * gridMaskColor.g);
                diffuse = lerp(diffuse, half3(0, 0, 1), flag * gridMaskColor.b);
                diffuse = lerp(diffuse, half3(1, 0, 0), flag * gridMaskColor.r);
                diffuse = lerp(diffuse, diffuse * half3(0.5, 0.5, 0.5), saturate(_DarkFlag * (1 - darkMaskColor.a)));
                diffuse = lerp(diffuse, diffuse * rangeMaskColor.rgb, rangeMaskColor.a);

                // ---- 势力图（sango_shaderLib:453-465）----
                float factionRow = floor(i.posWorld.z / (_GridSize) % 2);
                float2 factionBaseUV = float2(i.posWorld.z / (_MapWidth), 1 - i.posWorld.x / (_MapHeight));
                float2 factionUV = float2(factionBaseUV.x, factionBaseUV.y + factionRow * 0.5 / (_MapHeight / _GridSize));
                half4 factionMapColor = SAMPLE_TEXTURE2D(_FactionMap, smpPoint, factionUV);
                half factionAlpha = saturate(factionMapColor.a * _FactionMapAlpha * 0.4);
                diffuse = lerp(diffuse, factionMapColor.rgb, factionAlpha);

                // ---- 输出（alpha 口径照抄 SANGO_TERRAIN 分支；CPU 顶点色恒白 = 全不透明）----
                float4 finalRGBA = float4(diffuse, _Alpha * saturate(i.vertColor.a));

                // ---- 雾（sango_shaderLib:527-536）----
                finalRGBA.rgb = MixFog(finalRGBA.rgb, i.fogCoord);
                return finalRGBA;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // 【只 include Core.hlsl，不 include Shadows.hlsl】
            // Shadows.hlsl 内部的 SampleShadowmapSingle 等函数在个别编译路径下会引用
            // LerpWhiteTo（Shadows.hlsl:260），而本 pass 用不到采样阴影贴图的任何函数，
            // 干脆不引入该文件，杜绝这一类 undeclared identifier 报错。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 与 FORWARD 相同的 UnityPerMaterial 布局（SRP Batcher 要求各 pass 一致）
            CBUFFER_START(UnityPerMaterial)
                float4 _TexSize;
                float _QuadSize;
                float _HeightRange;
                float _LayerCount;
                float _BaseColorIntensity;
                float _Alpha;
                float4 _BaseColor;
            CBUFFER_END

            // 阴影投射几何常量：URP 的 ShadowUtils.SetupShadowCasterConstantBuffer
            // 以全局形式设置（SetGlobalVector），本 pass 自行声明即可
            float3 _LightDirection;
            float3 _LightPosition;
            float4 _ShadowBias;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 偏移实现照抄 URP Shadows.hlsl:433 的 ApplyShadowBias（_ShadowBias.x = 深度偏移、
            // .y = 法线偏移），这样本 pass 无需 include Shadows.hlsl
            float3 ApplyTerrainShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)
            {
                float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
                float scale = invNdotL * _ShadowBias.y;
                positionWS = normalWS * scale + positionWS;
                positionWS = lightDirection * _ShadowBias.x + positionWS;
                return positionWS;
            }

            // 与 URP ShadowCasterPass.hlsl:GetShadowPositionHClip 相同的偏移口径。
            // 【为什么不用 ShadowCasterPass.hlsl】它内部引用 _BaseMap/sampler_BaseMap/_BaseColor/_Cutoff
            // （LitInput.hlsl 的采样接口），本 shader 不提供这些接口会编译失败；地形阴影不需要
            // 贴图 alpha 裁剪，最小实现即可。
            float4 GetShadowPositionHClip(ShadowAttributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyTerrainShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            ShadowVaryings ShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
