using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    public class TextureGenerator : MonoBehaviour
    {

        public bool updateTextureAuto = false;
        [Range(8, 512)]
        public int baseShapeResolution = 256;
        [Range(8, 512)]
        public int detailResolution = 32;
        
        public ComputeShader computeShader = null;
        public ImprovedPerlinSettings perlinShapeSettings;
        public WorleySettings[] worleyShapeSettings = new WorleySettings[4];
        public WorleySettings[] worleyDetailSettings = new WorleySettings[3];

        public List<ComputeBuffer> buffersToDelete;
        public RenderTexture _baseShapeRenderTexture = null;
        public RenderTexture _detailRenderTexture = null;


        public static Vector4 GetChannelMask(TEXTURE_CHANNEL channel)
        {
            Vector4 ret = new Vector4();
            ret.x = (channel == TEXTURE_CHANNEL.R) ? 1 : 0;
            ret.y = (channel == TEXTURE_CHANNEL.G) ? 1 : 0;
            ret.z = (channel == TEXTURE_CHANNEL.B) ? 1 : 0;
            ret.w = (channel == TEXTURE_CHANNEL.A) ? 1 : 0;
            return ret;
        }


        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}