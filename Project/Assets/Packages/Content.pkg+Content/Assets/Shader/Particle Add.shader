// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Sango/Particles/Additive" {
Properties {
	_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	_MainTex ("Particle Texture", 2D) = "white" {}
	_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
    _GlowStrength ("Glow Strength", Float) = 1.0
    _Alpha ("Alpha", Range(1, 0)) = 1

    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4


	
    [Enum(UnityEngine.Rendering.BlendMode)] _SRC_Color("SRC Color", Float) = 5
    [Enum(UnityEngine.Rendering.BlendMode)] _DST_Color("DST Color", Float) = 1
    [Enum(UnityEngine.Rendering.BlendMode)] _SRC_Alpha("SRC Aplha", Float) = 5
    [Enum(UnityEngine.Rendering.BlendMode)] _DST_Alpha("DST Aplha", Float) = 1
}

Category {
	Tags { "Queue"="Transparent" "RenderType"="Glow11Transparent" }
	//Blend SrcAlpha One
	//Blend SrcAlpha One,Zero One
	Blend [_SRC_Color] [_DST_Color], [_SRC_Alpha] [_DST_Alpha]
	//ColorMask RGB
	Cull Off Lighting Off ZWrite Off
	ZTest [_ZTest]
	
	SubShader {
		Pass {
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_particles
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			CBUFFER_START(UnityPerMaterial)				
				float4 _MainTex_ST;
				sampler2D _MainTex;
				fixed4 _TintColor;
				float _Alpha;
			CBUFFER_END
			
			struct appdata_t {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				#ifdef SOFTPARTICLES_ON
				float4 projPos : TEXCOORD2;
				#endif
			};
			

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				#ifdef SOFTPARTICLES_ON
				o.projPos = ComputeScreenPos (o.vertex);
				COMPUTE_EYEDEPTH(o.projPos.z);
				#endif
				o.color = v.color;
				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			sampler2D_float _CameraDepthTexture;
			float _InvFade;
			
			fixed4 frag (v2f i) : SV_Target
			{
				#ifdef SOFTPARTICLES_ON
				float sceneZ = LinearEyeDepth (SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
				float partZ = i.projPos.z;
				float fade = saturate (_InvFade * (sceneZ-partZ));
				i.color.a *= fade;
				#endif
				
				fixed4 col = 2.0f * i.color * _TintColor * tex2D(_MainTex, i.texcoord);
				UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0,0,0,0)); // fog towards black due to our blend mode
				col.a  *= _Alpha;
				return col;
			}
			ENDCG 
		}
	}
	
	
}
}
