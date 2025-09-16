// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Gizmo Solid Component"
{
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		_IsLit ("Is Lit", int) = 1
		_LightDir ("Light Dir", Vector) = (1, 1, 1, 0)
		_LightIntensity ("Light Intensity", float) = 1.5
		_CullMode ("Cull Mode", int) = 2			
		_StencilRefValue ("Stencil Ref Value", int) = 1
		_ZTest ("ZTest", int) = 1
		_ZWrite("ZWrite", int) = 1
	}

	Subshader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
		Pass
		{
			Tags {
					"LightMode" = "UniversalForward"
				}

			Blend SrcAlpha OneMinusSrcAlpha
			ZTest [_ZTest]
			ZWrite [_ZWrite]
			Cull [_CullMode]

			Stencil
			{
				Ref [_StencilRefValue]		
				Comp Always					
				Pass Replace					
			}

			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"

			#pragma fragment frag
			#pragma vertex vert

			CBUFFER_START(UnityPerMaterial)
			float4 _Color;
			int _IsLit;
			float4 _LightDir;
			float _LightIntensity;
			CBUFFER_END

			struct vInput
			{
				float4 vertexPos : POSITION;
				float3 vertexNormal : NORMAL;
			};

			struct vOutput
			{
				float4 clipPos : SV_POSITION;
				float3 worldNormal : TEXCOORD1;
			};

			vOutput vert(vInput input)
			{
				vOutput o;
				o.clipPos = TransformObjectToHClip(input.vertexPos.xyz);
				float3 normalWS = TransformObjectToWorldNormal(input.vertexNormal);
				o.worldNormal = normalWS;
				return o;
			}

			float4 frag(vOutput o) : COLOR
			{
				if(_IsLit == 0) return _Color;
				else
				{
					o.worldNormal = normalize(o.worldNormal);

					float minInfluence = 0.35f;
					float lightInfluence = saturate(dot(-_LightDir.xyz, o.worldNormal));
					lightInfluence = saturate(lightInfluence + minInfluence * (1.0f - lightInfluence));

					return _Color * float4(lightInfluence * _LightIntensity, lightInfluence * _LightIntensity, lightInfluence * _LightIntensity, 1.0f);
				}
			}
			ENDHLSL
		}
	}
}