using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    public class TextureDisplay : MonoBehaviour
    {
        public TextureGenerator myGenerator;
        
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
        [SerializeField, HideInInspector]
        private Material _material;



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

        public RenderTexture GetTexture(TEXTURE_TYPE type)
        {
            return (type == TEXTURE_TYPE.BASE_SHAPE) ? myGenerator._baseShapeRenderTexture : myGenerator._detailRenderTexture;
        }

        public WorleySettings activeWorleySettings
        {
            get
            {
                WorleySettings[] currSettings = (displayType == TEXTURE_TYPE.BASE_SHAPE) ? myGenerator.worleyShapeSettings : myGenerator.worleyDetailSettings;
                if (currSettings.Length <= (int)displayChannel) //this is to avoid out of bounds error case when we use Alpha channel on detail texture which has only RGB channels
                {
                    return null;
                }
                return currSettings[(int)displayChannel];
            }
        }


        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null || !displayTexture)
            {
                Graphics.Blit(source, destination);
                return;
            }


            material.SetTexture("_DisplayTex3D", GetTexture(displayType)); //input the procedural texture
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
