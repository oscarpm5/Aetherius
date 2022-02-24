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

			float Remap(float v, float minOrigin, float maxOrigin, float minTarget, float maxTarget)
			{
				return minTarget +
					(
					((v - minOrigin) * (maxTarget - minTarget))
					/
					(maxOrigin - minOrigin)
					);
			}

			int maxSteps;
			float maxRayDist;//In meters "Far Plane" of the raycast
			float minCloudHeight;
			float maxCloudHeight;
			Texture3D<float4> baseShapeTexture;
			Texture3D<float4> detailTexture; //TODO see if I can get around using a float3
			Texture2D<float4> weatherMapTexture;
			SamplerState samplerbaseShapeTexture;
			SamplerState samplerdetailTexture;
			SamplerState samplerweatherMapTexture;
			float baseShapeSize;
			float weatherMapSize;
			float globalCoverage;
			float3 weatherMapOffset;

			float ShapeAltering(float heightPercent)
			{
				return	saturate(Remap(heightPercent, 0.0, 0.1, 0.0, 1.0))* saturate(Remap(heightPercent, 0.2, 1.0, 1.0, 0.0)); //TODO Change
			}


			float GetDensity(float3 currPos)
			{
				float baseScale = 1 / 1000.0;
				
				float cloud = 0.0;
				if (currPos.y >= minCloudHeight && currPos.y <= maxCloudHeight) //If inside of bouds of cloud layer
				{
					float cloudHeightPercent = Remap(currPos.y, minCloudHeight, maxCloudHeight, 0.0, 1.0);//value between 0 & 1 showing where we are in the cloud layer
					float4 lowFreqNoise = baseShapeTexture.Sample(samplerbaseShapeTexture, currPos * baseScale * baseShapeSize);
					float lowFreqFBM = (lowFreqNoise.g * 0.625) + (lowFreqNoise.b * 0.5) + (lowFreqNoise.a * 0.25);


					float cloudNoise = Remap(lowFreqNoise.r, -(1.0-lowFreqFBM), 1.0, 0.0, 1.0);
					float weatherMapCloud = weatherMapTexture.Sample(samplerweatherMapTexture,(currPos.xz+weatherMapOffset.xz) * baseScale * weatherMapSize); //We sample the weather map
					cloud = saturate(Remap(cloudNoise,1.0- globalCoverage * weatherMapCloud,1.0,0.0,1.0)); //Cloud noise is remapped into the weatherMap takin into accoun global coverage as well
					
					cloud = weatherMapCloud * ShapeAltering(cloudHeightPercent);//TODO DEBUGGING
				}

				return cloud;
			}

			
			float4 Raymarching(float3 ro, float3 rd) //where ro is ray origin & rd is ray direction
			{
				float3 col = float3(1.0,1.0,1.0);

				float stepLength = maxRayDist / maxSteps; //TODO provisional, will find another solution for the stepping later

				float3 currPos = ro;
				float density = 0.0;
				for (int currStep = 0; currStep < maxSteps; ++currStep)
				{
					currPos = ro + rd * stepLength * currStep;
					if (density < 1.0)
					{
						density += GetDensity(currPos)*0.1;
					}

				}

				density = saturate(density);//we dont want density above 1 for now (TODO visual glitch in the sun if above 1, fix this?)

				return float4(col,density);
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
