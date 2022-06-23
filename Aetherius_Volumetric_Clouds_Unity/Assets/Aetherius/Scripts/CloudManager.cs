using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    [ExecuteInEditMode]
    public class CloudManager : MonoBehaviour
    {

        public CLOUD_CONTROL mode = CLOUD_CONTROL.SIMPLE;
        public CLOUD_PRESET preset = CLOUD_PRESET.CLOUDY;
        public CLOUD_RESOLUTION resolution = CLOUD_RESOLUTION.ORIGINAL;

        public CloudShape simple;
        public CloudShape advanced;

        //Ray March
        public float baseRaymarchStep = 400.0f;
        public float dynamicStepsCoefficient = 100000.0f;
        public Texture2D blueNoise;

        //Weather System
        public int wmSeed = 307;
        public bool windDisplacesWeatherMap = true;
        public bool cumulusHorizon = false;
        public Vector2 cumulusHorizonGradient = new Vector2(18000, 75000);
        public Texture2D advancedWM;
        //WS Cloud Layers
        public Vector4 cloudLayerGradient1 = new Vector4(0.0f, 0.05f, 0.1f, 0.3f);
        public Vector4 cloudLayerGradient2 = new Vector4(0.2f, 0.3f, 0.3f, 0.45f);
        public Vector4 cloudLayerGradient3 = new Vector4(0.0f, 0.1f, 0.7f, 1.0f);

        public float densityCurveMultiplier1 = 1.0f;
        public float densityCurveMultiplier2 = 1.0f;
        public float densityCurveMultiplier3 = 1.0f;
        public AnimationCurve densityCurve1 = new AnimationCurve( //Only in advanced mode
          new Keyframe[3] {
                new Keyframe(0.103f,0.020f,3.556f,3.556f),
                new Keyframe(0.205f,0.961f,-0.311f,-0.311f),
                new Keyframe(0.300f,0,-3.059f,-3.059f)}
          );
        public AnimationCurve densityCurve2 = new AnimationCurve( //Only in advanced mode //TODO change parameters
         new Keyframe[4] {
                new Keyframe(0.318f,0.024f,59.606f,59.606f),
                new Keyframe(0.351f,0.993f,0.319f,0.319f),
                new Keyframe(0.418f,0.855f,-6.531f,-6.531f),
                new Keyframe(0.699f,0.010f,0.182f,-0.182f)}
         );

        public AnimationCurve densityCurve3 = new AnimationCurve( //Only in advanced mode //TODO change parameters
         new Keyframe[3] {
                new Keyframe(0.003f,0.749f,0.000f,0.000f),
                new Keyframe(0.499f,0.498f,1.500f,1.500f),
                new Keyframe(1.000f,1.000f,0.000f,0.000f)}
         );


        //WS Transition
        public bool transitioning = false;
        public float transitionTimeWM = 10.0f;
        public float currentTransitionTimeWM = 0.0f;

        //Atmosphere
        public float minCloudHeightMeters = 1000.0f;
        public float maxCloudHeightMeters = 8000.0f;
        public float baseShapeWindMult = 1.5f;
        public float detailShapeWindMult = 3.0f;
        public float skewAmmount = 0.1f;
        public Vector3 windDirection = new Vector3(0.01f, 0.025f, 0.005f);
        public int planetRadiusKm = 6371;
        public Vector2Int hazeVisibilityAtmos = new Vector2Int(50000, 100000); //distance through the atmosphere layer

        //Lighting
        public Light sunLight;
        public float lightIntensityMult = 6.5f;
        public float ambientLightIntensity = 1.0f;
        public float extintionC = 0.0f;
        public float scatterC = 0.1f;
        public float absorptionC = 0.0f;
        public float shadowSize = 100.0f;
        public bool softerShadows = false;
        public int lightIterations = 4;

        public TextureGenerator textureGenerator;
        public List<Vector4> conekernel;

        public Quaternion lastSunRotation;

        public float currentTimeUpdateGI = 0.0f;
        public float timeToUpdateGI = 0.05f;
        public Vector3 lightOctaveParameters = new Vector3(0.3f, 0.75f, 0.5f);
        private List<Vector4> GenerateConeKernels()
        {
            List<Vector4> newList = new List<Vector4>();
            for (int i = 0; i < 6; ++i)
            {
                newList.Add(Random.onUnitSphere);
            }

            return newList;
        }

        public CloudShape currentShape
        {
            get
            {
                if (mode == CLOUD_CONTROL.SIMPLE)
                {
                    return simple;
                }
                return advanced;
            }
        }

        public void OnEnable()
        {

            if (textureGenerator == null)
            {
                textureGenerator = new TextureGenerator();
            }
            textureGenerator.InitializeTextures();
            conekernel = GenerateConeKernels();
            textureGenerator.GenerateWeatherMap(256, ref textureGenerator.originalWM, wmSeed, preset);
            textureGenerator.GenerateAllNoise();


            sunLight = RenderSettings.sun;

        }


        // Update is called once per frame
        public void Update()
        {
            currentTimeUpdateGI += Time.deltaTime;

            if (sunLight.transform.rotation != lastSunRotation && currentTimeUpdateGI >= timeToUpdateGI)
            {
                DynamicGI.UpdateEnvironment();
                lastSunRotation = sunLight.transform.rotation;
                currentTimeUpdateGI = 0.0f;
            }


            if (transitioning)
            {
                if (currentTransitionTimeWM > transitionTimeWM)
                {
                    StopWMTransition();
                }
                else
                {
                    currentTransitionTimeWM += Time.deltaTime;
                }
            }
        }

        public void OnGUI()
        {
#if UNITY_EDITOR
            // Ensure continuous Update calls.
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                //UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        //TODO pass the preset as an argument in the future instead of picking the one in the class
        public void StartWMTransition(float duration = -1.0f)//If duration = -1 interpret this as no argument
        {
            if (transitioning) //if a transition was ocurring already generate a texture with the status of the transition currently and lerp with that
            {
                textureGenerator.LerpWM(256, Mathf.Clamp01(currentTransitionTimeWM / transitionTimeWM));
                Debug.Log("Changing Transition on the fly!");

            }

            if (duration >= 0.0f)
            {
                transitionTimeWM = duration;
            }
            currentTransitionTimeWM = 0.0f;

            textureGenerator.GenerateWeatherMap(256, ref textureGenerator.newWM, wmSeed, preset);
            transitioning = true;

            Debug.Log("TransitionStarted!");

        }

        public void StopWMTransition()
        {
            if (!transitioning)
                return;


            if (textureGenerator.newWM != null)
            {
                Utility.ReleaseTexture(ref textureGenerator.newWM);
            }
            textureGenerator.GenerateWeatherMap(256, ref textureGenerator.originalWM, wmSeed, preset); //TODO provisional solution, texture gets erased when assigning: proceduralWM = proceduralWMNew

            transitioning = false;
            currentTransitionTimeWM = 0.0f;
            Debug.Log("TransitionEnded!");
        }

        public void CreateLUTBuffer(int samples, ref AnimationCurve curve, string name, ref Material mat)
        {
            List<float> fTest = DensityGradientLutFromCurve(ref curve, samples);
            ComputeBuffer newBuff = textureGenerator.CreateComputeBuffer(sizeof(float), fTest.ToArray());
            mat.SetBuffer(name, newBuff);
        }
        private List<float> DensityGradientLutFromCurve(ref AnimationCurve curve, int samples)
        {
            List<float> retList = new List<float>(samples);

            for (int i = 0; i < samples; ++i)
            {
                retList.Add(curve.Evaluate(i * (1.0f / samples)));
            }
            return retList;
        }

        public void SetMaterialProperties(ref Material mat, Vector2 texDimensions)
        {
           
                mat.SetTexture("weatherMapTextureNew", textureGenerator.newWM);

            if (mode == CLOUD_CONTROL.ADVANCED && advancedWM !=null)
            {
                mat.SetTexture("weatherMapTexture", advancedWM);
                mat.SetFloat("transitionLerpT", 0.0f);
            }
            else
            {
                mat.SetFloat("transitionLerpT", Mathf.Clamp01(currentTransitionTimeWM / transitionTimeWM));
                mat.SetTexture("weatherMapTexture", textureGenerator.GetWM(wmSeed, preset));

            }


            mat.SetTexture("baseShapeTexture", textureGenerator.GetTexture(TEXTURE_TYPE.BASE_SHAPE));
            mat.SetTexture("detailTexture", textureGenerator.GetTexture(TEXTURE_TYPE.DETAIL));
            mat.SetTexture("blueNoiseTexture", blueNoise);
            mat.SetVector("texDimensions", texDimensions);

            mat.SetFloat("hazeMinDist", hazeVisibilityAtmos.x);
            mat.SetFloat("hazeMaxDist", hazeVisibilityAtmos.y);


            mat.SetFloat("baseShapeSize", currentShape.baseShapeSize);
            mat.SetFloat("detailSize", currentShape.detailSize);
            mat.SetFloat("weatherMapSize", currentShape.weatherMapSize);

            mat.SetFloat("globalCoverage", currentShape.globalCoverage);
            mat.SetFloat("globalDensity", currentShape.globalDensity);

            Vector3 currentSunDir = sunLight.transform.rotation * Vector3.forward;
            mat.SetVector("sunDir", currentSunDir);
            mat.SetVector("coefficients", new Vector2(absorptionC + scatterC, scatterC));//extinction, scatter
            mat.SetInt("softerShadows", softerShadows ? 1 : 0);
            mat.SetFloat("shadowSize", shadowSize);
            mat.SetInt("lightIterations", lightIterations);


            Color[] c = new Color[2];
            Vector3[] dirs = new Vector3[2];
            dirs[0] = Vector3.up;
            dirs[1] = -currentSunDir;


            RenderSettings.skybox.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;


            RenderSettings.ambientProbe.Evaluate(dirs, c);
            List<Vector4> ambientColors = new List<Vector4>();
            ambientColors.Add(c[0]);//Up
            ambientColors.Add(c[1]);//SunDir

            float weightedGrayscaleAmbientSky = 0.299f * ambientColors[0].x + 0.587f * ambientColors[0].y + 0.114f * ambientColors[0].z;
            float weightedGrayscaleAmbientSun = 0.299f * ambientColors[1].x + 0.587f * ambientColors[1].y + 0.114f * ambientColors[1].z;
            float weightedGrayscaleAmbient = Mathf.Min(Mathf.Max(weightedGrayscaleAmbientSun, weightedGrayscaleAmbientSky), 0.18f);

            float inclination = Vector3.Dot(-currentSunDir, Vector3.up);
            inclination = Utility.RemapClamp(inclination, -0.10f, 0.0f, 0.0f, 1.0f);
            //inclination = inclination*inclination;
            float newAmbientLightIntensity = Mathf.Lerp(6.0f * ambientLightIntensity, ambientLightIntensity, inclination);

            for (int i = 0; i < ambientColors.Count; ++i)
            {
                ambientColors[i] *= newAmbientLightIntensity;
            }

            mat.SetVector("lightColor", sunLight.color * sunLight.intensity * lightIntensityMult * weightedGrayscaleAmbient);
            mat.SetVectorArray("ambientColors", ambientColors);
            mat.SetVectorArray("coneKernel", conekernel);


            mat.SetVector("windDir", windDirection);
            mat.SetInt("windDisplacesWeatherMap", windDisplacesWeatherMap ? 1 : 0);
            mat.SetFloat("baseShapeWindMult", baseShapeWindMult);
            mat.SetFloat("detailShapeWindMult", detailShapeWindMult);
            mat.SetFloat("skewAmmount", skewAmmount);

            mat.SetInt("mode", (int)mode);

            int planetRadiusMeters = planetRadiusKm * 1000;
            Vector3 planetAtmos = new Vector3(planetRadiusMeters, planetRadiusMeters + minCloudHeightMeters, planetRadiusMeters + maxCloudHeightMeters);//Center of the planet, radius of min cloud sphere, radius of max cloud sphere
            mat.SetVector("planetAtmos", planetAtmos);
            mat.SetFloat("maxRayPossibleDist", Mathf.Sqrt(Mathf.Pow(planetAtmos.z, 2) - Mathf.Pow(planetAtmos.y, 2)) * 2.0f); //maximum ray length on the cloud layer
            mat.SetFloat("maxRayPossibleGroundDist", Mathf.Sqrt(Mathf.Pow(planetAtmos.z, 2) - Mathf.Pow(planetAtmos.x, 2)) * 2.0f);//maximum ray length of a ray travelling trough the atmosphere without touching the ground 
            mat.SetVector("dynamicRaymarchParameters", new Vector2(baseRaymarchStep, dynamicStepsCoefficient));

            mat.SetInt("cumulusHorizon", cumulusHorizon ? 1 : 0);
            mat.SetVector("cumulusHorizonGradient", cumulusHorizonGradient);
            mat.SetVector("cloudLayerGradient1", cloudLayerGradient1);
            mat.SetVector("cloudLayerGradient2", cloudLayerGradient2);
            mat.SetVector("cloudLayerGradient3", cloudLayerGradient3);


            int lutBufferSize = 256;
            CreateLUTBuffer(lutBufferSize, ref densityCurve1, "densityCurveBuffer1", ref mat);
            mat.SetInt("densityCurveBufferSize1", lutBufferSize);
            mat.SetFloat("densityCurveMultiplier1", densityCurveMultiplier1);
            CreateLUTBuffer(lutBufferSize, ref densityCurve2, "densityCurveBuffer2", ref mat);
            mat.SetInt("densityCurveBufferSize2", lutBufferSize);
            mat.SetFloat("densityCurveMultiplier2", densityCurveMultiplier2);
            CreateLUTBuffer(lutBufferSize, ref densityCurve3, "densityCurveBuffer3", ref mat);
            mat.SetInt("densityCurveBufferSize3", lutBufferSize);
            mat.SetFloat("densityCurveMultiplier3", densityCurveMultiplier3);


            CreateLightParamPows(lightIterations, ref mat);

        }

        void CreateLightParamPows(int bufferSize, ref Material mat)
        {
            List<Vector3> lightParamPows = new List<Vector3>();


            for (int i = 0; i < bufferSize; ++i)
            {
                lightParamPows.Add(new Vector3(Mathf.Pow(lightOctaveParameters.x, i), Mathf.Pow(lightOctaveParameters.y, i), Mathf.Pow(lightOctaveParameters.z, i)));
            }
            ComputeBuffer newBuff = textureGenerator.CreateComputeBuffer(sizeof(float) * 3, lightParamPows.ToArray());
            mat.SetBuffer("lightOctaveParameters", newBuff);
        }

    }
}