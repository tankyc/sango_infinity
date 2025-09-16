// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "GLLine"
{
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
		Pass
		{
			Tags {
					"LightMode" = "UniversalForward"
				}

			ZTest On	
			ZWrite Off			
			Lighting Off						
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"

			#pragma vertex vert
			#pragma fragment frag

			struct vInput
			{
				float4 vertexPos : POSITION;
				float4 vertexColor : COLOR;
			};

			struct vOutput
			{
				float4 clipPos : SV_POSITION;
				float4 color : COLOR;
			};

			vOutput vert(vInput input)
			{
				vOutput o;
				o.clipPos = TransformObjectToHClip(input.vertexPos.xyz);
				o.color = input.vertexColor;

				return o;
			}

			float4 frag(vOutput input) : COLOR
			{
				return input.color;
			}
				ENDHLSL
		}
	}
}
