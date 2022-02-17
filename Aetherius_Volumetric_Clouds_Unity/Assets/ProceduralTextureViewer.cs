using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorleyData
{
    public int seed;

    public int numberOfCellsA;
    public int numberOfCellsB;
    public int numberOfCellsC;
}

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class ProceduralTextureViewer : MonoBehaviour
{
    //Compute shader
    [Header("Texture Generation")]
    public ComputeShader computeShader = null;
    public bool updateTextureAuto = false;
    [Range(1, 100)]
    public int numberOfCellsOnAxisA = 1;
    [Range(1, 100)]
    public int numberOfCellsOnAxisB = 1;
    [Range(1, 100)]
    public int numberOfCellsOnAxisC = 1;
    [Range(8, 1024)]
    public int resolution = 256;
    private RenderTexture _renderTexture3D = null;

    List<ComputeBuffer> buffersToDelete;

    //Display shader
    [Header("Texture Display")]
    public Shader displayPreviewShader;
    public bool displayTexture = false;
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

    public void UpdateNoise()
    {
        GenerateTexture3D(resolution, ref _renderTexture3D);
        //Generate3DWorley(resolution, ref _renderTexture3D);
        Generate3DPerlin(resolution, ref _renderTexture3D);
        DeleteComputeBuffers();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (updateTextureAuto == true)
        {
            UpdateNoise();
        }

        if (material == null || !displayTexture)
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetTexture("_DisplayTex3D", _renderTexture3D); //input the procedural texture
        material.SetFloat("slice3DTex", textureSlice);
        material.SetFloat("debugTextureSize", debugDisplaySize);
        material.SetFloat("tileAmmount", tileAmmount);
        Graphics.Blit(source, destination, material);
    }

    public void Generate3DWorley(int dimensions, ref RenderTexture targetTexture)
    {
        int dim = Mathf.Max(dimensions, 8);
        if (computeShader == null)
            return;

        GeneratePointsWorley();
        int currKernel = computeShader.FindKernel("Worley3DTextureWithPoints");
        computeShader.SetTexture(currKernel, "Result3D", targetTexture);
        computeShader.SetInt("textureSizeP", dim);
        computeShader.SetInt("numCellsA", numberOfCellsOnAxisA);
        computeShader.SetInt("numCellsB", numberOfCellsOnAxisB);
        computeShader.SetInt("numCellsC", numberOfCellsOnAxisC);

        computeShader.Dispatch(currKernel, dim / 8, dim / 8, dim / 8); //Image size divided by the thread size of each group
    }


    void GeneratePointsWorley() //TODO retrieve data from Worley Data not for number of Cells
    {
        System.Random random = new System.Random(0);

        GeneratePoints(random, numberOfCellsOnAxisA, "pointsA", "Worley3DTextureWithPoints");
        GeneratePoints(random, numberOfCellsOnAxisB, "pointsB", "Worley3DTextureWithPoints");
        GeneratePoints(random, numberOfCellsOnAxisC, "pointsC", "Worley3DTextureWithPoints");
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

    void CreateComputeBuffer(int dataStride, System.Array data, string bufferName, string kernelName)
    {
        ComputeBuffer newBuffer = new ComputeBuffer(data.Length, dataStride, ComputeBufferType.Structured);
        newBuffer.SetData(data);
        computeShader.SetBuffer(computeShader.FindKernel(kernelName), bufferName, newBuffer);

        if (buffersToDelete == null)
            buffersToDelete = new List<ComputeBuffer>();

        buffersToDelete.Add(newBuffer);
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

    void GenerateTexture3D(int texResolution, ref RenderTexture myTexture)
    {
        int res = Mathf.Max(texResolution, 8);
        if (myTexture == null || myTexture.height != res || myTexture.width != res || myTexture.volumeDepth != res) //if texture doesnt exist or resolution has changed, recreate the texture
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
            myTexture.filterMode = FilterMode.Point;
            myTexture.wrapMode = TextureWrapMode.Repeat;
            myTexture.Create();
        }
    }

    private void OnEnable() //Called after component awakens and after a hot reload
    {
        UpdateNoise();
    }

    private void OnDisable() //happens before a hot reload
    {
        DeleteComputeBuffers();

        if (_renderTexture3D != null)
        {
            _renderTexture3D.Release();
            _renderTexture3D = null;
        }
    }

    void GeneratePermutationTable(int size, int seed, string bufferName)
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

        CreateComputeBuffer(sizeof(int), permutation, bufferName, "Perlin3DTexture");
    }

    void GenerateCornerVectors()
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

        CreateComputeBuffer(sizeof(float) * 3, directions, "vecTable", "Perlin3DTexture");
    }
    void Generate3DPerlin(int dimensions, ref RenderTexture targetTexture)
    {
        int dim = Mathf.Max(dimensions, 8);
        if (computeShader == null)
            return;

        GenerateCornerVectors();
        GeneratePermutationTable(256, 0, "permTable");
        computeShader.SetInt("gridSize", gridSizePerlin);
        computeShader.SetInt("octaves", numOctavesPerlin);
        computeShader.SetFloat("persistence", persistencePerlin); //less than 1
        computeShader.SetFloat("lacunarity", lacunarityPerlin); //More than 1
        int currKernel = computeShader.FindKernel("Perlin3DTexture");
        computeShader.SetTexture(currKernel, "Result3D", targetTexture);
        computeShader.SetInt("textureSizeP", dim);


        computeShader.Dispatch(currKernel, dim / 8, dim / 8, dim / 8); //Image size divided by the thread size of each group
    }

}

