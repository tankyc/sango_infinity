
Shader "Sango/flag_urp" {
	Properties{
		_BaseMap("MainTex", 2D) = "white" {}
		_TextTex("TextTex", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)
		_OutlineWidth("width", float) = 3.5
		_TextFactor("textFactor", float) = 1
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
				#define SANGO_COLOR 1
				#define SANGO_AMBIENT_NO_LIGHT 1
				#define SANGO_TEXT 1
				#define SANGO_FLAG 1
				#include "sango_shaderLib.hlsl"
				//#pragma multi_compile_fwdbase
				#pragma multi_compile_fog
				#pragma skip_variants DIRLIGHTMAP_COMBINED
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
				#pragma multi_compile _ SANGO_EDITOR
				#pragma skip_variants FOG_EXP FOG_EXP2
				#pragma exclude_renderers xbox360 ps3 
				#pragma target 3.0
				#pragma vertex sango_vert
				#pragma fragment sango_frag
				#pragma multi_compile_instancing

				ENDHLSL
			}

			Pass
			{
				Name "ShadowCaster"
				Tags{"LightMode" = "ShadowCaster"}

				ZWrite On
				ZTest LEqual
				ColorMask 0
				// 【SRP Batcher】原为 Cull[_Cull]：_Cull 既没有在 Properties 声明，
				// 也不在 UnityPerMaterial CBUFFER 里，会让整个 shader 被判定为
				// "SRP Batcher 不兼容"，旗帜因此永远无法合批。
				// mat_flag 的 _Cull 本来就是 2(Back)，这里直接写成常量，语义不变。
				Cull Back

					HLSLPROGRAM
					#pragma exclude_renderers xbox360 ps3 
					#pragma target 3.0

					#define SANGO_FOG 1
					#define SANGO_ALPHA_TEST 1
					#define SANGO_TERRAIN_TYPE 1
					#define SANGO_COLOR 1
					#define SANGO_AMBIENT_NO_LIGHT 1
					#define SANGO_TEXT 1
					#define SANGO_FLAG 1
					#define _ALPHATEST_ON
					#include "sango_shaderLib.hlsl"
					// -------------------------------------
					// Material Keywords
					#pragma shader_feature_local_fragment _ALPHATEST_ON
					#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

					//--------------------------------------
					// GPU Instancing
					#pragma multi_compile_instancing
					#pragma multi_compile _ DOTS_INSTANCING_ON

					// -------------------------------------
					// Universal Pipeline keywords

					// This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
					#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

					#pragma vertex ShadowPassVertex
					#pragma fragment ShadowPassFragment

					//#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
					#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
					ENDHLSL
				}


			}

			//Fallback "Legacy Shaders/Diffuse"
}

