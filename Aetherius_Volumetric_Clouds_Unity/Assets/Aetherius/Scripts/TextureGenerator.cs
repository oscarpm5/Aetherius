using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    [System.Serializable]
    public class TextureGenerator 
    {


        [Range(8, 512)]
        public int baseShapeResolution = 128;
        [Range(8, 512)]
        public int detailResolution = 32;
        public ComputeShader computeShader = null;
        public ImprovedPerlinSettings perlinShapeSettings;
        public WorleySettings[] worleyShapeSettings = new WorleySettings[4];
        public WorleySettings[] worleyDetailSettings = new WorleySettings[3];

        public List<ComputeBuffer> buffersToDelete;
        public RenderTexture _baseShapeRenderTexture=null;
        public RenderTexture _detailRenderTexture=null;

        public RenderTexture originalWM = null;
        public RenderTexture newWM = null;


        public void InitializeTextures()
        {

            Utility.GenerateRenderTexture(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_DIMENSIONS.TEX_3D);
            Utility.GenerateRenderTexture(detailResolution, ref _detailRenderTexture, TEXTURE_DIMENSIONS.TEX_3D);
            Utility.GenerateRenderTexture(256, ref originalWM, TEXTURE_DIMENSIONS.TEX_2D);
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

        public ref RenderTexture GetWM(int seed,CLOUD_PRESET preset)
        {
            if (originalWM == null)
            {
                GenerateWeatherMap(256,ref originalWM,seed,preset);
            }

            return ref originalWM;
        }

      

        //Compute Buffers =========================================================================
       
        public ComputeBuffer CreateComputeBuffer(int dataStride,System.Array data)
        {
            return Utility.CreateComputeBuffer(ref buffersToDelete, dataStride, data);
        }

        private ComputeBuffer CreateComputeBuffer(int dataStride, System.Array data,string bufferName,int kernelIndex)
        {
            return Utility.CreateComputeBuffer(ref buffersToDelete, ref computeShader,dataStride, data,bufferName,kernelIndex);
        }

        public void DeleteComputeBuffers()
        {
            Utility.DeleteComputeBuffers(ref buffersToDelete);
        }

        //Compute shaders =========================================================================
        public bool DispatchComputeShader(int kernelIndex, Vector3Int textureDimensions)
        {
            bool ret = Utility.DispatchComputeShader(ref computeShader, kernelIndex, textureDimensions);
            return ret;
        }

        //Worley Related ==========================================================================
        void GeneratePointsWorley(WorleySettings currSettings, int kernelIndex)
        {
            System.Random random = new System.Random(currSettings.seed);

            GeneratePoints(random, currSettings.numberOfCellsAxisA, "pointsA", kernelIndex);
            GeneratePoints(random, currSettings.numberOfCellsAxisB, "pointsB", kernelIndex);
            GeneratePoints(random, currSettings.numberOfCellsAxisC, "pointsC", kernelIndex);
        }

        void GeneratePoints(System.Random rand, int numberOfCellsAxis, string bufferName, int kernelIndex)
        {
            int totalNumOfCells = numberOfCellsAxis * numberOfCellsAxis * numberOfCellsAxis;//3D grid
            Vector3[] points = new Vector3[totalNumOfCells];
            for (int i = 0; i < totalNumOfCells; ++i)
            {
                points[i] = new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
            }

            CreateComputeBuffer(sizeof(float) * 3, points, bufferName, kernelIndex);
        }

        void GeneratePoints2D(System.Random rand, int numberOfCellsAxis, string bufferName, int kernelIndex)
        {
            int totalNumOfCells = numberOfCellsAxis * numberOfCellsAxis;//2D grid
            Vector2[] points = new Vector2[totalNumOfCells];
            for (int i = 0; i < totalNumOfCells; ++i)
            {
                points[i] = new Vector2((float)rand.NextDouble(), (float)rand.NextDouble());
            }

            CreateComputeBuffer(sizeof(float) * 2, points, bufferName, kernelIndex);
        }
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

            int currKernel = computeShader.FindKernel("Worley3DTextureWithPoints");

            GeneratePointsWorley(currSettings, currKernel);

            computeShader.SetTexture(currKernel, "Result3D", targetTexture);
            computeShader.SetInt("textureSizeP", dim);
            computeShader.SetInt("numCellsA", currSettings.numberOfCellsAxisA);
            computeShader.SetInt("numCellsB", currSettings.numberOfCellsAxisB);
            computeShader.SetInt("numCellsC", currSettings.numberOfCellsAxisC);
            computeShader.SetFloat("persistenceWorley", currSettings.persistence); //less than 1
            computeShader.SetVector("channelToWriteTo", GetChannelMask(channelToWriteTo));

            DispatchComputeShader(currKernel, new Vector3Int(dim, dim, dim));

        }


        //Perlin-Worley Related ===================================================================

        void GeneratePermutationTable(int size, int seed, string bufferName, int kernelIndex)
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

            CreateComputeBuffer(sizeof(int), permutation, bufferName, kernelIndex);
        }

        void GenerateCornerVectors(string bufferName,int kernelIndex)
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
            
            
            CreateComputeBuffer(sizeof(float) * 3, directions, bufferName, kernelIndex);
        }

        void GenerateCornerVectors2D(string bufferName,int kernelIndex)
        {
            Vector2[] directions = new Vector2[8];
            directions[0] = new Vector2(1, 1);
            directions[1] = new Vector2(-1, 1);
            directions[2] = new Vector2(1, -1);
            directions[3] = new Vector2(-1, -1);
            directions[4] = new Vector2(1, 0);
            directions[5] = new Vector2(-1, 0);
            directions[6] = new Vector2(0, 1);
            directions[7] = new Vector2(0, -1);

            CreateComputeBuffer(sizeof(float) * 2, directions, bufferName, kernelIndex);
        }

        //TODO consider separating worley-perlin generation and mixing them later
        public void Generate3DPerlinWorley(int dimensions, ref RenderTexture targetTexture, TEXTURE_CHANNEL channelToWriteTo, TEXTURE_TYPE type)
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

            int currKernel = computeShader.FindKernel("PerlinWorley3DTexture");

            //Worley
            GeneratePointsWorley(currSettings, currKernel);
            computeShader.SetInt("numCellsA", currSettings.numberOfCellsAxisA);
            computeShader.SetInt("numCellsB", currSettings.numberOfCellsAxisB);
            computeShader.SetInt("numCellsC", currSettings.numberOfCellsAxisC);
            computeShader.SetFloat("persistenceWorley", currSettings.persistence); //less than 1

            //Perlin
            GenerateCornerVectors("vecTable", currKernel);
            GeneratePermutationTable(256, perlinShapeSettings.seed, "permTable", currKernel);
            computeShader.SetInt("gridSize", perlinShapeSettings.gridSizePerlin);
            computeShader.SetInt("octaves", perlinShapeSettings.numOctavesPerlin);
            computeShader.SetFloat("persistencePerlin", perlinShapeSettings.persistencePerlin); //less than 1
            computeShader.SetFloat("lacunarity", perlinShapeSettings.lacunarityPerlin); //More than 1


            computeShader.SetTexture(currKernel, "Result3D", targetTexture);
            computeShader.SetInt("textureSizeP", dim);
            computeShader.SetVector("channelToWriteTo", GetChannelMask(channelToWriteTo));

            DispatchComputeShader(currKernel, new Vector3Int(dim, dim, dim));

        }

        //Weather Map Related =====================================================================
        private void GenerateWeatherMapChannel(CLOUD_PRESET preset, TEXTURE_CHANNEL channelToWriteTo, int kernelIndex, int dim, ref RenderTexture output, int seed)
        {
            WeatherMapChannelSettingsData data = Utility.GetWMChannelData(channelToWriteTo, preset);

            if (!data.activeChannel) //If channel isn't active just initialize to 0
                return;

            //Perlin -> Cloudmap Density
            GenerateCornerVectors2D("vecTableWMDensity", kernelIndex);
            GeneratePermutationTable(256, seed, "permTableWMDensity", kernelIndex);
            computeShader.SetInt("gridSizeWMDensity", data.perlinGridSize);
            computeShader.SetInt("octavesWMDensity", data.perlinOctaves);
            computeShader.SetInt("texDim", dim);
            computeShader.SetFloat("persistenceWMDensityPerlin", data.perlinPersistence); //less than 1
            computeShader.SetFloat("lacunarityWMDensityPerlin", data.perlinLacunarity); //More than 1

            //Worley
            computeShader.SetInt("numWorleyCellsWMDensityA", data.worleyNumCellsA);
            computeShader.SetInt("numWorleyCellsWMDensityB", data.worleyNumCellsB);
            computeShader.SetInt("numWorleyCellsWMDensityC", data.worleyNumCellsC);

            computeShader.SetFloat("persistenceWMDensityWorley", data.worleyPersistence); //less than 1
            System.Random random = new System.Random(seed);//TODO generalise for generate Permutation table (input random directly)
            GeneratePoints2D(random, data.worleyNumCellsA, "pointsWorleyWMDensityA", kernelIndex);
            GeneratePoints2D(random, data.worleyNumCellsB, "pointsWorleyWMDensityB", kernelIndex);
            GeneratePoints2D(random, data.worleyNumCellsC, "pointsWorleyWMDensityC", kernelIndex);


            computeShader.SetVector("channelMask", GetChannelMask(channelToWriteTo));
            computeShader.SetVector("minMaxBounds", data.minMaxBounds);
            computeShader.SetTexture(kernelIndex, "result", output);

            DispatchComputeShader(kernelIndex, new Vector3Int(dim, dim, 1));
            DeleteComputeBuffers();
        }

        public void GenerateWeatherMap(int resolution, ref RenderTexture output, int seed, CLOUD_PRESET preset)
        {
            int dim = Mathf.Max(resolution, 8);

            Utility.GenerateRenderTexture(resolution, ref output, TEXTURE_DIMENSIONS.TEX_2D);

            int kernelIndex = computeShader.FindKernel("GenerateWeatherMap");

            GenerateWeatherMapChannel(preset, TEXTURE_CHANNEL.R,kernelIndex, dim, ref output,  seed);
            GenerateWeatherMapChannel(preset, TEXTURE_CHANNEL.G,kernelIndex, dim, ref output,  seed + 1);
            GenerateWeatherMapChannel(preset, TEXTURE_CHANNEL.B,kernelIndex, dim, ref output,  seed + 2);

            //TODO this line will be used to model precipitation extra absorption or any extra info
            //GenerateWeatherMapChannel(preset, TEXTURE_CHANNEL.A, kernelIndex, dim, ref output, seed);
            output.GenerateMips();
        }


        public bool LerpWM(int resolution, float t)
        {
            if (originalWM.width != newWM.width || originalWM.height != newWM.height)
                return false;

            int dim = Mathf.Max(resolution, 8);
            int kernelIndex = computeShader.FindKernel("TextureLerp2D");
            computeShader.SetTexture(kernelIndex, "output", originalWM);
            computeShader.SetTexture(kernelIndex, "input", newWM);
            computeShader.SetFloat("t", t);
            DispatchComputeShader(kernelIndex, new Vector3Int(dim, dim, 1));
            originalWM.GenerateMips();
            DeleteComputeBuffers();
            return true;
        }

        //Noise Generation ========================================================================

        public void GenerateAllNoise()
        {
            GenerateBaseShapeNoise();
            GenerateDetailNoise();
        }

        public void GenerateBaseShapeNoise()
        {
            //Base Shape Texture
            Utility.GenerateRenderTexture(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_DIMENSIONS.TEX_3D);
            Generate3DPerlinWorley(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_CHANNEL.R, TEXTURE_TYPE.BASE_SHAPE);
            Generate3DWorley(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_CHANNEL.G, TEXTURE_TYPE.BASE_SHAPE);
            Generate3DWorley(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_CHANNEL.B, TEXTURE_TYPE.BASE_SHAPE);
            Generate3DWorley(baseShapeResolution, ref _baseShapeRenderTexture, TEXTURE_CHANNEL.A, TEXTURE_TYPE.BASE_SHAPE);
            _baseShapeRenderTexture.GenerateMips();
            DeleteComputeBuffers();
        }
        public void GenerateDetailNoise()
        {
            //Detail Texture
            Utility.GenerateRenderTexture(detailResolution, ref _detailRenderTexture, TEXTURE_DIMENSIONS.TEX_3D);
            Generate3DWorley(detailResolution, ref _detailRenderTexture, TEXTURE_CHANNEL.R, TEXTURE_TYPE.DETAIL);
            Generate3DWorley(detailResolution, ref _detailRenderTexture, TEXTURE_CHANNEL.G, TEXTURE_TYPE.DETAIL);
            Generate3DWorley(detailResolution, ref _detailRenderTexture, TEXTURE_CHANNEL.B, TEXTURE_TYPE.DETAIL);
            _detailRenderTexture.GenerateMips();
            DeleteComputeBuffers();
        }

        public void CleanUp()
        {
            DeleteComputeBuffers();
            Utility.ReleaseTexture(ref _baseShapeRenderTexture);
            Utility.ReleaseTexture(ref _detailRenderTexture);
            Utility.ReleaseTexture(ref originalWM);
            Utility.ReleaseTexture(ref newWM);

        }

    }
}