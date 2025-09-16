// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "XZGrid"
{
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		_CamFarPlane ("Cam Far Plane", float) = 1000
		_CamLook ("Cam Look", Vector) = (0, 0, 1, 0)
		_FadeScale ("Fade Scale", float) = 1
	}

	Subshader
	{
		Tags {"RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True"}
		Pass
		{
			Tags {
					"LightMode" = "UniversalForward"
				}
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off

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
			float _CamFarPlane;
			float4 _CamLook;
			float _FadeScale;
			CBUFFER_END

			struct vInput
			{
				float4 vertexPos : POSITION;
			};

			struct vOutput
			{
				float4 clipPos : SV_POSITION;
				float3 viewPos : TEXCOORD0;
			};

			vOutput vert(vInput input)
			{
				vOutput o;
				o.clipPos = TransformObjectToHClip(input.vertexPos.xyz);
				o.viewPos = TransformWorldToView(input.vertexPos.xyz);

				return o;
			}

			float4 frag(vOutput o) : COLOR
			{
				float distFromFarPlane = abs(o.viewPos.z);
				float farPlaneAlphaScale = saturate(1.0f - distFromFarPlane / (_CamFarPlane * max(1.0f, _FadeScale) * 0.1f * (1000.0f / _CamFarPlane)));
				return float4(_Color.rgb, _Color.a * farPlaneAlphaScale);
			}
				ENDHLSL
		}
	}
}