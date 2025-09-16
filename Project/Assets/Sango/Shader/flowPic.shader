Shader "Sango/flowPic"
{
	Properties{
		_MainTex ("Texture", 2D) = "white" {}
		_FlowTex ("Flow", 2D) = "white" {}
		_FlowSpeed("FlowSpeed", float)=1
		_FlowPower("FlowPower", float)=1
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
			float4 _FlowTex_ST;
			float _FlowSpeed;
			float _FlowPower;
			CBUFFER_END
			TEXTURE2D(_MainTex);
			TEXTURE2D(_FlowTex);

			#define smp SamplerState_Linear_Repeat
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
				half2 flow = SAMPLE_TEXTURE2D(_FlowTex,smp,i.uv).rg;
				flow = (flow-0.5)*2;
				flow.x = -flow.x;
					
				float p0 = frac(_Time.y*_FlowSpeed*0.1);
				float p1 = frac(_Time.y*_FlowSpeed*0.1 + 0.5);
				half3 c0 = SAMPLE_TEXTURE2D(_MainTex,smp,i.uv - flow*p0*_FlowPower).rgb;
				half3 c1 = SAMPLE_TEXTURE2D(_MainTex,smp,i.uv - flow*p1*_FlowPower).rgb;
				float lerpflow = abs((p0-0.5)*2);
				half3 c2 = lerp(c0,c1, lerpflow);

				return half4(c2, 1);
			}
							
			ENDHLSL
		}


	}
}