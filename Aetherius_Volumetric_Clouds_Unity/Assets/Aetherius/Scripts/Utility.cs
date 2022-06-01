using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    public enum CLOUD_CONTROL
    {
        SIMPLE,
        ADVANCED
    }

    public enum CLOUD_PRESET
    {
        SPARSE,
        CLOUDY,
        STORMY,
        OVERCAST
    }

    public enum CLOUD_RESOLUTION
    {
        ORIGINAL,
        HALF,
        QUARTER
    }
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




    [System.Serializable]
    public class CloudShape //TODO convert to serializable object?
    {
        public float baseShapeSize = 10000.0f; //Advanced
        public float detailSize = 1500.0f; //Advanced
        public float globalCoverage = 0.5f; //Advanced
        public float globalDensity = 0.2f; //Advanced
        public float weatherMapSize = 36000.0f; //Advanced
    }

    public class Utility
    {
        public static float Remap(float v, float minOrigin, float maxOrigin, float minTarget, float maxTarget)
        {
            return minTarget + (((v - minOrigin) / (maxOrigin - minOrigin)) * (maxTarget - minTarget));
        }
        public static float RemapClamp(float v, float minOrigin, float maxOrigin, float minTarget, float maxTarget)
        {
            return Mathf.Clamp(minTarget + (((v - minOrigin) / (maxOrigin - minOrigin)) * (maxTarget - minTarget)),minTarget,maxTarget);
        }


        public static ComputeBuffer CreateComputeBuffer(ref List<ComputeBuffer> toDeleteList, int dataStride, System.Array data)
        {
            ComputeBuffer newBuffer = new ComputeBuffer(data.Length, dataStride, ComputeBufferType.Structured);
            newBuffer.SetData(data);

            if (toDeleteList == null)
                toDeleteList = new List<ComputeBuffer>();

            toDeleteList.Add(newBuffer);
            return newBuffer;
        }

        public static ComputeBuffer CreateComputeBuffer(ref List<ComputeBuffer> toDeleteList, ref ComputeShader compShader, int dataStride, System.Array data, string bufferName, int kernelIndex)
        {
            ComputeBuffer newBuffer = new ComputeBuffer(data.Length, dataStride, ComputeBufferType.Structured);
            newBuffer.SetData(data);
            compShader.SetBuffer(kernelIndex, bufferName, newBuffer);

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

        public static bool DispatchComputeShader(ref ComputeShader toDispatch, int kernelIndex, Vector3Int textureDimensions)
        {
            if (toDispatch == null)
                return false;

            uint[] kernelGroupSizes = new uint[3];
            toDispatch.GetKernelThreadGroupSizes(kernelIndex, out kernelGroupSizes[0], out kernelGroupSizes[1], out kernelGroupSizes[2]);
            toDispatch.Dispatch(kernelIndex, textureDimensions.x / (int)kernelGroupSizes[0], textureDimensions.y / (int)kernelGroupSizes[1], textureDimensions.z / (int)kernelGroupSizes[2]); //Image size divided by the thread size of each group

            return true;
        }

        public static bool GenerateRenderTexture(int texResolution, ref RenderTexture myTexture, TEXTURE_DIMENSIONS dimensions, UnityEngine.Experimental.Rendering.GraphicsFormat format = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, FilterMode filterMode = FilterMode.Trilinear)
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
                myTexture.useMipMap = true;
                myTexture.autoGenerateMips = false;
                myTexture.wrapMode = TextureWrapMode.Repeat;
                myTexture.graphicsFormat = format;
                myTexture.Create();
                //myTexture.GenerateMips();

                isNewlyCreated = true;

            }
            return isNewlyCreated;
        }
        public static void ReleaseTexture(ref RenderTexture textureToRelease)
        {
            if (textureToRelease != null)
            {
                textureToRelease.Release();
                textureToRelease = null;
            }
        }

        //TODO replace for Scriptable Object Assets
        public static WeatherMapChannelSettingsData GetWMChannelData(TEXTURE_CHANNEL channel, CLOUD_PRESET preset) //TODO make this an external file?
        {
            WeatherMapChannelSettingsData ret = new WeatherMapChannelSettingsData();

            switch (preset)
            {
                case CLOUD_PRESET.SPARSE:
                    {
                        switch (channel)
                        {
                            case TEXTURE_CHANNEL.R:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 17;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 3;
                                    ret.worleyNumCellsB = 5;
                                    ret.worleyNumCellsC = 7;
                                    ret.worleyPersistence = 0.4f;

                                    //General
                                    ret.minMaxBounds = new Vector2(-1.5f, 1.1f);
                                    ret.activeChannel = true;
                                }
                                break;
                            case TEXTURE_CHANNEL.G:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 13;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 3;
                                    ret.worleyNumCellsB = 4;
                                    ret.worleyNumCellsC = 5;
                                    ret.worleyPersistence = 0.3f;

                                    //General
                                    ret.minMaxBounds = new Vector2(-1.5f, 1.0f);
                                    ret.activeChannel = true;
                                }
                                break;
                            case TEXTURE_CHANNEL.B:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 9;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 3;
                                    ret.worleyNumCellsB = 5;
                                    ret.worleyNumCellsC = 9;
                                    ret.worleyPersistence = 0.4f;

                                    //General
                                    ret.minMaxBounds = new Vector2(0.0f, 1.0f);
                                    ret.activeChannel = false;
                                }
                                break;
                            case TEXTURE_CHANNEL.A:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 23;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 5;
                                    ret.worleyNumCellsB = 11;
                                    ret.worleyNumCellsC = 19;
                                    ret.worleyPersistence = 0.5f;
                                    ret.activeChannel = false;
                                }
                                break;
                        }
                    }
                    break;
                case CLOUD_PRESET.CLOUDY:
                    {
                        switch (channel)
                        {
                            case TEXTURE_CHANNEL.R:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 23;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 7;
                                    ret.worleyNumCellsB = 11;
                                    ret.worleyNumCellsC = 27;
                                    ret.worleyPersistence = 0.5f;

                                    //General
                                    ret.minMaxBounds = new Vector2(-0.5f, 1.1f);
                                    ret.activeChannel = true;
                                }
                                break;
                            case TEXTURE_CHANNEL.G:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 13;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 3;
                                    ret.worleyNumCellsB = 5;
                                    ret.worleyNumCellsC = 7;
                                    ret.worleyPersistence = 0.5f;

                                    //General
                                    ret.minMaxBounds = new Vector2(-0.5f, 1.0f);
                                    ret.activeChannel = true;
                                }
                                break;
                            case TEXTURE_CHANNEL.B:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 9;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 3;
                                    ret.worleyNumCellsB = 5;
                                    ret.worleyNumCellsC = 9;
                                    ret.worleyPersistence = 0.4f;

                                    //General
                                    ret.minMaxBounds = new Vector2(0.0f, 1.0f);
                                    ret.activeChannel = false;
                                }
                                break;
                            case TEXTURE_CHANNEL.A:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 23;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 5;
                                    ret.worleyNumCellsB = 11;
                                    ret.worleyNumCellsC = 19;
                                    ret.worleyPersistence = 0.5f;
                                    ret.activeChannel = false;
                                }
                                break;
                        }
                    }
                    break;
                case CLOUD_PRESET.STORMY:
                    {
                        switch (channel)
                        {
                            case TEXTURE_CHANNEL.R:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 17;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 3;
                                    ret.worleyNumCellsB = 6;
                                    ret.worleyNumCellsC = 7;
                                    ret.worleyPersistence = 0.3f;

                                    //General
                                    ret.minMaxBounds = new Vector2(-0.5f, 1.1f);
                                    ret.activeChannel = true;
                                }
                                break;
                            case TEXTURE_CHANNEL.G:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 17;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 3;
                                    ret.worleyNumCellsB = 5;
                                    ret.worleyNumCellsC = 7;
                                    ret.worleyPersistence = 0.3f;

                                    //General
                                    ret.minMaxBounds = new Vector2(-1.5f, 1.5f);
                                    ret.activeChannel = true;
                                }
                                break;
                            case TEXTURE_CHANNEL.B:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 13;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 3;
                                    ret.worleyNumCellsB = 4;
                                    ret.worleyNumCellsC = 6;
                                    ret.worleyPersistence = 0.3f;

                                    //General
                                    ret.minMaxBounds = new Vector2(-0.75f, 1.5f);
                                    ret.activeChannel = true;
                                }
                                break;
                            case TEXTURE_CHANNEL.A:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 23;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 5;
                                    ret.worleyNumCellsB = 11;
                                    ret.worleyNumCellsC = 19;
                                    ret.worleyPersistence = 0.5f;
                                    ret.activeChannel = true;
                                }
                                break;
                        }
                    }
                    break;
                case CLOUD_PRESET.OVERCAST:
                    {
                        switch (channel)
                        {
                            case TEXTURE_CHANNEL.R:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 23;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 7;
                                    ret.worleyNumCellsB = 11;
                                    ret.worleyNumCellsC = 27;
                                    ret.worleyPersistence = 0.5f;

                                    //General
                                    ret.minMaxBounds = new Vector2(0.75f, 1.0f);
                                    ret.activeChannel = true;
                                }
                                break;
                            case TEXTURE_CHANNEL.G:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 13;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 3;
                                    ret.worleyNumCellsB = 5;
                                    ret.worleyNumCellsC = 7;
                                    ret.worleyPersistence = 0.5f;

                                    //General
                                    ret.minMaxBounds = new Vector2(-0.5f, 1.0f);
                                    ret.activeChannel = true;
                                }
                                break;
                            case TEXTURE_CHANNEL.B:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 13;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 3;
                                    ret.worleyNumCellsB = 4;
                                    ret.worleyNumCellsC = 6;
                                    ret.worleyPersistence = 0.3f;

                                    //General
                                    ret.minMaxBounds = new Vector2(-1.0f, 1.0f);
                                    ret.activeChannel = true;
                                }
                                break;
                            case TEXTURE_CHANNEL.A:
                                {
                                    //Perlin Related
                                    ret.perlinGridSize = 23;
                                    ret.perlinOctaves = 4;
                                    ret.perlinPersistence = 0.5f;
                                    ret.perlinLacunarity = 2.0f;

                                    //Worley Related
                                    ret.worleyNumCellsA = 5;
                                    ret.worleyNumCellsB = 11;
                                    ret.worleyNumCellsC = 19;
                                    ret.worleyPersistence = 0.5f;
                                    ret.activeChannel = true;
                                }
                                break;
                        }
                    }
                    break;
            }



            return ret;
        }


    }

}
