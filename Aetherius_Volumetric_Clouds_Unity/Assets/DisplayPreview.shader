Shader "Aetherius/DisplayPreview"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
		SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

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
			sampler2D _DisplayTex2D;
			sampler3D _DisplayTex3D;
			float debugTextureSize;//between 0 and 1
			float tileAmmount;//amount of tiling for the texture
			float slice3DTex;
			float4 channelMask;
			bool displayGrayscale;
			bool displayAllChannels;

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);

				float w = _ScreenParams.x;//width of the cam target texture in pixels
				float h = _ScreenParams.y;//height of the cam target texture in pixels

				float minDimensionsScaled = min(w, h) * debugTextureSize;//we get the shortest screen axis in pixels and multiply it by a scale factor between 0 and 1
				float2 currPixel = float2(i.uv.x * w, i.uv.y * h);//Pixel equivalent of uv.xy //TODO change name to a more readable one


				if (currPixel.x < minDimensionsScaled && currPixel.y < minDimensionsScaled) //overwrite only the pixels inside the wanted square
				{
					float2 st = (currPixel / minDimensionsScaled) * tileAmmount;
					col = tex3D(_DisplayTex3D, float3(frac(st), slice3DTex));
					
					if (!displayAllChannels) //If we only want to display one channel
					{
						col *= channelMask;

						if (displayGrayscale || channelMask.w == 1) //we want to show alpha channel as a grayscale texture with an alpha of 1
						{
							float colChannel = col.x + col.y + col.z + col.w;//we know only one channel will be different from 0 here
							col = float4(colChannel, colChannel, colChannel, 1.0);
						}
					}				
				}

				return col;

			}
			ENDCG
		}
	}
}
