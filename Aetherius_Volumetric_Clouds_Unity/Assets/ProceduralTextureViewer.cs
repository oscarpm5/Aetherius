using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
//[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class ProceduralTextureViewer : MonoBehaviour
{
    public enum TEXTURE_CHANNEL
    {
        R,
        G,
        B,
        A
    }

    public TEXTURE_CHANNEL displayChannel;

    //Compute shader
    [Header("Texture Generation")]
    public ComputeShader computeShader = null;
    public bool updateTextureAuto = false;
    private bool _updateNoise;
    [Range(8, 1024)]
    public int resolution = 256;
    private RenderTexture _renderTexture3D = null;
    public WorleySettings[] worleyShapeSettings = new WorleySettings[4];
    List<ComputeBuffer> buffersToDelete;

    //Display shader
    [Header("Texture Display")]
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
    [Range(1, 12)]
    public int numOctavesPerlin = 5;
    [Range(0.0f, 1.0f)]
    public float persistencePerlin = 0.5f;
    [Range(1.0f, 10.0f)]
    public float lacunarityPerlin = 3.0f;
    [Range(1, 32)]
    public int gridSizePerlin = 32;

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
    private Material _material;

    public Vector4 channelMask
    {
        get
        {
            return GetChannelMask(displayChannel);
        }
    }

    public WorleySettings activeWorleyShapeSettings
    {
        get
        {
            return worleyShapeSettings[(int)displayChannel];
        }
    }


    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {

        UpdateNoise();


        if (material == null || !displayTexture)
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetTexture("_DisplayTex3D", _renderTexture3D); //input the procedural texture
        material.SetFloat("slice3DTex", textureSlice);
        material.SetFloat("debugTextureSize", debugDisplaySize);
        material.SetFloat("tileAmmount", tileAmmount);
        material.SetVector("channelMask", channelMask);
        material.SetInt("displayGrayscale", displayGrayscale ? 1 : 0);
        material.SetInt("displayAllChannels", displayAllChannels ? 1 : 0);

        Graphics.Blit(source, destination, material);
    }
    private void OnEnable() //Called after component awakens and after a hot reload
    {
        GenerateAllNoise();
    }
    private void OnDisable() //happens before a hot reload
    {
        DeleteComputeBuffers();
        ReleaseTexture(ref _renderTexture3D);
    }
    Vector4 GetChannelMask(TEXTURE_CHANNEL channel)
    {
        Vector4 ret = new Vector4();
        ret.x = (channel == TEXTURE_CHANNEL.R) ? 1 : 0;
        ret.y = (channel == TEXTURE_CHANNEL.G) ? 1 : 0;
        ret.z = (channel == TEXTURE_CHANNEL.B) ? 1 : 0;
        ret.w = (channel == TEXTURE_CHANNEL.A) ? 1 : 0;
        return ret;
    }
    public void UpdateNoise()
    {
        //TODO handle detail too here

        bool hasGeneratedNewTexture = GenerateTexture3D(resolution, ref _renderTexture3D);
        if (_updateNoise == true || (hasGeneratedNewTexture&&updateTextureAuto))
        {
            _updateNoise = false;

            if (displayChannel == TEXTURE_CHANNEL.R)
                Generate3DPerlinWorley(resolution, ref _renderTexture3D, TEXTURE_CHANNEL.R);
            else
                Generate3DWorley(resolution, ref _renderTexture3D, displayChannel);

            DeleteComputeBuffers();
        }
    }

    public void GenerateAllNoise()
    {
        _updateNoise = false;
        GenerateTexture3D(resolution, ref _renderTexture3D);
        Generate3DPerlinWorley(resolution, ref _renderTexture3D, TEXTURE_CHANNEL.R);
        Generate3DWorley(resolution, ref _renderTexture3D, TEXTURE_CHANNEL.G);
        Generate3DWorley(resolution, ref _renderTexture3D, TEXTURE_CHANNEL.B);
        Generate3DWorley(resolution, ref _renderTexture3D, TEXTURE_CHANNEL.A);
        //TODO generate detail too here
        
        DeleteComputeBuffers();

    }

    //Generation methods ======================================================================
    public void Generate3DWorley(int dimensions, ref RenderTexture targetTexture, TEXTURE_CHANNEL channelToWriteTo)
    {
        int dim = Mathf.Max(dimensions, 8);
        if (computeShader == null)
            return;

        WorleySettings currSettings = worleyShapeSettings[(int)channelToWriteTo];
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
    void Generate3DPerlinWorley(int dimensions, ref RenderTexture targetTexture, TEXTURE_CHANNEL channelToWriteTo)
    {
        int dim = Mathf.Max(dimensions, 8);
        if (computeShader == null)
            return;

        WorleySettings currSettings = worleyShapeSettings[(int)channelToWriteTo];
        if (currSettings == null)
        {
            Debug.LogWarning("Assign a Worley Settings Scriptable Object to the [" + ((int)channelToWriteTo).ToString() + "] element of the 'Worley Settings' array");
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
        GeneratePermutationTable(256, 0, "permTable", kernelName);
        computeShader.SetInt("gridSize", gridSizePerlin);
        computeShader.SetInt("octaves", numOctavesPerlin);
        computeShader.SetFloat("persistencePerlin", persistencePerlin); //less than 1
        computeShader.SetFloat("lacunarity", lacunarityPerlin); //More than 1


        computeShader.SetTexture(currKernel, "Result3D", targetTexture);
        computeShader.SetInt("textureSizeP", dim);
        computeShader.SetVector("channelToWriteTo", GetChannelMask(channelToWriteTo));  

        computeShader.Dispatch(currKernel, dim / 8, dim / 8, dim / 8); //Image size divided by the thread size of each group
       

    }

    void ClearToBlack3DTexture(int dimensions, ref RenderTexture targetTexture)
    {
        int dim = Mathf.Max(dimensions, 8);
        if (computeShader == null)
            return;

        int currKernel = computeShader.FindKernel("InitializeTexture");
        computeShader.SetTexture(currKernel, "Result3D", targetTexture);
        computeShader.Dispatch(currKernel, dim / 8, dim / 8, dim / 8); //Image size divided by the thread size of each group
    }

    //Worley Related ==========================================================================
    void GeneratePointsWorley(WorleySettings currSettings, string kernelName) //TODO retrieve data from Worley Data not for number of Cells
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

        CreateComputeBuffer(sizeof(float) * 3, points, bufferName, kernelName);
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

        CreateComputeBuffer(sizeof(int), permutation, bufferName, kernelName);
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

        CreateComputeBuffer(sizeof(float) * 3, directions, "vecTable", kernelName);
    }

    //Compute Buffers =========================================================================
    ComputeBuffer CreateComputeBuffer(int dataStride, System.Array data, string bufferName, string kernelName)
    {
        ComputeBuffer newBuffer = new ComputeBuffer(data.Length, dataStride, ComputeBufferType.Structured);
        newBuffer.SetData(data);
        computeShader.SetBuffer(computeShader.FindKernel(kernelName), bufferName, newBuffer);

        if (buffersToDelete == null)
            buffersToDelete = new List<ComputeBuffer>();

        buffersToDelete.Add(newBuffer);
        return newBuffer;
    }

    void DeleteComputeBuffers()
    {
        if (buffersToDelete == null)
            return;

        foreach (ComputeBuffer currentBuffer in buffersToDelete)
        {
            currentBuffer.Release();
        }
        buffersToDelete = null;
    }

    //Textures ================================================================================
    //returns ture if a texture has been created
    bool GenerateTexture3D(int texResolution, ref RenderTexture myTexture)
    {
        bool isNewlyCreated = false;
        UnityEngine.Experimental.Rendering.GraphicsFormat format = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
        int res = Mathf.Max(texResolution, 8);
        if (myTexture == null || !myTexture.IsCreated() || myTexture.height != res || myTexture.width != res || myTexture.volumeDepth != res || myTexture.graphicsFormat != format) //if texture doesnt exist or resolution has changed, recreate the texture
        {
            Debug.Log("GeneratingTexture...");

            if (myTexture != null)
            {
                Debug.Log("Deleting ancient texture...");
                myTexture.Release();
                myTexture = null;
            }

            myTexture = new RenderTexture(res, res, 0);
            myTexture.enableRandomWrite = true;//So it can be used by the compute shader
            myTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            myTexture.volumeDepth = res;
            myTexture.filterMode = FilterMode.Trilinear;
            myTexture.wrapMode = TextureWrapMode.Repeat;
            myTexture.graphicsFormat = format;
            myTexture.Create();

            isNewlyCreated = true;

        }
        return isNewlyCreated;
    }
    void ReleaseTexture(ref RenderTexture textureToRelease)
    {
        if (textureToRelease != null)
        {
            textureToRelease.Release();
            textureToRelease = null;
        }
    }



    public void NoiseSettingsChanged()
    {
        if (updateTextureAuto)
        {
            _updateNoise = true;
            Debug.Log("Settings have changed!");
        }
    }

    public void UpdateNoiseManually()
    {
        GenerateAllNoise();
    }
}

