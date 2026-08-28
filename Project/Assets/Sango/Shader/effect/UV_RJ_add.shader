// Shader created with Shader Forge v1.40 
// Shader Forge (c) Freya Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.40;sub:START;pass:START;ps:flbk:,iptp:0,cusa:False,bamd:0,cgin:,cpap:True,lico:1,lgpr:1,limd:0,spmd:1,trmd:0,grmd:0,uamb:True,mssp:True,bkdf:False,hqlp:False,rprd:False,enco:False,rmgx:True,imps:True,rpth:0,vtps:0,hqsc:True,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:2,bsrc:0,bdst:0,dpts:2,wrdp:False,dith:0,atcv:False,rfrpo:True,rfrpn:Refraction,coma:15,ufog:True,aust:True,igpj:True,qofs:0,qpre:3,rntp:2,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:0.5,fgcg:0.5,fgcb:0.5,fgca:1,fgde:0.01,fgrn:0,fgrf:300,stcl:False,atwp:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:False,fnfb:False,fsmp:False;n:type:ShaderForge.SFN_Final,id:9361,x:33754,y:32715,varname:node_9361,prsc:2|custl-5551-OUT;n:type:ShaderForge.SFN_Tex2d,id:3278,x:32135,y:32792,ptovrint:False,ptlb:Tex,ptin:_Tex,varname:_diffuse,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False|UVIN-9859-OUT;n:type:ShaderForge.SFN_Color,id:3172,x:32130,y:33132,ptovrint:False,ptlb:Tex_color,ptin:_Tex_color,varname:_diffusecolor,prsc:2,glob:False,taghide:False,taghdr:True,tagprd:False,tagnsco:False,tagnrm:False,c1:1,c2:1,c3:1,c4:1;n:type:ShaderForge.SFN_ValueProperty,id:7027,x:30858,y:32818,ptovrint:False,ptlb:Tex _U_speed,ptin:_Tex_U_speed,varname:_diffuseUspeed,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0;n:type:ShaderForge.SFN_ValueProperty,id:1307,x:30858,y:32906,ptovrint:False,ptlb:Tex_V_speed,ptin:_Tex_V_speed,varname:_diffuseVspeed,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0;n:type:ShaderForge.SFN_Append,id:7827,x:31057,y:32837,varname:node_7827,prsc:2|A-7027-OUT,B-1307-OUT;n:type:ShaderForge.SFN_Time,id:8425,x:31070,y:32682,varname:node_8425,prsc:2;n:type:ShaderForge.SFN_Multiply,id:6262,x:31329,y:32784,varname:node_6262,prsc:2|A-8425-T,B-7827-OUT;n:type:ShaderForge.SFN_TexCoord,id:3980,x:31328,y:33060,varname:node_3980,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_VertexColor,id:562,x:32143,y:33405,varname:node_562,prsc:2;n:type:ShaderForge.SFN_Multiply,id:2602,x:33030,y:33260,varname:node_2602,prsc:2|A-8477-OUT,B-3172-A,C-562-A,D-774-R,E-5193-OUT;n:type:ShaderForge.SFN_TexCoord,id:5495,x:31196,y:33408,varname:node_5495,prsc:2,uv:1,uaff:True;n:type:ShaderForge.SFN_Append,id:9748,x:31502,y:33372,varname:node_9748,prsc:2|A-5495-U,B-5495-V;n:type:ShaderForge.SFN_Add,id:809,x:31723,y:33201,varname:node_809,prsc:2|A-3980-UVOUT,B-9748-OUT;n:type:ShaderForge.SFN_SwitchProperty,id:9859,x:31894,y:32780,ptovrint:False,ptlb:yici_UV_ON/OFF,ptin:_yici_UV_ONOFF,varname:_UVon,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-8625-OUT,B-809-OUT;n:type:ShaderForge.SFN_Tex2d,id:7675,x:31209,y:33680,ptovrint:False,ptlb:rongjietu,ptin:_rongjietu,varname:_rongjietu,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Step,id:2935,x:32163,y:33931,varname:node_2935,prsc:2|A-5495-Z,B-7675-R;n:type:ShaderForge.SFN_SwitchProperty,id:8477,x:32392,y:32926,ptovrint:False,ptlb:Tex_R/A,ptin:_Tex_RA,varname:_diffuseRA,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-3278-R,B-3278-A;n:type:ShaderForge.SFN_SwitchProperty,id:5193,x:32682,y:33909,ptovrint:False,ptlb:rongjie ON,ptin:_rongjieON,varname:_rongjieON,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-4941-OUT,B-2935-OUT;n:type:ShaderForge.SFN_Vector1,id:4941,x:32326,y:33883,varname:node_4941,prsc:2,v1:1;n:type:ShaderForge.SFN_Tex2d,id:774,x:32158,y:33628,ptovrint:False,ptlb:mask,ptin:_mask,varname:_zhezhao,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Multiply,id:5551,x:33348,y:33034,varname:node_5551,prsc:2|A-149-OUT,B-2602-OUT;n:type:ShaderForge.SFN_Multiply,id:149,x:32839,y:32841,varname:node_149,prsc:2|A-3278-RGB,B-3172-RGB,C-562-RGB;n:type:ShaderForge.SFN_Add,id:8625,x:31636,y:32792,varname:node_8625,prsc:2|A-6262-OUT,B-3980-UVOUT;proporder:3172-3278-7027-1307-8477-7675-9859-5193-774;pass:END;sub:END;*/

