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
            GenerateTexture();
        }

        if (material == null || !displayTexture)
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetTexture("_DisplayTex", renderTexture); //input the procedural texture
        material.SetFloat("debugTextureSize", debugDisplaySize);

        Graphics.Blit(source, destination,material);
    }

    private void Start()
    {
        renderTexture = null;
        _material = null;
        GenerateTexture();
    }


    public void GenerateTexture()
    {

        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(256, 256, 24);
            renderTexture.enableRandomWrite = true;//So it can be used by the compute shader
            renderTexture.Create();
        }

        if (computeShader == null)
            return;

        int currKernel = computeShader.FindKernel("CSMain");
        computeShader.SetTexture(currKernel, "Result", renderTexture);
        computeShader.Dispatch(currKernel, renderTexture.width / 8, renderTexture.height / 8, 1); //Image size divided by the thread size of each group

    }

}
