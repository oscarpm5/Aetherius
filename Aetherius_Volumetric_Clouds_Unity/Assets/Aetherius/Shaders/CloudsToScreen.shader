Shader "Aetherius/CloudsToScreen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            sampler2D cloudTex;//RGB scattered luminance, A scattered transmittance


            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 cloudData = tex2D(cloudTex, i.uv);
                float3 scatteredLuminance = cloudData.rgb;
                float scatteredtransmittance = cloudData.a;
                return fixed4(col.rgb * scatteredtransmittance + scatteredLuminance,1.0);


                /*for (int y = -1; y < 1; ++y)
                {
                    for (int x = -1; x < 1; ++x)
                    {

                    }
                }*/
            }
            ENDCG
        }
    }
}
