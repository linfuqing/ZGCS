﻿Shader "ZG/ViewDir4Alpha" 
{
	Properties 
	{
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
		_Base("Base", Range(0,1)) = 0.0
		_Forward("Forward", Vector) = (0.0, 0.0, 1.0, 0.0)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
	}

	SubShader 
	{
		Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert alpha:blend

		// Use shader model 3.0 target, to get nicer looking lighting
		//#pragma target 3.0

		sampler2D _MainTex;

		struct Input 
		{
			fixed alpha;
			float2 uv_MainTex;
		};

		half _Glossiness;
		half _Metallic;
		half _Base;
		float4 _Forward;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_CBUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_CBUFFER_END

		void vert(inout appdata_full v, out Input o) 
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);
			o.alpha = saturate(_Base + 1.0 - max(0.0, dot(normalize(ObjSpaceViewDir(v.vertex)), _Forward)));
		}

		void surf (Input IN, inout SurfaceOutputStandard o) 
		{
			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
			o.Albedo = c.rgb;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a * IN.alpha;// saturate(_Base + 1.0 - max(0.0, dot(IN.viewDir, _Forward.xyz)));
		}
		ENDCG
	}
	FallBack "Diffuse"
}