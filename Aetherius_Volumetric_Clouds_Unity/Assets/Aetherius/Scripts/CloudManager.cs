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
        public Vector2 cumulusHorizonGradient = new Vector2(18000,75000);
        //WS Cloud Layers
        public Vector4 cloudLayerGradient1 = new Vector4(0.0f, 0.05f, 0.1f, 0.3f);
        public Vector4 cloudLayerGradient2 = new Vector4(0.2f, 0.3f, 0.3f, 0.45f);
        public Vector4 cloudLayerGradient3 = new Vector4(0.0f, 0.1f, 0.7f, 1.0f);
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

        private void Awake()
        {
            conekernel = GenerateConeKernels();
        }


        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
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

        //TODO pass the preset as an argument in the future instead of picking the one in the class
        public void StartWMTransition(int duration=-1)//If duration = -1 interpret this as no argument
        {
            if (transitioning) //if a transition was ocurring already generate a texture with the status of the transition currently and lerp with that
            {
                ProceduralTextureViewer.LerpWM(256, ref noiseGen.computeShader, ref proceduralWM, ref proceduralWMNew, Mathf.Clamp01(currentTransitionTimeWM / transitionTimeWM), ref toDeleteCompBuffers);
                Debug.Log("Changing Transition on the fly!");

            }

            if(duration!=-1)
            {
                transitionTimeWM = duration;
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

        public void CreateLUTBuffer(int samples, ref AnimationCurve curve, string name)
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

        public void SetMaterialProperties(ref Material mat)
        {
            //TODO
        }

    }
}