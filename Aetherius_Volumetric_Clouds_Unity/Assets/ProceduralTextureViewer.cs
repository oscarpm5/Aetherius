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
    public ComputeShader computeShader= null;
    public bool updateTextureAuto = false;
    [Range(1,100)]
    public int numberOfCells = 1;
    public int resolution = 256;
    private RenderTexture _renderTexture3D = null;

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
        Generate3DWorley(resolution, ref _renderTexture3D);
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

    public void Generate3DWorley(int dimensions,ref RenderTexture targetTexture)
    {
        int dim = Mathf.Max(dimensions, 8);
        if (computeShader == null)
            return;

        int currKernel = computeShader.FindKernel("Worley3DTexture");
        computeShader.SetTexture(currKernel, "Result3D", targetTexture);
        computeShader.SetFloat("textureSizeP", dim);
        computeShader.SetInt("numCells", numberOfCells);

        computeShader.Dispatch(currKernel, dim / 8, dim / 8, dim / 8); //Image size divided by the thread size of each group
    }


    void GeneratePointsWorley(WorleyData data)
    {
        System.Random random = new System.Random(data.seed);



    }

    void CreateComputeBuffer(int dataStride, System.Array data, string bufferName, int kernel)
    {
        ComputeBuffer newBuffer = new ComputeBuffer(data.Length, dataStride, ComputeBufferType.Structured);

    }

    void GenerateTexture3D(int texResolution, ref RenderTexture myTexture)
    {
        int res = Mathf.Max(texResolution, 8);
        if (myTexture == null || myTexture.height != res || myTexture.width!= res || myTexture.volumeDepth != res) //if texture doesnt exist or resolution has changed, recreate the texture
        {
            if(myTexture!=null)
            {
                myTexture.Release();
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

}

