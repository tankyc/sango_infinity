// Shader created with Shader Forge v1.40 
// Shader Forge (c) Freya Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.40;sub:START;pass:START;ps:flbk:,iptp:0,cusa:False,bamd:0,cgin:,cpap:True,lico:1,lgpr:1,limd:0,spmd:1,trmd:0,grmd:0,uamb:True,mssp:True,bkdf:False,hqlp:False,rprd:False,enco:False,rmgx:True,imps:True,rpth:0,vtps:0,hqsc:True,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:2,bsrc:3,bdst:7,dpts:2,wrdp:False,dith:0,atcv:False,rfrpo:True,rfrpn:Refraction,coma:15,ufog:True,aust:True,igpj:True,qofs:0,qpre:3,rntp:2,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:0.5,fgcg:0.5,fgcb:0.5,fgca:1,fgde:0.01,fgrn:0,fgrf:300,stcl:False,atwp:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:False,fnfb:False,fsmp:False;n:type:ShaderForge.SFN_Final,id:9361,x:33754,y:32715,varname:node_9361,prsc:2|custl-7134-OUT,alpha-2602-OUT;n:type:ShaderForge.SFN_Tex2d,id:3278,x:32120,y:32806,ptovrint:False,ptlb:Tex,ptin:_Tex,varname:_diffuse,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False|UVIN-9859-OUT;n:type:ShaderForge.SFN_Color,id:3172,x:32364,y:33157,ptovrint:False,ptlb:Tex_color,ptin:_Tex_color,varname:_diffusecolor,prsc:2,glob:False,taghide:False,taghdr:True,tagprd:False,tagnsco:False,tagnrm:False,c1:1,c2:1,c3:1,c4:1;n:type:ShaderForge.SFN_ValueProperty,id:7027,x:30858,y:32818,ptovrint:False,ptlb:diffuse U speed,ptin:_diffuseUspeed,varname:_diffuseUspeed,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0;n:type:ShaderForge.SFN_ValueProperty,id:1307,x:30858,y:32906,ptovrint:False,ptlb:diffuse V speed,ptin:_diffuseVspeed,varname:_diffuseVspeed,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0;n:type:ShaderForge.SFN_Append,id:7827,x:31057,y:32837,varname:node_7827,prsc:2|A-7027-OUT,B-1307-OUT;n:type:ShaderForge.SFN_Time,id:8425,x:31070,y:32682,varname:node_8425,prsc:2;n:type:ShaderForge.SFN_Multiply,id:6262,x:31329,y:32784,varname:node_6262,prsc:2|A-8425-T,B-7827-OUT;n:type:ShaderForge.SFN_Add,id:3952,x:31615,y:32905,varname:node_3952,prsc:2|A-6262-OUT,B-8070-OUT,C-3980-UVOUT;n:type:ShaderForge.SFN_TexCoord,id:3980,x:31328,y:33060,varname:node_3980,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_VertexColor,id:562,x:32340,y:33394,varname:node_562,prsc:2;n:type:ShaderForge.SFN_Multiply,id:2602,x:33022,y:33265,varname:node_2602,prsc:2|A-8477-OUT,B-3172-A,C-562-A,D-774-R,E-5193-OUT;n:type:ShaderForge.SFN_TexCoord,id:5495,x:31181,y:33520,varname:node_5495,prsc:2,uv:1,uaff:True;n:type:ShaderForge.SFN_Append,id:9748,x:31464,y:33442,varname:node_9748,prsc:2|A-5495-U,B-5495-V;n:type:ShaderForge.SFN_Add,id:809,x:31678,y:33148,varname:node_809,prsc:2|A-3980-UVOUT,B-9748-OUT,C-8070-OUT;n:type:ShaderForge.SFN_SwitchProperty,id:9859,x:31917,y:32823,ptovrint:False,ptlb:yici_UV_ON/OFF,ptin:_yici_UV_ONOFF,varname:_UVon,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-3952-OUT,B-809-OUT;n:type:ShaderForge.SFN_Tex2d,id:7675,x:31181,y:33707,ptovrint:False,ptlb:rongjietu,ptin:_rongjietu,varname:_rongjietu,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Step,id:2935,x:32163,y:33931,varname:node_2935,prsc:2|A-5495-Z,B-7675-R;n:type:ShaderForge.SFN_SwitchProperty,id:8477,x:32380,y:32946,ptovrint:False,ptlb:Tex_R/A,ptin:_Tex_RA,varname:_diffuseRA,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-3278-R,B-3278-A;n:type:ShaderForge.SFN_SwitchProperty,id:5193,x:32682,y:33909,ptovrint:False,ptlb:rongjie ON,ptin:_rongjieON,varname:_rongjieON,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-4941-OUT,B-2935-OUT;n:type:ShaderForge.SFN_Vector1,id:4941,x:32326,y:33883,varname:node_4941,prsc:2,v1:1;n:type:ShaderForge.SFN_Tex2d,id:774,x:32340,y:33628,ptovrint:False,ptlb:mask,ptin:_mask,varname:_zhezhao,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:8006,x:30234,y:32872,ptovrint:False,ptlb:noise_01,ptin:_noise_01,varname:_noise_01,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False|UVIN-9904-OUT;n:type:ShaderForge.SFN_Append,id:5587,x:29536,y:32953,varname:node_5587,prsc:2|A-4654-X,B-4654-Y;n:type:ShaderForge.SFN_Time,id:8658,x:29536,y:32775,varname:node_8658,prsc:2;n:type:ShaderForge.SFN_Multiply,id:8453,x:29783,y:32836,varname:node_8453,prsc:2|A-8658-T,B-5587-OUT;n:type:ShaderForge.SFN_Add,id:9904,x:30009,y:32872,varname:node_9904,prsc:2|A-8453-OUT,B-7038-UVOUT;n:type:ShaderForge.SFN_Vector4Property,id:4654,x:29346,y:32953,ptovrint:False,ptlb:noise_speed,ptin:_noise_speed,varname:_noise_speed,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0,v2:0,v3:0,v4:0;n:type:ShaderForge.SFN_Multiply,id:1409,x:30581,y:33133,varname:node_1409,prsc:2|A-8006-R,B-1078-OUT;n:type:ShaderForge.SFN_Slider,id:1078,x:29812,y:33245,ptovrint:False,ptlb:raodong qiangdu,ptin:_raodongqiangdu,varname:_raodongqiangdu,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0,max:1;n:type:ShaderForge.SFN_Multiply,id:7134,x:33046,y:32945,varname:node_7134,prsc:2|A-3278-RGB,B-3172-RGB,C-562-RGB;n:type:ShaderForge.SFN_TexCoord,id:7038,x:29797,y:33041,varname:node_7038,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_SwitchProperty,id:8070,x:30899,y:33120,ptovrint:False,ptlb:raodong_ON/OFF,ptin:_raodong_ONOFF,varname:node_8070,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-6744-OUT,B-1409-OUT;n:type:ShaderForge.SFN_Vector1,id:6744,x:30647,y:33038,varname:node_6744,prsc:2,v1:0;proporder:3172-3278-7027-1307-8477-7675-9859-5193-774-8006-4654-1078-8070;pass:END;sub:END;*/

