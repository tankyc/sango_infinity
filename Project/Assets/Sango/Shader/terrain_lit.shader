Shader "Sango/terrain_urp_lit" {
	// terrain_urp 的法线贴图版本：给地表层挂 _BumpMap，用扰动后的世界法线参与漫反射，
	// 由 MapLayer.LayerData.isLit == true 触发（见 MapLayer.SetLit / UpdateMaterial）。
	// 除多了 SANGO_TERRAIN_LIT 分支外，与 Sango/terrain_urp 完全一致。
	Properties{
		_BaseMap("MainTex", 2D) = "white" {}
		_BaseColorIntensity("BaseColorFactor", float) = 0.85
		_OutlineWidth("width", float) = 3.5
		_Alpha("Alpha", float) = 1

		// 法线贴图（运行时由 MapLayer 通过 material.SetTexture("_BumpMap", tex) 设置）
		_BumpMap("NormalMap", 2D) = "bump" {}
		// 法线强度：0 = 关闭凹凸，1 = 原始强度
		_NormalScale("NormalScale", float) = 1.0
		// 法线贴图绿通道方向：0 = OpenGL(Y+)，1 = DirectX(Y-) 需要翻转
		_NormalFlipY("NormalFlipY", float) = 0.0
	}
		SubShader{
			Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "TransparentCutout" }
			LOD 300

			Cull[_Cull]

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
				#define SANGO_TERRAIN_LIT 1
				#define SANGO_FACTION_MAP 1
				#include "sango_shaderLib.hlsl"

				#pragma skip_variants DIRLIGHTMAP_COMBINED
				//#pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
				#pragma multi_compile _ SANGO_EDITOR
				#pragma multi_compile_instancing
				#pragma multi_compile_fog
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
				#pragma multi_compile _ _SHADOWS_SOFT //阴影抗锯齿

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
				#pragma exclude_renderers xbox360 ps3 
				#pragma target 3.0

				#define SANGO_BASE_COLOR 1
				#define SANGO_GRID_COLOR 1
				#define SANGO_FOG 1
				#define SANGO_BRUSH 1
				#define SANGO_TERRAIN_TYPE 1
				#define SANGO_TERRAIN 1
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

				#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
				ENDHLSL
			}
		}

		Fallback "Sango/sango_urp"
}
