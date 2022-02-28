using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{

    [RequireComponent(typeof(Camera))]
    [ExecuteInEditMode]
    [ImageEffectAllowedInSceneView]
    public class RaymarchCamera : MonoBehaviour
    {
        private Camera _cam;

        [SerializeField]
        private Shader _shader;
        private Material _material;
        [Header("Ray March")]
        [Range(32, 1024)]
        public int maxSteps = 256;
        public float maxRayDist = 500.0f;

        [Header("Noise")]
        public float baseShapeSize = 1.0f;
        public float detailSize = 1.0f;
        [Header("Weather Map")]
        public Texture2D weatherMap;
        public float weatherMapSize = 1.0f;
        public Vector3 weatherMapOffset = Vector3.zero;
        [Header("Cloud")]
        public Light sunLight;
        [Range(0.0f, 1.0f)]
        public float globalCoverage = 0.5f;
        public float globalDensity = 1.0f;
        public float minCloudHeight = 250.0f;
        public float maxCloudHeight = 250.0f;

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
        private void OnEnable()
        {
            noiseGen = GetComponent<ProceduralTextureViewer>();
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

            //GetComponent<ProceduralTextureViewer>().UpdateNoise();


            rayMarchMaterial.SetMatrix("_CamFrustum", CamFrustrumFromCam(_camera));
            rayMarchMaterial.SetMatrix("_CamToWorldMat", _camera.cameraToWorldMatrix);
            rayMarchMaterial.SetTexture("_MainTex", source); //input the rendered camera texture 
            rayMarchMaterial.SetInt("maxSteps", maxSteps);
            rayMarchMaterial.SetFloat("maxRayDist", maxRayDist);
            rayMarchMaterial.SetFloat("minCloudHeight", minCloudHeight);
            rayMarchMaterial.SetFloat("maxCloudHeight", maxCloudHeight);
            rayMarchMaterial.SetFloat("baseShapeSize", baseShapeSize);
            rayMarchMaterial.SetFloat("detailSize", detailSize);
            rayMarchMaterial.SetFloat("weatherMapSize", weatherMapSize);
            rayMarchMaterial.SetFloat("globalCoverage", globalCoverage);
            rayMarchMaterial.SetFloat("globalDensity", globalDensity);
            rayMarchMaterial.SetVector("sunDir", sunLight.transform.rotation * Vector3.forward);
            rayMarchMaterial.SetVector("weatherMapOffset", weatherMapOffset);
            rayMarchMaterial.SetTexture("baseShapeTexture", noiseGen.GetTexture(ProceduralTextureViewer.TEXTURE_TYPE.BASE_SHAPE));
            rayMarchMaterial.SetTexture("detailTexture", noiseGen.GetTexture(ProceduralTextureViewer.TEXTURE_TYPE.DETAIL));
            rayMarchMaterial.SetTexture("weatherMapTexture", weatherMap);

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
    }
}
