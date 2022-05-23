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

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 ray : TEXCOORD1;
			};

			v2f vert(appdata v)
			{
				v2f o;

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;

				float3 ray = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
				o.ray = mul(unity_CameraToWorld, float4(ray, 0));

				return o;
			}

			float Remap(float v, float minOrigin, float maxOrigin, float minTarget, float maxTarget)
			{
				return minTarget + (((v - minOrigin) / (maxOrigin - minOrigin)) * (maxTarget - minTarget));
			}

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

			float3 sunDir;
			float extintionC;
			float absorptionC;
			float scatterC;

			float lightIntensity;
			float3 lightColor;
			float ambientLightIntensity;
			float3 ambientColors[2];
			float4 coneKernel[6];
			bool softerShadows;
			float shadowSize;

			StructuredBuffer<float> densityCurveBuffer1;
			int densityCurveBufferSize1;
			float densityCurveMultiplier1;
			StructuredBuffer<float> densityCurveBuffer2;
			int densityCurveBufferSize2;
			float densityCurveMultiplier2;
			StructuredBuffer<float> densityCurveBuffer3;
			int densityCurveBufferSize3;
			float densityCurveMultiplier3;

			int mode;

			float3 planetAtmos;
			float3 windDir;
			float baseShapeWindMult;
			float detailShapeWindMult;
			float skewAmmount;

			bool cumulusHorizon;
			bool windDisplacesWeatherMap;
			float2 cumulusHorizonGradient;
			float4 cloudLayerGradient1;
			float4 cloudLayerGradient2;
			float4 cloudLayerGradient3;

			sampler2D _CameraDepthTexture;

			int maxRayVisibilityDist;

			bool transitioningWM;
			float transitionLerpT;
			Texture2D<float4> weatherMapTextureNew;
			SamplerState samplerweatherMapTextureNew;

			struct AtmosIntersection
			{
				float3 r1o;//origin
				float r1m;//magnitude

				bool hasRay2;

				float3 r2o;//origin
				float r2m;//magnitude
			};


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
			bool GetRayAtmosphere(float3 ro,float3 rd, out AtmosIntersection intersection)
			{
				intersection.hasRay2 = false;

				float3 planetO = float3(0.0, planetAtmos.x,0.0);
				float d = length(ro - planetO);//distance btween camera and the planet center

				float2 innerAtmT = GetAtmosphereIntersection(ro, rd, planetO, planetAtmos.y);
				float2 outerAtmT = GetAtmosphereIntersection(ro, rd, planetO, planetAtmos.z);
				float2 groundT = GetAtmosphereIntersection(ro, rd, planetO, -planetAtmos.x);

				if (d < planetAtmos.y)//if camera is under the atmosphere
				{
					if (max(groundT.x, groundT.y) != -1.0) //if collision is with ground return false
					{
						return false;
					}

					intersection.r1o = ro + rd* innerAtmT.y;//second hit as 1st will always be behind camera in this case
					intersection.r1m = outerAtmT.y - innerAtmT.y;//second hit as 1st will always be behind camera in this case

					return true;
				}

				if (d < planetAtmos.z) //if camera is inside the atmosphere
				{
					intersection.r1o = ro;

					if (innerAtmT.x > 0.0)
					{
						intersection.r1m = innerAtmT.x;

						if (groundT.x < 0.0) //If no ground collision, secondary ray
						{
							intersection.hasRay2 = true;
							intersection.r2o = ro + rd * innerAtmT.y;
							intersection.r2m = outerAtmT.y - innerAtmT.y;
						}


						return true;
					}

					intersection.r1m = outerAtmT.y;




					//if (max(groundT.x, groundT.y) != -1.0)
					//{
					//	rayLength = min(innerAtmT.x, innerAtmT.y);
					//	return true;
					//}

					//rayLength = outerAtmT.y;//only care about 2nd intersection as 1s will always be behind camera

					return true;
				}

				//If camera is above atmosphere

				if (outerAtmT.x < 0.0)
					return false;//No hit or behind camera!

				intersection.r1o = ro + rd * outerAtmT.x;

				if (innerAtmT.x > 0.0)
				{

					intersection.r1m = innerAtmT.x - outerAtmT.x;


					if (groundT.x < 0.0) //If no ground collision, secondary ray
					{
						intersection.hasRay2 = true;
						intersection.r2o = ro + rd * innerAtmT.y;
						intersection.r2m = outerAtmT.y - innerAtmT.y;
					}

					return true;
				}

				intersection.r1m = outerAtmT.y - outerAtmT.x;

				//if (max(groundT.x,groundT.y) != -1.0) //if there is an intersection with the innerAtm shell
				//{
				//	rayLength = innerAtmT.x - outerAtmT.x;
				//	return true;
				//}

				//rayLength = outerAtmT.y - outerAtmT.x;
				return true;
			}

			float DensityGradient(float heightPercent, float4 parameters)
			{
				return saturate(Remap(heightPercent, parameters.x, parameters.y, 0.0, 1.0)) * saturate(Remap(heightPercent, parameters.z, parameters.w, 1.0, 0.0));
			}

			float3 ShapeAlteringSimple(float heightPercent)
			{
				return float3(DensityGradient(heightPercent, cloudLayerGradient1),
					DensityGradient(heightPercent, cloudLayerGradient2),
					DensityGradient(heightPercent, cloudLayerGradient3));
			}

			float3 ShapeAlteringAdvanced(float heightPercent)//TODO adapt with more than 1 layer
			{
				return float3(densityCurveBuffer1[heightPercent * densityCurveBufferSize1] * densityCurveMultiplier1,
					densityCurveBuffer2[heightPercent * densityCurveBufferSize2] * densityCurveMultiplier2,
					densityCurveBuffer3[heightPercent * densityCurveBufferSize3] * densityCurveMultiplier3);
			}

			float3 ShapeAltering(float heightPercent) //Makes Clouds have more shape at the top & be more round towards the bottom, the weather map also influences the density
			{
				if (mode == 0)//Simple mode
				{
					return ShapeAlteringSimple(heightPercent);
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
				float densityBottom = saturate(Remap(heightPercent, 0.0, 0.1, 0.0, 1.0));
				float densityTop = saturate(Remap(heightPercent,0.9,1.0,1.0,0.0));

				return (Remap(heightPercent,0.0,1.0,0.25,1.0)) * densityBottom * densityTop * weatherMapDensity * globalCoverage * 2.0;
			}

			float GetDensity(float3 currPos,float sampleLvl,bool onlyBase)
			{
				//sample lvl incremented with dist
				sampleLvl += min(length(currPos - _WorldSpaceCameraPos) / 200000.0,4);//TODO expose these variables?
				float3 initialPos = currPos;
				float density = 0.0;
				float cloudHeightPercent = GetCloudLayerHeightSphere(currPos);//value between 0 & 1 showing where we are in the cloud
				float fTime = _Time;
				float3 windOffset = -windDir * float3(fTime, fTime, fTime);//TODO make this & skewk consistent around the globe
				float3 skewPos = currPos - normalize(windDir) * cloudHeightPercent * cloudHeightPercent * 100 * skewAmmount;
				float4 weatherMapCloud = weatherMapTexture.SampleLevel(samplerweatherMapTexture, (skewPos.xz / weatherMapSize) + windOffset.xz * windDisplacesWeatherMap,sampleLvl); //We sample the weather map (r coverage,g type)
				if (transitioningWM == true)
					weatherMapCloud = lerp(weatherMapCloud, weatherMapTextureNew.SampleLevel(samplerweatherMapTextureNew, (skewPos.xz / weatherMapSize) + windOffset.xz * windDisplacesWeatherMap, sampleLvl), transitionLerpT);


				//Cloud Base shape
				float4 lowFreqNoise = baseShapeTexture.SampleLevel(samplerbaseShapeTexture, (currPos / baseShapeSize) + windOffset * baseShapeWindMult, sampleLvl);
				float lowFreqFBM = (lowFreqNoise.g * 0.625) + (lowFreqNoise.b * 0.25) + (lowFreqNoise.a * 0.125);
				float cloudNoiseBase = (Remap(lowFreqNoise.r, lowFreqFBM - 1.0, 1.0,0.0 , 1.0));

				float3 shapeAltering = ShapeAltering(cloudHeightPercent);
				//Coverage
				if (cumulusHorizon == true) //cumulonimbus towards horizon
				{
					weatherMapCloud.b = max(weatherMapCloud.b, saturate(Remap(length(initialPos.xz - _WorldSpaceCameraPos.xz), cumulusHorizonGradient.x, cumulusHorizonGradient.y, 0.0, 1.0)));
				}

				
				float baseCloudWithCoverageA = (Remap(cloudNoiseBase * shapeAltering.x, 1.0 - weatherMapCloud.r, 1.0, 0.0, 1.0));
				float baseCloudWithCoverageB = (Remap(cloudNoiseBase * shapeAltering.y, 1.0 - weatherMapCloud.g, 1.0, 0.0, 1.0));
				float baseCloudWithCoverageC = (Remap(cloudNoiseBase * shapeAltering.z, 1.0 - weatherMapCloud.b, 1.0, 0.0, 1.0));

				baseCloudWithCoverageA *= DensityAltering(cloudHeightPercent, weatherMapCloud.r);
				baseCloudWithCoverageB *= DensityAltering(cloudHeightPercent, weatherMapCloud.g);
				baseCloudWithCoverageC *= DensityAltering(cloudHeightPercent, weatherMapCloud.b);

				float baseCloudWithCoverage = max(max(baseCloudWithCoverageA, baseCloudWithCoverageB) ,baseCloudWithCoverageC);

				float finalCloud = baseCloudWithCoverage;
				
				if (!onlyBase)
				{
					////Detail Shape
					float4 highFreqNoise = detailTexture.SampleLevel(samplerdetailTexture, (currPos / detailSize) + windOffset * detailShapeWindMult, sampleLvl);
					float highFreqFBM = (highFreqNoise.r * 0.625) + (highFreqNoise.g * 0.25) + (highFreqNoise.b * 0.125);
					float detailNoise = (lerp(highFreqFBM,1.0 - highFreqFBM,saturate(cloudHeightPercent * 10.0)));
					detailNoise *= Remap(saturate(globalCoverage), 0.0, 1.0, 0.1, 0.3);

					//Detail - Base Shape
					finalCloud = saturate(Remap(baseCloudWithCoverage, detailNoise, 1.0,0.0, 1.0));
				}

				density = finalCloud * globalDensity;

				return density;
			}

			float HenyeyGreenstein(float cosAngle, float g) //G ranges between -1 & 1
			{
				float g2 = g * g;
				return ((1.0 - g2) / pow(1.0 + g2 - 2.0 * g * cosAngle, 1.5)) / (4 * 3.1415);
			}

			int CalculateStepsForRay(float3 ro,float3 rd,float t)
			{
				float3 planetOrigin = float3(0.0,planetAtmos.x,0.0);

				float3 planetNorm = normalize(ro + rd*t*0.5 - planetOrigin);

				float d = abs(dot(planetNorm, rd));

				return lerp(128, 64, d);
			}

			float CalculateMaxRayDist(float rayLength)
			{
				return min(maxRayVisibilityDist, rayLength);
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

			float LightShadowTransmittance(float3 pos, float initialStepSize,float eCoeff)
			{
				int iter = 4;

				float shadow = 1.0;
				for (int currStep = 0; currStep < iter; ++currStep)
				{
					pos += currStep * initialStepSize * -sunDir;
					float density = GetDensity(pos, (float(currStep) / float(iter)) * 2.0,false);

					shadow *= exp(-density * currStep * initialStepSize * eCoeff);
				}

				return shadow;//TODO can lerp powder effect here (multiplying shadow) but might not be energy conserving!
			}

			float LightShadowTransmittanceCone(float3 pos, float initialStepSize, float eCoeff)
			{
				int iter = 4;

				float shadow = 1.0;
				for (int currStep = 0; currStep < iter; ++currStep)
				{
					float3 newPos = initialStepSize * -sunDir + (coneKernel[currStep].xyz * currStep);
					pos += newPos; 
					float density = GetDensity(pos, (float(currStep) / float(iter)) * 2.0,false);

					shadow *= exp(-density * length(newPos) * eCoeff);
				}

				return shadow;//TODO can lerp powder effect here (multiplying shadow) but might not be energy conserving!

			}

			float3 LightScatter(float3 currPos, float cosAngle,int i)
			{
				//must be a<=b to be energy conserving
				float a = 0.25;
				float b = 0.75;
				float c = 0.5;
				float newExtinctionC = extintionC * pow(a,i);
				float newScatterC = scatterC * pow(b,i);

				float3 ambientSky = ambientColors[0].xyz * ambientLightIntensity;
				float3 ambientFloor = ambientColors[1].xyz * ambientLightIntensity * 0.5;
				float heightPercent = GetCloudLayerHeightSphere(currPos);
				float t = saturate(Remap(heightPercent, 0.0, 1.0, 0.15, 1.0));

				float3 l = lightColor * lightIntensity;

				float shadow = 1.0;
				if (softerShadows == true)
				{
					shadow = LightShadowTransmittanceCone(currPos, shadowSize, newExtinctionC);
				}
				else 
				{
					shadow = LightShadowTransmittance(currPos, shadowSize, newExtinctionC);
				}

				return l * shadow * DoubleLobeScattering(cosAngle * pow(c,i), 0.3, 0.15, 0.5) * newScatterC + ambientSky* t *shadow* (1.0/4.0*3.1415) * newScatterC;
			}


			void RaymarchThroughAtmos(float3 pos,float3 rd, int maxSteps,
				float stepLengthBase,float maxDepth,float cosAngle,bool isMaxDepth,
				inout float scatTransmittance, inout float3 scatLuminance,out float3 atmosphereHazePos)
			{

				for (int t = 0; t < maxSteps; ++t)
				{
					if (IsPosVisible(pos, maxDepth, isMaxDepth))//Checks if an object is occluding the raymarch
					{
						float currDensity = GetDensity(pos, 0.0, false);

						if (scatTransmittance >= 0.9)
							atmosphereHazePos = pos;

						if (currDensity > 0.0)
						{
							float extinction = currDensity * extintionC;
							float clampedExtinction = max(extinction, 0.0000001);
							float transmittance = exp(-clampedExtinction * stepLengthBase);

							float3 luminance = float3(0.0, 0.0, 0.0);
							for (int i = 0; i < 2; ++i)
							{
								luminance += LightScatter(pos, cosAngle, i);
							}
							luminance *= currDensity;

							float3 integScatt = (luminance - luminance * transmittance) / clampedExtinction;
							scatLuminance += scatTransmittance * integScatt;

							scatTransmittance *= transmittance;
						}


					}
					pos += rd * stepLengthBase;
				}
			}


			float3 Raymarching(float3 col,float3 rd, AtmosIntersection atmosIntersection,float2 uv,float maxDepth,bool isMaxDepth)
			{
				uint blueNoiseW;
				uint blueNoiseH;
				blueNoiseTexture.GetDimensions(blueNoiseW, blueNoiseH);
				uv.x *= (_ScreenParams.x / _ScreenParams.y);
				uv *= min(_ScreenParams.x, _ScreenParams.y) / blueNoiseW;
				float blueNoiseOffset = blueNoiseTexture.Sample(samplerblueNoiseTexture, uv);
				float cosAngle = dot(-rd,sunDir);//We assume they are normalized


				int maxStepsRay = CalculateStepsForRay(atmosIntersection.r1o,rd, atmosIntersection.r1m);
				float stepLength = CalculateMaxRayDist(atmosIntersection.r1m) / float(maxStepsRay);

				float3 startingPos = atmosIntersection.r1o + rd * stepLength * blueNoiseOffset;
				float scatteredtransmittance = 1.0;
				float3 scatteredLuminance = float3(0.0, 0.0, 0.0);

				float3 atmosphereHazePos;

				RaymarchThroughAtmos(startingPos, rd, maxStepsRay, stepLength,maxDepth,cosAngle,isMaxDepth, scatteredtransmittance, scatteredLuminance, atmosphereHazePos);

				//[loop] for (int currStep = 0; currStep < maxStepsRay; ++currStep)
				//{
				//	if (IsPosVisible(currPos, maxDepth,isMaxDepth))//TODO why cant atmos ray be out of here?
				//	{
				//		float currDensity = GetDensity(currPos,0.0,false);

				//		if (scatteredtransmittance >= 0.9)
				//			atmosphereHazePos = currPos;

				//		if (currDensity > 0.0)
				//		{
				//			float extinction = currDensity * extintionC;
				//			float clampedExtinction = max(extinction, 0.0000001);
				//			float transmittance = exp(-clampedExtinction * stepLength);

				//			float3 luminance = float3(0.0,0.0,0.0);
				//			for (int i = 0; i < 2; ++i)
				//			{
				//				luminance += LightScatter(currPos, cosAngle,i);
				//			}
				//			luminance *= currDensity;

				//			float3 integScatt = (luminance - luminance * transmittance) / clampedExtinction;
				//			scatteredLuminance += scatteredtransmittance * integScatt;

				//			scatteredtransmittance *= transmittance;
				//		}
				//	}
				//	currPos += rd * stepLength;
				//}

				float minHazeDist = maxRayVisibilityDist * 0.5;//TODO consider making this public
				float maxHazeDist = maxRayVisibilityDist;//Horizon max view

				float3 initPosHaze = _WorldSpaceCameraPos;
				float3 vecFromPlanetCenter = _WorldSpaceCameraPos - float3(0.0, planetAtmos.x, 0.0);

				if (length(vecFromPlanetCenter) > planetAtmos.z)//if positon is outside the atmosphere
				{
					initPosHaze = startingPos;
				}

				float hazeAmmount = saturate(Remap(length(atmosphereHazePos - initPosHaze), minHazeDist, maxHazeDist, 0.0, 1.0));

				col = lerp(scatteredtransmittance * col + scatteredLuminance,col,1.0 - (1.0 - hazeAmmount) * (1.0 - hazeAmmount));
				return float3(col);
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float3 rayOrigin = _WorldSpaceCameraPos;
				float viewLength = length(i.ray);
				float3 rayDirection = i.ray / viewLength;

				fixed3 col = tex2D(_MainTex, i.uv);
				float depth = tex2D(_CameraDepthTexture,i.uv);
				float linearDepth = Linear01Depth(depth);//depth 0,1
				float depthMeters = _ProjectionParams.z * linearDepth;
				float4 posView = float4(i.ray * depthMeters, 1.0);
				float3 posWorld = mul(unity_CameraToWorld, posView).xyz;
				depthMeters = length(posWorld - _WorldSpaceCameraPos);//depth in meters

				float t = 0.0;
				AtmosIntersection atmosIntersection;
				bool isAtmosRay = GetRayAtmosphere(rayOrigin, rayDirection, atmosIntersection);

				if (!isAtmosRay || atmosIntersection.r1m <= 0.0)
				{
					return fixed4(col, 1.0);
				}
				else 
				{
					float3 result = Raymarching(col,rayDirection, atmosIntersection,i.uv, depthMeters,linearDepth >= 1.0);
					return fixed4(result,1.0);
				}
			}
	ENDCG
			}
	}
}