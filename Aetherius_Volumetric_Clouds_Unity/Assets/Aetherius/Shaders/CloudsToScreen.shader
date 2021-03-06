Shader "Aetherius/CloudsToScreen"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
		SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//#pragma enable_d3d11_debug_symbols //TODO delete when finished debugging

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			sampler2D _MainTex;
			sampler2D cloudTex;//RGB scattered luminance, A scattered transmittance
			float2 texelRes;
			bool useBlur;
			int kernelHalfDim;

			fixed4 frag(v2f i) : SV_Target
			{
				if (useBlur == true)
				{
					float4 col = tex2D(_MainTex, i.uv);
					float4 cloudData;
					for (int y = -kernelHalfDim; y <= kernelHalfDim; ++y)
					{
						for (int x = -kernelHalfDim; x <= kernelHalfDim; ++x)
						{
							float2 offset = texelRes * float2(x,y);
							cloudData += tex2D(cloudTex, i.uv + offset);
						}
					}
					cloudData /= (kernelHalfDim * 2 + 1) * (kernelHalfDim * 2 + 1);
					float3 scatteredLuminance = cloudData.rgb;
					float scatteredtransmittance = cloudData.a;
					return fixed4(col.rgb * scatteredtransmittance + scatteredLuminance, 1.0);
				}
				else
				{
					float4 col = tex2D(_MainTex, i.uv);
					float4 cloudData = tex2D(cloudTex, i.uv);
					float3 scatteredLuminance = cloudData.rgb;
					float scatteredtransmittance = cloudData.a;
					return fixed4(col.rgb * scatteredtransmittance + scatteredLuminance, 1.0);
				}

			}
			ENDCG
		}
	}
}
