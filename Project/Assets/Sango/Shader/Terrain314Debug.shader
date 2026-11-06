// Terrain314 channel inspector.
// Purpose: verify the meaning of each 314 terrain texture channel before
// committing to a blend formula. All labels are ASCII on purpose: a previous
// version used Chinese property names and the multi-byte characters got
// truncated on disk, which broke ShaderLab parsing.
Shader "Sango/Terrain314Debug"
{
    Properties
    {
        // 0 height, 1 control rgba, 2..5 control r/g/b/a,
        // 6..8 field0..2, 9 auxiliary
        _Mode ("Display Mode", Float) = 1

        _TerrainSize ("Terrain World Size", Float) = 204800
        _ControlTex ("Initial Control", 2D) = "black" {}
        _Field0Tex ("Field0", 2D) = "black" {}
        _Field1Tex ("Field1", 2D) = "black" {}
        _Field2Tex ("Field2", 2D) = "black" {}
        _AuxiliaryTex ("Auxiliary", 2D) = "black" {}
        _HeightTex ("Height Normalized", 2D) = "black" {}
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
            Name "Terrain314Debug"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

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
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_ControlTex);   SAMPLER(sampler_ControlTex);
            TEXTURE2D(_Field0Tex);    SAMPLER(sampler_Field0Tex);
            TEXTURE2D(_Field1Tex);    SAMPLER(sampler_Field1Tex);
            TEXTURE2D(_Field2Tex);    SAMPLER(sampler_Field2Tex);
            TEXTURE2D(_AuxiliaryTex); SAMPLER(sampler_AuxiliaryTex);
            TEXTURE2D(_HeightTex);    SAMPLER(sampler_HeightTex);

            CBUFFER_START(UnityPerMaterial)
                float _Mode;
                float _TerrainSize;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            // World XZ to terrain UV. 314 uses the opposite V direction.
            float2 WorldToTerrainUV(float3 positionWS)
            {
                float2 uv = positionWS.xz / max(_TerrainSize, 1.0);
                return float2(uv.x, 1.0 - uv.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = saturate(WorldToTerrainUV(input.positionWS));
                int mode = (int)round(_Mode);

                if (mode == 0)
                {
                    half h = SAMPLE_TEXTURE2D(_HeightTex, sampler_HeightTex, uv).r;
                    return half4(h, h, h, 1);
                }

                half4 control = SAMPLE_TEXTURE2D(_ControlTex, sampler_ControlTex, uv);
                if (mode == 1) return control;
                if (mode == 2) return half4(control.rrr, 1);
                if (mode == 3) return half4(control.ggg, 1);
                if (mode == 4) return half4(control.bbb, 1);
                if (mode == 5) return half4(control.aaa, 1);
                if (mode == 6) return half4(SAMPLE_TEXTURE2D(_Field0Tex, sampler_Field0Tex, uv).rgb, 1);
                if (mode == 7) return half4(SAMPLE_TEXTURE2D(_Field1Tex, sampler_Field1Tex, uv).rgb, 1);
                if (mode == 8) return half4(SAMPLE_TEXTURE2D(_Field2Tex, sampler_Field2Tex, uv).rgb, 1);
                return half4(SAMPLE_TEXTURE2D(_AuxiliaryTex, sampler_AuxiliaryTex, uv).rgb, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
