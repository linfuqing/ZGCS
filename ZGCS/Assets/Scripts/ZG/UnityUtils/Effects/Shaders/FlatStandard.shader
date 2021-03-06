﻿Shader "ZG/FlatStandard" {
	Properties {
		[Toggle(ENABLE_TEXTURE)] _EnalbeTexture("Enalbe Texture", Float) = 1
		[Toggle(ENABLE_COLOR)] _EnalbeColor("Enalbe Color", Float) = 0
		[Toggle(ENABLE_EMISSION)] _EnalbeEmission("Enalbe Emission", Float) = 0

		_Emission ("Emission", Color) = (0, 0, 0, 0)
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows
		#pragma shader_feature __ ENABLE_TEXTURE
		#pragma shader_feature __ ENABLE_COLOR
		#pragma shader_feature __ ENABLE_EMISSION

		// Use shader model 3.0 target, to get nicer looking lighting
		//#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
			float3 worldNormal;
			float3 viewDir;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;
		half4 _Emission;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void surf (Input IN, inout SurfaceOutputStandard o) 
		{
#if ENABLE_TEXTURE && ENABLE_COLOR
			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
#elif ENABLE_TEXTURE
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
#else 
			fixed4 c = _Color;
#endif
			o.Albedo = c.rgb * (dot(IN.worldNormal, IN.viewDir) * 0.5f + 0.5f);

#if ENABLE_EMISSION
			o.Emission = _Emission;
#endif
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
