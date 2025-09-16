Shader "Sango/brush"
{
	Properties{
		_MainTex ("Texture", 2D) = "white" {}
		//_BrushTex("Brush Texture",2D)= "white" {}
		//_Color("Color",Color)=(1,1,1,1)
		//_UV("UV",Vector)=(0,0,0,0)
		//_Size("Size",Range(1,1000))=1
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
			float4 _Brush;
			half4 _BrushUV;
			float _BrushSize;
			half4 _BrushColor;
			TEXTURE2D(_MainTex);
			TEXTURE2D(_BrushTex);

			#define smp SamplerState_Linear_Clamp
			SAMPLER(smp);

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = TransformObjectToHClip(v.vertex.xyz);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			half4 frag (v2f i) : SV_Target
			{
				// sample the texture
				float size = _BrushSize;
				float2 uv = i.uv + (0.5f/size);
				uv = uv - _BrushUV.xy;
				uv *= size;
				half4 col = SAMPLE_TEXTURE2D(_BrushTex, smp, uv);
				//half4 srcColor = SAMPLE_TEXTURE2D(_MainTex,smp, i.uv);
				col.rgb = 1;
                    //float3 gammaToLinear8 = GammaToLinearSpace(temp_cast_2);
                   

				if(col.a<0.1)
				{
					col.a = 0;
				}
				col  *= _BrushColor;
				 //float3 gammaToLinear8 = LinearToSRGB(col.rgb);
				/*float3 gammaToLinear8 = SRGBToLinear(col.rgb);
                col.rgb = gammaToLinear8;*/
				//col.a = 1;
				return col;
			}
							
			ENDHLSL
		}


	}
}