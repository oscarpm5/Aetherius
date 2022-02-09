using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class RaymarchCamera : MonoBehaviour
{
    private Camera _cam;

    [SerializeField]
    private Shader _shader;
    private Material _material;

    public Material _rayMarchMaterial
    {
        get
        {
            if (!_material && _shader)
            {
                _material = new Material(_shader);
                _material.hideFlags = HideFlags.HideAndDontSave;
            }
            return _material;
        }
    }

    public Camera _camera
    {
        get //TODO will this work with the editor cam?
        {
            if (!_cam)
            {
                _cam = GetComponent<Camera>();
            }
            return _cam;
        }

    }


    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_rayMarchMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        _rayMarchMaterial.SetMatrix("_CamFrustum", CamFrustrumFromCam(_camera));
        _rayMarchMaterial.SetMatrix("_CamToWorldMat", _camera.cameraToWorldMatrix);


        //Create a screen quad
        RenderTexture.active = destination;
        GL.PushMatrix();
        GL.LoadOrtho();
        _rayMarchMaterial.SetPass(0);
        GL.Begin(GL.QUADS);
        //Bottom Left
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 3.0f);
        //Bottom Right
        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 2.0f);
        //Top Right
        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 1.0f);
        //Top Left
        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f);

        GL.End();
        GL.PopMatrix();
    }


    private Matrix4x4 CamFrustrumFromCam(Camera cam)
    {
        Matrix4x4 frustum = Matrix4x4.identity;
        float foV = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);

        Vector3 goUp = Vector3.up * foV;
        Vector3 goRight = Vector3.right * foV * cam.aspect;


        Vector3 TL = (-Vector3.forward - goRight + goUp); //TOP LEFT CORNER
        Vector3 TR = (-Vector3.forward + goRight + goUp); //TOP RIGHT CORNER
        Vector3 BR = (-Vector3.forward + goRight - goUp); //BOTTOM RIGHT CORNER
        Vector3 BL = (-Vector3.forward - goRight - goUp); //BOTTOM LEFT CORNER

        frustum.SetRow(0, TL);
        frustum.SetRow(1, TR);
        frustum.SetRow(2, BR);
        frustum.SetRow(3, BL);

        return frustum;
    }
}
