
Shader "Sango/terrain_urp" {
	Properties{
		_MainTex("MainTex", 2D) = "white" {}
		_BaseColorIntensity("BaseColorFactor", float) = 1
		_Alpha("Alpha", float) = 1

		//_ShadowColor("Shadow Color", Color) = (0.2,0.2,1,1)
		//_OutlineWidth("width", float) = 0.14//定义一个变量
		

		//_BaseTex("BaseTex", 2D) = "white" {}
		//_GridTex("Grid", 2D) = "white" {}
		//_GridMask("Grid Mask", 2D) = "black" {}
		//_GridFlag("gridFlag", float) = 1//定义一个变量
		//_DarkFlag("gridFlag", float) = 1//定义一个变量
		//_MixBegin("mixBegin", float) = 800//和天空盒混合距离
		//_MixEnd("mixEnd", float) = 800//和天空盒混合距离
		//_MixPower("mixPower", float) = 7.5//和天空盒混合强度

		}
		SubShader{
			Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "TransparentCutout" }
			LOD 300
			
			Pass {
				Name "FORWARD"
				Tags {
					"LightMode" = "UniversalForward"
				}
				Fog { Mode Off }  
				Blend SrcAlpha OneMinusSrcAlpha
				HLSLPROGRAM
				#define SANGO_BASE_COLOR 1
				#define SANGO_GRID_COLOR 1
				#define SANGO_FOG 1
				#define SANGO_BRUSH 1
				#define SANGO_TERRAIN_TYPE 1
				#define SANGO_TERRAIN 1
				#include "sango_shaderLib.hlsl"

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
			Pass
				{
					Name "ShadowCaster"
					Tags{"LightMode" = "ShadowCaster"}

					ZWrite On
					ZTest LEqual
					ColorMask 0
					Cull[_Cull]

					HLSLPROGRAM
					#pragma only_renderers gles gles3 glcore d3d11
					#pragma target 2.0

					//--------------------------------------
					// GPU Instancing
					#pragma multi_compile_instancing

					// -------------------------------------
					// Material Keywords
					#pragma shader_feature_local_fragment _ALPHATEST_ON
					#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

					// -------------------------------------
					// Universal Pipeline keywords

					// This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
					#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

					#pragma vertex ShadowPassVertex
					#pragma fragment ShadowPassFragment

					#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
					#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
					ENDHLSL
				}

				Pass
				{
					Name "DepthOnly"
					Tags{"LightMode" = "DepthOnly"}

					ZWrite On
					ColorMask 0
					Cull[_Cull]

					HLSLPROGRAM
					#pragma only_renderers gles gles3 glcore d3d11
					#pragma target 2.0

					//--------------------------------------
					// GPU Instancing
					#pragma multi_compile_instancing

					#pragma vertex DepthOnlyVertex
					#pragma fragment DepthOnlyFragment

					// -------------------------------------
					// Material Keywords
					#pragma shader_feature_local_fragment _ALPHATEST_ON
					#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

					#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
					#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
					ENDHLSL
				}
					
			}

			Fallback "Sango/sango_urp"
}

