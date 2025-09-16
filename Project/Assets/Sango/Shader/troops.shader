
Shader "Sango/troops_urp" {
	Properties{
		_MainTex("MainTex", 2D) = "white" {}
		_MaskTex("MaskTex", 2D) = "black" {}
		_MaskColor("MaskColor", Color) = (1,1,1,1)
		_Alpha("alpha", float) = 0.5
		_HorizontalAmount("Horizontal Amount", Float) = 8
		_HorizontalMax("Horizontal Max", Float) = 8
		_VerticalAmount("Vertical Amount", Float) = 8
		_VerticalIndex("Vertical Index", Float) = 8
		_Speed("Speed", Float) = 10
	}
		SubShader{
			Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "TransparentCutout" }
			LOD 300


			//ZWrite Off
			Lighting Off
			Pass {
				Name "FORWARD"
				Tags {
					"LightMode" = "UniversalForward"
				}
				//Blend SrcAlpha OneMinusSrcAlpha
				HLSLPROGRAM
				#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
				#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"

				#pragma skip_variants SHADOWS_SOFT DIRLIGHTMAP_COMBINED
				#pragma skip_variants FOG_EXP FOG_EXP2
				#pragma exclude_renderers xbox360 ps3 
				#pragma target 3.0
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_instancing


				CBUFFER_START(UnityPerMaterial)
				half _Alpha;
				float _HorizontalAmount;
				float _HorizontalMax;
				float _VerticalIndex;
				float _VerticalAmount;
				float _Speed;
				half4 _MaskColor;
				float4 _MainTex_ST;
				CBUFFER_END


				TEXTURE2D(_MainTex);
				TEXTURE2D(_MaskTex);

				#define smp SamplerState_Linear_Repeat
				// SAMPLER(sampler_MainTex); 默认采样器
				SAMPLER(smp);

				struct VertexInput
				{
					float4 vertex : POSITION;
					float2 uv0 : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct VertexOutput
				{
					float4 pos : SV_POSITION;
					float2 uv0 : TEXCOORD0;
					float3 dir : TEXCOORD1;
					float4 shadowCoord : TEXCOORD2;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					//LIGHTING_COORDS(3, 4)
						//float3 posWorld		: TEXCOORD5;
						//float3 NtoV : TEXCOORD6;
				};

				//定义InstancingBuffer的结构体
				UNITY_INSTANCING_BUFFER_START(troops)
					UNITY_DEFINE_INSTANCED_PROP(float, _StartTime)
				UNITY_INSTANCING_BUFFER_END(troops)

				VertexOutput vert(VertexInput v) {
					UNITY_SETUP_INSTANCE_ID(v);
					VertexOutput o = (VertexOutput)0;
					o.uv0 = TRANSFORM_TEX(v.uv0,_MainTex);
					
					float3 center = float3(0, 0, 0);
					  //物体空间原点
					 //将相机位置转换至物体空间并计算相对原点朝向，物体旋转后的法向将与之平行，这里实现的是Viewpoint-oriented Billboard
					  //float3 viewer = mul(unity_WorldToObject,float4(_WorldSpaceCameraPos, 1));
					
					 float3 viewer = TransformWorldToObject(_WorldSpaceCameraPos.xyz);

					 float3 normalDir = viewer - center;
					 // _VerticalBillboarding为0到1，控制物体法线朝向向上的限制，实现Axial Billboard到World-Oriented Billboard的变换
					 //normalDir.y =	normalDir.y * _VerticalBillboarding;
					 normalDir.y =	0;
					 normalDir = normalize(normalDir);
					 //若原物体法线已经朝向上，这up为z轴正方向，否者默认为y轴正方向
					 float3 upDir = abs(normalDir.y) > 0.999 ? float3(0, 0, 1) : float3(0, 1, 0);
					 //利用初步的upDir计算righDir，并以此重建准确的upDir，达到新建转向后坐标系的目的
					 float3 rightDir = normalize(cross(upDir, normalDir));     upDir = normalize(cross(normalDir, rightDir));
					 // 计算原物体各顶点相对位移，并加到新的坐标系上
					 float3 centerOffs = v.vertex.xyz - center;
					 float3 localPos = center + rightDir * centerOffs.x + upDir * centerOffs.y;

					 VertexPositionInputs vertexInput = GetVertexPositionInputs(localPos);
					 o.pos = vertexInput.positionCS;

					 //float3 forwardPos = float3(0,0,1);
					 //float3 localCamForward = mul(unity_WorldToObject,float4(forwardPos, 1));
					 //float dir = dot(forwardPos, localCamForward);
					 //float lorr = cross(forwardPos, localCamForward).y;
					 //o.dir.x = dir;
					 //o.dir.y = lorr;

					//TRANSFER_VERTEX_TO_FRAGMENT(o);
					UNITY_TRANSFER_INSTANCE_ID (v,o);
					return o;
				}

				float4 frag(VertexOutput i) : COLOR{

					UNITY_SETUP_INSTANCE_ID(i);
					float iStartTime = UNITY_ACCESS_INSTANCED_PROP(troops, _StartTime);
				

					float time = floor(_Speed * _Time.y + iStartTime);
					//time = time - floor(time / 18) * 18;  

					float row = _VerticalIndex;// floor(time / _HorizontalAmount);    // /运算获取当前行

					float column = floor(time % _HorizontalMax);  // %运算获取当前列

					//首先把原纹理坐标i.uv按行数和列数进行等分，然后使用当前的行列进行偏移
					half2 uv = i.uv0 + half2(column, row);
					uv.x /= _HorizontalAmount;
					uv.y /= _VerticalAmount;

					half4 _MainTex_var = SAMPLE_TEXTURE2D(_MainTex, smp, uv);
					half4 _MaskTex_var = SAMPLE_TEXTURE2D(_MaskTex, smp, uv);
					clip( _MainTex_var.a - 0.5f);
					
					half3 diffuse = lerp(_MainTex_var.rgb, _MaskColor.rgb, _MaskTex_var.r);

					//half3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
					//float  atten = LIGHT_ATTENUATION(i);

					//// 计算漫反射颜色
					//half NdotL = max(0, dot( i.normalDir, lightDirection ));
					//half3 directDiffuse = NdotL * _LightColor0.rgb  * atten + UNITY_LIGHTMODEL_AMBIENT.xyz;
					////_MainTex_var.rgb = lerp(_MainTex_var.rgb,  _MainTex_var.rgb * _skincolor.rgb, _MainTex_var.a);
					//half3 diffuse = directDiffuse * _MainTex_var.rgb;
					//half3 baseColor = tex2D(_MainTex2,i.posWorld.xz/(1025*10));

					////half3 diffuse = _MainTex_var2.rgb * i.vertColor.r + _MainTex_var*(1-i.vertColor.r);
					////half3 diffuse = _MainTex_var.rgb;

					//diffuse = lerp(diffuse,  diffuse* baseColor, 1);

					//// light
					//// 雾效处理
					half4 finalRGBA = half4(diffuse.rgb, _Alpha * _MainTex_var.a);
					return finalRGBA;
				}

				ENDHLSL

			}


		}
}
