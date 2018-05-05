// Upgrade NOTE: upgraded instancing buffer 'Props' to new syntax.

Shader "ZG/FlatWater" 
{
	Properties 
	{
		[Toggle(FLAT_FRAG)]_FragFlat("Fragment Flat", Float) = 0

		_SegmentWidth("Segment Width", Float) = 1
		_SegmentHeight("Segment Height", Float) = 1

		_GAmplitude("Wave Amplitude", Vector) = (0.3 ,0.35, 0.25, 0.25)
		_GFrequency("Wave Frequency", Vector) = (1.3, 1.35, 1.25, 1.25)
		_GSteepness("Wave Steepness", Vector) = (1.0, 1.0, 1.0, 1.0)
		_GSpeed("Wave Speed", Vector) = (1.2, 1.375, 1.1, 1.5)
		_GDirectionAB("Wave Direction", Vector) = (0.3 ,0.85, 0.85, 0.25)
		_GDirectionCD("Wave Direction", Vector) = (0.1 ,0.9, 0.5, 0.5)

		_MaxDistance("Max Distance", Float) = 10.0
		_Shininess("Shininess", Float) = 5.0
		_ShallowWaterColor("Shallow Water Color", Color) = (1.0 ,1.0, 1.0, 1.0)
		_DeepWaterColor("Deep Water Color", Color) = (1.0 ,1.0, 1.0, 1.0)

		_CubeMap("Cube Map", Cube) = "" {}
	}

	SubShader 
	{
		Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" "DisableBatching" = "True" }

		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Lambert alpha:blend vertex:vert

		#pragma shader_feature __ FLAT_FRAG
		// Use shader model 3.0 target, to get nicer looking lighting
		//#pragma target 3.0

		/*struct appdata 
		{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
		};*/

		float _SegmentWidth;
		float _SegmentHeight;

		float4 _GAmplitude;
		float4 _GFrequency;
		float4 _GSteepness;
		float4 _GSpeed;
		float4 _GDirectionAB;
		float4 _GDirectionCD;

		half3 GerstnerOffset4(half2 xzVtx, half4 steepness, half4 amp, half4 freq, half4 speed, half4 dirAB, half4 dirCD)
		{
			half3 offsets;

			half4 AB = steepness.xxyy * amp.xxyy * dirAB.xyzw;
			half4 CD = steepness.zzww * amp.zzww * dirCD.xyzw;

			half4 dotABCD = freq.xyzw * half4(dot(dirAB.xy, xzVtx), dot(dirAB.zw, xzVtx), dot(dirCD.xy, xzVtx), dot(dirCD.zw, xzVtx));
			half4 TIME = _Time.yyyy * speed;

			half4 COS = cos(dotABCD + TIME);
			half4 SIN = sin(dotABCD + TIME);

			offsets.x = dot(COS, half4(AB.xz, CD.xz));
			offsets.z = dot(COS, half4(AB.yw, CD.yw));
			offsets.y = dot(SIN, amp);

			return offsets;
		}

		half3 GerstnerOffset(float4 vertex)
		{
			return GerstnerOffset4(mul(unity_ObjectToWorld, vertex).xz, _GSteepness, _GAmplitude, _GFrequency, _GSpeed, _GDirectionAB, _GDirectionCD);
		}

		void vert(inout appdata_full v)
		{
#ifndef FLAT_FRAG
			//v.normal.x = sign(v.normal.x);
			//v.normal.y = sign(v.normal.y);
			//v.normal.z = max(sign(v.normal.z), 0.0);
			half x = v.vertex.x + _SegmentWidth * v.normal.x, y = v.vertex.z + _SegmentHeight * v.normal.y, z = 1.0 - v.normal.z;
			float4 vertex = float4(v.normal.z * min(v.vertex.x, x) + z * max(v.vertex.x, x), v.vertex.y, v.normal.z * min(v.vertex.z, y) + z * max(v.vertex.z, y), v.vertex.w);
			half3 offset = GerstnerOffset(vertex);

			z = v.normal.z * 2.0 - 1.0;
			x = _SegmentWidth * z;
			y = _SegmentHeight * z;
			half3 distanceX = GerstnerOffset(vertex + float4(x, 0.0, 0.0, 0.0)) + float3(x, 0.0, 0.0) - offset;
			half3 distanceY = GerstnerOffset(vertex + float4(0.0, 0.0, y, 0.0)) + float3(0.0, 0.0, y) - offset;
			v.normal = cross(normalize(distanceY), normalize(distanceX));
#endif
			v.vertex.xyz += GerstnerOffset(v.vertex);
		}

		struct Input 
		{
			float3 worldNormal;
			float3 worldPos;
		};

		float _MaxDistance;
		float _Shininess;
		float4 _ShallowWaterColor;
		float4 _DeepWaterColor;

		samplerCUBE _CubeMap;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		half FastFresnel(float r, float facing, float shininess)
		{
			return r + (1.0 - r) * pow(1.0 - facing, shininess);
		}

		void surf (Input IN, inout SurfaceOutput o) 
		{
			float3 normalDirection;

#ifdef FLAT_FRAG
			float3 normalX = normalize(float3(ddx(IN.worldPos.x), ddx(IN.worldPos.y), ddx(IN.worldPos.z)));
			float3 normalY = normalize(float3(ddy(IN.worldPos.x), ddy(IN.worldPos.y), ddy(IN.worldPos.z)));

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
			normalDirection = IN.worldNormal;
#endif

			float3 worldSpaceViewDir = UnityWorldSpaceViewDir(IN.worldPos);
			float3 viewDirection = normalize(worldSpaceViewDir);

			//half3 skyColor = texCUBE(_CubeMap, reflect(-viewDirection, normalDirection)).rgb;

			half facing = dot(viewDirection, normalDirection);
			half3 waterColor = lerp(_ShallowWaterColor.rgb, _DeepWaterColor.rgb, facing);

			float fresnel = FastFresnel(saturate(length(worldSpaceViewDir) / _MaxDistance), facing, _Shininess);

			o.Albedo = waterColor / fresnel;// +skyColor;
			o.Alpha = fresnel;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
