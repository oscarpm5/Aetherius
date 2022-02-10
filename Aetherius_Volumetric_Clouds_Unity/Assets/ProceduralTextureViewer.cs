using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class ProceduralTextureViewer : MonoBehaviour
{
    //Compute shader
    [SerializeField]
    private ComputeShader _computeShader;
    [SerializeField]
    private RenderTexture _renderTexture;

    //Display shader
    [SerializeField]
    private Shader _displayPreviewShader;
    private Material _material;
    [SerializeField]
    private bool _displayTexture = false;

    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float _debugDisplaySize = 0.5f;

    public Material displayMaterial
    {
        get
        {
            if (!_material && _displayPreviewShader)
            {
                _material = new Material(_displayPreviewShader);
                _material.hideFlags = HideFlags.HideAndDontSave;
            }
            return _material;
        }
    }


    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_renderTexture == null)
        {
            _renderTexture = new RenderTexture(256, 256, 24);
            _renderTexture.enableRandomWrite = true;//So it can be used by the compute shader
            _renderTexture.Create();
        }

        if (_computeShader == null)
            return;

        _computeShader.SetTexture(0, "Result", _renderTexture);
        _computeShader.Dispatch(0, _renderTexture.width / 8, _renderTexture.height / 8, 1); //Image size divided by the thread size of each group



        if (displayMaterial == null || !_displayTexture)
        {
            Graphics.Blit(source, destination);
            return;
        }

        displayMaterial.SetTexture("_DisplayTex", _renderTexture); //input the procedural texture
        displayMaterial.SetFloat("debugTextureSize", _debugDisplaySize);

        Graphics.Blit(source, destination,displayMaterial);
    }

}
