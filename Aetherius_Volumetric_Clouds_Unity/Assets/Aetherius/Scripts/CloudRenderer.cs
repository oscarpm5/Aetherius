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
        [HideInInspector]
        public Material _material;
        [HideInInspector]
        public Material _displayMaterial;

        [SerializeField]
        private CloudManager _cloudManager;

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

        public Material displayMaterial
        {
            get
            {
                if (!_displayMaterial && _displayToScreen)
                {
                    _displayMaterial = new Material(_displayToScreen);
                    _displayMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                return _displayMaterial;
            }
        }

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

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            cam.depthTextureMode = DepthTextureMode.Depth;

            if (rayMarchMaterial == null || displayMaterial == null || _cloudManager == null)
            {
                Graphics.Blit(source, destination);
                Debug.LogWarning("Couldn't render clouds. Check that the script has both a shader and a cloud manager assigned.");
                return;
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

            Graphics.Blit(source, destination, displayMaterial);

            _cloudManager.textureGenerator.DeleteComputeBuffers();

            if (halfResTexture != null)
            {
                halfResTexture.Release();
            }
        }

    }
}
