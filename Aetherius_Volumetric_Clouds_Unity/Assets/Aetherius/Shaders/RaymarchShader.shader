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
			Texture2D<float> blueNoiseTexture;
			SamplerState samplerblueNoiseTexture;
			SamplerState samplerbaseShapeTexture;
			SamplerState samplerdetailTexture;
			SamplerState samplerweatherMapTexture;
			float baseShapeSize;
			float detailSize;
			float weatherMapSize;
			float globalCoverage;
			float globalDensity;
			float3 weatherMapOffset;
			float3 sunDir;
			float lightAbsorption;
			float lightIntensity;
			float3 lightColor;
			float4 coneKernel[6];
			float osA;
			float ambientMin;//0 to 1
			float attenuationClamp;//0 to 1

			float silverIntesity;//0 to 1
			float silverExponent;//0 to 1

			float ShapeAltering(float heightPercent) //Round clouds towards edges (bottom & top) of the layer cloud
			{
				return	saturate(Remap(heightPercent, 0.0, 0.1, 0.0, 1.0)) * saturate(Remap(heightPercent, 0.2, 1.0, 1.0, 0.0)); //TODO Change
			}

			float DensityAltering(float heightPercent,float weatherMapSample) //Makes Clouds have more shape at the top & be more round towards the bottom, the weather map also influences the density
			{
				float densityBottom = saturate(Remap(heightPercent,0.0,0.2,0.0,1.0));//Reduces density towards the bottom of the cloud
				float densityTop = saturate(Remap(heightPercent, 0.9, 1.0, 1.0, 0.0));
				return heightPercent * globalDensity * weatherMapSample * densityBottom * densityTop * 2.0; //2 as weatherMap *2 when weathermap > 0.5 creates higher density clouds
			}

			float GetDensity(float3 currPos)
			{
				float baseScale = 1 / 1000.0;

				float density = 0.0;
				if (currPos.y >= minCloudHeight && currPos.y <= maxCloudHeight) //If inside of bouds of cloud layer
				{
					float cloudHeightPercent = Remap(currPos.y, minCloudHeight, maxCloudHeight, 0.0, 1.0);//value between 0 & 1 showing where we are in the cloud layer
					float weatherMapCloud = weatherMapTexture.Sample(samplerweatherMapTexture, (currPos.xz + weatherMapOffset.xz) * baseScale * weatherMapSize); //We sample the weather map
					float4 lowFreqNoise = baseShapeTexture.Sample(samplerbaseShapeTexture, currPos * baseScale * baseShapeSize);
					float4 highFreqNoise = detailTexture.Sample(samplerdetailTexture, currPos * baseScale * detailSize);//TODO make a detail size variable 

					//Cloud Base shape
					float lowFreqFBM = (lowFreqNoise.g * 0.625) + (lowFreqNoise.b * 0.25) + (lowFreqNoise.a * 0.125);
					float cloudNoise = Remap(lowFreqNoise.r, -(1.0 - lowFreqFBM), 1.0, 0.0, 1.0);
					
					//Detail Shape
					float highFreqFBM = (highFreqNoise.r * 0.625) + (highFreqNoise.g * 0.25) + (highFreqNoise.b * 0.125);
					float detailNoise = lerp(highFreqFBM,1.0 - highFreqFBM, saturate(cloudHeightPercent * 5.0)) * 0.3 * exp(-globalCoverage * .75);

					//Detail - Base Shape
					float cloudBase = saturate(Remap(cloudNoise * ShapeAltering(cloudHeightPercent),1.0 - (weatherMapCloud * globalCoverage),1.0,0.0,1.0)); //Cloud noise is remapped into the weatherMap takin into accoun global coverage as well
					density = saturate(Remap(cloudBase, detailNoise,1.0,0.0,1.0));
					density *= DensityAltering(cloudHeightPercent, weatherMapCloud);
				}


				return density;
			}

			float BeerLambertLaw(float accDensity, float absorptionCoefficient)
			{
				return exp(-accDensity * absorptionCoefficient);
			}			


			float DensityTowardsLight(float3 currPosition,float cloudLayerHeight)
			{
				int iter = 6;
				float accDensity = 0.0;
				float stepSize = (cloudLayerHeight*0.5)/float(iter);//TODO make as variable (maybe when we have cone light samples?)
				float3 startingPos = currPosition;
				for (int currStep = 0; currStep < iter; ++currStep)
				{
					currPosition += -sunDir * stepSize + (stepSize* coneKernel[currStep].xyz* float(currStep));
					accDensity += GetDensity(currPosition)*stepSize;
				}

				accDensity += GetDensity(startingPos - sunDir * stepSize*6.0 +(-sunDir *stepSize *6*3))*stepSize;

				return accDensity;
			}

			float HenyeyGreenstein(float cosAngle, float g) //G ranges between -1 & 1
			{
				float g2 = g * g;
				return ((1.0 - g2) / pow(1.0 + g2 - 2.0 * g * cosAngle, 1.5)) / (4 * 3.1415);
			}

			float InScatteringExtra(float cosAngle)
			{
				return silverIntesity * pow(saturate(cosAngle), silverExponent);
			}

			float IOS(float cosAngle, float inS,float outS)
			{
				return lerp(max(HenyeyGreenstein(cosAngle, inS), InScatteringExtra(cosAngle)), HenyeyGreenstein(cosAngle, -outS),0.5);
			}

			float Attenuation(float lightDensity, float cosAngle)
			{
				float prim = exp(-lightAbsorption * lightDensity);
				float scnd = exp(-lightAbsorption * attenuationClamp) * 0.7;

				float checkval = Remap(cosAngle, 0.0, 1.0, scnd, scnd * 0.5);
				return max(checkval, prim);
			}

			//OutScatterAmbient
			float OSa(float density,float hPercent)
			{
				
				float depth= osA* pow(density, Remap(hPercent, 0.3, 0.9, 0.5, 1.0));
				float vertical = pow(saturate(Remap(hPercent, 0.0, 0.3, 0.8, 1.0)),0.8);
				float outScatter = depth * vertical;
				
				return 1.0 - saturate(outScatter);
			}

			

			float4 Raymarching(float3 ro, float3 rd,float2 uv) //where ro is ray origin & rd is ray direction
			{
				float3 col = float3(1.0,1.0,1.0);

				float stepLength = maxRayDist / maxSteps; //TODO provisional, will find another solution for the stepping later
				uv.x *= (_ScreenParams.x / _ScreenParams.y);
				uv *= min(_ScreenParams.x, _ScreenParams.y)/ 128;//we assome blue noise has a 128 res texture
				float3 currPos = ro + rd* stepLength * (blueNoiseTexture.Sample(samplerblueNoiseTexture, uv ) -0.5)*2.0;
				float density = 0.0;
				float lightEnergy = 0.0;
				float transmission = 1.0;
				float cosAngle = dot( -rd,sunDir);//We assume they are normalized

				for (int currStep = 0; currStep < maxSteps; ++currStep)
				{
					if (density < 1.0)
					{
						float currDensity = GetDensity(currPos);
						if (currDensity > 0.0)
						{

							float cloudHeightPercent = saturate(Remap(currPos.y, minCloudHeight, maxCloudHeight, 0.0, 1.0));//value between 0 & 1 showing where we are in the cloud layer


							//float absorption = BeerLambertLaw(DensityTowardsLight(currPos), lightAbsorption);
							//float scattering = IOS(cosAngle, 0.2,0.1);
							//float ambientScatter = OSa(currDensity*stepLength, cloudHeightPercent);

							//lightEnergy += absorption * scattering * ambientScatter* transmission * currDensity * stepLength * lightIntensity;
							//transmission *= BeerLambertLaw(currDensity * stepLength, lightAbsorption);//TODO make different light absorption parameters when on cloud or towards light	
							
							
							float attenuation_prob = Attenuation(DensityTowardsLight(currPos, maxCloudHeight- minCloudHeight), cosAngle);
							float ambient_out_scatter = OSa(currDensity * stepLength, cloudHeightPercent);
							float sun_highlight = IOS(cosAngle, 0.2, 0.1);

							//Deprecated
							//attenuation_prob = BeerLambertLaw(DensityTowardsLight(currPos, maxCloudHeight - minCloudHeight), lightAbsorption);
							//ambient_out_scatter = 1.0;
							//sun_highlight = HenyeyGreenstein(cosAngle, 0.2);
							//End of deprecated




							float attenuation = attenuation_prob * sun_highlight * ambient_out_scatter;
							attenuation = max(currDensity * stepLength* ambientMin *(1.0 -pow(saturate(currStep*stepLength / 4000), 2)), attenuation);
							lightEnergy += attenuation* currDensity * stepLength* lightIntensity;
							
							
							density += currDensity * stepLength;

						}
					}
					currPos += rd * stepLength;


				}
				//TODO density above 1 makes banding worse somehow, fix, do we really need to clamp density?
				density = saturate(density);//we dont want density above 1 for now (TODO visual glitch in the sun if above 1, fix this?)

				col = lightColor*  lightEnergy;

				//col = lightColor* lightIntensity * transmission + lightEnergy;
				//col = col * lightEnergy;
				float div = (1.0 / 2.2);
				col = pow(col, float3(div, div, div));//Linear to gamma needed? TODO depends on the project settings i think

				return float4(col, density);
			}



			fixed4 frag(v2f i) : SV_Target
			{
				fixed3 col = tex2D(_MainTex, i.uv);
			// just invert the colors
			//col.rgb = 1 - col.rgb;

			float3 rayDirection = normalize(i.ray.xyz);
			float3 rayOrigin =  _WorldSpaceCameraPos;

			float4 result = Raymarching(rayOrigin, rayDirection,i.uv);

			return fixed4(col * (1.0 - result.w) + result.rgb * result.w,1.0); //lerp between colors of the scene & the color of the volume (TODO temporal, will have another solution later)

			}
ENDCG
		}
	}
}
