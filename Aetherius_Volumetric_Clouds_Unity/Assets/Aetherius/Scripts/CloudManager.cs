using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    [ExecuteInEditMode]
    public class CloudManager : MonoBehaviour
    {

        public CLOUD_CONTROL mode = CLOUD_CONTROL.SIMPLE;
        public CLOUD_PRESET preset = CLOUD_PRESET.SPARSE;

        public CloudShape simple;
        public CloudShape advanced;

        //Ray March
        public int maxRayVisibilityDist = 50000;
        public Texture2D blueNoise;

        //Weather System
        public int wmSeed = 307;
        public bool windDisplacesWeatherMap = true;
        public bool cumulusHorizon = false;
        public Vector2 cumulusHorizonGradient = new Vector2(18000, 75000);
        //WS Cloud Layers
        public Vector4 cloudLayerGradient1 = new Vector4(0.0f, 0.05f, 0.1f, 0.3f);
        public Vector4 cloudLayerGradient2 = new Vector4(0.2f, 0.3f, 0.3f, 0.45f);
        public Vector4 cloudLayerGradient3 = new Vector4(0.0f, 0.1f, 0.7f, 1.0f);

        public float densityCurveMultiplier1 = 1.0f;
        public float densityCurveMultiplier2 = 1.0f;
        public float densityCurveMultiplier3 = 1.0f;
        public AnimationCurve densityCurve1 = new AnimationCurve( //Only in advanced mode
          new Keyframe[3] {
                new Keyframe(0.0f,0.0f,14.5f,14.5f),
                new Keyframe(0.2f,1.0f,0.15f,0.15f),
                new Keyframe(1.0f,0.0f,-3.0f,-3.0f)}
          );
        public AnimationCurve densityCurve2 = new AnimationCurve( //Only in advanced mode //TODO change parameters
         new Keyframe[3] {
                new Keyframe(0.0f,0.0f,14.5f,14.5f),
                new Keyframe(0.2f,1.0f,0.15f,0.15f),
                new Keyframe(1.0f,0.0f,-3.0f,-3.0f)}
         );
        public AnimationCurve densityCurve3 = new AnimationCurve( //Only in advanced mode //TODO change parameters
         new Keyframe[3] {
                new Keyframe(0.0f,0.0f,14.5f,14.5f),
                new Keyframe(0.2f,1.0f,0.15f,0.15f),
                new Keyframe(1.0f,0.0f,-3.0f,-3.0f)}
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

        //Lighting
        public Light sunLight;
        public float lightIntensityMult = 6.5f;
        public float ambientLightIntensity = 1.0f;
        public float extintionC = 0.0f;
        public float scatterC = 0.1f;
        public float absorptionC = 0.0f;
        public float shadowSize = 100.0f;
        public bool softerShadows = false;
        
        public TextureGenerator textureGenerator;
        public List<Vector4> conekernel;
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

            if (transitioning)
            {
                if (currentTransitionTimeWM >= transitionTimeWM)
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
        public void StartWMTransition(int duration = -1)//If duration = -1 interpret this as no argument
        {
            if (transitioning) //if a transition was ocurring already generate a texture with the status of the transition currently and lerp with that
            {
                textureGenerator.LerpWM(256, Mathf.Clamp01(currentTransitionTimeWM / transitionTimeWM));
                Debug.Log("Changing Transition on the fly!");

            }

            if (duration != -1)
            {
                transitionTimeWM = duration;
            }
            textureGenerator.GenerateWeatherMap(256, ref textureGenerator.newWM, wmSeed, preset);
            currentTransitionTimeWM = 0.0f;
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

        public void SetMaterialProperties(ref Material mat)
        {
            if (transitioning)
            {
                mat.SetTexture("weatherMapTextureNew", textureGenerator.newWM);
                mat.SetFloat("transitionLerpT", Mathf.Clamp01(currentTransitionTimeWM / transitionTimeWM));
            }
            mat.SetInt("transitioningWM", transitioning ? 1 : 0);


            mat.SetTexture("baseShapeTexture", textureGenerator.GetTexture(TEXTURE_TYPE.BASE_SHAPE));
            mat.SetTexture("detailTexture", textureGenerator.GetTexture(TEXTURE_TYPE.DETAIL));
            mat.SetTexture("weatherMapTexture", textureGenerator.GetWM(wmSeed, preset));
            mat.SetTexture("blueNoiseTexture", blueNoise);


            mat.SetFloat("minCloudHeight", minCloudHeightMeters);
            mat.SetFloat("maxCloudHeight", maxCloudHeightMeters);
            mat.SetInt("maxRayVisibilityDist", maxRayVisibilityDist);

            mat.SetFloat("baseShapeSize", currentShape.baseShapeSize);
            mat.SetFloat("detailSize", currentShape.detailSize);
            mat.SetFloat("weatherMapSize", currentShape.weatherMapSize);

            mat.SetFloat("globalCoverage", currentShape.globalCoverage);
            mat.SetFloat("globalDensity", currentShape.globalDensity);

            mat.SetVector("sunDir", sunLight.transform.rotation * Vector3.forward);
            mat.SetFloat("absorptionC", absorptionC);
            mat.SetFloat("scatterC", scatterC);
            mat.SetFloat("extintionC", absorptionC + scatterC);
            mat.SetFloat("lightIntensity", sunLight.intensity * lightIntensityMult);
            mat.SetFloat("ambientLightIntensity", ambientLightIntensity);
            mat.SetInt("softerShadows", softerShadows ? 1 : 0);
            mat.SetFloat("shadowSize", shadowSize);



            Color[] c = new Color[2];
            Vector3[] dirs = new Vector3[2];
            dirs[0] = Vector3.up;
            dirs[1] = Vector3.down;


            RenderSettings.ambientProbe.Evaluate(dirs, c);
            List<Vector4> ambientColors = new List<Vector4>();
            ambientColors.Add(c[0]);
            ambientColors.Add(c[1]);

            mat.SetVector("lightColor", sunLight.color);
            mat.SetVectorArray("ambientColors", ambientColors);
            mat.SetVectorArray("coneKernel", conekernel);


            mat.SetVector("windDir", windDirection);
            mat.SetInt("windDisplacesWeatherMap", windDisplacesWeatherMap ? 1 : 0);
            mat.SetFloat("baseShapeWindMult", baseShapeWindMult);
            mat.SetFloat("detailShapeWindMult", detailShapeWindMult);
            mat.SetFloat("skewAmmount", skewAmmount);

            mat.SetInt("mode", (int)mode);

            int planetRadiusMeters = planetRadiusKm * 1000;
            mat.SetVector("planetAtmos", new Vector3(-planetRadiusMeters, planetRadiusMeters + minCloudHeightMeters, planetRadiusMeters + maxCloudHeightMeters));//Center of the planet, radius of min cloud sphere, radius of max cloud sphere
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


        }

    }
}