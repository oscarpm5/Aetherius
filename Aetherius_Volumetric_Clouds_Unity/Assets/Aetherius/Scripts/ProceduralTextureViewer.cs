using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{

    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    [ExecuteInEditMode]
    public class ProceduralTextureViewer : MonoBehaviour
    {
        public enum TEXTURE_CHANNEL
        {
            R,
            G,
            B,
            A
        }

        public enum TEXTURE_TYPE
        {
            BASE_SHAPE,
            DETAIL
        }

        public enum TEXTURE_DIMENSIONS
        {
            TEX_2D,
            TEX_3D
        }

        //Display shader
        [Header("Texture Display")]
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



        [Range(0.0f, 1.0f)]
        public float textureSlice = 1.0f;
        [SerializeField, HideInInspector]
        private Material _material;
        //Compute shader
        [Header("Texture Generation")]
        public bool updateTextureAuto = false;
        [HideInInspector]
        [Range(8, 512)]
        public int baseShapeResolution = 256;
        [HideInInspector]
        [Range(8, 512)]
        public int detailResolution = 32;

        public ComputeShader computeShader = null;
        public ImprovedPerlinSettings perlinShapeSettings;
        public WorleySettings[] worleyShapeSettings = new WorleySettings[4];
        public WorleySettings[] worleyDetailSettings = new WorleySettings[3];
        [SerializeField, HideInInspector]
        private bool _updateNoise;
        [SerializeField, HideInInspector]
        private List<ComputeBuffer> buffersToDelete;
        [SerializeField, HideInInspector]
        private RenderTexture _baseShapeRenderTexture = null;
        [SerializeField, HideInInspector]
        private RenderTexture _detailRenderTexture = null;

        //Utility =================================================================================
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
        public Vector4 channelMask
        {
            get
            {
                return GetChannelMask(displayChannel);
            }
        }
        public RenderTexture activeTexture
        {
            get
            {
                return (displayType == TEXTURE_TYPE.BASE_SHAPE) ? _baseShapeRenderTexture : _detailRenderTexture;
            }
        }
        public WorleySettings activeWorleySettings
        {
            get
            {
                WorleySettings[] currSettings = (displayType == TEXTURE_TYPE.BASE_SHAPE) ? worleyShapeSettings : worleyDetailSettings;
                if (currSettings.Length <= (int)displayChannel) //this is to avoid out of bounds error case when we use Alpha channel on detail texture which has only RGB channels
                {
                    return null;
                }
                return currSettings[(int)displayChannel];
            }
        }
        public static Vector4 GetChannelMask(TEXTURE_CHANNEL channel)
        {
            Vector4 ret = new Vector4();
            ret.x = (channel == TEXTURE_CHANNEL.R) ? 1 : 0;
            ret.y = (channel == TEXTURE_CHANNEL.G) ? 1 : 0;
            ret.z = (channel == TEXTURE_CHANNEL.B) ? 1 : 0;
            ret.w = (channel == TEXTURE_CHANNEL.A) ? 1 : 0;
            return ret;
        }

        public RenderTexture GetTexture(TEXTURE_TYPE type)
        {
            return (type == TEXTURE_TYPE.BASE_SHAPE) ? _baseShapeRenderTexture : _detailRenderTexture;
        }

        //Unity methods ===========================================================================
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            UpdateNoise();


            if (material == null || !displayTexture)
            {
                Graphics.Blit(source, destination);
                return;
            }

            material.SetTexture("_DisplayTex3D", activeTexture); //input the procedural texture
            material.SetFloat("slice3DTex", textureSlice);
            material.SetFloat("debugTextureSize", debugDisplaySize);
            material.SetFloat("tileAmmount", tileAmmount);
            material.SetVector("channelMask", channelMask);
            material.SetInt("displayGrayscale", displayGrayscale ? 1 : 0);
            material.SetInt("displayAllChannels", displayAllChannels ? 1 : 0);
            material.SetInt("isDetail", (int)displayType);//0 if base shape, 1 if detail

            Graphics.Blit(source, destination, material);
        }
        public void OnEnable() //Called after component awakens and after a hot reload
        {
            GenerateAllNoise();
        }
        public void OnDisable() //happens before a hot reload
        {
            DeleteComputeBuffers(ref buffersToDelete);
            ReleaseTexture(ref _baseShapeRenderTexture);
        }


        //Generation methods ======================================================================
        public void Generate3DWorley(int dimensions, ref RenderTexture targetTexture, TEXTURE_CHANNEL channelToWriteTo, TEXTURE_TYPE type)
        {
            int dim = Mathf.Max(dimensions, 8);
            if (computeShader == null)
                return;

            WorleySettings[] settings = (type == TEXTURE_TYPE.BASE_SHAPE) ? worleyShapeSettings : worleyDetailSettings;
            WorleySettings currSettings = settings[(int)channelToWriteTo];
            if (currSettings == null)
            {
                Debug.LogWarning("Assign a Worley Settings Scriptable Object to the [" + ((int)channelToWriteTo).ToString() + "] element of the 'Worley Settings' array");
                return;
            }
            string kernelName = "Worley3DTextureWithPoints";
            GeneratePointsWorley(currSettings, kernelName);

            int currKernel = computeShader.FindKernel(kernelName);
            computeShader.SetTexture(currKernel, "Result3D", targetTexture);
            computeShader.SetInt("textureSizeP", dim);
            computeShader.SetInt("numCellsA", currSettings.numberOfCellsAxisA);
            computeShader.SetInt("numCellsB", currSettings.numberOfCellsAxisB);
            computeShader.SetInt("numCellsC", currSettings.numberOfCellsAxisC);
            computeShader.SetFloat("persistenceWorley", currSettings.persistence); //less than 1
            computeShader.SetVector("channelToWriteTo", GetChannelMask(channelToWriteTo));

            computeShader.Dispatch(currKernel, dim / 8, dim / 8, dim / 8); //Image size divided by the thread size of each group

        }
        void Generate3DPerlinWorley(int dimensions, ref RenderTexture targetTexture, TEXTURE_CHANNEL channelToWriteTo, TEXTURE_TYPE type)
        {
            int dim = Mathf.Max(dimensions, 8);
            if (computeShader == null)
                return;

            WorleySettings[] settings = (type == TEXTURE_TYPE.BASE_SHAPE) ? worleyShapeSettings : worleyDetailSettings;
            WorleySettings currSettings = settings[(int)channelToWriteTo];

            if (currSettings == null)
            {
                Debug.LogWarning("Assign a Worley Settings Scriptable Object to the [" + ((int)channelToWriteTo).ToString() + "] element of the 'Worley Settings' array");
                return;
            }
            if (perlinShapeSettings == null)
            {
                Debug.LogWarning("Assign an Improved Perlin Settings Scriptable Object to the 'Perlin Shape Settings' slot");
                return;
            }

            string kernelName = "PerlinWorley3DTexture";
            int currKernel = computeShader.FindKernel(kernelName);

            //Worley
            GeneratePointsWorley(currSettings, kernelName);
            computeShader.SetInt("numCellsA", currSettings.numberOfCellsAxisA);
            computeShader.SetInt("numCellsB", currSettings.numberOfCellsAxisB);
            computeShader.SetInt("numCellsC", currSettings.numberOfCellsAxisC);
            computeShader.SetFloat("persistenceWorley", currSettings.persistence); //less than 1

            //Perlin
            GenerateCornerVectors(kernelName);
            GeneratePermutationTable(256, perlinShapeSettings.seed, "permTable", kernelName);
            computeShader.SetInt("gridSize", perlinShapeSettings.gridSizePerlin);
            computeShader.SetInt("octaves", perlinShapeSettings.numOctavesPerlin);
            computeShader.SetFloat("persistencePerlin", perlinShapeSettings.persistencePerlin); //less than 1
            computeShader.SetFloat("lacunarity", perlinShapeSettings.lacunarityPerlin); //More than 1


            computeShader.SetTexture(currKernel, "Result3D", targetTexture);
            computeShader.SetInt("textureSizeP", dim);
            computeShader.SetVector("channelToWriteTo", GetChannelMask(channelToWriteTo));

            computeShader.Dispatch(currKernel, dim / 8, dim / 8, dim / 8); //Image size divided by the thread size of each group


        }
        public void GenerateAllNoise()
        {
            _updateNoise = false;
            GenerateBaseShapeNoise();
            GenerateDetailNoise();
        }
        public void GenerateBaseShapeNoise()
        {
            //Base Shape Texture
            GenerateRenderTexture(baseShapeResolution, ref _baseShapeRenderTexture,TEXTURE_DIMENSIONS.TEX_3D, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm);
            Generate3DPerlinWorley(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_CHANNEL.R, TEXTURE_TYPE.BASE_SHAPE);
            Generate3DWorley(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_CHANNEL.G, TEXTURE_TYPE.BASE_SHAPE);
            Generate3DWorley(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_CHANNEL.B, TEXTURE_TYPE.BASE_SHAPE);
            Generate3DWorley(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_CHANNEL.A, TEXTURE_TYPE.BASE_SHAPE);
            DeleteComputeBuffers(ref buffersToDelete);

        }
        public void GenerateDetailNoise()
        {
            //Detail Texture
            GenerateRenderTexture(detailResolution, ref _detailRenderTexture, TEXTURE_DIMENSIONS.TEX_3D, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm);
            Generate3DWorley(detailResolution, ref _detailRenderTexture, TEXTURE_CHANNEL.R, TEXTURE_TYPE.DETAIL);
            Generate3DWorley(detailResolution, ref _detailRenderTexture, TEXTURE_CHANNEL.G, TEXTURE_TYPE.DETAIL);
            Generate3DWorley(detailResolution, ref _detailRenderTexture, TEXTURE_CHANNEL.B, TEXTURE_TYPE.DETAIL);
            DeleteComputeBuffers(ref buffersToDelete);
        }

        //Update methods ==========================================================================
        public void UpdateNoise()
        {
            if (_updateNoise == true)
            {
                _updateNoise = false;

                if (displayType == TEXTURE_TYPE.BASE_SHAPE) //We only update the texture that is being displayed as is the one being edited
                {
                    if (GenerateRenderTexture(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_DIMENSIONS.TEX_3D, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm))
                    {
                        GenerateBaseShapeNoise();
                        return;
                    }


                    if (displayChannel == TEXTURE_CHANNEL.R)
                    {
                        Generate3DPerlinWorley(_baseShapeRenderTexture.height, ref _baseShapeRenderTexture, displayChannel, displayType);
                    }
                    else
                    {
                        Generate3DWorley(_baseShapeRenderTexture.height, ref _baseShapeRenderTexture, displayChannel, displayType);
                    }


                }
                else
                {
                    if (GenerateRenderTexture(detailResolution, ref _detailRenderTexture, TEXTURE_DIMENSIONS.TEX_3D, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm))
                    {
                        GenerateDetailNoise();
                        return;
                    }

                    Generate3DWorley(_detailRenderTexture.height, ref _detailRenderTexture, displayChannel, displayType);
                }
            }
        }
        public void UpdateNoiseManually()
        {
            GenerateAllNoise();
        }
        public void NoiseSettingsChanged()
        {
            if (updateTextureAuto)
            {
                _updateNoise = true;
            }
        }
        public void ValidateUpdate()
        {
            if (!updateTextureAuto)
            {
                _updateNoise = false;
            }
        }

        //Worley Related ==========================================================================
        void GeneratePointsWorley(WorleySettings currSettings, string kernelName)
        {
            System.Random random = new System.Random(currSettings.seed);

            GeneratePoints(random, currSettings.numberOfCellsAxisA, "pointsA", kernelName);
            GeneratePoints(random, currSettings.numberOfCellsAxisB, "pointsB", kernelName);
            GeneratePoints(random, currSettings.numberOfCellsAxisC, "pointsC", kernelName);
        }
        void GeneratePoints(System.Random rand, int numberOfCellsAxis, string bufferName, string kernelName)
        {
            int totalNumOfCells = numberOfCellsAxis * numberOfCellsAxis * numberOfCellsAxis;//3D grid
            Vector3[] points = new Vector3[totalNumOfCells];
            for (int i = 0; i < totalNumOfCells; ++i)
            {
                points[i] = new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
            }

            CreateComputeBuffer(ref buffersToDelete,ref computeShader,sizeof(float) * 3, points, bufferName, kernelName);
        }

        //Improved Perlin Related =================================================================
        void GeneratePermutationTable(int size, int seed, string bufferName, string kernelName)
        {
            System.Random rand = new System.Random(seed);

            int[] permutation = new int[size];

            for (int i = 0; i < size; ++i)
            {
                permutation[i] = i;
            }

            for (int i = size - 1; i > 0; --i)
            {
                int index = Mathf.RoundToInt((float)rand.NextDouble() * (i - 1));
                int aux = permutation[i];
                permutation[i] = permutation[index];
                permutation[index] = aux;
            }

            CreateComputeBuffer(ref buffersToDelete, ref computeShader, sizeof(int), permutation, bufferName, kernelName);
        }
        void GenerateCornerVectors(string kernelName)
        {
            Vector3[] directions = new Vector3[12];
            directions[0] = new Vector3(1, 1, 0);
            directions[1] = new Vector3(-1, 1, 0);
            directions[2] = new Vector3(1, -1, 0);
            directions[3] = new Vector3(-1, -1, 0);
            directions[4] = new Vector3(1, 0, 1);
            directions[5] = new Vector3(-1, 0, 1);
            directions[6] = new Vector3(1, 0, -1);
            directions[7] = new Vector3(-1, 0, -1);
            directions[8] = new Vector3(0, 1, 1);
            directions[9] = new Vector3(0, -1, 1);
            directions[10] = new Vector3(0, 1, -1);
            directions[11] = new Vector3(0, -1, -1);

            CreateComputeBuffer(ref buffersToDelete, ref computeShader, sizeof(float) * 3, directions, "vecTable", kernelName);
        }

        //Compute Buffers =========================================================================
        
        public static ComputeBuffer CreateComputeBuffer(ref List<ComputeBuffer> toDeleteList,ref ComputeShader compShader, int dataStride, System.Array data, string bufferName, string kernelName)
        {
            ComputeBuffer newBuffer = new ComputeBuffer(data.Length, dataStride, ComputeBufferType.Structured);
            newBuffer.SetData(data);
            compShader.SetBuffer(compShader.FindKernel(kernelName), bufferName, newBuffer);

            if (toDeleteList == null)
                toDeleteList = new List<ComputeBuffer>();

            toDeleteList.Add(newBuffer);
            return newBuffer;
        }
        public static void DeleteComputeBuffers(ref List<ComputeBuffer> toDeleteList)
        {
            if (toDeleteList == null)
                return;

            foreach (ComputeBuffer currentBuffer in toDeleteList)
            {
                currentBuffer.Release();
            }
            toDeleteList = null;
        }

        

        //Textures ================================================================================
        //returns ture if a texture has been created
        //bool GenerateTexture3D(int texResolution, ref RenderTexture myTexture, UnityEngine.Experimental.Rendering.GraphicsFormat format)
        //{
        //    bool isNewlyCreated = false;
        //    int res = Mathf.Max(texResolution, 8);
        //    if (myTexture == null || !myTexture.IsCreated() || myTexture.height != res || myTexture.width != res || myTexture.volumeDepth != res || myTexture.graphicsFormat != format) //if texture doesnt exist or resolution has changed, recreate the texture
        //    {
        //        //Debug.Log("GeneratingTexture...");

        //        if (myTexture != null)
        //        {
        //            //Debug.Log("Deleting previous texture...");
        //            myTexture.Release();
        //            myTexture = null;
        //        }

        //        myTexture = new RenderTexture(res, res, 0);
        //        myTexture.enableRandomWrite = true;//So it can be used by the compute shader
        //        myTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        //        myTexture.volumeDepth = res;
        //        myTexture.filterMode = FilterMode.Trilinear;
        //        myTexture.wrapMode = TextureWrapMode.Repeat;
        //        myTexture.graphicsFormat = format;
        //        myTexture.Create();

        //        isNewlyCreated = true;

        //    }
        //    return isNewlyCreated;
        //}
        public static void ReleaseTexture(ref RenderTexture textureToRelease)
        {
            if (textureToRelease != null)
            {
                textureToRelease.Release();
                textureToRelease = null;
            }
        }


        public static bool GenerateRenderTexture(int texResolution, ref RenderTexture myTexture, TEXTURE_DIMENSIONS dimensions, UnityEngine.Experimental.Rendering.GraphicsFormat format,FilterMode filterMode = FilterMode.Trilinear)
        {
            bool isNewlyCreated = false;
            int res = Mathf.Max(texResolution, 8);
            if (myTexture == null || !myTexture.IsCreated() || myTexture.height != res || myTexture.width != res || myTexture.volumeDepth != res || myTexture.graphicsFormat != format) //if texture doesnt exist or resolution has changed, recreate the texture
            {
                //Debug.Log("GeneratingTexture...");

                if (myTexture != null)
                {
                    //Debug.Log("Deleting previous texture...");
                    myTexture.Release();
                    myTexture = null;
                }

                myTexture = new RenderTexture(res, res, 0);
                myTexture.enableRandomWrite = true;//So it can be used by the compute shader

                switch (dimensions)
                {
                    case TEXTURE_DIMENSIONS.TEX_2D:
                        {
                            myTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
                        }
                        break;
                    case TEXTURE_DIMENSIONS.TEX_3D:
                        {
                            myTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                            myTexture.volumeDepth = res;
                        }
                        break;
                }

                myTexture.filterMode = filterMode;
                myTexture.wrapMode = TextureWrapMode.Repeat;
                myTexture.graphicsFormat = format;
                myTexture.Create();

                isNewlyCreated = true;

            }
            return isNewlyCreated;
        }

    }

}