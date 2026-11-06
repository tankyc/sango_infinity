// Terrain314 terrain blend shader.
//
// Reference math (San14 remake repo):
//   src/San14.Data/Rendering/Terrain/TerrainGbiMaterialMath.cs
//     :36-41   DecodePaletteIndex   (int)(channel * 255)
//     :44-57   ControlCoordinates   four corner control coordinates
//     :67-74   CornerWeights        bilinear corner weights
//     :80-99   HeightBlend          qB / threshold / wA / wB
//     :106-136 EvaluateQuad         per-corner branch and weighted sum
//     :139-146 TransformNormal      cross(T,N)*handedness*nx + T*ny + N*nz
//   src/San14.Data/Rendering/Terrain/TerrainRendererPalette.cs:36-44
//     the palette is four parallel arrays indexed by the control value:
//       vDtlTexIdx  -> which root array layer to sample
//       vDtlBldSrp  -> height blend sharpness of that layer
//       vDtlScaleU / vDtlScaleV -> world units covered by one detail tiling
//   src/San14.Data/Rendering/Terrain/TerrainPatchInputMath.cs:70-72
//     HeightUv: control uv is a plain linear map of the patch uv bounds (no V flip)
//
// The reference leaves the map UV convention and the sampler state to the caller,
// so the mapping below is a documented project decision, not a copied formula:
//   phase * frequency  ->  frac(uv * _ControlCells)      (patch-local phase, equivalent global)
//   count              ->  _ControlCells + 1             (2049 control samples)
//   controlUv          ->  the terrain uv                (world XZ / _TerrainSize)
//   detail uv          ->  world XZ / palette ScaleU,V   (the palette tiling, repeat sampler)
//
// Exported map channel evidence (analyze_channels.py statistics):
//   initial control 2049x2049: R = palette parameter index 0..16, G always 255,
//     B always 0, A always 255
//     -> G = 255 means the "second layer" branch never fires on this map.
//   root slot0 1024x1024 x 19 layers: albedo array, A = occlusion
//   root slot1: normal array, A = height (the reference NormalHeight)
//   root slot2: reflectance array, A = linear roughness
//
// All property names are ASCII on purpose: Chinese strings were truncated on
// disk in an earlier revision, which broke ShaderLab parsing.
Shader "Sango/Terrain314"
{
    Properties
    {
        [Header(TerrainScale)]
        _TerrainSize ("Terrain World Size", Float) = 204800
        _ControlCells ("Control Cell Count", Float) = 2048
        _HeightRange ("Height Range", Float) = 6966.25

        [Header(Blend)]
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        // 0 = leaf level, full 4-corner palette blend; 1 = non-leaf, coarse single sample.
        // 314 ships these as two separate programs (coarse-vs / coarse-ps), switched per batch.
        _CoarseMode ("Coarse Mode (0=near 1=coarse)", Float) = 0


        [Header(Textures)]
        _ControlTex ("Initial Control (R=palette parameter)", 2D) = "black" {}
        _AlbedoArray ("Root Albedo Array", 2DArray) = "" {}
        _NormalArray ("Root Normal Array (A=height)", 2DArray) = "" {}
        _ReflectanceArray ("Root Reflectance Array (A=roughness)", 2DArray) = "" {}
        // Declared for Terrain314MaterialSet.CreateMaterial, which still binds it.
        // The reference takes the blend height from NormalHeight.w (:87-88), so this
        // texture is intentionally not sampled here.
        _FieldHeight ("Field Height (reserved, unused)", 2D) = "black" {}
        // Terrain314Renderer binds the 2049x2049 R32 height field here. Reserved for
        // the DS141 vertex displacement (:204-214); not sampled yet. Declaring it keeps
        // SetTexture from logging "doesn't have a texture property".
        _HeightTex ("Height Field (reserved, unused)", 2D) = "black" {}
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
            Name "Terrain314"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Mirrors TerrainGbiMaterialMath.MissingSecondaryIndex
            #define MISSING_SECONDARY_INDEX 255
            // Must match Terrain314MaterialSet.PaletteCapacity
            #define PALETTE_CAPACITY 32

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            TEXTURE2D(_ControlTex);   SAMPLER(sampler_ControlTex);
            TEXTURE2D_ARRAY(_AlbedoArray); SAMPLER(sampler_AlbedoArray);
            TEXTURE2D_ARRAY(_NormalArray); SAMPLER(sampler_NormalArray);
            TEXTURE2D_ARRAY(_ReflectanceArray); SAMPLER(sampler_ReflectanceArray);

            // Four palette columns, bound with Material.SetFloatArray.
            // Kept outside UnityPerMaterial because SRP Batcher cannot take arrays.
            float _PaletteTexIndex[PALETTE_CAPACITY];
            float _PaletteScaleU[PALETTE_CAPACITY];
            float _PaletteScaleV[PALETTE_CAPACITY];
            float _PaletteSharpness[PALETTE_CAPACITY];
            float _PaletteCount;

            CBUFFER_START(UnityPerMaterial)
                float _TerrainSize;
                float _ControlCells;
                float _HeightRange;
                float _CoarseMode;
                float4 _Tint;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionWS = positions.positionWS;
                output.positionCS = positions.positionCS;
                output.normalWS = normals.normalWS;
                return output;
            }

            // :36-41 normalized control channel * 255 with the original ftoi truncation
            float DecodePaletteIndex(float channel)
            {
                return (float)(int)(channel * 255.0);
            }

            // control 图与高度场是同一套行列索引。
            // 高度场已在加载时把数据 X/Z 互换（列 = 南/X、行 = 东/Z），
            // 但 control 图仍是导出时的"列 = 东、行 = 南"，所以取样前要把分量换回来，
            // 否则地表材质会与地形整体错位。
            // 314 的 HeightUv 本身是线性映射且不翻 V，但导出的 PNG 是自上而下存储的，
            // Unity 采样需要翻转 V。
            float2 ControlUv(float2 worldXZ)
            {
                float2 swapped = float2(worldXZ.y, worldXZ.x);
                float2 uv = swapped / max(_TerrainSize, 1.0);
                return saturate(float2(uv.x, 1.0 - uv.y));
            }

            // :67-74 bilinear corner weights in order 00, 10, 01, 11
            float4 CornerWeights(float2 fraction)
            {
                float x = fraction.x, y = fraction.y;
                return float4((1.0 - y) * (1.0 - x), (1.0 - y) * x, (1.0 - x) * y, y * x);
            }

            // :218-219 original accumulation order: (c10*wY + c00*wX) + c01*wZ + c11*wW
            float SumQuad(float c00, float c10, float c01, float c11, float4 w)
            {
                return ((c10 * w.y + c00 * w.x) + c01 * w.z) + c11 * w.w;
            }
            float3 SumQuad(float3 c00, float3 c10, float3 c01, float3 c11, float4 w)
            {
                return ((c10 * w.y + c00 * w.x) + c01 * w.z) + c11 * w.w;
            }
            float4 SumQuad(float4 c00, float4 c10, float4 c01, float4 c11, float4 w)
            {
                return ((c10 * w.y + c00 * w.x) + c01 * w.z) + c11 * w.w;
            }

            // :44-57 the four corners straddle controlUv by the in-cell fraction times
            // g = 1/(count-1). The original anchors at controlUv itself, so the weighted
            // sum reproduces controlUv exactly.
            void ControlCoordinates(float2 controlUv, float2 fraction, float count,
                out float2 c00, out float2 c10, out float2 c01, out float2 c11)
            {
                float g = 1.0 / (count - 1.0);
                c00 = controlUv + float2(-fraction.x, -fraction.y) * g;
                c10 = controlUv + float2(1.0 - fraction.x, -fraction.y) * g;
                c01 = controlUv + float2(-fraction.x, 1.0 - fraction.y) * g;
                c11 = controlUv + float2(1.0 - fraction.x, 1.0 - fraction.y) * g;
                // The reference has no sampler state; clamp so the edges do not wrap.
                c00 = saturate(c00); c10 = saturate(c10);
                c01 = saturate(c01); c11 = saturate(c11);
            }

            // :87-92 qB / threshold / wA / wB. Sharpness belongs to the second layer.
            void HeightWeights(float heightA, float heightB, float beta, float secondarySharpness,
                out float wA, out float wB, out float sum)
            {
                float qA = heightA;
                float qB = (beta - 0.5) * 2.0 + heightB;
                float threshold = max(qB, qA) - (1.0 - secondarySharpness);
                wA = max(qA - threshold, 0.0);
                wB = max(qB - threshold, 0.0);
                sum = wB + wA;
            }

            // :216-217 Combine: B*wB then mad A*wA, then divide. Never a beta lerp.
            float3 Combine(float3 a, float3 b, float wA, float wB, float sum)
            {
                return (b * wB + a * wA) / sum;
            }
            float4 Combine(float4 a, float4 b, float wA, float wB, float sum)
            {
                return (b * wB + a * wA) / sum;
            }

            // --- palette columns -------------------------------------------------
            int ClampParameter(int parameter)
            {
                int limit = max((int)_PaletteCount, 1) - 1;
                return clamp(parameter, 0, min(limit, PALETTE_CAPACITY - 1));
            }

            // vDtlTexIdx: control value -> root array layer
            uint DetailLayer(int parameter)
            {
                return (uint)max(_PaletteTexIndex[ClampParameter(parameter)], 0.0);
            }

            // vDtlScaleU / vDtlScaleV: world units covered by one detail tiling
            // 与 ControlUv 同一套分量交换：U 沿世界 Z（东）、V 沿世界 X（南），
            // 这样 vDtlScaleU / vDtlScaleV 仍分别对应原版的 U / V 两列。
            float2 DetailUv(int parameter, float2 worldXZ)
            {
                int index = ClampParameter(parameter);
                float2 scale = float2(max(_PaletteScaleU[index], 1.0), max(_PaletteScaleV[index], 1.0));
                return float2(worldXZ.y, worldXZ.x) / scale;
            }

            // vDtlBldSrp: per layer sharpness, not a global constant
            float DetailSharpness(int parameter)
            {
                return saturate(_PaletteSharpness[ClampParameter(parameter)]);
            }

            float3 SampleAlbedo(float2 uv, uint layer)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_AlbedoArray, uv, layer).rgb;
            }
            float4 SampleNormal(float2 uv, uint layer)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, uv, layer);
            }
            float4 SampleReflectance(float2 uv, uint layer)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_ReflectanceArray, sampler_ReflectanceArray, uv, layer);
            }

            // :119-128 one control corner. Writes the three targets the reference keeps:
            // AlbedoOcclusion.rgb, NormalHeight.xyz (still encoded) and ReflectanceLinearRoughness.
            // control.r/g are palette parameters, so they must be resolved to a layer fist.
            void EvaluateCorner(float3 control, float2 worldXZ, float beta,
                out float3 albedo, out float3 encodedNormal, out float4 reflectance)
            {
                // fxc complains about possibly uninitialized outs across the early returns
                albedo = 0.0;
                encodedNormal = float3(0.0, 0.0, 1.0);
                reflectance = float4(0.0, 0.0, 0.0, 1.0);

                int parameterFirst = (int)DecodePaletteIndex(control.r);
                int parameterSecond = (int)DecodePaletteIndex(control.g);

                // :124 second == 255 -> the corner only has the first layer.
                if (parameterSecond >= MISSING_SECONDARY_INDEX || control.b > 0.99)
                {
                    // :125 blue above 0.99 switches straight to the second layer.
                    int parameter = parameterSecond >= MISSING_SECONDARY_INDEX
                        ? parameterFirst : parameterSecond;
                    float2 detailUv = DetailUv(parameter, worldXZ);
                    uint layer = DetailLayer(parameter);
                    albedo = SampleAlbedo(detailUv, layer);
                    encodedNormal = SampleNormal(detailUv, layer).xyz;
                    reflectance = SampleReflectance(detailUv, layer);
                    return;
                }

                // :126 both layers exist and the cell is inside the blend range.
                float2 detailUvFirst = DetailUv(parameterFirst, worldXZ);
                float2 detailUvSecond = DetailUv(parameterSecond, worldXZ);
                uint layerFirst = DetailLayer(parameterFirst);
                uint layerSecond = DetailLayer(parameterSecond);

                float4 normalA = SampleNormal(detailUvFirst, layerFirst);
                float4 normalB = SampleNormal(detailUvSecond, layerSecond);

                float wA, wB, sum;
                HeightWeights(normalA.a, normalB.a, beta,
                    DetailSharpness(parameterSecond), wA, wB, sum);

                // The reference throws on sum <= 0; on GPU we fall back to the B layer.
                if (sum <= 0.0)
                {
                    albedo = SampleAlbedo(detailUvSecond, layerSecond);
                    encodedNormal = normalB.xyz;
                    reflectance = SampleReflectance(detailUvSecond, layerSecond);
                    return;
                }

                albedo = Combine(SampleAlbedo(detailUvFirst, layerFirst),
                    SampleAlbedo(detailUvSecond, layerSecond), wA, wB, sum);
                encodedNormal = Combine(normalA.xyz, normalB.xyz, wA, wB, sum);
                reflectance = Combine(SampleReflectance(detailUvFirst, layerFirst),
                    SampleReflectance(detailUvSecond, layerSecond), wA, wB, sum);
            }

            // :139-146 PS13 basis, no final normalize. T comes from the world normal
            // because the terrain mesh has no meaningful UV tangent.
            //
            // 数据 X/Z 互换后 detail 的 U 沿世界 +Z、V 沿世界 +X（见 DetailUv），
            // 切线基必须按这个方向重建，否则山体明暗会与实际朝向对不上。
            float3 TransformNormal(float3 encodedNormal, float3 normalWS)
            {
                float3 nt = normalize(encodedNormal * 2.0 - 1.0);
                float3 n = normalize(normalWS);

                // U 基 = 世界 +Z 在切平面上的投影；近平地时退化为 +Z
                float3 tangent = cross(float3(1.0, 0.0, 0.0), n);
                tangent = length(tangent) > 1e-5 ? normalize(tangent) : float3(0.0, 0.0, 1.0);

                // V 基 = U × N，对平地即世界 +X
                float3 bitangent = cross(n, tangent);
                bitangent = length(bitangent) > 1e-5 ? normalize(bitangent) : float3(1.0, 0.0, 0.0);

                return tangent * nt.x + bitangent * nt.y + n * nt.z;
            }

            // Shared forward lighting. The reference only writes a g-buffer (EncodeMaterialBuffer
            // :164 explicitly adds no light), so this part is a project-side approximation.
            half4 ShadeSurface(float3 albedo, float3 encodedNormal, float roughness,
                float3 positionWS, float3 normalWS)
            {
                float3 worldNormal = TransformNormal(encodedNormal, normalWS);
                float3 color = albedo * _Tint.rgb;

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 V = normalize(GetWorldSpaceViewDir(positionWS));
                float3 H = normalize(L + V);
                float ndotl = saturate(dot(worldNormal, L)) * 0.75 + 0.25;
                float specPower = lerp(128.0, 8.0, roughness);
                float specular = pow(saturate(dot(worldNormal, H)), specPower)
                                 * lerp(1.0, 0.1, roughness) * 0.25;

                color *= ndotl * mainLight.color.xyz;
                color += specular * mainLight.color.xyz;
                return half4(color, 1.0);
            }

            // Non-leaf levels. 314 draws these with the separate coarse-vs / coarse-ps programs
            // (TerrainPrograms.h:50-58, batch flag set by TerrainMeshPreparation.cs:226-227),
            // so the leaf-only work is skipped here: one control tap instead of four corners,
            // one palette layer instead of a height blend -> 4 taps instead of up to 24.
            half4 CoarseShade(float2 worldXZ, float3 positionWS, float3 normalWS)
            {
                float2 uv = ControlUv(worldXZ);

                float3 control = SAMPLE_TEXTURE2D(_ControlTex, sampler_ControlTex, uv).rgb;
                int parameter = (int)DecodePaletteIndex(control.r);

                float2 detailUv = DetailUv(parameter, worldXZ);
                uint layer = DetailLayer(parameter);

                float3 albedo = SampleAlbedo(detailUv, layer);
                float3 encodedNormal = SampleNormal(detailUv, layer).xyz;
                float roughness = saturate(SampleReflectance(detailUv, layer).a);
                return ShadeSurface(albedo, encodedNormal, roughness, positionWS, normalWS);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 世界坐标直接用于取样：数据层的 X/Z 互换已在 Terrain314Source.Load 完成，
                // 这里不再有任何镜像或转置。
                float2 worldXZ = input.positionWS.xz;

                // 非叶层走 coarse 路径（原版是独立程序，这里靠材质上的 _CoarseMode 区分）
                if (_CoarseMode > 0.5)
                {
                    return CoarseShade(worldXZ, input.positionWS, input.normalWS);
                }

                // control uv covers the whole control map (V flip inside)
                float2 uv = ControlUv(worldXZ);

                // 1) in-cell fraction comes from phase * frequency, not from the corners
                float2 fraction = frac(uv * _ControlCells);

                // 2) four corner control coordinates (:44-57)
                float2 c00, c10, c01, c11;
                ControlCoordinates(uv, fraction, _ControlCells + 1.0, c00, c10, c01, c11);

                float3 s00 = SAMPLE_TEXTURE2D(_ControlTex, sampler_ControlTex, c00).rgb;
                float3 s10 = SAMPLE_TEXTURE2D(_ControlTex, sampler_ControlTex, c10).rgb;
                float3 s01 = SAMPLE_TEXTURE2D(_ControlTex, sampler_ControlTex, c01).rgb;
                float3 s11 = SAMPLE_TEXTURE2D(_ControlTex, sampler_ControlTex, c11).rgb;

                // 3) corner weights (:67-74) and the quad wide beta sum (:117)
                float4 weights = CornerWeights(fraction);
                float beta = saturate(SumQuad(s00.b, s10.b, s01.b, s11.b, weights));

                // 4) per corner material evaluation (:119-128)
                float3 albedo[4];
                float3 normal[4];
                float4 reflectance[4];
                EvaluateCorner(s00, worldXZ, beta, albedo[0], normal[0], reflectance[0]);
                EvaluateCorner(s10, worldXZ, beta, albedo[1], normal[1], reflectance[1]);
                EvaluateCorner(s01, worldXZ, beta, albedo[2], normal[2], reflectance[2]);
                EvaluateCorner(s11, worldXZ, beta, albedo[3], normal[3], reflectance[3]);

                // 5) weighted corner sums (:129-134)
                float3 albedoSrgb = SumQuad(albedo[0], albedo[1], albedo[2], albedo[3], weights);
                float3 encodedNormal = SumQuad(normal[0], normal[1], normal[2], normal[3], weights);
                float4 reflect = SumQuad(reflectance[0], reflectance[1], reflectance[2], reflectance[3],
                    weights);
                float roughness = saturate(reflect.a);

                // 6) 着色（near / coarse 共用同一段光照）
                return ShadeSurface(albedoSrgb, encodedNormal, roughness,
                    input.positionWS, input.normalWS);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
