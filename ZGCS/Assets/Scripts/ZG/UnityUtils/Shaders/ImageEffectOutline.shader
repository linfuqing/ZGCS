Shader "ZG/ImageEffectOutline"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	SubShader
	{
		Pass
		{
			ZTest Off
			Cull Off
			ZWrite Off
			Fog{ Mode Off }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest  

			#include "UnityCG.cginc"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;

			v2f vert(appdata_img v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord.xy;

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				return tex2D(_MainTex, i.uv);
			}
			ENDCG
		}

		Pass
		{
			ZTest Off
			Cull Off
			ZWrite Off
			Fog{ Mode Off }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//#pragma fragmentoption ARB_precision_hint_fastest  

			#include "UnityCG.cginc"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 uv01 : TEXCOORD1;
				float4 uv23 : TEXCOORD2;
				float4 uv45 : TEXCOORD3;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;

			float _BlurOffsetX;
			float _BlurOffsetY;

			v2f vert(appdata_img v)
			{
				v2f o;
				float2 offsets = float2(_MainTex_TexelSize.x * _BlurOffsetX, _MainTex_TexelSize.y * _BlurOffsetY);

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord.xy;

				o.uv01 = v.texcoord.xyxy + offsets.xyxy * float4(1, 1, -1, -1);
				o.uv23 = v.texcoord.xyxy + offsets.xyxy * float4(1, 1, -1, -1) * 2.0;
				o.uv45 = v.texcoord.xyxy + offsets.xyxy * float4(1, 1, -1, -1) * 3.0;

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 color = fixed4(0.0, 0.0, 0.0, 0.0);

				color += 0.40 * tex2D(_MainTex, i.uv);
				color += 0.15 * tex2D(_MainTex, i.uv01.xy);
				color += 0.15 * tex2D(_MainTex, i.uv01.zw);
				color += 0.10 * tex2D(_MainTex, i.uv23.xy);
				color += 0.10 * tex2D(_MainTex, i.uv23.zw);
				color += 0.05 * tex2D(_MainTex, i.uv45.xy);
				color += 0.05 * tex2D(_MainTex, i.uv45.zw);

				return color;
			}
			ENDCG
		}

		Pass
		{
			ZTest Off
			Cull Off
			ZWrite Off
			Fog{ Mode Off }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//#pragma fragmentoption ARB_precision_hint_fastest

			#include "UnityCG.cginc"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			sampler2D _BlurTex;

			v2f vert(appdata_img v)
			{
				v2f o;

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord.xy;

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				return tex2D(_BlurTex, i.uv) - tex2D(_MainTex, i.uv);
			}
			ENDCG
		}

		Pass
		{
			ZTest Off
			Cull Off
			ZWrite Off
			Fog{ Mode Off }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//#pragma fragmentoption ARB_precision_hint_fastest

			#include "UnityCG.cginc"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			sampler2D _BlurTex;
			float _Strength;

			v2f vert(appdata_img v)
			{
				v2f o;

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord.xy;

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				return tex2D(_MainTex, i.uv) + tex2D(_BlurTex, i.uv) * _Strength;
			}
			ENDCG
		}
	}
}
