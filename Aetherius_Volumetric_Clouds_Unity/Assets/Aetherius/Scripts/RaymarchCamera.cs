using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{



    [System.Serializable]
    public struct CloudShape
    {
        public float baseShapeSize; //Advanced
        public float detailSize; //Advanced
        public float globalCoverage; //Advanced
        public float globalDensity; //Advanced

        public Texture2D weatherMap; //Advanced -> TODO when we have weather map generation
        public float weatherMapSize; //Advanced
    }

    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    [ExecuteInEditMode]
    public class RaymarchCamera : MonoBehaviour
    {
        public enum CLOUD_CONTROL
        {
            SIMPLE,
            ADVANCED
        }

        public enum CLOUD_PRESET
        {
            SPARSE,
            CLOUDY,
            STORMY,
            OVERCAST
        }

        private Camera _cam;

        public CLOUD_CONTROL mode = CLOUD_CONTROL.SIMPLE;
        [HideInInspector]
        public CLOUD_PRESET preset = CLOUD_PRESET.SPARSE;

        public CloudShape simple;
        public CloudShape advanced;

        [SerializeField, HideInInspector]
        private List<ComputeBuffer> toDeleteCompBuffers;

        [SerializeField]
        private Shader _shader;
        [SerializeField, HideInInspector]
        private Material _material;
        [Header("Ray March")]
        public int maxRayVisibilityDist = 50000;

        public Texture2D blueNoise;

        [Header("Weather Map")]
        public Texture2D weatherMap;
        public bool windDisplacesWeatherMap = true;

        public RenderTexture proceduralWM;
        public RenderTexture proceduralWMNew;
        public float transitionTimeWM = 10.0f;
        public float currentTransitionTimeWM = 0.0f;
        public bool transitioning = false;

        public int seedWM = 307;
        [HideInInspector]
        public bool cumulusHorizon = false;
        [HideInInspector]
        public Vector2 cumulusHorizonGradient;
        public Vector4 cloudLayerGradient1 = new Vector4(0.0f, 0.05f, 0.1f, 0.3f);
        public Vector4 cloudLayerGradient2 = new Vector4(0.2f, 0.3f, 0.3f, 0.45f);
        public Vector4 cloudLayerGradient3 = new Vector4(0.0f, 0.1f, 0.7f, 1.0f);


        [Header("Cloud")]
        public int planetRadiusKm = 6371;
        public float minCloudHeightMeters = 1000.0f;
        public float maxCloudHeightMeters = 8000.0f;
        public Vector3 windDirection = new Vector3(0.01f, 0.05f, 0.005f);
        public float baseShapeWindMult = 1.5f;
        public float detailShapeWindMult = 3.0f;
        public float skewAmmount = 1.0f;
        [HideInInspector]
        public AnimationCurve densityCurve = new AnimationCurve(
            new Keyframe[3] {
                new Keyframe(0.0f,0.0f,14.5f,14.5f),
                new Keyframe(0.2f,1.0f,0.15f,0.15f),
                new Keyframe(1.0f,0.0f,-3.0f,-3.0f)}
            );


        [Header("Lighting")]
        public Light sunLight;
        public float lightIntensityMult = 10.0f;
        public float ambientLightIntensity = 1.0f;
        [HideInInspector]
        public float extintionC = 1.0f;
        public float scatterC = 1.0f;
        public float absorptionC = 1.0f;

        List<Vector4> conekernel;

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

        public Material rayMarchMaterial
        {
            get
            {
                if (!_material && _shader)
                {
                    _material = new Material(_shader);
                    _material.hideFlags = HideFlags.HideAndDontSave;
                }
                return _material;
            }
        }

        public Camera _camera
        {
            get //TODO will this work with the editor cam?
            {
                if (!_cam)
                {
                    _cam = GetComponent<Camera>();
                }
                return _cam;
            }

        }
        private ProceduralTextureViewer noiseGen;

        public ref RenderTexture GetProceduralWM()
        {
            if (proceduralWM == null)
            {
                GenerateWM(ref proceduralWM);
            }

            return ref proceduralWM;
        }
        private void Awake()
        {
            noiseGen = GetComponent<ProceduralTextureViewer>();
            SetShapeParams(ref simple);

            if (advanced.globalDensity == 0.0f) //We use density as a flag to know whether it has been initialized -> TODO find a better way
            {
                SetShapeParams(ref advanced);
            }
        }

        public void StartWMTransition()//TODO pass the preset as an argument in the future instead of picking the one in the class
        {
            if (transitioning) //if a transition was ocurring already generate a texture with the status of the transition currently and lerp with that
            {
                ProceduralTextureViewer.LerpWM(256, ref noiseGen.computeShader, ref proceduralWM, ref proceduralWMNew, Mathf.Clamp01(currentTransitionTimeWM / transitionTimeWM), ref toDeleteCompBuffers);
                Debug.Log("Changing Transition on the fly!");

            }
            GenerateWM(ref proceduralWMNew);
            currentTransitionTimeWM = 0.0f;
            transitioning = true;
            Debug.Log("TransitionStarted!");

        }

        public void StopWMTransition()
        {
            if (!transitioning)
                return;


            if (proceduralWMNew != null)
            {
                ProceduralTextureViewer.ReleaseTexture(ref proceduralWMNew);
            }
            GenerateWM(ref proceduralWM); //TODO provisional solution, texture gets erased when assigning: proceduralWM = proceduralWMNew


            transitioning = false;
            currentTransitionTimeWM = 0.0f;
            Debug.Log("TransitionEnded!");
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (rayMarchMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (noiseGen == null)
            {
                noiseGen = GetComponent<ProceduralTextureViewer>();//try to get the component if it wasnt created before

                if (noiseGen == null)
                {
                    Debug.LogWarning("Couldn't find 'ProceduralTextureViewer Component on the camera'");
                    return;
                }
            }



            if (conekernel == null)
                conekernel = GenerateConeKernels();

            //GetComponent<ProceduralTextureViewer>().UpdateNoise();


            //UpdateGradientLUTs();

            if (transitioning)
            {
                rayMarchMaterial.SetTexture("weatherMapTextureNew", proceduralWMNew);
                rayMarchMaterial.SetFloat("transitionLerpT", Mathf.Clamp01(currentTransitionTimeWM / transitionTimeWM));
            }
            rayMarchMaterial.SetInt("transitioningWM", transitioning ? 1 : 0);



            rayMarchMaterial.SetTexture("_MainTex", source); //input the rendered camera texture 
            rayMarchMaterial.SetTexture("baseShapeTexture", noiseGen.GetTexture(ProceduralTextureViewer.TEXTURE_TYPE.BASE_SHAPE));
            rayMarchMaterial.SetTexture("detailTexture", noiseGen.GetTexture(ProceduralTextureViewer.TEXTURE_TYPE.DETAIL));
            rayMarchMaterial.SetTexture("weatherMapTexture", GetProceduralWM());
            rayMarchMaterial.SetTexture("blueNoiseTexture", blueNoise);


            rayMarchMaterial.SetFloat("minCloudHeight", minCloudHeightMeters);
            rayMarchMaterial.SetFloat("maxCloudHeight", maxCloudHeightMeters);
            rayMarchMaterial.SetInt("maxRayVisibilityDist", maxRayVisibilityDist);

            rayMarchMaterial.SetFloat("baseShapeSize", currentShape.baseShapeSize);
            rayMarchMaterial.SetFloat("detailSize", currentShape.detailSize);
            rayMarchMaterial.SetFloat("weatherMapSize", currentShape.weatherMapSize);

            rayMarchMaterial.SetFloat("globalCoverage", currentShape.globalCoverage);
            rayMarchMaterial.SetFloat("globalDensity", currentShape.globalDensity);

            rayMarchMaterial.SetVector("sunDir", sunLight.transform.rotation * Vector3.forward);
            rayMarchMaterial.SetFloat("absorptionC", absorptionC); 
            rayMarchMaterial.SetFloat("scatterC", scatterC);
            rayMarchMaterial.SetFloat("extintionC", absorptionC+scatterC);
            rayMarchMaterial.SetFloat("lightIntensity", sunLight.intensity * lightIntensityMult);
            rayMarchMaterial.SetFloat("ambientLightIntensity", ambientLightIntensity);


            Color[] c = new Color[2];
            Vector3[] dirs = new Vector3[2];
            dirs[0] = Vector3.up;
            dirs[1] = Vector3.down;


            RenderSettings.ambientProbe.Evaluate(dirs, c);
            List<Vector4> ambientColors = new List<Vector4>();
            ambientColors.Add(c[0]);
            ambientColors.Add(c[1]);

            rayMarchMaterial.SetVector("lightColor", sunLight.color);
            rayMarchMaterial.SetVectorArray("ambientColors", ambientColors);
            rayMarchMaterial.SetVectorArray("coneKernel", conekernel);


            rayMarchMaterial.SetVector("windDir", windDirection);
            rayMarchMaterial.SetInt("windDisplacesWeatherMap", windDisplacesWeatherMap ? 1 : 0);
            rayMarchMaterial.SetFloat("baseShapeWindMult", baseShapeWindMult);
            rayMarchMaterial.SetFloat("detailShapeWindMult", detailShapeWindMult);
            rayMarchMaterial.SetFloat("skewAmmount", skewAmmount);

            rayMarchMaterial.SetInt("mode", (int)mode);

            int planetRadiusMeters = planetRadiusKm * 1000;
            rayMarchMaterial.SetVector("planetAtmos", new Vector3(-planetRadiusMeters, planetRadiusMeters + minCloudHeightMeters, planetRadiusMeters + maxCloudHeightMeters));//Center of the planet, radius of min cloud sphere, radius of max cloud sphere
            rayMarchMaterial.SetInt("cumulusHorizon", cumulusHorizon ? 1 : 0);
            rayMarchMaterial.SetVector("cumulusHorizonGradient", cumulusHorizonGradient);
            rayMarchMaterial.SetVector("cloudLayerGradient1", cloudLayerGradient1);
            rayMarchMaterial.SetVector("cloudLayerGradient2", cloudLayerGradient2);
            rayMarchMaterial.SetVector("cloudLayerGradient3", cloudLayerGradient3);


            int lutBufferSize = 256;
            CreateLUTBuffer(lutBufferSize, ref densityCurve, "densityCurveBuffer");
            rayMarchMaterial.SetInt("densityCurveBufferSize", lutBufferSize);

            Graphics.Blit(source, destination, rayMarchMaterial);

            ProceduralTextureViewer.DeleteComputeBuffers(ref toDeleteCompBuffers);
        }


        private Matrix4x4 CamFrustrumFromCam(Camera cam)
        {
            Matrix4x4 frustum = Matrix4x4.identity;
            float foV = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);

            Vector3 goUp = Vector3.up * foV;
            Vector3 goRight = Vector3.right * foV * cam.aspect;


            Vector3 TL = (-Vector3.forward - goRight + goUp); //TOP LEFT CORNER
            Vector3 TR = (-Vector3.forward + goRight + goUp); //TOP RIGHT CORNER
            Vector3 BR = (-Vector3.forward + goRight - goUp); //BOTTOM RIGHT CORNER
            Vector3 BL = (-Vector3.forward - goRight - goUp); //BOTTOM LEFT CORNER

            frustum.SetRow(0, TL);
            frustum.SetRow(1, TR);
            frustum.SetRow(2, BR);
            frustum.SetRow(3, BL);

            return frustum;
        }

        private List<Vector4> GenerateConeKernels()
        {
            List<Vector4> newList = new List<Vector4>();
            for (int i = 0; i < 6; ++i)
            {
                newList.Add(Random.onUnitSphere);
            }

            return newList;
        }


        void CreateLUTBuffer(int samples, ref AnimationCurve curve, string name)
        {
            List<float> fTest = DensityGradientLutFromCurve(ref curve, samples);
            ComputeBuffer newBuff = ProceduralTextureViewer.CreateComputeBuffer(ref toDeleteCompBuffers, sizeof(float), fTest.ToArray());
            rayMarchMaterial.SetBuffer(name, newBuff);
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

        private void OnEnable()
        {
            _camera.depthTextureMode = DepthTextureMode.Depth;
        }

        public void GenerateWM(ref RenderTexture newWM)
        {
            ProceduralTextureViewer.GenerateWeatherMap(256, ref noiseGen.computeShader, ref newWM, ref toDeleteCompBuffers, seedWM, preset);
        }

        public void OnDisable() //happens before a hot reload
        {
            CleanUp();
        }



        public void OnApplicationQuit()
        {
            CleanUp();
        }

        void CleanUp()
        {

        }

        public void Update()
        {
            if (_camera == Camera.main && transitioning)
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

        void SetShapeParams(ref CloudShape myShape)//TODO in the future it will generate parameters from a preset
        {
            myShape.baseShapeSize = 10000.0f;
            myShape.detailSize = 1500.0f;
            myShape.globalCoverage = 0.5f;
            myShape.globalDensity = 0.01f;
            //myShape.weatherMap = GenerateWeatherMap() //TODO
            myShape.weatherMapSize = 36000.0f;
        }


    }



}
