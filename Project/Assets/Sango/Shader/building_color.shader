
Shader "Sango/building_color_urp" {
	Properties{
		_BaseMap("MainTex", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)
		_OutlineWidth("width", float) = 3.5
		_BaseColorIntensity("BaseColorFactor", float) = 0.7
		}
		SubShader{
			Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "TransparentCutout" }
			LOD 300

		//	Pass
		//	{
		//		Name "OUTLINEPASS"
		//		Tags {
		//			"LightMode" = "SRPDefaultUnlit"
		//		}
		//		Fog { Mode Off }
		//		ZWrite On
		//		Cull Front
		//		Blend SrcAlpha OneMinusSrcAlpha
		//		HLSLPROGRAM
		//		#define SANGO_BASE_COLOR_ADD 1
		//		#define SANGO_COLOR 1
		//		#define SANGO_FOG 1
		//		#define SANGO_ALPHA_TEST 1
		//		#define SANGO_TERRAIN_TYPE 1
		//		#include "sango_outlineLib.hlsl"
		//	//#pragma multi_compile_fwdbase
		//	//#pragma multi_compile_fog
		//	#pragma skip_variants SHADOWS_SOFT DIRLIGHTMAP_COMBINED
		//	//#pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
		//	#pragma multi_compile _ SANGO_EDITOR
		//	#pragma skip_variants FOG_EXP FOG_EXP2
		//	#pragma exclude_renderers xbox360 ps3 
		//	#pragma target 3.0
		//	#pragma vertex outline_vert
		//	#pragma fragment outline_frag
		//	#pragma multi_compile_instancing
		//	#pragma multi_compile_fog

		//	ENDHLSL
		//}
			
			Pass {
				Name "FORWARD"
				Tags {
					"LightMode" = "UniversalForward"
				}
				Blend SrcAlpha OneMinusSrcAlpha
				HLSLPROGRAM
				#define SANGO_BASE_COLOR 1
				#define SANGO_FOG 1
				#define SANGO_ALPHA_TEST 1
				#define SANGO_TERRAIN_TYPE 1
				#define SANGO_COLOR 1
				
				#include "sango_shaderLib.hlsl"
				//#pragma multi_compile_fwdbase
				//#pragma multi_compile_fog
				#pragma skip_variants DIRLIGHTMAP_COMBINED
				//#pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
				#pragma multi_compile _ SANGO_EDITOR
				#pragma skip_variants FOG_EXP FOG_EXP2
				#pragma exclude_renderers xbox360 ps3 
				#pragma target 3.0
				#pragma vertex sango_vert
				#pragma fragment sango_frag
				#pragma multi_compile_fog

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
				#pragma exclude_renderers xbox360 ps3 
				#pragma target 3.0

				#define SANGO_BASE_COLOR 1
				#define SANGO_FOG 1
				#define SANGO_ALPHA_TEST 1
				#define SANGO_TERRAIN_TYPE 1
				#define SANGO_COLOR 1
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

