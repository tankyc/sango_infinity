// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Sango/skybox" {
Properties {
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_BeginHeight("Start", Float) = 0
	_EndHeight("End", Float) = 0
	_MixBegin("mixBegin", float) = 800//和天空盒混合距离
	_MixEnd("mixEnd", float) = 800//和天空盒混合距离
	_MixPower("mixPower", float) = 7.5//和天空盒混合强度
}

SubShader {
	Tags {"RenderPipeline" = "UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
	LOD 200
	
	Cull Off
    Lighting Off 
    Fog { Mode Off }  
	ZWrite Off
	ZTest Off
	Blend SrcAlpha OneMinusSrcAlpha 
	
	Pass { 
	
		Name "FORWARD"
		Tags {
			"LightMode" = "UniversalForward"
		}
		HLSLPROGRAM
			
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"
            #pragma skip_variants SHADOWS_SOFT DIRLIGHTMAP_COMBINED
			#pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
			#pragma skip_variants FOG_EXP FOG_EXP2
			#pragma exclude_renderers xbox360 ps3 
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag

			struct VertexInput {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct VertexOutput {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
				float2 fogCoord : TEXCOORD1;
				float4 screenPos : TEXCOORD2;
			};
			CBUFFER_START(UnityPerMaterial)
			float4 _MainTex_ST;
			float _EndHeight;
			float _BeginHeight;
			CBUFFER_END
			TEXTURE2D(_MainTex);
			float _MixBegin;
			float _MixPower;
			float _MixEnd;

			TEXTURE2D_X_FLOAT(_CameraDepthTexture);
			SAMPLER(sampler_CameraDepthTexture);
			#define smp SamplerState_Linear_Repeat
			SAMPLER(smp);

			// factor = (end-z)/(end-start) = z * (-1/(end-start)) + (end/(end-start))
			VertexOutput vert (VertexInput v)
			{
				VertexOutput o = (VertexOutput)0;
				o.vertex = TransformObjectToHClip(v.vertex.xyz);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				float4 posWorld = mul(unity_ObjectToWorld, v.vertex);
				o.fogCoord.x = posWorld.y;
				o.screenPos = ComputeScreenPos(o.vertex);
				return o;
			}
			
			half4 frag (VertexOutput i) : SV_Target
			{
				half4 col = SAMPLE_TEXTURE2D(_MainTex, smp, i.texcoord);
				//col.rgb = lerp((unity_FogColor).rgb, (col).rgb, saturate(i.fogCoord.x));
				float heightAlpha = saturate((i.fogCoord.x - _BeginHeight) / (_EndHeight - _BeginHeight));
				float2 screenPos= i.screenPos .xy / i.screenPos .w;
				float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenPos).r;
				float depthValue = Linear01Depth(depth, _ZBufferParams);

				float linear01Depth = pow(saturate((depthValue*3500 - _MixBegin) / (_MixEnd - _MixBegin)), _MixPower) ;

				col.a = (linear01Depth) * saturate(heightAlpha);
				return half4(col.rgb, col.a);
			}
		ENDHLSL
	}
}

}