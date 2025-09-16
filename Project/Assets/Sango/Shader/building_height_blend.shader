
Shader "Sango/building_height_blend" {
	Properties{
		_MainTex("MainTex", 2D) = "white" {}
		_BlendHeight("BlendHeight", float) = -0.8
		_BlendPower("BlendPower", float) = 25
		_OutlineWidth("width", float) = 0.0001
		_BaseColorIntensity("BaseColorFactor", float) = 0.6
		}
		SubShader{
			Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "TransparentCutout" }
			LOD 300

			Pass {
				Name "FORWARD"
				Tags {
					"LightMode" = "UniversalForward"
				}
				Blend SrcAlpha OneMinusSrcAlpha
				HLSLPROGRAM
				#define SANGO_FOG 1
				#define SANGO_ALPHA_TEST 1
				#define SANGO_TERRAIN_TYPE 1
				#define SANGO_BLEND_HEIGHT 1
				#define SANGO_BASE_COLOR 1
				
				#include "sango_shaderLib.hlsl"
				//#pragma multi_compile_fwdbase
				//#pragma multi_compile_fog
				#pragma skip_variants SHADOWS_SOFT DIRLIGHTMAP_COMBINED
				//#pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
				#pragma multi_compile _ SANGO_EDITOR
				#pragma skip_variants FOG_EXP FOG_EXP2
				#pragma exclude_renderers xbox360 ps3 
				#pragma target 3.0
				#pragma vertex sango_vert
				#pragma fragment sango_frag

				ENDHLSL
			}

			
			// Pass
			//{
			//	Name "OUTLINEPASS"
			//	Tags {
			//		"LightMode" = "SRPDefaultUnlit"
			//	}
			//	Fog { Mode Off }
			//	//ZWrite Off
			//	Cull Front
			//	Blend SrcAlpha OneMinusSrcAlpha
			//	HLSLPROGRAM
			//	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			//	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			//	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			//	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			//	#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"


			//	#pragma vertex vert
			//	#pragma fragment frag

			//	struct VertexInput
			//	{
			//		float4 vertex : POSITION;
			//		float3 normal : NORMAL;
			//	};

			//	struct VertexOutput
			//	{
			//		float4 pos : SV_POSITION;
			//		float4 screenPos : TEXCOORD2;
			//	};

			//	CBUFFER_START(UnityPerMaterial)
			//	float _OutlineWidth;
			//	CBUFFER_END
			//	float _Distance;
			//	float _Power;
			//	float _MixBegin;
			//	float _MixPower;
			//	float _MixEnd;
			//	TEXTURE2D_X_FLOAT(_CameraDepthTexture);
			//	SAMPLER(sampler_CameraDepthTexture);

			//	VertexOutput vert(VertexInput v)
			//	{
			//		VertexOutput o;

			//		float camDist = distance(TransformObjectToWorld(v.vertex.xyz), _WorldSpaceCameraPos);
			//		v.vertex.xyz += normalize(v.normal) * camDist * _OutlineWidth;

			//		o.pos = TransformObjectToHClip(v.vertex.xyz);
			//		o.screenPos = ComputeScreenPos(o.pos);
			//		return o;
			//	}

			//	half4 frag(VertexOutput i) : SV_TARGET
			//	{
			//		float2 screenPos = i.screenPos.xy / i.screenPos.w;
			//		float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenPos).r;
			//		float depthValue = Linear01Depth(depth, _ZBufferParams);
			//		float linear01Depth = pow(saturate((depthValue * 3500 - _MixBegin) / (_MixEnd - _MixBegin)), _MixPower);

			//		half4 finalRGBA = half4(0,0,0, 0.8 * saturate(1 - linear01Depth));//saturate(1-linear01Depth));
			//		//UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
			//		return finalRGBA;
			//	}

			//	ENDHLSL
			//}
 

			pass {

					Name "ShadowCast"

					Tags{ "LightMode" = "ShadowCaster" }
					HLSLPROGRAM

					#pragma vertex ShadowPassVertex
					#pragma fragment ShadowPassFragment

					#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

					CBUFFER_START(UnityPerMaterial)
					CBUFFER_END

					struct Attributes
					{
						float4 positionOS   : POSITION;
						float3 normalOS     : NORMAL;
					};

					struct Varyings
					{
						float4 positionCS   : SV_POSITION;
					};

					Varyings ShadowPassVertex(Attributes input)
					{
						Varyings output;

						float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
						float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

						float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
						output.positionCS = positionCS;
						return output;
					}
					half4 ShadowPassFragment(Varyings input) : SV_TARGET
					{
						return 0;
					}
					ENDHLSL
				}


			}

			//Fallback "Legacy Shaders/Diffuse"
}