Shader "URP/Effect/UV_RJ_add" {
    Properties {
        [HDR]_Tex_color ("Tex_color", Color) = (1,1,1,1)
        _Tex ("Tex", 2D) = "white" {}
        _Tex_U_speed ("Tex _U_speed", Float ) = 0
        _Tex_V_speed ("Tex_V_speed", Float ) = 0
        [MaterialToggle] _Tex_RA ("Tex_R/A", Float ) = 0
        _rongjietu ("rongjietu", 2D) = "white" {}
        [MaterialToggle] _yici_UV_ONOFF ("yici_UV_ON/OFF", Float ) = 0
        [MaterialToggle] _rongjieON ("rongjie ON", Float ) = 1
        _mask ("mask", 2D) = "white" {}
        [Toggle]_useFog("Enable Fog",Float) = 1
        _Alpha("Alpha", Float) = 1

        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4

        [Enum(UnityEngine.Rendering.BlendMode)] _SRC_Color("SRC Color", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DST_Color("DST Color", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SRC_Alpha("SRC Aplha", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DST_Alpha("DST Aplha", Float) = 1
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

            //Blend One One
            //Blend SrcAlpha One,Zero One
            Blend [_SRC_Color] [_DST_Color], [_SRC_Alpha] [_DST_Alpha]

            Cull Off
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            //#include "UnityCG.cginc"
            //#pragma multi_compile_fwdbase
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			CBUFFER_START(UnityPerMaterial)				
				sampler2D _Tex;
                float4 _Tex_ST;
                sampler2D _rongjietu;
                float4 _rongjietu_ST;
                sampler2D _mask;
                float4 _mask_ST;
                float4 _Tex_color;
                float _Tex_U_speed;
                float _Tex_V_speed;
                half _yici_UV_ONOFF;
                half _Tex_RA;
                half _useFog;
                half _rongjieON;
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
            //UNITY_INSTANCING_BUFFER_START( Props )
            //    UNITY_DEFINE_INSTANCED_PROP( float4, _Tex_color)
            //    UNITY_DEFINE_INSTANCED_PROP( float, _Tex_U_speed)
            //    UNITY_DEFINE_INSTANCED_PROP( float, _Tex_V_speed)
            //    UNITY_DEFINE_INSTANCED_PROP( half, _yici_UV_ONOFF)
            //    UNITY_DEFINE_INSTANCED_PROP( half, _Tex_RA)
            //    UNITY_DEFINE_INSTANCED_PROP( half, _rongjieON)
            //UNITY_INSTANCING_BUFFER_END( Props )
            struct VertexInput {
                //UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float2 texcoord0 : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 vertexColor : COLOR;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
               // UNITY_VERTEX_INPUT_INSTANCE_ID
                float2 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 vertexColor : COLOR;
                //UNITY_FOG_COORDS(2)
                float fogCoord : TEXCOORD2;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                //UNITY_SETUP_INSTANCE_ID( v );
                //UNITY_TRANSFER_INSTANCE_ID( v, o );
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.vertexColor = v.vertexColor;
                //o.pos = UnityObjectToClipPos( v.vertex );
                o.pos = TransformObjectToHClip(v.vertex.xyz);

                //UNITY_TRANSFER_FOG(o,o.pos);
                o.fogCoord = ComputeFogFactor(o.pos.z);
                return o;
            }
            float4 frag(VertexOutput i/*, float facing : VFACE*/) : COLOR {
                //UNITY_SETUP_INSTANCE_ID( i );
                //float isFrontFace = ( facing >= 0 ? 1 : 0 );
                //float faceSign = ( facing >= 0 ? 1 : -1 );
////// Lighting:
                float4 node_8425 = _Time;
                float _Tex_U_speed_var = _Tex_U_speed;
                float _Tex_V_speed_var =_Tex_V_speed ;
                float2 node_6262 = (node_8425.g*float2(_Tex_U_speed_var,_Tex_V_speed_var));
                float2 _yici_UV_ONOFF_var = lerp( (node_6262+i.uv0), (i.uv0+float2(i.uv1.r,i.uv1.g)),  _yici_UV_ONOFF  );
                float4 _Tex_var = tex2D(_Tex,TRANSFORM_TEX(_yici_UV_ONOFF_var, _Tex));
                float4 _Tex_color_var = _Tex_color;
                float _Tex_RA_var = lerp( _Tex_var.r, _Tex_var.a, _Tex_RA  );
                float4 _mask_var = tex2D(_mask,TRANSFORM_TEX(i.uv0, _mask));
                float4 _rongjietu_var = tex2D(_rongjietu,TRANSFORM_TEX(i.uv0, _rongjietu));
                float _rongjieON_var = lerp( 1.0, step(i.uv1.b,_rongjietu_var.r),  _rongjieON  );
                float3 finalColor = ((_Tex_var.rgb*_Tex_color_var.rgb*i.vertexColor.rgb)*(_Tex_RA_var*_Tex_color_var.a*i.vertexColor.a*_mask_var.r*_rongjieON_var));
                half4 finalRGBA = half4(finalColor,1);
                //UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                if (_useFog > 0)
                {
                    finalRGBA.rgb = lerp(finalRGBA.rgb, MixFog(finalRGBA.rgb, i.fogCoord), finalRGBA.rgb.r);
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
                //V2F_SHADOW_CASTER;
                float4 vert : SV_POSITION;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                //o.pos = UnityObjectToClipPos( v.vertex );
                o.vert = TransformObjectToHClip(v.vertex.xyz);
                //TRANSFER_SHADOW_CASTER(o)
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
    //CustomEditor "ShaderForgeMaterialInspector"
}
