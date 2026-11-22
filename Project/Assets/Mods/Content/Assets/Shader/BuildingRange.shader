// 建筑范围专用 Shader —— 由 Sango/Particles/Alpha Blended 派生，
// 唯一差别是在 Category 上加了 Offset -1, -1（深度偏移），用于抵抗地形 LOD 的穿插。
//
// 为什么不直接复用 Sango/Particles/Alpha Blended：
//   那个 shader 被粒子系统共用，加 Offset 会连带影响所有粒子（烟雾、火焰等的排序与遮挡），
//   所以这里单独复制一份，只给 Resources/buildingRange*.mat 用。
Shader "Sango/BuildingRange" {
Properties {
	_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	_MainTex ("Particle Texture", 2D) = "white" {}
	_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
	[Toggle]_useFog("Enable Fog",Float) = 1

    _Alpha ("Alpha", Range(1, 0)) = 1
	[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4

	// 【呼吸闪烁】
	//   _BreathSpeed —— 每秒脉动次数（0.5 = 2 秒一个来回）
	//   _BreathMin   —— 脉动到最暗时保留的透明度系数（1 = 不呼吸）
	// 三层范围（占地 / 攻击 / 效果）共用同一套参数，相位一致，不会互相抢眼。
	_BreathSpeed ("Breath Speed (次/秒)", Range(0, 5)) = 0.5
	_BreathMin ("Breath Min Alpha", Range(0, 1)) = 0.45
}

Category {
	Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
	Blend SrcAlpha OneMinusSrcAlpha
	//ColorMask RGB
	Cull Off Lighting Off ZWrite Off
	ZTest [_ZTest]

	// 【地形 LOD 抗穿插】
	// 地形按 LOD 抽稀后，一个大三角形跨越很多格子，其内部是线性插值高度；
	// 而本面片是逐格 5x5 顶点采样地形顶点高度铺出来的，两者在格子中间必然对不上，
	// 于是面片在凹陷处沉到地形网格下面、被深度测试挡掉（表现为范围"被啃掉一块"）。
	//
	// Offset 在光栅化阶段把深度往相机方向压：
	//   · 第一个参数 slope-scale factor，处理斜面；
	//   · 第二个参数 constant units，处理正对相机的平面。
	// 偏移量只有深度缓冲的极小一段，远小于"地形"与"建筑"之间的真实深度差，
	// 因此面片仍然会被不透明建筑正常遮挡，不会糊到城池模型正面。
	Offset -1, -1

	SubShader {
		Pass {
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_particles
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			
			CBUFFER_START(UnityPerMaterial)				
				sampler2D _MainTex;			
				float4 _MainTex_ST;
				fixed4 _TintColor;
				half _useFog;
				float _Alpha;
				float _BreathSpeed;
				half _BreathMin;
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
				if (_useFog > 0)
				{
					UNITY_APPLY_FOG(i.fogCoord, col);
				}
				
				col.a = saturate(col.a) * _Alpha;

				// 【呼吸闪烁】alpha 在 _BreathMin ~ 1 之间做正弦循环。
				// 用 _Time.y（游戏时间，受 Time.timeScale 影响）而不是 _SinTime，
				//   便于暂停时范围也跟着静止；6.28318530718 = 2π，
				//   _BreathSpeed = 0.5 时周期 2 秒，接近人眼"呼吸"的节奏。
				// 只脉动 alpha、不动 rgb：颜色由 _TintColor 决定，乘亮度容易在亮场景过曝。
				float breathWave = sin(_Time.y * _BreathSpeed * 6.28318530718) * 0.5 + 0.5;
				col.a = saturate(col.a * lerp(_BreathMin, 1.0, breathWave));

				return col;
			}
			ENDCG 
		}
	}	
}
}
