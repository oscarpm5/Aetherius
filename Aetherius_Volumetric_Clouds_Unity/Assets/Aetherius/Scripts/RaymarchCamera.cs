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

        private Camera _cam;

        public CLOUD_CONTROL mode = CLOUD_CONTROL.SIMPLE;


        public CloudShape simple;
        public CloudShape advanced;

        [SerializeField, HideInInspector]
        private List<ComputeBuffer> toDeleteCompBuffers;

        [SerializeField]
        private Shader _shader;
        [SerializeField, HideInInspector]
        private Material _material;
        [Header("Ray March")]
        

        public Texture2D blueNoise;
        [Header("Weather Map")]
        public Texture2D weatherMap;
        public bool windDisplacesWeatherMap = true;
        public RenderTexture proceduralWM;
        public int seedWM = 307;
        [HideInInspector]
        public bool cumulusHorizon = false;
        [HideInInspector]
        public Vector2 cumulusHorizonGradient;

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
        [Range(0.0f, 10.0f)]
        public float lightAbsorption = 1.0f;
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
            if(proceduralWM==null)
            {
                GenerateWM();
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

            rayMarchMaterial.SetMatrix("_CamFrustum", CamFrustrumFromCam(_camera));
            rayMarchMaterial.SetMatrix("_CamToWorldMat", _camera.cameraToWorldMatrix);

            rayMarchMaterial.SetTexture("_MainTex", source); //input the rendered camera texture 
            rayMarchMaterial.SetTexture("baseShapeTexture", noiseGen.GetTexture(ProceduralTextureViewer.TEXTURE_TYPE.BASE_SHAPE));
            rayMarchMaterial.SetTexture("detailTexture", noiseGen.GetTexture(ProceduralTextureViewer.TEXTURE_TYPE.DETAIL));
            rayMarchMaterial.SetTexture("weatherMapTexture", GetProceduralWM());
            rayMarchMaterial.SetTexture("blueNoiseTexture", blueNoise);


            rayMarchMaterial.SetFloat("minCloudHeight", minCloudHeightMeters);
            rayMarchMaterial.SetFloat("maxCloudHeight", maxCloudHeightMeters);
            
            rayMarchMaterial.SetFloat("baseShapeSize", currentShape.baseShapeSize);
            rayMarchMaterial.SetFloat("detailSize", currentShape.detailSize);
            rayMarchMaterial.SetFloat("weatherMapSize", currentShape.weatherMapSize);
            
            rayMarchMaterial.SetFloat("globalCoverage", currentShape.globalCoverage);
            rayMarchMaterial.SetFloat("globalDensity", currentShape.globalDensity);
            
            rayMarchMaterial.SetVector("sunDir", sunLight.transform.rotation * Vector3.forward);      
            rayMarchMaterial.SetFloat("lightAbsorption", lightAbsorption);
            rayMarchMaterial.SetFloat("lightIntensity", sunLight.intensity * lightIntensityMult);


            Color[] c = new Color[6];
            Vector3[] dirs = new Vector3[6];
            dirs[0] = sunLight.transform.rotation * Vector3.forward;
            dirs[1] = sunLight.transform.rotation * Vector3.back;
            dirs[2] = sunLight.transform.rotation * Vector3.right;
            dirs[3] = sunLight.transform.rotation * Vector3.left;
            dirs[4] = sunLight.transform.rotation * Vector3.up;
            dirs[5] = sunLight.transform.rotation * Vector3.down;

            RenderSettings.ambientProbe.Evaluate(dirs, c);
            Color ambientCol = new Color(0.0f, 0.0f, 0.0f);
            for (int i = 0; i < c.Length; i++)
            {
                ambientCol += c[i];
            }

            ambientCol /= (float)c.Length;

            rayMarchMaterial.SetVector("lightColor", sunLight.color);
            rayMarchMaterial.SetVector("ambientColor", ambientCol);
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

            int lutBufferSize = 256;
            CreateLUTBuffer(lutBufferSize, ref densityCurve, "densityCurveBuffer");
            rayMarchMaterial.SetInt("densityCurveBufferSize", lutBufferSize);

            //Create a screen quad
            RenderTexture.active = destination;
            GL.PushMatrix();
            GL.LoadOrtho();
            rayMarchMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            //Bottom Left
            GL.MultiTexCoord2(0, 0.0f, 0.0f);
            GL.Vertex3(0.0f, 0.0f, 3.0f);
            //Bottom Right
            GL.MultiTexCoord2(0, 1.0f, 0.0f);
            GL.Vertex3(1.0f, 0.0f, 2.0f);
            //Top Right
            GL.MultiTexCoord2(0, 1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            //Top Left
            GL.MultiTexCoord2(0, 0.0f, 1.0f);
            GL.Vertex3(0.0f, 1.0f, 0.0f);

            GL.End();
            GL.PopMatrix();



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

        public void GenerateWM()
        {
            ProceduralTextureViewer.GenerateWeatherMap(256, ref noiseGen.computeShader, ref proceduralWM, ref toDeleteCompBuffers, seedWM);
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
