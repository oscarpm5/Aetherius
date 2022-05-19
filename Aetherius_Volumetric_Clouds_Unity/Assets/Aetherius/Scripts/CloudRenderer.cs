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
        public Material _material;

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

            if (rayMarchMaterial == null || _cloudManager == null)
            {
                Graphics.Blit(source, destination);
                Debug.LogWarning("Couldn't render clouds. Check that the script has both a shader and a cloud manager assigned.");
                return;
            }


            rayMarchMaterial.SetTexture("_MainTex", source); //input the rendered camera texture 

            Material mat = rayMarchMaterial; //TODO this may cause problems?
            _cloudManager.SetMaterialProperties(ref mat);

            Graphics.Blit(source, destination, rayMarchMaterial);

            _cloudManager.textureGenerator.DeleteComputeBuffers();
        }

    }
}
