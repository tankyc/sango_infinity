// 314 程序化植被（GroundDetail）着色器。
//
// 【对齐依据】
//   314 的植被实例数据由 MapProceduralPlacementProducer 生成（见
//   D:/San14/src/San14.Data/Rendering/Terrain/MapProceduralPlacementProducer.cs:94-182），
//   渲染侧按 element 分成若干 bank 做 instancing（GroundDetailPopulation.cs:31-60）。
//   风摆来自 Tree2WindFrameSource / GroundDetailWindResources，
//   本质是"按世界位置采样风向场 + 顶点高度加权"。
//
// 【与 314 的差异】
//   314 的植被有 31 个专属模板模型（DisplaySet）与多层风场贴图，本项目没有导出这些资源，
//   暂时用工程的 mesh_tree_*.obj + 纯色顶点着色，保留"位置/朝向/缩放/风摆"四项行为。
//
// 【贴图通道】
//   已预留 _BaseMap + _Cutoff：314 的植被模板贴图导出后，由渲染器按 element 用
//   MaterialPropertyBlock 绑定 _BaseMap，即可从纯色切到贴图，无需再改 shader。
//   _BaseMap 默认 white，未绑定贴图时行为与纯色版本完全一致。
Shader "Sango/Terrain314Vegetation"
{
    Properties
    {
        [Header(Surface)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.35
        _BaseColor ("Base Color", Color) = (0.28, 0.42, 0.18, 1)
        _TipColor ("Tip Color", Color) = (0.52, 0.66, 0.28, 1)
        _AmbientBoost ("Ambient Boost", Float) = 0.45

        [Header(Wind)]
        // 风向：xz 为水平方向，w 为强度
        _WindDirection ("Wind Direction", Vector) = (1, 0.35, 0, 1)
        _WindStrength ("Wind Strength", Float) = 22
        _WindFrequency ("Wind Frequency", Float) = 1.6
        // 顶点位移从根部到顶端的过渡高度（局部空间）
        _WindHeight ("Wind Height", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }
        LOD 200

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            // 贴图与采样器不放进 UnityPerMaterial：SRP Batcher 要求贴图在 CBUFFER 外单独声明
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Cutoff;
                float4 _BaseColor;
                float4 _TipColor;
                float _AmbientBoost;
                float4 _WindDirection;
                float _WindStrength;
                float _WindFrequency;
                float _WindHeight;
            CBUFFER_END

            struct VertexInput
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct VertexOutput
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float2 uv : TEXCOORD4;
            };

            /// <summary>
            /// 风摆：两段正弦叠加，按顶点在模型内的高度加权。
            /// 314 是采样风场贴图（GroundDetailWindResources），这里用世界位置驱动的
            /// 程序化风向场替代，保留"整片植被不同步摆动"的观感。
            /// </summary>
            float3 ApplyWind(float3 positionWS, float3 positionOS)
            {
                float2 windDirection = normalize(_WindDirection.xy + float2(1e-5, 0.0));
                float wave = sin(_Time.y * _WindFrequency + positionWS.x * 0.013 + positionWS.z * 0.017)
                           + 0.45 * sin(_Time.y * (_WindFrequency * 2.3) + positionWS.x * 0.041 - positionWS.z * 0.037);
                // 根部不动，顶端摆幅最大
                float heightWeight = saturate(positionOS.y / max(_WindHeight, 1e-3));
                heightWeight *= heightWeight;
                float2 offset = windDirection * (wave * _WindStrength * heightWeight * _WindDirection.w);
                return float3(offset.x, 0.0, offset.y);
            }

            VertexOutput vert(VertexInput input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                VertexOutput output;
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.vertex.xyz);
                positionWS += ApplyWind(positionWS, input.vertex.xyz);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normal);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(VertexOutput input) : SV_Target
            {
                // 双面：背面把法线翻过来，否则树冠内部会全黑。
                // 不用 FRONT_FACE 语义，改为按视线方向判断，避免依赖各图形 API 的宏差异。
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                if (dot(normalWS, viewDirection) < 0.0) normalWS = -normalWS;

                // element 之间的色彩差异由 MaterialPropertyBlock 覆盖 _BaseColor 实现；
                // 顶点色只在模型自带时当作亮度调制（OBJ 默认全白，不影响结果）。
                float3 tint = _BaseColor.rgb * lerp(0.85, 1.15, saturate(input.color.g));

                // 贴图：未绑定（默认 white）时 _BaseMap 采样恒为 1，退化成原来的纯色着色；
                // 绑定 314 导出的植被贴图后，用 A 通道做 alpha clip（树冠镂空）。
                half4 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(surface.a - _Cutoff);
                tint *= surface.rgb;

                Light mainLight = GetMainLight();
                float lambert = saturate(dot(normalWS, mainLight.direction)) * 0.85 + 0.15;
                float3 color = tint * (mainLight.color * lambert + _AmbientBoost);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
