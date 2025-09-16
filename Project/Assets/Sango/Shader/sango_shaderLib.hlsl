#ifndef SANGO_SHADER_LIB
#define SANGO_SHADER_LIB

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"
           
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;

#if SANGO_WATER | SANGO_TERRAIN
half _Alpha;
#endif

#if SANGO_WATER
float _HorizontalAmount;
float _VerticalAmount;
float _Speed;
#endif

#if SANGO_BLEND_HEIGHT
float _BlendHeight;
float _BlendPower;
#endif

#if SANGO_COLOR
half4 _Color;
#endif

CBUFFER_END


// 阴影颜色
half4 _ShadowColor;
TEXTURE2D(_MainTex);
float _MapWidth;
float _MapHeight;

half _GridSize;

// 地格显示
#if SANGO_GRID_COLOR
half _GridFlag;
float _DarkFlag;
#endif

// fog
#if SANGO_FOG
float _FogFlag;
half4 _FogColor = half4(0,0,0,0);
float _MixBegin;
float _MixPower;
float _MixEnd;
#endif

// 编辑器

#if defined(SANGO_EDITOR)

#if SANGO_BRUSH
// 笔刷
float _BrushType;
float4 _Brush;
half4 _BrushUV;
float _BrushSize;
half4 _BrushColor;
TEXTURE2D(_BrushTex);
#endif

// 地格信息显示
#if SANGO_TERRAIN_TYPE
float _TerrainTypeShowFlag;
float _terrainTypeMaskCol;
float _terrainTypeMaskRow;
float _terrainTypeAlpha;
TEXTURE2D(_TerrainTypeTex);
TEXTURE2D(_TerrainTypeMaskTex);
#endif

#define smp_clamp SamplerState_Linear_Clamp
#define smp_point_clamp SamplerState_Point_Clamp
SAMPLER(smp_clamp);
SAMPLER(smp_point_clamp);
#endif


#if SANGO_BASE_COLOR
TEXTURE2D(_BaseTex);
float _BaseColorIntensity;
#endif

#if SANGO_GRID_COLOR
TEXTURE2D(_GridTex);
TEXTURE2D(_GridMask);
TEXTURE2D(_RangeMask);
TEXTURE2D(_DarkMask);
#endif

