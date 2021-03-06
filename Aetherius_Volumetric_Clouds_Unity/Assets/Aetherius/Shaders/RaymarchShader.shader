Shader "Aetherius/RaymarchShader"
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

			float2 texDimensions;



			Texture3D<float4> baseShapeTexture;
			Texture3D<float3> detailTexture; 
			Texture2D<float3> weatherMapTexture;
			Texture2D<float3> weatherMapTextureNew;
			Texture2D<float> blueNoiseTexture;
			SamplerState samplerbaseShapeTexture;
			SamplerState samplerdetailTexture;
			SamplerState samplerweatherMapTexture;
			SamplerState samplerblueNoiseTexture;

			float baseShapeSize;
			float detailSize;
			float weatherMapSize;

			float globalCoverage;
			float globalDensity;

			float3 sunDir;
			float2 coefficients;//x extintion, y scatter //We do not use absorption in the shader (onloy used to calculate extinction/scatter relation)

			float3 lightColor;
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

			float maxRayPossibleDist;
			float maxRayPossibleGroundDist;
			float2 dynamicRaymarchParameters;

			float hazeMinDist;
			float hazeMaxDist;

			float transitionLerpT;


			int lightIterations;

			static const float3 fbmMultipliers = { 0.625,0.25,0.125 };
			static const int iter = 4;

			StructuredBuffer<float3> lightOctaveParameters;


			struct AtmosIntersection
			{
				float4 intersectionsT; //Origin ray1, end ray 1, origin ray 2, end ray 2
				bool startsInAtmos;
				bool hasRay2;

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
						const float aux = t0;
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

			//outputs the length of the ray, returns false if no intersection has been found
			bool GetRayAtmosphere(float3 rd, out AtmosIntersection intersection)
			{
				intersection.startsInAtmos = true;
				intersection.intersectionsT = float4(0.0,0.0,0.0,0.0);
				intersection.hasRay2 = false;

				float3 planetO = { 0.0, -planetAtmos.x,0.0 };
				float d = length(_WorldSpaceCameraPos - planetO);//distance btween camera and the planet center

				float2 innerAtmT = GetAtmosphereIntersection(_WorldSpaceCameraPos, rd, planetO, planetAtmos.y);
				float2 outerAtmT = GetAtmosphereIntersection(_WorldSpaceCameraPos, rd, planetO, planetAtmos.z);
				float2 groundT = GetAtmosphereIntersection(_WorldSpaceCameraPos, rd, planetO, planetAtmos.x);

				if (d < planetAtmos.y)//if camera is under the atmosphere
				{


					if (groundT.y < 0.0) //when no ground collision, secondary ray
					{
						intersection.intersectionsT.x = innerAtmT.y;//second hit as 1st will always be behind camera in this case
						intersection.intersectionsT.y = outerAtmT.y;//second hit as 1st will always be behind camera in this case

						return true;

					}
					else //if collision is with ground return false
					{
						return false;
					}


				}
				else if (d < planetAtmos.z) //if camera is inside the atmosphere
				{

					if (innerAtmT.x <= 0.0)
					{
						intersection.intersectionsT.y = outerAtmT.y;
					}
					else
					{
						intersection.intersectionsT.y = innerAtmT.x;

						if (groundT.y < 0.0) //If no ground collision, secondary ray
						{
							intersection.hasRay2 = true;
							intersection.intersectionsT.z = innerAtmT.y;
							intersection.intersectionsT.w = outerAtmT.y;
						}

					}

					return true;

				}
				else if (outerAtmT.x < 0.0)//If camera is above atmosphere
				{
					intersection.startsInAtmos = false;
					return false;//No hit or behind camera!
				}
				else
				{
					intersection.startsInAtmos = false;

					intersection.intersectionsT.x = outerAtmT.x;

					if (innerAtmT.x > 0.0)
					{
						intersection.intersectionsT.y = innerAtmT.x;

						if (groundT.x < 0.0) //If no ground collision, secondary ray
						{
							intersection.hasRay2 = true;
							intersection.intersectionsT.z = innerAtmT.y;
							intersection.intersectionsT.w = outerAtmT.y;
						}
					}
					else
					{
						intersection.intersectionsT.y = outerAtmT.y;
					}

					return true;
				}

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



			float3 ShapeAlteringAdvanced(float heightPercent)
			{
				float3 indexF = float3(densityCurveBufferSize1, densityCurveBufferSize2, densityCurveBufferSize3)* heightPercent;
				int3 indexI = trunc(indexF);

				float3 t = frac(indexF);



				return float3(lerp(densityCurveBuffer1[indexI.x], densityCurveBuffer1[indexI.x+1],t.x) * densityCurveMultiplier1,
					lerp(densityCurveBuffer2[indexI.y], densityCurveBuffer2[indexI.y + 1], t.y) * densityCurveMultiplier2,
					lerp(densityCurveBuffer3[indexI.z], densityCurveBuffer3[indexI.z + 1], t.z) * densityCurveMultiplier3);
			}

			float3 ShapeAltering(float heightPercent) //Makes Clouds have more shape at the top & be more round towards the bottom, the weather map also influences the density
			{
				if (mode == 0)//Simple mode
				{
					return ShapeAlteringSimple(heightPercent);
				}
				else
				{
					//Advanced mode
					return ShapeAlteringAdvanced(heightPercent);
				}

			}

			//value between 0 & 1 showing where we are in the cloud layer
			float GetCloudLayerHeightPlane(float currentYPos, float cloudMin, float cloudMax)
			{
				return saturate((currentYPos - cloudMin) / (cloudMax - cloudMin));//Plane height
			}

			//value between 0 & 1 showing where we are in the cloud layer
			float GetCloudLayerHeightSphere(float3 currentPos)
			{
				float dFromCenter = length(currentPos - float3(0.0, -planetAtmos.x, 0.0));

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
				float3 initialPos = currPos;
				float density = 0.0;
				float cloudHeightPercent = GetCloudLayerHeightSphere(currPos);//value between 0 & 1 showing where we are in the cloud
				float fTime = _Time;
				float3 windOffset = -windDir * float3(fTime, fTime, fTime);//TODO make this & skewk consistent around the globe
				float3 skewPos = currPos - normalize(windDir) * cloudHeightPercent * cloudHeightPercent * 100 * skewAmmount;

				float2 wmUVs = (skewPos.xz / weatherMapSize) + windOffset.xz * windDisplacesWeatherMap;
				float3 weatherMapCloudOriginal = weatherMapTexture.SampleLevel(samplerweatherMapTexture, wmUVs, sampleLvl);//We sample the weather map (r coverage,g type)
				float3 weatherMapCloudNew = weatherMapTextureNew.SampleLevel(samplerweatherMapTexture, wmUVs, sampleLvl);
				float3 weatherMapCloud = lerp(weatherMapCloudOriginal, weatherMapCloudNew,saturate(transitionLerpT));




				//Cloud Base shape
				float4 lowFreqNoise = baseShapeTexture.SampleLevel(samplerbaseShapeTexture, (currPos / baseShapeSize) + windOffset * baseShapeWindMult, sampleLvl);
				float lowFreqFBM = dot(lowFreqNoise.gba, fbmMultipliers);

				float3 shapeAltering = ShapeAltering(cloudHeightPercent) * (Remap(lowFreqNoise.r, lowFreqFBM - 1.0, 1.0, 0.0, 1.0)); //shape altering * cloud noise base
				//Coverage
				if (cumulusHorizon == true) //cumulonimbus towards horizon
				{
					weatherMapCloud.b = max(weatherMapCloud.b, saturate(Remap(length(initialPos.xz - _WorldSpaceCameraPos.xz), cumulusHorizonGradient.x, cumulusHorizonGradient.y, 0.0, 1.0)));
				}

				float baseCloudWithCoverageA = (Remap(shapeAltering.x, 1.0 - weatherMapCloud.r, 1.0, 0.0, 1.0));
				float baseCloudWithCoverageB = (Remap(shapeAltering.y, 1.0 - weatherMapCloud.g, 1.0, 0.0, 1.0));
				float baseCloudWithCoverageC = (Remap(shapeAltering.z, 1.0 - weatherMapCloud.b, 1.0, 0.0, 1.0));

				baseCloudWithCoverageA *= DensityAltering(cloudHeightPercent, weatherMapCloud.r);
				baseCloudWithCoverageB *= DensityAltering(cloudHeightPercent, weatherMapCloud.g);
				baseCloudWithCoverageC *= DensityAltering(cloudHeightPercent, weatherMapCloud.b);

				float baseCloudWithCoverage = max(max(baseCloudWithCoverageA, baseCloudWithCoverageB) ,baseCloudWithCoverageC);

				float finalCloud = baseCloudWithCoverage;

				if (!onlyBase)
				{
					////Detail Shape
					float3 highFreqNoise = detailTexture.SampleLevel(samplerdetailTexture, (currPos / detailSize) + windOffset * detailShapeWindMult, sampleLvl);
					float highFreqFBM = dot(highFreqNoise,fbmMultipliers); //ax*bx+ay*by+az*bz
					float detailNoise = (lerp(highFreqFBM,1.0 - highFreqFBM,saturate(cloudHeightPercent * 10.0))) *
						Remap(saturate(globalCoverage), 0.0, 1.0, 0.1, 0.3);

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

			bool IsPosVisible(float3 pos,float maxDepth,bool isMaxDepth)
			{
				if (!isMaxDepth)
				{
					float3 distFromCamVec = (pos - _WorldSpaceCameraPos);
					float distFromCamSq = dot(distFromCamVec, distFromCamVec);
					return distFromCamSq <= maxDepth * maxDepth;
				}
				else
				{
					return true;
				}
			}

			float DoubleLobeScattering(float cosAngle, float l1, float l2, float mix)
			{
				return lerp(HenyeyGreenstein(cosAngle, l1), HenyeyGreenstein(cosAngle, -l2), mix);
			}

			float LightShadowTransmittance(float3 pos, float initialStepSize,float eCoeff,float initialSampleLvl)
			{
				float shadow = 1.0;
				for (int currStep = 0; currStep < iter; ++currStep)
				{
					pos += currStep * initialStepSize * -sunDir;
					float density = GetDensity(pos, initialSampleLvl + (float(currStep) / float(iter)) * 2.0,false);

					shadow *= exp(-density * currStep * initialStepSize * eCoeff);
				}

				return shadow;
			}

			float LightShadowTransmittanceCone(float3 pos, float initialStepSize, float eCoeff, float initialSampleLvl)
			{

				float shadow = 1.0;
				for (int currStep = 0; currStep < iter; ++currStep)
				{
					float3 newPos = initialStepSize * -sunDir + (coneKernel[currStep].xyz * currStep);
					pos += newPos;
					float density = GetDensity(pos, initialSampleLvl +(float(currStep) / float(iter)) * 2.0,false);

					shadow *= exp(-density * length(newPos) * eCoeff);
				}

				return shadow;
			}

			float3 LightScatter(float3 currPos, float cosAngle,int i, float initialSampleLvl)
			{
				//must be a<=b to be energy conserving

				float3 newCoefficients = float3(coefficients.xy, 1.0) * lightOctaveParameters[i];//extinction, scatter, scattering Multiplier


				float3 ambientSky = ambientColors[0].xyz;
				float3 ambientSun = ambientColors[1].xyz;

				float heightPercent = GetCloudLayerHeightSphere(currPos);
				float t = saturate(Remap(heightPercent, 0.0, 1.0, 0.15, 1.0));

				float shadow = 1.0;
				if (softerShadows == true)
				{
					shadow = LightShadowTransmittanceCone(currPos, shadowSize, newCoefficients.x, initialSampleLvl);
				}
				else
				{
					shadow = LightShadowTransmittance(currPos, shadowSize, newCoefficients.x, initialSampleLvl);
				}

				return newCoefficients.y * (lightColor * shadow * DoubleLobeScattering(cosAngle , 0.3 * newCoefficients.z, 0.15 * newCoefficients.z, 0.5) + (ambientSun * 0.5 + ambientSky * 0.5) * t * shadow * (1.0 / 4.0 * 3.1415));
			}

			void RaymarchThroughAtmos(float blueNoiseOffset,float3 rd, float tInit,float tMax,
				float maxDepth, bool isInsideAtmos, float cosAngle,bool isMaxDepth,
				inout bool atmosphereHazeAssigned,
				inout float scatTransmittance, inout float3 scatLuminance, inout float atmosphereHazeT)
			{
				bool isBaseStep = true;//Base step or full step
				int samplesWithZeroDensity = 0;

				float currentT = tInit;
				float previousT = currentT;

				bool finished = false;
				
				
				const float startExpDist = (planetAtmos.z-planetAtmos.y);
				const float startExpDistInit = (tInit + startExpDist);
				float stepLength = dynamicRaymarchParameters.x;
				
				while (currentT <= tMax && finished == false)
				{
					if (isInsideAtmos == true)
					{
						if (currentT >= startExpDist)
						{
							float distFromExpStart = (currentT - startExpDist);
							float distancePercentageFromStart = (distFromExpStart / (maxRayPossibleGroundDist - startExpDist));
							float t = saturate(distancePercentageFromStart * distancePercentageFromStart * 15);
							stepLength = lerp(dynamicRaymarchParameters.x, dynamicRaymarchParameters.y, t);
						}
					}
					else
					{


						if (currentT >= tInit)
						{
							float distFromExpStart = (currentT - tInit);
							float distancePercentageFromStart = (distFromExpStart / (maxRayPossibleGroundDist));
							float t = saturate(distancePercentageFromStart * distancePercentageFromStart * 15);
							stepLength = lerp(dynamicRaymarchParameters.x, dynamicRaymarchParameters.y, t);
						}
					}
					


					float detailedStepLength = stepLength * 0.20;


					float3 currPos = _WorldSpaceCameraPos + rd * (currentT + stepLength * blueNoiseOffset);

					if (IsPosVisible(currPos, maxDepth, isMaxDepth))//Checks if an object is occluding the raymarch
					{
						float currDensity = 0.0;

						float densityStepLOD = min(currentT / weatherMapSize, 4);
						if (scatTransmittance < 0.1)
						{
							densityStepLOD += 1;
							detailedStepLength = stepLength * 0.4;
						}

						if (isBaseStep == false) //detailed step
						{
							currDensity = GetDensity(currPos, densityStepLOD, false);

							

							if (currDensity > 0.0)
							{
								samplesWithZeroDensity = 0;

								//Normal Raymarch


								float extinction = currDensity * coefficients.x;
								float clampedExtinction = max(extinction, 0.0000001);
								float transmittance = exp(-clampedExtinction * detailedStepLength);

								float3 luminance = float3(0.0, 0.0, 0.0);
								for (int i = 0; i < lightIterations; ++i)
								{
									luminance += LightScatter(currPos, cosAngle, i, densityStepLOD);
								}
								luminance *= currDensity;

								float3 integScatt = (luminance - luminance * transmittance) / clampedExtinction;
								scatLuminance += scatTransmittance * integScatt;

								scatTransmittance *= transmittance;


								if (scatTransmittance <= 0.8 && atmosphereHazeAssigned == false)
								{
									atmosphereHazeT = currentT;
									atmosphereHazeAssigned = true;
								}


								if (scatTransmittance < 0.01)
								{
									scatTransmittance = 0.0;
									finished = true;
								}
							}
							else //If density is 0
							{
								samplesWithZeroDensity++;
								if (samplesWithZeroDensity >= 8)//When several continuous samples with 0 density are encountered switch back to non detailed samples
								{
									samplesWithZeroDensity = 0;
									isBaseStep = true;
								}
							}
							previousT = currentT;
							currentT += detailedStepLength;
						}
						else
						{

							currDensity = GetDensity(currPos, densityStepLOD, true);

							if (currDensity > 0.0) //change to detailed steps if there is a cloud
							{
								currentT = previousT; //go back to previous pos before changing
								isBaseStep = false;
							}
							else //continue at low resolution
							{
								previousT = currentT;
								currentT += stepLength;
							}
						}


					}
					else
					{
						finished = true;
					}
				}

			}

			float4 Raymarching(float3 rd, AtmosIntersection atmosIntersection,float2 uv,float maxDepth,bool isMaxDepth)
			{
				uint blueNoiseW;
				uint blueNoiseH;
				blueNoiseTexture.GetDimensions(blueNoiseW, blueNoiseH);
				uv.x *= (texDimensions.x / texDimensions.y);
				uv *= min(texDimensions.x, texDimensions.y) / blueNoiseW;
				float blueNoiseOffset = blueNoiseTexture.Sample(samplerblueNoiseTexture, uv);
				float cosAngle = dot(-rd,sunDir);//We assume they are normalized

				float scatteredtransmittance = 1.0;
				float3 scatteredLuminance = float3(0.0, 0.0, 0.0);

				float atmosphereHazeT = 0.0;
				bool atmosphereHazeAssigned = false;

				RaymarchThroughAtmos(blueNoiseOffset, rd, atmosIntersection.intersectionsT.x, atmosIntersection.intersectionsT.y,maxDepth, atmosIntersection.startsInAtmos,cosAngle,isMaxDepth, atmosphereHazeAssigned, scatteredtransmittance, scatteredLuminance, atmosphereHazeT);

				if (atmosIntersection.hasRay2)
				{
					RaymarchThroughAtmos(blueNoiseOffset, rd, atmosIntersection.intersectionsT.z, atmosIntersection.intersectionsT.w, maxDepth, true, cosAngle, isMaxDepth, atmosphereHazeAssigned, scatteredtransmittance, scatteredLuminance, atmosphereHazeT);
				}

				float ammountTravelledThroughAtmos = 0.0;
				if (!atmosIntersection.startsInAtmos)
				{
					ammountTravelledThroughAtmos = atmosphereHazeT - atmosIntersection.intersectionsT.x;
				}
				else
				{
					ammountTravelledThroughAtmos = atmosphereHazeT;
				}

				float atmosphereVisibDist = maxRayPossibleGroundDist * 0.5;
				if (hazeMaxDist > 0.0)
				{
					atmosphereVisibDist = min(hazeMaxDist, atmosphereVisibDist);
				}

				float maxHazeDist = atmosphereVisibDist;//Horizon max view
				float minHazeDist = hazeMinDist;//TODO consider making this public
				float hazeAmmount = saturate(Remap(ammountTravelledThroughAtmos, minHazeDist, maxHazeDist, 0.0, 1.0));

				hazeAmmount = 1.0 - (1.0 - hazeAmmount) * (1.0 - hazeAmmount);
				return float4(scatteredLuminance * saturate(1.0 - hazeAmmount), max(scatteredtransmittance,hazeAmmount));
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float viewLength = length(i.ray);
				float3 rayDirection = i.ray / viewLength;

				float depth = tex2D(_CameraDepthTexture,i.uv);
				float linearDepth = Linear01Depth(depth);//depth 0,1
				float depthMeters = _ProjectionParams.z * linearDepth;
				float4 posView = float4(i.ray * depthMeters, 1.0);
				float3 posWorld = mul(unity_CameraToWorld, posView).xyz;
				depthMeters = length(posWorld - _WorldSpaceCameraPos);//depth in meters

				float t = 0.0;
				AtmosIntersection atmosIntersection;
				bool isAtmosRay = GetRayAtmosphere(rayDirection, atmosIntersection);

				if (isAtmosRay && atmosIntersection.intersectionsT.y - atmosIntersection.intersectionsT.x > 0.0)
				{
					return Raymarching(rayDirection, atmosIntersection, i.uv, depthMeters, linearDepth >= 1.0);
				}
				else
				{
					return fixed4(0.0, 0.0, 0.0, 1.0);
				}
			}
	ENDCG
			}
	}
}