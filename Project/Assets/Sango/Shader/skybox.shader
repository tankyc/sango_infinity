// 战场天空盒：贴在地形之上的"远景替代物"，用 alpha 与真实场景混合。
//
// 三条 alpha 融合相乘，作为最终透明度：
//   1. 深度融合：采样 _CameraDepthTexture 得到场景的相机空间距离（米）。
//      近处透明（让真实地形显示）、远处不透明（用天空图盖住远端地形），中间按曲线渐变。
//   2. 世界高度融合：按世界高度整体淡出（低于起点完全透明），处理地形低洼/水面以下的情况。
//   3. 自身高度融合：按天空盒自身的局部高度（底边 0 → 顶边 1）做渐变，
//      让底边融进地形，避免出现一条生硬的水平切边。
//
// 渲染约束：
//   · ZTest Off / ZWrite Off：天空盒永远画在场景几何之后，可见性完全交给 alpha，
//     所以调 RenderQueue 不会改变它和地形的遮挡关系（队列只决定绘制先后）。
//   · 顶点被压到远裁剪面：天空盒贴着背景，不会与地形/建筑发生几何穿插。
//   · 依赖 URP 的 Depth Texture（URP Asset 勾选 Depth Texture），
//     否则 _CameraDepthTexture 采样为空，深度融合会全部落到同一端。
Shader "Sango/skybox"
{
    Properties
    {
        _MainTex("天空贴图", 2D) = "white" {}
        _Color("整体色调", Color) = (1, 1, 1, 1)

        // ---------- 1. 深度融合（与场景深度做 alpha 混合） ----------
        // 起点/终点与 MapCamera 的远裁剪面口径对齐（400 ~ 1800，起点 ≈ 终点 × 0.26）
        _DepthFadeBegin("深度融合起点(相机距离/米)", Float) = 468
        _DepthFadeEnd("深度融合终点(相机距离/米)", Float) = 1800
        // 曲线强度：1 = 线性，值越大越"陡"（更靠终点才出现天空）
        _DepthPower("深度融合曲线强度", Float) = 4

        // ---------- 2. 世界高度融合 ----------
        _HeightFadeBegin("世界高度融合起点", Float) = -200
        _HeightFadeEnd("世界高度融合终点", Float) = -150
        _HeightPower("世界高度融合曲线强度", Float) = 18

        // ---------- 3. 自身高度融合（0 = 天空盒底边，1 = 顶边） ----------
        _LocalFadeBegin("自身高度融合起点(0~1)", Float) = 0
        _LocalFadeEnd("自身高度融合终点(0~1)", Float) = 0.25
        _LocalPower("自身高度融合曲线强度", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float worldY : TEXCOORD2;
                float localY01 : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _DepthFadeBegin;
                float _DepthFadeEnd;
                float _DepthPower;
                float _HeightFadeBegin;
                float _HeightFadeEnd;
                float _HeightPower;
                float _LocalFadeBegin;
                float _LocalFadeEnd;
                float _LocalPower;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // URP 的场景深度（需要 URP Asset 勾选 Depth Texture）
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.worldY = mul(unity_ObjectToWorld, v.positionOS).y;
                // 原始网格局部高度 0~4，这里按原高度归一化（0.25 = 1/4，与 SkyRows 对应）：
                // 原有区段仍是 0~1，与属性口径一致；上下各延伸 50% 的区段会得到 <0 / >1，
                // 其可见性由 _LocalFadeBegin / _LocalFadeEnd 决定
                // （默认 0~0.25 时下方延伸段完全透明，只作为几何冗余防止露出硬边）。
                o.localY01 = v.positionOS.y * 0.25;

                // 把顶点压到远裁剪面：天空盒永远在场景几何之后，不与地形穿插。
                // 是否可见完全由片元的 alpha 决定，这里只保证"不会因为几何靠前而被挡住"。
                #if UNITY_REVERSED_Z
                    o.positionCS.z = o.positionCS.w * 0.0001f;
                #else
                    o.positionCS.z = o.positionCS.w * 0.9999f;
                #endif

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;

                // ---------- 1. 深度融合 ----------
                // 采样场景深度并转成相机空间距离（米）。
                // 【必须用 LinearEyeDepth】若用 Linear01Depth × 常数，farClip 一变混合距离就整体缩放，
                // 天空盒会在中远景直接变成全透明（表现就是"被地形盖住"）。
                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 1e-5);
                float sceneRawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);

                float depthRange = max(_DepthFadeEnd - _DepthFadeBegin, 0.001);
                float depth01 = saturate((sceneEyeDepth - _DepthFadeBegin) / depthRange);
                depth01 = pow(depth01, max(_DepthPower, 0.001));

                // ---------- 2. 世界高度融合 ----------
                // 低于起点完全透明（埋在地形以下），高于终点完全保留。
                float heightRange = max(_HeightFadeEnd - _HeightFadeBegin, 0.001);
                float height01 = saturate((i.worldY - _HeightFadeBegin) / heightRange);
                height01 = pow(height01, max(_HeightPower, 0.001));

                // ---------- 3. 自身高度融合 ----------
                // 底边（0）透明，按区间渐变到顶边（1），让天空盒底部融进地形。
                float localRange = max(_LocalFadeEnd - _LocalFadeBegin, 0.001);
                float local01 = saturate((i.localY01 - _LocalFadeBegin) / localRange);
                local01 = pow(local01, max(_LocalPower, 0.001));

                // 三条融合相乘：既要"在远处"、又要"在地形之上"、还要"高于天空盒底边"，才会显示天空
                col.a *= depth01 * height01 * local01;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