Shader "URP/Effect/UV_RJ_alpha" {
    Properties {
        [HDR]_Tex_color ("Tex_color", Color) = (1,1,1,1)
        _Tex ("Tex", 2D) = "white" {}
        _diffuseUspeed ("diffuse U speed", Float ) = 0
        _diffuseVspeed ("diffuse V speed", Float ) = 0
        [MaterialToggle] _Tex_RA ("Tex_R/A", Float ) = 0
        _rongjietu ("rongjietu", 2D) = "white" {}
        [MaterialToggle] _yici_UV_ONOFF ("yici_UV_ON/OFF", Float ) = 0
        [MaterialToggle] _rongjieON ("rongjie ON", Float ) = 1
        _mask ("mask", 2D) = "white" {}
        [Toggle]_useFog("Enable Fog",Float) = 1

        _noise_01 ("noise_01", 2D) = "white" {}
        _noise_speed ("noise_speed", Vector) = (0,0,0,0)
        _raodongqiangdu ("raodong qiangdu", Range(0, 1)) = 0
        [MaterialToggle] _raodong_ONOFF ("raodong_ON/OFF", Float ) = 0
        [HideInInspector]_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
        _Alpha("Alpha", Float) = 1
    }
    SubShader {
        Tags {
			"RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"="True"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        ZTest [_ZTest]
        

        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="UniversalForward"
            }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            //#include "UnityCG.cginc"
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)				
				sampler2D _Tex;
                float4 _Tex_ST;
                sampler2D _rongjietu;
                float4 _rongjietu_ST;
                sampler2D _mask;
                float4 _mask_ST;
                sampler2D _noise_01;
                float4 _noise_01_ST;
                float4 _Tex_color;
                float _diffuseUspeed;
                float _diffuseVspeed;
                half _yici_UV_ONOFF;
                half _useFog;
                half _Tex_RA;
                half _rongjieON;
                float4 _noise_speed;
                float _raodongqiangdu;
                half _raodong_ONOFF;
                float _Alpha;
			CBUFFER_END

            //#pragma multi_compile_fwdbase
            #pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #pragma multi_compile_fog
            #pragma target 3.0

            //uniform sampler2D _Tex; uniform float4 _Tex_ST;
            //uniform sampler2D _rongjietu; uniform float4 _rongjietu_ST;
            //uniform sampler2D _mask; uniform float4 _mask_ST;
            //uniform sampler2D _noise_01; uniform float4 _noise_01_ST;
           /* UNITY_INSTANCING_BUFFER_START( Props )
                UNITY_DEFINE_INSTANCED_PROP( float4, _Tex_color)
                UNITY_DEFINE_INSTANCED_PROP( float, _diffuseUspeed)
                UNITY_DEFINE_INSTANCED_PROP( float, _diffuseVspeed)
                UNITY_DEFINE_INSTANCED_PROP( half, _yici_UV_ONOFF)
                UNITY_DEFINE_INSTANCED_PROP( half, _Tex_RA)
                UNITY_DEFINE_INSTANCED_PROP( half, _rongjieON)
                UNITY_DEFINE_INSTANCED_PROP( float4, _noise_speed)
                UNITY_DEFINE_INSTANCED_PROP( float, _raodongqiangdu)
                UNITY_DEFINE_INSTANCED_PROP( half, _raodong_ONOFF)
            UNITY_INSTANCING_BUFFER_END( Props )*/
            struct VertexInput {
                 float4 vertex : POSITION;
                float2 texcoord0 : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 vertexColor : COLOR;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 vertexColor : COLOR;
                //UNITY_FOG_COORDS(2)                
                float fogCoord : TEXCOORD2;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.vertexColor = v.vertexColor;
                o.pos = TransformObjectToHClip( v.vertex.xyz );
                //UNITY_TRANSFER_FOG(o,o.pos);
                o.fogCoord = ComputeFogFactor(o.pos.z);
                return o;
            }
            float4 frag(VertexOutput i, float facing : VFACE) : COLOR {
                float isFrontFace = ( facing >= 0 ? 1 : 0 );
                float faceSign = ( facing >= 0 ? 1 : -1 );
////// Lighting:
                float4 node_8425 = _Time;
                float _diffuseUspeed_var = _diffuseUspeed;
                float _diffuseVspeed_var = _diffuseVspeed;
                float4 node_8658 = _Time;
                float4 _noise_speed_var = _noise_speed;
                float2 node_9904 = ((node_8658.g*float2(_noise_speed_var.r,_noise_speed_var.g))+i.uv0);
                float4 _noise_01_var = tex2D(_noise_01,TRANSFORM_TEX(node_9904, _noise_01));
                float _raodongqiangdu_var = _raodongqiangdu;
                float _raodong_ONOFF_var = lerp( 0.0, (_noise_01_var.r*_raodongqiangdu_var), _raodong_ONOFF  );
                float2 _yici_UV_ONOFF_var = lerp( ((node_8425.g*float2(_diffuseUspeed_var,_diffuseVspeed_var))+_raodong_ONOFF_var+i.uv0), (i.uv0+float2(i.uv1.r,i.uv1.g)+_raodong_ONOFF_var), _yici_UV_ONOFF );
                float4 _Tex_var = tex2D(_Tex,TRANSFORM_TEX(_yici_UV_ONOFF_var, _Tex));
                float4 _Tex_color_var = _Tex_color;
                float3 finalColor = (_Tex_var.rgb*_Tex_color_var.rgb*i.vertexColor.rgb);
                float _Tex_RA_var = lerp( _Tex_var.r, _Tex_var.a,  _Tex_RA );
                float4 _mask_var = tex2D(_mask,TRANSFORM_TEX(i.uv0, _mask));
                float4 _rongjietu_var = tex2D(_rongjietu,TRANSFORM_TEX(i.uv0, _rongjietu));
                float _rongjieON_var = lerp( 1.0, step(i.uv1.b,_rongjietu_var.r),  _rongjieON );
                half4 finalRGBA = half4(finalColor,(_Tex_RA_var*_Tex_color_var.a*i.vertexColor.a*_mask_var.r*_rongjieON_var));
                //UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                if (_useFog > 0)
                {
                    finalRGBA.rgb = MixFog(finalRGBA.rgb, i.fogCoord);
                }
                finalRGBA.a *= _Alpha;

                return finalRGBA;
            }
            ENDHLSL
        }
        Pass {
            Name "ShadowCaster"
            Tags {
                "LightMode"="ShadowCaster"
            }
            Offset 1, 1
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            //#include "UnityCG.cginc"
            //#include "Lighting.cginc"
            
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_fog
            #pragma target 3.0
            struct VertexInput {
                float4 vertex : POSITION;
            };
            struct VertexOutput {
                float4 vert : SV_POSITION;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.vert = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }
            float4 frag(VertexOutput i/*, float facing : VFACE*/) : COLOR {
                //float isFrontFace = ( facing >= 0 ? 1 : 0 );
                //float faceSign = ( facing >= 0 ? 1 : -1 );
                //SHADOW_CASTER_FRAGMENT(i)
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
    CustomEditor "ShaderForgeMaterialInspector"
}