TEXTURE2D_X_FLOAT(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

//为了方便操作 定义预定义
#define smp SamplerState_Linear_Repeat
#define smpPoint SamplerState_Point_Repeat
// SAMPLER(sampler_MainTex); 默认采样器
SAMPLER(smp);
SAMPLER(smpPoint);

struct SangoVertexInput
{
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 vertColor : COLOR;
	float2 uv : TEXCOORD0;
};

struct SangoVertexOutput
{
	float4 pos : SV_POSITION;
	float4 vertColor : COLOR;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
	float3 posWorld		: TEXCOORD1;
	float4 screenPos : TEXCOORD2;
	float4 shadowCoord : TEXCOORD3;
#if SANGO_BLEND_HEIGHT
	float3 posObject	: TEXCOORD4;
#endif
};

float2 SangoWaterTransofromUV(SangoVertexOutput i)
{
#if SANGO_WATER
	float c_add = floor((i.posWorld.z + i.posWorld.x)/ (120) % 225);
	float time = floor(_Speed * _Time.y + c_add);
	//time = time - floor(time / 18) * 18;  
	float row = floor(time / _HorizontalAmount);    // /运算获取当前行
	float column = floor(time - row * _HorizontalAmount);  // %运算获取当前列

	//首先把原纹理坐标i.uv按行数和列数进行等分，然后使用当前的行列进行偏移
	float2 uv = i.uv + half2(column, -row);
	uv.x /= _HorizontalAmount;
	uv.y /= _VerticalAmount;
	return uv;
#else
	return i.uv;
#endif
}


SangoVertexOutput sango_vert(SangoVertexInput v) 
{
	SangoVertexOutput o = (SangoVertexOutput)0;
	o.uv = TRANSFORM_TEX(v.uv,_MainTex);

	VertexNormalInputs vertexNormalInput = GetVertexNormalInputs(v.normal);
	o.normal = vertexNormalInput.normalWS;

	VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
	o.pos = vertexInput.positionCS;
	o.posWorld = vertexInput.positionWS;

	#if SANGO_BLEND_HEIGHT
	o.posObject = v.vertex.xyz;
	#endif

	o.vertColor = v.vertColor;

	o.shadowCoord = GetShadowCoord(vertexInput);
	o.screenPos = ComputeScreenPos(o.pos);
	return o;
}

float4 sango_frag(SangoVertexOutput i) : COLOR
{
	#if SANGO_WATER
	i.uv = SangoWaterTransofromUV(i);
	#endif

	half4 _MainTex_var = SAMPLE_TEXTURE2D(_MainTex,smp,i.uv);

	#if SANGO_ALPHA_TEST 
	clip(_MainTex_var.a - 0.5);
	#endif

	Light mainLight = GetMainLight(i.shadowCoord);
	float3 lightDirection = mainLight.direction;
	half3 ambient = SampleSH(i.normal).rgb;
	//阴影数据
	half shadow = mainLight.shadowAttenuation;
	// 计算漫反射颜色
	half NdotL = saturate(dot(lightDirection, i.normal));
	half3 directDiffuse = lerp(_ShadowColor.rgb, ((mainLight.color.rgb)), NdotL) * shadow + ambient;
	half3 diffuse = directDiffuse *_MainTex_var.rgb;

	// 计算BASE遮罩
	#if SANGO_BASE_COLOR
	float2 baseUV = float2(i.posWorld.z / (_MapWidth), 1 - i.posWorld.x / (_MapHeight));
	half4 baseColor = SAMPLE_TEXTURE2D(_BaseTex, smp, baseUV);
	half3 baseDiffuse = lerp(diffuse, baseColor.rgb*diffuse,  _BaseColorIntensity);
	diffuse = baseDiffuse;
	#endif

	

	#if SANGO_TERRAIN_TYPE
#if defined(SANGO_EDITOR)
	float  terrainTyperow = floor(i.posWorld.z / (_GridSize) % 2);    // /运算获取当前行
	float2 terrainTypegridUV = float2(i.posWorld.z / (_GridSize), 1 - i.posWorld.x / (_GridSize)+terrainTyperow * 0.5);
	float2 terrainTypebaseUV = float2(i.posWorld.z / (_MapWidth), 1 - i.posWorld.x / (_MapHeight));
	float2 terrainTypegridMaskUV = float2(terrainTypebaseUV.x, terrainTypebaseUV.y + terrainTyperow * 0.5 / (_MapHeight / _GridSize));
	float4 terrainTypeShowUV = SAMPLE_TEXTURE2D(_TerrainTypeMaskTex, smpPoint, terrainTypegridMaskUV);
	//float3 gammaToLinear8 = LinearToSRGB(terrainTypeShowUV.rgb);
	float3 gammaToLinear8 = terrainTypeShowUV.rgb;
					
	float2 tyUV = float2(terrainTypegridUV.x - floor(terrainTypegridUV.x), terrainTypegridUV.y - floor(terrainTypegridUV.y));
	float2 ttUV = gammaToLinear8.xy + float2(tyUV.x / _terrainTypeMaskCol, tyUV.y / _terrainTypeMaskRow);

	half4 tarrainTypeColor = SAMPLE_TEXTURE2D(_TerrainTypeTex, smpPoint, ttUV);

	//diffuse = lerp(diffuse, tarrainTypeColor, _TerrainTypeShowFlag * _terrainTypeAlpha);
	diffuse = diffuse + tarrainTypeColor * _TerrainTypeShowFlag * _terrainTypeAlpha;
#endif
	#endif

#if SANGO_GRID_COLOR
	float row = floor(i.posWorld.z / (_GridSize) % 2);    // /运算获取当前行
	float2 gridUV = float2(i.posWorld.z / (_GridSize), 1 - i.posWorld.x / (_GridSize)+row * 0.5);
	half4 gridColor = SAMPLE_TEXTURE2D(_GridTex, smp, gridUV);
	float2 gridMaskUV = float2(baseUV.x, baseUV.y + row * 0.5 / (_MapHeight / _GridSize));
	half4 gridMaskColor = SAMPLE_TEXTURE2D(_GridMask, smpPoint, gridMaskUV);
	half4 rangeMaskColor = SAMPLE_TEXTURE2D(_RangeMask, smpPoint, gridMaskUV);
	half flag = saturate(gridColor.a * _GridFlag);
	diffuse = lerp(diffuse, gridColor.rgb, gridColor.a * _GridFlag * gridMaskColor.a * 1);
	diffuse = lerp(diffuse, half3(0, 0.9, 0), flag * gridMaskColor.g);
	diffuse = lerp(diffuse, half3(0, 0, 0.9), flag * gridMaskColor.b);
	diffuse = lerp(diffuse, half3(0.9, 0, 0), flag * gridMaskColor.r);
	diffuse = lerp(diffuse, diffuse * half3(0.33, 0.27, 0.21), saturate(_DarkFlag * (1 - gridMaskColor.r)));
	diffuse = lerp(diffuse, diffuse * rangeMaskColor.rgb, rangeMaskColor.a);
#endif

	#if SANGO_BRUSH
	#if defined(SANGO_EDITOR)
	if(_BrushType > 0)
	{
		float size = _BrushSize;
		float2 uv = baseUV + (0.5f/size);
		uv = uv - _BrushUV.xy;
		uv *= size;
		half4 col = SAMPLE_TEXTURE2D(_BrushTex, smp_point_clamp,uv);
		col.rgb = 1;
		col *= _BrushColor;
		/*float3 gammaToLinear8 = SRGBToLinear(col.rgb);
		col.rgb = gammaToLinear8;*/

		diffuse = lerp(diffuse, baseColor.rgb, _BrushUV.z);
		diffuse = lerp(diffuse, col.rgb, col.a);
	}
	else
	{
		float3 brushCenter = _Brush.xyz;
		float3 pixsPos = i.posWorld;
		brushCenter.y = 0;
		pixsPos.y = 0;
		float d = saturate(distance(brushCenter, pixsPos) / _Brush.w + 0.34);
		diffuse = lerp(half3(0, 0.9, 0), diffuse, d);
	}
	#endif
	#endif
		
	#if SANGO_COLOR
		diffuse = diffuse * _Color.rgb;
	#endif

	//
	// light
	// 雾效处理
	#if SANGO_WATER
		half4 finalRGBA = half4(diffuse, _Alpha * _MainTex_var.a * saturate(i.vertColor.a));
	#elif SANGO_TERRAIN
		half4 finalRGBA = half4(diffuse, _Alpha * saturate(i.vertColor.a));
	#else
		half4 finalRGBA = half4(diffuse,  saturate(i.vertColor.a));
	#endif

	#if SANGO_BLEND_HEIGHT
	finalRGBA.a = finalRGBA.a * saturate(pow( i.posObject.y - _BlendHeight, _BlendPower));
	#endif

	#if SANGO_FOG && FOG_EXP
		float2 screenPos= i.screenPos .xy / i.screenPos .w;
		float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenPos).r;
		float depthValue = LinearEyeDepth(depth, _ZBufferParams);
		float linear01Depth = pow(saturate((depthValue - _MixBegin) / (_MixEnd - _MixBegin)), _MixPower) ;

		finalRGBA = lerp(finalRGBA,  _FogColor,  linear01Depth * _FogColor.a);
		//finalRGBA = float4(depthValue,depthValue,depthValue,1);
	#endif

	return finalRGBA;
}

#endif