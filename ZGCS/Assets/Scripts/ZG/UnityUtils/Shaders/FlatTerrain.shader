// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "ZG/FlatTerrain"
{
	Properties
	{
		[Toggle(LIGHT_FLAT)] _LightFlat("Flat Light", Float) = 0
		[Toggle(LIGHT_SPEC)] _LightSpec("Specular Light", Float) = 0
		[Toggle(EDGE_HARD)] _EdgeHard("Hard Edge", Float) = 0

		_Color("Diffuse Material Color", Color) = (1,1,1,1)
		_SpecColor("Specular Material Color", Color) = (1,1,1,1)
		_Shininess("Shininess", Float) = 10

		_Control("SplatMap (RGBA)", 2D) = "red" {}
		_Splat0("Layer 0 (R)", 2D) = "white" {}
		_Splat1("Layer 1 (G)", 2D) = "white" {}
		_Splat2("Layer 2 (B)", 2D) = "white" {}
		_Splat3("Layer 3 (A)", 2D) = "white" {}
	}
	
	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
		}
		Pass
		{
			Name "FORWARD"
			Tags
			{
				"LightMode" = "ForwardBase"
			}

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//#define UNITY_PASS_FORWARDBASE
			#include "UnityCG.cginc"
			#include "AutoLight.cginc"
			//#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			#pragma shader_feature __ LIGHT_FLAT
			#pragma shader_feature __ LIGHT_SPEC
			#pragma shader_feature __ EDGE_HARD
			//#pragma target 2.0

			uniform float4 _LightColor0;
			
			struct VertexOutput 
			{
				float4 pos : SV_POSITION;
				float3 posWorld : TEXCOORD0;

#ifndef LIGHT_FLAT
				float3 normal : TEXCOORD1;
#endif
				
				half2 uv0 : TEXCOORD2;
				half2 uv1 : TEXCOORD3;
				half2 uv2 : TEXCOORD4;
				half2 uv3 : TEXCOORD5;
				half2 uv4 : TEXCOORD6;
				
				LIGHTING_COORDS(7, 8)
				UNITY_FOG_COORDS(9)
			};

			uniform half4 _Color;
			uniform half4 _SpecColor;
			uniform half _Shininess;

			uniform sampler2D _Control;
			uniform half4 _Control_ST;

			uniform sampler2D _Splat0, _Splat1, _Splat2, _Splat3;
			uniform half4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;

			VertexOutput vert(appdata_base v)
			{
				VertexOutput o = (VertexOutput)0;

				o.posWorld = mul(unity_ObjectToWorld, v.vertex);
				o.pos = UnityObjectToClipPos(v.vertex);
				UNITY_TRANSFER_FOG(o, o.pos);
				TRANSFER_VERTEX_TO_FRAGMENT(o)

#ifndef LIGHT_FLAT
				o.normal = UnityObjectToWorldNormal(v.normal);
#endif

				o.uv0 = TRANSFORM_TEX(v.texcoord.xy, _Control);
				o.uv1 = TRANSFORM_TEX(v.texcoord.xy, _Splat0);
				o.uv2 = TRANSFORM_TEX(v.texcoord.xy, _Splat1);
				o.uv3 = TRANSFORM_TEX(v.texcoord.xy, _Splat2);
				o.uv4 = TRANSFORM_TEX(v.texcoord.xy, _Splat3);

				return o;
			}

			fixed4 frag(VertexOutput i) : SV_Target
			{
				float3 normalDirection;
#ifdef LIGHT_FLAT
				float3 normalX = normalize(float3(ddx(i.posWorld.x), ddx(i.posWorld.y), ddx(i.posWorld.z)));
				float3 normalY = normalize(float3(ddy(i.posWorld.x), ddy(i.posWorld.y), ddy(i.posWorld.z)));

#if UNITY_UV_STARTS_AT_TOP
				if (_ProjectionParams.x < 0)
					normalDirection = cross(normalY, normalX);
				else
					normalDirection = cross(normalX, normalY);
#else
				if (_ProjectionParams.x < 0)
					normalDirection = cross(normalX, normalY);
				else
					normalDirection = cross(normalY, normalX);
#endif
#else
				normalDirection = i.normal;
#endif

				float3 lightDirection = normalize(UnityWorldSpaceLightDir(i.posWorld));
				float nDotL = dot(normalDirection, lightDirection);

				half3 light = max(0.0, nDotL) * _Color.rgb;

#ifdef LIGHT_SPEC
				float3 viewDirection = normalize(UnityWorldSpaceViewDir(i.posWorld));
				light += max(0.0, sign(nDotL)) * 
					pow(max(0.0, dot(
					reflect(-lightDirection, normalDirection),
					viewDirection)), _Shininess) * 
					_SpecColor.rgb;
#endif
				float attenuation = LIGHT_ATTENUATION(i);
				light *= attenuation * _LightColor0.rgb;
				light += UNITY_LIGHTMODEL_AMBIENT.rgb;

				half4 splatControl = tex2D(_Control, i.uv0);

#ifdef EDGE_HARD
				splatControl = normalize(splatControl);
				splatControl.r = round(splatControl.r);
				splatControl.g = saturate(round(splatControl.g - splatControl.r));
				splatControl.b = saturate(round(splatControl.b - splatControl.g));
				splatControl.a = saturate(round(splatControl.a - splatControl.b));
#endif

				half4 splatColor = splatControl.r * tex2D(_Splat0, i.uv1);
				splatColor += splatControl.g * tex2D(_Splat1, i.uv2);
				splatColor += splatControl.b * tex2D(_Splat2, i.uv3);
				splatColor += splatControl.a * tex2D(_Splat3, i.uv4);

				fixed4 finalColor = fixed4(light.rgb, _Color.a) * splatColor;
				UNITY_APPLY_FOG(i.fogCoord, finalColor);

				return finalColor;

			}
			ENDCG
		}

		// ------------------------------------------------------------------
		//  Additive forward pass (one light per pass)
		Pass
		{
			Name "FORWARD_DELTA"
			Tags{ "LightMode" = "ForwardAdd" }
			Blend One One
			Fog{ Color(0,0,0,0) } // in additive pass fog should be black
			ZWrite Off
			ZTest LEqual
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//#define UNITY_PASS_FORWARDADD
			#include "UnityCG.cginc"
			#include "AutoLight.cginc"
			//#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

			#pragma multi_compile_fwdadd
			#pragma shader_feature __ LIGHT_FLAT
			#pragma shader_feature __ LIGHT_SPEC
			//#pragma target 2.0

			uniform float4 _LightColor0;

			struct VertexOutput
			{
				float4 pos : SV_POSITION;
				float3 posWorld : TEXCOORD0;

#ifndef LIGHT_FLAT
				float3 normal : TEXCOORD1;
#endif
				LIGHTING_COORDS(2, 3)
			};

			uniform half4 _Color;
			uniform half4 _SpecColor;
			uniform half _Shininess;

			VertexOutput vert(appdata_base v)
			{
				VertexOutput o = (VertexOutput)0;

				o.posWorld = mul(unity_ObjectToWorld, v.vertex);
				o.pos = UnityObjectToClipPos(v.vertex);
				TRANSFER_VERTEX_TO_FRAGMENT(o)

#ifndef LIGHT_FLAT
				o.normal = UnityObjectToWorldNormal(v.normal);
#endif

				return o;
			}

			fixed4 frag(VertexOutput i) : SV_Target
			{
				float3 normalDirection;
#ifdef LIGHT_FLAT
				float3 normalX = normalize(float3(ddx(i.posWorld.x), ddx(i.posWorld.y), ddx(i.posWorld.z)));
				float3 normalY = normalize(float3(ddy(i.posWorld.x), ddy(i.posWorld.y), ddy(i.posWorld.z)));

#if UNITY_UV_STARTS_AT_TOP
				if (_ProjectionParams.x < 0)
					normalDirection = cross(normalY, normalX);
				else
					normalDirection = cross(normalX, normalY);
#else
				if (_ProjectionParams.x < 0)
					normalDirection = cross(normalX, normalY);
				else
					normalDirection = cross(normalY, normalX);
#endif
#else
				normalDirection = i.normal;
#endif

				float3 lightDirection = normalize(UnityWorldSpaceLightDir(i.posWorld));
				float nDotL = dot(normalDirection, lightDirection);

				half3 light = max(0.0, nDotL) * _Color.rgb;

#ifdef LIGHT_SPEC
				float3 viewDirection = normalize(UnityWorldSpaceViewDir(i.posWorld));
				light += max(0.0, sign(nDotL)) *
					pow(max(0.0, dot(
						reflect(-lightDirection, normalDirection),
						viewDirection)), _Shininess) *
					_SpecColor.rgb;
#endif
				float attenuation = LIGHT_ATTENUATION(i);
				light *= attenuation * _LightColor0.rgb;

				return fixed4(light, 1.0);
			}
			ENDCG
		}

		UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
	}
}
