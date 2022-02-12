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
			sampler2D _DisplayTex;
			float debugTextureSize;//between 0 and 1
			float debugTextureOffsetX;
			float debugTextureOffsetY;


			fixed4 debugTextureDisplay(float2 uv)
			{
				return tex2D(_DisplayTex, uv);
			}


			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);

				float w = _ScreenParams.x;//width of the cam target texture in pixels
				float h = _ScreenParams.y;//height of the cam target texture in pixels

				float minDimensions = min(w, h);//we get the shortest screen axis in pixels

				float x = i.uv.x * w; //Pixel equivalent of uv.x
				float y = i.uv.y * h; //Pixel equivalent of uv.y



				if (x + debugTextureOffsetX < minDimensions * debugTextureSize && y + debugTextureOffsetY < (minDimensions * debugTextureSize))
				{
					col = debugTextureDisplay(float2(x / (minDimensions * debugTextureSize),y / (minDimensions * debugTextureSize)));
				}
				return col;

			}
			ENDCG
		}
	}
}