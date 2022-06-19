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

			float minCloudHeight;
			float maxCloudHeight;


			Texture3D<float4> baseShapeTexture;
			Texture3D<float3> detailTexture; //TODO see if I can get around using a float3
			Texture2D<float3> weatherMapTexture;
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
			float3 ambientColors[3];
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

			float maxRayUserDist;
			float maxRayPossibleDist;
			float maxRayPossibleGroundDist;
			float hazeMinDist;
			float hazeMaxDist;

			bool transitioningWM;
			float transitionLerpT;
			Texture2D<float4> weatherMapTextureNew;
			SamplerState samplerweatherMapTextureNew;

			int lightIterations;

			static const float3 fbmMultipliers = { 0.625,0.25,0.125 };
			static const float3 lightOctaveParameters = { 0.25,0.75,0.6 };
			static const int iter = 4;




			struct AtmosIntersection
			{

				float3 r1o;//origin
				float r1m;//magnitude
				float3 r2o;//origin
				float r2m;//magnitude

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
				intersection.startsInAtmos = false;
				intersection.r1o = ro;
				intersection.r2o = ro;
				intersection.hasRay2 = false;
				intersection.r1m = 0.0;
				intersection.r2m = 0.0;

				float3 planetO = { 0.0, -planetAtmos.x,0.0 };
				float d = length(ro - planetO);//distance btween camera and the planet center

				float2 innerAtmT = GetAtmosphereIntersection(ro, rd, planetO, planetAtmos.y);
				float2 outerAtmT = GetAtmosphereIntersection(ro, rd, planetO, planetAtmos.z);
				float2 groundT = GetAtmosphereIntersection(ro, rd, planetO, planetAtmos.x);

				if (d < planetAtmos.y)//if camera is under the atmosphere
				{
					intersection.startsInAtmos = true;

					if (max(groundT.x, groundT.y) != -1.0) //if collision is with ground return false
					{
						return false;
					}

					intersection.r1o = ro + rd * innerAtmT.y;//second hit as 1st will always be behind camera in this case
					intersection.r1m = outerAtmT.y - innerAtmT.y;//second hit as 1st will always be behind camera in this case

					return true;
				}
				else if (d < planetAtmos.z) //if camera is inside the atmosphere
				{
					intersection.startsInAtmos = true;
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
					else
					{
						intersection.r1m = outerAtmT.y;

						return true;
					}

				}
				else if (outerAtmT.x < 0.0)//If camera is above atmosphere
				{
					return false;//No hit or behind camera!
				}
				else
				{
					intersection.startsInAtmos = false;

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
					}
					else
					{
						intersection.r1m = outerAtmT.y - outerAtmT.x;
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
				sampleLvl += min(length(currPos - _WorldSpaceCameraPos) / 200000.0,4);//TODO expose these variables?
				float3 initialPos = currPos;
				float density = 0.0;
				float cloudHeightPercent = GetCloudLayerHeightSphere(currPos);//value between 0 & 1 showing where we are in the cloud
				float fTime = _Time;
				float3 windOffset = -windDir * float3(fTime, fTime, fTime);//TODO make this & skewk consistent around the globe
				float3 skewPos = currPos - normalize(windDir) * cloudHeightPercent * cloudHeightPercent * 100 * skewAmmount;
				float3 weatherMapCloud = weatherMapTexture.SampleLevel(samplerweatherMapTexture, (skewPos.xz / weatherMapSize) + windOffset.xz * windDisplacesWeatherMap,sampleLvl); //We sample the weather map (r coverage,g type)
				if (transitioningWM == true)
					weatherMapCloud = lerp(weatherMapCloud, weatherMapTextureNew.SampleLevel(samplerweatherMapTextureNew, (skewPos.xz / weatherMapSize) + windOffset.xz * windDisplacesWeatherMap, sampleLvl), transitionLerpT);

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

			float CalculateStepsForRay(float rayLength)
			{
				float atmosphereHeight = planetAtmos.z - planetAtmos.y;
				rayLength = max(rayLength, atmosphereHeight);
				return Remap(rayLength, atmosphereHeight,maxRayPossibleDist, 64.0,128.0);
			}

			float CalculateMaxRayDist(float rayLength)
			{
				if (maxRayUserDist == 0.0)
				{
					return rayLength;
				}
				else
				{
					return min(maxRayUserDist, rayLength);
				}
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

			float LightShadowTransmittance(float3 pos, float initialStepSize,float eCoeff)
			{
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

				float newExtinctionC = coefficients.x * pow(lightOctaveParameters.x,i);
				float newScatterC = coefficients.y * pow(lightOctaveParameters.y,i);

				float3 ambientSky = ambientColors[0].xyz;
				float3 ambientFloor = ambientColors[1].xyz * 0.5;
				float3 ambientSun = ambientColors[2].xyz;

				float heightPercent = GetCloudLayerHeightSphere(currPos);
				float t = saturate(Remap(heightPercent, 0.0, 1.0, 0.15, 1.0));

				float shadow = 1.0;
				if (softerShadows == true)
				{
					shadow = LightShadowTransmittanceCone(currPos, shadowSize, newExtinctionC);
				}
				else
				{
					shadow = LightShadowTransmittance(currPos, shadowSize, newExtinctionC);
				}

				const float cMult = pow(lightOctaveParameters.z, i);
				return lightColor * shadow * DoubleLobeScattering(cosAngle , 0.3 * cMult, 0.15 * cMult, 0.5) * newScatterC + (ambientSun * 0.5 + ambientSky * 0.5) * t * shadow * (1.0 / 4.0 * 3.1415) * newScatterC;
			}

			void RaymarchThroughAtmos(float3 pos,float3 rd, int maxSteps,
				float stepLengthBase,float maxDepth,float cosAngle,bool isMaxDepth,
				inout bool atmosphereHazeAssigned,
				inout float scatTransmittance, inout float3 scatLuminance, inout float3 atmosphereHazePos)
			{
				bool isBaseStep = true;//Base step or full step
				int samplesWithZeroDensity = 0;


				const float tMax = stepLengthBase * maxSteps;//TODO consider passing the total length of the ray as a parameter
				float currentT = 0;
				while (currentT <= tMax)
				{
					float3 currPos = pos + rd * currentT;

					if (IsPosVisible(currPos, maxDepth, isMaxDepth) && scatTransmittance > 0.0)//Checks if an object is occluding the raymarch
					{
						float currDensity = 0.0;

						if (isBaseStep == true)
						{
							currDensity = GetDensity(currPos, 0.0, true);

							if (currDensity > 0.0) //change to detailed steps if there is a cloud
							{
								currentT -= stepLengthBase; //go back to previous pos before changing
								currentT = max(0, currentT);//also clamp to the start of the cloud
								isBaseStep = false;
							}
							else //continue at low resolution
							{
								currentT += stepLengthBase;
							}
						}
						else
						{
							currDensity = GetDensity(currPos, 0.0, false);

							if (scatTransmittance <= 0.9 && atmosphereHazeAssigned == false)
							{
								atmosphereHazePos = currPos;
								atmosphereHazeAssigned = true;
							}



							if (currDensity > 0.0)
							{
								samplesWithZeroDensity = 0;

								//Normal Raymarch


								float extinction = currDensity * coefficients.x;
								float clampedExtinction = max(extinction, 0.0000001);
								float transmittance = exp(-clampedExtinction * stepLengthBase);

								float3 luminance = float3(0.0, 0.0, 0.0);
								for (int i = 0; i < lightIterations; ++i)
								{
									luminance += LightScatter(currPos, cosAngle, i);
								}
								luminance *= currDensity;

								float3 integScatt = (luminance - luminance * transmittance) / clampedExtinction;
								scatLuminance += scatTransmittance * integScatt;

								scatTransmittance *= transmittance;



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

							currentT += stepLengthBase * 0.125;
						}


					}



					currentT += stepLengthBase;
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

				float maxStepsRay = CalculateStepsForRay(atmosIntersection.r1m);
				float stepLength = CalculateMaxRayDist(atmosIntersection.r1m) / maxStepsRay;

				float3 startingPos = atmosIntersection.r1o + rd * stepLength * blueNoiseOffset;
				float scatteredtransmittance = 1.0;
				float3 scatteredLuminance = float3(0.0, 0.0, 0.0);

				float3 atmosphereHazePos = float3(0.0, 0.0, 0.0);
				bool atmosphereHazeAssigned = false;

				RaymarchThroughAtmos(startingPos, rd, maxStepsRay, stepLength,maxDepth,cosAngle,isMaxDepth, atmosphereHazeAssigned, scatteredtransmittance, scatteredLuminance, atmosphereHazePos);

				if (atmosIntersection.hasRay2)
				{
					maxStepsRay = CalculateStepsForRay(atmosIntersection.r2m);
					stepLength = CalculateMaxRayDist(atmosIntersection.r2m) / maxStepsRay;

					startingPos = atmosIntersection.r2o + rd * stepLength * blueNoiseOffset;

					RaymarchThroughAtmos(startingPos, rd, maxStepsRay, stepLength, maxDepth, cosAngle, isMaxDepth, atmosphereHazeAssigned, scatteredtransmittance, scatteredLuminance, atmosphereHazePos);
				}

				float ammountTravelledThroughAtmos = 0.0;
				if (atmosIntersection.startsInAtmos)
				{
					ammountTravelledThroughAtmos = length(atmosphereHazePos - _WorldSpaceCameraPos);
				}
				else
				{
					ammountTravelledThroughAtmos = length(atmosphereHazePos - atmosIntersection.r1o);
				}

				float atmosphereVisibDist = maxRayPossibleGroundDist * 0.5;
				if (hazeMaxDist > 0.0)
				{
					atmosphereVisibDist = min(hazeMaxDist, atmosphereVisibDist);
				}

				float maxHazeDist = atmosphereVisibDist;//Horizon max view
				float minHazeDist = hazeMinDist;//TODO consider making this public
				float hazeAmmount = saturate(Remap(ammountTravelledThroughAtmos, minHazeDist, maxHazeDist, 0.0, 1.0));


				//col = lerp(scatteredtransmittance * col + scatteredLuminance, col, 1.0 - (1.0 - hazeAmmount) * (1.0 - hazeAmmount));

				hazeAmmount = 1.0 - (1.0 - hazeAmmount) * (1.0 - hazeAmmount);
				return float4(scatteredLuminance * saturate(1.0 - hazeAmmount), max(scatteredtransmittance,hazeAmmount));
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float3 rayOrigin = _WorldSpaceCameraPos;
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
				bool isAtmosRay = GetRayAtmosphere(rayOrigin, rayDirection, atmosIntersection);

				if (!isAtmosRay || atmosIntersection.r1m <= 0.0)
				{
					return fixed4(0.0,0.0,0.0, 1.0);
				}
				else
				{
					float4 result = Raymarching(rayDirection, atmosIntersection,i.uv, depthMeters,linearDepth >= 1.0);
					return result;
				}
			}
	ENDCG
			}
	}
}