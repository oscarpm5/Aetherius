using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    [ExecuteInEditMode]
    public class TextureDisplay : MonoBehaviour
    {
        public CloudManager cloudManager;

        public TEXTURE_CHANNEL displayChannel;
        public TEXTURE_TYPE displayType;
        public Shader displayPreviewShader;
        public bool displayTexture = false;
        public bool displayGrayscale = false;
        public bool displayAllChannels = false;
        [Range(0.0f, 1.0f)]
        public float debugDisplaySize = 0.5f;
        [Range(1, 5)]
        public float tileAmmount = 1;
        [Range(0, 8)]
        public int textureLOD = 0;
        [Range(0.0f, 1.0f)]
        public float textureSlice = 1.0f;
        private Material _material;

        public ref TextureGenerator textureGenerator
        {
            get
            {
                if(cloudManager==null)
                {
                    cloudManager = GetComponent<CloudManager>();

                }
                return ref cloudManager.textureGenerator;
            }
        }

        public void OnEnable()
        {
            if (cloudManager == null)
            {
                cloudManager = GetComponent<CloudManager>();              
            }
        }

        public Material material
        {
            get
            {
                if (!_material && displayPreviewShader)
                {
                    _material = new Material(displayPreviewShader);
                    _material.hideFlags = HideFlags.HideAndDontSave;
                }
                return _material;
            }
        }


        public WorleySettings activeWorleySettings
        {
            get
            {
                WorleySettings[] currSettings = (displayType == TEXTURE_TYPE.BASE_SHAPE) ? textureGenerator.worleyShapeSettings : textureGenerator.worleyDetailSettings;
                if (currSettings.Length <= (int)displayChannel) //this is to avoid out of bounds error case when we use Alpha channel on detail texture which has only RGB channels
                {
                    return null;
                }
                return currSettings[(int)displayChannel];
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                Debug.Log("material null! on: "+ gameObject.name.ToString());
            }

            if (material == null || !displayTexture)
            {
                Graphics.Blit(source, destination);
                return;
            }

            material.SetTexture("_DisplayTex3D", textureGenerator.GetTexture(displayType)); //input the procedural texture
            material.SetFloat("slice3DTex", textureSlice);
            material.SetFloat("debugTextureSize", debugDisplaySize);
            material.SetFloat("tileAmmount", tileAmmount);
            material.SetVector("channelMask", TextureGenerator.GetChannelMask(displayChannel));
            material.SetInt("displayGrayscale", displayGrayscale ? 1 : 0);
            material.SetInt("displayAllChannels", displayAllChannels ? 1 : 0);
            material.SetInt("isDetail", (int)displayType);//0 if base shape, 1 if detail

            material.SetInt("lod", textureLOD);

            Graphics.Blit(source, destination, material);
        }
       

    }
}
