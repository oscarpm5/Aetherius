using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode] [ImageEffectAllowedInSceneView]
public class RaymarchCamera : MonoBehaviour
{
    [SerializeField]
    private Shader _shader;

    Material _material;



    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_material == null)
            _material = new Material(_shader);


        Graphics.Blit(source, destination, _material);
    }

}
