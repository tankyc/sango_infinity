// 314 式海面着色器（Terrain314 独立体系）。
//
// 【对齐依据】D:/San14 的水面实现分两层：
//   数据层  src/San14.Data/Rendering/Environment/State/OceanSurfaceSettings.cs:7
//           （WaterHeight 是世界常数，不是"高度场 <= 阈值即水"）
//   绘制层  native/San14.Render.D3D11/Passes/Environment/Ocean/OceanRenderer.h:192-224
//           （G-Buffer 不透明、写深度、写模板、背面剔除）
//   光学    src/San14.Data/Rendering/Environment/Interop/OceanShaderAbi.cs:51-75
//           （折射深度 + 深度散射 + 反射率/粗糙度 + 三层气泡泡沫）
//
// 【与 314 的差异】314 走 deferred 六张 MRT 且顶点位移来自 CPU 端 FFT
// （OceanFft.h:195-245）。本项目是 URP Forward，改成：
//   1) 透明混合（Queue=Transparent、ZWrite Off），靠深度测试被陆地遮挡；
//   2) 顶点位移改用 4 层 Gerstner 波在 VS 内解析求解（省掉 CPU 回写 VB）；
//   3) 没有 3D 法线贴图与气泡数组，泡沫用程序化噪声 + 水深阈值近似。
// 这三点差异在 Terrain314WaterRenderer 的类注释里同步记录，避免误判为"已完全对齐"。
Shader "Sango/Terrain314Water"
{
    Properties
    {
        // 314 导出：water/wv-height-mask-mip0.bin -> height-mask.r32（2048² R32，海床高度）
        [Header(Height Mask)]
        _HeightMaskTex ("Height Mask (R32)", 2D) = "black" {}
        // 世界坐标 -> 遮罩 uv 的缩放（1 / 地形世界边长 204800）
        _HeightMaskScale ("Height Mask Scale", Float) = 0.0000048828125
        // 314 导出：OceanSurfaceSettings.WaterHeight = 450
        _WaterHeight ("Water Height", Float) = 450

        // 以下三张由 San14TerrainExport 从 314 原版导出（water/ 目录）
        [Header(314 Surface Textures)]
        _NormalVolume ("Normal Volume (RG8 3D)", 3D) = "gray" {}
        _NormalNoise ("Normal Noise (R8)", 2D) = "gray" {}
        _BubbleArray ("Bubble Array (RG8 2DArray)", 2DArray) = "gray" {}
        // OceanSurfaceSettings.NormalAnimationSpeed = 50
        _NormalAnimationSpeed ("Normal Animation Speed", Float) = 50
        _BubblePeriod ("Bubble Period", Float) = 3
        _DetailNormalStrength ("Detail Normal Strength", Float) = 0.6
        _BubbleStrength ("Bubble Strength", Float) = 0.6

        [Header(Water Color)]
        _ShallowColor ("Shallow Color", Color) = (0.18, 0.42, 0.46, 1)
        _DeepColor ("Deep Color", Color) = (0.03, 0.12, 0.20, 1)
        _ReflectionColor ("Reflection Color", Color) = (0.55, 0.68, 0.78, 1)

        [Header(Depth Optics)]
        // 水深到该值即完全变成 DeepColor（314 的散射色近似）
        _DepthFade ("Depth Fade (world units)", Float) = 900
        // 每米增加的不透明度，浅水更透明
        _OpacityPerMeter ("Opacity Per Meter", Float) = 0.004
        _MaxOpacity ("Max Opacity", Float) = 0.92

        [Header(Fresnel And Specular)]
        _FresnelPower ("Fresnel Power", Float) = 5
        _SpecularPower ("Specular Power", Float) = 240
        _SpecularIntensity ("Specular Intensity", Float) = 1.2

        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (0.86, 0.92, 0.94, 1)
        _FoamDistance ("Foam Distance (world units)", Float) = 160
        _FoamStrength ("Foam Strength", Float) = 1.0

        [Header(Waves)]
        // 全局波幅缩放：0 = 静水（远海 quad 用）
        _WaveScale ("Wave Scale", Float) = 1
        // xy = 主导风向（四层在此基础上各偏一个固定角度），zw 保留
        _WaveDirection ("Wave Direction", Vector) = (1, 0.35, 0, 0)
        _WaveAmplitude ("Wave Amplitude", Vector) = (26, 14, 7, 3.5)
        _WaveLength ("Wave Length", Vector) = (2400, 1300, 620, 310)
        _WaveSpeed ("Wave Speed", Vector) = (9, 11, 14, 18)
        _WaveSteepness ("Wave Steepness", Vector) = (0.55, 0.5, 0.45, 0.4)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-100"
            "RenderType" = "Transparent"
        }
        LOD 200

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            // 水面不写深度：透明物体写深度会挡掉后面更近的透明物；
            // 314 是 deferred 不透明写深度，这一点属于项目侧差异（见文件头说明）
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            CBUFFER_START(UnityPerMaterial)
                float _NormalAnimationSpeed;
                float _BubblePeriod;
                float _DetailNormalStrength;
                float _BubbleStrength;
                float _HeightMaskScale;
                float _WaterHeight;
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _ReflectionColor;
                float _DepthFade;
                float _OpacityPerMeter;
                float _MaxOpacity;
                float _FresnelPower;
                float _SpecularPower;
                float _SpecularIntensity;
                float4 _FoamColor;
                float _FoamDistance;
                float _FoamStrength;
                float _WaveScale;
                float4 _WaveDirection;
                float4 _WaveAmplitude;
                float4 _WaveLength;
                float4 _WaveSpeed;
                float4 _WaveSteepness;
            CBUFFER_END

            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            // 314 导出的海床高度遮罩（R32，2048²，与地形高度场同一套行列）
            TEXTURE2D(_HeightMaskTex);
            SAMPLER(sampler_HeightMaskTex);
            // 314 导出的水体贴图：体积法线 / 法线噪声 / 三层气泡数组
            TEXTURE3D(_NormalVolume);
            SAMPLER(sampler_NormalVolume);
            TEXTURE2D(_NormalNoise);
            SAMPLER(sampler_NormalNoise);
            TEXTURE2D_ARRAY(_BubbleArray);
            SAMPLER(sampler_BubbleArray);

            struct VertexInput
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
            };

            struct VertexOutput
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            /// <summary>
            /// 把四层 Gerstner 波叠加成顶点位移与解析法线。
            /// 314 的位移来自 CPU 端 FFT（OceanFft.h:195-222），这里是项目侧替代实现，
            /// 方向/振幅/波长/速度/陡度五组参数逐层对应 FFT 谱的"长波到短波"分层。
            /// </summary>
            void EvaluateWaves(float2 worldXZ, float time, out float3 displacement,
                out float3 normalWS)
            {
                displacement = float3(0.0, 0.0, 0.0);
                // 法线的切向偏导累加量
                float3 tangentX = float3(1.0, 0.0, 0.0);
                float3 tangentZ = float3(0.0, 0.0, 1.0);

                // 四层固定展开：移动端也吃得消，且避免动态索引常量数组
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    // 每层在主导风向上再偏一个固定角度，避免四层同向叠成"推土机波"
                    float angle = (i == 0) ? 0.0 : ((i == 1) ? 0.62 : ((i == 2) ? -0.52 : 1.15));
                    float2 baseDirection = normalize(_WaveDirection.xy + float2(1e-5, 0.0));
                    float2 direction = float2(
                        baseDirection.x * cos(angle) - baseDirection.y * sin(angle),
                        baseDirection.x * sin(angle) + baseDirection.y * cos(angle));

                    float amplitude = _WaveAmplitude[i] * _WaveScale;
                    float wavelength = max(_WaveLength[i], 1.0);
                    float steepness = _WaveSteepness[i];
                    // 深水色散关系 omega = sqrt(g * k)，再乘人工倍率把周期拉到视觉合适的 4~8 秒
                    float waveNumber = 6.2831853 / wavelength;
                    float omega = sqrt(9.8 * waveNumber) * max(_WaveSpeed[i], 0.1);
                    float phase = waveNumber * dot(direction, worldXZ) - omega * time;

                    float sinPhase = sin(phase);
                    float cosPhase = cos(phase);
                    // Gerstner：水平方向按陡度挤压，垂直方向按正弦抬升
                    float qa = steepness * amplitude;
                    displacement.x += qa * direction.x * cosPhase;
                    displacement.z += qa * direction.y * cosPhase;
                    displacement.y += amplitude * sinPhase;

                    // 解析法线：对 P.x / P.z 求偏导后 cross(dP/dz, dP/dx) 得朝上的法线
                    float qakSin = steepness * amplitude * waveNumber * sinPhase;
                    float waCos = waveNumber * amplitude * cosPhase;
                    tangentX += float3(
                        -qakSin * direction.x * direction.x,
                        waCos * direction.x,
                        -qakSin * direction.x * direction.y);
                    tangentZ += float3(
                        -qakSin * direction.x * direction.y,
                        waCos * direction.y,
                        -qakSin * direction.y * direction.y);
                }

                normalWS = normalize(cross(tangentZ, tangentX));
            }

            VertexOutput vert(VertexInput input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                VertexOutput output;
                // 必须显式传递实例 ID，否则 VertexOutput 里的 instanceID 未初始化，
                // D3D 编译期会报 "Output value 'vert' is not completely initialized"
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                // DrawMeshInstanced 的 TRS 矩阵：把 -0.5..0.5 的 tile 顶点铺到世界
                float3 positionWS = TransformObjectToWorld(input.vertex.xyz);
                float time = _Time.y;

                float3 displacement;
                float3 normalWS;
                EvaluateWaves(positionWS.xz, time, displacement, normalWS);

                positionWS += displacement;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            /// <summary>
            /// 程序化泡沫噪声：两级 value noise 叠加，替代 314 的 wv-bubble 贴图数组
            /// （OceanTextures.h:44-56 的三层气泡贴图本项目没有导出）。
            /// </summary>
            float FoamNoise(float2 worldXZ, float time)
            {
                float2 p = worldXZ * 0.0125;
                float low = sin(p.x * 1.7 + time * 0.9) * sin(p.y * 1.9 - time * 0.7);
                float high = sin(p.x * 7.3 - time * 1.7) * sin(p.y * 8.1 + time * 1.3);
                return saturate(low * 0.6 + high * 0.4 + 0.5);
            }

            half4 frag(VertexOutput input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                // 314 体积法线：xy 平面按世界 XZ 滚动，w 轴随时间推进（NormalAnimationSpeed）。
                // U 沿世界 Z、V 沿世界 X，与 DetailUv 同一套分量约定；
                // 噪声图先扰动平面 uv，避免整片海面出现规则的平铺纹。
                float2 surfaceUv = float2(input.positionWS.z, input.positionWS.x) * 0.0006;
                float2 noiseSample = SAMPLE_TEXTURE2D(_NormalNoise, sampler_NormalNoise,
                    surfaceUv * 2.7 + _Time.y * 0.013).rg;
                surfaceUv += (noiseSample - 0.5) * 0.015;

                float3 volumeSample = SAMPLE_TEXTURE3D(_NormalVolume, sampler_NormalVolume,
                    float3(surfaceUv, frac(_Time.y * _NormalAnimationSpeed * 0.01))).rgb;
                float2 detailNormal = volumeSample.rg * 2.0 - 1.0;
                normalWS = normalize(normalWS +
                    float3(detailNormal.x, 0.0, detailNormal.y) * _DetailNormalStrength);

                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);

                // 1) 水深：场景深度（地形）减去水面自身深度。
                //    314 用折射深度图 + depthToZ 线性化（WaterCameraFrame.h:33-47），语义一致。
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                float sceneRawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float waterEyeDepth = input.positionCS.w;
                float waterDepth = max(sceneEyeDepth - waterEyeDepth, 0.0);

                // 1b) 314 导出的海床高度遮罩（2048² R32）。
                //     它和地形高度场同行列（列 = 东、行 = 南），因此取样前交换 X/Z；
                //     远处逐像素深度精度不够，用遮罩算出的 shoreDepth 更稳，
                //     同时它也是水陆边缘淡出与浅滩泡沫的唯一依据。
                float2 maskUv = float2(input.positionWS.z, input.positionWS.x) * _HeightMaskScale;
                float terrainHeight = SAMPLE_TEXTURE2D(_HeightMaskTex, sampler_HeightMaskTex, maskUv).r;
                float shoreDepth = _WaterHeight - terrainHeight;

                // 陆地格（掩码高于水面或刚好齐平）直接按 shoreDepth 淡出，避免水面糊在岸上
                float shoreFade = saturate(shoreDepth * 0.05);

                // 2) 水体色：按水深从浅到深插值
                float depthRatio = saturate(waterDepth / max(_DepthFade, 1.0));
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthRatio);

                // 3) 菲涅尔 + 反射
                float fresnel = pow(saturate(1.0 - saturate(dot(normalWS, viewDirection))), _FresnelPower);
                float3 reflection = _ReflectionColor.rgb;
                float3 color = lerp(waterColor, reflection, fresnel * 0.75);

                // 4) 主光方向的高光（314 的反射是标量 reflectance，这里用 Blinn-Phong 近似）
                Light mainLight = GetMainLight();
                float3 halfDirection = normalize(mainLight.direction + viewDirection);
                float specular = pow(saturate(dot(normalWS, halfDirection)), _SpecularPower) * _SpecularIntensity;
                color += mainLight.color * specular;

                // 5) 泡沫：只在浅水带出现，靠噪声打散。
                //    用 shoreDepth 与逐像素水深取小值——纯深度法在远处精度不足，
                //    会在整片外海刷出一圈假泡沫。
                float foamDepth = min(waterDepth, max(shoreDepth, 0.0));
                float foamMask = saturate(1.0 - foamDepth / max(_FoamDistance, 1.0));
                float foam = foamMask * FoamNoise(input.positionWS.xz, _Time.y) * _FoamStrength;

                // 314 的三层气泡数组（表面 / 内部 / 岸边）：这里取第 0 层做表面泡沫，
                // 相位按 SurfaceBubblePeriod 循环，只在浅水带出现。
                float bubblePhase = frac(_Time.y / max(_BubblePeriod, 0.01));
                float2 bubbleUv = float2(input.positionWS.z, input.positionWS.x) * 0.0035 + bubblePhase;
                float bubble = SAMPLE_TEXTURE2D_ARRAY(_BubbleArray, sampler_BubbleArray, bubbleUv, 0).r;
                foam = max(foam, bubble * foamMask * _BubbleStrength);

                color = lerp(color, _FoamColor.rgb, saturate(foam));

                // 6) 透明度：浅水更透，深水趋近 _MaxOpacity。
                //    depthRatio 项保证"垂直俯视浅水"时水体仍有存在感——纯菲涅尔在正视角下接近 0，
                //    会让人误以为水面没画出来。
                float alpha = saturate(waterDepth * _OpacityPerMeter + fresnel * 0.5 + depthRatio * 0.35);
                alpha = lerp(alpha, _MaxOpacity, depthRatio);
                alpha *= shoreFade;

                color = MixFog(color, input.fogFactor);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
