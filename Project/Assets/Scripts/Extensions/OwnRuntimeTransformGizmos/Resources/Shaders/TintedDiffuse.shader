// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "TintedDiffuse" 
{
	Properties 
	{
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("MainTexture", 2D) = "white" {}
		_ZWrite ("ZWrite", int) = 1
	}
	SubShader 
	{
		Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent"  "Queue" = "Transparent" }

		Pass
		{
			Tags {
					"LightMode" = "UniversalForward"
				}
			Blend SrcAlpha OneMinusSrcAlpha
			ZTest On
			Cull Off
			ZWrite [_ZWrite]

			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"

			#pragma vertex vert
			#pragma fragment frag

			CBUFFER_START(UnityPerMaterial)
			float4 _Color;
			CBUFFER_END
			TEXTURE2D(_MainTex);
			#define smp SamplerState_Linear_Repeat
			SAMPLER(smp);

			struct vInput 
			{
				float4 vertexPos : POSITION;
				float2 vertexUV : TEXCOORD0;
			};

			struct vOutput
			{
				float4 clipPos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			vOutput vert(vInput input)
			{
				vOutput o;
				o.clipPos = TransformObjectToHClip(input.vertexPos.xyz);
				o.uv = input.vertexUV;

				return o;
			}

			float4 frag(vOutput input) : COLOR
			{		
				return SAMPLE_TEXTURE2D(_MainTex, smp, input.uv) * _Color;
			}
			ENDHLSL
		}
	}
}
