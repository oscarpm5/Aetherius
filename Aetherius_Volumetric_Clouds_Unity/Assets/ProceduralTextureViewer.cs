using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class ProceduralTextureViewer : MonoBehaviour
{
    public bool updateTextureAuto = false;

    //Compute shader
    public ComputeShader computeShader;
    public RenderTexture renderTexture2D = null;
    public RenderTexture renderTexture3D = null;

    //Display shader
    public Shader displayPreviewShader;
    private Material _material;
    public bool displayTexture = false;
    [Range(0.0f, 1.0f)]
    public float debugDisplaySize = 0.5f;
    [Range(1, 5)]
    public float tileAmmount = 1;
    public int numberOfCells = 1;
    [Range(0.0f,1.0f)]
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


    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (updateTextureAuto)
        {
            GenerateTexture(256);
        }

        if (material == null || !displayTexture)
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetTexture("_DisplayTex2D", renderTexture2D); //input the procedural texture
        material.SetTexture("_DisplayTex3D", renderTexture3D); //input the procedural texture
        material.SetFloat("slice3DTex", textureSlice);
        material.SetFloat("debugTextureSize", debugDisplaySize);
        material.SetFloat("tileAmmount", tileAmmount);
        Graphics.Blit(source, destination, material);
    }

    private void Start()
    {
        renderTexture2D = null;
        renderTexture3D = null;
        _material = null;
        GenerateTexture(256);
    }


    public void GenerateTexture(int dimensions)
    {
        Generate3DWorley(dimensions);
    }

    public void Generate2DWorley(int dimensions)
    {
        if (renderTexture2D == null)
        {
            renderTexture2D = new RenderTexture(dimensions, dimensions, 24);
            renderTexture2D.enableRandomWrite = true;//So it can be used by the compute shader
            renderTexture2D.Create();
        }

        if (computeShader == null)
            return;


        int currKernel = computeShader.FindKernel("Worley2DTexture");
        computeShader.SetTexture(currKernel, "Result2D", renderTexture2D);
        computeShader.SetFloat("textureSizeP", dimensions);

        computeShader.SetInt("numCells", numberOfCells);

        computeShader.Dispatch(currKernel, dimensions / 8, dimensions / 8, 1); //Image size divided by the thread size of each group
    }


    public void Generate3DWorley(int dimensions)
    {
        if (renderTexture3D == null)
        {
            renderTexture3D = new RenderTexture(dimensions, dimensions, 0);
            renderTexture3D.enableRandomWrite = true;//So it can be used by the compute shader
            renderTexture3D.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            renderTexture3D.volumeDepth = dimensions;
            renderTexture3D.Create();
        }

        if (computeShader == null)
            return;

        int currKernel = computeShader.FindKernel("Worley3DTexture");
        computeShader.SetTexture(currKernel, "Result3D", renderTexture3D);
        computeShader.SetFloat("textureSizeP", dimensions);

        computeShader.SetInt("numCells", numberOfCells);

        computeShader.Dispatch(currKernel, dimensions / 8, dimensions / 8, dimensions / 8); //Image size divided by the thread size of each group
    }
}

