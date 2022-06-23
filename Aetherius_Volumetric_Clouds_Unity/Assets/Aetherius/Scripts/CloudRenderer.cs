using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    [ExecuteInEditMode]
    public class CloudRenderer : MonoBehaviour
    {

        private Camera _cam;
        public Shader _shader;
        public Shader _displayToScreen;


        [SerializeField]
        private CloudManager _cloudManager;
        [HideInInspector]
        public Material rayMarchMaterial;

        [HideInInspector]
        public Material displayMaterial;

        public Camera cam
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

        [ImageEffectOpaque]
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            cam.depthTextureMode = DepthTextureMode.Depth;



            if (_shader == null || _displayToScreen == null || _cloudManager == null)
            {
                Graphics.Blit(source, destination);
                Debug.LogWarning("Couldn't render clouds. Check that the script has both a shader and a cloud manager assigned.");
                return;
            }

            if (rayMarchMaterial==null || rayMarchMaterial.shader!=_shader)
            {
                rayMarchMaterial = new Material(_shader);
            }

            if (displayMaterial == null || displayMaterial.shader != _displayToScreen)
            {
                displayMaterial = new Material(_displayToScreen);
            }






            RenderTexture halfResTexture = null;
            int resDecrease = (int)Mathf.Pow(2, (int)_cloudManager.resolution);
            Vector2Int halfRes = new Vector2Int(source.width / resDecrease, source.height / resDecrease);
            {
                halfResTexture = new RenderTexture(halfRes.x, halfRes.y, 0);
                halfResTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
                halfResTexture.filterMode = FilterMode.Trilinear;
                halfResTexture.useMipMap = false;
                halfResTexture.autoGenerateMips = false;
                halfResTexture.wrapMode = TextureWrapMode.Clamp;
                halfResTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
                halfResTexture.Create();
            }

            rayMarchMaterial.SetTexture("_MainTex", source); //input the rendered camera texture 

            Material mat = rayMarchMaterial; //TODO this may cause problems?
            _cloudManager.SetMaterialProperties(ref mat, halfRes);




            Graphics.Blit(source, halfResTexture, rayMarchMaterial);


            displayMaterial.SetTexture("_MainTex", source); //input the rendered camera texture 
            displayMaterial.SetTexture("cloudTex", halfResTexture); //input the rendered camera texture 
            displayMaterial.SetVector("texelRes", new Vector2(1.0f / halfRes.x, 1.0f / halfRes.y));
            SetResolutionParameters(_cloudManager.resolution);



            Graphics.Blit(source, destination, displayMaterial);

            _cloudManager.textureGenerator.DeleteComputeBuffers();

            if (halfResTexture != null)
            {
                halfResTexture.Release();
            }
        }

        void SetResolutionParameters(CLOUD_RESOLUTION resolution)
        {
            bool useBlur = false;
            int kernelHalfDim = 0;
            switch (resolution)
            {
                case CLOUD_RESOLUTION.ORIGINAL:
                    {
                        useBlur = false;
                        kernelHalfDim = 0;
                    }
                    break;
                case CLOUD_RESOLUTION.HALF:
                    {
                        useBlur = true;
                        kernelHalfDim = 1;
                    }
                    break;
                case CLOUD_RESOLUTION.QUARTER:
                    {
                        useBlur = true;
                        kernelHalfDim = 1;
                    }
                    break;
            }

            displayMaterial.SetInt("useBlur", useBlur ? 1 : 0);
            displayMaterial.SetInt("kernelHalfDim", kernelHalfDim);

        }
    }
}
