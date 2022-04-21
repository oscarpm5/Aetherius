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

			float Remap(float v, float minOrigin, float maxOrigin, float minTarget, float maxTarget)
			{
				return minTarget + (((v - minOrigin) / (maxOrigin - minOrigin)) * (maxTarget - minTarget));
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
			float3 ambientColor;
			float4 coneKernel[6];
			float osA;
			float ambientMin;//0 to 1
			float attenuationClamp;//0 to 1

			float silverIntesity;//0 to 1
			float silverExponent;//0 to 1
			float shadowBaseLight;//0 to 1

			StructuredBuffer<float> densityCurveBuffer;

			int mode;

			float3 planetAtmos;
			float3 windDir;
			float baseShapeWindMult;
			float detailShapeWindMult;
			float skewAmmount;

			bool cumulusHorizon;
			bool windDisplacesWeatherMap;
			float2 cumulusHorizonGradient;


			sampler2D _CameraDepthTexture;

			float2 GetAtmosphereIntersection(float3 ro,float3 rd,float3 sphO, float r)//returns -1 when no intersection has been found
			{
				float t0 = -1.0;
				float t1 = -1.0;

				float t = dot(sphO - ro,rd);

				float3 p = ro + rd * t;
				float y = length(sphO - p);



				if (y <= r)
				{
					float x = sqrt(r * r - y * y);
					t0 = t - x;
					t1 = t + x;

					if (t0 > t1)
					{
						float aux = t0;
						t0 = t1;
						t1 = aux;
					}


					if (t0 < 0.0)
						t0 = -1.0;

					if (t1 < 0.0)
						t1 = -1.0;
				}

				return float2(t0, t1);
			}

			//outputs a ro + the length of the ray, returns false if no intersection has been found
			bool GetRayAtmosphere(float3 camPos,float3 rd,out float3 rayOrigin, out float rayLength)
			{
				float3 planetO = float3(0.0, planetAtmos.x,0.0);
				float d = length(camPos - planetO);//distance btween camera and the planet center

				float2 innerAtmT = GetAtmosphereIntersection(camPos, rd, planetO, planetAtmos.y);
				float2 outerAtmT = GetAtmosphereIntersection(camPos, rd, planetO, planetAtmos.z);
				float2 groundT = GetAtmosphereIntersection(camPos, rd, planetO, -planetAtmos.x);

				if (d < planetAtmos.y)//if camera is under the atmosphere
				{
					rayOrigin = camPos + rd * innerAtmT.y;//second hit as 1st will always be behind camera in this case
					rayLength = outerAtmT.y - innerAtmT.y;//second hit as 1st will always be behind camera in this case

					if (max(groundT.x, groundT.y) != -1.0)
					{
						return false;
					}

					return true;
				}

				if (d < planetAtmos.z) //if camera is inside the atmosphere
				{
					float innerAtmIntersection = min(innerAtmT.x, innerAtmT.y);//only care about first intersection as we are outside the sphere
					rayOrigin = camPos;

					if (innerAtmIntersection != -1.0)
					{
						rayLength = innerAtmIntersection;
						return true;
					}

					rayLength = outerAtmT.y;//only care about 2nd intersection as 1s will always be behind camera

					return true;
				}

				//If camera is above atmosphere

				if (max(outerAtmT.x, outerAtmT.y) == -1.0)
					return false;//No hit!

				float innerAtmIntersection = min(innerAtmT.x, innerAtmT.y);//only care about first intersection as we are outside the sphere

				if (innerAtmIntersection != -1.0) //if there is an intersection with the innerAtm shell
				{
					rayOrigin = camPos + rd * outerAtmT.x;
					rayLength = innerAtmIntersection - outerAtmT.x;
					return true;
				}

				rayOrigin = camPos + rd * outerAtmT.x;
				rayLength = outerAtmT.y;
				return true;

			}


			float DensityGradient(float heightPercent, float4 parameters)
			{
				return saturate(Remap(heightPercent, parameters.x, parameters.y, 0.0, 1.0)) * saturate(Remap(heightPercent, parameters.z, parameters.w, 1.0, 0.0));
			}

			float ShapeAlteringSimple(float heightPercent,float weatherMapCloudType, float distance)
			{


				float4 stratus = float4(0.0, 0.1, 0.2, 0.3);
				float4 stratocumulus = float4(0.0, 0.2, 0.3, 0.6);
				float4 cumulus = float4(0.0, 0.2, 0.4, 0.9);

				/*
					float4 stratus = float4(0.0, 0.1, 0.2, 0.3);
				float4 stratocumulus = float4(0.0, 0.2, 0.4, 0.7);
				float4 cumulus = float4(0.0, 0.15, 0.7, 0.9);
				*/

				float cloudType = weatherMapCloudType;

				if (cumulusHorizon==true)
				{
					cloudType = max(weatherMapCloudType, saturate(Remap(distance, cumulusHorizonGradient.x, cumulusHorizonGradient.y,0.0,1.0)));
				}

				
				float ret = 1.0;

				if (cloudType < 0.5)//mix between stratus & stratocumulus
				{
					ret = lerp(DensityGradient(heightPercent, stratus), DensityGradient(heightPercent, stratocumulus), (cloudType*2.0));
				}
				else //mix between stratocumulus & cumulus
				{
					ret = lerp(DensityGradient(heightPercent, stratocumulus), DensityGradient(heightPercent, cumulus), (cloudType -0.5)*2.0);
				}



				return ret;
			}

			float ShapeAlteringAdvanced(float heightPercent)
			{
				//uint numStructs;
				//uint stride;
				//densityCurveBuffer.GetDimensions(numStructs, stride);

				uint maxN = 256;


				return  densityCurveBuffer[heightPercent * maxN];
			}

			float ShapeAltering(float heightPercent,float weatherMapCloudType,float distance) //Makes Clouds have more shape at the top & be more round towards the bottom, the weather map also influences the density
			{
				if (mode == 0)//Simple mode
				{
					return ShapeAlteringSimple(heightPercent, weatherMapCloudType, distance);
				}

				//Advanced mode
				return ShapeAlteringAdvanced(heightPercent);

			}

			//value between 0 & 1 showing where we are in the cloud layer
			float GetCloudLayerHeightPlane(float currentYPos, float cloudMin, float cloudMax)
			{
				return saturate((currentYPos - cloudMin) / (cloudMax - cloudMin));//Plane height

			}

			//value between 0 & 1 showing where we are in the cloud layer
			float GetCloudLayerHeightSphere(float3 currentPos)
			{
				float dFromCenter = length(currentPos - float3(0.0, planetAtmos.x, 0.0));

				return GetCloudLayerHeightPlane(dFromCenter,planetAtmos.y,planetAtmos.z);

			}

			float DensityAltering(float heightPercent,float weatherMapDensity)
			{
				float densityBottom = heightPercent * saturate(Remap(heightPercent, 0.0, 0.15, 0.0, 1.0));
				float densityTop = saturate(Remap(heightPercent,0.9,1.0,1.0,0.0));

				return densityBottom * densityTop  * weatherMapDensity * 2.0;
			}

			float GetDensity(float3 currPos)
			{
				float3 initialPos = currPos;
				float density = 0.0;
				float cloudHeightPercent = GetCloudLayerHeightSphere(currPos);//value between 0 & 1 showing where we are in the cloud 
				float fTime = _Time;
				float3 windOffset = -windDir * float3(fTime, fTime, fTime);//TODO make this & skewk consistent around the globe
				float3 skewPos = currPos -normalize(windDir)  * cloudHeightPercent * cloudHeightPercent * 100 * skewAmmount;
				float4 weatherMapCloud = weatherMapTexture.Sample(samplerweatherMapTexture, (skewPos.xz / weatherMapSize) + windOffset.xz* windDisplacesWeatherMap); //We sample the weather map (r coverage,g type)
				float4 lowFreqNoise = baseShapeTexture.Sample(samplerbaseShapeTexture, (currPos / baseShapeSize) + windOffset * baseShapeWindMult);
				float4 highFreqNoise = detailTexture.Sample(samplerdetailTexture, (currPos / detailSize) + windOffset * detailShapeWindMult);

				//Cloud Base shape
				float lowFreqFBM = (lowFreqNoise.g * 0.625) + (lowFreqNoise.b * 0.25) + (lowFreqNoise.a * 0.125);
				float cloudNoiseBase = (Remap(lowFreqNoise.r, lowFreqFBM-1.0, 1.0,0.0 , 1.0));
				cloudNoiseBase *= ShapeAltering(cloudHeightPercent, weatherMapCloud.g,length(initialPos.xz - _WorldSpaceCameraPos.xz));

				//Coverage
				float cloudCoverage = weatherMapCloud.r;
				float baseCloudWithCoverage = (Remap(cloudNoiseBase, 1.0- globalCoverage*cloudCoverage ,1.0,0.0,1.0));

				baseCloudWithCoverage *= DensityAltering(cloudHeightPercent, cloudCoverage);

				
				////Detail Shape
				float highFreqFBM = (highFreqNoise.r * 0.625) + (highFreqNoise.g * 0.25) + (highFreqNoise.b * 0.125);
				float detailNoise = lerp(highFreqFBM,1.0 - highFreqFBM,saturate(cloudHeightPercent * 5.0));
				detailNoise *= 0.35 * exp(-globalCoverage * 0.75);

				//Detail - Base Shape
				float finalCloud = saturate(Remap(baseCloudWithCoverage, detailNoise, 1.0,0.0, 1.0));

				density = finalCloud * globalDensity;



				return density;
			}

			float BeerLambertLaw(float accDensity, float absorptionCoefficient)
			{
				float ret = exp(-accDensity * absorptionCoefficient);
				return shadowBaseLight + ret * (1.0 - shadowBaseLight);
			}

			float PowderEffect(float accDensity, float absorptionCoefficient)
			{
				float ret = 1.0 - exp(-accDensity * absorptionCoefficient * 2.0);
				return shadowBaseLight + ret * (1.0 - shadowBaseLight);
			}


			float DensityTowardsLight(float3 currPosition)
			{
				int iter = 6;
				float accDensity = 0.0;
				float stepSize = (float(maxCloudHeight - minCloudHeight) * 0.5) / float(iter);//TODO make as variable (maybe when we have cone light samples?)
				float3 startingPos = currPosition;

				for (int currStep = 0; currStep < iter; ++currStep)
				{
					currPosition += -sunDir * stepSize;
					accDensity += GetDensity(currPosition) * stepSize;

				}

				accDensity += GetDensity(startingPos - sunDir * stepSize * float(iter) * 3.0) * stepSize;

				return accDensity;
			}

			float DensityTowardsLightCone(float3 currPosition,float coneWidthScale)//cone width scale between 0 & 1
			{
				int iter = 6;
				float accDensity = 0.0;
				float stepSize = (float(maxCloudHeight - minCloudHeight) * 0.5) / float(iter);//TODO make as variable (maybe when we have cone light samples?)
				float3 startingPos = currPosition;
				for (int currStep = 0; currStep < iter; ++currStep)
				{
					currPosition += -sunDir * stepSize + (stepSize * coneKernel[currStep].xyz * float(currStep) * coneWidthScale);
					accDensity += GetDensity(currPosition) * stepSize;
				}

				accDensity += GetDensity(startingPos - sunDir * stepSize * 6.0 + (-sunDir * stepSize * 6 * 3 * coneWidthScale)) * stepSize;

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

				float depth = osA * pow(density, Remap(hPercent, 0.3, 0.9, 0.5, 1.0));
				float vertical = pow(saturate(Remap(hPercent, 0.0, 0.3, 0.8, 1.0)),0.8);
				float outScatter = depth * vertical;

				return 1.0 - saturate(outScatter);
			}

			int CalculateStepsForRay(float3 rd)
			{
				float3 planetOrigin = float3(0.0,planetAtmos.x,0.0);
				float3 camPos = _WorldSpaceCameraPos;

				float3 planetNorm = normalize(camPos - planetOrigin);

				float d = abs(dot(planetNorm, rd));

				return lerp(128, 64, d);

			}

			float CalculateMaxRayDist(float rayLength)
			{
				return min(sqrt(planetAtmos.z * planetAtmos.z - planetAtmos.x * planetAtmos.x), rayLength);
			}

			bool IsPosVisible(float3 pos,float maxDepth,bool isMaxDepth)
			{
				if (isMaxDepth)
					return true;

				float3 distFromCamVec = (pos - _WorldSpaceCameraPos);
				float distFromCamSq = dot(distFromCamVec, distFromCamVec);
				return distFromCamSq <= maxDepth * maxDepth;
			}


			float DoubleLobeScattering(float cosAngle, float l1, float l2, float mix)
			{
				return lerp(HenyeyGreenstein(cosAngle, l1), HenyeyGreenstein(cosAngle, -l2), mix);
			}

			float LightShadowTransmittance(float3 pos, float sampleDistance)
			{
				int iter = 6;
				float stepSize = (sampleDistance / float(iter));//TODO make as variable (maybe when we have cone light samples?)

				float shadow = 1.0;
				float accDensity = 0.0;
				for (int currStep = 0; currStep < iter - 1; ++currStep)
				{
					float3 newPos = pos + currStep * stepSize * -sunDir;
					float density = GetDensity(newPos);

					accDensity += density;
					shadow *= exp(-density * stepSize * lightAbsorption);
				}

				shadow *= exp(-GetDensity(pos + sampleDistance * 5.0 * -sunDir) * stepSize * lightAbsorption);//long range sample

				return shadow;//TODO can lerp powder effect here (multiplying shadow) but might not be energy conserving!
			}

			float3 Raymarching(float3 col,bool isAtmosRay,float3 ro, float3 rd,float maxRayLength,float2 uv,float maxDepth,bool isMaxDepth) //where ro is ray origin & rd is ray direction
			{

				uint blueNoiseW;
				uint blueNoiseH;
				blueNoiseTexture.GetDimensions(blueNoiseW, blueNoiseH);

				const int maxStepsRay = CalculateStepsForRay(rd);
				float stepLength = CalculateMaxRayDist(maxRayLength) / maxStepsRay;
				uv.x *= (_ScreenParams.x / _ScreenParams.y);
				uv *= min(_ScreenParams.x, _ScreenParams.y) / blueNoiseW;



				float3 startingPos = ro + rd * stepLength * (blueNoiseTexture.Sample(samplerblueNoiseTexture, uv) - 0.5) * 2.0;
				float3 currPos = startingPos;
				float cosAngle = dot(-rd,sunDir);//We assume they are normalized

				float3 scatteredLuminance = float3(0.0, 0.0, 0.0);
				float scatteredtransmittance = 1.0;
				float3 atmosphereHazePos = startingPos;
				[loop] for (int currStep = 0; currStep < maxStepsRay; ++currStep)
				{
					if (isAtmosRay && IsPosVisible(currPos, maxDepth,isMaxDepth && scatteredtransmittance > 0.01))//TODO why cant atmos ray be out of here?
					{
						float currDensity = GetDensity(currPos);

						if (scatteredtransmittance >= 0.99)
							atmosphereHazePos = currPos;

						if (currDensity > 0.0)
						{
							float shadow = LightShadowTransmittance(currPos, 2000.0f);
							float transmittance = exp(-currDensity * stepLength);

							float clampedExtinction = max(currDensity, 0.0000001);


							float3 luminance = lightColor * lightIntensity * shadow * currDensity * DoubleLobeScattering(cosAngle, 0.3, 0.2, 0.4);
							float3 integScatt = (luminance - luminance * transmittance) / clampedExtinction;
							scatteredLuminance += scatteredtransmittance * integScatt;

							scatteredtransmittance *= transmittance;
						}
				
					}
					currPos += rd * stepLength;
				}

				float minHazeDist = 18000.0f;//TODO consider making this public
				float maxHazeDist = sqrt(planetAtmos.z*planetAtmos.z -(planetAtmos.x*planetAtmos.x));//Horizon max view

				float3 initPosHaze = _WorldSpaceCameraPos;
				float3 vecFromPlanetCenter = _WorldSpaceCameraPos - float3(0.0, planetAtmos.x, 0.0);
				
				if (length(vecFromPlanetCenter) > planetAtmos.z )//if positon is outside the atmosphere
				{
					initPosHaze = startingPos;
				}


				float hazeAmmount = saturate(Remap(length(atmosphereHazePos - initPosHaze), minHazeDist, maxHazeDist, 0.0, 1.0));

				col = lerp(scatteredtransmittance * col + scatteredLuminance,col,1.0-(1.0-hazeAmmount)*(1.0-hazeAmmount));
				//col = scatteredtransmittance * col + scatteredLuminance;
				return float3(col);
			}



			fixed4 frag(v2f i) : SV_Target
			{
				fixed3 col = tex2D(_MainTex, i.uv);
			float depth = tex2D(_CameraDepthTexture,i.uv);
			float linearDepth = Linear01Depth(depth);//depth 0,1
			float depthMeters = _ProjectionParams.z* linearDepth;
			float4 posView = float4(i.ray * depthMeters, 1.0);
			float3 posWorld = mul(unity_CameraToWorld, posView).xyz;
			depthMeters = length(posWorld - _WorldSpaceCameraPos);//depth in meters
			
			float3 rayDirection = normalize(i.ray.xyz);
			float3 rayOrigin = _WorldSpaceCameraPos;
			float t = 0.0;

			
			bool isAtmosRay = GetRayAtmosphere(_WorldSpaceCameraPos, rayDirection, rayOrigin, t);

			float3 result = Raymarching(col,isAtmosRay,rayOrigin, rayDirection,t,i.uv, depthMeters,linearDepth>=1.0);
			return fixed4(result,1.0); 

			}
ENDCG
		}
	}
}
