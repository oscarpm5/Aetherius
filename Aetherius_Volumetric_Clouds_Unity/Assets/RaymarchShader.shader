Shader "Aetherius/RaymarchShader"
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

			sampler2D _MainTex;

			float4x4 _CamFrustum;//Eye Space
			float4x4 _CamToWorldMat;//Convert camera to world space

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float3 ray : TEXCOORD1;
			};

			v2f vert(appdata v)
			{
				v2f o;

				half index = v.vertex.z; //we use the z component of the vertex as an index for the _CamFrustum matrix
				v.vertex.z = 0;

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;

				o.ray = _CamFrustum[(int)index].xyz;

				o.ray /= abs(o.ray.z);//Normalize in z direction
				o.ray = mul(_CamToWorldMat, o.ray);

				return o;
			}

			float Sphere(float3 centre, float radius, float3 p)
			{
				return length(centre - p) - radius;
			}

			float4 Raymarching(float3 ro, float3 rd) //where ro is ray origin & rd is ray direction
			{
				float4 result = float4(0.0,0.0,0.0,0.0);
				float maxSteps = 256;
				float maxRayDist = 5;//In meters "Far Plane" of the raycast
				float stepLength = maxRayDist / maxSteps; //TODO provisional, will find another solution for the stepping later

				float3 currPos = ro;

				for (int currStep = 0; currStep < maxSteps; ++currStep)
				{
					currPos = ro + rd * stepLength * currStep;

					if (Sphere(float3(0.0,0.0,0.0), 0.5, currPos) <= 0.0)
					{
						result.w += .025;//this is the density for now TODO change
					}

				}

				result.w = clamp(result.w, 0.0, 1.0);//we dont want density above 1 for now (TODO visual glitch in the sun if above 1, fix this?)
				result.rgb = float3(1.0,0.1,1.0);

				return result;
			}



			fixed4 frag(v2f i) : SV_Target
			{
				fixed3 col = tex2D(_MainTex, i.uv);
			// just invert the colors
			//col.rgb = 1 - col.rgb;

			float3 rayDirection = normalize(i.ray.xyz);
			float3 rayOrigin = _WorldSpaceCameraPos;

			float4 result = Raymarching(rayOrigin, rayDirection);

			return fixed4(col * (1.0 - result.w) + result.rgb * result.w,1.0); //lerp between colors of the scene & the color of the volume (TODO temporal, will have another solution later)

			}
ENDCG
		}
	}
}