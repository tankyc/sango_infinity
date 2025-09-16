
Shader "Sango/outline_urp" {
	Properties{
		_MainTex("MainTex", 2D) = "white" {}
		_OutlineWidth("width", float) = 0.004//定义一个变量
	}
		SubShader{
			Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "TransparentCutout" }

			Pass
			{
				Name "OUTLINEPASS"
				Tags {
					"LightMode" = "UniversalForward"
				}
				Fog { Mode Off }
				ZWrite Off
				Cull Front
				Blend SrcAlpha OneMinusSrcAlpha
				HLSLPROGRAM
				#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
				#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"


				#pragma vertex vert
				#pragma fragment frag

				struct VertexInput
				{
					float4 vertex : POSITION;
					float3 normal : NORMAL;
					float2 uv : TEXCOORD0;
				};

				struct VertexOutput
				{
					float4 pos : SV_POSITION;
					float4 screenPos : TEXCOORD2;
					float2 uv : TEXCOORD0;

				};

				CBUFFER_START(UnityPerMaterial)
				float _OutlineWidth;
				float4 _MainTex_ST; 
				CBUFFER_END
				float _Power;
				float _MixBegin;
				float _MixPower;
				float _MixEnd;
				TEXTURE2D_X_FLOAT(_CameraDepthTexture);
				SAMPLER(sampler_CameraDepthTexture);
				TEXTURE2D(_MainTex);
				#define smp SamplerState_Linear_Repeat
				SAMPLER(smp);

				VertexOutput vert(VertexInput v)
				{
					VertexOutput o;

					//float camDist = distance(TransformObjectToWorld(v.vertex.xyz), _WorldSpaceCameraPos);
					//v.vertex.xyz += normalize(v.vertex.xyz) * camDist * _OutlineWidth;

					
					v.vertex.xyz += normalize(v.vertex.xyz) * _OutlineWidth;
					o.uv = TRANSFORM_TEX(v.uv, _MainTex);

					o.pos = TransformObjectToHClip(v.vertex.xyz);
					o.screenPos = ComputeScreenPos(o.pos);
					return o;
				}

				half4 frag(VertexOutput i) : SV_TARGET
				{
					float2 screenPos = i.screenPos.xy / i.screenPos.w;
					float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenPos).r;
					float depthValue = Linear01Depth(depth, _ZBufferParams);
					float linear01Depth = pow(saturate((depthValue * 3500 - _MixBegin) / (_MixEnd - _MixBegin)), _MixPower);
					half4 _MainTex_var = SAMPLE_TEXTURE2D(_MainTex, smp, i.uv);

					clip(_MainTex_var.a - 0.5);

					half4 finalRGBA = half4(0,0,0, 0.8 * saturate(1 - linear01Depth));//saturate(1-linear01Depth));
					//UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
					return finalRGBA;
				}

				ENDHLSL

			}
		}
}

