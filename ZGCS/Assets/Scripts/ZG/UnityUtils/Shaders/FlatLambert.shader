Shader "ZG/FlatLambert"
{
	Properties
	{ 
		[Toggle(LIGHT_MAP)] _LightMapEnable("Light Map Enable", Float) = 0

		[Toggle(LERP_NOMAL)] _LerpNormalEnable("Lerp Normal Enable", Float) = 0

		[Toggle(SPECULAR)]_SpecularEnable("Specular", Float) = 0

		_Power("Power", Float) = 5

		_MaxDistance("Max Distance", Float) = 20
		_MinDistance("Min Distance", Float) = 8

		_LightMap("Light Map", 2D) = "white" {}

		_MainTex("Albedo (RGB)", 2D) = "white" {}
	}

	SubShader
	{
		Tags
		{ 
			"RenderType" = "Opaque" 
		}

		LOD 100

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

#pragma multi_compile_fwdbase
		// make fog work
#pragma multi_compile_fog

#pragma shader_feature __ SPECULAR
#pragma shader_feature __ LERP_NOMAL
#pragma shader_feature __ LIGHT_MAP

#include "UnityCG.cginc"
#include "AutoLight.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;

#ifdef LERP_NOMAL
				float4 tangent : TANGENT;
#endif

#ifdef LIGHT_MAP
				float2 uv_LightMap : TEXCOORD1;
#endif

				float2 uv_MainTex : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv_MainTex : TEXCOORD0;
#ifdef LIGHT_MAP
				float2 uv_LightMap : TEXCOORD1; 
#endif
#ifdef SPECULAR
				float3 worldNormal : TEXCOORD2;
#else
				float3 ambient : TEXCOORD2;
				float3 diffuse : TEXCOORD3;
#endif
				float3 worldPos : TEXCOORD4;
				float4 pos : SV_POSITION;
				SHADOW_COORDS(5)
				UNITY_FOG_COORDS(6)
			};

			uniform float4 _LightColor0;

#ifdef SPECULAR
			float _Power;
#endif

#ifdef LERP_NOMAL
			float _MaxDistance;
			float _MinDistance;
#endif

#ifdef LIGHT_MAP
			sampler2D _LightMap;
			float4 _LightMap_ST;
#endif

			sampler2D _MainTex;
			float4 _MainTex_ST;

			v2f vert(appdata v)
			{
				v2f o;

				o.pos = UnityObjectToClipPos(v.vertex);

				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

#ifdef LERP_NOMAL
				half3 normal = lerp(v.normal, v.tangent.xyz, saturate((distance(_WorldSpaceCameraPos.xyz, o.worldPos) - _MinDistance) / (_MaxDistance - _MinDistance)));
#else
				half3 normal = v.normal;
#endif

#ifdef SPECULAR
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
#else
				half3 worldNormal = UnityObjectToWorldNormal(normal);
				o.ambient = ShadeSH9(half4(worldNormal, 1)) * saturate(dot(worldNormal, normalize(UnityWorldSpaceViewDir(o.worldPos))) * 0.5f + 0.5f);
				o.diffuse = max(dot(worldNormal, normalize(UnityWorldSpaceLightDir(o.worldPos))), 0.0f) * _LightColor0.rgb;
#endif
				o.uv_MainTex = TRANSFORM_TEX(v.uv_MainTex, _MainTex);

#ifdef LIGHT_MAP
				o.uv_LightMap = TRANSFORM_TEX(v.uv_LightMap, _LightMap);
#endif

				TRANSFER_SHADOW(o);
				UNITY_TRANSFER_FOG(o,o.pos);

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				// sample the texture
				fixed4 color = tex2D(_MainTex, i.uv_MainTex);
#ifdef LIGHT_MAP
				color *= tex2D(_LightMap, i.uv_LightMap);
#endif
				UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
#ifdef SPECULAR
				float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
				float3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
				float ambient = saturate(dot(i.worldNormal, viewDir) * 0.5f + 0.5f);
				float diffuse = max(dot(i.worldNormal, lightDir), 0.0f);
				float specular = pow(max(0.0f, dot(normalize(viewDir + lightDir), i.worldNormal)), _Power);
				color.rgb *= ambient * UNITY_LIGHTMODEL_AMBIENT.rgb + (diffuse + specular) * atten * _LightColor0.rgb;
#else
				color.rgb *= i.ambient + i.diffuse * atten;
#endif
				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, color);
				return color;
			}
			ENDCG
		}

		Pass
		{
			Name "FORWARD_DELTA"
			Tags
			{
				"LightMode" = "ForwardAdd"
			}

			Blend One One
			CGPROGRAM

#pragma vertex vert
#pragma fragment frag

#pragma multi_compile_fwdadd_fullshadows
#pragma shader_feature __ SPECULAR

#include "UnityCG.cginc"
#include "AutoLight.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv_MainTex : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv_MainTex : TEXCOORD0;

#ifdef SPECULAR
				float3 worldNormal : TEXCOORD2;
#else
				float3 diffuse : TEXCOORD2;
#endif
				float3 worldPos : TEXCOORD3;
				float4 pos : SV_POSITION;
			};

			float _Power;

			uniform float4 _LightColor0;

			sampler2D _MainTex;
			float4 _MainTex_ST;

			v2f vert(appdata v)
			{
				v2f o;

				o.pos = UnityObjectToClipPos(v.vertex);

				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
#ifdef SPECULAR
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
#else
				half3 worldNormal = UnityObjectToWorldNormal(v.normal);
				o.diffuse = max(dot(worldNormal, normalize(UnityWorldSpaceLightDir(o.worldPos))), 0.0f) * _LightColor0.rgb;
#endif
				o.uv_MainTex = TRANSFORM_TEX(v.uv_MainTex, _MainTex);

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				// sample the texture
				fixed4 color = tex2D(_MainTex, i.uv_MainTex);

				UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
#ifdef SPECULAR
				float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
				float3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
				float diffuse = max(dot(i.worldNormal, lightDir), 0.0f);
				float specular = pow(max(0.0f, dot(normalize(viewDir + lightDir), i.worldNormal)), _Power);
				color.rgb *= (diffuse + specular) * atten * _LightColor0.rgb;
#else
				color.rgb *= i.diffuse * atten;
#endif
				return color;
			}
			ENDCG
		}

		UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
	}
}
