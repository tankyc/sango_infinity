Shader "Sango/blit"
{
	Properties{
		_MainTex ("Texture", 2D) = "white" {}
		}

	SubShader{
		Tags { "RenderType"="UniversalPipeline" }
		LOD 100
		ZTest Always Cull Off ZWrite Off Fog{ Mode Off }
		Blend SrcAlpha OneMinusSrcAlpha
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _MainTex_ST;
			CBUFFER_END
			TEXTURE2D(_MainTex);

			#define smp SamplerState_Linear_Clamp
			SAMPLER(smp);

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = TransformObjectToHClip(v.vertex.xyz);
				//o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.uv = v.uv;
				return o;
			}
			half4 frag (v2f i) : SV_Target
			{
				// sample the texture
				//float size = _BrushSize;
				float2 uv = TRANSFORM_TEX(i.uv, _MainTex);
				////uv = uv - _BrushUV.xy;
				//uv *= size;
				half4 col = SAMPLE_TEXTURE2D(_MainTex, smp, uv);
				//if(col.a<0.1)
				//{
				//	col.a = 0;
				//}
				// //float3 gammaToLinear8 = LinearToSRGB(col.rgb);
				//float3 gammaToLinear8 = SRGBToLinear(col.rgb);
    //            col.rgb = gammaToLinear8;
				return col;
			}
							
			ENDHLSL
		}


	}
}