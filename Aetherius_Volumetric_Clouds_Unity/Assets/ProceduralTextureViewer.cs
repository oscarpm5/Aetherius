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
    public RenderTexture renderTexture=null;

    //Display shader
    public Shader displayPreviewShader;
    private Material _material;
    public bool displayTexture = false;
    [Range(0.0f, 1.0f)]
    public float debugDisplaySize = 0.5f;
    [Range(1, 5)]
    public float tileAmmount = 1;
    public Vector2 numberOfCells = Vector2.one;

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
        if(updateTextureAuto)
        {
            GenerateTexture(256);
        }

        if (material == null || !displayTexture)
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetTexture("_DisplayTex", renderTexture); //input the procedural texture
        material.SetFloat("debugTextureSize", debugDisplaySize);
        material.SetFloat("tileAmmount", tileAmmount);
        Graphics.Blit(source, destination,material);
    }

    private void Start()
    {
        renderTexture = null;
        _material = null;
        GenerateTexture(256);
    }


    public void GenerateTexture(int dimensions)
    {
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(dimensions, dimensions, 24);
            renderTexture.enableRandomWrite = true;//So it can be used by the compute shader
            //renderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            //renderTexture.volumeDepth = dimensions;
            renderTexture.Create();
        }

        if (computeShader == null)
            return;

        int currKernel = computeShader.FindKernel("CSMain");
        computeShader.SetTexture(currKernel, "Result", renderTexture);
        computeShader.SetFloat("textureSizeP", dimensions);
      
        float []numCells = new float[2];
        numCells[0] = numberOfCells.x;
        numCells[1] = numberOfCells.y;
        computeShader.SetFloats("numCells", numCells);

        //computeShader.Dispatch(currKernel, dimensions / 8, dimensions / 8, dimensions / 8); //Image size divided by the thread size of each group
        computeShader.Dispatch(currKernel, dimensions / 8, dimensions / 8,1); //Image size divided by the thread size of each group

    }

}
