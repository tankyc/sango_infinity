#ifndef SANGO_OUTLINE_LIB
#define SANGO_OUTLINE_LIB

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"
// 【SRP Batcher】与 sango_shaderLib.hlsl 同一套约束：
//   ① 成员不能包在 #if 里（否则不同 Pass/变体的 CBUFFER 大小不同，偏移会错位）；
//   ② 成员一律用 float，不用 half（mediump 在 uniform block 里部分 Mali 驱动读成 0）。
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float _OutlineWidth;
float _Alpha;
float _HorizontalAmount;
float _VerticalAmount;
float _Speed;
float _BlendHeight;
float _BlendPower;
float4 _BaseColor;
float _Cutoff;
float4 _Color;
float _FlashFactor;
float _BaseColorIntensity;
CBUFFER_END

// 纯预处理宏，必须放在 CBUFFER 之外
#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
#define APPLY_FOG 1            
#endif

float _Power;
float _MixBegin;
float _MixPower;
float _MixEnd;
half4 _FogColor;

TEXTURE2D_X_FLOAT(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);
TEXTURE2D(_BaseMap);
#define smp SamplerState_Linear_Repeat
SAMPLER(smp);

struct VertexInput
{
	UNITY_VERTEX_INPUT_INSTANCE_ID
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;

};

struct VertexOutput
{
	UNITY_VERTEX_INPUT_INSTANCE_ID
	float4 pos : SV_POSITION;
	//float4 screenPos : TEXCOORD2;
	float2 uv : TEXCOORD0;
	half fogCoord: TEXCOORD1;

};

VertexOutput outline_vert(VertexInput v)
{
	UNITY_SETUP_INSTANCE_ID(v);

	VertexOutput o;

	float camDist = distance(TransformObjectToWorld(v.vertex.xyz), _WorldSpaceCameraPos) * _OutlineWidth * 0.0006;
	float3 _vertex = v.vertex.xyz;
	//v.vertex.xyz += normalize(v.vertex.xyz) * camDist ;
	v.vertex.xyz += normalize(v.normal) * camDist;

	//v.vertex.xyz += normalize(v.vertex.xyz) * _OutlineWidth;
	o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

	o.pos = TransformObjectToHClip(v.vertex.xyz);
	o.fogCoord = ComputeFogFactor(o.pos.z);

	//o.screenPos = ComputeScreenPos(o.pos);
	UNITY_TRANSFER_INSTANCE_ID(v, o);

	return o;
}


half4 outline_frag(VertexOutput i) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(i);

	/*float2 screenPos = i.screenPos.xy / i.screenPos.w;
	float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenPos).r;
	float depthValue = Linear01Depth(depth, _ZBufferParams);
	float linear01Depth = pow(saturate((depthValue * 980 - _MixBegin) / (_MixEnd - _MixBegin)), _MixPower);*/

	half4 _MainTex_var = SAMPLE_TEXTURE2D(_BaseMap, smp, i.uv);

	clip(_MainTex_var.a - 0.5);

	half4 finalRGBA = 1;

	finalRGBA.rgb = MixFog(0, i.fogCoord);

	//finalRGBA.rgb = lerp(_FogColor.rgb, 0, saturate(1 - linear01Depth));//saturate(1-linear01Depth));
	//UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
	return finalRGBA;
}

#endif