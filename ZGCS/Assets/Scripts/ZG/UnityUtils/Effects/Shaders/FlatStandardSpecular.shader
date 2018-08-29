Shader "ZG/FlatStandardSpecular" {
	Properties {
		[Toggle(ENABLE_TEXTURE)] _EnalbeTexture("Enalbe Texture", Float) = 1
		[Toggle(ENABLE_COLOR)] _EnalbeColor("Enalbe Color", Float) = 0
		[Toggle(ENABLE_EMISSION)] _EnalbeEmission("Enalbe Emission", Float) = 0

		_Emission("Emission", Color) = (0, 0, 0, 0)

		_Specular("Specular", Color) = (1, 1, 1, 1)

		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Smoothness("Smoothness", Range(0,1)) = 0.5
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf StandardSpecular fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
			float3 worldNormal;
			float3 viewDir;
		};

		half _Smoothness;
		half4 _Emission;
		half4 _Specular;
		fixed4 _Color;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void surf (Input IN, inout SurfaceOutputStandardSpecular o) 
		{

#if ENABLE_TEXTURE && ENABLE_COLOR
			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
#elif ENABLE_TEXTURE
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
#else 
			fixed4 c = _Color;
#endif
			o.Albedo = c.rgb * (dot(IN.worldNormal, IN.viewDir) * 0.5f + 0.5f);

#if ENABLE_EMISSION
			o.Emission = _Emission;
#endif
			o.Specular = _Specular;
			o.Smoothness = _Smoothness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
